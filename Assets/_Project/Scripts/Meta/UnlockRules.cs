using Crashverse.Data;

namespace Crashverse.Meta
{
    /// <summary>DP §1 AC3: linear nodes; bridge gate after the previous island's last node; L10 needs L9.</summary>
    public static class UnlockRules
    {
        public static bool IsLevelUnlocked(LevelEntry level, CampaignSave save)
        {
            if (level == null) return false;
            if (string.IsNullOrEmpty(level.requires)) return true;
            return save.IsCleared(level.requires);
        }

        public static bool IsIslandOpen(IslandData island, CampaignSave save) =>
            string.IsNullOrEmpty(island.gateAfterLevel) || save.IsCleared(island.gateAfterLevel);

        /// <summary>Power-up tiles: "level_reached" unlocks once that level is unlocked; "reward" once it is cleared.</summary>
        public static bool IsPowerUpUnlocked(PowerUpData p, ContentDatabase db, CampaignSave save)
        {
            var lvl = db.GetLevel(p.unlockLevelId);
            if (lvl == null) return false; // future region, not in this build
            return p.unlockType == "reward" ? save.IsCleared(lvl.id) : IsLevelUnlocked(lvl, save);
        }

        public static int TotalStars(ChapterData ch, CampaignSave save)
        {
            int s = 0;
            foreach (var l in ch.levels) s += save.BestStars(l.id);
            return s;
        }
    }
}
