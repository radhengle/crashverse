using System.Net.Http.Headers;
using System.Text;

namespace Crashverse.Server.Tests;

/// <summary>Runs only under the Firestore emulator (see README: firebase emulators:exec). Skipped otherwise.</summary>
public sealed class EmulatorFactAttribute : FactAttribute
{
    public EmulatorFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"))) Skip = "FIRESTORE_EMULATOR_HOST not set (run via firebase emulators:exec)"; }
}

[Trait("Category", "Emulator")]
public class FirestoreEmulatorTests
{
    static string Host => Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
    const string Project = "demo-crashverse";

    [EmulatorFact]
    public async Task FirestoreStore_CrudUniquenessAppendOnlyAndLocks()
    {
        var db = new FirestoreDocStore(Project);
        var id = "t/" + Guid.NewGuid().ToString("N");
        Assert.True(await db.TryCreate("purchases", id, new PurchaseDoc { tokenHash = id, playerId = "p" }));
        Assert.False(await db.TryCreate("purchases", id, new PurchaseDoc { tokenHash = id, playerId = "q" })); // unique token
        Assert.Equal("p", (await db.Get<PurchaseDoc>("purchases", id)).playerId);
        Assert.True(await db.TryCreate("ledger", id, new LedgerEntry { id = id }));
        await Assert.ThrowsAsync<AppendOnlyViolation>(() => db.Put("ledger", id, new LedgerEntry()));
        Assert.Contains(await db.List<PurchaseDoc>("purchases", "t/"), p => p.tokenHash == id);
        int counter = 0;
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => db.Atomic("lock-" + id, async () => { var c = counter; await Task.Delay(5); counter = c + 1; return 0; })));
        Assert.Equal(5, counter);
    }

    [EmulatorFact]
    public async Task ServerRunsEndToEnd_OnFirestore()
    {
        // pass the Firestore store explicitly (no process-wide env var: other test classes run in parallel)
        await using var h = await Harness.Start(null, new FirestoreDocStore(Project));
        Assert.IsType<FirestoreDocStore>(h.X.Db);
        var s = await h.Guest();
        Assert.Equal(100, (await h.Post("/v1/daily-reward/claim", null, s))["coins"].GetInt32());
        var tok = h.PlayPurchase(s, "gems_small");
        Assert.Equal("verified", (await h.Verify(s, "gems_small", tok))["status"].GetString());
        Assert.Equal("verified", (await h.Verify(s, "gems_small", tok))["status"].GetString());
        Assert.Equal(100, (await h.Wallet(s)).gems);
    }

    /// <summary>B2: firestore.rules deny every client read/write, signed in or not. Only the server (Admin) touches data.</summary>
    [EmulatorFact]
    public async Task ClientSdkAccess_IsDenied_ByRules()
    {
        var db = new FirestoreDocStore(Project);
        await db.Put("wallets", "victim", new WalletDoc { playerId = "victim", gems = 5 });
        using var http = new HttpClient { BaseAddress = new Uri($"http://{Host}/v1/projects/{Project}/databases/(default)/documents/") };
        // the emulator accepts unsigned JWTs as a signed-in Firebase user
        string Seg(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var userJwt = Seg("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." + Seg("{\"sub\":\"victim\",\"user_id\":\"victim\",\"aud\":\"" + Project + "\",\"iss\":\"https://securetoken.google.com/" + Project + "\",\"iat\":1700000000,\"exp\":4100000000,\"auth_time\":1700000000,\"firebase\":{\"sign_in_provider\":\"anonymous\"}}") + ".";
        foreach (var auth in new[] { null, userJwt })
        {
            async Task<HttpStatusCode> Do(HttpMethod m, string path, string body = null)
            {
                var req = new HttpRequestMessage(m, path);
                if (auth != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
                if (body != null) req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return (await http.SendAsync(req)).StatusCode;
            }
            Assert.Equal(HttpStatusCode.Forbidden, await Do(HttpMethod.Get, "wallets/victim"));
            Assert.Equal(HttpStatusCode.Forbidden, await Do(HttpMethod.Get, "wallets"));
            Assert.Equal(HttpStatusCode.Forbidden, await Do(HttpMethod.Patch, "wallets/victim", "{\"fields\":{\"json\":{\"stringValue\":\"{\\\"gems\\\":999999}\"}}}"));
            Assert.Equal(HttpStatusCode.Forbidden, await Do(HttpMethod.Delete, "wallets/victim"));
            Assert.Equal(HttpStatusCode.Forbidden, await Do(HttpMethod.Post, "boards?documentId=x", "{\"fields\":{}}"));
        }
        Assert.Equal(5, (await db.Get<WalletDoc>("wallets", "victim")).gems);
    }

    /// <summary>Agreed-interface account/privacy flow on the Firestore emulator: single-use reauth, delete, cancel, consent, hide name.</summary>
    [EmulatorFact]
    public async Task AccountPrivacyFlow_RunsOnFirestore()
    {
        await using var h = await Harness.Start(null, new FirestoreDocStore(Project));
        var s = await h.Linked("emu-" + Guid.NewGuid().ToString("N"));
        var sub = h.Facebook.Tokens.Keys.Last();
        var re = await h.Post("/v1/auth/reauth", new { credential = new { provider = "Facebook", token = sub } }, s);
        var tok = re["reauthToken"].GetString();
        Assert.Equal("scheduled", (await h.Post("/v1/account/delete", new { reauthToken = tok }, s))["status"].GetString());
        Assert.Equal("active", (await h.Post("/v1/account/delete/cancel", null, s))["status"].GetString());
        Assert.Equal("REAUTH_REQUIRED", (await h.Post("/v1/account/delete", new { reauthToken = tok }, s)).Code); // single use across the shared store
        Assert.Equal("pending", (await h.Post("/v1/consent/parental/request", new { parentEmail = "p@example.com" }, s))["status"].GetString());
        Assert.Equal("pending", (await h.Get("/v1/consent/parental/status", s))["status"].GetString());
        Assert.True((await h.Post("/v1/profile/visibility", new { hideName = true }, s))["hideName"].GetBoolean());
    }
}
