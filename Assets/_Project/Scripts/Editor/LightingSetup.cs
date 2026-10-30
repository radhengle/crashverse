using System.IO;
using System.Linq;
using Crashverse.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.Rendering;

namespace Crashverse.EditorTools
{
    /// <summary>Creates/updates the global post Volume profiles (Low + full) and the bright skybox material under Resources/Lighting,
    /// and applies RenderTiers to URP_{Low,Mid,High} (created by UrpSetup, PR #23). Idempotent; run from BuildScript after UrpSetup.</summary>
    [InitializeOnLoad]
    public static class LightingSetup
    {
        const string Dir = "Assets/_Project/Resources/Lighting";
        static LightingSetup() { EditorApplication.delayCall += () => Ensure(); }

        [MenuItem("Crashverse/Rendering/Setup Lighting + Post")]
        public static void Ensure()
        {
            UrpSetup.Ensure();
            for (int i = 0; i < RenderTiers.All.Length; i++)
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"Assets/_Project/Settings/URP/URP_{RenderTiers.All[i].name}.asset");
                if (a) ApplyTier(a, RenderTiers.All[i]);
            }
            ApplyQualityLevels();
            Directory.CreateDirectory(Dir);
            Profile($"{Dir}/VP_Global_Low.asset", full: false);
            Profile($"{Dir}/VP_Global.asset", full: true);
            Skybox($"{Dir}/M_Sky_Bright.mat");
            Ssao();
            AssetDatabase.SaveAssets();
        }

        public static void ApplyTier(UniversalRenderPipelineAsset a, RenderTier t)
        {
            var so = new SerializedObject(a);
            void B(string p, bool v) { var x = so.FindProperty(p); if (x != null) x.boolValue = v; else Debug.LogWarning("[LightingSetup] no URP field " + p); }
            void I(string p, int v) { var x = so.FindProperty(p); if (x != null) x.intValue = v; else Debug.LogWarning("[LightingSetup] no URP field " + p); }
            void F(string p, float v) { var x = so.FindProperty(p); if (x != null) x.floatValue = v; else Debug.LogWarning("[LightingSetup] no URP field " + p); }
            B("m_SupportsHDR", t.hdr); I("m_MSAA", t.msaa); F("m_RenderScale", t.renderScale);
            I("m_MainLightRenderingMode", 1); B("m_MainLightShadowsSupported", t.mainShadows); I("m_MainLightShadowmapResolution", t.shadowResolution);
            F("m_ShadowDistance", t.shadowDistance); I("m_ShadowCascadeCount", t.cascades); F("m_Cascade2Split", t.cascade2Split); B("m_SoftShadowsSupported", t.softShadows);
            I("m_AdditionalLightsRenderingMode", t.additionalLights > 0 ? 1 : 0); I("m_AdditionalLightsPerObjectLimit", t.additionalLights);
            B("m_AdditionalLightShadowsSupported", false); B("m_UseSRPBatcher", t.srpBatcher);
            B("m_RequireOpaqueTexture", false); B("m_RequireDepthTexture", false); I("m_ColorGradingMode", 0); // LDR grading (mobile)
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(a);
        }

        /// <summary>Realistic PBR budgets into each QualitySettings level (texture mip limit, anisotropic, LOD bias / max LOD, built-in
        /// shadow mirror of the URP values). Never touches vSyncCount or anything frame-rate/physics related (QualityController owns those).</summary>
        public static void ApplyQualityLevels()
        {
            int keep = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            for (int q = 0; q < names.Length; q++)
            {
                QualitySettings.SetQualityLevel(q, false);
                var t = RenderTiers.All[RenderTiers.IndexFor(names[q])];
                QualitySettings.globalTextureMipmapLimit = t.textureMipLimit;
                QualitySettings.anisotropicFiltering = t.anisotropic;
                QualitySettings.lodBias = t.lodBias;
                QualitySettings.maximumLODLevel = t.maximumLODLevel;
                QualitySettings.shadows = t.mainShadows ? (t.softShadows ? UnityEngine.ShadowQuality.All : UnityEngine.ShadowQuality.HardOnly) : UnityEngine.ShadowQuality.Disable;
                QualitySettings.shadowDistance = t.shadowDistance;
                QualitySettings.shadowCascades = t.cascades;
                QualitySettings.shadowCascade2Split = t.cascade2Split;
            }
            QualitySettings.SetQualityLevel(keep, false);
        }

        static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet<T>(out var c)) { c = p.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, p); }
            c.active = true; return c;
        }

        static void Profile(string path, bool full)
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
            // ACES + warm dramatic grade on every tier (RenderTiers). Same grading on Low and full so the palette reads the same.
            var tm = Get<Tonemapping>(p); tm.mode.Override(RenderTiers.Tonemapper);
            var ca = Get<ColorAdjustments>(p); ca.saturation.Override(RenderTiers.PostSaturation); ca.contrast.Override(RenderTiers.PostContrast); ca.postExposure.Override(RenderTiers.PostExposure);
            ca.colorFilter.Override(RenderTiers.PostColorFilter);
            var wb = Get<WhiteBalance>(p); wb.temperature.Override(RenderTiers.WhiteBalanceTemperature); wb.tint.Override(RenderTiers.WhiteBalanceTint);
            var st = Get<SplitToning>(p); st.shadows.Override(RenderTiers.SplitShadows); st.highlights.Override(RenderTiers.SplitHighlights); st.balance.Override(RenderTiers.SplitBalance);
            if (full)
            {   // HDR threshold 1.1: only emissive Core/FX bloom; lit sandstone, steel and sky stay below it.
                var b = Get<Bloom>(p); b.threshold.Override(RenderTiers.BloomThreshold); b.intensity.Override(RenderTiers.BloomIntensity); b.scatter.Override(RenderTiers.BloomScatter); b.highQualityFiltering.Override(false);
                var v = Get<Vignette>(p); v.intensity.Override(RenderTiers.VignetteIntensity); v.smoothness.Override(0.4f);
            }
            else
            {   // Low: no Bloom/Vignette at all (cheapest path)
                foreach (var c in p.components.ToArray()) if (c is Bloom || c is Vignette) { p.Remove(c.GetType()); Object.DestroyImmediate(c, true); }
            }
            EditorUtility.SetDirty(p);
        }

        /// <summary>Mobile-safe SSAO renderer feature on the shared URP renderer: half resolution, depth source, low samples, after
        /// opaque. LightingRig switches it off on Low. The feature type is looked up by name so a URP update can't break the build.</summary>
        static void Ssao()
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Project/Settings/URP/URP_Renderer.asset");
            var type = System.Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
            if (rd == null || type == null) { Debug.LogWarning("[LightingSetup] SSAO skipped (renderer or feature type missing)"); return; }
            var f = rd.rendererFeatures.FirstOrDefault(x => x != null && x.GetType() == type);
            if (f == null)
            {
                f = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type); f.name = "SSAO";
                AssetDatabase.AddObjectToAsset(f, rd);
                rd.rendererFeatures.Add(f);
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long id))
                {
                    var rso = new SerializedObject(rd); var map = rso.FindProperty("m_RendererFeatureMap");
                    if (map != null) { map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id; rso.ApplyModifiedPropertiesWithoutUndo(); }
                }
            }
            ResourceReloader.TryReloadAllNullIn(f, UniversalRenderPipelineAsset.packagePath);
            var so = new SerializedObject(f);
            void Set(string n, System.Action<SerializedProperty> a) { var x = so.FindProperty("m_Settings." + n); if (x != null) a(x); else Debug.LogWarning("[LightingSetup] no SSAO field " + n); }
            Set("Downsample", x => x.boolValue = true); Set("AfterOpaque", x => x.boolValue = true);
            Set("Source", x => x.enumValueIndex = 0); Set("NormalSamples", x => x.enumValueIndex = 0);
            Set("Intensity", x => x.floatValue = RenderTiers.SsaoIntensity); Set("Radius", x => x.floatValue = RenderTiers.SsaoRadius);
            Set("DirectLightingStrength", x => x.floatValue = RenderTiers.SsaoDirectLighting);
            so.ApplyModifiedPropertiesWithoutUndo(); f.SetActive(true);
            EditorUtility.SetDirty(f); EditorUtility.SetDirty(rd);
        }

        static void Skybox(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(m, path); }
            // Style board v3 §6: sky #4FB8FF fading to a warm #FFE3A3 horizon (Procedural: tint drives the zenith, ground colour the
            // lower hemisphere/horizon band), bright exposure.
            m.SetFloat("_SunSize", 0.04f); m.SetFloat("_AtmosphereThickness", 0.7f); m.SetFloat("_Exposure", 1.3f);
            m.SetColor("_SkyTint", RenderTiers.SkyTop); m.SetColor("_GroundColor", RenderTiers.SkyHorizon);
            EditorUtility.SetDirty(m);
        }
    }
}
