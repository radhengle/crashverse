using Crashverse.Data;
using Crashverse.Destruction;
using Crashverse.Gameplay;
using Crashverse.Level;
using NUnit.Framework;
using UnityEngine;

namespace Crashverse.Tests.Gameplay
{
    /// <summary>EditMode: VFX #31 hooks (pieceId = StableId.Of(blockId), DamageFeedback on every destructible).</summary>
    public class PieceFeedbackWiringTests
    {
        GameObject _root;
        [TearDown] public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        [Test] public void PieceId_MatchesPhysics20StableIdValues()
        {
            // Pinned to #20 StableId.Of (FNV-1a 32, masked, never 0/-1).
            Assert.AreEqual(204219580, PieceIds.Of("b1"));
            Assert.AreEqual(501293940, PieceIds.Of("L1_tower_03"));
            Assert.AreEqual(1, PieceIds.Of(""));
            Assert.AreEqual(1, PieceIds.Of(null));
            Assert.AreEqual(PieceIds.Of("b1"), PieceIds.Of("b1"));
            Assert.AreNotEqual(PieceIds.None, PieceIds.Of("x"));
        }

        [Test] public void PooledDebris_PieceIdStableAcrossReuse_AndClearedWhenPooled()
        {
            _root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _root.AddComponent<Rigidbody>();
            var piece = _root.AddComponent<DebrisPiece>();
            Assert.AreEqual(-1, piece.PieceId);

            int a = PieceIds.Of("blockA"), b = PieceIds.Of("blockB");
            piece.PieceId = a; piece.Activate(null, 50, new Crashverse.Sim.SimRng(1));
            Assert.AreEqual(a, piece.PieceId);
            piece.Deactivate();
            Assert.AreEqual(-1, piece.PieceId, "pooled chunk must not keep a stale id");
            piece.PieceId = b; piece.Activate(null, 50, new Crashverse.Sim.SimRng(1));
            Assert.AreEqual(b, piece.PieceId, "reuse takes the new source id");
            piece.Deactivate();
            piece.PieceId = a; piece.Activate(null, 50, new Crashverse.Sim.SimRng(1));
            Assert.AreEqual(a, piece.PieceId, "same blockId → same id after reuse");
        }

        [Test] public void VfxMaterialMapping()
        {
            Assert.AreEqual(0, PieceIds.VfxMaterialOf((int)MaterialKind.Wood, false, false));
            Assert.AreEqual(1, PieceIds.VfxMaterialOf((int)MaterialKind.Brick, false, false));
            Assert.AreEqual(2, PieceIds.VfxMaterialOf((int)MaterialKind.Steel, false, false));
            Assert.AreEqual(3, PieceIds.VfxMaterialOf((int)MaterialKind.Steel, true, false));
            Assert.AreEqual(5, PieceIds.VfxMaterialOf((int)MaterialKind.Wood, false, true));
            Assert.AreEqual(6, PieceIds.VfxMaterialOf((int)MaterialKind.Glass, false, false));
            Assert.AreEqual(7, PieceIds.VfxMaterialOf((int)MaterialKind.Stone, false, false));
            Assert.AreEqual((int)Crashverse.VFX.VfxMaterial.Stone, PieceIds.VfxMaterialOf((int)MaterialKind.Stone, false, false));
        }

        [Test] public void DamageFeedbackMaterialFromImportedFractureClass()
        {
            _root = new GameObject("Kit_Keep_Wall");
            Assert.AreEqual(Crashverse.VFX.VfxMaterial.Brick, Crashverse.VFX.DamageFeedback.MaterialFromHints(_root, Crashverse.VFX.VfxMaterial.Brick), "no hints: fallback");
            var h = _root.AddComponent<Crashverse.Import.ImportedPhysicsHints>();
            h.fractureClass = "Stone";
            Assert.AreEqual(Crashverse.VFX.VfxMaterial.Stone, Crashverse.VFX.DamageFeedback.MaterialFromHints(_root, Crashverse.VFX.VfxMaterial.Wood));
            Assert.AreEqual(0u, Crashverse.VFX.DamageFeedback.DustOverrideFromHints(_root), "stone: recipe dust");
            h.fractureClass = "Roof";
            Assert.AreEqual(Crashverse.VFX.VfxMaterial.Stone, Crashverse.VFX.DamageFeedback.MaterialFromHints(_root, Crashverse.VFX.VfxMaterial.Wood), "Roof uses Stone (PM)");
            Assert.AreEqual(0xD9A28Cu, Crashverse.VFX.DamageFeedback.DustOverrideFromHints(_root), "Roof: terracotta dust");
            h.fractureClass = "Thatch";
            Assert.AreEqual(Crashverse.VFX.VfxMaterial.Wood, Crashverse.VFX.DamageFeedback.MaterialFromHints(_root, Crashverse.VFX.VfxMaterial.Wood), "unmapped class keeps fallback");
            Assert.AreEqual(0u, Crashverse.VFX.DamageFeedback.DustOverrideFromHints(_root));
            Object.DestroyImmediate(h);
            Assert.AreEqual(0u, Crashverse.VFX.DamageFeedback.DustOverrideFromHints(_root), "no hints: no override");
        }

        [Test] public void AllBuiltDestructibles_GetDamageFeedbackWhenVfxPresent()
        {
            _root = new GameObject("LB");
            var lb = _root.AddComponent<LevelBuilder>();
            var layout = new LayoutData { id = "t", blocks = new[] {
                new BlockData { id = "w1", mat = "wood",  p = new float[]{0,0,0}, s = new float[]{1,1,1} },
                new BlockData { id = "b1", mat = "brick", p = new float[]{0,1,0}, s = new float[]{1,1,1}, sup = new[]{"w1"} },
                new BlockData { id = "s1", mat = "steel", p = new float[]{0,2,0}, s = new float[]{1,1,1}, sup = new[]{"b1"}, joint = true },
            }};
            lb.Build(layout, Vector3.zero, new RuleModifiers());
            Assert.AreEqual(3, lb.Blocks.Count);
            foreach (var blk in lb.Blocks)
            {
#if CRASHVERSE_VFX_DAMAGE
                var fb = blk.GetComponent<Crashverse.VFX.DamageFeedback>();
                Assert.IsNotNull(fb, blk.blockId);
                Assert.AreEqual(PieceIds.Of(blk.blockId), fb.pieceId);
                Assert.IsTrue(Crashverse.VFX.DamageFeedback.TryGet(fb.pieceId, out var reg) && reg == fb);
#else
                Assert.IsFalse(PieceFeedbackWiring.Enabled);
#endif
            }
        }
    }
}
