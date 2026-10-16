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
            b.LinkAccount("Google").Wait();
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
            var w = b.GetWallet().Result; T.True(w.powerUps.Contains("gravity_well"), "L10 power-up granted by server on first validated clear");
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
            var b = NewBackend("2026-11-08T10:00:00"); b.LinkAccount("Google").Wait(); b.GrantPremiumPassStub();
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
            var b = NewBackend("2026-10-20T10:00:00"); b.LinkAccount("Google").Wait();
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
            var b = NewBackend("2026-10-16T10:00:00"); b.LinkAccount("Google").Wait();
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
            var b = NewBackend("2026-10-16T10:00:00"); b.LinkAccount("Google").Wait();
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
    }
}
