using System;
using System.Collections.Generic;

namespace Crashverse.VFX
{
    /// <summary>Pass/fail targets for the L10 full-budget collapse test (vfx/PERF_TEST_L10.md). Times in ms, per tier.</summary>
    public struct VfxPerfTargets
    {
        public double GpuP95Ms, CpuP95Ms, FrameP99Ms, HitchMs, FrameBudgetMs; public int MaxHitches, MaxParticles, MaxDrawCalls, MaxSetPass;
        /// <summary>Tier 0 Low (30 fps), 1 Mid (60 fps), 2 High (60 fps). Particles = impact lane + power-up lane; draw calls from spec §4.</summary>
        public static VfxPerfTargets For(int tier) => tier == 0
            ? new VfxPerfTargets { GpuP95Ms = 25, CpuP95Ms = 25, FrameP99Ms = 40, HitchMs = 50, MaxHitches = 2, FrameBudgetMs = 33.4, MaxParticles = 115, MaxDrawCalls = 120, MaxSetPass = 40 }
            : new VfxPerfTargets { GpuP95Ms = 12, CpuP95Ms = 12, FrameP99Ms = 20, HitchMs = 33.4, MaxHitches = 2, FrameBudgetMs = 16.7, MaxParticles = 230,
                                   MaxDrawCalls = tier == 1 ? 150 : 200, MaxSetPass = tier == 1 ? 50 : 60 };
    }

    /// <summary>Engine-free accumulator for one capture window. Allocation-free per frame after construction.</summary>
    public sealed class VfxPerfStats
    {
        readonly List<double> _gpu, _cpu, _frame; int _peakParticles, _peakDraw, _peakSetPass, _hitches; readonly double _hitchMs;
        public int Frames => _frame.Count; public int PeakParticles => _peakParticles; public int PeakDrawCalls => _peakDraw; public int PeakSetPass => _peakSetPass; public int Hitches => _hitches;
        public VfxPerfStats(double hitchMs, int capacity = 1200) { _hitchMs = hitchMs; _gpu = new List<double>(capacity); _cpu = new List<double>(capacity); _frame = new List<double>(capacity); }

        /// <summary>gpuMs ≤ 0 = unavailable this frame (FrameTimingManager may report 0 on some drivers).</summary>
        public void Add(double frameMs, double cpuMs, double gpuMs, int particles, int drawCalls, int setPass)
        {
            _frame.Add(frameMs); if (cpuMs > 0) _cpu.Add(cpuMs); if (gpuMs > 0) _gpu.Add(gpuMs);
            if (frameMs > _hitchMs) _hitches++;
            _peakParticles = Math.Max(_peakParticles, particles); _peakDraw = Math.Max(_peakDraw, drawCalls); _peakSetPass = Math.Max(_peakSetPass, setPass);
        }

        public static double Percentile(List<double> v, double p)
        {
            if (v.Count == 0) return 0; var a = v.ToArray(); Array.Sort(a);
            int i = (int)Math.Ceiling(p / 100.0 * a.Length) - 1; return a[Math.Max(0, Math.Min(a.Length - 1, i))];
        }
        public double GpuP95 => Percentile(_gpu, 95); public double CpuP95 => Percentile(_cpu, 95); public double FrameP99 => Percentile(_frame, 99); public double FrameP95 => Percentile(_frame, 95);
        public bool GpuAvailable => _gpu.Count > 0;

        /// <summary>Returns failures (empty = PASS). Without GPU timing, frame p95 must stay within the frame budget (16.7 ms Mid/High, 33.4 ms Low).</summary>
        public List<string> Evaluate(in VfxPerfTargets t)
        {
            var f = new List<string>();
            // GPU time can read 0 on some drivers: fall back to frame p95 against the GPU target (flagged "gpu=n/a" in the summary).
            if (GpuAvailable) { if (GpuP95 > t.GpuP95Ms) f.Add($"GPU p95 {GpuP95:0.0} > {t.GpuP95Ms}"); }
            else if (FrameP95 > t.FrameBudgetMs) f.Add($"frame p95 {FrameP95:0.0} > {t.FrameBudgetMs} (GPU n/a)");
            if (CpuP95 > t.CpuP95Ms) f.Add($"CPU p95 {CpuP95:0.0} > {t.CpuP95Ms}");
            if (FrameP99 > t.FrameP99Ms) f.Add($"frame p99 {FrameP99:0.0} > {t.FrameP99Ms}");
            if (_hitches > t.MaxHitches) f.Add($"hitches {_hitches} > {t.MaxHitches}");
            if (_peakParticles > t.MaxParticles) f.Add($"particles {_peakParticles} > {t.MaxParticles}");
            if (_peakDraw > t.MaxDrawCalls) f.Add($"draw calls {_peakDraw} > {t.MaxDrawCalls}");
            if (_peakSetPass > t.MaxSetPass) f.Add($"SetPass {_peakSetPass} > {t.MaxSetPass}");
            return f;
        }

        public string Summary(int tier, in VfxPerfTargets t, string extra = "")
        {
            var fails = Evaluate(t);
            return $"VFXPERF_SUMMARY,tier={tier}{extra},frames={Frames},gpu_p95={(GpuAvailable ? GpuP95.ToString("0.00") : "n/a")},frame_p95={FrameP95:0.00},cpu_p95={CpuP95:0.00},frame_p99={FrameP99:0.00},hitches={_hitches}," +
                   $"peak_particles={_peakParticles},peak_draw={_peakDraw},peak_setpass={_peakSetPass},result={(fails.Count == 0 ? "PASS" : "FAIL:" + string.Join("|", fails))}";
        }
    }
}
