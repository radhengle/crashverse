using System.Collections.Generic;

namespace Crashverse.Meta
{
    /// <summary>
    /// Per-run bookkeeping for objective pieces. "Destroyed" (for %) and "combo point" are tracked separately, so a piece that first
    /// counts as destroyed by sagging still earns its one combo point when it later breaks or leaves the island (BUG-11).
    /// </summary>
    public class BreakLedger<T>
    {
        readonly HashSet<T> _destroyed = new HashSet<T>();
        readonly HashSet<T> _combo = new HashSet<T>();

        /// <returns>True if this observation should add a combo point (once per piece per run).</returns>
        public bool Observe(T piece, bool countsDestroyed, bool brokenOrOutOfBounds)
        {
            if (countsDestroyed || brokenOrOutOfBounds) _destroyed.Add(piece);
            return brokenOrOutOfBounds && _combo.Add(piece);
        }

        public bool IsDestroyed(T piece) => _destroyed.Contains(piece);
        public void Clear() { _destroyed.Clear(); _combo.Clear(); }
    }
}
