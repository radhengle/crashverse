using System;
using System.Collections.Generic;

namespace Crashverse.VFX
{
    /// <summary>Material ids, matching the projectile Animator `Material` int (animation/02).</summary>
    public enum VfxMaterial { Wood = 0, Brick = 1, Steel = 2, SteelJoint = 3, Terrain = 4, Explosive = 5 }

    /// <summary>One impact/dust recipe row (VFX_Shader_Spec_v1 §5). Pure data, tier/motion scaling in VfxRules.</summary>
    public struct ImpactRecipe
    {
        public int Particles; public float LifetimeSec; public int FlashFrames; public uint ColorA, ColorB, ColorC;
        public ImpactRecipe(int p, float life, int flash, uint a, uint b, uint c = 0) { Particles = p; LifetimeSec = life; FlashFrames = flash; ColorA = a; ColorB = b; ColorC = c == 0 ? a : c; }
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
                case VfxMaterial.Brick: return new ImpactRecipe(16, 0.60f, 2, 0xD2664C, 0xD9A37E, 0xE9997C) /* body, dust, chipped faces */;
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

        /// <summary>One global particle pool (PM 16 Oct): 150 impacts + 80 for the active power-up = 230; Low tier ×0.5. Oldest culled first.</summary>
        public const int ImpactBudget = 150, PowerUpBudget = 80;
        public static int GlobalParticleBudget(int tier) => tier == 0 ? (ImpactBudget + PowerUpBudget) / 2 : ImpactBudget + PowerUpBudget;

        /// <summary>Reduce motion Shockwave: link flashes replaced by one 100 ms ease-out tint per activation, alpha ≤0.5.</summary>
        public const float RmShockwaveTintSec = 0.1f, RmMaxAlpha = 0.5f;
        /// <summary>Reduce motion Time Fracture: static 30% env desaturation under a static 25% tint; tint fades out 300 ms; no crack animation.</summary>
        public const float RmTimeDesat = 0.30f, RmTimeTint = 0.25f, RmTimeTintFadeOutSec = 0.3f;
        /// <summary>Alpha of the RM Shockwave tint at t seconds after activation (ease-out quad from 0.5 to 0).</summary>
        public static float RmShockwaveTintAlpha(float t)
        {
            if (t <= 0f) return RmMaxAlpha; if (t >= RmShockwaveTintSec) return 0f;
            float u = t / RmShockwaveTintSec; return RmMaxAlpha * (1f - (1f - (1f - u) * (1f - u)));
        }

        /// <summary>Shader.globalMaximumLOD per tier: GlossyPBR SubShader LOD 300 (High/Mid) vs 150 (Low). Applied by bootstrap (OPEN_QUESTIONS Q-D6).</summary>
        public static int ShaderLod(int tier) => tier == 0 ? 150 : 300;

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

    /// <summary>
    /// Global particle budget shared by impacts and the active power-up. Emitters register their particle count; when over budget the
    /// oldest live emitters are culled first (ids returned to the caller to stop). Allocation-free after construction.
    /// </summary>
    public class ParticleBudget
    {
        readonly int[] _ids, _counts; int _head, _len, _used;
        public int Capacity { get; }
        public int Used => _used;
        public ParticleBudget(int capacity, int maxEmitters = 64) { Capacity = capacity; _ids = new int[maxEmitters]; _counts = new int[maxEmitters]; }

        /// <summary>Registers an emitter; writes culled ids into `culled` and returns how many. Count is clamped to capacity.</summary>
        public int Request(int id, int count, int[] culled)
        {
            count = Math.Min(count, Capacity); int n = 0;
            while ((_used + count > Capacity || _len == _ids.Length) && _len > 0)
            {
                if (culled != null && n < culled.Length) culled[n] = _ids[_head];
                n++; _used -= _counts[_head]; _head = (_head + 1) % _ids.Length; _len--;
            }
            int tail = (_head + _len) % _ids.Length; _ids[tail] = id; _counts[tail] = count; _len++; _used += count;
            return n;
        }

        /// <summary>Emitter finished naturally.</summary>
        public void Release(int id)
        {
            for (int i = 0; i < _len; i++)
            {
                int k = (_head + i) % _ids.Length; if (_ids[k] != id) continue;
                _used -= _counts[k];
                for (int j = i; j < _len - 1; j++) { int a = (_head + j) % _ids.Length, b = (_head + j + 1) % _ids.Length; _ids[a] = _ids[b]; _counts[a] = _counts[b]; }
                _len--; return;
            }
        }
    }
}
