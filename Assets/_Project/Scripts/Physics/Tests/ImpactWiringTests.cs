using System.Collections.Generic;
using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    public class ImpactWiringTests
    {
        [Test] public void CrackSteps_33_66_90()
        {
            Assert.AreEqual(0, CrackSteps.StepFor(329));
            Assert.AreEqual(1, CrackSteps.StepFor(330));
            Assert.AreEqual(2, CrackSteps.StepFor(660));
            Assert.AreEqual(3, CrackSteps.StepFor(900));
            Assert.AreEqual(3, CrackSteps.StepFor(1000));
        }

        [Test] public void CrackSteps_DamagePermille()
        {
            Assert.AreEqual(0, CrackSteps.DamagePermille(30f, 30f));
            Assert.AreEqual(500, CrackSteps.DamagePermille(15f, 30f));
            Assert.AreEqual(1000, CrackSteps.DamagePermille(-5f, 30f));
            Assert.AreEqual(0, CrackSteps.DamagePermille(1f, 0f));
        }

        [Test] public void CrackSteps_BigHitEmitsEachStepOnce()
        {
            var got = new List<int>();
            int s = CrackSteps.Advance(0, 3, got.Add);
            Assert.AreEqual(new[] { 1, 2, 3 }, got.ToArray()); Assert.AreEqual(3, s);
            got.Clear(); s = CrackSteps.Advance(s, 2, got.Add); // never goes back down, never repeats
            Assert.AreEqual(0, got.Count); Assert.AreEqual(3, s);
        }

        [Test] public void Qualify_ImpulseOrSpeed()
        {
            var c = ImpactReportConfig.Default;
            Assert.IsFalse(ImpactQualify.Qualifies(4f, 999, c));
            Assert.IsTrue(ImpactQualify.Qualifies(5f, 0, c));
            Assert.IsTrue(ImpactQualify.Qualifies(0f, 1000, c));
        }

        [Test] public void Throttle_UnorderedPair_Cooldown()
        {
            var t = new ImpactPairThrottle();
            Assert.IsTrue(t.TryPass(1, 2, 0, 10));
            Assert.IsFalse(t.TryPass(2, 1, 0, 10)); // other body's duplicate callback
            Assert.IsFalse(t.TryPass(1, 2, 9, 10));
            Assert.IsTrue(t.TryPass(1, 2, 10, 10));
            Assert.IsTrue(t.TryPass(1, 3, 9, 10));
        }

        [Test] public void StableId_IsStableAndNotReserved()
        {
            Assert.AreEqual(StableId.Of("wall_03"), StableId.Of("wall_03"));
            Assert.AreNotEqual(StableId.Of("wall_03"), StableId.Of("wall_04"));
            Assert.Greater(StableId.Of(""), 0);
            Assert.GreaterOrEqual(StableId.Of("x"), 2);
        }

        [Test] public void SourceScope_NestsAndDefaultsToDebris()
        {
            ImpactSourceScope.ResetForTests();
            Assert.AreEqual(ImpactSourceKind.Debris, ImpactSourceScope.Kind);
            using (ImpactSourceScope.Push(ImpactSourceKind.PowerUp, PowerUpIds.ChainReactor))
            {
                Assert.AreEqual(ImpactSourceKind.PowerUp, ImpactSourceScope.Kind);
                using (ImpactSourceScope.Push(ImpactSourceKind.Explosion)) Assert.AreEqual(ImpactSourceKind.Explosion, ImpactSourceScope.Kind);
                Assert.AreEqual(PowerUpIds.ChainReactor, ImpactSourceScope.Id);
            }
            Assert.AreEqual(ImpactSourceKind.Debris, ImpactSourceScope.Kind);
        }

        [Test] public void Cascade_StartJoinEnd_Replays()
        {
            for (int run = 0; run < 2; run++)
            {
                var s = new CascadeSession();
                Assert.AreEqual(0, s.JoinForBreak(0, out _)); // no cascade open: plain break
                int id = s.JoinForCollapse(10, out bool started, out int n);
                Assert.IsTrue(started); Assert.AreEqual(1, id); Assert.AreEqual(1, n);
                Assert.AreEqual(id, s.JoinForCollapse(12, out started, out n)); Assert.IsFalse(started); Assert.AreEqual(2, n);
                Assert.AreEqual(id, s.JoinForBreak(20, out n)); Assert.AreEqual(3, n);
                Assert.IsFalse(s.TryEnd(44, 25, out _, out _));
                Assert.IsTrue(s.TryEnd(45, 25, out int ended, out n)); Assert.AreEqual(id, ended); Assert.AreEqual(3, n);
                Assert.AreEqual(2, s.JoinForCollapse(50, out started, out _)); Assert.IsTrue(started);
            }
        }
    }
}
