using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>
/// Wallet + append-only ledger. Every balance change writes exactly one ledger entry inside the wallet lock.
/// Gem debt (Security S6, default refund policy): gems may go negative only through refund clawback; while negative,
/// gem spending is locked and any gem income pays the debt first (it simply adds to the negative balance).
/// </summary>
public sealed class WalletService
{
    readonly Ctx _x; public WalletService(Ctx x) { _x = x; }

    public async Task<WalletDoc> Get(string pid) => await _x.Db.Get<WalletDoc>("wallets", pid) ?? new WalletDoc { playerId = pid };

    public static WalletEx View(WalletDoc w) => new()
    {
        coins = w.coins, shards = w.shards, extraCharges = w.extraCharges, gems = w.gems, gemSpendLocked = w.gems < 0,
        cosmetics = new List<string>(w.cosmetics), powerUps = new List<string>(w.powerUps)
    };

    public sealed class Change
    {
        public Dictionary<string, int> delta = new(); public List<string> add = new(); public string addSource; public List<string> remove = new();
        public bool allowNegativeGems; public bool floorAtZero; public string note;
    }

    /// <summary>Applies a change and appends the ledger entry. Must be called inside Locked().</summary>
    public async Task<(WalletDoc w, LedgerEntry e)> ApplyUnlocked(string pid, Change c, string reason, string refId)
    {
        var w = await Get(pid);
        var applied = new Dictionary<string, int>();
        foreach (var (cur, d0) in c.delta)
        {
            int bal = Bal(w, cur), d = d0;
            if (d < 0 && c.floorAtZero) d = -Math.Min(-d, Math.Max(0, bal)); // stop at zero
            if (d < 0 && !c.floorAtZero && bal + d < 0 && !(cur == "gems" && c.allowNegativeGems))
                throw new ApiError(409, "INSUFFICIENT_FUNDS", $"not enough {cur}");
            Set(w, cur, bal + d); applied[cur] = d;
        }
        var added = new List<string>(); var removed = new List<string>();
        foreach (var i in c.add) { var list = i.StartsWith("powerup:") ? w.powerUps : w.cosmetics; var id = i.StartsWith("powerup:") ? i[8..] : i;
            if (!list.Contains(id)) { list.Add(id); added.Add(i); if (list == w.cosmetics && c.addSource != null) w.cosmeticSource[id] = c.addSource; } }
        foreach (var i in c.remove) if (w.cosmetics.Remove(i)) { w.cosmeticSource.Remove(i); removed.Add(i); }
        await _x.Db.Put("wallets", pid, w);
        var e = new LedgerEntry
        {
            id = $"{pid}:{_x.NowMs:D15}:{Hash.NewId()}", playerId = pid, ts = _x.NowMs, reason = reason, refId = refId, note = c.note, delta = applied,
            balanceAfter = new() { ["coins"] = w.coins, ["shards"] = w.shards, ["gems"] = w.gems, ["extraCharges"] = w.extraCharges }, itemsAdded = added, itemsRemoved = removed
        };
        if (!await _x.Db.TryCreate("ledger", e.id, e)) throw new ApiError(500, "INTERNAL", "ledger collision", true);
        return (w, e);
    }

    public Task<T> Locked<T>(string pid, Func<Task<T>> f) => _x.Db.Atomic("wallet:" + pid, f);

    public Task<(WalletDoc w, LedgerEntry e)> Apply(string pid, Change c, string reason, string refId) => Locked(pid, () => ApplyUnlocked(pid, c, reason, refId));

    static int Bal(WalletDoc w, string c) => c switch { "coins" => w.coins, "shards" => w.shards, "gems" => w.gems, "extraCharges" => w.extraCharges, _ => throw new ArgumentException(c) };
    static void Set(WalletDoc w, string c, int v) { switch (c) { case "coins": w.coins = v; break; case "shards": w.shards = v; break; case "gems": w.gems = v; break; case "extraCharges": w.extraCharges = v; break; } }

    public static Change FromReward(Reward r, string source = "reward")
    {
        var c = new Change { addSource = source };
        if (r == null) return c;
        switch (r.type)
        {
            case "coins": c.delta["coins"] = r.amount; break;
            case "shard": c.delta["shards"] = r.amount; break;
            case "charge": c.delta["extraCharges"] = r.amount; break;
            case "gems": c.delta["gems"] = r.amount; break;
            case "cosmetic": if (!string.IsNullOrEmpty(r.id)) c.add.Add(r.id); break;
            case "powerup": if (!string.IsNullOrEmpty(r.id)) c.add.Add("powerup:" + r.id); break;
            case "chest":
                foreach (var part in (r.id ?? "").Split(';'))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && int.TryParse(kv[1], out var n))
                    {
                        var inner = FromReward(new Reward { type = kv[0], amount = n });
                        foreach (var (k, v) in inner.delta) c.delta[k] = c.delta.GetValueOrDefault(k) + v;
                    }
                }
                break;
        }
        return c;
    }

    public Task<(WalletDoc w, LedgerEntry e)> Grant(string pid, Reward r, string reason, string refId, string source = "reward") =>
        Apply(pid, FromReward(r, source), reason, refId);

    /// <summary>Idempotent per Idempotency-Key: a retried consume never double-spends.</summary>
    public Task<(bool consumed, WalletDoc w)> ConsumeCharge(string pid, string idemKey) => Locked(pid, async () =>
    {
        if (string.IsNullOrEmpty(idemKey)) throw new ApiError(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency-Key header required");
        var prior = await _x.Db.Get<LedgerEntry>("idem", pid + ":" + idemKey);
        if (prior != null) return (prior.delta.Count > 0, await Get(pid));
        var w = await Get(pid);
        if (w.extraCharges <= 0) { await _x.Db.Put("idem", pid + ":" + idemKey, new LedgerEntry()); return (false, w); }
        var (w2, e) = await ApplyUnlocked(pid, new Change { delta = { ["extraCharges"] = -1 } }, "consume_charge", idemKey);
        await _x.Db.Put("idem", pid + ":" + idemKey, e);
        return (true, w2);
    });

    /// <summary>Coin/gem store purchase of a cosmetic (SG-33). Gem spending is locked while in gem debt.</summary>
    public Task<WalletDoc> Buy(string pid, string sku) => Locked(pid, async () =>
    {
        var item = _x.Sku(sku) ?? throw new ApiError(404, "UNKNOWN_SKU", "unknown sku");
        if (item.type == "season_pass") throw new ApiError(400, "USE_SEASON_UNLOCK", "the season pass is unlocked only through /v1/season/premium/unlock");
        if (item.priceType != "gems" && item.priceType != "coins") throw new ApiError(400, "REAL_MONEY_SKU", "buy via Google Play");
        var w = await Get(pid);
        if (w.cosmetics.Contains(sku)) return w;
        if (item.priceType == "gems" && w.gems < 0) throw new ApiError(409, "GEM_DEBT_LOCKED", "Gem spending is locked until your refunded gem balance is repaid");
        var c = new Change { delta = { [item.priceType] = -item.price }, add = { sku }, addSource = item.priceType };
        return (await ApplyUnlocked(pid, c, "store_buy", sku)).w;
    });

    public async Task<List<LedgerEntry>> Ledger(string pid) => await _x.Db.List<LedgerEntry>("ledger", pid + ":");
}
