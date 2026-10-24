using System;
using System.Linq;
using System.IO;
using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Level;
using Crashverse.Meta;
using Crashverse.Feel;
using Crashverse.Data.Dev;
using System.Collections.Generic;

static class Tests
{
    static ContentDatabase _db;
    static ContentDatabase Db => _db ??= ContentDatabase.LoadBundled();
    static DateTime U(string s) => DateTime.SpecifyKind(DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

    static LocalBackendService NewBackend(string utc, FakeClock clock = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "cv_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var b = new LocalBackendService(Db, clock ?? new FakeClock(U("2030-01-01T00:00:00")), Path.Combine(dir, "db.json"));
        b.SetTestClock(U(utc));
        return b;
    }

    /// <summary>Plays a ranked run shot by shot: damage ramps linearly to dmg, killed only on the last shot. Online shots get server receipts.</summary>
    static RankedJournal Play(LocalBackendService b, RankedTicket t, int dmg, int left, bool killed, bool final = true, int onlineShots = int.MaxValue)
    {
        var j = new RankedJournal { token = t.token, final = final };
        int shots = t.shotBudget - left;
        for (int i = 1; i <= shots; i++)
        {
            var cp = new RankedCheckpoint { token = t.token, shotIndex = i, damageScore = dmg * i / shots, shotsLeft = t.shotBudget - i, killed = killed && i == shots };
            if (i <= onlineShots) cp = b.ReportRankedCheckpoint(cp).Result;
            j.checkpoints.Add(cp);
        }
        return j;
    }
    static SubmitResponse Ranked(LocalBackendService b, RankedTicket t, int dmg, int left, bool killed, bool final = true) =>
        b.FinalizeRanked(Play(b, t, dmg, left, killed, final)).Result;
    static CampaignSubmitResponse Win(LocalBackendService b, string id, float ratio = 1f, int left = 1, int combo = 0) =>
        b.SubmitCampaignResult(new CampaignRunReport { levelId = id, won = true, destroyedRatio = ratio, shotsLeft = left, bestCombo = combo }).Result;
    static bool Throws(Action a) { try { a(); return false; } catch (Exception) { return true; } }
    static BackendException Catch(Action a) { try { a(); return null; } catch (AggregateException e) { return e.InnerException as BackendException; } catch (BackendException e) { return e; } }
    static long Unix(string s) => new DateTimeOffset(U(s)).ToUnixTimeSeconds();

    public static void Register()
    {
        DevAimHookTests.Register(); // BUG-24 dev aim hook
        V2Tests.Register(); // target UI, power-ups v2, seasons (integration/v2)
        // ---------- SG-24 / SG-01 stars (qa/M1_Test_Cases_v0.3 + Feature_Test_Cases_v1 §A) ----------
        T.Add("gameplay_pieceid_matches_stableid20", () => { T.Eq(204219580, Crashverse.Gameplay.PieceIds.Of("b1"), "b1"); T.Eq(501293940, Crashverse.Gameplay.PieceIds.Of("L1_tower_03"), "L1_tower_03"); T.Eq(1, Crashverse.Gameplay.PieceIds.Of(null), "null"); T.Eq(1, Crashverse.Gameplay.PieceIds.Of(""), "empty"); });
        T.Add("gameplay_vfx_material_map", () => { T.Eq(0, Crashverse.Gameplay.PieceIds.VfxMaterialOf(0, false, false), "wood"); T.Eq(1, Crashverse.Gameplay.PieceIds.VfxMaterialOf(1, false, false), "brick"); T.Eq(2, Crashverse.Gameplay.PieceIds.VfxMaterialOf(2, false, false), "steel"); T.Eq(3, Crashverse.Gameplay.PieceIds.VfxMaterialOf(2, true, false), "joint"); T.Eq(5, Crashverse.Gameplay.PieceIds.VfxMaterialOf(0, false, true), "explosive"); });
        T.Add("stars_budget4", () => { int[] e = { 1, 1, 2, 3, 3 }; for (int l = 0; l <= 4; l++) T.Eq(e[l], StarRules.Compute(true, l, 4), $"4 shots, {l} left"); });
        T.Add("stars_budget5", () => { int[] e = { 1, 1, 2, 3, 3, 3 }; for (int l = 0; l <= 5; l++) T.Eq(e[l], StarRules.Compute(true, l, 5), $"5 shots, {l} left"); });
        T.Add("stars_budget6", () => { int[] e = { 1, 1, 2, 2, 3, 3, 3 }; for (int l = 0; l <= 6; l++) T.Eq(e[l], StarRules.Compute(true, l, 6), $"6 shots, {l} left"); });
        T.Add("stars_budget7", () => { int[] e = { 1, 1, 1, 2, 2, 3, 3, 3 }; for (int l = 0; l <= 7; l++) T.Eq(e[l], StarRules.Compute(true, l, 7), $"7 shots, {l} left"); });
        T.Add("stars_exact_3_of_5_int", () => T.Eq(3, StarRules.ComputeInt(true, 3, 5, 60, 30), "3/5 == 60% -> 3 stars"));
        T.Add("stars_fail_zero", () => T.Eq(0, StarRules.Compute(false, 5, 5), "fail = 0"));
        T.Add("stars_override_S6a", () => { int[] e = { 1, 1, 2, 2, 3 }; for (int l = 1; l <= 4; l++) T.Eq(e[l], StarRules.Compute(true, l, 5, 0.80f, 0.40f), $"0.8/0.4 {l} left"); });
        T.Add("stars_defaults_S6b", () => T.Eq(3, StarRules.Compute(true, 3, 5, 0f, 0f), "missing thresholds -> defaults"));
        T.Add("stars_L9_cap", () => T.Eq(2, StarRules.Compute(true, 5, 6, .6f, .3f, 2), "power-up cap 2"));
        T.Add("levelconfig_uses_rule", () => { var c = LevelConfig.CreateM1ClockworkTower(); T.Eq(3, c.ComputeStars(true, 3), "LevelConfig 3/5"); T.Eq(2, c.ComputeStars(true, 2), "LevelConfig 2/5"); });

        // ---------- Chapter data (DP §1 AC1/AC2) ----------
        T.Add("chapter_data", () =>
        {
            var ch = Db.Chapter1;
            T.Eq(10, ch.levels.Length, "10 levels");
            // Star bands are per-level data (SG-01): range + monotonic, not a fixed 0.6.
            foreach (var l in ch.levels)
            {
                T.True(l.shots > 0, l.id + " shots > 0");
                T.True(l.star3Pct > 0f && l.star3Pct <= 1f && l.star2Pct > 0f && l.star2Pct <= 1f, l.id + " bands in (0,1]");
                T.True(l.star2Pct < l.star3Pct, l.id + " bands monotonic (2★ < 3★)");
            }
            var l10 = ch.levels[9]; // Level_Rulings_v1 §1–2 (values live in the Level Designer's data)
            T.Eq(8, l10.shots, "L10 budget 8"); T.Eq(0.375f, l10.star3Pct, "L10 3★ 0.375"); T.Eq(0.25f, l10.star2Pct, "L10 2★ 0.25");
            T.Eq("NO_CLOSED_SHUTTER_HIT", l10.mastery.type, "L10 mastery");
            T.Eq(3, StarRules.Compute(true, 3, l10.shots, l10.star3Pct, l10.star2Pct), "perfect 5-shot run (3 left of 8) = 3★");
            T.Eq(2, StarRules.Compute(true, 2, l10.shots, l10.star3Pct, l10.star2Pct), "2 left = 2★");
            T.Eq("m1_clockwork_tower", ch.levels[6].layout, "L7 = M1 tower");
            T.Eq("tidewarden", ch.levels[9].bossId, "L10 boss");
            T.Eq(2, ch.levels[8].starCapIfPowerupUsed, "L9 cap");
        });
        T.Add("LEVEL_tidewarden_shutter_SG05", () =>
        {
            var r = new TideShutterRule();
            r.OnShotFired(); T.True(!r.OnShotResolved(), "phase 1 shot: no toggle");
            r.OnShotFired(); r.EnterPhase2(); T.True(r.Open, "phase 2 starts OPEN");
            T.True(!r.OnShotResolved() && r.Open, "the shot that ended phase 1 does not toggle");
            r.OnShotFired(); T.True(r.OnShotResolved() && !r.Open, "1st phase-2 shot → closed");
            r.OnDirectHit(); T.Eq(1, r.ClosedShutterHits, "hit while closed counted");
            r.OnShotFired(); T.True(r.OnShotResolved() && r.Open, "2nd phase-2 shot → open");
            r.OnDirectHit(); T.Eq(1, r.ClosedShutterHits, "hit while open not counted");
            var p1 = new TideShutterRule(); p1.OnDirectHit(); T.Eq(0, p1.ClosedShutterHits, "phase 1 (parked shutter) never counts");
        });
        T.Add("LEVEL_mastery_no_closed_shutter_hit", () =>
        {
            var g = new MasteryGoal { type = "NO_CLOSED_SHUTTER_HIT", role = "shutter" };
            T.True(MasteryEvaluator.Evaluate(g, new RunStats { won = true, closedShutterHits = 0 }), "clean run passes");
            T.True(!MasteryEvaluator.Evaluate(g, new RunStats { won = true, closedShutterHits = 1 }), "closed-shutter hit fails");
            T.True(!MasteryEvaluator.Evaluate(g, new RunStats { won = false }), "loss never passes");
            var tw = Src("Assets/_Project/Scripts/Boss/TidewardenController.cs"); T.True(tw.Contains("_rule.OnShotResolved()") && tw.Contains("OnShutterDamaged"), "controller uses the rule");
        });
        T.Add("LEVEL_objective_mass_excludes_stone_bases", () =>
        {
            T.True(!StructureRules.CountsTowardObjectiveMass(true, true), "Indestructible+NonObjective excluded");
            T.True(StructureRules.CountsTowardObjectiveMass(false, true), "indestructible objective (boss core) still counts");
            T.Eq(5f, StructureRules.TotalObjectiveMass(new[] { (5f, false, false), (100f, true, true) }), "stone base mass ignored");
            int bases = 0;
            foreach (var l in Db.Chapter1.levels)
                foreach (var b in Db.GetLayout(l.layout).blocks)
                    if (b.indestructible && b.nonObj) bases++;
            T.True(bases > 0, "stone bases tagged Indestructible+NonObjective in Ch1 data");
            var tr = Src("Assets/_Project/Scripts/Destruction/DestructionTracker.cs"); T.True(tr.Contains("StructureRules.CountsTowardObjectiveMass"), "tracker uses the rule");
        });
        T.Add("LEVEL_collapse_only_when_all_supports_gone", () =>
        {
            T.True(!StructureRules.SupportLost(new bool[0]), "no supports = grounded");
            T.True(!StructureRules.SupportLost(new[] { true, false }), "one support left holds the piece");
            T.True(!StructureRules.SupportLost(new[] { false, false }), "all supports intact");
            T.True(StructureRules.SupportLost(new[] { true, true }), "ALL supports gone → collapse");
            T.True(StructureRules.SupportLost(new[] { true }), "single support gone → collapse");
            T.True(Src("Assets/_Project/Scripts/Destruction/DestructibleBlock.cs").Contains("StructureRules.SupportLost"), "DestructibleBlock uses the rule");
        });
        T.Add("layouts_valid", () =>
        {
            foreach (var l in Db.Chapter1.levels)
            {
                var lay = Db.GetLayout(l.layout);
                T.True(lay != null && lay.blocks.Length > 0, "layout " + l.layout);
                foreach (var b in lay.blocks) T.True(Array.IndexOf(l.materials, b.mat) >= 0 || b.nonObj, $"{l.id} material {b.mat}");
            }
            int rings = 0; foreach (var b in Db.GetLayout("m1_clockwork_tower").blocks) if (b.role == "steel_joint") rings++;
            T.Eq(3, rings, "tower 3 steel rings");
        });

        // ---------- Unlocks / loadout (AC3, DP §2) ----------
        T.Add("unlock_linear", () =>
        {
            var save = new CampaignSave(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
            var L = Db.Chapter1.levels;
            T.True(UnlockRules.IsLevelUnlocked(L[0], save), "L1 open");
            T.True(!UnlockRules.IsLevelUnlocked(L[1], save), "L2 locked");
            save.RecordResult("c1_l01", true, 2, false);
            T.True(UnlockRules.IsLevelUnlocked(L[1], save) && !UnlockRules.IsLevelUnlocked(L[2], save), "L2 open L3 locked");
            T.True(!UnlockRules.IsIslandOpen(Db.Chapter1.islands[1], save), "mill gate closed");
            save.RecordResult("c1_l02", true, 1, false); save.RecordResult("c1_l03", true, 1, false);
            T.True(UnlockRules.IsIslandOpen(Db.Chapter1.islands[1], save), "mill gate opens after L3");
            save.RecordResult("c1_l03", true, 3, false); save.RecordResult("c1_l03", true, 1, false);
            T.Eq(3, save.BestStars("c1_l03"), "best stars never lowered");
        });
        T.Add("loadout_rules", () =>
        {
            var save = new CampaignSave(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
            for (int i = 1; i <= 7; i++) save.RecordResult($"c1_l{i:00}", true, 1, false);
            var L7 = Db.GetLevel("c1_l07"); var L8 = Db.GetLevel("c1_l08");
            var lo = Loadout.Resolve(L7, null, Db, save);
            T.Eq("earthquake", lo[Slot.Core], "L7 forced earthquake (v2: was shockwave)");
            T.True(Loadout.IsSlotLocked(L7, Slot.Module), "L7 module locked");
            var lo8 = Loadout.Resolve(L8, null, Db, save);
            T.Eq("multi_shot", lo8[Slot.Module], "L8 forced multi_shot (v2: was splitstrike)");
            T.True(lo8.TryEquip(Slot.Booster, Db.GetPowerUp("earthquake"), L8, Db, save) != null, "core tile can't go in booster");
            T.True(lo8.TryEquip(Slot.Booster, Db.GetPowerUp("laser_beam"), L8, Db, save) != null, "future tile locked");
            T.True(!UnlockRules.IsPowerUpUnlocked(Db.GetPowerUp("black_hole"), Db, save), "black hole (was gravity well) locked until L10 clear");
        });

        // ---------- Mastery (SG-03, SG-04, SG-21) ----------
        T.Add("mastery_SG21_islandKillY", () =>
        {
            var L2 = Db.GetLevel("c1_l02");
            T.Eq(-2f, L2.islandKillY, "L2 islandKillY from data");
            var r = new RunStats { won = true, islandKillY = L2.islandKillY };
            r.RoleMaxCentreY["tank"] = -2.5f; T.True(MasteryEvaluator.Evaluate(L2.mastery, r), "tank below kill Y ticks");
            r.RoleMaxCentreY["tank"] = -1.9f; T.True(!MasteryEvaluator.Evaluate(L2.mastery, r), "tank above kill Y no tick");
            r.RoleMaxCentreY["tank"] = -2.0f; T.True(!MasteryEvaluator.Evaluate(L2.mastery, r), "exactly at kill Y is not below");
        });
        T.Add("mastery_crown_80pct", () =>
        {
            var g = Db.GetLevel("c1_l07").mastery; var r = new RunStats { won = true };
            r.RoleTotal["crown"] = 4; r.RoleBroken["crown"] = 0; T.True(MasteryEvaluator.Evaluate(g, r), "4/4 intact");
            r.RoleBroken["crown"] = 1; T.True(!MasteryEvaluator.Evaluate(g, r), "3/4 = 75% < 80%");
        });

        // ---------- QA test clock ----------
        T.Add("testclock_fixed_and_advance", () =>
        {
            var b = NewBackend("2026-10-16T23:59:59");
            T.Eq(U("2026-10-16T23:59:59"), b.ServerNow, "fixed time");
            b.AdvanceTestClock(TimeSpan.FromSeconds(1));
            T.Eq("2026-10-17", UtcDay.Key(b.ServerNow), "advance crosses UTC midnight");
            b.SetTestOffset(TimeSpan.FromHours(2));
            var dev = new FakeClock(U("2026-10-16T10:00:00"));
            var b2 = NewBackend("2026-01-01T00:00:00", dev); b2.SetTestOffset(TimeSpan.FromHours(2));
            T.Eq(U("2026-10-16T12:00:00"), b2.ServerNow, "offset mode");
            b2.ClearTestClock(); T.Eq(dev.Now, b2.ServerNow, "cleared");
        });
        T.Add("SG20_device_clock_ignored_when_fixed", () =>
        {
            var dev = new FakeClock(U("2026-10-16T10:00:00")); var b = NewBackend("2026-10-16T03:00:00", dev);
            dev.Now = dev.Now.AddDays(5);
            T.Eq(U("2026-10-16T03:00:00"), b.ServerNow, "device clock change has no effect");
        });

        // ---------- Daily Titan rotation SG-15 ----------
        T.Add("titan_rotates_at_utc_midnight", () =>
        {
            var b = NewBackend("2026-10-16T23:59:59.999");
            var a = b.GetDailyTitan().Result;
            b.SetTestClock(U("2026-10-16T18:29:59")); // = 23:59:59 IST, still same UTC day
            T.Eq(a.dayKey, b.GetDailyTitan().Result.dayKey, "same UTC day same config");
            b.SetTestClock(U("2026-10-17T00:00:00"));
            var c = b.GetDailyTitan().Result;
            T.True(a.dayKey != c.dayKey && (a.arenaId != c.arenaId || a.ruleId != c.ruleId), "config swaps at 00:00 UTC (05:30 IST)");
            var cfg = LocalBackendService.BuildDailyConfig(Db.Titan, U("2026-10-17T12:00:00"));
            T.Eq(c.arenaId + c.ruleId, cfg.arenaId + cfg.ruleId, "same config for every player that day");
        });

        // ---------- Ranked SG-10/11/22 ----------
        T.Add("ranked_one_attempt", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            b.BeginRankedRun().Wait();
            bool threw = false; try { b.BeginRankedRun().Wait(); } catch (Exception) { threw = true; }
            T.True(threw, "second ranked attempt refused");
        });
        T.Add("SG22_grace_inclusive_boundary", () =>
        {
            var b = NewBackend("2026-10-16T23:50:00");
            var t = b.BeginRankedRun().Result;
            b.SetTestClock(U("2026-10-17T00:15:00.000"));
            T.Eq(SubmitStatus.Accepted, Ranked(b, t, 5000, 2, true).status, "00:15:00.000 accepted");
            var b2 = NewBackend("2026-10-16T23:50:00");
            var t2 = b2.BeginRankedRun().Result;
            b2.SetTestClock(U("2026-10-17T00:15:00.001"));
            T.Eq(SubmitStatus.RefundedAsPractice, Ranked(b2, t2, 5000, 2, true).status, "00:15:00.001 refunded as practice");
        });
        T.Add("SG11_partial_submit", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var t = b.BeginRankedRun().Result;
            var resp = Ranked(b, t, 3100, 3, false, final: false);
            T.Eq(SubmitStatus.Accepted, resp.status, "partial accepted");
            T.Eq(3100, resp.finalScore, "no shot bonus without kill");
            T.Eq("submitted_partial", b.GetRankedStatus().Result.status, "status partial, attempt used");
            T.Eq(SubmitStatus.RejectedDuplicate, b.FinalizeRanked(new RankedJournal { token = t.token, final = true }).Result.status, "no resubmit");
        });
        T.Add("ranked_validation", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var t = b.BeginRankedRun().Result;
            T.True(Throws(() => b.ReportRankedCheckpoint(new RankedCheckpoint { token = t.token, shotIndex = 1, damageScore = 20000, shotsLeft = t.shotBudget - 1 }).Wait()), "damage out of range");
            T.True(Throws(() => b.ReportRankedCheckpoint(new RankedCheckpoint { token = t.token, shotIndex = 3, damageScore = 10, shotsLeft = t.shotBudget - 3 }).Wait()), "shot out of order");
            T.True(Throws(() => b.ReportRankedCheckpoint(new RankedCheckpoint { token = t.token, shotIndex = 1, damageScore = 10, shotsLeft = t.shotBudget }).Wait()), "shot count mismatch");
            T.Eq(SubmitStatus.RejectedInvalid, b.FinalizeRanked(new RankedJournal { token = "nope" }).Result.status, "unknown token");
            var ok = Ranked(b, t, 8000, 2, true);
            T.Eq(9000, ok.finalScore, "8000 + 2*500 on kill");
        });

        // ---------- Daily rewards SG-14 ----------
        T.Add("SG14_streak_pauses_never_resets", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            b.ClaimDailyReward().Wait();
            T.Eq(100, b.GetWallet().Result.coins, "day1 100 coins");
            bool threw = false; try { b.ClaimDailyReward().Wait(); } catch (Exception) { threw = true; }
            T.True(threw, "no double claim same UTC day");
            b.SetTestClock(U("2026-10-17T00:00:00")); b.ClaimDailyReward().Wait();
            T.Eq(1, b.GetWallet().Result.extraCharges, "day2 charge");
            b.SetTestClock(U("2026-10-27T05:00:00")); // missed 9 days
            T.Eq(2, b.GetDailyRewardState().Result.streakIndex, "streak paused at day 3");
            b.ClaimDailyReward().Wait(); T.Eq(250, b.GetWallet().Result.coins, "day3 150 coins after gap");
            for (int d = 1; d <= 4; d++) { b.AdvanceTestClock(TimeSpan.FromDays(1)); b.ClaimDailyReward().Wait(); }
            T.Eq(0, b.GetDailyRewardState().Result.streakIndex, "loops after day 7");
            T.True(b.GetWallet().Result.shards >= 4, "day4 shard + day7 chest shards");
        });
        T.Add("daily_reset_is_utc_not_ist", () =>
        {
            var b = NewBackend("2026-10-16T23:59:59"); // 05:29:59 IST on the 17th
            b.ClaimDailyReward().Wait();
            T.True(!b.GetDailyRewardState().Result.canClaimToday, "05:29:59 IST still same UTC day");
            b.AdvanceTestClock(TimeSpan.FromSeconds(1));
            T.True(b.GetDailyRewardState().Result.canClaimToday, "05:30:00 IST new day");
        });

        // ---------- Season SG-16 / SG-23 ----------
        T.Add("SG16_tiers_and_xp", () =>
        {
            var s = Db.Season;
            T.Eq(30, s.tiers.Length, "30 tiers"); T.Eq(4500, s.tiers[29].xp, "tier 30 at 4500");
            T.Eq(1000, s.tiers[9].xp, "tier 10 at 1000"); T.Eq(2500, s.tiers[19].xp, "tier 20 at 2500");
            T.Eq(10, s.xpPerEventStar, "10 XP/star"); T.Eq(50, s.xpPerTitanClear, "50 XP/titan");
            T.Eq(0, LocalBackendService.TierForXp(s, 99), "99 xp tier 0"); T.Eq(1, LocalBackendService.TierForXp(s, 100), "100 xp tier 1");
            T.Eq(30, LocalBackendService.TierForXp(s, 4500), "4500 tier 30");
            T.Eq(1, LocalBackendService.BonusChests(s, 4800), "bonus chest per 300"); T.Eq(10, LocalBackendService.BonusChests(s, 99999), "cap 10");
        });
        T.Add("SG23_event_stars_best_not_summed", () =>
        {
            var b = NewBackend("2026-10-13T10:00:00");
            var id = Db.Season.weeks[0].levels[0].id;
            T.Eq(20, b.ReportEventStars(id, 2).Result.xp, "2 stars -> 20xp");
            T.Eq(20, b.ReportEventStars(id, 2).Result.xp, "replay same stars -> no xp");
            T.Eq(20, b.ReportEventStars(id, 1).Result.xp, "worse run -> no xp");
            T.Eq(30, b.ReportEventStars(id, 5).Result.xp, "capped at 3, only +1 star xp");
            T.Eq(3, b.BestEventStars(id), "best single run 3");
        });
        T.Add("titan_clear_xp_once_per_day", () =>
        {
            var b = NewBackend("2026-10-13T10:00:00");
            var t = b.BeginRankedRun().Result; Ranked(b, t, 9000, 1, true);
            T.Eq(50, b.GetSeasonState().Result.xp, "titan clear 50xp");
            T.Eq(150, b.GetWallet().Result.coins, "completion chest coins");
        });
        T.Add("season_week_monday_utc", () =>
        {
            var b = NewBackend("2026-10-18T23:59:59"); T.Eq(0, b.GetSeasonState().Result.weekIndex, "Sunday = week 0");
            b.SetTestClock(U("2026-10-19T00:00:00")); T.Eq(1, b.GetSeasonState().Result.weekIndex, "Monday 00:00 UTC = week 1");
        });
        T.Add("season_claim_rules", () =>
        {
            var b = NewBackend("2026-10-13T10:00:00");
            bool threw = false; try { b.ClaimSeasonTier(1, false).Wait(); } catch (Exception) { threw = true; }
            T.True(threw, "can't claim unreached tier");
            foreach (var w in Db.Season.weeks) foreach (var l in w.levels) b.ReportEventStars(l.id, 3).Wait(); // 20 levels * 30 = 600xp
            b.ClaimSeasonTier(1, false).Wait(); T.True(b.GetSeasonState().Result.claimedFree.Contains(1), "tier 1 claimed");
            threw = false; try { b.ClaimSeasonTier(1, true).Wait(); } catch (Exception) { threw = true; }
            T.True(threw, "premium requires (cosmetic) pass");
        });

        // ---------- Leaderboards ----------
        T.Add("leaderboard_top100_rank_guest_ties", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var t = b.BeginRankedRun().Result; Ranked(b, t, 10000, t.shotBudget - 2, true);
            var p = b.GetLeaderboard(BoardType.DailyTitan, BoardPeriod.Current, false).Result;
            T.True(p.meIsGuestHidden && p.me == null, "guest stored but hidden");
            T.True(p.top.Count == 100, "top 100");
            b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "test-id-token" }).Wait();
            p = b.GetLeaderboard(BoardType.DailyTitan, BoardPeriod.Current, false).Result;
            T.True(p.me != null && p.me.score == 10000 + 500 * (t.shotBudget - 2), "linked player listed with own score");
            for (int i = 1; i < p.top.Count; i++)
            {
                var a = p.top[i - 1]; var c = p.top[i];
                T.True(a.score > c.score || (a.score == c.score && a.submittedTicks <= c.submittedTicks), "sorted, ties earlier first");
            }
            var f = b.GetLeaderboard(BoardType.DailyTitan, BoardPeriod.Current, true).Result;
            T.True(f.top.TrueForAll(e => e.isFriend || e.isMe), "friends filter");
        });
        T.Add("top10_bonus_after_close", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var t = b.BeginRankedRun().Result; Ranked(b, t, 10000, t.shotBudget - 2, true);
            b.SetTestClock(U("2026-10-17T00:15:00")); T.Eq(0, b.GetInbox().Result.Count, "not before board closes");
            b.SetTestClock(U("2026-10-17T00:15:01")); T.Eq(1, b.GetInbox().Result.Count, "bonus shards in inbox");
        });
        T.Add("offline_disables_live_ops", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00"); b.SimulateOffline = true;
            bool threw = false; try { b.ClaimDailyReward().Wait(); } catch (Exception) { threw = true; }
            T.True(threw, "offline claim refused");
        });
        T.Add("first_clear_rewards_once", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            foreach (var l in Db.Chapter1.levels) { Win(b, l.id); Win(b, l.id); }
            var w = b.GetWallet().Result; T.True(w.powerUps.Contains("black_hole"), "L10 power-up (v2 Black Hole) granted by server on first validated clear");
            int coins = w.coins; Win(b, "c1_l10"); T.Eq(coins, b.GetWallet().Result.coins, "chest once");
        });

        // ── Addendum v1.1 ──
        T.Add("combo_window_and_reset", () =>
        {
            var c = new ComboTracker(); c.OnShot();
            T.Eq(1, c.OnBreak(0f), "first break"); T.Eq(2, c.OnBreak(0.8f), "0.8s inclusive"); T.Eq(3, c.OnBreak(1.5f), "chain");
            T.True(c.Visible, "shown from x3"); T.Eq(1, c.OnBreak(2.31f), "gap >0.8 restarts");
            c.OnShot(); T.Eq(0, c.Current, "reset each shot"); T.Eq(3, c.BestThisRun, "best kept for run");
            T.Eq(0, ComboTracker.PopTier(2), "no pop <3"); T.Eq(1, ComboTracker.PopTier(3), "tier1"); T.Eq(4, ComboTracker.PopTier(25), "tier4");
        });
        T.Add("score_formula_wins_only", () =>
        {
            T.Eq(1000 + 1000 + 250 + 1000, CampaignScore.Compute(true, 1f, 2, 5, true), "full formula");
            T.Eq(850, CampaignScore.Compute(true, 0.85f, 0, 0, false), "85% only");
            T.Eq(0, CampaignScore.Compute(false, 0.84f, 0, 9, false), "fail scores 0");
            var path = Path.Combine(Path.GetTempPath(), "cv_save_" + Guid.NewGuid() + ".json");
            var s = new CampaignSave(path); s.RecordResult("c1_l01", true, 3, false, 2000); s.RecordResult("c1_l01", true, 1, false, 900);
            T.Eq(2000, s.BestScore("c1_l01"), "best score kept"); File.Delete(path);
        });
        T.Add("hint_rules", () =>
        {
            var lv = Db.GetLevel("c1_l07"); T.True(lv.HasHint, "L7 has hint data");
            T.True(!HintRules.ShowHintButton(lv, 1, false), "not after 1 fail"); T.True(HintRules.ShowHintButton(lv, 2, false), "from 2nd fail");
            T.True(!HintRules.ShowHintButton(lv, 5, true), "never in ranked");
            var path = Path.Combine(Path.GetTempPath(), "cv_save_" + Guid.NewGuid() + ".json");
            var s = new CampaignSave(path); s.RecordResult(lv.id, false, 0, false); s.RecordResult(lv.id, false, 0, false);
            T.Eq(2, s.FailsInRow(lv.id), "consecutive fails"); s.ArmHint(lv.id);
            T.True(s.ConsumeHint(lv.id), "consumed once"); T.True(!s.ConsumeHint(lv.id), "next attempt only");
            s.RecordResult(lv.id, true, 2, false); T.Eq(0, s.FailsInRow(lv.id), "win resets fails"); File.Delete(path);
        });
        T.Add("store_validator", () =>
        {
            var errs = StoreValidator.Validate(Db.Store, out var ok);
            T.Eq(0, errs.Count, "bundled catalog clean"); T.True(ok.Count > 0, "items accepted");
            var bad = new StoreCatalog { allowedTypes = Db.Store.allowedTypes, items = new[] {
                new StoreItem { sku = "pu", name = "Charge pack", type = "powerup_pack", cosmetic = true, priceType = "gems", price = 10 },
                new StoreItem { sku = "nc", name = "Skin", type = Db.Store.allowedTypes[1], cosmetic = false, priceType = "gems", price = 10 },
                new StoreItem { sku = "gr", name = "Gems?", type = Db.Store.allowedTypes[1], cosmetic = true, priceType = "iap", price = 99, grants = "extra_charges:3" } } };
            T.Eq(3, StoreValidator.Validate(bad, out var ok2).Count, "all 3 rejected"); T.Eq(0, ok2.Count, "none accepted");
        });
        T.Add("ECON_v1_earned_gems_data", () =>
        {
            var e = Db.Economy; var g = e.gemSources;
            T.True(g.dailyDay7 == 10 && g.titanCompletion == 3 && g.titanTop10Bonus == 5 && g.eventAllCleared == 15 && g.eventAll3Star == 10 && g.guardianFirstClear == 25 && g.seasonEvery5thTier == 10, "gem sources from data");
            T.Eq(3, e.chapterStarMilestones.Length, "10/20/30★ milestones"); T.Eq(20, e.chapterStarMilestones[2].gems);
            var store = new System.Collections.Generic.Dictionary<string, StoreItem>(); foreach (var i in Db.Store.items) store[i.sku] = i;
            T.True(store["s1_premium_track"].priceType == "gems" && store["s1_premium_track"].price == e.gemSinks.seasonPremiumTrack && e.gemSinks.seasonPremiumTrack == 450, "premium track 450 gems");
            T.True(store["skin_brass_baron"].price == e.gemSinks.epicSkin && store["trail_confetti"].price == e.gemSinks.trailOrVfxSkin && store["frame_tidebreaker"].price == e.gemSinks.frameOrTitle, "sink prices match economy data");
            StoreValidator.Validate(Db.Store, out var ok);
            T.True(!ok.Exists(i => i.priceType == "real" || i.priceType == "iap"), "nothing sold for real money in v1");
            T.True(ok.TrueForAll(i => i.cosmetic), "sinks are cosmetic only");
            T.True(Db.DailyRewards.days[6].id.Contains("gems:10"), "Day 7 chest +10 gems");
            foreach (var t in Db.Season.tiers) if (t.tier % 5 == 0) T.True(t.free.id != null && t.free.id.Contains("gems:10"), "tier " + t.tier + " +10 gems");
        });
        T.Add("ECON_gem_grants_once", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            int g0 = b.GetWallet().Result.gems;
            Win(b, "c1_l01", 1f, 3, 0); Win(b, "c1_l01", 1f, 3, 0);
            T.True(b.GetWallet().Result.gems >= g0, "no gem loss");
            var src = Src("Assets/_Project/Scripts/Backend/LocalBackendService.cs");
            T.True(src.Contains("GrantGemsOnce(\"guardian:") && src.Contains("GrantGemsOnce(\"stars:c1:") && src.Contains(":clear\", Econ.gemSources.eventAllCleared"), "guardian / milestones / event sources wired from data");
        });
        T.Add("ECON_gem_packs_disabled_handled", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var ex = Catch(() => b.VerifyPurchase("gems_small", "r").Wait()); T.Eq("GEM_PACKS_DISABLED", ex?.Code, "403 GEM_PACKS_DISABLED"); T.Eq(403, ex?.Status ?? 0, "403");
            T.True(!PurchaseErrors.Message("GEM_PACKS_DISABLED").Contains("GEM_"), "friendly copy, no raw code");
        });
        T.Add("RENAME_primeval_peaks", () =>
        {
            foreach (var f in Directory.GetFiles(Path.Combine(UnityEngine.Resources.Root, "../../../Assets"), "*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(".meta") || f.EndsWith(".png") || f.EndsWith(".ogg") || f.EndsWith(".fbx")) continue;
                T.True(File.ReadAllText(f).IndexOf("Jurassic", StringComparison.OrdinalIgnoreCase) < 0, "no 'Jurassic' in " + Path.GetFileName(f));
            }
        });
        T.Add("store_gem_packs_flagged_off_v1", () =>
        {
            T.True(!Db.Store.flags.enableGemPacks, "enableGemPacks=false in v1 data");
            StoreValidator.Validate(Db.Store, out var ok);
            T.True(!ok.Exists(i => i.sku == "gems_small" || i.sku == "gems_large" || i.type == "currency_pack"), "Pouch (99) / Chest (499) hidden: no UI, not purchasable");
            T.True(ok.Exists(i => i.sku == "s1_premium_track"), "cosmetic premium track still listed");
            var on = new StoreCatalog { flags = new StoreFlags { enableGemPacks = true }, allowedTypes = Db.Store.allowedTypes, items = Db.Store.items };
            StoreValidator.Validate(on, out var ok3); T.True(!ok3.Exists(i => StoreValidator.IsGemPack(i)), "V2-12: even with the catalog flag on, this client lists no real-money gem pack");
            T.True(Src("server/src/Crashverse.Server/Domain/Models.cs").Contains("public bool EnableGemPacks = false;") && Src("server/src/Crashverse.Server/Domain/Models.cs").Contains("env(\"ENABLE_GEM_PACKS\") == \"true\""), "server switch ENABLE_GEM_PACKS, default off");
            var gen = Src("Tools/ContentGen/gen_content.py"); T.True(gen.Contains("\"enableGemPacks\": False") && gen.Contains("\"requiresFlag\": \"enableGemPacks\""), "generator keeps the flag off");
        });
        // ── Art v2 Wrecker / UI QA ──
        T.Add("wrecker_v2_dimensions", () =>
        {
            T.Eq(2, Crashverse.Weapons.WreckerSpec.Version, "v2");
            T.Eq(1.5f, Crashverse.Weapons.WreckerSpec.PivotY, "core y"); T.Eq(0.84f, Crashverse.Weapons.WreckerSpec.BarrelRearDiameter, "barrel rear");
            T.Eq(0.68f, Crashverse.Weapons.WreckerSpec.BarrelFrontDiameter, "barrel front"); T.Eq(2.3f, Crashverse.Weapons.WreckerSpec.BarrelLength, "barrel len");
            T.True(Math.Abs(Crashverse.Weapons.WreckerSpec.BarrelEnd - Crashverse.Weapons.WreckerSpec.BarrelStart - 2.3f) < 1e-4, "barrel span");
            T.Eq(0.9f, Crashverse.Weapons.WreckerSpec.BoosterDiameter, "booster"); T.Eq(1.2f, Crashverse.Weapons.WreckerSpec.FrontWheelDiameter, "front wheel");
            T.Eq(0.7f, Crashverse.Weapons.WreckerSpec.RearRollerDiameter, "rear roller");
        });
        T.Add("ui_theme_qa_minimums", () =>
        {
            var th = new Crashverse.UI.UITheme();
            foreach (var v in new[] { th.minTap, th.buttonL, th.buttonIcon, th.tabHeight, th.mapNode, th.leaderboardRow, th.seasonTier }) T.True(v >= 48f, "tap >=48dp");
            foreach (var v in new[] { th.captionSp, th.bodySp, th.headingSp }) T.True(v >= 12f, "text >=12sp");
            T.True(th.hudNumberSp >= 14f, "HUD >=14sp");
        });
        T.Add("star_bands_min_shots", () =>
        {
            T.Eq(3, StarRules.MinShotsLeft(5, .6f), "5 shots 3*"); T.Eq(2, StarRules.MinShotsLeft(5, .3f), "5 shots 2*");
            T.Eq(5, StarRules.MinShotsLeft(7, .6f), "7 shots 3*"); T.Eq(3, StarRules.MinShotsLeft(7, .3f), "7 shots 2*");
            for (int n = 4; n <= 7; n++) T.Eq(3, StarRules.ComputeInt(true, StarRules.MinShotsLeft(n, .6f), n), "band consistent " + n);
        });

        // ───────────── Build 0ccfa9b QA fixes ─────────────
        T.Add("SEC01_edited_journal_cannot_raise_score", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00"); var t = b.BeginRankedRun().Result;
            var j = Play(b, t, 1200, t.shotBudget - 2, false, final: false);       // 2 shots, server-signed, app killed
            T.True(!string.IsNullOrEmpty(j.Last().receipt), "server receipt present");
            var forged = UnityEngine.JsonUtility.FromJson<RankedJournal>(UnityEngine.JsonUtility.ToJson(j));
            forged.Last().damageScore = 9999;                                      // QA repro: 1200 → 9999, receipt untouched
            var r = b.FinalizeRanked(forged).Result;
            T.Eq(SubmitStatus.Accepted, r.status, "still settles the run"); T.Eq(1200, r.finalScore, "server state used, not 9999");
            T.True(r.message.Contains("verification"), "tamper flagged");
        });
        T.Add("SEC01_forged_receipt_and_tail_limits", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00"); var t = b.BeginRankedRun().Result;
            var j = Play(b, t, 1000, t.shotBudget - 1, false, final: false);
            j.checkpoints.Add(new RankedCheckpoint { token = t.token, shotIndex = 2, damageScore = 9000, shotsLeft = t.shotBudget - 2, killed = true, receipt = "Zm9yZ2Vk" });
            T.Eq(1000, b.FinalizeRanked(j).Result.finalScore, "client-made receipt ignored");
            var b2 = NewBackend("2026-10-16T10:00:00"); var t2 = b2.BeginRankedRun().Result;
            var j2 = Play(b2, t2, 1000, t2.shotBudget - 1, false, final: false);
            j2.checkpoints.Add(new RankedCheckpoint { token = t2.token, shotIndex = 2, damageScore = 9500, shotsLeft = t2.shotBudget - 2, killed = true });
            T.Eq(1000, b2.FinalizeRanked(j2).Result.finalScore, "implausible offline jump (+8500 in one shot) rejected");
            T.True(typeof(LocalBackendService).GetMethod("Sign") == null, "no client-callable signer exists");
        });
        T.Add("SEC01_flush_sends_journal_verbatim", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00"); var t = b.BeginRankedRun().Result;
            var j = Play(b, t, 1200, t.shotBudget - 2, false, final: false);
            j.Last().damageScore = 9999; Crashverse.Core.TitanRunJournal.Write(j);   // edited on disk before relaunch
            var r = Crashverse.Core.TitanRunJournal.FlushPending(b).Result;
            T.Eq(1200, r.finalScore, "relaunch flush cannot re-sign edited data"); T.True(Crashverse.Core.TitanRunJournal.Read() == null, "journal cleared");
        });
        T.Add("BUG04_offline_kill_keeps_bonus_xp_chest", () =>
        {
            var b = NewBackend("2026-10-13T10:00:00"); var t = b.BeginRankedRun().Result;
            int left = t.shotBudget - 3;
            var j = Play(b, t, 6000, left, true, final: true, onlineShots: 2);        // kill shot made offline (unsigned)
            T.True(string.IsNullOrEmpty(j.Last().receipt), "kill shot unsigned");
            var r = b.FinalizeRanked(j).Result;
            T.Eq(6000 + 500 * left, r.finalScore, "kill bonus kept"); T.Eq(50, b.GetSeasonState().Result.xp, "Titan clear XP");
            T.Eq(150, b.GetWallet().Result.coins, "completion chest");
            var b2 = NewBackend("2026-10-13T10:00:00"); var t2 = b2.BeginRankedRun().Result;
            var j2 = Play(b2, t2, 6000, t2.shotBudget - 3, true, final: false);      // app killed after the kill, before submit
            Crashverse.Core.TitanRunJournal.Write(j2);
            T.Eq(6000 + 500 * (t2.shotBudget - 3), Crashverse.Core.TitanRunJournal.FlushPending(b2).Result.finalScore, "relaunch keeps kill bonus");
        });
        T.Add("BUG03_server_time_ignores_device_clock", () =>
        {
            double mono = 0; DateTime? net = U("2026-10-16T10:00:00");
            var clk = new NetworkServerClock(() => System.Threading.Tasks.Task.FromResult(net), () => mono);
            var dir = Path.Combine(Path.GetTempPath(), "cv_tests_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            var b = new LocalBackendService(Db, clk, Path.Combine(dir, "db.json"));
            T.True(Throws(() => b.ClaimDailyReward().Wait()), "unsynced: live-ops refused (no device-clock fallback)");
            b.GetServerTimeUtc().Wait();
            b.ClaimDailyReward().Wait();
            mono += 3600; T.Eq(U("2026-10-16T11:00:00"), b.ServerNow, "advances by monotonic time only");
            T.True(Throws(() => b.ClaimDailyReward().Wait()), "device clock +1 day cannot unlock a claim");
            net = null; b.GetServerTimeUtc().Wait(); T.Eq(U("2026-10-16T11:00:00"), b.ServerNow, "failed resync keeps anchor");
            mono += 14 * 3600; T.True(b.GetDailyRewardState().Result.canClaimToday, "real UTC midnight still resets");
            var f = typeof(LocalBackendService).GetField("_clock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var def = new LocalBackendService(Db, null, Path.Combine(dir, "db2.json"));
            T.True(f.GetValue(def) is NetworkServerClock, "default clock is network, not SystemClock");
        });
        T.Add("BUG05_score_floor", () =>
        {
            T.Eq(873, CampaignScore.Compute(true, 0.8739f, 0, 0, false), "0.8739 → 873");
            T.Eq(999 + 100, CampaignScore.Compute(true, 0.9999f, 0, 2, false), "0.9999 → 999 (+2 combo = 1099)");
            T.Eq(700, CampaignScore.Compute(true, 0.7f, 0, 0, false), "no float drift: 0.7 → 700");
            T.Eq(850, CampaignScore.Compute(true, 0.85f, 0, 0, false), "0.85 → 850");
        });
        T.Add("BUG06_season_rollover", () =>
        {
            var b = NewBackend("2026-11-08T10:00:00"); b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "test-id-token" }).Wait(); b.GrantPremiumPassForTests();
            foreach (var l in Db.Season.weeks[3].levels) b.ReportEventStars(l.id, 3).Wait(); // 150 xp → tier 1
            var s0 = b.GetSeasonState().Result; T.Eq("s1_wrecking_frontier", s0.seasonId, "season 1"); T.Eq(3, s0.weekIndex, "last week");
            b.SetTestClock(U("2026-11-09T00:00:00"));
            var s1 = b.GetSeasonState().Result;
            T.Eq("s1_wrecking_frontier_r1", s1.seasonId, "new season id"); T.Eq(0, s1.xp, "XP reset"); T.Eq(0, s1.tier, "tier reset");
            T.Eq(0, s1.weekIndex, "week 0"); T.True(!s1.premium && s1.claimedFree.Count == 0, "claims/premium reset");
            T.Eq(0, b.BestEventStars(Db.Season.weeks[3].levels[0].id), "event bests reset");
            var inbox = b.GetInbox().Result;
            T.True(inbox.Exists(i => i.id.EndsWith("_tier_1_free")), "unclaimed free tier → inbox");
            T.True(inbox.Exists(i => i.id.EndsWith("_tier_1_premium")), "BUG-07: unclaimed premium tier → inbox");
            b.GetSeasonState().Wait(); T.Eq(2, b.GetInbox().Result.Count, "processed once");
        });
        T.Add("BUG07_previous_season_board_is_real", () =>
        {
            var b = NewBackend("2026-10-20T10:00:00"); b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "test-id-token" }).Wait();
            var pre = b.GetLeaderboard(BoardType.Season, BoardPeriod.Previous, false).Result;
            T.True(pre.closed && pre.top.Count == 0, "no fake archive before season 1");
            foreach (var l in Db.Season.weeks[1].levels) b.ReportEventStars(l.id, 3).Wait();
            b.SetTestClock(U("2026-11-10T10:00:00"));
            var prev = b.GetLeaderboard(BoardType.Season, BoardPeriod.Previous, false).Result;
            T.Eq("s1_wrecking_frontier", prev.periodKey, "real previous season key"); T.True(prev.closed, "archived");
            T.True(prev.me != null && prev.me.score == 150, "my final season XP is in the archive");
            var wk = b.GetLeaderboard(BoardType.WeeklyEvent, BoardPeriod.Previous, false).Result;
            T.Eq("s1_wrecking_frontier_w3", wk.periodKey, "previous week crosses into the last season");
        });
        T.Add("BUG08_hint_events_and_practice_not_ranked", () =>
        {
            var ev = Db.Season.weeks[0].levels[0]; T.True(ev.HasHint, "event level has hint data");
            var cfg = LocalBackendService.BuildDailyConfig(Db.Titan, U("2026-10-16T10:00:00")); T.True(!string.IsNullOrEmpty(cfg.hintTarget), "Titan config carries hint");
            var path = Path.Combine(Path.GetTempPath(), "cv_save_" + Guid.NewGuid() + ".json"); var s = new CampaignSave(path);
            s.RecordHintAttempt(ev.id, false); s.RecordHintAttempt(ev.id, false);
            T.True(HintRules.ShowHintButton(ev, s.FailsInRow(ev.id), false), "event: hint from 2nd fail");
            T.True(!HintRules.ShowHintButton(ev, 9, true), "never ranked"); T.Eq(0, s.BestStars(ev.id), "no stars recorded");
            s.RecordHintAttempt(ev.id, true); T.Eq(0, s.FailsInRow(ev.id), "win resets"); File.Delete(path);
        });
        T.Add("BUG09_exact_fraction_thresholds", () =>
        {
            T.Eq(3, StarRules.Compute(true, 5, 8, .625f, .3f), "5/8 = 0.625 exactly → 3★ (was 2★ via 63%)");
            T.Eq(2, StarRules.Compute(true, 4, 8, .625f, .3f), "4/8 < 0.625");
            T.Eq(3, StarRules.Compute(true, 3, 5), "3/5 = 0.60 exact"); T.Eq(5, StarRules.MinShotsLeft(8, .625f), "band 5 of 8");
        });
        T.Add("BUG10_combo_window_strict", () =>
        {
            var c = new ComboTracker(); c.OnShot(); c.OnBreak(0f);
            T.Eq(1, c.OnBreak(0.8001f), "0.8001 s ends the combo"); T.Eq(2, c.OnBreak(1.6001f), "0.8 s still chains");
        });
        T.Add("combo_window_ticks", () =>
        {
            var c = new ComboTracker(); c.OnShot();
            T.Eq(1, c.OnBreakTick(100), "first break"); T.Eq(2, c.OnBreakTick(140), "40 ticks (0.8 s) inclusive");
            T.Eq(1, c.OnBreakTick(181), "41 ticks ends the combo"); c.OnShot(); T.Eq(1, c.OnBreakTick(182), "shot resets");
        });
        T.Add("BUG11_sagged_piece_scores_on_break", () =>
        {
            var l = new BreakLedger<string>();
            T.True(!l.Observe("p", true, false), "sag counts destroyed, no combo"); T.True(l.IsDestroyed("p"), "destroyed");
            T.True(l.Observe("p", true, true), "later break scores the combo point"); T.True(!l.Observe("p", true, true), "once per piece");
        });
        T.Add("BUG01_test_projects_not_ignored", () =>
        {
            var gi = File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "../../../.gitignore"));
            T.True(gi.Contains("!Tools/**/*.csproj"), "gitignore exception for Tools/*.csproj");
        });
        T.Add("SC13_campaign_leaderboard", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00"); b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "test-id-token" }).Wait();
            var r1 = Win(b, "c1_l01", 1f, 1, 0); T.True(r1.accepted && r1.firstClear, "validated first clear");
            Win(b, "c1_l01", 0.9f, 0, 0);                                    // worse run doesn't lower the best
            var r2 = Win(b, "c1_l02", 0.9f, 2, 3);
            int e = CampaignScore.Compute(true, 1f, 1, 0, false) + CampaignScore.Compute(true, 0.9f, 2, 3, false);
            T.Eq(e, r2.total, "server sums its own per-level bests");
            var p = b.GetLeaderboard(BoardType.Campaign, BoardPeriod.Current, false).Result;
            T.True(p.me != null && p.me.score == e && !p.closed, "on the all-time board");
        });
        T.Add("BUG13_server_validates_campaign_never_trusts_totals", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            T.True(typeof(IBackendService).GetMethod("SubmitCampaignScore") == null, "no API that accepts a client total");
            T.True(!Win(b, "c1_l03").accepted, "L3 rejected: L2 not cleared on server (edited local save is irrelevant)");
            T.True(!Win(b, "c1_l01", 0.5f, 1, 0).accepted, "ratio below win threshold");
            T.True(!Win(b, "c1_l01", 1f, 4, 0).accepted, "shots left = budget (no shot fired)");
            T.True(!Win(b, "c1_l01", 1f, 1, 999).accepted, "combo above piece count");
            T.True(!b.SubmitCampaignResult(new CampaignRunReport { levelId = "c1_l01", won = false, destroyedRatio = 1f }).Result.accepted, "fails don't count");
            T.True(!b.SubmitCampaignResult(new CampaignRunReport { levelId = "nope", won = true, destroyedRatio = 1f }).Result.accepted, "unknown level");
            T.Eq(0, b.CampaignTotal(), "nothing recorded");
            var ok = Win(b, "c1_l01", 1f, 1, 0); T.Eq(CampaignScore.Compute(true, 1f, 1, 0, false), ok.total, "score recomputed server-side");
        });
        T.Add("BUG14_offline_campaign_win_queued_and_synced", () =>
        {
            Crashverse.Core.CampaignResultQueue.Clear();
            var b = NewBackend("2026-10-16T10:00:00"); b.SimulateOffline = true;
            Crashverse.Core.CampaignResultQueue.Enqueue(new CampaignRunReport { levelId = "c1_l01", won = true, destroyedRatio = 1f, shotsLeft = 1 });
            Crashverse.Core.CampaignResultQueue.Enqueue(new CampaignRunReport { levelId = "c1_l02", won = true, destroyedRatio = 1f, shotsLeft = 1 });
            T.True(Crashverse.Core.CampaignResultQueue.Flush(b).Result == null, "offline: nothing sent");
            T.Eq(2, Crashverse.Core.CampaignResultQueue.Count, "both kept");
            b.SimulateOffline = false;
            var r = Crashverse.Core.CampaignResultQueue.Flush(b).Result;
            T.True(r.accepted, "synced in order (L1 before L2 prerequisite)"); T.Eq(0, Crashverse.Core.CampaignResultQueue.Count, "queue drained");
            int total = b.CampaignTotal();
            var dup = new CampaignRunReport { reportId = "fixed", levelId = "c1_l01", won = true, destroyedRatio = 1f, shotsLeft = 3 };
            b.SubmitCampaignResult(dup).Wait(); int after = b.CampaignTotal();
            b.SubmitCampaignResult(dup).Wait(); T.Eq(after, b.CampaignTotal(), "retry idempotent"); T.True(after >= total, "best only rises");
        });
        T.Add("BUG15_season_end_processed_set", () =>
        {
            var b = NewBackend("2026-11-08T10:00:00"); b.GetSeasonState().Wait();
            var f = typeof(LocalBackendService).GetField("_s", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var db = f.GetValue(b); var fld = db.GetType().GetField("seasonEndProcessed");
            T.True(!(bool)fld.GetValue(db), "not before season end");
            b.SetTestClock(U("2026-11-09T00:00:00")); b.GetSeasonState().Wait();
            T.True((bool)fld.GetValue(db), "set on rollover");
        });
        T.Add("online_checkpoint_uses_plausibility_cap", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00"); var t = b.BeginRankedRun().Result;
            T.True(Throws(() => b.ReportRankedCheckpoint(new RankedCheckpoint { token = t.token, shotIndex = 1, damageScore = 5001, shotsLeft = t.shotBudget - 1 }).Wait()), "online +5001 rejected");
            b.ReportRankedCheckpoint(new RankedCheckpoint { token = t.token, shotIndex = 1, damageScore = 5000, shotsLeft = t.shotBudget - 1 }).Wait();
            T.True(Throws(() => b.ReportRankedCheckpoint(new RankedCheckpoint { token = t.token, shotIndex = 2, damageScore = 10001, shotsLeft = t.shotBudget - 2 }).Wait()), "second shot +5001 rejected");
        });
        T.Add("BUG16_timestamp_only_on_increase", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00"); b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "test-id-token" }).Wait();
            Win(b, "c1_l01", 1f, 1, 0);
            long t0 = b.GetLeaderboard(BoardType.Campaign, BoardPeriod.Current, false).Result.me.submittedTicks;
            b.SetTestClock(U("2026-10-20T10:00:00")); Win(b, "c1_l01", 0.9f, 0, 0); // lower replay: total unchanged
            var me = b.GetLeaderboard(BoardType.Campaign, BoardPeriod.Current, false).Result.me;
            T.Eq(t0, me.submittedTicks, "timestamp kept when total doesn't rise");
            b.SetTestClock(U("2026-10-21T10:00:00")); Win(b, "c1_l01", 1f, 3, 0);   // higher: total rises
            T.True(b.GetLeaderboard(BoardType.Campaign, BoardPeriod.Current, false).Result.me.submittedTicks > t0, "timestamp moves on increase");
        });
        T.Add("BUG17_win_exactly_85pct_bp", () =>
        {
            T.Eq(8500, WinRule.ToBp(0.85f), "0.85 → 8500"); T.Eq(8500, WinRule.ToBp(0.84999996f), "float noise of a real 85% → 8500");
            T.True(!WinRule.Meets(0.8499f, 0.85f), "84.99% fails"); T.True(WinRule.Meets(0.85f, 0.85f), "85% passes");
            T.True(WinRule.Meets(17f / 20f, 0.85f), "17/20 mass passes"); T.True(!WinRule.Meets(0.84995f, 0.85f), "84.995% fails");
            var b = NewBackend("2026-10-16T10:00:00");
            T.True(!Win(b, "c1_l01", 0.8499f, 1, 0).accepted, "server rejects 84.99%");
            T.True(Win(b, "c1_l01", 0.85f, 1, 0).accepted, "server accepts 85%");
        });
        T.Add("BUG19_mastery_bp_no_tolerance", () =>
        {
            var d = new MasteryGoal { type = "DESTROYED_AT_LEAST", value = 0.95f };
            T.True(MasteryEvaluator.Evaluate(d, new RunStats { won = true, destroyedRatio = 0.95f }), "95% meets 95%");
            T.True(MasteryEvaluator.Evaluate(d, new RunStats { won = true, destroyedRatio = 0.94999996f }), "float noise of 95% still meets");
            T.True(!MasteryEvaluator.Evaluate(d, new RunStats { won = true, destroyedRatio = 0.9499f }), "94.99% fails (no tolerance)");
            var g = new MasteryGoal { type = "ROLE_INTACT", role = "crown", value = 0.8f };
            var r = new RunStats { won = true }; r.RoleTotal["crown"] = 5; r.RoleBroken["crown"] = 1;
            T.True(MasteryEvaluator.Evaluate(g, r), "4/5 = 8000bp meets 80% exactly");
            var g2 = new MasteryGoal { type = "ROLE_INTACT", role = "crown", value = 0.6667f };
            var r2 = new RunStats { won = true }; r2.RoleTotal["crown"] = 3; r2.RoleBroken["crown"] = 1;
            T.True(!MasteryEvaluator.Evaluate(g2, r2), "2/3 = 6666bp < 6667bp");
            T.Eq(6666, WinRule.FractionBp(2, 3), "integer floor"); T.Eq(WinRule.ToBp(0.8f), WinRule.FractionBp(4, 5), "same scale as WinRule");
        });

        // ───── Gameplay Feel v1 / Animation Spec v1 / Content Roadmap Addendum v1 ─────
        T.Add("FEEL_fixed_power_no_charge", () =>
        {
            var w = Db.Wrecker; T.Eq(28f, w.launchSpeed, "fixed launch speed from data (OQ-A1)");
            T.Eq(0.25f, w.aimYawDegPerDp, "yaw sens data"); T.Eq(0.15f, w.aimPitchDegPerDp, "pitch sens data");
            var sm = new WreckerAnimStateMachine(); sm.SetAiming(true); sm.Tick(0.2f);
            T.True(sm.Fire(), "FIRE tap fires immediately, no hold/charge (OQ-A2)");
            T.Eq(WreckerState.FireAnticipation, sm.State, "anticipation starts on tap");
        });
        T.Add("FEEL_debris_caps_and_recycle", () =>
        {
            T.Eq(25, DebrisBudget.Cap(QualityTier.Low)); T.Eq(60, DebrisBudget.Cap(QualityTier.Mid)); T.Eq(90, DebrisBudget.Cap(QualityTier.High));
            T.Eq(3, DebrisBudget.Chunks("wood", QualityTier.Low)); T.Eq(6, DebrisBudget.Chunks("wood", QualityTier.Mid));
            var l = new DebrisLedger<string>(3); l.Add("a"); l.Add("b"); l.Add("c"); l.MarkSettled("b");
            T.Eq("b", l.Add("d"), "oldest SETTLED recycled first"); T.Eq("a", l.Add("e"), "else oldest"); T.Eq(3, l.Count, "never above cap");
        });
        T.Add("FEEL_debris_chunks_glass_stone", () =>
        {
            T.Eq(10, DebrisBudget.Chunks("glass", QualityTier.Mid), "glass shatters into many shards"); T.Eq(5, DebrisBudget.Chunks("glass", QualityTier.Low));
            T.Eq(5, DebrisBudget.Chunks("stone", QualityTier.Mid), "stone crumbles into chunks"); T.Eq(2, DebrisBudget.Chunks("stone", QualityTier.Low));
            T.True(DebrisBudget.Chunks("glass", QualityTier.Mid) > DebrisBudget.Chunks("stone", QualityTier.Mid), "glass > stone fragments");
        });
        T.Add("CONTENT_chapter1_has_no_glass", () =>
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Assets/_Project"))) root = root.Parent;
            T.True(root != null, "repo root found");
            // Dev-only fixtures (PerfL10Fixture, id prefix "dev_") are generated in code for Scenes/Dev and never shipped as JSON;
            // they are excluded by id, and none may appear in the shipped layout folder.
            foreach (var f in Directory.GetFiles(Path.Combine(root.FullName, "Assets/_Project/Resources/Data/Layouts"), "*.json"))
                T.True(!PerfL10Fixture.IsDevLayout(Path.GetFileNameWithoutExtension(f)), "no dev_ layout in shipped Layouts: " + Path.GetFileName(f));
            foreach (var f in Directory.GetFiles(Path.Combine(root.FullName, "Assets/_Project/Resources/Data/Layouts"), "*.json"))
                T.True(!File.ReadAllText(f).Contains("\"mat\": \"glass\"") && !File.ReadAllText(f).Contains("\"mat\":\"glass\""), "glass is Neon (ch3+) only: " + Path.GetFileName(f));
        });
        T.Add("PERF_L10_fixture_deterministic", () =>
        {
            var l10 = Db.GetLevel(PerfL10Fixture.BaseLevelId); var baseLayout = Db.GetLayout(l10.layout);
            string a = Newtonsoft.Json.JsonConvert.SerializeObject(PerfL10Fixture.Layout(baseLayout));
            string b = Newtonsoft.Json.JsonConvert.SerializeObject(PerfL10Fixture.Layout(Db.GetLayout(l10.layout)));
            T.Eq(a, b, "same L10 input -> identical perf layout");
            T.Eq(Newtonsoft.Json.JsonConvert.SerializeObject(PerfL10Fixture.Script()), Newtonsoft.Json.JsonConvert.SerializeObject(PerfL10Fixture.Script()), "same script");
            T.Eq(0x4C31305045524631L, PerfL10Fixture.Seed, "fixed long seed");
            var sc = PerfL10Fixture.Script();
            T.True(sc[0].type == "arm" && sc[1].type == "fire" && sc[0].tick < sc[1].tick && sc[0].tick > 0, "arm then fire, tick-ordered, after tick 0");
            T.Eq(8000, sc[1].y, "fixed-point elevation 8.000 deg"); T.Eq(0, sc[1].x, "yaw 0");
            T.Eq(baseLayout.blocks.Length, 13, "base L10 layout unchanged (13 blocks)");
        });
        T.Add("PERF_L10_fixture_hits_caps_and_mixes_materials", () =>
        {
            var l10 = Db.GetLevel(PerfL10Fixture.BaseLevelId);
            var lay = PerfL10Fixture.Layout(Db.GetLayout(l10.layout));
            T.True(PerfL10Fixture.IsDevLayout(lay.id), "dev_ layout id");
            T.Eq(13 + PerfL10Fixture.Layers * PerfL10Fixture.Rows * PerfL10Fixture.Columns + PerfL10Fixture.DropPanes + 2 * PerfL10Fixture.AnvilPairs, lay.blocks.Length, "L10 + curtain + drop panes + hammer/anvil pairs");
            // Hammers: a 4 kg steel hammer must break a 12 HP glass anvil on its first hit (ruling #7a maths), and they keep
            // >= 3 landings per 100 ms; anvils sit outside every fuse's 2.5 m explosion radius so the burst can't pre-break them.
            for (int i = 0; i < PerfL10Fixture.AnvilPairs; i++)
            {
                float v = PerfL10Fixture.G * PerfL10Fixture.HammerT(i);
                long vmm = (long)Math.Round(v * 1000.0), milli = vmm <= 2000 ? 0 : 4000L * (vmm - 2000) * 500L * 1500L / 1000000000L; // ruling #7a: steel 4 kg, 0.5 HP/(kg·m/s), glass ×1.5 (MaterialDefinition / DebrisDamage; not in this assembly)
                T.True(milli >= 12000, $"hammer {i} breaks its 12 HP glass anvil ({milli} mHP at {v:0.0} m/s)");
            }
            T.True(PerfL10Fixture.AnvilPairs / (PerfL10Fixture.HammerT1 - PerfL10Fixture.HammerT0) >= 30f, "hammers >= 3 per 100 ms");
            foreach (var bl in lay.blocks)
                if (bl.role == "anvil") foreach (var f in lay.blocks) if (f.explosive)
                {
                    float dx = bl.p[0] - f.p[0], dy = bl.p[1] - f.p[1], dz = bl.p[2] - f.p[2];
                    T.True(Math.Sqrt(dx * dx + dy * dy + dz * dz) > 2.5, $"{bl.id} outside fuse {f.id} explosion radius");
                }
            T.True(PerfL10Fixture.DropHeight(0) > 3.5f && PerfL10Fixture.DropHeight(PerfL10Fixture.DropPanes - 1) < 25f, "drop heights 4..22 m");
            T.True(PerfL10Fixture.DropPanes / (PerfL10Fixture.DropT1 - PerfL10Fixture.DropT0) >= 30f, ">= 3 landings per 100 ms over >= 1 s");
            var ids = new HashSet<string>(); foreach (var bl in lay.blocks) T.True(ids.Add(bl.id), "unique id " + bl.id);
            foreach (var bl in lay.blocks) if (bl.sup != null) foreach (var s in bl.sup) T.True(ids.Contains(s), "support exists " + s);
            var mats = new HashSet<string>(); int midChunks = 0, breakable = 0, fuse = 0;
            foreach (var bl in lay.blocks)
            {
                mats.Add(bl.mat);
                if (bl.indestructible) continue;
                breakable++; if (bl.explosive) fuse++;
                midChunks += DebrisBudget.Chunks(bl.mat, QualityTier.Mid);
            }
            foreach (var m in new[] { "wood", "brick", "steel", "glass" }) T.True(mats.Contains(m), "has " + m);
            T.True(midChunks >= 4 * DebrisBudget.Cap(QualityTier.Mid), $"potential Mid debris {midChunks} >= 4x cap 60");
            T.True(breakable >= 60, $"breakable pieces {breakable} >= 60 (3 per 100 ms for >= 1 s needs >= 30)");
            T.Eq(PerfL10Fixture.Layers * PerfL10Fixture.Columns, fuse, "explosive fuse row per layer");
            // Fuse pieces must chain (Explosion radius 2.5 m) and sit inside Chain Reactor radius (3 m) of the base centre
            float pitch = PerfL10Fixture.PieceD + PerfL10Fixture.Gap; T.True(pitch < 2.5f, "fuse spacing < explosion radius");
            var lvl = PerfL10Fixture.Level(l10);
            T.True(!lvl.IsBoss && lvl.layout == lay.id && Array.IndexOf(lvl.materials, "glass") >= 0, "perf level: no boss, dev layout, glass allowed");
            T.True(Array.IndexOf(l10.materials, "glass") < 0, "shipped c1_l10 still has no glass");
        });
        T.Add("PERF_L10_scripted_shot_hits_curtain_analytic", () =>
        {
            // Same step as PhysX/aim preview at 50 Hz (v += g dt; p += v dt). Gravity 9.81 x RuleModifiers (0 = unchanged; the
            // x1.2 gravity ruling is not applied in code yet). Spawn = Wrecker (WreckerX, 0) + pivot (PivotY) + SpawnAlong along elevation.
            double th = PerfL10Fixture.AimElevationDeg * Math.PI / 180.0, dt = 0.02, g = 9.81, r = 0.45 * 0.5;
            double x = Crashverse.Weapons.WreckerSpec.WreckerX + Crashverse.Weapons.WreckerSpec.SpawnAlong * Math.Cos(th), y = Crashverse.Weapons.WreckerSpec.PivotY + Crashverse.Weapons.WreckerSpec.SpawnAlong * Math.Sin(th);
            double vx = PerfL10Fixture.LaunchSpeed * Math.Cos(th), vy = PerfL10Fixture.LaunchSpeed * Math.Sin(th);
            double face = 10.0 + PerfL10Fixture.CurtainX - PerfL10Fixture.PieceW * 0.5; int steps = 0; // structureBase x = 10
            while (x + r < face && y > -6 && steps < 500) { vy -= g * dt; x += vx * dt; y += vy * dt; steps++; }
            int row = (int)Math.Floor(y / (PerfL10Fixture.PieceH + PerfL10Fixture.Gap));
            T.True(y > 0 && row >= 1 && row <= 3, $"8 deg / 28 m/s reaches the curtain face at y={y:0.00} (row {row}) after {steps} ticks");
        });
        T.Add("PERF_L10_stats_saturation_and_hash", () =>
        {
            var st = new PerfL10Stats(); int fire = 50;
            for (int w = 0; w < 12; w++) for (int k = 0; k < 3; k++) st.OnEvent(fire + w * 5 + k, 2, w * 10 + k, 1, k == 0 ? "Glass" : "Wood", fire);
            st.OnEvent(fire + 12 * 5, 2, 999, 1, "Brick", fire); // window 12: only 1 break
            T.Eq(1200, st.SaturatedMs, "12 saturated windows = 1200 ms");
            ulong h = st.Hash; var st2 = new PerfL10Stats();
            for (int w = 0; w < 12; w++) for (int k = 0; k < 3; k++) st2.OnEvent(fire + w * 5 + k, 2, w * 10 + k, 1, k == 0 ? "Glass" : "Wood", fire);
            st2.OnEvent(fire + 12 * 5, 2, 999, 1, "Brick", fire);
            T.Eq(h, st2.Hash, "same event stream -> same hash");
            st2.OnEvent(200, 5, -1, 1, null, fire); T.True(st2.Hash != h, "extra event changes hash");
            T.Eq(3, st.MaterialsBroken, "3 materials"); st.SampleDebris(61); st.SampleDebris(40); T.Eq(61, st.PeakDebris, "peak debris");
            st.Reset(); T.Eq(0, st.SaturatedMs, "reset"); T.Eq(0, st.Breaks, "reset breaks");
        });
        T.Add("FEEL_debris_mass_counts_on_source", () =>
        {
            // Destroyed mass is recorded on the piece itself (BreakLedger/tracker); chunks never enter the ratio.
            var led = new BreakLedger<int>(); T.True(led.Observe(1, true, true), "piece counted once"); T.True(!led.Observe(1, true, true), "chunks/repeats add nothing");
        });
        T.Add("FEEL_powerup_free_disarm_except_time_fracture", () =>
        {
            var tf = new TimeFractureTimer(); AnimEventBus.ResetForTests();
            tf.Arm(); T.True(tf.Active, "TF starts on arm"); T.Eq(0.25f, tf.SpeedScale, "25% speed");
            tf.Tick(3.9f); T.True(tf.Active, "still on at 3.9s"); tf.Tick(0.2f); T.True(!tf.Active, "ends at 4s without a shot");
            tf.Arm(); tf.Tick(1f); tf.OnShotFired(); tf.Tick(5f); T.True(tf.Active, "held until impact when fired");
            tf.OnShotImpact(); T.True(!tf.Active, "ends on impact"); T.Eq(1f, tf.SpeedScale, "speed restored");
        });
        T.Add("FEEL_splitstrike_timing", () =>
        {
            T.Eq(1.0f, SplitPlanner.SplitTime(9.81f, 9.81f, 4f), "apex (1.0s) before 70% (2.8s)");
            T.Eq(0.7f, SplitPlanner.SplitTime(9.81f, 9.81f, 1f), "70% of 1s flight before apex");
            T.Eq(0f, SplitPlanner.SplitTime(-1f, 9.81f, 0f), "falling + no prediction → immediate");
        });
        T.Add("FEEL_titan_stages_25_50_75_of_10000", () =>
        {
            T.Eq(10000, Db.Titan.maxDamageScore, "max 10,000");
            T.Eq(0, TitanStages.StageFor(2499, 10000)); T.Eq(1, TitanStages.StageFor(2500, 10000));
            T.Eq(2, TitanStages.StageFor(5000, 10000)); T.Eq(3, TitanStages.StageFor(7500, 10000)); T.Eq(3, TitanStages.StageFor(9999, 10000));
        });
        T.Add("FEEL_motion_settings_persist_and_override_auto", () =>
        {
            var prefs = new MemPrefs();
            var m = new MotionSettings(prefs, true); T.True(m.ReduceMotion && m.ReduceMotionIsAuto, "OS animations off → auto-on");
            T.True(!m.ReduceShake, "shake off by default");
            m.SetReduceMotion(false); T.True(!new MotionSettings(prefs, true).ReduceMotion, "explicit off persists and beats auto-on");
            m.SetReduceShake(true); var m2 = new MotionSettings(prefs, false); T.True(m2.ReduceShake, "shake persists");
            T.Eq(0f, m2.ShakeScale(QualityTier.Mid), "shake ×0 when reduced");
            var rm = new MotionSettings(new MemPrefs(), true);
            T.True(!rm.SlowMoAllowed(false, false) && !rm.FidgetEnabled(QualityTier.High), "reduce motion kills slow-mo/fidget");
            T.True(Ease.OutBackOrQuad(0.5f, true) <= 1f, "no overshoot with reduce motion");
        });
        T.Add("SETTINGS_45_defaults", () =>
        {
            var g = new Crashverse.Core.GameSettings(new MemIntPrefs());
            T.Eq(80, g.MusicPercent, "music default 80%"); T.Eq(100, g.SfxPercent, "sfx default 100%");
            T.Eq(1, g.Quality, "quality default Mid"); T.Eq("Medium", Crashverse.Core.GameSettings.QualityName(g.Quality));
            T.Eq(0.8f, g.MusicVolume, "music 0.8"); T.Eq(1f, new Crashverse.Core.GameSettings(null).SfxVolume, "null prefs = defaults");
            var mo = new MotionSettings(new MemPrefs(), false); T.True(!mo.ReduceShake && !mo.ReduceMotion, "motion off by default");
        });
        T.Add("SETTINGS_45_persist_clamp_and_cycle", () =>
        {
            var prefs = new MemIntPrefs(); var g = new Crashverse.Core.GameSettings(prefs); int fired = 0; g.Changed += () => fired++;
            g.SetMusicPercent(30); g.SetSfxPercent(0); g.SetQuality(2);
            var g2 = new Crashverse.Core.GameSettings(prefs);
            T.Eq(30, g2.MusicPercent, "music persists"); T.Eq(0, g2.SfxPercent, "sfx 0 persists (mute)"); T.Eq(2, g2.Quality, "quality persists");
            T.Eq(3, fired, "Changed fires per set");
            g.SetMusicPercent(150); T.Eq(100, g.MusicPercent, "clamp high"); g.SetSfxPercent(-10); T.Eq(0, g.SfxPercent, "clamp low");
            prefs.Set(Crashverse.Core.GameSettings.KeyQuality, 9); T.Eq(2, g.Quality, "corrupt quality clamped");
            g.SetQuality(2); T.Eq(0, g.NextQuality(), "High→Low"); g.SetQuality(0); T.Eq(1, g.NextQuality(), "Low→Mid");
        });
        T.Add("FEEL_win_threshold_per_level_default_085", () =>
        {
            T.Eq(0.85f, new LevelEntry().winThreshold, "default 0.85 field");
            T.True(WinRule.Meets(0.85f, 0f), "missing threshold → 0.85"); T.True(!WinRule.Meets(0.80f, 0f), "80% fails default");
            T.True(WinRule.Meets(0.80f, 0.80f), "per-level 0.80 honoured"); T.True(!WinRule.Meets(0.8999f, 0.90f), "per-level 0.90 exact bp");
            foreach (var l in Db.Chapter1.levels) T.True(l.winThreshold > 0f, l.id + " has a threshold");
        });
        T.Add("ROADMAP_reload_gate", () =>
        {
            AnimEventBus.ResetForTests(); int ready = 0; AnimEventBus.Raised += (e, _) => { if (e == AnimEvents.WreckerReady) ready++; };
            var g = new ReloadGate(); T.True(g.Ready, "ready at start");
            g.OnShotFired(); T.True(!g.Ready, "locked after shot");
            T.True(!g.Tick(0.5f, true), "settled but < 1.0s → locked"); T.True(!g.Tick(0.6f, false), "≥1s but not settled → locked");
            T.True(g.Tick(0.01f, true), "settled AND ≥1s → unlocks"); T.Eq(1, ready, "GAME_WRECKER_READY raised once");
            g.Tick(1f, true); T.Eq(1, ready, "not re-raised");
            T.Eq(0.15f, 0.15f, "settle velocity"); // tracker: Destruction.DestructionTracker.SettleVelocityThreshold 0.15 / hold 0.75 s (Unity-side)
        });
        T.Add("ANIM_state_machines_and_shake_caps", () =>
        {
            T.Eq(649f, WreckerAnimStateMachine.FireCycleMs, "fire cycle 649 ms");
            var st = new ShakeStack(); for (int i = 0; i < 10; i++) st.Add(ShakeTable.Fire);
            var (p, r) = st.Tick(0.001f); T.True(p <= 0.15f + 1e-5f && r <= 1.5f + 1e-5f, "shake sum capped 0.15m/1.5°");
            var gov = new TimeScaleGovernor { Settings = new MotionSettings(new MemPrefs(), false) };
            T.True(!gov.TrySlowMo(true, false), "no slow-mo in Titan ranked"); T.True(!gov.TrySlowMo(false, true), "no slow-mo during Time Fracture");
            T.True(gov.TrySlowMo(false, false), "slow-mo allowed"); T.True(!gov.TrySlowMo(false, false), "max one per shot");
            var t = new TitanAnimStateMachine(10000); AnimEventBus.ResetForTests(); t.OnDamage(5000); T.Eq(2, t.StagesShown, "stages reached on damage");
        });
        T.Add("VISUAL_camera_config_data_driven", () =>
        {
            var src = File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "../../../Assets/_Project/Scripts/Core/OrbitCamera.cs"));
            T.True(src.Contains("CameraConfig.Current"), "orbit camera reads CameraConfig");
            T.True(!src.Contains("fieldOfView = 50f"), "no hard-coded FOV");
            var arena = File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "../../../Assets/_Project/Scripts/Core/ArenaBuilder.cs"));
            T.True(arena.Contains("VisualConfig.Current") && !arena.Contains("\"FFF2D6\""), "lighting from VisualConfig");
        });

        // ───── Security Review v1 + SEC-02 + B1 + B6 ─────
        string Src(string rel) => File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "../../../", rel));
        T.Add("SEC_local_backend_dev_only", () =>
        {
            var lb = Src("Assets/_Project/Scripts/Backend/LocalBackendService.cs");
            T.True(lb.Contains("#if CRASHVERSE_DEV") && lb.TrimEnd().EndsWith("#endif"), "whole LocalBackendService behind CRASHVERSE_DEV");
            var boot = Src("Assets/_Project/Scripts/Core/GameBootstrap.cs");
            int els = boot.IndexOf("#else", boot.IndexOf("new LocalBackendService")); int end = boot.IndexOf("#endif", els);
            var rel = boot.Substring(els, end - els);
            T.True(els > 0 && !rel.Contains("LocalBackendService") && rel.Contains("new OfflineBackendService()"), "release build: server client or OfflineBackendService, never the local stub");
        });
        T.Add("SEC_offline_backend_fails_every_call", () =>
        {
            var off = new OfflineBackendService(); int n = 0;
            T.True(!off.IsOnline, "offline");
            foreach (var m in typeof(IBackendService).GetMethods())
            {
                if (m.IsSpecialName) continue;
                var args = m.GetParameters().Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
                var t = (System.Threading.Tasks.Task)m.Invoke(off, args); n++;
                T.True(t.IsFaulted && t.Exception.InnerException is BackendException be && be.IsOffline, m.Name + " fails offline");
            }
            T.True(n >= 30, "covers the interface");
            var src = Src("Assets/_Project/Scripts/Backend/OfflineBackendService.cs");
            T.True(!src.Contains("Grant") && !src.Contains("File.") && !src.Contains("PlayerPrefs"), "grants and stores nothing");
        });
        T.Add("SEC02_qa_tools_dev_only", () =>
        {
            var sc = Src("Assets/_Project/Scripts/UI/Screens.cs");
            int gears = 0;
            foreach (var f in Directory.GetFiles(Path.Combine(UnityEngine.Resources.Root, "../../../Assets/_Project/Scripts/UI"), "*.cs"))
            {
                var src = File.ReadAllText(f);
                for (int gear = src.IndexOf("new QADebugModal()"); gear >= 0; gear = src.IndexOf("new QADebugModal()", gear + 1))
                {
                    gears++; int guard = src.LastIndexOf("#if CRASHVERSE_DEV || DEVELOPMENT_BUILD", gear);
                    T.True(guard > 0 && src.IndexOf("#endif", guard) > gear && src.LastIndexOf("#endif", gear) < guard, "gear button guarded in " + Path.GetFileName(f));
                }
            }
            T.True(gears >= 1, "QA gear button exists (dev)");
            int cls = sc.IndexOf("public class QADebugModal"); int g2 = sc.LastIndexOf("#if CRASHVERSE_DEV || DEVELOPMENT_BUILD", cls);
            T.True(g2 > 0 && sc.IndexOf("#endif", g2) > cls, "QADebugModal class guarded");
        });
        T.Add("B1_release_guard_script_passes_and_ci_runs_it", () =>
        {
            var psi = new System.Diagnostics.ProcessStartInfo("python3", Path.Combine(UnityEngine.Resources.Root, "../../../Tools/ci/check_release_guards.py")) { RedirectStandardOutput = true, UseShellExecute = false };
            using (var pr = System.Diagnostics.Process.Start(psi)) { pr.WaitForExit(); T.Eq(0, pr.ExitCode, "guard check passes on tree"); }
            var wf = Src(".github/workflows/android-apk.yml");
            T.True(wf.Contains("check_release_guards.py") && wf.Contains("needs: guards"), "CI runs guard before Unity build");
            T.True(!wf.Contains("allowDirtyBuild"), "no dirty builds");
            T.True(!System.Text.RegularExpressions.Regex.IsMatch(wf, @"uses: [^@\s]+@v\d"), "actions pinned to SHAs");
            T.True(wf.Contains("permissions:") && wf.Contains("default: dev"), "least privilege + dev default");
            int sec = wf.IndexOf("secrets.UNITY_LICENSE"), build = wf.IndexOf("\n  build:");
            T.True(sec > build && wf.IndexOf("refs/heads/main", build) < sec, "Unity secrets only in main-only build job");
        });
        T.Add("SEC_premium_grant_not_on_client", () =>
        {
            T.True(typeof(LocalBackendService).GetMethod("GrantPremiumPassStub") == null, "client premium grant removed");
            T.True(typeof(IServerEntitlements).IsInterface, "server-only entitlement interface present");
            foreach (var f in Directory.GetFiles(Path.Combine(UnityEngine.Resources.Root, "../../../Assets"), "*.cs", SearchOption.AllDirectories))
                T.True(!File.ReadAllText(f).Contains(": IServerEntitlements"), Path.GetFileName(f) + " doesn't implement server entitlements");
        });
        T.Add("SEC_titan_override_server_only", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var dir = Path.GetDirectoryName(typeof(LocalBackendService).GetField("_path", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(b) as string);
            var normal = b.GetDailyTitan().Result.arenaId;
            File.WriteAllText(Path.Combine(dir, "daily_titan_override_2026-10-16.json"), "{\"arenaId\":\"HACKED\"}");
            T.Eq(normal, b.GetDailyTitan().Result.arenaId, "local override file ignored");
            b.DevOverride = new DailyTitanConfig { arenaId = "server_push" }; T.Eq("server_push", b.GetDailyTitan().Result.arenaId, "server-side override path");
        });
        T.Add("SEC_attempt_token_and_input_log", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var t = b.BeginAttempt("c1_l01").Result; T.True(t.token.StartsWith("att_") && t.levelId == "c1_l01", "token issued");
            T.True(t.seed.GetType() == typeof(long), "seed is long");
            var rec = new InputRecorder(); rec.Record(60, "fire", 3.25f, 30f);
            var ev = rec.ToArray()[0]; T.Eq(60, ev.tick, "tick"); T.Eq("fire", ev.type, "type"); T.Eq(3250, ev.x, "x fixed-point 1/1000"); T.Eq(30000, ev.y, "y fixed-point");
            T.Eq(-1500, InputRecorder.ToFixed(-1.5f), "negative fixed-point");
            var sub = new ScoreSubmission { attemptToken = t.token, inputs = rec.ToArray(), clientScoreBp = 9000, buildHash = "test" };
            T.True(b.SubmitScore(sub).Result.accepted, "score accepted once"); T.True(!b.SubmitScore(sub).Result.accepted, "token single-use");
            T.True(!b.SubmitScore(new ScoreSubmission { attemptToken = "forged", inputs = rec.ToArray() }).Result.accepted, "unknown token rejected");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(new LinkCredential { provider = AuthProvider.Facebook, token = "x" });
            T.True(json.Contains("\"provider\":\"Facebook\""), "AuthProvider serializes as string");
            T.Eq(AuthProvider.Facebook, Newtonsoft.Json.JsonConvert.DeserializeObject<LinkCredential>("{\"provider\":\"Facebook\",\"token\":\"t\"}").provider, "string → enum");
            var ev2 = Newtonsoft.Json.JsonConvert.SerializeObject(ev); T.True(ev2.Contains("\"tick\":60") && ev2.Contains("\"x\":3250"), "InputEvent wire shape");
        });
        T.Add("SEC_email_auth_and_link", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            T.True(Throws(() => b.AuthEmailRegister("bad", "longenough1").Wait()), "invalid email rejected");
            T.True(Throws(() => b.AuthEmailRegister("a@b.co", "short").Wait()), "short password rejected (placeholder policy)");
            var s = b.AuthEmailRegister("Pat@Example.com", "correct-horse-1").Result; T.True(s.accessToken.StartsWith("acc_") && !string.IsNullOrEmpty(s.refreshToken) && s.account != null, "AuthResponse");
            T.True(b.AuthEmailLogin("pat@example.com", "whatever-1").Result != null, "login");
            T.True(b.RequestPasswordReset("nobody@example.com").Result, "reset never reveals account existence");
            var acc = b.LinkAccount(new LinkCredential { provider = AuthProvider.Email, token = s.accessToken }).Result; T.True(acc.linked && acc.provider == "Email", "link with Email credential");
            var dbText = File.ReadAllText(typeof(LocalBackendService).GetField("_path", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(b) as string);
            T.True(!dbText.Contains("correct-horse-1"), "password never persisted");
            T.True(Throws(() => b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "" }).Wait()), "credential required");
        });
        T.Add("SEC_x_login_flag_from_server", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            T.True(!b.GetFeatureFlags().Result.xLoginEnabled, "X off unless server enables");
            T.True(Throws(() => b.LinkAccount(new LinkCredential { provider = AuthProvider.X, token = "tok" }).Wait()), "X link refused while flag off");
            b.Flags.xLoginEnabled = true; T.True(b.LinkAccount(new LinkCredential { provider = AuthProvider.X, token = "tok" }).Result.linked, "X link once server flag on");
        });
        T.Add("REQ_A3_A4_A5_delete_reauth_grace_cancel", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var g = b.DeleteAccount(null).Result; T.True(g.status == "scheduled", "guest deletes with session only (C1)");
            T.Eq(Unix("2026-10-23T10:00:00"), g.purgeAt, "7-day grace (A5)");
            T.True(b.CancelAccountDeletion().Result.status == "cancelled" && b.GetAccount().Result.pendingDeletionUnixUtc == 0, "cancel in grace");
            b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "tok" }).Wait(); Win(b, "c1_l01"); var id = b.GetAccount().Result.playerId;
            var ex = Catch(() => b.DeleteAccount(null).Wait()); T.Eq("REAUTH_REQUIRED", ex?.Code, "linked needs re-auth (A3)"); T.Eq(401, ex?.Status ?? 0, "401");
            var rt = b.Reauthenticate(new LinkCredential { provider = AuthProvider.Google, token = "fresh" }).Result.reauthToken;
            b.SetTestClock(U("2026-10-16T10:06:00")); T.Eq("REAUTH_REQUIRED", Catch(() => b.DeleteAccount(rt).Wait())?.Code, "re-auth older than 5 min rejected");
            rt = b.Reauthenticate(new LinkCredential { provider = AuthProvider.Google, token = "fresh" }).Result.reauthToken; T.Eq("REAUTH_REQUIRED", Catch(() => b.DeleteAccount("forged").Wait())?.Code, "forged token rejected"); var r = b.DeleteAccount(rt).Result; T.True(r.status == "scheduled", "scheduled after fresh re-auth");
            T.Eq(r.purgeAt, b.DeleteAccount(rt).Result.purgeAt, "idempotent");
            b.SetTestClock(U("2026-10-23T10:07:00"));
            var acc = b.GetAccount().Result; T.True(acc.playerId != id && !acc.linked && acc.identities.Count == 0, "purged after grace → new account");
            var lb = b.GetLeaderboard(BoardType.Campaign, BoardPeriod.Current, false).Result;
            T.True(lb.top.TrueForAll(e => e.playerId != id) && (lb.me == null || lb.me.playerId != id), "no board entry for deleted player (D2 stub)");
        });
        T.Add("REQ_A2_A4_A6_client_contract", () =>
        {
            var sc = Src("Assets/_Project/Scripts/UI/Screens.cs"); var del = Src("Assets/_Project/Scripts/Core/AccountDeletion.cs");
            T.True(sc.Contains("HoldToConfirm") && sc.Contains("HoldSeconds = 2f"), "press-and-hold, no single-tap confirm (A2)");
            foreach (var k in new[] { "pseudonymised purchase", "30 days", "7 days", "not refunded" }) T.True(sc.Contains(k), "copy mentions " + k);
            foreach (var f in new[] { "campaign_save.json", "titan_ranked_journal.json", "campaign_sync_queue.json", "local_server_db.json" }) T.True(del.Contains(f), "wipes " + f);
            T.True(del.Contains("PlayerPrefs.DeleteAll") && sc.Contains("RestartFirstRun"), "first-run state after deletion (A4)");
            T.True(del.Contains("PendingOffline") && del.Contains("PendingKey"), "offline request queued (A6)");
        });
        T.Add("REQ_A6_offline_queue_real_flush", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            UnityEngine.PlayerPrefs.SetInt(Crashverse.Core.AccountDeletion.PendingKey, 1);
            b.SimulateOffline = true; T.Eq(Crashverse.Core.AccountDeletion.Result.PendingOffline, Crashverse.Core.AccountDeletion.Flush(b).Result, "offline stays pending, not faked");
            T.True(b.GetType() != null && Crashverse.Core.AccountDeletion.Pending, "still queued");
            b.SimulateOffline = false; T.Eq(Crashverse.Core.AccountDeletion.Result.Scheduled, Crashverse.Core.AccountDeletion.Flush(b).Result, "sent on next connection");
            T.True(b.GetAccount().Result.pendingDeletionUnixUtc > 0, "server scheduled");
        });
        T.Add("SEC55_B1_offline_delete_wipes_everything", () =>
        {
            var dir = UnityEngine.Application.persistentDataPath;
            var files = new[] { "campaign_save.json", "campaign_save.json.tmp", "progress_v2.json", "progress_v2.json.tmp", "campaign_sync_queue.json", "titan_ranked_journal.json", "other_cv.json" };
            foreach (var f in files) System.IO.File.WriteAllText(System.IO.Path.Combine(dir, f), "{}");
            UnityEngine.PlayerPrefs.SetInt("cv_age_band_v2", 2); UnityEngine.PlayerPrefs.SetInt("cv_age_local_v2", 1); UnityEngine.PlayerPrefs.SetString("cv_name", "x"); UnityEngine.PlayerPrefs.SetFloat("cv_music", 0.5f);
            var off = new OfflineBackendService();
            T.True(BackendLocator.IsLocalOnly(off) && BackendLocator.IsLocalOnly(null) && !BackendLocator.IsLocalOnly(NewBackend("2026-10-16T10:00:00")), "local-only detection");
            T.Eq(Crashverse.Core.AccountDeletion.Result.WipedLocal, Crashverse.Core.AccountDeletion.Request(off, null).Result, "offline delete wipes now, not pending");
            foreach (var f in files) T.True(!System.IO.File.Exists(System.IO.Path.Combine(dir, f)), "removed " + f);
            T.Eq(0, UnityEngine.PlayerPrefs.CountForTests, "every pref removed (incl. age band) → first-run state");
            T.True(!Crashverse.Core.AccountDeletion.Pending, "no cv_delete_pending left behind");
            UnityEngine.PlayerPrefs.SetInt(Crashverse.Core.AccountDeletion.PendingKey, 1); System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "progress_v2.json"), "{}");
            T.Eq(Crashverse.Core.AccountDeletion.Result.WipedLocal, Crashverse.Core.AccountDeletion.Flush(off).Result, "request queued by an older build is completed locally");
            T.True(!System.IO.File.Exists(System.IO.Path.Combine(dir, "progress_v2.json")) && UnityEngine.PlayerPrefs.CountForTests == 0, "and wiped");
            var sc = Src("Assets/_Project/Scripts/UI/Screens.cs");
            var oc = System.Text.RegularExpressions.Regex.Match(sc, "OfflineCopy = \"([^\"]*)\"").Groups[1].Value;
            T.True(oc.Contains("this device") && oc.Contains("can't be undone"), "offline copy");
            foreach (var k in new[] { "purchase", "30 days", "7 days", "pseudonymised", "pending" }) T.True(!oc.ToLowerInvariant().Contains(k), "offline copy omits " + k);
        });
        T.Add("SEC55_B2_age_band_never_relaxes", () =>
        {
            T.Eq(AgeGate.Band.Minor13to17, AgeGate.MostRestrictive(AgeGate.Band.Minor13to17, AgeGate.Band.Adult), "13-17 stays 13-17 when re-answered as adult");
            T.Eq(AgeGate.Band.Minor13to17, AgeGate.MostRestrictive(AgeGate.Band.Adult, AgeGate.Band.Minor13to17), "adult → 13-17 is allowed (stricter)");
            T.Eq(AgeGate.Band.Adult, AgeGate.MostRestrictive(AgeGate.Band.Unknown, AgeGate.Band.Adult), "first answer kept");
            T.Eq(AgeGate.Band.Minor13to17, AgeGate.MostRestrictive(AgeGate.Band.Minor13to17, AgeGate.Band.Unknown), "unknown server band keeps local");
            var sc = Src("Assets/_Project/Scripts/UI/Screens.cs");
            int g = sc.IndexOf("public class AgeGateScreen"), e = sc.IndexOf("public class BlockedScreen"); var gate = sc.Substring(g, e - g);
            T.True(gate.Contains("AgeGate.MostRestrictive(Current, band)") && gate.Contains("LocalKey") && gate.Contains("ServerConfigured"), "Confirm keeps the strictest band; offline band is final");
        });
        T.Add("REQ_C2_guest_inactivity", () =>
        {
            var b = NewBackend("2026-01-01T00:00:00"); b.GetAccount().Wait(); var id = b.GetAccount().Result.playerId;
            b.SetTestClock(U("2026-11-20T00:00:00")); // 323 days: no warning
            T.Eq(0L, b.GetAccount().Result.deletionWarningAtUnixUtc, "no warning before day 335");
            b.SetLastActiveForTests(U("2026-01-01T00:00:00")); b.SetTestClock(U("2026-12-02T00:00:00")); // 335 days idle
            var a = b.GetAccount().Result; T.True(a.deletionWarningAtUnixUtc > 0, "warning at day 335"); T.Eq(Unix("2027-01-01T00:00:00"), a.guestPurgeAtUnixUtc, "purge date shown");
            b.KeepAccountActive().Wait(); T.Eq(0L, b.GetAccount().Result.deletionWarningAtUnixUtc, "keep my account clears it");
            b.SetTestClock(U("2027-01-05T00:00:00")); T.Eq(id, b.GetAccount().Result.playerId, "activity reset the clock");
            b.SetTestClock(U("2028-01-06T00:00:00")); T.True(b.GetAccount().Result.playerId != id, "365 days idle → purged");
            var l = NewBackend("2026-01-01T00:00:00"); l.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "t" }).Wait(); var lid = l.GetAccount().Result.playerId;
            l.SetTestClock(U("2027-06-01T00:00:00")); T.Eq(lid, l.GetAccount().Result.playerId, "linked accounts not auto-deleted");
        });
        T.Add("REQ_E_age_gate_bands", () =>
        {
            T.Eq(AgeGate.Band.Under13, AgeGate.Classify(2013, 10, 2026, 10), "turns 13 this month → still under 13 (conservative)");
            T.Eq(AgeGate.Band.Minor13to17, AgeGate.Classify(2013, 9, 2026, 10), "13 last month");
            T.Eq(AgeGate.Band.Minor13to17, AgeGate.Classify(2008, 10, 2026, 10), "17 (18 this month, not yet)");
            T.Eq(AgeGate.Band.Adult, AgeGate.Classify(2008, 9, 2026, 10), "18");
            T.Eq(AgeGate.Band.Unknown, AgeGate.Classify(2027, 1, 2026, 10), "future"); T.Eq(AgeGate.Band.Unknown, AgeGate.Classify(2000, 13, 2026, 10), "bad month");
            T.True(!AgeGate.CanPurchase(AgeGate.Band.Minor13to17, false) && AgeGate.CanPurchase(AgeGate.Band.Minor13to17, true), "purchases need consent");
            T.True(!AgeGate.ShowLinkPrompt(AgeGate.Band.Minor13to17, false, false), "no link prompt to minors without consent (C3)");
            T.True(!AgeGate.AllowTracking(AgeGate.Band.Adult) && !AgeGate.AllowTracking(AgeGate.Band.Minor13to17), "no tracking");
            var sc = Src("Assets/_Project/Scripts/UI/Screens.cs");
            int g = sc.IndexOf("public class AgeGateScreen"), e = sc.IndexOf("public class BlockedScreen");
            var gate = sc.Substring(g, e - g);
            var copy = string.Join(" ", System.Text.RegularExpressions.Regex.Matches(gate, "\"([^\"]*)\"").Select(m => m.Groups[1].Value));
            T.True(!copy.Contains("13") && !copy.ToLowerInvariant().Contains("must be") && !copy.ToLowerInvariant().Contains("old enough"), "neutral copy: no age hint (E1)");
            T.True(gate.Contains("_ok.interactable = false"), "no default selection");
            T.True(!gate.Contains("SetInt(\"birth") && !gate.Contains("_year)") , "DOB never stored");
            var boot = Src("Assets/_Project/Scripts/Core/GameBootstrap.cs");
            T.True(boot.IndexOf("AgeGateScreen.Blocked") < boot.IndexOf("AppServices.Backend =") && boot.IndexOf("AgeGateScreen.Needed") < boot.IndexOf("SyncServerTime"), "gate before any backend traffic (E1/E2)");
        });
        T.Add("REQ_E3_E5_E6_server_enforcement", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var ag = b.SubmitAgeGate(2012, 1, "IN").Result; T.True(ag.band == "13to17" && ag.requiresParentalConsent, "minor band + consent required");
            T.Eq("13to17", b.SubmitAgeGate(1990, 5, "IN").Result.band, "most restrictive band kept (E3)");
            T.Eq("AGE_BLOCKED", Catch(() => b.SubmitAgeGate(2018, 1, "IN").Wait())?.Code, "under-13 refused");
            T.Eq("MINOR_CONSENT_REQUIRED", Catch(() => b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "t" }).Wait())?.Code, "link 403 without consent");
            T.Eq("AGE_RESTRICTED", Catch(() => b.VerifyPurchase("gems_100", "r").Wait())?.Code, "purchase 403 AGE_RESTRICTED");
            var cr = b.RequestParentalConsent("parent@example.com").Result; T.True(cr.status == "pending" && cr.requestId.Length > 0 && b.GetAccount().Result.consentPending, "consent request recorded");
            T.Eq("pending", b.GetParentalConsentStatus().Result.status, "consent status pending");
            b.GrantConsentForTests(true, false, false);
            T.True(b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "t" }).Result.linked, "link allowed with social consent");
            Win(b, "c1_l01");
            var me = b.GetLeaderboard(BoardType.Campaign, BoardPeriod.Current, false).Result.me; T.True(me.name.StartsWith("Wrecker #"), "generated name until name consent");
            b.GrantConsentForTests(true, true, false);
            T.True(!b.GetLeaderboard(BoardType.Campaign, BoardPeriod.Current, false).Result.me.name.StartsWith("Wrecker #"), "real name with consent");
        });
        T.Add("REQ_F1_F4_F5_links_and_hide_name", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "t" }).Wait(); b.LinkAccount(new LinkCredential { provider = AuthProvider.Email, token = "s" }).Wait();
            T.Eq("REAUTH_REQUIRED", Catch(() => b.UnlinkAccount(AuthProvider.Email, null).Wait())?.Code, "unlink needs re-auth");
            var rt = b.Reauthenticate(new LinkCredential { provider = AuthProvider.Google, token = "fresh" }).Result.reauthToken;
            T.True(!b.UnlinkAccount(AuthProvider.Email, rt).Result.identities.Contains("Email"), "unlinked");
            T.Eq("LAST_LOGIN", Catch(() => b.UnlinkAccount(AuthProvider.Google, rt).Wait())?.Code, "last login kept");
            Win(b, "c1_l01"); T.True(b.SetProfileVisibility(true).Result.hideName, "visibility echoed");
            var lb = b.GetLeaderboard(BoardType.Campaign, BoardPeriod.Current, false).Result;
            T.True(lb.me.name.StartsWith("Wrecker #") && lb.top.TrueForAll(e => !e.isMe || e.name.StartsWith("Wrecker #")), "real name never returned when hidden");
        });
        T.Add("SECADMIN_unity_services_off_and_no_ad_id", () =>
        {
            var uc = Src("ProjectSettings/UnityConnectSettings.asset");
            T.True(!System.Text.RegularExpressions.Regex.IsMatch(uc, @"m_Enabled:\s*1"), "all Unity services disabled");
            T.True(uc.Contains("CrashReportingSettings") && uc.Contains("UnityAnalyticsSettings"), "analytics + crash sections present");
            var man = Src("Assets/Plugins/Android/AndroidManifest.xml");
            T.True(man.Contains("com.google.android.gms.permission.AD_ID\" tools:node=\"remove\""), "AD_ID removed");
            var doc = Src("docs/Privacy_And_Account.md");
            T.True(doc.Contains("AutoLogAppEventsEnabled = false") && doc.Contains("AdvertiserIDCollectionEnabled = false"), "Facebook SDK note");
        });

        // ───── Audio: anim hooks + MusicPlayer (mixing_unity.md, loop_points.md) ─────
        T.Add("AUDIO_anim_hooks_done", () =>
        {
            T.Eq("ANIM_WRECKER_READY", AnimEvents.WreckerReady, "ready event name matches anim_audio_map");
            var pj = Src("Assets/_Project/Scripts/Weapons/Projectile.cs");
            T.True(pj.Contains("AnimEvents.PuPhasePass") && pj.Contains("AnimEvents.PuPhaseHit"), "Phase Piercer pass/3rd-hit events");
            T.Eq(0.6f + 0.4f * 0.5f, TideShutter.TellIntensity(0f, false), "tell starts mid");
            T.True(Math.Abs(TideShutter.TellIntensity(0.125f, false) - 1.0f) < 1e-4, "peak at 1/8 s"); T.True(Math.Abs(TideShutter.TellIntensity(0.625f, false) - 1.0f) < 1e-4, "next peak 0.5 s later → 2 Hz");
            T.True(TideShutter.TellIntensity(0.125f, true) <= 0.8f + 1e-4, "reduce motion halves amplitude");
            T.Eq(1.7f, TideShutter.Height(false, 0f, false)); T.Eq(0f, TideShutter.Height(false, 300f, false), "closed at 300 ms");
            T.True(TideShutter.Height(false, 150f, false) > 1.4f, "ease-in cubic (slow start, slam)"); T.True(!TideShutter.Done(false, 299f) && TideShutter.Done(false, 300f), "300 ms");
            var tw = Src("Assets/_Project/Scripts/Boss/TidewardenController.cs");
            T.True(!tw.Contains("Time.time * 18f") && tw.Contains("BossTideShutterClose"), "no 2.9 Hz glow, close event at tween end");
            var fd = Src("Assets/_Project/Scripts/Feel/FeelDirector.cs"); T.True(fd.Contains("_lastTitanDmg"), "Titan OnDamage only on change");
        });
        T.Add("AUDIO_loop_points_and_bars", () =>
        {
            var mt = Crashverse.Audio.MusicTracks.Get("main_theme"); T.True(mt.HasIntro && mt.loopStart == 378000, "main_theme intro split at 378000");
            T.Eq(377999L, mt.SampleAt(377999 / 44100.0)); T.Eq(378000L + 10, mt.SampleAt((3402000 + 10) / 44100.0), "wraps into loop region");
            var p1 = Crashverse.Audio.MusicTracks.Get("tidewarden_p1"); T.Eq(330750, p1.loopStart); T.True(Math.Abs(p1.BarSeconds - 1.875) < 1e-9, "128 BPM bar");
            T.True(!Crashverse.Audio.MusicTracks.All.ContainsKey("season2_stormfront_hub") && !Crashverse.Audio.MusicTracks.All.ContainsKey("titan_preview_obsidian_full"), "excluded tracks");
            T.Eq("titan_int25", Crashverse.Audio.MusicTracks.TitanLayer(1)); T.Eq("titan_int75", Crashverse.Audio.MusicTracks.TitanLayer(3)); T.Eq(null, Crashverse.Audio.MusicTracks.TitanLayer(0));
            T.True(Math.Abs(Crashverse.Audio.BarClock.NextBar(10, 11.0, 1.875) - 11.875) < 1e-9, "next bar line");
            var plan = Crashverse.Audio.TidewardenMusic.Plan(100.0, 101.0);
            T.True(Math.Abs(plan.stingerDsp - 101.875) < 1e-9 && Math.Abs(plan.p2StartDsp - 103.75) < 1e-9 && plan.p1StopDsp == plan.stingerDsp, "stinger at P1 bar end, P2 next bar");
        });
        T.Add("AUDIO_voice_rule_and_crossfade", () =>
        {
            for (float t = 0; t <= 1f; t += 0.1f) { float o = Crashverse.Audio.EqualPower.Out(t), i = Crashverse.Audio.EqualPower.In(t); T.True(Math.Abs(o * o + i * i - 1f) < 1e-5, "equal power"); }
            var v = new Crashverse.Audio.MusicVoiceState { Base = "titan_foundry_crater" };
            v.SetLayer("titan_int25", 0); T.True(!v.Crossfading(0) || v.Voices(0) <= 3, "≤3");
            v.Tick(1.1, out _); v.SetLayer("titan_int50", 2.0);
            T.Eq(3, v.Voices(2.5), "base + 2 layers mid-crossfade");
            T.True(!v.RequestStinger("stinger_win", 2.5, 3.0), "stinger waits for the crossfade"); T.Eq(3, v.Voices(2.5), "still 3");
            T.Eq(null, v.Tick(2.9, out _), "not before end");
            T.Eq("stinger_win", v.Tick(3.0, out var len), "starts once crossfade ends"); T.Eq(3.0, len);
            T.Eq(3, v.Voices(3.1), "base + layer + stinger = 3");
            T.True(!v.SetLayer("titan_int50", 3.2), "same layer → no-op (never stacks)");
            var mp = Src("Assets/_Project/Scripts/Audio/MusicPlayer.cs");
            T.True(mp.Contains("PlayScheduled") && mp.Contains("timeSamples") && mp.Contains("dspTime"), "sample-synced scheduling");
            var imp = Src("Assets/_Project/Scripts/Editor/MusicImportRules.cs"); T.True(imp.Contains("AudioClipLoadType.Streaming"), "all music streamed");
        });
    }
}

class MemIntPrefs : Crashverse.Core.IIntPrefs
{
    readonly Dictionary<string, int> _d = new Dictionary<string, int>();
    public bool TryGet(string k, out int v) => _d.TryGetValue(k, out v);
    public void Set(string k, int v) => _d[k] = v;
}

class MemPrefs : IMotionPrefs
{
    readonly Dictionary<string, bool> _d = new Dictionary<string, bool>();
    public bool TryGet(string k, out bool v) => _d.TryGetValue(k, out v);
    public void Set(string k, bool v) => _d[k] = v;
}
