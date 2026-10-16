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
    /// <summary>Raw (id, json) listing, used only by the data-lifecycle jobs.</summary>
    Task<List<(string id, string json)>> ListRaw(string collection);
    /// <summary>Deletes a document even in an append-only collection. Only the deletion/retention jobs call this, and every call is audited.</summary>
    Task<bool> LifecyclePurge(string collection, string id);
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
    /// <summary>Every collection that can hold player-keyed data (the deletion job scans all of them).</summary>
    public static readonly string[] PlayerData = { "accounts", "attempts", "boards", "campaignBest", "campaignProgress", "consents", "dailyReward", "emailCreds",
        "entitlements", "friendCodes", "friends", "grantKeys", "idem", "identities", "inbox", "installAge", "ledger", "linkNonces", "purchases", "rankedStatus",
        "replayQueue", "runs", "seasonProgress", "sessions", "shadowBoards", "wallets", "webDeletion", "audit" };
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

[DevOnly] public sealed class InMemoryDocStore : IDocStore
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

    public Task<List<(string id, string json)>> ListRaw(string collection) =>
        Task.FromResult(C(collection).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, kv.Value)).ToList());
    public Task<bool> LifecyclePurge(string collection, string id) => Task.FromResult(C(collection).TryRemove(id, out _));

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
[DevOnly] public sealed class FirestoreDocStore : IDocStore
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

    /// <summary>N3 fencing: leases held by this async flow. Every write made while holding one runs in a transaction that re-reads the
    /// lease and commits only if we still own it and it has not expired, so work that outlives LeaseTtl cannot commit after a takeover.</summary>
    static readonly AsyncLocal<System.Collections.Immutable.ImmutableList<(DocumentReference lease, string owner)>> HeldLeases = new();
    public sealed class LeaseLostException : InvalidOperationException { public LeaseLostException(string k) : base("lock lease lost before commit: " + k) { } }

    async Task Fenced(Action<Transaction> write, Func<Transaction, Task> read = null)
    {
        var held = HeldLeases.Value;
        if (held == null || held.IsEmpty) { await _db.RunTransactionAsync(async tx => { if (read != null) await read(tx); write(tx); return 0; }); return; }
        await _db.RunTransactionAsync(async tx =>
        {
            foreach (var (lease, owner) in held)
            {
                var s = await tx.GetSnapshotAsync(lease);
                if (!s.Exists || s.GetValue<string>("owner") != owner || s.GetValue<DateTime>("expireAt") <= DateTime.UtcNow) throw new LeaseLostException(lease.Id);
            }
            if (read != null) await read(tx);
            write(tx); return 0;
        });
    }
    DocumentReference Ref(string c, string id) => _db.Collection(c).Document(Esc(id));

    public async Task<T> Get<T>(string collection, string id) where T : class
    {
        var s = await Ref(collection, id).GetSnapshotAsync();
        return s.Exists ? StoreJson.De<T>(s.GetValue<string>("json")) : null;
    }

    public async Task<bool> TryCreate<T>(string collection, string id, T doc) where T : class
    {
        var r = Ref(collection, id); bool exists = false;
        await Fenced(tx => { if (!exists) tx.Create(r, new Dictionary<string, object> { ["id"] = id, ["json"] = StoreJson.Ser(doc) }); },
            async tx => exists = (await tx.GetSnapshotAsync(r)).Exists);
        return !exists;
    }

    public async Task Put<T>(string collection, string id, T doc) where T : class
    {
        if (Collections.AppendOnly.Contains(collection)) throw new AppendOnlyViolation(collection);
        var r = Ref(collection, id);
        await Fenced(tx => tx.Set(r, new Dictionary<string, object> { ["id"] = id, ["json"] = StoreJson.Ser(doc) }));
    }

    public async Task<bool> Delete(string collection, string id)
    {
        if (Collections.AppendOnly.Contains(collection)) throw new AppendOnlyViolation(collection);
        var r = Ref(collection, id); bool exists = false;
        await Fenced(tx => { if (exists) tx.Delete(r); }, async tx => exists = (await tx.GetSnapshotAsync(r)).Exists);
        return exists;
    }

    public async Task<List<T>> List<T>(string collection, string idPrefix = "") where T : class
    {
        Query q = _db.Collection(collection).OrderBy("id");
        if (idPrefix.Length > 0) q = q.WhereGreaterThanOrEqualTo("id", idPrefix).WhereLessThan("id", idPrefix + "\uf8ff");
        var snap = await q.GetSnapshotAsync();
        return snap.Documents.Select(d => StoreJson.De<T>(d.GetValue<string>("json"))).ToList();
    }

    public async Task<List<(string id, string json)>> ListRaw(string collection)
    {
        var snap = await _db.Collection(collection).OrderBy("id").GetSnapshotAsync();
        return snap.Documents.Select(d => (d.GetValue<string>("id"), d.GetValue<string>("json"))).ToList();
    }
    public async Task<bool> LifecyclePurge(string collection, string id)
    {
        var r = Ref(collection, id); bool exists = false;
        await Fenced(tx => { if (exists) tx.Delete(r); }, async tx => exists = (await tx.GetSnapshotAsync(r)).Exists);
        return exists;
    }

    /// <summary>Lease lifetime. Bodies are short (one player's read-modify-write); a crashed holder's lease expires and is taken over.</summary>
    public static TimeSpan LeaseTtl = TimeSpan.FromSeconds(15);

    /// <summary>M1: lease lock with expiry. Acquire = transaction that creates the lease or replaces an expired one; the lease stores an
    /// owner token and `expireAt` (a Firestore TTL policy on _locks.expireAt garbage-collects leftovers). Release deletes only our own lease.</summary>
    public async Task<T> Atomic<T>(string lockKey, Func<Task<T>> body)
    {
        if (Reentrancy.Holds(lockKey)) return await body();
        var lease = _db.Collection("_locks").Document(Esc(lockKey));
        var owner = Hash.NewId();
        for (int i = 0; ; i++)
        {
            bool got = await _db.RunTransactionAsync(async tx =>
            {
                var s = await tx.GetSnapshotAsync(lease);
                if (s.Exists && s.GetValue<DateTime>("expireAt") > DateTime.UtcNow) return false;
                tx.Set(lease, new Dictionary<string, object> { ["owner"] = owner, ["expireAt"] = DateTime.UtcNow.Add(LeaseTtl) });
                return true;
            });
            if (got) break;
            if (i > 400) throw new TimeoutException("lock " + lockKey);
            await Task.Delay(10 + Random.Shared.Next(20));
        }
        var prevLeases = HeldLeases.Value ?? System.Collections.Immutable.ImmutableList<(DocumentReference, string)>.Empty;
        try { using (Reentrancy.Enter(lockKey)) { HeldLeases.Value = prevLeases.Add((lease, owner)); try { return await body(); } finally { HeldLeases.Value = prevLeases; } } }
        finally
        {
            await _db.RunTransactionAsync(async tx =>
            {
                var s = await tx.GetSnapshotAsync(lease);
                if (s.Exists && s.GetValue<string>("owner") == owner) tx.Delete(lease);
                return 0;
            });
        }
    }
}
