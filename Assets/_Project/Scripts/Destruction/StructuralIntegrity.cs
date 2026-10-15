using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Periodically re-evaluates support graphs so unsupported blocks collapse.
    /// </summary>
    public class StructuralIntegrity : MonoBehaviour
    {
        DestructibleBlock[] _blocks;
        float _timer;

        public void Bind(DestructibleBlock[] blocks)
        {
            _blocks = blocks;
        }

        void FixedUpdate()
        {
            if (_blocks == null) return;
            _timer += Time.fixedDeltaTime;
            if (_timer < 0.1f) return;
            _timer = 0f;
            for (int i = 0; i < _blocks.Length; i++)
            {
                var b = _blocks[i];
                if (b != null) b.EvaluateSupport();
            }
        }
    }
}
