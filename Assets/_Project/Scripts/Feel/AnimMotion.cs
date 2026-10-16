using System;

namespace Crashverse.Feel
{
    /// <summary>Named easing curves so tweens can be stored as data (no delegates, no allocation). Maps onto <see cref="Ease"/>.</summary>
    public enum EaseType { Linear, InQuad, OutQuad, InCubic, OutCubic, InOutCubic, OutExpo, InExpo, InOutSine, OutBack, InBack }

    public static class EaseFn
    {
        /// <summary>Evaluates <paramref name="type"/> at t (clamped to [0,1]). <paramref name="backS"/> is the out/in-back overshoot constant.</summary>
        public static float Evaluate(EaseType type, float t, float backS = 1.70158f)
        {
            switch (type)
            {
                case EaseType.InQuad: return Ease.InQuad(t);
                case EaseType.OutQuad: return Ease.OutQuad(t);
                case EaseType.InCubic: t = Clamp01(t); return t * t * t;
                case EaseType.OutCubic: return Ease.OutCubic(t);
                case EaseType.InOutCubic: return Ease.InOutCubic(t);
                case EaseType.OutExpo: return Ease.OutExpo(t);
                case EaseType.InExpo: return Ease.InExpo(t);
                case EaseType.InOutSine: return Ease.InOutSine(t);
                case EaseType.OutBack: return Ease.OutBack(t, backS);
                case EaseType.InBack: return Ease.InBack(t, backS);
                default: return Ease.Linear(t);
            }
        }

        /// <summary>Reduce Motion variant: overshooting curves fall back to their non-overshooting equivalent (06/07: out-back → out-quad).</summary>
        public static EaseType Reduced(EaseType type, bool reduceMotion)
        {
            if (!reduceMotion) return type;
            return type == EaseType.OutBack ? EaseType.OutQuad : type == EaseType.InBack ? EaseType.InQuad : type;
        }

        internal static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }

    /// <summary>
    /// Dependency-free value tween (no DOTween/LeanTween in the repo). A plain struct: start it, then call <see cref="Tick"/> with
    /// UNSCALED seconds (UI and cosmetic motion ignore hit stop / slow-mo, 06). No allocation, no callbacks.
    /// </summary>
    public struct FloatTween
    {
        public float From, To, DurationMs, ElapsedMs, BackS;
        public EaseType Ease;
        public bool Running;

        public void Start(float from, float to, float durationMs, EaseType ease, float backS = 1.70158f)
        {
            From = from; To = to; DurationMs = durationMs; Ease = ease; BackS = backS; ElapsedMs = 0f; Running = durationMs > 0f;
        }

        /// <summary>Normalised progress 0..1 (1 when finished or never started with a duration).</summary>
        public float Progress => DurationMs <= 0f ? 1f : EaseFn.Clamp01(ElapsedMs / DurationMs);
        public float Value => From + (To - From) * EaseFn.Evaluate(Ease, Progress, BackS);
        public bool Done => !Running;

        /// <returns>Current value after advancing.</returns>
        public float Tick(float realDt)
        {
            if (Running) { ElapsedMs += realDt * 1000f; if (ElapsedMs >= DurationMs) { ElapsedMs = DurationMs; Running = false; } }
            return Value;
        }
    }

    /// <summary>
    /// 01 Aim smoothing: critically damped spring (ω = 18 rad/s on mid/high, 14 on low; spec 01 says "≈120 ms to 95%", but at ω = 18 the 120 ms point is ~63% — see SettleTime95), max 240°/s.
    /// Runs on unscaled time so aim stays responsive under Time Fracture / slow-mo. The trajectory arc and the shot always use the
    /// TARGET angles; only the visible barrel lags.
    /// </summary>
    public static class AimSpring
    {
        public const float OmegaMid = 18f, OmegaLow = 14f, MaxDegPerSec = 240f;
        public static float Omega(QualityTier tier) => tier == QualityTier.Low ? OmegaLow : OmegaMid;

        /// <summary>Advances <paramref name="x"/>/<paramref name="v"/> toward <paramref name="target"/> (exact critically-damped solution, stable for any dt).</summary>
        public static void Step(ref float x, ref float v, float target, float omega, float dt, float maxSpeed = MaxDegPerSec)
        {
            if (dt <= 0f) return;
            float prev = x;
            float d = x - target;
            float e = (float)Math.Exp(-omega * dt);
            float temp = (v + omega * d) * dt;
            v = (v - omega * temp) * e;
            x = target + (d + temp) * e;
            if (maxSpeed > 0f)
            {
                float maxStep = maxSpeed * dt, step = x - prev;
                if (step > maxStep) { x = prev + maxStep; v = maxSpeed; }
                else if (step < -maxStep) { x = prev - maxStep; v = -maxSpeed; }
            }
        }

        /// <summary>Seconds for a unit step (from rest, no speed cap) to reach 95%: ω·t ≈ 4.744, so ~0.26 s at ω = 18. At ω = 18 the 120 ms mark is the ~63% rise (ω·t ≈ 2.15).</summary>
        public static float SettleTime95(float omega) => 4.744f / omega;
    }

    /// <summary>01 Wrecker cannon procedural motion (rigs not ready: code-driven). All functions are pure; ms are state-local.</summary>
    public static class WreckerMotion
    {
        public const float BreathDeg = 0.6f, BreathLoopMs = 2400f, PodBob = 0.01f;
        public const float AnticipationPull = 0.03f, RecoilSlide = 0.25f, RecoilKickDeg = 4f, RecoverOvershootMax = 0.02f, RecoverBackS = 1.4f;
        public const float ReadyMs = 300f, ReadyDipDeg = -1.5f, ReadyGlowPeak = 0.4f;
        public const float SwapOutMs = 150f, SwapInMs = 300f, SwapPeak = 1.15f;

        /// <summary>Idle breathing pitch offset (±0.6°, 2400 ms sine). Reduce Motion: amplitude ×0.5 (07).</summary>
        public static float IdleBreathDeg(float ms, bool reduceMotion) =>
            BreathDeg * (reduceMotion ? 0.5f : 1f) * (float)Math.Sin(2.0 * Math.PI * (ms % BreathLoopMs) / BreathLoopMs);

        /// <summary>Module pod bob (+0.01 m, same loop, phase-shifted a quarter so it reads as breathing).</summary>
        public static float PodBobMeters(float ms, bool reduceMotion) =>
            PodBob * (reduceMotion ? 0.5f : 1f) * 0.5f * (1f + (float)Math.Sin(2.0 * Math.PI * (ms % BreathLoopMs) / BreathLoopMs + Math.PI * 0.5));

        /// <summary>
        /// Barrel slide BACK along its axis in metres (positive = toward the breech). Anticipation pulls 0.03 (ease-in quad, 66 ms);
        /// Recoil slides to 0.25 (ease-out expo, 200 ms); Recover returns with ease-out-back 1.4, overshoot clamped to 0.02 m.
        /// Reduce Motion keeps the recoil (readability, 07) but removes the overshoot.
        /// </summary>
        public static float SlideBack(WreckerState s, float ms, bool reduceMotion)
        {
            switch (s)
            {
                case WreckerState.FireAnticipation: return AnticipationPull * Ease.InQuad(ms / WreckerAnimStateMachine.AnticipationMs);
                case WreckerState.Recoil:
                    return AnticipationPull + (RecoilSlide - AnticipationPull) * Ease.OutExpo(ms / WreckerAnimStateMachine.RecoilMs);
                case WreckerState.Recover:
                {
                    float k = Ease.OutBackOrQuad(ms / WreckerAnimStateMachine.RecoverMs, reduceMotion, RecoverBackS);
                    float v = RecoilSlide * (1f - k);
                    return v < -RecoverOvershootMax ? -RecoverOvershootMax : v;
                }
                default: return 0f;
            }
        }

        /// <summary>Pitch kick (+4° up) on recoil, back to 0 on recover. Caller clamps to the 70° max elevation.</summary>
        public static float KickDeg(WreckerState s, float ms, bool reduceMotion)
        {
            if (s == WreckerState.Recoil) return RecoilKickDeg * Ease.OutExpo(ms / WreckerAnimStateMachine.RecoilMs);
            if (s == WreckerState.Recover)
            {
                float v = RecoilKickDeg * (1f - Ease.OutBackOrQuad(ms / WreckerAnimStateMachine.RecoverMs, reduceMotion, RecoverBackS));
                return v < 0f ? 0f : v;
            }
            return 0f;
        }

        /// <summary>Ready (ANIM_WRECKER_READY): barrel dip −1.5° → 0 with ease-out-back over 300 ms.</summary>
        public static float ReadyDipDegAt(float ms, bool reduceMotion)
        {
            if (ms < 0f || ms >= ReadyMs) return 0f;
            return ReadyDipDeg * (1f - Ease.OutBackOrQuad(ms / ReadyMs, reduceMotion));
        }

        /// <summary>Ready Core glow multiplier 1.0 → 1.4 → 1.0 (half-sine, 300 ms), scaled by MotionSettings.FlashScale under Reduce Motion.</summary>
        public static float ReadyGlow(float ms, float flashScale)
        {
            if (ms < 0f || ms >= ReadyMs) return 1f;
            return 1f + ReadyGlowPeak * flashScale * (float)Math.Sin(Math.PI * ms / ReadyMs);
        }

        /// <summary>
        /// Slot swap scale of the slot part (Module pod on the barrel top): 0–150 ms old part 1 → 0 (ease-in back), 150–450 ms new part
        /// 0 → 1.15 → 1 (ease-out-back). Reduce Motion: no anticipation dip and no overshoot. Never negative.
        /// </summary>
        public static float SlotSwapScale(float ms, bool reduceMotion)
        {
            if (ms <= 0f) return 1f;
            if (ms < SwapOutMs)
            {
                float t = ms / SwapOutMs;
                float k = reduceMotion ? Ease.InQuad(t) : Ease.InBack(t);
                float v = 1f - k;
                return v < 0f ? 0f : v;
            }
            float u = (ms - SwapOutMs) / SwapInMs;
            if (u >= 1f) return 1f;
            if (reduceMotion) return Ease.OutQuad(u);
            // peak 1.15 at ~2/3 then settle (two-segment so the peak is exact, not an out-back constant guess)
            const float split = 0.66f;
            return u < split ? SwapPeak * Ease.OutQuad(u / split) : SwapPeak + (1f - SwapPeak) * Ease.InOutSine((u - split) / (1f - split));
        }

        /// <summary>True once the swap has passed the 150 ms midpoint (swap the part's look here; ANIM_WRECKER_SLOT_IN fires at 150 ms).</summary>
        public static bool SlotSwapPastMid(float ms) => ms >= SwapOutMs;
    }
}
