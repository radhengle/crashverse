using System;
using System.IO;
using System.Linq;
using Crashverse.Data;
using Crashverse.Meta;
using NUnit.Framework;

namespace Crashverse.Backend.Tests
{
    /// <summary>integration/v2 local data (Unity side of Tools/Tests V2Tests): bundled JSON loads in the player and the rules hold.</summary>
    public class MetaV2Tests
    {
        static ContentDatabase _db;
        static ContentDatabase Db => _db ??= ContentDatabase.LoadBundled();
        string _path;
        [SetUp] public void SetUp() { _path = Path.Combine(Path.GetTempPath(), "cv_v2_" + Guid.NewGuid().ToString("N") + ".json"); }
        [TearDown] public void TearDown() { if (File.Exists(_path)) File.Delete(_path); }

        [Test]
        public void PowerUps_v2_catalog_loads_8_in_order()
        {
            var ids = PowerUpRulesV2.InUnlockOrder(Db.PowerUps).Select(p => p.id).ToArray();
            CollectionAssert.AreEqual(new[] { "earthquake", "multi_shot", "black_hole", "mega_bomb", "shield", "laser_beam", "guided_rocket", "time_freeze" }, ids);
            Assert.AreEqual(200, Db.GetPowerUp("time_freeze").p.durationTicks);
        }

        [Test]
        public void Meta_v2_loads_seasons_avatars_badges()
        {
            Assert.AreEqual(4, Db.Meta.seasons.Length);
            Assert.AreEqual("Primeval Peaks", Db.Meta.seasons[3].name);
            Assert.AreEqual(4, Db.Meta.avatars.Length); Assert.AreEqual(3, Db.Meta.badges.Length);
            Assert.NotNull(DailyBossRules.ForDate("20261024", Db.Meta.dailyBoss));
        }

        [Test]
        public void Guest_profile_persists()
        {
            var p = new PlayerProgress(_path);
            Assert.IsNull(p.CreateProfile("Unity Guest", 2, 1, 4, 3));
            var q = new PlayerProgress(_path);
            Assert.True(q.Data.profile.created); Assert.AreEqual("Unity Guest", q.Data.profile.name); Assert.AreEqual(2, q.Data.profile.avatar);
        }
    }
}
