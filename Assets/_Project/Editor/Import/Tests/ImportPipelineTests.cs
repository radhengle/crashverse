using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Crashverse.EditorTools.Import;
using Crashverse.Import;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using C = Crashverse.EditorTools.Import.ImportConventions;

namespace Crashverse.Tests
{
    public class ImportNamingTests
    {
        [TestCase("UCX_Wall_frag_03", "Wall_frag_03")]
        [TestCase("UCX_Kit_Brick_Wall_frag_17", "Kit_Brick_Wall_frag_17")]
        [TestCase("UCX_Carriage_01", "Carriage")]
        [TestCase("UCX_Barrel", "Barrel")]
        [TestCase("Wheel_FL_COL", "Wheel_FL")]
        [TestCase("Wheel_FL_COL_02", "Wheel_FL")]
        public void HullTarget(string hull, string target) => Assert.AreEqual(target, C.ColliderTarget(hull));

        [TestCase("Wall_frag_03")] [TestCase("JNT_Wall_frag_01__Wall_frag_02")] [TestCase("ANCHOR_Wall_frag_01")] [TestCase("Socket_Muzzle")]
        public void NotAHull(string name) => Assert.IsNull(C.ColliderTarget(name));

        [TestCase("Wall_frag_03", true)] [TestCase("Wall_frag_03_LOD1", true)] [TestCase("UCX_Wall_frag_03", false)]
        [TestCase("JNT_Wall_frag_01__Wall_frag_02", false)] [TestCase("ANCHOR_Wall_frag_01", false)] [TestCase("Wall", false)]
        public void Fragment(string name, bool frag) => Assert.AreEqual(frag, C.IsFragment(name));

        [TestCase("JNT_Wall_frag_01__Wall_frag_02", "Wall_frag_01", "Wall_frag_02")]
        [TestCase("JNT_Wall_frag_01__Wall_frag_02_01", "Wall_frag_01", "Wall_frag_02")]
        public void Joint(string name, string a, string b)
        {
            Assert.IsTrue(C.TryParseJoint(name, out var ga, out var gb));
            Assert.AreEqual(a, ga); Assert.AreEqual(b, gb);
        }

        [Test] public void JointNeedsTwoFragments() => Assert.IsFalse(C.TryParseJoint("JNT_Wall", out _, out _));
        [Test] public void Sockets() { Assert.AreEqual("Muzzle", C.SocketName("Socket_Muzzle")); Assert.AreEqual("Muzzle", C.SocketName("Muzzle_Spawn")); }
        [Test]
        public void LodChainRequirementExcludesFracturedKit()
        {
            Assert.AreEqual(C.AssetClass.KitFractured, C.Classify("Assets/_Project/Models/BuildingKit/Kit_Brick/Kit_Brick_Wall_Fractured.fbx"));
            Assert.IsFalse(C.RequiresLodChain(C.Classify("Assets/_Project/Models/BuildingKit/Kit_Brick/Kit_Brick_Wall_Fractured.fbx")));
            Assert.IsTrue(C.LodZeroOnly(C.AssetClass.KitFractured));
            Assert.IsTrue(C.RequiresLodChain(C.Classify("Assets/_Project/Models/BuildingKit/Kit_Brick/Kit_Brick_Wall.fbx")));
            Assert.IsTrue(C.RequiresLodChain(C.Classify("Assets/_Project/Models/Island_Sunnyville/Island_Sunnyville.fbx")));
            Assert.IsTrue(C.RequiresLodChain(C.Classify("Assets/_Project/Models/Wrecker_v2/Wrecker_v2_LOD0.fbx")));
        }

        [Test] public void FragmentLimitIs400() => Assert.AreEqual(400, C.FragmentTriLimit);

        [Test] public void KitPath() { Assert.IsTrue(C.KitPathOk("Assets/_Project/Models/BuildingKit/Kit_Wood/Kit_Wood_Wall.fbx")); Assert.IsFalse(C.KitPathOk("Assets/_Project/Models/Kit_Wood/Kit_Wood_Wall.fbx"));
            Assert.IsTrue(C.KitPathOk("Assets/_Project/Models/Realistic/Buildings/Kit_RealBrick/Kit_RealBrick_Wall.fbx"), "realistic v1 pieces");
            Assert.IsFalse(C.KitPathOk("Assets/_Project/Models/Realistic/Foliage/Oak/Oak.fbx"), "only Kit_Real* under Realistic"); }
    }

    public class ImportedPhysicsHintsTests
    {
        [Test]
        public void ReadsSIPropertiesAndNeverOverwritesWithMissingOrBlank()
        {
            var go = new GameObject("Wall_frag_01");
            try
            {
                var h = go.AddComponent<ImportedPhysicsHints>();
                h.breakForce = 7f; h.anchorTo = "Keep";
                CrashverseImportPostprocessor.ApplyUserProperties(go,
                    new[] { "mass_kg", "break_force_N", "break_torque_Nm", "anchor_to", "density_kg_m3" },
                    new object[] { "12.5", "", 800.0, "", 600 }, "Assets/_Project/Models/BuildingKit/x.fbx");
                Assert.AreEqual(1, go.GetComponents<ImportedPhysicsHints>().Length);
                Assert.AreEqual(12.5f, h.mass, 1e-4f);
                Assert.AreEqual(7f, h.breakForce, "blank break_force_N must not overwrite");
                Assert.AreEqual(800f, h.breakTorque, 1e-4f);
                Assert.AreEqual("Keep", h.anchorTo, "blank anchor_to must not overwrite");

                CrashverseImportPostprocessor.ApplyUserProperties(go, new[] { "anchor_to", "break_force_N" }, new object[] { "Ground", 450f }, "Assets/_Project/Models/BuildingKit/x.fbx");
                Assert.AreEqual("Ground", h.anchorTo);
                Assert.AreEqual(450f, h.breakForce);
                Assert.AreEqual(12.5f, h.mass, 1e-4f, "missing mass_kg must not overwrite");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NoPropertiesAddsNoComponent()
        {
            var go = new GameObject("Carriage");
            try
            {
                CrashverseImportPostprocessor.ApplyUserProperties(go, new[] { "foo" }, new object[] { 1 }, "Assets/_Project/Models/Wrecker_v2/x.fbx");
                Assert.IsNull(go.GetComponent<ImportedPhysicsHints>());
            }
            finally { Object.DestroyImmediate(go); }
        }
    }

    /// <summary>Builds a tiny procedural fractured kit piece at test time (no committed binaries), runs PrefabBuilder and checks the output.
    /// Temp folders live under Models/BuildingKit/__ImportTest and Prefabs/Models/__ImportTest and are deleted afterwards.</summary>
    public class PrefabBuilderImportTests
    {
        const string Dir = "Assets/_Project/Models/BuildingKit/__ImportTest";
        const string PrefabDir = C.PrefabRoot + "/__ImportTest";
        const string Name = "Kit_Test_Wall_Fractured";

        static GameObject Part(string name, Transform parent, Vector3 pos, bool mesh = true)
        {
            GameObject go;
            if (mesh) { go = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(go.GetComponent<Collider>()); go.name = name; }
            else go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go;
        }

        static void Mk(string path)
        {
            var parts = path.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string p = string.Join("/", parts.Take(i + 1));
                if (!AssetDatabase.IsValidFolder(p)) AssetDatabase.CreateFolder(string.Join("/", parts.Take(i)), parts[i]);
            }
        }

        [SetUp] public void SetUp() { Mk(Dir + "/textures"); }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Dir);
            AssetDatabase.DeleteAsset(PrefabDir);
            AssetDatabase.Refresh();
        }

        [Test]
        public void BuildsCollidersLodSocketsAndSortsFragments()
        {
            var root = new GameObject(Name);
            string src = $"{Dir}/{Name}.prefab";
            try
            {
                // Deliberately unsorted.
                Part("Wall_frag_02", root.transform, new Vector3(1, 0, 0));
                Part("Wall_frag_01", root.transform, Vector3.zero);
                Part("UCX_Wall_frag_01", root.transform, Vector3.zero);                 // same pose -> on the fragment
                Part("UCX_Wall_frag_02", root.transform, new Vector3(1, 0.25f, 0));     // pivot differs -> COL_Wall_frag_02 child
                Part("UCX_Missing_frag_09", root.transform, Vector3.zero);              // no target -> error, skipped
                Part("JNT_Wall_frag_01__Wall_frag_02", root.transform, new Vector3(0.5f, 0, 0), false);
                Part("ANCHOR_Wall_frag_01", root.transform, Vector3.zero, false);
                Part("Socket_Muzzle", root.transform, new Vector3(0, 1, 0), false);
                PrefabUtility.SaveAsPrefabAsset(root, src);
            }
            finally { Object.DestroyImmediate(root); }

            LogAssert.Expect(LogType.Error, new Regex("UCX_Missing_frag_09.*Skipped"));
            var built = CrashversePrefabBuilder.Build($"{Dir}/{Name}", new System.Collections.Generic.Dictionary<int, string> { { -1, src } });
            Assert.IsNotNull(built);
            Assert.AreEqual($"{PrefabDir}/{Name}.prefab", AssetDatabase.GetAssetPath(built));
            var t = built.transform;

            Transform F(string n) => CrashverseImportPostprocessor.FindDeep(t, n);
            var f1 = F("Wall_frag_01"); var f2 = F("Wall_frag_02");
            Assert.IsNotNull(f1); Assert.IsNotNull(f2);

            // Exactly one convex collider per fragment, none anywhere else, no root fallback.
            var mc1 = f1.GetComponentsInChildren<MeshCollider>(true).Where(c => c.transform == f1 || c.transform.parent == f1).ToArray();
            var mc2 = f2.GetComponentsInChildren<MeshCollider>(true).Where(c => c.transform == f2 || c.transform.parent == f2).ToArray();
            Assert.AreEqual(1, mc1.Length); Assert.AreSame(f1, mc1[0].transform); Assert.IsTrue(mc1[0].convex);
            Assert.AreEqual(1, mc2.Length); Assert.AreEqual("COL_Wall_frag_02", mc2[0].name); Assert.IsTrue(mc2[0].convex);
            Assert.AreEqual(2, built.GetComponentsInChildren<Collider>(true).Length);
            Assert.IsNull(built.GetComponent<BoxCollider>());
            Assert.IsFalse(built.GetComponentsInChildren<Transform>(true).Any(x => x.name.StartsWith("UCX_")), "hull objects removed");

            // Fractured kit: LOD0 only, no LODGroup (Physics: fragments need no LODs).
            Assert.IsNull(built.GetComponent<LODGroup>());
            CollectionAssert.AreEquivalent(new[] { "Wall_frag_01", "Wall_frag_02" }, built.GetComponentsInChildren<Renderer>(true).Select(r => r.name).ToArray());

            // Helpers kept.
            Assert.IsNotNull(F("Socket_Muzzle"));
            Assert.IsNotNull(F("JNT_Wall_frag_01__Wall_frag_02"));
            Assert.IsNotNull(F("ANCHOR_Wall_frag_01"));

            // Ordinal order: fragments then joints.
            Assert.Less(f1.GetSiblingIndex(), f2.GetSiblingIndex());
            Assert.Less(f2.GetSiblingIndex(), F("JNT_Wall_frag_01__Wall_frag_02").GetSiblingIndex());

            // No physics components.
            Assert.IsEmpty(built.GetComponentsInChildren<Rigidbody>(true));
            Assert.IsEmpty(built.GetComponentsInChildren<Joint>(true));
        }

        /// <summary>Saves a procedural model prefab named <paramref name="name"/> with meshes of the given triangle counts per LOD name.</summary>
        static string SaveModel(string name, params (string obj, int tris)[] parts)
        {
            var root = new GameObject(name);
            try
            {
                foreach (var (obj, tris) in parts)
                {
                    var mesh = new Mesh { name = obj + "_mesh" };
                    var v = new Vector3[tris * 3]; var idx = new int[tris * 3];
                    for (int i = 0; i < tris; i++)
                    {
                        float x = i * 0.01f;
                        v[i * 3] = new Vector3(x, 0, 0); v[i * 3 + 1] = new Vector3(x, 1, 0); v[i * 3 + 2] = new Vector3(x + 0.005f, 0, 1);
                        idx[i * 3] = i * 3; idx[i * 3 + 1] = i * 3 + 1; idx[i * 3 + 2] = i * 3 + 2;
                    }
                    mesh.vertices = v; mesh.triangles = idx;
                    AssetDatabase.CreateAsset(mesh, $"{Dir}/{obj}_mesh.asset");
                    var go = new GameObject(obj);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>();
                }
                string path = $"{Dir}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return path;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static GameObject BuildOne(string name, string path) =>
            CrashversePrefabBuilder.Build($"{Dir}/{name}", new System.Collections.Generic.Dictionary<int, string> { { -1, path } });

        [Test]
        public void OverBudgetKitPieceIsRejectedAndNoPrefabWritten()
        {
            string path = SaveModel("Kit_Test_Big", ("Big_LOD0", 1600), ("Big_LOD1", 400), ("Big_LOD2", 100));
            LogAssert.Expect(LogType.Error, new Regex(@"Kit_Test_Big: LOD0 1600 tris over budget 1500\. Prefab NOT built"));
            Assert.IsNull(BuildOne("Kit_Test_Big", path));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Kit_Test_Big.prefab"));
        }

        [Test]
        public void WithinBudgetKitPieceBuilds()
        {
            string path = SaveModel("Kit_Test_Ok", ("Ok_LOD0", 1500), ("Ok_LOD1", 400), ("Ok_LOD2", 100));
            var built = BuildOne("Kit_Test_Ok", path);
            Assert.IsNotNull(built);
            Assert.AreEqual(3, built.GetComponent<LODGroup>().GetLODs().Length);
        }

        [Test]
        public void FracturedKitIgnoresLodObjectsWithWarning()
        {
            string path = SaveModel("Kit_Test_Lods_Fractured", ("Wall_frag_01", 20), ("Wall_frag_01_LOD1", 10), ("Wall_frag_02", 20), ("Wall_frag_02_LOD2", 5));
            LogAssert.Expect(LogType.Warning, new Regex(@"Kit_Test_Lods_Fractured: fractured kit pieces use LOD0 only; ignoring 2 LOD1/LOD2 objects"));
            var built = BuildOne("Kit_Test_Lods_Fractured", path);
            Assert.IsNotNull(built);
            Assert.IsNull(built.GetComponent<LODGroup>());
            CollectionAssert.AreEquivalent(new[] { "Wall_frag_01", "Wall_frag_02" }, built.GetComponentsInChildren<Renderer>(true).Select(r => r.name).ToArray());
        }

        [Test]
        public void OverBudgetFracturedTotalIsRejected()
        {
            string path = SaveModel("Kit_Test_Heavy_Fractured", ("Heavy_frag_01", 280), ("Heavy_frag_02", 280), ("Heavy_frag_03", 280), ("Heavy_frag_04", 280), ("Heavy_frag_05", 280), ("Heavy_frag_06", 101));
            LogAssert.Expect(LogType.Error, new Regex(@"Kit_Test_Heavy_Fractured: LOD0 1501 tris over budget 1500\. Prefab NOT built"));
            Assert.IsNull(BuildOne("Kit_Test_Heavy_Fractured", path));
        }

        [Test]
        public void FragmentAt400TrisIsRejected_399Builds()
        {
            string bad = SaveModel("Kit_Test_Frag400_Fractured", ("Big_frag_01", 400), ("Big_frag_02", 100));
            LogAssert.Expect(LogType.Error, new Regex(@"Kit_Test_Frag400_Fractured: fragment Big_frag_01 has 400 tris, must be < 400\. Prefab NOT built"));
            Assert.IsNull(BuildOne("Kit_Test_Frag400_Fractured", bad));
            string ok = SaveModel("Kit_Test_Frag399_Fractured", ("Ok_frag_01", 399), ("Ok_frag_02", 100));
            Assert.IsNotNull(BuildOne("Kit_Test_Frag399_Fractured", ok));
        }

        [TestCase("Kit_Test_NoLod2", "LOD2")]
        [TestCase("Island_Test_NoLod1", "LOD1")]
        [TestCase("Wrecker_Test_NoLods", "LOD1")]
        public void MissingLodIsRejected(string name, string missing)
        {
            string path = missing == "LOD2" ? SaveModel(name, ("P_LOD0", 600), ("P_LOD1", 200))
                        : name.StartsWith("Island") ? SaveModel(name, ("P_LOD0", 600), ("P_LOD2", 100))
                        : SaveModel(name, ("Body", 600));
            LogAssert.Expect(LogType.Error, new Regex($"{name}: {missing} missing.*Prefab NOT built"));
            if (name.StartsWith("Wrecker")) LogAssert.Expect(LogType.Error, new Regex($"{name}: LOD2 missing.*Prefab NOT built"));
            Assert.IsNull(BuildOne(name, path));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab"));
        }

        [Test]
        public void OverBudgetWreckerIsRejected()
        {
            string path = SaveModel("Wrecker_Test_Big", ("W_LOD0", 12001), ("W_LOD1", 4000), ("W_LOD2", 1000));
            LogAssert.Expect(LogType.Error, new Regex(@"LOD0 12001 tris over budget 12000"));
            Assert.IsNull(BuildOne("Wrecker_Test_Big", path));
        }

        static void WritePng(string path, Color c)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.SetPixels(Enumerable.Repeat(c, 16).ToArray()); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void HeroBuildingTextureImportsAt1024_KitStays512()
        {
            WritePng($"{Dir}/textures/T_Bld_TestHero_A.png", Color.white);
            WritePng($"{Dir}/textures/T_Bld_TestHero_N.png", new Color(0.5f, 0.5f, 1f));
            WritePng($"{Dir}/textures/T_TestKitB_A.png", Color.white);
            TextureImporterPlatformSettings A(string stem) => ((TextureImporter)AssetImporter.GetAtPath($"{Dir}/textures/{stem}.png")).GetPlatformTextureSettings("Android");
            Assert.AreEqual(1024, A("T_Bld_TestHero_A").maxTextureSize, "T_Bld_* hero building maps cap at 1024 on Android");
            Assert.AreEqual(TextureImporterFormat.ASTC_6x6, A("T_Bld_TestHero_A").format);
            Assert.AreEqual(1024, A("T_Bld_TestHero_N").maxTextureSize);
            Assert.AreEqual(TextureImporterFormat.ASTC_5x5, A("T_Bld_TestHero_N").format);
            Assert.AreEqual(512, A("T_TestKitB_A").maxTextureSize, "other BuildingKit maps keep the 512 kit cap");
        }

        [Test]
        public void InnerMaterialPrefersInnerMapsAndFallsBackPerMap()
        {
            string tex = $"{Dir}/textures/T_Brick";
            WritePng(tex + "_A.png", Color.red);
            WritePng(tex + "_N.png", new Color(0.5f, 0.5f, 1f));
            WritePng(tex + "_ORM.png", new Color(1f, 0.6f, 0f));
            WritePng(tex + "_Inner_A.png", Color.gray);           // inner albedo + normal present, no inner ORM
            WritePng(tex + "_Inner_N.png", new Color(0.5f, 0.5f, 1f));
            var shader = Shader.Find(C.ShaderName);
            var inner = MaterialWiring.CreateOrWire("M_Brick_Inner", Dir, shader);
            var outer = MaterialWiring.CreateOrWire("M_Brick", Dir, shader);
            Texture2D L(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            Assert.AreEqual(L(tex + "_Inner_A.png"), inner.GetTexture("_BaseMap"));
            Assert.AreEqual(L(tex + "_Inner_N.png"), inner.GetTexture("_BumpMap"));
            Assert.AreEqual(L(tex + "_ORM.png"), inner.GetTexture("_ORMMap"), "missing inner ORM falls back to main");
            Assert.AreEqual(1f, inner.GetFloat("_InnerFace"));
            Assert.AreEqual(L(tex + "_A.png"), outer.GetTexture("_BaseMap"), "outer never uses inner maps");
            Assert.AreEqual(L(tex + "_N.png"), outer.GetTexture("_BumpMap"));
            Assert.AreEqual(256, ((TextureImporter)AssetImporter.GetAtPath(tex + "_Inner_A.png")).GetPlatformTextureSettings("Android").maxTextureSize);
        }

        [Test]
        public void WiresGlossyPbrMaterialsAndTextureImport()
        {
            string tex = $"{Dir}/textures/T_TestKit";
            WritePng(tex + "_A.png", Color.white);
            WritePng(tex + "_N.png", new Color(0.5f, 0.5f, 1f));
            WritePng(tex + "_ORM.png", new Color(1f, 0.5f, 0f));
            WritePng(tex + "_E.png", Color.cyan);

            var n = (TextureImporter)AssetImporter.GetAtPath(tex + "_N.png");
            var orm = (TextureImporter)AssetImporter.GetAtPath(tex + "_ORM.png");
            var a = (TextureImporter)AssetImporter.GetAtPath(tex + "_A.png");
            Assert.AreEqual(TextureImporterType.NormalMap, n.textureType);
            Assert.IsFalse(orm.sRGBTexture);
            Assert.IsTrue(a.sRGBTexture);
            var android = orm.GetPlatformTextureSettings("Android");
            Assert.IsTrue(android.overridden);
            Assert.AreEqual(TextureImporterFormat.ASTC_6x6, android.format);
            Assert.AreEqual(TextureImporterFormat.ASTC_5x5, n.GetPlatformTextureSettings("Android").format);
            Assert.AreEqual(512, android.maxTextureSize, "kit maps cap at 512 on Android");

            var shader = Shader.Find(C.ShaderName);
            Assert.IsNotNull(shader, C.ShaderName + " must exist (vfx/v1-foundation)");

            var m = MaterialWiring.CreateOrWire("M_TestKit_Emissive", Dir, shader);
            var inner = MaterialWiring.CreateOrWire("M_TestKit_Inner", Dir, shader);
            Assert.AreEqual(C.ShaderName, m.shader.name);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_A.png"), m.GetTexture("_BaseMap"));
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_N.png"), m.GetTexture("_BumpMap"));
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_ORM.png"), m.GetTexture("_ORMMap"));
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_E.png"), m.GetTexture("_EmissionMap"));
            Assert.AreEqual(0f, m.GetFloat("_InnerFace"));
            Assert.Greater(m.GetColor("_EmissionColor").maxColorComponent, 1f, "emissive default ~2.5");
            Assert.AreEqual(1f, inner.GetFloat("_InnerFace"));
            Assert.IsEmpty(m.shaderKeywords, "GlossyPBR uses no keywords");
            Assert.IsEmpty(inner.shaderKeywords);
        }
    }

    /// <summary>Hero buildings (PM-approved via 3D Modeler): 1024 Android cap, fracture class from models/HeroBuildings/Kit_&lt;B&gt;_stats.json
    /// with an FBX fracture_class override; missing JSON or entry never overwrites.</summary>
    public class HeroBuildingImportTests
    {
        // Verbatim shape of the 3D Modeler sidecar (Kit_House_stats.json, trimmed to two modules plus test entries).
        const string HouseJson = @"{
 ""building"": ""House"",
 ""texel_px_per_m"": 77.1,
 ""atlas"": 2048,
 ""modules"": {
  ""GroundDoor"": {
   ""LOD0"": 640, ""LOD1"": 319, ""LOD2"": 126, ""ucx"": 8,
   ""aabb"": [[-2.0, -1.0499999523162842, 0.0], [2.0, 0.20000000298023224, 2.799999952316284]],
   ""frac"": ""Stone"", ""anchor"": ""bottom"", ""anchor_to"": ""Ground"",
   ""note"": ""ground floor front: arched blue door, window, shop sign, lantern"",
   ""frags"": 7, ""frag_max"": 213, ""Fractured"": 1057, ""mass"": ""686.8-1774.3"", ""joints"": 10, ""anchors"": 4, ""trims_dropped"": 0
  },
  ""Floor"": { ""LOD0"": 92, ""frac"": ""Wood"", ""anchor_to"": ""Support"", ""frags"": 12 },
  ""NoClass"": { ""LOD0"": 300 }
 }
}";

        [TestCase("T_Bld_X_A", "Assets/_Project/Models/Buildings/Bld_X/textures/T_Bld_X_A.png", 1024)]
        [TestCase("T_Bld_X_ORM", "Assets/_Project/Models/BuildingKit/Kit_X/textures/T_Bld_X_ORM.png", 1024)]
        [TestCase("T_House_A", "Assets/_Project/Models/BuildingKit/Kit_House/textures/T_House_A.png", 1024)]
        [TestCase("T_House_Inner_A", "Assets/_Project/Models/BuildingKit/Kit_House/textures/T_House_Inner_A.png", 256)]
        [TestCase("T_Bld_X_Inner_N", "Assets/_Project/Models/Buildings/Bld_X/textures/T_Bld_X_Inner_N.png", 256)]
        [TestCase("T_Brick_A", "Assets/_Project/Models/BuildingKit/Kit_Brick/textures/T_Brick_A.png", 512)]
        [TestCase("T_House_A", "Assets/_Project/Models/BuildingKit/Kit_Brick/textures/T_House_A.png", 512)]   // folder, not file name
        [TestCase("T_Wrecker_A", "Assets/_Project/Models/Wrecker_v2/textures/T_Wrecker_A.png", 1024)]
        [TestCase("T_Keep_N", "Assets/_Project/Models/BuildingKit/Kit_Keep/textures/T_Keep_N.png", 1024)]
        [TestCase("T_Tower_ORM", "Assets/_Project/Models/BuildingKit/Kit_Tower/textures/T_Tower_ORM.png", 1024)]
        [TestCase("T_Gate_A", "Assets/_Project/Models/BuildingKit/Kit_Gate/textures/T_Gate_A.png", 1024)]
        [TestCase("T_Props_A", "Assets/_Project/Models/BuildingKit/Kit_Props/textures/T_Props_A.png", 512)]   // shared props: kit cap (PM)
        [TestCase("T_Props_N", "Assets/_Project/Models/BuildingKit/Kit_Props/textures/T_Props_N.png", 512)]
        public void AndroidTextureCap(string stem, string path, int max) => Assert.AreEqual(max, C.PolicyFor(stem, path).androidMax);

        [Test]
        public void HeroCapKeepsAstcFormats()
        {
            const string d = "Assets/_Project/Models/Buildings/Bld_X/textures/";
            Assert.AreEqual("6x6", C.PolicyFor("T_Bld_X_A", d + "T_Bld_X_A.png").astc);
            Assert.AreEqual("5x5", C.PolicyFor("T_Bld_X_N", d + "T_Bld_X_N.png").astc);
            Assert.AreEqual("6x6", C.PolicyFor("T_Bld_X_ORM", d + "T_Bld_X_ORM.png").astc);
            Assert.AreEqual(512, C.PolicyFor("T_Bld_X_E", d + "T_Bld_X_E.png").androidMax, "_E stays <= 512");
        }

        [Test]
        public void HeroModuleFromFolderAndFileName()
        {
            Assert.IsTrue(C.TryHeroModule("Assets/_Project/Models/BuildingKit/Kit_House/Kit_House_GroundDoor_Fractured.fbx", out var k, out var m, out var f));
            Assert.AreEqual("House", k); Assert.AreEqual("GroundDoor", m); Assert.IsTrue(f);
            Assert.IsTrue(C.TryHeroModule("Assets/_Project/Models/BuildingKit/Kit_Keep/Kit_Keep_Wall_LOD2.fbx", out _, out m, out f));
            Assert.AreEqual("Wall", m); Assert.IsFalse(f);
            Assert.IsFalse(C.TryHeroModule("Assets/_Project/Models/BuildingKit/Kit_Brick/Kit_Brick_Wall.fbx", out _, out _, out _), "regular kit");
            Assert.IsTrue(C.TryHeroModule("Assets/_Project/Models/BuildingKit/Kit_Props/Kit_Props_Barrel_Fractured.fbx", out k, out m, out f), "Kit_Props reads its sidecar");
            Assert.AreEqual("Props", k); Assert.AreEqual("Barrel", m); Assert.IsTrue(f);
            Assert.IsFalse(C.IsHeroBuildingKit("Props")); Assert.IsTrue(C.IsHeroBuildingKit("House"));
            var prev = C.HeroStatsExists;
            try
            {
                C.HeroStatsExists = kit => kit == "Mill";   // any kit with a sidecar reads fractureClass from it...
                Assert.IsTrue(C.TryHeroModule("Assets/_Project/Models/BuildingKit/Kit_Mill/Kit_Mill_Sail.fbx", out _, out m, out _));
                Assert.AreEqual("Sail", m);
                // ...but a sidecar does not make it a hero building: kit texture cap.
                Assert.AreEqual(512, C.PolicyFor("T_Mill_A", "Assets/_Project/Models/BuildingKit/Kit_Mill/textures/T_Mill_A.png").androidMax);
            }
            finally { C.HeroStatsExists = prev; }
        }

        [Test]
        public void ParsesRealSidecarSchema()
        {
            var st = C.ParseHeroStats(HouseJson);
            Assert.AreEqual("House", st.building);
            CollectionAssert.AreEqual(new[] { "GroundDoor", "Floor", "NoClass" }, st.modules.Keys.ToArray());
            Assert.AreEqual("Stone", st.ClassFor("GroundDoor"));
            Assert.AreEqual("Wood", st.ClassFor("Floor"));
            Assert.IsTrue(st.HasModule("NoClass")); Assert.IsNull(st.ClassFor("NoClass"));
            Assert.IsFalse(st.HasModule("Chimney")); Assert.IsNull(st.ClassFor("Chimney"));
            Assert.Throws<System.FormatException>(() => C.ParseHeroStats(@"{""building"":""House""}"));
            Assert.Throws<System.FormatException>(() => C.ParseHeroStats(@"{""modules"":{"));
        }

        static GameObject Module(string name, params string[] frags)
        {
            var root = new GameObject(name);
            foreach (var f in frags) new GameObject(f).transform.SetParent(root.transform, false);
            return root;
        }
        static string Cls(GameObject root, string child = null)
        {
            var t = child == null ? root.transform : CrashverseImportPostprocessor.FindDeep(root.transform, child);
            var h = t.GetComponent<ImportedPhysicsHints>();
            return h != null ? h.fractureClass : null;
        }

        const string FracturedPath = "Assets/_Project/Models/BuildingKit/Kit_House/Kit_House_GroundDoor_Fractured.fbx";

        [Test]
        public void JsonClassGoesOnRootAndEveryLod0Fragment()
        {
            var root = Module("Kit_House_GroundDoor_Fractured", "GroundDoor_frag_01", "GroundDoor_frag_02", "GroundDoor_frag_02_LOD1", "UCX_GroundDoor_frag_01", "JNT_GroundDoor_frag_01__GroundDoor_frag_02");
            try
            {
                int n = CrashverseImportPostprocessor.ApplyFractureClass(root, C.ParseHeroStats(HouseJson).ClassFor("GroundDoor"), FracturedPath);
                Assert.AreEqual(3, n);
                Assert.AreEqual("Stone", Cls(root));
                Assert.AreEqual("Stone", Cls(root, "GroundDoor_frag_01"));
                Assert.AreEqual("Stone", Cls(root, "GroundDoor_frag_02"));
                Assert.IsNull(Cls(root, "GroundDoor_frag_02_LOD1"), "LOD objects are not fragments to write");
                Assert.IsNull(Cls(root, "UCX_GroundDoor_frag_01"));
                Assert.IsNull(Cls(root, "JNT_GroundDoor_frag_01__GroundDoor_frag_02"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FbxFractureClassOverridesJson()
        {
            var root = Module("Kit_House_GroundDoor_Fractured", "GroundDoor_frag_01", "GroundDoor_frag_02", "GroundDoor_frag_03");
            try
            {
                // fracture_class on the module root (applies to its fragments) and on one fragment (applies to that fragment only).
                CrashverseImportPostprocessor.ApplyUserProperties(root, new[] { "fracture_class" }, new object[] { "Steel" }, FracturedPath);
                var f2 = CrashverseImportPostprocessor.FindDeep(root.transform, "GroundDoor_frag_02").gameObject;
                CrashverseImportPostprocessor.ApplyUserProperties(f2, new[] { "fracture_class", "mass_kg" }, new object[] { "Glass", 12.0 }, FracturedPath);
                var f3 = CrashverseImportPostprocessor.FindDeep(root.transform, "GroundDoor_frag_03").gameObject;
                CrashverseImportPostprocessor.ApplyUserProperties(f3, new[] { "fracture_class", "mass_kg" }, new object[] { "", 5.0 }, FracturedPath); // blank = not authored

                CrashverseImportPostprocessor.ApplyFractureClass(root, "Stone", FracturedPath);
                Assert.AreEqual("Steel", Cls(root), "root FBX value wins over JSON");
                Assert.AreEqual("Steel", Cls(root, "GroundDoor_frag_01"), "fragments inherit the module's FBX value");
                Assert.AreEqual("Glass", Cls(root, "GroundDoor_frag_02"), "fragment FBX value wins");
                Assert.AreEqual(12f, f2.GetComponent<ImportedPhysicsHints>().mass, 1e-4f);
                Assert.AreEqual("Steel", Cls(root, "GroundDoor_frag_03"));
                Assert.AreEqual(1, f3.GetComponents<ImportedPhysicsHints>().Length);

                // FBX value with no JSON at all still applies.
                var r2 = Module("Kit_House_Odd", "Odd_frag_01");
                try
                {
                    CrashverseImportPostprocessor.ApplyUserProperties(r2, new[] { "fracture_class" }, new object[] { "Wood" }, FracturedPath);
                    CrashverseImportPostprocessor.ApplyFractureClass(r2, null, FracturedPath);
                    Assert.AreEqual("Wood", Cls(r2)); Assert.AreEqual("Wood", Cls(r2, "Odd_frag_01"));
                }
                finally { Object.DestroyImmediate(r2); }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MissingJsonOrEntryNeverOverwrites()
        {
            var root = Module("Kit_House_Chimney_Fractured", "Chimney_frag_01", "Chimney_frag_02");
            try
            {
                var h1 = CrashverseImportPostprocessor.FindDeep(root.transform, "Chimney_frag_01").gameObject.AddComponent<ImportedPhysicsHints>();
                h1.mass = 40f; h1.fractureClass = "Brick";
                var st = C.ParseHeroStats(HouseJson);
                Assert.AreEqual(0, CrashverseImportPostprocessor.ApplyFractureClass(root, st.ClassFor("Chimney"), FracturedPath), "missing entry");
                Assert.AreEqual(0, CrashverseImportPostprocessor.ApplyFractureClass(root, st.ClassFor("NoClass"), FracturedPath), "entry without frac");
                Assert.AreEqual(0, CrashverseImportPostprocessor.ApplyFractureClass(root, null, FracturedPath), "missing JSON");
                Assert.IsNull(root.GetComponent<ImportedPhysicsHints>(), "no component added when there is no value");
                Assert.IsNull(CrashverseImportPostprocessor.FindDeep(root.transform, "Chimney_frag_02").GetComponent<ImportedPhysicsHints>());
                Assert.AreEqual("Brick", h1.fractureClass); Assert.AreEqual(40f, h1.mass);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ValidatorModuleCrossCheckWarnsBothWays()
        {
            var w = C.CompareHeroModules("House", new[] { "GroundDoor", "Floor", "Chimney" }, new[] { "Floor", "GroundDoor", "Cellar" });
            Assert.AreEqual(2, w.Count);
            StringAssert.Contains("module Chimney is in models/HeroBuildings/Kit_House_stats.json but there is no Kit_House_Chimney.fbx", w[0]);
            StringAssert.Contains("Kit_House_Cellar has no entry in models/HeroBuildings/Kit_House_stats.json", w[1]);
            Assert.IsEmpty(C.CompareHeroModules("House", new[] { "A" }, new[] { "A" }));
            Assert.AreEqual(1, C.CompareHeroModules("House", new[] { "A" }, null).Count, "JSON present, no FBX yet");
        }
    }
}
