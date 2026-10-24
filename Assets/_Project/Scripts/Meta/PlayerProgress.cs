using System;
using System.Collections.Generic;
using System.IO;
using Crashverse.Data;
using UnityEngine;

namespace Crashverse.Meta
{
    // integration/v2: the server stays offline, so profile, wallet, chests, Daily Boss and achievement progress are local
    // (Application.persistentDataPath/progress_v2.json, same atomic write as CampaignSave). Campaign stars stay in CampaignSave.

    [Serializable] public class ProfileData { public bool created; public string name = ""; public int avatar; public int badge; }

    [Serializable]
    public class ProgressData
    {
        public int version = 1;
        public ProfileData profile = new ProfileData();
        public int coins, gems, chests, chestsOpened, powerUpsUsed, hardCleared;
        public List<string> dailyBossWins = new List<string>();
        public List<string> achievements = new List<string>();
    }

    public class PlayerProgress
    {
        public ProgressData Data { get; private set; } = new ProgressData();
        readonly string _path;
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "progress_v2.json");
        public PlayerProgress(string path = null) { _path = path ?? DefaultPath; Load(); }

        public void Load()
        {
            try { if (File.Exists(_path)) Data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(_path)) ?? new ProgressData(); }
            catch (Exception e) { Debug.Log("[Progress] corrupt save, resetting: " + e.Message); Data = new ProgressData(); }
            if (Data.profile == null) Data.profile = new ProfileData();
            if (Data.dailyBossWins == null) Data.dailyBossWins = new List<string>();
            if (Data.achievements == null) Data.achievements = new List<string>();
        }

        public void Save()
        {
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(Data));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }

        public void Grant(RewardBundle r) { if (r == null) return; Data.coins += r.coins; Data.gems += r.gems; Data.chests += r.chests; }

        /// <summary>Profile step (Target UI §2). Returns an error string, or null when saved.</summary>
        public string CreateProfile(string name, int avatar, int badge, int avatarCount, int badgeCount)
        {
            var n = ProfileRules.Clean(name);
            var err = ProfileRules.ValidateName(n);
            if (err != null) return err;
            if (avatar < 0 || avatar >= avatarCount) return "Choose an avatar";
            if (badge < 0 || badge >= badgeCount) return "Choose a badge";
            Data.profile = new ProfileData { created = true, name = n, avatar = avatar, badge = badge };
            Save();
            return null;
        }

        public bool HasDailyBossWin(string dateKey) => Data.dailyBossWins.Contains(dateKey);

        /// <summary>First Daily Boss win of a local date grants the bundle; repeats grant nothing.</summary>
        public RewardBundle RecordDailyBossWin(string dateKey, RewardBundle reward)
        {
            if (HasDailyBossWin(dateKey)) return new RewardBundle();
            Data.dailyBossWins.Add(dateKey);
            Grant(reward); Save();
            return reward ?? new RewardBundle();
        }

        /// <summary>Opens one chest with a deterministic roll (seed = chests opened so far). Returns the contents (0 if none left).</summary>
        public RewardBundle OpenChest(ChestRules rules)
        {
            if (Data.chests <= 0 || rules == null) return new RewardBundle();
            uint s = DailyBossRules.Seed("chest:" + Data.chestsOpened);
            var r = new RewardBundle { coins = rules.coinsMin + (int)(s % (uint)Math.Max(1, rules.coinsMax - rules.coinsMin + 1)) };
            if ((s / 1000u) % 100u < (uint)rules.gemsChancePct) r.gems = rules.gems;
            Data.chests--; Data.chestsOpened++; Data.coins += r.coins; Data.gems += r.gems; Save();
            return r;
        }
    }

    /// <summary>Target UI §1/§2: local guest profile. Social sign-in is shown but disabled ("Coming soon") while the server is offline.</summary>
    public static class ProfileRules
    {
        public const int MinName = 3, MaxName = 16;
        public static readonly string[] SocialProviders = { "Google", "Facebook", "Instagram", "X", "Email" };
        public static bool SocialSignInEnabled(string provider) => false; // offline build: every provider is "Coming soon"

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var b = new System.Text.StringBuilder();
            foreach (char c in s.Trim()) { if (char.IsLetterOrDigit(c) || c == '_' || c == ' ') b.Append(c); }
            var r = b.ToString();
            while (r.Contains("  ")) r = r.Replace("  ", " ");
            return r.Length > MaxName ? r.Substring(0, MaxName) : r;
        }

        public static string ValidateName(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Trim().Length < MinName) return $"Name needs at least {MinName} letters or numbers";
            if (n.Length > MaxName) return $"Name can be at most {MaxName} characters";
            foreach (char c in n) if (!(char.IsLetterOrDigit(c) || c == '_' || c == ' ')) return "Use letters, numbers, spaces or _";
            return null;
        }

        /// <summary>Default guest name, e.g. "Smasher_4821" (deterministic for a seed).</summary>
        public static string DefaultName(int seed) => "Smasher_" + (1000 + (int)((uint)(seed * 2654435761u) % 9000u));
    }

    /// <summary>Target UI §6: level complete rewards (coins, gems, chest), from meta_v2.json levelRewards.</summary>
    public static class RewardRules
    {
        public static RewardBundle ForLevel(LevelRewardRules r, LevelEntry level, bool won, int stars, int prevBestStars, bool firstClear)
        {
            var b = new RewardBundle();
            if (!won || r == null || level == null) return b;
            bool hard = HardLevelRules.IsHard(level);
            if (firstClear) b.coins = r.coinsFirstClear + r.coinsPerStar * stars + (hard ? r.hardBonusCoins : 0);
            else b.coins = stars > prevBestStars ? r.coinsPerStar * (stars - prevBestStars) : r.replayCoins;
            if (stars >= 3 && prevBestStars < 3) b.gems = r.gemsFirst3Star + (hard ? r.hardBonusGems : 0);
            if (firstClear && r.chestEveryLevels > 0 && level.number > 0 && level.number % r.chestEveryLevels == 0) b.chests = 1;
            return b;
        }
    }

    /// <summary>Target UI §9: the 4 season themes. A season is open once the previous one's last level is cleared and it has levels.</summary>
    public static class SeasonRules
    {
        public static SeasonTheme ForLevel(MetaV2Data m, int levelNumber)
        {
            if (m?.seasons == null || m.seasons.Length == 0) return null;
            foreach (var s in m.seasons) if (levelNumber >= s.firstLevel && levelNumber <= s.lastLevel) return s;
            return m.seasons[m.seasons.Length - 1];
        }
        public static bool IsOpen(SeasonTheme s, ChapterData ch, CampaignSave save)
        {
            if (s == null || s.comingSoon) return false;
            if (s.firstLevel <= 1) return true;
            foreach (var l in ch.levels) if (l.number == s.firstLevel - 1) return save.IsCleared(l.id);
            return false;
        }
        /// <summary>Current = the last open season.</summary>
        public static SeasonTheme Current(MetaV2Data m, ChapterData ch, CampaignSave save)
        {
            SeasonTheme cur = m?.seasons != null && m.seasons.Length > 0 ? m.seasons[0] : null;
            if (m?.seasons != null) foreach (var s in m.seasons) if (IsOpen(s, ch, save)) cur = s;
            return cur;
        }
        public static int StarsInSeason(SeasonTheme s, ChapterData ch, CampaignSave save)
        {
            int n = 0; if (s == null) return 0;
            foreach (var l in ch.levels) if (l.number >= s.firstLevel && l.number <= s.lastLevel) n += save.BestStars(l.id);
            return n;
        }
        public static int LevelsInSeason(SeasonTheme s, ChapterData ch)
        {
            int n = 0; if (s == null) return 0;
            foreach (var l in ch.levels) if (l.number >= s.firstLevel && l.number <= s.lastLevel) n++;
            return n;
        }
    }

    /// <summary>Target UI §11 achievements (stubs): progress is computed from local data, nothing is granted yet.</summary>
    public static class AchievementRules
    {
        public static int Progress(AchievementDef a, ChapterData ch, CampaignSave save, PlayerProgress p)
        {
            if (a == null) return 0;
            switch (a.type)
            {
                case "levels_cleared": { int n = 0; foreach (var l in ch.levels) if (save.IsCleared(l.id)) n++; return n; }
                case "total_stars": return UnlockRules.TotalStars(ch, save);
                case "three_star_levels": { int n = 0; foreach (var l in ch.levels) if (save.BestStars(l.id) >= 3) n++; return n; }
                case "hard_cleared": { int n = 0; foreach (var l in ch.levels) if (HardLevelRules.IsHard(l) && save.IsCleared(l.id)) n++; return n; }
                case "daily_boss_wins": return p?.Data.dailyBossWins.Count ?? 0;
                case "powerups_used": return p?.Data.powerUpsUsed ?? 0;
                case "chests_opened": return p?.Data.chestsOpened ?? 0;
                default: return 0;
            }
        }
        public static bool Done(AchievementDef a, ChapterData ch, CampaignSave save, PlayerProgress p) => a != null && a.target > 0 && Progress(a, ch, save, p) >= a.target;
    }
}
