namespace Crashverse.Server.Tests;

/// <summary>backend/security-followups: previous purchase-binding secret accept-until window, and ageTokenUses purge grace.</summary>
public class SecurityFollowUpTests
{
    sealed class VersionedSecrets : ISecretProvider
    {
        public Task<byte[]> Get(string name) => throw new InvalidOperationException("unpinned");
        public Task<byte[]> Get(string name, string version) => Task.FromResult(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name + "@" + version)));
    }
    sealed class FixedClock : Crashverse.Data.IClock { public DateTime UtcNow { get; set; } }

    static string ProdErrors(Dictionary<string, string> env, DateTime now)
    {
        var o = new ServerOptions { Env = CrashverseEnv.Prod };
        ServerOptions.ApplyEnvFlags(o, k => env.TryGetValue(k, out var v) ? v : null);
        return Assert.Throws<ProdStartupException>(() => ServerApp.ProdContext(o, new ServerApp.ProdAdapters { Secrets = new VersionedSecrets(), Clock = new FixedClock { UtcNow = now } })).Message;
    }

    static Dictionary<string, string> Env(string until) { var d = new Dictionary<string, string> { ["SECRET_VERSION_PURCHASE_BINDING"] = "3", ["SECRET_VERSION_PURCHASE_BINDING_PREVIOUS"] = "2", ["SECRET_VERSION_DELETION_PEPPER"] = "1" }; if (until != null) d["SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL"] = until; return d; }

    [Fact]
    public void PreviousSecret_RequiresAcceptUntil_InTheFuture()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        Assert.Contains("_ACCEPT_UNTIL must be set", ProdErrors(Env(null), now));
        Assert.Contains("_ACCEPT_UNTIL must be set", ProdErrors(Env("16/10/2026"), now));          // not ISO
        Assert.Contains("_ACCEPT_UNTIL must be set", ProdErrors(Env("2027-01-01T00:00:00+05:30"), now)); // not UTC
        Assert.Contains("_ACCEPT_UNTIL has passed", ProdErrors(Env("2026-10-15"), now));
        Assert.Contains("_ACCEPT_UNTIL has passed", ProdErrors(Env("2026-10-16T11:59:59Z"), now));
        Assert.DoesNotContain("ACCEPT_UNTIL", ProdErrors(Env("2026-10-16"), now));                // end of that UTC day
        Assert.DoesNotContain("ACCEPT_UNTIL", ProdErrors(Env("2027-04-30T00:00:00Z"), now));
        var noPrev = Env(null); noPrev.Remove("SECRET_VERSION_PURCHASE_BINDING_PREVIOUS");
        Assert.DoesNotContain("ACCEPT_UNTIL", ProdErrors(noPrev, now));                              // only required with _PREVIOUS
    }

    [Fact]
    public async Task PreviousSecret_UseIsLogged_AndStopsAfterAcceptUntil()
    {
        await using var h = await Harness.Start();
        var logs = new List<string>(); h.X.SecurityLog = logs.Add;
        var auth = new AuthService(h.X); var s = await h.Guest();
        var oldId = auth.ObfuscatedId(s.pid);
        h.X.Opt.PreviousObfuscationPepper = h.X.Opt.ObfuscationPepper; h.X.Opt.PreviousObfuscationPepperVersion = h.X.Opt.ObfuscationPepperVersion;
        h.X.Opt.ObfuscationPepper = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32); h.X.Opt.ObfuscationPepperVersion = "v9";
        h.X.Opt.PreviousAcceptUntilUnixMs = h.X.NowMs + 60_000;

        Assert.True(auth.MatchesObfuscatedId(s.pid, auth.ObfuscatedId(s.pid)));
        Assert.Empty(logs);                                            // current secret: no log
        Assert.True(auth.MatchesObfuscatedId(s.pid, oldId));
        Assert.Single(logs); Assert.Contains("PREVIOUS secret", logs[0]); Assert.DoesNotContain(s.pid, logs[0]); // pseudonymised
        Assert.True(auth.MatchesObfuscatedId(s.pid, oldId)); Assert.Equal(2, logs.Count); // every use is logged
        h.Advance(TimeSpan.FromMinutes(2));
        Assert.False(auth.MatchesObfuscatedId(s.pid, oldId)); Assert.Equal(2, logs.Count); // window closed
    }

    [Fact]
    public async Task AgeTokenUses_PurgedOneHourAfterExpiry_SingleUseEnforcedUntilThen()
    {
        await using var h = await Harness.Start();
        var comp = new ComplianceService(h.X); var life = new DataLifecycleService(h.X);
        long expSec = h.X.NowMs / 1000 + 600;
        await comp.ConsumeAgeToken(new AgeGateDoc { jti = "j1", exp = expSec });
        h.Advance(TimeSpan.FromMinutes(11));                           // past expiry, inside the 1 h grace
        await life.Retention();
        Assert.NotNull(await h.X.Db.Get<IdentityDoc>("ageTokenUses", "j1"));
        await Assert.ThrowsAsync<ApiError>(() => comp.ConsumeAgeToken(new AgeGateDoc { jti = "j1", exp = expSec })); // still single-use
        h.Advance(TimeSpan.FromMinutes(60));                           // expiry + 61 min
        Assert.True((await life.Retention()).ageTokenUsesExpired >= 1);
        Assert.Null(await h.X.Db.Get<IdentityDoc>("ageTokenUses", "j1"));
    }

    [Fact] // Security condition (a): ACCEPT_UNTIL is capped at startup (default 400 days, configurable)
    public void AcceptUntil_CappedAtStartup()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        Assert.DoesNotContain("ACCEPT_UNTIL", ProdErrors(Env("2027-11-19"), now));                 // ~399 days
        Assert.Contains("more than 400 days away", ProdErrors(Env("2027-11-21"), now));            // ~401 days
        var tight = Env("2027-01-31"); tight["SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL_MAX_DAYS"] = "90";
        Assert.Contains("more than 90 days away", ProdErrors(tight, now));
        var bad = Env("2027-11-21"); bad["SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL_MAX_DAYS"] = "abc"; // invalid = default 400
        Assert.Contains("more than 400 days away", ProdErrors(bad, now));
        var huge = Env("2028-11-01"); huge["SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL_MAX_DAYS"] = "5000"; // clamped to 730
        Assert.Contains("more than 730 days away", ProdErrors(huge, now));
        var under = Env("2028-09-01"); under["SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL_MAX_DAYS"] = "5000"; // ~686 days: allowed
        Assert.DoesNotContain("ACCEPT_UNTIL", ProdErrors(under, now));
    }

    [Fact] // Security condition (b): the runtime check fails closed without ACCEPT_UNTIL
    public async Task PreviousSecret_NeverAcceptedWithoutAcceptUntil()
    {
        await using var h = await Harness.Start();
        var logs = new List<string>(); h.X.SecurityLog = logs.Add;
        var auth = new AuthService(h.X); var s = await h.Guest();
        var oldId = auth.ObfuscatedId(s.pid);
        h.X.Opt.PreviousObfuscationPepper = h.X.Opt.ObfuscationPepper; h.X.Opt.PreviousObfuscationPepperVersion = h.X.Opt.ObfuscationPepperVersion;
        h.X.Opt.ObfuscationPepper = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32); h.X.Opt.ObfuscationPepperVersion = "v9";
        h.X.Opt.PreviousAcceptUntilUnixMs = null;
        Assert.False(auth.MatchesObfuscatedId(s.pid, oldId)); Assert.Empty(logs);
        Assert.True(auth.MatchesObfuscatedId(s.pid, auth.ObfuscatedId(s.pid))); // current secret unaffected
        h.X.Opt.PreviousAcceptUntilUnixMs = h.X.NowMs + 60_000;
        Assert.True(auth.MatchesObfuscatedId(s.pid, oldId)); Assert.Single(logs);
    }
}
