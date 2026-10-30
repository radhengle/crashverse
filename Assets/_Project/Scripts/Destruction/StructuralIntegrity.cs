using System.Collections.Generic;
using Crashverse.Sim;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Re-evaluates support graphs every EvalEveryTicks sim ticks (5 = 0.1 s) so unsupported blocks collapse, and owns the
    /// progressive-collapse schedule: a piece that lost all supports (SupportRule) drops CollapseRules.DelayTicks later
    /// (3 + material sag + 0..2 seeded jitter), due pieces popped in (tick, block index) order at the start of each tick.
    /// </summary>
    public class StructuralIntegrity : MonoBehaviour
    {
        public static StructuralIntegrity Current { get; private set; }
        DestructibleBlock[] _blocks;
        readonly Dictionary<DestructibleBlock, int> _index = new Dictionary<DestructibleBlock, int>();
        CollapseSchedule _schedule = new CollapseSchedule(1);
        int[] _due = new int[1];
        readonly ChunkEvent _ev = new ChunkEvent();
        public const int EvalEveryTicks = 5;
        int _lastEvalTick = int.MinValue;
        public int Pending => _schedule.Count;

        public void Bind(DestructibleBlock[] blocks)
        {
            _blocks = blocks;
            _index.Clear();
            int n = blocks != null ? blocks.Length : 0;
            for (int i = 0; i < n; i++) if (blocks[i] != null) _index[blocks[i]] = i;
            _schedule = new CollapseSchedule(Mathf.Max(1, n));
            _due = new int[Mathf.Max(1, n)];
            _lastEvalTick = int.MinValue;
            Current = this;
        }

        void OnEnable() { if (_blocks != null) Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        /// <summary>Queues <paramref name="b"/> to collapse after its delay (+ <paramref name="extraTicks"/>). false = not bound here.</summary>
        public bool ScheduleCollapse(DestructibleBlock b, int extraTicks)
        {
            if (b == null || !_index.TryGetValue(b, out int i)) return false;
            long seed = SimRunner.Instance != null ? SimRunner.Instance.Seed : 0L;
            int due = SimRunner.NowTick + CollapseRules.DelayTicks(ImpactReport.TypeOf(b), b.PieceId, seed) + Mathf.Max(0, extraTicks);
            return _schedule.Schedule(i, due);
        }

        void FixedUpdate()
        {
            if (_blocks == null) return;
            int now = SimRunner.NowTick;
            int n = _schedule.PopDue(now, _due);
            for (int k = 0; k < n; k++)
            {
                var b = _blocks[_due[k]];
                if (b == null || b.IsBroken || b.IsCollapsed || b.IsOutOfBounds) continue;
                b.ForceCollapse();
                _ev.tick = now; _ev.pieceId = b.PieceId; _ev.chunkIndex = -1; _ev.chunkCount = b.ChunkCount;
                _ev.kind = b.ChunkKind; _ev.material = ImpactReport.TypeOf(b); _ev.tier = ImpactTier.Light;
                _ev.position = b.transform.position.ToSim(); _ev.normal = Vector3.down.ToSim(); _ev.size = b.transform.localScale.ToSim();
                _ev.speed = 0f; _ev.presentationOnly = false; _ev.ranked = SimRunner.Instance != null && SimRunner.Instance.Ranked;
                Crashverse.Feel.AnimEventBus.Raise(ChunkEvents.CollapseStep, _ev);
            }
            if (_lastEvalTick != int.MinValue && now >= _lastEvalTick && now - _lastEvalTick < EvalEveryTicks) return;
            _lastEvalTick = now;
            for (int i = 0; i < _blocks.Length; i++)
            {
                var b = _blocks[i];
                if (b != null) b.EvaluateSupport();
            }
        }
    }
}
