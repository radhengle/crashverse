using System;
using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    /// <summary>Glass/Stone materials: enum stability, counts, debris multipliers, chapter gating.</summary>
    public class MaterialGlassStoneTests
    {
        [Test] public void MaterialType_ExistingValuesUnchanged_StoneAppended()
        {
            Assert.AreEqual(0, (int)MaterialType.Wood);  Assert.AreEqual(1, (int)MaterialType.Brick);
            Assert.AreEqual(2, (int)MaterialType.Steel); Assert.AreEqual(3, (int)MaterialType.Glass);
            Assert.AreEqual(4, (int)MaterialType.Terrain); Assert.AreEqual(5, (int)MaterialType.Stone);
            Assert.AreEqual(6, Enum.GetValues(typeof(MaterialType)).Length);
        }

        [Test] public void MaterialCounts_TracksStone_AndDominantCanBeStone()
        {
            var c = new MaterialCounts();
            c.Add(MaterialType.Stone, 3); c.Add(MaterialType.Glass, 2); c.Add(MaterialType.Wood);
            Assert.AreEqual(3, c.stone); Assert.AreEqual(6, c.Total);
            Assert.AreEqual(3, c.Get(MaterialType.Stone));
            Assert.AreEqual(MaterialType.Stone, c.Dominant);
        }

        [Test] public void MaterialCounts_Tie_GoesToLowerEnumValue()
        {
            var c = new MaterialCounts(); c.Add(MaterialType.Stone, 2); c.Add(MaterialType.Glass, 2);
            Assert.AreEqual(MaterialType.Glass, c.Dominant);
        }

        [Test] public void DebrisMultipliers_GlassAndStone()
        {
            Assert.AreEqual(1500, DebrisMaterialMult.Glass);
            Assert.AreEqual(900, DebrisMaterialMult.Stone);
            // 0.5 × 2 kg × 10 m/s over threshold = 10 HP base
            Assert.AreEqual(15000, DebrisDamage.ComputeMilliHp(12000, 2000, DebrisMaterialMult.Glass, false));
            Assert.AreEqual(9000, DebrisDamage.ComputeMilliHp(12000, 2000, DebrisMaterialMult.Stone, false));
            Assert.AreEqual(60000, DebrisDamage.ComputeMilliHp(int.MaxValue, int.MaxValue, DebrisMaterialMult.Glass, false)); // cap holds
        }

        [Test] public void Glass_NeonChapterAndLaterOnly()
        {
            Assert.IsFalse(MaterialRules.AllowedInChapter("glass", 1));
            Assert.IsFalse(MaterialRules.AllowedInChapter("glass", 2));
            Assert.IsTrue(MaterialRules.AllowedInChapter("glass", MaterialRules.NeonChapter));
            Assert.IsTrue(MaterialRules.AllowedInChapter("stone", 1));
            Assert.IsTrue(MaterialRules.AllowedInChapter("wood", 1));
        }
    }
}
