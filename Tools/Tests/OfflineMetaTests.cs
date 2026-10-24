using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Crashverse.Data;
using Crashverse.Meta;

/// <summary>integration/v2 offline replacements: local leaderboard, daily login, achievements with rewards, free season pass, Seasons 2–4.</summary>
static class OfflineMetaTests
{
    static ContentDatabase _db;
    static ContentDatabase Db => _db ??= ContentDatabase.LoadBundled();
    static string Tmp() => Path.Combine(Path.GetTempPath(), "cv_off_" + Guid.NewGuid().ToString("N") + ".json");
    static void With(Action<PlayerProgress, CampaignSave> f)
    {
        string a = Tmp(), b = Tmp();
        try { f(new PlayerProgress(a), new CampaignSave(b)); }
        finally { foreach (var x in new[] { a, b, a + ".tmp", b + ".tmp" }) if (File.Exists(x)) File.Delete(x); }
    }

    public static void Register()
    {
        T.Add("off_daily_login_7_day_cycle", () => With((p, _) =>
        {
            var cyc = Db.Meta.dailyLogin; T.Eq(7, cyc.Length, "7-day cycle");
            var d0 = new DateTime(2026, 10, 24, 9, 0, 0);
            int coins = 0;
            for (int i = 0; i < 8; i++)
            {
                var k = DailyLoginRules.Key(d0.AddDays(i));
                T.True(DailyLoginRules.CanClaim(p.Data, k), "claimable day " + i);
                var r = DailyLoginRules.Claim(p, cyc, k); coins += r.coins;
                T.Eq(cyc[i % 7].coins, r.coins, "day " + (i % 7 + 1) + " reward");
                T.True(!DailyLoginRules.CanClaim(p.Data, k), "once per day " + i);
                T.True(DailyLoginRules.Claim(p, cyc, k).IsEmpty, "second claim empty " + i);
            }
            T.Eq(coins, p.Data.coins, "coins granted"); T.Eq(8, p.Data.loginClaims, "claims counted");
            // a missed day restarts at day 1
            var gap = DailyLoginRules.Key(d0.AddDays(10));
            T.Eq(cyc[0].coins, DailyLoginRules.Claim(p, cyc, gap).coins, "missed day: back to day 1");
            var re = new PlayerProgress(PathOf(p)); T.Eq(p.Data.loginLast, re.Data.loginLast, "persisted");
        }));
        T.Add("off_daily_login_clock_rollback_cannot_reclaim", () => With((p, _) =>
        {
            var cyc = Db.Meta.dailyLogin; var d = new DateTime(2026, 10, 24);
            DailyLoginRules.Claim(p, cyc, DailyLoginRules.Key(d.AddDays(5))); // played with the clock pushed ahead
            var back = DailyLoginRules.Key(d); DailyLoginRules.Observe(p.Data, back);
            T.True(DailyLoginRules.RolledBack(p.Data, back), "rollback detected");
            T.True(DailyLoginRules.Claim(p, cyc, back).IsEmpty, "no claim after rollback");
            T.True(DailyLoginRules.Claim(p, cyc, DailyLoginRules.Key(d.AddDays(5))).IsEmpty, "same max day: no double claim");
            T.True(!DailyLoginRules.Claim(p, cyc, DailyLoginRules.Key(d.AddDays(6))).IsEmpty, "next real day claims");
            T.Eq(DailyLoginRules.Key(d.AddDays(6)), p.Data.loginMaxSeen, "max seen kept");
        }));
        T.Add("off_season_pass_free_track", () => With((p, _) =>
        {
            var def = Db.Meta.seasonPass; T.True(def.tiers.Length >= 5 && def.xpPerTier > 0, "tiers defined");
            T.Eq(0, SeasonPassRules.XpForRun(def, false, 3, true), "loss: no xp");
            int xp = SeasonPassRules.XpForRun(def, true, 3, true); T.Eq(def.xpWin + 3 * def.xpPerStar + def.xpFirstClear, xp, "win xp");
            SeasonPassRules.AddXp(p, "s1_sunny_shores", def.xpPerTier * 2 + 5);
            var s = SeasonPassRules.For(p.Data, "s1_sunny_shores");
            T.Eq(2, SeasonPassRules.TierReached(s.xp, def), "2 tiers");
            T.True(SeasonPassRules.CanClaim(s, 0, def) && SeasonPassRules.CanClaim(s, 1, def) && !SeasonPassRules.CanClaim(s, 2, def), "claimable tiers");
            int c0 = p.Data.coins; var r = SeasonPassRules.Claim(p, "s1_sunny_shores", 0, def);
            T.Eq(def.tiers[0].coins, p.Data.coins - c0, "tier reward"); T.True(SeasonPassRules.Claim(p, "s1_sunny_shores", 0, def).IsEmpty, "claim once");
            T.True(SeasonPassRules.Claim(p, "s1_sunny_shores", 5, def).IsEmpty, "locked tier");
            T.Eq(1, SeasonPassRules.Unclaimed(s, def), "1 left");
            SeasonPassRules.AddXp(p, "s1_sunny_shores", 1_000_000); T.Eq(def.tiers.Length, SeasonPassRules.TierReached(SeasonPassRules.For(p.Data, "s1_sunny_shores").xp, def), "capped at last tier");
            var re = new PlayerProgress(PathOf(p)); T.Eq(1, SeasonPassRules.For(re.Data, "s1_sunny_shores").claimed.Count, "persisted");
            // free track only: no price, no premium fields
            T.True(typeof(SeasonPassDef).GetFields().All(f => !f.Name.ToLowerInvariant().Contains("premium") && !f.Name.ToLowerInvariant().Contains("price")), "no premium track");
        }));
        T.Add("off_achievements_10_with_rewards", () => With((p, save) =>
        {
            var m = Db.Meta; var ch = Db.Chapter1;
            T.True(m.achievements.Length >= 10, "~10 achievements");
            var known = new HashSet<string> { "levels_cleared", "total_stars", "three_star_levels", "hard_cleared", "daily_boss_wins", "powerups_used", "chests_opened", "login_days", "pass_tiers" };
            foreach (var a in m.achievements) { T.True(known.Contains(a.type), "tied to a stat " + a.id); T.True(a.reward != null && !a.reward.IsEmpty, "has reward " + a.id); T.True(a.reward.gems <= 10, "modest gems " + a.id); }
            var first = m.achievements.First(a => a.id == "first_smash");
            T.True(!AchievementRules.CanClaim(first, ch, save, p) && AchievementRules.Claim(first, ch, save, p).IsEmpty, "not done: nothing");
            save.RecordResult(ch.levels[0].id, true, 3, false, 1500);
            T.True(AchievementRules.CanClaim(first, ch, save, p), "done: claimable");
            int c0 = p.Data.coins; AchievementRules.Claim(first, ch, save, p);
            T.Eq(first.reward.coins, p.Data.coins - c0, "reward granted"); T.True(AchievementRules.Claimed(first, p), "claimed");
            T.True(AchievementRules.Claim(first, ch, save, p).IsEmpty, "claim once");
            var login = m.achievements.First(a => a.type == "login_days"); p.Data.loginClaims = login.target; T.True(AchievementRules.Done(login, ch, save, p), "login stat");
            var pass = m.achievements.First(a => a.type == "pass_tiers"); SeasonPassRules.For(p.Data, "x").claimed.AddRange(Enumerable.Range(0, pass.target)); T.True(AchievementRules.Done(pass, ch, save, p), "pass stat");
        }));
        T.Add("off_local_leaderboard", () =>
        {
            var l = Db.Chapter1.levels[2]; var names = Db.Meta.rivalNames;
            var a = LocalLeaderboard.ForLevel(l, 0, "Pat", names); var b = LocalLeaderboard.ForLevel(l, 0, "Pat", names);
            T.Eq(LocalLeaderboard.Rivals, a.Count, "rivals only before a clear"); T.True(a.All(r => r.isBot), "rivals are bots");
            T.Eq(string.Join(",", a.Select(r => r.name + r.score)), string.Join(",", b.Select(r => r.name + r.score)), "deterministic");
            T.True(a.Select(r => r.name).Distinct().Count() == a.Count, "distinct names");
            for (int i = 1; i < a.Count; i++) T.True(a[i - 1].score >= a[i].score && a[i].rank == i + 1, "sorted " + i);
            var top = LocalLeaderboard.ForLevel(l, 1_000_000, "Pat", names); T.True(top[0].isPlayer && top[0].rank == 1 && top[0].name == "Pat", "player best on top");
            var low = LocalLeaderboard.ForLevel(l, 1, "Pat", names); T.Eq(LocalLeaderboard.Rivals + 1, low.Last().rank, "player last"); T.True(low.Last().isPlayer, "player row");
            T.True(LocalLeaderboard.ForLevel(Db.Chapter1.levels[3], 0, "", names).Select(r => r.score).SequenceEqual(a.Select(r => r.score)) == false, "differs per level");
        });
        T.Add("off_seasons_2_4_placeholders", () =>
        {
            var ch = Db.Chapter1; var m = Db.Meta;
            T.Eq(40, ch.levels.Length, "40 levels");
            for (int i = 0; i < ch.levels.Length; i++) T.Eq(i + 1, ch.levels[i].number, "numbered " + i);
            for (int i = 1; i < ch.levels.Length; i++) T.Eq(ch.levels[i - 1].id, ch.levels[i].requires, "requires chain " + ch.levels[i].id);
            T.Eq(ch.levels.Length, ch.levels.Select(l => l.id).Distinct().Count(), "unique ids");
            foreach (var s in m.seasons) { T.True(!s.comingSoon, "season open for play " + s.id); T.Eq(10, SeasonRules.LevelsInSeason(s, ch), "10 levels " + s.id); }
            foreach (var l in ch.levels.Where(SeasonContent.IsPlaceholder))
            {
                var lay = Db.GetLayout(l.layout); T.True(lay != null && lay.blocks.Length >= 8, "layout " + l.id);
                var ids = new HashSet<string>(lay.blocks.Select(b => b.id));
                foreach (var b in lay.blocks) foreach (var sp in b.sup) T.True(ids.Contains(sp), "sup resolves " + l.id + "/" + b.id);
                T.True(lay.blocks.Count(b => !b.nonObj) >= 7, "objective pieces " + l.id);
                T.True(ids.Contains(l.hintTarget), "hint target " + l.id);
                T.True(l.name.Contains("(preview)"), "marked preview " + l.id);
                T.True(HeroSlotRules.ClearOfLanes(HeroSlotRules.Position(l.heroSlot)) && HeroSlotRules.Validate(l.heroSlot) == null, "hero slot " + l.id);
                var season = SeasonRules.ForLevel(m, l.number); T.Eq(season.id, l.island, "themed by season " + l.id);
                T.True(l.shots >= 4 && l.star2Pct < l.star3Pct, "bands " + l.id);
            }
            T.Eq("20,30,40", string.Join(",", ch.levels.Where(HardLevelRules.IsHard).Select(l => l.number)), "placeholder finales tagged hard");
            var again = ContentDatabase.LoadBundled().Chapter1;
            T.Eq(string.Join(",", ch.levels.Select(l => l.id + l.shots)), string.Join(",", again.levels.Select(l => l.id + l.shots)), "deterministic");
        });
        T.Add("off_seasons_delivered_chapter_preferred", () =>
        {
            var ch1 = Db.Chapter1Authored; var meta = new MetaV2Data { seasons = Db.Meta.seasons.Select(s => new SeasonTheme { id = s.id, name = s.name, number = s.number, firstLevel = s.firstLevel, lastLevel = s.lastLevel, comingSoon = true }).ToArray() };
            meta.seasons[0].comingSoon = false;
            var c2 = new ChapterData { id = "c2", levels = Enumerable.Range(11, 10).Select(n => new LevelEntry { id = "c2_l" + n, number = n, layout = "lay" + n, shots = 5, requires = n == 11 ? "c1_l10" : "c2_l" + (n - 1) }).ToArray() };
            var bad = new ChapterData { id = "c3", levels = new[] { new LevelEntry { id = "c3_l21", number = 21, layout = "missing" } } };
            var gen = new List<string>();
            var merged = SeasonContent.Merge(ch1, meta, new[] { c2, bad }, id => id != null && id.StartsWith("lay"), l => gen.Add(l.id));
            T.Eq(40, merged.levels.Length, "40 levels");
            T.True(merged.levels.Where(l => l.number >= 11 && l.number <= 20).All(l => l.id.StartsWith("c2_")), "delivered S2 used");
            T.True(merged.levels.Where(l => l.number >= 21).All(SeasonContent.IsPlaceholder), "invalid S3 rejected: placeholders");
            T.Eq(20, gen.Count, "layouts registered for placeholders only");
            T.Eq("c1_l10", merged.levels.First(l => l.number == 11).requires, "chain into delivered");
            T.Eq(10, ch1.levels.Length, "authored chapter untouched");
        });
    }

    static string PathOf(PlayerProgress p) => (string)typeof(PlayerProgress).GetField("_path", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(p);
}
