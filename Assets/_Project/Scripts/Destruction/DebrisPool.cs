using Crashverse.Core;
using System.Collections.Generic;
using Crashverse.Sim;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Pooled sim debris (03) + chunked destruction (physics/Chunked_Destruction.md). Tier caps 25 / 60 / 90 colliding chunks
    /// (OQ-D1), ranked: DebrisRules.RankedSimCap (60) on layer 9. Every chunk lives in a ChunkLedger slot:
    /// Held (kinematic in place until its release tick) → Dynamic → Frozen rubble (settled 25 ticks → kinematic) → Fading → Free.
    /// At cap the oldest frozen rubble fades first, then the oldest settled chunk, then the oldest. Glass never comes here
    /// (GlassShardFx, presentation only). Deterministic: layouts are precomputed per block at level build, slots are taken
    /// in stable order, lifetimes count sim ticks, velocities come from the debris SimRng stream. The pool prewarms to
    /// cap + FadeHeadroom so play never instantiates; nothing in SpawnChunks / FixedUpdate allocates.
    /// </summary>
    public class DebrisPool : MonoBehaviour
    {
        public static DebrisPool Instance { get; private set; }

        int maxActiveDebris => DebrisRules.SimCap(IsRanked, Crashverse.Feel.DebrisBudget.Cap(Crashverse.Perf.QualityController.FeelTier));
        /// <summary>Tier used for chunk counts: Mid in ranked runs (tier-independent), the device tier otherwise.</summary>
        public static Crashverse.Feel.QualityTier ChunkTier => IsRanked ? Crashverse.Feel.QualityTier.Mid : Crashverse.Perf.QualityController.FeelTier;
        public static int ChunkTierIndex => (int)ChunkTier;
        static bool IsRanked => SimRunner.Instance != null && SimRunner.Instance.Running && SimRunner.Instance.Ranked;

        /// <summary>High cap (90) + fade headroom: the most slots any tier uses.</summary>
        public const int MaxSlots = 90 + ChunkBudget.FadeHeadroom;
        readonly ChunkLedger _ledger = new ChunkLedger(MaxSlots, DebrisRules.RankedSimCap);
        readonly List<DebrisPiece> _slots = new List<DebrisPiece>(MaxSlots); // index = ledger slot = creation order
        readonly int[] _demoted = new int[MaxSlots];
        readonly int[] _orderScratch = new int[64];
        readonly ChunkEvent _evRelease = new ChunkEvent(), _evSettle = new ChunkEvent(), _evFade = new ChunkEvent(), _evLand = new ChunkEvent();
        [SerializeField] int prewarm = 24; // minimum; actual prewarm = max(prewarm, tier cap + FadeHeadroom)
        SimRng _rng = new SimRng(DebrisRules.RngSeed(0));
        int _runId = -1;
        Transform _root;

        /// <summary>Colliding debris bodies (held + dynamic + frozen rubble): what the caps bound. Perf capture / tests.</summary>
        public int ActiveCount => _ledger.SimCount;
        public int DynamicCount => _ledger.CountIn(ChunkState.Dynamic) + _ledger.CountIn(ChunkState.Held);
        public int RubbleCount => _ledger.CountIn(ChunkState.Frozen);
        public int PooledCount => _slots.Count;

        void Awake()
        {
            Instance = this;
            _root = new GameObject("DebrisRoot").transform;
            _root.SetParent(transform);
            Prewarm();
            Crashverse.Perf.QualityController.TierChanged += Prewarm;
            SimRunner.RunStarted += OnRunStarted;
            if (SimRunner.Instance != null && SimRunner.Instance.Running) OnRunStarted(SimRunner.Instance);
        }

        /// <summary>Top the pool up to max(prewarm, cap + FadeHeadroom); apply the tier's cap and rubble lifetime.</summary>
        void Prewarm()
        {
            int target = Mathf.Min(MaxSlots, Mathf.Max(prewarm, maxActiveDebris + ChunkBudget.FadeHeadroom));
            while (_slots.Count < target) CreatePiece();
            _ledger.Cap = maxActiveDebris;
            _ledger.RubbleLifeTicks = ChunkBudget.RubbleLifeTicks(ChunkTierIndex);
        }

        void OnRunStarted(SimRunner sim) => ResetForRun(sim.Seed, sim.RunId);

        /// <summary>Every run start and Retry: recycle all debris, clear the ledger (slot order = creation order), re-seed the debris RNG.</summary>
        public void ResetForRun(long runSeed, int runId)
        {
            for (int i = 0; i < _slots.Count; i++) _slots[i].Deactivate();
            _ledger.Clear();
            Prewarm();
            _rng = new SimRng(DebrisRules.RngSeed(runSeed));
            _runId = runId;
        }

        SimRng Rng
        {
            get
            {
                var sim = SimRunner.Instance;
                if (sim != null && sim.Running && sim.RunId != _runId) ResetForRun(sim.Seed, sim.RunId);
                return _rng;
            }
        }

        void OnDestroy()
        {
            Crashverse.Perf.QualityController.TierChanged -= Prewarm;
            SimRunner.RunStarted -= OnRunStarted;
            if (Instance == this) Instance = null;
        }

        void CreatePiece()
        {
            int slot = _ledger.AddSlot();
            if (slot < 0) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); RenderMaterials.Apply(go);
            go.name = "Debris";
            go.tag = "Debris";
            go.layer = SimLayers.Debris; // fixed layer; ranked matrix in SimLayers
            go.transform.SetParent(_root);
            go.SetActive(false);
            var rb = go.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.sleepThreshold = 0.08f;
            var piece = go.AddComponent<DebrisPiece>();
            piece.Slot = slot;
            piece.Pool = this;
            _slots.Add(piece);
        }

        /// <summary>A slot for one more colliding chunk: fades victims to stay under cap, reuses free (else oldest fading) slots.</summary>
        DebrisPiece Acquire(int now)
        {
            _ledger.Cap = maxActiveDebris;
            int nd = _ledger.MakeRoom(now, _demoted);
            for (int i = 0; i < nd && i < _demoted.Length; i++) { var v = _slots[_demoted[i]]; v.BeginFade(); Raise(ChunkEvents.Fade, _evFade, v, now); }
            int slot = _ledger.TakeFree(out bool wasFading);
            if (slot < 0) return null; // cap 0
            var p = _slots[slot];
            if (wasFading) p.Deactivate();
            return p;
        }

        /// <summary>Legacy greybox burst (random cubes around the piece). Kept for callers without a chunk layout.</summary>
        /// <param name="pieceId">Source piece id (PieceIds.Of(blockId)); stamped on every chunk, overwritten on pooled reuse.</param>
        public void SpawnBurst(Vector3 pos, Vector3 parentScale, MaterialDefinition mat, int count, int pieceId = -1)
        {
            var rng = Rng; // before touching the pool: may reset it for a new run
            int now = SimRunner.NowTick;
            for (int i = 0; i < count; i++)
            {
                var piece = Acquire(now);
                if (piece == null) return;
                float s = Mathf.Clamp(Mathf.Min(parentScale.x, Mathf.Min(parentScale.y, parentScale.z)) * 0.35f, 0.15f, 0.55f);
                piece.transform.position = pos + rng.InsideUnitSphere() * 0.4f;
                piece.transform.rotation = rng.Rotation();
                piece.transform.localScale = Vector3.one * s;
                piece.PieceId = pieceId;
                int life = SimTime.SecondsToTicks(mat != null ? mat.debrisLifetimeSeconds : 2.5f);
                piece.Activate(mat, life, rng);
                _ledger.Activate(piece.Slot, now, now, life);
            }
        }

        /// <summary>
        /// Breaks <paramref name="src"/> into its precomputed chunks. Chunks nearest <paramref name="hitWorld"/> are released first
        /// (ChunkLayout.AssignReleaseOrder); the rest are held kinematic in place and drop over the next few ticks.
        /// Returns the number of sim chunks spawned. Glass is handled by GlassShardFx (0 here).
        /// </summary>
        public int SpawnChunks(DestructibleBlock src, Vector3 hitWorld, MaterialType matType, ImpactTier tier)
        {
            if (src == null || src.ChunkCount <= 0) return 0;
            var chunks = src.ChunkLayoutBuffer;
            int count = src.ChunkCount;
            if (ChunkKindRules.PresentationOnly(chunks[0].kind)) return 0;
            var rng = Rng;
            int now = SimRunner.NowTick;
            var t = src.transform;
            Vector3 size = t.localScale;
            Vector3 hl = t.InverseTransformPoint(hitWorld); // unit-cube space
            ChunkLayout.AssignReleaseOrder(chunks, count, ChunkLayout.ToMmSigned(hl.x * size.x), ChunkLayout.ToMmSigned(hl.y * size.y),
                                           ChunkLayout.ToMmSigned(hl.z * size.z), ChunkKindRules.ReleaseSpreadTicks(chunks[0].kind), _orderScratch);
            float blockVol = Mathf.Max(1e-4f, size.x * size.y * size.z);
            Vector3 inherit = src.Body != null && !src.Body.isKinematic ? src.Body.velocity : Vector3.zero;
            Color col = src.CurrentColor;
            int spawned = 0;
            for (int i = 0; i < count; i++) // index order: slot assignment is stable
            {
                var piece = Acquire(now);
                if (piece == null) break;
                var c = chunks[i];
                var local = new Vector3(c.cx * 0.001f, c.cy * 0.001f, c.cz * 0.001f);
                var csize = new Vector3(c.sx * 0.001f, c.sy * 0.001f, c.sz * 0.001f);
                Vector3 world = t.position + t.rotation * local;
                float mass = Mathf.Max(0.1f, src.Mass * (csize.x * csize.y * csize.z) / blockVol);
                Vector3 away = world - hitWorld; float d = away.magnitude; away = d > 1e-4f ? away * (1f / d) : Vector3.up;
                float kick = tier == ImpactTier.Heavy ? 3f : tier == ImpactTier.Medium ? 1.8f : 0.8f;
                Vector3 v = inherit + away * kick + rng.InsideUnitSphere() * 0.5f;
                Vector3 w = rng.InsideUnitSphere() * (c.kind == ChunkKind.TimberBeam ? 2.5f : 4f);
                piece.PieceId = src.PieceId;
                piece.ActivateChunk(src.material, matType, c, i, count, world, t.rotation, csize * 0.98f, mass, v, w, col);
                _ledger.Activate(piece.Slot, now, now + c.delayTicks, ChunkBudget.MaxDynamicTicks);
                if (c.delayTicks <= 0) { piece.Release(); Raise(ChunkEvents.Release, _evRelease, piece, now); }
                spawned++;
            }
            return spawned;
        }

        public void ClearAll()
        {
            for (int i = 0; i < _slots.Count; i++) _slots[i].Deactivate();
            _ledger.Clear();
        }

        /// <summary>One sim tick per FixedUpdate (0.02 s): release held chunks, freeze settled ones, fade and recycle.</summary>
        void FixedUpdate()
        {
            int now = SimRunner.NowTick;
            for (int i = 0; i < _slots.Count; i++)
            {
                var st = _ledger.State(i);
                if (st == ChunkState.Free) continue;
                var p = _slots[i];
                switch (_ledger.Advance(i, now, p.IsSettledNow))
                {
                    case ChunkTransition.Release: p.Release(); Raise(ChunkEvents.Release, _evRelease, p, now); break;
                    case ChunkTransition.Freeze: p.Freeze(); Raise(ChunkEvents.Settle, _evSettle, p, now); break;
                    case ChunkTransition.StartFade: p.BeginFade(); Raise(ChunkEvents.Fade, _evFade, p, now); break;
                    case ChunkTransition.Recycle: p.Deactivate(); break;
                    default: if (st == ChunkState.Fading) p.FadeVisual(_ledger.FadeLeft(i) / (float)ChunkBudget.FadeTicks); break;
                }
            }
        }

        /// <summary>Presentation hook from DebrisPiece.OnCollisionEnter (dust / impact VFX on landing). Never affects the sim.</summary>
        internal void RaiseLand(DebrisPiece p, Vector3 point, Vector3 normal, float speed, float impulse)
        {
            Fill(_evLand, p, SimRunner.NowTick);
            _evLand.position = point.ToSim(); _evLand.normal = normal.ToSim(); _evLand.speed = speed;
            _evLand.tier = ImpactClassifier.Tier(impulse, ImpactEventEmitter.Instance != null ? ImpactEventEmitter.Instance.Thresholds : ImpactTierThresholds.Default);
            Crashverse.Feel.AnimEventBus.Raise(ChunkEvents.Land, _evLand);
        }

        void Raise(string name, ChunkEvent e, DebrisPiece p, int now)
        {
            Fill(e, p, now);
            Crashverse.Feel.AnimEventBus.Raise(name, e);
        }

        void Fill(ChunkEvent e, DebrisPiece p, int now)
        {
            e.tick = now; e.pieceId = p.PieceId; e.chunkIndex = p.ChunkIndex; e.chunkCount = p.ChunkCount;
            e.kind = p.Kind; e.material = p.MaterialType; e.tier = ImpactTier.Light;
            e.position = p.transform.position.ToSim(); e.normal = Vector3.up.ToSim(); e.size = p.transform.localScale.ToSim();
            e.speed = p.Speed; e.presentationOnly = false; e.ranked = IsRanked;
        }
    }

    public class DebrisPiece : MonoBehaviour
    {
        Rigidbody _rb;
        Renderer _rend;
        Collider _col;
        MaterialPropertyBlock _block;
        const int FadeTicks = 30; // 0.6 s at 50 Hz (legacy Tick)
        int _lifeTicks;
        Color _color;
        Vector3 _pendingV, _pendingW, _scale;
        int _lastLandTick = int.MinValue;
        /// <summary>Source piece id (VFX #31 / Physics #20). -1 while pooled.</summary>
        public int PieceId { get; set; } = -1;
        public int Slot { get; set; } = -1;
        internal DebrisPool Pool { get; set; }
        public int ChunkIndex { get; private set; } = -1;
        public int ChunkCount { get; private set; }
        public ChunkKind Kind { get; private set; }
        public MaterialType MaterialType { get; private set; } = MaterialType.Terrain;
        public float Speed => _rb != null && !_rb.isKinematic ? _rb.velocity.magnitude : 0f;

        void Cache()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rend == null) _rend = GetComponent<Renderer>();
            if (_col == null) _col = GetComponent<Collider>();
            if (_block == null) _block = new MaterialPropertyBlock();
        }

        /// <summary>Legacy burst chunk: dynamic at once with a random kick.</summary>
        public void Activate(MaterialDefinition mat, int lifeTicks, SimRng rng)
        {
            Cache();
            _lifeTicks = lifeTicks;
            ChunkIndex = -1; ChunkCount = 0; Kind = ChunkKind.StoneBlock; MaterialType = MaterialType.Terrain;
            _color = mat != null ? mat.breakFxColor : Color.gray;
            _scale = transform.localScale;
            gameObject.SetActive(true);
            if (_col != null) _col.enabled = true;
            _rb.isKinematic = false;
            _rb.detectCollisions = true;
            _rb.velocity = rng.InsideUnitSphere() * 3f + Vector3.up * 2f;
            _rb.angularVelocity = rng.InsideUnitSphere() * 6f;
            SetColor(_color);
        }

        /// <summary>Chunk from a precomputed layout: placed exactly where it sat in the block, held kinematic until Release.</summary>
        public void ActivateChunk(MaterialDefinition mat, MaterialType type, in ChunkSpec c, int index, int count, Vector3 pos, Quaternion rot,
                                  Vector3 size, float mass, Vector3 velocity, Vector3 angular, Color color)
        {
            Cache();
            ChunkIndex = index; ChunkCount = count; Kind = c.kind; MaterialType = type;
            _lifeTicks = int.MaxValue;
            transform.position = pos; transform.rotation = rot;
            transform.localScale = size; _scale = size;
            _color = color;
            _pendingV = velocity; _pendingW = angular;
            _lastLandTick = int.MinValue;
            gameObject.SetActive(true);
            if (_col != null) _col.enabled = true;
            _rb.detectCollisions = true;
            _rb.isKinematic = true;
            _rb.mass = mass;
            SetColor(color);
        }

        /// <summary>Held → dynamic with the velocity computed at spawn.</summary>
        public void Release()
        {
            Cache();
            if (!_rb.isKinematic) return;
            _rb.isKinematic = false;
            _rb.velocity = _pendingV;
            _rb.angularVelocity = _pendingW;
            _rb.WakeUp();
        }

        /// <summary>Settled → static rubble: kinematic, collider kept, no further solver cost.</summary>
        public void Freeze()
        {
            Cache();
            _rb.velocity = Vector3.zero; _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        /// <summary>Leaves the sim at once (collider off); FadeVisual shrinks/fades it, then the pool recycles it.</summary>
        public void BeginFade()
        {
            Cache();
            if (!_rb.isKinematic) { _rb.velocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
            if (_col != null) _col.enabled = false;
        }

        public void FadeVisual(float a)
        {
            a = Mathf.Clamp01(a);
            transform.localScale = _scale * (0.35f + 0.65f * a);
            var c = _color; c.a = a; SetColor(c);
        }

        /// <summary>Asleep, or slower than 0.15 m/s (DestructionTracker settle speed) and barely spinning.</summary>
        public bool IsSettledNow => _rb == null || _rb.isKinematic || _rb.IsSleeping() ||
                                    (_rb.velocity.sqrMagnitude < 0.15f * 0.15f && _rb.angularVelocity.sqrMagnitude < 0.5f * 0.5f);

        public bool IsSettled => _rb != null && (_rb.isKinematic || _rb.velocity.sqrMagnitude < 0.15f * 0.15f);

        public void Deactivate()
        {
            PieceId = -1;
            ChunkIndex = -1; ChunkCount = 0;
            if (_rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
                _rb.detectCollisions = true;
            }
            if (_col != null) _col.enabled = true;
            gameObject.SetActive(false);
        }

        /// <summary>Legacy per-piece lifetime (tests / old callers). The pool now drives lifetimes through ChunkLedger.</summary>
        /// <returns>true when life expired</returns>
        public bool Tick()
        {
            _lifeTicks--;
            if (_lifeTicks < FadeTicks && _rend != null) FadeVisual(_lifeTicks / (float)FadeTicks);
            return _lifeTicks <= 0;
        }

        /// <summary>Dust / impact VFX hook only (presentation). 10-tick cooldown per chunk so a rolling block doesn't spam.</summary>
        void OnCollisionEnter(Collision c)
        {
            if (Pool == null || ChunkIndex < 0) return;
            float speed = c.relativeVelocity.magnitude;
            if (DebrisDamage.ToMmPerS(speed) < ChunkEvents.LandMinMmPerS) return;
            int now = SimRunner.NowTick;
            if (now - _lastLandTick < 10) return;
            _lastLandTick = now;
            Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            Vector3 n = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.up;
            Pool.RaiseLand(this, p, n, speed, c.impulse.magnitude);
        }

        void SetColor(Color c)
        {
            if (_rend == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_Color", c);
            _rend.SetPropertyBlock(_block);
        }
    }
}
