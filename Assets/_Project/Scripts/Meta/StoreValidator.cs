using System.Collections.Generic;
using Crashverse.Data;

namespace Crashverse.Meta
{
    /// <summary>
    /// Addendum §4 (VM-13): store sells cosmetics and currency packs only. Every SKU must be cosmetic=true,
    /// of an allowed type, and must not grant or contain anything gameplay-affecting. Run at load; offending items are dropped.
    /// </summary>
    public static class StoreValidator
    {
        static readonly string[] BannedWords = { "powerup", "power_up", "charge", "shot", "continue", "skip", "unlock", "star", "titan", "attempt", "upgrade", "level", "progress" };
        static readonly string[] AllowedGrants = { "gems" };

        public static List<string> Validate(StoreCatalog c, out List<StoreItem> accepted)
        {
            var errors = new List<string>(); accepted = new List<StoreItem>();
            if (c?.items == null) { errors.Add("catalog missing"); return errors; }
            var bySku = new Dictionary<string, StoreItem>();
            foreach (var i in c.items) if (i?.sku != null) bySku[i.sku] = i;
            foreach (var i in c.items)
            {
                var e = Check(i, c, bySku, 0);
                if (e == null) accepted.Add(i); else errors.Add($"{i?.sku}: {e}");
            }
            return errors;
        }

        static string Check(StoreItem i, StoreCatalog c, Dictionary<string, StoreItem> bySku, int depth)
        {
            if (i == null) return "null item";
            if (!i.cosmetic) return "not flagged cosmetic=true";
            if (c.allowedTypes == null || System.Array.IndexOf(c.allowedTypes, i.type) < 0) return $"type '{i.type}' not allowed";
            string t = (i.type ?? "").ToLowerInvariant();
            foreach (var w in BannedWords) if (t.Contains(w)) return $"gameplay type '{i.type}'";
            if (!string.IsNullOrEmpty(i.grants))
                foreach (var part in i.grants.Split(';'))
                {
                    var key = part.Split(':')[0].Trim().ToLowerInvariant();
                    if (System.Array.IndexOf(AllowedGrants, key) < 0) return $"grants gameplay '{key}'";
                }
            if (i.type == "currency_pack" && string.IsNullOrEmpty(i.grants)) return "currency pack grants nothing";
            if (i.contents != null)
            {
                if (depth > 2) return "bundle nesting too deep";
                foreach (var sku in i.contents)
                {
                    if (!bySku.TryGetValue(sku, out var inner)) return $"bundle references unknown '{sku}'";
                    var e = Check(inner, c, bySku, depth + 1);
                    if (e != null) return $"bundle item {sku}: {e}";
                }
            }
            return null;
        }
    }
}
