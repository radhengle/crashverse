using System;
using Crashverse.Data;

namespace Crashverse.Meta
{
    /// <summary>
    /// Power-ups v2 (design/Powerups_v2_Spec.md). Pure, deterministic rules shared by the client sim and tests.
    /// Effect ids in powerups.json: SHOCKWAVE (Earthquake), SPLIT (Multi Shot), GRAVITY_WELL (Black Hole), MEGA_BOMB,
    /// SHIELD, PHASE_PIERCE (Laser Beam), GUIDED (Guided Rocket), TIME_FRACTURE (Time Freeze). CHAIN_REACTOR is removed.
    /// </summary>
    public static class PowerUpRulesV2
    {
        public static readonly string[] Effects = { "SHOCKWAVE", "SPLIT", "GRAVITY_WELL", "MEGA_BOMB", "SHIELD", "PHASE_PIERCE", "GUIDED", "TIME_FRACTURE" };
        public const float SimStep = 0.02f;
        /// <summary>Shield on boss levels: refund when the shot took less than 5% of the boss's max HP.</summary>
        public const float ShieldBossThreshold = 0.05f;

        public static bool IsKnownEffect(string e) => Array.IndexOf(Effects, e) >= 0;

        /// <summary>Unlock order = order in powerups.json (spec AC1: the picker shows the 8 tiles in unlock order).</summary>
        /// <summary>Target UI §10 power-ups grid caption under each icon.</summary>
        public static string Tagline(string effect)
        {
            switch (effect)
            {
                case "MEGA_BOMB": return "Bigger Blast";
                case "SPLIT": return "3 Shots at Once";
                case "GUIDED": return "Auto Target";
                case "TIME_FRACTURE": return "Slows Time";
                case "SHIELD": return "Protects Steps";
                case "SHOCKWAVE": return "Shakes Building";
                case "PHASE_PIERCE": return "Precision Hit";
                case "GRAVITY_WELL": return "Pulls & Destroys";
                default: return "";
            }
        }
        public static PowerUpData[] InUnlockOrder(PowerUpCatalog c) => c?.powerups ?? new PowerUpData[0];

        /// <summary>Mega Bomb: peak damage at the centre, linear falloff to 0 at the radius (inclusive edge = 0).</summary>
        public static float MegaBombDamage(float distance, float radius, float peak)
        {
            if (radius <= 0f || distance >= radius) return 0f;
            if (distance <= 0f) return peak;
            return peak * (1f - distance / radius);
        }

        /// <summary>Mega Bomb also sets off explosive barrels / energy cores whose centre is within the radius (inclusive).</summary>
        public static bool MegaBombDetonates(float distance, float radius) => distance <= radius + 1e-3f;

        /// <summary>
        /// Shield ("Protects Steps"): after settle, the shot is refunded when it destroyed less than threshold of the objective
        /// mass (normal levels, threshold from data = 0.10) or less than 5% of the boss's max HP (boss levels).
        /// </summary>
        public static bool ShieldRefunds(bool bossLevel, float objectiveFractionThisShot, float bossHpFractionThisShot, float threshold)
        {
            if (bossLevel) return bossHpFractionThisShot < ShieldBossThreshold;
            return objectiveFractionThisShot < (threshold > 0f ? threshold : 0.1f);
        }

        /// <summary>Guided Rocket: max turn per sim tick (deg) from turnRate (deg/s).</summary>
        public static float MaxTurnPerTick(float turnRateDegPerSec) => Math.Max(0f, turnRateDegPerSec) * SimStep;

        /// <summary>Clamp the requested yaw change for one tick; 0 once the steering window (steerTicks after launch) is over.</summary>
        public static float SteerForTick(float requestedDeg, int ticksSinceLaunch, int steerTicks, float turnRateDegPerSec)
        {
            if (ticksSinceLaunch < 0 || ticksSinceLaunch >= steerTicks) return 0f;
            float m = MaxTurnPerTick(turnRateDegPerSec);
            return requestedDeg > m ? m : requestedDeg < -m ? -m : requestedDeg;
        }

        /// <summary>Quantize a steering value to the 1/1000 fixed point the input recorder stores (replay = same value).</summary>
        public static float Quantize(float deg) => (float)Math.Round(deg * 1000.0) / 1000f;

        /// <summary>Time Freeze: 200 ticks from arm; ends earlier at the fired shot's impact tick.</summary>
        public static int TimeFreezeEndTick(int armTick, int durationTicks, int impactTick)
        {
            int end = armTick + Math.Max(1, durationTicks);
            return impactTick >= armTick && impactTick < end ? impactTick : end;
        }

        /// <summary>Ticks for a power-up's timed effect: durationTicks if set, else the legacy seconds value.</summary>
        public static int DurationTicks(PowerUpParams p) => p == null ? 0 : p.durationTicks > 0 ? p.durationTicks : (int)Math.Round(p.duration / SimStep);
    }
}
