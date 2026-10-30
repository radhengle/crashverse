using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    /// <summary>
    /// Phase 1 environment for every run (concept art design/level_concepts_v1): photo sky, ocean with a turquoise lagoon and foam
    /// line, a textured grass island with a sand beach, shoreline rocks, palms, a plank harbour deck under the cannon with posts, rope and
    /// a cannonball stack, distant islands and King Grimhold's palace on Crown Rock on the horizon. Presentation only: no colliders
    /// (the island's own collider/kill plane are untouched), never read by the sim.
    /// </summary>
    public static class KingdomEnvironment
    {
        public const float WaterY = -0.35f;
        static float Rx => Crashverse.Core.ArenaBuilder.IslandLength * 0.5f;
        static float Rz => Crashverse.Core.ArenaBuilder.IslandWidth * 0.5f;

        public static GameObject Build(Transform parent, Vector3 cannonRoot, Vector3 structureBase, bool palaceLevel, bool dark)
        {
            var root = new GameObject("KingdomEnvironment"); root.transform.SetParent(parent, false);
            Ocean(root.transform);
            Island(root.transform, structureBase);
            Deck(root.transform, cannonRoot);
            Shore(root.transform, cannonRoot, structureBase);
            Horizon(root.transform, palaceLevel);
            return root;
        }

        static GameObject Obj(string name, Transform parent, Mesh mesh, Material mat, bool shadows = true, bool receive = true)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = receive;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        // ---------------- ocean ----------------
        static void Ocean(Transform root)
        {
            var mat = KingdomMaterials.Water; if (mat == null) { Debug.LogWarning("[Kingdom] water shader missing"); return; }
            mat.SetVector("_Island", new Vector4(0f, 0f, Rx * 1.0f, Rz * 1.0f));
            int n = 96; float size = 900f, x0 = -330f, z0 = -450f;
            var v = new Vector3[(n + 1) * (n + 1)]; var t = new int[n * n * 6];
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    // denser near the island: quadratic spacing around the arena
                    float u = i / (float)n * 2f - 1f, w = j / (float)n * 2f - 1f;
                    float x = Mathf.Sign(u) * u * u * size * 0.5f + 30f, z = Mathf.Sign(w) * w * w * size * 0.5f;
                    v[j * (n + 1) + i] = new Vector3(x, WaterY, z);
                }
            int k = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                    t[k++] = a; t[k++] = c; t[k++] = b; t[k++] = b; t[k++] = c; t[k++] = d;
                }
            var m = new Mesh { name = "Ocean", indexFormat = IndexFormat.UInt32 }; m.vertices = v; m.triangles = t;
            m.RecalculateNormals(); m.bounds = new Bounds(new Vector3(30f, 0f, 0f), new Vector3(size, 4f, size));
            Obj("Ocean", root, m, mat, false, false);
            _ = x0; _ = z0;
        }

        // ---------------- island ----------------
        static float Edge(float a, float seed) => 1f + 0.035f * Mathf.Sin(a * 5f + seed) + 0.025f * Mathf.Sin(a * 11f + seed * 2.3f) + 0.015f * Mathf.Sin(a * 23f + seed * 0.7f);

        static Mesh Ring(float s0, float s1, float y0, float y1, int seg, float tile, float seed, float wob)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            int rings = 4;
            for (int r = 0; r <= rings; r++)
            {
                float f = r / (float)rings; float s = Mathf.Lerp(s0, s1, f); float y = Mathf.Lerp(y0, y1, Mathf.SmoothStep(0f, 1f, f));
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg; float e = Mathf.Lerp(1f, Edge(a, seed), wob);
                    var p = new Vector3(Mathf.Cos(a) * Rx * s * e, y, Mathf.Sin(a) * Rz * s * e);
                    v.Add(p); uv.Add(new Vector2(p.x, p.z) / tile);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a = r * (seg + 1) + i, b = a + 1, c = a + seg + 1, d = c + 1;
                    t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c);
                }
            var m = new Mesh { name = "Ring" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateTangents();
            return m;
        }

        static Mesh Disc(float rx, float rz, float y, int seg, float tile, float seed, Vector3 c)
        {
            var v = new List<Vector3> { c + new Vector3(0f, y, 0f) }; var uv = new List<Vector2> { new Vector2(c.x, c.z) / tile }; var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg; float e = Edge(a, seed);
                var p = c + new Vector3(Mathf.Cos(a) * rx * e, y, Mathf.Sin(a) * rz * e);
                v.Add(p); uv.Add(new Vector2(p.x, p.z) / tile);
            }
            for (int i = 0; i < seg; i++) { t.Add(0); t.Add(i + 2); t.Add(i + 1); }
            var m = new Mesh { name = "Disc" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateTangents();
            return m;
        }

        static void Island(Transform root, Vector3 structureBase)
        {
            // grass top (wavy edge, slightly inside the collider rim) + sand beach that slopes under the water
            Obj("IslandGrass", root, Ring(0f, 0.97f, 0.005f, 0.005f, 96, 6f, 1.3f, 1f), KingdomMaterials.Grass, false, true);
            Obj("IslandBeach", root, Ring(0.9f, 1.22f, -0.01f, WaterY - 1.4f, 96, 5f, 1.3f, 0.6f), KingdomMaterials.Sand, false, true);
            // sandy plaza under the building and a path toward the cannon
            Obj("Plaza", root, Disc(7.5f, 6.5f, 0.012f, 40, 5f, 4.1f, new Vector3(structureBase.x, 0f, structureBase.z)), KingdomMaterials.Sand, false, true);
        }

        // ---------------- harbour deck ----------------
        static Mesh _cube;
        static Mesh Cube(Vector3 size, float tile) => CvMesh.BoxCube(size, tile);

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile, Quaternion rot = default)
        {
            var go = Obj(name, parent, Cube(size, tile), mat);
            go.transform.localPosition = pos; go.transform.localRotation = rot == default ? Quaternion.identity : rot; go.transform.localScale = size;
            return go;
        }

        static Mesh _cyl;
        static Mesh Cylinder()
        {
            if (_cyl != null) return _cyl;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder); _cyl = tmp.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(tmp);
            return _cyl;
        }
        static Mesh _sph;
        static Mesh Sphere()
        {
            if (_sph != null) return _sph;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); _sph = tmp.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(tmp);
            return _sph;
        }

        static void Deck(Transform root, Vector3 cannon)
        {
            var d = new GameObject("HarbourDeck").transform; d.SetParent(root, false); d.position = new Vector3(cannon.x, 0f, cannon.z);
            Box("Deck", d, new Vector3(-0.6f, -0.14f, 0f), new Vector3(8.5f, 0.3f, 7.0f), KingdomMaterials.Planks, 2.2f);
            // beams under the deck edge + posts with rope at the front-left/right corners (bottom corners of the screen)
            foreach (float z in new[] { -3.45f, 3.45f })
            {
                Box("Beam", d, new Vector3(-0.6f, -0.3f, z), new Vector3(8.6f, 0.35f, 0.35f), KingdomMaterials.Wood, 1.2f);
                foreach (float x in new[] { -4.6f, -2.4f, -0.2f, 2.0f, 3.5f })
                {
                    var p = Obj("Post", d, Cylinder(), KingdomMaterials.Wood);
                    p.transform.localPosition = new Vector3(x, 0.35f, z); p.transform.localScale = new Vector3(0.32f, 0.75f, 0.32f);
                    var cap = Obj("PostCap", d, Cylinder(), KingdomMaterials.Iron);
                    cap.transform.localPosition = new Vector3(x, 1.12f, z); cap.transform.localScale = new Vector3(0.36f, 0.04f, 0.36f);
                }
                for (int i = 0; i < 4; i++)
                {
                    float xa = new[] { -4.6f, -2.4f, -0.2f, 2.0f }[i], xb = new[] { -2.4f, -0.2f, 2.0f, 3.5f }[i];
                    var rope = Obj("Rope", d, Cylinder(), KingdomMaterials.Make("Rope", "T_K_Bark", new Color(0.95f, 0.85f, 0.62f), 0f, 0.9f, new Vector2(0.3f, 3f)), false);
                    rope.transform.localPosition = new Vector3((xa + xb) * 0.5f, 0.92f, z);
                    rope.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    rope.transform.localScale = new Vector3(0.07f, (xb - xa) * 0.5f, 0.07f);
                }
            }
            // cannonball pyramid + powder barrels + crates beside the cannon
            int n = 0;
            for (int layer = 0; layer < 3; layer++)
                for (int i = 0; i < 3 - layer; i++)
                    for (int j = 0; j < 3 - layer; j++)
                    {
                        var b = Obj("Cannonball", d, Sphere(), KingdomMaterials.Iron, n++ < 6);
                        b.transform.localPosition = new Vector3(-2.2f + (i + layer * 0.5f) * 0.36f, 0.19f + layer * 0.29f, 2.0f + (j + layer * 0.5f) * 0.36f);
                        b.transform.localScale = Vector3.one * 0.36f;
                    }
            Props(d, new Vector3(-2.4f, 0f, -2.3f), 0);
        }

        static void Props(Transform parent, Vector3 at, int seed)
        {
            var barrel = Obj("PowderBarrel", parent, Cylinder(), KingdomMaterials.Make("BarrelWood", "T_K_Wood", Color.white, 0f, 0.8f, new Vector2(2f, 1f)));
            barrel.transform.localPosition = at + new Vector3(0f, 0.5f, 0f); barrel.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            foreach (float y in new[] { 0.18f, 0.82f })
            {
                var hoop = Obj("Hoop", parent, Cylinder(), KingdomMaterials.Iron, false);
                hoop.transform.localPosition = at + new Vector3(0f, y, 0f); hoop.transform.localScale = new Vector3(0.84f, 0.035f, 0.84f);
            }
            Box("Crate", parent, at + new Vector3(0.9f, 0.4f, -0.3f), new Vector3(0.8f, 0.8f, 0.8f), KingdomMaterials.Wood, 0.8f, Quaternion.Euler(0f, 18f, 0f));
        }

        // ---------------- shoreline ----------------
        static void Shore(Transform root, Vector3 cannon, Vector3 structure)
        {
            var rocks = CvMesh.Load("Kingdom/Meshes/Rocks"); var palms = CvMesh.Load("Kingdom/Meshes/Palms");
            var rng = new System.Random(1789);
            var rockNames = new[] { "Rock_A", "Rock_B", "Rock_C" };
            var shore = new GameObject("Shore").transform; shore.SetParent(root, false);
            for (int i = 0; i < 26; i++)
            {
                float a = i * Mathf.PI * 2f / 26f + (float)rng.NextDouble() * 0.15f;
                var p = new Vector3(Mathf.Cos(a) * Rx * 1.03f, WaterY + 0.05f, Mathf.Sin(a) * Rz * 1.06f);
                if (p.x < cannon.x + 6f && Mathf.Abs(p.z) < 5f) continue; // keep the harbour deck clear
                if (!rocks.TryGetValue(rockNames[i % 3], out var rm)) continue;
                float s = 0.6f + (float)rng.NextDouble() * 0.9f;
                var r = Obj("ShoreRock", shore, rm, KingdomMaterials.Rock, true);
                r.GetComponent<MeshFilter>().sharedMesh = BoxUvCached(rm, 2.5f * s);
                r.transform.localPosition = p; r.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                r.transform.localScale = new Vector3(s, s * (0.7f + (float)rng.NextDouble() * 0.5f), s);
            }
            // palms on the beach, out of the firing lane (|z| > 6 between the cannon and the building)
            float[] ang = { 0.55f, 0.95f, 1.35f, 1.85f, 2.3f, 2.75f, 3.55f, 3.95f, 4.45f, 4.9f, 5.35f, 5.8f };
            for (int i = 0; i < ang.Length; i++)
            {
                float a = ang[i]; var p = new Vector3(Mathf.Cos(a) * Rx * 0.93f, 0f, Mathf.Sin(a) * Rz * 0.9f);
                if (Mathf.Abs(p.z) < 6.5f && p.x > cannon.x - 2f && p.x < structure.x + 12f) continue;
                if (p.x < cannon.x + 6f) continue; // next to / behind the camera: fronds would fill the screen corners
                Palm(shore, palms, p, i);
            }
        }

        static readonly Dictionary<Mesh, Mesh> _boxUv = new Dictionary<Mesh, Mesh>();
        static Mesh BoxUvCached(Mesh m, float tile)
        {
            if (_boxUv.TryGetValue(m, out var hit)) return hit;
            return _boxUv[m] = CvMesh.WithBoxUv(m, tile, Vector3.one * 2.5f);
        }

        static void Palm(Transform parent, Dictionary<string, Mesh> palms, Vector3 p, int i)
        {
            string id = i % 2 == 0 ? "PalmA" : "PalmB";
            if (!palms.TryGetValue(id + "_Trunk", out var trunk) || !palms.TryGetValue(id + "_Leaf", out var leaf)) return;
            var t = new GameObject(id).transform; t.SetParent(parent, false); t.localPosition = p;
            t.localRotation = Quaternion.Euler(0f, i * 47f, 0f); t.localScale = Vector3.one * (0.85f + (i % 3) * 0.12f);
            Obj("Trunk", t, trunk, KingdomMaterials.Bark).AddComponent<Sway>().amp = 0f;
            var l = Obj("Leaves", t, leaf, KingdomMaterials.Leaf);
            l.AddComponent<Sway>().amp = 1.6f;
        }

        // ---------------- horizon ----------------
        static void Horizon(Transform root, bool palaceLevel)
        {
            var isles = CvMesh.Load("Kingdom/Meshes/Isles"); var palace = CvMesh.Load("Kingdom/Meshes/Palace"); var palms = CvMesh.Load("Kingdom/Meshes/Palms");
            var h = new GameObject("Horizon").transform; h.SetParent(root, false);
            void Isle(string id, Dictionary<string, Mesh> set, Vector3 pos, float scale, float yaw)
            {
                var t = new GameObject(id).transform; t.SetParent(h, false); t.localPosition = pos; t.localRotation = Quaternion.Euler(0f, yaw, 0f); t.localScale = Vector3.one * scale;
                foreach (var kv in set)
                {
                    if (!kv.Key.StartsWith(id)) continue;
                    var mat = KingdomMaterials.ForTag(kv.Key);
                    float tile = KingdomMaterials.TileFor(kv.Key);
                    var mesh = tile > 0f ? CvMesh.WithBoxUv(kv.Value, tile, Vector3.one * scale) : kv.Value;
                    Obj(kv.Key, t, mesh, mat, false, false);
                }
            }
            Isle("IsleA", isles, new Vector3(150f, WaterY, 70f), 1f, 20f);
            Isle("IsleB", isles, new Vector3(185f, WaterY, -75f), 1.1f, 140f);
            Isle("IsleA", isles, new Vector3(-60f, WaterY, 150f), 1.3f, 75f);
            Isle("IsleB", isles, new Vector3(260f, WaterY, 25f), 0.9f, 260f);
            // King Grimhold's palace on Crown Rock: always on the horizon (screen right), close and huge on L10
            var pal = new GameObject("CrownRockPalace").transform; pal.SetParent(h, false);
            pal.localPosition = palaceLevel ? new Vector3(185f, WaterY, -16f) : new Vector3(230f, WaterY, -45f);
            pal.localRotation = Quaternion.Euler(0f, 110f, 0f); pal.localScale = Vector3.one * (palaceLevel ? 1.9f : 1.6f);
            foreach (var kv in palace)
            {
                float tile = KingdomMaterials.TileFor(kv.Key);
                var mesh = tile > 0f ? CvMesh.WithBoxUv(kv.Value, tile, Vector3.one) : kv.Value;
                Obj(kv.Key, pal, mesh, KingdomMaterials.ForTag(kv.Key), false, false);
            }
            // a few palms on the near isles' grass caps
            int i = 0;
            foreach (var at in new[] { new Vector3(146f, 5.8f, 66f), new Vector3(154f, 5.9f, 73f), new Vector3(180f, 8.8f, -72f), new Vector3(189f, 8.8f, -79f) })
                Palm(h, palms, at, i++);
        }
    }

    /// <summary>Cheap wind sway for palm fronds / flags (presentation only, unscaled time).</summary>
    public class Sway : MonoBehaviour
    {
        public float amp = 1.5f, speed = 1.1f; Quaternion _base; float _phase;
        void Start() { _base = transform.localRotation; _phase = (transform.position.x * 0.37f + transform.position.z * 0.21f) % 6.28f; }
        void Update()
        {
            if (amp <= 0f) { enabled = false; return; }
            float t = Time.unscaledTime * speed + _phase;
            transform.localRotation = _base * Quaternion.Euler(Mathf.Sin(t) * amp, 0f, Mathf.Sin(t * 1.3f + 1f) * amp * 0.7f);
        }
    }
}
