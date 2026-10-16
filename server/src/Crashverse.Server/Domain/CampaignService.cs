using Crashverse.Backend;
using System.Text.Json.Nodes;
using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

public sealed class AttemptRequest { public string mode = "campaign"; public string levelId; public string levelVersion; public int count = 1; }
public sealed class AttemptView { public string attemptToken; public string levelId; public string levelVersion; public long seed; public long expiresUnixUtc; }
public sealed class EventStarsRequest { public string attemptToken; public int stars; public string simVersion; public string buildHash; public List<InputEvent> inputs = new(); }

/// <summary>
/// Campaign + weekly-event results (H1). Attempt tokens: signed, single-use, bound to player + mode + level + level version,
/// expiring (≤ 7 days), pool of 20 outstanding per mode (D4). Scores come from inputs + plausibility; boards wait for replay.
/// </summary>
public sealed class CampaignService
{
    readonly Ctx _x; public CampaignService(Ctx x) { _x = x; }

    LevelEntry LevelFor(string mode, string id) => mode == "event" ? _x.Content.EventLevel(id) : _x.Content.Level(id);

    public async Task<ListResponse<AttemptView>> IssueAttempts(string pid, AttemptRequest r)
    {
        if (r.mode != "campaign" && r.mode != "event") throw new ApiError(400, "BAD_REQUEST", "mode must be campaign or event");
        var lvl = LevelFor(r.mode, r.levelId) ?? throw new ApiError(404, "UNKNOWN_LEVEL", "unknown level");
        var ver = Content.LevelVersion(lvl);
        if (r.levelVersion != null && r.levelVersion != ver) throw new ApiError(409, "STALE_LEVEL_VERSION", "update content");
        return await _x.Db.Atomic("attempts:" + pid + ":" + r.mode, async () =>
        {
            var outstanding = (await _x.Db.List<AttemptDoc>("attempts", pid + ":" + r.mode + ":")).Count(a => !a.used && a.expUnixMs > _x.NowMs);
            int n = Math.Max(0, Math.Min(Math.Max(1, r.count), _x.Opt.AttemptPoolSize - outstanding));
            var list = new List<AttemptView>();
            for (int i = 0; i < n; i++)
            {
                var id = Hash.NewId(); long seed = BitConverter.ToInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
                var exp = _x.Now.AddDays(_x.Opt.AttemptTokenTtlDays);
                await _x.Db.Put("attempts", $"{pid}:{r.mode}:{id}", new AttemptDoc { attemptId = id, playerId = pid, mode = r.mode, levelId = lvl.id, levelVersion = ver, seed = seed, expUnixMs = Ctx.UnixMs(exp) });
                var tok = _x.Tokens.Issue("attempt", new JsonObject { ["aid"] = id, ["sub"] = pid, ["mode"] = r.mode, ["lvl"] = lvl.id, ["lv"] = ver, ["seed"] = seed },
                    TimeSpan.FromDays(_x.Opt.AttemptTokenTtlDays));
                list.Add(new AttemptView { attemptToken = tok, levelId = lvl.id, levelVersion = ver, seed = seed, expiresUnixUtc = Ctx.Unix(exp) });
            }
            return new ListResponse<AttemptView>(list);
        });
    }

    /// <summary>Verifies and burns an attempt token (single use). Throws on anything wrong.</summary>
    async Task<AttemptDoc> Burn(string pid, string mode, string levelId, string token)
    {
        var c = _x.Tokens.Verify("attempt", token, out var err) ?? throw new ApiError(401, "INVALID_ATTEMPT", "attempt token: " + err);
        if ((string)c["sub"] != pid || (string)c["mode"] != mode || (string)c["lvl"] != levelId) throw new ApiError(403, "INVALID_ATTEMPT", "attempt token not valid for this player/level");
        var id = $"{pid}:{mode}:{(string)c["aid"]}";
        return await _x.Db.Atomic("attempt:" + id, async () =>
        {
            var a = await _x.Db.Get<AttemptDoc>("attempts", id) ?? throw new ApiError(401, "INVALID_ATTEMPT", "unknown attempt");
            if (a.used) throw new ApiError(409, "ATTEMPT_REUSED", "attempt token already used");
            if (a.expUnixMs < _x.NowMs) throw new ApiError(401, "INVALID_ATTEMPT", "attempt token expired");
            if (a.levelVersion != Content.LevelVersion(LevelFor(mode, levelId))) throw new ApiError(409, "STALE_LEVEL_VERSION", "level changed since the token was issued");
            a.used = true; await _x.Db.Put("attempts", id, a);
            return a;
        });
    }

    string Validate(CampaignRunReportEx r, LevelEntry lvl, CampaignProgressDoc prog)
    {
        if (!r.won) return "only wins are recorded";
        if (!string.IsNullOrEmpty(lvl.requires) && !prog.cleared.Contains(lvl.requires)) return "prerequisite level not cleared on server";
        if (r.shotsLeft < 0 || r.shotsLeft > lvl.shots - 1) return "shots left out of range";
        if (float.IsNaN(r.destroyedRatio) || r.destroyedRatio < 0f || r.destroyedRatio > 1f) return "ratio out of range";
        if (!lvl.IsBoss && !Crashverse.Meta.WinRule.Meets(r.destroyedRatio, lvl.winThreshold)) return "ratio below win threshold";
        int pieces = _x.Content.Pieces(lvl);
        if (r.bestCombo < 0 || r.bestCombo > pieces) return "combo out of range";
        return InputsPlausible(r.inputs, lvl.shots - r.shotsLeft);
    }

    /// <summary>Input-log plausibility (agreed InputEvent shape): one fire per shot used, settle after each, server minimum shot interval in ticks, ranges, per-shot event cap.</summary>
    string InputsPlausible(List<InputEvent> inputs, int shotsUsed) => InputRules.Check(inputs, shotsUsed, _x.Opt.MinShotIntervalMs * _x.Opt.PhysicsHz / 1000);

    public int Ceiling(LevelEntry l) => 1000 + 500 * Math.Max(0, l.shots - 1) + 50 * _x.Content.Pieces(l) + 1000;

    public async Task<CampaignSubmitResponseEx> Submit(string pid, CampaignRunReportEx r)
    {
        var res = await SubmitCore(pid, r);
        res.status ??= !res.accepted ? "rejected" : res.validation == "pending" ? "pending" : "accepted";
        return res;
    }

    async Task<CampaignSubmitResponseEx> SubmitCore(string pid, CampaignRunReportEx r)
    {
        if (r == null) throw new ApiError(400, "BAD_REQUEST", "body required");
        r.simVersion ??= r.buildHash; // agreed interface: buildHash identifies the client simulation build
        if (string.IsNullOrEmpty(r.levelId))   // SubmitScore carries only the attempt token: the level comes from its signed claims
            r.levelId = (string)_x.Tokens.Verify("attempt", r.attemptToken, out _)?["lvl"];
        if (string.IsNullOrEmpty(r.reportId) && !string.IsNullOrEmpty(r.attemptToken)) r.reportId = Hash.Sha256Hex(r.attemptToken)[..32];
        if (string.IsNullOrEmpty(r.reportId)) throw new ApiError(400, "BAD_REQUEST", "attemptToken required");
        var pkey = pid;
        return await _x.Db.Atomic("campaign:" + pid, async () =>
        {
            var prog = await _x.Db.Get<CampaignProgressDoc>("campaignProgress", pkey) ?? new CampaignProgressDoc();
            if (prog.processedReports.Contains(r.reportId))
                return new CampaignSubmitResponseEx { accepted = true, message = "Already synced", levelBest = (await Best(pid, r.levelId))?.best ?? 0, total = await Total(pid) };
            var lvl = _x.Content.Level(r.levelId) ?? throw new ApiError(404, "UNKNOWN_LEVEL", "unknown level");
            await Burn(pid, "campaign", lvl.id, r.attemptToken); // H1: no token, no result (replayed reports fail here)
            if (r.simVersion != _x.Opt.SimVersion) return new CampaignSubmitResponseEx { accepted = false, message = "Rejected: simVersion mismatch", total = await Total(pid) };
            var err = Validate(r, lvl, prog);
            if (err != null)
            {
                await _x.Audit(pid, "campaign_rejected", pid, $"{lvl.id}: {err}");
                return new CampaignSubmitResponseEx { accepted = false, message = "Rejected: " + err, total = await Total(pid) };
            }
            int score = Crashverse.Meta.CampaignScore.Compute(true, r.destroyedRatio, r.shotsLeft, r.bestCombo, r.mastery);
            if (score > Ceiling(lvl)) return new CampaignSubmitResponseEx { accepted = false, message = "Rejected: score above level ceiling", total = await Total(pid) };

            var runId = Hash.NewId();
            var run = new RunDoc { runId = runId, playerId = pid, mode = "campaign", key = lvl.id, simVersion = r.simVersion, campaign = r, clientScore = r.clientScoreBp > 0 ? r.clientScoreBp : score, finalScore = score,
                issuedUnixMs = _x.NowMs, finalized = true, boardKeys = { "campaign:c1_alltime" } };
            bool first = !prog.cleared.Contains(lvl.id);
            if (first) prog.cleared.Add(lvl.id);
            bool deferred = false;
            if (first && lvl.reward != null && lvl.reward.Length > 0 && !prog.rewarded.Contains(lvl.id))
            {
                var day = UtcDay.Key(_x.Now);
                int today = prog.firstClearsByDay.GetValueOrDefault(day);
                if (today < _x.Opt.FirstClearDailyCap) // review gap 4: capped pre-validation rewards, clawed back if replay fails
                {
                    prog.firstClearsByDay[day] = today + 1; prog.rewarded.Add(lvl.id);
                    var w = new WalletService(_x);
                    foreach (var rw in lvl.reward) run.provisionalLedgerIds.Add((await w.Grant(pid, rw, "first_clear_provisional", runId)).e.id);
                }
                else { deferred = true; run.firstClearDeferred = true; }
            }
            await _x.Db.Put("runs", runId, run);
            var rec = await Best(pid, lvl.id) ?? new LevelBestDoc { levelId = lvl.id };
            if (score > rec.best) { rec.best = score; rec.runId = runId; rec.validated = false; }
            await _x.Db.Put("campaignBest", pid + ":" + lvl.id, rec);
            int total = await Total(pid);
            var lb = new Leaderboards(_x);
            await lb.Submit("campaign:c1_alltime", pid, total, runId, replace: true);
            var rs = new ReplayService(_x);
            bool top = await lb.Position("campaign:c1_alltime", pid) <= _x.Opt.TopN;
            if (await rs.ShouldReplay(run, top || first && lvl.IsBoss)) await rs.Enqueue(runId, "campaign");
            prog.processedReports.Add(r.reportId);
            await _x.Db.Put("campaignProgress", pkey, prog);
            return new CampaignSubmitResponseEx { accepted = true, message = "Campaign result synced (pending validation)", levelBest = rec.best, total = total, firstClear = first, validation = "pending", rewardDeferred = deferred, serverScoreBp = score };
        });
    }

    /// <summary>Replay outcome for a campaign run: lower-of score; failure claws back provisional first-clear rewards (floor 0).</summary>
    public async Task OnReplay(RunDoc run, bool ok, int scoreBefore)
    {
        var pid = run.playerId; var w = new WalletService(_x);
        await _x.Db.Atomic("campaign:" + pid, async () =>
        {
            var rec = await Best(pid, run.key);
            if (rec != null && rec.runId == run.runId)
            {
                if (ok) { rec.best = run.finalScore; rec.validated = true; }
                else { rec.best = 0; rec.runId = null; }
                await _x.Db.Put("campaignBest", pid + ":" + run.key, rec);
                await new Leaderboards(_x).Submit("campaign:c1_alltime", pid, await Total(pid), null, replace: true);
            }
            var prog = await _x.Db.Get<CampaignProgressDoc>("campaignProgress", pid) ?? new CampaignProgressDoc();
            var lvl = _x.Content.Level(run.key);
            if (ok && run.firstClearDeferred && lvl?.reward != null && !prog.rewarded.Contains(run.key))
            {
                prog.rewarded.Add(run.key);
                foreach (var rw in lvl.reward) await w.Grant(pid, rw, "first_clear_validated", run.runId);
            }
            if (!ok)
            {
                foreach (var id in run.provisionalLedgerIds)
                {
                    var e = await _x.Db.Get<LedgerEntry>("ledger", id); if (e == null) continue;
                    var c = new WalletService.Change { floorAtZero = true, note = "replay failed: provisional first-clear clawback" };
                    foreach (var (k, v) in e.delta) c.delta[k] = -v;
                    c.remove.AddRange(e.itemsAdded.Where(i => !i.StartsWith("powerup:")));
                    await w.Apply(pid, c, "replay_clawback", run.runId);
                }
                prog.rewarded.Remove(run.key);
                if (!(await Best(pid, run.key) is { best: > 0 })) prog.cleared.Remove(run.key);
            }
            await _x.Db.Put("campaignProgress", pid, prog);
            return 0;
        });
    }

    async Task<LevelBestDoc> Best(string pid, string lvl) => await _x.Db.Get<LevelBestDoc>("campaignBest", pid + ":" + lvl);
    public async Task<int> Total(string pid) => (await _x.Db.List<LevelBestDoc>("campaignBest", pid + ":")).Sum(b => b.best);

    /// <summary>Event stars (P4): attempt token + inputs; the server caps stars by what the shots used allow (StarRules).</summary>
    public async Task<SeasonState> EventStars(string pid, string eventLevelId, EventStarsRequest r)
    {
        var lvl = _x.Content.EventLevel(eventLevelId) ?? throw new ApiError(404, "UNKNOWN_LEVEL", "unknown event level");
        await Burn(pid, "event", lvl.id, r.attemptToken);
        r.simVersion ??= r.buildHash;
        if (r.simVersion != _x.Opt.SimVersion) throw new ApiError(422, "RESULT_REJECTED", "simVersion mismatch");
        int used = InputRules.Fires(r.inputs);
        if (used < 1 || used > lvl.shots) throw new ApiError(422, "RESULT_REJECTED", "input log does not match the level");
        var p = InputsPlausible(r.inputs, used); if (p != null) throw new ApiError(422, "RESULT_REJECTED", p);
        int maxStars = Crashverse.Meta.StarRules.ComputeInt(true, lvl.shots - used, lvl.shots);
        int stars = Math.Max(0, Math.Min(Math.Min(_x.Content.Season.eventStarCapPerLevel, maxStars), r.stars));
        var runId = Hash.NewId();
        await _x.Db.Put("runs", runId, new RunDoc { runId = runId, playerId = pid, mode = "event", key = lvl.id, simVersion = r.simVersion, eventStars = stars, finalScore = stars, clientScore = stars,
            issuedUnixMs = _x.NowMs, finalized = true, campaign = new CampaignRunReportEx { inputs = r.inputs, won = true, shotsLeft = lvl.shots - used } });
        return await new SeasonService(_x).ReportEventStars(pid, lvl.id, stars, runId);
    }
}
