using System.Collections.Generic;
using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    public class DebrisDamageTests
    {
        const int Kg = 1000; // grams
        const int W = DebrisMaterialMult.Wood;
        static int D(int mmps, int g, int mult = W) => DebrisDamage.ComputeMilliHp(mmps, g, mult, false);

        [Test] public void AtThreshold_NoDamage() => Assert.AreEqual(0, D(2000, 5 * Kg));
        [Test] public void BelowThreshold_NoDamage() => Assert.AreEqual(0, D(1999, 5 * Kg));

        [Test] public void JustAboveThreshold_IsContinuous_AboutZero()
        {
            Assert.AreEqual(2, D(2001, 4 * Kg));      // 0.5 × 4 kg × 0.001 m/s = 0.002 HP
            Assert.Less(D(2010, 10 * Kg), 100);       // < 0.1 HP just above threshold
        }

        [Test] public void Scaling_LinearInMassAndExcessSpeed()
        {
            int a = D(4000, 2 * Kg);                  // 0.5 × 2 × 2 = 2 HP
            Assert.AreEqual(2000, a);
            Assert.AreEqual(2 * a, D(4000, 4 * Kg));  // 2x mass
            Assert.AreEqual(2 * a, D(6000, 2 * Kg));  // 2x excess speed (4 m/s over)
        }

        [Test] public void CappedAt60PerCollision()
        {
            Assert.AreEqual(60000, D(50000, 100 * Kg));
            Assert.AreEqual(60000, D(int.MaxValue, int.MaxValue, DebrisMaterialMult.Brick));
            Assert.AreEqual(59999, D(2000 + 119998, 1 * Kg)); // just under the cap
        }

        [Test] public void MaterialMultipliers()
        {
            Assert.AreEqual(10000, D(12000, 2 * Kg, DebrisMaterialMult.Wood));   // 0.5 × 2 × 10 × 1.0
            Assert.AreEqual(11000, D(12000, 2 * Kg, DebrisMaterialMult.Brick));  // × 1.1
            Assert.AreEqual(8000, D(12000, 2 * Kg, DebrisMaterialMult.Steel));   // × 0.8
            Assert.AreEqual(60000, D(1000000, 2 * Kg, DebrisMaterialMult.Steel)); // cap applies after the multiplier
        }

        [Test] public void RestingContact_NoDamage()
        {
            Assert.AreEqual(0, D(0, 50 * Kg));
            Assert.AreEqual(0, DebrisDamage.ComputeMilliHp(10000, 50 * Kg, W, restingContact: true));
        }

        [Test] public void ConfigFromData()
        {
            var cfg = new DebrisDamageConfig { minSpeedMmPerS = 1000, milliHpPerKgMetrePerS = 1000, capMilliHp = 5000 };
            Assert.AreEqual(500, DebrisDamage.ComputeMilliHp(1500, 1 * Kg, W, false, cfg));
            Assert.AreEqual(5000, DebrisDamage.ComputeMilliHp(9000, 1 * Kg, W, false, cfg));
        }

        [Test] public void UnitConversion_Rounds()
        {
            Assert.AreEqual(2001, DebrisDamage.ToMmPerS(2.001f));
            Assert.AreEqual(1500, DebrisDamage.ToGrams(1.5f));
        }

        [Test] public void Accumulator_StableOrder_RegardlessOfReportOrder()
        {
            var a = new DebrisDamageAccumulator<string>();
            var b = new DebrisDamageAccumulator<string>();
            a.Add("B2", "x", 10, "B2"); a.Add("A1", "y", 20, "A1"); a.Add("A1", "x", 30, "A1");
            b.Add("A1", "x", 30, "A1"); b.Add("B2", "x", 10, "B2"); b.Add("A1", "y", 20, "A1");
            var ra = new List<string>(); var rb = new List<string>();
            a.Flush((t, d) => ra.Add(t + ":" + d)); b.Flush((t, d) => rb.Add(t + ":" + d));
            CollectionAssert.AreEqual(new[] { "A1:30", "A1:20", "B2:10" }, ra);
            CollectionAssert.AreEqual(ra, rb);
            Assert.AreEqual(0, a.Count);
        }

        [Test] public void Accumulator_DuplicatePairCountsOnce()
        {
            var acc = new DebrisDamageAccumulator<string>();
            acc.Add("A", "s", 100, "A"); acc.Add("A", "s", 40, "A");
            int total = 0; acc.Flush((t, d) => total += d);
            Assert.AreEqual(100, total);
        }
    }

    public class SupportRuleTests
    {
        [Test] public void OneOfTwoSupportsRemoved_NoCollapse() => Assert.IsFalse(SupportRule.ShouldCollapse(2, 1));
        [Test] public void AllSupportsRemoved_Collapse() => Assert.IsTrue(SupportRule.ShouldCollapse(2, 0));
        [Test] public void Grounded_NeverCollapses() => Assert.IsFalse(SupportRule.ShouldCollapse(0, 0));
    }
}
