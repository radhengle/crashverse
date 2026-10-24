using System.IO;
using System.Linq;
using Crashverse.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            Directory.CreateDirectory(Dir);
            Profile($"{Dir}/VP_Global_Low.asset", full: false);
            Profile($"{Dir}/VP_Global.asset", full: true);
            Skybox($"{Dir}/M_Sky_Bright.mat");
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
            F("m_ShadowDistance", t.shadowDistance); I("m_ShadowCascadeCount", t.cascades); B("m_SoftShadowsSupported", t.softShadows);
            I("m_AdditionalLightsRenderingMode", t.additionalLights > 0 ? 1 : 0); I("m_AdditionalLightsPerObjectLimit", t.additionalLights);
            B("m_AdditionalLightShadowsSupported", false); B("m_UseSRPBatcher", t.srpBatcher);
            B("m_RequireOpaqueTexture", false); B("m_RequireDepthTexture", false); I("m_ColorGradingMode", 0); // LDR grading (mobile)
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(a);
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
            var tm = Get<Tonemapping>(p); tm.mode.Override(full ? TonemappingMode.ACES : TonemappingMode.Neutral);
            var ca = Get<ColorAdjustments>(p); ca.saturation.Override(full ? 18f : 12f); ca.contrast.Override(full ? 12f : 8f); ca.postExposure.Override(full ? 0.15f : 0.1f);
            if (full)
            {
                var b = Get<Bloom>(p); b.threshold.Override(1.0f); b.intensity.Override(0.6f); b.scatter.Override(0.6f); b.highQualityFiltering.Override(false);
                var v = Get<Vignette>(p); v.intensity.Override(0.18f); v.smoothness.Override(0.4f);
            }
            else
            {   // Low: no Bloom/Vignette at all (cheapest path)
                foreach (var c in p.components.ToArray()) if (c is Bloom || c is Vignette) { p.Remove(c.GetType()); Object.DestroyImmediate(c, true); }
            }
            EditorUtility.SetDirty(p);
        }

        static void Skybox(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_SunSize", 0.04f); m.SetFloat("_AtmosphereThickness", 0.8f); m.SetFloat("_Exposure", 1.35f);
            m.SetColor("_SkyTint", new Color(0.55f, 0.75f, 1f)); m.SetColor("_GroundColor", new Color(0.62f, 0.60f, 0.56f));
            EditorUtility.SetDirty(m);
        }
    }
}
