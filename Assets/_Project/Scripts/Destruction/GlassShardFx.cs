using Crashverse.Core;
using Crashverse.Sim;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// PRESENTATION ONLY. Short-lived glass shards for a broken pane: no Rigidbody, no Collider, never in the sim, never counted
    /// against debris caps, never reads or advances the sim RNG. Integrated in LateUpdate with render time (allowed: nothing
    /// here feeds back into the sim). Own SimRng stream re-seeded per pane from its piece id, so a replay looks the same.
    /// Pool size per tier: ChunkBudget.GlassShardPool (24 / 48 / 72); oldest shard is reused when full. No allocation after Awake.
    /// </summary>
    public class GlassShardFx : MonoBehaviour
    {
        public static GlassShardFx Instance { get; private set; }
        const long Salt = 0x474C4153; // "GLAS"
        const float Gravity = -9.81f;
        const float MinLife = 0.45f, MaxLife = 0.8f;

        Transform[] _t; Renderer[] _r; Vector3[] _v, _w, _scale; float[] _life, _age;
        MaterialPropertyBlock _block;
        int _next, _size;
        readonly SimRng _rng = new SimRng(Salt);
        readonly ChunkEvent _ev = new ChunkEvent();

        public int ActiveCount { get; private set; }

        void Awake()
        {
            Instance = this;
            _size = MaxPool;
            _t = new Transform[_size]; _r = new Renderer[_size]; _v = new Vector3[_size]; _w = new Vector3[_size]; _scale = new Vector3[_size];
            _life = new float[_size]; _age = new float[_size];
            _block = new MaterialPropertyBlock();
            for (int i = 0; i < _size; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); RenderMaterials.Apply(go);
                go.name = "GlassShard";
                var col = go.GetComponent<Collider>(); if (col != null) Destroy(col); // presentation only
                go.transform.SetParent(transform);
                go.SetActive(false);
                _t[i] = go.transform; _r[i] = go.GetComponent<Renderer>();
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static int MaxPool => ChunkBudget.GlassShardPool(ChunkBudget.TierHigh);
        static int TierPool => ChunkBudget.GlassShardPool((int)Crashverse.Perf.QualityController.FeelTier);

        /// <summary>Shatters a pane into its layout's shards (sizes from the precomputed glass layout). Returns shards shown.</summary>
        public int Shatter(DestructibleBlock src, Vector3 hitWorld, Vector3 hitNormal, ImpactTier tier)
        {
            if (src == null || src.ChunkCount <= 0) return 0;
            var chunks = src.ChunkLayoutBuffer; int count = src.ChunkCount;
            var t = src.transform;
            _rng.Reset(unchecked((long)src.PieceId ^ Salt));
            int limit = Mathf.Min(count, TierPool);
            float burst = tier == ImpactTier.Heavy ? 5f : tier == ImpactTier.Medium ? 3.5f : 2f;
            Color tint = src.CurrentColor; tint.a = 0.75f;
            for (int i = 0; i < limit; i++)
            {
                var c = chunks[i];
                int k = _next; _next = (_next + 1) % TierPoolClamp();
                Vector3 local = new Vector3(c.cx * 0.001f, c.cy * 0.001f, c.cz * 0.001f);
                Vector3 pos = t.position + t.rotation * local;
                Vector3 away = pos - hitWorld; float d = away.magnitude; away = d > 1e-4f ? away * (1f / d) : hitNormal;
                _t[k].position = pos; _t[k].rotation = t.rotation;
                // thin, smaller than the cell: shards, not tiles
                _scale[k] = new Vector3(c.sx * 0.0006f, c.sy * 0.0006f, Mathf.Max(0.01f, c.sz * 0.001f));
                _t[k].localScale = _scale[k];
                _v[k] = away * burst * (0.6f + 0.4f * _rng.NextFloat01()) + new Vector3(_rng.NextSigned(), _rng.NextFloat01(), _rng.NextSigned()) * 1.2f;
                _w[k] = new Vector3(_rng.NextSigned(), _rng.NextSigned(), _rng.NextSigned()) * 540f;
                _life[k] = MinLife + (MaxLife - MinLife) * _rng.NextFloat01();
                _age[k] = 0f;
                _block.SetColor("_BaseColor", tint); _block.SetColor("_Color", tint);
                _r[k].SetPropertyBlock(_block);
                if (!_t[k].gameObject.activeSelf) { _t[k].gameObject.SetActive(true); ActiveCount++; }
            }
            _ev.tick = SimRunner.NowTick; _ev.pieceId = src.PieceId; _ev.chunkIndex = -1; _ev.chunkCount = limit;
            _ev.kind = ChunkKind.GlassShard; _ev.material = MaterialType.Glass; _ev.tier = tier;
            _ev.position = hitWorld.ToSim(); _ev.normal = hitNormal.ToSim(); _ev.size = t.localScale.ToSim();
            _ev.speed = 0f; _ev.presentationOnly = true; _ev.ranked = SimRunner.Instance != null && SimRunner.Instance.Ranked;
            Crashverse.Feel.AnimEventBus.Raise(ChunkEvents.GlassShatter, _ev);
            return limit;
        }

        int TierPoolClamp() => Mathf.Clamp(TierPool, 1, _size);

        void LateUpdate()
        {
            if (ActiveCount == 0) return;
            float dt = Time.deltaTime; // presentation only
            for (int i = 0; i < _size; i++)
            {
                var tr = _t[i];
                if (!tr.gameObject.activeSelf) continue;
                _age[i] += dt;
                if (_age[i] >= _life[i]) { tr.gameObject.SetActive(false); ActiveCount--; continue; }
                _v[i].y += Gravity * dt;
                tr.position += _v[i] * dt;
                tr.rotation = tr.rotation * Quaternion.Euler(_w[i].x * dt, _w[i].y * dt, _w[i].z * dt);
                float a = 1f - _age[i] / _life[i];
                tr.localScale = _scale[i] * (0.4f + 0.6f * a);
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _size; i++) if (_t[i] != null) _t[i].gameObject.SetActive(false);
            ActiveCount = 0;
        }
    }
}
