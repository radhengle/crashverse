using System.Text;
using UnityEngine;
using Crashverse.Feel;
#if UNITY_2022_3_OR_NEWER
using Unity.Profiling;
using UnityEngine.SceneManagement;
#endif

namespace Crashverse.VFX
{
    /// <summary>
    /// In-build profiler hook + on-screen overlay for the L10 full-budget collapse test (vfx/PERF_TEST_L10.md), made for a phone-only
    /// tester: no PC, no profiler, results read from a screenshot. Compiled only with CRASHVERSE_DEV or DEVELOPMENT_BUILD (whole body
    /// stripped from release players) and, at runtime, only active when Debug.isDebugBuild or CRASHVERSE_DEV.
    ///
    /// Per frame: GPU + CPU frame time (FrameTimingManager; GPU may read 0 on some drivers → "n/a", pass/fail falls back to frame time),
    /// particles = VfxDirector live particles + the dev power-up dummy lane (Physics #35, soft lookup by reflection so #31 builds without
    /// #35), draw + SetPass calls (ProfilerRecorder). Capture window: PHYS_COLLAPSE_START → 3 s after PHYS_COLLAPSE_END (15 s max), once per
    /// run; after the last run (-perfRuns, default 1) a frozen PASS/FAIL panel stays up until tapped. Logs VFXPERF lines to logcat too.
    /// Auto-bootstrap: in Scenes/Dev/L10_PerfFullBudget the probe (and a VfxDirector, if none) is created from code; the scene file is untouched.
    /// </summary>
    public class VfxPerfProbe : MonoBehaviour
    {
        public const string PerfSceneName = "L10_PerfFullBudget";
        public VfxDirector director;
        public QualityTier tier = QualityTier.Mid;
        public float tailSeconds = 3f, maxSeconds = 15f;
        public int runs = 1;
        public bool logEveryFrame = true, showOverlay = true;

#if CRASHVERSE_DEV || DEVELOPMENT_BUILD
        VfxPerfStats _stats; float _start = -1f, _endAt = -1f; int _frame, _runsDone;
        readonly VfxRollingWindow _gpuWin = new VfxRollingWindow(120), _frameWin = new VfxRollingWindow(120);
        readonly StringBuilder _sb = new StringBuilder(256), _final = new StringBuilder(512);
        string _liveText = "", _finalText; bool _finalPass = true; float _nextText;
        double _gpu, _frameMs; int _particles, _peakParticles, _lane, _draw, _setPass;
        VfxPerfArgs _args;

        public bool Capturing => _start >= 0f;
#if UNITY_2022_3_OR_NEWER
        static string BuildTag => Application.version;
#else
        static string BuildTag => "";
#endif

        // ---------- soft lookups into Physics #35 (no compile dependency) ----------
        static System.Reflection.PropertyInfo _laneProp; static System.Type _stageType; static bool _looked;
        static void Lookup()
        {
            if (_looked) return; _looked = true;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("Crashverse.DevTools.Perf.PerfDummyPowerUpLane"); if (t != null) _laneProp = t.GetProperty("LiveParticles");
                var s = asm.GetType("Crashverse.DevTools.Perf.PerfL10Stage"); if (s != null) _stageType = s;
            }
        }
        static int LaneParticles() => _laneProp != null ? (int)_laneProp.GetValue(null) : 0;

        /// <summary>PERFL10 determinism hash of the current/last run from PerfL10Stage.Stats.Hash, or null.</summary>
        string StageHash()
        {
#if UNITY_2022_3_OR_NEWER
            if (_stageType == null) return null;
            var stage = FindObjectOfType(_stageType); if (stage == null) return null;
            var stats = _stageType.GetProperty("Stats")?.GetValue(stage); if (stats == null) return null;
            var h = stats.GetType().GetProperty("Hash")?.GetValue(stats) ?? stats.GetType().GetField("Hash")?.GetValue(stats);
            return h is ulong u ? u.ToString("X16") : h?.ToString();
#else
            return null;
#endif
        }

#if UNITY_2022_3_OR_NEWER
        readonly FrameTiming[] _ft = new FrameTiming[1];
        ProfilerRecorder _drawRec, _setPassRec;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        static void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (s.name != PerfSceneName || FindObjectOfType<VfxPerfProbe>() != null) return;
            new GameObject("VfxPerfProbe (auto)").AddComponent<VfxPerfProbe>();
        }

        void Awake()
        {
            if (!Debug.isDebugBuild) { enabled = false; return; }
            Lookup();
            _args = VfxPerfArgs.Parse(System.Environment.GetCommandLineArgs());
            if (_args.Tier >= 0) tier = (QualityTier)_args.Tier;
            if (_args.Runs > 0) runs = _args.Runs;
            else if (_stageType != null) { var st = FindObjectOfType(_stageType); var f = _stageType.GetField("runs"); if (st != null && f != null) runs = Mathf.Max(1, (int)f.GetValue(st)); }
            if (director == null) director = FindObjectOfType<VfxDirector>();
            if (director == null)
            {
                var go = new GameObject("VfxDirector (auto)"); go.SetActive(false);
                director = go.AddComponent<VfxDirector>(); director.tier = tier; go.SetActive(true); // Awake runs with the right tier + Resources assets
            }
        }

        void OnEnable()
        {
            AnimEventBus.Raised += OnEvent;
            _drawRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }
        void OnDisable() { AnimEventBus.Raised -= OnEvent; _drawRec.Dispose(); _setPassRec.Dispose(); }
#else
        void OnEnable() { AnimEventBus.Raised += OnEvent; }
        void OnDisable() { AnimEventBus.Raised -= OnEvent; }
#endif

        void OnEvent(string name, object payload)
        {
            if (_finalText != null && _runsDone >= runs) return;
            if (name == "PHYS_COLLAPSE_START" || name == "COLLAPSE_CREAK") { if (!Capturing) StartCapture(); }
            else if ((name == "PHYS_COLLAPSE_END" || name == "COLLAPSE_LAND") && Capturing) _endAt = Time.unscaledTime + tailSeconds;
        }

        public void StartCapture()
        {
            var t = VfxPerfTargets.For((int)tier);
            _stats = new VfxPerfStats(t.HitchMs); _start = Time.unscaledTime; _endAt = -1f; _frame = 0; _peakParticles = 0;
            _gpuWin.Clear(); _frameWin.Clear();
            Debug.Log("VFXPERF_HEADER,frame,t,frame_ms,cpu_ms,gpu_ms,particles,lane,draw,setpass");
        }

        public void StopCapture()
        {
            if (!Capturing) return;
            var t = VfxPerfTargets.For((int)tier); var hash = StageHash();
            string extra = $",run={_runsDone + 1}/{runs},lane={(_args.NoPowerUpLane ? "off" : "on")}" + (hash != null ? ",perfl10_hash=" + hash : "");
            string line = _stats.Summary((int)tier, t, extra); Debug.Log(line);
            var fails = _stats.Evaluate(t); _finalPass &= fails.Count == 0; _runsDone++; _start = -1f;
            if (_final.Length == 0) _final.Append("L10 PERF  ").Append(VfxPerfOverlayText.TierNames[(int)tier]).Append("  build ").Append(BuildTag).Append('\n');
            _final.Append("run ").Append(_runsDone).Append(": ").Append(fails.Count == 0 ? "PASS" : "FAIL").Append("  GPU p95 ")
                  .Append(_stats.GpuAvailable ? VfxPerfOverlayText.Ms(_stats.GpuP95) : "n/a").Append("  frame p95 ").Append(VfxPerfOverlayText.Ms(_stats.FrameP95))
                  .Append("  peak ").Append(_stats.PeakParticles).Append("  draw ").Append(_stats.PeakDrawCalls).Append('/').Append(_stats.PeakSetPass).Append('\n');
            foreach (var f in fails) _final.Append("   - ").Append(f).Append('\n');
            if (_runsDone >= runs)
            {
                _final.Append(_finalPass ? "RESULT: PASS" : "RESULT: FAIL");
                if (hash != null) _final.Append("   PERFL10 ").Append(hash);
                _final.Append("\n(tap to close)");
                _finalText = _final.ToString();
            }
        }

        void Update()
        {
#if UNITY_2022_3_OR_NEWER
            FrameTimingManager.CaptureFrameTimings();
            double cpu = 0; _gpu = 0;
            if (FrameTimingManager.GetLatestTimings(1, _ft) > 0) { cpu = _ft[0].cpuFrameTime; _gpu = _ft[0].gpuFrameTime; }
            _draw = (int)_drawRec.LastValue; _setPass = (int)_setPassRec.LastValue;
#else
            double cpu = 0; _gpu = 0;
#endif
            _frameMs = Time.unscaledDeltaTime * 1000.0;
            _lane = LaneParticles();
            _particles = (director != null ? director.LiveParticleCount() : 0) + _lane;
            _peakParticles = Mathf.Max(_peakParticles, _particles);
            if (_gpu > 0) _gpuWin.Add(_gpu); _frameWin.Add(_frameMs);

            if (Capturing)
            {
                _stats.Add(_frameMs, cpu, _gpu, _particles, _draw, _setPass);
                float tt = Time.unscaledTime - _start;
                if (logEveryFrame) Debug.Log($"VFXPERF,{_frame++},{tt:0.000},{_frameMs:0.00},{cpu:0.00},{_gpu:0.00},{_particles},{_lane},{_draw},{_setPass}");
                if ((_endAt >= 0f && Time.unscaledTime >= _endAt) || tt >= maxSeconds) StopCapture();
            }
            if (showOverlay && Time.unscaledTime >= _nextText) // text rebuilt at 4 Hz: one string per 250 ms, no per-frame garbage
            {
                _nextText = Time.unscaledTime + 0.25f;
                VfxPerfOverlayText.Live(_sb, (int)tier, Capturing, Mathf.Min(_runsDone + 1, runs), runs, _gpu, _frameMs,
                                        _gpuWin.Count > 0 ? _gpuWin.Percentile(95) : 0, _frameWin.Percentile(95), _particles, _peakParticles, _lane, _draw, _setPass);
                _liveText = _sb.ToString();
            }
        }

#if UNITY_2022_3_OR_NEWER
        GUIStyle _style, _finalStyle; Texture2D _bg;
        void OnGUI()
        {
            if (!showOverlay) return;
            var safe = Screen.safeArea; // keep out of the punch-hole / cutout and gesture bar
            int fs = Mathf.Max(22, Mathf.RoundToInt(Mathf.Min(safe.width, safe.height) / 26f));
            if (_style == null)
            {
                _bg = new Texture2D(1, 1); _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f)); _bg.Apply();
                _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, padding = new RectOffset(16, 16, 12, 12) };
                _style.normal.background = _bg; _style.normal.textColor = Color.white;
                _finalStyle = new GUIStyle(_style);
            }
            _style.fontSize = fs; _finalStyle.fontSize = Mathf.RoundToInt(fs * 1.1f);
            // GUI uses top-left origin; Screen.safeArea uses bottom-left.
            float top = Screen.height - safe.yMax, left = safe.x, w = safe.width - 32f;
            GUI.Label(new Rect(left + 16f, top + 16f, w, fs * 6.6f), _liveText, _style);
            if (_finalText != null)
            {
                _finalStyle.normal.textColor = _finalPass ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.45f, 0.4f);
                var r = new Rect(left + 16f, top + 32f + fs * 6.6f, w, fs * (4f + 1.35f * (runs * 2 + 2)));
                if (GUI.Button(r, _finalText, _finalStyle)) _finalText = null; // stays until tapped
            }
        }
#endif
#endif
    }
}
