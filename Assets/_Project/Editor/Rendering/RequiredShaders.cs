using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Rendering.Editor
{
    /// <summary>
    /// Every shader the game resolves at runtime (Shader.Find or code-built materials) must be in the player build, or it is
    /// stripped and renders magenta on device. This keeps GraphicsSettings "Always Included Shaders" in sync before every build
    /// (and on demand via the menu). Coordinate additions with VFX (Crashverse/* shaders, PR #2).
    /// </summary>
    public class RequiredShaders : IPreprocessBuildWithReport
    {
        public static readonly string[] Names =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "Sprites/Default",              // AimController LineRenderer (Shader.Find)
            "Crashverse/GlossyPBR",
            "Crashverse/GlossyToon",
            "Crashverse/VfxParticle",
        };

        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report)
        {
            var missing = Ensure();
            if (missing.Count > 0)
                throw new BuildFailedException("Required shaders not found (would render magenta): " + string.Join(", ", missing));
        }

        [MenuItem("Crashverse/Rendering/Sync Always-Included Shaders")]
        static void Menu() { var m = Ensure(); Debug.Log(m.Count == 0 ? "[Rendering] shaders in sync" : "[Rendering] missing: " + string.Join(", ", m)); }

        /// <summary>Adds any missing required shader to Always Included. Returns names that could not be found.</summary>
        public static List<string> Ensure()
        {
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            var present = new HashSet<Shader>();
            for (int i = 0; i < arr.arraySize; i++) { var s = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader; if (s) present.Add(s); }
            var missing = new List<string>();
            foreach (var n in Names)
            {
                var s = Shader.Find(n);
                if (s == null) { missing.Add(n); continue; }
                if (present.Contains(s)) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = s;
                present.Add(s);
            }
            if (so.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssets();
            return missing;
        }

        public static HashSet<Shader> AlwaysIncluded()
        {
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var arr = new SerializedObject(gs).FindProperty("m_AlwaysIncludedShaders");
            return new HashSet<Shader>(Enumerable.Range(0, arr.arraySize)
                .Select(i => arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader).Where(s => s != null));
        }
    }
}
