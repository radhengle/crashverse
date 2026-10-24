using System.Collections.Generic;
using Crashverse.Data;

namespace Crashverse.Meta
{
    /// <summary>Snapshot taken after settle when the run resolves (SG-04).</summary>
    public class RunStats
    {
        public bool won; public int shotsUsed; public int shotsLeft; public int stars; public float destroyedRatio;
        public bool powerUpUsed; public float islandKillY = -2f;
        /// <summary>L10: direct hits on the Tidewarden while its shutter was closed (phase 2).</summary>
        public int closedShutterHits;
        public readonly HashSet<string> FirstImpactRoles = new HashSet<string>();
        public readonly Dictionary<string, int> RoleTotal = new Dictionary<string, int>();
        public readonly Dictionary<string, int> RoleBroken = new Dictionary<string, int>();
        public readonly Dictionary<string, float> RoleMaxCentreY = new Dictionary<string, float>();
    }

    /// <summary>SG-03: mastery is a separate tick and never changes stars.</summary>
    public static class MasteryEvaluator
    {
        public static bool Evaluate(MasteryGoal g, RunStats r)
        {
            if (g == null || !r.won) return false;
            switch (g.type)
            {
                case "CLEAR_IN_SHOTS": return r.shotsUsed <= (int)g.value;
                case "DESTROYED_AT_LEAST": return WinRule.Meets(r.destroyedRatio, g.value); // BUG-19: basis points, no tolerance
                case "STARS": return r.stars >= (int)g.value;
                case "NO_POWERUP": return !r.powerUpUsed;
                case "SHOTS_LEFT_AT_LEAST": return r.shotsLeft >= (int)g.value;
                case "THREE_STARS_NO_ROLE_HITS": return r.stars >= 3 && !r.FirstImpactRoles.Contains(g.role);
                case "ALL_ROLE_BROKEN":
                    return r.RoleTotal.TryGetValue(g.role, out var t) && t > 0 &&
                           r.RoleBroken.TryGetValue(g.role, out var b) && b >= t;
                case "ROLE_INTACT": // >= value fraction of role pieces unbroken (HP > 0) when win fires
                    if (!r.RoleTotal.TryGetValue(g.role, out var tot) || tot == 0) return false;
                    r.RoleBroken.TryGetValue(g.role, out var br);
                    return WinRule.MeetsFraction(tot - br, tot, g.value <= 0 ? 0.8f : g.value); // BUG-19: integer bp
                case "NO_CLOSED_SHUTTER_HIT": return r.closedShutterHits == 0; // Level_Rulings_v1 §1
                case "ROLE_BELOW_HEIGHT": // SG-21: every role piece centre below level.islandKillY after settle
                    return r.RoleMaxCentreY.TryGetValue(g.role, out var y) && y < r.islandKillY;
                default: return false;
            }
        }
    }
}
