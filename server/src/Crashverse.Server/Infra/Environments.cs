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
        Check("replay", x.Replay); Check("captcha", x.Captcha); Check("secrets", x.Secrets);
        if (x.Opt.ObfuscationPepper is not { Length: >= 32 }) bad.Add($"purchase-binding secret ({SecretNames.PurchaseBinding}) not loaded from the secret provider");
        if (x.Opt.DeletionPepper is not { Length: >= 32 }) bad.Add($"deletion pepper ({SecretNames.DeletionPepper}) not loaded from the secret provider"); Check("nonces", x.Nonces); Check("limits", x.Limits);
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

/// <summary>N1: dev wiring must never run in the cloud or on a public interface, whatever CRASHVERSE_ENV says.</summary>
public static class DevGuard
{
    static readonly string[] CloudMarkers = { "K_SERVICE", "K_REVISION", "K_CONFIGURATION", "CLOUD_RUN_JOB", "FUNCTION_TARGET", "FUNCTION_NAME", "GAE_SERVICE", "GAE_APPLICATION" };
    static readonly string[] ProjectMarkers = { "GOOGLE_CLOUD_PROJECT", "GCLOUD_PROJECT", "GCP_PROJECT" };

    public static void CheckNotCloud(Func<string, string> env)
    {
        foreach (var m in CloudMarkers)
            if (!string.IsNullOrEmpty(env(m))) throw new ProdStartupException($"dev wiring refused: {m} is set (cloud runtime detected)");
        bool emulator = IsLoopbackHost(HostOf(env("FIRESTORE_EMULATOR_HOST")));
        foreach (var m in ProjectMarkers)
            if (!string.IsNullOrEmpty(env(m)) && !emulator) throw new ProdStartupException($"dev wiring refused: {m} is set outside the local emulator");
    }

    /// <summary>Every listen URL must be localhost or a loopback address (no 0.0.0.0, *, +, or a LAN/public IP).</summary>
    public static void CheckLoopbackOnly(IEnumerable<string> urls)
    {
        foreach (var u in urls)
        {
            var host = Uri.TryCreate(u.Replace("://*", "://wildcard-star").Replace("://+", "://wildcard-plus"), UriKind.Absolute, out var uri) ? uri.Host : u;
            if (!IsLoopbackHost(host)) throw new ProdStartupException($"dev wiring refused: listening on {u}; bind to localhost/127.0.0.1/[::1] only");
        }
    }

    static string HostOf(string hostPort)
    {
        if (string.IsNullOrEmpty(hostPort)) return null;
        if (hostPort.StartsWith("[")) return hostPort[1..hostPort.IndexOf(']')];
        int i = hostPort.LastIndexOf(':'); return i > 0 ? hostPort[..i] : hostPort;
    }

    public static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrEmpty(host)) return false;
        host = host.Trim('[', ']');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return System.Net.IPAddress.TryParse(host, out var ip) && System.Net.IPAddress.IsLoopback(ip);
    }
}
