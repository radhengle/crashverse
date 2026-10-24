using System;
using System.Collections.Generic;
using System.Globalization;
using Crashverse.Data;

namespace Crashverse.Meta
{
    // integration/v2 offline replacements for the online stubs: everything below is local data on this device (progress_v2.json /
    // campaign_save.json), clearly labelled local in the UI, and grants only coins / earned gems / chests (no purchase path).

    /// <summary>Daily login: a 7-day cycle on the device's LOCAL date. Clock rollback can't re-claim: the highest date ever seen is
    /// kept, and a claim needs today > last claim and today >= max seen. Missing a day restarts the cycle at day 1.</summary>
    public static class DailyLoginRules
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string Key(DateTime localNow) => localNow.ToString("yyyyMMdd", Inv);
        public static string Yesterday(string key) => DateTime.ParseExact(key, "yyyyMMdd", Inv).AddDays(-1).ToString("yyyyMMdd", Inv);
        static int Cmp(string a, string b) => string.CompareOrdinal(a ?? "", b ?? "");

        /// <summary>Remember the latest local date seen (call on launch/resume and before every claim).</summary>
        public static void Observe(ProgressData d, string key) { if (Cmp(key, d.loginMaxSeen) > 0) d.loginMaxSeen = key; }
        public static bool RolledBack(ProgressData d, string key) => !string.IsNullOrEmpty(d.loginMaxSeen) && Cmp(key, d.loginMaxSeen) < 0;
        public static bool CanClaim(ProgressData d, string key) => !RolledBack(d, key) && Cmp(key, d.loginLast) > 0;
        /// <summary>Cycle index (0-based) today's claim would get.</summary>
        public static int NextDay(ProgressData d, string key, int cycle)
        {
            if (cycle <= 0) return 0;
            bool streak = !string.IsNullOrEmpty(d.loginLast) && d.loginLast == Yesterday(key) && d.loginDay >= 0;
            return streak ? (d.loginDay + 1) % cycle : 0;
        }
        public static RewardBundle Claim(PlayerProgress p, RewardBundle[] cycle, string key, SeasonPassDef pass = null, string seasonId = null)
        {
            var d = p.Data; Observe(d, key);
            if (cycle == null || cycle.Length == 0 || !CanClaim(d, key)) return new RewardBundle();
            int day = NextDay(d, key, cycle.Length); var src = cycle[day] ?? new RewardBundle();
            var r = new RewardBundle { coins = src.coins, gems = src.gems, chests = src.chests };
            p.Grant(r); d.loginLast = key; d.loginDay = day; d.loginClaims++;
            if (pass != null && seasonId != null) SeasonPassRules.AddXp(p, seasonId, pass.xpDailyLogin, false);
            p.Save();
            return r;
        }
    }

    /// <summary>Season pass, FREE track only (no premium track, nothing to buy). XP from play goes to the season of the level played.</summary>
    public static class SeasonPassRules
    {
        public static PassProgress For(ProgressData d, string seasonId)
        {
            foreach (var s in d.pass) if (s.season == seasonId) return s;
            var n = new PassProgress { season = seasonId }; d.pass.Add(n); return n;
        }
        public static int TierReached(int xp, SeasonPassDef def) => def == null || def.xpPerTier <= 0 || def.tiers == null ? 0 : Math.Min(def.tiers.Length, Math.Max(0, xp) / def.xpPerTier);
        public static int XpForRun(SeasonPassDef def, bool won, int stars, bool firstClear) =>
            def == null || !won ? 0 : def.xpWin + def.xpPerStar * Math.Max(0, Math.Min(3, stars)) + (firstClear ? def.xpFirstClear : 0);
        public static void AddXp(PlayerProgress p, string seasonId, int xp, bool save = true)
        {
            if (xp <= 0 || string.IsNullOrEmpty(seasonId)) return;
            For(p.Data, seasonId).xp += xp; if (save) p.Save();
        }
        public static bool CanClaim(PassProgress s, int tier, SeasonPassDef def) =>
            s != null && def?.tiers != null && tier >= 0 && tier < def.tiers.Length && TierReached(s.xp, def) > tier && !s.claimed.Contains(tier);
        public static RewardBundle Claim(PlayerProgress p, string seasonId, int tier, SeasonPassDef def)
        {
            var s = For(p.Data, seasonId);
            if (!CanClaim(s, tier, def)) return new RewardBundle();
            var src = def.tiers[tier] ?? new RewardBundle(); var r = new RewardBundle { coins = src.coins, gems = src.gems, chests = src.chests };
            s.claimed.Add(tier); p.Grant(r); p.Save(); return r;
        }
        public static int Unclaimed(PassProgress s, SeasonPassDef def) { int n = 0; if (def?.tiers == null || s == null) return 0; for (int t = 0; t < def.tiers.Length; t++) if (CanClaim(s, t, def)) n++; return n; }
    }

    /// <summary>Local leaderboard: the player's best score per level plus seeded practice rivals (bots). Never sent anywhere.</summary>
    public static class LocalLeaderboard
    {
        [Serializable] public class Row { public int rank; public string name; public int score; public bool isPlayer, isBot; }
        public const int Rivals = 9;
        /// <summary>Par score for the rival spread (rough CampaignScore range of a level).</summary>
        public static int Par(LevelEntry l) => 1200 + 180 * Math.Max(1, l?.number ?? 1) + (HardLevelRules.IsHard(l) ? 600 : 0);

        public static List<Row> ForLevel(LevelEntry l, int playerBest, string playerName, string[] rivalNames)
        {
            var rows = new List<Row>(); if (l == null) return rows;
            var names = rivalNames != null && rivalNames.Length > 0 ? rivalNames : new[] { "Bot" };
            uint s0 = DailyBossRules.Seed("rivals:" + l.id); int par = Par(l);
            for (int i = 0; i < Rivals; i++)
            {
                uint s = DailyBossRules.Seed("rival:" + l.id + ":" + i);
                float f = 0.45f + (s % 1000u) / 1000f * 1.15f; // 45%..160% of par
                string n = names[(int)((s0 + (uint)i) % (uint)names.Length)] + (names.Length < Rivals ? " " + (i + 1) : "");
                rows.Add(new Row { name = n, score = (int)Math.Round(par * f / 10f) * 10, isBot = true });
            }
            if (playerBest > 0) rows.Add(new Row { name = string.IsNullOrEmpty(playerName) ? "You" : playerName, score = playerBest, isPlayer = true });
            rows.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : a.isPlayer ? -1 : b.isPlayer ? 1 : string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < rows.Count; i++) rows[i].rank = i + 1;
            return rows;
        }
    }

    /// <summary>Seasons 2–4 content. Delivered Level Designer chapters (chapter2..4.json + Layouts) are appended when present and
    /// valid; any season left without levels gets deterministic placeholder levels built from the existing pieces, so every
    /// season is playable. Placeholder ids start with "pl_" and their names say "(preview)".</summary>
    public static class SeasonContent
    {
        public const string PlaceholderPrefix = "pl_";
        public static bool IsPlaceholder(LevelEntry l) => l?.id != null && l.id.StartsWith(PlaceholderPrefix);

        /// <summary>Returns the merged chapter (levels 1..N in number order, requires chained). registerLayout receives generated layouts;
        /// hasLayout says whether a delivered level's layout resolves. Seasons that end up with levels are no longer "coming soon".</summary>
        public static ChapterData Merge(ChapterData ch1, MetaV2Data meta, IList<ChapterData> delivered, Func<string, bool> hasLayout, Action<LayoutData> registerLayout)
        {
            if (ch1?.levels == null) return ch1;
            var levels = new List<LevelEntry>(ch1.levels); var islands = new List<IslandData>(ch1.islands ?? new IslandData[0]);
            var have = new HashSet<int>(); foreach (var l in levels) have.Add(l.number);
            if (delivered != null)
                foreach (var c in delivered)
                {
                    if (c?.levels == null) continue;
                    bool ok = true; foreach (var l in c.levels) if (l == null || have.Contains(l.number) || hasLayout == null || !hasLayout(l.layout)) { ok = false; break; }
                    if (!ok) continue; // all or nothing per delivered chapter
                    foreach (var l in c.levels) { levels.Add(l); have.Add(l.number); }
                    if (c.islands != null) islands.AddRange(c.islands);
                }
            foreach (var s in meta?.seasons ?? new SeasonTheme[0])
            {
                if (s.number <= 1) continue;
                for (int n = s.firstLevel; n <= s.lastLevel; n++)
                    if (!have.Contains(n)) { var (l, lay) = ProceduralLevels.Make(s, n); registerLayout?.Invoke(lay); levels.Add(l); have.Add(n); }
            }
            levels.Sort((a, b) => a.number.CompareTo(b.number));
            for (int i = 1; i < levels.Count; i++) if (string.IsNullOrEmpty(levels[i].requires)) levels[i].requires = levels[i - 1].id;
            foreach (var s in meta?.seasons ?? new SeasonTheme[0]) if (s.number > 1 && levels.Exists(l => l.number >= s.firstLevel && l.number <= s.lastLevel)) s.comingSoon = false;
            return new ChapterData { id = ch1.id, name = ch1.name, nextRegion = ch1.nextRegion, islands = islands.ToArray(), levels = levels.ToArray() };
        }
    }

    /// <summary>Placeholder levels from the existing piece kinds (indestructible island rock, slabs, pillars, walls, a crown),
    /// deterministic per level number. Simple stacked towers: winnable by knocking out the ground-floor pillars.</summary>
    public static class ProceduralLevels
    {
        static readonly string[][] SeasonMats = { new[] { "wood", "brick" }, new[] { "wood", "stone", "steel" }, new[] { "brick", "glass", "steel" }, new[] { "wood", "stone", "glass" } };

        public static (LevelEntry, LayoutData) Make(SeasonTheme s, int number)
        {
            uint r = DailyBossRules.Seed("pl:" + s.id + ":" + number);
            int idx = number - s.firstLevel; bool hard = idx == s.lastLevel - s.firstLevel; // season finale slot, tagged hard in data
            int floors = 2 + (int)(r % 2u) + (idx >= 5 ? 1 : 0);
            var mats = SeasonMats[Math.Min(SeasonMats.Length - 1, Math.Max(0, s.number - 1))];
            string id = SeasonContent.PlaceholderPrefix + s.id + "_l" + (idx + 1).ToString("00");
            var blocks = new List<BlockData>();
            float w = 2.6f + (r / 7u % 3u) * 0.4f, d = 2.8f, h = 1.6f + (r / 13u % 3u) * 0.3f;
            blocks.Add(B("rock", "brick", 0f, 0.6f, 0f, 5f, 1.2f, 5f, null, "island", nonObj: true, indestructible: true, color: "8A8A8A"));
            string below = "rock"; float y = 1.2f;
            for (int f = 0; f < floors; f++)
            {
                string m = mats[(int)((r / (uint)(17 + f)) % (uint)mats.Length)];
                if (f == 0 && m == "glass") m = mats[0]; // never glass legs on the ground floor
                var legs = new List<string>();
                for (int k = 0; k < 4; k++)
                {
                    string lid = $"f{f}leg{k}"; legs.Add(lid);
                    blocks.Add(B(lid, m, k < 2 ? -w / 2f + 0.25f : w / 2f - 0.25f, y + h / 2f, k % 2 == 0 ? -d / 2f + 0.25f : d / 2f - 0.25f, 0.35f, h, 0.35f, new[] { below }, f == 0 ? "leg" : ""));
                }
                y += h; string slab = $"f{f}slab";
                string sm = mats[(f + 1) % mats.Length]; if (sm == "glass") sm = mats[0];
                blocks.Add(B(slab, sm, 0f, y + 0.2f, 0f, w, 0.4f, d, legs.ToArray(), ""));
                y += 0.4f; below = slab;
            }
            blocks.Add(B("crown", mats[0], 0f, y + 0.5f, 0f, 1.2f, 1.0f, 1.2f, new[] { below }, "crown"));
            var lay = new LayoutData { id = id, blocks = blocks.ToArray() };
            int shots = 4 + floors / 2 + (hard ? 2 : 0);
            var lvl = new LevelEntry
            {
                id = id, number = number, island = s.id, name = s.name + " " + (idx + 1) + " (preview)", layout = id, objective = "DESTROY_STRUCTURE",
                shots = shots, winThreshold = 0.85f, materials = mats, teaches = "Preview level: knock out the legs", mastery = null,
                forced = new SlotRule[0], lockedSlots = new string[0], requires = "", bossId = "", reward = new Reward[0],
                star3Pct = hard ? 0.5f : 0.6f, star2Pct = hard ? 0.33f : 0.3f, hintTarget = "f0leg0", hintYaw = 0f, hintPitch = 10f, hard = hard,
                heroSlot = new HeroSlot { kind = idx % 2 == 0 ? "keep" : "gatehouse", fbx = "", pos = (float[])HeroSlotRules.DefaultPos.Clone() },
            };
            return (lvl, lay);
        }

        static BlockData B(string id, string mat, float x, float y, float z, float sx, float sy, float sz, string[] sup, string role, bool nonObj = false, bool indestructible = false, string color = "") =>
            new BlockData { id = id, mat = mat, p = new[] { x, y, z }, s = new[] { sx, sy, sz }, sup = sup ?? new string[0], joint = false, role = role ?? "", nonObj = nonObj, color = color,
                            explosive = false, indestructible = indestructible, spinAxis = new float[0], pivot = new float[0], spinDps = 0f };
    }
}
