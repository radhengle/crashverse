using System.Text;
using Crashverse.Backend;
using System.Text.Json;
using Crashverse.Data;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

public sealed class VerifyRequest { public string productId; public string purchaseToken; public string orderId; public string integrityToken; }
public sealed class VerifyResponse { public string status; public string reason; public WalletEx wallet; public SeasonState season; }
public sealed class PurchaseView { public string productId; public string orderId; public string state; public long grantedUnixUtc; }

/// <summary>
/// Google Play purchases (C2). The client grants nothing; only this service does, after androidpublisher confirms the
/// purchase. Tokens are unique (sha256 key). RTDN is only a trigger: the Play API is re-queried before any grant/revoke.
/// Refund policy is configurable: gemDebt (default, Security S6 adopted by PM) or floorAtZero (PM's original decision 3).
/// </summary>
public sealed class PurchaseService
{
    readonly Ctx _x; public PurchaseService(Ctx x) { _x = x; }
    WalletService W => new(_x);

    StoreItem RealMoneySku(string productId)
    {
        var i = _x.Content.Sku(productId);
        if (i == null || i.priceType != "real") throw new ApiError(400, "INVALID_PURCHASE", "unknown product");
        return i;
    }

    static int Gems(StoreItem i)
    {
        int n = 0;
        foreach (var part in (i.grants ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        { var kv = part.Split(':'); if (kv.Length == 2 && kv[0].Trim() == "gems" && int.TryParse(kv[1], out var g)) n += g; }
        return n;
    }

    List<string> Cosmetics(StoreItem i)
    {
        if (i.type == "currency_pack" || i.type == "season_pass") return new List<string>();
        if (i.contents != null && i.contents.Length > 0)
            return i.contents.Where(c => { var inner = _x.Content.Sku(c); return inner != null && inner.type != "currency_pack" && inner.type != "season_pass"; }).Distinct().ToList();
        return new List<string> { i.sku };
    }

    public async Task<VerifyResponse> Verify(string pid, VerifyRequest r)
    {
        if (string.IsNullOrEmpty(r?.purchaseToken)) throw new ApiError(400, "INVALID_PURCHASE", "purchaseToken required");
        var item = RealMoneySku(r.productId);
        // S2: purchases soft-fail on integrity (log + risk only); the Play API receipt is the authority.
        await new TitanService(_x).CheckIntegrity(r.integrityToken, Hash.Sha256B64Url("purchase|" + pid + "|" + r.purchaseToken), ranked: false, pid);
        var p = await _x.Play.GetProduct(_x.Opt.PackageName, r.productId, r.purchaseToken);
        if (p == null) return await Rejected(pid, "purchase not found");
        if (p.purchaseState == 2) return new VerifyResponse { status = "pending", wallet = WalletService.View(await W.Get(pid)), season = await new SeasonService(_x).State(pid) };
        if (p.purchaseState != 0) return await Rejected(pid, "purchase not in purchased state");
        if (!new AuthService(_x).MatchesObfuscatedId(pid, p.obfuscatedExternalAccountId)) throw new ApiError(403, "INVALID_PURCHASE", "purchase belongs to another account");
        return await Grant(pid, item, r.purchaseToken, p);
    }

    /// <summary>Agreed interface: a definitive Play "no" is a 200 with status=rejected (pending|verified|rejected). Abuse cases (token reuse, other account) stay errors.</summary>
    async Task<VerifyResponse> Rejected(string pid, string why)
    {
        await _x.Audit(pid, "purchase_rejected", pid, why);
        return new VerifyResponse { status = "rejected", reason = why, wallet = WalletService.View(await W.Get(pid)), season = await new SeasonService(_x).State(pid) };
    }

    async Task<VerifyResponse> Grant(string pid, StoreItem item, string token, PlayProductPurchase p)
    {
        var th = Hash.Sha256Hex(token);
        var doc = new PurchaseDoc { tokenHash = th, playerId = pid, productId = item.sku, orderId = p.orderId, state = "granting", grantedUnixMs = _x.NowMs };
        if (!await _x.Db.TryCreate("purchases", th, doc)) // uniqueness: one token, one grant, ever
        {
            var ex = await _x.Db.Get<PurchaseDoc>("purchases", th);
            if (ex.playerId != pid) { await _x.Audit(pid, "purchase_token_reuse", pid, th); throw new ApiError(409, "INVALID_PURCHASE", "purchase token already used"); }
            return new VerifyResponse { status = "verified", wallet = WalletService.View(await W.Get(pid)), season = await new SeasonService(_x).State(pid) };
        }
        int gems = Gems(item); var cos = Cosmetics(item);
        var change = new WalletService.Change { addSource = "cash:" + th };
        if (gems > 0) change.delta["gems"] = gems;
        change.add.AddRange(cos);
        var (w, _) = await W.Apply(pid, change, "purchase", th);
        doc.gems = gems; doc.cosmetics = cos;
        if (item.type == "season_pass")
        {
            var sid = new Calendar(_x).SeasonKey(_x.Now);
            doc.entitlementId = SeasonService.PremiumEntitlementId(pid, sid);
            await _x.Db.Put("entitlements", doc.entitlementId, new EntitlementDoc { id = doc.entitlementId, playerId = pid, type = "season_premium", seasonId = sid, state = "active", purchaseTokenHash = th });
        }
        doc.state = "granted"; await _x.Db.Put("purchases", th, doc);
        if (item.type == "currency_pack") await _x.Play.Consume(_x.Opt.PackageName, item.sku, token);
        else await _x.Play.Acknowledge(_x.Opt.PackageName, item.sku, token);
        await _x.Audit(pid, "purchase_granted", pid, $"{item.sku} {p.orderId}");
        return new VerifyResponse { status = "verified", wallet = WalletService.View(w), season = await new SeasonService(_x).State(pid) };
    }

    public async Task<ListResponse<PurchaseView>> Restore(string pid) =>
        new((await _x.Db.List<PurchaseDoc>("purchases")).Where(p => p.playerId == pid)
            .Select(p => new PurchaseView { productId = p.productId, orderId = p.orderId, state = p.state, grantedUnixUtc = p.grantedUnixMs / 1000 }));

    /// <summary>Revokes a purchase after the Play API confirmed it is voided. Idempotent.</summary>
    public Task<bool> Revoke(string tokenHash, string why) => _x.Db.Atomic("purchase:" + tokenHash, async () =>
    {
        var doc = await _x.Db.Get<PurchaseDoc>("purchases", tokenHash);
        if (doc == null || doc.state == "voided") return false;
        var pid = doc.playerId; var policy = _x.Opt.RefundPolicy;
        await W.Locked(pid, async () =>
        {
            var w = await W.Get(pid);
            var c = new WalletService.Change { note = $"refund ({policy}): {why}" };
            if (doc.gems > 0)
            {
                if (policy == RefundPolicies.GemDebt) { c.delta["gems"] = -doc.gems; c.allowNegativeGems = true; } // debt; gem-bought items kept
                else
                {
                    int take = Math.Min(doc.gems, Math.Max(0, w.gems)); c.delta["gems"] = -take;          // stops at zero
                    c.note += $"; unspent clawed back {take}, shortfall {doc.gems - take} not recovered";
                }
            }
            c.remove.AddRange(doc.cosmetics); // cosmetics bought directly with cash are revoked (both policies)
            if (doc.entitlementId != null)
            {
                var ent = await _x.Db.Get<EntitlementDoc>("entitlements", doc.entitlementId);
                if (ent != null)
                {
                    ent.state = "revoked"; await _x.Db.Put("entitlements", ent.id, ent);
                    // PM decision: cosmetics claimed from premium tiers of the refunded pass are revoked too.
                    c.remove.AddRange(w.cosmeticSource.Where(kv => kv.Value == "premium_tier:" + ent.seasonId).Select(kv => kv.Key));
                }
            }
            await W.ApplyUnlocked(pid, c, "refund_clawback", tokenHash);
            return 0;
        });
        doc.state = "voided"; doc.voidedUnixMs = _x.NowMs; await _x.Db.Put("purchases", tokenHash, doc);
        await _x.Audit("play", "purchase_voided", pid, $"{doc.productId} {doc.orderId} policy={policy} {why}");
        await _x.Db.Atomic("account:" + pid, async () =>
        {
            var a = await _x.Db.Get<AccountDoc>("accounts", pid); if (a == null) return 0;
            a.refundTimesUnixMs.Add(_x.NowMs);
            long since = _x.NowMs - (long)TimeSpan.FromDays(_x.Opt.RefundAbuseWindowDays).TotalMilliseconds;
            a.refundTimesUnixMs.RemoveAll(t => t < since);
            if (a.refundTimesUnixMs.Count >= _x.Opt.RefundAbuseCount && !a.refundAbuse)
            {
                a.refundAbuse = true; a.riskScore += 25; // S6: flag + risk, never an automatic ban
                await _x.Audit("system", "refund_abuse_flag", pid, $"{a.refundTimesUnixMs.Count} refunds in {_x.Opt.RefundAbuseWindowDays}d");
            }
            await _x.Db.Put("accounts", pid, a); return 0;
        });
        return true;
    });

    /// <summary>Scheduled job: poll the Voided Purchases API (every 6 h in prod).</summary>
    public async Task<int> PollVoided()
    {
        var cp = await _x.Db.Get<LedgerEntry>("jobs", "voided_checkpoint") ?? new LedgerEntry();
        int n = 0;
        foreach (var v in await _x.Play.ListVoided(_x.Opt.PackageName, cp.ts))
        { if (await Revoke(Hash.Sha256Hex(v.purchaseToken), "voided purchases API")) n++; cp.ts = Math.Max(cp.ts, v.voidedTimeMillis); }
        await _x.Db.Put("jobs", "voided_checkpoint", cp);
        return n;
    }

    /// <summary>
    /// Pub/Sub push (RTDN). Gap 6: OIDC verified by the caller; duplicates dropped by messageId; Play API re-queried
    /// before granting or revoking; the notification body itself is never trusted.
    /// </summary>
    public async Task<string> HandleRtdn(JsonElement body)
    {
        var msg = body.GetProperty("message");
        var messageId = msg.GetProperty("messageId").GetString();
        if (!await _x.Db.TryCreate("rtdnMessages", messageId, new IdentityDoc { key = messageId, linkedUnixMs = _x.NowMs })) return "duplicate";
        var data = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(msg.GetProperty("data").GetString()))).RootElement;
        if (data.GetProperty("packageName").GetString() != _x.Opt.PackageName) return "ignored_package";
        if (data.TryGetProperty("voidedPurchaseNotification", out var vn))
        {
            var token = vn.GetProperty("purchaseToken").GetString();
            var voided = await _x.Play.ListVoided(_x.Opt.PackageName, _x.NowMs - (long)TimeSpan.FromDays(30).TotalMilliseconds);
            if (!voided.Any(v => v.purchaseToken == token)) { await _x.Audit("play", "rtdn_unconfirmed_void", null, Hash.Sha256Hex(token)); return "unconfirmed"; }
            return await Revoke(Hash.Sha256Hex(token), "RTDN voided (confirmed)") ? "revoked" : "noop";
        }
        if (data.TryGetProperty("oneTimeProductNotification", out var on))
        {
            int type = on.GetProperty("notificationType").GetInt32();
            var token = on.GetProperty("purchaseToken").GetString(); var sku = on.GetProperty("sku").GetString();
            var p = await _x.Play.GetProduct(_x.Opt.PackageName, sku, token);
            if (p == null) return "unknown_token";
            if (type == 1 && p.purchaseState == 0) // ONE_TIME_PRODUCT_PURCHASED: grant if the client never called verify
            {
                var owner = (await _x.Db.List<AccountDoc>("accounts")).FirstOrDefault(a => new AuthService(_x).MatchesObfuscatedId(a.playerId, p.obfuscatedExternalAccountId));
                if (owner == null) return "unknown_account";
                var r = await Grant(owner.playerId, RealMoneySku(sku), token, p); return r.status;
            }
            if (type == 2) // CANCELED: only act if Play confirms
            {
                if (p.purchaseState != 1) { await _x.Audit("play", "rtdn_unconfirmed_cancel", null, Hash.Sha256Hex(token)); return "unconfirmed"; }
                return await Revoke(Hash.Sha256Hex(token), "RTDN canceled (confirmed)") ? "revoked" : "noop";
            }
            return "ignored";
        }
        return "ignored";
    }
}
