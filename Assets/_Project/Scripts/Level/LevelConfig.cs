using UnityEngine;

namespace Crashverse.Level
{
    public enum ObjectiveType
    {
        DESTROY_STRUCTURE = 0
    }

    public enum StarFormulaType
    {
        STARS_FROM_MOVES_REMAINING = 0
    }

    /// <summary>
    /// Level data per Design Contract v0.1. Stars use moves-remaining table (not % destroyed).
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "Crashverse/Level Config", order = 20)]
    public class LevelConfig : ScriptableObject
    {
        public string levelId = "m1_clockwork_tower";
        public string displayName = "Clockwork Tower";
        public ObjectiveType objectiveType = ObjectiveType.DESTROY_STRUCTURE;
        public int moveBudget = 5;
        [Range(0.5f, 1f)] public float winThreshold = 0.85f;
        public StarFormulaType starFormula = StarFormulaType.STARS_FROM_MOVES_REMAINING;

        [Header("Layout")]
        public float wreckerX = -20f;
        public float targetDistanceMin = 25f;
        public float targetDistanceMax = 40f;

        public static LevelConfig CreateM1ClockworkTower()
        {
            var cfg = CreateInstance<LevelConfig>();
            cfg.name = "m1_clockwork_tower";
            cfg.levelId = "m1_clockwork_tower";
            cfg.displayName = "Clockwork Tower";
            cfg.objectiveType = ObjectiveType.DESTROY_STRUCTURE;
            cfg.moveBudget = 5;
            cfg.winThreshold = 0.85f;
            cfg.starFormula = StarFormulaType.STARS_FROM_MOVES_REMAINING;
            cfg.wreckerX = -20f;
            cfg.targetDistanceMin = 25f;
            cfg.targetDistanceMax = 40f;
            return cfg;
        }

        /// <summary>
        /// Contract §5: on win only — 0–1 left → 1★, 2–3 → 2★, 4–5 → 3★. Fail = 0.
        /// </summary>
        public int ComputeStars(bool won, int movesRemaining)
        {
            if (!won) return 0;
            if (starFormula != StarFormulaType.STARS_FROM_MOVES_REMAINING) return 0;
            if (movesRemaining >= 4) return 3;
            if (movesRemaining >= 2) return 2;
            return 1; // 0–1
        }
    }
}
