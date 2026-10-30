using System.Net;
using System.Text.Json;
using Crashverse.Backend;
using Crashverse.Server.Domain;
using Crashverse.Server.Infra;
using AuthResponse = Crashverse.Server.Domain.AuthResponse;

namespace Crashverse.Server.Tests;

/// <summary>Developer ⇄ Backend interface agreement (2026-10-16): wire shapes, new account/privacy endpoints.</summary>
public class InterfaceAgreementTests
{
    static readonly JsonSerializerOptions J = new() { IncludeFields = true };

    // ---------------- Inputs / SubmitScore ----------------
    [Fact]
    public async Task SubmitScore_AgreedShape_IsAccepted_LevelAndReportFromToken()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var begin = await h.Post("/v1/attempts", new { mode = "campaign", levelId = "c1_l01", count = 1 }, s); // BeginAttempt
        Assert.Equal(HttpStatusCode.OK, begin.status);
        var att = begin["items"][0];
        Assert.Equal(JsonValueKind.Number, att.GetProperty("seed").ValueKind); att.GetProperty("seed").GetInt64(); // long seed
        var tok = att.GetProperty("attemptToken").GetString();
        // raw agreed JSON: SubmitScore {attemptToken, inputs[{tick,type,x,y}], clientScoreBp, buildHash} (+ run summary until the replay worker exists)
        var inputs = Enumerable.Range(1, 3).SelectMany(i => Harness.In(i * 200)).Select(e => new { e.tick, e.type, e.x, e.y }).ToArray();
        var body = new { attemptToken = tok, inputs, clientScoreBp = 4321, buildHash = SimVersions.Current, won = true, destroyedRatio = 0.9f, shotsLeft = 1, bestCombo = 3 };
        var r = await h.Post("/v1/score", body, s);
        Assert.Equal(HttpStatusCode.OK, r.status);
        Assert.True(r["accepted"].GetBoolean()); Assert.Equal("pending", r["status"].GetString());
        Assert.True(r["serverScoreBp"].GetInt32() > 0);
        var again = await h.Post("/v1/score", body, s); // same attempt token = same report → idempotent, not double-counted
        Assert.Equal("Already synced", again["message"].GetString());
        var run = (await h.X.Db.List<RunDoc>("runs")).Single();
        Assert.Equal(4321, run.clientScore); Assert.Equal(SimVersions.Current, run.simVersion); Assert.Equal(12, run.campaign.inputs.Count);
    }

    [Fact]
    public async Task SubmitScore_WrongBuildHash_IsRejected_AndNoSeparateInputLogEndpoint()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var rep = Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4); rep.simVersion = null; rep.buildHash = "other-build";
        var r = await h.Post("/v1/score", rep, s);
        Assert.False(r["accepted"].GetBoolean()); Assert.Equal("rejected", r["status"].GetString());
        foreach (var p in new[] { "/v1/attempts/input-log", "/v1/input-log", "/v1/score/inputs", "/v1/titan/ranked/inputs" })
            Assert.Equal(HttpStatusCode.NotFound, (await h.Post(p, new { }, s)).status);
    }

    [Fact]
    public void InputRules_FixedPoint_TypesOrderRangesAndPerShotCap()
    {
        var ok = Harness.In(200).Concat(Harness.In(400)).ToList();
        Assert.Null(InputRules.Check(ok, 2, 75));
        Assert.Equal("input log does not match shots used", InputRules.Check(ok, 3, 75));
        Assert.Equal("unknown input type", InputRules.Check(new() { new() { tick = 1, type = "teleport" } }, 0, 0));
        Assert.Equal("input ticks out of order", InputRules.Check(new() { new() { tick = 5, type = "aim" }, new() { tick = 4, type = "aim" } }, 0, 0));
        Assert.Equal("inputs out of range", InputRules.Check(new() { new() { tick = 1, type = "aim", x = 360_001 } }, 0, 0));
        Assert.Equal("inputs out of range", InputRules.Check(new() { new() { tick = 1, type = "power", x = 1001 } }, 0, 0));
        Assert.Equal("missing settle", InputRules.Check(new() { new() { tick = 1, type = "fire" } }, 1, 0));
        Assert.Equal("settle without shot", InputRules.Check(new() { new() { tick = 1, type = "settle" } }, 0, 0));
        var spam = Enumerable.Range(0, InputRules.MaxEventsPerShot + 1).Select(i => new InputEvent { tick = i, type = "aim" }).Concat(Harness.In(500)).ToList();
        Assert.Contains("too many input events", InputRules.Check(spam, 2, 0));
        Assert.NotEqual(InputRules.Hash(ok), InputRules.Hash(Harness.In(200).Concat(Harness.In(401)).ToList())); // receipts bind exact inputs
        Assert.Equal(1234, InputRecorder.ToFixed(1.2344f)); // client converts at capture; server only ever sees ints
    }

    [Fact]
    public async Task Titan_Checkpoint_TakesThatShotsEvents_TwoFiresMakesPractice()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var t = await h.StartRanked(s); var tok = t["token"].GetString(); int budget = t["shotBudget"].GetInt32();
        h.Advance(TimeSpan.FromSeconds(3));
        var two = Harness.In(10).Concat(Harness.In(400)).ToList();
        await h.Post("/v1/titan/ranked/checkpoint", new RankedCheckpointEx { token = tok, shotIndex = 1, damageScore = 100, shotsLeft = budget - 1, inputs = two }, s);
        var run = (await h.X.Db.List<RunDoc>("runs")).Single();
        Assert.True(run.practice); Assert.Equal("input log does not match shots used", run.practiceReason);
    }

    // ---------------- Auth ----------------
    [Fact]
    public async Task LinkCredential_ProviderAsString_AnyCase_IncludesFacebook()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        h.Facebook.Tokens["fb-cap"] = new ProviderIdentity { subject = "cap" };
        // exactly what the Unity client sends: StringEnumConverter → "Facebook"
        var json = "{\"provider\":\"Facebook\",\"token\":\"fb-cap\",\"codeVerifier\":null,\"redirectUri\":null,\"nonce\":null}";
        var r = await h.Post("/v1/account/link", json, s);
        Assert.Equal(HttpStatusCode.OK, r.status);
        Assert.Equal("facebook", r["identities"][0].GetString()); Assert.Equal(JsonValueKind.String, r["provider"].ValueKind);
        Assert.Equal("BAD_PROVIDER", (await h.Post("/v1/account/link", new { provider = "Apple", token = "a" }, s)).Code); // Apple: not offered at launch
        var si = await h.Post("/v1/auth/sign-in", new { provider = "facebook", token = "fb-cap", deviceInstallId = "d", ageToken = await h.AgeToken() }, null);
        Assert.Equal(s.pid, si["account"].GetProperty("playerId").GetString());
    }

    [Fact]
    public void EmailLogin_ReturnsAuthResponse_AndProvidersAreStrings()
    {
        Assert.Equal(typeof(Task<AuthResponse>), typeof(AuthService).GetMethod(nameof(AuthService.EmailLogin))!.ReturnType);
        Assert.Equal(new[] { "accessToken", "refreshToken", "account" }, typeof(AuthResponse).GetFields().Select(f => f.Name));
        Assert.Equal("{\"provider\":\"Facebook\"}", Newtonsoft.Json.JsonConvert.SerializeObject(new { provider = AuthProvider.Facebook })); // client side
    }

    // ---------------- Purchases ----------------
    [Fact]
    public async Task VerifyPurchase_StatusIsPendingVerifiedOrRejected_AlwaysWithWalletAndSeason()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        foreach (var (state, want) in new[] { (2, "pending"), (0, "verified"), (1, "rejected") })
        {
            var r = await h.Verify(s, "gems_small", h.PlayPurchase(s, "gems_small", state));
            Assert.Equal(HttpStatusCode.OK, r.status);
            Assert.Equal(want, r["status"].GetString());
            Assert.Equal(JsonValueKind.Object, r["wallet"].ValueKind); Assert.Equal(JsonValueKind.Object, r["season"].ValueKind);
        }
        Assert.Equal(100, (await h.Wallet(s)).gems);
    }

    // ---------------- Re-auth + deletion ----------------
    static async Task<string> Reauth(Harness h, Session s, object cred) { var r = await h.Post("/v1/auth/reauth", new { credential = cred }, s); Assert.Equal(HttpStatusCode.OK, r.status); return r["reauthToken"].GetString(); }

    [Fact]
    public async Task Reauth_IsVerified_ShortLived_AndBoundToThePlayer()
    {
        await using var h = await Harness.Start();
        var a = await h.Linked("ra"); var b = await h.Linked("rb");
        var r = await h.Post("/v1/auth/reauth", new { credential = new { provider = "facebook", token = "fb-ra" } }, a);
        Assert.Equal(HttpStatusCode.OK, r.status);
        Assert.Equal(Ctx.Unix(h.Clock.Now.AddMinutes(5)), r["expiresAt"].GetInt64()); // Unix seconds UTC
        Assert.Equal("REAUTH_FAILED", (await h.Post("/v1/auth/reauth", new { credential = new { provider = "facebook", token = "fb-rb" } }, a)).Code); // b's login
        Assert.Equal("REAUTH_FAILED", (await h.Post("/v1/auth/reauth", new { credential = new { provider = "facebook", token = "forged" } }, a)).Code);
        Assert.Equal("REAUTH_FAILED", (await h.Post("/v1/auth/reauth", new { credential = new { provider = "guest", token = a.refresh } }, a)).Code); // linked ≠ guest proof
        Assert.Equal("BAD_REQUEST", (await h.Post("/v1/auth/reauth", new { }, a)).Code);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Post("/v1/auth/reauth", new { credential = new { provider = "facebook", token = "fb-ra" } }, null)).status);
        var bt = await Reauth(h, b, new { provider = "facebook", token = "fb-rb" });
        Assert.Equal("REAUTH_REQUIRED", (await h.Post("/v1/account/delete", new { reauthToken = bt }, a)).Code); // another player's token
    }

    [Fact]
    public async Task Delete_RequiresFreshSingleUseReauth_SchedulesPurgeIn7Days()
    {
        await using var h = await Harness.Start();
        var s = await h.Linked("del");
        Assert.Equal("REAUTH_REQUIRED", (await h.Post("/v1/account/delete", new { }, s)).Code);
        var stale = await Reauth(h, s, new { provider = "facebook", token = "fb-del" });
        h.Advance(TimeSpan.FromMinutes(6));
        await h.Renew(s);
        var exp = await h.Post("/v1/account/delete", new { reauthToken = stale }, s);
        Assert.Equal(HttpStatusCode.Unauthorized, exp.status); Assert.Equal("REAUTH_REQUIRED", exp.Code);
        var tok = await Reauth(h, s, new { provider = "facebook", token = "fb-del" });
        var d = await h.Post("/v1/account/delete", new { reauthToken = tok }, s);
        Assert.Equal("scheduled", d["status"].GetString());
        Assert.Equal(Ctx.Unix(h.Clock.Now.AddDays(7)), d["purgeAt"].GetInt64());
        Assert.False(await h.X.Nonces.TryUse("reauth:" + (string)h.X.Tokens.Verify("reauth", tok, out _)["jti"], TimeSpan.FromMinutes(1))); // consumed: single use
    }

    [Fact]
    public async Task PendingDeletion_BlocksPlay_ShowsScheduled_CancelRestores_UntilPurge()
    {
        await using var h = await Harness.Start();
        var s = await h.Linked("cx");
        await h.Post("/v1/account/delete", new { reauthToken = await Reauth(h, s, new { provider = "facebook", token = "fb-cx" }) }, s);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Get("/v1/wallet", s)).status);         // no play while pending
        var acc = await h.Get("/v1/account", s);                                                 // but the app can show "cancel deletion" (A5)
        Assert.Equal("scheduled", acc["status"].GetString()); Assert.True(acc["purgeAt"].GetInt64() > 0);
        Assert.Equal("active", (await h.Post("/v1/account/delete/cancel", null, s))["status"].GetString());
        Assert.Equal(HttpStatusCode.OK, (await h.Get("/v1/wallet", s)).status);
        Assert.Equal("active", (await h.Get("/v1/account", s))["status"].GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Post("/v1/account/delete/cancel", null, null)).status);
        // after the grace period cancel is refused
        await h.Post("/v1/account/delete", new { reauthToken = await Reauth(h, s, new { provider = "facebook", token = "fb-cx" }) }, s);
        h.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1)); await h.Renew(s);
        Assert.Equal("ACCOUNT_PURGED", (await h.Post("/v1/account/delete/cancel", null, s)).Code);
    }

    [Fact]
    public async Task Guest_CanDelete_WithSessionAsProof_OrGuestReauth()
    {
        await using var h = await Harness.Start();
        var g = await h.Guest();
        Assert.Equal("scheduled", (await h.Post("/v1/account/delete", new { reauthToken = (string)null }, g))["status"].GetString()); // C1: client passes null
        var g2 = await h.Guest();
        var t = await Reauth(h, g2, new { provider = "guest", token = g2.refresh });
        Assert.Equal("scheduled", (await h.Post("/v1/account/delete", new { reauthToken = t }, g2))["status"].GetString());
        var g3 = await h.Guest();
        Assert.Equal("REAUTH_FAILED", (await h.Post("/v1/auth/reauth", new { credential = new { provider = "guest", token = g2.refresh } }, g3)).Code);
    }

    [Fact]
    public async Task Email_Reauth_UsesPassword()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        await h.Post("/v1/auth/email/register", new { email = "r@example.com", password = "correct horse 1" }, s);
        await h.Post("/v1/auth/email/verify", new { email = "r@example.com", code = h.Email.Sent.Last().body.Split(' ').Last() }, s);
        Assert.Equal("REAUTH_FAILED", (await h.Post("/v1/auth/reauth", new { credential = new { provider = "Email", token = "wrong password!" } }, s)).Code);
        await Reauth(h, s, new { provider = "Email", token = "correct horse 1" });
    }

    [Fact]
    public async Task KeepActive_ReturnsServerTime_AndRequiresBearer()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        h.Advance(TimeSpan.FromHours(3)); await h.Renew(s);
        var r = await h.Post("/v1/account/keep-active", null, s);
        Assert.Equal(Ctx.Unix(h.Clock.Now), r["lastActiveAt"].GetInt64());
        Assert.Equal(Ctx.UnixMs(h.Clock.Now), (await h.X.Db.Get<AccountDoc>("accounts", s.pid)).lastActiveUnixMs);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Post("/v1/account/keep-active", null, null)).status);
    }

    // ---------------- Age gate + parental consent ----------------
    [Theory]
    [InlineData(2014, 10, "US", "under13", true)]   // 12 (birthday assumed end of month)
    [InlineData(2013, 9, "US", "13to15", false)]    // 13
    [InlineData(2013, 10, "US", "under13", true)]   // turns 13 this month → still treated as 12
    [InlineData(2012, 1, "DE", "13to15", true)]     // 14 in Germany (consent age 16)
    [InlineData(2012, 1, "GB", "13to15", false)]
    [InlineData(2010, 1, "DE", "16to17", false)]
    [InlineData(2009, 1, "IN", "16to17", true)]     // 17 in India (DPDP: 18)
    [InlineData(1990, 5, "fr", "adult", false)]
    public async Task AgeGate_Bands_AndConsentByCountry_NoAuth(int y, int m, string c, string band, bool consent)
    {
        await using var h = await Harness.Start();
        var r = await h.Post("/v1/agegate", new { birthYear = y, birthMonth = m, country = c, deviceInstallId = "d" }, null);
        Assert.Equal(HttpStatusCode.OK, r.status);
        Assert.Equal(band, r["band"].GetString()); Assert.Equal(consent, r["requiresParentalConsent"].GetBoolean());
    }

    [Fact]
    public async Task AgeGate_RejectsBadInput_StoresNothing_IsRateLimited()
    {
        await using var h = await Harness.Start(o => o.AuthPerMinute = 2);
        Assert.Equal("BAD_REQUEST", (await h.Post("/v1/agegate", new { birthYear = 2010, birthMonth = 13, country = "US", deviceInstallId = "d" }, null)).Code);
        Assert.Equal("BAD_REQUEST", (await h.Post("/v1/agegate", new { birthYear = 2010, birthMonth = 1, country = "USA", deviceInstallId = "d" }, null)).Code);
        Assert.Equal("BAD_REQUEST", (await h.Post("/v1/agegate", new { birthYear = 2027, birthMonth = 1, country = "US", deviceInstallId = "d" }, null)).Code);
        Assert.Empty(await h.X.Db.List<AccountDoc>("accounts"));
        Assert.Empty(await h.X.Db.List<ConsentDoc>("consents"));
        Resp last = null; for (int i = 0; i < 3; i++) last = await h.Post("/v1/agegate", new { birthYear = 2000, birthMonth = 1, country = "US", deviceInstallId = "d" }, null);
        Assert.Equal(HttpStatusCode.TooManyRequests, last.status);
    }

    [Fact]
    public async Task ParentalConsent_Request_Status_Decide_ThroughFakeMailer()
    {
        await using var h = await Harness.Start();
        var s = await h.Guest();
        var mail = (FakeParentalConsentMailer)h.X.ConsentMailer;
        Assert.Equal("none", (await h.Get("/v1/consent/parental/status", s))["status"].GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Post("/v1/consent/parental/request", new { parentEmail = "p@example.com" }, null)).status);
        Assert.Equal("BAD_EMAIL", (await h.Post("/v1/consent/parental/request", new { parentEmail = "nope" }, s)).Code);
        var r = await h.Post("/v1/consent/parental/request", new { parentEmail = "Parent@Example.com" }, s);
        Assert.Equal("pending", r["status"].GetString()); var rid = r["requestId"].GetString();
        Assert.Equal(("parent@example.com", rid), (mail.Sent.Single().to, mail.Sent.Single().requestId));
        Assert.DoesNotContain("parent@", JsonSerializer.Serialize(await h.X.Db.Get<ConsentDoc>("consents", s.pid), J)); // only a hash is stored
        Assert.Equal("pending", (await h.Get("/v1/consent/parental/status", s))["status"].GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Send(HttpMethod.Post, "/internal/consent/decide", new { token = mail.Sent[0].token, grant = true })).status); // push auth
        var d = await h.Send(HttpMethod.Post, "/internal/consent/decide", new { token = mail.Sent[0].token, grant = true }, bearer: "fake-pubsub-oidc");
        Assert.Equal("granted", d["status"].GetString());
        Assert.Equal("granted", (await h.Get("/v1/consent/parental/status", s))["status"].GetString());
        Assert.Equal("CONSENT_NOT_PENDING", (await h.Send(HttpMethod.Post, "/internal/consent/decide", new { token = mail.Sent[0].token, grant = false }, bearer: "fake-pubsub-oidc")).Code);
        var s2 = await h.Guest();
        await h.Post("/v1/consent/parental/request", new { parentEmail = "p2@example.com" }, s2);
        await h.Send(HttpMethod.Post, "/internal/consent/decide", new { token = mail.Sent[1].token, grant = false }, bearer: "fake-pubsub-oidc");
        Assert.Equal("denied", (await h.Get("/v1/consent/parental/status", s2))["status"].GetString());
        for (int i = 0; i < 2; i++) await h.Post("/v1/consent/parental/request", new { parentEmail = "p2@example.com" }, s2);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await h.Post("/v1/consent/parental/request", new { parentEmail = "p2@example.com" }, s2)).status); // 3/day
    }

    // ---------------- Profile visibility ----------------
    [Fact]
    public async Task HideName_AppliesToLeaderboardsAndFriends_ButNotToMe()
    {
        await using var h = await Harness.Start();
        var a = await h.Linked(); var b = await h.Linked();
        await h.Post("/v1/friends", new { friendCode = b.friendCode }, a);
        h.Replay.Fn = r => new ReplayResult { damageScore = r.clientScore };
        await h.RankedRun(b, 1, 1000);
        await h.Job("/internal/jobs/replay");
        string NameOf(Resp p, string pid) => p["top"].EnumerateArray().Single(e => e.GetProperty("playerId").GetString() == pid).GetProperty("name").GetString();
        Assert.Equal("Facebook Wrecker", NameOf(await h.Get("/v1/leaderboards/0", a), b.pid));
        var v = await h.Post("/profile/visibility".Insert(0, "/v1"), new { hideName = true }, b);
        Assert.True(v["hideName"].GetBoolean());
        var hidden = NameOf(await h.Get("/v1/leaderboards/0", a), b.pid);
        Assert.StartsWith("Wrecker #", hidden); Assert.DoesNotContain("Facebook", hidden);
        Assert.Equal(hidden, (await h.Get("/v1/friends", a))["items"][0].GetProperty("name").GetString());
        Assert.Equal("Facebook Wrecker", NameOf(await h.Get("/v1/leaderboards/0", b), b.pid)); // the player still sees their own name
        Assert.True((await h.Get("/v1/account", b))["hideName"].GetBoolean());
        Assert.False((await h.Post("/v1/profile/visibility", new { hideName = false }, b))["hideName"].GetBoolean());
        Assert.Equal("Facebook Wrecker", NameOf(await h.Get("/v1/leaderboards/0", a), b.pid));
    }
}
