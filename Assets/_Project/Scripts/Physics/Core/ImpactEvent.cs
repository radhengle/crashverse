using System;
using System.Collections.Generic;

namespace Crashverse.Sim
{
    // Engine-free impact model (no UnityEngine). Classification, payload and deterministic per-tick ordering.
    // All thresholds are data (ImpactTierThresholds), filled from ImpactAudioProfile on the engine side. Starting tunables.

    public enum ImpactKind : byte { Impact = 0, Crack = 1, Break = 2, JointSnap = 3, CollapseStart = 4, CollapseEnd = 5 }
    public enum ImpactTier : byte { Light = 0, Medium = 1, Heavy = 2 }
    public enum SizeClass : byte { S = 0, M = 1, L = 2 }
    public enum ImpactSourceKind : byte { Cannon = 0, PowerUp = 1, Debris = 2, Explosion = 3 }
    /// <summary>Stable material family for audio/VFX banks. Ground and non-destructible scenery = Terrain.</summary>
    public enum MaterialType : byte { Wood = 0, Brick = 1, Steel = 2, Glass = 3, Terrain = 4 }

    /// <summary>Fixed-size per-material piece counts (no allocation). Dominant: highest count, ties to the lower enum value.</summary>
    [Serializable]
    public struct MaterialCounts
    {
        public int wood, brick, steel, glass, terrain;
        public int Total => wood + brick + steel + glass + terrain;
        public int Get(MaterialType t) => t switch { MaterialType.Wood => wood, MaterialType.Brick => brick, MaterialType.Steel => steel, MaterialType.Glass => glass, _ => terrain };
        public void Add(MaterialType t, int n = 1)
        {
            switch (t) { case MaterialType.Wood: wood += n; break; case MaterialType.Brick: brick += n; break; case MaterialType.Steel: steel += n; break;
                         case MaterialType.Glass: glass += n; break; default: terrain += n; break; }
        }
        public MaterialType Dominant
        {
            get { var best = MaterialType.Wood; int bc = wood;
                  for (var t = MaterialType.Brick; t <= MaterialType.Terrain; t++) { int c = Get(t); if (c > bc) { bc = c; best = t; } }
                  return Total == 0 ? MaterialType.Terrain : best; }
        }
    }

    /// <summary>
    /// Single tick definition for ImpactEvents in every mode: an event carries the tick whose physics step produced it.
    /// Ranked (Script simulation): Physics.Simulate runs inside SimRunner's tick body, so tag = current tick and flush on
    /// Stepped(t). Non-ranked (auto-simulate): Unity steps physics after all FixedUpdates, i.e. after SimRunner already
    /// advanced Tick to t+1; callbacks arriving outside the tick body are tagged t (= Tick - 1) and the buffer is flushed at
    /// the start of the next SimRunner tick (TickBegin), before that tick's logic. Reports raised by script inside the tick body
    /// in non-ranked mode are tagged with the current tick and wait for the same flush, so each flush holds exactly one tick.
    /// </summary>
    public static class ImpactTickPolicy
    {
        public static int Tag(int runnerTick, bool insideTickBody) => insideTickBody ? runnerTick : Math.Max(0, runnerTick - 1);
        public static bool FlushOnStepped(bool ranked) => ranked;
        public static bool FlushOnTickBegin(bool ranked) => !ranked;
    }

    /// <summary>Engine-free 3-vector so the Core asmdef stays noEngineReferences.</summary>
    [Serializable]
    public struct SimVec3
    {
        public float x, y, z;
        public SimVec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public override string ToString() => $"({x}, {y}, {z})";
    }

    /// <summary>Tier and size thresholds (data). Light &lt; mediumMin ≤ Medium ≤ heavyMin &lt; Heavy, in N·s.</summary>
    [Serializable]
    public struct ImpactTierThresholds
    {
        public float mediumMinImpulse;   // default 40 N·s
        public float heavyMinImpulse;    // default 150 N·s (Heavy is strictly greater)
        public float sizeMediumMinKg;    // default 5 kg
        public float sizeLargeMinKg;     // default 50 kg
        public static ImpactTierThresholds Default => new ImpactTierThresholds
            { mediumMinImpulse = 40f, heavyMinImpulse = 150f, sizeMediumMinKg = 5f, sizeLargeMinKg = 50f };

        public bool IsValid => mediumMinImpulse >= 0f && heavyMinImpulse >= mediumMinImpulse && sizeLargeMinKg >= sizeMediumMinKg;
    }

    public static class ImpactClassifier
    {
        /// <summary>Light &lt; 40, Medium 40–150 (inclusive), Heavy &gt; 150 with default thresholds. NaN/negative → Light.</summary>
        public static ImpactTier Tier(float impulse, in ImpactTierThresholds t)
        {
            if (!(impulse >= t.mediumMinImpulse)) return ImpactTier.Light; // also catches NaN
            return impulse > t.heavyMinImpulse ? ImpactTier.Heavy : ImpactTier.Medium;
        }

        public static SizeClass Size(float massKg, in ImpactTierThresholds t)
        {
            if (!(massKg >= t.sizeMediumMinKg)) return SizeClass.S;
            return massKg >= t.sizeLargeMinKg ? SizeClass.L : SizeClass.M;
        }
    }

    /// <summary>
    /// Payload raised through AnimEventBus for Impact/Crack/Break/JointSnap/CollapseStart/CollapseEnd. Produced on the sim tick;
    /// consumers (audio, VFX, camera, hit-stop) are presentation-only and must never write back into the sim.
    /// </summary>
    [Serializable]
    public struct ImpactEvent
    {
        public int tick;
        public ImpactKind kind;
        public ImpactTier tier;
        public float impulse;          // N·s
        public int materialA, materialB; // raw MaterialDefinition index; -1 = none/ground (kept for debugging)
        public MaterialType materialTypeA, materialTypeB; // stable families for audio banks; ground = Terrain
        public MaterialType dominantMaterial; // CollapseStart/End: dominant family of the cascade's pieces
        public MaterialCounts materialCounts; // CollapseStart (planned) / CollapseEnd (final) per-material counts
        public SimVec3 position, normal;
        public int pieceId;            // -1 for none (e.g. CollapseStart/End use cascade only)
        public SizeClass sizeClass;
        public int damageStep;         // crack stage 1..3 for Crack, else 0
        public int cascadeId;          // 0 = not part of a collapse
        public int cascadeCount;       // pieces broken so far in this cascade (Break, CollapseEnd), planned/initial for CollapseStart
        public ImpactSourceKind source;
        public int sourceId;           // power-up id when source == PowerUp, else 0
        public int seq;                // insertion order within the tick, final tie-breaker

        public static ImpactEvent Make(int tick, ImpactKind kind, float impulse, float massKg, in ImpactTierThresholds t)
            => new ImpactEvent { tick = tick, kind = kind, impulse = impulse, tier = ImpactClassifier.Tier(impulse, t),
                                 sizeClass = ImpactClassifier.Size(massKg, t), pieceId = -1, materialA = -1, materialB = -1,
                                 materialTypeA = MaterialType.Terrain, materialTypeB = MaterialType.Terrain, dominantMaterial = MaterialType.Terrain };
    }

    /// <summary>Deterministic ordering inside a tick: kind (CollapseStart first, CollapseEnd last), cascade, piece, source, seq.
    /// Never compares floats from PhysX, so ordering does not depend on callback order differences in float results.</summary>
    public sealed class ImpactEventOrder : IComparer<ImpactEvent>
    {
        public static readonly ImpactEventOrder Instance = new ImpactEventOrder();
        static int Rank(ImpactKind k) => k switch
        {
            ImpactKind.CollapseStart => 0, ImpactKind.JointSnap => 1, ImpactKind.Break => 2,
            ImpactKind.Crack => 3, ImpactKind.Impact => 4, _ => 5 // CollapseEnd
        };
        public int Compare(ImpactEvent a, ImpactEvent b)
        {
            int c = a.tick.CompareTo(b.tick); if (c != 0) return c;
            c = Rank(a.kind).CompareTo(Rank(b.kind)); if (c != 0) return c;
            c = a.cascadeId.CompareTo(b.cascadeId); if (c != 0) return c;
            c = a.pieceId.CompareTo(b.pieceId); if (c != 0) return c;
            c = ((int)a.source).CompareTo((int)b.source); if (c != 0) return c;
            c = a.sourceId.CompareTo(b.sourceId); if (c != 0) return c;
            return a.seq.CompareTo(b.seq);
        }
    }

    /// <summary>Collects events during a tick and flushes them sorted. Allocation-free after warm-up.</summary>
    public sealed class ImpactEventBuffer
    {
        readonly List<ImpactEvent> _pending = new List<ImpactEvent>(64);
        int _seq;
        public int Count => _pending.Count;

        public void Add(ImpactEvent e) { e.seq = _seq++; _pending.Add(e); }

        /// <summary>Emit all pending events for the tick in deterministic order, then clear.</summary>
        public void Flush(Action<ImpactEvent> emit)
        {
            _pending.Sort(ImpactEventOrder.Instance);
            for (int i = 0; i < _pending.Count; i++) emit(_pending[i]);
            _pending.Clear(); _seq = 0;
        }

        public void Clear() { _pending.Clear(); _seq = 0; }
    }

    /// <summary>Assigns cascade ids (1, 2, ...) and counts Breaks per collapse, per material. Reset on BeginRun so ids replay identically.</summary>
    public sealed class CascadeTracker
    {
        readonly Dictionary<int, MaterialCounts> _counts = new Dictionary<int, MaterialCounts>();
        int _next = 1;
        public int Begin() { int id = _next++; _counts[id] = default; return id; }
        public int RecordBreak(int cascadeId) => RecordBreak(cascadeId, MaterialType.Terrain);
        public int RecordBreak(int cascadeId, MaterialType m)
        {
            if (!_counts.TryGetValue(cascadeId, out var c)) return 0;
            c.Add(m); _counts[cascadeId] = c; return c.Total;
        }
        public MaterialCounts Counts(int cascadeId) => _counts.TryGetValue(cascadeId, out var c) ? c : default;
        public bool IsActive(int cascadeId) => _counts.ContainsKey(cascadeId);
        public int End(int cascadeId) => End(cascadeId, out _);
        public int End(int cascadeId, out MaterialCounts counts)
        {
            if (!_counts.TryGetValue(cascadeId, out counts)) return 0;
            _counts.Remove(cascadeId); return counts.Total;
        }
        public void Reset() { _counts.Clear(); _next = 1; }
    }

    /// <summary>Presentation lookup: hit-stop milliseconds per tier (0/40/80 default). Pure; used by ImpactFeelProfile.</summary>
    public static class HitStopRule
    {
        /// <summary>Returns ms to freeze presentation; 0 when reduce motion is on. Never affects sim or Time.timeScale.</summary>
        public static int Millis(ImpactTier tier, int lightMs, int mediumMs, int heavyMs, int maxMs, bool reduceMotion)
        {
            if (reduceMotion) return 0;
            int ms = tier == ImpactTier.Heavy ? heavyMs : tier == ImpactTier.Medium ? mediumMs : lightMs;
            return ms < 0 ? 0 : ms > maxMs ? maxMs : ms;
        }
    }
}
