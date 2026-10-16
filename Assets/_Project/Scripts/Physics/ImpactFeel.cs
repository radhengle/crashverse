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

    public enum ImpactAudioRolloff : byte { Logarithmic = 0, Linear = 1 }

    /// <summary>Owned by the Sound Manager. Tier thresholds (single source for sim tier classification) and all impact audio tunables.
    /// Audio-only: nothing here is read back by the sim except <see cref="thresholds"/> (unchanged behaviour).</summary>
    [CreateAssetMenu(menuName = "Crashverse/Impact Audio Profile", fileName = "ImpactAudioProfile")]
    public class ImpactAudioProfile : ScriptableObject
    {
        // ---------- Existing fields (names/serialization unchanged) ----------
        [Header("Tier thresholds (N·s) and size classes (kg). Starting tunables.")]
        [Tooltip("Global sim-side tier classification (raised on the event). Per-material audio re-tiering is in 'materials'.")]
        public ImpactTierThresholds thresholds = ImpactTierThresholds.Default;
        [Header("x = impulse N·s (0..300), y = volume 0..1")]
        [Tooltip("Legacy/global fallback volume curve, used when a kind curve is missing.")]
        public AnimationCurve impulseToVolume = AnimationCurve.EaseInOut(5f, 0.06f, 300f, 1f);
        [Header("x = impulse N·s, y = pitch multiplier")]
        [Tooltip("Legacy/global fallback pitch curve, used when a kind curve is missing.")]
        public AnimationCurve impulseToPitch = AnimationCurve.Linear(0f, 1.05f, 300f, 0.85f);
        [Tooltip("Legacy jitter (pitch multiplier ±). Superseded by pitchVarianceSemitones; kept for asset compatibility.")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.08f;
        [Header("Voice limits per tier Low/Mid/High; debris rattle aggregation")]
        public int voicesLow = 12, voicesMid = 24, voicesHigh = 32;
        [Tooltip("Legacy ms value; rattleWindowTicks is authoritative (tick-based rate limiting).")]
        public int rattleWindowMs = 100;
        [Tooltip("Max simultaneous debris rattle voices.")]
        public int rattleMaxVoices = 3;

        // ---------- Per-material ----------
        [Serializable]
        public struct MaterialAudio
        {
            [Tooltip("Material family this entry describes.")] public MaterialType material;
            [Tooltip("Audio re-tier: impulse ≥ this is Medium (N·s).")] public float mediumMinImpulse;
            [Tooltip("Audio re-tier: impulse > this is Heavy (N·s).")] public float heavyMinImpulse;
            [Tooltip("Base pitch offset in semitones (glass is brittle/high).")] public float pitchOffsetSemitones;
            [Tooltip("Higher wins a mixed pair. Default steel 4 > brick 3 > glass 2 > wood 1 > terrain 0.")] public int hardness;
            [Tooltip("Max voices for this material on Low/Mid/High device tiers (within voicesLow/Mid/High).")] public int voiceCapLow, voiceCapMid, voiceCapHigh;
            [Tooltip("Sample bank / mixer path for this material.")] public string bank;
            [Tooltip("COLLAPSE_LAND sfx id for this material (empty = no collapse sound).")] public string collapseSfx;
        }

        [Header("Per-material tiers, pitch, hardness, voice caps, banks")]
        public MaterialAudio[] materials =
        {
            new MaterialAudio { material = MaterialType.Wood,  mediumMinImpulse = 30f, heavyMinImpulse = 120f, pitchOffsetSemitones = 0f, hardness = 1, voiceCapLow = 4, voiceCapMid = 8, voiceCapHigh = 10, bank = "SFX/Destruction/Wood",  collapseSfx = "sfx_wood_collapse" },
            new MaterialAudio { material = MaterialType.Brick, mediumMinImpulse = 40f, heavyMinImpulse = 150f, pitchOffsetSemitones = 0f, hardness = 3, voiceCapLow = 4, voiceCapMid = 8, voiceCapHigh = 10, bank = "SFX/Destruction/Brick", collapseSfx = "sfx_brick_collapse" },
            new MaterialAudio { material = MaterialType.Steel, mediumMinImpulse = 60f, heavyMinImpulse = 200f, pitchOffsetSemitones = 0f, hardness = 4, voiceCapLow = 3, voiceCapMid = 6, voiceCapHigh = 8,  bank = "SFX/Destruction/Steel", collapseSfx = "sfx_steel_collapse" },
            new MaterialAudio { material = MaterialType.Glass, mediumMinImpulse = 15f, heavyMinImpulse = 80f,  pitchOffsetSemitones = 3f, hardness = 2, voiceCapLow = 3, voiceCapMid = 6, voiceCapHigh = 8,  bank = "SFX/Destruction/Glass", collapseSfx = "sfx_glass_collapse" },
            new MaterialAudio { material = MaterialType.Terrain, mediumMinImpulse = 40f, heavyMinImpulse = 150f, pitchOffsetSemitones = 0f, hardness = 0, voiceCapLow = 2, voiceCapMid = 3, voiceCapHigh = 4, bank = "SFX/Destruction/Impacts", collapseSfx = "" },
        };

        // ---------- Per-kind curves ----------
        [Serializable]
        public struct KindCurves
        {
            [Tooltip("x = impulse N·s (0..300), y = volume 0..1")] public AnimationCurve volume;
            [Tooltip("x = impulse N·s (0..300), y = pitch multiplier")] public AnimationCurve pitch;
        }
        [Header("Per-kind impulse curves (Impact / Crack / Break / JointSnap)")]
        public KindCurves impactCurves    = new KindCurves { volume = AnimationCurve.EaseInOut(5f, 0.06f, 300f, 1f),  pitch = AnimationCurve.Linear(0f, 1.05f, 300f, 0.85f) };
        public KindCurves crackCurves     = new KindCurves { volume = AnimationCurve.EaseInOut(0f, 0.25f, 300f, 0.8f), pitch = AnimationCurve.Linear(0f, 1.10f, 300f, 0.95f) };
        public KindCurves breakCurves     = new KindCurves { volume = AnimationCurve.EaseInOut(0f, 0.40f, 300f, 1f),  pitch = AnimationCurve.Linear(0f, 1.00f, 300f, 0.85f) };
        public KindCurves jointSnapCurves = new KindCurves { volume = AnimationCurve.EaseInOut(0f, 0.50f, 300f, 1f),  pitch = AnimationCurve.Linear(0f, 1.00f, 300f, 0.90f) };
        [Tooltip("Pitch multiplier added per Crack damageStep above 1 (step 1 = +0, step 3 = +0.06).")]
        [Range(0f, 0.1f)] public float crackStepPitch = 0.03f;

        // ---------- Size & variance ----------
        [Header("Size class pitch / volume")]
        [Tooltip("Semitones for S / M / L pieces.")] public float sizePitchS = 2f, sizePitchM = 0f, sizePitchL = -3f;
        [Tooltip("Volume multiplier for S / M / L pieces (result clamped to 1).")] public float sizeVolumeS = 0.8f, sizeVolumeM = 1f, sizeVolumeL = 1.15f;
        [Header("Random variance (per play)")]
        [Tooltip("± semitones of random pitch.")] [Range(0f, 3f)] public float pitchVarianceSemitones = 1f;
        [Tooltip("± dB of random volume.")] [Range(0f, 6f)] public float volumeVarianceDb = 1.5f;

        // ---------- Rate limits / cutoffs ----------
        [Header("Rate limits (sim ticks @50 Hz, measured on ImpactEvent.tick)")]
        [Tooltip("Min ticks between sounds for the same pieceId (4 = 80 ms).")] public int perPieceCooldownTicks = 4;
        [Tooltip("Debris rattle aggregation window in ticks (5 = 100 ms).")] public int rattleWindowTicks = 5;
        [Tooltip("PHYS_IMPACT below this impulse (N·s) is silent. Crack/Break/JointSnap always pass.")] public float minImpulse = 5f;

        // ---------- Spatial ----------
        [Header("Distance falloff")]
        public ImpactAudioRolloff rolloff = ImpactAudioRolloff.Logarithmic;
        [Tooltip("Full volume inside this distance (m).")] public float minDistance = 5f;
        [Tooltip("Silent/clamped beyond this distance (m).")] public float maxDistance = 40f;
        [Tooltip("0 = 2D, 1 = 3D. 0.5 keeps the 2.5D view readable.")] [Range(0f, 1f)] public float spatialBlend = 0.5f;

        // ---------- Priority ----------
        [Header("Source priority (Unity AudioSource.priority, lower = more important)")]
        public int priorityCannon = 32, priorityExplosion = 64, priorityPowerUp = 96, priorityDebris = 192;

        // ---------- Mixed pairs ----------
        [Header("Material pair resolution")]
        [Tooltip("Volume of the softer material's layer when a mixed pair hits (0 = none).")] [Range(0f, 1f)] public float softLayerVolume = 0.3f;
        [Tooltip("Only add the soft layer on Heavy hits.")] public bool softLayerHeavyOnly = true;

        // ---------- Collapse ----------
        [Header("Collapse: one COLLAPSE_LAND per cascadeId on PHYS_COLLAPSE_END")]
        [Tooltip("cascadeCount ≤ this is Small.")] public int collapseSmallMax = 5;
        [Tooltip("cascadeCount ≤ this is Medium; above is Large.")] public int collapseMediumMax = 20;
        [Tooltip("Volume for Small / Medium / Large collapse.")] public float collapseVolS = 0.6f, collapseVolM = 0.85f, collapseVolL = 1f;
        [Tooltip("Semitones for Small / Medium / Large collapse.")] public float collapsePitchS = 2f, collapsePitchM = 0f, collapsePitchL = -2f;
        [Tooltip("Per-piece PHYS_BREAK voices allowed while a cascade is active.")] public int collapseBreakVoices = 1;
        [Tooltip("If PHYS_COLLAPSE_END hasn't arrived this many ticks after START, play with the running count (150 = 3 s).")] public int collapseFallbackTicks = 150;

        // ---------- Existing API (unchanged) ----------
        public float Volume(float impulse) => Mathf.Clamp01(impulseToVolume.Evaluate(impulse));
        public float Pitch(float impulse) => impulseToPitch.Evaluate(impulse);
        public int VoiceLimit(QualityTier t) => t == QualityTier.Low ? voicesLow : t == QualityTier.Mid ? voicesMid : voicesHigh;

        // ---------- New helpers (pure, audio-side) ----------
        public static float SemitonesToRatio(float st) => (float)Math.Pow(2.0, st / 12.0);
        public static float DbToLinear(float db) => (float)Math.Pow(10.0, db / 20.0);

        public bool TryGetMaterial(MaterialType m, out MaterialAudio result)
        {
            if (materials != null)
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i].material == m) { result = materials[i]; return true; }
            result = default; return false;
        }

        /// <summary>Per-material audio tier; falls back to the global thresholds.</summary>
        public ImpactTier TierFor(MaterialType m, float impulse)
        {
            if (!TryGetMaterial(m, out var a)) return ImpactClassifier.Tier(impulse, thresholds);
            if (!(impulse >= a.mediumMinImpulse)) return ImpactTier.Light;
            return impulse > a.heavyMinImpulse ? ImpactTier.Heavy : ImpactTier.Medium;
        }

        public int VoiceCap(MaterialType m, QualityTier t)
        {
            int limit = VoiceLimit(t);
            if (!TryGetMaterial(m, out var a)) return limit;
            int cap = t == QualityTier.Low ? a.voiceCapLow : t == QualityTier.Mid ? a.voiceCapMid : a.voiceCapHigh;
            return Mathf.Clamp(cap, 0, limit);
        }

        public KindCurves CurvesFor(ImpactKind k)
        {
            var c = k == ImpactKind.Crack ? crackCurves : k == ImpactKind.Break ? breakCurves : k == ImpactKind.JointSnap ? jointSnapCurves : impactCurves;
            if (c.volume == null) c.volume = impulseToVolume;
            if (c.pitch == null) c.pitch = impulseToPitch;
            return c;
        }

        /// <summary>False if this event should be silent by impulse cutoff (Impact only).</summary>
        public bool PassesCutoff(in ImpactEvent e) => e.kind != ImpactKind.Impact || e.impulse >= minImpulse;

        public int Priority(ImpactSourceKind s) => s switch
        {
            ImpactSourceKind.Cannon => priorityCannon, ImpactSourceKind.Explosion => priorityExplosion,
            ImpactSourceKind.PowerUp => priorityPowerUp, _ => priorityDebris
        };

        /// <summary>Harder material wins the bank; softer one is returned as an optional layer (volume 0 if none).</summary>
        public MaterialType ResolvePair(MaterialType a, MaterialType b, ImpactTier tier, out MaterialType soft, out float softVolume)
        {
            int ha = TryGetMaterial(a, out var ma) ? ma.hardness : 0, hb = TryGetMaterial(b, out var mb) ? mb.hardness : 0;
            MaterialType hard = ha >= hb ? a : b; soft = ha >= hb ? b : a;
            softVolume = (soft == hard || (softLayerHeavyOnly && tier != ImpactTier.Heavy)) ? 0f : softLayerVolume;
            return hard;
        }

        /// <summary>Final volume 0..1 (deterministic part; add RandomVolumeMul at play time).</summary>
        public float VolumeFor(in ImpactEvent e)
        {
            float v = CurvesFor(e.kind).volume.Evaluate(e.impulse);
            v *= e.sizeClass == SizeClass.S ? sizeVolumeS : e.sizeClass == SizeClass.L ? sizeVolumeL : sizeVolumeM;
            return Mathf.Clamp01(v);
        }

        /// <summary>Final pitch multiplier (deterministic part; add RandomPitchMul at play time).</summary>
        public float PitchFor(in ImpactEvent e, MaterialType m)
        {
            float p = CurvesFor(e.kind).pitch.Evaluate(e.impulse);
            if (e.kind == ImpactKind.Crack && e.damageStep > 1) p += crackStepPitch * (e.damageStep - 1);
            float st = e.sizeClass == SizeClass.S ? sizePitchS : e.sizeClass == SizeClass.L ? sizePitchL : sizePitchM;
            if (TryGetMaterial(m, out var a)) st += a.pitchOffsetSemitones;
            return p * SemitonesToRatio(st);
        }

        /// <summary>u in [0,1) from a presentation RNG (never the sim RNG).</summary>
        public float RandomPitchMul(float u) => SemitonesToRatio((u * 2f - 1f) * pitchVarianceSemitones);
        public float RandomVolumeMul(float u) => DbToLinear((u * 2f - 1f) * volumeVarianceDb);

        public enum CollapseSize : byte { Small = 0, Medium = 1, Large = 2 }
        public CollapseSize CollapseSizeFor(int cascadeCount) =>
            cascadeCount <= collapseSmallMax ? CollapseSize.Small : cascadeCount <= collapseMediumMax ? CollapseSize.Medium : CollapseSize.Large;
        public float CollapseVolume(CollapseSize s) => s == CollapseSize.Small ? collapseVolS : s == CollapseSize.Medium ? collapseVolM : collapseVolL;
        public float CollapsePitch(CollapseSize s) => SemitonesToRatio(s == CollapseSize.Small ? collapsePitchS : s == CollapseSize.Medium ? collapsePitchM : collapsePitchL);
        /// <summary>COLLAPSE_LAND sfx for a PHYS_COLLAPSE_START/END payload, chosen by its dominantMaterial ("" = none).</summary>
        public string CollapseSfxFor(in ImpactEvent e) => TryGetMaterial(e.dominantMaterial, out var m) ? m.collapseSfx ?? "" : "";
        /// <summary>Material entry for the collapse (dominantMaterial), falling back to Terrain.</summary>
        public bool TryGetCollapseMaterial(in ImpactEvent e, out MaterialAudio result) =>
            TryGetMaterial(e.dominantMaterial, out result) || TryGetMaterial(MaterialType.Terrain, out result);

        void OnValidate()
        {
            perPieceCooldownTicks = Mathf.Max(0, perPieceCooldownTicks);
            rattleWindowTicks = Mathf.Max(1, rattleWindowTicks);
            rattleWindowMs = rattleWindowTicks * 20; // keep legacy field in sync (50 Hz)
            minImpulse = Mathf.Max(0f, minImpulse);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            collapseMediumMax = Mathf.Max(collapseSmallMax, collapseMediumMax);
            collapseFallbackTicks = Mathf.Max(1, collapseFallbackTicks);
        }
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
        public CascadeSession Session { get; private set; }
        public ImpactReportConfig reportConfig = ImpactReportConfig.Default;
        public readonly ImpactPairThrottle Throttle = new ImpactPairThrottle();
        /// <summary>Tick an event raised right now is tagged with (ImpactTickPolicy).</summary>
        public int Tick => CurrentTick;
        /// <summary>Add a fully built event to this tick's buffer (wiring helpers in ImpactReport).</summary>
        public void Add(ImpactEvent e) => _buffer.Add(e);
        SimRunner _runner;
        static readonly Action<ImpactEvent> Emit = e => AnimEventBus.Raise(ImpactEvents.NameOf(e.kind), e);

        public ImpactTierThresholds Thresholds => audioProfile != null ? audioProfile.thresholds : ImpactTierThresholds.Default;
        int CurrentTick => _runner != null ? ImpactTickPolicy.Tag(_runner.Tick, _runner.InTick) : 0;
        bool Ranked => _runner != null && _runner.Ranked;

        void OnEnable()
        {
            Instance = this; if (Session == null) Session = new CascadeSession(Cascades);
            _runner = SimRunner.Instance != null ? SimRunner.Instance : FindObjectOfType<SimRunner>();
            if (_runner != null) { _runner.Stepped += OnStepped; _runner.TickBegin += OnTickBegin; }
        }
        void OnDisable() { if (_runner != null) { _runner.Stepped -= OnStepped; _runner.TickBegin -= OnTickBegin; } if (Instance == this) Instance = null; }

        /// <summary>Call from SimRunner.BeginRun / Retry so cascade ids replay identically.</summary>
        public void ResetRun() { _buffer.Clear(); Cascades.Reset(); Session?.Reset(); Throttle.Clear(); ImpactSourceScope.ResetForTests(); }

        // See ImpactTickPolicy: ranked flushes after its own Physics.Simulate; non-ranked flushes the previous step at the next tick start.
        void OnStepped(int tick)
        {
            // Close a quiet cascade (collapse ended) on the sim tick, then flush (ranked).
            if (Session != null && Session.TryEnd(tick, reportConfig.cascadeQuietTicks, out int id, out int n, out var counts))
            {
                var e = ImpactEvent.Make(CurrentTick, ImpactKind.CollapseEnd, 0f, 0f, Thresholds);
                e.cascadeId = id; e.cascadeCount = n; e.materialCounts = counts; e.dominantMaterial = counts.Dominant; _buffer.Add(e);
            }
            if (ImpactTickPolicy.FlushOnStepped(Ranked)) _buffer.Flush(Emit);
        }
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
