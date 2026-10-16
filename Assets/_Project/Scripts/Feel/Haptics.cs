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

    /// <summary>
    /// COLLAPSE_LAND haptic merge (Sound Manager, haptics.md §4): landings within 40 ms of the first one merge into ONE pulse, emitted when the
    /// window closes, with strength from the strongest material seen: steel &gt; stone &gt; brick &gt; glass &gt; wood. Unknown material = CollapseLand default (bucket 3).
    /// </summary>
    public struct CollapseHapticMerge
    {
        public const float WindowMs = HapticBuckets.MinGapMs;
        bool _open; float _startMs; int _rank;
        public bool Open => _open;
        /// <summary>Rank of the strongest material in the current window (see Rank).</summary>
        public int WinnerRank => _rank;

        /// <summary>wood 1, glass 2, brick 3, unknown 3, stone 4, steel 5.</summary>
        public static int Rank(string material)
        {
            switch (material)
            {
                case "steel": return 5; case "stone": return 4; case "brick": return 3;
                case "glass": return 2; case "wood": return 1; default: return 3;
            }
        }
        /// <summary>Collapse pulse bucket per material rank: wood/glass 2, brick/stone/unknown 3, steel 4.</summary>
        public static int BucketForRank(int rank) => rank >= 5 ? 4 : rank >= 3 ? 3 : 2;

        public void Add(string material, float nowMs)
        {
            if (!_open) { _open = true; _startMs = nowMs; _rank = 0; }
            int r = Rank(material); if (r > _rank) _rank = r;
        }

        /// <summary>Returns the bucket to pulse once the window (start + 40 ms) has closed, else 0.</summary>
        public int Flush(float nowMs)
        {
            if (!_open || nowMs - _startMs < WindowMs) return 0;
            _open = false; return BucketForRank(_rank);
        }
    }

    /// <summary>
    /// Haptic lead offset (Sound Manager, haptics.md §5). Pulses currently fire synchronously on the triggering event's frame, which already is
    /// the SFX transient (fire at launch, EXPLODE_CRATE at its +100 ms peak), so a negative lead is clamped to 0 today. If an event ever carries a
    /// known delay to its audio peak, the pulse is scheduled at eventMs + max(0, peakDelayMs + LeadMs): earlier than the peak, never before the event.
    /// </summary>
    public static class HapticTiming
    {
        /// <summary>Config: haptic lead relative to the audio peak (ms). Negative = before the peak. Default −20 ms.</summary>
        public static float LeadMs = -20f;
        public static float FireAtMs(float eventMs, float peakDelayMs, float leadMs) => eventMs + Math.Max(0f, peakDelayMs + leadMs);
        public static float FireAtMs(float eventMs, float peakDelayMs = 0f) => FireAtMs(eventMs, peakDelayMs, LeadMs);
    }
}
