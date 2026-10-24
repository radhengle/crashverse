using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    /// <summary>EditMode: ImportedPhysicsHints.fractureClass → MaterialType (FractureClassRules). Engine-free.</summary>
    public class FractureClassRulesTests
    {
        [SetUp] public void SetUp() => FractureClassRules.ResetWarningsForTests();

        static MaterialType Map(string s) { Assert.AreEqual(FractureClassStatus.Mapped, FractureClassRules.Resolve(s, out var t), s); return t; }

        [Test] public void EveryHeroKitValue_Maps()
        {
            // Values in use in models/HeroBuildings/Kit_*_stats.json (3d-modeler/models-v3): Stone, Wood, Steel, Roof.
            Assert.AreEqual(MaterialType.Stone, Map("Stone"));
            Assert.AreEqual(MaterialType.Wood, Map("Wood"));
            Assert.AreEqual(MaterialType.Steel, Map("Steel"));
            Assert.AreEqual(MaterialType.Stone, Map("Roof"));   // terracotta tile roof → Stone (physics/FractureClass_Mapping.md)
        }

        [Test] public void EveryMaterialKindName_MapsToItself()
        {
            Assert.AreEqual(MaterialType.Brick, Map("Brick"));
            Assert.AreEqual(MaterialType.Glass, Map("Glass"));
            CollectionAssert.AreEqual(new[] { "Wood", "Brick", "Steel", "Glass", "Stone", "Roof" }, FractureClassRules.KnownClasses());
        }

        [TestCase("roof"), TestCase("ROOF"), TestCase("rOoF"), TestCase("  Roof "), TestCase("\tRoof\r\n")]
        public void CaseInsensitive_AndTrimmed(string s)
        {
            Assert.AreEqual(MaterialType.Stone, Map(s));
            Assert.IsTrue(FractureClassRules.IsRoof(s));
        }

        [TestCase("wood", MaterialType.Wood), TestCase(" STEEL", MaterialType.Steel), TestCase("stone ", MaterialType.Stone),
         TestCase("bRiCk", MaterialType.Brick), TestCase("GLASS", MaterialType.Glass)]
        public void CaseInsensitive_OtherClasses(string s, MaterialType expected) => Assert.AreEqual(expected, Map(s));

        [Test] public void CultureIndependent_TurkishAndInvariant()
        {
            var prev = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (var c in new[] { "tr-TR", "az-Latn-AZ", "lt-LT", "" })
                {
                    Thread.CurrentThread.CurrentCulture = c.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(c);
                    Assert.AreEqual(MaterialType.Stone, Map("ROOF"), c);
                    Assert.AreEqual(MaterialType.Steel, Map("steel"), c);
                    Assert.AreEqual(MaterialType.Wood, Map("WOOD"), c);
                }
            }
            finally { Thread.CurrentThread.CurrentCulture = prev; }
        }

        [TestCase(null), TestCase(""), TestCase("   "), TestCase("\t\n")]
        public void Empty_IsNotAuthored_FallbackNoWarning(string s)
        {
            var warnings = new List<string>();
            Assert.AreEqual(FractureClassStatus.Empty, FractureClassRules.ResolveWarnOnce(s, out var t, warnings.Add));
            Assert.AreEqual(FractureClassRules.DefaultFallback, t);
            Assert.AreEqual(MaterialType.Wood, FractureClassRules.DefaultFallback);
            Assert.AreEqual(FractureClassStatus.Empty, FractureClassRules.Resolve(s, out var t2, MaterialType.Brick));
            Assert.AreEqual(MaterialType.Brick, t2, "caller fallback is honoured");
            CollectionAssert.IsEmpty(warnings);
        }

        [TestCase("Marble"), TestCase("Terrain"), TestCase("Roofs"), TestCase("Ro of"), TestCase("Stone/Roof"), TestCase("ROOF\u0130")]
        public void Unknown_FallsBack(string s)
        {
            Assert.AreEqual(FractureClassStatus.Unknown, FractureClassRules.Resolve(s, out var t));
            Assert.AreEqual(FractureClassRules.DefaultFallback, t);
            Assert.AreEqual(FractureClassStatus.Unknown, FractureClassRules.Resolve(s, out var t2, MaterialType.Steel));
            Assert.AreEqual(MaterialType.Steel, t2);
            Assert.IsFalse(FractureClassRules.TryResolve(s, out _));
        }

        [Test] public void Unknown_WarnsOncePerDistinctValue()
        {
            var w = new List<string>();
            for (int i = 0; i < 5; i++) FractureClassRules.ResolveWarnOnce("Marble", out _, w.Add);
            FractureClassRules.ResolveWarnOnce(" marble ", out _, w.Add);   // same value after trim + case fold
            Assert.AreEqual(1, w.Count);
            StringAssert.Contains("'Marble'", w[0]);
            FractureClassRules.ResolveWarnOnce("Thatch", out _, w.Add);
            Assert.AreEqual(2, w.Count);
            FractureClassRules.ResolveWarnOnce("Roof", out _, w.Add);         // known: never warns
            Assert.AreEqual(2, w.Count);
        }

        [Test] public void Deterministic_RepeatedCallsSameResult()
        {
            foreach (var s in new[] { "Roof", "stone", "Wood", "x", "" })
            {
                var st = FractureClassRules.Resolve(s, out var a);
                for (int i = 0; i < 100; i++) { Assert.AreEqual(st, FractureClassRules.Resolve(s, out var b)); Assert.AreEqual(a, b); }
            }
        }

        [Test] public void NoNewEnumValues_SerializedValuesUnchanged()
        {
            Assert.AreEqual(6, Enum.GetValues(typeof(MaterialType)).Length);
            Assert.AreEqual(5, (int)MaterialType.Stone);
        }
    }
}
