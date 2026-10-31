using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    public enum KingdomRegion { Harbour, MillFields, Clocktower, CrownRock, Quarry }

    /// <summary>
    /// Phase 1 environment for every run (concept art design/level_concepts_v1): photo sky, ocean with a turquoise lagoon and foam
    /// line, a textured grass island with a sand beach, shoreline rocks, palms, a plank harbour deck under the cannon with posts, rope and
    /// a cannonball stack, distant islands and King Grimhold's palace on Crown Rock on the horizon. Presentation only: no colliders
    /// (the island's own collider/kill plane are untouched), never read by the sim.
    /// </summary>
    public static class KingdomEnvironment
    {
        public const float WaterY = -0.35f;
        static float Rx => Crashverse.Core.ArenaBuilder.IslandRx;
        static float Rz => Crashverse.Core.ArenaBuilder.IslandRz;
        static float Cx => Crashverse.Core.ArenaBuilder.IslandCx; // compact levels: the target's own small island (ArenaLayoutRules)
        static bool Compact => Rx < 20f;
        static bool OnIsland(Vector3 p, float margin = 0.85f) { float u = (p.x - Cx) / Rx, w = p.z / Rz; return u * u + w * w < margin * margin; }

        public static GameObject Build(Transform parent, Vector3 cannonRoot, Vector3 structureBase, bool palaceLevel, bool dark, KingdomRegion region, Bounds? structure = null)
        {
            var root = new GameObject("KingdomEnvironment"); root.transform.SetParent(parent, false);
            var bounds = structure ?? new Bounds(structureBase + Vector3.up * 3f, new Vector3(8f, 6f, 8f));
            Ocean(root.transform);
            Island(root.transform, structureBase);
            Deck(root.transform, cannonRoot);
            Shore(root.transform, cannonRoot, structureBase);
            Horizon(root.transform, palaceLevel, region);
            if (Rx >= 20f) Yard(root.transform, cannonRoot, bounds);
            Harbour(root.transform, structureBase);
            RegionSetDressing(root.transform, structureBase, bounds, region);
            Foliage(root.transform, cannonRoot, bounds);
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

        static GameObject ObjOwned(string name, Transform parent, Mesh mesh, Material mat, bool shadows = true, bool receive = true)
        {
            var go = Obj(name, parent, mesh, mat, shadows, receive);
            RuntimeMeshCleanup.Own(parent, mesh);
            return go;
        }

        // ---------------- ocean ----------------
        static void Ocean(Transform root)
        {
            var mat = KingdomMaterials.Water; if (mat == null) { Debug.LogWarning("[Kingdom] water shader missing"); return; }
            mat.SetVector("_Island", new Vector4(Cx, 0f, Rx * 1.0f, Rz * 1.0f));
            // shore band is in ellipse units: a small island needs wider units for a ~5 m bright turquoise lagoon and visible foam (L01.jpg)
            mat.SetFloat("_ShoreFade", Compact ? 1.5f : 0.9f); mat.SetFloat("_FoamWidth", Compact ? 0.085f : 0.035f);
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
            ObjOwned("Ocean", root, m, mat, false, false);
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
            // PM QA 31 Oct (L01.jpg): a sand island, not a lawn: dry sand on top, darker trampled sand under the building
            var sandTop = ObjOwned("IslandSand", root, Ring(0f, 0.97f, 0.005f, 0.005f, 96, 6f, 1.3f, 1f), KingdomMaterials.Make("SandDry", "T_K_Sand", new Color(1.04f, 0.98f, 0.88f), 0f, 0.97f, Vector2.one), false, true);
            var beach = ObjOwned("IslandBeach", root, Ring(0.9f, 1.22f, -0.01f, WaterY - 1.4f, 96, 5f, 1.3f, 0.6f), KingdomMaterials.Sand, false, true);
            sandTop.transform.localPosition = beach.transform.localPosition = new Vector3(Cx, 0f, 0f);
            // sandy plaza under the building and a path toward the cannon
            ObjOwned("Plaza", root, Disc(Mathf.Min(7.5f, Rx * 0.75f), Mathf.Min(6.5f, Rz * 0.75f), 0.012f, 40, 5f, 4.1f, new Vector3(structureBase.x, 0f, structureBase.z)), KingdomMaterials.Make("SandTrod", "T_K_Sand", new Color(0.82f, 0.74f, 0.62f), 0f, 0.9f, Vector2.one), false, true);
        }

        // ---------------- harbour deck ----------------
        static Mesh _cube;
        static Mesh Cube(Vector3 size, float tile) => CvMesh.BoxCube(size, tile);

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile, Quaternion rot = default)
        {
            var go = Obj(name, parent, Cube(size, tile), mat);
            go.transform.localPosition = pos; go.transform.localRotation = (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f ? Quaternion.identity : rot); go.transform.localScale = size;
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

        /// <summary>Set dressing around the target (concept art): a sand path from the deck, fences down both sides, barrel + crate
        /// stacks at the front corners, a hand cart and a sack pile. Visual only (no colliders), outside the structure's footprint.</summary>
        static void Yard(Transform root, Vector3 cannon, Bounds sb)
        {
            var y = new GameObject("Yard").transform; y.SetParent(root, false);
            float halfW = Mathf.Max(2.5f, sb.extents.z), x0 = sb.min.x, x1 = sb.max.x, cz = sb.center.z;
            // sand path (slightly raised so it never z-fights the grass)
            float pathStart = cannon.x + 3.8f, pathEnd = x0 - 0.5f;
            // sand island now: no separate path strip (pathStart/pathEnd kept for the fences below)
            // fences along both sides of the yard
            foreach (float side in new[] { -1f, 1f })
            {
                float z = cz + side * (halfW + 2.2f);
                for (float x = x0 - 3f; x <= x1 + 1.5f; x += 1.6f)
                {
                    var post = Obj("FencePost", y, Cylinder(), KingdomMaterials.Wood);
                    post.transform.localPosition = new Vector3(x, 0.5f, z); post.transform.localScale = new Vector3(0.14f, 0.5f, 0.14f);
                }
                foreach (float h in new[] { 0.45f, 0.85f })
                    Box("FenceRail", y, new Vector3((x0 - 3f + x1 + 1.5f) * 0.5f, h, z), new Vector3(x1 + 4.5f - x0, 0.09f, 0.07f), KingdomMaterials.Planks, 1.5f);
            }
            // barrel + crate stacks at the front corners
            Props(y, new Vector3(x0 - 1.6f, 0f, cz - halfW - 1.1f), 1);
            Props(y, new Vector3(x0 - 1.2f, 0f, cz + halfW + 0.9f), 2);
            Box("Crate", y, new Vector3(x0 - 0.6f, 1.1f, cz + halfW + 0.7f), new Vector3(0.6f, 0.6f, 0.6f), KingdomMaterials.Wood, 0.6f, Quaternion.Euler(0f, -12f, 0f));
            // hand cart
            var cart = new GameObject("Cart").transform; cart.SetParent(y, false);
            cart.localPosition = new Vector3(x0 - 3.2f, 0f, cz + halfW + 1.4f); cart.localRotation = Quaternion.Euler(0f, 35f, 0f);
            Box("CartBed", cart, new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 0.12f, 1.0f), KingdomMaterials.Planks, 1f);
            Box("CartSideL", cart, new Vector3(0f, 0.95f, 0.47f), new Vector3(1.6f, 0.3f, 0.06f), KingdomMaterials.Planks, 1f);
            Box("CartSideR", cart, new Vector3(0f, 0.95f, -0.47f), new Vector3(1.6f, 0.3f, 0.06f), KingdomMaterials.Planks, 1f);
            Box("CartHandle", cart, new Vector3(-1.3f, 0.7f, 0f), new Vector3(1.2f, 0.07f, 0.07f), KingdomMaterials.Wood, 1f, Quaternion.Euler(0f, 0f, 10f));
            foreach (float z in new[] { -0.56f, 0.56f })
            {
                var w = Obj("CartWheel", cart, Cylinder(), KingdomMaterials.Wood);
                w.transform.localPosition = new Vector3(0.1f, 0.45f, z); w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); w.transform.localScale = new Vector3(0.9f, 0.04f, 0.9f);
            }
            for (int i = 0; i < 3; i++)
            {
                var sack = Obj("Sack", cart, Sphere(), KingdomMaterials.Make("Sack", "T_K_Sand", new Color(0.95f, 0.88f, 0.72f), 0f, 0.95f, Vector2.one));
                sack.transform.localPosition = new Vector3(-0.45f + i * 0.45f, 1.0f, 0f); sack.transform.localScale = new Vector3(0.45f, 0.35f, 0.55f);
            }
        }

        /// <summary>Concept L01 harbour: two plank piers on posts running from the beach into the lagoon beside the target, with
        /// moored rowboats and mooring bollards. Visual only.</summary>
        static void Harbour(Transform root, Vector3 structure)
        {
            var h = new GameObject("Harbour").transform; h.SetParent(root, false);
            foreach (float side in new[] { -1f, 1f })
            {
                float zEdge = side * Rz * 0.88f, len = 9f, x = structure.x + side * 3f;
                var pier = new GameObject("Pier").transform; pier.SetParent(h, false); pier.localPosition = new Vector3(x, 0f, zEdge);
                float dir = side;
                for (int i = 0; i < 14; i++)
                    Box("PierPlank", pier, new Vector3(((i % 3) - 1) * 0.03f, 0.22f, dir * (i * 0.65f + 0.3f)), new Vector3(2.2f, 0.09f, 0.58f), KingdomMaterials.Planks, 1.2f, Quaternion.Euler(0f, (i % 2 == 0 ? 0.8f : -0.6f), 0f));
                foreach (float px in new[] { -1.05f, 1.05f })
                    for (float z = 0.6f; z < len; z += 2.2f)
                    {
                        var post = Obj("PierPost", pier, Cylinder(), KingdomMaterials.Wood);
                        post.transform.localPosition = new Vector3(px, -0.2f, dir * z); post.transform.localScale = new Vector3(0.24f, 0.75f, 0.24f);
                    }
                // rowboats moored alongside
                Rowboat(h, new Vector3(x + 2.3f, WaterY + 0.05f, zEdge + dir * 5.5f), side > 0 ? 8f : 172f);
                if (side > 0) Rowboat(h, new Vector3(x - 2.4f, WaterY + 0.05f, zEdge + dir * 7.5f), -14f);
            }
        }

        static void Rowboat(Transform parent, Vector3 at, float yaw)
        {
            var b = new GameObject("Rowboat").transform; b.SetParent(parent, false); b.localPosition = at; b.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var hull = KingdomMaterials.Make("BoatHull", "T_K_Wood", new Color(0.95f, 0.9f, 0.85f), 0f, 0.7f, Vector2.one);
            var trim = KingdomMaterials.Make("BoatTrim", null, new Color(0.08f, 0.2f, 0.45f), 0f, 0.5f, Vector2.one);
            // tapered hull from stacked strakes: wide amidships, narrow at bow/stern
            for (int i = 0; i < 9; i++)
            {
                float f = (i - 4) / 4f, w = Mathf.Lerp(1.25f, 0.35f, f * f), y = 0.12f + (1f - f * f) * 0.0f + Mathf.Abs(f) * 0.12f;
                Box("Strake", b, new Vector3(f * 1.7f, y, 0f), new Vector3(0.42f, 0.32f, w), hull, 1f);
                Box("Gunwale", b, new Vector3(f * 1.7f, y + 0.18f, w * 0.5f), new Vector3(0.42f, 0.06f, 0.06f), trim, 1f);
                Box("Gunwale", b, new Vector3(f * 1.7f, y + 0.18f, -w * 0.5f), new Vector3(0.42f, 0.06f, 0.06f), trim, 1f);
            }
            Box("Thwart", b, new Vector3(0.3f, 0.3f, 0f), new Vector3(0.25f, 0.05f, 1.1f), KingdomMaterials.Planks, 1f);
            var oar = Obj("Oar", b, Cylinder(), KingdomMaterials.Wood);
            oar.transform.localPosition = new Vector3(-0.2f, 0.4f, 0.2f); oar.transform.localRotation = Quaternion.Euler(80f, 20f, 0f); oar.transform.localScale = new Vector3(0.06f, 1.1f, 0.06f);
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
                var p = new Vector3(Cx + Mathf.Cos(a) * Rx * 1.03f, WaterY + 0.05f, Mathf.Sin(a) * Rz * 1.06f);
                if (p.x < cannon.x + 6f && Mathf.Abs(p.z) < 5f) continue; // keep the harbour deck clear
                if (!rocks.TryGetValue(rockNames[i % 3], out var rm)) continue;
                float s = (0.6f + (float)rng.NextDouble() * 0.9f) * (Compact ? 0.4f : 1f); // small island: low surf rocks, not boulders over the sand
                if (Compact && (i % 2 == 1 || p.x < Cx - Rx * 0.5f)) continue; // keep the cannon-facing beach open (L01.jpg)
                var r = Obj("ShoreRock", shore, rm, KingdomMaterials.Rock, true);
                r.GetComponent<MeshFilter>().sharedMesh = BoxUvCached(rm, 2.5f * s);
                r.transform.localPosition = p; r.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                r.transform.localScale = new Vector3(s, s * (0.7f + (float)rng.NextDouble() * 0.5f), s);
            }
            // L01.jpg: weathered rock outcrops on the sand either side of the building and along the path (in frame at the new
            // camera, outside the firing lane |z| < 2.6 and the building footprint)
            for (int i = 0; i < 14; i++)
            {
                float side = (i & 1) == 0 ? -1f : 1f;
                float x = Mathf.Lerp(cannon.x + 9f, structure.x + 5f, (i / 2) / 6f) + (float)rng.NextDouble() * 2f;
                float z = side * (3.2f + (float)rng.NextDouble() * 3.5f + (Mathf.Abs(x - structure.x) < 6f ? 2.5f : 0f));
                if (!rocks.TryGetValue(rockNames[(i + 1) % 3], out var rm)) continue;
                float s = 0.35f + (float)rng.NextDouble() * 0.55f;
                var r = Obj("SandRock", shore, BoxUvCached(rm, 2.5f * s), KingdomMaterials.Rock, true);
                bool land = OnIsland(new Vector3(x, 0f, z), 0.95f);
                if (Compact && (!land || x < Cx - Rx * 0.3f)) continue; // compact: no rocks in the water lane / on the front beach
                r.transform.localPosition = new Vector3(x, land ? -0.12f * s : WaterY - 0.1f * s, z);
                r.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                float surfScale = land ? 1f : 1.6f;
                r.transform.localScale = new Vector3(s * 1.2f * surfScale, s * (0.55f + (float)rng.NextDouble() * 0.4f), s * surfScale);
            }
            // palms on the beach, out of the firing lane (|z| > 6 between the cannon and the building)
            if (Compact)
            {
                // QA L1: palms on the island's far left/right edges, leaning out over the water, so the fronds frame the hut instead
                // of covering its roof (concept L01: palms sit at the shoreline beside the building)
                float[] side = { -1f, 1f, -1f, 1f }; float[] dx = { 0.35f, 0.15f, -0.45f, -0.6f };
                for (int i = 0; i < side.Length; i++)
                {
                    var p = new Vector3(Cx + dx[i] * Rx, 0f, side[i] * Rz * Mathf.Sqrt(1f - dx[i] * dx[i]) * 0.93f); // on the sand rim, not in the water
                    Palm(shore, palms, p, i, side[i] * (14f + i * 3f), i < 2 ? 0.8f : 0.62f);
                }
                return;
            }
            float[] ang = { 0.55f, 0.95f, 1.35f, 1.85f, 2.3f, 2.75f, 3.55f, 3.95f, 4.45f, 4.9f, 5.35f, 5.8f };
            for (int i = 0; i < ang.Length; i++)
            {
                float a = ang[i]; var p = new Vector3(Cx + Mathf.Cos(a) * Rx * 0.93f, 0f, Mathf.Sin(a) * Rz * 0.9f);
                if (Compact && i % 2 != 0) continue; // small island: a few palms, not a ring of them
                if (Compact ? (p.x < Cx && Mathf.Abs(p.z) < Rz * 0.6f) : (Mathf.Abs(p.z) < 6.5f && p.x > cannon.x - 2f && p.x < structure.x + 12f)) continue;
                if (p.x < cannon.x + 6f) continue; // next to / behind the camera: fronds would fill the screen corners
                Palm(shore, palms, p, i);
            }
        }

        /// <summary>Concept L1: ferns, round bushes and small rocks hugging the building's footprint and the beach. Merged into one
        /// mesh per material (3 draw calls), no colliders, never inside the cannon's sight line to the building's front.</summary>
        static void Foliage(Transform root, Vector3 cannon, Bounds sb)
        {
            var rng = new System.Random(4242); float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere); var sphere = sph.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(sph);
            var cub = GameObject.CreatePrimitive(PrimitiveType.Cube); var cube = cub.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(cub);
            var rocks = CvMesh.Load("Kingdom/Meshes/Rocks");
            var dark = new List<CombineInstance>(); var light = new List<CombineInstance>(); var stone = new List<CombineInstance>();
            Vector3 c = sb.center; float ex = sb.extents.x + 0.6f, ez = sb.extents.z + 0.6f;
            for (int i = 0; i < (Compact ? 90 : 46); i++)
            {
                float a = R(0f, Mathf.PI * 2f), d = R(1.0f, 1.9f);
                var p = new Vector3(c.x + Mathf.Cos(a) * ex * d, 0f, c.z + Mathf.Sin(a) * ez * d);
                if (Compact) // small island (L01.jpg): bushes / ferns all over the sand around the stilts, beach rim kept open
                {
                    float r = R(0.35f, 0.88f); p = new Vector3(Cx + Mathf.Cos(a) * Rx * r, 0f, Mathf.Sin(a) * Rz * r);
                    if (Mathf.Abs(p.x - c.x) < 0.9f && Mathf.Abs(p.z - c.z) < 0.9f) continue; // the centre post / pedestal
                }
                if (p.x < c.x - ex * 0.9f && Mathf.Abs(p.z - c.z) < 1.4f) continue; // keep the front door / sight line clear
                if (!OnIsland(p, 0.92f) || p.x < cannon.x + 7f) continue;
                int kind = i % 3;
                if (kind == 0)
                {   // round bush: 3-4 squashed blobs
                    for (int k = 0; k < 4; k++)
                    {
                        float s = R(0.45f, 0.85f);
                        var m = Matrix4x4.TRS(p + new Vector3(R(-0.35f, 0.35f), s * 0.35f, R(-0.35f, 0.35f)), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(s, s * 0.75f, s));
                        (k % 2 == 0 ? dark : light).Add(new CombineInstance { mesh = sphere, transform = m });
                    }
                }
                else if (kind == 1)
                {   // fern: a rosette of arched fronds (thin tilted slabs)
                    int fr = 7; float len = R(0.6f, 1.0f), yaw0 = R(0f, 360f);
                    for (int k = 0; k < fr; k++)
                    {
                        var q = Quaternion.Euler(0f, yaw0 + k * 360f / fr, 0f) * Quaternion.Euler(0f, 0f, R(28f, 48f));
                        var m = Matrix4x4.TRS(p + q * new Vector3(len * 0.5f, 0f, 0f) + Vector3.up * 0.05f, q, new Vector3(len, 0.025f, 0.16f));
                        (k % 2 == 0 ? light : dark).Add(new CombineInstance { mesh = cube, transform = m });
                    }
                }
                else if (rocks.TryGetValue(i % 2 == 0 ? "Rock_A" : "Rock_C", out var rm))
                {
                    float s = R(0.18f, 0.4f);
                    stone.Add(new CombineInstance { mesh = rm, transform = Matrix4x4.TRS(p + Vector3.down * 0.05f, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(s, s * R(0.6f, 1f), s)) });
                }
            }
            var t = new GameObject("Foliage").transform; t.SetParent(root, false);
            void Merge(string n, List<CombineInstance> l, Material mat, float tile)
            {
                if (l.Count == 0) return;
                var combined = new Mesh { name = n + "_Combined", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                combined.CombineMeshes(l.ToArray(), true, true); combined.RecalculateBounds();
                Mesh mesh = combined;
                if (tile > 0f)
                {
                    mesh = CvMesh.WithBoxUv(combined, tile, Vector3.one);
                    Object.Destroy(combined);
                }
                ObjOwned(n, t, mesh, mat, true, true);
            }
            Merge("Foliage_Dark", dark, KingdomMaterials.Make("LeafDark", "T_K_Grass", new Color(0.30f, 0.62f, 0.22f), 0f, 0.7f, Vector2.one), 1.2f);
            Merge("Foliage_Light", light, KingdomMaterials.Make("LeafLight", "T_K_Grass", new Color(0.55f, 0.85f, 0.30f), 0f, 0.65f, Vector2.one), 1.2f);
            Merge("Foliage_Rock", stone, KingdomMaterials.Rock, 1.5f);
        }

        // Box-projected rock UVs depend on both the source mesh and the requested tile size.
        // Keying by Mesh alone reused the first random rock's UV scale for every later instance,
        // making the same shoreline asset show inconsistent texel density across the island.
        readonly struct BoxUvKey : System.IEquatable<BoxUvKey>
        {
            readonly int meshId, tileMilli;
            public BoxUvKey(Mesh mesh, float tile) { meshId = mesh.GetInstanceID(); tileMilli = Mathf.RoundToInt(tile * 1000f); }
            public bool Equals(BoxUvKey other) => meshId == other.meshId && tileMilli == other.tileMilli;
            public override bool Equals(object obj) => obj is BoxUvKey other && Equals(other);
            public override int GetHashCode() { unchecked { return meshId * 397 ^ tileMilli; } }
        }
        static readonly Dictionary<BoxUvKey, Mesh> _boxUv = new Dictionary<BoxUvKey, Mesh>();
        static Mesh BoxUvCached(Mesh m, float tile)
        {
            var key = new BoxUvKey(m, tile);
            if (_boxUv.TryGetValue(key, out var hit) && hit != null) return hit;
            return _boxUv[key] = CvMesh.WithBoxUv(m, tile, Vector3.one * 2.5f);
        }

        static void Palm(Transform parent, Dictionary<string, Mesh> palms, Vector3 p, int i, float leanOut = 0f, float scale = 0f)
        {
            string id = i % 2 == 0 ? "PalmA" : "PalmB";
            if (!palms.TryGetValue(id + "_Trunk", out var trunk) || !palms.TryGetValue(id + "_Leaf", out var leaf)) return;
            var t = new GameObject(id).transform; t.SetParent(parent, false); t.localPosition = p;
            t.localRotation = Quaternion.Euler(leanOut, 0f, 0f) * Quaternion.Euler(0f, i * 47f, 0f); // leanOut: tilt about x, away from the building (+z side tilts to +z)
            t.localScale = Vector3.one * (scale > 0f ? scale : 0.85f + (i % 3) * 0.12f);
            Obj("Trunk", t, trunk, KingdomMaterials.Bark).AddComponent<Sway>().amp = 0f;
            var l = Obj("Leaves", t, leaf, KingdomMaterials.Leaf);
            l.AddComponent<Sway>().amp = 1.6f;
        }

        // ---------------- region landmarks ----------------
        // These skyline silhouettes make the campaign's journey legible before the player even reads the level title.
        // They are presentation-only, have no colliders, and sit on distant islands outside the playable firing lane.
        static void WindmillLandmark(Transform parent)
        {
            var t = new GameObject("CrownMillLandmark").transform; t.SetParent(parent, false);
            t.localPosition = new Vector3(150f, WaterY, 70f);
            Box("MillTower", t, new Vector3(0f, 5.2f, 0f), new Vector3(5.2f, 10.4f, 5.2f), KingdomMaterials.Make("MillStone", "T_K_Stone", new Color(0.82f, 0.72f, 0.55f), 0f, 0.9f, Vector2.one), 2.5f);
            Box("MillRoof", t, new Vector3(0f, 10.8f, 0f), new Vector3(6.2f, 1.0f, 6.2f), KingdomMaterials.Roof, 2f);
            Box("MillDoor", t, new Vector3(-2.66f, 1.3f, 0f), new Vector3(0.08f, 2.4f, 1.1f), KingdomMaterials.Bore, 1f);
            var rotor = new GameObject("WindmillRotor").transform; rotor.SetParent(t, false);
            rotor.localPosition = new Vector3(-2.8f, 8.7f, 0f);
            for (int i = 0; i < 4; i++)
            {
                var blade = new GameObject("BladePivot").transform; blade.SetParent(rotor, false);
                blade.localRotation = Quaternion.Euler(i * 90f, 0f, 0f);
                Box("Sail", blade, new Vector3(0f, 2.15f, 0f), new Vector3(0.34f, 4.3f, 0.66f), KingdomMaterials.Planks, 1.2f);
                Box("SailTip", blade, new Vector3(0f, 4.05f, 0f), new Vector3(0.42f, 0.45f, 0.82f), KingdomMaterials.Wood, 1f);
            }
            var hub = Obj("MillHub", rotor, Sphere(), KingdomMaterials.Iron, false, false);
            hub.transform.localScale = Vector3.one * 0.9f;
            rotor.gameObject.AddComponent<WindmillRotor>().degreesPerSecond = 8f;
        }

        static void ClocktowerLandmark(Transform parent)
        {
            var t = new GameObject("CurfewClocktowerLandmark").transform; t.SetParent(parent, false);
            t.localPosition = new Vector3(184f, WaterY, -75f);
            var stone = KingdomMaterials.Make("ClocktowerStone", "T_K_Stone", new Color(0.86f, 0.82f, 0.73f), 0f, 0.88f, Vector2.one);
            Box("ClockTowerBase", t, new Vector3(0f, 6f, 0f), new Vector3(5.2f, 12f, 5.2f), stone, 2.5f);
            Box("ClockTowerUpper", t, new Vector3(0f, 14.5f, 0f), new Vector3(4.2f, 7f, 4.2f), stone, 2f);
            Box("ClockTowerRoof", t, new Vector3(0f, 18.3f, 0f), new Vector3(5.4f, 1.0f, 5.4f), KingdomMaterials.Roof, 2f);
            var dial = Obj("ClockDial", t, Cylinder(), KingdomMaterials.Make("ClockDial", null, new Color(0.92f, 0.82f, 0.58f), 0.25f, 0.55f, Vector2.one), false, false);
            dial.transform.localPosition = new Vector3(-2.68f, 14.6f, 0f);
            dial.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            dial.transform.localScale = new Vector3(2.4f, 0.12f, 2.4f);
            Box("ClockMinuteHand", t, new Vector3(-2.78f, 14.6f, 0f), new Vector3(0.08f, 1.05f, 0.08f), KingdomMaterials.CannonBronze, 1f);
            Box("ClockHourHand", t, new Vector3(-2.78f, 14.6f, 0.25f), new Vector3(0.08f, 0.08f, 0.72f), KingdomMaterials.CannonBronze, 1f);
            for (int i = 0; i < 4; i++)
                Box("ClockMerlon", t, new Vector3((i % 2 == 0 ? -1f : 1f) * 1.35f, 19.2f, (i < 2 ? -1f : 1f) * 1.35f), new Vector3(0.7f, 1.0f, 0.7f), stone, 1f);
        }

        static void QuarryLandmark(Transform parent)
        {
            var rocks = CvMesh.Load("Kingdom/Meshes/Rocks");
            var t = new GameObject("IronjawQuarryLandmark").transform; t.SetParent(parent, false);
            t.localPosition = new Vector3(154f, WaterY, 68f);
            string[] ids = { "Rock_A", "Rock_B", "Rock_C" };
            for (int i = 0; i < ids.Length; i++)
            {
                if (!rocks.TryGetValue(ids[i], out var mesh)) continue;
                var rock = Obj(ids[i], t, mesh, KingdomMaterials.Rock, false, false);
                rock.transform.localPosition = new Vector3((i - 1) * 4.6f, 1.5f + (i % 2) * 1.4f, (i % 2 == 0 ? -1f : 1f) * 2.5f);
                rock.transform.localRotation = Quaternion.Euler(0f, i * 41f, 0f);
                rock.transform.localScale = new Vector3(2.8f, 3.8f + i * 0.8f, 2.7f);
            }
        }

        static void RegionSetDressing(Transform root, Vector3 structure, Bounds bounds, KingdomRegion region)
        {
            if (region == KingdomRegion.MillFields)
            {
                var hay = KingdomMaterials.Make("HayBale", "T_K_Grass", new Color(1.15f, 0.82f, 0.34f), 0f, 0.98f, Vector2.one);
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = bounds.center.z + side * (bounds.extents.z + 3.2f);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = bounds.min.x - 2.2f + i * 1.25f;
                        Box("HayBale", root, new Vector3(x, 0.42f, z), new Vector3(1.1f, 0.84f, 0.8f), hay, 0.8f, Quaternion.Euler(0f, side * (8f + i * 7f), 0f));
                    }
                    Box("HayStackTop", root, new Vector3(bounds.min.x - 1.6f, 1.15f, z), new Vector3(0.9f, 0.62f, 0.72f), hay, 0.7f, Quaternion.Euler(0f, side * 18f, 0f));
                }
            }
            else if (region == KingdomRegion.Clocktower)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var post = Obj("TownLampPost", root, Cylinder(), KingdomMaterials.Wood, false, false);
                    post.transform.localPosition = new Vector3(bounds.min.x - 2.5f, 2.0f, bounds.center.z + side * (bounds.extents.z + 3.0f));
                    post.transform.localScale = new Vector3(0.18f, 2.0f, 0.18f);
                    var lantern = Obj("TownLantern", root, Sphere(), KingdomMaterials.Make("LanternGlow", null, new Color(1f, 0.68f, 0.24f), 0f, 0.25f, Vector2.one, new Color(1.2f, 0.48f, 0.12f)), false, false);
                    lantern.transform.localPosition = post.transform.localPosition + Vector3.up * 2.1f;
                    lantern.transform.localScale = Vector3.one * 0.42f;
                }
            }
            else if (region == KingdomRegion.CrownRock)
            {
                var gold = KingdomMaterials.CannonBronze;
                foreach (float side in new[] { -1f, 1f })
                {
                    var standard = Obj("CrownRockStandard", root, Cylinder(), KingdomMaterials.Gold, false, false);
                    standard.transform.localPosition = new Vector3(bounds.min.x - 2.8f, 2.2f, bounds.center.z + side * (bounds.extents.z + 3.2f));
                    standard.transform.localScale = new Vector3(0.12f, 2.2f, 0.12f);
                    Box("StandardCrossbar", root, standard.transform.localPosition + Vector3.up * 1.65f, new Vector3(0.14f, 0.14f, 1.5f), gold, 1f);
                    Box("StandardCloth", root, standard.transform.localPosition + Vector3.up * 1.0f, new Vector3(0.12f, 1.0f, 0.9f), KingdomMaterials.FlagRed, 1f);
                }
            }
        }

        // ---------------- horizon ----------------
        static void Horizon(Transform root, bool palaceLevel, KingdomRegion region)
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
                    if (tile > 0f) ObjOwned(kv.Key, t, mesh, mat, false, false);
                    else Obj(kv.Key, t, mesh, mat, false, false);
                }
            }
            Isle("IsleA", isles, new Vector3(150f, WaterY, 70f), 1f, 20f);
            Isle("IsleB", isles, new Vector3(185f, WaterY, -75f), 1.1f, 140f);
            Isle("IsleA", isles, new Vector3(-60f, WaterY, 150f), 1.3f, 75f);
            Isle("IsleB", isles, new Vector3(260f, WaterY, 25f), 0.9f, 260f);
            if (region == KingdomRegion.MillFields) WindmillLandmark(h);
            else if (region == KingdomRegion.Clocktower) ClocktowerLandmark(h);
            else if (region == KingdomRegion.Quarry) QuarryLandmark(h);
            // King Grimhold's palace on Crown Rock: always on the horizon (screen right), close and huge on L10
            var pal = new GameObject("CrownRockPalace").transform; pal.SetParent(h, false);
            pal.localPosition = palaceLevel ? new Vector3(185f, WaterY, -16f) : new Vector3(230f, WaterY, -45f);
            pal.localRotation = Quaternion.Euler(0f, 110f, 0f); pal.localScale = Vector3.one * (palaceLevel ? 1.9f : 1.6f);
            foreach (var kv in palace)
            {
                float tile = KingdomMaterials.TileFor(kv.Key);
                var mesh = tile > 0f ? CvMesh.WithBoxUv(kv.Value, tile, Vector3.one) : kv.Value;
                if (tile > 0f) ObjOwned(kv.Key, pal, mesh, KingdomMaterials.ForTag(kv.Key), false, false);
                else Obj(kv.Key, pal, mesh, KingdomMaterials.ForTag(kv.Key), false, false);
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
            // Respect the system-wide Reduce Motion preference for decorative wind motion as well as HUD animation.
            if (Crashverse.Feel.MotionSettings.Current != null && Crashverse.Feel.MotionSettings.Current.ReduceMotion)
            {
                if (transform.localRotation != _base) transform.localRotation = _base;
                return;
            }
            if (amp <= 0f) { enabled = false; return; }
            float t = Time.unscaledTime * speed + _phase;
            transform.localRotation = _base * Quaternion.Euler(Mathf.Sin(t) * amp, 0f, Mathf.Sin(t * 1.3f + 1f) * amp * 0.7f);
        }
    }
    /// <summary>Slow background windmill motion; purely decorative and disabled while Reduce Motion is active.</summary>
    public class WindmillRotor : MonoBehaviour
    {
        public float degreesPerSecond = 8f;
        void Update()
        {
            if (Crashverse.Feel.MotionSettings.Current != null && Crashverse.Feel.MotionSettings.Current.ReduceMotion) return;
            transform.Rotate(Vector3.right, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }

}
