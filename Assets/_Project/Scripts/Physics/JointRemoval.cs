using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.Sim
{
    /// <summary>
    /// Tick-exact structural joint removal (JointBreakPolicy). Joints never break by force, so the damage/support path removes
    /// them. A deferred Destroy lands at end of frame, after a device-dependent number of 50 Hz ticks, and DestroyImmediate is
    /// not allowed inside PhysX contact callbacks. So removals queue here and SimRunner flushes the queue with DestroyImmediate
    /// at tick begin and again just before the step: every joint queued before a step is gone for that step, on every device.
    /// Outside a running sim (EditMode tests, editor tools) removal is immediate.
    /// </summary>
    public static class JointRemoval
    {
        static readonly List<Joint> _pending = new List<Joint>(64);
        public static int Pending => _pending.Count;

        public static void Remove(Joint j)
        {
            if (j == null) return;
            var sim = SimRunner.Instance;
            if (!Application.isPlaying) { Object.DestroyImmediate(j); return; }
            if (sim == null || !sim.Running) { Object.Destroy(j); return; } // no sim clock to stay exact against
            if (!_pending.Contains(j)) _pending.Add(j);
        }

        /// <summary>Called by SimRunner outside physics callbacks.</summary>
        public static void Flush()
        {
            if (_pending.Count == 0) return;
            for (int i = 0; i < _pending.Count; i++) if (_pending[i] != null) Object.DestroyImmediate(_pending[i]);
            _pending.Clear();
        }
    }
}
