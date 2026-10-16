using Crashverse.Backend;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>Agreed interface. provider is a lowercase string: google | facebook | email | x | guest (guest: re-auth only).
/// token = Google ID token, Facebook access token, X authorization code, or (email/guest re-auth) the password / current refresh token.</summary>
public class LinkCredential { public string provider; public string token; public string codeVerifier; public string redirectUri; public string nonce; }
public sealed class LinkRequest : LinkCredential { public string conflictResolution; }
public static class Providers
{
    public static readonly string[] All = { "google", "facebook", "email", "x" };
    public static string Norm(string p) => (p ?? "").Trim().ToLowerInvariant();
}

/// <summary>Guest/refresh sessions, provider linking (H4), email accounts, X behind a flag. M4: 128-bit server ids.</summary>
public sealed class AuthService
{
    readonly Ctx _x; public AuthService(Ctx x) { _x = x; }

    public async Task<AccountDoc> Account(string pid) => await _x.Db.Get<AccountDoc>("accounts", pid) ?? throw new ApiError(401, "UNAUTHENTICATED", "Unknown account");

    public AccountStateEx View(AccountDoc a) => new()
    {
        playerId = a.playerId, displayName = a.displayName, linked = !a.isGuest, provider = a.primaryProvider,
        providers = new List<string>(a.providers), identities = new List<string>(a.providers), obfuscatedAccountId = ObfuscatedId(a.playerId), friendCode = a.friendCode,
        status = a.status == "deleted_pending" ? "scheduled" : "active", purgeAt = a.purgeAtUnixMs / 1000, hideName = a.hideName
    };

    /// <summary>Play Billing obfuscatedAccountId = HMAC(pepper, playerId). The pepper is versioned, never rotated (review gap 7).</summary>
    public string ObfuscatedId(string pid) => _x.Opt.ObfuscationPepperVersion + "_" + Hash.HmacHex(_x.Opt.ObfuscationPepper, pid)[..40];

    public string VerifyAccess(string bearer)
    {
        var c = _x.Tokens.Verify("access", bearer, out var err);
        if (c == null) throw new ApiError(401, err == "token expired" ? "TOKEN_EXPIRED" : "UNAUTHENTICATED", err);
        return (string)c["sub"];
    }

    async Task<AuthResponse> NewSession(AccountDoc a)
    {
        var fam = Hash.NewId(); var secret = Hash.NewId();
        await _x.Db.Put("sessions", fam, new SessionDoc { familyId = fam, playerId = a.playerId, currentHash = Hash.Sha256Hex(secret), expUnixMs = _x.NowMs + (long)TimeSpan.FromDays(_x.Opt.RefreshTtlDays).TotalMilliseconds });
        return new AuthResponse { accessToken = Access(a.playerId, fam), refreshToken = fam + "." + secret, account = View(a) };
    }
    string Access(string pid, string fam) => _x.Tokens.Issue("access", new JsonObject { ["sub"] = pid, ["fam"] = fam }, TimeSpan.FromMinutes(_x.Opt.AccessTtlMinutes));

    async Task<AccountDoc> CreateAccount(string device, bool guest = true)
    {
        var a = new AccountDoc { playerId = Hash.NewId(), displayName = "Guest Wrecker", createdUnixMs = _x.NowMs, deviceInstallId = device, isGuest = guest };
        for (int i = 0; i < 10; i++)
        {
            var code = FriendCode();
            if (await _x.Db.TryCreate("friendCodes", code, new IdentityDoc { key = code, playerId = a.playerId })) { a.friendCode = code; break; }
        }
        if (!await _x.Db.TryCreate("accounts", a.playerId, a)) throw new ApiError(500, "INTERNAL", "id collision", true);
        return a;
    }
    static string FriendCode() { const string A = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; var b = RandomNumberGenerator.GetBytes(8); return new string(b.Select(x => A[x % A.Length]).ToArray()); }

    public async Task<AuthResponse> Guest(string deviceInstallId, string ageToken)
    {
        var g = new ComplianceService(_x).VerifyAgeToken(ageToken);
        if (string.IsNullOrWhiteSpace(deviceInstallId) || deviceInstallId.Length > 128) throw new ApiError(400, "BAD_REQUEST", "deviceInstallId required");
        var a = await CreateAccount(deviceInstallId);
        await new ComplianceService(_x).ApplyAge(a, g); await _x.Db.Put("accounts", a.playerId, a);
        return await NewSession(a);
    }

    /// <summary>Rotating refresh tokens; presenting an already-rotated secret revokes the family (reuse detection).</summary>
    public Task<AuthResponse> Refresh(string refreshToken)
    {
        var parts = (refreshToken ?? "").Split('.');
        if (parts.Length != 2) throw new ApiError(401, "UNAUTHENTICATED", "bad refresh token");
        return _x.Db.Atomic("session:" + parts[0], async () =>
        {
            var s = await _x.Db.Get<SessionDoc>("sessions", parts[0]);
            if (s == null || s.revoked || s.expUnixMs < _x.NowMs) throw new ApiError(401, "UNAUTHENTICATED", "session revoked or expired");
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(s.currentHash), Convert.FromHexString(Hash.Sha256Hex(parts[1]))))
            {
                s.revoked = true; await _x.Db.Put("sessions", s.familyId, s);
                await _x.Audit("system", "refresh_reuse_revoked", s.playerId, s.familyId);
                throw new ApiError(401, "UNAUTHENTICATED", "refresh token reuse detected; session revoked");
            }
            var secret = Hash.NewId(); s.currentHash = Hash.Sha256Hex(secret); await _x.Db.Put("sessions", s.familyId, s);
            var a = await Account(s.playerId);
            return new AuthResponse { accessToken = Access(a.playerId, s.familyId), refreshToken = s.familyId + "." + secret, account = View(a) };
        });
    }

    public async Task<bool> SessionActive(string bearer)
    {
        var c = _x.Tokens.Verify("access", bearer, out _); if (c == null) return false;
        var s = await _x.Db.Get<SessionDoc>("sessions", (string)c["fam"]);
        return s != null && !s.revoked;
    }

    async Task RevokeAll(string pid, string exceptFamily)
    {
        foreach (var s in await _x.Db.List<SessionDoc>("sessions"))
            if (s.playerId == pid && s.familyId != exceptFamily && !s.revoked) { s.revoked = true; await _x.Db.Put("sessions", s.familyId, s); }
    }

    /// <summary>Google requires a server-issued nonce bound to the player (replayed ID tokens from other apps fail).</summary>
    public async Task<string> LinkNonce(string pid)
    {
        var n = Hash.NewId();
        await _x.Db.Put("linkNonces", pid, new IdentityDoc { key = n, playerId = pid, linkedUnixMs = _x.NowMs });
        return n;
    }

    /// <summary>H4: verify the provider credential on the server. No credential, no link.</summary>
    public async Task<ProviderIdentity> VerifyProvider(string pid, string provider, LinkCredential c)
    {
        ProviderIdentity id = null;
        provider = Providers.Norm(provider ?? c?.provider);
        switch (provider)
        {
            case "google":
                if (string.IsNullOrEmpty(c?.token)) break;
                string expected = null;
                if (pid != null)
                {
                    var n = await _x.Db.Get<IdentityDoc>("linkNonces", pid);
                    if (n == null || _x.NowMs - n.linkedUnixMs > 600_000) throw new ApiError(400, "PROVIDER_TOKEN_INVALID", "request a link nonce first");
                    expected = Hash.Sha256Hex(n.key);
                }
                id = await _x.Google.Verify(c.token, expected);
                if (id != null && pid != null) await _x.Db.Delete("linkNonces", pid); // single use
                break;
            case "facebook":
                if (!string.IsNullOrEmpty(c?.token)) id = await _x.Facebook.Verify(c.token); break;
            case "x":
                if (!_x.Opt.XEnabled) throw new ApiError(404, "FEATURE_DISABLED", "X login is disabled");
                if (!string.IsNullOrEmpty(c?.token)) id = await _x.X.Exchange(c.token, c.codeVerifier, c.redirectUri); break;
            default: throw new ApiError(400, "BAD_PROVIDER", "unsupported provider");
        }
        if (id == null || string.IsNullOrEmpty(id.subject)) throw new ApiError(401, "PROVIDER_TOKEN_INVALID", "provider token could not be verified");
        id.provider = provider;
        return id;
    }

    public async Task<object> Link(string pid, LinkRequest r)
    {
        var a = await Account(pid);
        ComplianceService.Enforce(a, "link", _x.ConsentAges);
        var id = await VerifyProvider(pid, Providers.Norm(r?.provider), r);
        var key = $"{id.provider}:{id.subject}";
        var existing = await _x.Db.Get<IdentityDoc>("identities", key);
        if (existing != null && existing.playerId != pid)
        {
            if (r.conflictResolution == "switch_to_existing")
            {
                await _x.Audit(pid, "link_conflict_switch", existing.playerId, key);
                return await NewSession(await Account(existing.playerId));
            }
            var other = await Account(existing.playerId);
            throw new ApiError(409, "IDENTITY_IN_USE", "This login already belongs to another account", false,
                new { current = new { a.playerId, a.displayName }, existing = new { other.playerId, other.displayName } });
        }
        if (existing == null && !await _x.Db.TryCreate("identities", key, new IdentityDoc { key = key, playerId = pid, linkedUnixMs = _x.NowMs }))
            throw new ApiError(409, "IDENTITY_IN_USE", "This login already belongs to another account");
        return await _x.Db.Atomic("account:" + pid, async () =>
        {
            a = await Account(pid);
            bool wasGuest = a.isGuest;
            if (!a.providers.Contains(id.provider)) a.providers.Add(id.provider);
            if (wasGuest) { a.primaryProvider = id.provider; a.displayName = id.provider switch { "google" => "Google", "facebook" => "Facebook", "x" => "X", _ => "Email" } + " Wrecker"; }
            a.isGuest = false;
            await _x.Db.Put("accounts", pid, a);
            await _x.Audit(pid, "link", pid, key);
            if (wasGuest) await new Leaderboards(_x).OnLinked(pid); // SG-19 / SG-36
            return (object)View(a);
        });
    }

    public async Task<AccountStateEx> Unlink(string pid, string provider, LinkCredential recent)
    {
        var a = await Account(pid);
        if (!a.providers.Contains(provider)) throw new ApiError(404, "NOT_LINKED", "provider not linked");
        if (a.providers.Count <= 1) throw new ApiError(409, "LAST_LOGIN_METHOD", "cannot unlink the last login method");
        if (provider == "email") throw new ApiError(400, "BAD_REQUEST", "use email reset/remove flow");
        var id = await VerifyProvider(pid, provider, recent); // re-auth
        var key = $"{provider}:{id.subject}";
        var doc = await _x.Db.Get<IdentityDoc>("identities", key);
        if (doc?.playerId != pid) throw new ApiError(403, "REAUTH_FAILED", "re-authentication does not match the linked identity");
        await _x.Db.Delete("identities", key);
        a.providers.Remove(provider); if (a.primaryProvider == provider) a.primaryProvider = a.providers[0];
        await _x.Db.Put("accounts", pid, a);
        await RevokeAll(pid, null);
        await _x.Audit(pid, "unlink", pid, key);
        return View(a);
    }

    public async Task<AuthResponse> SignIn(string provider, LinkCredential c, string device, string ageToken = null)
    {
        var g = new ComplianceService(_x).VerifyAgeToken(ageToken);
        provider = Providers.Norm(provider ?? c?.provider);
        var id = await VerifyProvider(null, provider, c);
        var key = $"{provider}:{id.subject}";
        var existing = await _x.Db.Get<IdentityDoc>("identities", key);
        if (existing != null)
        {
            var ex = await Account(existing.playerId); await new ComplianceService(_x).ApplyAge(ex, g); await _x.Db.Put("accounts", ex.playerId, ex);
            return await NewSession(ex);
        }
        var a = await CreateAccount(device, guest: false);
        await new ComplianceService(_x).ApplyAge(a, g);
        if (!await _x.Db.TryCreate("identities", key, new IdentityDoc { key = key, playerId = a.playerId, linkedUnixMs = _x.NowMs }))
            throw new ApiError(409, "IDENTITY_IN_USE", "retry sign-in", true);
        a.providers.Add(provider); a.primaryProvider = provider; a.displayName = provider + " Wrecker";
        await _x.Db.Put("accounts", a.playerId, a);
        return await NewSession(a);
    }

    // ---------------- Email ----------------
    static string NormEmail(string e) => (e ?? "").Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormKC);
    static void CheckEmail(string e) { if (e.Length < 5 || e.Length > 254 || !e.Contains('@') || e.IndexOf('@') == 0) throw new ApiError(400, "BAD_EMAIL", "invalid email"); }
    static void CheckPassword(string p) { if (p == null || p.Length < 10 || p.Length > 128) throw new ApiError(400, "WEAK_PASSWORD", "password must be 10-128 characters"); }
    (string salt, string hash) HashPw(string pw, byte[] salt = null)
    {
        salt ??= RandomNumberGenerator.GetBytes(16);
        // PBKDF2-SHA256 (built in). Argon2id is the preferred upgrade (review §4); the iteration count is stored per record.
        var h = Rfc2898DeriveBytes.Pbkdf2(pw, salt, _x.Opt.PasswordIterations, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(h));
    }
    bool CheckPw(EmailCredDoc d, string pw)
    {
        var h = Rfc2898DeriveBytes.Pbkdf2(pw ?? "", Convert.FromBase64String(d.salt), d.iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(h, Convert.FromBase64String(d.hash));
    }
    static string Code() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>Attach email+password to the signed-in account. Generic response whether or not the email exists.</summary>
    public async Task<object> EmailRegister(string pid, string email, string password)
    {
        email = NormEmail(email); CheckEmail(email); CheckPassword(password);
        var (salt, hash) = HashPw(password); var code = Code();
        var doc = new EmailCredDoc { email = email, playerId = pid, salt = salt, hash = hash, iterations = _x.Opt.PasswordIterations, verifyCodeHash = Hash.Sha256Hex(code) };
        if (await _x.Db.TryCreate("emailCreds", email, doc))
            await _x.Email.Send(email, "Verify your CRASHVERSE email", "Code: " + code);
        return new { message = "If this email can be used, a verification code has been sent." };
    }

    public async Task<AccountStateEx> EmailVerify(string pid, string email, string code)
    {
        email = NormEmail(email);
        var d = await _x.Db.Get<EmailCredDoc>("emailCreds", email);
        if (d == null || d.playerId != pid || d.verified || d.verifyCodeHash != Hash.Sha256Hex(code ?? "")) throw new ApiError(400, "INVALID_CODE", "invalid or expired code");
        d.verified = true; d.verifyCodeHash = null; await _x.Db.Put("emailCreds", email, d);
        var key = "email:" + email;
        if (!await _x.Db.TryCreate("identities", key, new IdentityDoc { key = key, playerId = pid, linkedUnixMs = _x.NowMs }))
            throw new ApiError(409, "IDENTITY_IN_USE", "email already linked");
        return await _x.Db.Atomic("account:" + pid, async () =>
        {
            var a = await Account(pid); bool wasGuest = a.isGuest;
            if (!a.providers.Contains("email")) a.providers.Add("email");
            if (wasGuest) { a.primaryProvider = "email"; a.displayName = "Email Wrecker"; }
            a.isGuest = false; await _x.Db.Put("accounts", pid, a);
            await _x.Audit(pid, "link", pid, key);
            if (wasGuest) await new Leaderboards(_x).OnLinked(pid);
            return View(a);
        });
    }

    public async Task<AuthResponse> EmailLogin(string email, string password)
    {
        email = NormEmail(email);
        var d = await _x.Db.Get<EmailCredDoc>("emailCreds", email);
        bool ok = d != null && d.verified && CheckPw(d, password);
        if (d == null) HashPw(password ?? "x"); // equalise timing
        if (!ok) throw new ApiError(401, "INVALID_CREDENTIALS", "email or password is incorrect");
        return await NewSession(await Account(d.playerId));
    }

    public async Task<object> EmailResetRequest(string email)
    {
        email = NormEmail(email);
        var d = await _x.Db.Get<EmailCredDoc>("emailCreds", email);
        if (d != null && d.verified)
        {
            var code = Code(); d.resetCodeHash = Hash.Sha256Hex(code); d.resetExpUnixMs = _x.NowMs + 15 * 60_000;
            await _x.Db.Put("emailCreds", email, d);
            await _x.Email.Send(email, "Reset your CRASHVERSE password", "Code: " + code);
        }
        return new { message = "If an account exists for this email, a reset code has been sent." };
    }

    public async Task<object> EmailReset(string email, string code, string newPassword)
    {
        email = NormEmail(email); CheckPassword(newPassword);
        var d = await _x.Db.Get<EmailCredDoc>("emailCreds", email);
        if (d == null || d.resetCodeHash == null || d.resetExpUnixMs < _x.NowMs || d.resetCodeHash != Hash.Sha256Hex(code ?? ""))
            throw new ApiError(400, "INVALID_CODE", "invalid or expired code");
        var (salt, hash) = HashPw(newPassword);
        d.salt = salt; d.hash = hash; d.iterations = _x.Opt.PasswordIterations; d.resetCodeHash = null;
        await _x.Db.Put("emailCreds", email, d);
        await RevokeAll(d.playerId, null); // review §4: revoke sessions on password change
        await _x.Audit(d.playerId, "password_reset", d.playerId, "");
        return new { message = "Password updated. Please sign in again." };
    }

    // ---------------- Re-auth, deletion, keep-active (Deletion_AgeGate_Requirements A3, A5, C1, C2) ----------------
    public const int ReauthTtlMinutes = 5;

    /// <summary>Proves control of the account right now. Linked: provider credential whose identity maps to this player,
    /// or email+password. Guest (no linked login): the current refresh token (C1). Returns a single-use token valid 5 minutes.</summary>
    public async Task<object> Reauth(string pid, LinkCredential c)
    {
        if (c == null || string.IsNullOrEmpty(c.token)) throw new ApiError(400, "BAD_REQUEST", "credential required");
        var a = await Account(pid); var prov = Providers.Norm(c.provider); bool ok;
        if (prov == "guest")
        {
            if (!a.isGuest) throw new ApiError(401, "REAUTH_FAILED", "linked accounts must re-authenticate with a linked login");
            var parts = c.token.Split('.'); var s = parts.Length == 2 ? await _x.Db.Get<SessionDoc>("sessions", parts[0]) : null;
            ok = s != null && !s.revoked && s.playerId == pid && s.expUnixMs > _x.NowMs && s.currentHash == Hash.Sha256Hex(parts[1]);
        }
        else if (prov == "email")
        {
            var d = (await _x.Db.List<EmailCredDoc>("emailCreds")).FirstOrDefault(e => e.playerId == pid && e.verified);
            ok = d != null && CheckPw(d, c.token);
            if (d == null) HashPw(c.token);
        }
        else
        {
            if (!a.providers.Contains(prov)) throw new ApiError(401, "REAUTH_FAILED", "provider not linked to this account");
            ProviderIdentity id;
            if (prov == "google") { id = await _x.Google.Verify(c.token, null); if (id != null) id.provider = "google"; } // no link nonce for re-auth
            else { try { id = await VerifyProvider(null, prov, c); } catch (ApiError e) when (e.Code == "PROVIDER_TOKEN_INVALID") { id = null; } }
            var doc = id == null ? null : await _x.Db.Get<IdentityDoc>("identities", $"{prov}:{id.subject}");
            ok = doc?.playerId == pid;
        }
        if (!ok) { await _x.Audit(pid, "reauth_failed", pid, prov); throw new ApiError(401, "REAUTH_FAILED", "re-authentication failed"); }
        var exp = _x.Now.AddMinutes(ReauthTtlMinutes);
        var tok = _x.Tokens.Issue("reauth", new JsonObject { ["sub"] = pid, ["jti"] = Hash.NewId() }, TimeSpan.FromMinutes(ReauthTtlMinutes));
        await _x.Audit(pid, "reauth", pid, prov);
        return new ReauthResponse { reauthToken = tok, expiresAt = Ctx.Unix(exp) };
    }

    async Task ConsumeReauth(string pid, string token)
    {
        var c = string.IsNullOrEmpty(token) ? null : _x.Tokens.Verify("reauth", token, out _);
        if (c == null || (string)c["sub"] != pid || !await _x.Nonces.TryUse("reauth:" + (string)c["jti"], TimeSpan.FromMinutes(ReauthTtlMinutes + 1)))
            throw new ApiError(401, "REAUTH_REQUIRED", "a fresh re-authentication (under 5 minutes, single use) is required");
    }

    /// <summary>A3/A5: needs a fresh reauth; schedules the purge 7 days out. Sessions stay valid so the player can cancel in the grace period.</summary>
    public async Task<object> DeleteAccount(string pid, string reauthToken)
    {
        var acc = await Account(pid);
        // C1: a guest has no other credential, so the bearer session is the proof (client passes null). Linked accounts must re-auth (A3).
        if (!(acc.isGuest && string.IsNullOrEmpty(reauthToken))) await ConsumeReauth(pid, reauthToken);
        return await _x.Db.Atomic("account:" + pid, async () =>
        {
            var a = await Account(pid);
            if (a.status != "deleted_pending") { a.status = "deleted_pending"; a.purgeAtUnixMs = Ctx.UnixMs(_x.Now.AddDays(7)); await _x.Db.Put("accounts", pid, a); await _x.Audit(pid, "delete_requested", pid, ""); }
            return (object)new DeleteAccountResponse { status = "scheduled", purgeAt = a.purgeAtUnixMs / 1000 };
        });
    }

    public async Task<object> CancelDelete(string pid) => await _x.Db.Atomic("account:" + pid, async () =>
    {
        var a = await Account(pid);
        if (a.status == "deleted_pending")
        {
            if (a.purgeAtUnixMs <= _x.NowMs) throw new ApiError(410, "ACCOUNT_PURGED", "grace period is over");
            a.status = "active"; a.purgeAtUnixMs = 0; await _x.Db.Put("accounts", pid, a); await _x.Audit(pid, "delete_cancelled", pid, "");
        }
        return (object)new StatusResponse { status = "active" };
    });

    public async Task<object> KeepActive(string pid) => await _x.Db.Atomic("account:" + pid, async () =>
    {
        var a = await Account(pid); a.lastActiveUnixMs = _x.NowMs; await _x.Db.Put("accounts", pid, a);
        return (object)new KeepActiveResponse { lastActiveAt = Ctx.Unix(_x.Now) };
    });

    public async Task<object> SetVisibility(string pid, bool hide) => await _x.Db.Atomic("account:" + pid, async () =>
    {
        var a = await Account(pid);
        if (!hide) ComplianceService.Enforce(a, "name", _x.ConsentAges); // E5: unconsented minors keep the generated name
        a.hideName = hide; await _x.Db.Put("accounts", pid, a);
        await _x.Audit(pid, "visibility", pid, hide ? "hide" : "show");
        return (object)new VisibilityResponse { hideName = a.hideName };
    });

    public static string PublicName(AccountDoc a) => a == null ? null : a.hideName || a.minorRestricted ? "Wrecker #" + a.playerId[..Math.Min(4, a.playerId.Length)].ToUpperInvariant() : a.displayName;
}
