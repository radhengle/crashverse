using UnityEngine;
using Crashverse.Feel;
#if UNITY_2022_3_OR_NEWER
using Unity.Profiling;
#endif

namespace Crashverse.VFX
{
    /// <summary>
    /// Lightweight in-build profiler hook for the L10 full-budget collapse test (vfx/PERF_TEST_L10.md). Dev builds only
    /// (CRASHVERSE_DEV or DEVELOPMENT_BUILD); compiles to an inert component otherwise. Per frame: GPU + CPU frame time from
    /// FrameTimingManager (needs Player Settings > Frame Timing Stats on), live particle count from VfxDirector, draw calls + SetPass
    /// from ProfilerRecorder. Window opens on PHYS_COLLAPSE_START (or COLLAPSE_CREAK, or StartCapture()) and closes `tailSeconds` after
    /// PHYS_COLLAPSE_END / COLLAPSE_LAND, or after maxSeconds. Logs one CSV line per frame ("VFXPERF,...") and a summary with PASS/FAIL.
    /// Read with: adb logcat -s Unity | grep VFXPERF
    /// </summary>
    public class VfxPerfProbe : MonoBehaviour
    {
        public VfxDirector director;
        public QualityTier tier = QualityTier.Mid;
        public float tailSeconds = 3f, maxSeconds = 15f;
        public bool logEveryFrame = true;

#if CRASHVERSE_DEV || DEVELOPMENT_BUILD
        VfxPerfStats _stats; float _start = -1f, _endAt = -1f; int _frame;
#if UNITY_2022_3_OR_NEWER
        readonly FrameTiming[] _ft = new FrameTiming[1];
        ProfilerRecorder _draw, _setPass;
        void OnEnable()
        {
            AnimEventBus.Raised += OnEvent;
            _draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }
        void OnDisable() { AnimEventBus.Raised -= OnEvent; _draw.Dispose(); _setPass.Dispose(); }
#else
        void OnEnable() { AnimEventBus.Raised += OnEvent; }
        void OnDisable() { AnimEventBus.Raised -= OnEvent; }
#endif
        public bool Capturing => _start >= 0f;

        void OnEvent(string name, object payload)
        {
            if (name == "PHYS_COLLAPSE_START" || name == "COLLAPSE_CREAK") { if (!Capturing) StartCapture(); }
            else if ((name == "PHYS_COLLAPSE_END" || name == "COLLAPSE_LAND") && Capturing) _endAt = Time.unscaledTime + tailSeconds;
        }

        public void StartCapture()
        {
            var t = VfxPerfTargets.For((int)tier);
            _stats = new VfxPerfStats(t.HitchMs); _start = Time.unscaledTime; _endAt = -1f; _frame = 0;
            Debug.Log("VFXPERF_HEADER,frame,t,frame_ms,cpu_ms,gpu_ms,particles,draw,setpass");
        }

        public void StopCapture()
        {
            if (!Capturing) return;
            var t = VfxPerfTargets.For((int)tier);
            Debug.Log(_stats.Summary((int)tier, t)); _start = -1f;
        }

        void Update()
        {
#if UNITY_2022_3_OR_NEWER
            FrameTimingManager.CaptureFrameTimings();
#endif
            if (!Capturing) return;
            double cpu = 0, gpu = 0, frame = Time.unscaledDeltaTime * 1000.0; int draw = 0, setPass = 0;
#if UNITY_2022_3_OR_NEWER
            if (FrameTimingManager.GetLatestTimings(1, _ft) > 0) { cpu = _ft[0].cpuFrameTime; gpu = _ft[0].gpuFrameTime; }
            draw = (int)_draw.LastValue; setPass = (int)_setPass.LastValue;
#endif
            int particles = director != null ? director.LiveParticleCount() : 0;
            _stats.Add(frame, cpu, gpu, particles, draw, setPass);
            float tt = Time.unscaledTime - _start;
            if (logEveryFrame) Debug.Log($"VFXPERF,{_frame++},{tt:0.000},{frame:0.00},{cpu:0.00},{gpu:0.00},{particles},{draw},{setPass}");
            if ((_endAt >= 0f && Time.unscaledTime >= _endAt) || tt >= maxSeconds) StopCapture();
        }
#endif
    }
}
