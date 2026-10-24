using System;
using System.Text.RegularExpressions;
using Crashverse.Destruction;
using Crashverse.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crashverse.Tests.Gameplay
{
    /// <summary>EditMode: MaterialDefinition.FromFractureClass (ImportedPhysicsHints.fractureClass → MaterialKind).</summary>
    public class MaterialFractureClassTests
    {
        [SetUp] public void SetUp() => FractureClassRules.ResetWarningsForTests();

        [TestCase("Stone", MaterialKind.Stone), TestCase("Wood", MaterialKind.Wood), TestCase("Steel", MaterialKind.Steel),
         TestCase("Roof", MaterialKind.Stone), TestCase("Brick", MaterialKind.Brick), TestCase("Glass", MaterialKind.Glass),
         TestCase(" roof ", MaterialKind.Stone), TestCase("STONE", MaterialKind.Stone), TestCase("wood\n", MaterialKind.Wood)]
        public void KnownValues_Map(string s, MaterialKind expected)
        {
            Assert.AreEqual(expected, MaterialDefinition.FromFractureClass(s));
            Assert.AreEqual(expected, MaterialDefinition.FromFractureClass(s, MaterialKind.Brick), "fallback unused for known values");
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(null), TestCase(""), TestCase("  ")]
        public void Empty_FallbackSilently(string s)
        {
            Assert.AreEqual(MaterialKind.Wood, MaterialDefinition.FromFractureClass(s));
            Assert.AreEqual(MaterialKind.Steel, MaterialDefinition.FromFractureClass(s, MaterialKind.Steel));
            LogAssert.NoUnexpectedReceived();
        }

        [Test] public void Unknown_FallbackWithSingleWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Unknown fractureClass 'Thatch'.*MaterialKind fallback: Wood"));
            Assert.AreEqual(MaterialKind.Wood, MaterialDefinition.FromFractureClass("Thatch"));
            Assert.AreEqual(MaterialKind.Wood, MaterialDefinition.FromFractureClass("thatch "));
            Assert.AreEqual(MaterialKind.Brick, MaterialDefinition.FromFractureClass("THATCH", MaterialKind.Brick));
            LogAssert.NoUnexpectedReceived();   // one warning only
        }

        [Test] public void RoofKind_IsStone_AndEventTypeIsStone()
        {
            var kind = MaterialDefinition.FromFractureClass("Roof");
            Assert.AreEqual(MaterialKind.Stone, kind);
            var def = MaterialDefinition.CreateRuntime(kind);
            try
            {
                Assert.AreEqual(DebrisMaterialMult.Stone, def.debrisDamageTakenPermille);
                Assert.AreEqual("stone", MaterialDefinition.KeyOf(kind));
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        [Test] public void KindOf_CoversEveryMaterialType()
        {
            foreach (MaterialType t in Enum.GetValues(typeof(MaterialType)))
            {
                var k = MaterialDefinition.KindOf(t, MaterialKind.Brick);
                if (t == MaterialType.Terrain) Assert.AreEqual(MaterialKind.Brick, k);
                else Assert.AreEqual(t.ToString(), k.ToString());
            }
            Assert.AreEqual(5, Enum.GetValues(typeof(MaterialKind)).Length, "no new MaterialKind value");
        }
    }
}
