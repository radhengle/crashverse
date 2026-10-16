#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Crashverse.EditorTools.Import
{
    /// <summary>
    /// Single source of truth for model naming, budgets and import policy (VFX & Tech Art, vfx/IMPORT_PIPELINE.md).
    /// Pure C# (no UnityEditor calls) so the policy is easy to review and unit test.
    /// Conventions follow models/README.md (3d-modeler/models-v1) and the v3 build scripts (3d-modeler/models-v3).
    /// </summary>
    public static class ImportConventions
    {
        public const string ModelsRoot = "Assets/_Project/Models/";
        public const string PrefabRoot = "Assets/_Project/Prefabs/Models";
        public const string ShaderName = "Crashverse/GlossyPBR";
        public const string FragmentTag = "Fragment";          // used only if defined in TagManager (Developer-owned)

        public enum AssetClass { Hero, Environment, Kit, KitFractured, Unknown }

        public static bool InScope(string assetPath) => assetPath != null && assetPath.Replace('\\', '/').StartsWith(ModelsRoot, StringComparison.Ordinal);

        public static string FileStem(string assetPath) => Path.GetFileNameWithoutExtension(assetPath);

        public static AssetClass Classify(string assetPath)
        {
            string n = StripLodSuffix(FileStem(assetPath));
            if (n.StartsWith("Kit_", StringComparison.Ordinal)) return n.EndsWith("_Fractured", StringComparison.Ordinal) ? AssetClass.KitFractured : AssetClass.Kit;
            if (n.StartsWith("Island_", StringComparison.Ordinal) || n.StartsWith("Env_", StringComparison.Ordinal)) return AssetClass.Environment;
            if (n.StartsWith("Wrecker", StringComparison.Ordinal) || n.StartsWith("Hero_", StringComparison.Ordinal)) return AssetClass.Hero;
            return AssetClass.Unknown;
        }

        /// <summary>Static (lightmapped, UV2 generated): island/environment and intact kit pieces. Fragments and the Wrecker move.</summary>
        public static bool IsStatic(AssetClass c) => c == AssetClass.Environment || c == AssetClass.Kit;

        /// <summary>Only files ending in _Rig or _Anim keep animation; every prop imports with animation off.</summary>
        public static bool WantsAnimation(string assetPath)
        {
            string n = FileStem(assetPath);
            return n.EndsWith("_Rig", StringComparison.Ordinal) || n.EndsWith("_Anim", StringComparison.Ordinal);
        }

        // ---------- LOD ----------
        static readonly Regex LodRx = new Regex(@"^(?<base>.+?)_LOD(?<lvl>[0-2])$", RegexOptions.Compiled);

        /// <summary>Returns LOD level 0-2 for names ending _LOD0/_LOD1/_LOD2, else -1.</summary>
        public static int LodLevel(string name, out string baseName)
        {
            var m = LodRx.Match(name ?? "");
            if (m.Success) { baseName = m.Groups["base"].Value; return m.Groups["lvl"].Value[0] - '0'; }
            baseName = name; return -1;
        }
        public static string StripLodSuffix(string name) { LodLevel(name, out var b); return b; }

        /// <summary>Screen-relative transition heights (LOD i is used while the object covers more than value i of screen height).
        /// Last entry is the cull height. Tuned for 6" phones at 720-1080p, landscape, orthographic-ish 35-45 deg FOV.</summary>
        public static float[] LodHeights(AssetClass c)
        {
            switch (c)
            {
                case AssetClass.Hero: return new[] { 0.30f, 0.12f, 0.02f };        // Wrecker always near camera; almost never culled
                case AssetClass.Environment: return new[] { 0.45f, 0.18f, 0.005f }; // island fills screen; tiny cull
                case AssetClass.Kit:
                case AssetClass.KitFractured: return new[] { 0.20f, 0.08f, 0.015f };
                default: return new[] { 0.25f, 0.10f, 0.02f };
            }
        }

        // ---------- colliders, sockets, fragments ----------
        /// <summary>UCX_&lt;Target&gt;[_##] (models README) or &lt;Target&gt;_COL[_##]. Returns target object name or null.
        /// Fragment hulls are exactly UCX_&lt;Piece&gt;_frag_## and keep the full name (UCX_Wall_frag_03 -> Wall_frag_03);
        /// any other UCX_ name drops one trailing _## hull index (UCX_Carriage_01 -> Carriage).</summary>
        public static string ColliderTarget(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (name.StartsWith("UCX_", StringComparison.Ordinal))
            {
                string t = name.Substring(4);
                return FragRx.IsMatch(t) ? t : Regex.Replace(t, @"_\d{2}$", "");
            }
            var m = Regex.Match(name, @"^(?<t>.+?)_COL(_\d{2})?$");
            return m.Success ? m.Groups["t"].Value : null;
        }

        /// <summary>Socket names the gameplay/VFX code looks up. v3 uses Socket_X; v1 used Muzzle_Spawn.</summary>
        public static readonly string[] HeroSockets = { "Muzzle", "Core", "Module", "Booster", "WheelDust" };

        public static string SocketName(string objName)
        {
            if (string.IsNullOrEmpty(objName)) return null;
            if (objName.StartsWith("Socket_", StringComparison.Ordinal)) return objName.Substring(7);
            if (objName == "Muzzle_Spawn") return "Muzzle";
            return null;
        }

        static readonly Regex FragRx = new Regex(@"_frag_\d{2}$", RegexOptions.Compiled);
        /// <summary>&lt;Piece&gt;_frag_## (optionally _LODn). Hulls (UCX_), joints (JNT_) and anchors (ANCHOR_) that name a fragment are not fragments.</summary>
        public static bool IsFragment(string name) => name != null && !name.StartsWith("UCX_", StringComparison.Ordinal) && !IsJoint(name) && !IsAnchor(name) && FragRx.IsMatch(StripLodSuffix(name));
        /// <summary>ANCHOR_&lt;Frag&gt; empties (Model_Spec_v1 rule 3), carrying anchor_to.</summary>
        public static bool IsAnchor(string name) => name != null && name.StartsWith("ANCHOR_", StringComparison.Ordinal);
        /// <summary>JNT_&lt;FragA&gt;__&lt;FragB&gt;[_##]: returns false unless both sides are present.</summary>
        public static bool TryParseJoint(string name, out string a, out string b)
        {
            a = b = null;
            if (!IsJoint(name)) return false;
            var m = Regex.Match(name, @"^JNT_(?<a>.+?_frag_\d{2})__(?<b>.+?_frag_\d{2})(_\d{2})?$");
            if (!m.Success) return false;
            a = m.Groups["a"].Value; b = m.Groups["b"].Value; return true;
        }
        /// <summary>Kit files must live under Assets/_Project/Models/BuildingKit/.</summary>
        public const string KitRoot = ModelsRoot + "BuildingKit/";
        public static bool KitPathOk(string assetPath) => assetPath.Replace('\\', '/').StartsWith(KitRoot, StringComparison.Ordinal);
        public static bool IsJoint(string name) => name != null && name.StartsWith("JNT_", StringComparison.Ordinal);
        /// <summary>Non-render helper empties that must survive prefab building untouched.</summary>
        public static bool IsHelper(string name) => SocketName(name) != null || IsJoint(name) || (name != null && (name.StartsWith("Spawn_", StringComparison.Ordinal) || IsAnchor(name) || name.EndsWith("_Hint", StringComparison.Ordinal) || name.StartsWith("VFX_", StringComparison.Ordinal)));

        // ---------- materials / textures ----------
        public static readonly string[] MapSuffixes = { "_A", "_N", "_ORM", "_E", "_Crack" };

        /// <summary>M_Wrecker -> Wrecker, M_Wood_Inner -> Wood (+inner), M_Wrecker_Emissive -> Wrecker (+emissive). Same rule as the 3D Modeler draft.</summary>
        public static string TextureAssetForMaterial(string matName, out bool inner, out bool emissive)
        {
            string a = matName.StartsWith("M_", StringComparison.Ordinal) ? matName.Substring(2) : matName;
            inner = a.EndsWith("_Inner", StringComparison.Ordinal); if (inner) a = a.Substring(0, a.Length - 6);
            emissive = a.EndsWith("_Emissive", StringComparison.Ordinal); if (emissive) a = a.Substring(0, a.Length - 9);
            return a;
        }

        public static string MapSuffix(string texStem)
        {
            if (texStem == null || !texStem.StartsWith("T_", StringComparison.Ordinal)) return null;
            foreach (var s in new[] { "_ORM", "_Crack", "_A", "_N", "_E" }) if (texStem.EndsWith(s, StringComparison.Ordinal)) return s;
            return null;
        }

        public struct TexturePolicy { public bool sRGB; public bool normal; public int maxSize; public int androidMax; public string astc; }

        /// <summary>Per-map policy. High/Mid share the Android override (1024, ASTC 6x6, normal 5x5); Low gets half resolution through
        /// QualitySettings.globalTextureMipmapLimit = 1 on the Low quality level (Developer-owned setting, see IMPORT_PIPELINE.md).</summary>
        public static TexturePolicy PolicyFor(string texStem, string assetPath)
        {
            string s = MapSuffix(texStem);
            bool inner = texStem.Contains("_Inner");
            // Textures are named T_<Asset>_*, so classify by location: BuildingKit -> Kit, else by the model folder name.
            string pth = assetPath.Replace('\\', '/');
            var c = pth.Contains("/BuildingKit/") ? AssetClass.Kit : Classify(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(pth))) ?? "");
            int max = inner ? 256 : (c == AssetClass.Kit || c == AssetClass.KitFractured ? 512 : 1024);
            switch (s)
            {
                case "_N": return new TexturePolicy { sRGB = false, normal = true, maxSize = 2048, androidMax = max, astc = "5x5" };
                case "_ORM": return new TexturePolicy { sRGB = false, maxSize = 2048, androidMax = max, astc = "6x6" };
                case "_Crack": return new TexturePolicy { sRGB = false, maxSize = 1024, androidMax = Math.Min(max, 512), astc = "8x8" };
                case "_E": return new TexturePolicy { sRGB = true, maxSize = 1024, androidMax = Math.Min(max, 512), astc = "8x8" };
                default: return new TexturePolicy { sRGB = true, maxSize = 2048, androidMax = max, astc = "6x6" };
            }
        }

        // ---------- budgets ----------
        /// <summary>LOD0 triangle budgets (PM decision, PR #28): Wrecker &lt;= 12k, island &lt;= 10k, kit pieces 500-1.5k.
        /// Kit range applies to each intact piece and to each fractured piece's total; fragments keep &lt;= 300 each.</summary>
        public const int HeroTriMax = 12000, EnvTriMax = 10000, KitTriMin = 500, KitTriMax = 1500, FragmentTriBudget = 300, OtherTriMax = 3000;
        public static int TriBudget(AssetClass c, string stem)
        {
            switch (c)
            {
                case AssetClass.Hero: return HeroTriMax;
                case AssetClass.Environment: return EnvTriMax;
                case AssetClass.Kit: case AssetClass.KitFractured: return KitTriMax;
                default: return OtherTriMax;
            }
        }
        /// <summary>QA: Wrecker (Hero), island (Environment) and intact kit pieces must ship LOD0, LOD1 and LOD2.
        /// KitFractured is exempt (Physics: fragments need no LODs): LOD0 only, no LODGroup, LOD1/LOD2 are warned and ignored.</summary>
        public static bool RequiresLodChain(AssetClass c) => c == AssetClass.Hero || c == AssetClass.Environment || c == AssetClass.Kit;
        public static bool LodZeroOnly(AssetClass c) => c == AssetClass.KitFractured;
        public static int TriMin(AssetClass c) => c == AssetClass.Kit || c == AssetClass.KitFractured ? KitTriMin : 0;

        /// <summary>Texel density target band in px/m (spec section 4b).</summary>
        public static void TexelTarget(AssetClass c, out float min, out float max)
        {
            switch (c)
            {
                case AssetClass.Hero: min = 180; max = 360; break;   // ~256
                case AssetClass.Kit: case AssetClass.KitFractured: min = 128; max = 256; break;
                case AssetClass.Environment: min = 16; max = 256; break; // unique macro map; detail tiles separately
                default: min = 64; max = 512; break;
            }
        }
    }
}
#endif
