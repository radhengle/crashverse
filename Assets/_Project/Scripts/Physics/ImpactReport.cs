using Crashverse.Destruction;
using UnityEngine;

namespace Crashverse.Sim
{
    /// <summary>
    /// Wiring helpers used by destruction, collapse, projectile and joint code. Everything goes into the ImpactEventEmitter's
    /// per-tick buffer (deterministic order, ImpactTickPolicy tagging). No-ops when no emitter exists (menus, editor tools).
    /// Source comes from ImpactSourceScope unless given explicitly.
    /// </summary>
    public static class ImpactReport
    {
        static ImpactEventEmitter E => ImpactEventEmitter.Instance;

        public static MaterialType TypeOf(DestructibleBlock b)
        {
            if (b == null || b.material == null) return MaterialType.Terrain;
            switch (b.material.kind)
            {
                case MaterialKind.Wood: return MaterialType.Wood;
                case MaterialKind.Brick: return MaterialType.Brick;
                case MaterialKind.Steel: return MaterialType.Steel;
                case MaterialKind.Glass: return MaterialType.Glass;
                case MaterialKind.Stone: return MaterialType.Stone;
                default: return MaterialType.Terrain;
            }
        }

        public static int IndexOf(DestructibleBlock b) => b != null && b.material != null ? (int)b.material.kind : -1;
        public static int IdOf(DestructibleBlock b) => b != null ? StableId.Of(b.blockId) : -1;
        static int IdOfCollider(Component c) => c == null ? -1 : StableId.Of("~" + c.gameObject.name);

        static ImpactEvent Base(ImpactKind kind, float impulse, DestructibleBlock a, DestructibleBlock b, Vector3 pos, Vector3 normal)
        {
            var e = ImpactEvent.Make(E.Tick, kind, impulse, a != null ? a.Mass : 0f, E.Thresholds);
            e.pieceId = IdOf(a); e.materialA = IndexOf(a); e.materialB = IndexOf(b);
            e.materialTypeA = TypeOf(a); e.materialTypeB = TypeOf(b);
            e.position = pos.ToSim(); e.normal = normal.ToSim();
            e.source = ImpactSourceScope.Kind; e.sourceId = ImpactSourceScope.Id;
            return e;
        }

        /// <summary>Collision Impact if it qualifies (impulse or speed) and the unordered pair isn't on cooldown.</summary>
        public static void Collision(DestructibleBlock a, DestructibleBlock b, Component otherCollider, float impulse, float relSpeed,
                                     Vector3 pos, Vector3 normal, ImpactSourceKind source, int sourceId = 0)
        {
            var em = E; if (em == null) return;
            var cfg = em.reportConfig;
            if (!ImpactQualify.Qualifies(impulse, DebrisDamage.ToMmPerS(relSpeed), cfg)) return;
            int idB = b != null ? IdOf(b) : IdOfCollider(otherCollider);
            if (!em.Throttle.TryPass(IdOf(a), idB, em.Tick, cfg.pairCooldownTicks)) return;
            var e = Base(ImpactKind.Impact, impulse, a, b, pos, normal);
            e.source = source; e.sourceId = sourceId;
            em.Add(e);
        }

        public static void Crack(DestructibleBlock a, int step)
        {
            var em = E; if (em == null) return;
            var e = Base(ImpactKind.Crack, 0f, a, null, a.transform.position, Vector3.up);
            e.damageStep = step; em.Add(e);
        }

        public static void Break(DestructibleBlock a)
        {
            var em = E; if (em == null) return;
            var e = Base(ImpactKind.Break, 0f, a, null, a.transform.position, Vector3.up);
            e.cascadeId = em.Session.JoinForBreak(em.Tick, out e.cascadeCount, e.materialTypeA);
            em.Add(e);
        }

        public static void JointSnap(DestructibleBlock a, DestructibleBlock other, float breakForce)
        {
            var em = E; if (em == null) return;
            // Joint break force (N) over one step → impulse (N·s) for tiering.
            var e = Base(ImpactKind.JointSnap, breakForce * SimTime.Step, a, other, a.transform.position, Vector3.up);
            em.Add(e);
        }

        /// <summary>A piece lost all supports. Starts a cascade (CollapseStart) if none is open, else joins it.</summary>
        public static void Collapse(DestructibleBlock a)
        {
            var em = E; if (em == null) return;
            var type = TypeOf(a);
            int id = em.Session.JoinForCollapse(em.Tick, out bool started, out int count, type);
            if (!started) return;
            var s = Base(ImpactKind.CollapseStart, 0f, a, null, a.transform.position, Vector3.down);
            s.pieceId = -1; s.cascadeId = id; s.cascadeCount = count;
            s.materialCounts = em.Cascades.Counts(id); s.dominantMaterial = s.materialCounts.Dominant;
            em.Add(s);
        }
    }
}
