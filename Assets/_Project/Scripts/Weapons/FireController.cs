using Crashverse.Destruction;
using Crashverse.PowerUps;
using UnityEngine;

namespace Crashverse.Weapons
{
    /// <summary>A confirmed fire consumes one shot (GameSession). Aim/cancel never does.</summary>
    public class FireController : MonoBehaviour
    {
        public AimController aim;
        public float defaultDamage = 60f;
        public float projectileScale = 0.45f;
        public float projectileMass = 0.8f;
        public float splitSpreadDeg = 8f;
        public bool CanFire { get; set; } = true;

        /// <summary>
        /// Animation Spec 01 (Developer-accepted): the breech anticipation (ANIM_WRECKER_FIRE_ANTICIPATE) plays for 66 ms (4 f) on the
        /// FIRE tap, then the projectile launches together with ANIM_WRECKER_FIRE. Shot accounting stays on the tap (GameSession);
        /// launch velocity/position and the split time are frozen at the tap so the shot is exactly what the player aimed.
        /// Uses scaled time, so a pause during the 66 ms holds the launch. Set to 0 to launch on the tap (old behaviour).
        /// </summary>
        public float launchDelayMs = Crashverse.Feel.WreckerAnimStateMachine.AnticipationMs;

        /// <summary>Projectiles in flight plus a launch still waiting out the anticipation (keeps the reload gate closed meanwhile).</summary>
        public static int ActiveProjectiles => Projectile.Alive.Count + _pendingCount;
        static int _pendingCount;

        struct PendingLaunch { public Vector3 pos, vel; public ShotModifiers mods; public float splitAt, leftMs; public System.Action onLaunched; }
        PendingLaunch _pending; bool _hasPending;
        public bool LaunchPending => _hasPending;

        /// <summary>Raised on the FIRE tap (shot confirmed).</summary>
        public event System.Action Fired;
        /// <summary>Raised when the projectile actually leaves the muzzle (tap + launchDelayMs).</summary>
        public event System.Action Launched;

        /// <param name="onLaunched">Invoked at the real launch (tap + delay), e.g. to record the input tick the server replays.</param>
        public bool Fire(ShotModifiers mods, System.Action onLaunched = null)
        {
            if (!CanFire || aim == null || _hasPending) return false;
            var v = aim.LaunchVelocity; // fixed power (OQ-A1), from the TARGET aim (not the smoothed barrel)
            var pos = aim.SpawnPosition;
            float splitAt = mods.split ? Crashverse.Feel.SplitPlanner.SplitTime(v.y, -Physics.gravity.y, aim.PredictedFlightTime) : -1f;
            aim.CancelAim();
            Fired?.Invoke();
            if (launchDelayMs <= 0f) { Launch(pos, v, mods, splitAt); onLaunched?.Invoke(); return true; }
            aim.SnapVisual(launchDelayMs / 1000f); // barrel catches up with the aim during the anticipation (01)
            _pending = new PendingLaunch { pos = pos, vel = v, mods = mods, splitAt = splitAt, leftMs = launchDelayMs, onLaunched = onLaunched };
            _hasPending = true; _pendingCount++;
            return true;
        }

        void Update()
        {
            if (!_hasPending) return;
            _pending.leftMs -= Time.deltaTime * 1000f;
            if (_pending.leftMs > 0f) return;
            var p = _pending; ClearPending();
            Launch(p.pos, p.vel, p.mods, p.splitAt);
            p.onLaunched?.Invoke();
        }

        /// <summary>Drops a launch that hasn't left the muzzle yet (Retry / run teardown).</summary>
        public void CancelPending() { if (_hasPending) ClearPending(); }
        void ClearPending() { _hasPending = false; _pending = default; if (_pendingCount > 0) _pendingCount--; }
        void OnDestroy() => CancelPending();

        void Launch(Vector3 pos, Vector3 v, ShotModifiers mods, float splitAt)
        {
            var go = CreateProjectileObject(projectileScale);
            go.GetComponent<Rigidbody>().mass = projectileMass;
            var p = go.GetComponent<Projectile>();
            p.splitSpreadDeg = splitSpreadDeg;
            p.Launch(pos, v, defaultDamage, mods, splitAt);
            Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.ProjLaunch);
            Launched?.Invoke();
        }

        public static GameObject CreateProjectileObject(float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.tag = "Projectile";
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<Rigidbody>();
            go.AddComponent<Projectile>();
            var r = go.GetComponent<Renderer>();
            var b = new MaterialPropertyBlock();
            var c = MaterialDefinition.Hex(WreckerSpec.Core);
            b.SetColor("_BaseColor", c); b.SetColor("_Color", c);
            r.SetPropertyBlock(b);
            return go;
        }
    }
}
