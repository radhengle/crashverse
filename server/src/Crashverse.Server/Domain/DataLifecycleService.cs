using System.Text.Json;
using System.Text.Json.Nodes;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>
/// S3 / B6 / Deletion_AgeGate_Requirements C2 + D1–D7: deletion purge, retention TTLs, guest inactivity and the web deletion request.
/// All jobs are idempotent and run from Cloud Scheduler through /internal/jobs/* (OIDC-authenticated).
/// </summary>
public sealed class DataLifecycleService
{
    readonly Ctx _x; public DataLifecycleService(Ctx x) { _x = x; }
    ServerOptions O => _x.Opt;

    public sealed class PurgeReport { public int purged; public int overdueAlerts; public Dictionary<string, int> counts = new(); }

    /// <summary>D1: purges every account whose grace period has ended. Completes well inside 30 days (7-day grace + daily job).
    /// Alerts (audit "deletion_overdue_alert") for anything pending longer than DeletionAlertDays.</summary>
    public async Task<PurgeReport> PurgeDue()
    {
        var rep = new PurgeReport();
        foreach (var a in await _x.Db.List<AccountDoc>("accounts"))
        {
            if (a.status != "deleted_pending" || a.purgeAtUnixMs == 0) continue;
            long requested = a.purgeAtUnixMs - (long)TimeSpan.FromDays(O.DeletionGraceDays).TotalMilliseconds;
            if (_x.NowMs - requested > TimeSpan.FromDays(O.DeletionAlertDays).TotalMilliseconds) { rep.overdueAlerts++; await _x.Audit("lifecycle", "deletion_overdue_alert", a.playerId, ""); }
            if (a.purgeAtUnixMs > _x.NowMs) continue;
            foreach (var (k, v) in await PurgeAccount(a.playerId)) rep.counts[k] = rep.counts.GetValueOrDefault(k) + v;
            rep.purged++;
        }
        return rep;
    }

    /// <summary>Ownership, never substring: the doc id is the pid, starts with "pid:" (per-player sub-keys), is a board entry
    /// "boardKey|pid", or its top-level playerId/ownerId field equals the pid. Other players' docs that merely mention the pid
    /// (e.g. a friends list) are not owned and are never deleted.</summary>
    public static bool Owns(string col, string id, string json, string pid)
    {
        if (string.IsNullOrEmpty(pid)) return false;
        if (id == pid || id.StartsWith(pid + ":", StringComparison.Ordinal)) return true;
        if ((col == "boards" || col == "shadowBoards") && id.EndsWith("|" + pid, StringComparison.Ordinal)) return true;
        if (col == "replayQueue") return false; // its playerId field holds the queue reason; handled by run id
        try
        {
            if (JsonNode.Parse(json) is JsonObject o)
                foreach (var f in new[] { "playerId", "ownerId" })
                    if (o.TryGetPropertyValue(f, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var sv) && sv == pid) return true;
        }
        catch (JsonException) { }
        return false;
    }

    /// <summary>Top-100 by public rank: shadow entries, inactive accounts and hidden guests don't take a slot (the subject's own
    /// deleted_pending status is treated as active).</summary>
    async Task<bool> InPublicTop100(string boardKey, string pid)
    {
        var cal = new Calendar(_x); bool closed = cal.IsClosed(boardKey, _x.Now); int pos = 0;
        foreach (var e in await new Leaderboards(_x).Entries(boardKey))
        {
            if (e.shadow) continue;
            var acc = await _x.Db.Get<AccountDoc>("accounts", e.playerId);
            if (acc == null || (acc.status != "active" && e.playerId != pid)) continue;
            if (acc.isGuest || (e.guestAtSubmit && closed)) continue; // hidden
            if (++pos > 100) return false;
            if (e.playerId == pid) return true;
        }
        return false;
    }

    /// <summary>D2–D7 for one player. Idempotent: a second run finds nothing to do.</summary>
    public async Task<Dictionary<string, int>> PurgeAccount(string pid)
    {
        var counts = new Dictionary<string, int>();
        void Count(string c) => counts[c] = counts.GetValueOrDefault(c) + 1;
        var acc = await _x.Db.Get<AccountDoc>("accounts", pid);
        var tomb = "tomb_" + Hash.NewId();
        var pseudo = _x.Pseudo(pid);

        // D3: top-100 runs kept for anti-cheat are detached (tombstone id, no name); the board entries themselves are deleted.
        var keepRuns = new HashSet<string>();
        foreach (var e in (await _x.Db.List<BoardEntryDoc>("boards")).Where(e => e.playerId == pid && !e.shadow))
        {
            if (!await InPublicTop100(e.boardKey, pid)) continue;
            foreach (var r in (await _x.Db.List<RunDoc>("runs")).Where(r => r.playerId == pid && r.boardKeys.Contains(e.boardKey))) keepRuns.Add(r.runId);
        }

        // D5: non-purchase ledger rows are folded into anonymous daily totals, then deleted.
        foreach (var le in await _x.Db.List<LedgerEntry>("ledger", pid + ":"))
        {
            var day = DateTimeOffset.FromUnixTimeMilliseconds(le.ts).UtcDateTime.ToString("yyyy-MM-dd");
            await _x.Db.Atomic("ledgerDaily:" + day, async () =>
            {
                var agg = await _x.Db.Get<Dictionary<string, int>>("ledgerDaily", day) ?? new Dictionary<string, int>();
                foreach (var (k, v) in le.delta) agg[$"{le.reason}:{k}"] = agg.GetValueOrDefault($"{le.reason}:{k}") + v;
                await _x.Db.Put("ledgerDaily", day, agg); return 0;
            });
        }

        var myRuns = (await _x.Db.List<RunDoc>("runs")).Where(r => r.playerId == pid).Select(r => r.runId).ToHashSet();
        foreach (var col in Collections.PlayerData)
        {
            foreach (var (id, json) in await _x.Db.ListRaw(col))
            {
                if (col == "audit")
                {
                    // Audit rows hold only the pseudonym; strip any detail that names the player (D6: no PII in audit).
                    // Matched by pid only: display names are shared ("Guest Wrecker") and would redact other players' rows.
                    var ae = JsonSerializer.Deserialize<AuditEntry>(json, new JsonSerializerOptions { IncludeFields = true });
                    if (ae.detail == null || !ae.detail.Contains(pid, StringComparison.Ordinal)) continue;
                    await _x.Db.LifecyclePurge(col, id);
                    ae.detail = "[redacted: account deleted]";
                    await _x.Db.TryCreate(col, id, ae); Count(col); continue;
                }
                if (col == "friends" && !Owns(col, id, json, pid))
                {
                    // Another player's friends doc: drop the deleted player from it, never delete it.
                    var f = JsonSerializer.Deserialize<FriendDoc>(json, new JsonSerializerOptions { IncludeFields = true });
                    if (f?.added == null || f.added.RemoveAll(o => o == pid) == 0) continue;
                    await _x.Db.Put(col, id, f); Count("friends_unlinked"); continue;
                }
                if (col == "replayQueue" ? !myRuns.Contains(id) : !Owns(col, id, json, pid)) continue;
                if (col == "purchases")
                {
                    // D4: kept pseudonymised for tax; deleted by the retention job after TaxRetentionDays (NEEDS LEGAL REVIEW).
                    var p = JsonSerializer.Deserialize<PurchaseDoc>(json, new JsonSerializerOptions { IncludeFields = true });
                    p.playerId = pseudo; p.retainUntilUnixMs = _x.NowMs + (long)TimeSpan.FromDays(O.TaxRetentionDays).TotalMilliseconds;
                    await _x.Db.Put(col, id, p); Count("purchases_pseudonymised"); continue;
                }
                if (col == "runs" && keepRuns.Contains(id))
                {
                    var r = JsonSerializer.Deserialize<RunDoc>(json, new JsonSerializerOptions { IncludeFields = true });
                    r.playerId = tomb; r.campaign = r.campaign == null ? null : new CampaignRunReportEx { levelId = r.campaign.levelId, won = r.campaign.won, destroyedRatio = r.campaign.destroyedRatio,
                        shotsLeft = r.campaign.shotsLeft, bestCombo = r.campaign.bestCombo, inputs = r.campaign.inputs, simVersion = r.campaign.simVersion };
                    r.provisionalLedgerIds.Clear(); r.boardKey = null; r.boardKeys.Clear();
                    await _x.Db.Put(col, id, r); Count("runs_tombstoned"); continue;
                }
                await _x.Db.LifecyclePurge(col, id); Count(col);
            }
        }
        if (counts.Count > 0) await _x.Audit("lifecycle", "account_deleted", null, string.Join(",", counts.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")) + " subject=" + pseudo);
        return counts;
    }

    /// <summary>A used age token's jti record is kept one hour past the token's expiry (clock-skew margin), so single use stays enforced
    /// until it is purged.</summary>
    public const long AgeTokenUsePurgeGraceMs = 60 * 60 * 1000;

    public sealed class RetentionReport { public int inputLogsCleared; public int purchasesExpired; public int ageTokenUsesExpired; }

    /// <summary>S3: input logs are kept 90 days (InputLogRetentionDays), then stripped from the run. Pseudonymised purchase rows are
    /// deleted once their tax retention ends.</summary>
    public async Task<RetentionReport> Retention()
    {
        var rep = new RetentionReport();
        long cutoff = _x.NowMs - (long)TimeSpan.FromDays(O.InputLogRetentionDays).TotalMilliseconds;
        foreach (var r in await _x.Db.List<RunDoc>("runs"))
        {
            if (r.issuedUnixMs >= cutoff || r.inputsPurged) continue;
            await _x.Db.Atomic("run:" + r.runId, async () =>
            {
                var run = await _x.Db.Get<RunDoc>("runs", r.runId); if (run == null) return 0;
                if (run.campaign != null) run.campaign.inputs = new();
                foreach (var c in run.checkpoints) c.inputs = null;
                run.inputsPurged = true; await _x.Db.Put("runs", run.runId, run); rep.inputLogsCleared++; return 0;
            });
        }
        foreach (var (id, json) in await _x.Db.ListRaw("purchases"))
        {
            var p = JsonSerializer.Deserialize<PurchaseDoc>(json, new JsonSerializerOptions { IncludeFields = true });
            if (p.retainUntilUnixMs > 0 && p.retainUntilUnixMs <= _x.NowMs) { await _x.Db.LifecyclePurge("purchases", id); rep.purchasesExpired++; }
        }
        // Low-2: a used age token past its expiry can never be replayed (VerifyAgeToken rejects expired tokens), so its jti record goes.
        foreach (var (id, json) in await _x.Db.ListRaw("ageTokenUses"))
        {
            var u = JsonSerializer.Deserialize<IdentityDoc>(json, new JsonSerializerOptions { IncludeFields = true });
            long exp = u.expiresUnixMs > 0 ? u.expiresUnixMs : u.linkedUnixMs;
            if (exp > 0 && exp + AgeTokenUsePurgeGraceMs <= _x.NowMs) { await _x.Db.LifecyclePurge("ageTokenUses", id); rep.ageTokenUsesExpired++; }
        }
        return rep;
    }

    public sealed class InactivityReport { public int warned; public int scheduled; }

    /// <summary>C2: guests inactive for 335 days get deletionWarningAt (shown in-app with a keep option); at 365 days they enter the
    /// normal deletion job (purge on the next run). Any authenticated activity resets the clock.</summary>
    public async Task<InactivityReport> GuestInactivity()
    {
        var rep = new InactivityReport();
        foreach (var a0 in await _x.Db.List<AccountDoc>("accounts"))
        {
            if (!a0.isGuest || a0.status != "active") continue;
            await _x.Db.Atomic("account:" + a0.playerId, async () =>
            {
                var a = await _x.Db.Get<AccountDoc>("accounts", a0.playerId);
                long last = Math.Max(a.createdUnixMs, a.lastActiveUnixMs); double days = (_x.NowMs - last) / 86_400_000.0;
                if (days >= O.GuestInactivityDeleteDays)
                {
                    a.status = "deleted_pending"; a.purgeAtUnixMs = _x.NowMs; await _x.Db.Put("accounts", a.playerId, a);
                    await _x.Audit("lifecycle", "guest_inactive_delete", a.playerId, ""); rep.scheduled++;
                }
                else if (days >= O.GuestInactivityWarnDays && a.deletionWarningAtUnixMs == 0)
                {
                    a.deletionWarningAtUnixMs = _x.NowMs; await _x.Db.Put("accounts", a.playerId, a); rep.warned++;
                }
                return 0;
            });
        }
        return rep;
    }

    // ---------------- B6: web deletion request (no app needed) ----------------
    public sealed class WebReq { public string email; public string code; public string captchaToken; public LinkCredential credential; }
    sealed class WebDeletionDoc { public string emailHash; public string codeHash; public long expUnixMs; public int attempts; }

    static readonly object Generic = new { status = "received", message = "If an account exists for this email, a verification code has been sent." };

    public async Task<object> WebRequest(WebReq r, string ip)
    {
        await RequireCaptcha(r?.captchaToken, ip);
        var email = (r?.email ?? "").Trim().ToLowerInvariant();
        var cred = email.Length > 0 ? await _x.Db.Get<EmailCredDoc>("emailCreds", email) : null;
        if (cred != null && cred.verified)
        {
            var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            await _x.Db.Put("webDeletion", Hash.Sha256Hex(email), new WebDeletionDoc { emailHash = Hash.Sha256Hex(email), codeHash = Hash.Sha256Hex(code), expUnixMs = _x.NowMs + 15 * 60_000 });
            await _x.Email.Send(email, "Confirm CRASHVERSE account deletion", "Code: " + code);
            await _x.Audit("web", "web_deletion_code_sent", cred.playerId, "");
        }
        return Generic;
    }

    public async Task<object> WebConfirm(WebReq r)
    {
        var email = (r?.email ?? "").Trim().ToLowerInvariant(); var key = Hash.Sha256Hex(email);
        return await _x.Db.Atomic("webDeletion:" + key, async () =>
        {
            var d = await _x.Db.Get<WebDeletionDoc>("webDeletion", key);
            if (d == null || d.expUnixMs < _x.NowMs || d.attempts >= 5) throw new ApiError(400, "INVALID_CODE", "invalid or expired code");
            if (d.codeHash != Hash.Sha256Hex(r?.code ?? "")) { d.attempts++; await _x.Db.Put("webDeletion", key, d); throw new ApiError(400, "INVALID_CODE", "invalid or expired code"); }
            await _x.Db.Delete("webDeletion", key);
            var cred = await _x.Db.Get<EmailCredDoc>("emailCreds", email) ?? throw new ApiError(400, "INVALID_CODE", "invalid or expired code");
            return await Schedule(cred.playerId, "web_email");
        });
    }

    public async Task<object> WebProvider(WebReq r, string ip)
    {
        await RequireCaptcha(r?.captchaToken, ip);
        var c = r?.credential ?? throw new ApiError(400, "BAD_REQUEST", "credential required");
        var prov = Providers.Norm(c.provider);
        if (prov is not ("google" or "facebook" or "x")) throw new ApiError(400, "BAD_PROVIDER", "use email verification or a social login");
        var id = await new AuthService(_x).VerifyProvider(null, prov, c);
        var doc = await _x.Db.Get<IdentityDoc>("identities", $"{prov}:{id.subject}") ?? throw new ApiError(404, "NOT_FOUND", "no account for this login");
        return await Schedule(doc.playerId, "web_" + prov);
    }

    async Task RequireCaptcha(string token, string ip)
    {
        if (string.IsNullOrEmpty(token) || !await _x.Captcha.Verify(token, ip)) throw new ApiError(400, "CAPTCHA_FAILED", "captcha required");
    }

    async Task<object> Schedule(string pid, string via) => await _x.Db.Atomic("account:" + pid, async () =>
    {
        var a = await _x.Db.Get<AccountDoc>("accounts", pid) ?? throw new ApiError(404, "NOT_FOUND", "no account");
        if (a.status != "deleted_pending")
        {
            a.status = "deleted_pending"; a.purgeAtUnixMs = Ctx.UnixMs(_x.Now.AddDays(O.DeletionGraceDays)); await _x.Db.Put("accounts", pid, a);
            await _x.Audit("web", "delete_requested", pid, via);
        }
        return (object)new Crashverse.Backend.DeleteAccountResponse { status = "scheduled", purgeAt = a.purgeAtUnixMs / 1000 };
    });
}
