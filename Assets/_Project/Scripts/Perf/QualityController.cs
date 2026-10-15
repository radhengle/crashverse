using UnityEngine;

namespace Crashverse.Perf
{
    /// <summary>Mid = 60 fps target; Low = 30 fps target. Sleep thresholds + fixed timestep.</summary>
    public class QualityController : MonoBehaviour
    {
        public enum Tier { Mid, Low }
        public static Tier Current { get; private set; } = Tier.Mid;

        void Awake()
        {
            ApplyMid();
        }

        public static void Toggle()
        {
            if (Current == Tier.Mid) ApplyLow();
            else ApplyMid();
        }

        public static void ApplyMid()
        {
            Current = Tier.Mid;
            Application.targetFrameRate = 60;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length > 1 ? 1 : 0, true);
            Time.fixedDeltaTime = 0.02f; // 50 Hz physics
            Physics.sleepThreshold = 0.08f;
            Physics.defaultSolverIterations = 6;
        }

        public static void ApplyLow()
        {
            Current = Tier.Low;
            Application.targetFrameRate = 30;
            QualitySettings.SetQualityLevel(0, true);
            Time.fixedDeltaTime = 0.0333333f; // lighter physics step
            Physics.sleepThreshold = 0.12f;
            Physics.defaultSolverIterations = 4;
        }
    }
}
