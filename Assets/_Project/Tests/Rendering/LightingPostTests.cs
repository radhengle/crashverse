using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crashverse.Rendering.Tests
{
    public class LightingPostTests
    {
        [Test]
        public void UrpTierAssets_MatchRenderTiers()
        {
            foreach (var t in RenderTiers.All)
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"Assets/_Project/Settings/URP/URP_{t.name}.asset");
                Assert.NotNull(a, $"URP_{t.name} missing (UrpSetup, PR #23)");
                Assert.AreEqual(t.hdr, a.supportsHDR, t.name + " HDR");
                Assert.AreEqual(t.msaa, a.msaaSampleCount, t.name + " MSAA");
                Assert.AreEqual(t.renderScale, a.renderScale, 1e-3f, t.name + " renderScale");
                Assert.AreEqual(t.shadowDistance, a.shadowDistance, 1e-3f, t.name + " shadowDistance");
                Assert.AreEqual(t.cascades, a.shadowCascadeCount, t.name + " cascades");
                Assert.AreEqual(t.mainShadows, a.supportsMainLightShadows, t.name + " main shadows");
                Assert.AreEqual(t.srpBatcher, a.useSRPBatcher, t.name + " SRP batcher");
                Assert.AreEqual(t.additionalLights, a.maxAdditionalLightsCount, t.name + " additional lights");
            }
        }

        [Test]
        public void LowTier_IsCheapest()
        {
            var low = RenderTiers.All[0];
            Assert.IsFalse(low.hdr || low.bloom || low.vignette || low.mainShadows || low.additionalLights > 0);
            Assert.AreEqual(1, low.msaa);
        }

        [Test]
        public void VolumeProfiles_HaveExpectedEffects()
        {
            var full = Resources.Load<VolumeProfile>(RenderTiers.VolumeFullResource);
            var low = Resources.Load<VolumeProfile>(RenderTiers.VolumeLowResource);
            Assert.NotNull(full, "VP_Global missing"); Assert.NotNull(low, "VP_Global_Low missing");
            Assert.IsTrue(full.Has<Bloom>() && full.Has<Tonemapping>() && full.Has<ColorAdjustments>() && full.Has<Vignette>());
            Assert.IsTrue(low.Has<Tonemapping>() && low.Has<ColorAdjustments>());
            Assert.IsFalse(low.Has<Bloom>() || low.Has<Vignette>(), "Low must skip Bloom/Vignette");
            Assert.IsFalse(full.Has<DepthOfField>() || full.Has<MotionBlur>(), "no expensive effects");
        }

        [Test]
        public void Skybox_ShipsInResources()
        {
            var m = Resources.Load<Material>(RenderTiers.SkyboxResource);
            Assert.NotNull(m); Assert.AreEqual("Skybox/Procedural", m.shader.name);
        }

        [Test]
        public void TierLookup_MapsQualityNames()
        {
            Assert.AreEqual(0, RenderTiers.IndexFor("Low")); Assert.AreEqual(1, RenderTiers.IndexFor("Mid")); Assert.AreEqual(2, RenderTiers.IndexFor("High"));
        }
    }
}
