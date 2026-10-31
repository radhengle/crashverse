using System.Collections.Generic;
using Crashverse.Destruction;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    /// <summary>
    /// Re-themes a level's pieces as King Grimhold's royal structures (design/Season1_KingStory_v1.md §1): richer graded CC0 surfaces
    /// (planks, red brick or castle stone, red clay roof tiles) on the realistic kit pieces and on any block the kit didn't dress, plus
    /// crimson Ironvale banners on the cannon-facing walls and a royal flag on the top piece. Visual only: colliders, mass, joints and
    /// chunk layouts are untouched; banners/flags hide with their piece.
    /// </summary>
    public static class KingdomTheme
    {
        public static bool CastleStone(string levelId) =>
            levelId != null && (levelId.Contains("l07") || levelId.Contains("clockwork") || levelId.Contains("l09") || levelId.Contains("l10") || levelId.Contains("palace") || levelId.Contains("boss"));

        static readonly Dictionary<Material, Material> _swap = new Dictionary<Material, Material>();

        static Material Variant(Material baseMat, bool inner)
        {
            if (!inner) return baseMat;
            var key = baseMat.name + "_Inner";
            var m = KingdomMaterials.Make(key.Replace("MK_", ""), null, Color.white, 0f, 0.9f, Vector2.one);
            if (m.GetTexture("_BaseMap") == null) { m.SetTexture("_BaseMap", baseMat.GetTexture("_BaseMap")); m.SetFloat("_InnerFace", 1f); }
            return m;
        }

        static Material ForKit(Material src, bool castle)
        {
            if (src == null) return null;
            if (_swap.TryGetValue(src, out var hit)) return hit;
            string n = src.name; bool inner = n.Contains("_Inner");
            Material r;
            if (n.StartsWith("M_RealWood")) r = Variant(KingdomMaterials.Wood, inner);
            else if (n.StartsWith("M_RealBrick")) r = Variant(castle ? KingdomMaterials.Stone : KingdomMaterials.Brick, inner);
            else if (n.StartsWith("M_RealRoof")) r = Variant(KingdomMaterials.Roof, inner);
            else { KingdomMaterials.Grade(src); r = src; }
            return _swap[src] = r;
        }

        public static void Apply(IReadOnlyList<DestructibleBlock> blocks, string levelId)
        {
            if (blocks == null || blocks.Count == 0) return;
            _swap.Clear();
            bool castle = CastleStone(levelId);
            ArchitectureDresser.Apply(blocks, castle); // Phase 1.5: timber pieces become real architecture (cabins, roofs, decks on stilts)
            foreach (var b in blocks)
            {
                if (b == null) continue;
                if (ArchitectureDresser.Dressed.Contains(b)) continue;
                if (b.visual != null)
                {
                    foreach (var r in b.visual.GetComponentsInChildren<Renderer>(true))
                    {
                        var ms = r.sharedMaterials;
                        for (int i = 0; i < ms.Length; i++) ms[i] = ForKit(ms[i], castle);
                        r.sharedMaterials = ms;
                    }
                }
                else if (!ArchitectureDresser.Dressed.Contains(b)) Retexture(b, castle);
            }
            bool clocktower = levelId != null && levelId.Contains("clockwork");
            Decorate(blocks, castle, clocktower);
            if (clocktower) AddClockFace(blocks);
            if (levelId != null && levelId.Contains("l09")) AddBeaconLantern(blocks);
            if (levelId != null && levelId.Contains("l10")) AddIronFistCrest(blocks);
        }

        /// <summary>L1 "Royal Watch Post": a pitched shingle roof over the top piece, a ladder up the cannon-facing side and rope
        /// railings, so the timber pieces read as a lookout hut (concept L01). Visual children of real pieces: hidden when they break.</summary>
        static void LookoutHut(IReadOnlyList<DestructibleBlock> blocks)
        {
            DestructibleBlock top = null; float topY = float.MinValue; Bounds all = default; bool any = false;
            foreach (var b in blocks)
            {
                if (b == null || b.role == "island") continue;
                var bb = new Bounds(b.transform.position, b.transform.localScale);
                if (!any) { all = bb; any = true; } else all.Encapsulate(bb);
                float y = bb.max.y; if (y > topY && b.transform.localScale.z >= 1f) { topY = y; top = b; }
            }
            if (top == null) return;
            var ts = top.transform.localScale;
            // roof: gable ridge across the screen (along z), eaves overhanging 0.35 m
            var roof = MetricChild(top, "Kingdom_HutRoof");
            float w = ts.z + 0.7f, d = ts.x + 0.9f, h = Mathf.Clamp(d * 0.45f, 0.8f, 1.8f);
            var m = Prism(w, d, h);
            var roofMesh = CvMesh.WithBoxUv(m, 1.2f, Vector3.one);
            RuntimeMeshCleanup.Own(roof, roofMesh);
            var r = R("Roof", roof, roofMesh, KingdomMaterials.Roof, true);
            Object.Destroy(m);
            r.transform.localPosition = new Vector3(0f, ts.y * 0.5f, 0f);
            // ladder on the front face of the structure, leaning from the ground to the top piece
            var lowest = all.min.y; var frontX = all.min.x;
            var ladderHost = MetricChild(top, "Kingdom_Ladder");
            float lh = topY - lowest; if (lh < 1.5f) return;
            var lad = new GameObject("Ladder").transform; lad.SetParent(ladderHost, false);
            lad.position = new Vector3(frontX - 0.35f, lowest, all.center.z + all.extents.z * 0.45f);
            lad.rotation = Quaternion.Euler(0f, 0f, -8f);
            foreach (float z in new[] { -0.28f, 0.28f })
            {
                var rail = R("LadderRail", lad, Cyl(), KingdomMaterials.Wood, true);
                rail.transform.localPosition = new Vector3(0f, lh * 0.5f, z); rail.transform.localScale = new Vector3(0.08f, lh * 0.5f, 0.08f);
            }
            for (float y = 0.35f; y < lh - 0.1f; y += 0.4f)
            {
                var rung = R("LadderRung", lad, Cyl(), KingdomMaterials.Wood);
                rung.transform.localPosition = new Vector3(0f, y, 0f); rung.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); rung.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f);
            }
        }

        static Mesh Prism(float w, float d, float h)
        {
            float x = d * 0.5f, z = w * 0.5f;
            var v = new List<Vector3>(); var t = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 e) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(e); t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
            void Tri(Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.AddRange(new[] { i, i + 1, i + 2 }); }
            Vector3 fl = new Vector3(-x, 0, z), fr = new Vector3(-x, 0, -z), bl = new Vector3(x, 0, z), br = new Vector3(x, 0, -z), rl = new Vector3(0, h, z), rr = new Vector3(0, h, -z);
            Quad(fl, rl, rr, fr);   // front slope (faces -x / up)
            Quad(br, rr, rl, bl);   // back slope
            Tri(fl, bl, rl);        // gable +z
            Tri(br, fr, rr);        // gable -z
            var m = new Mesh { name = "KHutRoof" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            // make every face visible from outside regardless of winding (double-sided copy)
            var v2 = new List<Vector3>(v); var t2 = new List<int>(t); int n = v.Count;
            for (int i = 0; i < t.Count; i += 3) { t2.Add(t[i] + n); t2.Add(t[i + 2] + n); t2.Add(t[i + 1] + n); }
            v2.AddRange(v); m.SetVertices(v2); m.SetTriangles(t2, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>QA L1: the layout's island block read as a grey concrete slab. Visual only (collider unchanged): a weathered rock
        /// outcrop from the Kingdom rock kit fitted to the block's unit box, flat-topped so the stilts still stand on it, slightly wider at
        /// the base and sunk into the sand so it reads as a natural ledge, not a plinth.</summary>
        const float FlagPole = Crashverse.Meta.CannonCameraRules.RoofFlagPole;

        static Mesh IslandOutcrop(Vector3 scale)
        {
            var rocks = CvMesh.Load("Kingdom/Meshes/Rocks");
            if (rocks == null || !rocks.TryGetValue("Rock_B", out var src) || src == null) return CvMesh.BoxCube(scale, 3f);
            var m = Object.Instantiate(src); m.name = "IslandOutcrop";
            var v = m.vertices; var b = src.bounds; var ext = b.size;
            for (int i = 0; i < v.Length; i++)
            {
                var q = v[i] - b.center;
                float ny = q.y / Mathf.Max(0.01f, ext.y) + 0.5f;                       // 0 bottom .. 1 top
                float widen = Mathf.Lerp(1.30f, 0.98f, Mathf.Clamp01(ny));             // skirt flares out into the sand
                var r = new Vector3(q.x / Mathf.Max(0.01f, ext.x) * widen, Mathf.Min(ny, 0.94f) / 0.94f * 1.15f - 0.65f, q.z / Mathf.Max(0.01f, ext.z) * widen);
                v[i] = r;                                                              // top flush with the block (+0.5) so the stilts stand on it, bottom sunk 0.15 into the sand
            }
            m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds();
            var boxed = CvMesh.WithBoxUv(m, 2.5f, scale);
            Object.Destroy(m); // WithBoxUv returns the owned render mesh; release this temporary deformed source.
            return boxed;
        }

        /// <summary>A block the kit didn't dress: tiled kingdom surface on a box-UV cube, white base tint (damage still darkens).</summary>
        static void Retexture(DestructibleBlock b, bool castle)
        {
            var mf = b.GetComponent<MeshFilter>(); var mr = b.GetComponent<MeshRenderer>(); if (mf == null || mr == null || !mr.enabled) return;
            if (b.role == "island")
            {
                mr.sharedMaterial = KingdomMaterials.Rock;
                mf.sharedMesh = IslandOutcrop(b.transform.localScale);
                RuntimeMeshCleanup.Own(b.transform, mf.sharedMesh);
                b.SetBaseTint(new Color(0.86f, 0.80f, 0.70f));
                return;
            }
            Material m; float tile;
            var kind = b.material != null ? b.material.kind : MaterialKind.Wood;
            switch (kind)
            {
                case MaterialKind.Brick: m = castle ? KingdomMaterials.Stone : KingdomMaterials.Brick; tile = castle ? 3f : 1.75f; break;
                case MaterialKind.Steel: m = KingdomMaterials.Iron; tile = 1.5f; break;
                case MaterialKind.Glass: return;
                default: m = KingdomMaterials.Wood; tile = 1.4f; break;
            }
            if (b.isRoof || b.role == "roof") { m = KingdomMaterials.Roof; tile = 2f; }
            mf.sharedMesh = CvMesh.BoxCube(b.transform.localScale, tile);
            mr.sharedMaterial = m;
            b.SetBaseTint(kind == MaterialKind.Steel ? new Color(0.75f, 0.78f, 0.85f) : Color.white);
        }

        // ---------------- banners + flag ----------------
        static Mesh _quad;
        static Mesh Quad()
        {
            if (_quad != null) return _quad;
            // YZ plane facing -X (toward the cannon); u runs screen-right (= world -z) for a camera on -X
            var m = new Mesh { name = "KBannerQuad" };
            m.vertices = new[] { new Vector3(0f, -0.5f, 0.5f), new Vector3(0f, -0.5f, -0.5f), new Vector3(0f, 0.5f, -0.5f), new Vector3(0f, 0.5f, 0.5f) };
            m.normals = new[] { Vector3.left, Vector3.left, Vector3.left, Vector3.left };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            m.RecalculateBounds(); m.RecalculateTangents();
            return _quad = m;
        }
        static Mesh _quadBack;
        static Mesh QuadBack()
        {
            if (_quadBack != null) return _quadBack;
            var m = Object.Instantiate(Quad()); m.name = "KBannerQuadBack";
            m.normals = new[] { Vector3.right, Vector3.right, Vector3.right, Vector3.right };
            m.triangles = new[] { 0, 2, 3, 0, 1, 2 };
            return _quadBack = m;
        }

        /// <summary>Child whose local space is metres (undoes the block's non-uniform scale).</summary>
        static Transform MetricChild(DestructibleBlock b, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(b.transform, false);
            var s = b.transform.localScale; t.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
            b.Broken += _ => { if (t != null) t.gameObject.SetActive(false); };
            b.WentOutOfBounds += _ => { if (t != null) t.gameObject.SetActive(false); };
            return t;
        }

        static GameObject R(string name, Transform p, Mesh mesh, Material mat, bool shadow = false)
        {
            var go = new GameObject(name); go.transform.SetParent(p, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off; return go;
        }

        static Mesh _cyl;
        static Mesh Cyl() { if (_cyl == null) { var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder); _cyl = t.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(t); } return _cyl; }
        static Mesh _sphere;
        static Mesh Sphere() { if (_sphere == null) { var t = GameObject.CreatePrimitive(PrimitiveType.Sphere); _sphere = t.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(t); } return _sphere; }


        /// <summary>Ironvale's crimson-and-gold faction crest, mounted off-centre on the Palace gate lintel.</summary>
        static void AddIronFistCrest(IReadOnlyList<DestructibleBlock> blocks)
        {
            DestructibleBlock host = null;
            foreach (var b in blocks) if (b != null && b.blockId == "gateLintel") { host = b; break; }
            if (host == null) return;
            var root = MetricChild(host, "Kingdom_IronFistCrest");
            var cube = CvMesh.BoxCube(Vector3.one, 0.25f);
            float front = -host.transform.localScale.x * 0.5f - 0.045f;
            float z = 0.92f; // right side of the lintel: keep the central face readable for hidden-core exposure
            var plate = R("IronFistCrest_Plate", root, cube, KingdomMaterials.FlagRed);
            plate.transform.localPosition = new Vector3(front, 0f, z);
            plate.transform.localScale = new Vector3(0.08f, 1.05f, 1.05f);

            var gold = new List<CombineInstance>();
            void Add(Vector3 position, Vector3 size, Quaternion rotation = default)
            {
                if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
                    rotation = Quaternion.identity;
                gold.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(position, rotation, size) });
            }

            // Four-piece frame.
            float x = front - 0.055f;
            Add(new Vector3(x, 0.52f, z), new Vector3(0.07f, 0.07f, 1.14f));
            Add(new Vector3(x, -0.52f, z), new Vector3(0.07f, 0.07f, 1.14f));
            Add(new Vector3(x, 0f, z - 0.54f), new Vector3(0.07f, 1.0f, 0.07f));
            Add(new Vector3(x, 0f, z + 0.54f), new Vector3(0.07f, 1.0f, 0.07f));

            // Stylised fist silhouette: cuff, palm, four raised knuckles and a turned thumb.
            Add(new Vector3(x, -0.36f, z), new Vector3(0.08f, 0.18f, 0.34f));
            Add(new Vector3(x, -0.08f, z - 0.02f), new Vector3(0.08f, 0.34f, 0.38f));
            for (int i = 0; i < 4; i++)
                Add(new Vector3(x, 0.17f, z - 0.15f + i * 0.10f), new Vector3(0.08f, 0.13f, 0.085f));
            Add(new Vector3(x, -0.08f, z + 0.25f), new Vector3(0.08f, 0.15f, 0.22f), Quaternion.Euler(0f, 0f, -24f));

            var emblem = new Mesh { name = "IronFistCrest_Gold", indexFormat = IndexFormat.UInt32 };
            emblem.CombineMeshes(gold.ToArray(), true, true);
            emblem.RecalculateBounds();
            var sigil = new GameObject("IronFistCrest_Sigil");
            sigil.transform.SetParent(root, false);
            sigil.AddComponent<MeshFilter>().sharedMesh = emblem;
            sigil.AddComponent<MeshRenderer>().sharedMaterial =
                KingdomMaterials.Make("IronFistGold", null, new Color(1.0f, 0.72f, 0.26f), 0.82f, 0.3f, Vector2.one);
            RuntimeMeshCleanup.Own(root, emblem);
        }

        /// <summary>Bright, gold-framed beacon lens attached to the existing Level 9 lantern block.</summary>
        static void AddBeaconLantern(IReadOnlyList<DestructibleBlock> blocks)
        {
            DestructibleBlock host = null;
            foreach (var b in blocks) if (b != null && b.blockId == "lhLantern") { host = b; break; }
            if (host == null) return;
            var root = MetricChild(host, "Kingdom_BeaconLens");
            var brass = KingdomMaterials.CannonBronze;
            var emissive = KingdomMaterials.Make("BeaconLensGlow", null, new Color(1f, 0.78f, 0.42f), 0.05f, 0.22f,
                Vector2.one, new Color(2.0f, 1.0f, 0.35f));
            foreach (float y in new[] { -0.42f, 0.38f })
            {
                var ring = R("BeaconLens_Ring", root, Cyl(), brass);
                ring.transform.localPosition = new Vector3(0f, y, 0f);
                ring.transform.localScale = new Vector3(0.96f, 0.075f, 0.96f);
            }
            var lens = R("BeaconLens_Core", root, Sphere(), emissive);
            lens.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            lens.transform.localScale = Vector3.one * 0.68f;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var strut = R("BeaconLens_Strut", root, Cyl(), brass);
                    strut.transform.localPosition = new Vector3(x * 0.39f, 0f, z * 0.39f);
                    strut.transform.localScale = new Vector3(0.055f, 0.72f, 0.055f);
                }
            var crown = R("BeaconLens_Cap", root, Cyl(), brass);
            crown.transform.localPosition = new Vector3(0f, 0.43f, 0f);
            crown.transform.localScale = new Vector3(0.72f, 0.07f, 0.72f);
        }

        /// <summary>Adds a breakable clock face to the cannon-facing upper storey of the Curfew Clocktower.</summary>
        static void AddClockFace(IReadOnlyList<DestructibleBlock> blocks)
        {
            DestructibleBlock host = null;
            foreach (var b in blocks) if (b != null && b.blockId == "wallF2") { host = b; break; }
            if (host == null) return;
            var root = MetricChild(host, "Kingdom_ClockFace");
            float front = -host.transform.localScale.x * 0.5f - 0.025f;
            var rim = R("ClockFace_Rim", root, Cyl(), KingdomMaterials.CannonBronze);
            rim.transform.localPosition = new Vector3(front, 0.42f, 0f);
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rim.transform.localScale = new Vector3(1.12f, 0.055f, 1.12f);
            var plate = R("ClockFace_Plate", root, Cyl(), KingdomMaterials.Make("ClockFaceIvory", null, new Color(0.82f, 0.75f, 0.61f), 0.12f, 0.72f, Vector2.one));
            plate.transform.localPosition = new Vector3(front - 0.055f, 0.42f, 0f);
            plate.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            plate.transform.localScale = new Vector3(0.94f, 0.035f, 0.94f);
            var dark = KingdomMaterials.Make("ClockHands", null, new Color(0.10f, 0.16f, 0.22f), 0.45f, 0.48f, Vector2.one);
            var handMesh = CvMesh.BoxCube(Vector3.one, 0.2f);
            var minute = R("ClockMinuteHand", root, handMesh, dark);
            minute.transform.localPosition = new Vector3(front - 0.095f, 0.61f, 0f);
            minute.transform.localScale = new Vector3(0.035f, 0.39f, 0.045f);
            var hour = R("ClockHourHand", root, handMesh, dark);
            hour.transform.localPosition = new Vector3(front - 0.095f, 0.42f, 0.12f);
            hour.transform.localScale = new Vector3(0.035f, 0.045f, 0.28f);
            var hub = R("ClockHub", root, Cyl(), KingdomMaterials.Gold);
            hub.transform.localPosition = new Vector3(front - 0.12f, 0.42f, 0f);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hub.transform.localScale = Vector3.one * 0.12f;
        }

        static void Decorate(IReadOnlyList<DestructibleBlock> blocks, bool castle, bool authoredBanners = false)
        {
            // banners: up to 2 walls with the biggest cannon-facing area
            var walls = new List<DestructibleBlock>();
            DestructibleBlock top = null; float topY = float.MinValue;
            foreach (var b in blocks)
            {
                if (b == null || b.role == "island" || b.isRoof || b.role == "roof") continue;
                var s = b.transform.localScale;
                if (!authoredBanners && s.y >= 1.1f && !ArchitectureDresser.Dressed.Contains(b) && s.z >= 1.1f && s.x <= Mathf.Max(s.y, s.z)) walls.Add(b);
                float y = b.transform.position.y + s.y * 0.5f;
                if (y > topY && s.x >= 0.25f && s.z >= 0.25f) { topY = y; top = b; }
            }
            walls.Sort((a, c) => (c.transform.localScale.y * c.transform.localScale.z).CompareTo(a.transform.localScale.y * a.transform.localScale.z));
            int placed = 0; var used = new List<Vector3>();
            foreach (var w in walls)
            {
                if (placed >= 2) break;
                bool near = false; foreach (var u in used) if ((u - w.transform.position).sqrMagnitude < 4f) near = true;
                if (near) continue;
                used.Add(w.transform.position); placed++;
                var s = w.transform.localScale; var m = MetricChild(w, "Kingdom_Banner");
                float bw = Mathf.Min(1.2f, s.z * 0.42f), bh = Mathf.Min(2.0f, s.y * 0.72f, bw * 1.75f);
                // Hinge the cloth from its top rod so the royal colours catch the same island breeze as the palms and roof flag.
                var rig = new GameObject("BannerRig").transform; rig.SetParent(m, false);
                rig.localPosition = new Vector3(-s.x * 0.5f - 0.05f, s.y * 0.5f - 0.1f, 0f);
                var q = R("Banner", rig, Quad(), KingdomMaterials.Banner);
                q.transform.localPosition = new Vector3(0.02f, -bh * 0.5f - 0.02f, 0f);
                q.transform.localScale = new Vector3(1f, bh, bw);
                var rod = R("BannerRod", rig, Cyl(), KingdomMaterials.Gold);
                rod.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); rod.transform.localScale = new Vector3(0.06f, bw * 0.6f, 0.06f);
                rig.gameObject.AddComponent<Sway>().amp = 3f;
            }
            if (top != null)
            {
                var s = top.transform.localScale; var m = MetricChild(top, "Kingdom_Flag");
                var pole = R("FlagPole", m, Cyl(), KingdomMaterials.Gold, true);
                pole.transform.localPosition = new Vector3(0f, s.y * 0.5f + FlagPole * 0.5f, 0f); pole.transform.localScale = new Vector3(0.07f, FlagPole * 0.5f, 0.07f);
                var cloth = new GameObject("FlagCloth").transform; cloth.SetParent(m, false); cloth.localPosition = new Vector3(0f, s.y * 0.5f + FlagPole - 0.38f, 0f);
                foreach (var mesh in new[] { Quad(), QuadBack() })
                {
                    var f = R("Flag", cloth, mesh, KingdomMaterials.Banner);
                    f.transform.localPosition = new Vector3(0f, 0f, -0.55f); f.transform.localScale = new Vector3(1f, 0.75f, 1.1f);
                }
                cloth.gameObject.AddComponent<Sway>().amp = 6f;
            }
        }
    }
}
