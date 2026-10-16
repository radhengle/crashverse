using System.Reflection;
using Crashverse.Server.Domain;

namespace Crashverse.Server.Infra;

/// <summary>Marks a dev/test-only implementation (fakes, stubs, in-process keys, in-memory or emulator stores).
/// The prod guard refuses to start if any component carries it.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DevOnlyAttribute : Attribute { }

public enum CrashverseEnv { Dev, Test, Prod }

public static class Envs
{
    /// <summary>CRASHVERSE_ENV = dev | test | prod. Missing, empty or unknown means prod (fail closed).</summary>
    public static CrashverseEnv Parse(string v) => (v ?? "").Trim().ToLowerInvariant() switch
    {
        "dev" => CrashverseEnv.Dev, "test" => CrashverseEnv.Test, _ => CrashverseEnv.Prod,
    };
    /// <summary>Server-issued token flavor: "prod" only in prod, "test" otherwise. Never taken from a request.</summary>
    public static string Flavor(CrashverseEnv e) => e == CrashverseEnv.Prod ? "prod" : "test";
}

public sealed class ProdStartupException : Exception { public ProdStartupException(string m) : base(m) { } }

/// <summary>C1 (PR #1 security review): prod refuses dev wiring.</summary>
public static class ProdGuard
{
    public static void Validate(Ctx x)
    {
        var bad = new List<string>();
        void Check(string name, object o)
        {
            if (o == null) { bad.Add($"{name}: not configured"); return; }
            var t = o.GetType();
            if (t.GetCustomAttribute<DevOnlyAttribute>() != null || t.Name.StartsWith("Fake") || t.Name.StartsWith("Stub") || t.Name.StartsWith("InMemory"))
                bad.Add($"{name}: dev-only implementation {t.Name}");
        }
        Check("store", x.Db); Check("integrity", x.Integrity); Check("play", x.Play); Check("pushAuth", x.PushAuth);
        Check("google", x.Google); Check("facebook", x.Facebook); Check("email", x.Email); Check("consentMailer", x.ConsentMailer);
        Check("replay", x.Replay); Check("captcha", x.Captcha); Check("nonces", x.Nonces); Check("limits", x.Limits);
        if (x.Opt.XEnabled) Check("x", x.X);
        if (x.Tokens == null) bad.Add("tokens: not configured");
        else foreach (var r in x.Tokens.Rings) Check($"signer[{r.Purpose}]", r.Active);
        if (x.Receipts == null || x.Receipts.Ephemeral) bad.Add("receipts: master must come from the secret store, not an in-process key");
        if (x.Opt.IntegrityMode != "enforce") bad.Add($"INTEGRITY_MODE={x.Opt.IntegrityMode} is not allowed in prod");
        if (x.Opt.RefundPolicy != RefundPolicies.GemDebt && x.Opt.RefundPolicy != RefundPolicies.FloorAtZero) bad.Add($"REFUND_POLICY={x.Opt.RefundPolicy} is unknown");
        if (x.Opt.AllowClientRunSummary) bad.Add("ALLOW_CLIENT_RUN_SUMMARY is dev-only");
        if (x.Opt.AdminKey != null) bad.Add("admin key must not be set in prod (use IAM + OIDC)");
        if (x.Opt.Env != CrashverseEnv.Prod) bad.Add("options are not marked prod");
        if (bad.Count > 0) throw new ProdStartupException("Refusing to start in prod:\n - " + string.Join("\n - ", bad));
    }
}
