using System.Collections.Generic;
using Crashverse.Meta;
using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// destroyedMass / totalObjectiveMass (Contract §1). A piece counts as destroyed when broken (HP 0), out of bounds
    /// (fell off the island), or toppled (dropped more than max(1m, half its height) from its authored position).
    /// Settle = no dynamic ObjectivePiece faster than 0.15 m/s for 0.75 s. Also tracks Titan damage, combo and roles.
    /// </summary>
    public class DestructionTracker : MonoBehaviour
    {
        public const float SettleVelocityThreshold = 0.15f;
        public const float SettleHoldSeconds = 0.75f;

        readonly List<DestructibleBlock> _pieces = new List<DestructibleBlock>();
        readonly HashSet<DestructibleBlock> _counted = new HashSet<DestructibleBlock>();
        readonly BreakLedger<DestructibleBlock> _ledger = new BreakLedger<DestructibleBlock>();
        float _totalMass, _destroyedMass, _quietTime, _totalHp;
        public float DamageDealt { get; private set; }
        public float TotalObjectiveHp => _totalHp;
        public readonly ComboTracker Combo = new ComboTracker();
        public readonly HashSet<string> FirstImpactRoles = new HashSet<string>();
        bool _awaitingFirstImpact;

        public float TotalObjectiveMass => _totalMass;
        public float DestroyedMass => _destroyedMass;
        public float DestroyedRatio => _totalMass > 0.001f ? Mathf.Clamp01(_destroyedMass / _totalMass) : 0f;
        public bool IsSettled => _quietTime >= SettleHoldSeconds;
        public float QuietTime => _quietTime;
        public IReadOnlyList<DestructibleBlock> Pieces => _pieces;

        public event System.Action<int> ComboChanged;

        public void Register(IEnumerable<DestructibleBlock> blocks)
        {
            UnregisterAll();
            foreach (var b in blocks)
            {
                if (b == null) continue;
                b.Damaged += OnDamaged;
                if (!b.isObjectivePiece || !Crashverse.Meta.StructureRules.CountsTowardObjectiveMass(!b.isObjectivePiece, b.indestructible)) continue; // stone bases excluded (§6)
                _pieces.Add(b);
                _totalMass += b.Mass;
                if (!b.indestructible) _totalHp += b.MaxHp;
            }
            Combo.ResetRun();
        }

        public void UnregisterAll()
        {
            foreach (var b in _pieces) if (b != null) b.Damaged -= OnDamaged;
            _pieces.Clear(); _counted.Clear(); _ledger.Clear();
            _totalMass = _destroyedMass = _totalHp = DamageDealt = 0f;
            _quietTime = SettleHoldSeconds;
            FirstImpactRoles.Clear();
        }

        /// <summary>Called on each confirmed fire: combos never carry across shots (Addendum §2).</summary>
        public void OnShotFired()
        {
            Combo.OnShot();
            _quietTime = 0f;
            _awaitingFirstImpact = true;
        }

        void OnDamaged(DestructibleBlock b, float removed, bool direct)
        {
            if (b.isObjectivePiece) DamageDealt += removed;
            if (direct && _awaitingFirstImpact) { _awaitingFirstImpact = false; if (!string.IsNullOrEmpty(b.role)) FirstImpactRoles.Add(b.role); }
            _quietTime = 0f;
        }

        public static bool CountsDestroyed(DestructibleBlock b) =>
            b.IsBroken || b.IsOutOfBounds || (b.InitialY - b.transform.position.y) > Mathf.Max(1f, b.Height * 0.5f);

        void FixedUpdate()
        {
            if (_pieces.Count == 0) return;
            float d = 0f; bool moving = false;
            foreach (var b in _pieces)
            {
                if (b == null) continue;
                bool gone = CountsDestroyed(b);
                if (gone)
                {
                    d += b.Mass;
                    // BUG-11: a piece already counted by sagging still scores its combo point when it later breaks or falls off (once per piece)
                    if (_ledger.Observe(b, true, b.IsBroken || b.IsOutOfBounds))
                        ComboChanged?.Invoke(Combo.OnBreak(Time.time));
                }
                var rb = b.Body;
                if (!b.IsBroken && !b.IsOutOfBounds && rb != null && !rb.isKinematic &&
                    (rb.velocity.sqrMagnitude > SettleVelocityThreshold * SettleVelocityThreshold ||
                     rb.angularVelocity.sqrMagnitude > SettleVelocityThreshold * SettleVelocityThreshold))
                    moving = true;
            }
            _destroyedMass = Mathf.Min(d, _totalMass);
            _quietTime = moving ? 0f : _quietTime + Time.fixedDeltaTime;
        }

        /// <summary>Role stats for mastery (SG-04/SG-21), taken after settle.</summary>
        public void FillRoleStats(RunStats r, IEnumerable<DestructibleBlock> all)
        {
            foreach (var b in all)
            {
                if (b == null || string.IsNullOrEmpty(b.role)) continue;
                r.RoleTotal.TryGetValue(b.role, out var t); r.RoleTotal[b.role] = t + 1;
                if (b.IsBroken) { r.RoleBroken.TryGetValue(b.role, out var br); r.RoleBroken[b.role] = br + 1; }
                float y = b.IsOutOfBounds ? float.NegativeInfinity : b.transform.position.y;
                if (b.IsOutOfBounds) y = -1000f;
                if (!r.RoleMaxCentreY.TryGetValue(b.role, out var my) || y > my) r.RoleMaxCentreY[b.role] = y;
            }
            foreach (var role in FirstImpactRoles) r.FirstImpactRoles.Add(role);
        }
    }
}
