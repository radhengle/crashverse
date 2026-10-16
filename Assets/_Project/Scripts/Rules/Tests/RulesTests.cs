using Crashverse.Meta;
using NUnit.Framework;

namespace Crashverse.Tests
{
    public class RulesTests
    {
        [Test] public void WinRule_ExactThresholdPasses() { Assert.IsTrue(WinRule.Meets(0.85f, 0.85f)); Assert.AreEqual(8499, WinRule.FractionBp(8499, 10000)); }
        [Test] public void WinRule_FractionBpFloors() { Assert.AreEqual(6666, WinRule.FractionBp(2, 3)); Assert.AreEqual(8000, WinRule.FractionBp(4, 5)); }
        [Test] public void StarRules_ExactBandsPass() { Assert.AreEqual(3, StarRules.ComputeInt(true, 3, 5)); Assert.AreEqual(0, StarRules.ComputeInt(false, 5, 5)); Assert.AreEqual(2, StarRules.Compute(true, 3, 5, cap: 2)); }
    }
}
