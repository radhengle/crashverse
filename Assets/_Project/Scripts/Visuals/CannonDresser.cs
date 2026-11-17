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

        /// <summary>scale: render-only size (CannonCameraRules.Settings.cannonScale; compact levels 1.25). The carriage grows from the
        /// ground and the barrel rides on a mount that follows the pitch pivot (aim + recoil) at the scaled trunnion height, so the
        /// pivots, projectile spawn and hint maths are unchanged. Returns the visual muzzle (null on failure).</summary>
        public static Transform Dress(Crashverse.Weapons.WreckerBuilder wb, float scale = 1f)
        {
            if (wb == null || wb.yawPivot == null || wb.pitchPivot == null) return null;
            var barrel = CvMesh.Load("Kingdom/Meshes/Cannon_Barrel"); var carriage = CvMesh.Load("Kingdom/Meshes/Cannon_Carriage");
            if (barrel.Count == 0 || carriage.Count == 0) return null;
            foreach (var r in wb.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            float k = VisualScale * Mathf.Max(0.5f, scale);

            var bRoot = new GameObject("Kingdom_Barrel").transform;
            if (Mathf.Abs(k - 1f) < 1e-3f) bRoot.SetParent(wb.pitchPivot, false);
            else
            {
                bRoot.SetParent(wb.yawPivot, false);
                var f = bRoot.gameObject.AddComponent<PivotFollower>(); f.src = wb.pitchPivot; f.offset = new Vector3(0f, wb.yawPivot.localPosition.y * (k - 1f), 0f); f.LateUpdate();
            }
            bRoot.localScale = Vector3.one * k;
            foreach (var kv in barrel) Part(kv.Key, kv.Value, bRoot, KingdomMaterials.TileFor(kv.Key));
            foreach (var kv in barrel) if (kv.Key.EndsWith("_Gold")) ThickenBands(bRoot, kv.Value);
            var muzzle = new GameObject("Kingdom_Muzzle").transform; muzzle.SetParent(bRoot, false);
            muzzle.localPosition = wb.projectileSpawn != null ? wb.projectileSpawn.localPosition : new Vector3(1.95f, 0f, 0f);

            var cRoot = new GameObject("Kingdom_Carriage").transform; cRoot.SetParent(wb.yawPivot, false);
            cRoot.localPosition = new Vector3(0f, -wb.yawPivot.localPosition.y, 0f); // ground under the pivot
            cRoot.localScale = Vector3.one * k;
            foreach (var kv in carriage) Part(kv.Key, kv.Value, cRoot, kv.Key.EndsWith("_Wood") ? 0.9f : KingdomMaterials.TileFor(kv.Key), true);

            // Hero-asset detailing: orange flame crests and visible timber wheel spokes.
            // Render-only additions preserve the existing colliders, yaw/pitch pivots and projectile spawn.
            AddRoundedBreech(bRoot);
            AddFlameCrests(bRoot);
            AddWheelSpokes(cRoot);
            return muzzle;
        }

        /// <summary>PM b18 QA (L01.jpg): the gold bands read as thin lines. Every ring-shaped cluster of the Barrel_Gold mesh (centred on
        /// the bore axis, narrower than 0.45 m) gets a thicker, wider rounded antique-gold collar around it. Render-only.</summary>
        static void ThickenBands(Transform barrelRoot, Mesh gold)
        {
            if (gold == null || !gold.isReadable) return;
            var v = gold.vertices; if (v.Length == 0) return;
            var xs = new System.Collections.Generic.List<Vector3>(v); xs.Sort((a, b) => a.x.CompareTo(b.x));
            int start = 0;
            for (int i = 1; i <= xs.Count; i++)
            {
                if (i < xs.Count && xs[i].x - xs[i - 1].x < 0.05f) continue;
                float x0 = xs[start].x, x1 = xs[i - 1].x, rMax = 0f, rMin = 9f; Vector2 mean = Vector2.zero; int cnt = i - start;
                for (int j = start; j < i; j++) { float r = Mathf.Sqrt(xs[j].y * xs[j].y + xs[j].z * xs[j].z); rMax = Mathf.Max(rMax, r); rMin = Mathf.Min(rMin, r); mean += new Vector2(xs[j].y, xs[j].z); }
                mean /= Mathf.Max(1, cnt); start = i;
                if (cnt < 8 || x1 - x0 > 0.45f || rMax < 0.15f || mean.magnitude > 0.05f || rMin < rMax * 0.5f) continue;
                float r1 = rMax + 0.045f;
                Lathe("Cannon_BandCollar", new[] { new Vector2(x0 - 0.06f, rMax - 0.03f), new Vector2(x0 - 0.045f, r1 - 0.01f), new Vector2(x0 - 0.02f, r1),
                                                   new Vector2(x1 + 0.02f, r1), new Vector2(x1 + 0.045f, r1 - 0.01f), new Vector2(x1 + 0.06f, rMax - 0.03f) },
                      barrelRoot, KingdomMaterials.CannonBronze, 36);
            }
        }

        /// <summary>Copies the pitch pivot's local pose (aim elevation + recoil) onto the scaled barrel's mount.</summary>
        public sealed class PivotFollower : MonoBehaviour
        {
            public Transform src; public Vector3 offset;
            public void LateUpdate() { if (src == null) return; transform.localPosition = src.localPosition + offset; transform.localRotation = src.localRotation; }
        }

        /// <summary>L1 QA 16 Nov: the breech read as a flat disc. A rounded cast-iron dome behind the breech band (enclosing the old flat
        /// face) and a larger polished bronze cascabel knob on a neck, as in L01.jpg. Render-only; inside the WreckerSpec envelope
        /// (x >= -1.86, the framing tests' cannonRear).</summary>
        static void AddRoundedBreech(Transform barrelRoot)
        {
            var dome = new[] { new Vector2(-1.34f, 0.0f), new Vector2(-1.34f, 0.535f), new Vector2(-1.40f, 0.515f), new Vector2(-1.46f, 0.47f), new Vector2(-1.51f, 0.40f),
                               new Vector2(-1.55f, 0.31f), new Vector2(-1.58f, 0.21f), new Vector2(-1.60f, 0.12f), new Vector2(-1.61f, 0.0f) };
            var knob = new[] { new Vector2(-1.58f, 0.095f), new Vector2(-1.63f, 0.085f), new Vector2(-1.66f, 0.12f), new Vector2(-1.70f, 0.175f), new Vector2(-1.75f, 0.195f),
                               new Vector2(-1.80f, 0.175f), new Vector2(-1.84f, 0.11f), new Vector2(-1.86f, 0.0f) };
            var ring = new[] { new Vector2(-1.33f, 0.50f), new Vector2(-1.34f, 0.545f), new Vector2(-1.38f, 0.535f), new Vector2(-1.39f, 0.50f) };
            Lathe("Cannon_BreechDome", dome, barrelRoot, KingdomMaterials.CannonIron);
            Lathe("Cannon_BreechRing", ring, barrelRoot, KingdomMaterials.CannonBronze);
            Lathe("Cannon_Cascabel", knob, barrelRoot, KingdomMaterials.CannonBronze);
        }

        static void Lathe(string name, Vector2[] prof, Transform parent, Material mat, int segs = 28)
        {
            // double-sided (each copy has its own vertices so the normals of the visible side always face the camera)
            var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
            for (int copy = 0; copy < 2; copy++)
            {
                int b0 = v.Count;
                foreach (var p in prof)
                    for (int i = 0; i < segs; i++) { float a = i * Mathf.PI * 2f / segs; v.Add(new Vector3(p.x, Mathf.Cos(a) * p.y, Mathf.Sin(a) * p.y)); }
                for (int k = 0; k < prof.Length - 1; k++)
                    for (int i = 0; i < segs; i++)
                    {
                        int j = (i + 1) % segs, a0 = b0 + k * segs + i, a1 = b0 + k * segs + j, c0 = a0 + segs, c1 = a1 + segs;
                        if (copy == 0) { t.Add(a0); t.Add(a1); t.Add(c0); t.Add(a1); t.Add(c1); t.Add(c0); }
                        else { t.Add(a0); t.Add(c0); t.Add(a1); t.Add(a1); t.Add(c0); t.Add(c1); }
                    }
            }
            var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On;
            RuntimeMeshCleanup.Own(parent, m);
        }

        static void AddFlameCrests(Transform barrelRoot)
        {
            // Barrel axis is +X; Unity Y is vertical and Unity Z is lateral. Conform each crest
            // vertex to the barrel's tapered circular surface instead of placing a flat plate outside it.
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
                vertices[0] = CrestSurfacePoint(0.38f, 0.02f, side);
                for (int i = 0; i < points.Length; i++)
                    vertices[i + 1] = CrestSurfacePoint(0.38f + points[i].x, 0.02f + points[i].y, side);
                var triangles = new int[points.Length * 3];
                for (int i = 0; i < points.Length; i++)
                {
                    int a = 1 + i, b = 1 + ((i + 1) % points.Length);
                    triangles[i * 3] = 0;
                    // The polygon is clockwise in X/Y; reverse on +Z so both outward faces render.
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

        static Vector3 CrestSurfacePoint(float x, float vertical, int side)
        {
            // This crest occupies the straight barrel section x=0..0.9, whose radius tapers 0.51 -> 0.49 m.
            float radius = Mathf.Lerp(0.51f, 0.49f, Mathf.Clamp01(x / 0.9f));
            float y = Mathf.Clamp(vertical, -radius * 0.95f, radius * 0.95f);
            float lateral = Mathf.Sqrt(Mathf.Max(0f, radius * radius - y * y)) + 0.008f;
            return new Vector3(x, y, side * lateral);
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
            // PM 17 Nov (b16 QA, L01.jpg): the carriage must read like the concept: warm timber side cheeks with bronze cap plates and
            // trim (was iron, so the cheeks merged into one dark block behind the barrel)
            var mat = name.EndsWith("_Wood") ? KingdomMaterials.CarriageWood : name.EndsWith("_Gold") ? KingdomMaterials.CannonBronze : !carriage && name.EndsWith("_Navy") ? KingdomMaterials.CannonIron : KingdomMaterials.ForTag(name);
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
        }
    }
}
