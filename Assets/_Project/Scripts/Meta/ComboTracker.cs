namespace Crashverse.Meta
{
    /// <summary>
    /// Addendum v1.1 §2: +1 per ObjectivePiece destroyed/out-of-bounds within 0.8s (inclusive) of the previous break.
    /// Starts at the first break of a shot, ends after a >0.8s gap, resets every shot. Feedback + score only.
    /// </summary>
    public class ComboTracker
    {
        public const float Window = 0.8f;
        /// <summary>Window in 50 Hz sim ticks (0.8 s = 40 ticks), inclusive. Integer twin of Window for tick-driven play.</summary>
        public const int WindowTicks = 40;
        int _lastBreakTick = int.MinValue;
        public const int ShowFrom = 3;
        public int Current { get; private set; }
        public int BestThisRun { get; private set; }
        float _lastBreak = float.NegativeInfinity;

        public void OnShot() { Current = 0; _lastBreak = float.NegativeInfinity; _lastBreakTick = int.MinValue; }
        public void ResetRun() { OnShot(); BestThisRun = 0; }

        /// <returns>The combo count after this break.</returns>
        public int OnBreak(float time)
        {
            Current = (Current > 0 && time - _lastBreak <= Window) ? Current + 1 : 1; // BUG-10: strictly <= 0.8 s, no tolerance
            _lastBreak = time;
            if (Current > BestThisRun) BestThisRun = Current;
            return Current;
        }

        /// <summary>Tick-based OnBreak (no float time): +1 when within WindowTicks (inclusive) of the previous break.</summary>
        public int OnBreakTick(int tick)
        {
            Current = (Current > 0 && _lastBreakTick != int.MinValue && (long)tick - _lastBreakTick <= WindowTicks) ? Current + 1 : 1;
            _lastBreakTick = tick;
            if (Current > BestThisRun) BestThisRun = Current;
            return Current;
        }

        public bool Visible => Current >= ShowFrom;
        /// <summary>Pop tier: 0 none, 1 from x3, 2 from x5, 3 from x10, 4 from x20.</summary>
        public static int PopTier(int n) => n >= 20 ? 4 : n >= 10 ? 3 : n >= 5 ? 2 : n >= 3 ? 1 : 0;
    }

    /// <summary>Addendum §3: win only; 1000×%destroyed + 500×shotsLeft + 50×bestCombo + 1000 mastery.</summary>
    public static class CampaignScore
    {
        public static int Compute(bool won, float destroyedRatio, int shotsLeft, int bestCombo, bool mastery)
        {
            if (!won) return 0;
            float r = destroyedRatio < 0 ? 0 : destroyedRatio > 1 ? 1 : destroyedRatio;
            // BUG-05: floor with exact decimal maths (0.8739 → 873, 0.9999 → 999; no float drift such as 0.7f → 699)
            return (int)decimal.Floor((decimal)r * 1000m) + 500 * System.Math.Max(0, shotsLeft) + 50 * System.Math.Max(0, bestCombo) + (mastery ? 1000 : 0);
        }
    }

    /// <summary>Addendum §1: free hint from the 2nd consecutive fail; never in ranked Titan; needs hint data.</summary>
    public static class HintRules
    {
        public static bool ShowHintButton(Crashverse.Data.LevelEntry level, int consecutiveFails, bool rankedTitan) =>
            !rankedTitan && level != null && level.HasHint && consecutiveFails >= 2;
    }
}
