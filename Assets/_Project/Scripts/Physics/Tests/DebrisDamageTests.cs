using System.Collections.Generic;
using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    public class DebrisDamageTests
    {
        const int Kg = 1000; // grams

        [Test] public void AtThreshold_NoDamage() => Assert.AreEqual(0, DebrisDamage.ComputeMilliHp(2000, 5 * Kg, false));
        [Test] public void BelowThreshold_NoDamage() => Assert.AreEqual(0, DebrisDamage.ComputeMilliHp(1999, 5 * Kg, false));
        [Test] public void JustAboveThreshold_Damages() => Assert.Greater(DebrisDamage.ComputeMilliHp(2001, 5 * Kg, false), 0);

        [Test] public void ScalesWithMassTimesSpeed()
        {
            int a = DebrisDamage.ComputeMilliHp(3000, 2 * Kg, false);
            Assert.AreEqual(2 * a, DebrisDamage.ComputeMilliHp(3000, 4 * Kg, false));   // 2x mass
            Assert.AreEqual(2 * a, DebrisDamage.ComputeMilliHp(6000, 2 * Kg, false));   // 2x speed
            Assert.AreEqual(15000, a);                                                  // 2 kg × 3 m/s × 2.5 HP
        }

        [Test] public void CappedAt60PerCollision()
        {
            Assert.AreEqual(60000, DebrisDamage.ComputeMilliHp(50000, 100 * Kg, false));
            Assert.AreEqual(DebrisDamageConfig.Default.capMilliHp, DebrisDamage.ComputeMilliHp(int.MaxValue, int.MaxValue, false));
        }

        [Test] public void RestingContact_NoDamage()
        {
            Assert.AreEqual(0, DebrisDamage.ComputeMilliHp(0, 50 * Kg, false));
            Assert.AreEqual(0, DebrisDamage.ComputeMilliHp(10000, 50 * Kg, restingContact: true));
        }

        [Test] public void ConfigFromData()
        {
            var cfg = new DebrisDamageConfig { minSpeedMmPerS = 1000, milliHpPerKgMetrePerS = 1000, capMilliHp = 5000 };
            Assert.AreEqual(1500, DebrisDamage.ComputeMilliHp(1500, 1 * Kg, false, cfg));
            Assert.AreEqual(5000, DebrisDamage.ComputeMilliHp(9000, 1 * Kg, false, cfg));
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
