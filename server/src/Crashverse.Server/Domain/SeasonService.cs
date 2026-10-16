using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>Season XP/tiers (SG-16/17/23), daily reward (SG-14), inbox. Premium only from an active entitlement (C2).</summary>
public sealed class SeasonService
{
    readonly Ctx _x; readonly Calendar _cal;
    public SeasonService(Ctx x) { _x = x; _cal = new Calendar(x); }
    SeasonData S => _x.Content.Season;

    public static int TierForXp(SeasonData s, int xp) { int t = 0; foreach (var x in s.tiers) if (xp >= x.xp) t = x.tier; return t; }
    public static int BonusChests(SeasonData s, int xp) { int cap = s.tiers[^1].xp; return xp <= cap ? 0 : Math.Min(s.bonusChestCap, (xp - cap) / s.bonusChestEveryXp); }

    public static string PremiumEntitlementId(string pid, string seasonId) => $"{pid}:season_premium:{seasonId}";
    public async Task<bool> HasPremium(string pid, string seasonId) => (await _x.Db.Get<EntitlementDoc>("entitlements", PremiumEntitlementId(pid, seasonId)))?.state == "active";

    async Task<SeasonProgressDoc> Progress(string pid, string seasonId) => await _x.Db.Get<SeasonProgressDoc>("seasonProgress", pid + ":" + seasonId) ?? new SeasonProgressDoc { seasonId = seasonId };
    Task Save(string pid, SeasonProgressDoc p) => _x.Db.Put("seasonProgress", pid + ":" + p.seasonId, p);

    /// <summary>Lazy rollover (BUG-06/15): unclaimed reached tiers of the previous season go to the inbox for 30 days. Idempotent.</summary>
    async Task Rollover(string pid)
    {
        var prevId = _cal.SeasonIdFor(_cal.SeasonIndex(_x.Now) - 1);
        var prev = await _x.Db.Get<SeasonProgressDoc>("seasonProgress", pid + ":" + prevId);
        if (prev == null || prev.endProcessed) return;
        bool prem = await HasPremium(pid, prevId);
        long exp = Ctx.Unix(_x.Now.AddDays(S.unclaimedInboxDays));
        for (int i = 1; i <= TierForXp(S, prev.xp); i++)
        {
            var tr = S.tiers[i - 1];
            if (!prev.claimedFree.Contains(i) && tr.free != null)
                await _x.Db.TryCreate("inbox", $"{pid}:{prevId}_tier_{i}_free", new InboxDoc { id = $"{prevId}_tier_{i}_free", playerId = pid, source = $"Unclaimed season tier {i}", reward = tr.free, expiresUnixUtc = exp });
            if (prem && !prev.claimedPremium.Contains(i) && tr.premium != null)
                await _x.Db.TryCreate("inbox", $"{pid}:{prevId}_tier_{i}_premium", new InboxDoc { id = $"{prevId}_tier_{i}_premium", playerId = pid, source = $"Unclaimed premium tier {i}", reward = tr.premium, expiresUnixUtc = exp });
        }
        prev.endProcessed = true; await Save(pid, prev);
    }

    public async Task<SeasonState> State(string pid)
    {
        await Rollover(pid);
        var now = _x.Now; var sid = _cal.SeasonKey(now); var p = await Progress(pid, sid); int w = _cal.WeekIndex(now);
        return new SeasonState
        {
            seasonId = sid, name = _cal.SeasonIndex(now) == 0 ? S.name : S.name.Replace("Season 1", "Season " + (_cal.SeasonIndex(now) + 1)),
            xp = p.xp, tier = TierForXp(S, p.xp), weekIndex = w, weekEventId = w >= 0 && w < S.weeks.Length ? S.weeks[w].id : "",
            weekEndsUnixUtc = Ctx.Unix(_cal.SeasonStartFor(now).AddDays((w + 1) * S.weekDays)), seasonEndsUnixUtc = Ctx.Unix(_cal.SeasonEndFor(now)),
            premium = await HasPremium(pid, sid), bonusChests = BonusChests(S, p.xp), claimedFree = new(p.claimedFree), claimedPremium = new(p.claimedPremium)
        };
    }

    public Task OnTitanClear(string pid, string dayKey, string runId) => _x.Db.Atomic("season:" + pid, async () =>
    {
        var sid = _cal.SeasonKey(_x.Now); var p = await Progress(pid, sid);
        if (p.titanClearDays.Contains(dayKey)) return 0;
        p.titanClearDays.Add(dayKey); p.xp += S.xpPerTitanClear; p.titanXp += S.xpPerTitanClear; await Save(pid, p);
        var w = new WalletService(_x);
        foreach (var cr in _x.Content.Titan.completionRewards ?? Array.Empty<Reward>()) await w.Grant(pid, cr, "titan_clear", runId);
        await new Leaderboards(_x).Submit("season:" + sid, pid, BoardXp(p), runId, replace: true);
        await AttachRun(runId, "season:" + sid);
        return 0;
    });

    /// <summary>H2: while the public ranked board is off, the season board excludes Titan-derived XP (progression XP is unaffected).</summary>
    int BoardXp(SeasonProgressDoc p) => _x.Opt.RankedPublicBoardEnabled && _x.Replay.Configured ? p.xp : p.xp - p.titanXp;

    async Task AttachRun(string runId, string boardKey)
    {
        await _x.Db.Atomic("run:" + runId, async () =>
        {
            var r = await _x.Db.Get<RunDoc>("runs", runId); if (r == null) return 0;
            if (!r.boardKeys.Contains(boardKey)) r.boardKeys.Add(boardKey);
            await _x.Db.Put("runs", runId, r); return 0;
        });
    }

    public Task<SeasonState> ReportEventStars(string pid, string levelId, int stars, string runId) => _x.Db.Atomic("season:" + pid, async () =>
    {
        var now = _x.Now; var sid = _cal.SeasonKey(now); var p = await Progress(pid, sid);
        int best = p.eventBest.GetValueOrDefault(levelId);
        if (stars > best)
        {
            p.xp += (stars - best) * S.xpPerEventStar; p.eventBest[levelId] = stars; await Save(pid, p);
            int w = _cal.WeekIndex(now); int weekly = 0;
            if (w >= 0 && w < S.weeks.Length) foreach (var l in S.weeks[w].levels) weekly += p.eventBest.GetValueOrDefault(l.id);
            var lb = new Leaderboards(_x);
            await lb.Submit("week:" + _cal.WeekKey(now), pid, weekly, runId, replace: true);
            await lb.Submit("season:" + sid, pid, BoardXp(p), runId, replace: true);
            await AttachRun(runId, "week:" + _cal.WeekKey(now)); await AttachRun(runId, "season:" + sid);
            var run = await _x.Db.Get<RunDoc>("runs", runId);
            if (w >= 0 && w < S.weeks.Length) await new EconomyService(_x).OnEventProgress(pid, _cal.WeekKey(now), S.weeks[w].levels.Select(l => l.id), p.eventBest);
            var rs = new ReplayService(_x);
            if (await rs.ShouldReplay(run, await lb.Position("week:" + _cal.WeekKey(now), pid) <= _x.Opt.TopN)) await rs.Enqueue(runId, "event");
        }
        return await State(pid);
    });

    public Task<SeasonState> ClaimTier(string pid, int tier, bool premium) => _x.Db.Atomic("season:" + pid, async () =>
    {
        if (tier < 1 || tier > S.tiers.Length) throw new ApiError(400, "BAD_TIER", "Bad tier");
        var sid = _cal.SeasonKey(_x.Now); var p = await Progress(pid, sid);
        if (TierForXp(S, p.xp) < tier) throw new ApiError(409, "TIER_NOT_REACHED", "Tier not reached");
        if (premium && !await HasPremium(pid, sid)) throw new ApiError(403, "PREMIUM_NOT_OWNED", "Premium track (cosmetic only) not owned");
        var list = premium ? p.claimedPremium : p.claimedFree;
        if (!list.Contains(tier))
        {
            list.Add(tier); var t = S.tiers[tier - 1];
            await new WalletService(_x).Grant(pid, premium ? t.premium : t.free, premium ? "season_tier_premium" : "season_tier", $"{sid}:{tier}",
                premium ? "premium_tier:" + sid : "reward");
            if (!premium) await new EconomyService(_x).SeasonTier(pid, sid, tier);
            await Save(pid, p);
        }
        return await State(pid);
    });

    // ---------------- daily reward ----------------
    public async Task<DailyRewardState> DailyState(string pid)
    {
        var d = await _x.Db.Get<DailyRewardDoc>("dailyReward", pid) ?? new DailyRewardDoc();
        var now = _x.Now;
        return new DailyRewardState { streakIndex = d.streakIndex, lastClaimDayKey = d.lastClaimDayKey, canClaimToday = d.lastClaimDayKey != UtcDay.Key(now),
            nextResetUnixUtc = Ctx.Unix(UtcDay.NextReset(now)), days = _x.Content.DailyRewards.days };
    }

    public Task<WalletEx> ClaimDaily(string pid) => _x.Db.Atomic("daily:" + pid, async () =>
    {
        var d = await _x.Db.Get<DailyRewardDoc>("dailyReward", pid) ?? new DailyRewardDoc();
        var key = UtcDay.Key(_x.Now);
        if (d.lastClaimDayKey == key) throw new ApiError(409, "ALREADY_CLAIMED", "Already claimed today");
        var days = _x.Content.DailyRewards.days;
        var (w, _) = await new WalletService(_x).Grant(pid, days[d.streakIndex % days.Length], "daily_reward", key);
        if (d.streakIndex % days.Length == 6 && await new EconomyService(_x).Day7Chest(pid, key)) w = await new WalletService(_x).Get(pid); // Economy v1: Day 7 chest + 10 gems
        d.streakIndex = (d.streakIndex + 1) % days.Length; d.lastClaimDayKey = key;
        await _x.Db.Put("dailyReward", pid, d);
        return WalletService.View(w);
    });

    // ---------------- inbox ----------------
    public async Task<List<InboxItem>> Inbox(string pid)
    {
        await Rollover(pid);
        long now = Ctx.Unix(_x.Now);
        return (await _x.Db.List<InboxDoc>("inbox", pid + ":")).Where(i => !i.claimed && i.expiresUnixUtc > now)
            .Select(i => new InboxItem { id = i.id, source = i.source, reward = i.reward, expiresUnixUtc = i.expiresUnixUtc, claimed = false }).ToList();
    }

    public Task<WalletEx> ClaimInbox(string pid, string itemId) => _x.Db.Atomic("inbox:" + pid + ":" + itemId, async () =>
    {
        var w = new WalletService(_x);
        var it = await _x.Db.Get<InboxDoc>("inbox", pid + ":" + itemId);
        if (it != null && !it.claimed && it.expiresUnixUtc > Ctx.Unix(_x.Now))
        {
            it.claimed = true; await _x.Db.Put("inbox", pid + ":" + itemId, it);
            return WalletService.View((await w.Grant(pid, it.reward, "inbox", itemId)).w);
        }
        return WalletService.View(await w.Get(pid));
    });
}

/// <summary>D6: mutual-add friend codes stored on the server.</summary>
public sealed class Friends
{
    readonly Ctx _x; public Friends(Ctx x) { _x = x; }
    public sealed class FriendView { public string playerId; public string name; public bool mutual; }

    public async Task<ListResponse<FriendView>> Add(string pid, string code)
    {
        var target = await _x.Db.Get<IdentityDoc>("friendCodes", (code ?? "").Trim().ToUpperInvariant()) ?? throw new ApiError(404, "UNKNOWN_FRIEND_CODE", "unknown friend code");
        if (target.playerId == pid) throw new ApiError(400, "BAD_REQUEST", "cannot add yourself");
        await _x.Db.Atomic("friends:" + pid, async () =>
        {
            var f = await _x.Db.Get<FriendDoc>("friends", pid) ?? new FriendDoc { playerId = pid };
            if (f.added.Count >= 200) throw new ApiError(409, "FRIEND_LIMIT", "friend limit reached");
            if (!f.added.Contains(target.playerId)) f.added.Add(target.playerId);
            await _x.Db.Put("friends", pid, f); return 0;
        });
        return await List(pid);
    }

    public async Task<List<string>> Mutual(string pid)
    {
        var f = await _x.Db.Get<FriendDoc>("friends", pid) ?? new FriendDoc();
        var res = new List<string>();
        foreach (var o in f.added) if ((await _x.Db.Get<FriendDoc>("friends", o))?.added.Contains(pid) == true) res.Add(o);
        return res;
    }

    public async Task<ListResponse<FriendView>> List(string pid)
    {
        var f = await _x.Db.Get<FriendDoc>("friends", pid) ?? new FriendDoc();
        var mutual = (await Mutual(pid)).ToHashSet(); var list = new List<FriendView>();
        foreach (var o in f.added) list.Add(new FriendView { playerId = o, name = AuthService.PublicName(await _x.Db.Get<AccountDoc>("accounts", o)), mutual = mutual.Contains(o) });
        return new ListResponse<FriendView>(list);
    }

    public async Task<ListResponse<FriendView>> Remove(string pid, string other)
    {
        await _x.Db.Atomic("friends:" + pid, async () =>
        {
            var f = await _x.Db.Get<FriendDoc>("friends", pid) ?? new FriendDoc { playerId = pid };
            f.added.Remove(other); await _x.Db.Put("friends", pid, f); return 0;
        });
        return await List(pid);
    }
}
