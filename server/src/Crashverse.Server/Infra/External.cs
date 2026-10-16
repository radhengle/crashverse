using System.Text.Json;

namespace Crashverse.Server.Infra;

// ---------------- Play Integrity ----------------
public sealed class IntegrityVerdict
{
    public string requestPackageName; public string requestHash; public long timestampMillis;
    public string appRecognitionVerdict; public string packageName; public string[] certificateSha256Digest = Array.Empty<string>();
    public string[] deviceRecognitionVerdict = Array.Empty<string>(); public string appLicensingVerdict;
}

/// <summary>Decodes a Play Integrity token. Prod: playintegrity.googleapis.com decodeIntegrityToken with workload identity (S7).</summary>
public interface IIntegrityVerifier { Task<IntegrityVerdict> Decode(string integrityToken); }

/// <summary>DEV/TEST: the token is "fake:" + JSON of an IntegrityVerdict. Anything else fails to decode.</summary>
[DevOnly] public sealed class FakeIntegrityVerifier : IIntegrityVerifier
{
    public Task<IntegrityVerdict> Decode(string token)
    {
        if (token == null || !token.StartsWith("fake:")) return Task.FromResult<IntegrityVerdict>(null);
        try { return Task.FromResult(JsonSerializer.Deserialize<IntegrityVerdict>(token[5..], new JsonSerializerOptions { IncludeFields = true })); }
        catch { return Task.FromResult<IntegrityVerdict>(null); }
    }
    public static string Make(IntegrityVerdict v) => "fake:" + JsonSerializer.Serialize(v, new JsonSerializerOptions { IncludeFields = true });
}

// ---------------- Identity providers (H4) ----------------
public sealed class ProviderIdentity { public string provider; public string subject; public string email; public bool emailVerified; public string name; }

/// <summary>Google ID token: JWKS signature, iss, aud, exp, nonce. Returns null when invalid.</summary>
public interface IGoogleTokenVerifier { Task<ProviderIdentity> Verify(string idToken, string expectedNonce); }
/// <summary>Facebook access token via debug_token (is_valid, app_id). Returns null when invalid.</summary>
public interface IFacebookTokenVerifier { Task<ProviderIdentity> Verify(string accessToken); }
/// <summary>X OAuth2 PKCE code exchange on the server, then /2/users/me. Disabled by flag auth.x.enabled=false.</summary>
public interface IXAuthClient { Task<ProviderIdentity> Exchange(string code, string codeVerifier, string redirectUri); }

/// <summary>Production Google verifier (not exercised in tests; needs network to fetch Google's JWKS).</summary>
public sealed class GoogleIdTokenVerifier : IGoogleTokenVerifier
{
    readonly string _audience;
    readonly Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration> _cfg =
        new("https://accounts.google.com/.well-known/openid-configuration", new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever());
    public GoogleIdTokenVerifier(string audience) { _audience = audience; }
    public async Task<ProviderIdentity> Verify(string idToken, string expectedNonce)
    {
        try
        {
            var conf = await _cfg.GetConfigurationAsync(CancellationToken.None);
            var h = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var p = h.ValidateToken(idToken, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidIssuers = new[] { "accounts.google.com", "https://accounts.google.com" }, ValidAudience = _audience,
                IssuerSigningKeys = conf.SigningKeys, ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(2)
            }, out _);
            if (expectedNonce != null && p.FindFirst("nonce")?.Value != expectedNonce) return null;
            return new ProviderIdentity { provider = "google", subject = p.FindFirst("sub")?.Value, email = p.FindFirst("email")?.Value,
                emailVerified = p.FindFirst("email_verified")?.Value == "true" };
        }
        catch { return null; }
    }
}

/// <summary>Production Facebook verifier (not exercised in tests). App secret comes from the secret store, never the client.</summary>
public sealed class FacebookDebugTokenVerifier : IFacebookTokenVerifier
{
    readonly HttpClient _http; readonly string _appId; readonly string _appSecret;
    public FacebookDebugTokenVerifier(HttpClient http, string appId, string appSecret) { _http = http; _appId = appId; _appSecret = appSecret; }
    public async Task<ProviderIdentity> Verify(string accessToken)
    {
        try
        {
            var url = $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={_appId}|{_appSecret}";
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url));
            var d = doc.RootElement.GetProperty("data");
            if (!d.GetProperty("is_valid").GetBoolean() || d.GetProperty("app_id").GetString() != _appId) return null;
            return new ProviderIdentity { provider = "facebook", subject = d.GetProperty("user_id").GetString() };
        }
        catch { return null; }
    }
}

/// <summary>TEST/DEV fakes: tokens registered up front map to identities; everything else is rejected.</summary>
[DevOnly] public sealed class FakeGoogleVerifier : IGoogleTokenVerifier
{
    public readonly Dictionary<string, (ProviderIdentity id, string nonce)> Tokens = new();
    public Task<ProviderIdentity> Verify(string t, string nonce) =>
        Task.FromResult(t != null && Tokens.TryGetValue(t, out var v) && (v.nonce == null || v.nonce == nonce) ? v.id : null);
}
[DevOnly] public sealed class FakeFacebookVerifier : IFacebookTokenVerifier
{
    public readonly Dictionary<string, ProviderIdentity> Tokens = new();
    public Task<ProviderIdentity> Verify(string t) => Task.FromResult(t != null && Tokens.TryGetValue(t, out var v) ? v : null);
}
[DevOnly] public sealed class FakeXClient : IXAuthClient
{
    public readonly Dictionary<string, ProviderIdentity> Codes = new();
    public Task<ProviderIdentity> Exchange(string code, string v, string r) => Task.FromResult(code != null && v != null && Codes.TryGetValue(code, out var id) ? id : null);
}

// ---------------- Email ----------------
/// <summary>Parental-consent emails go through their own sender so production can use a vetted template/provider. Fake in dev/tests.</summary>
public interface IParentalConsentMailer { Task SendConsentRequest(string parentEmail, string requestId, string approveToken); }
[DevOnly] public sealed class FakeParentalConsentMailer : IParentalConsentMailer
{
    public readonly List<(string to, string requestId, string token)> Sent = new();
    public Task SendConsentRequest(string to, string id, string t) { lock (Sent) Sent.Add((to, id, t)); return Task.CompletedTask; }
}
public interface IEmailSender { Task Send(string to, string subject, string body); }
[DevOnly] public sealed class FakeEmailSender : IEmailSender
{
    public readonly List<(string to, string subject, string body)> Sent = new();
    public Task Send(string to, string s, string b) { lock (Sent) Sent.Add((to, s, b)); return Task.CompletedTask; }
}

// ---------------- Google Play Developer API (C2) ----------------
public sealed class PlayProductPurchase
{
    public int purchaseState; // 0 purchased, 1 canceled, 2 pending
    public int consumptionState; public int acknowledgementState;
    public string orderId; public string obfuscatedExternalAccountId; public string productId; public long purchaseTimeMillis;
}
public sealed class PlayVoidedPurchase { public string purchaseToken; public string orderId; public long voidedTimeMillis; public int voidedReason; }

/// <summary>androidpublisher v3. Prod: workload identity, no JSON key file (S7).</summary>
public interface IPlayDeveloperApi
{
    Task<PlayProductPurchase> GetProduct(string packageName, string productId, string purchaseToken); // null if unknown token
    Task Acknowledge(string packageName, string productId, string purchaseToken);
    Task Consume(string packageName, string productId, string purchaseToken);
    Task<List<PlayVoidedPurchase>> ListVoided(string packageName, long startTimeMillis);
}

[DevOnly] public sealed class FakePlayApi : IPlayDeveloperApi
{
    public readonly Dictionary<string, PlayProductPurchase> Purchases = new();
    public readonly List<PlayVoidedPurchase> Voided = new();
    public readonly List<string> Acked = new(), Consumed = new();
    public Task<PlayProductPurchase> GetProduct(string p, string productId, string t) =>
        Task.FromResult(Purchases.TryGetValue(t, out var x) && x.productId == productId ? x : null);
    public Task Acknowledge(string p, string pr, string t) { Acked.Add(t); if (Purchases.TryGetValue(t, out var x)) x.acknowledgementState = 1; return Task.CompletedTask; }
    public Task Consume(string p, string pr, string t) { Consumed.Add(t); if (Purchases.TryGetValue(t, out var x)) x.consumptionState = 1; return Task.CompletedTask; }
    public Task<List<PlayVoidedPurchase>> ListVoided(string p, long since) => Task.FromResult(Voided.Where(v => v.voidedTimeMillis >= since).ToList());
}

/// <summary>Authenticates callers of /internal/* (Pub/Sub push, Cloud Scheduler): a Google-signed OIDC token from workload identity,
/// checked for audience and service-account email. There is no shared secret.</summary>
public interface IPushAuthVerifier { Task<bool> Verify(string bearerToken); }

/// <summary>Production: validates a Google-issued OIDC ID token (JWKS from accounts.google.com) with the expected audience,
/// an allow-listed service-account email and email_verified=true. Not exercised in tests (needs network).</summary>
public sealed class GoogleOidcPushAuthVerifier : IPushAuthVerifier
{
    readonly string _audience; readonly HashSet<string> _serviceAccounts;
    readonly Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration> _cfg =
        new("https://accounts.google.com/.well-known/openid-configuration", new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever());
    public GoogleOidcPushAuthVerifier(string audience, IEnumerable<string> serviceAccountEmails)
    {
        if (string.IsNullOrWhiteSpace(audience)) throw new ArgumentException("OIDC audience required");
        _audience = audience; _serviceAccounts = serviceAccountEmails.Where(e => !string.IsNullOrWhiteSpace(e)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_serviceAccounts.Count == 0) throw new ArgumentException("at least one service-account email required");
    }
    public async Task<bool> Verify(string token)
    {
        try
        {
            var conf = await _cfg.GetConfigurationAsync(CancellationToken.None);
            var p = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidIssuers = new[] { "accounts.google.com", "https://accounts.google.com" }, ValidAudience = _audience,
                IssuerSigningKeys = conf.SigningKeys, ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);
            return p.FindFirst("email_verified")?.Value == "true" && _serviceAccounts.Contains(p.FindFirst("email")?.Value ?? "");
        }
        catch { return false; }
    }
}

/// <summary>DEV/TEST ONLY. The expected token is random per process unless a test sets it; never a constant in the repo.</summary>
[DevOnly] public sealed class FakePushAuthVerifier : IPushAuthVerifier
{
    public string Expected = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    public Task<bool> Verify(string t) => Task.FromResult(t == Expected);
}

// ---------------- Replay worker (H1/H2) ----------------
public sealed class ReplayResult { public int damageScore; public bool killed; public float destroyedRatio; public int bestCombo; public bool mastery; public bool won; public int shotsLeft; }

/// <summary>
/// Headless Unity re-simulation (S4 slice). v0 ships only the interface: StubReplayWorker returns null (not available),
/// so runs stay pending and never become visible in the top-N. Tests inject a scripted worker.
/// </summary>
public interface ICaptchaVerifier { Task<bool> Verify(string token, string remoteIp); }
/// <summary>DEV/TEST: accepts "ok".</summary>
[DevOnly] public sealed class FakeCaptchaVerifier : ICaptchaVerifier { public Task<bool> Verify(string t, string ip) => Task.FromResult(t == "ok"); }

public interface IReplayWorker { Task<ReplayResult> Replay(RunRecordView run); bool Configured => false; }
/// <summary>Replay contract (Physics PR #6): integer ticks at physicsHz (50). For tick t = 0, 1, …: first apply every InputEvent with
/// tick == t in list order, then Physics.Simulate(1/physicsHz). `seed` is the attempt/run token seed (long), the client's RunContext.seed.</summary>
public sealed class RunRecordView { public int physicsHz = 50; public string runId; public string mode; public string levelOrDay; public long seed; public string simVersion; public List<Crashverse.Backend.InputEvent> inputs; public int clientScore; }
[DevOnly] public sealed class StubReplayWorker : IReplayWorker { public Task<ReplayResult> Replay(RunRecordView run) => Task.FromResult<ReplayResult>(null); }
[DevOnly] public sealed class ScriptedReplayWorker : IReplayWorker
{
    public Func<RunRecordView, ReplayResult> Fn = r => null;
    public bool IsConfigured = true; public bool Configured => IsConfigured;
    public Task<ReplayResult> Replay(RunRecordView run) => Task.FromResult(Fn(run));
}

/// <summary>Random source for sampling (injectable for tests).</summary>
public interface ISampler { double Next(); }
public sealed class SystemSampler : ISampler { public double Next() => Random.Shared.NextDouble(); }
[DevOnly] public sealed class FixedSampler : ISampler { public double Value = 0.99; public double Next() => Value; }
