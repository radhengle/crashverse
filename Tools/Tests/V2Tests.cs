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
    }
}
