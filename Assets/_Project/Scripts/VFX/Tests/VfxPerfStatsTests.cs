using NUnit.Framework;

namespace Crashverse.VFX.Tests
{
    public class VfxPerfStatsTests
    {
        [Test] public void PassAndFailAgainstMidTargets()
        {
            var t = VfxPerfTargets.For(1);
            Assert.AreEqual(12, t.GpuP95Ms);
            var ok = new VfxPerfStats(t.HitchMs);
            for (int i = 0; i < 100; i++) ok.Add(16.6, 9, 10, 200, 120, 40);
            Assert.IsEmpty(ok.Evaluate(t));
            StringAssert.Contains("result=PASS", ok.Summary(1, t));

            var bad = new VfxPerfStats(t.HitchMs);
            for (int i = 0; i < 100; i++) bad.Add(i < 3 ? 40 : 16.6, 9, i < 10 ? 14 : 10, 240, 120, 40);
            var f = bad.Evaluate(t);
            Assert.IsTrue(f.Exists(s => s.StartsWith("GPU p95")));
            Assert.IsTrue(f.Exists(s => s.StartsWith("hitches")));
            Assert.IsTrue(f.Exists(s => s.StartsWith("particles")));
        }

        [Test] public void MissingGpuTiming_FallsBackToFrameTime()
        {
            var t = VfxPerfTargets.For(1);
            var ok = new VfxPerfStats(t.HitchMs); for (int i = 0; i < 50; i++) ok.Add(16.6, 8, 0, 10, 10, 10);
            Assert.IsEmpty(ok.Evaluate(t)); StringAssert.Contains("gpu_p95=n/a", ok.Summary(1, t));
            var slow = new VfxPerfStats(t.HitchMs); for (int i = 0; i < 50; i++) slow.Add(25, 8, 0, 10, 10, 10);
            Assert.IsTrue(slow.Evaluate(t).Exists(x => x.Contains("GPU n/a")));
        }

        [Test] public void ArgsAndRollingWindow()
        {
            var a = VfxPerfArgs.Parse(new[] { "x", "-perfTier", "High", "-perfRuns", "3", "-perfNoPowerUpLane" });
            Assert.AreEqual(2, a.Tier); Assert.AreEqual(3, a.Runs); Assert.IsTrue(a.NoPowerUpLane);
            Assert.AreEqual(-1, VfxPerfArgs.Parse(new string[0]).Tier);
            var w = new VfxRollingWindow(20); for (int i = 1; i <= 40; i++) w.Add(i);
            Assert.AreEqual(20, w.Count); Assert.AreEqual(39, w.Percentile(95)); Assert.AreEqual(21, w.Percentile(0));
            var sb = new System.Text.StringBuilder();
            VfxPerfOverlayText.Live(sb, 1, true, 1, 3, 0, 16.6, 0, 17, 200, 230, 80, 120, 40);
            StringAssert.Contains("GPU n/a", sb.ToString()); StringAssert.Contains("lane 80", sb.ToString());
        }
    }
}
