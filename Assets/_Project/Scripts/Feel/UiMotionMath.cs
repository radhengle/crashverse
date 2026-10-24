using System;

namespace Crashverse.Feel
{
    /// <summary>
    /// 06 UI motion curves (engine-free, unscaled ms). Reduce Motion variants per 07: overshoots removed, pops capped at 1.1, colour-only
    /// threshold tick. Star pop / button release / combo peak sizes reuse WinSequence, ButtonMotion and ComboUiStateMachine from FeelCore.
    /// </summary>
    public static class UiMotionMath
    {
        // ── Button (06: 1 → 0.92 on press, 120 ms out-quad; release via ButtonMotion.ReleaseScale) ──
        public static float PressScale(float ms) => 1f + (ButtonMotion.Pressed - 1f) * Ease.OutQuad(ms / ButtonMotion.Ms);

        // ── Stars (06: each 250 ms, 300 ms stagger, starting after the 300 ms ribbon) ──
        public const float StarStartMs = 300f, StarStaggerMs = 300f, StarPeakMs = 150f, EmptyStarFadeMs = 150f;
        /// <summary>ms into star <paramref name="index"/> (0-based) for a sequence that started <paramref name="seqMs"/> ago.</summary>
        public static float MsIntoStar(float seqMs, int index) => seqMs - (StarStartMs + StarStaggerMs * index);
        /// <summary>Earned star: 0 → 1.3 → 1 (Reduce Motion peak 1.1, no overshoot ease). Unearned: no pop, scale 1, alpha fades in.</summary>
        public static float StarScale(float msIntoStar, bool earned, bool reduceMotion) =>
            earned ? WinSequence.StarScale(msIntoStar, reduceMotion) : (msIntoStar <= 0f ? 0f : 1f);
        public static float EmptyStarAlpha(float msIntoStar) => msIntoStar <= 0f ? 0f : Math.Min(1f, msIntoStar / EmptyStarFadeMs);

        // ── Combo (06: in 180 ms 0→peak→1, punch 1→1.12→1 in 80 ms, tier-up 1→peak→1 in 250 ms, out 250 ms) ──
        public const float ComboPunch = 1.12f;
        public static float ComboScale(ComboUiState s, float ms, int n, bool reduceMotion)
        {
            float peak = ComboUiStateMachine.PeakScale(n, reduceMotion);
            switch (s)
            {
                case ComboUiState.Show: return UpDown(ms / ComboUiStateMachine.InMs, 0f, peak, 1f, reduceMotion);
                case ComboUiState.Punch: return UpDown(ms / ComboUiStateMachine.PunchMs, 1f, reduceMotion ? Math.Min(ComboPunch, 1.05f) : ComboPunch, 1f, reduceMotion);
                case ComboUiState.TierUp: return UpDown(ms / ComboUiStateMachine.TierUpMs, 1f, peak, 1f, reduceMotion);
                case ComboUiState.Hold: return 1f;
                case ComboUiState.Out: return 1f - Ease.InQuad(ms / ComboUiStateMachine.OutMs);
                default: return 0f;
            }
        }
        public static float ComboAlpha(ComboUiState s, float ms) =>
            s == ComboUiState.Hidden ? 0f : s == ComboUiState.Out ? 1f - Ease.InQuad(ms / ComboUiStateMachine.OutMs) : 1f;

        /// <summary>a → peak over the first 60% (out-back, or out-quad under Reduce Motion), then peak → b (in-out sine).</summary>
        static float UpDown(float t, float a, float peak, float b, bool reduceMotion)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            const float split = 0.6f;
            if (t < split) { float k = Ease.OutQuad(t / split); return a + (peak - a) * k; }
            return peak + (b - peak) * Ease.InOutSine((t - split) / (1f - split));
        }

        // ── Count-up (06: score 600 ms ease-out-quad on results; HUD per settle 300 ms; damage counter 400 ms) ──
        public const float ResultsScoreMs = 600f, HudScoreMs = 300f, DamageTickMs = 400f;
        /// <summary>Integer count-up from → to (floored, exact at the end).</summary>
        public static int CountUp(int from, int to, float ms, float durationMs)
        {
            if (durationMs <= 0f || ms >= durationMs) return to;
            if (ms <= 0f) return from;
            return from + (int)Math.Floor((to - from) * Ease.OutQuad(ms / durationMs));
        }

        // ── % bar + win tick (06: fill 400 ms ease-out per settle; tick at level winThreshold pops 1→1.4→1 in 250 ms out-back) ──
        public const float BarFillMs = 400f, TickPopMs = 250f, TickPeak = 1.4f;
        public static float TickScale(float msSinceHit, bool reduceMotion)
        {
            if (reduceMotion || msSinceHit < 0f || msSinceHit >= TickPopMs) return 1f; // Reduce Motion: colour change only
            return UpDown(msSinceHit / TickPopMs, 1f, TickPeak, 1f, false);
        }
        /// <summary>The tick "hits" the first time the displayed fill reaches the threshold (≥, same rule as WinRule).</summary>
        public static bool ReachedThreshold(float displayedFill, float threshold) => displayedFill + 1e-4f >= threshold;

        // ── Shots-left punch (06: 200 ms, 1.25) ──
        public const float ShotPunchMs = 200f, ShotPunch = 1.25f;
        public static float ShotPunchScale(float ms, bool reduceMotion)
        {
            if (ms < 0f || ms >= ShotPunchMs) return 1f;
            return UpDown(ms / ShotPunchMs, 1f, reduceMotion ? 1.1f : ShotPunch, 1f, reduceMotion);
        }
    }
}
