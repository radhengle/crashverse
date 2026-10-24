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

        /// <summary>Android max size per texture group: Hero/Environment 1024, kit 512, hero buildings 1024 (PM via 3D Modeler), any _Inner 256.</summary>
        public const int AndroidMaxDefault = 1024, AndroidMaxKit = 512, AndroidMaxHeroBuilding = 1024, AndroidMaxInner = 256;

        /// <summary>Per-map policy. High/Mid share the Android override (1024, ASTC 6x6, normal 5x5); Low gets half resolution through
        /// QualitySettings.globalTextureMipmapLimit = 1 on the Low quality level (Developer-owned setting, see IMPORT_PIPELINE.md).</summary>
        public static TexturePolicy PolicyFor(string texStem, string assetPath)
        {
            string s = MapSuffix(texStem);
            bool inner = texStem.Contains("_Inner");
            // Textures are named T_<Asset>_*, so classify by location: BuildingKit -> Kit, else by the model folder name.
            // Hero buildings (Bld_* folder, T_Bld_* asset prefix, or a hero kit folder BuildingKit/Kit_<B>/) are checked first.
            string pth = assetPath.Replace('\\', '/');
            var c = pth.Contains("/BuildingKit/") ? AssetClass.Kit : Classify(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(pth))) ?? "");
            int max = inner ? AndroidMaxInner
                    : IsHeroBuildingTexture(texStem, pth) ? AndroidMaxHeroBuilding
                    : (c == AssetClass.Kit || c == AssetClass.KitFractured ? AndroidMaxKit : AndroidMaxDefault);
            switch (s)
            {
                case "_N": return new TexturePolicy { sRGB = false, normal = true, maxSize = 2048, androidMax = max, astc = "5x5" };
                case "_ORM": return new TexturePolicy { sRGB = false, maxSize = 2048, androidMax = max, astc = "6x6" };
                case "_Crack": return new TexturePolicy { sRGB = false, maxSize = 1024, androidMax = Math.Min(max, 512), astc = "8x8" };
                case "_E": return new TexturePolicy { sRGB = true, maxSize = 1024, androidMax = Math.Min(max, 512), astc = "8x8" };
                default: return new TexturePolicy { sRGB = true, maxSize = 2048, androidMax = max, astc = "6x6" };
            }
        }

        // ---------- hero buildings (3D Modeler hero buildings v1: models/HeroBuildings/README.md) ----------
        /// <summary>Per-kit stats sidecars live outside Assets, relative to the project root: models/HeroBuildings/Kit_&lt;B&gt;_stats.json.</summary>
        public const string HeroStatsDir = "models/HeroBuildings/";
        /// <summary>Hero building kits (3D Modeler hero buildings v1). Only these, T_Bld_* and Bld_* folders get the 1024 Android texture cap (PM).</summary>
        public static readonly string[] HeroBuildingKits = { "Keep", "Tower", "Gate", "House" };
        /// <summary>Kits that ship a stats sidecar for fractureClass: the hero building kits plus the shared Kit_Props (which keeps the 512 kit
        /// texture cap). Any other BuildingKit/Kit_&lt;B&gt; with a sidecar also reads it. A listed kit without its sidecar is a validator warning.</summary>
        public static readonly string[] KnownStatsKits = { "Keep", "Tower", "Gate", "House", "Props" };
        /// <summary>Sidecar lookup, replaceable in tests. Default: File.Exists relative to the project root (Unity's working directory).</summary>
        public static Func<string, bool> HeroStatsExists = kit => File.Exists(HeroStatsPath(kit));
        public static string HeroStatsPath(string kit) => HeroStatsDir + "Kit_" + kit + "_stats.json";
        public static string HeroStatsDependency(string kit) => "crashverse/hero-stats/Kit_" + kit;

        /// <summary>&lt;B&gt; of the kit folder Assets/_Project/Models/BuildingKit/Kit_&lt;B&gt;/... (folder, not file name), else null.</summary>
        public static string KitFolder(string assetPath)
        {
            if (assetPath == null) return null;
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(KitRoot, StringComparison.Ordinal)) return null;
            string rest = p.Substring(KitRoot.Length);
            int i = rest.IndexOf('/');
            if (i <= 0) return null;
            string f = rest.Substring(0, i);
            return f.StartsWith("Kit_", StringComparison.Ordinal) && f.Length > 4 ? f.Substring(4) : null;
        }

        /// <summary>Kit whose FBX modules read fractureClass from models/HeroBuildings/Kit_&lt;B&gt;_stats.json (KnownStatsKits or a sidecar exists).</summary>
        public static bool UsesHeroStats(string kit) => !string.IsNullOrEmpty(kit) && (Array.IndexOf(KnownStatsKits, kit) >= 0 || HeroStatsExists(kit));
        /// <summary>Hero building kit folder for the texture cap. A stats sidecar alone does not make a kit a hero building (Kit_Props stays 512).</summary>
        public static bool IsHeroBuildingKit(string kit) => !string.IsNullOrEmpty(kit) && Array.IndexOf(HeroBuildingKits, kit) >= 0;

        /// <summary>Hero building texture: asset prefix T_Bld_*, any folder under Models/ named Bld_*, or a hero kit folder
        /// (BuildingKit/Kit_Keep|Tower|Gate|House/textures/T_Keep_A.png: the hero kits name their atlases T_&lt;B&gt;_*, not T_Bld_*).
        /// Shared props (Kit_Props) and other kits stay at the 512 kit cap even when they have a stats sidecar.</summary>
        public static bool IsHeroBuildingTexture(string texStem, string assetPath)
        {
            if (texStem != null && texStem.StartsWith("T_Bld_", StringComparison.Ordinal)) return true;
            string p = (assetPath ?? "").Replace('\\', '/');
            if (p.StartsWith(ModelsRoot, StringComparison.Ordinal))
            {
                var segs = p.Substring(ModelsRoot.Length).Split('/');
                for (int i = 0; i < segs.Length - 1; i++) if (segs[i].StartsWith("Bld_", StringComparison.Ordinal)) return true;
            }
            return IsHeroBuildingKit(KitFolder(p));
        }

        /// <summary>BuildingKit/Kit_House/Kit_House_GroundDoor_Fractured[_LODn].fbx -> kit House, module GroundDoor, fractured.
        /// False unless the folder is a stats kit (hero buildings or Kit_Props) and the file is Kit_&lt;B&gt;_&lt;Module&gt;.</summary>
        public static bool TryHeroModule(string assetPath, out string kit, out string module, out bool fractured)
        {
            kit = KitFolder(assetPath); module = null; fractured = false;
            if (!UsesHeroStats(kit)) { kit = null; return false; }
            string n = StripLodSuffix(FileStem(assetPath)), pre = "Kit_" + kit + "_";
            if (!n.StartsWith(pre, StringComparison.Ordinal)) return false;
            n = n.Substring(pre.Length);
            if (n.EndsWith("_Fractured", StringComparison.Ordinal)) { fractured = true; n = n.Substring(0, n.Length - 10); }
            module = n;
            return n.Length > 0;
        }

        /// <summary>Parsed Kit_&lt;B&gt;_stats.json. Real schema (3D Modeler, hero buildings v1):
        /// { "building": "House", "texel_px_per_m": 77.1, "atlas": 2048, "modules": { "&lt;Module&gt;": { "frac": "Stone", "LOD0": 640, ... }, ... } }.
        /// Only modules[*].frac is used (fracture_class is accepted as an alias). Everything else is ignored.</summary>
        public sealed class HeroStats
        {
            public string building;
            /// <summary>Module name -> fracture class (null when the module entry has no class). Insertion order = JSON order.</summary>
            public readonly Dictionary<string, string> modules = new Dictionary<string, string>(StringComparer.Ordinal);
            public bool HasModule(string m) => m != null && modules.ContainsKey(m);
            public string ClassFor(string m) => m != null && modules.TryGetValue(m, out var c) ? c : null;
        }

        /// <summary>Parses the sidecar text. Throws FormatException on malformed JSON or a missing "modules" object.</summary>
        public static HeroStats ParseHeroStats(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) throw new FormatException("stats JSON root must be an object");
            if (!root.TryGetValue("modules", out var mo) || !(mo is Dictionary<string, object> mods)) throw new FormatException("stats JSON has no \"modules\" object");
            var st = new HeroStats { building = root.TryGetValue("building", out var b) ? b as string : null };
            foreach (var kv in mods)
            {
                string cls = null;
                if (kv.Value is Dictionary<string, object> e)
                {
                    if (e.TryGetValue("fracture_class", out var fc) && fc is string s1 && s1.Length > 0) cls = s1;
                    else if (e.TryGetValue("frac", out var f) && f is string s2 && s2.Length > 0) cls = s2;
                }
                st.modules[kv.Key] = cls;
            }
            return st;
        }

        /// <summary>Validator cross-check (warnings): modules listed in the JSON with no Kit_&lt;B&gt;_&lt;Module&gt;[_Fractured].fbx, and FBX modules with no JSON entry.</summary>
        public static List<string> CompareHeroModules(string kit, IEnumerable<string> jsonModules, IEnumerable<string> fbxModules)
        {
            var j = new SortedSet<string>(jsonModules ?? Array.Empty<string>(), StringComparer.Ordinal);
            var f = new SortedSet<string>(fbxModules ?? Array.Empty<string>(), StringComparer.Ordinal);
            var w = new List<string>();
            foreach (var m in j) if (!f.Contains(m)) w.Add($"module {m} is in {HeroStatsPath(kit)} but there is no Kit_{kit}_{m}.fbx / Kit_{kit}_{m}_Fractured.fbx");
            foreach (var m in f) if (!j.Contains(m)) w.Add($"Kit_{kit}_{m} has no entry in {HeroStatsPath(kit)}");
            return w;
        }

        /// <summary>Minimal JSON reader (objects, arrays, strings, numbers, true/false/null) so no package is needed.
        /// JsonUtility cannot read the sidecar because "modules" is a keyed object, not an array.</summary>
        public static class MiniJson
        {
            public static object Parse(string s)
            {
                if (s == null) throw new FormatException("null JSON");
                int i = 0;
                var v = Value(s, ref i);
                Ws(s, ref i);
                if (i != s.Length) throw new FormatException($"trailing characters at {i}");
                return v;
            }

            static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object Value(string s, ref int i)
            {
                Ws(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end of JSON");
                char c = s[i];
                if (c == '{')
                {
                    var o = new Dictionary<string, object>(StringComparer.Ordinal); i++;
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; return o; }
                    while (true)
                    {
                        Ws(s, ref i);
                        if (i >= s.Length || s[i] != '"') throw new FormatException($"expected key at {i}");
                        string k = Str(s, ref i);
                        Ws(s, ref i);
                        if (i >= s.Length || s[i] != ':') throw new FormatException($"expected ':' at {i}");
                        i++;
                        o[k] = Value(s, ref i);
                        Ws(s, ref i);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        if (i < s.Length && s[i] == '}') { i++; return o; }
                        throw new FormatException($"expected ',' or '}}' at {i}");
                    }
                }
                if (c == '[')
                {
                    var a = new List<object>(); i++;
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; return a; }
                    while (true)
                    {
                        a.Add(Value(s, ref i));
                        Ws(s, ref i);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        if (i < s.Length && s[i] == ']') { i++; return a; }
                        throw new FormatException($"expected ',' or ']' at {i}");
                    }
                }
                if (c == '"') return Str(s, ref i);
                if (Lit(s, ref i, "true")) return true;
                if (Lit(s, ref i, "false")) return false;
                if (Lit(s, ref i, "null")) return null;
                int st = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (i > st && double.TryParse(s.Substring(st, i - st), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
                throw new FormatException($"unexpected character '{c}' at {st}");
            }

            static bool Lit(string s, ref int i, string lit)
            {
                if (string.CompareOrdinal(s, i, lit, 0, lit.Length) != 0) return false;
                i += lit.Length; return true;
            }

            static string Str(string s, ref int i)
            {
                var sb = new System.Text.StringBuilder(); i++;   // opening quote
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("bad \\u escape");
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: throw new FormatException($"bad escape \\{e}");
                    }
                }
                throw new FormatException("unterminated string");
            }
        }

        // ---------- budgets ----------
        /// <summary>LOD0 triangle budgets (PM decision, PR #28): Wrecker &lt;= 12k, island &lt;= 10k, kit pieces 500-1.5k.
        /// Kit range applies to each intact piece and to each fractured piece's total; each fragment must be &lt; FragmentTriLimit (400, Model_Spec_v1 v3: "fragments &lt; 400 tris").</summary>
        public const int HeroTriMax = 12000, EnvTriMax = 10000, KitTriMin = 500, KitTriMax = 1500, FragmentTriLimit = 400, OtherTriMax = 3000;
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
