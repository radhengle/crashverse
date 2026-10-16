namespace Crashverse.Meta
{
    /// <summary>Addendum §1: free hint from the 2nd consecutive fail; never in ranked Titan; needs hint data.</summary>
    public static class HintRules
    {
        public static bool ShowHintButton(Crashverse.Data.LevelEntry level, int consecutiveFails, bool rankedTitan) =>
            !rankedTitan && level != null && level.HasHint && consecutiveFails >= 2;
    }
}
