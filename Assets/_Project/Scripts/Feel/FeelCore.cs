using System;
using System.Collections.Generic;

namespace Crashverse.Feel
{
    public enum QualityTier { Low = 0, Mid = 1, High = 2 }

    /// <summary>Easing curves (Animation Spec v1). t in [0,1].</summary>
    public static class Ease
    {
        public static float Linear(float t) => Clamp01(t);
        public static float InQuad(float t) { t = Clamp01(t); return t * t; }
        public static float OutQuad(float t) { t = Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float InOutCubic(float t) { t = Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f; }
        public static float OutCubic(float t) { t = Clamp01(t); return 1f - (float)Math.Pow(1f - t, 3); }
        public static float OutExpo(float t) { t = Clamp01(t); return t >= 1f ? 1f : 1f - (float)Math.Pow(2, -10 * t); }
        public static float InExpo(float t) { t = Clamp01(t); return t <= 0f ? 0f : (float)Math.Pow(2, 10 * t - 10); }
        public static float InOutSine(float t) { t = Clamp01(t); return -(float)(Math.Cos(Math.PI * t) - 1) / 2f; }
        /// <summary>Overshooting ease (out-back). s = 1.70158 by default; spec uses 1.2/1.4 variants.</summary>
        public static float OutBack(float t, float s = 1.70158f) { t = Clamp01(t); float c3 = s + 1f; return 1f + c3 * (float)Math.Pow(t - 1, 3) + s * (float)Math.Pow(t - 1, 2); }
        public static float InBack(float t, float s = 1.70158f) { t = Clamp01(t); return (s + 1f) * t * t * t - s * t * t; }
        /// <summary>Reduce Motion: overshoots removed (out-back → out-quad).</summary>
        public static float OutBackOrQuad(float t, bool reduceMotion, float s = 1.70158f) => reduceMotion ? OutQuad(t) : OutBack(t, s);
        static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }

    /// <summary>Event names from animation/audio_sync_table.md + anim_audio_map.csv (ANIM_WRECKER_READY = Content Roadmap §5 reload-beat hook).</summary>
    public static class AnimEvents
    {
        public const string WreckerIdleFidget = "ANIM_WRECKER_IDLE_FIDGET", WreckerAimStart = "ANIM_WRECKER_AIM_START", WreckerAimEnd = "ANIM_WRECKER_AIM_END",
            WreckerAimClamp = "ANIM_WRECKER_AIM_CLAMP", WreckerFireAnticipate = "ANIM_WRECKER_FIRE_ANTICIPATE", WreckerFire = "ANIM_WRECKER_FIRE",
            WreckerMuzzleSmoke = "ANIM_WRECKER_MUZZLE_SMOKE", WreckerRecoilLand = "ANIM_WRECKER_RECOIL_LAND", WreckerReArm = "ANIM_WRECKER_REARM",
            WreckerSlotOut = "ANIM_WRECKER_SLOT_OUT", WreckerSlotIn = "ANIM_WRECKER_SLOT_IN", WreckerWin = "ANIM_WRECKER_WIN", WreckerFail = "ANIM_WRECKER_FAIL",
            WreckerReady = "ANIM_WRECKER_READY", ReloadStart = "GAME_WRECKER_RELOAD_START", // READY name per anim_audio_map.csv
            ProjLaunch = "ANIM_PROJ_LAUNCH", ProjSplitPoint = "ANIM_PROJ_SPLIT_POINT", PuSplit = "PU_SPLIT", PuPhasePass = "PU_PHASE_PASS", PuPhaseHit = "PU_PHASE_HIT",
            PuTimeStart = "PU_TIME_START", PuTimeWarn = "PU_TIME_WARN", PuTimeEnd = "PU_TIME_END",
            CollapseLand = "COLLAPSE_LAND", DebrisFallOff = "DEBRIS_FALL_OFF",
            BossTitanStage = "BOSS_TITAN_STAGE", BossTitanDefeat = "BOSS_TITAN_DEFEAT", BossTitanHit = "BOSS_TITAN_HIT",
            BossTideTell = "BOSS_TIDE_TELL", BossTideShutterClose = "BOSS_TIDE_SHUTTER_CLOSE", BossTideShutterOpen = "BOSS_TIDE_SHUTTER_OPEN", BossTidePhase2 = "BOSS_TIDE_PHASE2", BossTideCoreHit = "BOSS_TIDE_CORE_HIT", BossTideDefeat = "BOSS_TIDE_DEFEAT",
            UiBtnPress = "UI_BTN_PRESS", UiComboUp = "UI_COMBO_UP", UiComboTier = "UI_COMBO_TIER", UiStar = "UI_STAR_", UiMasteryTick = "UI_MASTERY_TICK",
            UiWin = "UI_WIN", UiShotSpend = "UI_SHOT_SPEND", UiThresholdHit = "UI_THRESHOLD_HIT", CamSlowMoStart = "CAM_SLOWMO_START";
    }

    /// <summary>Animation Spec 02 projectile Animator `Material` int. 0–5 unchanged; Glass appended as 6. (Physics MaterialType #18 is a separate list where glass = 3.)</summary>
    public static class AnimMaterial
    {
        public const int Wood = 0, Brick = 1, Steel = 2, Joint = 3, Terrain = 4, Explosive = 5, Glass = 6;
        public const string ImpactGlass = "IMPACT_GLASS", BreakGlass = "BREAK_GLASS";
    }

    /// <summary>Global VFX/SFX event bus. Never drives gameplay (Animation Spec global rules).</summary>
    public static class AnimEventBus
    {
        public static event Action<string, object> Raised;
        public static int Count { get; private set; }
        public static void Raise(string name, object payload = null) { Count++; Raised?.Invoke(name, payload); }
        public static void ResetForTests() { Raised = null; Count = 0; }
    }

    // ───────────── Settings ─────────────
    public interface IMotionPrefs { bool TryGet(string key, out bool v); void Set(string key, bool v); }

    /// <summary>
    /// Feel Answers OQ-C2 + Content Roadmap §6. Reduce screen shake and Reduce motion are both OFF by default. Reduce motion switches ON
    /// automatically when the OS has animations removed, but an explicit player choice is persisted and always wins.
    /// </summary>
    public class MotionSettings
    {
        public const string KeyShake = "cv_reduce_shake", KeyMotion = "cv_reduce_motion";
        public static MotionSettings Current = new MotionSettings(null, false);
        readonly IMotionPrefs _prefs; readonly bool _osRemoveAnimations;
        public event Action Changed;

        public MotionSettings(IMotionPrefs prefs, bool osRemoveAnimations) { _prefs = prefs; _osRemoveAnimations = osRemoveAnimations; }

        public bool ReduceShake => _prefs != null && _prefs.TryGet(KeyShake, out var v) && v;
        public bool ReduceMotion => Resolve(_prefs != null && _prefs.TryGet(KeyMotion, out var v) ? v : (bool?)null, _osRemoveAnimations);
        public bool ReduceMotionIsAuto => !(_prefs != null && _prefs.TryGet(KeyMotion, out _)) && _osRemoveAnimations;
        public static bool Resolve(bool? explicitChoice, bool osRemoveAnimations) => explicitChoice ?? osRemoveAnimations;

        public void SetReduceShake(bool on) { _prefs?.Set(KeyShake, on); Changed?.Invoke(); }
        public void SetReduceMotion(bool on) { _prefs?.Set(KeyMotion, on); Changed?.Invoke(); } // explicit: persists, beats auto-on

        /// <summary>Camera shake multiplier: 0 when either setting is on; low tier ×0.7 (07 Perf).</summary>
        public float ShakeScale(QualityTier tier) => ReduceShake || ReduceMotion ? 0f : tier == QualityTier.Low ? 0.7f : 1f;
        /// <summary>07: cosmetic slow-mo never in Titan ranked, never during Time Fracture, off with Reduce motion (OQ-C3).</summary>
        public bool SlowMoAllowed(bool titanRanked, bool timeFractureActive) => !ReduceMotion && !titanRanked && !timeFractureActive;
        public float FlashScale => ReduceMotion ? 0.4f : 1f;
        public float MaxPopScale => ReduceMotion ? 1.1f : 1.3f;
        public bool FidgetEnabled(QualityTier tier) => !ReduceMotion && tier != QualityTier.Low;
        public bool FollowShotEnabled => !ReduceMotion;
    }

    // ───────────── Reload gate (Content Roadmap §5) ─────────────
    /// <summary>FIRE unlocks only when physics has settled AND ≥ 1.0 s has passed since the shot. Aim is never locked.</summary>
    public class ReloadGate
    {
        public const float MinReloadSeconds = 1.0f;
        public bool Ready { get; private set; } = true;
        public float SinceShot { get; private set; }
        public event Action BecameReady;

        public void OnShotFired() { Ready = false; SinceShot = 0f; AnimEventBus.Raise(AnimEvents.ReloadStart); }
        public void Reset() { Ready = true; SinceShot = 0f; }

        /// <param name="dt">Scaled sim time (settle uses scaled time, 07).</param>
        public bool Tick(float dt, bool physicsSettled)
        {
            if (Ready) return true;
            SinceShot += dt;
            if (physicsSettled && SinceShot >= MinReloadSeconds)
            {
                Ready = true;
                AnimEventBus.Raise(AnimEvents.WreckerReady);
                BecameReady?.Invoke();
            }
            return Ready;
        }
    }

    // ───────────── Debris budget (OQ-D1/D2) ─────────────
    public static class DebrisBudget
    {
        public static int Cap(QualityTier t) => t == QualityTier.Low ? 25 : t == QualityTier.High ? 90 : 60;
        /// <summary>Chunks per broken piece. Low tier halves wood 6→3, brick 4→2 (visual only; mass counts on the source piece).</summary>
        public static int Chunks(string material, QualityTier t)
        {
            int n = material == "wood" ? 6 : material == "brick" ? 4 : 0;
            return t == QualityTier.Low ? n / 2 : n;
        }
    }

    /// <summary>Tracks live debris rigidbodies; at cap, recycles the oldest SETTLED chunk first, else the oldest.</summary>
    public class DebrisLedger<T> where T : class
    {
        class Entry { public T item; public long order; public bool settled; }
        readonly List<Entry> _live = new List<Entry>(); long _seq;
        public int Cap { get; set; }
        public int Count => _live.Count;
        public DebrisLedger(int cap) { Cap = cap; }

        /// <summary>Registers a new chunk; returns the chunk that must be recycled to stay within cap (or null).</summary>
        public T Add(T item)
        {
            T victim = null;
            if (_live.Count >= Cap && Cap > 0) victim = PickVictim();
            if (victim != null) Remove(victim);
            _live.Add(new Entry { item = item, order = _seq++ });
            return victim;
        }

        public void MarkSettled(T item, bool settled = true) { var e = _live.Find(x => x.item == item); if (e != null) e.settled = settled; }
        public void Remove(T item) => _live.RemoveAll(x => x.item == item);
        public void Clear() => _live.Clear();

        public T PickVictim()
        {
            Entry best = null;
            foreach (var e in _live) if (e.settled && (best == null || e.order < best.order)) best = e;
            if (best == null) foreach (var e in _live) if (best == null || e.order < best.order) best = e;
            return best?.item;
        }
    }

    // ───────────── Splitstrike (OQ-P2) ─────────────
    public static class SplitPlanner
    {
        /// <summary>Seconds after launch at which to split: min(apex, 70% of predicted flight time). Apex = vy/g (0 if not rising).</summary>
        public static float SplitTime(float launchVy, float gravityMagnitude, float predictedFlightTime)
        {
            float t = float.PositiveInfinity;
            if (launchVy > 0f && gravityMagnitude > 0f) t = launchVy / gravityMagnitude;
            if (predictedFlightTime > 0f) t = Math.Min(t, 0.7f * predictedFlightTime);
            return float.IsInfinity(t) ? 0f : t;
        }
    }

    // ───────────── Time Fracture (OQ-P3) ─────────────
    /// <summary>Starts on arm (charge spent then, cannot be un-armed), 4 s real time at 25%; if a shot is fired during the slow it holds until that shot's impact.</summary>
    public class TimeFractureTimer
    {
        public bool Active { get; private set; }
        public float Remaining { get; private set; }
        public float SpeedScale => Active ? _slow : 1f;
        bool _holdForImpact, _warned; float _slow = 0.25f;
        public event Action Ended;

        public void Arm(float durationSeconds = 4f, float slow = 0.25f)
        {
            Active = true; Remaining = durationSeconds; _slow = slow; _holdForImpact = false; _warned = false;
            AnimEventBus.Raise(AnimEvents.PuTimeStart);
        }
        public void OnShotFired() { if (Active) _holdForImpact = true; }
        public void OnShotImpact() { if (!Active || !_holdForImpact) return; _holdForImpact = false; if (Remaining <= 0f) End(); }
        public void Cancel() { if (Active) End(); }

        /// <param name="realDt">Unscaled seconds.</param>
        public void Tick(float realDt)
        {
            if (!Active) return;
            Remaining -= realDt;
            if (!_warned && Remaining <= 0.5f) { _warned = true; AnimEventBus.Raise(AnimEvents.PuTimeWarn); }
            if (Remaining <= 0f && !_holdForImpact) End();
        }
        void End() { Active = false; Remaining = 0f; _holdForImpact = false; AnimEventBus.Raise(AnimEvents.PuTimeEnd); Ended?.Invoke(); }
    }

    // ───────────── Tidewarden shutter (05_bosses) ─────────────
    /// <summary>Shutter tell (exactly 2 Hz over the last 1 s before close) and open/close tweens (close 300 ms ease-in cubic, open 350 ms ease-out-back).</summary>
    public static class TideShutter
    {
        public const float TellSeconds = 1f, CloseMs = 300f, OpenMs = 350f, Travel = 1.7f;
        /// <summary>0.6 + 0.4·(0.5 + 0.5·sin(4π·t)). Reduce Motion keeps the tell as colour with amplitude ×0.5 (07).</summary>
        public static float TellIntensity(float tSinceStart, bool reduceMotion)
        {
            float p = 0.5f + 0.5f * (float)Math.Sin(4.0 * Math.PI * tSinceStart);
            return 0.6f + 0.4f * (reduceMotion ? 0.5f : 1f) * p;
        }
        /// <summary>Height above the closed pose for a tween: closing goes Travel→0 with ease-in cubic; opening 0→Travel with ease-out-back (quad under Reduce Motion).</summary>
        public static float Height(bool opening, float elapsedMs, bool reduceMotion)
        {
            if (opening) return Travel * Ease.OutBackOrQuad(elapsedMs / OpenMs, reduceMotion);
            float t = Math.Min(1f, Math.Max(0f, elapsedMs / CloseMs));
            return Travel * (1f - t * t * t);
        }
        public static bool Done(bool opening, float elapsedMs) => elapsedMs >= (opening ? OpenMs : CloseMs);
    }

    // ───────────── Titan stages (OQ-B3) ─────────────
    public static class TitanStages
    {
        public static readonly int[] StagePct = { 25, 50, 75 };
        /// <summary>0..3 stages passed (integer maths: damage×100 ≥ pct×max); defeat is at 100%.</summary>
        public static int StageFor(int damage, int maxDamage)
        {
            int s = 0;
            foreach (var p in StagePct) if ((long)damage * 100 >= (long)p * maxDamage) s++;
            return s;
        }
    }

    // ───────────── Camera shake (07) ─────────────
    public struct ShakeSpec { public float pos, rot, freq, durationMs; public ShakeSpec(float p, float r, float f, float d) { pos = p; rot = r; freq = f; durationMs = d; } }

    public static class ShakeTable
    {
        public static readonly ShakeSpec Fire = new ShakeSpec(0.06f, 0.6f, 9, 180), ImpactWood = new ShakeSpec(0.02f, 0.2f, 14, 100),
            BrickBreak = new ShakeSpec(0.04f, 0.4f, 12, 150), JointBreak = new ShakeSpec(0.06f, 0.6f, 12, 150),
            CollapseLand = new ShakeSpec(0.08f, 0.8f, 8, 250), Explosion = new ShakeSpec(0.10f, 1.0f, 10, 300),
            GravityBurst = new ShakeSpec(0.08f, 0.8f, 10, 250), BossPhase = new ShakeSpec(0.12f, 1.2f, 6, 600);
        public const float MaxPos = 0.15f, MaxRot = 1.5f;
    }

    /// <summary>Additive shakes with ease-out decay; summed amplitude capped at 0.15 m / 1.5°; scaled by MotionSettings (×0 when reduced).</summary>
    public class ShakeStack
    {
        class Live { public ShakeSpec s; public float t; }
        readonly List<Live> _live = new List<Live>();
        public float Scale = 1f;
        public void Add(ShakeSpec s) { if (Scale <= 0f) return; _live.Add(new Live { s = s }); }
        public void Clear() => _live.Clear();
        public int Count => _live.Count;

        /// <summary>Advance (unscaled) and return the current (pos, rot) amplitude envelope after caps.</summary>
        public (float pos, float rot) Tick(float realDt)
        {
            float p = 0f, r = 0f;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var l = _live[i]; l.t += realDt * 1000f;
                if (l.t >= l.s.durationMs) { _live.RemoveAt(i); continue; }
                float k = 1f - Ease.OutQuad(l.t / l.s.durationMs);
                p += l.s.pos * k; r += l.s.rot * k;
            }
            return (Math.Min(ShakeTable.MaxPos, p * Scale), Math.Min(ShakeTable.MaxRot, r * Scale));
        }
    }

    // ───────────── Hit stop / slow-mo (07) ─────────────
    public class TimeScaleGovernor
    {
        public float TimeScale { get; private set; } = 1f;
        float _hitStopLeftMs, _hitStopScale, _slowLeftMs, _slowTotalMs; bool _slowUsedThisShot;
        float _slowUsedMs;
        public MotionSettings Settings;

        /// <summary>Hit stop ≤ 66 ms at 0.05 (Reduce motion: ≤ 33 ms at 0.3).</summary>
        public void HitStop(float ms)
        {
            bool rm = Settings != null && Settings.ReduceMotion;
            float cap = rm ? 33f : 66f;
            _hitStopLeftMs = Math.Max(_hitStopLeftMs, Math.Min(cap, ms));
            _hitStopScale = rm ? 0.3f : 0.05f;
        }

        /// <summary>Big-collapse slow-mo 0.4 for 600 ms real; max one per shot, ≤ 800 ms total; obeys SlowMoAllowed.</summary>
        public bool TrySlowMo(bool titanRanked, bool timeFractureActive, float ms = 600f)
        {
            if (Settings != null && !Settings.SlowMoAllowed(titanRanked, timeFractureActive)) return false;
            if (Settings == null && (titanRanked || timeFractureActive)) return false;
            if (_slowUsedThisShot || _slowUsedMs + ms > 800f) return false;
            _slowUsedThisShot = true; _slowUsedMs += ms; _slowLeftMs = _slowTotalMs = ms;
            AnimEventBus.Raise(AnimEvents.CamSlowMoStart);
            return true;
        }

        public void OnShotFired() { _slowUsedThisShot = false; _slowUsedMs = 0f; }

        public float Tick(float realDt)
        {
            float ms = realDt * 1000f;
            if (_hitStopLeftMs > 0f) { _hitStopLeftMs -= ms; TimeScale = _hitStopScale; return TimeScale; }
            if (_slowLeftMs > 0f)
            {
                float el = _slowTotalMs - _slowLeftMs; _slowLeftMs -= ms;
                float k = el < 100f ? Ease.OutQuad(el / 100f) : _slowLeftMs < 200f ? Ease.OutQuad(1f - Math.Max(0f, _slowLeftMs) / 200f) * -1f + 1f : 1f;
                TimeScale = 1f - 0.6f * k; // 1 → 0.4 → 1
                return TimeScale;
            }
            TimeScale = 1f; return 1f;
        }
    }

    // ───────────── State machines ─────────────
    /// <summary>Simple timed state machine base: states with durations in ms; Tick advances and calls OnExit for timed states.</summary>
    public abstract class TimedStateMachine<TState> where TState : struct
    {
        public TState State { get; private set; }
        public float ElapsedMs { get; private set; }
        protected void Go(TState s) { State = s; ElapsedMs = 0f; OnEnter(s); }
        protected virtual void OnEnter(TState s) { }
        protected virtual float DurationMs(TState s) => -1f; // -1 = untimed
        protected virtual void OnTimeout(TState s) { }
        public void Tick(float realDt)
        {
            ElapsedMs += realDt * 1000f;
            float d = DurationMs(State);
            if (d >= 0f && ElapsedMs >= d) OnTimeout(State);
            OnTick(realDt);
        }
        protected virtual void OnTick(float dt) { }
    }

    public enum WreckerState { Idle, Fidget, AimEnter, Aim, AimCancel, FireAnticipation, Recoil, Recover, Cooldown, ReArm, SlotSwap, Win, Fail }

    /// <summary>
    /// 01 Wrecker cannon, code-driven (no Charge state: OQ-A1 fixed power). FireAnticipation→Recover is non-interruptible;
    /// Cooldown waits for the reload gate (ready), then ReArm. Aim input during reload is buffered by AimController (never locked).
    /// </summary>
    public class WreckerAnimStateMachine : TimedStateMachine<WreckerState>
    {
        public const float IdleLoopMs = 2400, FidgetMs = 900, AimEnterMs = 100, AimCancelMs = 200, AnticipationMs = 66, RecoilMs = 200,
            RecoverMs = 350, CooldownMinMs = 500, ReArmMs = 250, SlotSwapMs = 450, WinFailMs = 800;
        public const float FireCycleMs = AnticipationMs + 33 + RecoilMs + RecoverMs; // 649 ms (spec)
        public bool Aiming { get; private set; }
        public bool GateReady { get; set; } = true;
        public MotionSettings Settings; public QualityTier Tier = QualityTier.Mid;
        float _nextFidgetMs; readonly Random _rng = new Random(7); bool _smokeSent, _slotInSent;
        public bool Interruptible => State != WreckerState.FireAnticipation && State != WreckerState.Recoil && State != WreckerState.Recover;

        public WreckerAnimStateMachine() { Reset(); }
        public void Reset() { Aiming = false; GateReady = true; ScheduleFidget(); Go(WreckerState.Idle); }
        void ScheduleFidget() => _nextFidgetMs = 6000 + (float)_rng.NextDouble() * 4000;

        protected override float DurationMs(WreckerState s)
        {
            switch (s)
            {
                case WreckerState.Fidget: return FidgetMs; case WreckerState.AimEnter: return AimEnterMs; case WreckerState.AimCancel: return AimCancelMs;
                case WreckerState.FireAnticipation: return AnticipationMs; case WreckerState.Recoil: return RecoilMs; case WreckerState.Recover: return RecoverMs;
                case WreckerState.ReArm: return ReArmMs; case WreckerState.SlotSwap: return SlotSwapMs; default: return -1f;
            }
        }

        protected override void OnEnter(WreckerState s)
        {
            switch (s)
            {
                case WreckerState.Fidget: AnimEventBus.Raise(AnimEvents.WreckerIdleFidget); break;
                case WreckerState.AimEnter: AnimEventBus.Raise(AnimEvents.WreckerAimStart); break;
                case WreckerState.AimCancel: AnimEventBus.Raise(AnimEvents.WreckerAimEnd); break;
                case WreckerState.FireAnticipation: AnimEventBus.Raise(AnimEvents.WreckerFireAnticipate); break;
                case WreckerState.Recoil: _smokeSent = false; AnimEventBus.Raise(AnimEvents.WreckerFire); break;
                case WreckerState.ReArm: AnimEventBus.Raise(AnimEvents.WreckerReArm); break;
                case WreckerState.SlotSwap: _slotInSent = false; AnimEventBus.Raise(AnimEvents.WreckerSlotOut); break;
                case WreckerState.Win: AnimEventBus.Raise(AnimEvents.WreckerWin); break;
                case WreckerState.Fail: AnimEventBus.Raise(AnimEvents.WreckerFail); break;
            }
        }

        protected override void OnTimeout(WreckerState s)
        {
            switch (s)
            {
                case WreckerState.Fidget: ScheduleFidget(); Go(Aiming ? WreckerState.AimEnter : WreckerState.Idle); break;
                case WreckerState.AimEnter: Go(WreckerState.Aim); break;
                case WreckerState.AimCancel: Go(WreckerState.Idle); break;
                case WreckerState.FireAnticipation: Go(WreckerState.Recoil); break;
                case WreckerState.Recoil: AnimEventBus.Raise(AnimEvents.WreckerRecoilLand); Go(WreckerState.Recover); break;
                case WreckerState.Recover: Go(WreckerState.Cooldown); break;
                case WreckerState.ReArm: Go(Aiming ? WreckerState.Aim : WreckerState.Idle); break;
                case WreckerState.SlotSwap: if (!_slotInSent) { _slotInSent = true; AnimEventBus.Raise(AnimEvents.WreckerSlotIn); } Go(Aiming ? WreckerState.Aim : WreckerState.Idle); break;
            }
        }

        protected override void OnTick(float dt)
        {
            if (State == WreckerState.Recoil && !_smokeSent && ElapsedMs >= 33f) { _smokeSent = true; AnimEventBus.Raise(AnimEvents.WreckerMuzzleSmoke); }
            if (State == WreckerState.SlotSwap && !_slotInSent && ElapsedMs >= WreckerMotion.SwapOutMs) { _slotInSent = true; AnimEventBus.Raise(AnimEvents.WreckerSlotIn); } // 9 f / 150 ms (audio_sync_table)
            if (State == WreckerState.Cooldown && GateReady && ElapsedMs >= CooldownMinMs) Go(WreckerState.ReArm);
            if (State == WreckerState.Idle)
            {
                bool fidgetOn = Settings == null ? Tier != QualityTier.Low : Settings.FidgetEnabled(Tier);
                if (fidgetOn && !Aiming && ElapsedMs >= _nextFidgetMs) Go(WreckerState.Fidget);
            }
        }

        public void SetAiming(bool on)
        {
            if (on == Aiming) return;
            Aiming = on;
            if (on && (State == WreckerState.Idle || State == WreckerState.Fidget || State == WreckerState.AimCancel)) Go(WreckerState.AimEnter);
            else if (!on && (State == WreckerState.Aim || State == WreckerState.AimEnter)) Go(WreckerState.AimCancel); // releasing never fires (OQ-A2)
        }

        /// <summary>FIRE button. Returns false if the state machine is mid-cycle (gameplay gate is the authority; this mirrors it).</summary>
        public bool Fire()
        {
            if (!Interruptible || State == WreckerState.Cooldown || State == WreckerState.ReArm || State == WreckerState.Win || State == WreckerState.Fail) return false;
            GateReady = false;
            Go(WreckerState.FireAnticipation);
            return true;
        }

        public void SlotSwap() { if (State == WreckerState.Idle || State == WreckerState.Aim) Go(WreckerState.SlotSwap); }
        public void Win() { if (State == WreckerState.Cooldown || Interruptible) Go(WreckerState.Win); }
        public void Fail() { if (State == WreckerState.Cooldown || Interruptible) Go(WreckerState.Fail); }
    }

    public enum CollapseState { Standing, Breaking, Chaining, Settling, Settled, DebrisFade }

    /// <summary>03 CollapseDirector: feel only, never decides what breaks; settle never waits for debris fade.</summary>
    public class CollapseDirector : TimedStateMachine<CollapseState>
    {
        public const float ChainWindowMs = 800, RestMs = 1500, FadeMs = 500;
        float _sinceBreakMs;
        public void PieceBroken()
        {
            _sinceBreakMs = 0f;
            var s = State;
            if (s == CollapseState.Breaking || s == CollapseState.Chaining) { Go(CollapseState.Chaining); return; }
            Go(CollapseState.Breaking);
        }
        public void PhysicsSettled() { if (State == CollapseState.Settling || State == CollapseState.Breaking || State == CollapseState.Chaining) Go(CollapseState.Settled); }
        public void ShotFired() { Go(CollapseState.Standing); }
        protected override float DurationMs(CollapseState s) => s == CollapseState.Settled ? RestMs : s == CollapseState.DebrisFade ? FadeMs : -1f;
        protected override void OnTimeout(CollapseState s) { if (s == CollapseState.Settled) Go(CollapseState.DebrisFade); }
        protected override void OnTick(float dt)
        {
            _sinceBreakMs += dt * 1000f;
            if ((State == CollapseState.Breaking || State == CollapseState.Chaining) && _sinceBreakMs >= ChainWindowMs) Go(CollapseState.Settling);
        }
    }

    public enum ComboUiState { Hidden, Show, Hold, Punch, TierUp, Out }

    /// <summary>06 COMBO xN: from x3, punch on increment, tier-up at 5/10/20, out after 0.8 s without a break or on a new shot.</summary>
    public class ComboUiStateMachine : TimedStateMachine<ComboUiState>
    {
        public const float InMs = 180, OutMs = 250, PunchMs = 80, TierUpMs = 250, HoldWindowMs = 800;
        public int N { get; private set; }
        float _sinceBreakMs;
        public static float PeakScale(int n, bool reduceMotion)
        {
            float p = n >= 20 ? 1.7f : n >= 10 ? 1.5f : n >= 5 ? 1.35f : 1.2f;
            return reduceMotion ? Math.Min(p, 1.1f) : p;
        }
        public void OnCombo(int n)
        {
            _sinceBreakMs = 0f;
            int prev = N; N = n;
            if (n < 3) return;
            if (State == ComboUiState.Hidden || State == ComboUiState.Out) { Go(ComboUiState.Show); AnimEventBus.Raise(AnimEvents.UiComboUp, n); return; }
            AnimEventBus.Raise(AnimEvents.UiComboUp, n);
            if ((n == 5 || n == 10 || n == 20) && n != prev) { AnimEventBus.Raise(AnimEvents.UiComboTier, n); Go(ComboUiState.TierUp); }
            else Go(ComboUiState.Punch);
        }
        public void OnShot() { if (State != ComboUiState.Hidden) Go(ComboUiState.Out); N = 0; }
        protected override float DurationMs(ComboUiState s) => s == ComboUiState.Show ? InMs : s == ComboUiState.Punch ? PunchMs : s == ComboUiState.TierUp ? TierUpMs : s == ComboUiState.Out ? OutMs : -1f;
        protected override void OnTimeout(ComboUiState s) { if (s == ComboUiState.Out) Go(ComboUiState.Hidden); else Go(ComboUiState.Hold); }
        protected override void OnTick(float dt)
        {
            _sinceBreakMs += dt * 1000f;
            if (State == ComboUiState.Hold && _sinceBreakMs >= HoldWindowMs) Go(ComboUiState.Out);
        }
    }

    public enum TitanAnimState { Intro, Idle, Hit, Stage, Defeat, Survive }

    /// <summary>05 Emberforge Titan: idle/flinch/stage-break/defeat only; never attacks (OQ-B3). Stages at 25/50/75% of max damage.</summary>
    public class TitanAnimStateMachine : TimedStateMachine<TitanAnimState>
    {
        public const float IntroMs = 3000, HitMs = 250, StageMs = 800, DefeatMs = 4500, SurviveMs = 1500;
        public int StagesShown { get; private set; }
        readonly int _max;
        public TitanAnimStateMachine(int maxDamage) { _max = maxDamage; Go(TitanAnimState.Intro); }
        public void OnDamage(int totalDamage)
        {
            if (State == TitanAnimState.Defeat) return;
            int st = TitanStages.StageFor(totalDamage, _max);
            if (st > StagesShown) { StagesShown = st; AnimEventBus.Raise(AnimEvents.BossTitanStage, st); Go(TitanAnimState.Stage); }
            else { AnimEventBus.Raise(AnimEvents.BossTitanHit); if (State == TitanAnimState.Idle) Go(TitanAnimState.Hit); }
        }
        public void Kill() { AnimEventBus.Raise(AnimEvents.BossTitanDefeat); Go(TitanAnimState.Defeat); }
        public void OutOfShots() { if (State != TitanAnimState.Defeat) Go(TitanAnimState.Survive); } // spec 04: no survive event (retired); results flow owns the fail beat
        protected override float DurationMs(TitanAnimState s) => s == TitanAnimState.Intro ? IntroMs : s == TitanAnimState.Hit ? HitMs : s == TitanAnimState.Stage ? StageMs : s == TitanAnimState.Survive ? SurviveMs : -1f;
        protected override void OnTimeout(TitanAnimState s) => Go(TitanAnimState.Idle);
    }

    public enum TideAnimState { Intro, P1Idle, JointHit, JointBreak, PhaseChange, P2Open, Tell, P2Closing, P2Closed, P2Opening, CoreHit, Defeat }

    /// <summary>05 Tidewarden mirrors gameplay (ShutterOpen etc.). Core shows crack steps only (OQ-B1); arms detach as non-objective debris (OQ-B2).</summary>
    public class TidewardenAnimStateMachine : TimedStateMachine<TideAnimState>
    {
        public const float IntroMs = 2500, JointHitMs = 300, JointBreakMs = 600, PhaseChangeMs = 1800, ShutterMs = 350, CoreHitMs = 500, TellMs = 1000;
        public int JointsBroken { get; private set; }
        public int CoreCracks { get; private set; } // 0 intact, 1 cracked, 2 destroyed
        public TidewardenAnimStateMachine() { Go(TideAnimState.Intro); }
        public void JointHit() { if (State == TideAnimState.P1Idle) Go(TideAnimState.JointHit); }
        public void JointBroken() { JointsBroken++; Go(TideAnimState.JointBreak); }
        public void TellStart() { if (State == TideAnimState.P2Open) { AnimEventBus.Raise(AnimEvents.BossTideTell); Go(TideAnimState.Tell); } }
        public void SetShutter(bool open)
        {
            if (!open && (State == TideAnimState.Tell || State == TideAnimState.P2Open)) Go(TideAnimState.P2Closing);
            else if (open && State == TideAnimState.P2Closed) Go(TideAnimState.P2Opening);
        }
        public void CoreHit()
        {
            CoreCracks = Math.Min(2, CoreCracks + 1); AnimEventBus.Raise(AnimEvents.BossTideCoreHit, CoreCracks);
            if (CoreCracks >= 2) { AnimEventBus.Raise(AnimEvents.BossTideDefeat); Go(TideAnimState.Defeat); } else Go(TideAnimState.CoreHit);
        }
        protected override float DurationMs(TideAnimState s)
        {
            switch (s)
            {
                case TideAnimState.Intro: return IntroMs; case TideAnimState.JointHit: return JointHitMs; case TideAnimState.JointBreak: return JointBreakMs;
                case TideAnimState.PhaseChange: return PhaseChangeMs; case TideAnimState.P2Closing: case TideAnimState.P2Opening: return ShutterMs;
                case TideAnimState.CoreHit: return CoreHitMs; default: return -1f;
            }
        }
        protected override void OnTimeout(TideAnimState s)
        {
            switch (s)
            {
                case TideAnimState.Intro: case TideAnimState.JointHit: Go(TideAnimState.P1Idle); break;
                case TideAnimState.JointBreak: Go(JointsBroken >= 2 ? TideAnimState.PhaseChange : TideAnimState.P1Idle); break;
                case TideAnimState.PhaseChange: case TideAnimState.P2Opening: case TideAnimState.CoreHit: Go(TideAnimState.P2Open); break;
                case TideAnimState.P2Closing: Go(TideAnimState.P2Closed); break;
            }
        }
    }

    public enum CamState { AimCam, FollowShot, ImpactFrame, SettleWatch, ResultCam }

    /// <summary>07 camera states. Reduce motion: FollowShot off (camera stays in aim framing), blends ≥ 400 ms.</summary>
    public class CameraStateMachine : TimedStateMachine<CamState>
    {
        public const float FireBlendMs = 250, ImpactHoldMs = 400, SettleBlendMs = 500, FollowWeight = 0.2f;
        public MotionSettings Settings;
        public float BlendMs(float specMs) => Settings != null && Settings.ReduceMotion ? Math.Max(400f, specMs) : specMs;
        public void Fire() { Go(Settings == null || Settings.FollowShotEnabled ? CamState.FollowShot : CamState.SettleWatch); }
        public void FirstImpact() { if (State == CamState.FollowShot) Go(CamState.ImpactFrame); }
        public void Settled(bool ended) { if (State != CamState.AimCam) Go(ended ? CamState.ResultCam : CamState.AimCam); }
        protected override float DurationMs(CamState s) => s == CamState.ImpactFrame ? ImpactHoldMs : -1f;
        protected override void OnTimeout(CamState s) { if (s == CamState.ImpactFrame) Go(CamState.SettleWatch); }
    }

    /// <summary>06 Win sequence schedule (ms from start): ribbon 300 → stars 3×300 → mastery 300 → score 600 → coins 700; tap skips to the end state.</summary>
    public class WinSequence
    {
        public static readonly (string step, float startMs, float durMs)[] Steps =
            { ("Ribbon", 0, 300), ("Star1", 300, 300), ("Star2", 600, 300), ("Star3", 900, 300), ("Mastery", 1200, 300), ("Score", 1500, 600), ("Coins", 2100, 700), ("Buttons", 2800, 0) };
        public float ElapsedMs { get; private set; }
        public bool Done => ElapsedMs >= Steps[Steps.Length - 1].startMs;
        public void Tick(float realDt) => ElapsedMs += realDt * 1000f;
        public void Skip() => ElapsedMs = Steps[Steps.Length - 1].startMs;
        public float Progress(string step) { foreach (var s in Steps) if (s.step == step) return s.durMs <= 0 ? (ElapsedMs >= s.startMs ? 1f : 0f) : Math.Max(0f, Math.Min(1f, (ElapsedMs - s.startMs) / s.durMs)); return 0f; }
        /// <summary>Star pop scale: 0 → peak (150 ms) → 1 (100 ms); peak 1.3 (Reduce motion 1.1).</summary>
        public static float StarScale(float msIntoStar, bool reduceMotion)
        {
            float peak = reduceMotion ? 1.1f : 1.3f;
            if (msIntoStar <= 0f) return 0f;
            if (msIntoStar < 150f) return peak * Ease.OutBackOrQuad(msIntoStar / 150f, reduceMotion);
            if (msIntoStar < 250f) return peak + (1f - peak) * Ease.InOutSine((msIntoStar - 150f) / 100f);
            return 1f;
        }
    }

    /// <summary>06 button: 120 ms press 1→0.92 (out-quad), release →1.05→1 (out-back; Reduce motion: no overshoot).</summary>
    public static class ButtonMotion
    {
        public const float Ms = 120f, Pressed = 0.92f, Overshoot = 1.05f;
        public static float ReleaseScale(float ms, bool reduceMotion)
        {
            float t = Math.Min(1f, ms / Ms);
            if (reduceMotion) return Pressed + (1f - Pressed) * Ease.OutQuad(t);
            return t < 0.5f ? Pressed + (Overshoot - Pressed) * Ease.OutQuad(t * 2f) : Overshoot + (1f - Overshoot) * Ease.OutQuad((t - 0.5f) * 2f);
        }
    }
}
