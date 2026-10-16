using System;
using System.Collections.Generic;

namespace Crashverse.Sim
{
    /// <summary>
    /// Inputs keyed by sim tick. Stable order: ascending tick, then insertion order within a tick. Inputs for a tick that has
    /// already been simulated are rejected (returns false) so replay can't silently diverge.
    /// </summary>
    public sealed class TickInputQueue<T>
    {
        readonly List<KeyValuePair<int, T>> _items = new List<KeyValuePair<int, T>>();
        public int Count => _items.Count;
        public int LastDrainedTick { get; private set; } = -1;

        public bool Enqueue(int tick, T item)
        {
            if (tick <= LastDrainedTick || tick < 0) return false;
            int i = _items.Count;
            while (i > 0 && _items[i - 1].Key > tick) i--; // insert after all items with tick <= this one (stable)
            _items.Insert(i, new KeyValuePair<int, T>(tick, item));
            return true;
        }

        /// <summary>Calls apply for every input with tick &lt;= tick, in order, then marks tick as consumed.</summary>
        public int Drain(int tick, Action<int, T> apply)
        {
            int n = 0;
            while (_items.Count > 0 && _items[0].Key <= tick)
            {
                var kv = _items[0];
                _items.RemoveAt(0);
                apply(kv.Key, kv.Value);
                n++;
            }
            if (tick > LastDrainedTick) LastDrainedTick = tick;
            return n;
        }

        public void Clear() { _items.Clear(); LastDrainedTick = -1; }
    }

    /// <summary>Tick maths for the fixed 50 Hz sim (0.02 s). Integer ticks are the only sim clock.</summary>
    public static class SimTime
    {
        public const int TicksPerSecond = 50;
        public const float Step = 0.02f;
        /// <summary>Seconds to whole ticks, rounding up (2.5 s -> 125).</summary>
        public static int SecondsToTicks(float seconds) => seconds <= 0f ? 0 : (int)Math.Ceiling(seconds * TicksPerSecond - 1e-4);
    }
}
