using System.Collections.Generic;

namespace Crashverse.Meta
{
    /// <summary>
    /// Tidewarden shutter turn rule (SG-05, Level_Rulings_v1 §3). Phase 2 starts with the shutter OPEN. The shot that ends
    /// phase 1 never toggles it; only shots fired during phase 2 toggle it (open → closed → open ...).
    /// </summary>
    public sealed class TideShutterRule
    {
        public int Phase { get; private set; } = 1;
        public bool Open { get; private set; }
        public int ClosedShutterHits { get; private set; }
        bool _shotFiredInPhase2;

        public void OnShotFired() { _shotFiredInPhase2 = Phase == 2; }
        public void EnterPhase2() { if (Phase != 1) return; Phase = 2; Open = true; }
        /// <summary>After a shot resolves: true if the shutter toggles now (the new state is in Open).</summary>
        public bool OnShotResolved()
        {
            if (Phase != 2 || !_shotFiredInPhase2) return false; // the phase-ending shot was fired in phase 1
            _shotFiredInPhase2 = false;
            Open = !Open;
            return true;
        }
        /// <summary>A direct hit on the Tidewarden's shutter/core while the shutter is closed (phase 2 only). Mastery NO_CLOSED_SHUTTER_HIT.</summary>
        public void OnDirectHit() { if (Phase == 2 && !Open) ClosedShutterHits++; }
    }

    /// <summary>Structure rules shared by gameplay and tests.</summary>
    public static class StructureRules
    {
        /// <summary>Level_Rulings_v1 §6: Indestructible + NonObjective pieces (stone bases) are terrain, never in totalObjectiveMass.</summary>
        public static bool CountsTowardObjectiveMass(bool nonObjective, bool indestructible) => !nonObjective && !(nonObjective && indestructible);

        public static float TotalObjectiveMass(IEnumerable<(float mass, bool nonObjective, bool indestructible)> pieces)
        {
            float t = 0f;
            foreach (var p in pieces) if (CountsTowardObjectiveMass(p.nonObjective, p.indestructible)) t += p.mass;
            return t;
        }

        /// <summary>
        /// Collapse rule: a piece collapses only when ALL of its supports are gone (broken, collapsed or out of bounds).
        /// No supports = grounded, never collapses from support loss. One surviving support holds it.
        /// </summary>
        public static bool SupportLost(IReadOnlyList<bool> supportGone)
        {
            if (supportGone == null || supportGone.Count == 0) return false;
            foreach (var g in supportGone) if (!g) return false;
            return true;
        }
    }
}
