using System.Collections.Generic;
using Crashverse.Sim;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Pooled debris (03). Caps by tier 25 low / 60 mid / 90 high simulated rigidbodies (OQ-D1); at cap the OLDEST SETTLED chunk is
    /// recycled first (else the oldest). Chunks are visual only: destroyed mass is counted on the source piece (OQ-D2).
    /// Deterministic: offsets/velocities come from a SimRng stream forked from SimRunner's seed, lifetimes count sim ticks
    /// (FixedUpdate), and the pool prewarms to the current tier cap so play never instantiates under the cap.
    /// </summary>
    public class DebrisPool : MonoBehaviour
    {
        public static DebrisPool Instance { get; private set; }

        /// <summary>Ranked: one tier-independent cap (DebrisRules.RankedSimCap) because debris collides with scoring pieces.</summary>
        int maxActiveDebris => DebrisRules.SimCap(IsRanked, Crashverse.Feel.DebrisBudget.Cap(Crashverse.Perf.QualityController.FeelTier));
        /// <summary>Tier used for chunk counts: Mid in ranked runs (tier-independent), the device tier otherwise.</summary>
        public static Crashverse.Feel.QualityTier ChunkTier => IsRanked ? Crashverse.Feel.QualityTier.Mid : Crashverse.Perf.QualityController.FeelTier;
        static bool IsRanked => SimRunner.Instance != null && SimRunner.Instance.Running && SimRunner.Instance.Ranked;
        readonly Crashverse.Feel.DebrisLedger<DebrisPiece> _ledger = new Crashverse.Feel.DebrisLedger<DebrisPiece>(60);
        [SerializeField] int prewarm = 24; // minimum; actual prewarm = max(prewarm, tier cap)
        SimRng _rng = new SimRng(DebrisRules.RngSeed(0));
        int _runId = -1;
        readonly List<DebrisPiece> _all = new List<DebrisPiece>(); // creation order; defines the free-list order after a reset

        readonly Queue<DebrisPiece> _free = new Queue<DebrisPiece>();
        readonly List<DebrisPiece> _active = new List<DebrisPiece>();
        /// <summary>Live debris rigidbodies (perf capture / tests).</summary>
        public int ActiveCount => _active.Count;
        Transform _root;

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

        /// <summary>Top the pool up to max(prewarm, current tier cap).</summary>
        void Prewarm()
        {
            int target = Mathf.Max(prewarm, maxActiveDebris);
            while (_all.Count < target) _free.Enqueue(CreatePiece());
            _ledger.Cap = maxActiveDebris;
        }

        void OnRunStarted(SimRunner sim) => ResetForRun(sim.Seed, sim.RunId);

        /// <summary>
        /// Every run start and Retry: recycle all debris, rebuild the free list in creation order, re-seed the debris RNG from
        /// the run seed. Lifetimes restart from each piece's Activate, so a Retry is identical to a fresh start.
        /// </summary>
        public void ResetForRun(long runSeed, int runId)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i] != null) _active[i].Deactivate();
            _active.Clear(); _ledger.Clear(); _free.Clear();
            Prewarm();
            for (int i = 0; i < _all.Count; i++) { _all[i].Deactivate(); _free.Enqueue(_all[i]); }
            _rng = new SimRng(DebrisRules.RngSeed(runSeed));
            _runId = runId;
        }

        /// <summary>Debris RNG stream; also re-seeds lazily if the pool missed a RunStarted (created mid-run).</summary>
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

        DebrisPiece CreatePiece()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Debris";
            go.tag = "Debris";
            go.layer = SimLayers.Debris; // fixed layer; ranked matrix in SimLayers
            go.transform.SetParent(_root);
            go.SetActive(false);
            var rb = go.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.sleepThreshold = 0.08f;
            var piece = go.AddComponent<DebrisPiece>();
            _all.Add(piece);
            return piece;
        }

        public void SpawnBurst(Vector3 pos, Vector3 parentScale, MaterialDefinition mat, int count)
        {
            var rng = Rng; // before touching the free list: may reset the pool for a new run
            for (int i = 0; i < count; i++)
            {
                _ledger.Cap = maxActiveDebris;
                while (_active.Count >= maxActiveDebris) { var v = _ledger.PickVictim(); if (v == null) { DespawnOldest(); continue; } _active.Remove(v); _ledger.Remove(v); Recycle(v); }

                var piece = _free.Count > 0 ? _free.Dequeue() : CreatePiece();
                float s = Mathf.Clamp(Mathf.Min(parentScale.x, Mathf.Min(parentScale.y, parentScale.z)) * 0.35f, 0.15f, 0.55f);
                piece.transform.position = pos + rng.InsideUnitSphere() * 0.4f;
                piece.transform.rotation = rng.Rotation();
                piece.transform.localScale = Vector3.one * s;
                piece.Activate(mat, SimTime.SecondsToTicks(mat != null ? mat.debrisLifetimeSeconds : 2.5f), rng);
                _active.Add(piece); _ledger.Add(piece);
            }
        }

        public void ClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Recycle(_active[i]);
            _active.Clear(); _ledger.Clear();
        }

        void DespawnOldest()
        {
            if (_active.Count == 0) return;
            var oldest = _active[0];
            _active.RemoveAt(0); _ledger.Remove(oldest);
            Recycle(oldest);
        }

        internal void NotifyFinished(DebrisPiece piece)
        {
            _active.Remove(piece); _ledger.Remove(piece);
            Recycle(piece);
        }

        void Recycle(DebrisPiece piece)
        {
            if (piece == null) return;
            piece.Deactivate();
            _free.Enqueue(piece);
        }

        /// <summary>One sim tick per FixedUpdate (0.02 s); lifetimes are in ticks, not render time.</summary>
        void FixedUpdate()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _ledger.MarkSettled(_active[i], _active[i].IsSettled);
                if (_active[i].Tick())
                {
                    var p = _active[i];
                    _active.RemoveAt(i); _ledger.Remove(p);
                    Recycle(p);
                }
            }
        }
    }

    public class DebrisPiece : MonoBehaviour
    {
        Rigidbody _rb;
        Renderer _rend;
        const int FadeTicks = 30; // 0.6 s at 50 Hz
        int _lifeTicks;
        int _maxLifeTicks;
        Color _color;

        public void Activate(MaterialDefinition mat, int lifeTicks, SimRng rng)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rend == null) _rend = GetComponent<Renderer>();
            _maxLifeTicks = lifeTicks;
            _lifeTicks = lifeTicks;
            _color = mat != null ? mat.breakFxColor : Color.gray;
            gameObject.SetActive(true);
            _rb.isKinematic = false;
            _rb.velocity = rng.InsideUnitSphere() * 3f + Vector3.up * 2f;
            _rb.angularVelocity = rng.InsideUnitSphere() * 6f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", _color);
            block.SetColor("_Color", _color);
            _rend.SetPropertyBlock(block);
        }

        public bool IsSettled => _rb != null && (_rb.isKinematic || _rb.velocity.sqrMagnitude < 0.15f * 0.15f);

        public void Deactivate()
        {
            if (_rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
            gameObject.SetActive(false);
        }

        /// <returns>true when life expired</returns>
        public bool Tick()
        {
            _lifeTicks--;
            if (_lifeTicks < FadeTicks && _rend != null)
            {
                float a = Mathf.Clamp01(_lifeTicks / (float)FadeTicks);
                var c = _color;
                c.a = a;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                _rend.SetPropertyBlock(block);
            }
            return _lifeTicks <= 0;
        }
    }
}
