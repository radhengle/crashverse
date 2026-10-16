using System.Text.Json;
using System.Text.Json.Nodes;
using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>Daily Titan config (H3), ranked runs (SEC-01, C1, H2), integrity gate (S2), grace window (SG-10/22), top-10% (SG-12).</summary>
public sealed class TitanService
{
    readonly Ctx _x; readonly Calendar _cal;
    public TitanService(Ctx x) { _x = x; _cal = new Calendar(x); }
    TitanTemplates T => _x.Content.Titan;

    static int Mod(int a, int n) => ((a % n) + n) % n;
    public static DailyTitanConfig Build(TitanTemplates t, DateTime serverUtc)
    {
        int day = UtcDay.Index(serverUtc);
        var arena = t.arenas[Mod(day, t.arenas.Length)];
        var rule = t.rules[Mod(day * 5 + day / t.arenas.Length, t.rules.Length)];
        return new DailyTitanConfig
        {
            dayKey = UtcDay.Key(serverUtc), archetype = t.archetype, name = t.name, arenaId = arena.id, arenaName = arena.name,
            layout = arena.layout, ruleId = rule.id, ruleText = rule.text, mod = rule.mod,
            shots = Math.Max(1, t.baseShots + rule.mod.shotDelta), mastery = t.mastery, completionRewards = t.completionRewards,
            topPercent = t.topPercent, topPercentBonusShards = t.topPercentBonusShards, maxDamageScore = t.maxDamageScore,
            perShotLeftBonus = t.perShotLeftBonus, resetsAtUnixUtc = Ctx.Unix(UtcDay.NextReset(serverUtc)),
            hintTarget = arena.hintTarget, hintYaw = arena.hintYaw, hintPitch = arena.hintPitch
        };
    }

    /// <summary>Config is built on the server; the only override is a server-side, audited titanDays document (H3).</summary>
    public async Task<DailyTitanConfigEx> Today()
    {
        var now = _x.Now; var key = UtcDay.Key(now);
        var cfg = (await _x.Db.Get<TitanDayDoc>("titanDays", key))?.overrideConfig ?? Build(T, now);
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { IncludeFields = true });
        var ex = JsonSerializer.Deserialize<DailyTitanConfigEx>(json, new JsonSerializerOptions { IncludeFields = true });
        ex.dayKey = key; ex.resetsAtUnixUtc = Ctx.Unix(UtcDay.NextReset(now));
        ex.configHash = Hash.Sha256Hex(JsonSerializer.Serialize((DailyTitanConfig)ex, new JsonSerializerOptions { IncludeFields = true }))[..16];
        return ex;
    }

    public async Task SetOverride(string admin, string dayKey, DailyTitanConfig cfg)
    {
        await _x.Db.Put("titanDays", dayKey, new TitanDayDoc { dayKey = dayKey, overrideConfig = cfg, overrideBy = admin });
        await _x.Audit(admin, "titan_override", null, dayKey);
    }

    string StatusId(string pid, string day) => pid + ":" + day;
    public async Task<RankedStatus> Status(string pid)
    {
        var day = UtcDay.Key(_x.Now);
        var s = await _x.Db.Get<RankedStatusDoc>("rankedStatus", StatusId(pid, day)) ?? new RankedStatusDoc { dayKey = day };
        return new RankedStatus { dayKey = s.dayKey, attemptUsed = s.attemptUsed, submitted = s.submitted, score = s.score, status = s.status };
    }

    /// <summary>S2 integrity policy. Returns null when the verdict meets the ranked bar, else the reason.</summary>
    public async Task<string> CheckIntegrity(string token, string expectedRequestHash, bool ranked, string pid)
    {
        var v = await _x.Integrity.Decode(token);
        string fail = null;
        if (v == null) fail = "integrity token invalid or unavailable";
        else if (v.requestHash != expectedRequestHash) fail = "request hash mismatch";
        else if (Math.Abs(_x.NowMs - v.timestampMillis) > _x.Opt.IntegrityMaxAgeSeconds * 1000L) fail = "integrity verdict too old";
        else if (v.appRecognitionVerdict != "PLAY_RECOGNIZED") fail = "app not recognised by Play";
        else if (v.packageName != _x.Opt.PackageName || v.requestPackageName != _x.Opt.PackageName) fail = "package mismatch";
        else if (!v.certificateSha256Digest.Any(d => _x.Opt.CertDigests.Contains(d))) fail = "certificate mismatch";
        else if (!v.deviceRecognitionVerdict.Contains("MEETS_DEVICE_INTEGRITY")) fail = "device integrity not met";
        if (v != null && v.appLicensingVerdict != "LICENSED") await Risk(pid, 5, "unlicensed"); // logged + risk only (S2)
        if (fail != null)
        {
            await _x.Audit(pid, ranked ? "integrity_fail_ranked" : "integrity_fail_soft", pid, fail);
            await Risk(pid, ranked ? 10 : 5, fail);
            if (_x.Opt.IntegrityMode == "logOnly" || !ranked) return null; // log-only mode, or soft-fail for unranked/purchases
        }
        return fail;
    }

    async Task Risk(string pid, int add, string why)
    {
        if (pid == null) return;
        await _x.Db.Atomic("account:" + pid, async () =>
        {
            var a = await _x.Db.Get<AccountDoc>("accounts", pid); if (a == null) return 0;
            a.riskScore += add; await _x.Db.Put("accounts", pid, a); return 0;
        });
    }

    public static string StartRequestHash(string pid, string nonce) => Hash.Sha256B64Url("titan_start|" + pid + "|" + nonce);

    /// <summary>One ranked attempt per UTC day, consumed at start (transactional). Failed integrity → practice run, attempt NOT consumed.</summary>
    public async Task<RankedTicketEx> Start(string pid, string integrityToken, string nonce)
    {
        var now = _x.Now; var day = UtcDay.Key(now);
        var cfg = await Today();
        var fail = await CheckIntegrity(integrityToken, StartRequestHash(pid, nonce), ranked: true, pid);
        bool practice = fail != null;
        if (!practice)
        {
            await _x.Db.Atomic("ranked:" + StatusId(pid, day), async () =>
            {
                var st = await _x.Db.Get<RankedStatusDoc>("rankedStatus", StatusId(pid, day));
                if (st != null && st.attemptUsed) throw new ApiError(409, "ATTEMPT_USED", "Ranked attempt already used today");
                await _x.Db.Put("rankedStatus", StatusId(pid, day), new RankedStatusDoc { dayKey = day, attemptUsed = true, status = "in_progress" });
                return 0;
            });
        }
        var runId = Hash.NewId(); long seed = BitConverter.ToInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
        var run = new RunDoc
        {
            runId = runId, playerId = pid, mode = "titan", key = day, seed = seed, configHash = cfg.configHash, simVersion = _x.Opt.SimVersion,
            shotBudget = cfg.shots, issuedUnixMs = _x.NowMs, lastShotsLeft = cfg.shots, lastShotServerUnixMs = _x.NowMs, practice = practice, practiceReason = fail
        };
        await _x.Db.Put("runs", runId, run);
        var token = _x.Tokens.Issue("run", new JsonObject { ["rid"] = runId, ["sub"] = pid, ["day"] = day, ["cfg"] = cfg.configHash, ["seed"] = seed, ["prac"] = practice },
            TimeSpan.FromHours(_x.Opt.RunTokenTtlHours));
        return new RankedTicketEx
        {
            token = token, dayKey = day, issuedUnixUtc = Ctx.Unix(now), shotBudget = cfg.shots, seed = seed, configHash = cfg.configHash,
            physicsHz = _x.Opt.PhysicsHz, simVersion = _x.Opt.SimVersion, practice = practice, practiceReason = fail
        };
    }

    RunDoc RunFromToken(string pid, string token, out string err)
    {
        var c = _x.Tokens.Verify("run", token, out err);
        if (c == null) return null;
        if ((string)c["sub"] != pid) { err = "run token belongs to another player"; return null; }
        return _x.Db.Get<RunDoc>("runs", (string)c["rid"]).Result;
    }

    static string InputsHash(ShotInput i) => i == null ? "-" : Hash.Sha256Hex($"{i.yaw}|{i.pitch}|{i.power}|{i.powerUpId}|{i.fireFrame}|{i.settleFrames}")[..16];
    static string Canon(string runId, RankedCheckpoint c, ShotInput i) => $"{runId}|{c.shotIndex}|{c.damageScore}|{c.shotsLeft}|{(c.killed ? 1 : 0)}|{InputsHash(i)}";

    static string InputProblem(ShotInput i)
    {
        if (i == null) return "inputs missing";
        if (i.power < 0 || i.power > 1000 || Math.Abs(i.pitch) > 90_000 || Math.Abs(i.yaw) > 360_000 || i.settleFrames < 0 || i.fireFrame < 0) return "inputs out of range";
        return null;
    }

    string CheckStep(RunDoc r, int prevIndex, int prevDamage, bool prevKilled, RankedCheckpoint c)
    {
        if (prevKilled) return "run already finished";
        if (c.shotIndex != prevIndex + 1) return "shot out of order";
        if (c.shotsLeft != r.shotBudget - c.shotIndex || c.shotsLeft < 0) return "shot count mismatch";
        if (c.damageScore < prevDamage || c.damageScore > T.maxDamageScore) return "damage out of range";
        if (c.damageScore - prevDamage > T.offlineShotDamageCap) return "implausible damage for one shot"; // per-shot cap
        return null;
    }

    public Task<RankedCheckpointEx> Checkpoint(string pid, RankedCheckpointEx c)
    {
        var run0 = RunFromToken(pid, c?.token, out var err);
        if (run0 == null) throw new ApiError(404, "UNKNOWN_RUN", err ?? "Unknown run token");
        return _x.Db.Atomic("run:" + run0.runId, async () =>
        {
            var run = await _x.Db.Get<RunDoc>("runs", run0.runId);
            if (run.finalized) throw new ApiError(409, "RUN_CLOSED", "Run already submitted");
            var last = run.checkpoints.LastOrDefault();
            if (last != null && c.shotIndex == run.lastShotIndex && c.damageScore == run.lastDamage && c.killed == run.lastKilled && c.shotsLeft == run.lastShotsLeft
                && InputsHash(last.inputs) == InputsHash(c.inputs))
            { c.receipt = _x.Receipts.Sign(run.runId, Canon(run.runId, c, c.inputs)); return c; } // idempotent retry
            var e = CheckStep(run, run.lastShotIndex, run.lastDamage, run.lastKilled, c);
            if (e == null && _x.NowMs - run.lastShotServerUnixMs < _x.Opt.MinShotIntervalMs) e = "shots too close together"; // H2: server-measured
            if (e != null) throw new ApiError(422, "CHECKPOINT_REJECTED", "Checkpoint rejected: " + e);
            var ip = InputProblem(c.inputs);
            if (ip != null && !run.practice) { run.practice = true; run.practiceReason = ip; } // S1: no inputs → practice
            run.lastShotIndex = c.shotIndex; run.lastDamage = c.damageScore; run.lastShotsLeft = c.shotsLeft; run.lastKilled = c.killed; run.lastShotServerUnixMs = _x.NowMs;
            run.checkpoints.Add(new CheckpointRec { shotIndex = c.shotIndex, damageScore = c.damageScore, shotsLeft = c.shotsLeft, killed = c.killed, inputs = c.inputs, serverUnixMs = _x.NowMs });
            await _x.Db.Put("runs", run.runId, run);
            c.receipt = _x.Receipts.Sign(run.runId, Canon(run.runId, c, c.inputs));
            return c;
        });
    }

    /// <summary>
    /// SEC-01 / SG-11 / SG-13: the server's own run state is the source of truth. Signed checkpoints are verified (kid-aware);
    /// an unsigned offline tail is accepted step-by-step under the same caps, only inside the grace window.
    /// </summary>
    public Task<SubmitResponse> Finalize(string pid, RankedJournalEx j)
    {
        var run0 = RunFromToken(pid, j?.token, out var err);
        if (run0 == null) return Task.FromResult(new SubmitResponse { status = SubmitStatus.RejectedInvalid, message = err ?? "Unknown run token" });
        return _x.Db.Atomic("run:" + run0.runId, async () =>
        {
            var run = await _x.Db.Get<RunDoc>("runs", run0.runId);
            if (run.finalized) return new SubmitResponse { status = SubmitStatus.RejectedDuplicate, message = "Already submitted", finalScore = run.finalScore };
            int idx = run.lastShotIndex, dmg = run.lastDamage, left = run.lastShotsLeft; bool killed = run.lastKilled;
            bool tampered = false, usedTail = false;
            var deadline = _cal.RankedDeadline(run.key);
            bool late = _x.Now > deadline; // inclusive at 00:15:00.000
            foreach (var c in (j.checkpoints ?? new()).Where(c => c != null).OrderBy(c => c.shotIndex))
            {
                if (c.token != j.token) { tampered = true; continue; }
                if (!string.IsNullOrEmpty(c.receipt))
                {
                    if (!_x.Receipts.Verify(run.runId, Canon(run.runId, c, c.inputs), c.receipt)) tampered = true;
                    continue;
                }
                if (c.shotIndex <= idx) { if (c.shotIndex < idx || c.damageScore != dmg || c.killed != killed) tampered = true; continue; }
                if (tampered || late) break;
                if (CheckStep(run, idx, dmg, killed, c) != null || InputProblem(c.inputs) != null) { tampered = true; break; }
                idx = c.shotIndex; dmg = c.damageScore; left = c.shotsLeft; killed = c.killed; usedTail = true;
                run.checkpoints.Add(new CheckpointRec { shotIndex = c.shotIndex, damageScore = c.damageScore, shotsLeft = c.shotsLeft, killed = c.killed, inputs = c.inputs, serverUnixMs = _x.NowMs, offline = true });
            }
            run.lastShotIndex = idx; run.lastDamage = dmg; run.lastShotsLeft = left; run.lastKilled = killed;
            if (j.simVersion != run.simVersion && !run.practice) { run.practice = true; run.practiceReason = "simVersion mismatch"; }
            if (tampered) await _x.Audit(pid, "journal_tampered", pid, run.runId);
            bool final = j.final || killed || left == 0;
            run.finalized = true;
            var stId = StatusId(pid, run.key);
            var st = await _x.Db.Get<RankedStatusDoc>("rankedStatus", stId) ?? new RankedStatusDoc { dayKey = run.key };
            SubmitResponse resp;
            if (run.practice)
            {
                await _x.Db.Put("runs", run.runId, run);
                return new SubmitResponse { status = SubmitStatus.RefundedAsPractice, message = "Counted as practice: " + run.practiceReason };
            }
            if (late)
            {
                st.status = "refunded_practice"; await _x.Db.Put("rankedStatus", stId, st); await _x.Db.Put("runs", run.runId, run);
                return new SubmitResponse { status = SubmitStatus.RefundedAsPractice, message = "Submitted after the grace window; counted as practice" };
            }
            int score = Math.Min(Rules.TitanScore(dmg, left, killed, T), T.maxDamageScore + T.perShotLeftBonus * run.shotBudget);
            run.clientScore = score; run.finalScore = score;
            var boardKey = "titan:" + run.key; run.boardKeys.Add(boardKey);
            await _x.Db.Put("runs", run.runId, run);
            var lb = new Leaderboards(_x);
            await lb.Submit(boardKey, pid, score, run.runId);
            int pos = await lb.Position(boardKey, pid);
            int count = (await lb.Entries(boardKey)).Count(e => !e.shadow);
            bool topCandidate = pos <= _x.Opt.TopN || pos <= Math.Ceiling(count * T.topPercent);
            var rs = new ReplayService(_x);
            if (await rs.ShouldReplay(run, topCandidate)) await rs.Enqueue(run.runId, topCandidate ? "top" : "sample");
            st.submitted = true; st.score = score; st.status = final ? "submitted" : "submitted_partial"; st.attemptUsed = true;
            await _x.Db.Put("rankedStatus", stId, st);
            if (killed) await new SeasonService(_x).OnTitanClear(pid, run.key, run.runId);
            resp = new SubmitResponse { status = SubmitStatus.Accepted, finalScore = score, message = "Ranked score accepted (pending validation)" };
            if (tampered) resp.message += " (journal failed verification; server-held run state used)";
            else if (usedTail) resp.message += " (offline shots accepted after plausibility checks)";
            return resp;
        });
    }

    /// <summary>SG-12 + S1: after close + grace, every top-10% run must be replayed before its bonus is paid. Idempotent.</summary>
    public async Task<string> CloseDay(string dayKey)
    {
        if (_x.Now <= _cal.RankedDeadline(dayKey)) return "open";
        var lb = new Leaderboards(_x);
        var entries = (await lb.Entries("titan:" + dayKey)).Where(e => !e.shadow).ToList();
        int cut = (int)Math.Ceiling(entries.Count * T.topPercent);
        bool deferred = false;
        var rs = new ReplayService(_x);
        foreach (var e in entries.Take(cut))
        {
            if (e.pendingRuns.Count > 0) { foreach (var r in e.pendingRuns) await rs.Enqueue(r, "top10"); deferred = true; continue; }
            await _x.Db.TryCreate("inbox", e.playerId + ":titan_top_" + dayKey, new InboxDoc
            {
                id = "titan_top_" + dayKey, playerId = e.playerId, source = $"Daily Titan top 10% ({dayKey})",
                reward = new Reward { type = "shard", amount = T.topPercentBonusShards }, expiresUnixUtc = Ctx.Unix(_x.Now.AddDays(30))
            });
        }
        return deferred ? "deferred" : "done";
    }
}
