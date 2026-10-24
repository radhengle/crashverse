using NUnit.Framework;

namespace Crashverse.VFX.Tests
{
    public class VfxRecipeTests
    {
        [Test] public void AllRecipesExistAndFitImpactLane()
        {
            foreach (var id in VfxIds.All)
            {
                var r = VfxRecipes.Get(id);
                Assert.AreEqual(id, r.Id, id);
                Assert.IsNotNull(r.Layers, id);
                for (int tier = 0; tier <= 2; tier++)
                {
                    int n = r.TotalParticles(tier);
                    Assert.Greater(n, 0, id);
                    Assert.LessOrEqual(n, VfxRules.ImpactLaneBudget(tier), id);
                }
            }
        }

        [Test] public void MuzzleFlash_ArtColoursAndLowTierDropsSparks()
        {
            var r = VfxRecipes.Get(VfxIds.MuzzleFlash);
            var sparks = System.Array.Find(r.Layers, l => l.Name == "sparks");
            CollectionAssert.Contains(sparks.Steps, 0xE0FBFFu);
            CollectionAssert.Contains(sparks.Steps, 0x22D3EEu);
            Assert.AreEqual(6, sparks.Count);            // animation/01
            Assert.IsTrue(sparks.LowTierOff);            // 01 Perf: Low 6 -> 0
            Assert.LessOrEqual(r.TotalParticles(1), 24); // 01 Perf: muzzle VFX ≤ 24 particles (Mid)
            var flash = System.Array.Find(r.Layers, l => l.Name == "flash");
            Assert.AreEqual(VfxSheet.MuzzleFlash, flash.Sheet);
            Assert.AreEqual(0.05f, flash.LifeMax, 1e-4f);   // 3 frames
        }

        [Test] public void DustColoursPerMaterial()
        {
            CollectionAssert.Contains(VfxRecipes.Get(VfxIds.DustWood).Layers[0].Steps, 0xCFA779u);
            CollectionAssert.Contains(VfxRecipes.Get(VfxIds.DustBrick).Layers[0].Steps, 0xD9A37Eu);
            CollectionAssert.Contains(System.Array.Find(VfxRecipes.Get(VfxIds.SparksSteel).Layers, l => l.Name == "sparks").Steps, 0xFFD27Au);
            Assert.AreEqual(VfxIds.GlassShards, VfxRecipes.ImpactFor(VfxMaterial.Glass, true));
        }

        [Test] public void ExplosionHasStepPuffsRingAndDebris()
        {
            var r = VfxRecipes.Get(VfxIds.Explosion);
            var fire = System.Array.Find(r.Layers, l => l.Name == "fireball");
            Assert.AreEqual(3, fire.Steps.Length);
            Assert.IsTrue(System.Array.Exists(r.Layers, l => l.Sheet == VfxSheet.Ring));
            Assert.IsTrue(System.Array.Exists(r.Layers, l => l.Sheet == VfxSheet.DebrisChunks));
        }

        [Test] public void ReduceMotion_FlashLayersCappedCountsUnchanged()
        {
            foreach (var id in VfxIds.All)
                foreach (var l in VfxRecipes.Get(id).Layers)
                {
                    var rm = VfxRecipes.ApplyReduceMotion(l, true);
                    Assert.AreEqual(l.Count, rm.Count, id + "/" + l.Name);
                    if (!l.IsFlash) continue;
                    Assert.LessOrEqual(rm.Alpha, 0.5f);
                    Assert.AreEqual(l.Intensity * 0.4f, rm.Intensity, 1e-4f);
                    Assert.GreaterOrEqual(rm.LifeMin, 0.1f);
                }
        }
    }

    public class DamageRulesTests
    {
        [Test] public void FlashCappedAndReduceMotion()
        {
            Assert.AreEqual(0.85f, DamageRules.FlashPeak(2, false), 1e-5f);
            Assert.AreEqual(0.85f * 0.4f, DamageRules.FlashPeak(2, true), 1e-5f);
            Assert.Less(DamageRules.FlashPeak(0, false), DamageRules.FlashPeak(1, false));
        }

        [Test] public void FlashTiming()
        {
            Assert.AreEqual(0.85f, DamageRules.FlashAt(0f, 0.85f, 2, false), 1e-5f);
            Assert.AreEqual(0f, DamageRules.FlashAt(2f / 60f, 0.85f, 2, false));      // 2 f strobe ends
            Assert.Greater(DamageRules.FlashAt(0.08f, 0.34f, 2, true), 0f);          // RM ≥100 ms ramp
            Assert.AreEqual(0f, DamageRules.FlashAt(0.1f, 0.34f, 2, true));
        }

        [Test] public void CrackStepMapping()
        {
            Assert.AreEqual(0, DamageRules.ShaderStep(0));
            Assert.AreEqual(1, DamageRules.ShaderStep(1));
            Assert.AreEqual(2, DamageRules.ShaderStep(2));
            Assert.AreEqual(2, DamageRules.ShaderStep(3));
            Assert.AreEqual(2, DamageRules.Advance(2, 1)); // never goes back
            Assert.AreEqual(0.2f, DamageRules.DarkenPerStep(VfxMaterial.Brick) * 2, 1e-5f);
            Assert.AreEqual(0x7A3326u, DamageRules.CrackColor(VfxMaterial.Brick));
            Assert.AreEqual(0x6B4226u, DamageRules.CrackColor(VfxMaterial.Wood));
            Assert.AreEqual(0x3A3F48u, DamageRules.CrackColor(VfxMaterial.Steel));
        }
    }

    // Shape-compatible fake of Crashverse.Sim.ImpactEvent (PR #18) so the adapter is tested without the physics branch.
    public enum FakeKind : byte { Impact, Crack, Break, JointSnap, CollapseStart, CollapseEnd }
    public enum FakeMat : byte { Wood, Brick, Steel, Glass, Terrain }
    public struct FakeVec { public float x, y, z; }
    public struct ImpactEvent
    {
        public FakeKind kind; public byte tier; public float impulse; public FakeMat materialTypeA, materialTypeB, dominantMaterial;
        public FakeVec position; public int pieceId, damageStep; public byte source;
    }

    public class ImpactAdapterTests
    {
        [Test] public void ReadsImpactEventShape()
        {
            var ev = new ImpactEvent { kind = FakeKind.Crack, tier = 2, materialTypeA = FakeMat.Terrain, materialTypeB = FakeMat.Glass,
                                       position = new FakeVec { x = 1, y = 2, z = 3 }, pieceId = 42, damageStep = 3 };
            Assert.IsTrue(ImpactEventAdapter.TryRead(ev, out var e));
            Assert.AreEqual(VfxImpact.KCrack, e.Kind); Assert.AreEqual(42, e.PieceId); Assert.AreEqual(2f, e.Y);
            Assert.AreEqual(VfxMaterial.Glass, VfxImpactRouting.Struck(e));
            var r = VfxImpactRouting.Route(e);
            Assert.AreEqual(2, r.DamageStep); Assert.IsNull(r.EffectId);
            Assert.IsFalse(ImpactEventAdapter.TryRead("nope", out _));
        }

        [Test] public void RoutingRules()
        {
            var light = new VfxImpact { Kind = VfxImpact.KImpact, Tier = 0, Source = VfxImpact.SrcDebris, MaterialA = VfxMaterial.Wood };
            Assert.IsNull(VfxImpactRouting.Route(light).EffectId);                      // debris rattle: no particles
            var cannon = new VfxImpact { Kind = VfxImpact.KImpact, Tier = 1, Source = VfxImpact.SrcCannon, MaterialA = VfxMaterial.Steel };
            var rc = VfxImpactRouting.Route(cannon);
            Assert.AreEqual(VfxIds.SparksSteel, rc.EffectId); Assert.IsTrue(rc.HitFlash);
            var brk = new VfxImpact { Kind = VfxImpact.KBreak, MaterialA = VfxMaterial.Brick };
            Assert.IsTrue(VfxImpactRouting.Route(brk).RateLimited);
            var end = new VfxImpact { Kind = VfxImpact.KCollapseEnd, Dominant = VfxMaterial.Wood };
            Assert.AreEqual(0xCFA779u, VfxImpactRouting.Route(end).Tint);
        }
    }

    public class BudgetAndLimiterTests
    {
        [Test] public void SpawnLimiter_ThreePer100ms()
        {
            var l = new SpawnLimiter();
            Assert.IsTrue(l.TryAcquire(0f)); Assert.IsTrue(l.TryAcquire(0.01f)); Assert.IsTrue(l.TryAcquire(0.02f));
            Assert.IsFalse(l.TryAcquire(0.05f));
            Assert.IsTrue(l.TryAcquire(0.101f));
        }

        [Test] public void Budget_OldestCulledFirst()
        {
            var b = new ParticleBudget(VfxRules.ImpactLaneBudget(1)); var culled = new int[8];
            Assert.AreEqual(150, b.Capacity);
            Assert.AreEqual(0, b.Request(1, 60, culled)); Assert.AreEqual(0, b.Request(2, 60, culled));
            Assert.AreEqual(1, b.Request(3, 60, culled)); Assert.AreEqual(1, culled[0]);
            Assert.AreEqual(120, b.Used);
            Assert.AreEqual(75, VfxRules.ImpactLaneBudget(0)); Assert.AreEqual(80, VfxRules.PowerUpLaneBudget(2));
        }
    }
}
