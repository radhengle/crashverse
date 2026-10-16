using System.Collections.Generic;

namespace Crashverse.Meta
{
    /// <summary>
    /// Scoring-critical objective state kept outside PhysX (Backend request). The sim feeds it events by piece id; each
    /// objective piece counts once (break, out-of-bounds or sag, whichever comes first). Ratios are integer basis points,
    /// so client and server agree exactly given the same event stream.
    /// </summary>
    public sealed class ObjectiveCounter
    {
        readonly HashSet<string> _objectives = new HashSet<string>(System.StringComparer.Ordinal);
        readonly HashSet<string> _destroyed = new HashSet<string>(System.StringComparer.Ordinal);

        public int Total => _objectives.Count;
        public int Destroyed => _destroyed.Count;
        /// <summary>Destroyed/total in basis points (floor), same maths as WinRule.FractionBp.</summary>
        public int DestroyedBp => WinRule.FractionBp(Destroyed, Total);

        public void Reset(IEnumerable<string> objectivePieceIds)
        {
            _objectives.Clear(); _destroyed.Clear();
            if (objectivePieceIds == null) return;
            foreach (var id in objectivePieceIds) if (!string.IsNullOrEmpty(id)) _objectives.Add(id);
        }

        /// <returns>true the first time this objective piece is counted.</returns>
        public bool OnDestroyed(string pieceId) => pieceId != null && _objectives.Contains(pieceId) && _destroyed.Add(pieceId);

        public bool IsDestroyed(string pieceId) => pieceId != null && _destroyed.Contains(pieceId);

        /// <summary>Count-based win check against a level threshold (e.g. 0.85), in basis points.</summary>
        public bool Meets(float winThreshold) => WinRule.MeetsFraction(Destroyed, Total, winThreshold > 0f ? winThreshold : WinRule.DefaultWinThreshold);
    }
}
