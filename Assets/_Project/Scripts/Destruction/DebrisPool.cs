using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>Pooled debris with fade/despawn and a hard cap for mobile perf.</summary>
    public class DebrisPool : MonoBehaviour
    {
        public static DebrisPool Instance { get; private set; }

        [SerializeField] int maxActiveDebris = 48;
        [SerializeField] int prewarm = 24;

        readonly Queue<DebrisPiece> _free = new Queue<DebrisPiece>();
        readonly List<DebrisPiece> _active = new List<DebrisPiece>();
        Transform _root;

        void Awake()
        {
            Instance = this;
            _root = new GameObject("DebrisRoot").transform;
            _root.SetParent(transform);
            for (int i = 0; i < prewarm; i++)
                _free.Enqueue(CreatePiece());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        DebrisPiece CreatePiece()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Debris";
            go.tag = "Debris";
            go.transform.SetParent(_root);
            go.SetActive(false);
            var rb = go.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.sleepThreshold = 0.08f;
            var piece = go.AddComponent<DebrisPiece>();
            return piece;
        }

        public void SpawnBurst(Vector3 pos, Vector3 parentScale, MaterialDefinition mat, int count)
        {
            for (int i = 0; i < count; i++)
            {
                while (_active.Count >= maxActiveDebris)
                    DespawnOldest();

                var piece = _free.Count > 0 ? _free.Dequeue() : CreatePiece();
                float s = Mathf.Clamp(Mathf.Min(parentScale.x, Mathf.Min(parentScale.y, parentScale.z)) * 0.35f, 0.15f, 0.55f);
                piece.transform.position = pos + Random.insideUnitSphere * 0.4f;
                piece.transform.rotation = Random.rotation;
                piece.transform.localScale = Vector3.one * s;
                piece.Activate(mat, mat != null ? mat.debrisLifetimeSeconds : 2.5f);
                _active.Add(piece);
            }
        }

        public void ClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Recycle(_active[i]);
            _active.Clear();
        }

        void DespawnOldest()
        {
            if (_active.Count == 0) return;
            var oldest = _active[0];
            _active.RemoveAt(0);
            Recycle(oldest);
        }

        internal void NotifyFinished(DebrisPiece piece)
        {
            _active.Remove(piece);
            Recycle(piece);
        }

        void Recycle(DebrisPiece piece)
        {
            if (piece == null) return;
            piece.Deactivate();
            _free.Enqueue(piece);
        }

        void Update()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Tick(Time.deltaTime))
                {
                    var p = _active[i];
                    _active.RemoveAt(i);
                    Recycle(p);
                }
            }
        }
    }

    public class DebrisPiece : MonoBehaviour
    {
        Rigidbody _rb;
        Renderer _rend;
        float _life;
        float _maxLife;
        Color _color;

        public void Activate(MaterialDefinition mat, float life)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rend == null) _rend = GetComponent<Renderer>();
            _maxLife = life;
            _life = life;
            _color = mat != null ? mat.breakFxColor : Color.gray;
            gameObject.SetActive(true);
            _rb.isKinematic = false;
            _rb.velocity = Random.insideUnitSphere * 3f + Vector3.up * 2f;
            _rb.angularVelocity = Random.insideUnitSphere * 6f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", _color);
            block.SetColor("_Color", _color);
            _rend.SetPropertyBlock(block);
        }

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
        public bool Tick(float dt)
        {
            _life -= dt;
            if (_life < 0.6f && _rend != null)
            {
                float a = Mathf.Clamp01(_life / 0.6f);
                var c = _color;
                c.a = a;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                _rend.SetPropertyBlock(block);
            }
            return _life <= 0f;
        }
    }
}
