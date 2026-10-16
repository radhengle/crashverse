using System;

namespace Crashverse.Feel
{
    /// <summary>Haptics output. Driven by ShakeDispatcher from the same accepted shake events (fire, impacts, breaks, collapse, explosions, power-ups, bosses).</summary>
    public interface IHaptics
    {
        /// <summary>Short pulse for a strength bucket (0 = none, 1..4 light → heavy). nowMs = real (unscaled) time, used for the 40 ms rate limit.</summary>
        void Pulse(int bucket, float nowMs);
    }

    public sealed class NullHaptics : IHaptics { public static readonly NullHaptics Instance = new NullHaptics(); public void Pulse(int bucket, float nowMs) { } }

    /// <summary>Strength → bucket → (ms, amplitude 1–255). Values follow audio/haptics.md (light_tick / impact / break_med / boom). Engine-free, tested.</summary>
    public static class HapticBuckets
    {
        public const int Count = 4;
        public const float MinGapMs = 40f; // haptics.md: max 1 haptic per 40 ms
        static readonly int[] Ms = { 0, 12, 25, 40, 60 }, Amp = { 0, 60, 120, 180, 255 };
        /// <summary>strength = max(pos/0.15 m, rot/1.5°): wood 0.13 → 1, brick break 0.27 / fire 0.4 → 2, collapse 0.53 / explosion 0.67 → 3, boss 0.8 → 4.</summary>
        public static int For(float strength) => strength < 0.05f ? 0 : strength < 0.2f ? 1 : strength < 0.45f ? 2 : strength < 0.7f ? 3 : 4;
        public static int DurationMs(int bucket) => Ms[Clamp(bucket)];
        public static int Amplitude(int bucket) => Amp[Clamp(bucket)];
        static int Clamp(int b) => b < 0 ? 0 : b > Count ? Count : b;
    }

    /// <summary>Rate limiter shared by platform implementations: max one pulse per 40 ms, except a stronger bucket may cut in (haptics.md "★ cancels a lower one").</summary>
    public struct HapticGate
    {
        float _lastMs; int _lastBucket; bool _any;
        public bool Allow(int bucket, float nowMs)
        {
            if (bucket <= 0) return false;
            if (_any && nowMs - _lastMs < HapticBuckets.MinGapMs && bucket <= _lastBucket) return false;
            _any = true; _lastMs = nowMs; _lastBucket = bucket; return true;
        }
    }
}
