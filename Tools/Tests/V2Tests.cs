using System;
using System.IO;
using System.Linq;
using Crashverse.Data;
using Crashverse.Feel;
using Crashverse.Meta;

/// <summary>integration/v2: power-ups v2, target UI rules (profile, map, level select, hard levels, Daily Boss, seasons, progression).</summary>
static class V2Tests
{
    static ContentDatabase _db;
    static ContentDatabase Db => _db ??= ContentDatabase.LoadBundled();
    static string Tmp() => Path.Combine(Path.GetTempPath(), "cv_v2_" + Guid.NewGuid().ToString("N") + ".json");

    public static void Register()
    {
        // ---------- Power-ups v2 (design/Powerups_v2_Spec.md) ----------
        T.Add("pu_v2_catalog_8_in_unlock_order", () =>
        {
            var ids = PowerUpRulesV2.InUnlockOrder(Db.PowerUps).Select(p => p.id).ToArray();
            T.Eq("earthquake,multi_shot,black_hole,mega_bomb,shield,laser_beam,guided_rocket,time_freeze", string.Join(",", ids), "8 tiles, unlock order");
            foreach (var p in Db.PowerUps.powerups) T.True(PowerUpRulesV2.IsKnownEffect(p.effect), "known effect " + p.effect);
            T.True(Db.PowerUps.powerups.All(p => p.effect != "CHAIN_REACTOR"), "Chain Reactor removed");
            T.Eq(1, Db.PowerUps.powerups.Count(p => p.slot == "Core" && p.effect == "MEGA_BOMB"), "Mega Bomb is a Core");
            T.Eq(4, Db.PowerUps.powerups.Count(p => p.slot == "Booster"), "Booster slot has 4 options");
        });
        T.Add("pu_v2_values_from_json", () =>
        {
            var tf = Db.GetPowerUp("time_freeze"); T.Eq(200, tf.p.durationTicks, "Time Freeze 200 ticks"); T.Eq(0.25f, tf.p.slowFactor, "0.25x");
            var sh = Db.GetPowerUp("shield"); T.Eq(0.1f, sh.p.threshold, "Shield 10%");
            var gr = Db.GetPowerUp("guided_rocket"); T.Eq(90f, gr.p.turnRate, "90 deg/s"); T.Eq(60, gr.p.steerTicks, "60 ticks");
            var bh = Db.GetPowerUp("black_hole"); T.Eq(75, PowerUpRulesV2.DurationTicks(bh.p), "Black Hole 75 ticks");
            var mb = Db.GetPowerUp("mega_bomb"); T.Eq(3.5f, mb.p.radius, "3.5 m"); T.Eq(70f, mb.p.burstDamage, "70 at centre");
        });
        T.Add("pu_v2_mega_bomb_falloff", () =>
        {
            T.Eq(70f, PowerUpRulesV2.MegaBombDamage(0f, 3.5f, 70f), "centre");
            T.Eq(35f, PowerUpRulesV2.MegaBombDamage(1.75f, 3.5f, 70f), "half way");
            T.Eq(0f, PowerUpRulesV2.MegaBombDamage(3.5f, 3.5f, 70f), "0 at 3.5 m");
            T.True(PowerUpRulesV2.MegaBombDetonates(3.5f, 3.5f), "barrel at 3.5 m detonates (inclusive)");
            T.True(!PowerUpRulesV2.MegaBombDetonates(3.6f, 3.5f), "barrel at 3.6 m does not");
        });
        T.Add("pu_v2_shield_refund", () =>
        {
            T.True(PowerUpRulesV2.ShieldRefunds(false, 0.099f, 0f, 0.1f), "9.9% refunds");
            T.True(!PowerUpRulesV2.ShieldRefunds(false, 0.10f, 0f, 0.1f), "10% does not");
            T.True(PowerUpRulesV2.ShieldRefunds(true, 0.5f, 0.049f, 0.1f), "boss: 4.9% of HP refunds");
            T.True(!PowerUpRulesV2.ShieldRefunds(true, 0f, 0.05f, 0.1f), "boss: 5% does not");
        });
        T.Add("pu_v2_guided_steering_deterministic", () =>
        {
            T.Eq(1.8f, PowerUpRulesV2.MaxTurnPerTick(90f), "90 deg/s = 1.8 deg per 0.02 s tick");
            T.Eq(1.8f, PowerUpRulesV2.SteerForTick(10f, 5, 60, 90f), "clamped");
            T.Eq(-0.5f, PowerUpRulesV2.SteerForTick(-0.5f, 59, 60, 90f), "in range");
            T.Eq(0f, PowerUpRulesV2.SteerForTick(5f, 60, 60, 90f), "window over at 60 ticks");
            // replay: recorded fixed-point values give the same total turn
            float live = 0f, replay = 0f; var rec = new Crashverse.Backend.InputRecorder();
            for (int t = 0; t < 70; t++) { float d = PowerUpRulesV2.Quantize(PowerUpRulesV2.SteerForTick((t % 7) * 0.37f - 1f, t, 60, 90f)); live += d; if (d != 0f) rec.Record(t, "steer", d, 0f); }
            foreach (var e in rec.ToArray()) replay += Crashverse.Backend.InputRecorder.FromFixed(e.x);
            T.True(Math.Abs(live - replay) < 1e-4f, "replay of recorded steer events = live turn");
        });
        T.Add("pu_v2_time_freeze_200_ticks", () =>
        {
            var tf = new TimeFractureTimer(); AnimEventBus.ResetForTests();
            tf.ArmTicks(200, 0.25f); T.True(tf.Active, "starts on arm"); T.Eq(0.25f, tf.SpeedScale, "0.25x");
            for (int i = 0; i < 199; i++) tf.TickSim(); T.True(tf.Active, "on after 199 ticks");
            tf.Tick(10f); T.True(tf.Active, "wall-clock time doesn't end it");
            tf.TickSim(); T.True(!tf.Active, "ends at tick 200");
            tf.ArmTicks(200, 0.25f); for (int i = 0; i < 50; i++) tf.TickSim(); tf.OnShotFired(); tf.OnShotImpact();
            T.True(!tf.Active, "ends at the impact tick when that comes sooner");
            T.Eq(150, PowerUpRulesV2.TimeFreezeEndTick(100, 200, 150) - 0, "end tick = impact");
            T.Eq(300, PowerUpRulesV2.TimeFreezeEndTick(100, 200, 400), "end tick = arm + 200");
        });

        // ---------- Target UI rules ----------
        T.Add("v2_profile_guest_create_and_persist", () =>
        {
            var path = Tmp();
            try
            {
                var p = new PlayerProgress(path);
                T.True(!p.Data.profile.created, "fresh: no profile");
                T.True(ProfileRules.SocialProviders.All(x => !ProfileRules.SocialSignInEnabled(x)), "social sign-in all Coming soon");
                T.Eq("Google,Facebook,Instagram,X,Email", string.Join(",", ProfileRules.SocialProviders), "providers");
                T.True(p.CreateProfile("ab", 0, 0, 4, 3) != null, "2 chars rejected");
                T.True(p.CreateProfile("<<>>!", 0, 0, 4, 3) != null, "symbols-only rejected"); T.Eq("badname", ProfileRules.Clean("bad<name>"), "symbols stripped"); T.True(ProfileRules.ValidateName("bad<n>") != null, "raw symbols invalid");
                T.True(p.CreateProfile("Sam", 4, 0, 4, 3) != null, "avatar out of range");
                T.True(p.CreateProfile("Sam", 0, 3, 4, 3) != null, "badge out of range");
                T.True(!p.Data.profile.created, "still none");
                T.Eq(null, p.CreateProfile("  Wreck  Master_7 ", 3, 2, 4, 3), "valid");
                var q = new PlayerProgress(path);
                T.True(q.Data.profile.created, "persisted");
                T.Eq("Wreck Master_7", q.Data.profile.name, "cleaned name");
                T.Eq(3, q.Data.profile.avatar, "avatar 4"); T.Eq(2, q.Data.profile.badge, "badge 3");
                T.Eq(null, ProfileRules.ValidateName(ProfileRules.DefaultName(12345)), "default name valid");
                T.Eq(ProfileRules.DefaultName(7), ProfileRules.DefaultName(7), "default name deterministic");
                T.Eq(4, Db.Meta.avatars.Length, "4 avatars"); T.Eq(3, Db.Meta.badges.Length, "3 badges");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
        T.Add("v2_rewards_coins_gems_chest", () =>
        {
            var r = Db.Meta.levelRewards; var ch = Db.Chapter1;
            var l5 = ch.levels.First(l => l.number == 5); var l10 = ch.levels.First(l => l.number == 10); var l1 = ch.levels[0];
            var a = RewardRules.ForLevel(r, l1, true, 3, 0, true);
            T.Eq(r.coinsFirstClear + 3 * r.coinsPerStar, a.coins, "first clear coins"); T.Eq(r.gemsFirst3Star, a.gems, "first 3-star gems"); T.Eq(0, a.chests, "no chest on L1");
            T.Eq(1, RewardRules.ForLevel(r, l5, true, 1, 0, true).chests, "chest every 5 levels");
            var h = RewardRules.ForLevel(r, l10, true, 3, 0, true);
            T.Eq(r.coinsFirstClear + 3 * r.coinsPerStar + r.hardBonusCoins, h.coins, "hard bonus coins"); T.Eq(r.gemsFirst3Star + r.hardBonusGems, h.gems, "hard bonus gems");
            T.Eq(r.replayCoins, RewardRules.ForLevel(r, l1, true, 3, 3, false).coins, "replay coins");
            T.Eq(r.coinsPerStar, RewardRules.ForLevel(r, l1, true, 3, 2, false).coins, "new star only");
            T.True(RewardRules.ForLevel(r, l1, false, 0, 0, false).IsEmpty, "loss: nothing");
            var path = Tmp();
            try
            {
                var p = new PlayerProgress(path); p.Grant(h); p.Save();
                var q = new PlayerProgress(path); T.Eq(h.coins, q.Data.coins, "coins saved"); T.Eq(1, h.chests, "L10 first clear has a chest"); T.Eq(1, q.Data.chests, "chest saved");
                var c1 = q.OpenChest(Db.Meta.chest); T.True(c1 != null && c1.coins > 0, "chest opens"); T.Eq(1, q.Data.chestsOpened, "counted");
                T.True(q.OpenChest(Db.Meta.chest).IsEmpty, "no chest left");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
        T.Add("v2_hard_every_10th", () =>
        {
            var ch = Db.Chapter1;
            T.Eq("10", string.Join(",", ch.levels.Where(HardLevelRules.IsHard).Select(l => l.number)), "L10 hard in ch1");
            var l10 = ch.levels.First(l => l.number == 10);
            T.True(HardLevelRules.Shots(l10) < l10.shots, "tighter steps"); T.Eq(ch.levels[0].shots, HardLevelRules.Shots(ch.levels[0]), "normal unchanged"); T.True(HardLevelRules.ThemeDarken(l10) < 1f, "darker theme");
            T.Eq(MapNodeKind.Hard, WorldMapRules.KindFor(l10), "skull node"); T.Eq(MapNodeKind.Normal, WorldMapRules.KindFor(ch.levels[0]), "normal");
        });
        T.Add("v2_world_map_path_and_states", () =>
        {
            var ch = Db.Chapter1; var path = Tmp();
            try
            {
                var save = new CampaignSave(path);
                T.Eq(MapNodeState.Open, WorldMapRules.StateFor(ch.levels[0], save), "L1 open");
                T.Eq(MapNodeState.Locked, WorldMapRules.StateFor(ch.levels[1], save), "L2 locked");
                T.Eq(0, WorldMapRules.Frontier(ch, save), "frontier L1");
                save.RecordResult(ch.levels[0].id, true, 2, false, 100);
                T.Eq(MapNodeState.Cleared, WorldMapRules.StateFor(ch.levels[0], save), "L1 cleared");
                T.Eq(MapNodeState.Open, WorldMapRules.StateFor(ch.levels[1], save), "L2 open");
                T.Eq(1, WorldMapRules.Frontier(ch, save), "frontier L2");
                for (int i = 0; i < 12; i++) { T.True(WorldMapRules.X(i) >= 0.15f && WorldMapRules.X(i) <= 0.85f, "x in map " + i); }
                T.True(WorldMapRules.Y(1) > WorldMapRules.Y(0), "path goes up");
                T.True(WorldMapRules.ContentHeight(10) > WorldMapRules.Y(9), "content fits");
                float bx = WorldMapRules.DailyBossX(1); T.True(Math.Abs(bx - WorldMapRules.X(1)) > 0.2f, "daily boss beside the path");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
        T.Add("v2_daily_boss_seeded_by_date", () =>
        {
            var d = Db.Meta.dailyBoss;
            var a = DailyBossRules.ForDate("20261024", d); var b = DailyBossRules.ForDate("20261024", d); var c = DailyBossRules.ForDate("20261025", d);
            T.Eq(a.layout, b.layout, "same date same layout"); T.Eq(a.shots, b.shots, "same shots"); T.Eq(a.name, b.name, "same boss");
            T.Eq("daily_20261024", a.id, "id"); T.True(a.shots >= d.baseShots && a.shots <= d.baseShots + d.shotSpread, "shots in range");
            T.True(Db.GetLayout(a.layout) != null, "layout exists");
            T.True(a.id != c.id, "next day differs");
            T.Eq("20261024", DailyBossRules.DateKey(new DateTime(2026, 10, 24, 23, 59, 0)), "key");
            int distinct = Enumerable.Range(0, 30).Select(i => DailyBossRules.ForDate(DailyBossRules.DateKey(new DateTime(2026, 11, 1).AddDays(i)), d).layout).Distinct().Count();
            T.True(distinct >= 2, "rotates layouts over a month");
            T.Eq(1f, DailyBossRules.HpFraction(0f, 0.9f), "full HP"); T.Eq(0f, DailyBossRules.HpFraction(0.9f, 0.9f), "dead at threshold");
            var path = Tmp();
            try
            {
                var p = new PlayerProgress(path);
                T.True(!p.RecordDailyBossWin("20261024", d.reward).IsEmpty, "first win pays"); T.True(p.RecordDailyBossWin("20261024", d.reward).IsEmpty, "once per day");
                T.Eq(d.reward.coins, p.Data.coins, "reward coins");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
        T.Add("v2_seasons_and_achievements", () =>
        {
            var m = Db.Meta; var ch = Db.Chapter1;
            T.Eq(4, m.seasons.Length, "4 seasons");
            T.Eq("Sunny Shores,Frozen Peaks,Neon City,Primeval Peaks", string.Join(",", m.seasons.Select(s => s.name)), "names");
            T.Eq("bg_season_primeval_peaks", m.seasons[3].bg, "season 4 bg");
            T.Eq("Sunny Shores", SeasonRules.ForLevel(m, 7).name, "L7 season 1");
            var path = Tmp();
            try
            {
                var save = new CampaignSave(path);
                T.Eq("Sunny Shores", SeasonRules.Current(m, ch, save).name, "current");
                T.True(SeasonRules.IsOpen(m.seasons[0], ch, save) && !SeasonRules.IsOpen(m.seasons[1], ch, save), "S1 open, S2 coming soon");
                T.Eq(10, SeasonRules.LevelsInSeason(m.seasons[0], ch), "10 levels in S1");
                save.RecordResult(ch.levels[0].id, true, 3, false, 1);
                T.Eq(3, SeasonRules.StarsInSeason(m.seasons[0], ch, save), "season stars");
                T.True(m.achievements.Length >= 6, "achievement stubs");
                foreach (var a in m.achievements) T.True(a.target > 0 && !string.IsNullOrEmpty(a.name), "achievement " + a.id);
                var first = m.achievements.First(a => a.type == "levels_cleared");
                T.Eq(1, AchievementRules.Progress(first, ch, save, null), "1 level cleared");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
        T.Add("v2_hero_slot_and_power_up_grid", () =>
        {
            foreach (var l in Db.Chapter1.levels) T.Eq(null, HeroSlotRules.Validate(l.heroSlot), "hero slot ok " + l.id);
            T.Eq(null, HeroSlotRules.Validate(new HeroSlot { kind = "gatehouse", fbx = "Bld_Gatehouse_A", pos = new[] { 8f, 0f, 2f } }), "gatehouse ok");
            T.True(HeroSlotRules.Validate(new HeroSlot { kind = "tower" }) != null, "only keep or gatehouse");
            T.True(HeroSlotRules.Validate(new HeroSlot { kind = "keep", fbx = "Castle" }) != null, "Bld_* only");
            T.Eq(3, HeroSlotRules.Position(null).Length, "default anchor");
            foreach (var p in Db.PowerUps.powerups) T.True(PowerUpRulesV2.Tagline(p.effect).Length > 0, "grid caption " + p.id);
        });
    }
}
