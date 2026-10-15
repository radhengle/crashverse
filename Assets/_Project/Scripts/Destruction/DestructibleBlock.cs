using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// HP/damage model per Contract §3. Steel joints take jointDamageMultiplier.
    /// Support dependencies: if all supports are broken, this block collapses.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DestructibleBlock : MonoBehaviour
    {
        public MaterialDefinition material;
        public bool isObjectivePiece = true;
        public bool isSteelJoint;
        public float currentHp;

        readonly List<DestructibleBlock> _supports = new List<DestructibleBlock>();
        readonly List<DestructibleBlock> _dependents = new List<DestructibleBlock>();
        Rigidbody _rb;
        Renderer _renderer;
        bool _broken;
        bool _collapsed;

        public bool IsBroken => _broken;
        public bool IsCollapsed => _collapsed;
        public float Mass => material != null ? material.mass : 1f;
        public IReadOnlyList<DestructibleBlock> Supports => _supports;

        public event System.Action<DestructibleBlock> Broken;
        public event System.Action<DestructibleBlock> Collapsed;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _renderer = GetComponentInChildren<Renderer>();
            if (material != null)
            {
                currentHp = material.maxHp;
                _rb.mass = material.mass;
                ApplyColor(material.baseColor);
            }
            gameObject.tag = isObjectivePiece ? "ObjectivePiece" : "NonObjective";
        }

        public void Configure(MaterialDefinition mat, bool objective, bool steelJoint = false)
        {
            material = mat;
            isObjectivePiece = objective;
            isSteelJoint = steelJoint;
            currentHp = mat.maxHp;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.mass = mat.mass;
            _rb.isKinematic = true; // held by joints/supports until failure
            ApplyColor(mat.baseColor);
            gameObject.tag = objective ? "ObjectivePiece" : "NonObjective";
        }

        public void AddSupport(DestructibleBlock support)
        {
            if (support == null || support == this) return;
            if (!_supports.Contains(support))
            {
                _supports.Add(support);
                support._dependents.Add(this);
            }
        }

        public void ConnectJoint(DestructibleBlock other, float breakForceOverride = -1f)
        {
            if (other == null || other == this) return;
            var joint = gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = other.GetComponent<Rigidbody>();
            float force = breakForceOverride > 0f
                ? breakForceOverride
                : (material != null ? material.jointBreakForce : 800f);
            joint.breakForce = force;
            joint.breakTorque = force;
            AddSupport(other);
        }

        /// <summary>Apply impact damage. Returns true if this hit broke the block.</summary>
        public bool ApplyDamage(float baseDamage, bool hitAsJoint)
        {
            if (_broken) return false;
            float mult = 1f;
            if (hitAsJoint && material != null)
                mult = material.jointDamageMultiplier;
            else if (isSteelJoint && material != null)
                mult = material.jointDamageMultiplier;

            currentHp -= baseDamage * mult;
            if (material != null && currentHp < material.maxHp * 0.5f)
                ApplyColor(Color.Lerp(material.darkColor, material.baseColor, 0.35f));

            if (currentHp <= 0f)
            {
                Break();
                return true;
            }
            return false;
        }

        public void Break()
        {
            if (_broken) return;
            _broken = true;
            ReleasePhysics();
            BreakAllJoints();
            SpawnDebris();
            Broken?.Invoke(this);
            NotifyDependentsSupportLost();
            // Hide solid mesh; debris takes over visually
            if (_renderer != null) _renderer.enabled = false;
            // Keep collider briefly then disable to avoid stack
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
        }

        public void ForceCollapse()
        {
            if (_collapsed || _broken) return;
            _collapsed = true;
            ReleasePhysics();
            BreakAllJoints();
            Collapsed?.Invoke(this);
            // Collapsed but not fully destroyed yet — still counts when it falls below height / takes damage
            // For objective mass: pieces that fall below collapse height are counted by DestructionTracker
        }

        void ReleasePhysics()
        {
            if (_rb == null) return;
            _rb.isKinematic = false;
            _rb.WakeUp();
        }

        void BreakAllJoints()
        {
            foreach (var j in GetComponents<Joint>())
            {
                if (j != null) Destroy(j);
            }
        }

        void NotifyDependentsSupportLost()
        {
            // Copy list — dependents may mutate
            var deps = _dependents.ToArray();
            foreach (var d in deps)
            {
                if (d == null || d._broken) continue;
                d.EvaluateSupport();
            }
        }

        public void EvaluateSupport()
        {
            if (_broken || _collapsed) return;
            if (_supports.Count == 0) return;
            bool anyAlive = false;
            foreach (var s in _supports)
            {
                if (s != null && !s._broken && !s._collapsed)
                {
                    anyAlive = true;
                    break;
                }
            }
            if (!anyAlive)
                ForceCollapse();
        }

        void SpawnDebris()
        {
            if (material == null) return;
            var pool = DebrisPool.Instance;
            if (pool == null) return;
            int n = Mathf.Clamp(material.maxDebrisChunks, 1, 8);
            pool.SpawnBurst(transform.position, transform.localScale, material, n);
        }

        void ApplyColor(Color c)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) return;
            // URP/Lit or built-in; set property block to avoid material leaks
            var block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            _renderer.SetPropertyBlock(block);
        }

        void OnJointBreak(float breakForce)
        {
            // Joint failed from physics impulse — treat as structural damage
            if (!_broken)
                ApplyDamage(material != null ? material.maxHp * 0.5f : 50f, hitAsJoint: true);
        }
    }
}
