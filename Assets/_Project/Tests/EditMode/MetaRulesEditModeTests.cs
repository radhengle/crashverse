using System;
using System.IO;
using System.Linq;
using Crashverse.Data;
using Crashverse.Meta;
using NUnit.Framework;

namespace Crashverse.Backend.Tests
{
    /// <summary>QA V2-07: the v2 meta rules in the Unity player runtime (rewards, stars, season unlock, Daily Boss seed, hard levels).</summary>
    public class MetaRulesEditModeTests
    {
        static ContentDatabase _db;
        static ContentDatabase Db => _db ??= ContentDatabase.LoadBundled(true); // all seasons: content integrity incl. flagged-off S2-4
        string _dir;
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "cv_meta_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
        CampaignSave NewSave() => new CampaignSave(Path.Combine(_dir, "campaign_save.json"));
        static LevelEntry L(int n) => Db.Chapter1.levels.First(l => l.number == n);

        [Test]
        public void Rewards_first_clear_stars_gems_chest_and_replay()
        {
            var r = new LevelRewardRules();
            var first = RewardRules.ForLevel(r, L(1), true, 3, 0, true);
            Assert.AreEqual(r.coinsFirstClear + 3 * r.coinsPerStar, first.coins); Assert.AreEqual(r.gemsFirst3Star, first.gems); Assert.AreEqual(0, first.chests);
            Assert.AreEqual(1, RewardRules.ForLevel(r, L(r.chestEveryLevels), true, 1, 0, true).chests, "chest every Nth level, first clear");
            Assert.AreEqual(0, RewardRules.ForLevel(r, L(r.chestEveryLevels), true, 1, 1, false).chests, "no chest on replay");
            var replay = RewardRules.ForLevel(r, L(1), true, 3, 3, false);
            Assert.AreEqual(r.replayCoins, replay.coins); Assert.AreEqual(0, replay.gems, "3-star gems only once");
            var better = RewardRules.ForLevel(r, L(1), true, 3, 1, false);
            Assert.AreEqual(2 * r.coinsPerStar, better.coins); Assert.AreEqual(r.gemsFirst3Star, better.gems);
            Assert.True(RewardRules.ForLevel(r, L(1), false, 0, 0, false).IsEmpty, "a loss grants nothing");
            var hard = RewardRules.ForLevel(r, Db.Chapter1.levels.First(HardLevelRules.IsHard), true, 3, 0, true);
            Assert.AreEqual(r.coinsFirstClear + 3 * r.coinsPerStar + r.hardBonusCoins, hard.coins); Assert.AreEqual(r.gemsFirst3Star + r.hardBonusGems, hard.gems);
        }

        [Test]
        public void Stars_total_keeps_the_best_per_level()
        {
            var s = NewSave();
            s.RecordResult(L(1).id, true, 2, false); s.RecordResult(L(1).id, true, 1, false); s.RecordResult(L(2).id, true, 3, false); s.RecordResult(L(3).id, false, 0, false);
            Assert.AreEqual(2, s.BestStars(L(1).id)); Assert.AreEqual(5, UnlockRules.TotalStars(Db.Chapter1, s));
            Assert.False(s.IsCleared(L(3).id));
        }

        [Test]
        public void Seasons_open_only_after_the_previous_last_level_and_coming_soon_stays_shut()
        {
            var all = ContentDatabase.LoadBundled(true); // Seasons 2–4 are flagged off for players; the rules stay tested
            var m = all.Meta; var s = NewSave();
            Assert.True(SeasonRules.IsOpen(m.seasons[0], all.Chapter1, s), "season 1 open from the start");
            Assert.AreSame(m.seasons[0], SeasonRules.Current(m, all.Chapter1, s));
            foreach (var later in m.seasons.Skip(1)) Assert.False(SeasonRules.IsOpen(later, all.Chapter1, s), later.name + " shut on a new install");
            foreach (var cs in m.seasons.Where(x => x.comingSoon))
            {
                foreach (var l in all.Chapter1.levels) s.RecordResult(l.id, true, 3, false);
                Assert.False(SeasonRules.IsOpen(cs, all.Chapter1, s), cs.name + " is coming soon: never open");
            }
            Assert.AreSame(m.seasons[0], SeasonRules.ForLevel(m, 1));
            var s2 = m.seasons.First(x => x.number == 2);
            if (!s2.comingSoon) { s.RecordResult(L(s2.firstLevel - 1).id, true, 1, false); Assert.True(SeasonRules.IsOpen(s2, all.Chapter1, s), "season 2 opens after Level 10"); }
        }

        [Test]
        public void Daily_boss_is_deterministic_per_local_date()
        {
            var d = Db.Meta.dailyBoss;
            Assert.AreEqual("20261024", DailyBossRules.DateKey(new DateTime(2026, 10, 24, 23, 59, 0)));
            Assert.AreEqual(DailyBossRules.Seed("dailyboss:20261024"), DailyBossRules.Seed("dailyboss:20261024"));
            Assert.AreEqual(2166136261u, DailyBossRules.Seed(""), "FNV-1a offset basis (stable across runtimes)");
            var a = DailyBossRules.ForDate("20261024", d); var b = DailyBossRules.ForDate("20261024", d);
            Assert.AreEqual(a.layout, b.layout); Assert.AreEqual(a.name, b.name); Assert.AreEqual(a.shots, b.shots);
            Assert.That(a.shots, Is.InRange(d.baseShots, d.baseShots + d.shotSpread));
            int distinct = Enumerable.Range(1, 28).Select(i => DailyBossRules.ForDate("202611" + i.ToString("00"), d).layout + "|" + DailyBossRules.ForDate("202611" + i.ToString("00"), d).name).Distinct().Count();
            Assert.Greater(distinct, 1, "different dates give different bosses");
            var p = new PlayerProgress(Path.Combine(_dir, "progress_v2.json"));
            var g1 = p.RecordDailyBossWin("20261024", new RewardBundle { coins = 10 }); var g2 = p.RecordDailyBossWin("20261024", new RewardBundle { coins = 10 });
            Assert.AreEqual(10, g1.coins); Assert.True(g2.IsEmpty, "one reward per date"); Assert.AreEqual(10, p.Data.coins);
        }

        [Test]
        public void Only_levels_tagged_hard_get_the_shot_penalty()
        {
            // Season 1: c1_l10 (the King's Palace) keeps its authored 7 shots and 0.42 / 0.28 bands
            var l10 = Db.Chapter1Authored.levels.First(l => l.number == 10);
            Assert.False(HardLevelRules.IsHard(l10)); Assert.AreEqual(7, HardLevelRules.Shots(l10)); Assert.AreEqual(0.42f, l10.star3Pct); Assert.AreEqual(0.28f, l10.star2Pct);
            Assert.AreEqual(MapNodeKind.Normal, WorldMapRules.KindFor(l10));
            Assert.False(HardLevelRules.IsHard(new LevelEntry { number = 30, shots = 7 }), "untagged 10th level is not hard");
            foreach (var l in Db.Chapter1.levels)
            {
                Assert.AreEqual(l.hard, HardLevelRules.IsHard(l), l.id);
                if (HardLevelRules.IsHard(l)) { Assert.AreEqual(Math.Max(Math.Min(HardLevelRules.MinShots, l.shots), l.shots - HardLevelRules.ShotPenalty), HardLevelRules.Shots(l)); Assert.AreEqual(MapNodeKind.Hard, WorldMapRules.KindFor(l)); }
                else Assert.AreEqual(l.shots, HardLevelRules.Shots(l));
            }
        }

        [Test]
        public void Chest_roll_is_deterministic_and_within_bounds()
        {
            var rules = Db.Meta.chest;
            var p = new PlayerProgress(Path.Combine(_dir, "progress_v2.json")); p.Grant(new RewardBundle { chests = 2 });
            var c1 = p.OpenChest(rules);
            Assert.That(c1.coins, Is.InRange(rules.coinsMin, rules.coinsMax)); Assert.True(c1.gems == 0 || c1.gems == rules.gems);
            p.OpenChest(rules); Assert.True(p.OpenChest(rules).IsEmpty, "no chests left");
            var q = new PlayerProgress(Path.Combine(_dir, "other.json")); q.Grant(new RewardBundle { chests = 1 });
            Assert.AreEqual(c1.coins, q.OpenChest(rules).coins, "same roll for the same chest index");
        }
    
        [Test]
        public void DailyBoss_hint_comes_from_dailyBoss_hints_then_the_titan_arena_with_that_layout()
        {
            var d = new DailyBossData { layouts = new[] { "titan_a" }, names = new[] { "X" } };
            var t = new TitanTemplates { arenas = new[] { new TitanArena { id = "a", layout = "titan_a", hintTarget = "legL", hintYaw = -2.5f, hintPitch = 10.5f } } };
            Assert.IsFalse(DailyBossRules.ForDate("20261030", d).HasHint, "no titan data: no hint");
            var e = DailyBossRules.ForDate("20261030", d, t);
            Assert.IsTrue(e.HasHint); Assert.AreEqual("legL", e.hintTarget); Assert.AreEqual(-2.5f, e.hintYaw); Assert.AreEqual(10.5f, e.hintPitch);
            d.hints = new[] { new DailyBossHint { layout = "titan_a", hintTarget = "legR", hintYaw = 3f, hintPitch = 9f, text = "Topple it" } };
            e = DailyBossRules.ForDate("20261030", d, t);
            Assert.AreEqual("legR", e.hintTarget); Assert.AreEqual(3f, e.hintYaw);
            Assert.AreEqual("Topple it", DailyBossRules.HintFor("titan_a", d, t).text);
            Assert.IsNull(DailyBossRules.HintFor("titan_zzz", d, t));
        }

        [Test]
        public void Bundled_daily_boss_has_a_hint_for_every_arena()
        {
            var db = ContentDatabase.LoadBundled();
            foreach (var l in db.Meta.dailyBoss.layouts) Assert.IsNotNull(DailyBossRules.HintFor(l, db.Meta.dailyBoss, db.Titan), l);
        }
    
        [Test]
        public void Held_indestructible_pieces_never_block_a_win()
        {
            // Level Designer gap 7: an indestructible piece never falls (DestructibleBlock.ForceCollapse). It must be non-objective
            // (a Base / island under a breakable layer), except a boss's scripted core (bossId levels handle it themselves).
            var db = ContentDatabase.LoadBundled();
            foreach (var l in db.Chapter1.levels)
            {
                if (!string.IsNullOrEmpty(l.bossId)) continue;
                var lay = db.GetLayout(l.layout); Assert.NotNull(lay, l.layout);
                foreach (var b in lay.blocks) Assert.False(b.indestructible && !b.nonObj, $"{l.id}:{b.id} is indestructible but counts for the win");
            }
            foreach (var name in db.Meta.dailyBoss.layouts)
                foreach (var b in db.GetLayout(name).blocks) Assert.False(b.indestructible && !b.nonObj, $"{name}:{b.id} is indestructible but counts for the win");
        }
    }

}
