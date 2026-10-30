using System.Text.Json.Nodes;
using Crashverse.Backend;

namespace Crashverse.Server.Tests;

/// <summary>C1 / C2 / H1-H4 from SECURITY_REVIEW_v1.md, plus Security Admin's conditions (Backend_Plan_v1_Review.md).</summary>
public class C1_ServerHeldStateTests
{
    [Fact]
    public async Task EditedSignedCheckpoint_IsIgnored_ServerStateUsed()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var t = await h.StartRanked(s); var token = t["token"].GetString(); int budget = t["shotBudget"].GetInt32();
        var cp = (await h.Shot(s, token, 1, 1000, budget)).As<RankedCheckpointEx>();
        cp.damageScore = 5000; // edit after signing, keep the receipt
        var fin = await h.Finalize(s, token, new() { cp });
        Assert.Equal((int)SubmitStatus.Accepted, fin["status"].GetInt32());
        Assert.Equal(1000, fin["finalScore"].GetInt32());
        Assert.Contains("failed verification", fin["message"].GetString());
    }

    [Fact]
    public async Task ReceiptsCarryKid_AndRotateWithOverlap()
    {
        await using var h = await Harness.Start();
        var r = h.X.Receipts; var rec = r.Sign("run1", "canon");
        Assert.StartsWith("receipt-dev-1.", rec);
        r.Rotate("receipt-dev-2", System.Security.Cryptography.RandomNumberGenerator.GetBytes(32), TimeSpan.FromHours(26));
        Assert.True(r.Verify("run1", "canon", rec));                 // old kid valid during overlap
        Assert.StartsWith("receipt-dev-2.", r.Sign("run1", "canon"));
        h.Advance(TimeSpan.FromHours(27));
        Assert.False(r.Verify("run1", "canon", rec));                // retired after overlap
        Assert.False(r.Verify("run2", "canon", r.Sign("run1", "canon"))); // per-run HKDF key
    }

    [Fact]
    public async Task RunAndAccessTokens_AreES256_AndRotateWithOverlap()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var header = JsonNode.Parse(B64.FromUrl(s.access.Split('.')[0]));
        Assert.Equal("ES256", (string)header["alg"]); Assert.Equal("access-dev-1", (string)header["kid"]);
        var t = await h.StartRanked(s); var token = t["token"].GetString();
        h.X.Tokens.Ring("run").Rotate(new EcdsaDevSigner("run-dev-2"), TimeSpan.FromHours(26));
        h.X.Tokens.Ring("access").Rotate(new EcdsaDevSigner("access-dev-2"), TimeSpan.FromMinutes(15));
        Assert.Equal(HttpStatusCode.OK, (await h.Shot(s, token, 1, 100, t["shotBudget"].GetInt32())).status); // old run + access key in overlap
        h.Advance(TimeSpan.FromHours(27));
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Get("/v1/wallet", s)).status); // old access token: key retired (and expired)
        Assert.Null(h.X.Tokens.Verify("run", token, out var err)); Assert.NotNull(err);
    }

    [Fact]
    public async Task ForgedOrForeignRunToken_IsRejected()
    {
        await using var h = await Harness.Start();
        var a = await h.Guest(); var b = await h.Guest();
        var token = (await h.StartRanked(a))["token"].GetString();
        var r = await h.Post("/v1/titan/ranked/checkpoint", new RankedCheckpointEx { token = token, shotIndex = 1, damageScore = 10, shotsLeft = 5, inputs = Harness.In(1) }, b);
        Assert.Equal("UNKNOWN_RUN", r.Code);
        var parts = token.Split('.'); var forged = parts[0] + "." + parts[1] + "." + B64.Url(new byte[64]);
        Assert.Equal("UNKNOWN_RUN", (await h.Post("/v1/titan/ranked/checkpoint", new RankedCheckpointEx { token = forged, shotIndex = 1, inputs = Harness.In(1) }, a)).Code);
    }

    [Fact]
    public async Task ClientHasNoWalletWritePath_AndLedgerIsAppendOnly()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await h.Post("/v1/wallet", new { gems = 99999 }, s)).status);
        await h.Post("/v1/daily-reward/claim", null, s);
        var led = await h.Get("/v1/wallet/ledger", s);
        var id = led["items"][0].GetProperty("id").GetString();
        await Assert.ThrowsAsync<AppendOnlyViolation>(() => h.X.Db.Put("ledger", id, new LedgerEntry()));
        await Assert.ThrowsAsync<AppendOnlyViolation>(() => h.X.Db.Delete("ledger", id));
        Assert.False(await h.X.Db.TryCreate("ledger", id, new LedgerEntry()));
    }

    [Fact]
    public async Task ReplayedRequestNonce_IsRejected()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/daily-reward/claim", null, s, nonce: "nonce-aaaaaaaaaaaaaaaa")).status);
        Assert.Equal("NONCE_REPLAY", (await h.Post("/v1/daily-reward/claim", null, s, nonce: "nonce-aaaaaaaaaaaaaaaa")).Code);
        Assert.Equal("NONCE_REQUIRED", (await h.Post("/v1/daily-reward/claim", null, s, nonce: "short")).Code);
    }
}

public class C2_PurchaseTests
{
    [Fact]
    public async Task PremiumTrack_RequiresUnlock_AndIsNotAPlayProduct()
    {
        // Economy v1: the premium track is gem-priced (450) and unlocked only via /v1/season/premium/unlock, never a Play purchase.
        await using var h = await Harness.Start();
        var s = await h.Guest();
        await h.X.Db.Put("seasonProgress", s.pid + ":s1_wrecking_frontier", new SeasonProgressDoc { seasonId = "s1_wrecking_frontier", xp = 200 });
        Assert.Equal("PREMIUM_NOT_OWNED", (await h.Post("/v1/season/tiers/1/claim", new { premium = true }, s)).Code);
        Assert.Equal("INVALID_PURCHASE", (await h.Verify(s, "s1_premium_track", h.PlayPurchase(s, "s1_premium_track"))).Code);
        Assert.False((await h.Get("/v1/season", s))["premium"].GetBoolean());
        await new EconomyService(h.X).GrantGems(s.pid, 450, "test", "premium");
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/season/premium/unlock", new { }, s)).status);
        Assert.True((await h.Get("/v1/season", s))["premium"].GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/season/tiers/1/claim", new { premium = true }, s)).status);
    }

    [Fact]
    public async Task UnknownPendingOrForeignPurchases_GrantNothing()
    {
        await using var h = await Harness.Start();
        var a = await h.Guest(); var b = await h.Guest();
        var unknown = await h.Verify(a, "gems_small", "made-up-token"); // agreed interface: definitive Play "no" → 200 status=rejected
        Assert.Equal("rejected", unknown["status"].GetString()); Assert.Equal(0, unknown["wallet"].GetProperty("gems").GetInt32());
        Assert.Equal("rejected", (await h.Verify(a, "gems_small", h.PlayPurchase(a, "gems_small", state: 1)))["status"].GetString()); // cancelled
        var pending = h.PlayPurchase(a, "gems_small", state: 2);
        Assert.Equal("pending", (await h.Verify(a, "gems_small", pending))["status"].GetString());
        var bs = h.PlayPurchase(b, "gems_small"); // bound to b's obfuscatedAccountId
        Assert.Equal("INVALID_PURCHASE", (await h.Verify(a, "gems_small", bs)).Code);
        Assert.Equal("INVALID_PURCHASE", (await h.Verify(a, "skin_brass_baron", bs)).Code); // gem-priced sku is not a Play product
        Assert.Equal(0, (await h.Wallet(a)).gems);
    }

    [Fact]
    public async Task PurchaseTokenReuse_GrantsOnce()
    {
        await using var h = await Harness.Start();
        var a = await h.Guest();
        var tok = h.PlayPurchase(a, "gems_small");
        Assert.Equal("verified", (await h.Verify(a, "gems_small", tok))["status"].GetString());
        Assert.Equal("verified", (await h.Verify(a, "gems_small", tok))["status"].GetString());
        Assert.Equal(100, (await h.Wallet(a)).gems);
        Assert.Contains(tok, h.Play.Consumed);
        var b = await h.Guest(); h.Play.Purchases[tok].obfuscatedExternalAccountId = h.Obf(b); // even if Play said it's b's
        Assert.Equal("INVALID_PURCHASE", (await h.Verify(b, "gems_small", tok)).Code);
        Assert.Single((await h.Get("/v1/purchases", a))["items"].EnumerateArray());
    }

    [Fact]
    public async Task Rtdn_RequiresPushAuth_DropsDuplicates_AndRequeriesPlay()
    {
        await using var h = await Harness.Start();
        var a = await h.Guest();
        var tok = h.PlayPurchase(a, "gems_small");
        await h.Verify(a, "gems_small", tok);
        var voidMsg = new { packageName = h.X.Opt.PackageName, voidedPurchaseNotification = new { purchaseToken = tok, orderId = "o", productType = 2 } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Rtdn(voidMsg, auth: "nope")).status);
        Assert.Equal("unconfirmed", (await h.Rtdn(voidMsg))["result"].GetString()); // Play API does not list it as voided
        Assert.Equal(100, (await h.Wallet(a)).gems);
        h.Play.Voided.Add(new PlayVoidedPurchase { purchaseToken = tok, voidedTimeMillis = h.X.NowMs });
        Assert.Equal("revoked", (await h.Rtdn(voidMsg, "m1"))["result"].GetString());
        Assert.Equal("duplicate", (await h.Rtdn(voidMsg, "m1"))["result"].GetString());
        // a "purchased" notification is also re-checked: grant only after Play confirms
        var t2 = h.PlayPurchase(a, "gems_small");
        var buy = new { packageName = h.X.Opt.PackageName, oneTimeProductNotification = new { notificationType = 1, purchaseToken = t2, sku = "gems_small" } };
        Assert.Equal("verified", (await h.Rtdn(buy))["result"].GetString());
        var cancel = new { packageName = h.X.Opt.PackageName, oneTimeProductNotification = new { notificationType = 2, purchaseToken = t2, sku = "gems_small" } };
        Assert.Equal("unconfirmed", (await h.Rtdn(cancel))["result"].GetString()); // Play still says purchased
        Assert.Equal("unknown_token", (await h.Rtdn(new { packageName = h.X.Opt.PackageName, oneTimeProductNotification = new { notificationType = 1, purchaseToken = "forged", sku = "gems_small" } }))["result"].GetString());
    }

    async Task<(Harness h, Session s, string refundTok)> GemsSpentThenRefund(string policy)
    {
        var h = await Harness.Start(o => o.RefundPolicy = policy);
        var s = await h.Guest();
        var t1 = h.PlayPurchase(s, "gems_small"); var t2 = h.PlayPurchase(s, "gems_small");
        await h.Verify(s, "gems_small", t1); await h.Verify(s, "gems_small", t2); // 200 gems
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/store/buy", new { sku = "trail_confetti" }, s)).status); // -120 → 80
        h.Play.Voided.Add(new PlayVoidedPurchase { purchaseToken = t1, voidedTimeMillis = h.X.NowMs });
        Assert.Equal(1, (await h.Job("/internal/jobs/voided-poll"))["revoked"].GetInt32());
        return (h, s, t1);
    }

    [Fact]
    public async Task Refund_DefaultGemDebt_LocksGemSpending_KeepsGemBoughtItems()
    {
        Assert.Equal(RefundPolicies.GemDebt, new ServerOptions().RefundPolicy);
        var (h, s, _) = await GemsSpentThenRefund(RefundPolicies.GemDebt);
        await using var _h = h;
        var w = await h.Wallet(s);
        Assert.Equal(-20, w.gems); Assert.True(w.gemSpendLocked);
        Assert.Contains("trail_confetti", w.cosmetics);                         // gem-bought item kept
        Assert.Equal("GEM_DEBT_LOCKED", (await h.Post("/v1/store/buy", new { sku = "emote_salute" }, s)).Code);
        var t3 = h.PlayPurchase(s, "gems_small"); await h.Verify(s, "gems_small", t3); // income repays the debt first
        w = await h.Wallet(s); Assert.Equal(80, w.gems); Assert.False(w.gemSpendLocked);
        var led = (await h.Get("/v1/wallet/ledger", s))["items"].EnumerateArray().First(e => e.GetProperty("reason").GetString() == "refund_clawback");
        Assert.Equal(-100, led.GetProperty("delta").GetProperty("gems").GetInt32());
    }

    [Fact]
    public async Task Refund_FloorAtZeroAlternative_ClawsBackUnspentOnly_AndLogsShortfall()
    {
        var (h, s, _) = await GemsSpentThenRefund(RefundPolicies.FloorAtZero);
        await using var _h = h;
        var w = await h.Wallet(s);
        Assert.Equal(0, w.gems); Assert.False(w.gemSpendLocked);
        Assert.Contains("trail_confetti", w.cosmetics);
        var led = (await h.Get("/v1/wallet/ledger", s))["items"].EnumerateArray().First(e => e.GetProperty("reason").GetString() == "refund_clawback");
        Assert.Equal(-80, led.GetProperty("delta").GetProperty("gems").GetInt32());
        Assert.Contains("shortfall 20", led.GetProperty("note").GetString());
    }

    [Theory]
    [InlineData(RefundPolicies.GemDebt)]
    [InlineData(RefundPolicies.FloorAtZero)]
    public async Task Refund_CashCosmetic_IsRevoked_GemUnlockedPremiumKept(string policy)
    {
        // Economy v1: only cash SKUs can be voided by Play. The premium track is bought with gems, so a cash-skin refund
        // revokes that skin and leaves the gem-unlocked premium track and its tier cosmetics alone.
        await using var h = await Harness.Start(o => o.RefundPolicy = policy);
        h.X.Content.AcceptedStore.Add(new Crashverse.Data.StoreItem { sku = "skin_cash_test", type = "wrecker_skin", cosmetic = true, priceType = "real", price = 199 });
        var s = await h.Guest();
        var tc = h.PlayPurchase(s, "skin_cash_test"); await h.Verify(s, "skin_cash_test", tc);
        await new EconomyService(h.X).GrantGems(s.pid, 450, "test", "premium");
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/season/premium/unlock", new { }, s)).status);
        await h.X.Db.Put("seasonProgress", s.pid + ":s1_wrecking_frontier", new SeasonProgressDoc { seasonId = "s1_wrecking_frontier", xp = 200 });
        await h.Post("/v1/season/tiers/1/claim", new { premium = true }, s);
        var w = await h.Wallet(s); Assert.Contains("skin_cash_test", w.cosmetics); Assert.Contains("s1_cosmetic_01", w.cosmetics);
        h.Play.Voided.Add(new PlayVoidedPurchase { purchaseToken = tc, voidedTimeMillis = h.X.NowMs });
        await h.Job("/internal/jobs/voided-poll");
        w = await h.Wallet(s);
        Assert.DoesNotContain("skin_cash_test", w.cosmetics); Assert.Contains("s1_cosmetic_01", w.cosmetics);
        Assert.True((await h.Get("/v1/season", s))["premium"].GetBoolean());
        Assert.Contains((await h.X.Db.List<AuditEntry>("audit")), a => a.action == "purchase_voided");
    }

    [Fact]
    public async Task TwoRefundsIn90Days_FlagsAccount_WithoutBan()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        foreach (var i in new[] { 0, 1 })
        {
            var t = h.PlayPurchase(s, "gems_small"); await h.Verify(s, "gems_small", t);
            h.Play.Voided.Add(new PlayVoidedPurchase { purchaseToken = t, voidedTimeMillis = h.X.NowMs + i });
        }
        await h.Job("/internal/jobs/voided-poll");
        var a = await h.X.Db.Get<AccountDoc>("accounts", s.pid);
        Assert.True(a.refundAbuse); Assert.True(a.riskScore >= 25); Assert.Equal("active", a.status);
    }
}

public class H1_CampaignTests
{
    [Fact]
    public async Task ResultWithoutOrWithReusedOrExpiredToken_IsRejected()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal("INVALID_ATTEMPT", (await h.Post("/v1/campaign/results", Harness.Win("c1_l01", null, 1, 4), s)).Code);
        var tok = await h.Attempt(s, "c1_l01");
        Assert.True((await h.Post("/v1/campaign/results", Harness.Win("c1_l01", tok, 1, 4), s))["accepted"].GetBoolean());
        Assert.Equal("ATTEMPT_REUSED", (await h.Post("/v1/campaign/results", Harness.Win("c1_l01", tok, 1, 4), s)).Code); // replay of the same run
        var other = await h.Attempt(s, "c1_l02");
        Assert.Equal("INVALID_ATTEMPT", (await h.Post("/v1/campaign/results", Harness.Win("c1_l01", other, 1, 4), s)).Code); // bound to level
        var b = await h.Guest();
        Assert.Equal("INVALID_ATTEMPT", (await h.Post("/v1/campaign/results", Harness.Win("c1_l02", other, 1, 4), b)).Code); // bound to player
        var old = await h.Attempt(s, "c1_l02");
        h.Advance(TimeSpan.FromDays(8)); await h.Renew(s);
        Assert.Equal("INVALID_ATTEMPT", (await h.Post("/v1/campaign/results", Harness.Win("c1_l02", old, 1, 4), s)).Code); // expired (≤7 d)
    }

    [Fact]
    public async Task OfflineTokenPool_IsCappedAt20()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var r = await h.Post("/v1/campaign/attempts", new { levelId = "c1_l01", count = 50 }, s);
        Assert.Equal(20, r["items"].GetArrayLength());
        Assert.Equal(0, (await h.Post("/v1/campaign/attempts", new { levelId = "c1_l01", count = 5 }, s))["items"].GetArrayLength());
    }

    [Theory]
    [InlineData(4, 0.9f, 3, "shots left out of range")]       // more shots left than possible
    [InlineData(1, 0.5f, 3, "ratio below win threshold")]
    [InlineData(1, 0.9f, 9999, "combo out of range")]
    public async Task ForgedStats_AreRejected(int shotsLeft, float ratio, int combo, string why)
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var rep = Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), Math.Min(shotsLeft, 3), 4, ratio, combo);
        rep.shotsLeft = shotsLeft;
        var r = await h.Post("/v1/campaign/results", rep, s);
        Assert.False(r["accepted"].GetBoolean()); Assert.Contains(why, r["message"].GetString());
    }

    [Fact]
    public async Task InputLogMismatchOrSpeedhack_IsRejected()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var rep = Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4); rep.inputs.RemoveRange(0, 4); // drop one whole shot
        Assert.Contains("input log", (await h.Post("/v1/campaign/results", rep, s))["message"].GetString());
        rep = Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4); rep.inputs = Harness.In(200).Concat(Harness.In(360)).Concat(Harness.In(600)).ToList(); // 2nd fire 60 ticks after settle: < 1.5 s at 50 Hz
        Assert.Contains("too close", (await h.Post("/v1/campaign/results", rep, s))["message"].GetString());
    }

    [Fact]
    public async Task PendingEntry_HiddenInTopN_UntilReplayPasses()
    {
        await using var h = await Harness.Start();
        var s = await h.Linked(); var viewer = await h.Linked();
        await h.Post("/v1/campaign/results", Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4), s);
        var page = await h.Get("/v1/leaderboards/3", viewer);
        Assert.Equal(0, page["top"].GetArrayLength());
        h.Replay.Fn = r => new ReplayResult { won = true, shotsLeft = 1, destroyedRatio = 0.9f, bestCombo = 3 };
        var job = await h.Job("/internal/jobs/replay");
        Assert.True(job.status == HttpStatusCode.OK, job.json.ToString());
        Assert.Equal(1, job["done"].GetInt32());
        page = await h.Get("/v1/leaderboards/3", viewer);
        var e = page["top"][0];
        Assert.Equal(s.pid, e.GetProperty("playerId").GetString());
        Assert.False(e.GetProperty("provisional").GetBoolean());
        Assert.True(e.GetProperty("submittedUnixMs").GetInt64() > 0);
    }

    [Fact]
    public async Task ReplayMismatch_GoesToShadowBoard_NoBan_ClawsBackProvisionalFirstClear()
    {
        await using var h = await Harness.Start();
        var s = await h.Linked(); var viewer = await h.Linked();
        await h.ClearPrereqs(s, "c1_l10");
        var r = await h.Post("/v1/campaign/results", Harness.Win("c1_l10", await h.Attempt(s, "c1_l10"), 3, 8, 0.9f, 2), s);
        Assert.True(r["firstClear"].GetBoolean());
        Assert.Equal(500, (await h.Wallet(s)).coins); // provisional first-clear reward (capped per day)
        h.Replay.Fn = _ => new ReplayResult { won = true, shotsLeft = 3, destroyedRatio = 0.5f, bestCombo = 2 }; // far off
        await h.Job("/internal/jobs/replay");
        Assert.Equal(0, (await h.Wallet(s)).coins); // clawed back, floor at 0
        Assert.Equal(0, (await h.Get("/v1/leaderboards/3", viewer))["top"].GetArrayLength());
        Assert.NotNull(await h.X.Db.Get<BoardEntryDoc>("shadowBoards", "campaign:c1_alltime|" + s.pid));
        Assert.Equal("active", (await h.X.Db.Get<AccountDoc>("accounts", s.pid)).status);
        Assert.Contains(await h.X.Db.List<AuditEntry>("audit"), a => a.action == "replay_mismatch_shadowed");
    }

    [Fact]
    public async Task FirstClearRewards_OverDailyCap_AreDeferredUntilValidated()
    {
        await using var h = await Harness.Start(o => o.FirstClearDailyCap = 0);
        var s = await h.Guest();
        await h.ClearPrereqs(s, "c1_l10");
        var r = await h.Post("/v1/campaign/results", Harness.Win("c1_l10", await h.Attempt(s, "c1_l10"), 3, 8, 0.9f, 2), s);
        Assert.True(r["rewardDeferred"].GetBoolean()); Assert.Equal(0, (await h.Wallet(s)).coins);
        await new ReplayService(h.X).Enqueue((await h.X.Db.List<RunDoc>("runs")).Single().runId, "test");
        h.Replay.Fn = _ => new ReplayResult { won = true, shotsLeft = 3, destroyedRatio = 0.9f, bestCombo = 2 };
        await h.Job("/internal/jobs/replay");
        Assert.Equal(500, (await h.Wallet(s)).coins);
    }

    [Fact]
    public async Task LowerOfClientAndReplay_Counts()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var r = await h.Post("/v1/campaign/results", Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4, 0.95f, 3), s);
        Assert.Equal(950 + 500 + 150, r["provisionalBest"].GetInt32()); Assert.Equal(0, r["levelBest"].GetInt32()); // BE-01: validated best unchanged until replay
        await new ReplayService(h.X).Enqueue((await h.X.Db.List<RunDoc>("runs")).Single().runId, "test");
        h.Replay.Fn = _ => new ReplayResult { won = true, shotsLeft = 1, destroyedRatio = 0.94f, bestCombo = 2 };
        await h.Job("/internal/jobs/replay");
        Assert.Equal(940 + 500 + 100, (await h.X.Db.Get<LevelBestDoc>("campaignBest", s.pid + ":c1_l01")).best);
    }

    [Fact]
    public async Task EventStars_RequireToken_AndAreCappedByShotsUsed()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal("INVALID_ATTEMPT", (await h.Post("/v1/events/magnetic_mayhem_e1/stars", new { stars = 3, simVersion = SimVersions.Current, inputs = Harness.In(200) }, s)).Code);
        var tok = await h.Attempt(s, "magnetic_mayhem_e1", "event");
        var inputs = Enumerable.Range(1, 5).SelectMany(i => Harness.In(i * 200)).ToList(); // used all 5 shots → 0 left → 1 star max
        var st = await h.Post("/v1/events/magnetic_mayhem_e1/stars", new EventStarsRequest { attemptToken = tok, stars = 3, simVersion = SimVersions.Current, inputs = inputs }, s);
        Assert.Equal(10, st["xp"].GetInt32());
    }
}

public class H2_RankedTitanTests
{
    [Fact]
    public async Task ShotsTooFast_AndOverPerShotCap_AreRejected()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var t = await h.StartRanked(s); var tok = t["token"].GetString(); int b = t["shotBudget"].GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await h.Shot(s, tok, 1, 1000, b)).status);
        h.Advance(TimeSpan.FromMilliseconds(-2000)); // only ~1 s after the previous settled shot
        var r = await h.Shot(s, tok, 2, 2000, b);
        Assert.Equal("CHECKPOINT_REJECTED", r.Code); Assert.Contains("too close", r["error"].GetProperty("message").GetString());
        r = await h.Shot(s, tok, 2, 1000 + 5001, b);
        Assert.Contains("implausible damage", r["error"].GetProperty("message").GetString());
    }

    [Fact]
    public async Task MissingInputs_OrSimVersionMismatch_CountAsPractice()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var t = await h.StartRanked(s); var tok = t["token"].GetString();
        await h.Shot(s, tok, 1, 1000, t["shotBudget"].GetInt32(), inputs: false);
        Assert.Equal((int)SubmitStatus.RefundedAsPractice, (await h.Finalize(s, tok))["status"].GetInt32());
        var s2 = await h.Guest(); var t2 = await h.StartRanked(s2);
        Assert.Equal((int)SubmitStatus.RefundedAsPractice, (await h.Finalize(s2, t2["token"].GetString(), sim: "old"))["status"].GetInt32());
    }

    [Theory]
    [InlineData("device")] [InlineData("app")] [InlineData("package")] [InlineData("cert")] [InlineData("hash")] [InlineData("stale")]
    public async Task FailedIntegrity_TurnsRankedIntoPractice_WithoutConsumingAttempt(string what)
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var t = await h.StartRanked(s, v =>
        {
            switch (what)
            {
                case "device": v.deviceRecognitionVerdict = new[] { "MEETS_BASIC_INTEGRITY" }; break;
                case "app": v.appRecognitionVerdict = "UNRECOGNIZED_VERSION"; break;
                case "package": v.packageName = "com.evil.repack"; break;
                case "cert": v.certificateSha256Digest = new[] { "other" }; break;
                case "hash": v.requestHash = "x"; break;
                case "stale": v.timestampMillis -= 600_000; break;
            }
        });
        Assert.True(t["practice"].GetBoolean());
        Assert.Equal((int)SubmitStatus.RefundedAsPractice, (await h.Finalize(s, t["token"].GetString()))["status"].GetInt32());
        Assert.False((await h.Get("/v1/titan/ranked/status", s))["attemptUsed"].GetBoolean());
        Assert.False((await h.StartRanked(s))["practice"].GetBoolean());
    }

    [Fact]
    public async Task IntegrityLogOnlyMode_DoesNotDowngrade_ButLogs()
    {
        await using var h = await Harness.Start(o => o.IntegrityMode = "logOnly");
        var s = await h.Guest();
        var t = await h.StartRanked(s, v => v.deviceRecognitionVerdict = Array.Empty<string>());
        Assert.False(t["practice"].GetBoolean());
        Assert.Contains(await h.X.Db.List<AuditEntry>("audit"), a => a.action == "integrity_fail_ranked");
    }

    [Fact]
    public async Task UnlicensedOnly_IsNotRequired_ButRaisesRisk()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.False((await h.StartRanked(s, v => v.appLicensingVerdict = "UNLICENSED"))["practice"].GetBoolean());
        Assert.True((await h.X.Db.Get<AccountDoc>("accounts", s.pid)).riskScore > 0);
    }

    [Fact]
    public async Task OneRankedAttemptPerUtcDay()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal(HttpStatusCode.OK, (await h.StartRanked(s)).status);
        Assert.Equal("ATTEMPT_USED", (await h.StartRanked(s)).Code);
        h.At(new DateTime(2026, 10, 17, 0, 0, 0)); await h.Renew(s);
        Assert.Equal(HttpStatusCode.OK, (await h.StartRanked(s)).status);
    }

    [Fact]
    public async Task GraceWindow_InclusiveAt001500000()
    {
        await using var h = await Harness.Start();
        var a = await h.Guest(); var b = await h.Guest();
        h.At(new DateTime(2026, 10, 16, 23, 59, 0)); await h.Renew(a, b);
        var ta = (await h.StartRanked(a))["token"].GetString(); var tb = (await h.StartRanked(b))["token"].GetString();
        h.At(new DateTime(2026, 10, 17, 0, 15, 0, 0)); await h.Renew(a, b);
        var fa = await h.Finalize(a, ta);
        Assert.True(fa.status == HttpStatusCode.OK, fa.json.ToString());
        Assert.Equal((int)SubmitStatus.Accepted, fa["status"].GetInt32());
        h.At(new DateTime(2026, 10, 17, 0, 15, 0, 1));
        Assert.Equal((int)SubmitStatus.RefundedAsPractice, (await h.Finalize(b, tb))["status"].GetInt32());
    }

    [Fact]
    public async Task FinalizeTwice_IsDuplicate()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var (tok, fin) = await h.RankedRun(s, 2, 1000);
        Assert.Equal((int)SubmitStatus.Accepted, fin["status"].GetInt32());
        Assert.Equal((int)SubmitStatus.RejectedDuplicate, (await h.Finalize(s, tok))["status"].GetInt32());
    }

    [Fact]
    public async Task ReplayLowerScoreCounts_AndOutOfToleranceShadows()
    {
        await using var h = await Harness.Start();
        var a = await h.Linked(); var b = await h.Linked(); var viewer = await h.Linked();
        await h.RankedRun(a, 2, 2000); // 4000
        await h.RankedRun(b, 2, 2500); // 5000
        h.Replay.Fn = r => r.clientScore == 4000 ? new ReplayResult { damageScore = 3900 } : new ReplayResult { damageScore = 1000 };
        await h.Job("/internal/jobs/replay");
        var top = (await h.Get("/v1/leaderboards/0", viewer))["top"];
        Assert.Equal(1, top.GetArrayLength());
        Assert.Equal(3900, top[0].GetProperty("score").GetInt32()); // lower of client 4000 / replay 3900
        Assert.True((await h.X.Db.Get<BoardEntryDoc>("boards", "titan:2026-10-16|" + b.pid)).shadow);
        Assert.Equal("active", (await h.X.Db.Get<AccountDoc>("accounts", b.pid)).status);
    }

    [Fact]
    public async Task Top10PercentBonus_WaitsForReplay()
    {
        await using var h = await Harness.Start();
        var s = await h.Linked();
        await h.RankedRun(s, 2, 2000);
        h.At(new DateTime(2026, 10, 17, 0, 16, 0)); await h.Renew(s);
        Assert.StartsWith("deferred", (await h.Job("/internal/jobs/titan-close/2026-10-16"))["result"].GetString());
        Assert.Empty((await h.Get("/v1/inbox", s))["items"].EnumerateArray());
        h.Replay.Fn = _ => new ReplayResult { damageScore = 4000 };
        await h.Job("/internal/jobs/replay");
        Assert.Equal("done", (await h.Job("/internal/jobs/titan-close/2026-10-16"))["result"].GetString());
        Assert.Single((await h.Get("/v1/inbox", s))["items"].EnumerateArray());
    }

    [Fact]
    public async Task Sampling_RandomFivePercent_AndAllHighRisk()
    {
        await using var h = await Harness.Start(o => o.TopN = 0);
        h.X.Content.Titan.topPercent = 0f;
        var a = await h.Guest(); var risky = await h.Guest();
        await h.RankedRun(a, 1, 100);
        Assert.Equal("none", (await h.X.Db.List<RunDoc>("runs")).Single(r => r.playerId == a.pid).replayState);
        var acc = await h.X.Db.Get<AccountDoc>("accounts", risky.pid); acc.riskScore = 99; await h.X.Db.Put("accounts", risky.pid, acc);
        await h.RankedRun(risky, 1, 100);
        Assert.Equal("queued", (await h.X.Db.List<RunDoc>("runs")).Single(r => r.playerId == risky.pid).replayState);
        h.Sampler.Value = 0.01; var c = await h.Guest(); await h.RankedRun(c, 1, 100);
        Assert.Equal("queued", (await h.X.Db.List<RunDoc>("runs")).Single(r => r.playerId == c.pid).replayState);
    }
}

public class H3_ServerConfigTests
{
    [Fact]
    public async Task DailyTitan_IsServerBuilt_AndOverrideIsAdminOnlyAndAudited()
    {
        await using var h = await Harness.Start(o => o.AdminKey = "break-glass");
        var s = await h.Guest();
        var cfg = await h.Get("/v1/titan/today", s);
        Assert.Equal("2026-10-16", cfg["dayKey"].GetString());
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), cfg["resetsAtUnixUtc"].GetInt64());
        var t = await h.StartRanked(s);
        Assert.Equal(cfg["configHash"].GetString(), t["configHash"].GetString());
        Assert.Equal(cfg["shots"].GetInt32(), t["shotBudget"].GetInt32());
        var over = new Crashverse.Data.DailyTitanConfig { shots = 99, name = "hacked" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Send(HttpMethod.Put, "/internal/admin/titan/2026-10-16", over, s)).status); // player token is not enough
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Send(HttpMethod.Put, "/internal/admin/titan/2026-10-16", over, bearer: "fake-pubsub-oidc")).status);
        Assert.Equal(HttpStatusCode.OK, (await h.Send(HttpMethod.Put, "/internal/admin/titan/2026-10-16", over, bearer: "fake-pubsub-oidc", headers: new() { ["X-Admin-Key"] = "break-glass" })).status);
        Assert.Equal(99, (await h.Get("/v1/titan/today", s))["shots"].GetInt32());
        Assert.Contains(await h.X.Db.List<AuditEntry>("audit"), a => a.action == "titan_override");
    }
}

public class H4_AccountLinkingTests
{
    [Fact]
    public async Task LinkWithoutOrWithForgedToken_IsRejected()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal("PROVIDER_TOKEN_INVALID", (await h.Post("/v1/account/link", new { provider = "facebook" }, s)).Code);
        Assert.Equal("PROVIDER_TOKEN_INVALID", (await h.Post("/v1/account/link", new { provider = "facebook", token = "forged" }, s)).Code);
        Assert.False((await h.Get("/v1/account", s))["linked"].GetBoolean());
    }

    [Fact]
    public async Task Google_RequiresServerNonce()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal("PROVIDER_TOKEN_INVALID", (await h.Post("/v1/account/link", new { provider = "google", token = "g1" }, s)).Code);
        var nonce = (await h.Post("/v1/account/link-nonce", null, s))["nonce"].GetString();
        h.Google.Tokens["g1"] = (new ProviderIdentity { subject = "gsub" }, Hash.Sha256Hex("other-nonce"));
        Assert.Equal("PROVIDER_TOKEN_INVALID", (await h.Post("/v1/account/link", new { provider = "google", token = "g1" }, s)).Code);
        nonce = (await h.Post("/v1/account/link-nonce", null, s))["nonce"].GetString();
        h.Google.Tokens["g1"] = (new ProviderIdentity { subject = "gsub" }, Hash.Sha256Hex(nonce));
        var r = await h.Post("/v1/account/link", new { provider = "google", token = "g1" }, s);
        Assert.Equal(HttpStatusCode.OK, r.status); Assert.Contains("google", r["providers"].EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task IdentityAlreadyOnAnotherAccount_NeedsExplicitChoice()
    {
        await using var h = await Harness.Start();
        var a = await h.Linked("same"); var b = await h.Guest();
        var r = await h.Post("/v1/account/link", new { provider = "facebook", token = "fb-same" }, b);
        Assert.Equal("IDENTITY_IN_USE", r.Code);
        Assert.Equal(a.pid, r["error"].GetProperty("details").GetProperty("existing").GetProperty("playerId").GetString());
        var sw = await h.Post("/v1/account/link", new { provider = "facebook", token = "fb-same", conflictResolution = "switch_to_existing" }, b);
        Assert.Equal(a.pid, sw["account"].GetProperty("playerId").GetString()); // no silent merge: the client switched accounts
    }

    [Fact]
    public async Task Unlink_NeedsReauth_AndNeverTheLastMethod()
    {
        await using var h = await Harness.Start();
        var s = await h.Linked("u1");
        Assert.Equal("LAST_LOGIN_METHOD", (await h.Post("/v1/account/unlink", new { provider = "facebook", token = "fb-u1" }, s)).Code);
        await using var hx = await Harness.Start(o => o.XEnabled = true);
        var t = await hx.Linked("u2"); hx.XClient.Codes["c"] = new ProviderIdentity { subject = "x1" };
        Assert.Equal(HttpStatusCode.OK, (await hx.Post("/v1/account/link", new { provider = "x", token = "c", codeVerifier = "v" }, t)).status);
        Assert.Equal("PROVIDER_TOKEN_INVALID", (await hx.Post("/v1/account/unlink", new { provider = "x", token = "wrong", codeVerifier = "v" }, t)).Code);
        Assert.Equal(HttpStatusCode.OK, (await hx.Post("/v1/account/unlink", new { provider = "x", token = "c", codeVerifier = "v" }, t)).status);
    }

    [Fact]
    public async Task XLogin_DisabledByDefault()
    {
        await using var h = await Harness.Start();
        Assert.False(new ServerOptions().XEnabled);
        var s = await h.Guest(); h.XClient.Codes["c"] = new ProviderIdentity { subject = "x1" };
        Assert.Equal("FEATURE_DISABLED", (await h.Post("/v1/account/link", new { provider = "x", token = "c", codeVerifier = "v" }, s)).Code);
        Assert.Equal("FEATURE_DISABLED", (await h.Post("/v1/auth/x/start", null, null)).Code);
    }

    [Fact]
    public async Task GuestScores_HiddenUntilLinked_OpenBoardsOnly()
    {
        await using var h = await Harness.Start();
        var g = await h.Guest(); var viewer = await h.Linked();
        await h.RankedRun(g, 2, 2000);
        h.Replay.Fn = _ => new ReplayResult { damageScore = 4000 }; await h.Job("/internal/jobs/replay");
        Assert.True((await h.Get("/v1/leaderboards/0", g))["meIsGuestHidden"].GetBoolean());
        Assert.Equal(0, (await h.Get("/v1/leaderboards/0", viewer))["top"].GetArrayLength());
        h.Facebook.Tokens["fb-g"] = new ProviderIdentity { subject = "g" };
        await h.Post("/v1/account/link", new { provider = "facebook", token = "fb-g" }, g);
        Assert.Equal(1, (await h.Get("/v1/leaderboards/0", viewer))["top"].GetArrayLength());
    }
}
