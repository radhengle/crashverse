using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Periodically re-evaluates support graphs so unsupported blocks collapse.
    /// </summary>
    public class StructuralIntegrity : MonoBehaviour
    {
        DestructibleBlock[] _blocks;
        int _ticks;
        public int everyTicks = 5; // 0.1 s at 50 Hz; integer counter, not float time

        public void Bind(DestructibleBlock[] blocks)
        {
            _blocks = blocks;
        }

        void FixedUpdate()
        {
            if (_blocks == null) return;
            if (++_ticks < everyTicks) return;
            _ticks = 0;
            for (int i = 0; i < _blocks.Length; i++)
            {
                var b = _blocks[i];
                if (b != null) b.EvaluateSupport();
            }
        }
    }
}
