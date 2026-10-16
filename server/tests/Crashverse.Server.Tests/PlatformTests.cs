using Crashverse.Backend;

namespace Crashverse.Server.Tests;

public class AuthTests
{
    [Fact]
    public async Task Refresh_Rotates_AndReuseRevokesFamily()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest(); var old = s.refresh;
        await h.Renew(s);
        Assert.Equal(HttpStatusCode.OK, (await h.Get("/v1/account", s)).status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Post("/v1/auth/refresh", new { refreshToken = old }, null)).status); // stolen old token
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Get("/v1/account", s)).status);                               // whole family revoked
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Post("/v1/auth/refresh", new { refreshToken = s.refresh }, null)).status);
    }

    [Fact]
    public async Task GuestIds_Are128BitServerIssued()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal(16, B64.FromUrl(s.pid).Length);
        Assert.Equal(8, s.friendCode.Length);
    }

    [Fact]
    public async Task Email_RegisterVerifyLoginReset()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        Assert.Equal("WEAK_PASSWORD", (await h.Post("/v1/auth/email/register", new { email = "A@Example.com", password = "short" }, s)).Code);
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/auth/email/register", new { email = "A@Example.com", password = "correct horse 1" }, s)).status);
        var code = h.Email.Sent.Last().body.Split(' ').Last();
        Assert.Equal("INVALID_CREDENTIALS", (await h.Post("/v1/auth/email/login", new { email = "a@example.com", password = "correct horse 1" }, null)).Code); // not verified yet
        Assert.Equal("INVALID_CODE", (await h.Post("/v1/auth/email/verify", new { email = "a@example.com", code = "000000x" }, s)).Code);
        var v = await h.Post("/v1/auth/email/verify", new { email = "a@example.com", code }, s);
        Assert.True(v["linked"].GetBoolean());
        var login = await h.Post("/v1/auth/email/login", new { email = "a@example.com", password = "correct horse 1" }, null);
        Assert.Equal(s.pid, login["account"].GetProperty("playerId").GetString());
        var bad = await h.Post("/v1/auth/email/login", new { email = "a@example.com", password = "wrong password!" }, null);
        var none = await h.Post("/v1/auth/email/login", new { email = "nobody@example.com", password = "wrong password!" }, null);
        Assert.Equal(bad.Code, none.Code); // no account enumeration
        var r1 = await h.Post("/v1/auth/email/reset-request", new { email = "a@example.com" }, null);
        var r2 = await h.Post("/v1/auth/email/reset-request", new { email = "nobody@example.com" }, null);
        Assert.Equal(r1["message"].GetString(), r2["message"].GetString());
        var rc = h.Email.Sent.Last().body.Split(' ').Last();
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/auth/email/reset", new { email = "a@example.com", code = rc, newPassword = "battery staple 2" }, null)).status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Get("/v1/account", s)).status); // sessions revoked on reset
        Assert.Equal(HttpStatusCode.OK, (await h.Post("/v1/auth/email/login", new { email = "a@example.com", password = "battery staple 2" }, null)).status);
        Assert.Equal("INVALID_CODE", (await h.Post("/v1/auth/email/reset", new { email = "a@example.com", code = rc, newPassword = "battery staple 3" }, null)).Code); // single use
    }

    [Fact]
    public async Task SignIn_WithLinkedProvider_OnNewDevice()
    {
        await using var h = await Harness.Start();
        var s = await h.Linked("dev2");
        var r = await h.Post("/v1/auth/sign-in", new { provider = "facebook", token = "fb-dev2", deviceInstallId = "d2" }, null);
        Assert.Equal(s.pid, r["account"].GetProperty("playerId").GetString());
        Assert.Equal("PROVIDER_TOKEN_INVALID", (await h.Post("/v1/auth/sign-in", new { provider = "facebook", token = "nope" }, null)).Code);
    }
}

public class AbuseTests
{
    [Fact]
    public async Task UnauthenticatedGuest_IsStrictlyLimitedPerIp()
    {
        await using var h = await Harness.Start(o => o.GuestPerIpPerHour = 10);
        for (int i = 0; i < 10; i++) await h.Guest();
        var r = await h.Post("/v1/auth/guest", new { deviceInstallId = "d" }, null);
        Assert.Equal(HttpStatusCode.TooManyRequests, r.status); Assert.Equal("RATE_LIMITED", r.Code);
        Assert.True(r.raw.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task EmailLogin_IsLimitedPerMinute()
    {
        await using var h = await Harness.Start(o => o.AuthPerMinute = 5);
        for (int i = 0; i < 5; i++) await h.Post("/v1/auth/email/login", new { email = "x@y.z", password = "whatever123" }, null);
        Assert.Equal("RATE_LIMITED", (await h.Post("/v1/auth/email/login", new { email = "x@y.z", password = "whatever123" }, null)).Code);
    }

    [Fact]
    public async Task RateLimitAndNonceCache_AreSharedAcrossInstances()
    {
        var store = new InMemoryDocStore();
        await using var i1 = await Harness.Start(o => o.GuestPerIpPerHour = 4, store);
        await using var i2 = await Harness.Start(o => o.GuestPerIpPerHour = 4, store);
        await i1.Guest(); await i2.Guest(); await i1.Guest(); await i2.Guest();
        Assert.Equal("RATE_LIMITED", (await i1.Post("/v1/auth/guest", new { deviceInstallId = "d" }, null)).Code);
        Assert.Equal("RATE_LIMITED", (await i2.Post("/v1/auth/guest", new { deviceInstallId = "d" }, null)).Code);
        Assert.True(await i1.X.Nonces.TryUse("n1", TimeSpan.FromMinutes(10)));
        Assert.False(await i2.X.Nonces.TryUse("n1", TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task OversizedBody_IsRejected()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var r = await h.Post("/v1/friends", new { friendCode = new string('A', 70_000) }, s);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.status);
    }
}

public class LiveOpsTests
{
    [Fact]
    public async Task ServerTime_EndpointAndHeader()
    {
        await using var h = await Harness.Start();
        var r = await h.Get("/v1/time", null);
        Assert.Equal(new DateTimeOffset(Harness.T0).ToUnixTimeMilliseconds(), r["serverTimeUnixMs"].GetInt64());
        Assert.True(r.raw.Headers.Contains("X-Server-Time"));
    }

    [Fact]
    public async Task DailyReward_ResetsAtMidnightUtc()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        h.At(new DateTime(2026, 10, 16, 23, 59, 59)); await h.Renew(s);
        Assert.Equal(100, (await h.Post("/v1/daily-reward/claim", null, s))["coins"].GetInt32());
        Assert.Equal("ALREADY_CLAIMED", (await h.Post("/v1/daily-reward/claim", null, s)).Code);
        h.At(new DateTime(2026, 10, 17, 0, 0, 0));
        var st = await h.Get("/v1/daily-reward", s);
        Assert.True(st["canClaimToday"].GetBoolean());
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), st["nextResetUnixUtc"].GetInt64());
        Assert.Equal(1, (await h.Post("/v1/daily-reward/claim", null, s))["extraCharges"].GetInt32());
    }

    [Fact]
    public async Task ConsumeCharge_IsIdempotent()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        await h.X.Db.Put("wallets", s.pid, new WalletDoc { playerId = s.pid, extraCharges = 2 });
        Assert.True((await h.Post("/v1/wallet/consume-charge", new { }, s, idem: "k1"))["consumed"].GetBoolean());
        Assert.True((await h.Post("/v1/wallet/consume-charge", new { }, s, idem: "k1"))["consumed"].GetBoolean());
        Assert.Equal(1, (await h.Wallet(s)).extraCharges);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", (await h.Post("/v1/wallet/consume-charge", new { }, s)).Code);
    }

    [Fact]
    public async Task FeatureFlags_ReportXDisabled_AndAccountListsIdentities()
    {
        await using var h = await Harness.Start();
        Assert.False((await h.Get("/v1/config/flags", null))["xLoginEnabled"].GetBoolean());
        var s = await h.Linked();
        Assert.Equal("facebook", (await h.Get("/v1/account", s))["identities"][0].GetString());
    }

    [Fact]
    public async Task ListResponses_AreWrapped()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        foreach (var p in new[] { "/v1/inbox", "/v1/wallet/ledger", "/v1/purchases", "/v1/friends", "/v1/inventory" })
            Assert.Equal(System.Text.Json.JsonValueKind.Array, (await h.Get(p, s))["items"].ValueKind);
    }

    [Fact]
    public async Task SeasonRollover_SendsUnclaimedTiersToInbox()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        await h.X.Db.Put("seasonProgress", s.pid + ":s1_wrecking_frontier", new SeasonProgressDoc { seasonId = "s1_wrecking_frontier", xp = 250 });
        h.At(new DateTime(2026, 11, 9, 0, 0, 0)); await h.Renew(s); // season 2 starts Monday 00:00 UTC (SG-18)
        Assert.Equal("s1_wrecking_frontier_r1", (await h.Get("/v1/season", s))["seasonId"].GetString());
        Assert.Equal(2, (await h.Get("/v1/inbox", s))["items"].GetArrayLength());
    }

    [Fact]
    public async Task FriendCodes_AreMutual_AndFilterBoards()
    {
        await using var h = await Harness.Start();
        var a = await h.Linked(); var b = await h.Linked(); var c = await h.Linked();
        Assert.Equal("UNKNOWN_FRIEND_CODE", (await h.Post("/v1/friends", new { friendCode = "ZZZZZZZZ" }, a)).Code);
        var l = await h.Post("/v1/friends", new { friendCode = b.friendCode }, a);
        Assert.False(l["items"][0].GetProperty("mutual").GetBoolean());
        await h.Post("/v1/friends", new { friendCode = a.friendCode }, b);
        Assert.True((await h.Get("/v1/friends", a))["items"][0].GetProperty("mutual").GetBoolean());
        h.Replay.Fn = r => new ReplayResult { damageScore = r.clientScore };
        await h.RankedRun(b, 1, 1000); await h.RankedRun(c, 1, 2000);
        await h.Job("/internal/jobs/replay"); // top-N entries need a replay before they show
        var page = await h.Get("/v1/leaderboards/0?friends=true", a);
        Assert.Equal(new[] { b.pid }, page["top"].EnumerateArray().Select(e => e.GetProperty("playerId").GetString()));
        Assert.Equal(2, (await h.Get("/v1/leaderboards/0", a))["top"].GetArrayLength());
    }

    [Fact]
    public async Task Inventory_ShowsSource()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var t = h.PlayPurchase(s, "gems_small"); await h.Verify(s, "gems_small", t);
        await h.Post("/v1/store/buy", new { sku = "emote_salute" }, s);
        var inv = await h.Get("/v1/inventory", s);
        Assert.Equal("gems", inv["items"][0].GetProperty("source").GetString());
    }

    [Fact]
    public void SharedRules_MatchClientFormulas()
    {
        Assert.Equal(873 + 1000 + 100 + 1000, Crashverse.Meta.CampaignScore.Compute(true, 0.8739f, 2, 2, true)); // BUG-05
        Assert.True(Crashverse.Meta.WinRule.Meets(0.85f, 0.85f)); Assert.False(Crashverse.Meta.WinRule.Meets(0.8499f, 0.85f));
        var t = new Crashverse.Data.TitanTemplates { maxDamageScore = 10000, perShotLeftBonus = 500 };
        Assert.Equal(10000 + 1000, Rules.TitanScore(12000, 2, true, t)); Assert.Equal(4000, Rules.TitanScore(4000, 2, false, t));
    }
}
