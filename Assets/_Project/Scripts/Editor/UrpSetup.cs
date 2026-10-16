using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.Rendering;

namespace Crashverse.EditorTools
{
    /// <summary>
    /// BUG-23: the project had URP installed but no pipeline asset assigned, so procedural renderers drew with no/Built-in
    /// materials (magenta on device). Creates URP pipeline assets with mobile tiers (one per Quality level) and assigns
    /// them in Graphics and every Quality level. Runs on editor load and before every build (BuildScript), idempotent.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpSetup
    {
        const string Dir = "Assets/_Project/Settings/URP";
        static UrpSetup() { EditorApplication.delayCall += () => Ensure(); }

        struct Tier { public string name; public int msaa; public float scale; public float shadowDist; public int cascades; public bool hdr; }
        static readonly Tier[] Tiers =
        {
            new Tier { name = "Low",  msaa = 1, scale = 0.8f,  shadowDist = 20f, cascades = 1, hdr = false },
            new Tier { name = "Mid",  msaa = 2, scale = 0.9f,  shadowDist = 35f, cascades = 1, hdr = false },
            new Tier { name = "High", msaa = 4, scale = 1.0f,  shadowDist = 50f, cascades = 2, hdr = false },
        };

        public static UniversalRenderPipelineAsset Ensure()
        {
            Directory.CreateDirectory(Dir);
            var rdPath = Dir + "/URP_Renderer.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
            if (rd == null) { rd = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(rd, rdPath); }
            // dev-build-6 attempt 1 rendered black: a CreateInstance'd renderer has null shader/post-process resources in batchmode.
            ResourceReloader.TryReloadAllNullIn(rd, UniversalRenderPipelineAsset.packagePath);
            EditorUtility.SetDirty(rd);

            var assets = new UniversalRenderPipelineAsset[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
            {
                var t = Tiers[i]; var path = $"{Dir}/URP_{t.name}.asset";
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (a == null) { a = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(a, path); }
                ResourceReloader.TryReloadAllNullIn(a, UniversalRenderPipelineAsset.packagePath);
                a.msaaSampleCount = t.msaa; a.renderScale = t.scale; a.shadowDistance = t.shadowDist; a.shadowCascadeCount = t.cascades; a.supportsHDR = t.hdr;
                EditorUtility.SetDirty(a); assets[i] = a;
            }

            var mid = assets[1];
            if (GraphicsSettings.defaultRenderPipeline != mid) GraphicsSettings.defaultRenderPipeline = mid;
            int keep = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            for (int q = 0; q < names.Length; q++)
            {
                QualitySettings.SetQualityLevel(q, false);
                var want = names[q].ToLowerInvariant().Contains("low") ? assets[0] : names[q].ToLowerInvariant().Contains("high") ? assets[2] : assets[1];
                if (QualitySettings.renderPipeline != want) QualitySettings.renderPipeline = want;
            }
            QualitySettings.SetQualityLevel(keep, false);
            AssetDatabase.SaveAssets();
            return mid;
        }
    }
}
