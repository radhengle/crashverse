using Crashverse.Backend;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>
/// Server-written boards. Visibility (Security S1/B3): an entry with any un-replayed run is "pending". Pending entries never show
/// inside the top-N (they are queued for replay instead); outside the top-N they show with provisional=true. Shadow entries
/// (replay out of tolerance) live on the shadow board only. Guests are stored but not listed (SG-19/36).
/// </summary>
public sealed class Leaderboards
{
    readonly Ctx _x; public Leaderboards(Ctx x) { _x = x; }
    static string Id(string board, string pid) => board + "|" + pid;

    /// <summary>Upsert with BUG-16 semantics (timestamp moves only when the score rises). replace=true lets replace-boards go down.</summary>
    public Task Submit(string boardKey, string pid, int score, string runId, bool replace = false) => _x.Db.Atomic("board:" + Id(boardKey, pid), async () =>
    {
        var a = await _x.Db.Get<AccountDoc>("accounts", pid);
        var e = await _x.Db.Get<BoardEntryDoc>("boards", Id(boardKey, pid)) ?? new BoardEntryDoc { boardKey = boardKey, playerId = pid, score = -1 };
        if (e.score < 0) { e.score = score; e.reachedUnixMs = _x.NowMs; }
        else if (score > e.score) { e.score = score; e.reachedUnixMs = _x.NowMs; }
        else if (replace && score < e.score) e.score = score;
        e.guestAtSubmit = a?.isGuest ?? true;
        if (runId != null && !e.pendingRuns.Contains(runId)) e.pendingRuns.Add(runId);
        await _x.Db.Put("boards", Id(boardKey, pid), e);
        return 0;
    });

    public async Task<List<BoardEntryDoc>> Entries(string boardKey) => (await _x.Db.List<BoardEntryDoc>("boards", boardKey + "|"))
        .OrderByDescending(e => e.score).ThenBy(e => e.reachedUnixMs).ToList();

    /// <summary>Rank (1-based) of a player among non-shadow entries, or 0.</summary>
    public async Task<int> Position(string boardKey, string pid)
    {
        var l = (await Entries(boardKey)).Where(e => !e.shadow).ToList();
        return l.FindIndex(e => e.playerId == pid) + 1;
    }

    /// <summary>Replay finished for a run: drop it from pending lists; on failure move entries to the shadow board.</summary>
    public async Task OnReplay(RunDoc run, bool passed, int? correctedScore)
    {
        foreach (var key in run.boardKeys.Distinct())
            await _x.Db.Atomic("board:" + Id(key, run.playerId), async () =>
            {
                var e = await _x.Db.Get<BoardEntryDoc>("boards", Id(key, run.playerId));
                if (e == null) return 0;
                e.pendingRuns.Remove(run.runId);
                if (passed) { e.hasValidated = true; if (correctedScore.HasValue && key.StartsWith("titan:") && correctedScore < e.score) e.score = correctedScore.Value; }
                else
                {
                    e.shadow = true; // no auto-ban (S1): reviewed by a human
                    await _x.Db.Put("shadowBoards", Id(key, run.playerId), e);
                }
                await _x.Db.Put("boards", Id(key, run.playerId), e);
                return 0;
            });
    }

    /// <summary>SG-19 / SG-36: on link, entries on currently open boards (and the all-time campaign board) become listable.</summary>
    public async Task OnLinked(string pid)
    {
        var cal = new Calendar(_x);
        foreach (var e in (await _x.Db.List<BoardEntryDoc>("boards")).Where(e => e.playerId == pid))
            if (!cal.IsClosed(e.boardKey, _x.Now)) { e.guestAtSubmit = false; await _x.Db.Put("boards", Id(e.boardKey, pid), e); }
    }

    /// <summary>Null when the public Daily Titan board may be shown: flag on (strict "true"), a real replay worker configured,
    /// and 100% of the top-N replayed. Otherwise the reason. Pending top-N runs are queued as a side effect.</summary>
    public async Task<string> RankedBlockReason(List<BoardEntryDoc> entries, bool closed)
    {
        if (!_x.Opt.RankedPublicBoardEnabled) return "disabled";
        if (!_x.Replay.Configured) return "replay_worker_not_configured";
        string why = null; int pos = 0; var replay = new ReplayService(_x);
        foreach (var e in entries)
        {
            if (e.shadow) continue;
            var acc = await _x.Db.Get<AccountDoc>("accounts", e.playerId);
            if (acc == null || acc.status != "active" || acc.isGuest || (e.guestAtSubmit && closed)) continue;
            if (++pos > _x.Opt.TopN) break;
            if (e.pendingRuns.Count > 0 || !e.hasValidated) { why = "top_n_not_fully_replayed"; foreach (var r in e.pendingRuns) await replay.Enqueue(r, "top_n"); }
        }
        return why;
    }

    public async Task<LeaderboardPageEx> Page(string viewer, BoardType board, BoardPeriod period, bool friendsOnly)
    {
        var cal = new Calendar(_x); var now = _x.Now;
        var key = period == BoardPeriod.Current ? cal.CurrentKey(board, now) : cal.PreviousKey(board, now);
        bool closed = cal.IsClosed(key, now);
        HashSet<string> friends = friendsOnly ? (await new Friends(_x).Mutual(viewer)).ToHashSet() : null;
        var page = new LeaderboardPageEx { board = board, periodKey = key.Split(':', 2)[1], closed = closed, fetchedUnixUtc = Ctx.Unix(now) };
        var replay = new ReplayService(_x);
        var entries = await Entries(key);
        if (board == BoardType.DailyTitan) // B3: the public ranked board fails closed
        {
            var why = await RankedBlockReason(entries, closed);
            if (why != null)
            {
                page.publicBoard = false; page.publicBoardReason = why;
                var mine = entries.FirstOrDefault(e => e.playerId == viewer && !e.shadow);
                if (mine != null)
                {
                    var acc = await _x.Db.Get<AccountDoc>("accounts", viewer);
                    page.me = new LeaderboardEntryEx { rank = 0, playerId = viewer, name = acc?.displayName, score = mine.score, submittedUnixMs = mine.reachedUnixMs,
                        submittedTicks = DateTimeOffset.FromUnixTimeMilliseconds(mine.reachedUnixMs).UtcDateTime.Ticks, isMe = true, provisional = mine.pendingRuns.Count > 0 || !mine.hasValidated };
                }
                return page; // no other players, no ranks
            }
        }
        int position = 0, rank = 0;
        foreach (var e in entries)
        {
            if (e.shadow) continue;
            var acc = await _x.Db.Get<AccountDoc>("accounts", e.playerId);
            if (acc == null || acc.status != "active") continue;
            bool isMe = e.playerId == viewer;
            bool hiddenGuest = acc.isGuest || (e.guestAtSubmit && closed);
            if (hiddenGuest) { if (isMe) page.meIsGuestHidden = true; continue; }
            position++;
            bool pending = e.pendingRuns.Count > 0 || !e.hasValidated;
            if (pending)
            {
                if (position <= _x.Opt.TopN) foreach (var r in e.pendingRuns) await replay.Enqueue(r, "top_n");
                if (!isMe) continue; // provisional entries are never shown to others, at any rank (PR #1 review H2)
            }
            if (friends != null && !isMe && !friends.Contains(e.playerId)) continue;
            rank++;
            var le = new LeaderboardEntryEx
            {
                rank = rank, playerId = e.playerId, name = isMe ? acc.displayName : AuthService.PublicName(acc), score = e.score, submittedUnixMs = e.reachedUnixMs,
                submittedTicks = DateTimeOffset.FromUnixTimeMilliseconds(e.reachedUnixMs).UtcDateTime.Ticks,
                isFriend = friends?.Contains(e.playerId) ?? false, isMe = isMe, provisional = pending
            };
            if (isMe) page.me = le;
            if (rank <= 100) page.top.Add(le);
        }
        page.totalEntries = rank;
        return page;
    }
}

/// <summary>
/// Replay queue + tolerance comparison (S1). The worker is an interface; v0 uses StubReplayWorker (always "not available"),
/// so queued runs stay pending. Out-of-tolerance → shadow board + audit, never an automatic ban. Lower score counts.
/// </summary>
public sealed class ReplayService
{
    readonly Ctx _x; public ReplayService(Ctx x) { _x = x; }

    public Task Enqueue(string runId, string reason) => _x.Db.Atomic("run:" + runId, async () =>
    {
        var r = await _x.Db.Get<RunDoc>("runs", runId);
        if (r == null || r.replayState != "none") return 0;
        r.replayState = "queued"; await _x.Db.Put("runs", runId, r);
        await _x.Db.Put("replayQueue", runId, new IdentityDoc { key = runId, playerId = reason });
        return 0;
    });

    /// <summary>S1 sampling: top-N, Titan top-10%, p99 (handled by caller), random sample, and 100% for high-risk accounts.</summary>
    public async Task<bool> ShouldReplay(RunDoc run, bool topCandidate)
    {
        var a = await _x.Db.Get<AccountDoc>("accounts", run.playerId);
        return topCandidate || (a != null && (a.riskScore >= _x.Opt.HighRiskScore || a.refundAbuse)) || _x.Sampler.Next() < _x.Opt.RandomSampleRate;
    }

    public bool WithinTolerance(RunDoc run, ReplayResult rr, out string why)
    {
        var o = _x.Opt; why = null;
        if (run.mode == "titan")
        {
            if (rr.killed != run.lastKilled) { why = "kill flag mismatch"; return false; }
            if (Math.Abs(rr.damageScore - run.lastDamage) > Math.Max(o.TolPerShotAbs, o.TolFinalPct * Math.Max(1, rr.damageScore))) { why = "final damage outside tolerance"; return false; }
            return true;
        }
        var c = run.campaign;
        if (c != null)
        {
            if (rr.won != c.won) { why = "win mismatch"; return false; }
            if (rr.shotsLeft != c.shotsLeft) { why = "shot count mismatch"; return false; }
            if (Math.Abs(rr.destroyedRatio - c.destroyedRatio) > o.TolRatio) { why = "destroyed ratio outside tolerance"; return false; }
            if (Math.Abs(rr.bestCombo - c.bestCombo) > o.TolCombo) { why = "combo outside tolerance"; return false; }
        }
        return true;
    }

    /// <summary>Runs queued replays. Returns (processed, stillPending).</summary>
    public async Task<(int done, int pending)> Process()
    {
        int done = 0, pending = 0;
        foreach (var q in await _x.Db.List<IdentityDoc>("replayQueue"))
        {
            var run = await _x.Db.Get<RunDoc>("runs", q.key);
            if (run == null) { await _x.Db.Delete("replayQueue", q.key); continue; }
            var rr = await _x.Replay.Replay(new RunRecordView
            {
                physicsHz = _x.Opt.PhysicsHz, runId = run.runId, mode = run.mode, levelOrDay = run.key, seed = run.seed, simVersion = run.simVersion, clientScore = run.finalScore,
                inputs = run.campaign?.inputs ?? run.checkpoints.SelectMany(c => c.inputs ?? new List<InputEvent>()).ToList()
            });
            if (rr == null) { pending++; continue; }
            if (run.serverSummary)
            {
                await new CampaignService(_x).ApplyServerSummary(run, rr);
                await _x.Db.Delete("replayQueue", run.runId); done++; continue;
            }
            bool ok = WithinTolerance(run, rr, out var why);
            int corrected = run.finalScore;
            if (ok)
            {
                corrected = run.mode switch
                {
                    "titan" => Math.Min(run.finalScore, Rules.TitanScore(Math.Min(rr.damageScore, run.lastDamage), run.lastShotsLeft, run.lastKilled && rr.killed, _x.Content.Titan)),
                    "campaign" => Math.Min(run.finalScore, Crashverse.Meta.CampaignScore.Compute(rr.won, Math.Min(rr.destroyedRatio, run.campaign.destroyedRatio),
                        run.campaign.shotsLeft, Math.Min(rr.bestCombo, run.campaign.bestCombo), rr.mastery && run.campaign.mastery)),
                    _ => run.finalScore
                };
            }
            run.replayState = ok ? "passed" : "failed";
            run.replay = new ReplayResult_ { damageScore = rr.damageScore, killed = rr.killed, destroyedRatio = rr.destroyedRatio, bestCombo = rr.bestCombo, mastery = rr.mastery, won = rr.won, shotsLeft = rr.shotsLeft, score = corrected };
            int before = run.finalScore; run.finalScore = corrected;
            await _x.Db.Put("runs", run.runId, run);
            await _x.Db.Delete("replayQueue", run.runId);
            if (run.mode == "campaign") await new CampaignService(_x).OnReplay(run, ok, before);
            await new Leaderboards(_x).OnReplay(run, ok, corrected);
            if (!ok) await _x.Audit("replay", "replay_mismatch_shadowed", run.playerId, $"{run.runId}: {why}");
            done++;
        }
        return (done, pending);
    }
}

/// <summary>Scoring rules: delegates to the shared client source (Backend/ServerContracts.cs TitanScoring), compiled into the server.</summary>
public static class Rules
{
    public static int TitanScore(int damageScore, int shotsLeft, bool killed, Crashverse.Data.TitanTemplates t) => TitanScoring.Score(damageScore, shotsLeft, killed, t);
}
