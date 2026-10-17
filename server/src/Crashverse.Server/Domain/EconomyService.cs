using Crashverse.Backend;
using System.Text.Json;
using Crashverse.Data;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>Economy v1 values (server/config/economy_v1.json).</summary>
public sealed class EconomyConfig
{
    public int dailyDay7Gems, titanCompletionGems, titanTop10Gems, eventAllClearedGems, eventAllThreeStarGems, guardianFirstClearGems;
    public int seasonEveryNthTier, seasonNthTierGems, seasonPremiumUnlockGems;
    public List<Milestone> chapterStarMilestones = new(); public List<Achievement> achievements = new();
    public sealed class Milestone { public int stars; public int gems; }
    public sealed class Achievement { public string id; public int gems; }
    public static EconomyConfig Load(string path)
    {
        var c = JsonSerializer.Deserialize<EconomyConfig>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true })
                ?? throw new InvalidDataException("economy config empty");
        if (c.seasonPremiumUnlockGems <= 0 || c.seasonEveryNthTier <= 0) throw new InvalidDataException("economy config invalid");
        foreach (var a in c.achievements) if (a.gems < 5 || a.gems > 25) throw new InvalidDataException($"achievement {a.id}: gems must be 5-25");
        return c;
    }
}

/// <summary>Economy v1: gems are earned only. Every grant goes through the wallet ledger with one idempotency key per source
/// (grantKeys/{pid}:{source}:{key}), so retries, replays and duplicate jobs never pay twice.</summary>
public sealed class EconomyService
{
    readonly Ctx _x; public EconomyService(Ctx x) { _x = x; }
    EconomyConfig E => _x.Economy;

    /// <summary>Returns true if granted now, false if this source+key was already paid.</summary>
    public Task<bool> GrantGems(string pid, int gems, string source, string key)
    {
        if (gems <= 0) return Task.FromResult(false);
        var w = new WalletService(_x);
        return w.Locked(pid, async () =>
        {
            var id = $"{pid}:{source}:{key}";
            if (!await _x.Db.TryCreate("grantKeys", id, new IdentityDoc { key = id, playerId = pid, linkedUnixMs = _x.NowMs })) return false;
            await w.ApplyUnlocked(pid, new WalletService.Change { delta = { ["gems"] = gems }, note = source }, "gems:" + source, key);
            return true;
        });
    }

    public Task<bool> Day7Chest(string pid, string dayKey) => GrantGems(pid, E.dailyDay7Gems, "daily_day7", dayKey);
    public Task<bool> TitanCompletion(string pid, string dayKey) => GrantGems(pid, E.titanCompletionGems, "titan_completion", dayKey);
    public Task<bool> TitanTop10(string pid, string dayKey) => GrantGems(pid, E.titanTop10Gems, "titan_top10", dayKey);
    public Task<bool> SeasonTier(string pid, string sid, int tier) =>
        tier % E.seasonEveryNthTier == 0 ? GrantGems(pid, E.seasonNthTierGems, "season_tier", $"{sid}:{tier}") : Task.FromResult(false);

    public async Task<bool> Achievement(string pid, string id)
    {
        var a = E.achievements.FirstOrDefault(a => a.id == id);
        return a != null && await GrantGems(pid, a.gems, "achievement", id);
    }

    /// <summary>Weekly event: all 5 levels cleared → 15; every level at 3★ → +10 (each once per week).</summary>
    public async Task OnEventProgress(string pid, string weekKey, IEnumerable<string> weekLevels, Dictionary<string, int> best)
    {
        var lv = weekLevels.ToList(); if (lv.Count == 0) return;
        if (lv.All(l => best.GetValueOrDefault(l) >= 1)) await GrantGems(pid, E.eventAllClearedGems, "event_all_cleared", weekKey);
        if (lv.All(l => best.GetValueOrDefault(l) >= 3)) { await GrantGems(pid, E.eventAllThreeStarGems, "event_all_3star", weekKey); await Achievement(pid, "event_week_perfect"); }
    }

    /// <summary>Called only once a campaign run is validated by replay: star milestones per chapter and Guardian first clear.</summary>
    public async Task OnCampaignValidated(string pid, LevelEntry lvl, bool won, int shotsLeft)
    {
        if (!won) return;
        int stars = Crashverse.Meta.StarRules.Compute(true, shotsLeft, lvl.shots, lvl.star3Pct, lvl.star2Pct);
        var key = pid + ":" + lvl.id;
        var rec = await _x.Db.Get<LevelBestDoc>("campaignBest", key);
        if (rec != null && stars > rec.stars) { rec.stars = stars; await _x.Db.Put("campaignBest", key, rec); }
        await Achievement(pid, "first_campaign_clear");
        if (lvl.IsBoss) { await GrantGems(pid, E.guardianFirstClearGems, "guardian_first_clear", lvl.id); await Achievement(pid, "first_guardian_clear"); }
        int total = (await _x.Db.List<LevelBestDoc>("campaignBest", pid + ":")).Where(b => b.validated).Sum(b => b.stars);
        foreach (var m in E.chapterStarMilestones)
            if (total >= m.stars) await GrantGems(pid, m.gems, "chapter_stars", $"c1:{m.stars}");
    }

    /// <summary>Season premium track unlock for 450 earned gems. Idempotent; locked while in gem debt.</summary>
    public async Task<SeasonState> UnlockPremium(string pid)
    {
        var sid = new Calendar(_x).SeasonKey(_x.Now); var eid = SeasonService.PremiumEntitlementId(pid, sid);
        var w = new WalletService(_x);
        await w.Locked(pid, async () =>
        {
            if ((await _x.Db.Get<EntitlementDoc>("entitlements", eid))?.state == "active") return 0;
            var wd = await w.Get(pid);
            if (wd.gems < 0) throw new ApiError(409, "GEM_DEBT_LOCKED", "Gem spending is locked until your refunded gem balance is repaid");
            if (wd.gems < E.seasonPremiumUnlockGems) throw new ApiError(409, "INSUFFICIENT_FUNDS", "not enough gems");
            await w.ApplyUnlocked(pid, new WalletService.Change { delta = { ["gems"] = -E.seasonPremiumUnlockGems }, note = "season premium track" }, "season_premium_unlock", sid);
            await _x.Db.Put("entitlements", eid, new EntitlementDoc { id = eid, playerId = pid, type = "season_premium", seasonId = sid, state = "active", purchaseTokenHash = "gems" });
            return 0;
        });
        return await new SeasonService(_x).State(pid);
    }
}
