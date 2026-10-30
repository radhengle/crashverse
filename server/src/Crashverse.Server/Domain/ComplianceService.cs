using Crashverse.Backend;
using System.Text.Json.Nodes;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>Per-market digital age of consent, loaded from server/config/consent_ages.json (NEEDS LEGAL REVIEW). Missing/invalid file fails startup.</summary>
public sealed class ConsentAgeTable
{
    public int Default = 13; public Dictionary<string, int> Markets = new(); public string Status;
    public int For(string country) => Markets.TryGetValue((country ?? "").ToUpperInvariant(), out var a) ? a : Default;

    public static ConsentAgeTable Load(string path)
    {
        using var d = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var r = d.RootElement; var t = new ConsentAgeTable { Default = r.GetProperty("default").GetInt32(), Status = r.TryGetProperty("_status", out var s) ? s.GetString() : null };
        foreach (var m in r.GetProperty("markets").EnumerateObject())
        {
            int a = m.Value.GetInt32();
            if (m.Name.Length != 2 || a < 13 || a > 18) throw new InvalidDataException($"consent_ages.json: bad entry {m.Name}={a}");
            t.Markets[m.Name.ToUpperInvariant()] = a;
        }
        if (t.Default < 13 || t.Default > 18) throw new InvalidDataException("consent_ages.json: bad default");
        return t;
    }

    public static string FindPath()
    {
        var env = Environment.GetEnvironmentVariable("CONSENT_AGES_PATH");
        if (!string.IsNullOrEmpty(env)) return env;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "server", "config", "consent_ages.json");
            if (File.Exists(p)) return p;
            p = Path.Combine(d.FullName, "config", "consent_ages.json");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("consent_ages.json not found; set CONSENT_AGES_PATH");
    }
}

/// <summary>Neutral age gate (E1–E3) and parental consent. The date of birth is never stored: only band, country and the consent flag.</summary>
public sealed class ComplianceService
{
    readonly Ctx _x; public ComplianceService(Ctx x) { _x = x; }

    public int ConsentAgeFor(string country) => _x.ConsentAges.For(country);

    public sealed class AgeGateReq { public int birthYear; public int birthMonth; public string country; }

    /// <summary>Unauthenticated (it runs before any account exists). Age is counted in whole months against server UTC "now";
    /// the birth day is not collected, so a player is treated as having their birthday at the end of the birth month (conservative).</summary>
    public object AgeGate(AgeGateReq r)
    {
        var now = _x.Now;
        if (r == null || r.birthMonth < 1 || r.birthMonth > 12 || r.birthYear < 1900 || r.birthYear > now.Year) throw new ApiError(400, "BAD_REQUEST", "birthYear/birthMonth out of range");
        var country = (r.country ?? "").Trim().ToUpperInvariant();
        if (country.Length != 2 || !country.All(char.IsAsciiLetterUpper)) throw new ApiError(400, "BAD_REQUEST", "country must be ISO 3166-1 alpha-2");
        int months = (now.Year - r.birthYear) * 12 + (now.Month - r.birthMonth) - 1; // -1: birthday assumed at end of month
        if (months < 0) throw new ApiError(400, "BAD_REQUEST", "birth date in the future");
        int age = months / 12;
        string band = age < 13 ? "under13" : age < 16 ? "13to15" : age < 18 ? "16to17" : "adult"; // PM decision on PR #1
        bool consent = age < 13 || age < ConsentAgeFor(country);
        return new AgeGateResponse { band = band, requiresParentalConsent = consent };
    }

    public sealed class ConsentReq { public string parentEmail; }

    public async Task<object> RequestConsent(string pid, string parentEmail)
    {
        var email = (parentEmail ?? "").Trim().ToLowerInvariant();
        if (email.Length < 5 || email.Length > 254 || email.IndexOf('@') <= 0 || email.IndexOf('@') == email.Length - 1) throw new ApiError(400, "BAD_EMAIL", "invalid email");
        return await _x.Db.Atomic("consent:" + pid, async () =>
        {
            var d = await _x.Db.Get<ConsentDoc>("consents", pid);
            if (d?.status == "granted") return (object)new ConsentRequestResponse { status = "granted", requestId = d.requestId };
            var id = Hash.NewId();
            d = new ConsentDoc { requestId = id, playerId = pid, parentEmailHash = Hash.Sha256Hex(email), status = "pending", requestedUnixMs = _x.NowMs };
            await _x.Db.Put("consents", pid, d);
            var tok = _x.Tokens.Issue("consent", new JsonObject { ["sub"] = pid, ["rid"] = id }, TimeSpan.FromDays(7));
            await _x.ConsentMailer.SendConsentRequest(email, id, tok); // parent email is not stored, only its hash
            await _x.Audit(pid, "consent_requested", pid, id);
            return (object)new ConsentRequestResponse { status = "pending", requestId = id };
        });
    }

    public async Task<object> Status(string pid) => new ConsentStatusResponse { status = (await _x.Db.Get<ConsentDoc>("consents", pid))?.status ?? "none" };

    /// <summary>Called from the parent-facing page (not yet built) with the token from the email. Exposed as an internal endpoint for now.</summary>
    public async Task<object> Decide(string token, bool grant)
    {
        var c = _x.Tokens.Verify("consent", token, out var err) ?? throw new ApiError(401, "INVALID_TOKEN", "consent token: " + err);
        var pid = (string)c["sub"];
        return await _x.Db.Atomic("consent:" + pid, async () =>
        {
            var d = await _x.Db.Get<ConsentDoc>("consents", pid);
            if (d == null || d.requestId != (string)c["rid"] || d.status != "pending") throw new ApiError(409, "CONSENT_NOT_PENDING", "request is not pending");
            d.status = grant ? "granted" : "denied"; d.decidedUnixMs = _x.NowMs; await _x.Db.Put("consents", pid, d);
            await _x.Audit("parent", grant ? "consent_granted" : "consent_denied", pid, d.requestId);
            return (object)new ConsentStatusResponse { status = d.status };
        });
    }
}
