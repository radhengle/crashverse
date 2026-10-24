using System;
using System.Text;

namespace Crashverse.VFX
{
    /// <summary>Physics PR #35 command-line args (-perfTier Low|Mid|High, -perfRuns N, -perfNoPowerUpLane). Tier: 0 Low, 1 Mid, 2 High; -1 = not given.</summary>
    public struct VfxPerfArgs
    {
        public int Tier; public int Runs; public bool NoPowerUpLane;
        public static VfxPerfArgs Parse(string[] a)
        {
            var r = new VfxPerfArgs { Tier = -1, Runs = 0 };
            if (a == null) return r;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == "-perfTier" && i + 1 < a.Length)
                {
                    var v = a[i + 1].ToLowerInvariant(); r.Tier = v == "low" ? 0 : v == "mid" ? 1 : v == "high" ? 2 : -1;
                }
                else if (a[i] == "-perfRuns" && i + 1 < a.Length && int.TryParse(a[i + 1], out int n)) r.Runs = Math.Max(1, n);
                else if (a[i] == "-perfNoPowerUpLane") r.NoPowerUpLane = true;
            }
            return r;
        }
    }

    /// <summary>Allocation-free rolling window (last N samples) with p95 via a preallocated scratch buffer.</summary>
    public sealed class VfxRollingWindow
    {
        readonly double[] _v, _scratch; int _n, _head;
        public VfxRollingWindow(int size = 120) { _v = new double[size]; _scratch = new double[size]; }
        public int Count => _n;
        public void Add(double x) { _v[_head] = x; _head = (_head + 1) % _v.Length; if (_n < _v.Length) _n++; }
        public void Clear() { _n = 0; _head = 0; }
        public double Percentile(double p)
        {
            if (_n == 0) return 0; Array.Copy(_v, _scratch, _n); Array.Sort(_scratch, 0, _n);
            int i = (int)Math.Ceiling(p / 100.0 * _n) - 1; return _scratch[Math.Max(0, Math.Min(_n - 1, i))];
        }
    }

    /// <summary>Overlay text (pure, testable). Built at ≤4 Hz by the probe into a reused StringBuilder.</summary>
    public static class VfxPerfOverlayText
    {
        public static readonly string[] TierNames = { "Low", "Mid", "High" };
        public static string Ms(double v) => v > 0 ? v.ToString("0.0") : "n/a";

        public static void Live(StringBuilder sb, int tier, bool capturing, int run, int runs, double gpuMs, double frameMs, double gpuP95, double frameP95,
                                int particles, int peakParticles, int dummyLane, int draw, int setPass)
        {
            sb.Clear();
            sb.Append("VFX PERF  ").Append(TierNames[Math.Max(0, Math.Min(2, tier))]).Append(capturing ? "  ● REC run " : "  idle run ").Append(run).Append('/').Append(runs).Append('\n');
            sb.Append("GPU ").Append(Ms(gpuMs)).Append(" ms   frame ").Append(Ms(frameMs)).Append(" ms\n");
            sb.Append("p95 GPU ").Append(gpuP95 > 0 ? Ms(gpuP95) : "n/a (frame " + Ms(frameP95) + ")").Append("   p95 frame ").Append(Ms(frameP95)).Append('\n');
            sb.Append("particles ").Append(particles).Append(" (peak ").Append(peakParticles).Append(", lane ").Append(dummyLane).Append(")\n");
            sb.Append("draw ").Append(draw).Append("   setpass ").Append(setPass);
        }
    }
}
