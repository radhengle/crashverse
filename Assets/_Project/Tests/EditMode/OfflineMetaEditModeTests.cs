using System;
using System.IO;
using System.Linq;
using Crashverse.Data;
using Crashverse.Meta;
using NUnit.Framework;

namespace Crashverse.Backend.Tests
{
    /// <summary>Offline replacements in the Unity runtime: daily login (rollback-safe), free season pass, achievements, local leaderboard, Seasons 2–4.</summary>
    public class OfflineMetaEditModeTests
    {
        static ContentDatabase _db;
        static ContentDatabase Db => _db ??= ContentDatabase.LoadBundled();
        string _dir;
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "cv_off_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
        PlayerProgress NewProgress() => new PlayerProgress(Path.Combine(_dir, "progress_v2.json"));

        [Test]
        public void Daily_login_cycles_and_ignores_clock_rollback()
        {
            var p = NewProgress(); var cyc = Db.Meta.dailyLogin; var d = new DateTime(2026, 10, 24);
            Assert.AreEqual(7, cyc.Length);
            Assert.AreEqual(cyc[0].coins, DailyLoginRules.Claim(p, cyc, DailyLoginRules.Key(d)).coins);
            Assert.AreEqual(cyc[1].coins, DailyLoginRules.Claim(p, cyc, DailyLoginRules.Key(d.AddDays(1))).coins);
            Assert.True(DailyLoginRules.Claim(p, cyc, DailyLoginRules.Key(d.AddDays(1))).IsEmpty, "once a day");
            Assert.True(DailyLoginRules.Claim(p, cyc, DailyLoginRules.Key(d)).IsEmpty, "rollback can't re-claim");
            Assert.AreEqual(cyc[0].coins, DailyLoginRules.Claim(p, cyc, DailyLoginRules.Key(d.AddDays(4))).coins, "missed days restart the week");
            Assert.AreEqual(3, NewProgress().Data.loginClaims, "saved");
        }

        [Test]
        public void Season_pass_is_free_and_claims_once()
        {
            var p = NewProgress(); var def = Db.Meta.seasonPass;
            SeasonPassRules.AddXp(p, "s1_sunny_shores", def.xpPerTier);
            Assert.False(SeasonPassRules.Claim(p, "s1_sunny_shores", 0, def).IsEmpty);
            Assert.True(SeasonPassRules.Claim(p, "s1_sunny_shores", 0, def).IsEmpty);
            Assert.True(SeasonPassRules.Claim(p, "s1_sunny_shores", 1, def).IsEmpty, "tier 2 not reached");
        }

        [Test]
        public void Achievements_have_rewards_and_pay_once()
        {
            var p = NewProgress(); var s = new CampaignSave(Path.Combine(_dir, "campaign_save.json")); var ch = Db.Chapter1;
            Assert.GreaterOrEqual(Db.Meta.achievements.Length, 10);
            Assert.True(Db.Meta.achievements.All(a => a.reward != null && !a.reward.IsEmpty));
            var first = Db.Meta.achievements.First(a => a.id == "first_smash");
            s.RecordResult(ch.levels[0].id, true, 3, false, 1000);
            Assert.AreEqual(first.reward.coins, AchievementRules.Claim(first, ch, s, p).coins);
            Assert.True(AchievementRules.Claim(first, ch, s, p).IsEmpty);
        }

        [Test]
        public void Local_leaderboard_ranks_player_with_seeded_rivals()
        {
            var l = Db.Chapter1.levels[0];
            var rows = LocalLeaderboard.ForLevel(l, int.MaxValue / 2, "Pat", Db.Meta.rivalNames);
            Assert.AreEqual(LocalLeaderboard.Rivals + 1, rows.Count); Assert.True(rows[0].isPlayer); Assert.True(rows.Skip(1).All(r => r.isBot));
            Assert.AreEqual(string.Join(",", LocalLeaderboard.ForLevel(l, 0, "", Db.Meta.rivalNames).Select(r => r.score)), string.Join(",", LocalLeaderboard.ForLevel(l, 0, "", Db.Meta.rivalNames).Select(r => r.score)));
        }

        [Test]
        public void Seasons_2_to_4_are_playable_with_valid_layouts()
        {
            var ch = Db.Chapter1;
            Assert.AreEqual(40, ch.levels.Length);
            foreach (var l in ch.levels.Where(x => x.number > 10))
            {
                var lay = Db.GetLayout(l.layout); Assert.NotNull(lay, l.id);
                var ids = lay.blocks.Select(b => b.id).ToList();
                Assert.True(lay.blocks.All(b => b.sup.All(ids.Contains)), l.id + " supports resolve");
                Assert.True(HeroSlotRules.ClearOfLanes(HeroSlotRules.Position(l.heroSlot)), l.id + " hero slot clear of lanes");
            }
            Assert.True(Db.Meta.seasons.All(x => !x.comingSoon));
        }
    }
}
