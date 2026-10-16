using System;
using System.Collections.Generic;

namespace Crashverse.VFX
{
    /// <summary>Material ids, matching the projectile Animator `Material` int (animation/02).</summary>
    public enum VfxMaterial { Wood = 0, Brick = 1, Steel = 2, SteelJoint = 3, Terrain = 4, Explosive = 5 }

    /// <summary>One impact/dust recipe row (VFX_Shader_Spec_v1 §5). Pure data, tier/motion scaling in VfxRules.</summary>
    public struct ImpactRecipe
    {
        public int Particles; public float LifetimeSec; public int FlashFrames; public uint ColorA, ColorB;
        public ImpactRecipe(int p, float life, int flash, uint a, uint b) { Particles = p; LifetimeSec = life; FlashFrames = flash; ColorA = a; ColorB = b; }
    }

    /// <summary>
    /// Pure VFX rules (no UnityEngine), so they can be unit-tested like FeelCore. Values come from animation/02 + 03 and
    /// the Reduce motion table in animation/07. VFX never change gameplay.
    /// </summary>
    public static class VfxRules
    {
        public static ImpactRecipe Impact(VfxMaterial m)
        {
            switch (m)
            {
                case VfxMaterial.Wood: return new ImpactRecipe(12, 0.40f, 2, 0xF5C994, 0xCFA779);
                case VfxMaterial.Brick: return new ImpactRecipe(16, 0.60f, 2, 0xC0392B, 0xD9A37E);
                case VfxMaterial.Steel: return new ImpactRecipe(10, 0.30f, 2, 0xFFD27A, 0x3A3F48);
                case VfxMaterial.SteelJoint: return new ImpactRecipe(24, 0.30f, 4, 0xFFD27A, 0xE8B04B);
                case VfxMaterial.Terrain: return new ImpactRecipe(6, 0.40f, 0, 0xC98A4B, 0xC98A4B);
                default: return new ImpactRecipe(20, 0.40f, 0, 0xFF5A36, 0xFFFFFF); // explosive fireball; smoke separate 1.2 s
            }
        }

        /// <summary>Low tier x0.5 (02 Perf). Reduce motion leaves particle counts unchanged (07 per-file "02").</summary>
        public static int ParticleCount(int baseCount, int tier) => tier == 0 ? Math.Max(1, baseCount / 2) : baseCount;

        /// <summary>Flash peak: x0.4 and alpha ≤0.5 under Reduce motion (07).</summary>
        public static float FlashPeak(bool reduceMotion) => reduceMotion ? 0.4f : 1f;

        /// <summary>Flash duration: 1–2 f strobes become ≥100 ms ramps under Reduce motion (07).</summary>
        public static float FlashSeconds(int frames, bool reduceMotion)
        {
            float s = frames / 60f;
            return reduceMotion && frames > 0 ? Math.Max(0.1f, s) : s;
        }

        /// <summary>Live impact particle budget (02 Perf: Mid ≤150; Low ×0.5; High 220 per VFX spec §4).</summary>
        public static int LiveParticleBudget(int tier) => tier == 0 ? 75 : tier == 1 ? 150 : 220;

        /// <summary>Break FX spawn stagger: max 3 per 100 ms, extra queued ≤200 ms then dropped (03 beat 4).</summary>
        public const int MaxSpawnsPer100Ms = 3; public const float MaxQueueSec = 0.2f;
    }

    /// <summary>Rate limiter for break-FX spawns (03 beat 4). Time in seconds, caller-supplied (unscaled), allocation-free.</summary>
    public class SpawnLimiter
    {
        readonly float[] _stamps = new float[VfxRules.MaxSpawnsPer100Ms]; int _head;
        public SpawnLimiter() { for (int i = 0; i < _stamps.Length; i++) _stamps[i] = -1f; }
        public bool TryAcquire(float now)
        {
            if (_stamps[_head] >= 0f && now - _stamps[_head] < 0.1f) return false;
            _stamps[_head] = now; _head = (_head + 1) % _stamps.Length; return true;
        }
    }

    /// <summary>Fixed-size generic pool: no Instantiate after warm-up; when empty, recycles the oldest active item (02/03: pooled, no Instantiate).</summary>
    public class RingPool<T> where T : class
    {
        readonly Stack<T> _free = new Stack<T>(); readonly LinkedList<T> _active = new LinkedList<T>();
        readonly Action<T> _onGet, _onRelease;
        public int ActiveCount => _active.Count; public int FreeCount => _free.Count;
        public RingPool(Func<T> create, int size, Action<T> onGet, Action<T> onRelease)
        {
            _onGet = onGet; _onRelease = onRelease;
            for (int i = 0; i < size; i++) { var t = create(); _onRelease?.Invoke(t); _free.Push(t); }
        }
        public T Get()
        {
            T t;
            if (_free.Count > 0) t = _free.Pop();
            else if (_active.Count > 0) { t = _active.First.Value; _active.RemoveFirst(); _onRelease?.Invoke(t); }
            else return null;
            _active.AddLast(t); _onGet?.Invoke(t); return t;
        }
        public void Release(T t) { if (_active.Remove(t)) { _onRelease?.Invoke(t); _free.Push(t); } }
    }
}
