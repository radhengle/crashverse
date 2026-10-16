using System;
using System.Collections.Generic;

namespace Crashverse.Sim
{
    /// <summary>
    /// Debris damage constants (Level Rulings v1 §7, ruling #7a). Integer units: speed in mm/s, mass in grams, damage in
    /// milli-HP, multipliers in permille. Starting values for Level Designer to verify on L7.
    /// </summary>
    [Serializable]
    public struct DebrisDamageConfig
    {
        /// <summary>Damage only when relative impact speed is strictly above this (2 m/s); speed counts from here.</summary>
        public int minSpeedMmPerS;
        /// <summary>Milli-HP per (kg · m/s) of excess speed: 0.5 HP = 500.</summary>
        public int milliHpPerKgMetrePerS;
        /// <summary>Cap per collision (60 HP).</summary>
        public int capMilliHp;

        public static readonly DebrisDamageConfig Default = new DebrisDamageConfig
        {
            minSpeedMmPerS = 2000,
            milliHpPerKgMetrePerS = 500,
            capMilliHp = 60000,
        };
    }

    /// <summary>Per-material multipliers (permille) on damage TAKEN by the struck piece. Data lives on MaterialDefinition.</summary>
    public static class DebrisMaterialMult
    {
        public const int Wood = 1000, Brick = 1100, Steel = 800, Neutral = 1000;
        /// <summary>Starting tunables (Game designer to confirm): glass brittle ×1.5, stone tough ×0.9.</summary>
        public const int Glass = 1500, Stone = 900;
    }

    /// <summary>Pure, engine-free debris damage maths. Deterministic for identical integer inputs.</summary>
    public static class DebrisDamage
    {
        /// <summary>Metres/second and kilograms (from PhysX) to the integer units used by the rule. Rounds half away from zero.</summary>
        public static int ToMmPerS(float metresPerSecond) => (int)Math.Round(metresPerSecond * 1000.0, MidpointRounding.AwayFromZero);
        public static int ToGrams(float kilograms) => (int)Math.Round(kilograms * 1000.0, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Ruling #7a: damage = min(cap, 0.5 × mass_kg × (relSpeed − 2 m/s) × materialMult), only above 2 m/s; resting = 0.
        /// massGrams is the striking body's mass; targetMultPermille is the struck piece's material multiplier. Floors to milli-HP.
        /// </summary>
        public static int ComputeMilliHp(int relativeSpeedMmPerS, int massGrams, int targetMultPermille, bool restingContact, DebrisDamageConfig cfg)
        {
            if (restingContact || massGrams <= 0 || targetMultPermille <= 0 || relativeSpeedMmPerS <= cfg.minSpeedMmPerS) return 0;
            if (cfg.milliHpPerKgMetrePerS <= 0 || cfg.capMilliHp <= 0) return 0;
            long excess = (long)relativeSpeedMmPerS - cfg.minSpeedMmPerS;           // mm/s
            long momentum = (long)massGrams * excess;                                // g·mm/s = 1e-6 kg·m/s; < 2^63
            long scale = (long)cfg.milliHpPerKgMetrePerS * targetMultPermille;       // milli-HP·permille per kg·m/s
            if (momentum >= long.MaxValue / scale) return cfg.capMilliHp;             // overflow guard
            long dmg = momentum * scale / 1000000000L;                                // 1e6 (units) × 1e3 (permille)
            return (int)Math.Min(dmg, (long)cfg.capMilliHp);
        }

        public static int ComputeMilliHp(int relativeSpeedMmPerS, int massGrams, int targetMultPermille, bool restingContact) =>
            ComputeMilliHp(relativeSpeedMmPerS, massGrams, targetMultPermille, restingContact, DebrisDamageConfig.Default);
    }

    /// <summary>
    /// Collects contact damage during a physics step and releases it in a stable order (target key, then source key, ordinal),
    /// independent of the order PhysX reports collisions. One entry per (target, source) pair per tick: the largest wins,
    /// so a duplicate report of the same collision cannot double the damage.
    /// </summary>
    public sealed class DebrisDamageAccumulator<TTarget>
    {
        struct Hit { public string target, source; public int milliHp; public TTarget item; }
        readonly List<Hit> _hits = new List<Hit>();
        readonly Comparison<Hit> _order = (a, b) =>
        {
            int c = string.CompareOrdinal(a.target, b.target);
            return c != 0 ? c : string.CompareOrdinal(a.source, b.source);
        };

        public int Count => _hits.Count;

        public void Add(string targetKey, string sourceKey, int milliHp, TTarget item)
        {
            if (milliHp <= 0) return;
            for (int i = 0; i < _hits.Count; i++)
            {
                if (_hits[i].target == targetKey && _hits[i].source == sourceKey)
                {
                    if (milliHp > _hits[i].milliHp) { var h = _hits[i]; h.milliHp = milliHp; h.item = item; _hits[i] = h; }
                    return;
                }
            }
            _hits.Add(new Hit { target = targetKey ?? "", source = sourceKey ?? "", milliHp = milliHp, item = item });
        }

        /// <summary>Applies every pending hit in stable order, then clears.</summary>
        public void Flush(Action<TTarget, int> apply)
        {
            if (_hits.Count == 0) return;
            var batch = _hits.ToArray();
            _hits.Clear();
            Array.Sort(batch, _order);
            for (int i = 0; i < batch.Length; i++) apply(batch[i].item, batch[i].milliHp);
        }

        public void Clear() => _hits.Clear();
    }

    /// <summary>Collapse rule: a piece collapses only when ALL of its supports are gone. No supports at all = grounded.</summary>
    public static class SupportRule
    {
        public static bool ShouldCollapse(int supportCount, int supportsRemaining) => supportCount > 0 && supportsRemaining <= 0;
    }
}
