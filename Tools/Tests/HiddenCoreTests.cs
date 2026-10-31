using System.Collections.Generic;
using System.Linq;
using Crashverse.Data;
using Crashverse.Meta;

/// <summary>Season 1 hidden core (HiddenCoreRules): a seeded host on every campaign level, exposure, detonation and the radial blast.</summary>
static class HiddenCoreTests
{
    static ContentDatabase _db;
    static ContentDatabase Db => _db ??= ContentDatabase.LoadBundled();

    static List<HiddenCoreRules.Candidate> Cands(LayoutData lay)
    {
        var links = new Dictionary<string, int>();
        foreach (var b in lay.blocks) { links[b.id] = links.TryGetValue(b.id, out var n) ? n : 0; if (b.sup != null) foreach (var s in b.sup) { links[b.id]++; links[s] = (links.TryGetValue(s, out var m) ? m : 0) + 1; } }
        return lay.blocks.Select(b => new HiddenCoreRules.Candidate
        {
            id = b.id, x = b.p[0], y = b.p[1], z = b.p[2], minSide = System.Math.Min(b.s[0], System.Math.Min(b.s[1], b.s[2])), links = links[b.id],
            eligible = !b.indestructible && !b.explosive && !b.nonObj && b.role != "island" && b.role != "roof",
        }).ToList();
    }

    public static void Register()
    {
        T.Add("hidden_core_host_on_every_campaign_level", () =>
        {
            foreach (var l in Db.Chapter1.levels)
            {
                var lay = Db.GetLayout(l.layout); var c = Cands(lay);
                int i = HiddenCoreRules.PickHost(c, 12345L, l.id);
                T.True(i >= 0, "a host exists on " + l.id);
                if (i < 0) continue;
                var b = lay.blocks[i];
                T.True(!b.indestructible && !b.explosive && !b.nonObj && b.role != "island" && b.role != "roof", l.id + " host is a destructible structural piece: " + b.id);
                T.Eq(i, HiddenCoreRules.PickHost(c, 12345L, l.id), l.id + " same seed -> same host");
            }
        });
        T.Add("hidden_core_seed_varies_host", () =>
        {
            var l = Db.Chapter1.levels.First(x => x.number == 10); var c = Cands(Db.GetLayout(l.layout));
            var hosts = new HashSet<int>(); for (long s = 1; s <= 40; s++) hosts.Add(HiddenCoreRules.PickHost(c, s * 7919L, l.id));
            T.True(hosts.Count >= 2, "different run seeds pick different hosts on the King's Palace (" + hosts.Count + ")");
            T.Eq(-1, HiddenCoreRules.PickHost(new List<HiddenCoreRules.Candidate>(), 1, "x"), "no candidates -> no core");
        });
        T.Add("hidden_core_uses_full_64_bit_seed_and_is_order_independent", () =>
        {
            var l = Db.Chapter1.levels.First(x => x.number == 10); var c = Cands(Db.GetLayout(l.layout));
            int original = HiddenCoreRules.PickHost(c, 12345L, l.id);
            var reversed = c.AsEnumerable().Reverse().ToList();
            int reverseIndex = HiddenCoreRules.PickHost(reversed, 12345L, l.id);
            T.True(original >= 0 && reverseIndex >= 0, "both candidate orders select a host");
            if (original >= 0 && reverseIndex >= 0)
                T.Eq(c[original].id, reversed[reverseIndex].id, "candidate ordering does not change the selected host");
            var highBitHosts = new HashSet<int>();
            for (long i = 0; i < 40; i++) highBitHosts.Add(HiddenCoreRules.PickHost(c, (i << 32) | 12345L, l.id));
            T.True(highBitHosts.Count >= 2, "upper 32 seed bits affect host selection (" + highBitHosts.Count + " hosts)");
        });
        T.Add("hidden_core_expose_and_detonate_rules", () =>
        {
            T.True(!HiddenCoreRules.ShouldExpose(100, 100, false), "full HP hidden");
            T.True(HiddenCoreRules.ShouldExpose(66, 100, false), "66% HP exposed");
            T.True(HiddenCoreRules.ShouldExpose(100, 100, true), "a neighbour broke -> exposed");
            T.Eq(HiddenCoreRules.Outcome.Detonate, HiddenCoreRules.OnHostEvent(true, true, false), "direct hit while exposed");
            T.Eq(HiddenCoreRules.Outcome.Detonate, HiddenCoreRules.OnHostEvent(true, false, true), "host breaks while exposed");
            T.Eq(HiddenCoreRules.Outcome.Dud, HiddenCoreRules.OnHostEvent(false, false, true), "breaks before exposure = dud");
            T.Eq(HiddenCoreRules.Outcome.None, HiddenCoreRules.OnHostEvent(false, true, false), "hidden hit does nothing");
        });
        T.Add("hidden_core_huge_radial_blast", () =>
        {
            T.Eq(400f, HiddenCoreRules.BlastDamage(0f), "400 at the centre");
            T.True(System.Math.Abs(HiddenCoreRules.BlastDamage(6f) - 200f) < 0.01f, "50% at the 6 m edge");
            T.Eq(0f, HiddenCoreRules.BlastDamage(6.1f), "nothing beyond 6 m");
            T.True(HiddenCoreRules.BlastDamage(3f) > HiddenCoreRules.BlastDamage(4f), "falls off with distance");
            T.True(HiddenCoreRules.Radius > 3.5f * 1.5f, "bigger than the Mega Bomb");
        });
    }
}
