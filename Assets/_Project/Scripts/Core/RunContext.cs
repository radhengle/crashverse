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
        public int ShotBudget => System.Math.Max(1, level.shots + (mode == RunMode.TitanRanked || mode == RunMode.TitanPractice ? 0 : mod.shotDelta));
        public bool IsTitan => mode == RunMode.TitanRanked || mode == RunMode.TitanPractice;

        /// <summary>Daily Titan uses the normal pipeline with a synthetic level entry built from the server config.</summary>
        public static LevelEntry TitanLevel(DailyTitanConfig c) => new LevelEntry
        {
            id = "titan_" + c.dayKey, number = 0, name = c.name + " · " + c.arenaName, layout = c.layout, objective = "DESTROY_STRUCTURE",
            shots = c.shots, winThreshold = 0.85f, materials = new[] { "wood", "brick", "steel" }, mastery = c.mastery,
            forced = new SlotRule[0], lockedSlots = new string[0], requires = "", bossId = "", reward = new Reward[0]
        };
    }

    public class RunResult
    {
        public bool won; public int stars; public bool mastery; public int score; public int bestCombo; public float ratio;
        public int shotsLeft; public bool firstClear; public int titanScore; public SubmitResponse titanSubmit; public string note;
    }
}
