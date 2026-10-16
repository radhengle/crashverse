using System.Collections.Generic;
using UnityEngine;
using Crashverse.Feel;

namespace Crashverse.VFX
{
    /// <summary>Optional payload for impact/break/dust events. Senders that pass other payloads still get a spawn at the director.</summary>
    public struct VfxHit { public Vector3 Position; public VfxMaterial Material; public VfxHit(Vector3 p, VfxMaterial m) { Position = p; Material = m; } }

    /// <summary>
    /// Listens to AnimEventBus and plays pooled, code-built particle effects (VfxRecipes → VfxParticleBuilder). Pure listener: never raises
    /// events or touches gameplay. Sources:
    ///  - PHYS_* ImpactEvent (PR #18/#20) via ImpactEventAdapter: impacts, breaks, joint snaps, collapse dust, plus DamageFeedback by pieceId.
    ///  - Legacy AnimEvents (IMPACT_*/BREAK_*/EXPLODE_*/COLLAPSE_LAND/DEBRIS_BURST): used until the first PHYS_* event arrives, then
    ///    ignored for impact FX so one hit never spawns twice.
    ///  - ANIM_WRECKER_FIRE: Wrecker muzzle flash at muzzleSocket (animation/01).
    /// Budget: impacts lane 150 (Low 75) of the global 150 + 80 pool (power-up lane is reserved for the power-up FX PR); oldest culled first.
    /// Break-type spawns: max 3 per 100 ms (03 beat 4); dropped spawns still get their sound. Unscaled time throughout.
    /// </summary>
    public class VfxDirector : MonoBehaviour
    {
        public VfxAssets assets = new VfxAssets();
        public QualityTier tier = QualityTier.Mid;
        public Transform muzzleSocket;
        [Tooltip("Instances per effect (Low: half, min 2).")] public int poolPerEffect = 4;

        sealed class Fx { public GameObject Go; public ParticleSystem[] Systems; public string Id; public int BudgetId, Particles; public float Until; }

        readonly Dictionary<string, RingPool<Fx>> _pools = new Dictionary<string, RingPool<Fx>>();
        readonly Dictionary<string, VfxRecipe> _recipes = new Dictionary<string, VfxRecipe>();
        readonly List<Fx> _live = new List<Fx>();
        readonly SpawnLimiter _limiter = new SpawnLimiter();
        readonly int[] _culled = new int[16];
        ParticleBudget _budget; int _nextId; bool _physSeen; bool _builtRm; MotionSettings _motion;

        public int LiveCount => _live.Count;
        /// <summary>Live particles across active effects (perf probe; O(live systems), call once per frame at most).</summary>
        public int LiveParticleCount()
        {
            int n = 0;
            foreach (var f in _live) if (f.Systems != null) foreach (var s in f.Systems) if (s != null) n += s.particleCount;
            return n;
        }
        public int BudgetUsed => _budget?.Used ?? 0;
        static bool Rm => MotionSettings.Current != null && MotionSettings.Current.ReduceMotion;

        void Awake() { Build(); }

        void Build()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Kill(_live[i], false);
            _live.Clear(); _pools.Clear(); _recipes.Clear();
            // children of the director are the pooled FX
            var old = new List<Transform>(); foreach (Transform c in transform) old.Add(c);
            foreach (var c in old) Destroy(c.gameObject);
            VfxParticleBuilder.ClearMaterialCache();

            int t = (int)tier; _builtRm = Rm;
            _budget = new ParticleBudget(VfxRules.ImpactLaneBudget(t));
            foreach (var id in VfxIds.All)
            {
                var r = VfxRecipes.Get(id); _recipes[id] = r; string rid = id;
                int size = t == 0 ? Mathf.Max(2, poolPerEffect / 2) : poolPerEffect;
                _pools[id] = new RingPool<Fx>(() => Make(rid, r, t), size, OnGet, OnRelease);
            }
        }

        Fx Make(string id, VfxRecipe r, int t)
        {
            var systems = new List<ParticleSystem>(4);
            var go = VfxParticleBuilder.Build(r, assets, t, _builtRm, transform, systems);
            return new Fx { Go = go, Id = id, Systems = systems.ToArray(), Particles = r.TotalParticles(t) };
        }
        static void OnGet(Fx f) => f.Go.SetActive(true);
        static void OnRelease(Fx f)
        {
            if (f.Systems != null) foreach (var s in f.Systems) if (s != null) s.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            f.Go.SetActive(false);
        }

        void OnEnable()
        {
            AnimEventBus.Raised += OnEvent;
            _motion = MotionSettings.Current; if (_motion != null) _motion.Changed += OnMotionChanged;
        }
        void OnDisable()
        {
            AnimEventBus.Raised -= OnEvent;
            if (_motion != null) _motion.Changed -= OnMotionChanged; _motion = null;
        }
        void OnMotionChanged() { if (Rm != _builtRm) Build(); } // flash layers are baked per Reduce motion state (rare toggle)

        void OnEvent(string name, object payload)
        {
            if (ImpactEventAdapter.IsPhysEvent(name))
            {
                if (!ImpactEventAdapter.TryRead(payload, out var e)) return;
                _physSeen = true; HandleImpact(e); return;
            }
            switch (name)
            {
                case "ANIM_WRECKER_FIRE":
                    var mp = muzzleSocket != null ? muzzleSocket.position : payload is VfxHit mh ? mh.Position : transform.position;
                    var mf = muzzleSocket != null ? muzzleSocket.forward : Vector3.forward;
                    Play(VfxIds.MuzzleFlash, mp, mf, false, 0); return;
            }
            if (_physSeen) return; // PHYS_* is authoritative once present (no double spawns)
            switch (name)
            {
                case "IMPACT_WOOD": case "BREAK_WOOD": Legacy(VfxMaterial.Wood, payload, name[0] == 'B'); break;
                case "IMPACT_BRICK": case "BREAK_BRICK": Legacy(VfxMaterial.Brick, payload, name[0] == 'B'); break;
                case "IMPACT_STEEL": case "BREAK_STEEL": Legacy(VfxMaterial.Steel, payload, name[0] == 'B'); break;
                case "IMPACT_STEEL_JOINT": case "BREAK_STEEL_JOINT": Legacy(VfxMaterial.SteelJoint, payload, name[0] == 'B'); break;
                case "IMPACT_TERRAIN": Legacy(VfxMaterial.Terrain, payload, false); break;
                case "IMPACT_GLASS": case "BREAK_GLASS": Legacy(VfxMaterial.Glass, payload, name[0] == 'B'); break;
                case "EXPLODE_CRATE": case "EXPLODE_BARREL": Legacy(VfxMaterial.Explosive, payload, true); break;
                case "COLLAPSE_LAND":
                {
                    var m = payload is VfxHit h ? h.Material : VfxMaterial.Brick;
                    Play(VfxIds.CollapseDust, PosOf(payload), Vector3.up, false, VfxRecipes.DustTint(m)); break;
                }
                case "DEBRIS_BURST": Play(VfxRecipes.DustFor(payload is VfxHit d ? d.Material : VfxMaterial.Terrain), PosOf(payload), Vector3.up, true, 0); break;
            }
        }

        void Legacy(VfxMaterial m, object payload, bool isBreak)
        {
            if (payload is VfxHit h) m = h.Material;
            Play(VfxRecipes.ImpactFor(m, isBreak), PosOf(payload), Vector3.up, isBreak, 0);
        }

        Vector3 PosOf(object payload) => payload is VfxHit h ? h.Position : transform.position;

        /// <summary>ImpactEvent → particles + DamageFeedback (hit flash / crack step) on the piece.</summary>
        public void HandleImpact(in VfxImpact e)
        {
            var route = VfxImpactRouting.Route(e);
            if (e.PieceId != -1 && (route.HitFlash || route.DamageStep > 0) && DamageFeedback.TryGet(e.PieceId, out var fb))
            {
                if (route.HitFlash) fb.Hit(e.Tier);
                if (route.DamageStep > 0) fb.SetDamageStep(route.DamageStep);
            }
            if (route.EffectId != null) Play(route.EffectId, new Vector3(e.X, e.Y, e.Z), Vector3.up, route.RateLimited, route.Tint);
        }

        /// <summary>Spawn one pooled effect. Returns false when rate-limited/unknown.</summary>
        public bool Play(string id, Vector3 position, Vector3 forward, bool rateLimited, uint tint)
        {
            if (!_pools.TryGetValue(id, out var pool)) return false;
            if (rateLimited && !_limiter.TryAcquire(Time.unscaledTime)) return false; // 03 beat 4
            var fx = pool.Get(); if (fx == null) return false;
            _live.Remove(fx); if (fx.BudgetId != 0) _budget.Release(fx.BudgetId); // recycled oldest in its own pool

            fx.BudgetId = ++_nextId;
            int n = _budget.Request(fx.BudgetId, fx.Particles, _culled);
            for (int c = 0; c < n && c < _culled.Length; c++) CullById(_culled[c]); // global oldest-first cull

            fx.Go.transform.position = position;
            fx.Go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.up);
            var col = tint == 0 ? new Color(1f, 1f, 1f, 1f) : VfxColor.From(tint);
            foreach (var s in fx.Systems)
            {
                if (s == null) continue;
                if (tint != 0) { var main = s.main; var a = main.startColor.color.a; main.startColor = new ParticleSystem.MinMaxGradient(new Color(col.r, col.g, col.b, a)); }
                s.Play(true);
            }
            fx.Until = Time.unscaledTime + _recipes[id].Duration + 0.1f;
            _live.Add(fx); return true;
        }

        void CullById(int budgetId)
        {
            for (int i = 0; i < _live.Count; i++) if (_live[i].BudgetId == budgetId) { Kill(_live[i], true); return; }
        }

        void Kill(Fx fx, bool alreadyReleasedFromBudget)
        {
            if (!alreadyReleasedFromBudget && fx.BudgetId != 0) _budget.Release(fx.BudgetId);
            fx.BudgetId = 0; _live.Remove(fx);
            if (_pools.TryGetValue(fx.Id, out var p)) p.Release(fx);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--) if (now >= _live[i].Until) Kill(_live[i], false);
        }

        /// <summary>Flash scale for materials/particles (×0.4 under Reduce motion, animation/07). Reads the shared MotionSettings.</summary>
        public static float FlashScale => VfxRules.FlashPeak(Rm);
    }
}
