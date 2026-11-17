using System.Collections.Generic;
using Crashverse.Destruction;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    /// <summary>
    /// Phase 1.5: real architecture on each structural piece instead of a textured box. Every undressed timber piece gets built from
    /// architectural parts that fit its own box, so the layout, colliders, HP, joints, chunked destruction, sim and shot budgets don't
    /// change:
    ///  - wall: a plank-clad cabin with corner posts, a framed window with open shutters, and a door on the main cabin
    ///  - roof: a pitched shingle gable with a ridge beam and fascia boards
    ///  - deck/floor: a plank platform with edge beams and rope railings, on log stilts down to the ground
    ///  - post/support: a log
    ///  - small pieces (bell frame, bell, flag) get themed shapes
    /// The parts are merged into one mesh per material per piece (few draw calls). They are children of the piece, so they leave
    /// with it when it breaks, collapses or goes out of bounds.
    /// </summary>
    public static class ArchitectureDresser
    {
        class Builder
        {
            readonly Dictionary<Material, List<CombineInstance>> _parts = new Dictionary<Material, List<CombineInstance>>();
            public void Add(Material m, Mesh mesh, Matrix4x4 trs)
            {
                if (m == null || mesh == null) return;
                if (!_parts.TryGetValue(m, out var l)) _parts[m] = l = new List<CombineInstance>();
                l.Add(new CombineInstance { mesh = mesh, transform = trs });
            }
            public void Box(Material m, Vector3 c, Vector3 size, float tile, Quaternion r = default)
                => Add(m, CvMesh.BoxCube(size, tile), Matrix4x4.TRS(c, (r.x == 0f && r.y == 0f && r.z == 0f && r.w == 0f ? Quaternion.identity : r) /* Quaternion == is a dot test: default never equals */, size));
            public void Log(Material m, Vector3 a, Vector3 b, float radius)
            {
                var d = b - a; float len = d.magnitude; if (len < 1e-3f) return;
                Add(m, Cyl(), Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / len), new Vector3(radius * 2f, len * 0.5f, radius * 2f)));
            }
            public void Build(Transform parent, string name, bool shadows)
            {
                foreach (var kv in _parts)
                {
                    var mesh = new Mesh { name = name + "_" + kv.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                    mesh.RecalculateBounds(); if (mesh.uv != null && mesh.uv.Length == mesh.vertexCount) mesh.RecalculateTangents(); // normal maps
                    var go = new GameObject(name + "_" + kv.Key.name); go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    RuntimeMeshCleanup.Own(parent, mesh);
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = kv.Key;
                    r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                }
            }
        }

        static Mesh _cyl, _sph;
        static Mesh Cyl() { if (_cyl == null) { var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder); _cyl = t.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(t); } return _cyl; }
        static Mesh Sph() { if (_sph == null) { var t = GameObject.CreatePrimitive(PrimitiveType.Sphere); _sph = t.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(t); } return _sph; }

        static Material Planks => KingdomMaterials.Planks;
        static Material Timber => KingdomMaterials.Wood;
        static Material Shingle => KingdomMaterials.Roof;
        // PM QA (L01.jpg): timber huts get worn, weathered wood shingles (dark grey-brown, a few lighter sun-bleached ones), not clay tiles
        static Material WoodShingle => KingdomMaterials.Make("WoodShingle", "T_K_Planks", new Color(0.48f, 0.55f, 0.65f), 0f, 0.92f, Vector2.one);
        static Material WoodShingleWorn => KingdomMaterials.Make("WoodShingleWorn", "T_K_Planks", new Color(0.62f, 0.72f, 0.86f), 0f, 0.95f, Vector2.one);
        static Material WoodShingleDark => KingdomMaterials.Make("WoodShingleDark", "T_K_Wood", new Color(0.36f, 0.46f, 0.60f), 0f, 0.95f, Vector2.one);
        static Material Dark => KingdomMaterials.Make("WindowDark", null, new Color(0.06f, 0.07f, 0.09f), 0f, 0.4f, Vector2.one);
        static Material Rope => KingdomMaterials.Make("Rope", "T_K_Bark", new Color(0.95f, 0.85f, 0.62f), 0f, 0.9f, new Vector2(0.3f, 3f));
        static Material Bronze => KingdomMaterials.Make("Bronze", null, new Color(0.62f, 0.42f, 0.18f), 0.8f, 0.35f, Vector2.one);
        static Material Iron => KingdomMaterials.Iron;
        /// <summary>PM 17 Nov (b16 QA, L01.jpg): stilts, frame braces and the ladder in lighter, sun-weathered timber (T_K_Wood alone reads near-black).</summary>
        static Material Stilt => KingdomMaterials.Make("StiltTimber", "T_K_Planks", new Color(0.95f, 1.0f, 1.08f), 0f, 0.9f, Vector2.one);
        static Material Gap => KingdomMaterials.Make("BoardGap", "T_K_Wood", new Color(0.08f, 0.06f, 0.05f), 0f, 0.95f, Vector2.one);
        static Material PlanksWorn => KingdomMaterials.Make("PlanksWorn", "T_K_Planks", new Color(0.78f, 0.74f, 0.70f), 0f, 0.9f, Vector2.one); // sun-bleached boards
        // L1 QA 16 Nov (L01.jpg): weathered cabin boards in several tones, with grime and rot near the base
        // L1 QA 16 Nov (b): T_K_Planks/T_K_Wood are saturated orange-brown; cool tints pull the cabin to L01.jpg's grey-brown aged wood.
        static Material CabinBoard => KingdomMaterials.Make("CabinBoard", "T_K_Planks", new Color(0.74f, 0.6f, 0.46f), 0f, 0.92f, Vector2.one);
        static Material CabinBoardWorn => KingdomMaterials.Make("CabinBoardWorn", "T_K_Planks", new Color(0.92f, 0.78f, 0.62f), 0f, 0.94f, Vector2.one);
        static Material CabinBoardDark => KingdomMaterials.Make("CabinBoardDark", "T_K_Planks", new Color(0.46f, 0.36f, 0.27f), 0f, 0.95f, Vector2.one);
        static Material CabinBoardRot => KingdomMaterials.Make("CabinBoardRot", "T_K_Wood", new Color(0.62f, 0.56f, 0.46f), 0f, 0.97f, Vector2.one);
        static Material CabinGrime => KingdomMaterials.Make("CabinGrime", "T_K_Wood", new Color(0.3f, 0.25f, 0.2f), 0f, 0.98f, Vector2.one);
        static Material CarvedTrim => KingdomMaterials.Make("CarvedTrim", "T_K_Wood", new Color(0.8f, 0.68f, 0.54f), 0f, 0.85f, Vector2.one);

        public static HashSet<DestructibleBlock> Dressed = new HashSet<DestructibleBlock>();

        /// <summary>Dresses every undressed timber piece. Returns how many pieces were rebuilt.</summary>
        public static int Apply(IReadOnlyList<DestructibleBlock> blocks, bool castle = false)
        {
            Dressed.Clear(); int n = 0;
            if (blocks == null) return 0;
            foreach (var b in blocks)
            {
                if (b == null || b.role == "island") continue;
                var kind = b.material != null ? b.material.kind : MaterialKind.Wood;
                if (kind != MaterialKind.Wood && kind != MaterialKind.Brick) continue;
                if (b.role == "shoulder" || b.role == "arm" || b.role == "shutter" || b.role == "core" || b.role == "armor" || b.role == "prop") continue; // boss anatomy + props keep their look
                if (b.visual != null && !BoxKitOnly(b.visual)) continue; // authored building modules / props already are real architecture
                var s = b.transform.localScale; var id = (b.blockId ?? "").ToLowerInvariant();
                var bld = new Builder();
                bool ok;
                if (kind == MaterialKind.Brick)
                {
                    if (b.isRoof || b.role == "roof") ok = Roof(bld, s);
                    else if (b.role == "shoulder" || b.role == "arm" || b.role == "shutter" || b.role == "core" || b.role == "armor") ok = false; // boss anatomy keeps its look
                    else ok = Masonry(bld, s, castle, b, blocks);
                }
                else if (id.StartsWith("gable")) ok = GableHalf(bld, s, id.EndsWith("r") ? 1f : -1f); // PM b18 QA: L1 gable, ridge toward the camera
                else if (b.isRoof || b.role == "roof" || id.Contains("roof")) ok = Roof(bld, s, true);
                else if (id.Contains("flag")) ok = Flag(bld, s);
                else if (id == "bell" || id.EndsWith("bell")) ok = Bell(bld, s);
                else if (id.Contains("frame")) ok = Frame(bld, s);
                else if (id.Contains("crate")) ok = Crate(bld, s);
                else if (s.y <= 0.55f && s.x >= 1.2f && s.z >= 1.2f) ok = Deck(bld, s, b, blocks);
                else if (s.x <= 0.7f && s.z <= 0.7f && s.y >= 1f) ok = Post(bld, s);
                else if (s.y >= 0.8f && Mathf.Min(s.x, s.z) <= 0.3f && Mathf.Max(s.x, s.z) >= 0.8f) ok = Panel(bld, s, b.blockId); // L1 re-cut: plank wall panels
                else if (s.x >= 0.9f && s.y >= 0.9f && s.z >= 0.9f) ok = Cabin(bld, s, id.Contains("hut") || id.Contains("house") || id.Contains("main"));
                else ok = Beam(bld, s);
                if (!ok) continue;
                var root = Metric(b, "Kingdom_Arch");
                bld.Build(root, "Arch_" + b.blockId, true);
                var own = b.GetComponent<MeshRenderer>(); if (own != null) own.enabled = false;
                if (b.visual != null) foreach (var vr in b.visual.GetComponentsInChildren<Renderer>(true)) vr.enabled = false; // kit box dressing replaced
                Dressed.Add(b); n++;
            }
            return n;
        }

        /// <summary>True when a piece's visual is only the generic realistic box kit (Kit_RealWood_Block etc.), not authored modules.</summary>
        static bool BoxKitOnly(GameObject visual)
        {
            // RealKit names the LOD GameObjects "LOD0..2"; the generic piece is in the MESH name ("Kit_RealWood_Block_LOD0").
            // (The old check read the GameObject name, so nothing was ever dressed on device.) Props (crates/barrels) keep their model.
            bool any = false;
            foreach (var mf in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var n = mf.sharedMesh != null ? mf.sharedMesh.name : mf.name; int i = n.IndexOf("_LOD"); if (i > 0) n = n.Substring(0, i);
                if (n.StartsWith("Kit_RealProps_") || System.Array.IndexOf(Crashverse.Meta.RealPieceRules.Pieces, n) < 0) return false;
                any = true;
            }
            return any;
        }

        static Transform Metric(DestructibleBlock b, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(b.transform, false);
            var s = b.transform.localScale; t.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
            return t;
        }

        // ---------------- recipes (metres, origin = the piece's centre; -x faces the cannon) ----------------
        static bool Cabin(Builder k, Vector3 s, bool main)
        {
            float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f;
            // L01.jpg: weathered VERTICAL boards with dark gaps between them, each a little crooked / uneven at the ends,
            // over a dark inner core (the gaps read as shadow, never see-through)
            k.Box(Gap, Vector3.zero, s - Vector3.one * 0.1f, 1.2f);
            float bw = 0.3f, gap = 0.045f; /* PM b18 QA: solid wide boards, small dark gaps */ int seed = Mathf.RoundToInt(s.x * 97f + s.y * 31f + s.z * 13f);
            void Boards(Vector3 normal, Vector3 along, float width, float outAt)
            {
                int n = Mathf.Max(3, Mathf.RoundToInt((width - 0.2f) / (bw + gap))); float step = (width - 0.2f) / n;
                for (int i = 0; i < n; i++)
                {
                    uint h = (uint)(seed * 73856093 ^ (i + 1) * 19349663 ^ Mathf.RoundToInt(normal.x * 3f + normal.z * 7f) * 83492791);
                    float j0 = ((h & 255) / 255f - 0.5f), j1 = (((h >> 8) & 255) / 255f - 0.5f), j2 = (((h >> 16) & 255) / 255f - 0.5f);
                    float len = s.y * (0.97f + 0.05f * j1), dy = 0.03f * j2 - (s.y - len) * 0.5f;
                    var c = normal * (outAt + 0.018f * j1) + along * (-(width - 0.2f) * 0.5f + step * (i + 0.5f)) + Vector3.up * dy;
                    var rot = Quaternion.AngleAxis(11f * j0, normal) * Quaternion.AngleAxis(4f * j2, along); // crooked boards (b16 QA: readable at phone scale)
                    uint h2 = h * 2654435761u; float j3 = ((h2 >> 9) & 255) / 255f, j4 = ((h2 >> 19) & 255) / 255f;
                    var mat = j1 > 0.32f ? CabinBoardWorn : j1 < -0.3f ? CabinBoardDark : j3 > 0.86f ? CabinBoardRot : CabinBoard;
                    bool broken = j4 > 0.62f && i > 0 && i < n - 1; // L1 QA: a few snapped boards with the dark gap showing through
                    if (broken)
                    {
                        float keep = len * (0.45f + 0.2f * j3);
                        c += Vector3.up * ((len - keep) * 0.5f); len = keep;
                        rot = rot * Quaternion.AngleAxis(4f + 6f * j3, normal);
                    }
                    var size = Mathf.Abs(normal.x) > 0.5f ? new Vector3(0.07f, len, step - gap) : new Vector3(step - gap, len, 0.07f);
                    k.Box(mat, c, size, 1.1f, rot);
                    if (broken) // jagged stub at the bottom of the snapped board
                        k.Box(CabinBoardRot, normal * (outAt + 0.01f) + along * (-(width - 0.2f) * 0.5f + step * (i + 0.5f)) + Vector3.up * (-s.y * 0.5f + 0.16f),
                              Mathf.Abs(normal.x) > 0.5f ? new Vector3(0.07f, 0.3f, step - gap) : new Vector3(step - gap, 0.3f, 0.07f), 1.1f, Quaternion.AngleAxis(-7f, normal));
                }
            }
            Boards(Vector3.left, Vector3.forward, s.z, hx - 0.025f); Boards(Vector3.right, Vector3.forward, s.z, hx - 0.025f);
            Boards(Vector3.back, Vector3.right, s.x, hz - 0.025f); Boards(Vector3.forward, Vector3.right, s.x, hz - 0.025f);
            // a horizontal batten across the boards, top and bottom
            foreach (float yb in new[] { -hy + 0.18f, hy - 0.2f })
            {
                k.Box(Timber, new Vector3(-hx - 0.02f, yb, 0f), new Vector3(0.05f, 0.11f, s.z - 0.25f), 1f);
                k.Box(Timber, new Vector3(0f, yb, -hz - 0.02f), new Vector3(s.x - 0.25f, 0.11f, 0.05f), 1f);
                k.Box(Timber, new Vector3(0f, yb, hz + 0.02f), new Vector3(s.x - 0.25f, 0.11f, 0.05f), 1f);
            }
            // corner posts + sill / top plates
            foreach (float x in new[] { -hx + 0.08f, hx - 0.08f })
                foreach (float z in new[] { -hz + 0.08f, hz - 0.08f })
                    k.Box(Timber, new Vector3(x * 1.02f, 0.02f, z * 1.02f), new Vector3(0.24f, s.y + 0.08f, 0.24f), 1f); // chunky corner posts stand proud of the boards
            k.Box(Timber, new Vector3(-hx - 0.01f, -hy + 0.07f, 0f), new Vector3(0.1f, 0.14f, s.z), 1f);
            k.Box(Timber, new Vector3(-hx - 0.01f, hy - 0.07f, 0f), new Vector3(0.1f, 0.14f, s.z), 1f);
            // L1 QA 16 Nov: grime / damp stain along the base of every wall (dark, slightly proud of the boards)
            k.Box(CabinGrime, new Vector3(-hx - 0.012f, -hy + 0.2f, 0f), new Vector3(0.03f, 0.4f, s.z - 0.3f), 1f);
            k.Box(CabinGrime, new Vector3(0f, -hy + 0.2f, -hz - 0.012f), new Vector3(s.x - 0.3f, 0.4f, 0.03f), 1f);
            k.Box(CabinGrime, new Vector3(0f, -hy + 0.2f, hz + 0.012f), new Vector3(s.x - 0.3f, 0.4f, 0.03f), 1f);
            // carved frame on the cannon face: knee braces in the top corners and a notched (dentil) frieze under the eaves
            foreach (float sg in new[] { -1f, 1f })
            {
                var a = new Vector3(-hx - 0.06f, hy - 0.62f, sg * (hz - 0.14f)); var b = new Vector3(-hx - 0.06f, hy - 0.1f, sg * (hz - 0.62f));
                k.Log(CarvedTrim, a, b, 0.05f);
                k.Box(CarvedTrim, new Vector3(-hx - 0.07f, hy - 0.62f, sg * (hz - 0.14f)), new Vector3(0.06f, 0.1f, 0.14f), 1f);
            }
            int dent = Mathf.Max(4, Mathf.RoundToInt((s.z - 0.4f) / 0.16f));
            for (int i = 0; i < dent; i++)
                k.Box(CarvedTrim, new Vector3(-hx - 0.075f, hy - 0.31f, -(s.z - 0.4f) * 0.5f + (s.z - 0.4f) * (i + 0.5f) / dent), new Vector3(0.05f, 0.09f, 0.07f), 1f);
            k.Box(CarvedTrim, new Vector3(-hx - 0.06f, hy - 0.25f, 0f), new Vector3(0.04f, 0.035f, s.z - 0.3f), 1f);
            // cannon-facing openings: a door on the main cabin, otherwise a window with open shutters
            if (main && s.y >= 1.3f)
            {
                float dw = Mathf.Min(0.8f, s.z * 0.4f), dh = Mathf.Min(1.25f, s.y * 0.8f);
                k.Box(Dark, new Vector3(-hx - 0.005f, -hy + dh * 0.5f, 0f), new Vector3(0.02f, dh, dw), 1f);
                for (int i = 0; i < 4; i++) k.Box(Timber, new Vector3(-hx - 0.03f, -hy + dh * 0.5f, -dw * 0.375f + i * dw * 0.25f), new Vector3(0.04f, dh * 0.98f, dw * 0.23f), 1f); // door planks
                k.Box(Timber, new Vector3(-hx - 0.05f, -hy + dh + 0.05f, 0f), new Vector3(0.08f, 0.1f, dw + 0.2f), 1f); // lintel
                k.Box(Iron, new Vector3(-hx - 0.06f, -hy + dh * 0.5f, dw * 0.35f), new Vector3(0.03f, 0.06f, 0.06f), 1f); // handle
            }
            else if (s.z >= 1.0f && s.y >= 0.9f)
                Window(k, new Vector3(-hx, 0.05f * s.y, 0f), Mathf.Min(0.55f, s.z * 0.35f), Mathf.Min(0.5f, s.y * 0.45f));
            if (s.z >= 1.4f) Window(k, new Vector3(0f, 0.05f * s.y, -hz), Mathf.Min(0.5f, s.x * 0.35f), Mathf.Min(0.45f, s.y * 0.4f), true);
            return true;
        }

        /// <summary>Framed window on a face at c (face normal -x, or -z when side).</summary>
        static void Window(Builder k, Vector3 c, float w, float h, bool side = false)
        {
            Vector3 n = side ? Vector3.back : Vector3.left, a = side ? Vector3.right : Vector3.forward;
            Vector3 P(float along, float up, float outw) => c + a * along + Vector3.up * up + n * outw;
            Vector3 S(float along, float up, float depth) => side ? new Vector3(along, up, depth) : new Vector3(depth, up, along);
            k.Box(Dark, P(0f, 0f, 0.01f), S(w, h, 0.02f), 1f);
            k.Box(Timber, P(0f, h * 0.5f + 0.04f, 0.04f), S(w + 0.16f, 0.08f, 0.08f), 1f);
            k.Box(Timber, P(0f, -h * 0.5f - 0.05f, 0.06f), S(w + 0.24f, 0.08f, 0.14f), 1f); // sill
            k.Box(Timber, P(-w * 0.5f - 0.04f, 0f, 0.04f), S(0.08f, h, 0.08f), 1f);
            k.Box(Timber, P(w * 0.5f + 0.04f, 0f, 0.04f), S(0.08f, h, 0.08f), 1f);
            k.Box(Timber, P(0f, 0f, 0.03f), S(0.04f, h, 0.04f), 1f); // mullion
            // open shutters, swung out ~70°
            foreach (float sg in new[] { -1f, 1f })
            {
                var r = Quaternion.AngleAxis(sg * 70f, Vector3.up);
                k.Box(Planks, P(sg * (w * 0.5f + 0.1f), 0f, 0.1f), S(w * 0.5f, h, 0.04f), 1f, side ? Quaternion.AngleAxis(-sg * 70f, Vector3.up) : r);
            }
        }

        static bool Roof(Builder k, Vector3 s, bool wood = false)
        {
            // wood: deeper overhang (L01.jpg) and individual staggered, slightly crooked shingles on both slopes over a dark underlay
            float w = s.z + (wood ? 2.1f : 0.9f), d = s.x + (wood ? 2.3f : 1.0f) /* L1 QA 16 Nov: deeper eaves (L01.jpg) */, h = Mathf.Max(s.y, Mathf.Clamp(d * 0.42f, 0.6f, 2.2f)), y0 = -s.y * 0.5f;
            k.Add(wood ? WoodShingleDark : Shingle, CvMesh.WithBoxUv(Prism(w, d, h), 0.9f, Vector3.one), Matrix4x4.Translate(new Vector3(0f, y0, 0f)));
            float slope = Mathf.Atan2(h, d * 0.5f) * Mathf.Rad2Deg; float sl = Mathf.Sqrt(h * h + d * d * 0.25f);
            if (wood)
            {
                int rows = Mathf.Max(4, Mathf.RoundToInt(sl / 0.24f)); float rl = sl / rows; int seed = Mathf.RoundToInt(w * 53f + d * 29f);
                foreach (float sg in new[] { -1f, 1f })
                    for (int i = 0; i < rows; i++)
                    {
                        float f = (i + 0.5f) / rows, sw = 0.30f; int n = Mathf.Max(3, Mathf.RoundToInt(w / sw));
                        for (int j = 0; j < n; j++)
                        {
                            uint hsh = (uint)((seed + i * 131 + j * 17 + (sg > 0 ? 7919 : 0)) * 2654435761u);
                            float j0 = ((hsh >> 4) & 255) / 255f - 0.5f, j1 = ((hsh >> 12) & 255) / 255f - 0.5f, j2 = ((hsh >> 20) & 255) / 255f - 0.5f;
                            if (j2 > 0.46f && i > 0 && i < rows - 1) continue; // a missing shingle here and there
                            float z = -w * 0.5f + (j + 0.5f + ((i & 1) == 1 ? 0.5f : 0f)) * (w / n); if (z > w * 0.5f - 0.05f) continue;
                            var p = new Vector3(sg * d * 0.5f * (1f - f), y0 + h * f + 0.05f, z);
                            var rot = Quaternion.Euler(0f, 0f, sg * (90f - slope)) * Quaternion.Euler(j0 * 6f, 0f, -sg * 8f); // butt ends lifted, crooked
                            var m = j1 > 0.3f ? WoodShingleWorn : j1 < -0.35f ? WoodShingleDark : WoodShingle;
                            k.Box(m, p, new Vector3(0.045f + 0.02f * (j2 + 0.5f), rl * 1.35f, w / n * 0.92f), 0.9f, rot);
                        }
                    }
            }
            else
            {
                // shingle courses (stepped strips) on the cannon-facing slope for relief
                int rows = Mathf.Max(3, Mathf.RoundToInt(sl / 0.28f));
                for (int i = 0; i < rows; i++)
                {
                    float f = (i + 0.5f) / rows; var p = new Vector3(-d * 0.5f * (1f - f), y0 + h * f + 0.03f, 0f);
                    k.Box(Shingle, p, new Vector3(0.05f, sl / rows * 0.5f, w * 0.99f), 0.9f, Quaternion.Euler(0f, 0f, -(90f - slope)));
                }
            }
            k.Log(Timber, new Vector3(0f, y0 + h + 0.02f, -w * 0.5f - 0.1f), new Vector3(0f, y0 + h + 0.02f, w * 0.5f + 0.1f), 0.09f); // ridge
            // exposed rafter tails under the deep eaves (concept: chunky overhang with visible timber ends)
            for (float z = -w * 0.5f + 0.2f; z <= w * 0.5f - 0.15f; z += 0.45f)
                foreach (float sg in new[] { -1f, 1f })
                    k.Box(Timber, new Vector3(sg * (d * 0.5f - 0.25f), y0 - 0.06f, z), new Vector3(0.55f, 0.1f, 0.09f), 1f, Quaternion.Euler(0f, 0f, sg * Mathf.Atan2(h, d * 0.5f) * Mathf.Rad2Deg * 0.6f));
            // ridge cap boards + a weathered crown beam over each gable end
            foreach (float z in new[] { -w * 0.5f - 0.12f, w * 0.5f + 0.12f })
                k.Box(Timber, new Vector3(0f, y0 + h + 0.12f, z), new Vector3(0.12f, 0.3f, 0.12f), 1f);
            // fascia boards along the eaves + gable bargeboards
            k.Box(Timber, new Vector3(-d * 0.5f, y0 - 0.02f, 0f), new Vector3(0.08f, 0.12f, w + 0.05f), 1f);
            k.Box(Timber, new Vector3(d * 0.5f, y0 - 0.02f, 0f), new Vector3(0.08f, 0.12f, w + 0.05f), 1f);
            float bl = Mathf.Sqrt(h * h + d * d * 0.25f);
            foreach (float z in new[] { -w * 0.5f - 0.02f, w * 0.5f + 0.02f })
                foreach (float sg in new[] { -1f, 1f })
                    k.Box(Timber, new Vector3(sg * d * 0.25f, y0 + h * 0.5f, z), new Vector3(bl, 0.1f, 0.05f), 1f, Quaternion.Euler(0f, 0f, -sg * Mathf.Atan2(h, d * 0.5f) * Mathf.Rad2Deg));
            return true;
        }

        static bool Deck(Builder k, Vector3 s, DestructibleBlock self, IReadOnlyList<DestructibleBlock> blocks)
        {
            float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f;
            int n = Mathf.Max(4, Mathf.RoundToInt(s.z / 0.26f)); float pw = s.z / n;
            for (int i = 0; i < n; i++)
                k.Box(Planks, new Vector3(((i * 7) % 3 - 1) * 0.02f, hy - 0.05f, -hz + pw * (i + 0.5f)), new Vector3(s.x + 0.1f, 0.1f, pw * 0.92f), 1.2f);
            k.Box(Timber, new Vector3(0f, -0.05f, 0f), new Vector3(s.x - 0.1f, s.y - 0.12f, s.z - 0.1f), 1f); // joists block
            foreach (float x in new[] { -hx + 0.1f, hx - 0.1f }) k.Box(Timber, new Vector3(x, -hy + 0.12f, 0f), new Vector3(0.2f, 0.24f, s.z + 0.1f), 1f);
            // rope railing on posts around the platform's outer edge
            var posts = new List<Vector3>();
            for (float z = -hz + 0.12f; z <= hz - 0.1f; z += Mathf.Max(0.9f, s.z / 6f)) { posts.Add(new Vector3(-hx + 0.08f, 0f, z)); posts.Add(new Vector3(hx - 0.08f, 0f, z)); }
            foreach (var p in posts) k.Log(Timber, new Vector3(p.x, hy, p.z), new Vector3(p.x, hy + 0.95f, p.z), 0.05f);
            foreach (float x in new[] { -hx + 0.08f, hx - 0.08f })
                foreach (float y in new[] { 0.5f, 0.9f })
                    k.Log(Rope, new Vector3(x, hy + y, -hz + 0.12f), new Vector3(x, hy + y, hz - 0.12f), 0.025f);
            // stilts: log legs under the platform corners down to the ground under it
            float ground = 0f;
            foreach (var b in blocks) if (b != null && b.role == "island") { var bb = new Bounds(b.transform.position, b.transform.localScale); var c = self.transform.position; if (Mathf.Abs(c.x - bb.center.x) < bb.extents.x + 1f && Mathf.Abs(c.z - bb.center.z) < bb.extents.z + 1f) ground = Mathf.Max(ground, bb.max.y); }
            float drop = self.transform.position.y - hy - ground;
            // (L1 b20: the island block's top is 0.6 m, so these legs read as ~1.4 m stilts and a broken deck drops the house > 1 m)
            if (drop > 0.6f)
            {
                // PM 17 Nov (b16 QA, L01.jpg): an open timber frame under the platform: splayed log legs, X braces on every face and a
                // mid-height girt, in lighter weathered timber so it reads as a frame (not a dark mass) at phone scale
                var legs = new List<Vector3>();
                foreach (float x in new[] { -hx + 0.15f, hx - 0.15f })
                    foreach (float z in new[] { -hz + 0.2f, hz - 0.2f, 0f })
                    {
                        k.Log(Stilt, new Vector3(x, -hy, z), new Vector3(x * 1.08f, -hy - drop, z * 1.04f), 0.12f);
                        legs.Add(new Vector3(x, 0f, z));
                    }
                Vector3 Leg(float x, float z, float f) => new Vector3(x * (1f + 0.08f * f), -hy - drop * f, z * (1f + 0.04f * f));
                void Cross(float x0, float z0, float x1, float z1)
                {
                    k.Log(Stilt, Leg(x0, z0, 0.12f), Leg(x1, z1, 0.88f), 0.055f);
                    k.Log(Stilt, Leg(x0, z0, 0.88f), Leg(x1, z1, 0.12f), 0.055f);
                    k.Log(Stilt, Leg(x0, z0, 0.5f), Leg(x1, z1, 0.5f), 0.05f); // girt
                }
                float fx = -hx + 0.15f, bx = hx - 0.15f, zl = -hz + 0.2f, zr = hz - 0.2f;
                Cross(fx, zl, fx, 0f); Cross(fx, 0f, fx, zr);   // cannon-facing side
                Cross(bx, zl, bx, 0f); Cross(bx, 0f, bx, zr);   // back
                Cross(fx, zl, bx, zl); Cross(fx, zr, bx, zr);   // both ends
                // one big leaning ladder up the front-right of the right-most platform (L01.jpg), thick enough to read on a phone
                bool rightMost = true; var me = self.transform.position;
                foreach (var o in blocks)
                {
                    if (o == null || o == self) continue; var os = o.transform.localScale; var op = o.transform.position;
                    if (os.y <= 0.55f && os.x >= 1.2f && os.z >= 1.2f && Mathf.Abs(op.y - me.y) < 0.3f && Mathf.Abs(op.x - me.x) < 1.5f && op.z > me.z + 0.5f) { rightMost = false; break; }
                }
                if (rightMost)
                {
                    float lz = -hz * 0.62f, foot = -hx - 1.1f; // PM b18 QA (L01.jpg): the ladder on the right (+z is screen-left)
                    var lo = new Vector3(foot, -hy - drop, lz); var hi = new Vector3(-hx - 0.05f, hy + 0.75f, lz);
                    foreach (float dz in new[] { -0.3f, 0.3f }) k.Log(Stilt, lo + Vector3.forward * dz, hi + Vector3.forward * dz, 0.065f);
                    float lh = drop + s.y + 0.75f;
                    for (float y = 0.3f; y < lh - 0.15f; y += 0.34f)
                    {
                        var p = Vector3.Lerp(lo, hi, y / lh);
                        k.Log(Stilt, p + Vector3.back * 0.33f, p + Vector3.forward * 0.33f, 0.045f);
                    }
                }
            }
            return true;
        }

        /// <summary>Masonry piece: brick (town) or castle-stone body, dressed stone quoins at the corners, a moulded cornice, framed
        /// windows on the cannon face when large enough, and battlements on the topmost castle pieces.</summary>
        static bool Masonry(Builder k, Vector3 s, bool castle, DestructibleBlock self, IReadOnlyList<DestructibleBlock> blocks)
        {
            var body = castle ? KingdomMaterials.Stone : KingdomMaterials.Brick;
            var quoin = KingdomMaterials.Make("Quoin", "T_K_Stone", new Color(0.92f, 0.9f, 0.86f), 0f, 0.85f, Vector2.one);
            float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f;
            k.Box(body, Vector3.zero, s, castle ? 2.5f : 1.6f);
            if (Mathf.Min(s.x, s.z) >= 0.6f && s.y >= 0.6f)
            {
                int rows = Mathf.Max(2, Mathf.RoundToInt(s.y / 0.32f)); float rh = s.y / rows;
                foreach (float x in new[] { -hx, hx })
                    foreach (float z in new[] { -hz, hz })
                        for (int i = 0; i < rows; i++)
                        {
                            bool lng = i % 2 == 0; var c = new Vector3(x - Mathf.Sign(x) * 0.12f + Mathf.Sign(x) * 0.015f, -hy + rh * (i + 0.5f), z - Mathf.Sign(z) * (lng ? 0.2f : 0.12f) + Mathf.Sign(z) * 0.015f);
                            k.Box(quoin, c, new Vector3(lng ? 0.27f : 0.27f, rh * 0.9f, lng ? 0.43f : 0.27f), 1f);
                        }
                k.Box(quoin, new Vector3(0f, hy - 0.06f, 0f), new Vector3(s.x + 0.08f, 0.12f, s.z + 0.08f), 1f); // cornice
            }
            if (s.z >= 1.4f && s.y >= 1.2f)
            {
                float w = Mathf.Min(0.6f, s.z * 0.28f), h = Mathf.Min(0.8f, s.y * 0.45f);
                foreach (float z in s.z >= 2.6f ? new[] { -s.z * 0.25f, s.z * 0.25f } : new[] { 0f })
                {
                    var c = new Vector3(-hx, 0.05f, z);
                    k.Box(Dark, c + Vector3.left * 0.01f, new Vector3(0.02f, h, w), 1f);
                    k.Box(quoin, c + new Vector3(-0.05f, h * 0.5f + 0.06f, 0f), new Vector3(0.1f, 0.12f, w + 0.2f), 1f);
                    k.Box(quoin, c + new Vector3(-0.07f, -h * 0.5f - 0.05f, 0f), new Vector3(0.14f, 0.1f, w + 0.26f), 1f);
                    k.Box(Timber, c + new Vector3(-0.03f, 0f, 0f), new Vector3(0.04f, h, 0.05f), 1f);
                    k.Box(Timber, c + new Vector3(-0.03f, 0f, 0f), new Vector3(0.04f, 0.05f, w), 1f);
                }
            }
            // battlements on the topmost castle pieces (nothing resting on them)
            if (castle && self.Dependents.Count == 0 && s.x >= 0.8f && s.z >= 0.8f)
            {
                int n = Mathf.Max(2, Mathf.RoundToInt(s.z / 0.6f)); float mw = s.z / (n * 2 - 1);
                foreach (float x in new[] { -hx + 0.15f, hx - 0.15f })
                    for (int i = 0; i < n; i++) k.Box(body, new Vector3(x, hy + 0.22f, -hz + mw * (i * 2 + 0.5f)), new Vector3(0.3f, 0.44f, mw), 1.5f);
            }
            return true;
        }

        static bool Post(Builder k, Vector3 s)
        {
            float r = Mathf.Min(s.x, s.z) * 0.45f;
            k.Log(Stilt, new Vector3(0f, -s.y * 0.5f, 0f), new Vector3(0f, s.y * 0.5f, 0f), r);
            k.Log(Rope, new Vector3(0f, s.y * 0.2f, 0f), new Vector3(0f, s.y * 0.2f + 0.12f, 0f), r + 0.02f);
            return true;
        }

        /// <summary>PM 17 Nov (L1 re-cut into ~30 breakable pieces): a thin wall panel = rickety vertical boards on both faces over a dark
        /// core, uneven lengths, crooked (up to ~6 deg), a few snapped or missing boards, top/bottom battens and a diagonal brace.</summary>
        static bool Panel(Builder k, Vector3 s, string id)
        {
            bool alongZ = s.x <= s.z; float width = alongZ ? s.z : s.x, thick = alongZ ? s.x : s.z, hy = s.y * 0.5f;
            var along = alongZ ? Vector3.forward : Vector3.right; var normal = alongZ ? Vector3.left : Vector3.back;
            k.Box(Gap, Vector3.zero, alongZ ? new Vector3(thick * 0.5f, s.y - 0.1f, width - 0.06f) : new Vector3(width - 0.06f, s.y - 0.1f, thick * 0.5f), 1.2f);
            int seed = 0; foreach (char ch in id ?? "") seed = seed * 31 + ch;
            float bw = 0.3f, gap = 0.045f; /* PM b18 QA: solid wide boards, small dark gaps over the dark backing */ int n = Mathf.Max(3, Mathf.RoundToInt(width / (bw + gap))); float step = width / n;
            foreach (float side in new[] { 1f, -1f })
                for (int i = 0; i < n; i++)
                {
                    uint h = (uint)(seed * 73856093 ^ (i + 1) * 19349663 ^ (side > 0 ? 83492791 : 49979687));
                    float j0 = (h & 255) / 255f - 0.5f, j1 = ((h >> 8) & 255) / 255f - 0.5f, j2 = ((h >> 16) & 255) / 255f - 0.5f, j3 = ((h >> 24) & 255) / 255f;
                    if (side > 0 && j3 > 0.94f && i > 0 && i < n - 1) continue; // a missing board: the dark gap shows through
                    float len = s.y * (0.93f + 0.1f * j1), dy = 0.05f * j2 - (s.y - len) * 0.5f;
                    var c = normal * side * (thick * 0.5f - 0.02f + 0.02f * j1) + along * (-width * 0.5f + step * (i + 0.5f)) + Vector3.up * dy;
                    var rot = Quaternion.AngleAxis(12f * j0, normal) * Quaternion.AngleAxis(4f * j2, along); // b16 QA: up to ~6 deg read as straight at phone scale
                    var mat = j1 > 0.3f ? CabinBoardWorn : j1 < -0.28f ? CabinBoardDark : j3 > 0.8f ? CabinBoardRot : CabinBoard;
                    if (side > 0 && j3 > 0.64f && j3 <= 0.86f && i > 0 && i < n - 1) // snapped board: short, kinked, jagged stub at the base
                    {
                        float keep = len * (0.4f + 0.25f * j1 + 0.12f); c += Vector3.up * ((len - keep) * 0.5f); len = keep; rot = rot * Quaternion.AngleAxis(8f + 8f * j2, normal);
                        k.Box(CabinBoardRot, c - Vector3.up * (len * 0.5f + 0.35f), alongZ ? new Vector3(0.06f, 0.35f, step - gap) : new Vector3(step - gap, 0.35f, 0.06f), 1.1f, Quaternion.AngleAxis(-9f, normal));
                    }
                    k.Box(mat, c, alongZ ? new Vector3(0.06f, len, step - gap) : new Vector3(step - gap, len, 0.06f), 1.1f, rot);
                }
            foreach (float yb in new[] { -hy + 0.2f, hy - 0.22f }) // battens, slightly skewed
                k.Box(Timber, normal * (thick * 0.5f + 0.03f) + Vector3.up * yb, alongZ ? new Vector3(0.05f, 0.11f, width - 0.1f) : new Vector3(width - 0.1f, 0.11f, 0.05f), 1f,
                      Quaternion.AngleAxis(2.5f * Mathf.Sign(yb + 0.01f) * ((seed & 1) == 0 ? 1f : -1f), normal));
            var a = normal * (thick * 0.5f + 0.05f) + along * (-width * 0.5f + 0.12f) + Vector3.up * (-hy + 0.3f);
            var b2 = normal * (thick * 0.5f + 0.05f) + along * (width * 0.5f - 0.12f) + Vector3.up * (hy - 0.32f);
            if ((seed & 2) != 0) { float t = a.y; a.y = b2.y; b2.y = t; }
            k.Log(CarvedTrim, a, b2, 0.045f); // diagonal brace
            k.Box(CabinGrime, normal * (thick * 0.5f + 0.012f) + Vector3.up * (-hy + 0.2f), alongZ ? new Vector3(0.03f, 0.38f, width - 0.1f) : new Vector3(width - 0.1f, 0.38f, 0.03f), 1f);
            if ((id ?? "").ToLowerInvariant().Contains("win")) Window(k, normal, along, alongZ, thick, width, s.y);
            return true;
        }

        /// <summary>PM b18 QA (L01.jpg): a dark window opening in a timber frame with a sill and two board shutters hanging open.</summary>
        static void Window(Builder k, Vector3 normal, Vector3 along, bool alongZ, float thick, float width, float height)
        {
            float w = Mathf.Min(0.72f, width * 0.48f), h = Mathf.Min(0.7f, height * 0.45f), yc = height * 0.08f, f = thick * 0.5f;
            Vector3 Sz(float a, float y, float n) => alongZ ? new Vector3(n, y, a) : new Vector3(a, y, n);
            var c = normal * (f + 0.05f) + Vector3.up * yc;
            k.Box(Gap, c, Sz(w, h, 0.03f), 1f);                                                    // the dark room behind
            foreach (float sg in new[] { -1f, 1f })
            {
                k.Box(Timber, c + normal * 0.03f + along * sg * (w * 0.5f + 0.04f), Sz(0.09f, h + 0.16f, 0.07f), 1f);   // jambs
                k.Box(Timber, c + normal * 0.03f + Vector3.up * sg * (h * 0.5f + 0.04f), Sz(w + 0.16f, 0.09f, 0.07f), 1f); // head / sill
                var sh = c + normal * 0.09f + along * sg * (w * 0.5f + 0.1f + w * 0.25f);                // shutter swung open against the wall
                var rot = Quaternion.AngleAxis(sg * 14f, Vector3.up);
                for (int i = 0; i < 2; i++)
                    k.Box(i == 0 ? CabinBoardDark : CabinBoard, sh + along * ((i - 0.5f) * w * 0.25f), Sz(w * 0.24f, h * 0.98f, 0.045f), 1f, rot);
                k.Box(Timber, sh + normal * 0.03f + Vector3.up * (h * 0.25f), Sz(w * 0.5f, 0.06f, 0.03f), 1f, rot); // ledges
                k.Box(Timber, sh + normal * 0.03f - Vector3.up * (h * 0.25f), Sz(w * 0.5f, 0.06f, 0.03f), 1f, rot);
            }
            k.Box(Timber, c + normal * 0.06f - Vector3.up * (h * 0.5f + 0.1f), Sz(w + 0.3f, 0.07f, 0.16f), 1f); // projecting sill
        }

        /// <summary>PM b18 QA (L01.jpg): one half of a steep gable roof whose ridge runs toward the camera, so the gable triangle faces
        /// the cannon. side -1: the block's ridge edge is at +z (the half slopes down to -z); +1 the mirror. Dark underlay, staggered
        /// crooked shingles, deep eaves (0.7 m) and front/back overhangs (0.55 m), bargeboards, a ridge log, rafter tails and a plank
        /// gable end with dark gaps.</summary>
        static bool GableHalf(Builder k, Vector3 s, float side)
        {
            float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f, ov = 0.7f, ovx = 0.55f;
            float ang = Mathf.Atan2(s.y, s.z);
            var ridge = new Vector3(0f, hy, -side * hz);
            var down = new Vector3(0f, -Mathf.Sin(ang), side * Mathf.Cos(ang));      // along the slope, ridge -> eave
            var n = new Vector3(0f, Mathf.Cos(ang), side * Mathf.Sin(ang));          // slope normal (up / outward)
            float L = Mathf.Sqrt(s.y * s.y + s.z * s.z) + ov, len = s.x + 2f * ovx;
            var rot = Quaternion.LookRotation(down, n);
            k.Box(WoodShingleDark, ridge + down * (L * 0.5f) - n * 0.07f, new Vector3(len, 0.14f, L), 1f, rot); // underlay / sheathing
            int rows = Mathf.Max(5, Mathf.RoundToInt(L / 0.3f)); float rl = L / rows; int cols = Mathf.Max(4, Mathf.RoundToInt(len / 0.34f)); float cw = len / cols;
            int seed = Mathf.RoundToInt(s.x * 53f + s.z * 29f + side * 7f);
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    uint hsh = (uint)((seed + i * 131 + j * 17) * 2654435761u);
                    float j0 = ((hsh >> 4) & 255) / 255f - 0.5f, j1 = ((hsh >> 12) & 255) / 255f - 0.5f, j2 = ((hsh >> 20) & 255) / 255f - 0.5f;
                    if (j2 > 0.45f && i > 0 && i < rows - 1) continue; // a missing shingle here and there
                    float x = -len * 0.5f + (j + 0.5f + ((i & 1) == 1 ? 0.5f : 0f)) * cw; if (x > len * 0.5f - 0.05f) continue;
                    var p = ridge + down * ((i + 0.62f) * rl) + n * (0.02f + 0.03f * (i & 1)) + Vector3.right * x;
                    var m = j1 > 0.28f ? WoodShingleWorn : j1 < -0.33f ? WoodShingleDark : WoodShingle;
                    k.Box(m, p, new Vector3(cw * 0.93f, 0.05f, rl * 1.35f), 0.9f, rot * Quaternion.Euler(-7f, j0 * 6f, j0 * 4f));
                }
            foreach (float sx in new[] { -1f, 1f }) // bargeboards along both gable edges
                k.Box(Timber, ridge + down * (L * 0.5f) + Vector3.right * sx * (len * 0.5f + 0.03f) + n * 0.02f, new Vector3(0.08f, 0.26f, L + 0.05f), 1f, rot);
            k.Box(Timber, ridge + down * (L - 0.04f) - n * 0.04f, new Vector3(len + 0.1f, 0.2f, 0.09f), 1f, rot);  // eave fascia
            k.Log(Timber, new Vector3(-len * 0.5f - 0.1f, hy + 0.06f, ridge.z), new Vector3(len * 0.5f + 0.1f, hy + 0.06f, ridge.z), 0.1f); // ridge log
            for (float x = -len * 0.5f + 0.25f; x < len * 0.5f - 0.1f; x += 0.5f) // rafter tails under the eave
                k.Box(Timber, ridge + down * (L - 0.35f) - n * 0.16f + Vector3.right * x, new Vector3(0.09f, 0.11f, 0.6f), 1f, rot);
            // gable end walls (front + back): vertical boards up to the slope over a dark backing
            float bw = 0.3f; int nb = Mathf.Max(2, Mathf.CeilToInt(s.z / bw));
            foreach (float gx in new[] { -hx, hx })
                for (int i = 0; i < nb; i++)
                {
                    float z0 = -side * hz + side * (i * s.z / nb), z1 = -side * hz + side * ((i + 1) * s.z / nb), zc = (z0 + z1) * 0.5f;
                    float dist = Mathf.Abs(zc - ridge.z), top = hy - dist * s.y / s.z - 0.05f, hgt = top + hy; if (hgt < 0.08f) continue;
                    uint hsh = (uint)((seed + i * 977 + (gx < 0 ? 0 : 5)) * 2654435761u); float j1 = ((hsh >> 12) & 255) / 255f - 0.5f;
                    k.Box(Gap, new Vector3(gx * 0.98f, -hy + hgt * 0.5f, zc), new Vector3(0.05f, hgt, s.z / nb), 1f);
                    k.Box(j1 > 0.25f ? CabinBoardWorn : j1 < -0.3f ? CabinBoardDark : CabinBoard, new Vector3(gx + Mathf.Sign(gx) * 0.03f, -hy + hgt * 0.5f, zc),
                          new Vector3(0.05f, hgt * (0.96f + 0.04f * j1), s.z / nb - 0.045f), 1.1f);
                }
            return true;
        }

        static bool Beam(Builder k, Vector3 s) { k.Box(Timber, Vector3.zero, s, 1f); k.Box(Planks, new Vector3(0f, s.y * 0.5f + 0.01f, 0f), new Vector3(s.x * 0.98f, 0.02f, s.z * 0.98f), 1f); return true; }

        /// <summary>b18 (L01.jpg balcony clutter): a slatted cargo crate: dark core, horizontal slats with gaps and corner battens.</summary>
        static bool Crate(Builder k, Vector3 s)
        {
            k.Box(CabinGrime, Vector3.zero, s * 0.94f, 1f);
            float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f; int n = 3; float sh = s.y / n;
            for (int i = 0; i < n; i++)
            {
                float y = -hy + sh * (i + 0.5f); var m = i == 1 ? CabinBoardWorn : CabinBoard;
                k.Box(m, new Vector3(0f, y, -hz + 0.02f), new Vector3(s.x * 0.96f, sh * 0.78f, 0.04f), 1f);
                k.Box(m, new Vector3(0f, y, hz - 0.02f), new Vector3(s.x * 0.96f, sh * 0.78f, 0.04f), 1f);
                k.Box(m, new Vector3(-hx + 0.02f, y, 0f), new Vector3(0.04f, sh * 0.78f, s.z * 0.96f), 1f);
                k.Box(m, new Vector3(hx - 0.02f, y, 0f), new Vector3(0.04f, sh * 0.78f, s.z * 0.96f), 1f);
            }
            k.Box(CabinBoardDark, new Vector3(0f, hy - 0.02f, 0f), new Vector3(s.x * 0.98f, 0.04f, s.z * 0.98f), 1f);
            foreach (float x in new[] { -hx + 0.04f, hx - 0.04f }) foreach (float z in new[] { -hz + 0.04f, hz - 0.04f })
                k.Box(Timber, new Vector3(x, 0f, z), new Vector3(0.09f, s.y, 0.09f), 1f);
            return true;
        }

        static bool Frame(Builder k, Vector3 s)
        {
            float hz = s.z * 0.5f, hy = s.y * 0.5f;
            foreach (float z in new[] { -hz + 0.07f, hz - 0.07f }) k.Box(Timber, new Vector3(0f, 0f, z), new Vector3(Mathf.Min(0.14f, s.x), s.y, 0.14f), 1f);
            k.Box(Timber, new Vector3(0f, hy - 0.07f, 0f), new Vector3(Mathf.Min(0.16f, s.x), 0.14f, s.z + 0.1f), 1f);
            k.Box(Shingle, new Vector3(0f, hy + 0.08f, 0f), new Vector3(0.5f, 0.06f, s.z + 0.25f), 1f);
            return true;
        }

        static bool Bell(Builder k, Vector3 s)
        {
            float r = Mathf.Min(s.x, s.z) * 0.5f;
            k.Add(Bronze, Sph(), Matrix4x4.TRS(new Vector3(0f, s.y * 0.15f, 0f), Quaternion.identity, new Vector3(r * 1.6f, s.y * 0.7f, r * 1.6f)));
            k.Log(Bronze, new Vector3(0f, -s.y * 0.3f, 0f), new Vector3(0f, -s.y * 0.18f, 0f), r * 0.95f);
            k.Log(Iron, new Vector3(0f, s.y * 0.5f, 0f), new Vector3(0f, s.y * 0.35f, 0f), 0.04f);
            return true;
        }

        static bool Flag(Builder k, Vector3 s)
        {
            // L1 QA 16 Nov: the pennant hid behind the cabin roof; a taller pole with the cloth flying OUTWARD (+z = away from the cabin)
            float top = s.y * 0.5f + 1.1f;
            k.Log(Timber, new Vector3(0f, -s.y * 0.5f, 0f), new Vector3(0f, top, 0f), 0.045f);
            k.Add(KingdomMaterials.Banner, CvMesh.BoxCube(new Vector3(0.02f, 0.42f, 0.75f), 1f), Matrix4x4.TRS(new Vector3(0f, top - 0.24f, 0.4f), Quaternion.Euler(0f, 0f, -4f), new Vector3(0.02f, 0.42f, 0.75f)));
            return true;
        }

        public static Mesh Prism(float w, float d, float h)
        {
            float x = d * 0.5f, z = w * 0.5f;
            var v = new List<Vector3>(); var t = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 e) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(e); t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
            void Tri(Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.AddRange(new[] { i, i + 1, i + 2 }); }
            Vector3 fl = new Vector3(-x, 0, z), fr = new Vector3(-x, 0, -z), bl = new Vector3(x, 0, z), br = new Vector3(x, 0, -z), rl = new Vector3(0, h, z), rr = new Vector3(0, h, -z);
            Quad(fl, rl, rr, fr); Quad(br, rr, rl, bl); Tri(fl, bl, rl); Tri(br, fr, rr); Quad(fr, br, bl, fl);
            int n = v.Count; var t2 = new List<int>(t);
            for (int i = 0; i < t.Count; i += 3) { t2.Add(t[i] + n); t2.Add(t[i + 2] + n); t2.Add(t[i + 1] + n); }
            v.AddRange(new List<Vector3>(v));
            var m = new Mesh { name = "KPrism" }; m.SetVertices(v); m.SetTriangles(t2, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
