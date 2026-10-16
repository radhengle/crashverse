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
        [Test] public void KitPath() { Assert.IsTrue(C.KitPathOk("Assets/_Project/Models/BuildingKit/Kit_Wood/Kit_Wood_Wall.fbx")); Assert.IsFalse(C.KitPathOk("Assets/_Project/Models/Kit_Wood/Kit_Wood_Wall.fbx")); }
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
                Part("Wall_frag_02_LOD1", root.transform, new Vector3(1, 0, 0));
                Part("Wall_frag_01", root.transform, Vector3.zero);
                Part("Wall_frag_01_LOD1", root.transform, Vector3.zero);
                Part("Wall_frag_01_LOD2", root.transform, Vector3.zero);
                Part("Wall_frag_02_LOD2", root.transform, new Vector3(1, 0, 0));
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

            // LOD group: LOD0 = fragments, LOD1 = _LOD1 renderers.
            var lg = built.GetComponent<LODGroup>();
            Assert.IsNotNull(lg);
            var lods = lg.GetLODs();
            Assert.AreEqual(3, lods.Length);
            CollectionAssert.AreEquivalent(new[] { "Wall_frag_01", "Wall_frag_02" }, lods[0].renderers.Select(r => r.name).ToArray());
            CollectionAssert.AreEquivalent(new[] { "Wall_frag_01_LOD1", "Wall_frag_02_LOD1" }, lods[1].renderers.Select(r => r.name).ToArray());
            CollectionAssert.AreEquivalent(new[] { "Wall_frag_01_LOD2", "Wall_frag_02_LOD2" }, lods[2].renderers.Select(r => r.name).ToArray());
            Assert.AreEqual(C.LodHeights(C.AssetClass.KitFractured)[2], lods[2].screenRelativeTransitionHeight, 1e-5f);

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
}
