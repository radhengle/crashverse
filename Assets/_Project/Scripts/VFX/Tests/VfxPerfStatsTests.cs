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

        [Test] public void MissingGpuTimingIsReported()
        {
            var s = new VfxPerfStats(33.4); s.Add(16, 8, 0, 10, 10, 10);
            Assert.Contains("NO_GPU_TIMING", s.Evaluate(VfxPerfTargets.For(1)));
        }
    }
}
