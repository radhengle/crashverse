using Crashverse.Backend;
using System.Text.Json.Nodes;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>Neutral age gate (E1–E3) and parental consent. The date of birth is never stored: only band, country and the consent flag.</summary>
public sealed class ComplianceService
{
    readonly Ctx _x; public ComplianceService(Ctx x) { _x = x; }

    /// <summary>Digital age of consent by country (GDPR Art. 8 member-state choices; UK 13; IN 18 under DPDP; default 13). NEEDS LEGAL REVIEW.</summary>
    static readonly Dictionary<string, int> ConsentAge = new()
    {
        ["AT"] = 14, ["BE"] = 13, ["BG"] = 14, ["HR"] = 16, ["CY"] = 14, ["CZ"] = 15, ["DK"] = 13, ["EE"] = 13, ["FI"] = 13, ["FR"] = 15, ["DE"] = 16,
        ["GR"] = 15, ["HU"] = 16, ["IE"] = 16, ["IT"] = 14, ["LV"] = 13, ["LT"] = 14, ["LU"] = 16, ["MT"] = 13, ["NL"] = 16, ["PL"] = 16, ["PT"] = 13,
        ["RO"] = 16, ["SK"] = 16, ["SI"] = 15, ["ES"] = 14, ["SE"] = 13, ["GB"] = 13, ["US"] = 13, ["IN"] = 18,
    };
    public static int ConsentAgeFor(string country) => ConsentAge.TryGetValue(country ?? "", out var a) ? a : 13;

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
        string band = age < 13 ? "under13" : age < 16 ? "13to15" : "16plus";
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
