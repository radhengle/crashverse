using System.Collections.Generic;
using Crashverse.Destruction;
using Crashverse.PowerUps;
using Crashverse.Sim;
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
        int _launchTick; // sim tick at Launch; life/age are measured in ticks (0.02 s)
        bool _spent, _splitDone; float _splitAt;
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
            _launchTick = Crashverse.Sim.SimRunner.NowTick; _splitAt = splitAtSeconds; Latest = this;
            if (_rb == null) Awake();
            // Interpolated body: moving only the transform leaves the physics body at the spawn origin (it then hit the island
            // at x≈0 on the first tick, L1HINT run 40066075213). Move the body itself, then the transform.
            _rb.position = pos; transform.position = pos; _rb.angularVelocity = Vector3.zero;
            damage = dmg; mods = m ?? new ShotModifiers();
            _rb.velocity = vel;
            _spent = false; _splitDone = !mods.split;
            if (_col != null) _col.isTrigger = mods.pierce; // Phase Piercer passes through pieces
            Trace($"launch tick={_launchTick}");
        }

        /// <summary>Ticks since Launch (0.02 s each).</summary>
        int AgeTicks => Crashverse.Sim.SimRunner.NowTick - _launchTick;

        void FixedUpdate()
        {
            int age = AgeTicks;
            if (age >= Crashverse.Sim.SimTime.SecondsToTicks(lifeSeconds) || (KillPlane.Instance != null && transform.position.y < KillPlane.Instance.killY)) { Despawn(age >= Crashverse.Sim.SimTime.SecondsToTicks(lifeSeconds) ? "life" : "killplane"); return; }
            if (!_splitDone && (_splitAt >= 0f ? age >= Crashverse.Sim.SimTime.SecondsToTicks(_splitAt) : _rb.velocity.y <= 0f)) Split();
        }

        /// <summary>Splitstrike: at apex, 3 projectiles each doing 50% of the shot's final damage (SG-08).</summary>
        void Split()
        {
            _splitDone = true;
            Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.ProjSplitPoint); // spec 05: split point (apex or 70% flight), no sound
            Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.PuSplit);
            float d = damage * mods.splitDamageMult;
            var child = mods.CloneWithoutSplit();
            int extra = Mathf.Max(0, mods.splitCount - 1);
            for (int i = 0; i < extra; i++)
            {
                float ang = (i % 2 == 0 ? 1 : -1) * splitSpreadDeg * (1 + i / 2);
                var go = FireController.CreateProjectileObject(transform.localScale.x);
                var p = go.GetComponent<Projectile>();
                p.splitSpreadDeg = splitSpreadDeg; p._isSplitChild = true;
                p.Launch(transform.position, Quaternion.AngleAxis(ang, Vector3.up) * _rb.velocity, d, child);
            }
            damage = d; mods = child; _isSplitChild = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_spent || !mods.pierce) return;
            var b = other.GetComponentInParent<DestructibleBlock>();
            if (b == null) { Trace($"trigger '{other.name}' (root '{other.transform.root.name}')"); Impact(null, transform.position); return; } // ground
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
            var hb = c.collider.GetComponentInParent<DestructibleBlock>();
            Vector3 cp = c.contactCount > 0 ? c.GetContact(0).point : transform.position, cn = c.contactCount > 0 ? c.GetContact(0).normal : -transform.forward;
            Trace($"collide '{c.collider.name}' (root '{c.collider.transform.root.name}') block={(hb != null ? hb.name : "-")} at {cp}");
            if (hb != null) ImpactReport.Collision(hb, null, c.collider, c.impulse.magnitude, c.relativeVelocity.magnitude, cp, cn, ShotSource, ShotSourceId);
            Impact(hb, cp);
        }

        /// <summary>Direct-hit source: Cannon for plain shots; PowerUp+id when the hit itself is a power-up effect (Phase Piercer 3rd piece, Splitstrike child).</summary>
        ImpactSourceKind ShotSource => mods.pierce || _isSplitChild || mods.guided ? ImpactSourceKind.PowerUp : ImpactSourceKind.Cannon;
        int ShotSourceId => mods.pierce ? PowerUpIds.PhasePiercer : _isSplitChild ? PowerUpIds.Splitstrike : mods.guided ? PowerUpIds.GuidedRocket : PowerUpIds.None;
        bool _isSplitChild;

        void Impact(DestructibleBlock hit, Vector3 at)
        {
            _spent = true;
            _splitDone = true; // OQ-P2: hit before the split point → no split, full single-shot damage (charge already spent at fire)
            FirstImpact?.Invoke(this);
            if (hit != null)
            {
                if (_pierced.Count > 0 && mods.pierce && _rb != null) // trigger hit: no contact impulse, use momentum
                    ImpactReport.Collision(hit, null, null, _rb.mass * _rb.velocity.magnitude, _rb.velocity.magnitude, at, -_rb.velocity.normalized, ShotSource, ShotSourceId);
                using (ImpactSourceScope.Push(ShotSource, ShotSourceId)) hit.ApplyDamage(damage, directHit: true); // steel joints take 2x here
                if (mods.shockwave) using (ImpactSourceScope.Push(ImpactSourceKind.PowerUp, PowerUpIds.Shockwave)) Shockwave(hit);
            }
            if (mods.shockwave && mods.shockRadius > 0f) EarthquakeImpulse(at); // v2 Earthquake: radial impulse within R (4 m)
            if (mods.megaBomb) using (ImpactSourceScope.Push(ImpactSourceKind.PowerUp, PowerUpIds.MegaBomb)) MegaBomb(at);
            if (mods.gravityWell) GravityWellEffect.Spawn(at, mods.gwRadius, mods.gwTicks, mods.gwBurst);
            Despawn(hit != null ? "hit " + hit.name : "impact");
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

        /// <summary>Earthquake (v2): radial impulse on loose bodies within shockRadius (inclusive), same model as explosions.</summary>
        void EarthquakeImpulse(Vector3 at)
        {
            foreach (var c in Physics.OverlapSphere(at, mods.shockRadius))
            {
                var rb = c.attachedRigidbody; if (rb == null || rb.isKinematic) continue;
                if ((rb.position - at).magnitude > mods.shockRadius + 1e-3f) continue;
                rb.AddExplosionForce(400f, at, mods.shockRadius, 0.5f);
            }
        }

        /// <summary>
        /// Mega Bomb (v2, replaces Chain Reactor): radial blast set off on impact, megaDamage at the centre falling off linearly to 0 at
        /// megaRadius (3.5 m). Explosive barrels and energy cores whose centre is inside the radius detonate. Pieces are visited in a
        /// stable order (by name) so the result is deterministic.
        /// </summary>
        void MegaBomb(Vector3 at)
        {
            Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.ShakeRouter.PuChainPulse);
            var seen = new HashSet<DestructibleBlock>(); var list = new List<DestructibleBlock>();
            foreach (var c in Physics.OverlapSphere(at, mods.megaRadius))
            {
                var b = c.GetComponentInParent<DestructibleBlock>();
                if (b != null && !b.IsBroken && seen.Add(b)) list.Add(b);
            }
            list.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            foreach (var b in list)
            {
                if (b.IsBroken) continue;
                float d = (b.transform.position - at).magnitude;
                if (b.explosive && Crashverse.Meta.PowerUpRulesV2.MegaBombDetonates(d, mods.megaRadius)) { b.Break(); continue; }
                float dmg = Crashverse.Meta.PowerUpRulesV2.MegaBombDamage(d, mods.megaRadius, mods.megaDamage);
                if (dmg <= 0f) continue;
                b.ApplyDamage(dmg, false, splash: true);
                if (!b.IsBroken && b.Body != null && !b.Body.isKinematic) b.Body.AddExplosionForce(600f, at, mods.megaRadius * 1.5f);
            }
        }

        /// <summary>Guided Rocket (v2): rotate the flight direction about world up by deg (already clamped per tick and recorded).</summary>
        public void Steer(float deg)
        {
            if (!mods.guided || _spent || _rb == null || deg == 0f) return;
            _rb.velocity = Quaternion.Euler(0f, -deg, 0f) * _rb.velocity; // same sign as AimController: +yaw = toward world +z
        }
        public int TicksSinceLaunch => AgeTicks;
        public bool IsSpent => _spent;

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void Trace(string msg) => Debug.Log($"CVPROJ {msg} pos={transform.position} v={(_rb != null ? _rb.velocity : Vector3.zero)} age={AgeTicks}");

        void Despawn(string why = "")
        {
            Trace("despawn " + why);
            gameObject.SetActive(false);
            Destroy(gameObject, 0.05f);
        }
    }
}
