using System.Collections.Generic;

namespace Crashverse.Meta
{
    /// <summary>
    /// The pure rules for the hidden core (design/Season1_KingStory_v1.md §3). They are deterministic and Unity-free, so the logic
    /// tests can cover them. Visuals.HiddenCore applies them in a run.
    ///  - Host: among the eligible pieces (destructible, objective, not roof/explosive/island, min side ≥ 0.35 m, with at least one
    ///    support or dependent), take the 60 % closest to the centroid (ties by id), then pick one with System.Random(seed ^ FNV(levelId)).
    ///  - Exposed: the host is at ≤ 66 % HP, or any touching piece (support/dependent) broke.
    ///  - Detonates on a direct hit while exposed, or when the host breaks while exposed. Breaking before exposure is a dud.
    ///  - Blast: 400 damage at the centre, linear falloff to 40 % at the 6 m edge, nothing beyond.
    /// </summary>
    public static class HiddenCoreRules
    {
        public const float Radius = 6f, Damage = 400f, EdgeFactor = 0.4f, ExposeHpFrac = 0.66f, MinSide = 0.35f, PoolFrac = 0.6f;

        public struct Candidate
        {
            public string id; public float x, y, z, minSide; public bool eligible; public int links;
        }

        public static uint Fnv(string s) { uint h = 2166136261u; if (s != null) foreach (char c in s) { h ^= c; h *= 16777619u; } return h; }

        public static bool Eligible(Candidate c) => c.eligible && c.minSide >= MinSide && c.links > 0;

        /// <summary>Index into <paramref name="all"/> of the host, or -1 when no piece qualifies.</summary>
        public static int PickHost(IList<Candidate> all, long runSeed, string levelId)
        {
            var idx = new List<int>(); double cx = 0, cy = 0, cz = 0;
            for (int i = 0; i < all.Count; i++) if (Eligible(all[i])) { idx.Add(i); cx += all[i].x; cy += all[i].y; cz += all[i].z; }
            if (idx.Count == 0) return -1;
            cx /= idx.Count; cy /= idx.Count; cz /= idx.Count;
            double D(int i) { var c = all[i]; return (c.x - cx) * (c.x - cx) + (c.y - cy) * (c.y - cy) + (c.z - cz) * (c.z - cz); }
            idx.Sort((a, b) => { int k = D(a).CompareTo(D(b)); return k != 0 ? k : string.CompareOrdinal(all[a].id, all[b].id); });
            int pool = System.Math.Max(1, (int)System.Math.Ceiling(idx.Count * PoolFrac));
            int seed = (int)((uint)(runSeed ^ (runSeed >> 32)) ^ Fnv(levelId));
            return idx[new System.Random(seed).Next(pool)];
        }

        public static bool ShouldExpose(float hp, float maxHp, bool neighbourBroken) => neighbourBroken || (maxHp > 0f && hp > 0f && hp <= maxHp * ExposeHpFrac);

        public enum Outcome { None, Detonate, Dud }
        public static Outcome OnHostEvent(bool exposed, bool directHit, bool hostBroke)
        {
            if (exposed && (directHit || hostBroke)) return Outcome.Detonate;
            if (!exposed && hostBroke) return Outcome.Dud;
            return Outcome.None;
        }

        /// <summary>Damage the blast deals to a piece whose centre is <paramref name="distance"/> m from the core.</summary>
        public static float BlastDamage(float distance)
        {
            if (distance < 0f) distance = 0f;
            if (distance > Radius + 1e-3f) return 0f;
            float t = System.Math.Min(1f, distance / Radius);
            return Damage * (1f - (1f - EdgeFactor) * t);
        }
    }
}
