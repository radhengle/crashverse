using UnityEngine;

namespace Crashverse.Perf
{
    /// <summary>
    /// Tiers vary visuals, frame-rate target and debris caps only (25 low / 60 mid / 90 high, Feel.DebrisBudget).
    /// Physics is identical on every tier so scored runs replay the same: fixed 0.02 s step, 6 solver iterations, sleep 0.08.
    /// </summary>
    public class QualityController : MonoBehaviour
    {
        public enum Tier { Mid, Low, High }
        public static Crashverse.Feel.QualityTier FeelTier => Current == Tier.Low ? Crashverse.Feel.QualityTier.Low : Current == Tier.High ? Crashverse.Feel.QualityTier.High : Crashverse.Feel.QualityTier.Mid;
        public static event System.Action TierChanged;
        public static Tier Current { get; private set; } = Tier.Mid;

        public const float FixedStep = 0.02f;   // 50 Hz, all tiers (matches ProjectSettings/TimeManager.asset)
        public const int SolverIterations = 6;   // all tiers
        public const float SleepThreshold = 0.08f; // all tiers
        public static int DebrisCap => Crashverse.Feel.DebrisBudget.Cap(FeelTier);

        /// <summary>Physics settings shared by every tier. Never vary these per device.</summary>
        public static void LockPhysics()
        {
            Time.fixedDeltaTime = FixedStep;
            Physics.defaultSolverIterations = SolverIterations;
            Physics.sleepThreshold = SleepThreshold;
        }

        void Awake()
        {
            ApplyMid();
        }

        public static void Toggle()
        {
            if (Current == Tier.Mid) ApplyLow();
            else if (Current == Tier.Low) ApplyHigh();
            else ApplyMid();
        }

        public static void ApplyMid()
        {
            Current = Tier.Mid; TierChanged?.Invoke();
            Application.targetFrameRate = 60;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length > 1 ? 1 : 0, true);
            LockPhysics();
        }

        public static void ApplyLow()
        {
            Current = Tier.Low; TierChanged?.Invoke();
            Application.targetFrameRate = 30;
            QualitySettings.SetQualityLevel(0, true);
            LockPhysics();
        }
    
        /// <summary>High tier: 60 fps, 90 debris rigidbodies (OQ-D1).</summary>
        public static void ApplyHigh()
        {
            Current = Tier.High; TierChanged?.Invoke();
            Application.targetFrameRate = 60;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            LockPhysics();
        }
    }
}
