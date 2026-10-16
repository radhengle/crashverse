using System.Collections.Concurrent;
using System.Text.Json;
using Google.Cloud.Firestore;

namespace Crashverse.Server.Infra;

/// <summary>
/// Data layer. Everything the server persists goes through this interface (C1: nothing authoritative lives on the device).
/// Append-only collections (ledger, audit) can only be created, never overwritten or deleted.
/// </summary>
public interface IDocStore
{
    Task<T> Get<T>(string collection, string id) where T : class;
    /// <summary>Creates the document only if absent (uniqueness constraint). Returns false if it already exists.</summary>
    Task<bool> TryCreate<T>(string collection, string id, T doc) where T : class;
    Task Put<T>(string collection, string id, T doc) where T : class;
    Task<bool> Delete(string collection, string id);
    Task<List<T>> List<T>(string collection, string idPrefix = "") where T : class;
    /// <summary>Serialises read-modify-write sections on a key across all callers (and, for Firestore, all instances).</summary>
    Task<T> Atomic<T>(string lockKey, Func<Task<T>> body);
}

/// <summary>Tracks locks held by the current async flow so nested Atomic() on the same key does not deadlock.</summary>
static class Reentrancy
{
    static readonly AsyncLocal<System.Collections.Immutable.ImmutableHashSet<string>> Held = new();
    public static bool Holds(string k) => Held.Value?.Contains(k) == true;
    public static IDisposable Enter(string k)
    {
        var prev = Held.Value ?? System.Collections.Immutable.ImmutableHashSet<string>.Empty;
        Held.Value = prev.Add(k);
        return new Pop(prev);
    }
    sealed class Pop : IDisposable { readonly System.Collections.Immutable.ImmutableHashSet<string> _p; public Pop(System.Collections.Immutable.ImmutableHashSet<string> p) { _p = p; } public void Dispose() => Held.Value = _p; }
}

public static class Collections
{
    public static readonly HashSet<string> AppendOnly = new() { "ledger", "audit" };
}

public sealed class AppendOnlyViolation : InvalidOperationException
{
    public AppendOnlyViolation(string c) : base($"collection '{c}' is append-only") { }
}

static class StoreJson
{
    public static readonly JsonSerializerOptions Opts = new() { IncludeFields = true };
    public static string Ser<T>(T v) => JsonSerializer.Serialize(v, Opts);
    public static T De<T>(string s) => JsonSerializer.Deserialize<T>(s, Opts);
}

public sealed class InMemoryDocStore : IDocStore
{
    readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _c = new();
    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    ConcurrentDictionary<string, string> C(string n) => _c.GetOrAdd(n, _ => new());

    public Task<T> Get<T>(string collection, string id) where T : class =>
        Task.FromResult(C(collection).TryGetValue(id, out var s) ? StoreJson.De<T>(s) : null);

    public Task<bool> TryCreate<T>(string collection, string id, T doc) where T : class =>
        Task.FromResult(C(collection).TryAdd(id, StoreJson.Ser(doc)));

    public Task Put<T>(string collection, string id, T doc) where T : class
    {
        if (Collections.AppendOnly.Contains(collection)) throw new AppendOnlyViolation(collection);
        C(collection)[id] = StoreJson.Ser(doc); return Task.CompletedTask;
    }

    public Task<bool> Delete(string collection, string id)
    {
        if (Collections.AppendOnly.Contains(collection)) throw new AppendOnlyViolation(collection);
        return Task.FromResult(C(collection).TryRemove(id, out _));
    }

    public Task<List<T>> List<T>(string collection, string idPrefix = "") where T : class =>
        Task.FromResult(C(collection).Where(kv => kv.Key.StartsWith(idPrefix, StringComparison.Ordinal))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => StoreJson.De<T>(kv.Value)).ToList());

    public async Task<T> Atomic<T>(string lockKey, Func<Task<T>> body)
    {
        if (Reentrancy.Holds(lockKey)) return await body(); // re-entrant within one logical call flow
        var l = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await l.WaitAsync();
        try { using (Reentrancy.Enter(lockKey)) return await body(); } finally { l.Release(); }
    }
}

/// <summary>
/// Firestore implementation. Emulator only in v0: refuses to start unless FIRESTORE_EMULATOR_HOST is set (no cloud project,
/// no credentials, nothing provisioned). Each document stores {id, json}; ids are escaped so '/' is safe.
/// Atomic() uses a lease document created with CreateAsync (fails if it exists), so it serialises across instances.
/// </summary>
public sealed class FirestoreDocStore : IDocStore
{
    readonly FirestoreDb _db;
    public FirestoreDb Db => _db;

    public FirestoreDocStore(string projectId)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")))
            throw new InvalidOperationException("FirestoreDocStore v0 targets the Firestore emulator only: set FIRESTORE_EMULATOR_HOST");
        _db = new FirestoreDbBuilder { ProjectId = projectId, EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly }.Build();
    }

    static string Esc(string id) => Uri.EscapeDataString(id);
    DocumentReference Ref(string c, string id) => _db.Collection(c).Document(Esc(id));

    public async Task<T> Get<T>(string collection, string id) where T : class
    {
        var s = await Ref(collection, id).GetSnapshotAsync();
        return s.Exists ? StoreJson.De<T>(s.GetValue<string>("json")) : null;
    }

    public async Task<bool> TryCreate<T>(string collection, string id, T doc) where T : class
    {
        try { await Ref(collection, id).CreateAsync(new Dictionary<string, object> { ["id"] = id, ["json"] = StoreJson.Ser(doc) }); return true; }
        catch (Grpc.Core.RpcException e) when (e.StatusCode == Grpc.Core.StatusCode.AlreadyExists) { return false; }
    }

    public async Task Put<T>(string collection, string id, T doc) where T : class
    {
        if (Collections.AppendOnly.Contains(collection)) throw new AppendOnlyViolation(collection);
        await Ref(collection, id).SetAsync(new Dictionary<string, object> { ["id"] = id, ["json"] = StoreJson.Ser(doc) });
    }

    public async Task<bool> Delete(string collection, string id)
    {
        if (Collections.AppendOnly.Contains(collection)) throw new AppendOnlyViolation(collection);
        var r = Ref(collection, id); var s = await r.GetSnapshotAsync();
        if (!s.Exists) return false; await r.DeleteAsync(); return true;
    }

    public async Task<List<T>> List<T>(string collection, string idPrefix = "") where T : class
    {
        Query q = _db.Collection(collection).OrderBy("id");
        if (idPrefix.Length > 0) q = q.WhereGreaterThanOrEqualTo("id", idPrefix).WhereLessThan("id", idPrefix + "\uf8ff");
        var snap = await q.GetSnapshotAsync();
        return snap.Documents.Select(d => StoreJson.De<T>(d.GetValue<string>("json"))).ToList();
    }

    public async Task<T> Atomic<T>(string lockKey, Func<Task<T>> body)
    {
        if (Reentrancy.Holds(lockKey)) return await body();
        var lease = _db.Collection("_locks").Document(Esc(lockKey));
        for (int i = 0; ; i++)
        {
            try { await lease.CreateAsync(new Dictionary<string, object> { ["exp"] = DateTime.UtcNow.AddSeconds(10) }); break; }
            catch (Grpc.Core.RpcException e) when (e.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            {
                var s = await lease.GetSnapshotAsync();
                if (s.Exists && s.GetValue<DateTime>("exp") < DateTime.UtcNow) { await lease.DeleteAsync(); continue; } // stale lease
                if (i > 200) throw new TimeoutException("lock " + lockKey);
                await Task.Delay(10 + Random.Shared.Next(20));
            }
        }
        try { using (Reentrancy.Enter(lockKey)) return await body(); } finally { await lease.DeleteAsync(); }
    }
}
