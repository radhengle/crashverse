using System.Collections.Generic;
using Crashverse.Data;
using Crashverse.Sim;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// One structural piece. HP/damage per Contract §3 (steel joints 2× on direct hits, SG-08: splash is not multiplied).
    /// Support graph: when every support is gone the block collapses (becomes dynamic). Out-of-bounds (kill plane) counts
    /// as destroyed objective mass (Art v2). Indestructible pieces (boss core/shutter, sea-stack) report hits but take no HP loss.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DestructibleBlock : MonoBehaviour
    {
        public MaterialDefinition material;
        public string blockId;
        public string role = "";
        public bool isObjectivePiece = true;
        public bool isSteelJoint;
        public bool indestructible;
        public bool explosive;
        public float currentHp;
        public float InitialY { get; private set; }
        public float Height { get; private set; } = 1f;

        readonly List<DestructibleBlock> _supports = new List<DestructibleBlock>();
        readonly List<DestructibleBlock> _dependents = new List<DestructibleBlock>();
        Rigidbody _rb;
        Renderer _renderer;
        Color _baseColor;
        bool _broken, _collapsed, _outOfBounds;
        int _damageStep;
        int _crackStep; // 0..3, ImpactEvent Crack steps at 33/66/90% damage

        public bool IsBroken => _broken;
        public bool IsCollapsed => _collapsed;
        public bool IsOutOfBounds => _outOfBounds;
        public float Mass => material != null ? material.mass : 1f;
        public float MaxHp => material != null ? material.maxHp : 1f;
        public Rigidbody Body => _rb != null ? _rb : (_rb = GetComponent<Rigidbody>());
        public IReadOnlyList<DestructibleBlock> Supports => _supports;
        public IReadOnlyList<DestructibleBlock> Dependents => _dependents;

        public event System.Action<DestructibleBlock> Broken;
        public event System.Action<DestructibleBlock> Collapsed;
        public event System.Action<DestructibleBlock> WentOutOfBounds;
        /// <summary>(block, hpRemoved, directProjectileHit)</summary>
        public event System.Action<DestructibleBlock, float, bool> Damaged;

        public void Configure(BlockData d, MaterialDefinition mat)
        {
            material = mat;
            blockId = d.id;
            role = d.role ?? "";
            isObjectivePiece = !d.nonObj;
            isSteelJoint = d.joint;
            indestructible = d.indestructible;
            explosive = d.explosive;
            currentHp = mat.maxHp;
            _rb = GetComponent<Rigidbody>();
            _rb.mass = mat.mass;
            _rb.isKinematic = true; // held by supports until failure
            _rb.sleepThreshold = 0.08f;
            _baseColor = string.IsNullOrEmpty(d.color) ? mat.baseColor : MaterialDefinition.Hex(d.color);
            if (d.joint) _baseColor = Color.Lerp(_baseColor, MaterialDefinition.Hex("E8B04B"), 0.35f); // gold weak-point tint
            ApplyColor(_baseColor);
            gameObject.tag = isObjectivePiece ? "ObjectivePiece" : "NonObjective";
            InitialY = transform.position.y;
            Height = d.s != null && d.s.Length > 1 ? d.s[1] : 1f;
        }

        public void AddSupport(DestructibleBlock s, bool withJoint)
        {
            if (s == null || s == this || _supports.Contains(s)) return;
            _supports.Add(s);
            s._dependents.Add(this);
            if (!withJoint) return;
            var j = gameObject.AddComponent<FixedJoint>();
            j.connectedBody = s.Body;
            float f = material != null ? material.jointBreakForce : 800f;
            j.breakForce = f; j.breakTorque = f;
        }

        /// <summary>Applies damage. Returns HP actually removed (for Titan damage scoring).</summary>
        public float ApplyDamage(float amount, bool directHit, bool splash = false)
        {
            if (_broken || _outOfBounds || amount <= 0f) return 0f;
            float mult = (!splash && isSteelJoint && material != null) ? material.jointDamageMultiplier : 1f;
            float dmg = amount * mult;
            if (indestructible) { Damaged?.Invoke(this, 0f, directHit); return 0f; }
            float removed = Mathf.Min(currentHp, dmg);
            currentHp -= dmg;
            UpdateDamageVisual();
            if (currentHp > 0f) _crackStep = CrackSteps.Advance(_crackStep, CrackSteps.StepFor(CrackSteps.DamagePermille(currentHp, MaxHp)), s => ImpactReport.Crack(this, s));
            Damaged?.Invoke(this, removed, directHit);
            if (currentHp <= 0f) Break();
            return removed;
        }

        void UpdateDamageVisual()
        {
            // Art rule: damaged = darker, max 2 steps
            int step = currentHp < MaxHp * 0.34f ? 2 : currentHp < MaxHp * 0.67f ? 1 : 0;
            if (step == _damageStep || material == null) return;
            _damageStep = step;
            ApplyColor(Color.Lerp(_baseColor, material.darkColor, step * 0.45f));
        }

        public void Break()
        {
            if (_broken) return;
            _broken = true;
            currentHp = 0f;
            ImpactReport.Break(this);
            DestroyOwnJoints();
            if (DebrisPool.Instance != null && material != null)
                {
                int chunks = Crashverse.Feel.DebrisBudget.Chunks(material.kind == MaterialKind.Wood ? "wood" : material.kind == MaterialKind.Brick ? "brick" : "steel", Crashverse.Perf.QualityController.FeelTier);
                if (chunks > 0) DebrisPool.Instance.SpawnBurst(transform.position, transform.localScale, material, Mathf.Min(chunks, material.maxDebrisChunks)); // visual only; mass counted on this piece
            }
            if (_renderer != null) _renderer.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Body.isKinematic = true;
            Body.detectCollisions = false;
            var mp = GetComponent<MovingPart>(); if (mp != null) mp.enabled = false;
            Broken?.Invoke(this);
            if (explosive) Explosion.Detonate(transform.position, 2.5f, 80f, this); // Explosion source (scope pushed inside)
            NotifyDependents();
        }

        public void ForceCollapse()
        {
            if (_collapsed || _broken || indestructible && !isObjectivePiece) return;
            _collapsed = true;
            ImpactReport.Collapse(this);
            DestroyOwnJoints();
            var mp = GetComponent<MovingPart>(); if (mp != null) mp.enabled = false;
            Body.isKinematic = false;
            Body.WakeUp();
            Collapsed?.Invoke(this);
            NotifyDependents();
        }

        /// <summary>Called by the KillPlane when the piece falls off the floating island.</summary>
        public void MarkOutOfBounds()
        {
            if (_outOfBounds) return;
            _outOfBounds = true;
            Body.isKinematic = true;
            Body.detectCollisions = false;
            if (_renderer != null) _renderer.enabled = false;
            WentOutOfBounds?.Invoke(this);
            NotifyDependents();
        }

        void DestroyOwnJoints()
        {
            foreach (var j in GetComponents<Joint>()) if (j != null) Destroy(j);
        }

        void NotifyDependents()
        {
            var deps = _dependents.ToArray();
            foreach (var d in deps) if (d != null) d.EvaluateSupport();
        }

        public bool SupportLost
        {
            get
            {
                int remaining = 0;
                foreach (var s in _supports) if (s != null && !s._broken && !s._collapsed && !s._outOfBounds) remaining++;
                return SupportRule.ShouldCollapse(_supports.Count, remaining); // ALL supports gone; none at all = grounded
            }
        }

        public void EvaluateSupport()
        {
            if (_broken || _collapsed || _outOfBounds) return;
            if (SupportLost) ForceCollapse();
        }

        /// <summary>
        /// Falling pieces / debris damage what they hit (chain collapses, DP L4; Level Rulings v1 §7). Only OnCollisionEnter
        /// (an impact) is considered; resting contact (Stay) never damages. Damage is queued and applied at the start of the
        /// next sim tick in stable order (DebrisDamageQueue), not in PhysX callback order.
        /// </summary>
        void OnCollisionEnter(Collision c)
        {
            if (_broken || c.rigidbody == null || c.rigidbody.isKinematic) return;
            if (c.gameObject.GetComponent<Crashverse.Weapons.Projectile>() != null) return; // projectile applies its own damage
            var hitBlock = c.gameObject.GetComponent<DestructibleBlock>();
            Vector3 cp = c.contactCount > 0 ? c.GetContact(0).point : transform.position, cn = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.up;
            ImpactReport.Collision(this, hitBlock, c.collider, c.impulse.magnitude, c.relativeVelocity.magnitude, cp, cn, ImpactSourceKind.Debris);
            int speed = DebrisDamage.ToMmPerS(c.relativeVelocity.magnitude);
            int mult = material != null ? material.debrisDamageTakenPermille : DebrisMaterialMult.Neutral; // ruling #7a: TARGET (this piece) material
            int milli = DebrisDamage.ComputeMilliHp(speed, DebrisDamage.ToGrams(c.rigidbody.mass), mult, false, DebrisDamageQueue.Config);
            if (milli <= 0) return;
            var other = c.gameObject.GetComponent<DestructibleBlock>();
            DebrisDamageQueue.Enqueue(this, other != null ? other.blockId : "~" + c.gameObject.name, milli);
        }

        void OnJointBreak(float breakForce)
        {
            if (_broken) return;
            DestructibleBlock other = null;
            foreach (var j in GetComponents<Joint>()) if (j != null && j.currentForce.magnitude >= j.breakForce * 0.999f && j.connectedBody != null) { other = j.connectedBody.GetComponent<DestructibleBlock>(); break; }
            ImpactReport.JointSnap(this, other, breakForce);
            using (ImpactSourceScope.Push(ImpactSourceKind.Debris)) ApplyDamage(MaxHp * 0.5f, false, splash: true);
        }

        public void SetHighlight(Color c, float amount)
        {
            ApplyColor(Color.Lerp(_baseColor, c, Mathf.Clamp01(amount)));
        }

        public void ClearHighlight() => ApplyColor(_baseColor);

        void ApplyColor(Color c)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) return;
            var block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            if (material != null) block.SetFloat("_Smoothness", material.smoothness);
            _renderer.SetPropertyBlock(block);
        }
    }

    public static class Explosion
    {
        /// <summary>Radius inclusive, centre-to-centre (SG-08).</summary>
        public static void Detonate(Vector3 at, float radius, float damage, DestructibleBlock source,
                                    ImpactSourceKind kind = ImpactSourceKind.Explosion, int sourceId = 0)
        {
            using var _ = ImpactSourceScope.Push(kind, sourceId); // barrels/explosive pieces = Explosion; Gravity Well burst = PowerUp 3
            foreach (var c in Physics.OverlapSphere(at, radius))
            {
                var b = c.GetComponentInParent<DestructibleBlock>();
                if (b == null || b == source || b.IsBroken) continue;
                if ((b.transform.position - at).magnitude > radius + 1e-3f) continue;
                b.ApplyDamage(damage, false, splash: true);
                if (!b.IsBroken && b.Body != null && !b.Body.isKinematic) b.Body.AddExplosionForce(600f, at, radius * 1.5f);
            }
        }
    }
}
