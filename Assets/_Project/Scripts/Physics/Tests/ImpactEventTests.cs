using System.Collections.Generic;
using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    public class ImpactClassifierTests
    {
        static readonly ImpactTierThresholds D = ImpactTierThresholds.Default;

        [Test] public void Tier_Boundaries()
        {
            Assert.AreEqual(ImpactTier.Light, ImpactClassifier.Tier(0f, D));
            Assert.AreEqual(ImpactTier.Light, ImpactClassifier.Tier(39.99f, D));
            Assert.AreEqual(ImpactTier.Medium, ImpactClassifier.Tier(40f, D));
            Assert.AreEqual(ImpactTier.Medium, ImpactClassifier.Tier(150f, D));
            Assert.AreEqual(ImpactTier.Heavy, ImpactClassifier.Tier(150.01f, D));
        }

        [Test] public void Tier_NaNAndNegative_AreLight()
        {
            Assert.AreEqual(ImpactTier.Light, ImpactClassifier.Tier(float.NaN, D));
            Assert.AreEqual(ImpactTier.Light, ImpactClassifier.Tier(-5f, D));
        }

        [Test] public void Tier_UsesDataThresholds()
        {
            var t = D; t.mediumMinImpulse = 10f; t.heavyMinImpulse = 20f;
            Assert.AreEqual(ImpactTier.Medium, ImpactClassifier.Tier(15f, t));
            Assert.AreEqual(ImpactTier.Heavy, ImpactClassifier.Tier(25f, t));
        }

        [Test] public void Size_FromMass()
        {
            Assert.AreEqual(SizeClass.S, ImpactClassifier.Size(1f, D));
            Assert.AreEqual(SizeClass.M, ImpactClassifier.Size(5f, D));
            Assert.AreEqual(SizeClass.L, ImpactClassifier.Size(200f, D));
        }

        [Test] public void Defaults_AreValid() => Assert.IsTrue(D.IsValid);
    }

    public class ImpactOrderTests
    {
        static ImpactEvent E(ImpactKind k, int piece, int cascade = 0)
            => new ImpactEvent { tick = 5, kind = k, pieceId = piece, cascadeId = cascade };

        static List<ImpactEvent> Flush(IEnumerable<ImpactEvent> input)
        {
            var b = new ImpactEventBuffer(); foreach (var e in input) b.Add(e);
            var outp = new List<ImpactEvent>(); b.Flush(outp.Add); return outp;
        }

        [Test] public void Order_IsIndependentOfCallbackOrder()
        {
            var a = new[] { E(ImpactKind.Impact, 7), E(ImpactKind.Break, 3, 1), E(ImpactKind.CollapseStart, -1, 1), E(ImpactKind.Crack, 2), E(ImpactKind.CollapseEnd, -1, 1) };
            var b = new[] { a[4], a[3], a[0], a[2], a[1] };
            var ra = Flush(a); var rb = Flush(b);
            for (int i = 0; i < ra.Count; i++) { Assert.AreEqual(ra[i].kind, rb[i].kind); Assert.AreEqual(ra[i].pieceId, rb[i].pieceId); }
            Assert.AreEqual(ImpactKind.CollapseStart, ra[0].kind);
            Assert.AreEqual(ImpactKind.CollapseEnd, ra[ra.Count - 1].kind);
        }

        [Test] public void Order_SamePiece_KeepsInsertionOrder()
        {
            var r = Flush(new[] { new ImpactEvent { tick = 1, pieceId = 4, impulse = 1 }, new ImpactEvent { tick = 1, pieceId = 4, impulse = 2 } });
            Assert.AreEqual(1f, r[0].impulse); Assert.AreEqual(2f, r[1].impulse);
        }

        [Test] public void Flush_Clears()
        {
            var b = new ImpactEventBuffer(); b.Add(E(ImpactKind.Impact, 1)); b.Flush(_ => { });
            Assert.AreEqual(0, b.Count);
        }
    }

    public class CascadeAndHitStopTests
    {
        [Test] public void Cascade_IdsAndCounts_Replay()
        {
            var c = new CascadeTracker();
            int id = c.Begin(); Assert.AreEqual(1, id);
            Assert.AreEqual(1, c.RecordBreak(id)); Assert.AreEqual(2, c.RecordBreak(id));
            Assert.AreEqual(2, c.End(id)); Assert.IsFalse(c.IsActive(id));
            c.Reset(); Assert.AreEqual(1, c.Begin());
        }

        [Test] public void HitStop_Defaults_AndReduceMotion()
        {
            Assert.AreEqual(0, HitStopRule.Millis(ImpactTier.Light, 0, 40, 80, 80, false));
            Assert.AreEqual(40, HitStopRule.Millis(ImpactTier.Medium, 0, 40, 80, 80, false));
            Assert.AreEqual(80, HitStopRule.Millis(ImpactTier.Heavy, 0, 40, 200, 80, false)); // clamped to max
            Assert.AreEqual(0, HitStopRule.Millis(ImpactTier.Heavy, 0, 40, 80, 80, true));
        }
    }
}
