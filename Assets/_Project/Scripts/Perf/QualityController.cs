using UnityEngine;

namespace Crashverse.Perf
{
    /// <summary>Mid = 60 fps target; Low = 30 fps target. Sleep thresholds + fixed timestep.</summary>
    public class QualityController : MonoBehaviour
    {
        public enum Tier { Mid, Low, High }
        public static Crashverse.Feel.QualityTier FeelTier => Current == Tier.Low ? Crashverse.Feel.QualityTier.Low : Current == Tier.High ? Crashverse.Feel.QualityTier.High : Crashverse.Feel.QualityTier.Mid;
        public static event System.Action TierChanged;
        public static Tier Current { get; private set; } = Tier.Mid;

        void Awake()
        {
            ApplySaved(); // #45: persisted graphics quality (default Mid)
        }

        /// <summary>Applies the tier saved in GameSettings (0 Low, 1 Mid, 2 High).</summary>
        public static void ApplySaved() => Apply(Crashverse.Core.GameSettings.Current.Quality);
        public static void Apply(int q) { if (q == 0) ApplyLow(); else if (q == 2) ApplyHigh(); else ApplyMid(); }

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
            Time.fixedDeltaTime = 0.02f; // 50 Hz physics
            Physics.sleepThreshold = 0.08f;
            Physics.defaultSolverIterations = 6;
        }

        public static void ApplyLow()
        {
            Current = Tier.Low; TierChanged?.Invoke();
            Application.targetFrameRate = 30;
            QualitySettings.SetQualityLevel(0, true);
            Time.fixedDeltaTime = 0.0333333f; // lighter physics step
            Physics.sleepThreshold = 0.12f;
            Physics.defaultSolverIterations = 4;
        }
    
        /// <summary>High tier: 60 fps, 90 debris rigidbodies (OQ-D1).</summary>
        public static void ApplyHigh()
        {
            Current = Tier.High; TierChanged?.Invoke();
            Application.targetFrameRate = 60;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            Time.fixedDeltaTime = 0.02f;
            Physics.sleepThreshold = 0.08f;
            Physics.defaultSolverIterations = 8;
        }
    }
}
