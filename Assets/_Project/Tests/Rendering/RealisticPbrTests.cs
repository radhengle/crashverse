using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Rendering.Tests
{
    /// <summary>Realistic PBR tier budgets (RenderTiers), texture import policy and foliage LOD validation.
    /// Asset scans pass when nothing has been imported under Models/Realistic yet.</summary>
    public class RealisticPbrTests
    {
        [OneTimeSetUp]
        public void EnsureSettings()
        {
            var t = System.Type.GetType("Crashverse.EditorTools.LightingSetup, Assembly-CSharp-Editor");
            Assert.IsNotNull(t, "LightingSetup editor script missing");
            t.GetMethod("Ensure").Invoke(null, null);
        }

        // ---------------- Tier budgets ----------------
        [Test]
        public void TierTextureBudgets_FollowImportCapAndMipLimit()
        {
            foreach (var t in RenderTiers.All)
            {
                int eff = RenderTiers.AndroidImportMax >> t.textureMipLimit;
                Assert.AreEqual(eff, t.albedoMax, t.name + " albedo");
                Assert.AreEqual(eff, t.normalMax, t.name + " normal");
                Assert.AreEqual(eff, t.ormMax, t.name + " ORM");
                Assert.LessOrEqual(t.albedoMax, 1024, t.name + ": PM cap is 1024 on Android (2048 needs PM approval)");
            }
            Assert.AreEqual(512, RenderTiers.All[0].albedoMax); Assert.AreEqual(1024, RenderTiers.All[1].albedoMax);
        }

        [Test]
        public void TierLodAndShadowBudgets()
        {
            var lo = RenderTiers.All[0]; var mid = RenderTiers.All[1]; var hi = RenderTiers.All[2];
            Assert.AreEqual(0.7f, lo.lodBias, 1e-4f); Assert.AreEqual(1.0f, mid.lodBias, 1e-4f); Assert.AreEqual(1.5f, hi.lodBias, 1e-4f);
            Assert.GreaterOrEqual(lo.maximumLODLevel, mid.maximumLODLevel);
            Assert.IsFalse(lo.foliageShadows, "Low: foliage must not cast shadows");
            Assert.IsFalse(lo.mainShadows, "Low has no main-light shadows (foliage shadows off follows)");
            Assert.LessOrEqual(mid.cascades, hi.cascades); Assert.LessOrEqual(mid.shadowDistance, hi.shadowDistance);
            Assert.AreEqual(1, mid.cascades, "Mid is the 60 fps reference tier: one cascade");
            Assert.AreEqual(AnisotropicFiltering.Disable, lo.anisotropic);
            Assert.AreNotEqual(AnisotropicFiltering.ForceEnable, hi.anisotropic, "ForceEnable overrides per-texture aniso (mobile cost)");
            foreach (var t in RenderTiers.All) Assert.IsTrue(t.srpBatcher, t.name + " SRP batcher");
        }

        [Test]
        public void QualityLevels_MatchRenderTiers_AndLeaveVSyncAlone()
        {
            int keep = QualitySettings.GetQualityLevel();
            try
            {
                var names = QualitySettings.names;
                Assert.GreaterOrEqual(names.Length, 3, "Low/Mid/High quality levels");
                for (int q = 0; q < names.Length; q++)
                {
                    QualitySettings.SetQualityLevel(q, false);
                    var t = RenderTiers.All[RenderTiers.IndexFor(names[q])];
                    Assert.AreEqual(t.textureMipLimit, QualitySettings.globalTextureMipmapLimit, names[q] + " globalTextureMipmapLimit");
                    Assert.AreEqual(t.anisotropic, QualitySettings.anisotropicFiltering, names[q] + " aniso");
                    Assert.AreEqual(t.lodBias, QualitySettings.lodBias, 1e-4f, names[q] + " lodBias");
                    Assert.AreEqual(t.maximumLODLevel, QualitySettings.maximumLODLevel, names[q] + " maximumLODLevel");
                    Assert.AreEqual(0, QualitySettings.vSyncCount, names[q] + ": vSync stays 0 (frame pacing owned by QualityController)");
                }
            }
            finally { QualitySettings.SetQualityLevel(keep, false); }
        }

        // ---------------- Texture policy ----------------
        const string R = "Assets/_Project/Models/Realistic/";

        [Test]
        public void Policy_ScopeExcludesV2AndOtherModels()
        {
            Assert.IsTrue(RenderTiers.InRealisticScope(R + "Buildings/Bld_A/textures/T_Brick_A.png"));
            Assert.IsTrue(RenderTiers.InRealisticScope(R + "Materials/Textures/T_Moss_ORM.png"));
            Assert.IsFalse(RenderTiers.InRealisticScope("Assets/_Project/Models/Wrecker_v2/textures/T_Wrecker_A.png"));
            Assert.IsFalse(RenderTiers.InRealisticScope(R + "Props/Crate_v2/T_Crate_A.png"));
            Assert.IsFalse(RenderTiers.InRealisticScope("Assets/_Project/Models/BuildingKit/Kit_Base/textures/T_Gold_A.png"));
            Assert.IsNull(RenderTiers.TexturePolicy(R + "Props/preview.png"));
        }

        [Test]
        public void Policy_AlbedoNormalOrm()
        {
            var a = RenderTiers.TexturePolicy(R + "Props/Crate/T_Crate_A.png").Value;
            Assert.IsTrue(a.sRGB); Assert.IsFalse(a.normalMap); Assert.IsFalse(a.keepAlpha); Assert.AreEqual("6x6", a.astc);
            Assert.AreEqual(2048, a.maxSize); Assert.AreEqual(1024, a.androidMax);

            var n = RenderTiers.TexturePolicy(R + "Props/Crate/T_Crate_N.png").Value;
            Assert.IsFalse(n.sRGB); Assert.IsTrue(n.normalMap); Assert.That(n.astc, Is.EqualTo("4x4").Or.EqualTo("5x5")); Assert.AreEqual(1024, n.androidMax);

            var o = RenderTiers.TexturePolicy(R + "Props/Crate/T_Crate_ORM.png").Value;
            Assert.IsFalse(o.sRGB, "ORM is linear"); Assert.IsFalse(o.normalMap); Assert.AreEqual(1024, o.androidMax);
        }

        [Test]
        public void Policy_LeafAtlasKeepsAlphaAt1024()
        {
            var l = RenderTiers.TexturePolicy(R + "Foliage/Oak/T_OakLeaves_A.png").Value;
            Assert.IsTrue(l.keepAlpha); Assert.AreEqual(1024, l.maxSize); Assert.AreEqual(1024, l.androidMax); Assert.IsTrue(l.sRGB);
        }

        [Test]
        public void Policy_RejectsUnpackedMaskNames()
        {
            Assert.IsTrue(RenderTiers.IsUnpackedMaskName("T_Crate_MaskMap"));
            Assert.IsTrue(RenderTiers.IsUnpackedMaskName("T_Crate_Metallic"));
            Assert.IsFalse(RenderTiers.IsUnpackedMaskName("T_Crate_ORM"));
            Assert.IsNull(RenderTiers.TexturePolicy(R + "Props/Crate/T_Crate_MaskMap.png"));
        }

        static IEnumerable<string> RealisticAssets(string filter)
        {
            if (!AssetDatabase.IsValidFolder(RenderTiers.RealisticRoot.TrimEnd('/'))) return Enumerable.Empty<string>();
            return AssetDatabase.FindAssets(filter, new[] { RenderTiers.RealisticRoot.TrimEnd('/') })
                .Select(g => AssetDatabase.GUIDToAssetPath(g)).Where(RenderTiers.InRealisticScope).Distinct();
        }

        [Test]
        public void ImportedRealisticTextures_MatchPolicy()
        {
            var bad = new List<string>();
            foreach (var path in RealisticAssets("t:Texture2D"))
            {
                var stem = System.IO.Path.GetFileNameWithoutExtension(path);
                if (RenderTiers.IsUnpackedMaskName(stem)) { bad.Add(path + ": unpacked mask map, pack into T_<X>_ORM"); continue; }
                var pol = RenderTiers.TexturePolicy(path);
                if (pol == null || !(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                var p = pol.Value;
                var android = ti.GetPlatformTextureSettings("Android");
                if (ti.sRGBTexture != p.sRGB) bad.Add(path + ": sRGB");
                if ((ti.textureType == TextureImporterType.NormalMap) != p.normalMap) bad.Add(path + ": texture type");
                if (!ti.mipmapEnabled) bad.Add(path + ": mipmaps off");
                if (!android.overridden || android.maxTextureSize > p.androidMax) bad.Add(path + $": Android max {android.maxTextureSize} > {p.androidMax}");
                if (!android.format.ToString().StartsWith("ASTC")) bad.Add(path + ": Android format " + android.format);
                if (p.keepAlpha && ti.alphaSource == TextureImporterAlphaSource.None) bad.Add(path + ": leaf atlas lost alpha");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void RealisticFoliageMaterials_EnableInstancing()
        {
            var bad = RealisticAssets("t:Material").Where(RenderTiers.IsFoliagePath)
                .Select(p => (p, m: AssetDatabase.LoadAssetAtPath<Material>(p)))
                .Where(x => x.m != null && !x.m.enableInstancing).Select(x => x.p).ToList();
            Assert.IsEmpty(bad, "Foliage materials need GPU instancing (fallback when SRP batching breaks):\n" + string.Join("\n", bad));
        }

        // ---------------- Foliage LOD ----------------
        static int Tris(Renderer r)
        {
            Mesh m = null;
            if (r is MeshRenderer) m = r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
            else if (r is SkinnedMeshRenderer s) m = s.sharedMesh;
            if (m == null) return 0;
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++) if (m.GetTopology(i) == MeshTopology.Triangles) n += m.GetIndexCount(i) / 3;
            return (int)n;
        }

        /// <summary>Problems for one foliage prefab root (empty = valid). Public so the 3D Modeler checks can reuse it.</summary>
        public static List<string> ValidateFoliage(GameObject root, string path)
        {
            var errs = new List<string>();
            var lg = root.GetComponent<LODGroup>();
            if (lg == null) { errs.Add("no LODGroup on the prefab root"); return errs; }
            var lods = lg.GetLODs();
            if (lods.Length < 2) { errs.Add($"{lods.Length} LOD level(s); need geometry LODs + a final impostor"); return errs; }
            var budget = RenderTiers.BudgetFor(path);
            for (int i = 0; i < lods.Length; i++)
            {
                var rs = lods[i].renderers.Where(r => r != null).ToArray();
                if (rs.Length == 0) { errs.Add($"LOD{i} has no renderers"); continue; }
                bool last = i == lods.Length - 1;
                if (last)
                {
                    bool billboard = rs.All(r => r is BillboardRenderer);
                    int t = rs.Sum(r => Tris(r));
                    if (!billboard && (t < budget.impostorMinTris || t > budget.impostorMaxTris))
                        errs.Add($"final LOD{i} is not an impostor: {t} tris (want BillboardRenderer or {budget.impostorMinTris}-{budget.impostorMaxTris} tris)");
                    if (rs.Any(r => r.shadowCastingMode != ShadowCastingMode.Off)) errs.Add($"impostor LOD{i} must not cast shadows");
                }
                else
                {
                    int max = budget.lodTris[Mathf.Min(i, budget.lodTris.Length - 1)];
                    int t = rs.Sum(r => Tris(r));
                    if (t > max) errs.Add($"LOD{i} {t} tris > budget {max}");
                }
            }
            if (lg.fadeMode != RenderTiers.FoliageLodFadeMode) errs.Add($"fadeMode {lg.fadeMode}, want {RenderTiers.FoliageLodFadeMode} (shader has no LOD_FADE_CROSSFADE yet)");
            return errs;
        }

        [Test]
        public void FoliagePrefabs_HaveLodGroupImpostorAndStayInBudget()
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }).Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => p.Contains("/Realistic/") && RenderTiers.IsFoliagePath(p) && !p.ToLowerInvariant().Contains("_v2")).Distinct().ToList();
            if (prefabs.Count == 0) { Assert.Pass("No realistic foliage prefabs yet"); return; }
            var bad = new List<string>();
            foreach (var p in prefabs)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                foreach (var e in ValidateFoliage(go, p)) bad.Add($"{p}: {e}");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void FoliageValidator_SelfTest()
        {
            var created = new List<Object>();
            GameObject Quad(string n, Transform parent, ShadowCastingMode sh)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad); g.name = n; g.transform.SetParent(parent, false);
                g.GetComponent<MeshRenderer>().shadowCastingMode = sh; return g;
            }
            try
            {
                var tree = new GameObject("Tree_Test"); created.Add(tree);
                Assert.IsNotEmpty(ValidateFoliage(tree, "Foliage/Tree_Test.prefab"), "missing LODGroup must fail");

                var lod0 = Quad("LOD0", tree.transform, ShadowCastingMode.On);
                var imp = Quad("LOD1_Impostor", tree.transform, ShadowCastingMode.Off); // a quad = 2 tris
                var lg = tree.AddComponent<LODGroup>();
                lg.fadeMode = RenderTiers.FoliageLodFadeMode;
                lg.SetLODs(new[] { new LOD(0.3f, new Renderer[] { lod0.GetComponent<Renderer>() }), new LOD(0.01f, new Renderer[] { imp.GetComponent<Renderer>() }) });
                Assert.IsEmpty(ValidateFoliage(tree, "Foliage/Tree_Test.prefab"));

                imp.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                Assert.IsNotEmpty(ValidateFoliage(tree, "Foliage/Tree_Test.prefab"), "shadow-casting impostor must fail");
                imp.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.transform.SetParent(tree.transform, false); // 768 tris
                lg.SetLODs(new[] { new LOD(0.3f, new Renderer[] { lod0.GetComponent<Renderer>() }), new LOD(0.01f, new Renderer[] { sphere.GetComponent<Renderer>() }) });
                Assert.IsNotEmpty(ValidateFoliage(tree, "Foliage/Tree_Test.prefab"), "non-impostor final LOD must fail");

                Assert.AreEqual(3000, RenderTiers.BudgetFor("Foliage/Bush_Fern.prefab").lodTris[0]);
                Assert.AreEqual(6000, RenderTiers.BudgetFor("Foliage/Tree_Oak.prefab").lodTris[0]);
            }
            finally { foreach (var o in created) Object.DestroyImmediate(o); }
        }
    }
}
