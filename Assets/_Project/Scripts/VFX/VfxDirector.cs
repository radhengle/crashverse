using System.Collections.Generic;
using UnityEngine;
using Crashverse.Feel;

namespace Crashverse.VFX
{
    /// <summary>Optional payload for impact/break/dust events. Senders that pass other payloads still get a spawn at the director.</summary>
    public struct VfxHit { public Vector3 Position; public VfxMaterial Material; public VfxHit(Vector3 p, VfxMaterial m) { Position = p; Material = m; } }

    /// <summary>
    /// Listens to AnimEventBus (impact/break/dust/collapse events, animation/audio_sync_table.md) and plays pooled one-shot
    /// effects. Pure listener: never raises events or touches gameplay. Prefabs are optional; with none assigned it pools empty
    /// GameObjects so the timing/budget logic still runs (effects authored in Editor, see VFX_Shader_Spec_v1.md §5).
    /// </summary>
    public class VfxDirector : MonoBehaviour
    {
        public GameObject[] impactPrefabs = new GameObject[6]; // indexed by VfxMaterial
        public GameObject dustPrefab;
        public int poolPerMaterial = 6;
        public QualityTier tier = QualityTier.Mid;

        readonly Dictionary<int, RingPool<GameObject>> _pools = new Dictionary<int, RingPool<GameObject>>();
        readonly List<GameObject> _live = new List<GameObject>(); readonly List<float> _until = new List<float>();
        readonly SpawnLimiter _limiter = new SpawnLimiter();
        const int DustKey = 100;

        void Awake()
        {
            for (int m = 0; m < 6; m++) { int k = m; _pools[k] = MakePool(k < impactPrefabs.Length ? impactPrefabs[k] : null, "vfx_impact_" + k); }
            _pools[DustKey] = MakePool(dustPrefab, "vfx_dust");
        }

        RingPool<GameObject> MakePool(GameObject prefab, string name)
        {
            int size = tier == QualityTier.Low ? Mathf.Max(2, poolPerMaterial / 2) : poolPerMaterial;
            return new RingPool<GameObject>(
                () => { var g = prefab != null ? (GameObject)Instantiate(prefab, transform) : new GameObject(name); g.transform.SetParent(transform); return g; },
                size, g => g.SetActive(true), g => g.SetActive(false));
        }

        void OnEnable() { AnimEventBus.Raised += OnEvent; }
        void OnDisable() { AnimEventBus.Raised -= OnEvent; }

        void OnEvent(string name, object payload)
        {
            switch (name)
            {
                case "IMPACT_WOOD": case "BREAK_WOOD": Spawn(VfxMaterial.Wood, payload, name.StartsWith("BREAK")); break;
                case "IMPACT_BRICK": case "BREAK_BRICK": Spawn(VfxMaterial.Brick, payload, name.StartsWith("BREAK")); break;
                case "IMPACT_STEEL": case "BREAK_STEEL": Spawn(VfxMaterial.Steel, payload, name.StartsWith("BREAK")); break;
                case "IMPACT_STEEL_JOINT": case "BREAK_STEEL_JOINT": Spawn(VfxMaterial.SteelJoint, payload, name.StartsWith("BREAK")); break;
                case "IMPACT_TERRAIN": Spawn(VfxMaterial.Terrain, payload, false); break;
                case "EXPLODE_CRATE": case "EXPLODE_BARREL": Spawn(VfxMaterial.Explosive, payload, true); break;
                case "COLLAPSE_LAND": case "DEBRIS_BURST": SpawnPool(DustKey, payload, 1.2f, true); break;
            }
        }

        void Spawn(VfxMaterial m, object payload, bool isBreak)
        {
            if (payload is VfxHit h) m = h.Material;
            SpawnPool((int)m, payload, VfxRules.Impact(m).LifetimeSec, isBreak);
        }

        void SpawnPool(int key, object payload, float life, bool rateLimited)
        {
            if (rateLimited && !_limiter.TryAcquire(Time.unscaledTime)) return; // 03 beat 4: VFX dropped, sound still plays
            var g = _pools[key].Get(); if (g == null) return;
            g.transform.position = payload is VfxHit h ? h.Position : transform.position;
            _live.Add(g); _until.Add(Time.unscaledTime + life);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (now < _until[i]) continue;
                var g = _live[i]; _live.RemoveAt(i); _until.RemoveAt(i);
                foreach (var p in _pools.Values) p.Release(g);
            }
        }

        /// <summary>Flash scale for materials/particles (×0.4 under Reduce motion, animation/07). Reads the shared MotionSettings.</summary>
        public static float FlashScale => VfxRules.FlashPeak(MotionSettings.Current != null && MotionSettings.Current.ReduceMotion);
    }
}
