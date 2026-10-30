using Crashverse.Data;
using Crashverse.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Crashverse.Tests.EditMode
{
    /// <summary>Realistic v1 block -> piece mapping (models/Realistic_Scope_L1-10.md rows) and the fit/UV maths RealKit uses.</summary>
    public class RealPieceRulesEditModeTests
    {
        static BlockData B(string id, string mat, float x, float y, float z, string role = "", bool explosive = false) =>
            new BlockData { id = id, mat = mat, s = new[] { x, y, z }, role = role, explosive = explosive };

        [TestCase("rock", "brick", 4f, 1.2f, 4f, "island", null)]
        [TestCase("post", "wood", 0.5f, 2f, 0.5f, "support", "Kit_RealWood_Joint")]
        [TestCase("deck", "wood", 2.4f, 0.4f, 6f, "", "Kit_RealWood_Floor")]
        [TestCase("cabinL", "wood", 1.8f, 1.2f, 1.6f, "wall", "Kit_RealWood_Block")]
        [TestCase("roof", "wood", 2.6f, 0.6f, 2.6f, "roof", "Kit_RealRoof_Roof")]
        [TestCase("bellFrame", "wood", 0.3f, 1.2f, 0.8f, "", "Kit_RealWood_Wall")]
        [TestCase("wallF", "brick", 0.3f, 1.6f, 3.6f, "wall", "Kit_RealBrick_Wall")]
        [TestCase("slabL", "brick", 3.4f, 0.4f, 2f, "", "Kit_RealBrick_Floor")]
        [TestCase("ring0", "steel", 2.6f, 0.5f, 2.6f, "steel_joint", "Kit_RealSteel_Floor")]
        [TestCase("sign", "steel", 0.3f, 0.5f, 2.2f, "steel_joint", "Kit_RealSteel_Joint")]
        [TestCase("core", "steel", 0.4f, 1f, 1f, "core", "Kit_RealSteel_Wall")]
        [TestCase("braceL", "steel", 0.9f, 0.5f, 0.9f, "brace", "Kit_RealSteel_Block")]
        [TestCase("barrelDecor", "wood", 0.8f, 1.2f, 0.8f, "", "Kit_RealProps_Barrel")]
        public void Scope_rows_map_to_their_piece(string id, string mat, float x, float y, float z, string role, string piece)
        {
            Assert.AreEqual(piece, RealPieceRules.Classify(B(id, mat, x, y, z, role)));
        }

        [Test]
        public void Explosive_blocks_are_crates_and_every_piece_has_a_kit_and_tile()
        {
            Assert.AreEqual("Kit_RealProps_Crate", RealPieceRules.Classify(B("tnt", "wood", 1, 1, 1, "", true)));
            foreach (var p in RealPieceRules.Pieces)
            {
                var k = RealPieceRules.KitOf(p);
                Assert.Contains(k, new[] { "Brick", "Wood", "Steel", "Roof", "Props" }, p);
                Assert.Greater(RealPieceRules.TileM(k), 0.5f, p);
            }
        }

        [Test]
        public void Wall_thin_axis_goes_to_the_block_thin_axis_and_joint_long_axis_to_the_longest()
        {
            // wall piece 3 x 3 x 0.27 (thin z) onto a block 0.3 x 1.6 x 3.6 (thin x): block x takes piece z
            var m = RealPieceRules.AxisMap(new Vector3(3f, 3f, 0.27f), new Vector3(0.3f, 1.6f, 3.6f));
            Assert.AreEqual(2, m[0]);
            // joint 0.3 x 3 x 0.3 (long y) onto a beam 0.4 x 0.4 x 2.4 (long z)
            m = RealPieceRules.AxisMap(new Vector3(0.3f, 3f, 0.3f), new Vector3(0.4f, 0.4f, 2.4f));
            Assert.AreEqual(1, m[2]);
            // cube onto cube keeps y up
            m = RealPieceRules.AxisMap(new Vector3(1f, 1f, 1f), new Vector3(2f, 2f, 2f));
            Assert.AreEqual(1, m[1]);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, m);
        }

        [Test]
        public void Uv_is_metric_so_texel_density_survives_scaling()
        {
            // same 1 m step on a stretched block and a small one gives the same UV step (no stretching)
            var a = RealPieceRules.BoxUv(new Vector3(0f, 1f, 2f), Vector3.right, 1.75f);
            var b = RealPieceRules.BoxUv(new Vector3(5f, 1f, 3f), Vector3.right, 1.75f);
            Assert.AreEqual(1f / 1.75f, b.x - a.x, 1e-5f);
            Assert.AreEqual(a.y, b.y, 1e-5f);
            Assert.AreEqual(new Vector2(0.5f, 1f), RealPieceRules.BoxUv(new Vector3(1f, 9f, 2f), Vector3.up, 2f));
        }
    }
}
