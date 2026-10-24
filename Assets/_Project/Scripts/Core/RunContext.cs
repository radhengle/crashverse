using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Meta;

namespace Crashverse.Core
{
    public enum RunMode { Campaign, Event, TitanRanked, TitanPractice }

    /// <summary>Everything one attempt needs. Retry re-uses it (same loadout, full shots and charges).</summary>
    public class RunContext
    {
        public RunMode mode;
        public LevelEntry level;
        public LayoutData layout;
        public RuleModifiers mod = new RuleModifiers();
        public Loadout loadout = new Loadout();
        public int extraChargeSlot = -1;
        public bool hintArmed;
        public DailyTitanConfig titan;
        public RankedTicket ticket;
        /// <summary>Sim seed for this attempt (AttemptToken.seed for scored runs). Same seed + same tick-ordered inputs = same run.</summary>
        public long seed;
        /// <summary>Local Daily Boss (seeded by the device's local date; no server).</summary>
        public bool dailyBoss;
        /// <summary>Every 10th campaign level is hard: tighter steps (HardLevelRules) and the dark theme.</summary>
        public bool IsHard => mode == RunMode.Campaign && Crashverse.Meta.HardLevelRules.IsHard(level);
        public int ShotBudget => System.Math.Max(1, (IsHard ? Crashverse.Meta.HardLevelRules.Shots(level) : level.shots) + (mode == RunMode.TitanRanked || mode == RunMode.TitanPractice ? 0 : mod.shotDelta));
        public bool IsTitan => mode == RunMode.TitanRanked || mode == RunMode.TitanPractice;

        /// <summary>Daily Titan uses the normal pipeline with a synthetic level entry built from the server config.</summary>
        public static LevelEntry TitanLevel(DailyTitanConfig c) => new LevelEntry
        {
            id = "titan_" + c.dayKey, number = 0, name = c.name + " · " + c.arenaName, layout = c.layout, objective = "DESTROY_STRUCTURE",
            shots = c.shots, winThreshold = 0.85f, materials = new[] { "wood", "brick", "steel" }, mastery = c.mastery,
            forced = new SlotRule[0], lockedSlots = new string[0], requires = "", bossId = "", reward = new Reward[0],
            hintTarget = c.hintTarget, hintYaw = c.hintYaw, hintPitch = c.hintPitch // BUG-08: practice hint
        };
    }

    public class RunResult
    {
        public bool won; public int stars; public bool mastery; public int score; public int bestCombo; public float ratio;
        public int shotsLeft; public bool firstClear; public int titanScore; public SubmitResponse titanSubmit; public string note;
    }
}
