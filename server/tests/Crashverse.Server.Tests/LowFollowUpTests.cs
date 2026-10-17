using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Crashverse.Server.Tests;

/// <summary>Security Admin's Low follow-ups from the "Re-review 9b5574b" section.</summary>
public class LowFollowUpTests
{
    // ---- Low-1: configured listen URLs are checked before Kestrel binds ----
    [Theory]
    [InlineData("--urls", "http://0.0.0.0:5055", false)]
    [InlineData("--urls", "http://localhost:5055;http://*:5056", false)]
    [InlineData("--urls", "http://127.0.0.1:5055;http://[::1]:5056", true)]
    [InlineData("--Kestrel:Endpoints:Pub:Url", "http://192.168.1.10:5055", false)]
    [InlineData("--Kestrel:Endpoints:Loc:Url", "http://localhost:5055", true)]
    [InlineData("--http_ports", "8080", false)]
    public void Low1_ConfiguredUrls_CheckedBeforeBind(string key, string value, bool ok)
    {
        var app = WebApplication.CreateBuilder(new[] { key, value }).Build(); // built, never started: nothing is bound
        if (ok) DevGuard.CheckConfiguredLoopbackOnly(app.Configuration);
        else Assert.Throws<ProdStartupException>(() => DevGuard.CheckConfiguredLoopbackOnly(app.Configuration));
    }

    [Fact]
    public void Low1_NoConfiguredUrls_IsKestrelDefaultLoopback()
    {
        var c = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()).Build();
        Assert.Empty(DevGuard.ConfiguredUrls(c));
        DevGuard.CheckConfiguredLoopbackOnly(c);
    }

    // ---- Low-2: ageTokenUses records expire and are swept by Retention() ----
    [Fact]
    public async Task Low2_AgeTokenUses_HaveExpiry_AndAreSweptAfterIt()
    {
        await using var h = await Harness.Start();
        var comp = new ComplianceService(h.X);
        long expSec = h.X.NowMs / 1000 + 600;
        await comp.ConsumeAgeToken(new AgeGateDoc { jti = "j-old", exp = expSec });
        await comp.ConsumeAgeToken(new AgeGateDoc { jti = "j-new", exp = expSec + 3600 });
        Assert.Equal(expSec * 1000, (await h.X.Db.Get<IdentityDoc>("ageTokenUses", "j-old")).expiresUnixMs);
        var r0 = await new DataLifecycleService(h.X).Retention();
        Assert.Equal(0, r0.ageTokenUsesExpired);
        h.Advance(TimeSpan.FromMinutes(11));
        var r1 = await new DataLifecycleService(h.X).Retention();
        Assert.Equal(1, r1.ageTokenUsesExpired);
        Assert.Null(await h.X.Db.Get<IdentityDoc>("ageTokenUses", "j-old"));
        Assert.NotNull(await h.X.Db.Get<IdentityDoc>("ageTokenUses", "j-new")); // still inside its lifetime: reuse stays blocked
        await Assert.ThrowsAsync<ApiError>(() => comp.ConsumeAgeToken(new AgeGateDoc { jti = "j-new", exp = expSec + 3600 }));
    }

    // ---- Low-3: pinned Secret Manager versions, current + previous purchase-binding secret ----
    sealed class VersionedSecrets : ISecretProvider
    {
        public readonly List<string> Asked = new(); public HashSet<string> Missing = new();
        public Task<byte[]> Get(string name) => throw new InvalidOperationException("unpinned access");
        public Task<byte[]> Get(string name, string version)
        {
            Asked.Add(name + "@" + version);
            if (Missing.Contains(version)) throw new HttpRequestException("NOT_FOUND");
            return Task.FromResult(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name + "@" + version)));
        }
    }

    static ProdStartupException Prod(Dictionary<string, string> env, VersionedSecrets s)
    {
        var o = new ServerOptions { Env = CrashverseEnv.Prod };
        ServerOptions.ApplyEnvFlags(o, k => env.TryGetValue(k, out var v) ? v : null);
        var ex = Assert.Throws<ProdStartupException>(() => ServerApp.ProdContext(o, new ServerApp.ProdAdapters { Secrets = s }));
        return ex; // other adapters are absent in tests, so prod always refuses; we assert only on the secret lines
    }

    [Fact]
    public void Low3_ProdCheck_RequiresPinnedVersions_AndLoadsThem()
    {
        var unpinned = Prod(new() { ["SECRET_VERSION_PURCHASE_BINDING"] = "latest" }, new VersionedSecrets());
        Assert.Contains("SECRET_VERSION_PURCHASE_BINDING must pin", unpinned.Message);
        Assert.Contains("SECRET_VERSION_DELETION_PEPPER must pin", unpinned.Message);

        var s = new VersionedSecrets();
        var pinned = Prod(new() { ["SECRET_VERSION_PURCHASE_BINDING"] = "3", ["SECRET_VERSION_PURCHASE_BINDING_PREVIOUS"] = "2", ["SECRET_VERSION_DELETION_PEPPER"] = "1" }, s);
        Assert.DoesNotContain("SECRET_VERSION", pinned.Message);
        Assert.DoesNotContain("purchase-binding secret", pinned.Message); Assert.DoesNotContain("previous purchase-binding", pinned.Message);
        Assert.Contains("purchase-binding-pepper@3", s.Asked); Assert.Contains("purchase-binding-pepper@2", s.Asked); Assert.Contains("deletion-pepper@1", s.Asked);
        Assert.DoesNotContain(s.Asked, a => a.EndsWith("@latest"));

        var gone = Prod(new() { ["SECRET_VERSION_PURCHASE_BINDING"] = "3", ["SECRET_VERSION_PURCHASE_BINDING_PREVIOUS"] = "2", ["SECRET_VERSION_DELETION_PEPPER"] = "1" }, new VersionedSecrets { Missing = { "2" } });
        Assert.Contains("previous purchase-binding secret version not loaded", gone.Message);
        var curGone = Prod(new() { ["SECRET_VERSION_PURCHASE_BINDING"] = "3", ["SECRET_VERSION_DELETION_PEPPER"] = "1" }, new VersionedSecrets { Missing = { "3" } });
        Assert.Contains("purchase-binding secret", curGone.Message);
    }

    [Fact]
    public async Task Low3_Rotation_ExistingBindingsStillMatch_NewOnesUseCurrent()
    {
        await using var h = await Harness.Start();
        var auth = new AuthService(h.X); var s = await h.Guest(); var other = await h.Guest();
        var oldId = auth.ObfuscatedId(s.pid);              // binding made before rotation
        h.X.Opt.PreviousObfuscationPepper = h.X.Opt.ObfuscationPepper; h.X.Opt.PreviousObfuscationPepperVersion = h.X.Opt.ObfuscationPepperVersion;
        h.X.Opt.ObfuscationPepper = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32); h.X.Opt.ObfuscationPepperVersion = "v9";
        var newId = auth.ObfuscatedId(s.pid);
        Assert.NotEqual(oldId, newId); Assert.StartsWith("v9_", newId);
        Assert.True(auth.MatchesObfuscatedId(s.pid, oldId)); Assert.True(auth.MatchesObfuscatedId(s.pid, newId));
        Assert.False(auth.MatchesObfuscatedId(other.pid, oldId)); Assert.False(auth.MatchesObfuscatedId(other.pid, newId));
        h.X.Opt.PreviousObfuscationPepper = null; // previous retired: old bindings stop matching
        Assert.False(auth.MatchesObfuscatedId(s.pid, oldId));
    }
}
