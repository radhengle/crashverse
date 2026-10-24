using System.Collections.Generic;
using Crashverse.Data;
using Crashverse.Destruction;
using Crashverse.Meta;
using UnityEngine;

namespace Crashverse.PowerUps
{
    /// <summary>Effect parameters for one shot, built from powerups.json (never hard-coded). Power-ups v2 spec.</summary>
    public class ShotModifiers
    {
        public bool shockwave; public float shockDamage; public int shockDepth = 1; public float shockRadius; // Earthquake (+ radial impulse)
        public bool split; public int splitCount = 3; public float splitDamageMult = 0.5f;                     // Multi Shot
        public bool gravityWell; public float gwRadius, gwDuration, gwBurst; public int gwTicks;                // Black Hole
        public bool megaBomb; public float megaRadius, megaDamage;                                             // Mega Bomb (replaces Chain Reactor)
        public bool pierce; public int pierceCount = 2;                                                        // Laser Beam
        public bool guided; public float turnRate; public int steerTicks;                                      // Guided Rocket
        public bool shield; public float shieldThreshold = 0.1f;                                               // Shield (resolved by GameSession after settle)
        public bool Any => shockwave || split || gravityWell || megaBomb || pierce || guided || shield;

        public ShotModifiers CloneWithoutSplit()
        {
            var m = (ShotModifiers)MemberwiseClone();
            m.split = false;
            return m;
        }

        public void Apply(PowerUpData p)
        {
            var q = p.p;
            switch (p.effect)
            {
                case "SHOCKWAVE": shockwave = true; shockDamage = q.damage; shockDepth = Mathf.Max(1, q.linkDepth); shockRadius = q.radius; break;
                case "SPLIT": split = true; splitCount = Mathf.Max(2, q.count); splitDamageMult = q.damageMult; break;
                case "GRAVITY_WELL": gravityWell = true; gwRadius = q.radius; gwDuration = q.duration; gwBurst = q.burstDamage; gwTicks = PowerUpRulesV2.DurationTicks(q); break;
                case "MEGA_BOMB": megaBomb = true; megaRadius = q.radius; megaDamage = q.burstDamage; break;
                case "PHASE_PIERCE": pierce = true; pierceCount = Mathf.Max(0, q.passCount); break;
                case "GUIDED": guided = true; turnRate = q.turnRate; steerTicks = q.steerTicks; break;
                case "SHIELD": shield = true; shieldThreshold = q.threshold > 0f ? q.threshold : 0.1f; break;
            }
        }
    }

    /// <summary>
    /// Per-level charges for the 3 equipped tiles (DP §2). Arming a tile modifies the NEXT shot and does not cost a shot;
    /// the charge is consumed when that shot fires. Time Freeze activates immediately (200 sim ticks at 0.25x for moving parts).
    /// Retry restores charges (new runtime per attempt).
    /// </summary>
    public class PowerUpRuntime
    {
        public class SlotState { public PowerUpData data; public int charges; public bool armed; }
        public readonly SlotState[] Slots = new SlotState[3];
        public bool AnyUsed { get; private set; }
        public event System.Action Changed;

        public PowerUpRuntime(Loadout lo, ContentDatabase db, int extraChargeSlot = -1)
        {
            for (int i = 0; i < 3; i++)
            {
                var p = db.GetPowerUp(lo.Tiles[i]);
                Slots[i] = p == null ? null : new SlotState { data = p, charges = p.charges + (i == extraChargeSlot ? 1 : 0) };
            }
        }

        public bool CanArm(int i) => Slots[i] != null && Slots[i].charges > 0;

        public void ToggleArm(int i)
        {
            var s = Slots[i];
            if (s == null) return;
            if (s.armed) { s.armed = false; Changed?.Invoke(); return; } // OQ-P1: free disarm before FIRE, charge kept
            if (s.charges <= 0) return;
            if (s.data.effect == "TIME_FRACTURE" && TimeFractureEffect.IsActive) return; // running; can't stack or un-arm
            if (s.data.effect == "TIME_FRACTURE")
            {
                s.charges--; AnyUsed = true;
                TimeFractureEffect.ActivateTicks(PowerUpRulesV2.DurationTicks(s.data.p), s.data.p.slowFactor); // v2: 200 ticks in every mode
            }
            else s.armed = true;
            Changed?.Invoke();
        }

        /// <summary>Called on a confirmed fire: consumes armed charges and returns the shot's modifiers.</summary>
        public ShotModifiers ConsumeForShot()
        {
            var m = new ShotModifiers();
            foreach (var s in Slots)
            {
                if (s == null || !s.armed) continue;
                s.armed = false; s.charges--; AnyUsed = true;
                m.Apply(s.data);
            }
            Changed?.Invoke();
            return m;
        }
    }

    /// <summary>Gravity Well: pull loose debris within radius for duration, then burst damage (radius inclusive).</summary>
    public class GravityWellEffect : MonoBehaviour
    {
        float _radius, _burst; int _ticks;
        /// <param name="ticks">Black Hole: 75 sim ticks (one per FixedUpdate), deterministic.</param>
        public static void Spawn(Vector3 at, float radius, int ticks, float burst)
        {
            var go = new GameObject("GravityWell");
            go.transform.position = at;
            var g = go.AddComponent<GravityWellEffect>();
            g._radius = radius; g._ticks = Mathf.Max(1, ticks); g._burst = burst;
        }

        void FixedUpdate()
        {
            _ticks--;
            var at = transform.position;
            foreach (var c in Physics.OverlapSphere(at, _radius))
            {
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                rb.AddForce((at - rb.position).normalized * 25f * rb.mass);
            }
            if (_ticks > 0) return;
            Explosion.Detonate(at, _radius, _burst, null, Crashverse.Sim.ImpactSourceKind.PowerUp, Crashverse.Sim.PowerUpIds.GravityWell);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Time Fracture (OQ-P3): starts on ARM (charge spent then; cannot be un-armed), 4 s REAL time at 25% for moving parts only
    /// (not global timeScale). If a shot is fired during the slow it lasts until that shot's first impact.
    /// </summary>
    public class TimeFractureEffect : MonoBehaviour
    {
        static TimeFractureEffect _active;
        public static readonly Crashverse.Feel.TimeFractureTimer Timer = new Crashverse.Feel.TimeFractureTimer();
        public static bool IsActive => Timer.Active;
        public static void Activate(float duration, float slow)
        {
            Timer.Arm(duration > 0 ? duration : 4f, slow > 0 ? slow : 0.25f);
            if (_active == null) _active = new GameObject("TimeFracture").AddComponent<TimeFractureEffect>();
            MovingPart.GlobalSpeedScale = Timer.SpeedScale;
        }
        /// <summary>Power-ups v2: Time Freeze for a fixed number of sim ticks (counted in FixedUpdate).</summary>
        public static void ActivateTicks(int ticks, float slow)
        {
            Timer.ArmTicks(ticks > 0 ? ticks : 200, slow > 0 ? slow : 0.25f);
            if (_active == null) _active = new GameObject("TimeFreeze").AddComponent<TimeFractureEffect>();
            MovingPart.GlobalSpeedScale = Timer.SpeedScale;
        }
        public static void NotifyShotFired() => Timer.OnShotFired();
        void FixedUpdate() { if (Timer.TickMode) { Timer.TickSim(); MovingPart.GlobalSpeedScale = Timer.SpeedScale; } }
        void OnEnable() { Crashverse.Weapons.Projectile.FirstImpact += OnImpact; }
        void OnDisable() { Crashverse.Weapons.Projectile.FirstImpact -= OnImpact; }
        void OnImpact(Crashverse.Weapons.Projectile p) => Timer.OnShotImpact();
        void Update()
        {
            Timer.Tick(Time.unscaledDeltaTime);
            MovingPart.GlobalSpeedScale = Timer.SpeedScale;
            if (!Timer.Active) { _active = null; Destroy(gameObject); }
        }
        void OnDestroy() { MovingPart.GlobalSpeedScale = 1f; if (_active == this) { _active = null; Timer.Cancel(); } }
    }
}
