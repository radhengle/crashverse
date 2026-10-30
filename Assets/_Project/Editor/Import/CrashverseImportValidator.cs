#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using C = Crashverse.EditorTools.Import.ImportConventions;

namespace Crashverse.EditorTools.Import
{
    /// <summary>Crashverse/Import/Validation Report: naming, missing maps, texel density hint, triangle budgets, sockets.
    /// Writes Logs/CrashverseImportReport.md (outside Assets, not committed) and logs a summary. Never modifies assets.</summary>
    public static class CrashverseImportValidator
    {
        [MenuItem("Crashverse/Import/Validation Report")]
        public static void RunMenu()
        {
            var report = Run(out int errors, out int warnings);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CrashverseImportReport.md", report);
            var msg = $"[CrashverseImport] Validation: {errors} errors, {warnings} warnings. Logs/CrashverseImportReport.md";
            if (errors > 0) Debug.LogError(msg); else if (warnings > 0) Debug.LogWarning(msg); else Debug.Log(msg);
        }

        public static string Run(out int errors, out int warnings)
        {
            var sb = new StringBuilder("# Crashverse import validation\n\n| Level | Asset | Check | Detail |\n|---|---|---|---|\n");
            int e = 0, w = 0;
            void Err(string a, string c, string d) { e++; sb.Append($"| ERROR | {a} | {c} | {d} |\n"); }
            void Warn(string a, string c, string d) { w++; sb.Append($"| warn | {a} | {c} | {d} |\n"); }
            // Hero buildings: kit -> parsed stats (null = missing/unreadable), kit -> FBX modules seen.
            var heroStats = new Dictionary<string, C.HeroStats>();
            var heroFbxModules = new Dictionary<string, HashSet<string>>();

            foreach (var g in CrashversePrefabBuilder.ModelGroups())
            {
                string key = g.Key, baseName = Path.GetFileName(key), dir = Path.GetDirectoryName(key).Replace('\\', '/');
                var files = g.Value;
                string lod0 = files.TryGetValue(0, out var a0) ? a0 : files.TryGetValue(-1, out var s1) ? s1 : null;
                if (lod0 == null) { Err(baseName, "LOD", "LOD1/LOD2 file without _LOD0"); continue; }
                if (files.ContainsKey(-1) && files.Keys.Any(k => k >= 0)) Warn(baseName, "LOD", "both a plain FBX and _LODn files; plain one is ignored");
                var cls = C.Classify(lod0);
                if (cls == C.AssetClass.Unknown) Warn(baseName, "naming", "prefix not Wrecker*/Hero_/Island_/Env_/Kit_: default budgets used");

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(lod0);
                if (go == null) { Err(baseName, "import", "could not load " + lod0); continue; }
                var all = go.GetComponentsInChildren<Transform>(true);

                // Naming / structure
                foreach (var t in all)
                {
                    if (t == go.transform) continue;
                    if (t.name.Contains(" ") || t.name.Contains(".0")) Err(baseName, "naming", $"'{t.name}' has spaces or a Blender .00n suffix");
                    string tgt = C.ColliderTarget(t.name);
                    if (tgt != null && CrashverseImportPostprocessor.FindDeep(go.transform, tgt) == null && tgt != baseName)
                        Warn(baseName, "collider", $"{t.name}: target '{tgt}' not found, hull goes on the root");
                }
                if (cls == C.AssetClass.Hero)
                {
                    var socks = new HashSet<string>(all.Select(t => C.SocketName(t.name)).Where(x => x != null));
                    foreach (var need in C.HeroSockets) if (!socks.Contains(need)) Err(baseName, "socket", $"missing Socket_{need}");
                }
                if ((cls == C.AssetClass.Kit || cls == C.AssetClass.KitFractured) && !C.KitPathOk(lod0)) Err(baseName, "path", $"kit files must be under {C.KitRoot}");
                if (cls == C.AssetClass.KitFractured)
                {
                    if (files.Keys.Any(k => k >= 1) || all.Any(t => C.LodLevel(t.name, out _) >= 1))
                        Warn(baseName, "LOD", "fractured kit pieces use LOD0 only: LOD1/LOD2 files or objects are ignored");
                    var hullsByTarget = all.Where(t => t.name.StartsWith("UCX_")).GroupBy(t => C.ColliderTarget(t.name)).ToDictionary(g2 => g2.Key, g2 => g2.Count());
                    foreach (var f in all.Where(t => C.IsFragment(t.name) && C.LodLevel(t.name, out _) <= 0 && !t.name.StartsWith("UCX_")).OrderBy(t => t.name, System.StringComparer.Ordinal))
                    {
                        int n = hullsByTarget.TryGetValue(f.name, out var k) ? k : 0;
                        if (n != 1) Err(baseName, "collider", $"{f.name}: {n} UCX_{f.name} hulls, need exactly 1");
                        var fm = f.GetComponent<MeshFilter>();
                        if (fm != null && fm.sharedMesh != null)
                        {
                            var bnd = fm.sharedMesh.bounds;   // local space: centre offset = pivot distance from bounds centre
                            float off = Vector3.Scale(bnd.center, f.lossyScale).magnitude, tol = Mathf.Max(0.05f, 0.1f * Vector3.Scale(bnd.size, f.lossyScale).magnitude);
                            if (off > tol) Warn(baseName, "pivot", $"{f.name}: pivot {off:0.000} m from bounds centre (tol {tol:0.000}); README wants pivot = centre of mass");
                        }
                    }
                    var fragNames = new HashSet<string>(all.Where(t => C.IsFragment(t.name)).Select(t => t.name));
                    foreach (var j in all.Where(t => C.IsJoint(t.name)))
                        if (!C.TryParseJoint(j.name, out var ja, out var jb)) Err(baseName, "joint", $"{j.name}: expected JNT_<FragA>__<FragB>[_##]");
                        else if (!fragNames.Contains(ja) || !fragNames.Contains(jb)) Err(baseName, "joint", $"{j.name}: fragment {(fragNames.Contains(ja) ? jb : ja)} not found");
                    // JointBreakPolicy (physics/Joint_Break_Policy.md): kit joints never break by force. A finite Unity joint is an error;
                    // an explicit break_force_N / break_torque_Nm is ignored (kept as data) and only warned about.
                    foreach (var j in all.Where(t => C.IsJoint(t.name)))
                    {
                        foreach (var uj in j.GetComponents<Joint>())
                            if (!float.IsPositiveInfinity(uj.breakForce) || !float.IsPositiveInfinity(uj.breakTorque))
                                Err(baseName, "joint", $"{j.name}: {uj.GetType().Name} breakForce/breakTorque {uj.breakForce}/{uj.breakTorque} must be Infinity (JointBreakPolicy)");
                        var jh = j.GetComponent<Crashverse.Import.ImportedPhysicsHints>();
                        if (jh != null && ((jh.breakForce > 0f && !float.IsPositiveInfinity(jh.breakForce)) || (jh.breakTorque > 0f && !float.IsPositiveInfinity(jh.breakTorque))))
                            Warn(baseName, "joint", $"{j.name}: break_force_N/break_torque_Nm {jh.breakForce}/{jh.breakTorque} ignored; joints break only from damage (JointBreakPolicy)");
                    }
                    foreach (var an in all.Where(t => C.IsAnchor(t.name)))
                        if (!fragNames.Contains(an.name.Substring(7))) Err(baseName, "anchor", $"{an.name}: expected ANCHOR_<Frag> naming an existing fragment");
                    foreach (var h in hullsByTarget.Keys.Where(t => all.All(x => x.name != t))) Err(baseName, "collider", $"UCX_ hull targets missing '{h}'");
                }
                if (C.TryHeroModule(lod0, out var hKit, out var hModule, out _))
                    CheckHeroFractureClass(go, baseName, hKit, hModule, heroStats, heroFbxModules, Warn);
                bool anyHull = all.Any(t => C.ColliderTarget(t.name) != null);
                if (!anyHull) Warn(baseName, "collider", cls == C.AssetClass.KitFractured ? "no UCX_ hulls: fragments fall back to convex own-mesh" : "no UCX_/_COL hull: BoxCollider fallback");

                // Triangles (LOD0 renderers only, excluding hulls). Budgets: CrashverseImportConventions (PM decision).
                int total = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || C.ColliderTarget(mf.name) != null || C.LodLevel(mf.name, out _) > 0) continue;
                    int tris = Tris(mf.sharedMesh); total += tris;
                    if (C.IsFragment(mf.name) && tris >= C.FragmentTriLimit) Err(baseName, "tris", $"{mf.name} {tris} tris, fragments must be < {C.FragmentTriLimit} (prefab build refused)");
                }
                int budget = C.TriBudget(cls, baseName), min = C.TriMin(cls);
                foreach (var ge in CrashversePrefabBuilder.GateErrors(files, go, cls, out _)) Err(baseName, "gate", ge + " (prefab build refused)");
                if (total < min) Warn(baseName, "tris", $"LOD0 total {total} < {min} (kit pieces 500-1.5k)");
                sb.Append($"| info | {baseName} | tris | LOD0 {total} (budget {(min > 0 ? min + "-" : "<=")}{budget}), LOD files: {string.Join(",", files.Keys.OrderBy(k => k))} |\n");

                // Materials + maps
                var mats = go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
                foreach (var m in mats)
                {
                    if (!m.name.StartsWith("M_")) Err(baseName, "naming", $"material '{m.name}' should be M_<Asset|Mat>[_Inner|_Emissive]");
                    if (m.shader == null || m.shader.name != C.ShaderName) Warn(baseName, "material", $"{m.name} uses {(m.shader ? m.shader.name : "null")}, not {C.ShaderName} (run Rewire Materials)");
                    string asset = C.TextureAssetForMaterial(m.name, out bool inner, out bool emissive);
                    string tex = $"{dir}/textures/T_{asset}";
                    foreach (var sfx in new[] { "_A", "_N", "_ORM" })
                        if (inner ? !Exists(tex + "_Inner" + sfx) && !Exists(tex + sfx) : !Exists(tex + sfx))
                            Err(baseName, "maps", inner ? $"{m.name}: missing T_{asset}_Inner{sfx} and fallback T_{asset}{sfx}" : $"{m.name}: missing T_{asset}{sfx}");
                    if (emissive && !Exists(tex + "_E")) Err(baseName, "maps", $"{m.name}: _Emissive material without T_{asset}_E");
                    if (inner && m.HasProperty("_InnerFace") && m.GetFloat("_InnerFace") < 0.5f) Err(baseName, "material", $"{m.name}: _InnerFace must be 1");
                    CheckTextureImport(tex + "_N", true, false, baseName, Err);
                    CheckTextureImport(tex + "_ORM", false, false, baseName, Err);
                    CheckTextureImport(tex + "_A", false, true, baseName, Err);
                }

                // Texel density hint (LOD0, first material's albedo size)
                var tx = Texel(go, dir, mats);
                if (tx > 0)
                {
                    C.TexelTarget(cls, out float lo, out float hi);
                    string d = $"{tx:0} px/m (target {lo:0}-{hi:0})";
                    if (tx < lo * 0.75f || tx > hi * 1.5f) Warn(baseName, "texel", d); else sb.Append($"| info | {baseName} | texel | {d} |\n");
                }
            }
            // Hero buildings: every module in Kit_<B>_stats.json has an FBX and vice versa (warnings). Kits with a sidecar but no FBX yet are included.
            foreach (var kit in C.KnownStatsKits.Concat(heroFbxModules.Keys).Distinct())
            {
                var st = StatsFor(kit, heroStats);
                heroFbxModules.TryGetValue(kit, out var fbx);
                if (st == null) continue;   // missing sidecar is already warned per module; nothing to compare
                foreach (var msg in C.CompareHeroModules(kit, st.modules.Keys, fbx)) Warn("Kit_" + kit, "fracture class", msg);
            }
            foreach (var t in AssetDatabase.FindAssets("t:Texture2D", new[] { C.ModelsRoot.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath))
            {
                string stem = C.FileStem(t);
                if (t.Contains("/textures/") && C.MapSuffix(stem) == null) Warn(stem, "naming", $"{t}: not T_<Asset>_A|N|ORM|E|Crack (legacy palette?)");
            }
            errors = e; warnings = w;
            sb.Insert(0, $"Errors: {e}, warnings: {w}\n\n");
            return sb.ToString();
        }

        static C.HeroStats StatsFor(string kit, Dictionary<string, C.HeroStats> cache)
        {
            if (!cache.TryGetValue(kit, out var st)) cache[kit] = st = HeroStatsCache.Load(kit, out _);
            return st;
        }

        /// <summary>Hero building module: sidecar present, module listed with a class, root and LOD0 fragments carry fractureClass,
        /// and an FBX fracture_class that differs from the JSON is reported (it wins). All warnings: never blocks a build.</summary>
        static void CheckHeroFractureClass(GameObject go, string baseName, string kit, string module, Dictionary<string, C.HeroStats> stats,
                                           Dictionary<string, HashSet<string>> fbxModules, System.Action<string, string, string> warn)
        {
            if (!fbxModules.TryGetValue(kit, out var set)) fbxModules[kit] = set = new HashSet<string>();
            set.Add(module);
            var st = StatsFor(kit, stats);
            string json = st?.ClassFor(module);
            if (st == null) warn(baseName, "fracture class", $"no readable {C.HeroStatsPath(kit)}: fractureClass only from FBX fracture_class");
            else if (!st.HasModule(module)) warn(baseName, "fracture class", $"module {module} has no entry in {C.HeroStatsPath(kit)}");
            else if (json == null) warn(baseName, "fracture class", $"module {module} in {C.HeroStatsPath(kit)} has no \"frac\"");
            var targets = go.GetComponentsInChildren<Transform>(true).Where(t => t == go.transform || (C.IsFragment(t.name) && C.LodLevel(t.name, out _) <= 0));
            foreach (var t in targets)
            {
                var h = t.GetComponent<Crashverse.Import.ImportedPhysicsHints>();
                string v = h != null ? h.fractureClass : null;
                if (string.IsNullOrEmpty(v)) warn(baseName, "fracture class", $"{t.name}: no fractureClass on ImportedPhysicsHints");
                else if (json != null && v != json) warn(baseName, "fracture class", $"{t.name}: FBX fracture_class '{v}' overrides JSON '{json}'");
            }
        }

        static int Tris(Mesh m) { long n = 0; for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i); return (int)(n / 3); }

        static bool Exists(string pathNoExt) => File.Exists(pathNoExt + ".png") || File.Exists(pathNoExt + ".tga");

        static void CheckTextureImport(string pathNoExt, bool normal, bool srgb, string asset, System.Action<string, string, string> err)
        {
            var ti = AssetImporter.GetAtPath(pathNoExt + ".png") as TextureImporter;
            if (ti == null) return;
            if (normal && ti.textureType != TextureImporterType.NormalMap) err(asset, "texture", $"{Path.GetFileName(pathNoExt)} must be Normal map");
            if (!normal && ti.sRGBTexture != srgb) err(asset, "texture", $"{Path.GetFileName(pathNoExt)} sRGB must be {srgb}");
            var a = ti.GetPlatformTextureSettings("Android");
            if (!a.overridden || !a.format.ToString().StartsWith("ASTC")) err(asset, "texture", $"{Path.GetFileName(pathNoExt)} has no Android ASTC override");
        }

        /// <summary>px/m = sqrt(uvArea * texW * texH / worldArea) over LOD0 meshes (world area uses lossy scale; meshes are 1 unit = 1 m).</summary>
        static float Texel(GameObject go, string dir, Material[] mats)
        {
            var albedo = mats.Select(m => m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null).FirstOrDefault(t => t != null);
            int w = 0, h = 0;
            if (albedo != null) { w = albedo.width; h = albedo.height; }
            else
            {
                var first = mats.Select(m => C.TextureAssetForMaterial(m.name, out _, out _)).FirstOrDefault();
                var t = first == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/textures/T_{first}_A.png");
                if (t == null) return -1; w = t.width; h = t.height;
            }
            double uvA = 0, wA = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || C.ColliderTarget(mf.name) != null || C.LodLevel(mf.name, out _) > 0) continue;
                Vector3[] v; Vector2[] uv; int[] tri;
                try { v = mf.sharedMesh.vertices; uv = mf.sharedMesh.uv; tri = mf.sharedMesh.triangles; } catch { continue; }
                if (uv == null || uv.Length != v.Length) continue;
                var s = mf.transform.lossyScale;
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    Vector3 a = Vector3.Scale(v[tri[i]], s), b = Vector3.Scale(v[tri[i + 1]], s), c = Vector3.Scale(v[tri[i + 2]], s);
                    wA += Vector3.Cross(b - a, c - a).magnitude * 0.5;
                    Vector2 p = uv[tri[i]], q = uv[tri[i + 1]], r = uv[tri[i + 2]];
                    uvA += System.Math.Abs((q.x - p.x) * (r.y - p.y) - (r.x - p.x) * (q.y - p.y)) * 0.5;
                }
            }
            if (wA <= 1e-6) return -1;
            return (float)System.Math.Sqrt(uvA * w * h / wA);
        }
    }
}
#endif
