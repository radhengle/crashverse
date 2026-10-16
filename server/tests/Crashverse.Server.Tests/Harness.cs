using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crashverse.Data;
using InputEvent = Crashverse.Backend.InputEvent;
using Crashverse.Server;
using Crashverse.Server.Domain;
using Crashverse.Server.Infra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Crashverse.Server.Tests;

public sealed class Session { public string pid; public string access; public string refresh; public string friendCode; }
public sealed class Resp
{
    public HttpStatusCode status; public JsonElement json; public HttpResponseMessage raw;
    public string Code => json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var e) ? e.GetProperty("code").GetString() : null;
    public T As<T>() => json.Deserialize<T>(ServerApp.Json);
    public JsonElement this[string k] => json.GetProperty(k);
}

public sealed class Harness : IAsyncDisposable
{
    public static readonly DateTime T0 = new(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc); // season s1 week 0
    public Ctx X; public FakeClock Clock; public HttpClient Http; public WebApplication App;
    public FakeGoogleVerifier Google => (FakeGoogleVerifier)X.Google;
    public FakeFacebookVerifier Facebook => (FakeFacebookVerifier)X.Facebook;
    public FakeXClient XClient => (FakeXClient)X.X;
    public FakeEmailSender Email => (FakeEmailSender)X.Email;
    public FakePlayApi Play => (FakePlayApi)X.Play;
    public ScriptedReplayWorker Replay = new();
    public FixedSampler Sampler = new() { Value = 0.99 }; // not randomly sampled unless a test says so

    public static async Task<Harness> Start(Action<ServerOptions> cfg = null, IDocStore shared = null)
    {
        var h = new Harness { Clock = new FakeClock(T0) };
        var opt = new ServerOptions { Env = CrashverseEnv.Test, IapEnabled = true, EnableGemPacks = true, AllowClientRunSummary = true, RankedPublicBoardEnabled = true, PasswordIterations = 1000, AccountPerMinute = 10_000, GuestPerIpPerHour = 1000, GuestPerDevicePerHour = 1000, AuthPerMinute = 1000 };
        cfg?.Invoke(opt);
        h.X = ServerApp.DevContext(opt, h.Clock);
        if (shared != null) { h.X.Db = shared; h.X.Nonces = new StoreNonceCache(shared, h.Clock); h.X.Limits = new StoreRateLimiter(shared, h.Clock); }
        h.X.Replay = h.Replay; h.X.Sampler = h.Sampler; ((FakePushAuthVerifier)h.X.PushAuth).Expected = "fake-pubsub-oidc";
        h.App = ServerApp.Build(h.X, b => b.WebHost.UseTestServer());
        await h.App.StartAsync();
        h.Http = h.App.GetTestClient();
        return h;
    }

    public async ValueTask DisposeAsync() { await App.StopAsync(); await App.DisposeAsync(); }

    public void Advance(TimeSpan t) => Clock.Now = Clock.Now.Add(t);
    public void At(DateTime utc) => Clock.Now = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

    public async Task<Resp> Send(HttpMethod m, string path, object body = null, Session s = null, string nonce = null, string idem = null, string bearer = null, Dictionary<string, string> headers = null)
    {
        var req = new HttpRequestMessage(m, path);
        if (body != null) req.Content = new StringContent(body is string str ? str : JsonSerializer.Serialize(body, body.GetType(), ServerApp.Json), System.Text.Encoding.UTF8, "application/json");
        if (s != null) req.Headers.Authorization = new("Bearer", s.access);
        if (bearer != null) req.Headers.Authorization = new("Bearer", bearer);
        if (m != HttpMethod.Get) req.Headers.Add("X-Nonce", nonce ?? Guid.NewGuid().ToString("N"));
        if (idem != null) req.Headers.Add("Idempotency-Key", idem);
        if (headers != null) foreach (var (k, v) in headers) req.Headers.Add(k, v);
        var r = await Http.SendAsync(req);
        var txt = await r.Content.ReadAsStringAsync();
        return new Resp { status = r.StatusCode, raw = r, json = string.IsNullOrEmpty(txt) ? default : JsonDocument.Parse(txt).RootElement.Clone() };
    }
    public Task<Resp> Get(string p, Session s) => Send(HttpMethod.Get, p, null, s);
    public Task<Resp> Post(string p, object b, Session s, string nonce = null, string idem = null) => Send(HttpMethod.Post, p, b, s, nonce, idem);

    public async Task<Session> Guest(string device = null)
    {
        var r = await Post("/v1/auth/guest", new { deviceInstallId = device ?? Guid.NewGuid().ToString("N"), ageToken = await AgeToken() }, null);
        Assert.Equal(HttpStatusCode.OK, r.status);
        return new Session { pid = r["account"].GetProperty("playerId").GetString(), access = r["accessToken"].GetString(), refresh = r["refreshToken"].GetString(),
            friendCode = r["account"].GetProperty("friendCode").GetString() };
    }

    /// <summary>Passes the age gate (adult, US by default) and returns the signed age token account creation needs.</summary>
    public async Task<string> AgeToken(int birthYear = 1990, int birthMonth = 1, string country = "US")
    {
        var r = await Post("/v1/agegate", new { birthYear, birthMonth, country }, null);
        Assert.Equal(HttpStatusCode.OK, r.status);
        return r["ageToken"].GetString();
    }

    public async Task<Session> Linked(string sub = null)
    {
        var s = await Guest(); sub ??= Guid.NewGuid().ToString("N");
        Facebook.Tokens["fb-" + sub] = new ProviderIdentity { subject = sub };
        var r = await Post("/v1/account/link", new { provider = "facebook", token = "fb-" + sub }, s);
        Assert.Equal(HttpStatusCode.OK, r.status);
        return s;
    }

    public string Integrity(string pid, string nonce, Action<IntegrityVerdict> mutate = null)
    {
        var v = new IntegrityVerdict
        {
            requestPackageName = X.Opt.PackageName, packageName = X.Opt.PackageName, requestHash = TitanService.StartRequestHash(pid, nonce),
            timestampMillis = X.NowMs, appRecognitionVerdict = "PLAY_RECOGNIZED", certificateSha256Digest = X.Opt.CertDigests,
            deviceRecognitionVerdict = new[] { "MEETS_DEVICE_INTEGRITY" }, appLicensingVerdict = "LICENSED"
        };
        mutate?.Invoke(v);
        return FakeIntegrityVerifier.Make(v);
    }

    public async Task<Resp> StartRanked(Session s, Action<IntegrityVerdict> mutate = null)
    {
        var nonce = Guid.NewGuid().ToString("N");
        return await Post("/v1/titan/ranked/start", new { integrityToken = Integrity(s.pid, nonce, mutate) }, s, nonce);
    }

    /// <summary>One shot in the agreed InputEvent shape (fixed point 1/1000): aim, power, fire at `tick`, settle 100 ticks later.</summary>
    public static List<InputEvent> In(int tick) => new()
    {
        new() { tick = Math.Max(0, tick - 10), type = "aim", x = 1000, y = 20000 }, new() { tick = Math.Max(0, tick - 5), type = "power", x = 800 },
        new() { tick = tick, type = "fire", x = 1000, y = 20000 }, new() { tick = tick + 100, type = "settle" },
    };

    public async Task<Resp> Shot(Session s, string token, int idx, int damage, int budget, bool killed = false, bool inputs = true)
    {
        Advance(TimeSpan.FromSeconds(3));
        return await Post("/v1/titan/ranked/checkpoint", new RankedCheckpointEx { token = token, shotIndex = idx, damageScore = damage, shotsLeft = budget - idx, killed = killed, inputs = inputs ? In(idx * 200) : null }, s);
    }

    public Task<Resp> Finalize(Session s, string token, List<RankedCheckpointEx> cps = null, bool final = true, string sim = "dev") =>
        Post("/v1/titan/ranked/finalize", new RankedJournalEx { token = token, final = final, simVersion = sim, checkpoints = cps ?? new() }, s);

    /// <summary>Plays one complete ranked run: n shots of `per` damage.</summary>
    public async Task<(string token, Resp fin)> RankedRun(Session s, int shots, int per)
    {
        var t = await StartRanked(s); Assert.Equal(HttpStatusCode.OK, t.status);
        var token = t["token"].GetString(); int budget = t["shotBudget"].GetInt32();
        for (int i = 1; i <= shots; i++) Assert.Equal(HttpStatusCode.OK, (await Shot(s, token, i, per * i, budget)).status);
        return (token, await Finalize(s, token));
    }

    public async Task<string> Attempt(Session s, string level, string mode = "campaign")
    {
        var r = await Post(mode == "event" ? "/v1/events/attempts" : "/v1/campaign/attempts", new { levelId = level, count = 1 }, s);
        Assert.Equal(HttpStatusCode.OK, r.status);
        return r["items"][0].GetProperty("attemptToken").GetString();
    }

    public static CampaignRunReportEx Win(string level, string token, int shotsLeft, int shots, float ratio = 0.9f, int combo = 3) => new()
    {
        reportId = Guid.NewGuid().ToString("N"), levelId = level, won = true, destroyedRatio = ratio, shotsLeft = shotsLeft, bestCombo = combo, mastery = false,
        attemptToken = token, simVersion = "dev", inputs = Enumerable.Range(1, shots - shotsLeft).SelectMany(i => In(i * 200)).ToList()
    };

    public async Task ClearPrereqs(Session s, string upTo)
    {
        var cleared = X.Content.Chapter1.levels.TakeWhile(l => l.id != upTo).Select(l => l.id).ToList();
        await X.Db.Put("campaignProgress", s.pid, new CampaignProgressDoc { cleared = cleared });
    }

    /// <summary>Access JWTs live 15 minutes; after a clock jump the client refreshes (rotating refresh token).</summary>
    public async Task Renew(params Session[] ss)
    {
        foreach (var s in ss)
        {
            var r = await Post("/v1/auth/refresh", new { refreshToken = s.refresh }, null);
            Assert.Equal(HttpStatusCode.OK, r.status);
            s.access = r["accessToken"].GetString(); s.refresh = r["refreshToken"].GetString();
        }
    }

    public async Task<WalletEx> Wallet(Session s) => (await Get("/v1/wallet", s)).As<WalletEx>();

    public string Obf(Session s) => new AuthService(X).ObfuscatedId(s.pid);
    public string PlayPurchase(Session s, string sku, int state = 0, string token = null)
    {
        token ??= "ptok-" + Guid.NewGuid().ToString("N");
        Play.Purchases[token] = new PlayProductPurchase { purchaseState = state, orderId = "GPA." + token[^6..], obfuscatedExternalAccountId = Obf(s), productId = sku };
        return token;
    }
    public Task<Resp> Verify(Session s, string sku, string token) => Post("/v1/purchases/google/verify", new { productId = sku, purchaseToken = token, orderId = "x" }, s);

    public Task<Resp> Rtdn(object data, string messageId = null, string auth = "fake-pubsub-oidc") =>
        Send(HttpMethod.Post, "/internal/rtdn", new { message = new { messageId = messageId ?? Guid.NewGuid().ToString("N"), data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))) }, subscription = "s" }, bearer: auth);
    public Task<Resp> Job(string path) => Send(HttpMethod.Post, path, null, bearer: "fake-pubsub-oidc");
}
