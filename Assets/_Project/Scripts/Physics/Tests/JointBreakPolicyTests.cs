using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    /// <summary>EditMode: JointBreakPolicy (physics/Joint_Break_Policy.md). Engine-free.</summary>
    public class JointBreakPolicyTests
    {
        [Test] public void Constants_AreInfinite_AndExplicitValuesIgnored()
        {
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.BreakForce));
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.BreakTorque));
            Assert.AreEqual(JointBreakPolicy.AuthoredValueHandling.Ignored, JointBreakPolicy.ExplicitKitValues);
        }

        [TestCase(0f)] [TestCase(-5f)] [TestCase(float.NaN)]
        public void BlankKitValue_MapsToInfinity(float blank)
        {
            Assert.IsTrue(JointBreakPolicy.IsBlank(blank));
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.ResolveBreakForce(blank)));
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.ResolveBreakTorque(blank)));
        }

        [TestCase(250f)] [TestCase(2200f)] [TestCase(1e9f)]
        public void ExplicitFiniteKitValue_IsIgnored_StillInfinity(float authored)
        {
            Assert.IsFalse(JointBreakPolicy.IsBlank(authored));
            Assert.IsTrue(JointBreakPolicy.IsExplicitFinite(authored));
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.ResolveBreakForce(authored)));
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.ResolveBreakTorque(authored)));
        }

        [Test] public void Complies_OnlyWhenBothInfinite()
        {
            Assert.IsTrue(JointBreakPolicy.Complies(float.PositiveInfinity, float.PositiveInfinity));
            Assert.IsFalse(JointBreakPolicy.Complies(800f, float.PositiveInfinity));
            Assert.IsFalse(JointBreakPolicy.Complies(float.PositiveInfinity, 800f));
        }

        [Test] public void RankedStep_Unchanged_50Hz()
        {
            Assert.AreEqual(50, SimTime.TicksPerSecond);
            Assert.AreEqual(0.02f, SimTime.Step);
        }
    }
}
