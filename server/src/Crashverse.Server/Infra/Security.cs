using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crashverse.Data;

namespace Crashverse.Server.Infra;

/// <summary>Signing key abstraction (Security Review S4). Dev: local ECDSA P-256 (ES256). Prod: Cloud KMS asymmetric key (later).</summary>
public interface ISigner
{
    string Alg { get; }
    string Kid { get; }
    byte[] Sign(byte[] data);
    bool Verify(byte[] data, byte[] signature);
}

/// <summary>DEV ONLY: in-process ECDSA P-256 key, never persisted, never shipped to the client.</summary>
[DevOnly] public sealed class EcdsaDevSigner : ISigner, IDisposable
{
    readonly ECDsa _k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public EcdsaDevSigner(string kid) { Kid = kid; }
    public string Alg => "ES256";
    public string Kid { get; }
    public byte[] Sign(byte[] data) => _k.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    public bool Verify(byte[] data, byte[] sig) => _k.VerifyData(data, sig, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    public void Dispose() => _k.Dispose();
}

/// <summary>
/// One key ring per purpose (access, run, attempt). Rotation keeps the previous key valid for verification
/// for an overlap window (>= max token lifetime + grace), then it is dropped.
/// </summary>
public sealed class KeyRing
{
    readonly object _gate = new();
    readonly IClock _clock;
    ISigner _active;
    readonly List<(ISigner signer, DateTime notAfter)> _retired = new();
    public string Purpose { get; }
    public KeyRing(string purpose, ISigner initial, IClock clock) { Purpose = purpose; _active = initial; _clock = clock; }
    public ISigner Active { get { lock (_gate) return _active; } }

    public void Rotate(ISigner next, TimeSpan overlap)
    {
        lock (_gate) { _retired.Add((_active, _clock.UtcNow + overlap)); _active = next; }
    }

    public ISigner Find(string kid)
    {
        lock (_gate)
        {
            if (_active.Kid == kid) return _active;
            var now = _clock.UtcNow;
            _retired.RemoveAll(r => r.notAfter < now);
            return _retired.FirstOrDefault(r => r.signer.Kid == kid).signer;
        }
    }
}

public static class B64
{
    public static string Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static byte[] FromUrl(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}

/// <summary>Compact JWS (ES256) tokens: access JWTs, ranked run tokens, campaign/event attempt tokens.</summary>
public sealed class TokenService
{
    readonly Dictionary<string, KeyRing> _rings;
    readonly IClock _clock;
    /// <summary>Server-issued flavor claim ("prod" | "test") stamped on every token; comes from server environment only.</summary>
    public string Flavor { get; }
    public TokenService(IEnumerable<KeyRing> rings, IClock clock, string flavor = "test") { _rings = rings.ToDictionary(r => r.Purpose); _clock = clock; Flavor = flavor; }
    public KeyRing Ring(string purpose) => _rings[purpose];
    public IEnumerable<KeyRing> Rings => _rings.Values;

    public string Issue(string purpose, JsonObject claims, TimeSpan ttl)
    {
        var s = _rings[purpose].Active;
        var now = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
        claims["flv"] = Flavor; claims["pur"] = purpose; claims["iat"] = now; claims["exp"] = now + (long)ttl.TotalSeconds;
        var h = B64.Url(Encoding.UTF8.GetBytes(new JsonObject { ["alg"] = s.Alg, ["kid"] = s.Kid, ["typ"] = "JWT" }.ToJsonString()));
        var p = B64.Url(Encoding.UTF8.GetBytes(claims.ToJsonString()));
        return h + "." + p + "." + B64.Url(s.Sign(Encoding.ASCII.GetBytes(h + "." + p)));
    }

    /// <summary>Returns the claims, or null with a reason. Checks signature (by kid, incl. overlap keys), purpose and expiry.</summary>
    public JsonObject Verify(string purpose, string token, out string error)
    {
        error = "malformed token";
        if (string.IsNullOrEmpty(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3) return null;
        try
        {
            var header = JsonNode.Parse(B64.FromUrl(parts[0])).AsObject();
            if ((string)header["alg"] != "ES256") { error = "bad alg"; return null; }
            var signer = _rings[purpose].Find((string)header["kid"]);
            if (signer == null) { error = "unknown or retired key"; return null; }
            if (!signer.Verify(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), B64.FromUrl(parts[2]))) { error = "bad signature"; return null; }
            var claims = JsonNode.Parse(B64.FromUrl(parts[1])).AsObject();
            if ((string)claims["pur"] != purpose) { error = "wrong token purpose"; return null; }
            if (Flavor == "prod" && (string)claims["flv"] != "prod") { error = "test-flavor token rejected in prod"; return null; }
            if ((long)claims["exp"] < new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds()) { error = "token expired"; return null; }
            error = null; return claims;
        }
        catch (Exception) { error = "malformed token"; return null; }
    }
}

/// <summary>
/// Checkpoint receipts (SEC-01, C1): HMAC-SHA256 with a per-run key = HKDF(master[kid], runId). Masters live server-side only
/// (Secret Manager in prod; random in-process dev key here). The receipt carries the kid so masters can rotate with overlap.
/// </summary>
public sealed class ReceiptService
{
    readonly object _gate = new();
    readonly IClock _clock;
    (string kid, byte[] key) _active;
    readonly List<(string kid, byte[] key, DateTime notAfter)> _retired = new();
    /// <summary>True when the master was generated in-process (dev/test). Prod must load it from the secret store.</summary>
    public bool Ephemeral { get; }
    public ReceiptService(IClock clock, string kid = "r1", byte[] master = null)
    { _clock = clock; Ephemeral = master == null; _active = (kid, master ?? RandomNumberGenerator.GetBytes(32)); }

    public string ActiveKid { get { lock (_gate) return _active.kid; } }
    public void Rotate(string kid, byte[] master, TimeSpan overlap)
    { lock (_gate) { _retired.Add((_active.kid, _active.key, _clock.UtcNow + overlap)); _active = (kid, master); } }

    byte[] Master(string kid)
    {
        lock (_gate)
        {
            if (_active.kid == kid) return _active.key;
            _retired.RemoveAll(r => r.notAfter < _clock.UtcNow);
            return _retired.FirstOrDefault(r => r.kid == kid).key;
        }
    }

    static byte[] RunKey(byte[] master, string runId) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, Encoding.UTF8.GetBytes(runId), Encoding.UTF8.GetBytes("crashverse-receipt-v1"));

    public string Sign(string runId, string canonical)
    {
        string kid; byte[] m; lock (_gate) { kid = _active.kid; m = _active.key; }
        return kid + "." + Convert.ToBase64String(HMACSHA256.HashData(RunKey(m, runId), Encoding.UTF8.GetBytes(canonical)));
    }

    public bool Verify(string runId, string canonical, string receipt)
    {
        if (string.IsNullOrEmpty(receipt)) return false;
        var i = receipt.IndexOf('.'); if (i <= 0) return false;
        var m = Master(receipt[..i]); if (m == null) return false;
        byte[] given;
        try { given = Convert.FromBase64String(receipt[(i + 1)..]); } catch { return false; }
        var want = HMACSHA256.HashData(RunKey(m, runId), Encoding.UTF8.GetBytes(canonical));
        return given.Length == want.Length && CryptographicOperations.FixedTimeEquals(given, want); // L1
    }
}

/// <summary>Replay protection for X-Nonce (gap 1: must be shared across instances, so it is backed by IDocStore).</summary>
public interface INonceCache { Task<bool> TryUse(string nonce, TimeSpan ttl); }

/// <summary>Fixed-window limiter, shared store (gap 1 / S5).</summary>
public interface IRateLimiter { Task<bool> TryAcquire(string key, int limit, TimeSpan window); }

public sealed class StoreNonceCache : INonceCache
{
    sealed class N { public long exp; }
    readonly IDocStore _s; readonly IClock _c;
    public StoreNonceCache(IDocStore s, IClock c) { _s = s; _c = c; }
    public Task<bool> TryUse(string nonce, TimeSpan ttl) => _s.Atomic("nonce:" + nonce, async () =>
    {
        var now = _c.UtcNow.Ticks;
        var e = await _s.Get<N>("nonces", nonce);
        if (e != null && e.exp > now) return false;
        await _s.Put("nonces", nonce, new N { exp = now + ttl.Ticks }); // Firestore TTL policy on exp in prod
        return true;
    });
}

public sealed class StoreRateLimiter : IRateLimiter
{
    sealed class Ctr { public int n; public long exp; }
    readonly IDocStore _s; readonly IClock _c;
    public StoreRateLimiter(IDocStore s, IClock c) { _s = s; _c = c; }
    public Task<bool> TryAcquire(string key, int limit, TimeSpan window)
    {
        long w = _c.UtcNow.Ticks / window.Ticks;
        var id = $"{key}|{w}";
        return _s.Atomic("rl:" + id, async () =>
        {
            var c = await _s.Get<Ctr>("ratelimits", id) ?? new Ctr { exp = (w + 1) * window.Ticks };
            if (c.n >= limit) return false;
            c.n++; await _s.Put("ratelimits", id, c); return true;
        });
    }
}

public static class Hash
{
    public static string Sha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    public static string Sha256B64Url(string s) => B64.Url(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    public static string NewId() => B64.Url(RandomNumberGenerator.GetBytes(16)); // 128-bit (M4)
    public static string HmacHex(byte[] key, string s) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
