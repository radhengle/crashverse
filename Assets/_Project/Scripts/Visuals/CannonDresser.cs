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
            // PM b20 QA: the b19 band collars floated as oversized hoops; the mesh's own bronze bands stay
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

        /// <summary>L1 v2 goal cannon (design/L01_v2_spec.md §2, measured from L01_v2_goal.png): a procedural navy barrel at world scale
        /// whose axis rides 0.2 m above the pitch pivot (1.7 m at the trunnions), four gold rings (muzzle lip +1.97, ring +0.66,
        /// reinforce -0.53, breech ring -1.91, the widest), a sun emblem on the top at -1.45, a rounded breech with a cascabel knob, and
        /// stepped timber cheeks at z +/-(0.7..1.3) with trunnion caps. The muzzle (+1.97) sits on the projectile spawn (+1.95), so the
        /// hint maths and pivots are unchanged. Returns the visual muzzle.</summary>
        public static Transform DressGoal(Crashverse.Weapons.WreckerBuilder wb)
        {
            if (wb == null || wb.yawPivot == null || wb.pitchPivot == null) return null;
            foreach (var r in wb.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            Material Mk(string k, Color c, float metal, float rough, string tex = null, Color em = default) => KingdomMaterials.Make("TwCannon" + k, tex, c, metal, rough, Vector2.one, em);
            var navy = Mk("Navy", new Color(0.11f, 0.12f, 0.25f), 0.25f, 0.62f);           // PM b22: satin, not glossy          // #212548
            var navyDark = Mk("NavyDark", new Color(0.235f, 0.223f, 0.305f), 0.3f, 0.6f);  // #3C394E
            var gold = Mk("Gold", new Color(0.80f, 0.55f, 0.30f), 0.6f, 0.4f);          // #C08050
            var sunGold = Mk("Sun", new Color(0.97f, 0.89f, 0.45f), 0.4f, 0.3f, null, new Color(0.25f, 0.14f, 0.02f)); // #F8E474
            var sunDeep = Mk("SunDeep", new Color(0.93f, 0.57f, 0.08f), 0.4f, 0.35f);     // #EC9114
            var bore = Mk("Bore", new Color(0.03f, 0.03f, 0.05f), 0f, 1f);
            var wood = Mk("Wood", new Color(0.62f, 0.45f, 0.48f), 0f, 0.85f, "T_TW_Timber"); // dark brown #714130 lit, Poly Haven CC0 planks
            var iron = Mk("Iron", new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.45f);

            // Blender-built hero mesh (Tools/Blender/tide/build_cannon.py -> Kingdom/Meshes/Tide_Cannon): same dimensions as the
            // procedural fallback below, with rounded bevelled rings, a raised sun emblem and bevelled timber. Objects are <Part>_<Material>.
            var hero = CvMesh.Load("Kingdom/Meshes/Tide_Cannon");
            if (hero.ContainsKey("Barrel_Navy") && hero.ContainsKey("Carriage_Wood"))
            {
                var mats = new System.Collections.Generic.Dictionary<string, Material> {
                    { "Navy", navy }, { "NavyDark", navyDark }, { "Gold", gold }, { "Sun", sunGold }, { "SunDeep", sunDeep }, { "Bore", bore }, { "Wood", wood }, { "Iron", iron } };
                var hb = new GameObject("Kingdom_Barrel").transform; hb.SetParent(wb.yawPivot, false);
                var hf = hb.gameObject.AddComponent<PivotFollower>(); hf.src = wb.pitchPivot; hf.offset = new Vector3(0f, 0.2f, 0f); hf.LateUpdate();
                var hc = new GameObject("Kingdom_Carriage").transform; hc.SetParent(wb.yawPivot, false);
                hc.localPosition = new Vector3(0f, -wb.yawPivot.localPosition.y, 0f);
                foreach (var kv in hero)
                {
                    int us = kv.Key.LastIndexOf('_'); if (us < 0) continue;
                    if (!mats.TryGetValue(kv.Key.Substring(us + 1), out var mat)) continue;
                    var go = new GameObject("Tw" + kv.Key); go.transform.SetParent(kv.Key.StartsWith("Barrel") ? hb : hc, false);
                    go.AddComponent<MeshFilter>().sharedMesh = kv.Value;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.On;
                }
                Debug.Log("[Tide] hero cannon mesh loaded: " + hero.Count + " parts");
                var hm = new GameObject("Kingdom_Muzzle").transform; hm.SetParent(hb, false);
                hm.localPosition = new Vector3(wb.projectileSpawn != null ? wb.projectileSpawn.localPosition.x : 1.95f, 0f, 0f);
                return hm;
            }

            var bRoot = new GameObject("Kingdom_Barrel").transform; bRoot.SetParent(wb.yawPivot, false);
            var f = bRoot.gameObject.AddComponent<PivotFollower>(); f.src = wb.pitchPivot; f.offset = new Vector3(0f, 0.2f, 0f); f.LateUpdate();
            var t = new TwMesh();
            // navy body: breech dome -> breech ring -> body tapering 0.62 -> 0.52 with the reinforce and second ring -> muzzle lip
            t.Lathe(navy, 1f, new[] {
                new Vector2(-2.24f, 0f), new Vector2(-2.235f, 0.2f), new Vector2(-2.2f, 0.38f), new Vector2(-2.13f, 0.52f), new Vector2(-2.05f, 0.6f) }, 28);
            t.Lathe(navy, 1f, new[] { new Vector2(-1.78f, 0.62f), new Vector2(-0.68f, 0.6f) }, 28);
            t.Lathe(navy, 1f, new[] { new Vector2(-0.38f, 0.58f), new Vector2(0.55f, 0.55f) }, 28);
            t.Lathe(navy, 1f, new[] { new Vector2(0.78f, 0.535f), new Vector2(1.80f, 0.52f) }, 28);
            void Band(float x0, float x1, float rIn, float rOut, float lip)
            {
                // gold ring with a rounded lip at both ends (steps of rIn -> rOut - lip -> rOut -> rOut - lip -> rIn)
                t.Lathe(gold, 1f, new[] { new Vector2(x0, rIn), new Vector2(x0, rOut - lip), new Vector2(x0 + lip, rOut), new Vector2(x1 - lip, rOut), new Vector2(x1, rOut - lip), new Vector2(x1, rIn) }, 28);
            }
            Band(-2.12f, -1.74f, 0.6f, 0.72f, 0.04f);   // PM b22: thick raised rings
            t.Lathe(navyDark, 1f, new[] { new Vector2(-1.95f, 0.721f), new Vector2(-1.91f, 0.721f) }, 28); // the breech ring's groove
            Band(-0.72f, -0.34f, 0.58f, 0.70f, 0.04f);
            Band(0.50f, 0.82f, 0.535f, 0.63f, 0.04f);
            Band(1.76f, 2.04f, 0.52f, 0.62f, 0.04f);
            // muzzle face and bore
            t.Lathe(gold, 1f, new[] { new Vector2(2.04f, 0.58f), new Vector2(2.04f, 0.36f) }, 28);
            t.Lathe(bore, 1f, new[] { new Vector2(2.04f, 0.36f), new Vector2(1.5f, 0.36f), new Vector2(1.5f, 0f) }, 20);
            // cascabel: neck and knob behind the breech
            t.Lathe(navyDark, 1f, new[] { new Vector2(-2.24f, 0.13f), new Vector2(-2.32f, 0.09f), new Vector2(-2.36f, 0.09f) }, 16);
            t.Sphere(gold, 1f, new Vector3(-2.47f, 0f, 0f), Vector3.one * 0.13f, 12, 8);
            // trunnions into the cheeks (their dark iron cap straps sit on the cheek tops, below)
            foreach (float sz in new[] { -1f, 1f })
            {
                t.Cyl(navy, 1f, new Vector3(0f, -0.2f, sz * 0.5f), new Vector3(0f, -0.2f, sz * 0.72f), 0.13f, 0.13f, 12, true);
            }
            // sun emblem on the top of the breech (16 rays, long 0.28 / short 0.19, disc 0.15)
            var top = new Vector3(-1.25f, 0.61f, 0f); var up = Vector3.up;
            var disc = new Vector3[16];
            for (int i = 0; i < 16; i++) { float a = -i * Mathf.PI * 2f / 16f; disc[i] = top + new Vector3(Mathf.Cos(a) * 0.2f, 0.012f, Mathf.Sin(a) * 0.2f); }
            if (Vector3.Dot(Vector3.Cross(disc[1] - disc[0], disc[2] - disc[0]), up) < 0f) System.Array.Reverse(disc);
            t.Poly(sunGold, 1f, disc);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var sd = Vector3.Cross(up, d);
                float len = i % 2 == 0 ? 0.40f : 0.28f;
                // follow the barrel's curve so the rays lie on its surface
                Vector3 P(Vector3 q) { float z = q.z; float y = Mathf.Sqrt(Mathf.Max(0f, 0.62f * 0.62f - z * z)) + 0.008f; return new Vector3(q.x, y, z); }
                var q3 = new[] { P(top + d * 0.16f - sd * 0.06f), P(top + d * len), P(top + d * 0.16f + sd * 0.06f) };
                if (Vector3.Dot(Vector3.Cross(q3[1] - q3[0], q3[2] - q3[0]), up) < 0f) System.Array.Reverse(q3);
                t.Poly(i % 2 == 0 ? sunGold : sunDeep, 1f, q3);
            }
            t.Build(bRoot, "TwCannonBarrel", true);
            var muzzle = new GameObject("Kingdom_Muzzle").transform; muzzle.SetParent(bRoot, false);
            muzzle.localPosition = new Vector3(wb.projectileSpawn != null ? wb.projectileSpawn.localPosition.x : 1.95f, 0f, 0f);

            // carriage: two stepped timber cheeks (z +/-0.7..1.3) with gold caps, transoms and rear blocks; follows the yaw only
            Debug.LogWarning("[Tide] hero cannon mesh missing, procedural fallback");
            var cRoot = new GameObject("Kingdom_Carriage").transform; cRoot.SetParent(wb.yawPivot, false);
            cRoot.localPosition = new Vector3(0f, -wb.yawPivot.localPosition.y, 0f);
            var c = new TwMesh();
            foreach (float sz in new[] { -1f, 1f })
            {
                float z0 = sz * 0.7f, z1 = sz * 1.3f; float zl = Mathf.Min(z0, z1), zh = Mathf.Max(z0, z1);
                c.BoxMinMax(wood, 0.9f, new Vector3(-0.35f, 0f, zl), new Vector3(1.0f, 1.62f, zh));
                c.BoxMinMax(wood, 0.9f, new Vector3(-0.95f, 0f, zl), new Vector3(-0.35f, 1.2f, zh));
                c.BoxMinMax(wood, 0.9f, new Vector3(-1.55f, 0f, zl), new Vector3(-0.95f, 0.85f, zh));
                foreach (var (x0, x1, y) in new[] { (-0.35f, 1.0f, 1.62f), (-0.95f, -0.35f, 1.2f), (-1.55f, -0.95f, 0.85f) })
                {
                    c.BoxMinMax(gold, 1f, new Vector3(x0 - 0.02f, y, zl - 0.02f), new Vector3(x1 + 0.02f, y + 0.05f, zh + 0.02f));
                    c.BoxMinMax(gold, 1f, new Vector3(x0 - 0.03f, y - 0.42f, zl - 0.02f), new Vector3(x0 + 0.03f, y, zh + 0.02f));
                }
                // iron bolts on the outer and inner faces
                for (int k = 0; k < 3; k++)
                {
                    float bx = -1.3f + k * 0.6f, by = 0.45f;
                    c.Sphere(iron, 1f, new Vector3(bx, by, sz > 0 ? zh + 0.01f : zl - 0.01f), Vector3.one * 0.05f, 6, 4);
                    c.Sphere(iron, 1f, new Vector3(bx, by, sz > 0 ? zl - 0.01f : zh + 0.01f), Vector3.one * 0.05f, 6, 4);
                }
                // trunnion seat (dark iron cap strap over the pivot)
                c.BoxMinMax(iron, 1f, new Vector3(-0.2f, 1.6f, zl - 0.03f), new Vector3(0.2f, 1.7f, zh + 0.03f));
                // front blocks at the bottom (goal u 22-38 / 62-80, v 94-100) and the rear chock
                c.BoxMinMax(wood, 0.9f, new Vector3(0.6f, 0f, zl - 0.1f), new Vector3(1.05f, 0.35f, zh + 0.1f));
                c.BoxMinMax(wood, 0.9f, new Vector3(-1.7f, 0f, zl), new Vector3(-1.3f, 0.5f, zh));
            }
            c.BoxMinMax(wood, 0.9f, new Vector3(-1.5f, 0.15f, -0.7f), new Vector3(-1.0f, 0.5f, 0.7f));  // rear transom
            c.BoxMinMax(wood, 0.9f, new Vector3(0.3f, 0.15f, -0.7f), new Vector3(0.9f, 0.6f, 0.7f));    // front transom
            c.BoxMinMax(navyDark, 1f, new Vector3(-0.9f, 0.5f, -0.7f), new Vector3(-0.5f, 0.75f, 0.7f)); // elevation quoin
            c.Build(cRoot, "TwCannonCarriage", true);
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
            var knob = new[] { new Vector2(-1.580f, 0.100f), new Vector2(-1.620f, 0.085f), new Vector2(-1.645f, 0.085f), new Vector2(-1.671f, 0.145f), new Vector2(-1.710f, 0.150f), new Vector2(-1.749f, 0.145f), new Vector2(-1.785f, 0.130f), new Vector2(-1.816f, 0.106f), new Vector2(-1.840f, 0.075f), new Vector2(-1.855f, 0.039f), new Vector2(-1.860f, 0.000f) };
            Lathe("Cannon_BreechDome", dome, barrelRoot, KingdomMaterials.CannonIron);
            Lathe("Cannon_Cascabel", knob, barrelRoot, KingdomMaterials.CannonBronze, 32); // PM b20 QA: a round ball on a neck, so the near end reads as the breech (not a muzzle ring)
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
