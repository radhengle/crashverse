using System;
using System.Collections.Generic;

namespace Crashverse.Sim
{
    // Engine-free rules used by the destruction wiring. All values are starting tunables held in data.

    /// <summary>Design table ids (Design Pack v1 §2) carried in ImpactEvent.sourceId when source == PowerUp.</summary>
    public static class PowerUpIds
    {
        public const int None = 0, Shockwave = 1, Splitstrike = 2, GravityWell = 3, MegaBomb = 4, PhasePiercer = 5, TimeFracture = 6, Shield = 7, GuidedRocket = 8; // v2: Earthquake=1, Multi Shot=2, Black Hole=3, Mega Bomb=4 (took Chain Reactor's id), Laser Beam=5, Time Freeze=6
    }

    /// <summary>Crack steps at 33/66/90% damage taken (step 1..3). Returns 0 when undamaged.</summary>
    public static class CrackSteps
    {
        public static readonly int[] DefaultPermille = { 330, 660, 900 };

        /// <summary>Step from damage fraction in permille (integer so results don't depend on float rounding at the boundary).</summary>
        public static int StepFor(int damagePermille, int[] thresholds = null)
        {
            var t = thresholds ?? DefaultPermille; int s = 0;
            for (int i = 0; i < t.Length; i++) if (damagePermille >= t[i]) s = i + 1;
            return s;
        }

        public static int DamagePermille(float hp, float maxHp)
        {
            if (!(maxHp > 0f)) return 0;
            double d = 1.0 - Math.Max(0.0, hp) / maxHp;
            return (int)Math.Round(Math.Min(1.0, Math.Max(0.0, d)) * 1000.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Calls emit(step) for every step newly crossed, ascending (one big hit can cross several).</summary>
        public static int Advance(int previousStep, int newStep, Action<int> emit)
        {
            for (int s = previousStep + 1; s <= newStep; s++) emit(s);
            return Math.Max(previousStep, newStep);
        }
    }

    [Serializable]
    public struct ImpactReportConfig
    {
        public float minImpulse;       // N·s below which a collision raises no Impact (default 5)
        public int minRelSpeedMmPerS;  // and below this relative speed (default 1000); either qualifies
        public int pairCooldownTicks;  // same pair reports at most once per N ticks (default 10)
        public int cascadeQuietTicks;  // cascade ends after N ticks with no collapse/break (default 25)
        public static ImpactReportConfig Default => new ImpactReportConfig
            { minImpulse = 5f, minRelSpeedMmPerS = 1000, pairCooldownTicks = 10, cascadeQuietTicks = 25 };
    }

    public static class ImpactQualify
    {
        public static bool Qualifies(float impulse, int relSpeedMmPerS, in ImpactReportConfig c)
            => impulse >= c.minImpulse || relSpeedMmPerS >= c.minRelSpeedMmPerS;
    }

    /// <summary>Per-pair throttle. Unordered pair key, so the duplicate callback from the other body is dropped too.</summary>
    public sealed class ImpactPairThrottle
    {
        readonly Dictionary<long, int> _last = new Dictionary<long, int>();
        public static long Key(int a, int b) { int lo = Math.Min(a, b), hi = Math.Max(a, b); return ((long)lo << 32) | (uint)hi; }
        public bool TryPass(int a, int b, int tick, int cooldownTicks)
        {
            long k = Key(a, b);
            if (_last.TryGetValue(k, out var t) && tick - t < cooldownTicks) return false;
            _last[k] = tick; return true;
        }
        public void Clear() => _last.Clear();
    }

    /// <summary>Stable int id from a string blockId (FNV-1a 32). Same on every device and run; never 0 or -1.</summary>
    public static class StableId
    {
        public static int Of(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
                int r = (int)(h & 0x7FFFFFFF);
                return r <= 1 ? r + 2 : r;
            }
        }
    }

    /// <summary>Damage source for whatever is applied inside the scope. Nested scopes: innermost wins. Default Debris.</summary>
    public static class ImpactSourceScope
    {
        struct Entry { public ImpactSourceKind kind; public int id; }
        static readonly List<Entry> _stack = new List<Entry>();
        public static ImpactSourceKind Kind => _stack.Count > 0 ? _stack[_stack.Count - 1].kind : ImpactSourceKind.Debris;
        public static int Id => _stack.Count > 0 ? _stack[_stack.Count - 1].id : 0;
        public static Handle Push(ImpactSourceKind kind, int id = 0) { _stack.Add(new Entry { kind = kind, id = id }); return new Handle(_stack.Count); }
        public static void ResetForTests() => _stack.Clear();
        public readonly struct Handle : IDisposable
        {
            readonly int _depth;
            public Handle(int depth) { _depth = depth; }
            public void Dispose() { while (_stack.Count >= _depth && _stack.Count > 0) _stack.RemoveAt(_stack.Count - 1); }
        }
    }

    /// <summary>
    /// One structural collapse at a time. A collapse while no cascade is open starts one (CollapseStart); further collapses
    /// and breaks join it and refresh its activity; it ends (CollapseEnd) after quietTicks without activity.
    /// </summary>
    public sealed class CascadeSession
    {
        readonly CascadeTracker _tracker;
        public CascadeSession(CascadeTracker tracker = null) { _tracker = tracker ?? new CascadeTracker(); }
        public CascadeTracker Tracker => _tracker;
        public int ActiveId { get; private set; }
        int _lastActivity;

        /// <summary>Returns (cascadeId, started) for a collapsing piece; counts it.</summary>
        public int JoinForCollapse(int tick, out bool started, out int count, MaterialType m = MaterialType.Terrain)
        {
            started = ActiveId == 0;
            if (started) ActiveId = _tracker.Begin();
            _lastActivity = tick;
            count = _tracker.RecordBreak(ActiveId, m);
            return ActiveId;
        }

        /// <summary>For a Break: joins the open cascade if any (cascadeId 0 when none).</summary>
        public int JoinForBreak(int tick, out int count, MaterialType m = MaterialType.Terrain)
        {
            count = 0; if (ActiveId == 0) return 0;
            _lastActivity = tick; count = _tracker.RecordBreak(ActiveId, m); return ActiveId;
        }

        /// <summary>Call once per tick after the step. Returns true and the closing id/count when the cascade went quiet.</summary>
        public bool TryEnd(int tick, int quietTicks, out int endedId, out int count) => TryEnd(tick, quietTicks, out endedId, out count, out _);
        public bool TryEnd(int tick, int quietTicks, out int endedId, out int count, out MaterialCounts counts)
        {
            endedId = 0; count = 0; counts = default;
            if (ActiveId == 0 || tick - _lastActivity < quietTicks) return false;
            endedId = ActiveId; count = _tracker.End(ActiveId, out counts); ActiveId = 0; return true;
        }

        public void Reset() { _tracker.Reset(); ActiveId = 0; _lastActivity = 0; }
    }
}
