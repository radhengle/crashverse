using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    /// <summary>
    /// The Wrecker as in the concept art: navy barrel with gold bands, muzzle swell and cascabel on a gold-trimmed wooden truck carriage
    /// with spoked wheels (Resources/Kingdom/Meshes/Cannon_*.bytes, built by Tools/Blender/kingdom/build_assets.py). The procedural
    /// greybox parts keep their transforms, colliders and FeelDirector bindings; only their renderers are switched off. The carriage
    /// follows the yaw pivot (a real carriage turns with the barrel) and the barrel follows the pitch pivot (aim + recoil).
    /// </summary>
    public static class CannonDresser
    {
        public const float VisualScale = 1.0f; // the locked camera frames WreckerSpec's envelope (PlayMode AssertCannonFramedFromBehind)

        public static bool Dress(Crashverse.Weapons.WreckerBuilder wb)
        {
            if (wb == null || wb.yawPivot == null || wb.pitchPivot == null) return false;
            var barrel = CvMesh.Load("Kingdom/Meshes/Cannon_Barrel"); var carriage = CvMesh.Load("Kingdom/Meshes/Cannon_Carriage");
            if (barrel.Count == 0 || carriage.Count == 0) return false;
            foreach (var r in wb.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;

            var bRoot = new GameObject("Kingdom_Barrel").transform; bRoot.SetParent(wb.pitchPivot, false);
            bRoot.localScale = Vector3.one * VisualScale;
            foreach (var kv in barrel) Part(kv.Key, kv.Value, bRoot, KingdomMaterials.TileFor(kv.Key));

            var cRoot = new GameObject("Kingdom_Carriage").transform; cRoot.SetParent(wb.yawPivot, false);
            cRoot.localPosition = new Vector3(0f, -wb.yawPivot.localPosition.y, 0f) * VisualScale; // ground under the pivot
            cRoot.localPosition = new Vector3(0f, -wb.yawPivot.localPosition.y * VisualScale, 0f);
            cRoot.localScale = Vector3.one * VisualScale;
            foreach (var kv in carriage) Part(kv.Key, kv.Value, cRoot, kv.Key.EndsWith("_Wood") ? 0.9f : KingdomMaterials.TileFor(kv.Key), true);

            // Hero-asset detailing: orange flame crests and visible timber wheel spokes.
            // Render-only additions preserve the existing colliders, yaw/pitch pivots and projectile spawn.
            AddFlameCrests(bRoot);
            AddWheelSpokes(cRoot);
            return true;
        }

        static void AddFlameCrests(Transform barrelRoot)
        {
            // Barrel axis is +X. Put a crest on both lateral faces for readability throughout the orbit.
            var points = new Vector2[]
            {
                new Vector2(-0.18f, -0.22f), new Vector2(-0.08f, -0.08f),
                new Vector2(-0.12f, 0.08f), new Vector2(-0.02f, 0.02f),
                new Vector2(0.02f, 0.22f), new Vector2(0.13f, 0.10f),
                new Vector2(0.18f, -0.04f), new Vector2(0.10f, -0.02f),
                new Vector2(0.04f, -0.16f), new Vector2(-0.04f, -0.10f)
            };
            var mat = KingdomMaterials.Make("CannonFlameCrest", null, new Color(1f, 0.30f, 0.035f), 0.28f, 0.32f,
                Vector2.one, new Color(0.22f, 0.035f, 0.002f));
            for (int side = -1; side <= 1; side += 2)
            {
                var mesh = new Mesh { name = "Cannon_FlameCrest" };
                var vertices = new Vector3[points.Length + 1];
                vertices[0] = new Vector3(0.38f, side * 0.523f, 0.02f);
                for (int i = 0; i < points.Length; i++)
                    vertices[i + 1] = new Vector3(0.38f + points[i].x, side * 0.523f, 0.02f + points[i].y);
                var triangles = new int[points.Length * 3];
                for (int i = 0; i < points.Length; i++)
                {
                    int a = 1 + i, b = 1 + ((i + 1) % points.Length);
                    triangles[i * 3] = 0;
                    triangles[i * 3 + (side > 0 ? 2 : 1)] = a;
                    triangles[i * 3 + (side > 0 ? 1 : 2)] = b;
                }
                mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var go = new GameObject(side > 0 ? "Cannon_FlameCrest_Right" : "Cannon_FlameCrest_Left");
                go.transform.SetParent(barrelRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                RuntimeMeshCleanup.Own(barrelRoot, mesh);
            }
        }

        static void AddWheelSpokes(Transform carriageRoot)
        {
            // Warm timber spokes sit just proud of both wheel faces; the original tyre mesh remains intact.
            AddWheel(carriageRoot, 0.30f, 0.60f, 0.98f, 0.60f, "Front");
            AddWheel(carriageRoot, -1.30f, 0.38f, 0.98f, 0.38f, "Rear");
        }

        static Mesh _spokeCube, _hubCylinder;
        static Mesh PrimitiveMesh(ref Mesh cache, PrimitiveType type)
        {
            if (cache != null) return cache;
            var temp = GameObject.CreatePrimitive(type);
            cache = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
            return cache;
        }

        static void AddWheel(Transform root, float x, float y, float z, float radius, string label)
        {
            var spokeMat = KingdomMaterials.CarriageWood;
            var cube = PrimitiveMesh(ref _spokeCube, PrimitiveType.Cube);
            var cylinder = PrimitiveMesh(ref _hubCylinder, PrimitiveType.Cylinder);
            for (int side = -1; side <= 1; side += 2)
            {
                // Eight timber spokes are one mesh per wheel face (rather than 8 renderers).
                var pieces = new CombineInstance[8];
                for (int i = 0; i < pieces.Length; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    var pos = new Vector3(x + Mathf.Cos(angle) * radius * 0.52f,
                        y + Mathf.Sin(angle) * radius * 0.52f, side * (z + 0.125f));
                    var rot = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                    float length = radius * 0.90f;
                    var scale = new Vector3(length, Mathf.Max(0.075f, radius * 0.18f), 0.035f);
                    pieces[i] = new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(pos, rot, scale) };
                }
                var spokesMesh = new Mesh { name = "Cannon_" + label + "Wheel_Spokes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                spokesMesh.CombineMeshes(pieces, true, true);
                spokesMesh.RecalculateBounds();
                var spokes = new GameObject("Cannon_" + label + "Wheel_Spokes_" + side);
                spokes.transform.SetParent(root, false);
                spokes.AddComponent<MeshFilter>().sharedMesh = spokesMesh;
                spokes.AddComponent<MeshRenderer>().sharedMaterial = spokeMat;
                RuntimeMeshCleanup.Own(root, spokesMesh);

                var hub = new GameObject("Cannon_" + label + "Wheel_BrassHub_" + side);
                hub.transform.SetParent(root, false);
                hub.transform.localPosition = new Vector3(x, y, side * (z + 0.15f));
                hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                hub.transform.localScale = new Vector3(radius * 0.42f, 0.045f, radius * 0.42f);
                hub.AddComponent<MeshFilter>().sharedMesh = cylinder;
                hub.AddComponent<MeshRenderer>().sharedMaterial = KingdomMaterials.CannonBronze;
            }
        }

        static void Part(string name, Mesh mesh, Transform parent, float boxTile, bool carriage = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var partMesh = boxTile > 0f ? CvMesh.WithBoxUv(mesh, boxTile, Vector3.one) : mesh;
            go.AddComponent<MeshFilter>().sharedMesh = partMesh;
            if (boxTile > 0f) RuntimeMeshCleanup.Own(parent, partMesh);
            // PM QA 31 Oct: timber carriage = weathered dark brown wood with IRON fittings/bolts; bronze stays on the barrel bands
            var mat = name.EndsWith("_Wood") ? KingdomMaterials.CarriageWood : carriage && name.EndsWith("_Gold") ? KingdomMaterials.Iron
                : !carriage && name.EndsWith("_Gold") ? KingdomMaterials.CannonBronze : !carriage && name.EndsWith("_Navy") ? KingdomMaterials.CannonIron : KingdomMaterials.ForTag(name);
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
        }
    }
}
