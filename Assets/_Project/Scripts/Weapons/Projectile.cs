using System.Collections.Generic;
using Crashverse.Destruction;
using Crashverse.PowerUps;
using UnityEngine;

namespace Crashverse.Weapons
{
    /// <summary>Default shot + power-up effects (DP §2, SG-08). Values come from powerups.json via ShotModifiers.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public static readonly List<Projectile> Alive = new List<Projectile>();
        public float damage = 60f;
        public ShotModifiers mods = new ShotModifiers();
        public float lifeSeconds = 8f;
        public float splitSpreadDeg = 8f;

        Rigidbody _rb;
        Collider _col;
        float _life;
        bool _spent, _splitDone; float _age, _splitAt;
        /// <summary>First impact of any projectile (Time Fracture hold-until-impact, camera ImpactFrame).</summary>
        public static event System.Action<Projectile> FirstImpact;
        public static Projectile Latest;
        readonly HashSet<DestructibleBlock> _pierced = new HashSet<DestructibleBlock>();

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void OnEnable() { Alive.Add(this); }
        void OnDisable() { Alive.Remove(this); }

        /// <param name="splitAtSeconds">OQ-P2: min(apex, 70% predicted flight); &lt;0 = use apex.</param>
        public void Launch(Vector3 pos, Vector3 vel, float dmg, ShotModifiers m, float splitAtSeconds = -1f)
        {
            _age = 0f; _splitAt = splitAtSeconds; Latest = this;
            if (_rb == null) Awake();
            transform.position = pos;
            damage = dmg; mods = m ?? new ShotModifiers();
            _rb.velocity = vel;
            _life = lifeSeconds; _spent = false; _splitDone = !mods.split;
            if (_col != null) _col.isTrigger = mods.pierce; // Phase Piercer passes through pieces
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f || (KillPlane.Instance != null && transform.position.y < KillPlane.Instance.killY)) { Despawn(); return; }
            _age += Time.deltaTime;
            if (!_splitDone && (_splitAt >= 0f ? _age >= _splitAt : _rb.velocity.y <= 0f)) Split();
        }

        /// <summary>Splitstrike: at apex, 3 projectiles each doing 50% of the shot's final damage (SG-08).</summary>
        void Split()
        {
            _splitDone = true;
            Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.PuSplitApex);
            float d = damage * mods.splitDamageMult;
            var child = mods.CloneWithoutSplit();
            int extra = Mathf.Max(0, mods.splitCount - 1);
            for (int i = 0; i < extra; i++)
            {
                float ang = (i % 2 == 0 ? 1 : -1) * splitSpreadDeg * (1 + i / 2);
                var go = FireController.CreateProjectileObject(transform.localScale.x);
                var p = go.GetComponent<Projectile>();
                p.splitSpreadDeg = splitSpreadDeg;
                p.Launch(transform.position, Quaternion.AngleAxis(ang, Vector3.up) * _rb.velocity, d, child);
            }
            damage = d; mods = child;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_spent || !mods.pierce) return;
            var b = other.GetComponentInParent<DestructibleBlock>();
            if (b == null) { Impact(null, transform.position); return; } // ground
            if (b.IsBroken || !_pierced.Add(b)) return;
            if (_pierced.Count > mods.pierceCount)
            {
                Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.PuPhaseHit, b); // 3rd piece: solidify + full damage (04)
                Impact(b, transform.position);
            }
            else Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.PuPhasePass, b); // pass-through 1 & 2: ripple + shimmer, no hit stop
        }

        void OnCollisionEnter(Collision c)
        {
            if (_spent) return;
            Impact(c.collider.GetComponentInParent<DestructibleBlock>(), c.contacts.Length > 0 ? c.contacts[0].point : transform.position);
        }

        void Impact(DestructibleBlock hit, Vector3 at)
        {
            _spent = true;
            _splitDone = true; // OQ-P2: hit before the split point → no split, full single-shot damage (charge already spent at fire)
            FirstImpact?.Invoke(this);
            if (hit != null)
            {
                hit.ApplyDamage(damage, directHit: true); // steel joints take 2x here
                if (mods.shockwave) Shockwave(hit);
            }
            if (mods.chainReactor) ChainReactor(at);
            if (mods.gravityWell) GravityWellEffect.Spawn(at, mods.gwRadius, mods.gwDuration, mods.gwBurst);
            Despawn();
        }

        /// <summary>40 splash to every piece connected 1 support link deep; not multiplied by the joint bonus (SG-08).</summary>
        void Shockwave(DestructibleBlock hit)
        {
            var visited = new HashSet<DestructibleBlock> { hit };
            var frontier = new List<DestructibleBlock> { hit };
            for (int depth = 0; depth < Mathf.Max(1, mods.shockDepth); depth++)
            {
                var next = new List<DestructibleBlock>();
                foreach (var f in frontier)
                {
                    foreach (var n in f.Supports) if (n != null && visited.Add(n)) next.Add(n);
                    foreach (var n in f.Dependents) if (n != null && visited.Add(n)) next.Add(n);
                }
                foreach (var n in next) n.ApplyDamage(mods.shockDamage, directHit: false, splash: true);
                frontier = next;
            }
        }

        /// <summary>Detonates explosive pieces within radius (inclusive, centre to centre).</summary>
        void ChainReactor(Vector3 at)
        {
            foreach (var c in Physics.OverlapSphere(at, mods.chainRadius))
            {
                var b = c.GetComponentInParent<DestructibleBlock>();
                if (b != null && b.explosive && !b.IsBroken && (b.transform.position - at).magnitude <= mods.chainRadius + 1e-3f) b.Break();
            }
        }

        void Despawn()
        {
            gameObject.SetActive(false);
            Destroy(gameObject, 0.05f);
        }
    }
}
