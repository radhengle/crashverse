using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Re-evaluates support graphs every EvalEveryTicks sim ticks (5 = 0.1 s) so unsupported blocks collapse.
    /// </summary>
    public class StructuralIntegrity : MonoBehaviour
    {
        DestructibleBlock[] _blocks;
        public const int EvalEveryTicks = 5;
        int _lastEvalTick = int.MinValue;

        public void Bind(DestructibleBlock[] blocks)
        {
            _blocks = blocks;
        }

        void FixedUpdate()
        {
            if (_blocks == null) return;
            int now = Crashverse.Sim.SimRunner.NowTick;
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
