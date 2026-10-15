using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Tracks destroyedMass / totalObjectiveMass per Contract §1.
    /// NonObjective pieces are excluded. Settle: no ObjectivePiece RB velocity > 0.15 for 0.75s.
    /// </summary>
    public class DestructionTracker : MonoBehaviour
    {
        public const float SettleVelocityThreshold = 0.15f;
        public const float SettleHoldSeconds = 0.75f;
        public const float CollapseHeightY = 1.25f; // below this = counted destroyed/toppled

        readonly List<DestructibleBlock> _pieces = new List<DestructibleBlock>();
        float _totalMass;
        float _destroyedMass;
        float _settleTimer;
        bool _settled = true;

        public float TotalObjectiveMass => _totalMass;
        public float DestroyedMass => _destroyedMass;
        public float DestroyedRatio => _totalMass > 0.001f ? _destroyedMass / _totalMass : 0f;
        public bool IsSettled => _settled;

        public event System.Action<float> RatioChanged;
        public event System.Action Settled;

        public void RegisterStructure(IEnumerable<DestructibleBlock> blocks)
        {
            _pieces.Clear();
            _totalMass = 0f;
            _destroyedMass = 0f;
            _settleTimer = 0f;
            _settled = true;

            foreach (var b in blocks)
            {
                if (b == null || !b.isObjectivePiece) continue;
                _pieces.Add(b);
                _totalMass += b.Mass;
                b.Broken += OnBroken;
                b.Collapsed += OnCollapsed;
            }
            RatioChanged?.Invoke(DestroyedRatio);
        }

        public void UnregisterAll()
        {
            foreach (var b in _pieces)
            {
                if (b == null) continue;
                b.Broken -= OnBroken;
                b.Collapsed -= OnCollapsed;
            }
            _pieces.Clear();
            _totalMass = 0f;
            _destroyedMass = 0f;
        }

        void OnBroken(DestructibleBlock b)
        {
            RecalcDestroyed();
            _settled = false;
            _settleTimer = 0f;
        }

        void OnCollapsed(DestructibleBlock b)
        {
            _settled = false;
            _settleTimer = 0f;
        }

        void RecalcDestroyed()
        {
            float d = 0f;
            foreach (var b in _pieces)
            {
                if (b == null) continue;
                if (b.IsBroken)
                {
                    d += b.Mass;
                    continue;
                }
                // Toppled below height counts toward objective
                if (b.transform.position.y < CollapseHeightY)
                    d += b.Mass;
            }
            _destroyedMass = Mathf.Min(d, _totalMass);
            RatioChanged?.Invoke(DestroyedRatio);
        }

        void FixedUpdate()
        {
            if (_pieces.Count == 0) return;

            bool anyMoving = false;
            foreach (var b in _pieces)
            {
                if (b == null || b.IsBroken) continue;
                var rb = b.GetComponent<Rigidbody>();
                if (rb == null || rb.isKinematic) continue;
                if (rb.velocity.sqrMagnitude > SettleVelocityThreshold * SettleVelocityThreshold ||
                    rb.angularVelocity.sqrMagnitude > SettleVelocityThreshold * SettleVelocityThreshold)
                {
                    anyMoving = true;
                    break;
                }
            }

            // Also refresh toppled mass while settling
            RecalcDestroyed();

            if (anyMoving)
            {
                _settled = false;
                _settleTimer = 0f;
            }
            else
            {
                _settleTimer += Time.fixedDeltaTime;
                if (!_settled && _settleTimer >= SettleHoldSeconds)
                {
                    _settled = true;
                    Settled?.Invoke();
                }
            }
        }
    }
}
