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

namespace Crashverse.Tests
{
    public class ComboAndCounterTests
    {
        [NUnit.Framework.Test] public void Combo_TickWindowInclusive()
        {
            var c = new Crashverse.Meta.ComboTracker(); c.OnShot();
            NUnit.Framework.Assert.AreEqual(1, c.OnBreakTick(0));
            NUnit.Framework.Assert.AreEqual(2, c.OnBreakTick(40));
            NUnit.Framework.Assert.AreEqual(1, c.OnBreakTick(81));
        }

        [NUnit.Framework.Test] public void CampaignScore_Floors() =>
            NUnit.Framework.Assert.AreEqual(873 + 1000 + 150, Crashverse.Meta.CampaignScore.Compute(true, 0.8739f, 2, 3, false));

        [NUnit.Framework.Test] public void ObjectiveCounter_CountsOncePerObjective()
        {
            var o = new Crashverse.Meta.ObjectiveCounter(); o.Reset(new[] { "a", "b", "c", "d" });
            NUnit.Framework.Assert.IsTrue(o.OnDestroyed("a"));
            NUnit.Framework.Assert.IsFalse(o.OnDestroyed("a"));
            NUnit.Framework.Assert.IsFalse(o.OnDestroyed("zzz"));
            o.OnDestroyed("b"); o.OnDestroyed("c");
            NUnit.Framework.Assert.AreEqual(7500, o.DestroyedBp);
            NUnit.Framework.Assert.IsFalse(o.Meets(0.85f));
            o.OnDestroyed("d");
            NUnit.Framework.Assert.IsTrue(o.Meets(0.85f));
        }
    }
}
