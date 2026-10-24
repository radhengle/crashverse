using System;
using Crashverse.Data;

namespace Crashverse.Meta
{
    /// <summary>
    /// Target UI §8: a Daily Boss level seeded by the device's LOCAL date (offline build; no server). The same date always gives
    /// the same arena, boss name and step budget. The boss HP bar is the share of the win threshold still standing.
    /// </summary>
    public static class DailyBossRules
    {
        public static string DateKey(DateTime localDate) => localDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>FNV-1a 32-bit: stable across runtimes (string.GetHashCode is randomised per process).</summary>
        public static uint Seed(string key)
        {
            uint h = 2166136261;
            foreach (char c in key ?? "") { h ^= c; h *= 16777619; }
            return h;
        }

        public static LevelEntry ForDate(string dateKey, DailyBossData d)
        {
            if (d == null || d.layouts == null || d.layouts.Length == 0) return null;
            uint s = Seed("dailyboss:" + dateKey);
            int li = (int)(s % (uint)d.layouts.Length);
            int ni = d.names != null && d.names.Length > 0 ? (int)((s / 7u) % (uint)d.names.Length) : 0;
            int shots = d.baseShots + (int)((s / 131u) % (uint)Math.Max(1, d.shotSpread + 1));
            return new LevelEntry
            {
                id = "daily_" + dateKey, number = 0, name = d.names != null && d.names.Length > 0 ? d.names[ni] : "Daily Boss", layout = d.layouts[li],
                objective = "DESTROY_STRUCTURE", shots = shots, winThreshold = d.winThreshold > 0 ? d.winThreshold : 0.85f,
                materials = new[] { "wood", "brick", "steel" }, forced = new SlotRule[0], lockedSlots = new string[0], requires = "", bossId = "",
                reward = new Reward[0], mastery = null,
            };
        }

        /// <summary>1 = untouched, 0 = defeated (destroyed ratio reached the win threshold).</summary>
        public static float HpFraction(float destroyedRatio, float winThreshold)
        {
            float t = winThreshold > 0f ? winThreshold : 0.85f;
            float f = 1f - destroyedRatio / t;
            return f < 0f ? 0f : f > 1f ? 1f : f;
        }
    }
}
