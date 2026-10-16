using System.Text.Json;
using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Server.Domain;
using Crashverse.Server.Infra;

using LinkCredential = Crashverse.Server.Domain.LinkCredential;

namespace Crashverse.Server;

public static class ServerApp
{
    public static readonly JsonSerializerOptions Json = new() { IncludeFields = true };

    /// <summary>Default dev wiring: in-memory store (or Firestore emulator if CRASHVERSE_STORE=firestore), local ES256/HMAC keys, fakes for every external API.</summary>
    public static Ctx DevContext(ServerOptions opt, IClock clock = null, Func<string, string> env = null)
    {
        if (opt.Env == CrashverseEnv.Prod) throw new ProdStartupException("DevContext is dev/test only; CRASHVERSE_ENV is prod (or missing)");
        DevGuard.CheckNotCloud(env ?? Environment.GetEnvironmentVariable); // N1
        var secrets = new DevSecretProvider();
        opt.ObfuscationPepper ??= secrets.Get(SecretNames.PurchaseBinding).Result; opt.DeletionPepper ??= secrets.Get(SecretNames.DeletionPepper).Result;
        clock ??= new SystemClock();
        IDocStore db = Environment.GetEnvironmentVariable("CRASHVERSE_STORE") == "firestore"
            ? new FirestoreDocStore(Environment.GetEnvironmentVariable("FIRESTORE_PROJECT") ?? "demo-crashverse") : new InMemoryDocStore();
        opt.ContentRoot ??= FindContentRoot();
        return new Ctx
        {
            Db = db, Clock = clock, Opt = opt, Content = Content.Load(opt.ContentRoot),
            Tokens = new TokenService(new[] { new KeyRing("access", new EcdsaDevSigner("access-dev-1"), clock), new KeyRing("run", new EcdsaDevSigner("run-dev-1"), clock),
                new KeyRing("attempt", new EcdsaDevSigner("attempt-dev-1"), clock),
                new KeyRing("reauth", new EcdsaDevSigner("reauth-dev-1"), clock), new KeyRing("consent", new EcdsaDevSigner("consent-dev-1"), clock), new KeyRing("age", new EcdsaDevSigner("age-dev-1"), clock) }, clock, opt.Flavor),
            Receipts = new ReceiptService(clock, "receipt-dev-1"),
            Nonces = new StoreNonceCache(db, clock), Limits = new StoreRateLimiter(db, clock),
            Integrity = new FakeIntegrityVerifier(), Google = new FakeGoogleVerifier(), Facebook = new FakeFacebookVerifier(), X = new FakeXClient(),
            Email = new FakeEmailSender(), ConsentMailer = new FakeParentalConsentMailer(), ConsentAges = ConsentAgeTable.Load(opt.ConsentAgesPath ?? ConsentAgeTable.FindPath()), Economy = EconomyConfig.Load(opt.EconomyPath ?? ConsentAgeTable.FindPath("economy_v1.json")), Captcha = new FakeCaptchaVerifier(), Secrets = secrets, Play = new FakePlayApi(), PushAuth = new FakePushAuthVerifier(), Replay = new StubReplayWorker(), Sampler = new SystemSampler()
        };
    }

    /// <summary>Prod wiring (C1). Every adapter must be a real implementation; ProdGuard throws on any dev-only one or missing config.</summary>
    public sealed class ProdAdapters
    {
        public IDocStore Db; public IClock Clock; public Func<string, ISigner> Signer; public byte[] ReceiptMaster; public string ReceiptKid;
        public IIntegrityVerifier Integrity; public IPlayDeveloperApi Play; public IPushAuthVerifier PushAuth; public IGoogleTokenVerifier Google;
        public IFacebookTokenVerifier Facebook; public IXAuthClient X; public IEmailSender Email; public IParentalConsentMailer ConsentMailer;
        public IReplayWorker Replay; public ICaptchaVerifier Captcha; public ISampler Sampler; public ISecretProvider Secrets;
        public INonceCache Nonces; public IRateLimiter Limits;

        /// <summary>Builds what can be built from environment config. Store, KMS signer, Secret Manager receipt master, Play,
        /// Integrity, email and replay adapters are not written yet, so prod refuses to start (by design) until they exist.</summary>
        public static ProdAdapters FromEnvironment(Func<string, string> env)
        {
            var a = new ProdAdapters { Clock = new SystemClock(), Sampler = new SystemSampler() };
            if (env("SECRET_MANAGER_PROJECT") is { Length: > 0 } sp) a.Secrets = new SecretManagerSecretProvider(sp);
            if (env("GOOGLE_OAUTH_CLIENT_ID") is { Length: > 0 } gid) a.Google = new GoogleIdTokenVerifier(gid);
            if (env("FACEBOOK_APP_ID") is { Length: > 0 } fid && env("FACEBOOK_APP_SECRET") is { Length: > 0 } fs) a.Facebook = new FacebookDebugTokenVerifier(new HttpClient(), fid, fs);
            if (env("INTERNAL_OIDC_AUDIENCE") is { Length: > 0 } aud && env("INTERNAL_OIDC_SERVICE_ACCOUNTS") is { Length: > 0 } sas)
                a.PushAuth = new GoogleOidcPushAuthVerifier(aud, sas.Split(','));
            return a;
        }
    }

    public static Ctx ProdContext(ServerOptions opt, ProdAdapters a)
    {
        if (opt.Env != CrashverseEnv.Prod) throw new ProdStartupException("ProdContext requires CRASHVERSE_ENV=prod");
        var clock = a.Clock ?? new SystemClock();
        TokenService tokens = a.Signer == null ? null : new TokenService(new[] { "access", "run", "attempt", "reauth", "consent", "age" }.Select(p => new KeyRing(p, a.Signer(p), clock)), clock, opt.Flavor);
        var x = new Ctx
        {
            Db = a.Db, Clock = clock, Opt = opt, Tokens = tokens,
            Receipts = a.ReceiptMaster == null ? null : new ReceiptService(clock, a.ReceiptKid ?? "r1", a.ReceiptMaster),
            Nonces = a.Nonces ?? (a.Db == null ? null : new StoreNonceCache(a.Db, clock)), Limits = a.Limits ?? (a.Db == null ? null : new StoreRateLimiter(a.Db, clock)),
            Integrity = a.Integrity, Google = a.Google, Facebook = a.Facebook, X = a.X, Email = a.Email, ConsentMailer = a.ConsentMailer, Play = a.Play,
            PushAuth = a.PushAuth, Replay = a.Replay, Sampler = a.Sampler ?? new SystemSampler(), Captcha = a.Captcha, Secrets = a.Secrets,
        };
        if (a.Secrets != null)
        {
            try { opt.ObfuscationPepper = a.Secrets.Get(SecretNames.PurchaseBinding).GetAwaiter().GetResult(); } catch (Exception) { opt.ObfuscationPepper = null; }
            try { opt.DeletionPepper = a.Secrets.Get(SecretNames.DeletionPepper).GetAwaiter().GetResult(); } catch (Exception) { opt.DeletionPepper = null; }
        }
        else { opt.ObfuscationPepper = null; opt.DeletionPepper = null; }
        ProdGuard.Validate(x); // before touching content/config files: fail fast with the full list
        opt.ContentRoot ??= FindContentRoot();
        x.Content = Content.Load(opt.ContentRoot);
        x.ConsentAges = ConsentAgeTable.Load(opt.ConsentAgesPath ?? ConsentAgeTable.FindPath());
        x.Economy = EconomyConfig.Load(opt.EconomyPath ?? ConsentAgeTable.FindPath("economy_v1.json"));
        return x;
    }

    public static string FindContentRoot()
    {
        var env = Environment.GetEnvironmentVariable("CRASHVERSE_CONTENT");
        if (!string.IsNullOrEmpty(env)) return env;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "Assets", "_Project", "Resources", "Data");
            if (Directory.Exists(p)) return p;
        }
        throw new DirectoryNotFoundException("content root not found; set CRASHVERSE_CONTENT");
    }

    public static WebApplication Build(Ctx x, Action<WebApplicationBuilder> configure = null, string[] args = null)
    {
        var b = WebApplication.CreateBuilder(args ?? Array.Empty<string>());
        configure?.Invoke(b);
        b.WebHost.ConfigureKestrel(k => { k.Limits.MaxRequestBodySize = x.Opt.MaxJournalBytes; k.AddServerHeader = false; });
        b.Logging.SetMinimumLevel(LogLevel.Warning);
        var app = b.Build();
        Map(app, x);
        return app;
    }

    /// <summary>M2: client IP for rate-limit keys. With TRUST_X_FORWARDED_FOR=true (only behind Google's front end, which appends the
    /// client address), take the entry TrustedProxyHops from the right of X-Forwarded-For; otherwise the socket address.</summary>
    public static string ClientIp(HttpContext h, ServerOptions o)
    {
        if (o.TrustForwardedFor)
        {
            var parts = h.Request.Headers["X-Forwarded-For"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int i = parts.Length - Math.Max(1, o.TrustedProxyHops);
            if (i >= 0 && System.Net.IPAddress.TryParse(parts[i], out var ip)) return ip.ToString();
        }
        return h.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    static async Task<T> Body<T>(HttpContext h, long max = 0) where T : class
    {
        max = max == 0 ? 64 * 1024 : max;
        if (h.Request.ContentLength > max) throw new ApiError(413, "PAYLOAD_TOO_LARGE", "request body too large");
        using var ms = new MemoryStream();
        var buf = new byte[8192]; int n; long total = 0;
        while ((n = await h.Request.Body.ReadAsync(buf)) > 0) { total += n; if (total > max) throw new ApiError(413, "PAYLOAD_TOO_LARGE", "request body too large"); ms.Write(buf, 0, n); }
        if (ms.Length == 0) return null;
        try { return JsonSerializer.Deserialize<T>(ms.ToArray(), Json); }
        catch (JsonException) { throw new ApiError(400, "BAD_REQUEST", "malformed JSON"); }
    }

    static async Task Limit(Ctx x, string key, int limit, TimeSpan window)
    {
        if (!await x.Limits.TryAcquire(key, limit, window)) throw new ApiError(429, "RATE_LIMITED", "too many requests", true);
    }

    /// <summary>Order (gap 2): cheap JWT check → per-account rate limit → nonce (mutations) → work.</summary>
    static async Task<string> Auth(Ctx x, AuthService auth, HttpContext h, bool mutating, bool allowPendingDeletion = false)
    {
        var hdr = h.Request.Headers.Authorization.ToString();
        if (!hdr.StartsWith("Bearer ")) throw new ApiError(401, "UNAUTHENTICATED", "missing bearer token");
        var pid = auth.VerifyAccess(hdr[7..]);
        await Limit(x, "acct:" + pid, x.Opt.AccountPerMinute, TimeSpan.FromMinutes(1));
        if (!await auth.SessionActive(hdr[7..])) throw new ApiError(401, "UNAUTHENTICATED", "session revoked");
        var acc = await x.Db.Get<AccountDoc>("accounts", pid);
        if (acc == null || (acc.status != "active" && !(allowPendingDeletion && acc.status == "deleted_pending"))) throw new ApiError(401, "UNAUTHENTICATED", "account inactive");
        if (acc.status == "active" && (x.NowMs - acc.lastActiveUnixMs > 3_600_000 || acc.deletionWarningAtUnixMs != 0)) // C2: activity resets the inactivity clock
            await x.Db.Atomic("account:" + pid, async () => { var a = await x.Db.Get<AccountDoc>("accounts", pid); a.lastActiveUnixMs = x.NowMs; a.deletionWarningAtUnixMs = 0; await x.Db.Put("accounts", pid, a); return 0; });
        if (mutating)
        {
            var nonce = h.Request.Headers["X-Nonce"].ToString();
            if (nonce.Length < 16 || nonce.Length > 64) throw new ApiError(400, "NONCE_REQUIRED", "X-Nonce header required");
            if (!await x.Nonces.TryUse(pid + ":" + nonce, TimeSpan.FromMinutes(10))) throw new ApiError(409, "NONCE_REPLAY", "replayed request");
        }
        return pid;
    }

    static void Map(WebApplication app, Ctx x)
    {
        var auth = new AuthService(x);
        string Ip(HttpContext h) => ClientIp(h, x.Opt);
        app.Use(async (h, next) =>
        {
            h.Response.OnStarting(() => { h.Response.Headers["X-Server-Time"] = x.NowMs.ToString(); return Task.CompletedTask; });
            try { await next(); }
            catch (ApiError e)
            {
                h.Response.StatusCode = e.Status;
                if (e.Status == 429) h.Response.Headers.RetryAfter = "60";
                await h.Response.WriteAsJsonAsync(new { error = new { code = e.Code, message = e.Message, retryable = e.Retryable, details = e.Extra } }, Json);
            }
            catch (AppendOnlyViolation) { h.Response.StatusCode = 500; await h.Response.WriteAsJsonAsync(new { error = new { code = "INTERNAL", message = "internal error", retryable = false } }); }
        });

        IResult Ok(object o) => Results.Json(o, Json);
        void MG(string p, Func<HttpContext, Task<IResult>> f) => app.MapGet(p, (RequestDelegate)(async h => await (await f(h)).ExecuteAsync(h)));
        void MP(string p, Func<HttpContext, Task<IResult>> f) => app.MapPost(p, (RequestDelegate)(async h => await (await f(h)).ExecuteAsync(h)));
        void MD(string p, Func<HttpContext, Task<IResult>> f) => app.MapDelete(p, (RequestDelegate)(async h => await (await f(h)).ExecuteAsync(h)));
        void MU(string p, Func<HttpContext, Task<IResult>> f) => app.MapPut(p, (RequestDelegate)(async h => await (await f(h)).ExecuteAsync(h)));
        void Get(string p, Func<HttpContext, string, Task<object>> f) => MG("/v1" + p, async h => Ok(await f(h, await Auth(x, auth, h, false))));
        void Post(string p, Func<HttpContext, string, Task<object>> f) => MP("/v1" + p, async h => Ok(await f(h, await Auth(x, auth, h, true))));

        // ---- unauthenticated (strict limits, S5) ----
        app.MapGet("/v1/health", () => Ok(new { ok = true }));
        MG("/v1/time", async h => { await Limit(x, "time:" + Ip(h), x.Opt.TimePerIpPerMinute, TimeSpan.FromMinutes(1)); h.Response.Headers.CacheControl = "no-store"; return Ok(new { serverTimeUnixMs = x.NowMs }); });
        MP("/v1/auth/guest", async h =>
        {
            await Limit(x, "guest-ip:" + Ip(h), x.Opt.GuestPerIpPerHour, TimeSpan.FromHours(1));
            var r = await Body<GuestReq>(h) ?? new GuestReq();
            await Limit(x, "guest-dev:" + r.deviceInstallId, x.Opt.GuestPerDevicePerHour, TimeSpan.FromHours(1));
            return Ok(await auth.Guest(r.deviceInstallId, r.ageToken));
        });
        MP("/v1/auth/refresh", async h => { await Limit(x, "auth:" + Ip(h), x.Opt.AuthPerMinute * 4, TimeSpan.FromMinutes(1)); return Ok(await auth.Refresh((await Body<RefreshReq>(h))?.refreshToken)); });
        MP("/v1/auth/sign-in", async h =>
        {
            await Limit(x, "auth:" + Ip(h), x.Opt.AuthPerMinute, TimeSpan.FromMinutes(1));
            var r = await Body<SignInReq>(h) ?? new SignInReq();
            return Ok(await auth.SignIn(r.provider, r, r.deviceInstallId, r.ageToken));
        });
        MP("/v1/auth/email/login", async h =>
        {
            var r = await Body<EmailReq>(h) ?? new EmailReq();
            await Limit(x, "auth:" + Ip(h), x.Opt.AuthPerMinute, TimeSpan.FromMinutes(1));
            await Limit(x, "email:" + (r.email ?? "").ToLowerInvariant(), x.Opt.AuthPerMinute, TimeSpan.FromMinutes(1));
            return Ok(await auth.EmailLogin(r.email, r.password));
        });
        MP("/v1/auth/email/reset-request", async h =>
        {
            var r = await Body<EmailReq>(h) ?? new EmailReq();
            await Limit(x, "auth:" + Ip(h), x.Opt.AuthPerMinute, TimeSpan.FromMinutes(1));
            await Limit(x, "reset:" + (r.email ?? "").ToLowerInvariant(), 3, TimeSpan.FromHours(1));
            return Ok(await auth.EmailResetRequest(r.email));
        });
        MP("/v1/auth/email/reset", async h =>
        {
            var r = await Body<EmailReq>(h) ?? new EmailReq();
            await Limit(x, "auth:" + Ip(h), x.Opt.AuthPerMinute, TimeSpan.FromMinutes(1));
            return Ok(await auth.EmailReset(r.email, r.code, r.newPassword));
        });
        MP("/v1/auth/x/start", async h => { if (!x.Opt.XEnabled) throw new ApiError(404, "FEATURE_DISABLED", "X login is disabled"); return Ok(new { authorizeUrl = "https://x.com/i/oauth2/authorize" }); });

        MG("/v1/config/flags", async h => Ok(new FeatureFlagsEx { xLoginEnabled = x.Opt.XEnabled, emailAuthEnabled = true, accountDeletionEnabled = true, enableGemPacks = x.Opt.EnableGemPacks }));

        // ---- account (P1, P2, P7) ----
        MG("/v1/account", async h => Ok(auth.View(await auth.Account(await Auth(x, auth, h, false, allowPendingDeletion: true)))));
        Post("/account/link-nonce", async (h, pid) => new { nonce = await auth.LinkNonce(pid) });
        Post("/account/link", async (h, pid) => { ComplianceService.Enforce(await auth.Account(pid), "link", x.ConsentAges); return await auth.Link(pid, await Body<LinkRequest>(h)); });
        Post("/account/unlink", async (h, pid) => { var r = await Body<LinkRequest>(h) ?? new LinkRequest(); return await auth.Unlink(pid, Providers.Norm(r.provider), r); });
        Post("/auth/email/register", async (h, pid) => { await Limit(x, "auth:" + pid, x.Opt.AuthPerMinute, TimeSpan.FromMinutes(1)); var r = await Body<EmailReq>(h) ?? new EmailReq(); ComplianceService.Enforce(await auth.Account(pid), "link", x.ConsentAges); return await auth.EmailRegister(pid, r.email, r.password); });
        Post("/auth/email/verify", async (h, pid) => { var r = await Body<EmailReq>(h) ?? new EmailReq(); return await auth.EmailVerify(pid, r.email, r.code); });
        // Agreed interface: deletion needs a fresh (≤5 min, single-use) re-auth; cancel works during the grace period.
        Post("/auth/reauth", async (h, pid) => { await Limit(x, "reauth:" + pid, x.Opt.AuthPerMinute, TimeSpan.FromMinutes(1)); return await auth.Reauth(pid, (await Body<ReauthReq>(h))?.credential); });
        Post("/account/delete", async (h, pid) => await auth.DeleteAccount(pid, (await Body<DeleteReq>(h))?.reauthToken));
        MP("/v1/account/delete/cancel", async h => Ok(await auth.CancelDelete(await Auth(x, auth, h, true, allowPendingDeletion: true))));
        Post("/account/keep-active", async (h, pid) => await auth.KeepActive(pid));
        // N5: re-gate for accounts with no stored band (created before the age gate). The token must come from this account's install.
        Post("/account/agegate", async (h, pid) => await x.Db.Atomic("account:" + pid, async () =>
        {
            var a = await auth.Account(pid); var c = new ComplianceService(x);
            var g = c.VerifyAgeToken((await Body<AgeTokenReq>(h))?.ageToken, a.deviceInstallId);
            await c.ConsumeAgeToken(g); await c.ApplyAge(a, g); await x.Db.Put("accounts", pid, a);
            return (object)new AgeGateResponse { band = a.ageGate.band, requiresParentalConsent = a.ageGate.requiresParentalConsent };
        }));
        Post("/profile/visibility", async (h, pid) => await auth.SetVisibility(pid, (await Body<VisibilityReq>(h))?.hideName ?? false));

        // ---- age gate (unauthenticated, strict IP limit) + parental consent ----
        var comp = new ComplianceService(x);
        // ---- B6 web deletion (no app): email + code, or a social login; CAPTCHA + strict limits; deletion completes within 30 days ----
        MP("/v1/web/deletion/request", async h =>
        {
            var r = await Body<DataLifecycleService.WebReq>(h) ?? new();
            await Limit(x, "webdel-ip:" + Ip(h), 5, TimeSpan.FromHours(1));
            await Limit(x, "webdel-email:" + Hash.Sha256Hex((r.email ?? "").Trim().ToLowerInvariant()), 3, TimeSpan.FromDays(1));
            return Ok(await new DataLifecycleService(x).WebRequest(r, Ip(h)));
        });
        MP("/v1/web/deletion/confirm", async h =>
        {
            var r = await Body<DataLifecycleService.WebReq>(h) ?? new();
            await Limit(x, "webdel-ip:" + Ip(h), 10, TimeSpan.FromHours(1));
            return Ok(await new DataLifecycleService(x).WebConfirm(r));
        });
        MP("/v1/web/deletion/provider", async h =>
        {
            await Limit(x, "webdel-ip:" + Ip(h), 5, TimeSpan.FromHours(1));
            return Ok(await new DataLifecycleService(x).WebProvider(await Body<DataLifecycleService.WebReq>(h) ?? new(), Ip(h)));
        });
        MP("/v1/agegate", async h => { await Limit(x, "agegate:" + Ip(h), x.Opt.AuthPerMinute * 2, TimeSpan.FromMinutes(1)); return Ok(comp.AgeGate(await Body<ComplianceService.AgeGateReq>(h))); });
        Post("/consent/parental/request", async (h, pid) => { await Limit(x, "consent:" + pid, 3, TimeSpan.FromDays(1)); return await comp.RequestConsent(pid, (await Body<ComplianceService.ConsentReq>(h))?.parentEmail); });
        Get("/consent/parental/status", async (h, pid) => await comp.Status(pid));

        // ---- wallet / inventory / store ----
        var wallet = new WalletService(x);
        Get("/wallet", async (h, pid) => WalletService.View(await wallet.Get(pid)));
        Post("/wallet/consume-charge", async (h, pid) => { var (c, w) = await wallet.ConsumeCharge(pid, h.Request.Headers["Idempotency-Key"]); return new { consumed = c, wallet = WalletService.View(w) }; });
        Get("/wallet/ledger", async (h, pid) => new ListResponse<LedgerEntry>(await wallet.Ledger(pid)));
        Get("/inventory", async (h, pid) => { var w = await wallet.Get(pid); return new ListResponse<object>(w.cosmetics.Select(c => (object)new { id = c, kind = "cosmetic", source = w.cosmeticSource.GetValueOrDefault(c, "reward") })
            .Concat(w.powerUps.Select(p => (object)new { id = p, kind = "powerup", source = "reward" }))); });
        Post("/store/buy", async (h, pid) => { ComplianceService.Enforce(await auth.Account(pid), "purchase", x.ConsentAges); return WalletService.View(await wallet.Buy(pid, (await Body<SkuReq>(h))?.sku)); });

        // ---- inbox / daily reward / season ----
        var season = new SeasonService(x);
        Get("/inbox", async (h, pid) => new ListResponse<InboxItem>(await season.Inbox(pid)));
        Post("/inbox/{itemId}/claim", async (h, pid) => await season.ClaimInbox(pid, (string)h.Request.RouteValues["itemId"]));
        Get("/daily-reward", async (h, pid) => await season.DailyState(pid));
        Post("/daily-reward/claim", async (h, pid) => await season.ClaimDaily(pid));
        Get("/season", async (h, pid) => await season.State(pid));
        Post("/season/premium/unlock", async (h, pid) => { ComplianceService.Enforce(await auth.Account(pid), "purchase", x.ConsentAges); return await new EconomyService(x).UnlockPremium(pid); });
        Post("/season/tiers/{tier:int}/claim", async (h, pid) => await season.ClaimTier(pid, int.Parse((string)h.Request.RouteValues["tier"]), (await Body<TierReq>(h))?.premium ?? false));

        // ---- Daily Titan ----
        var titan = new TitanService(x);
        Get("/titan/today", async (h, pid) => await titan.Today());
        Get("/titan/ranked/status", async (h, pid) => await titan.Status(pid));
        Post("/titan/ranked/start", async (h, pid) => { await Limit(x, "rankedstart:" + pid, 10, TimeSpan.FromDays(1)); return await titan.Start(pid, (await Body<StartReq>(h))?.integrityToken, h.Request.Headers["X-Nonce"]); });
        Post("/titan/ranked/checkpoint", async (h, pid) => await titan.Checkpoint(pid, await Body<RankedCheckpointEx>(h) ?? throw new ApiError(400, "BAD_REQUEST", "body required")));
        MP("/v1/titan/ranked/finalize", async h => { var pid = await Auth(x, auth, h, true); return Ok(await titan.Finalize(pid, await Body<RankedJournalEx>(h, x.Opt.MaxJournalBytes))); });

        // ---- campaign / events ----
        var camp = new CampaignService(x);
        Post("/attempts", async (h, pid) => await camp.IssueAttempts(pid, await Body<AttemptRequest>(h) ?? new AttemptRequest()));
        Post("/campaign/attempts", async (h, pid) => { var r = await Body<AttemptRequest>(h) ?? new AttemptRequest(); r.mode = "campaign"; return await camp.IssueAttempts(pid, r); });
        Post("/events/attempts", async (h, pid) => { var r = await Body<AttemptRequest>(h) ?? new AttemptRequest(); r.mode = "event"; return await camp.IssueAttempts(pid, r); });
        MP("/v1/campaign/results", async h => { var pid = await Auth(x, auth, h, true); return Ok(await camp.Submit(pid, await Body<CampaignRunReportEx>(h, x.Opt.MaxJournalBytes))); });
        // Agreed interface SubmitScore {attemptToken, inputs[], clientScoreBp, buildHash}; BeginAttempt = POST /v1/attempts (POST /score in the client). No separate input-log endpoint.
        MP("/v1/score", async h => { var pid = await Auth(x, auth, h, true); return Ok(await camp.Submit(pid, await Body<CampaignRunReportEx>(h, x.Opt.MaxJournalBytes))); });
        Post("/events/{eventLevelId}/stars", async (h, pid) => await camp.EventStars(pid, (string)h.Request.RouteValues["eventLevelId"], await Body<EventStarsRequest>(h) ?? new EventStarsRequest()));

        // ---- leaderboards / friends ----
        var lb = new Leaderboards(x);
        Get("/leaderboards/{board:int}", async (h, pid) =>
        {
            int board = int.Parse((string)h.Request.RouteValues["board"]);
            if (board < 0 || board > 3) throw new ApiError(400, "BAD_REQUEST", "board 0-3");
            int.TryParse(h.Request.Query["period"], out var period);
            return await lb.Page(pid, (BoardType)board, period == 1 ? BoardPeriod.Previous : BoardPeriod.Current, h.Request.Query["friends"] == "true");
        });
        var friends = new Friends(x);
        Get("/friends", async (h, pid) => { ComplianceService.Enforce(await auth.Account(pid), "friends", x.ConsentAges); return await friends.List(pid); });
        Post("/friends", async (h, pid) => { ComplianceService.Enforce(await auth.Account(pid), "friends", x.ConsentAges); return await friends.Add(pid, (await Body<FriendReq>(h))?.friendCode); });
        Post("/friends/{playerId}/remove", async (h, pid) => await friends.Remove(pid, (string)h.Request.RouteValues["playerId"]));

        // ---- purchases (C2) ----
        var purchases = new PurchaseService(x);
        // Economy v1 sells nothing for real money: verification is behind IAP_ENABLED (default off).
        Post("/purchases/google/verify", async (h, pid) =>
        {
            if (!x.Opt.IapEnabled) throw new ApiError(404, "FEATURE_DISABLED", "real-money purchases are not available");
            ComplianceService.Enforce(await auth.Account(pid), "purchase", x.ConsentAges);
            var vr = await Body<VerifyRequest>(h);
            if (!x.Opt.EnableGemPacks && ServerOptions.GemPackSkus.Contains(vr?.productId))
                throw new ApiError(403, "GEM_PACKS_DISABLED", "gem packs are not on sale (ENABLE_GEM_PACKS is off)"); // before any Play call or grant
            return await purchases.Verify(pid, vr);
        });
        Get("/purchases", async (h, pid) => await purchases.Restore(pid));

        // ---- internal (Pub/Sub push + Cloud Scheduler; OIDC-verified) ----
        async Task Internal(HttpContext h)
        {
            var hdr = h.Request.Headers.Authorization.ToString();
            if (!hdr.StartsWith("Bearer ") || !await x.PushAuth.Verify(hdr[7..])) throw new ApiError(401, "UNAUTHENTICATED", "push auth failed");
        }
        MP("/internal/rtdn", async h =>
        {
            await Internal(h);
            using var d = await Body<JsonDocument>(h) ?? throw new ApiError(400, "BAD_REQUEST", "body required"); // 64 KB cap + JsonException → 400 (L2)
            if (!x.Opt.IapEnabled) return Ok(new { result = "ignored_iap_disabled" });
            return Ok(new { result = await purchases.HandleRtdn(d.RootElement) });
        });
        var life = new DataLifecycleService(x);
        MP("/internal/jobs/purge", async h => { await Internal(h); return Ok(await life.PurgeDue()); });
        MP("/internal/jobs/retention", async h => { await Internal(h); return Ok(await life.Retention()); });
        MP("/internal/jobs/guest-inactivity", async h => { await Internal(h); return Ok(await life.GuestInactivity()); });
        MP("/internal/consent/decide", async h => { await Internal(h); var d = await Body<ConsentDecision>(h) ?? new ConsentDecision(); return Ok(await comp.Decide(d.token, d.grant)); });
        MP("/internal/jobs/voided-poll", async h => { await Internal(h); return Ok(new { revoked = await purchases.PollVoided() }); });
        MP("/internal/jobs/replay", async h => { await Internal(h); var (d, p) = await new ReplayService(x).Process(); return Ok(new { done = d, pending = p }); });
        MP("/internal/jobs/titan-close/{dayKey}", async h => { await Internal(h); return Ok(new { result = await titan.CloseDay((string)h.Request.RouteValues["dayKey"]) }); });
        MU("/internal/admin/titan/{dayKey}", async h =>
        {
            await Internal(h);
            if (x.Opt.AdminKey == null || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(h.Request.Headers["X-Admin-Key"].ToString()), System.Text.Encoding.UTF8.GetBytes(x.Opt.AdminKey))) throw new ApiError(403, "FORBIDDEN", "admin disabled");
            await titan.SetOverride("admin", (string)h.Request.RouteValues["dayKey"], await Body<DailyTitanConfig>(h)); return Ok(new { ok = true });
        });
    }

    sealed class GuestReq { public string deviceInstallId; public string integrityToken; public string ageToken; }
    sealed class RefreshReq { public string refreshToken; }
    sealed class SignInReq : LinkCredential { public string deviceInstallId; public string ageToken; }
    sealed class ReauthReq { public LinkCredential credential; }
    sealed class DeleteReq { public string reauthToken; }
    sealed class VisibilityReq { public bool hideName; }
    sealed class ConsentDecision { public string token; public bool grant; }
    sealed class EmailReq { public string email; public string password; public string code; public string newPassword; }
    sealed class SkuReq { public string sku; }
    sealed class TierReq { public bool premium; }
    sealed class StartReq { public string integrityToken; }
    sealed class FriendReq { public string friendCode; }
}

public sealed class AgeTokenReq { public string ageToken; }

public class Program
{
    public static void Main(string[] args)
    {
        Func<string, string> env = Environment.GetEnvironmentVariable;
        var opt = new ServerOptions
        {
            Env = Envs.Parse(env("CRASHVERSE_ENV")), // missing/unknown = prod
            XEnabled = env("AUTH_X_ENABLED") == "true",
            RefundPolicy = env("REFUND_POLICY") ?? RefundPolicies.GemDebt,
            IntegrityMode = env("INTEGRITY_MODE") ?? "enforce",
        };
        ServerOptions.ApplyEnvFlags(opt, env);
        var ctx = opt.Env == CrashverseEnv.Prod ? ServerApp.ProdContext(opt, ServerApp.ProdAdapters.FromEnvironment(env)) : ServerApp.DevContext(opt);
        if (opt.RankedPublicBoardEnabled && !ctx.Replay.Configured) Console.Error.WriteLine("RANKED_PUBLIC_BOARD_ENABLED=true ignored: no replay worker configured (board stays closed)");
        var app = ServerApp.Build(ctx, args: args);
        app.Start();
        if (opt.Env != CrashverseEnv.Prod)
        {
            try { DevGuard.CheckLoopbackOnly(app.Urls); } // N1: actual bound addresses, after Kestrel resolved --urls/ASPNETCORE_URLS/config
            catch { app.StopAsync().GetAwaiter().GetResult(); throw; }
        }
        app.WaitForShutdown();
    }
}
