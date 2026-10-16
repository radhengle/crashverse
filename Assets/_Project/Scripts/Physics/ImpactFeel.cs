using System;
using Crashverse.Feel;
using UnityEngine;

namespace Crashverse.Sim
{
    /// <summary>AnimEventBus names for ImpactEvent payloads. Additive: existing AnimEvents (COLLAPSE_LAND, DEBRIS_FALL_OFF, ...) are unchanged.</summary>
    public static class ImpactEvents
    {
        public const string Impact = "PHYS_IMPACT", Crack = "PHYS_CRACK", Break = "PHYS_BREAK", JointSnap = "PHYS_JOINT_SNAP",
                            CollapseStart = "PHYS_COLLAPSE_START", CollapseEnd = "PHYS_COLLAPSE_END";
        public static string NameOf(ImpactKind k) => k switch
        {
            ImpactKind.Impact => Impact, ImpactKind.Crack => Crack, ImpactKind.Break => Break,
            ImpactKind.JointSnap => JointSnap, ImpactKind.CollapseStart => CollapseStart, _ => CollapseEnd
        };
        public static Vector3 ToVector3(this SimVec3 v) => new Vector3(v.x, v.y, v.z);
        public static SimVec3 ToSim(this Vector3 v) => new SimVec3(v.x, v.y, v.z);
    }

    /// <summary>Owned by the Sound Manager. Tier thresholds (single source for tier classification) and impulse→volume/pitch curves.</summary>
    [CreateAssetMenu(menuName = "Crashverse/Impact Audio Profile", fileName = "ImpactAudioProfile")]
    public class ImpactAudioProfile : ScriptableObject
    {
        [Header("Tier thresholds (N·s) and size classes (kg). Starting tunables.")]
        public ImpactTierThresholds thresholds = ImpactTierThresholds.Default;
        [Header("x = impulse N·s (0..300), y = volume 0..1")]
        public AnimationCurve impulseToVolume = AnimationCurve.EaseInOut(5f, 0.06f, 300f, 1f);
        [Header("x = impulse N·s, y = pitch multiplier")]
        public AnimationCurve impulseToPitch = AnimationCurve.Linear(0f, 1.05f, 300f, 0.85f);
        [Range(0f, 0.3f)] public float pitchJitter = 0.08f;
        [Header("Voice limits per tier Low/Mid/High; debris rattle aggregation")]
        public int voicesLow = 12, voicesMid = 24, voicesHigh = 32;
        public int rattleWindowMs = 100, rattleMaxVoices = 3;

        public float Volume(float impulse) => Mathf.Clamp01(impulseToVolume.Evaluate(impulse));
        public float Pitch(float impulse) => impulseToPitch.Evaluate(impulse);
        public int VoiceLimit(QualityTier t) => t == QualityTier.Low ? voicesLow : t == QualityTier.Mid ? voicesMid : voicesHigh;
    }

    /// <summary>Presentation-only feel per tier: hit-stop, camera shake, dust. Nothing here is read by the sim.</summary>
    [CreateAssetMenu(menuName = "Crashverse/Impact Feel Profile", fileName = "ImpactFeelProfile")]
    public class ImpactFeelProfile : ScriptableObject
    {
        [Serializable] public struct Shake { public float amplitude, frequency, duration; }
        public int hitStopLightMs = 0, hitStopMediumMs = 40, hitStopHeavyMs = 80, hitStopMaxMs = 80;
        public float hitStopCooldownSec = 0.5f;
        public Shake shakeLight = new Shake(), shakeMedium = new Shake { amplitude = 0.08f, frequency = 22f, duration = 0.15f },
                     shakeHeavy = new Shake { amplitude = 0.2f, frequency = 18f, duration = 0.3f };
        public int dustLight = 4, dustMedium = 12, dustHeavy = 30, dustHeavyLow = 10;

        public int HitStopMs(ImpactTier t, bool reduceMotion) => HitStopRule.Millis(t, hitStopLightMs, hitStopMediumMs, hitStopHeavyMs, hitStopMaxMs, reduceMotion);
        public Shake ShakeFor(ImpactTier t) => t == ImpactTier.Heavy ? shakeHeavy : t == ImpactTier.Medium ? shakeMedium : shakeLight;
        public int DustFor(ImpactTier t, QualityTier q) => t == ImpactTier.Heavy ? (q == QualityTier.Low ? dustHeavyLow : dustHeavy) : t == ImpactTier.Medium ? dustMedium : dustLight;
    }

    /// <summary>
    /// Sim-side collector. Destruction code calls Report*(...) during the tick (including collision callbacks inside
    /// Physics.Simulate); on SimRunner.Stepped the buffer is flushed in ImpactEventOrder and raised on AnimEventBus.
    /// </summary>
    public class ImpactEventEmitter : MonoBehaviour
    {
        public static ImpactEventEmitter Instance { get; private set; }
        public ImpactAudioProfile audioProfile;
        readonly ImpactEventBuffer _buffer = new ImpactEventBuffer();
        public readonly CascadeTracker Cascades = new CascadeTracker();
        SimRunner _runner;
        static readonly Action<ImpactEvent> Emit = e => AnimEventBus.Raise(ImpactEvents.NameOf(e.kind), e);

        public ImpactTierThresholds Thresholds => audioProfile != null ? audioProfile.thresholds : ImpactTierThresholds.Default;
        int CurrentTick => _runner != null ? ImpactTickPolicy.Tag(_runner.Tick, _runner.InTick) : 0;
        bool Ranked => _runner != null && _runner.Ranked;

        void OnEnable()
        {
            Instance = this;
            _runner = SimRunner.Instance != null ? SimRunner.Instance : FindObjectOfType<SimRunner>();
            if (_runner != null) { _runner.Stepped += OnStepped; _runner.TickBegin += OnTickBegin; }
        }
        void OnDisable() { if (_runner != null) { _runner.Stepped -= OnStepped; _runner.TickBegin -= OnTickBegin; } if (Instance == this) Instance = null; }

        /// <summary>Call from SimRunner.BeginRun / Retry so cascade ids replay identically.</summary>
        public void ResetRun() { _buffer.Clear(); Cascades.Reset(); }

        // See ImpactTickPolicy: ranked flushes after its own Physics.Simulate; non-ranked flushes the previous step at the next tick start.
        void OnStepped(int tick) { if (ImpactTickPolicy.FlushOnStepped(Ranked)) _buffer.Flush(Emit); }
        void OnTickBegin(int tick) { if (ImpactTickPolicy.FlushOnTickBegin(Ranked)) _buffer.Flush(Emit); }

        public void Report(ImpactKind kind, float impulse, float massKg, int pieceId, int matA, int matB, Vector3 pos, Vector3 normal,
                           ImpactSourceKind source, int sourceId = 0, int damageStep = 0, int cascadeId = 0,
                           MaterialType typeA = MaterialType.Terrain, MaterialType typeB = MaterialType.Terrain)
        {
            var e = ImpactEvent.Make(CurrentTick, kind, impulse, massKg, Thresholds);
            e.pieceId = pieceId; e.materialA = matA; e.materialB = matB; e.position = pos.ToSim(); e.normal = normal.ToSim();
            e.materialTypeA = typeA; e.materialTypeB = typeB;
            e.source = source; e.sourceId = sourceId; e.damageStep = damageStep; e.cascadeId = cascadeId;
            if (kind == ImpactKind.Break && cascadeId != 0) e.cascadeCount = Cascades.RecordBreak(cascadeId, typeA);
            _buffer.Add(e);
        }

        public int BeginCollapse(Vector3 pos, int plannedPieces, MaterialCounts planned = default)
        {
            int id = Cascades.Begin();
            var e = ImpactEvent.Make(CurrentTick, ImpactKind.CollapseStart, 0f, 0f, Thresholds);
            e.cascadeId = id; e.cascadeCount = plannedPieces; e.position = pos.ToSim();
            e.materialCounts = planned; e.dominantMaterial = planned.Dominant; _buffer.Add(e);
            return id;
        }

        public void EndCollapse(int cascadeId, Vector3 pos)
        {
            var e = ImpactEvent.Make(CurrentTick, ImpactKind.CollapseEnd, 0f, 0f, Thresholds);
            e.cascadeId = cascadeId; e.cascadeCount = Cascades.End(cascadeId, out var counts); e.position = pos.ToSim();
            e.materialCounts = counts; e.dominantMaterial = counts.Dominant; _buffer.Add(e);
        }
    }

    /// <summary>
    /// Presentation-only hit-stop. Freezes presentation (VFX/anim interpolation readers check IsFrozen) using unscaled real time.
    /// It NEVER touches Time.timeScale, Time.fixedDeltaTime, SimRunner or physics, in any mode.
    /// </summary>
    public class HitStopPresenter : MonoBehaviour
    {
        public ImpactFeelProfile profile;
        float _frozenUntil, _cooldownUntil;
        public bool IsFrozen => Time.unscaledTime < _frozenUntil;

        void OnEnable() => AnimEventBus.Raised += OnRaised;
        void OnDisable() => AnimEventBus.Raised -= OnRaised;

        void OnRaised(string name, object payload)
        {
            if (profile == null || !(payload is ImpactEvent e)) return;
            if (e.kind != ImpactKind.Impact && e.kind != ImpactKind.Break) return;
            float now = Time.unscaledTime;
            if (now < _cooldownUntil) return;
            int ms = profile.HitStopMs(e.tier, MotionSettings.Current.ReduceMotion);
            if (ms <= 0) return;
            _frozenUntil = Mathf.Max(_frozenUntil, now + ms / 1000f);
            _cooldownUntil = now + profile.hitStopCooldownSec;
        }
    }
}
