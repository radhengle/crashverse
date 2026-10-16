using System;
using System.IO;
using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Level;
using Crashverse.Meta;

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

    static RankedResult Res(RankedTicket t, int dmg, int left, bool killed, bool partial = false)
    {
        var r = new RankedResult { token = t.token, damageScore = dmg, shotsLeft = left, killed = killed, partial = partial };
        r.signature = LocalBackendService.Sign(r);
        return r;
    }

    public static void Register()
    {
        // ---------- SG-24 / SG-01 stars (qa/M1_Test_Cases_v0.3 + Feature_Test_Cases_v1 §A) ----------
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
            int[] shots = { 4, 4, 5, 5, 5, 5, 5, 5, 6, 7 };
            for (int i = 0; i < 10; i++) { T.Eq(shots[i], ch.levels[i].shots, "shots L" + (i + 1)); T.Eq(0.6f, ch.levels[i].star3Pct, "star3 L" + (i + 1)); }
            T.Eq("m1_clockwork_tower", ch.levels[6].layout, "L7 = M1 tower");
            T.Eq("tidewarden", ch.levels[9].bossId, "L10 boss");
            T.Eq(2, ch.levels[8].starCapIfPowerupUsed, "L9 cap");
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
            T.Eq("shockwave", lo[Slot.Core], "L7 forced shockwave");
            T.True(Loadout.IsSlotLocked(L7, Slot.Module), "L7 module locked");
            var lo8 = Loadout.Resolve(L8, null, Db, save);
            T.Eq("splitstrike", lo8[Slot.Module], "L8 forced splitstrike");
            T.True(lo8.TryEquip(Slot.Booster, Db.GetPowerUp("shockwave"), L8, Db, save) != null, "core tile can't go in booster");
            T.True(lo8.TryEquip(Slot.Booster, Db.GetPowerUp("phase_piercer"), L8, Db, save) != null, "future tile locked");
            T.True(!UnlockRules.IsPowerUpUnlocked(Db.GetPowerUp("gravity_well"), Db, save), "gravity well locked until L10 clear");
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
            T.Eq(SubmitStatus.Accepted, b.SubmitRanked(Res(t, 5000, 2, true)).Result.status, "00:15:00.000 accepted");
            var b2 = NewBackend("2026-10-16T23:50:00");
            var t2 = b2.BeginRankedRun().Result;
            b2.SetTestClock(U("2026-10-17T00:15:00.001"));
            T.Eq(SubmitStatus.RefundedAsPractice, b2.SubmitRanked(Res(t2, 5000, 2, true)).Result.status, "00:15:00.001 refunded as practice");
        });
        T.Add("SG11_partial_submit", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var t = b.BeginRankedRun().Result;
            var resp = b.SubmitRanked(Res(t, 3100, 3, false, partial: true)).Result;
            T.Eq(SubmitStatus.Accepted, resp.status, "partial accepted");
            T.Eq(3100, resp.finalScore, "no shot bonus without kill");
            T.Eq("submitted_partial", b.GetRankedStatus().Result.status, "status partial, attempt used");
            T.Eq(SubmitStatus.RejectedDuplicate, b.SubmitRanked(Res(t, 9000, 3, true)).Result.status, "no resubmit");
        });
        T.Add("ranked_validation", () =>
        {
            var b = NewBackend("2026-10-16T10:00:00");
            var t = b.BeginRankedRun().Result;
            var r = Res(t, 20000, 2, true); T.Eq(SubmitStatus.RejectedInvalid, b.SubmitRanked(r).Result.status, "score out of range");
            var r2 = Res(t, 100, 2, true); r2.signature = "bad"; T.Eq(SubmitStatus.RejectedInvalid, b.SubmitRanked(r2).Result.status, "bad signature");
            var r3 = new RankedResult { token = "nope" }; r3.signature = LocalBackendService.Sign(r3);
            T.Eq(SubmitStatus.RejectedInvalid, b.SubmitRanked(r3).Result.status, "unknown token");
            var ok = b.SubmitRanked(Res(t, 8000, 2, true)).Result;
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
            var t = b.BeginRankedRun().Result; b.SubmitRanked(Res(t, 9000, 1, true)).Wait();
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
            var t = b.BeginRankedRun().Result; b.SubmitRanked(Res(t, 10000, 6, true)).Wait();
            var p = b.GetLeaderboard(BoardType.DailyTitan, BoardPeriod.Current, false).Result;
            T.True(p.meIsGuestHidden && p.me == null, "guest stored but hidden");
            T.True(p.top.Count == 100, "top 100");
            b.LinkAccount("Google").Wait();
            p = b.GetLeaderboard(BoardType.DailyTitan, BoardPeriod.Current, false).Result;
            T.True(p.me != null && p.me.rank == 1, "linked player listed at rank 1 (13000)");
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
            var t = b.BeginRankedRun().Result; b.SubmitRanked(Res(t, 10000, 6, true)).Wait();
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
            var b = NewBackend("2026-10-16T10:00:00"); var L10 = Db.GetLevel("c1_l10");
            b.GrantCampaignReward(L10.id, L10.reward).Wait(); b.GrantCampaignReward(L10.id, L10.reward).Wait();
            var w = b.GetWallet().Result; T.Eq(500, w.coins, "chest once"); T.True(w.powerUps.Contains("gravity_well"), "gravity well granted");
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
                new StoreItem { sku = "nc", name = "Skin", type = Db.Store.allowedTypes[0], cosmetic = false, priceType = "gems", price = 10 },
                new StoreItem { sku = "gr", name = "Gems?", type = Db.Store.allowedTypes[0], cosmetic = true, priceType = "iap", price = 99, grants = "extra_charges:3" } } };
            T.Eq(3, StoreValidator.Validate(bad, out var ok2).Count, "all 3 rejected"); T.Eq(0, ok2.Count, "none accepted");
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
    }
}
