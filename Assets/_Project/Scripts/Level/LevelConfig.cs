using Crashverse.Meta;
using UnityEngine;

namespace Crashverse.Level
{
    public enum ObjectiveType { DESTROY_STRUCTURE = 0, BOSS = 1 }

    /// <summary>
    /// Optional SO wrapper for a level (designer-tunable in the Editor). Runtime levels come from
    /// Resources/Data/chapter1.json (LevelEntry); both use StarRules (Contract v0.2 / SG-01).
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "Crashverse/Level Config", order = 20)]
    public class LevelConfig : ScriptableObject
    {
        public string levelId = "c1_l07";
        public string displayName = "Clockwork Tower";
        public ObjectiveType objectiveType = ObjectiveType.DESTROY_STRUCTURE;
        public int shotBudget = 5;
        [Range(0.5f, 1f)] public float winThreshold = 0.85f;
        [Range(0f, 1f)] public float star3Pct = StarRules.DefaultStar3Pct;
        [Range(0f, 1f)] public float star2Pct = StarRules.DefaultStar2Pct;

        public static LevelConfig CreateM1ClockworkTower()
        {
            var cfg = CreateInstance<LevelConfig>();
            cfg.name = "m1_clockwork_tower";
            cfg.levelId = "c1_l07";
            return cfg;
        }

        /// <summary>SG-01: percentage of shots left vs per-level thresholds (no hard-coded table).</summary>
        public int ComputeStars(bool won, int shotsLeft) =>
            StarRules.Compute(won, shotsLeft, shotBudget, star3Pct, star2Pct);
    }
}
