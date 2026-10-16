using System.Net;
using System.Text.Json.Nodes;
using Crashverse.Server.Domain;
using Crashverse.Server.Infra;
using InputEvent = Crashverse.Backend.InputEvent;

namespace Crashverse.Server.Tests;

/// <summary>Security review C1/H2/H3/M2/M3, B6, S3, Economy v1, Physics PR #6, Developer review, ENABLE_GEM_PACKS.</summary>
public class FollowUpTests
{
    // ---------------- C1: prod startup ----------------
    [Fact]
    public void Prod_RefusesDevContext_AndFakesAndLogOnly()
    {
        Assert.Throws<ProdStartupException>(() => ServerApp.DevContext(new ServerOptions { Env = CrashverseEnv.Prod }));
        Assert.Equal(CrashverseEnv.Prod, Envs.Parse(null));
        Assert.Equal(CrashverseEnv.Prod, Envs.Parse("staging-ish"));
        Assert.Equal(CrashverseEnv.Dev, Envs.Parse("dev"));
        var dev = ServerApp.DevContext(new ServerOptions { Env = CrashverseEnv.Test, IntegrityMode = "logOnly", AllowClientRunSummary = true, AdminKey = "k" });
        var ex = Assert.Throws<ProdStartupException>(() => ProdGuard.Validate(dev));
        foreach (var s in new[] { "store", "integrity", "play", "pushAuth", "replay", "captcha", "receipts", "INTEGRITY_MODE=logOnly", "ALLOW_CLIENT_RUN_SUMMARY", "admin key", "not marked prod" })
            Assert.Contains(s, ex.Message);
        var empty = Assert.Throws<ProdStartupException>(() => ServerApp.ProdContext(new ServerOptions { Env = CrashverseEnv.Prod }, new ServerApp.ProdAdapters()));
        Assert.Contains("not configured", empty.Message);
    }

    // ---------------- Developer 5: flavor ----------------
    [Fact]
    public async Task ProdServer_RejectsTestFlavorAttemptToken_OnScore()
    {
        await using var h = await Harness.Start();
        var testTokens = h.X.Tokens;
        var prodTokens = new TokenService(testTokens.Rings, h.X.Clock, "prod");
        h.X.Tokens = prodTokens; var s = await h.Guest();          // prod-flavor access token
        h.X.Tokens = testTokens; var tok = await h.Attempt(s, "c1_l01"); // test-flavor attempt token
        h.X.Tokens = prodTokens;
        var r = await h.Post("/v1/score", Harness.Win("c1_l01", tok, 1, 4), s);
        Assert.Equal("TEST_FLAVOR_TOKEN", r.Code);
        Assert.Equal("test", (string)testTokens.Verify("attempt", tok, out _)["flv"]);
        // a test-flavor access token is refused outright by a prod server
        h.X.Tokens = testTokens; var t = await h.Guest(); h.X.Tokens = prodTokens;
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Get("/v1/wallet", t)).status);
    }

    // ---------------- H2: strict flags ----------------
    [Theory]
    [InlineData("true", true)] [InlineData("True", false)] [InlineData("1", false)] [InlineData("yes", false)] [InlineData(null, false)]
    public void Flags_AreStrictTrue(string v, bool on)
    {
        var o = new ServerOptions();
        ServerOptions.ApplyEnvFlags(o, k => k is "RANKED_PUBLIC_BOARD_ENABLED" or "ENABLE_GEM_PACKS" or "IAP_ENABLED" or "TRUST_X_FORWARDED_FOR" ? v : null);
        Assert.Equal(on, o.RankedPublicBoardEnabled); Assert.Equal(on, o.EnableGemPacks); Assert.Equal(on, o.IapEnabled); Assert.Equal(on, o.TrustForwardedFor);
    }

    // ---------------- H3 + Developer 6: age gate ----------------
    [Fact]
    public async Task AgeGate_TokenRequired_Under13Refused_BandOnlyStored()
    {
        await using var h = await Harness.Start();
        Assert.Equal("AGE_GATE_REQUIRED", (await h.Post("/v1/auth/guest", new { deviceInstallId = "d1" }, null)).Code);
        var u13 = await h.Post("/v1/agegate", new { birthYear = 2018, birthMonth = 1, country = "US", deviceInstallId = "d" }, null);
        Assert.Equal("under13", u13["band"].GetString());
        Assert.False(u13.json.TryGetProperty("ageToken", out var at) && at.ValueKind == System.Text.Json.JsonValueKind.String);
        var s = await h.Guest();
        var acc = await h.X.Db.Get<AccountDoc>("accounts", s.pid);
        Assert.Equal("adult", acc.ageGate.band); Assert.Equal("US", acc.ageGate.country);
        var raw = string.Join("\n", (await h.X.Db.ListRaw("accounts")).Select(p => p.Item2));
        Assert.DoesNotContain("birthYear", raw); Assert.DoesNotContain("birthMonth", raw); Assert.DoesNotContain("1990", raw);
    }

    [Fact]
    public async Task Minor_LimitsEnforcedOnServer()
    {
        await using var h = await Harness.Start();
        var tok = await h.AgeToken(2011, 1, "NL", device: "m1"); // 15 in NL (consent age 16)
        var r = await h.Post("/v1/auth/guest", new { deviceInstallId = "m1", ageToken = tok }, null);
        var s = new Session { access = r["accessToken"].GetString(), pid = r["account"].GetProperty("playerId").GetString() };
        Assert.True((await h.X.Db.Get<AccountDoc>("accounts", s.pid)).minorRestricted);
        foreach (var (p, b) in new (string, object)[] { ("/v1/store/buy", new { sku = "x" }), ("/v1/season/premium/unlock", new { }), ("/v1/friends", new { friendCode = "x" }),
            ("/v1/purchases/google/verify", new { productId = "x", purchaseToken = "t" }), ("/v1/account/link", new { provider = "google", token = "g" }),
            ("/v1/auth/email/register", new { email = "m@example.com", password = "correct horse 1" }) })
            Assert.Equal(HttpStatusCode.Forbidden, (await h.Post(p, b, s)).status);
    }

    // ---------------- M2: trusted X-Forwarded-For ----------------
    [Fact]
    public void ClientIp_HonoursXffOnlyWhenTrusted()
    {
        var c = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        c.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        c.Request.Headers["X-Forwarded-For"] = "6.6.6.6, 1.2.3.4";
        Assert.Equal("10.0.0.1", ServerApp.ClientIp(c, new ServerOptions()));
        Assert.Equal("1.2.3.4", ServerApp.ClientIp(c, new ServerOptions { TrustForwardedFor = true }));           // 1 hop: LB-appended entry
        Assert.Equal("6.6.6.6", ServerApp.ClientIp(c, new ServerOptions { TrustForwardedFor = true, TrustedProxyHops = 2 }));
        c.Request.Headers["X-Forwarded-For"] = "junk";
        Assert.Equal("10.0.0.1", ServerApp.ClientIp(c, new ServerOptions { TrustForwardedFor = true }));
    }

    // ---------------- M3 / S3: purge, retention, inactivity ----------------
    [Fact]
    public async Task Purge_RemovesEveryPlayerRow_HashesPurchases_Idempotent()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        await new EconomyService(h.X).Day7Chest(s.pid, "d1");
        await h.Post("/v1/score", Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4), s);
        await h.X.Db.Put("purchases", "tokhash1", new PurchaseDoc { playerId = s.pid });
        Assert.Equal("scheduled", (await h.Post("/v1/account/delete", new { }, s))["status"].GetString());
        h.Advance(TimeSpan.FromDays(8));
        Assert.Equal(1, (await h.Job("/internal/jobs/purge"))["purged"].GetInt32());
        foreach (var col in Collections.PlayerData)
            foreach (var (id, json) in await h.X.Db.ListRaw(col))
                Assert.False(id.Contains(s.pid) || json.Contains(s.pid), $"{col}/{id} still references the player");
        var p = await h.X.Db.Get<PurchaseDoc>("purchases", "tokhash1");
        Assert.NotEqual(s.pid, p.playerId); Assert.True(p.retainUntilUnixMs > h.X.NowMs);
        Assert.Empty(await new DataLifecycleService(h.X).PurgeAccount(s.pid)); // idempotent
        Assert.NotEmpty(await h.X.Db.List<Dictionary<string, int>>("ledgerDaily"));  // aggregated
        // tax retention end → row deleted
        h.Advance(TimeSpan.FromDays(h.X.Opt.TaxRetentionDays + 1));
        Assert.Equal(1, (await h.Job("/internal/jobs/retention"))["purchasesExpired"].GetInt32());
        Assert.Null(await h.X.Db.Get<PurchaseDoc>("purchases", "tokhash1"));
    }

    [Fact]
    public async Task Purge_DetachesTop100Runs()
    {
        await using var h = await Harness.Start();
        var s = await h.Linked();
        await h.RankedRun(s, 2, 2000);
        var lc = new DataLifecycleService(h.X);
        var counts = await lc.PurgeAccount(s.pid);
        Assert.True(counts.GetValueOrDefault("runs_tombstoned") >= 1);
        Assert.All(await h.X.Db.List<RunDoc>("runs"), r => Assert.StartsWith("tomb_", r.playerId));
        Assert.DoesNotContain(await h.X.Db.List<BoardEntryDoc>("boards"), e => e.playerId == s.pid);
    }

    [Fact]
    public async Task Retention_ClearsInputLogsAfter90Days()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        await h.Post("/v1/score", Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4), s);
        h.Advance(TimeSpan.FromDays(89));
        Assert.Equal(0, (await h.Job("/internal/jobs/retention"))["inputLogsCleared"].GetInt32());
        h.Advance(TimeSpan.FromDays(2));
        Assert.Equal(1, (await h.Job("/internal/jobs/retention"))["inputLogsCleared"].GetInt32());
        var run = (await h.X.Db.List<RunDoc>("runs")).Single();
        Assert.True(run.inputsPurged); Assert.Empty(run.campaign.inputs);
    }

    [Fact]
    public async Task GuestInactivity_WarnsAt335_DeletesAt365_ActivityResets()
    {
        await using var h = await Harness.Start();
        var a = await h.Guest(); var b = await h.Guest();
        h.Advance(TimeSpan.FromDays(300));
        await new AuthService(h.X).KeepActive(b.pid); // what /v1/account/keep-active (and any authenticated call) does on day 300
        h.Advance(TimeSpan.FromDays(36));
        Assert.Equal(1, (await h.Job("/internal/jobs/guest-inactivity"))["warned"].GetInt32());
        Assert.NotEqual(0, (await h.X.Db.Get<AccountDoc>("accounts", a.pid)).deletionWarningAtUnixMs);
        h.Advance(TimeSpan.FromDays(30));
        Assert.Equal(1, (await h.Job("/internal/jobs/guest-inactivity"))["scheduled"].GetInt32());
        Assert.Equal("deleted_pending", (await h.X.Db.Get<AccountDoc>("accounts", a.pid)).status);
        Assert.Equal("active", (await h.X.Db.Get<AccountDoc>("accounts", b.pid)).status);
        await h.Job("/internal/jobs/purge");
        Assert.Null(await h.X.Db.Get<AccountDoc>("accounts", a.pid));
    }

    // ---------------- B6: web deletion ----------------
    [Fact]
    public async Task WebDeletion_EmailCode_CaptchaAndGenericAnswers()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        await h.Post("/v1/auth/email/register", new { email = "w@example.com", password = "correct horse 1" }, s);
        await h.Post("/v1/auth/email/verify", new { email = "w@example.com", code = h.Email.Sent.Last().body.Split(' ').Last() }, s);
        Assert.Equal("CAPTCHA_FAILED", (await h.Post("/v1/web/deletion/request", new { email = "w@example.com" }, null)).Code);
        var unknown = await h.Post("/v1/web/deletion/request", new { email = "nobody@example.com", captchaToken = "ok" }, null);
        var known = await h.Post("/v1/web/deletion/request", new { email = "W@example.com", captchaToken = "ok" }, null);
        Assert.Equal(unknown.json.GetRawText(), known.json.GetRawText()); // no account enumeration
        var code = h.Email.Sent.Last().body.Split(' ').Last();
        Assert.Equal("INVALID_CODE", (await h.Post("/v1/web/deletion/confirm", new { email = "w@example.com", code = "000000x" }, null)).Code);
        var ok = await h.Post("/v1/web/deletion/confirm", new { email = "w@example.com", code }, null);
        Assert.Equal("scheduled", ok["status"].GetString());
        Assert.True(ok["purgeAt"].GetInt64() - h.X.NowMs / 1000 <= 30 * 86400);
        Assert.Equal("INVALID_CODE", (await h.Post("/v1/web/deletion/confirm", new { email = "w@example.com", code }, null)).Code); // single use
        h.Advance(TimeSpan.FromDays(8)); await h.Job("/internal/jobs/purge");
        Assert.Null(await h.X.Db.Get<AccountDoc>("accounts", s.pid));
    }

    [Fact]
    public async Task WebDeletion_RateLimited_AndProviderPath()
    {
        await using var h = await Harness.Start();
        var codes = new List<string>();
        for (int i = 0; i < 4; i++) codes.Add((await h.Post("/v1/web/deletion/request", new { email = "rl@example.com", captchaToken = "ok" }, null)).Code);
        Assert.Contains("RATE_LIMITED", codes);
        var l = await h.Linked("g-web");
        Assert.Equal("BAD_PROVIDER", (await h.Post("/v1/web/deletion/provider", new { captchaToken = "ok", credential = new { provider = "Email", token = "x" } }, null)).Code);
    }

    // ---------------- Economy v1 ----------------
    static async Task<int> Gems(Harness h, Session s) => (await h.Wallet(s)).gems;

    [Fact]
    public async Task Economy_EachGrantSource_PaysOnce()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest(); var e = new EconomyService(h.X); var c = h.X.Economy;
        async Task Expect(Func<Task<bool>> grant, int gems)
        {
            var before = await Gems(h, s);
            Assert.True(await grant()); Assert.Equal(before + gems, await Gems(h, s));
            Assert.False(await grant()); Assert.Equal(before + gems, await Gems(h, s)); // idempotency key
        }
        await Expect(() => e.Day7Chest(s.pid, "w1"), 10);
        await Expect(() => e.TitanCompletion(s.pid, "2026-10-16"), 3);
        await Expect(() => e.TitanTop10(s.pid, "2026-10-16"), 5);
        await Expect(() => e.SeasonTier(s.pid, "s1", 5), 10);
        Assert.False(await e.SeasonTier(s.pid, "s1", 4));
        foreach (var a in c.achievements) await Expect(() => e.Achievement(s.pid, a.id), a.gems);
        await Expect(() => e.GrantGems(s.pid, c.guardianFirstClearGems, "guardian_first_clear", "c1_boss"), 25);
        foreach (var m in c.chapterStarMilestones) await Expect(() => e.GrantGems(s.pid, m.gems, "chapter_stars", $"c1:{m.stars}"), m.gems);
        Assert.Equal(new[] { 5, 10, 20 }, c.chapterStarMilestones.Select(m => m.gems));
        var ledger = await h.X.Db.List<LedgerEntry>("ledger", s.pid + ":");
        Assert.All(ledger, l => Assert.StartsWith("gems:", l.reason));
    }

    [Fact]
    public async Task Economy_EventWeek_ClearedAndThreeStar()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest(); var e = new EconomyService(h.X);
        var lv = new[] { "e1", "e2", "e3", "e4", "e5" };
        await e.OnEventProgress(s.pid, "wk1", lv, lv.ToDictionary(l => l, _ => 1));
        Assert.Equal(15, await Gems(h, s));
        await e.OnEventProgress(s.pid, "wk1", lv, lv.ToDictionary(l => l, _ => 3));
        Assert.Equal(15 + 10 + 25, await Gems(h, s)); // +3★ bonus +event_week_perfect achievement
        await e.OnEventProgress(s.pid, "wk1", lv, lv.ToDictionary(l => l, _ => 3));
        Assert.Equal(50, await Gems(h, s));
    }

    [Fact]
    public async Task Economy_SeasonUnlock450_InsufficientAndDebt()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest(); var e = new EconomyService(h.X);
        await e.GrantGems(s.pid, 449, "test", "a");
        Assert.Equal("INSUFFICIENT_FUNDS", (await h.Post("/v1/season/premium/unlock", new { }, s)).Code);
        await e.GrantGems(s.pid, 1, "test", "b");
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/season/premium/unlock", new { }, s)).status);
        Assert.Equal(0, await Gems(h, s));
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/season/premium/unlock", new { }, s)).status); // idempotent, no second charge
        Assert.Equal(0, await Gems(h, s));
        var d = await h.Guest();
        var wd = await new WalletService(h.X).Get(d.pid); wd.gems = -5; await h.X.Db.Put("wallets", d.pid, wd); // refund gem debt
        Assert.Equal("GEM_DEBT_LOCKED", (await h.Post("/v1/season/premium/unlock", new { }, d)).Code);
    }

    [Fact]
    public async Task Iap_DisabledByDefault_VerifyFeatureDisabled_RtdnIgnored()
    {
        await using var h = await Harness.Start(o => o.IapEnabled = false);
        var s = await h.Guest();
        Assert.Equal("FEATURE_DISABLED", (await h.Verify(s, "skin_brass_baron", "t")).Code);
        Assert.False(new ServerOptions().IapEnabled);
    }

    // ---------------- ENABLE_GEM_PACKS ----------------
    [Fact]
    public async Task GemPacks_OffByDefault_RejectedAndNothingGranted_ServedInFlags()
    {
        await using var h = await Harness.Start(o => o.EnableGemPacks = false);
        Assert.False(new ServerOptions().EnableGemPacks);
        Assert.False((await h.Get("/v1/config/flags", null))["enableGemPacks"].GetBoolean());
        var s = await h.Guest();
        foreach (var sku in new[] { "gems_small", "gems_large" })
        {
            var r = await h.Verify(s, sku, h.PlayPurchase(s, sku));
            Assert.Equal(HttpStatusCode.Forbidden, r.status); Assert.Equal("GEM_PACKS_DISABLED", r.Code);
        }
        Assert.Equal(0, await Gems(h, s));
        Assert.Empty(await h.X.Db.List<PurchaseDoc>("purchases"));
    }

    [Fact]
    public async Task GemPacks_On_NotBlockedByFlag()
    {
        await using var h = await Harness.Start(o => o.EnableGemPacks = true);
        Assert.True((await h.Get("/v1/config/flags", null))["enableGemPacks"].GetBoolean());
        var s = await h.Guest();
        Assert.NotEqual("GEM_PACKS_DISABLED", (await h.Verify(s, "gems_small", h.PlayPurchase(s, "gems_small"))).Code);
    }

    // ---------------- Physics PR #6 ----------------
    [Fact]
    public void Ticks_InputsAtTickNApplyBeforeStepN_InListOrder()
    {
        var ev = new List<InputEvent> { new() { tick = 0, type = "aim" }, new() { tick = 0, type = "power" }, new() { tick = 0, type = "fire" }, new() { tick = 2, type = "settle" } };
        var log = new List<string>();
        InputRules.Drive(ev, e => log.Add(e.type + "@" + e.tick), t => log.Add("step" + t));
        Assert.Equal(new[] { "aim@0", "power@0", "fire@0", "step0", "step1", "settle@2", "step2" }, log);
        // settle on the fire tick is impossible: fire@N applies before step N, settle is observed after it
        var same = new List<InputEvent> { new() { tick = 5, type = "fire" }, new() { tick = 5, type = "settle" } };
        Assert.NotNull(InputRules.Check(same, 1, 0));
        var back = new List<InputEvent> { new() { tick = 5, type = "aim" }, new() { tick = 4, type = "fire" }, new() { tick = 9, type = "settle" } };
        Assert.NotNull(InputRules.Check(back, 1, 0));
    }

    [Fact]
    public async Task Seed_FromAttemptToken_ReachesReplay()
    {
        await using var h = await Harness.Start(o => o.AllowClientRunSummary = false);
        var s = await h.Guest();
        var att = await h.Post("/v1/campaign/attempts", new { levelId = "c1_l01", count = 1 }, s);
        var a = att["items"][0];
        long seed = a.GetProperty("seed").GetInt64();
        var tok = a.GetProperty("attemptToken").GetString();
        Assert.Equal(seed, (long)h.X.Tokens.Verify("attempt", tok, out _)["seed"]);
        RunRecordView seen = null;
        h.Replay.Fn = v => { seen = v; return new ReplayResult { won = true, destroyedRatio = 0.9f, shotsLeft = 1 }; };
        await h.Post("/v1/score", Harness.Win("c1_l01", tok, 1, 4), s);
        await h.Job("/internal/jobs/replay");
        Assert.NotNull(seen); Assert.Equal(seed, seen.seed); Assert.Equal(50, seen.physicsHz);
    }
}
