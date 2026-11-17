using System.Collections.Generic;
using Crashverse.Destruction;
using UnityEngine;

namespace Crashverse.Visuals
{
    /// <summary>
    /// L1 v2 "Tide Watchtower" dressing (design/L01_v2_spec.md, goal design/level_concepts_v1/L01_v2_goal.png). Every physics block of
    /// layout c1_l01_tide_watchtower gets the part of the goal image it stands for, built procedurally in its own (metric) space so it
    /// leaves with the block when it breaks or falls: battered stone tiers with a voussoir arch, the red corruption crystal, the
    /// horned skull, banners, the timber gallery and its lit room, the red pagoda roof with dormers and a turret, both archer towers
    /// (legs, braces, decks, railings, ladder, fire pots, the enemy cannon, archers) and the rock islets with steps, moss and foam.
    /// Numbers are world metres for the base (-3, 0, 0) the spec measured; other bases are offset. Visual only: colliders, HP,
    /// supports and the simulation are untouched.
    /// </summary>
    public static class TideWatchtower
    {
        public const string LayoutId = "c1_l01_tide_watchtower";
        static readonly Vector3 SpecBase = new Vector3(-3f, 0f, 0f);
        /// <summary>PM b25 QA (b26): everything above the islet top (y 0.8) is 1.4x taller, in the layout (Tools/ContentGen/l1v2/gen_layout.py,
        /// same K) and in the dressing (TwMesh warp; figures, skulls, barrels and fire pots keep their shape via PropBegin/PropEnd).</summary>
        // PM b26 QA (b27): piecewise height map, identical to Tools/ContentGen/l1v2/gen_layout.py Y(): below 0.78 unchanged (rocks,
        // sea); the islet top 0.8 becomes the plinth top P; tier 1 (to the arch top 3.65) x K1; up to the roof top 9.45 x K2; turret x K3.
        internal const float StretchP = 2.48f, StretchK1 = 1.42f, StretchK2 = 1.055f, StretchK3 = 0.85f, StretchPivot = 0.78f;
        const float StretchYA = StretchP + (3.65f - 0.8f) * StretchK1, StretchYR = StretchYA + (9.45f - 3.65f) * StretchK2;
        internal static float StretchY(float y) =>
            y < StretchPivot ? y : y <= 3.65f ? StretchP + (y - 0.8f) * StretchK1 : y <= 9.45f ? StretchYA + (y - 3.65f) * StretchK2 : StretchYR + (y - 9.45f) * StretchK3;
        static float StretchSlope(float y) => y < StretchPivot ? 1f : y <= 3.65f ? StretchK1 : y <= 9.45f ? StretchK2 : StretchK3;
        static float UnstretchY(float w) =>
            w < StretchPivot ? w : w <= StretchYA ? 0.8f + (w - StretchP) / StretchK1 : w <= StretchYR ? 3.65f + (w - StretchYA) / StretchK2 : 9.45f + (w - StretchYR) / StretchK3;
        static void Prop(TwMesh t, Vector3 anchor, float scale, System.Action draw) { t.PropBegin(anchor, scale); draw(); t.PropEnd(); }

        public static bool Matches(IReadOnlyList<DestructibleBlock> blocks)
        {
            if (blocks == null) return false;
            bool crystal = false, t1 = false, ltop = false;
            foreach (var b in blocks) { if (b == null) continue; var id = b.blockId; crystal |= id == "crystal"; t1 |= id == "t1L"; ltop |= id == "Ltop"; }
            return crystal && t1 && ltop;
        }

        // ---------------- palette (design/L01_v2_spec.md hex palette; tints = goal colour / texture mean) ----------------
        static Material M(string k, string tex, Color c, float metal = 0f, float rough = 0.9f, Color emission = default) => KingdomMaterials.Make("Tw" + k, tex, c, metal, rough, Vector2.one, emission);
        internal static Material Stone => M("Stone", "T_TW_Stone", new Color(0.80f, 0.79f, 0.80f));           // PM b22: darker grey stone
        internal static Material StoneLight => M("StoneLight", "T_TW_Stone", new Color(0.98f, 0.95f, 0.93f));
        internal static Material StoneDark => M("StoneDark", "T_TW_Stone", new Color(0.60f, 0.58f, 0.60f));
        internal static Material Mortar => M("Mortar", null, new Color(0.13f, 0.09f, 0.08f), 0f, 1f);
        internal static Material Dressed => M("Dressed", "T_TW_Stone", new Color(1.30f, 0.98f, 0.86f));   // arch voussoirs, jambs #C48A6F
        internal static Material ArchInside => M("ArchInside", null, new Color(0.27f, 0.06f, 0.11f), 0f, 0.95f, new Color(0.10f, 0.01f, 0.02f)); // #44101E
        internal static Material ArchCrack => M("ArchCrack", null, new Color(0.55f, 0.15f, 0.19f), 0f, 0.8f, new Color(0.9f, 0.12f, 0.08f));   // #8C2630 glowing
        internal static Material Rock => M("Rock", "T_TW_Rock", new Color(0.60f, 0.54f, 0.48f));        // b26: darker grey-brown        // #7C6E5F
        internal static Material RockDark => M("RockDark", "T_TW_Rock", new Color(0.40f, 0.38f, 0.36f));  // #393933 in shade
        internal static Material RockTop => M("RockTop", "T_TW_Rock", new Color(0.78f, 0.72f, 0.64f));    // sun-lit tops #E7D1A8
        internal static Material Moss => M("Moss", "T_TW_Grass", new Color(1.15f, 1.15f, 1.1f));
        internal static Material Fern => M("Fern", null, new Color(0.30f, 0.55f, 0.12f), 0f, 0.7f);
        internal static Material Foam => M("Foam", null, new Color(0.93f, 0.97f, 1.0f), 0f, 0.6f, new Color(0.18f, 0.2f, 0.22f));
        internal static Material Timber => M("Timber", "T_TW_Timber", new Color(0.58f, 0.50f, 0.52f));     // PM b24: darker, less orange // dark timber #361C14 .. lit #AF612C
        internal static Material TimberLit => M("TimberLit", "T_TW_Deck", new Color(0.64f, 0.52f, 0.52f));
        internal static Material Plank => M("Plank", "T_TW_Deck", new Color(0.58f, 0.48f, 0.49f));
        internal static Material Iron => M("Iron", null, new Color(0.16f, 0.16f, 0.18f), 0.5f, 0.55f);
        internal static Material Gold => M("Gold", null, new Color(0.86f, 0.60f, 0.27f), 0.35f, 0.45f);
        internal static Material RoofTile => M("RoofTile", "T_TW_Roof", new Color(1.05f, 0.78f, 0.78f), 0f, 0.6f);     // PM b22: saturated red   // #E15A43
        internal static Material RoofTileDark => M("RoofTileDark", "T_TW_Roof", new Color(0.78f, 0.55f, 0.55f), 0f, 0.7f);
        internal static Material RoofUnder => M("RoofUnder", null, new Color(0.36f, 0.10f, 0.08f), 0f, 0.9f);    // #5C1A15
        internal static Material Banner => M("Banner", null, new Color(0.60f, 0.07f, 0.05f), 0f, 0.85f);         // #8C140E
        internal static Material BannerFold => M("BannerFold", null, new Color(0.42f, 0.04f, 0.04f), 0f, 0.9f);
        internal static Material Emblem => M("Emblem", null, new Color(0.96f, 0.80f, 0.70f), 0f, 0.8f);
        internal static Material Bone => M("Bone", null, new Color(0.93f, 0.82f, 0.62f), 0f, 0.7f);            // #F7DEB1
        internal static Material BoneDark => M("BoneDark", null, new Color(0.62f, 0.38f, 0.22f), 0f, 0.8f);     // #C67B48
        internal static Material Socket => M("Socket", null, new Color(0.05f, 0.02f, 0.02f), 0f, 1f);
        internal static Material EyeGlow => M("EyeGlow", null, new Color(0.9f, 0.1f, 0.05f), 0f, 0.5f, new Color(2.4f, 0.25f, 0.1f));
        internal static Material Crystal => M("Crystal", null, new Color(0.91f, 0.09f, 0.18f), 0f, 0.25f, new Color(7.0f, 0.42f, 0.6f)); // #E9172D (PM b26 QA: brighter)
        internal static Material CrystalDark => M("CrystalDark", null, new Color(0.52f, 0.03f, 0.13f), 0f, 0.3f, new Color(0.75f, 0.02f, 0.08f)); // #840822
        internal static Material CrystalCore => M("CrystalCore", null, new Color(0.98f, 0.66f, 0.62f), 0f, 0.3f, new Color(3.2f, 1.6f, 1.0f)); // #F9A99D
        internal static Material Fire => M("Fire", null, new Color(1f, 0.45f, 0.08f), 0f, 1f, new Color(4.2f, 1.6f, 0.25f));
        internal static Material FireCore => M("FireCore", null, new Color(1f, 0.85f, 0.35f), 0f, 1f, new Color(5.0f, 3.8f, 1.2f));
        internal static Material WindowGlow => M("WindowGlow", null, new Color(1f, 0.63f, 0.25f), 0f, 1f, new Color(2.6f, 1.1f, 0.25f)); // #FFA040
        internal static Material BarrelWood => M("BarrelWood", "T_TW_Deck", new Color(1.45f, 1.12f, 0.90f));   // #AE5E30
        internal static Material Cloth => M("ClothDark", null, new Color(0.30f, 0.07f, 0.09f), 0f, 0.9f);
        internal static Material Leather => M("Leather", null, new Color(0.22f, 0.13f, 0.10f), 0f, 0.85f);
        internal static Material Hood => M("Hood", null, new Color(0.46f, 0.08f, 0.13f), 0f, 0.85f);
        internal static Material Rope => M("Rope", "T_K_Bark", new Color(1.3f, 1.05f, 0.85f));

        static Dictionary<string, DestructibleBlock> _by;
        static Mesh[] _stones, _rocks;

        static void LoadKit()
        {
            if (_stones != null) return;
            var kit = CvMesh.Load("Kingdom/Meshes/Tide_Kit"); var st = new List<Mesh>(); var rk = new List<Mesh>();
            for (int i = 0; i < 8; i++) { if (kit.TryGetValue("Stone_" + i, out var a)) st.Add(a); if (kit.TryGetValue("Rock_" + i, out var r)) rk.Add(r); }
            _stones = st.ToArray(); _rocks = rk.ToArray();
            Debug.Log("[Tide] kit stones " + _stones.Length + ", rocks " + _rocks.Length);
        }

        /// <summary>A dressed block: a Blender bevelled/chipped stone from the kit (tiny seeded tilt), or the procedural slab without it.</summary>
        static void StoneK(TwMesh t, Material m, float tile, Vector3 c, Vector3 size, int seed, float jitter = 0.05f, Quaternion rot = default)
        {
            if (_stones == null || _stones.Length == 0) { t.Slab(m, tile, c, size, seed, jitter, rot); return; }
            if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f) rot = Quaternion.identity;
            var tilt = Quaternion.Euler((Hash(seed, 1) - 0.5f) * 3f, (Hash(seed, 2) - 0.5f) * 3f, (Hash(seed, 3) - 0.5f) * 3f);
            t.Kit(m, tile, _stones[(seed & 0x7fffffff) % _stones.Length], c, size, rot * tilt);
        }

        /// <summary>A boulder: a Blender faceted rock from the kit, or the procedural jittered slab without it.</summary>
        static void RockK(TwMesh t, Material m, float tile, Vector3 c, Vector3 size, int seed, float jitter = 0.16f, Quaternion rot = default)
        {
            if (_rocks == null || _rocks.Length == 0) { t.Slab(m, tile, c, size, seed, jitter, rot); return; }
            t.Kit(m, tile, _rocks[(seed & 0x7fffffff) % _rocks.Length], c, size * 1.08f, rot);
        }
        static Dictionary<string, Mesh> _roof;
        /// <summary>Blender pagoda roofs (Tools/Blender/tide/build_roof.py -> Kingdom/Meshes/Tide_Roof): appends part_Tile/_Gold/_Timber
        /// in spec coordinates; false when the bundle is missing (procedural HipRoof fallback).</summary>
        static bool HeroRoof(TwMesh t, string part)
        {
            if (_roof == null) { _roof = CvMesh.Load("Kingdom/Meshes/Tide_Roof"); Debug.Log("[Tide] hero roof " + (_roof.ContainsKey("RoofL_Tile") ? "loaded" : "missing")); }
            if (!_roof.TryGetValue(part + "_Tile", out var tile)) return false;
            t.Kit(RoofTile, 1.2f, tile, Vector3.zero, Vector3.one);
            if (_roof.TryGetValue(part + "_Gold", out var g)) t.Kit(Gold, 1f, g, Vector3.zero, Vector3.one);
            if (_roof.TryGetValue(part + "_Timber", out var w)) t.Kit(Timber, 1f, w, Vector3.zero, Vector3.one);
            return true;
        }
        static Vector3 _off; // actual base - spec base

        /// <summary>Dresses every block of the Tide Watchtower layout. Returns the number of blocks dressed.</summary>
        public static int Apply(IReadOnlyList<DestructibleBlock> blocks)
        {
            if (!Matches(blocks)) return 0;
            LoadKit();
            if (TwMesh.ChamferMats == null) TwMesh.ChamferMats = new HashSet<Material>();
            TwMesh.ChamferMats.Add(Timber); TwMesh.ChamferMats.Add(TimberLit); TwMesh.ChamferMats.Add(Plank);
            _by = new Dictionary<string, DestructibleBlock>();
            foreach (var b in blocks) if (b != null && !string.IsNullOrEmpty(b.blockId)) _by[b.blockId] = b;
            var rc = _by["rockC"]; _off = rc.transform.position - new Vector3(-3.5f, (-1.6f + StretchY(0.8f)) * 0.5f, -0.35f); // rockC spans -1.6..P
            int n = 0;
            foreach (var b in blocks)
            {
                if (b == null) continue;
                var t = new TwMesh(); var c = b.transform.position; var s = b.transform.localScale;
                t.M = Matrix4x4.Translate(_off - c);
                t.WarpFn = StretchY; t.WarpSlope = StretchSlope; t.WarpSpecOffset = c - _off;
                bool ok = true;
                switch (b.blockId)
                {
                    case "rockC": RockC(t); break;
                    case "rockL": RockL(t); break;
                    case "rockR": RockR(t); break;
                    case "crystal": CrystalCluster(t, b); break;
                    case "t1L": Tier1(t, 0.83f, 2.2f, true); break;
                    case "t1R": Tier1(t, -3.53f, -1.49f, true); break;
                    case "t1B": Tier1Back(t); break;
                    case "lintel": Lintel(t, b); break;
                    case "t2FL": Tier2(t, -4.75f, -3.2f, -0.6f, 1.95f, true); break;
                    case "t2FR": Tier2(t, -4.75f, -3.2f, -3.15f, -0.6f, true); break;
                    case "t2BL": Tier2(t, -3.2f, -1.65f, -0.6f, 1.95f, false); break;
                    case "t2BR": Tier2(t, -3.2f, -1.65f, -3.15f, -0.6f, false); break;
                    case "galleryL": Gallery(t, -0.57f, 1.93f, b, true); break;
                    case "galleryR": Gallery(t, -3.07f, -0.57f, b, false); break;
                    case "roomL": Room(t, -0.79f, 1.28f, b); break;
                    case "roomR": Room(t, -2.86f, -0.79f, b); break;
                    case "roofL": MainRoof(t, -0.75f, 1.69f, b); break;
                    case "roofR": MainRoof(t, -3.19f, -0.75f, b); break;
                    case "turret": Turret(t, b); break;
                    case "brazierL": Brazier(t, new Vector3(-5.5f, 0.8f, 1.48f)); break;
                    case "brazierR": Brazier(t, new Vector3(-5.5f, 0.8f, -2.17f)); break;
                    case "fortBarrel": FortBarrel(t); break;
                    case "Llow": LeftLow(t); break;
                    case "Ltop": LeftTop(t, b); break;
                    case "Rlow": RightLow(t); break;
                    case "Rtop": RightTop(t); break;
                    case "Rscaffold": Scaffold(t); break;
                    default:
                        if (b.blockId.StartsWith("Lleg") || b.blockId.StartsWith("Rleg")) Leg(t, b, c - _off, s);
                        else ok = false;
                        break;
                }
                if (!ok || t.Empty) continue;
                var root = Metric(b, "Tide_" + b.blockId);
                t.Build(root, "Tw_" + b.blockId, true);
                // the dressing goes with the piece: hidden when it breaks into chunks or leaves the island (PM b22: the tower still stood at +15 s)
                var go = root.gameObject;
                b.Broken += _ => { if (go != null) go.SetActive(false); };
                b.WentOutOfBounds += _ => { if (go != null) go.SetActive(false); };
                var own = b.GetComponent<MeshRenderer>(); if (own != null) own.enabled = false;
                // every other look on the block (greybox, RealKit modules, kingdom visuals) is replaced by the Tide dressing
                foreach (var vr in b.GetComponentsInChildren<Renderer>(true)) if (!vr.transform.IsChildOf(root) && !(vr is ParticleSystemRenderer)) vr.enabled = false;
                if (b.visual != null && !b.visual.transform.IsChildOf(b.transform)) foreach (var vr in b.visual.GetComponentsInChildren<Renderer>(true)) if (!vr.transform.IsChildOf(root)) vr.enabled = false;
                n++;
            }
            Lights();
            // PM b26 QA (b27): the roof halves and turret hung in the air after the smash (each keeps standing while ANY support
            // survives). When the crystal core blows, the whole watchtower comes down storey by storey, like the Tidewarden defeat.
            if (_by.TryGetValue("crystal", out var core)) core.Broken += _ => CollapseTower();
            return n;
        }

        static void CollapseTower()
        {
            if (_by == null) return;
            var list = new List<DestructibleBlock>();
            foreach (var kv in _by)
            {
                var x = kv.Value; if (x == null || x.IsBroken || x.IsCollapsed || kv.Key.StartsWith("rock") || kv.Key == "crystal") continue;
                list.Add(x);
            }
            list.Sort((p, q) => { int c = p.transform.position.y.CompareTo(q.transform.position.y); return c != 0 ? c : string.CompareOrdinal(p.blockId, q.blockId); });
            int step = 0; var si = Crashverse.Destruction.StructuralIntegrity.Current;
            foreach (var x in list) if (si == null || !si.ScheduleCollapse(x, 2 * step++)) x.ForceCollapse();
        }

        static Transform Metric(DestructibleBlock b, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(b.transform, false);
            var s = b.transform.localScale; t.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
            return t;
        }

        static void Lights()
        {
            // the crystal's red glow on the arch interior and a warm fire light in the gallery room (spec §4)
            if (_by.TryGetValue("crystal", out var cr)) PointLight(cr.transform, "CrystalGlow", new Color(1f, 0.16f, 0.12f), 7f, 8.5f, new Vector3(-4.2f, 1.9f, -0.3f));
            if (_by.TryGetValue("roomL", out var rm)) PointLight(rm.transform, "GalleryFire", new Color(1f, 0.6f, 0.25f), 2.4f, 5f, new Vector3(-4.6f, 7.3f, -0.8f));
        }

        static void PointLight(Transform parent, string name, Color c, float intensity, float range, Vector3 specWorld)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, true); go.transform.position = new Vector3(specWorld.x, StretchY(specWorld.y), specWorld.z) + _off;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            var owner = parent.GetComponent<DestructibleBlock>();
            if (owner != null) owner.Broken += _ => { if (l != null) l.enabled = false; };
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static float Hash(int a, int b) { uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h % 10000u) / 10000f; }

        // ---------------- flames, figures and small props (shared with the environment) ----------------
        internal static void Flame(TwMesh t, Vector3 at, float h, float r)
        {
            t.Cyl(Fire, 1f, at, at + Vector3.up * h, r, 0f, 7, true);
            t.Cyl(Fire, 1f, at + V(r * 0.35f, 0f, r * 0.3f), at + V(r * 0.5f, h * 0.7f, r * 0.45f), r * 0.6f, 0f, 6, true);
            t.Cyl(Fire, 1f, at + V(-r * 0.3f, 0f, -r * 0.35f), at + V(-r * 0.45f, h * 0.75f, -r * 0.5f), r * 0.6f, 0f, 6, true);
            t.Cyl(FireCore, 1f, at + V(-r * 0.05f, 0f, 0f), at + V(-r * 0.1f, h * 0.6f, 0f), r * 0.55f, 0f, 6, true);
        }

        internal static void IronBowl(TwMesh t, Vector3 at, float r0, float r1, float h)
        {
            bool own = !t.InProp; if (own) t.PropBegin(at, 1f);
            t.Cyl(Iron, 1f, at, at + Vector3.up * h, r0, r1, 10, true);
            t.Cyl(Gold, 1f, at + Vector3.up * (h - 0.04f), at + Vector3.up * (h + 0.02f), r1 + 0.02f, r1 + 0.02f, 10, true);
            if (own) t.PropEnd();
        }

        internal static void Torch(TwMesh t, Vector3 baseAt, float pole)
        {
            t.Cyl(Timber, 0.6f, baseAt, baseAt + Vector3.up * pole, 0.045f, 0.05f, 6);
            IronBowl(t, baseAt + Vector3.up * pole, 0.06f, 0.11f, 0.12f);
            Flame(t, baseAt + Vector3.up * (pole + 0.08f), 0.38f, 0.11f);
        }

        /// <summary>Hooded, horned archer drawing a bow toward <paramref name="face"/> (spec §5; goal: arrows point screen-right).</summary>
        internal static void Archer(TwMesh t, Vector3 feet, Vector3 face, float scale = 1f)
        {
            face.y = 0f; face.Normalize(); var side = Vector3.Cross(Vector3.up, face).normalized; // side = the archer's right
            Vector3 P(float f, float up, float s) => feet + (face * f + Vector3.up * up + side * s) * scale;
            // legs + boots, belt, torso, cape
            foreach (float s in new[] { -0.13f, 0.13f })
            {
                t.Cyl(Leather, 1f, P(0f, 0f, s * 1.1f), P(0f, 0.12f, s * 1.1f), 0.09f * scale, 0.085f * scale, 7);
                t.Cyl(Cloth, 1f, P(0f, 0.1f, s), P(0f, 0.82f, s * 0.8f), 0.075f * scale, 0.1f * scale, 7);
            }
            t.Cyl(Hood, 1f, P(0f, 0.78f, 0f), P(0f, 1.36f, 0f), 0.2f * scale, 0.25f * scale, 9);
            t.Cyl(Gold, 1f, P(0f, 0.8f, 0f), P(0f, 0.88f, 0f), 0.21f * scale, 0.21f * scale, 9);
            t.Poly2(Cloth, 1f, P(-0.2f, 1.35f, -0.24f), P(-0.2f, 1.35f, 0.24f), P(-0.32f, 0.45f, 0.3f), P(-0.32f, 0.45f, -0.3f));
            // shoulder plates + hood + glowing eyes + horns
            t.Sphere(Iron, 1f, P(0f, 1.34f, -0.24f), Vector3.one * 0.1f * scale, 7, 4);
            t.Sphere(Iron, 1f, P(0f, 1.34f, 0.24f), Vector3.one * 0.1f * scale, 7, 4);
            t.Sphere(Hood, 1f, P(0f, 1.55f, 0f), new Vector3(0.18f, 0.2f, 0.18f) * scale, 9, 6);
            t.Sphere(Socket, 1f, P(0.1f, 1.53f, 0f), new Vector3(0.09f, 0.11f, 0.12f) * scale, 7, 4);
            t.Sphere(EyeGlow, 1f, P(0.17f, 1.55f, -0.045f), Vector3.one * 0.025f * scale, 5, 3);
            t.Sphere(EyeGlow, 1f, P(0.17f, 1.55f, 0.045f), Vector3.one * 0.025f * scale, 5, 3);
            foreach (float s in new[] { -1f, 1f })
            {
                var a = P(-0.02f, 1.66f, 0.1f * s); var b = P(-0.05f, 1.76f, 0.2f * s); var c = P(-0.08f, 1.88f, 0.22f * s);
                t.Cyl(Bone, 1f, a, b, 0.045f * scale, 0.03f * scale, 6); t.Cyl(Bone, 1f, b, c, 0.03f * scale, 0.008f * scale, 6, true);
            }
            // bow arm straight toward the target, draw hand at the cheek, bow (7 segments) curved toward the target, string + arrow
            var shoulderL = P(0f, 1.36f, -0.22f); var hand = P(0.62f, 1.4f, -0.08f); var drawHand = P(0.08f, 1.47f, 0.05f);
            t.Cyl(Hood, 1f, shoulderL, hand, 0.06f * scale, 0.05f * scale, 6);
            t.Cyl(Hood, 1f, P(0f, 1.36f, 0.22f), drawHand, 0.06f * scale, 0.05f * scale, 6);
            Vector3 prev = default; var top = Vector3.zero; var bot = Vector3.zero;
            for (int i = 0; i <= 7; i++)
            {
                float a = Mathf.Lerp(-1.05f, 1.05f, i / 7f); // radians around the grip
                var p = hand + (Vector3.up * Mathf.Sin(a) * 0.62f - face * (1f - Mathf.Cos(a)) * 0.42f) * scale;
                if (i > 0) t.Cyl(Gold, 1f, prev, p, 0.022f * scale, 0.022f * scale, 5, false);
                if (i == 0) bot = p; if (i == 7) top = p; prev = p;
            }
            t.Cyl(Bone, 1f, top, drawHand, 0.006f * scale, 0.006f * scale, 3, false);
            t.Cyl(Bone, 1f, bot, drawHand, 0.006f * scale, 0.006f * scale, 3, false);
            var tip = drawHand + face * 0.95f * scale;
            t.Cyl(Timber, 1f, drawHand - face * 0.05f, tip, 0.012f * scale, 0.012f * scale, 4, false);
            t.Cyl(Iron, 1f, tip, tip + face * 0.09f * scale, 0.03f * scale, 0f, 4, true);
        }

        /// <summary>A dark wooden skull banner (L_banner) or the big central banner: rod, cloth with a pointed frayed tip, skull emblem.</summary>
        internal static void SkullBanner(TwMesh t, float x, float zA, float zB, float yTop, float yTip, float yShoulder, bool fringe)
        {
            float zc = (zA + zB) * 0.5f, w = Mathf.Abs(zB - zA);
            t.Cyl(Gold, 1f, V(x - 0.04f, yTop + 0.05f, zA - 0.1f), V(x - 0.04f, yTop + 0.05f, zB + 0.1f), 0.035f, 0.035f, 6);
            // cloth faces -x (toward the camera): points listed so Cross(q1-q0, q2-q0) points to -x
            t.Poly2(Banner, 1f, V(x, yTop, zA), V(x, yShoulder, zA), V(x, yTip, zc), V(x, yShoulder, zB), V(x, yTop, zB));
            // darker vertical folds
            for (int i = 1; i <= 3; i++)
            {
                float z = Mathf.Lerp(zA, zB, i / 4f); float hw = w * 0.035f;
                float yBot = Mathf.Lerp(yShoulder, yTip, 1f - Mathf.Abs(z - zc) / (w * 0.5f)) + 0.05f;
                t.Poly(BannerFold, 1f, V(x - 0.006f, yTop - 0.05f, z - hw), V(x - 0.006f, yBot, z - hw), V(x - 0.006f, yBot, z + hw), V(x - 0.006f, yTop - 0.05f, z + hw));
            }
            if (fringe)
                for (int i = 0; i < 10; i++)
                {
                    float f0 = i / 10f, f1 = (i + 0.6f) / 10f;
                    Vector3 E(float f) => f < 0.5f ? Vector3.Lerp(V(x - 0.008f, yShoulder, zA), V(x - 0.008f, yTip, zc), f * 2f) : Vector3.Lerp(V(x - 0.008f, yTip, zc), V(x - 0.008f, yShoulder, zB), f * 2f - 1f);
                    var a = E(f0); var b = E(f1);
                    t.Poly2(Gold, 1f, a, a + Vector3.down * 0.12f, b + Vector3.down * 0.12f, b);
                }
            // pale horned skull emblem
            float ey = Mathf.Lerp(yTop, yShoulder, 0.45f), es = w * 0.22f, ex = x - 0.012f;
            var oct = new Vector3[8];
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI * 2f / 8f; oct[i] = V(ex, ey + Mathf.Sin(a) * es, zc + Mathf.Cos(a) * es * 0.95f); }
            t.Poly2(Emblem, 1f, oct);
            t.Poly2(Emblem, 1f, V(ex, ey - es * 0.6f, zc + es * 0.55f), V(ex, ey - es * 1.45f, zc + es * 0.4f), V(ex, ey - es * 1.45f, zc - es * 0.4f), V(ex, ey - es * 0.6f, zc - es * 0.55f));
            foreach (float s in new[] { -1f, 1f })
            {
                t.Poly2(Emblem, 1f, V(ex, ey + es * 0.5f, zc + s * es * 0.6f), V(ex, ey + es * 0.95f, zc + s * es * 1.6f), V(ex, ey + es * 1.55f, zc + s * es * 1.35f), V(ex, ey + es * 0.85f, zc + s * es * 0.9f));
                t.Poly2(Socket, 1f, V(ex - 0.004f, ey - es * 0.05f, zc + s * es * 0.2f), V(ex - 0.004f, ey - es * 0.4f, zc + s * es * 0.25f), V(ex - 0.004f, ey - es * 0.35f, zc + s * es * 0.62f), V(ex - 0.004f, ey + es * 0.05f, zc + s * es * 0.58f));
            }
        }

        // ---------------- island foundation ----------------
        static void Boulders(TwMesh t, int seed, Vector3 a, Vector3 b, int n, float s0, float s1, float yTop0, float yTop1, float waterY = -1f)
        {
            for (int i = 0; i < n; i++)
            {
                float f = (i + 0.5f) / n + (Hash(seed, i) - 0.5f) * 0.6f / n;
                var p = Vector3.Lerp(a, b, f);
                float sz = Mathf.Lerp(s0, s1, Hash(seed + 7, i)); float top = Mathf.Lerp(yTop0, yTop1, Hash(seed + 13, i));
                float h = top - (waterY - 0.6f);
                var rot = Quaternion.Euler((Hash(seed, i * 3) - 0.5f) * 26f, Hash(seed + 3, i) * 90f, (Hash(seed + 5, i) - 0.5f) * 26f);
                RockK(t, Hash(seed + 11, i) < 0.45f ? RockDark : Rock, 1.4f, V(p.x, (top + waterY - 0.6f) * 0.5f, p.z), V(sz, h, sz * (0.75f + 0.4f * Hash(seed + 17, i))), seed * 31 + i, 0.16f, rot);
                // sun-lit flat top slab + moss tuft
                StoneK(t, RockTop, 1.2f, V(p.x, top + 0.02f, p.z), V(sz * 0.7f, 0.14f, sz * 0.6f), seed * 57 + i, 0.2f, rot);
                if (Hash(seed + 19, i) < 0.55f) t.Slab(Moss, 0.8f, V(p.x + (Hash(seed, i + 40) - 0.5f) * sz * 0.4f, top + 0.09f, p.z + (Hash(seed, i + 50) - 0.5f) * sz * 0.4f), V(sz * 0.35f, 0.09f, sz * 0.3f), seed * 91 + i, 0.3f, rot);
                // foam ring where the rock meets the sea
                if (Hash(seed + 23, i) < 0.8f)
                    t.Slab(Foam, 1f, V(p.x - sz * 0.35f, waterY + 0.03f, p.z), V(sz * 0.55f, 0.06f, sz * 1.1f), seed * 101 + i, 0.35f);
            }
        }

        static void FernTuft(TwMesh t, Vector3 at, float size, int seed)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + Hash(seed, i); var d = V(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tip = at + d * size + Vector3.up * size * (0.5f + 0.4f * Hash(seed, i + 9)); var side = Vector3.Cross(Vector3.up, d) * size * 0.12f;
                t.Poly2(Fern, 1f, at - side, tip, at + side);
            }
        }

        static void RockC(TwMesh t)
        {
            // PM b26 QA (b27): the tower stands on a stepped stone plinth (top P = StretchY(0.8)) so the arch base sits well above the
            // islet; plinth, steps and boulders are drawn in world heights (identity prop: anchor below the warp pivot).
            float P = StretchP;
            Prop(t, Vector3.zero, 1f, () =>
            {
                t.BoxMinMax(Rock, 1.4f, V(-6.3f, -1.4f, -3.7f), V(-0.6f, 0.72f, 3.0f));
                // masonry plinth faces (front and both sides), coursed blocks with moss, cap stones on the rim
                const float ch = 0.42f; int rows = Mathf.CeilToInt((P - 0.2f) / ch);
                for (int r = 0; r < rows; r++)
                {
                    float y0 = 0.2f + r * ch, y1 = Mathf.Min(P - 0.08f, y0 + ch); if (y1 - y0 < 0.1f) continue;
                    float off = (r % 2) * 0.35f;
                    for (float z = -3.7f - off; z < 3.0f; z += 0.7f)
                    {
                        float z0 = Mathf.Max(-3.7f, z), z1 = Mathf.Min(3.0f, z + 0.7f); if (z1 - z0 < 0.12f) continue;
                        if (z1 > -1.75f && z0 < 1.05f) continue; // the steps run up the middle
                        int k = r * 37 + (int)((z + 4f) * 10f);
                        StoneK(t, Hash(k, 3) < 0.3f ? StoneDark : Hash(k, 4) < 0.5f ? StoneLight : Stone, 1f, V(-6.22f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), V(0.3f, y1 - y0 - 0.03f, z1 - z0 - 0.04f), 600 + k, 0.03f);
                        if (Hash(k, 5) < 0.26f) t.Slab(Moss, 0.8f, V(-6.38f, y0 + 0.05f, (z0 + z1) * 0.5f), V(0.06f, 0.14f, (z1 - z0) * 0.8f), 650 + k, 0.2f);
                    }
                    foreach (float zs in new[] { 2.92f, -3.62f })
                        for (float x = -6.2f - off; x < -0.6f; x += 0.75f)
                        {
                            float x0 = Mathf.Max(-6.3f, x), x1 = Mathf.Min(-0.6f, x + 0.75f); if (x1 - x0 < 0.12f) continue;
                            int k = r * 41 + (int)((x + 7f) * 10f) + (zs > 0f ? 0 : 500);
                            StoneK(t, Hash(k, 6) < 0.3f ? StoneDark : Stone, 1f, V((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, zs), V(x1 - x0 - 0.04f, y1 - y0 - 0.03f, 0.3f), 700 + k, 0.03f);
                        }
                }
                for (float z = -3.6f; z < 2.9f; z += 0.55f)
                    if (!(z + 0.55f > -1.75f && z < 1.05f))
                        StoneK(t, StoneLight, 1f, V(-6.2f, P - 0.04f, z + 0.27f), V(0.42f, 0.12f, 0.5f), 760 + (int)((z + 4f) * 10f), 0.03f);
                // paving on the plinth top in front of the arch
                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 4; j++)
                    {
                        float z0 = -1.6f + i * 0.4f, x0 = -6.0f + j * 0.42f;
                        StoneK(t, j % 2 == 0 ? StoneLight : Stone, 1f, V(x0 + 0.2f, P - 0.04f, z0 + 0.2f), V(0.38f, 0.1f, 0.37f), i * 7 + j, 0.05f);
                    }
                // stone steps from the plinth top down to the water under the arch centre
                const int steps = 10; float rise = (P + 0.55f) / steps;
                for (int i = 0; i < steps; i++)
                {
                    float top = P - (i + 1) * rise + rise, x1 = -6.3f - i * 0.3f;
                    StoneK(t, i % 2 == 0 ? StoneLight : Stone, 1f, V(x1 + 0.0f, (top - 1.4f) * 0.5f, -0.33f), V(0.34f, top + 1.4f, 2.7f), 300 + i, 0.03f);
                }
                // boulders flanking the steps (tall, to the plinth rim), the plinth sides, and ferns in the cracks
                Boulders(t, 1, V(-7.0f, 0f, 3.0f), V(-6.9f, 0f, 1.35f), 3, 1.1f, 1.6f, P - 0.9f, P + 0.2f);
                Boulders(t, 2, V(-6.9f, 0f, -2.0f), V(-6.8f, 0f, -3.7f), 3, 1.1f, 1.6f, P - 0.9f, P + 0.1f);
                Boulders(t, 3, V(-5.6f, 0f, 3.15f), V(-1.2f, 0f, 3.15f), 3, 1.0f, 1.5f, 0.3f, 1.3f);
                Boulders(t, 4, V(-5.6f, 0f, -3.85f), V(-1.2f, 0f, -3.85f), 3, 1.0f, 1.5f, 0.3f, 1.3f);
                FernTuft(t, V(-6.75f, P + 0.05f, 1.55f), 0.6f, 1); FernTuft(t, V(-6.6f, P - 0.1f, -2.1f), 0.55f, 2); FernTuft(t, V(-6.4f, 1.1f, 2.6f), 0.45f, 3);
            });
        }

        static void RockL(TwMesh t)
        {
            t.PropBegin(Vector3.zero, 1f); // world heights: the boulders are not part of the stretched tower
            t.BoxMinMax(Rock, 1.4f, V(-5.8f, -1.4f, 3.2f), V(-2.2f, -0.05f, 7.1f));
            Boulders(t, 11, V(-6.3f, 0f, 3.3f), V(-6.2f, 0f, 7.6f), 4, 1.1f, 1.7f, 0.2f, 1.85f);
            Boulders(t, 12, V(-4.6f, 0f, 7.6f), V(-2.4f, 0f, 7.4f), 2, 1.0f, 1.4f, 0.4f, 1.2f);
            Boulders(t, 13, V(-5.0f, 0f, 4.3f), V(-3.0f, 0f, 6.3f), 2, 0.8f, 1.1f, 0.05f, 0.35f);
            FernTuft(t, V(-6.0f, 1.2f, 6.8f), 0.5f, 4); FernTuft(t, V(-6.2f, 0.7f, 3.8f), 0.45f, 5);
            t.PropEnd();
        }

        static void RockR(TwMesh t)
        {
            t.PropBegin(Vector3.zero, 1f);
            t.BoxMinMax(Rock, 1.4f, V(-5.4f, -1.4f, -7.1f), V(-1.8f, -0.2f, -3.9f));
            Boulders(t, 21, V(-5.9f, 0f, -4.0f), V(-5.8f, 0f, -7.8f), 4, 1.0f, 1.5f, 0.1f, 1.35f);
            Boulders(t, 22, V(-4.4f, 0f, -7.8f), V(-2.0f, 0f, -7.6f), 2, 1.0f, 1.3f, 0.2f, 0.9f);
            FernTuft(t, V(-5.7f, 1.1f, -6.6f), 0.45f, 6); FernTuft(t, V(-5.9f, 0.9f, -4.6f), 0.5f, 7);
            t.PropEnd();
        }

        // ---------------- stone tower ----------------
        const float CourseH = 0.32f, Course0 = 0.8f;   // b26: 0.45 m courses after the 1.4x stretch
        const float ArchZc = -0.33f, ArchHw = 1.16f, ArchTop = 3.5f, ArchRin = 1.16f, ArchRout = 1.58f, JambW = 0.66f;
        static float ArchSpring => ArchTop - ArchRin;
        static float TaperL(float y) => 2.2f + (1.45f - 2.2f) * (y - 0.8f) / 5.4f;   // battered walls: 5.7 m at y 0.8 to 4.2 m at y 6.2
        static float TaperR(float y) => -3.53f + (-2.72f + 3.53f) * (y - 0.8f) / 5.4f;

        /// <summary>Half-width of the arch outline (opening + jambs + voussoir ring) at height y; 0 above it.</summary>
        static float ArchOutline(float y)
        {
            if (y < Course0) return 0f;
            if (y <= ArchSpring) return ArchHw + JambW;
            float d = y - ArchSpring; return d >= ArchRout ? 0f : Mathf.Sqrt(ArchRout * ArchRout - d * d);
        }

        /// <summary>Coursed, stagger-bonded masonry on the cannon-facing plane x = xf between y0..y1 and zMin..zMax (spec §4: ~12
        /// courses of 0.45 m, stones 0.6-0.9 m, mortar #402C25), following the battered taper and skipping the arch outline.</summary>
        static void Masonry(TwMesh t, float xf, float y0, float y1, float zMin, float zMax, bool arch, float depth)
        {
            for (int k = 0; k < 20; k++)
            {
                float cb = Course0 + k * CourseH, ct = cb + CourseH;
                float a = Mathf.Max(cb, y0), b = Mathf.Min(ct, y1); if (b - a < 0.05f) continue;
                float ym = (a + b) * 0.5f;
                float zl = Mathf.Min(zMax, TaperL(ym)), zr = Mathf.Max(zMin, TaperR(ym)); if (zl - zr < 0.05f) continue;
                float ex0 = 99f, ex1 = -99f;
                if (arch) { float hw = Mathf.Max(ArchOutline(a), ArchOutline(b)); if (hw > 0f) { ex0 = ArchZc - hw; ex1 = ArchZc + hw; } }
                // core (stone) + mortar backing for this course
                void Core(float z0, float z1)
                {
                    if (z1 - z0 < 0.02f) return;
                    t.BoxMinMax(Stone, 1.2f, V(xf + 0.02f, a, z0), V(xf + depth, b, z1));
                    t.BoxMinMax(Mortar, 1f, V(xf - 0.005f, a, z0), V(xf + 0.03f, b, z1));
                }
                // the core only leaves out the opening itself (the jambs and voussoirs sit in front of it)
                float oh = 0f;
                if (arch) { float yy = a; oh = yy <= ArchSpring ? ArchHw : (yy - ArchSpring < ArchRin ? Mathf.Sqrt(ArchRin * ArchRin - (yy - ArchSpring) * (yy - ArchSpring)) : 0f); }
                if (oh <= 0f) Core(zr, zl); else { Core(zr, Mathf.Min(zl, ArchZc - oh)); Core(Mathf.Max(zr, ArchZc + oh), zl); }
                // stones on a global z grid (so courses line up across blocks), staggered per course
                float z = -4.2f + (k % 2 == 0 ? 0f : 0.37f); int i = 0;
                while (z < 3f)
                {
                    float len = 0.6f + 0.3f * Hash(k, i); float s0 = z, s1 = z + len; z = s1; i++;
                    float c0 = Mathf.Max(s0, zr), c1 = Mathf.Min(s1, zl);
                    if (ex1 > ex0) { if (c0 < ex1 && c1 > ex0) { if (c0 < ex0) c1 = Mathf.Min(c1, ex0); else if (c1 > ex1) c0 = Mathf.Max(c0, ex1); else continue; } }
                    if (c1 - c0 < 0.08f) continue;
                    float hgt = (b - a) - 0.06f, out1 = 0.05f + 0.05f * Hash(k + 31, i);
                    var mat = Hash(k + 7, i) < 0.22f ? StoneDark : Hash(k + 9, i) < 0.35f ? StoneLight : Stone;
                    StoneK(t, mat, 0.9f, V(xf - out1 * 0.5f + 0.02f, (a + b) * 0.5f, (c0 + c1) * 0.5f), V(out1 + 0.06f, hgt, c1 - c0 - 0.05f), k * 97 + i, 0.06f);
                    // moss on a few stone tops and a green streak below some of them
                    if (Hash(k + 41, i) < 0.26f) t.Slab(Moss, 0.6f, V(xf - out1 - 0.02f, b - 0.04f, (c0 + c1) * 0.5f), V(0.08f, 0.09f, (c1 - c0) * 0.75f), k * 13 + i, 0.25f);
                    if (Hash(k + 43, i) < 0.07f) t.Slab(Moss, 0.6f, V(xf - out1 - 0.015f, (a + b) * 0.5f - 0.05f, (c0 + c1) * 0.5f), V(0.04f, (b - a) * 0.9f, 0.12f), k * 17 + i, 0.25f); // green streak
                }
            }
        }

        static void Tier1(TwMesh t, float zMin, float zMax, bool front)
        {
            Masonry(t, -4.9f, 0.8f, 4.0f, zMin, zMax, true, 3.38f);
            bool left = zMin > ArchZc;
            float s = left ? 1f : -1f, inner = ArchZc + s * ArchHw; // the jamb's inner edge (opening side)
            // opening side wall (dark red) from the face to the back block
            t.BoxMinMax(ArchInside, 1f, V(-4.9f, 0.8f, left ? inner : inner - 0.02f), V(-2.75f, 3.65f, left ? inner + 0.02f : inner));
            // jambs: 4 dressed blocks per side, alternating long / short, protruding 0.12, and a wider impost block
            float y = 0.8f; float jh = (ArchSpring - 0.8f - 0.26f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                float w = i % 2 == 0 ? JambW : JambW * 0.7f;
                float z0 = left ? inner : inner - w, z1 = left ? inner + w : inner;
                StoneK(t, i % 2 == 0 ? Dressed : StoneLight, 0.9f, V(-4.96f, y + jh * 0.5f, (z0 + z1) * 0.5f), V(0.16f, jh - 0.05f, w - 0.04f), 400 + i + (left ? 10 : 0), 0.04f);
                y += jh;
            }
            float iw = JambW + 0.28f;
            StoneK(t, Dressed, 0.9f, V(-5.0f, y + 0.13f, inner + s * (iw * 0.5f - 0.05f)), V(0.24f, 0.26f, iw), 420 + (left ? 1 : 0), 0.04f);
            // voussoirs on this side of the crown (9 in all, keystone on the lintel)
            for (int i = 0; i < 9; i++)
            {
                if (i == 4) continue;
                if (left != (i > 4)) continue;
                Voussoir(t, i);
            }
        }

        static void Voussoir(TwMesh t, int i)
        {
            float a0 = Mathf.PI * i / 9f + 0.012f, a1 = Mathf.PI * (i + 1) / 9f - 0.012f; // 0 = right (-z) springer, pi = left
            float rout = i == 4 ? ArchRout + 0.2f : ArchRout;
            Vector3 Pt(float a, float r, float x) => V(x, ArchSpring + Mathf.Sin(a) * r, ArchZc - Mathf.Cos(a) * r);
            float xf = i == 4 ? -5.06f : -5.0f, xb = -4.86f;
            var c = new Vector3[8];
            c[0] = Pt(a0, ArchRin, xf); c[1] = Pt(a0, ArchRin, xb); c[2] = Pt(a0, rout, xf); c[3] = Pt(a0, rout, xb);
            c[4] = Pt(a1, ArchRin, xf); c[5] = Pt(a1, ArchRin, xb); c[6] = Pt(a1, rout, xf); c[7] = Pt(a1, rout, xb);
            // corners: bit0 = x (front/back), bit1 = radius, bit2 = angle; (x, r, angle) is a positively oriented frame, so the
            // faces come out outward exactly as for an axis-aligned box
            t.Hexa(i == 4 ? StoneLight : Dressed, 0.9f, c);
        }

        static void Tier1Back(TwMesh t)
        {
            // the back wall of the arch: dark red stone with glowing cracks (spec §4: #44101E, cracks #8C2630)
            t.BoxMinMax(Stone, 1.2f, V(-2.73f, 0.8f, -1.47f), V(-1.52f, 3.98f, 0.81f));
            t.BoxMinMax(ArchInside, 1f, V(-2.76f, 0.8f, -1.49f), V(-2.72f, 3.65f, 0.83f));
            for (int i = 0; i < 9; i++)
            {
                float z = Mathf.Lerp(-1.3f, 0.65f, Hash(5, i)), y = Mathf.Lerp(1.2f, 3.3f, Hash(6, i)), a = Hash(7, i) * 180f;
                t.Box(ArchCrack, 1f, V(-2.775f, y, z), V(0.01f, 0.03f, 0.25f + 0.25f * Hash(8, i)), Quaternion.Euler(a, 0f, 0f));
            }
            Masonry(t, -2.75f, 3.65f, 4.0f, -1.49f, 0.83f, false, 1.2f);
        }

        static void Lintel(TwMesh t, DestructibleBlock b)
        {
            Masonry(t, -4.9f, 3.65f, 4.0f, -1.49f, 0.83f, true, 2.13f);
            t.BoxMinMax(ArchInside, 1f, V(-4.9f, 3.63f, -1.49f), V(-2.75f, 3.66f, 0.83f));
            Voussoir(t, 4);
            // PM b26 QA (b27): the horned skull sits ON the keystone (jaw on the arch top), x1.25; the banner hangs above its horns
            var m0 = t.M; var piv = V(-5.12f, 4.0f, -0.23f);
            t.M = m0 * Matrix4x4.Translate(V(-0.06f, 0.25f, 0f));
            Prop(t, piv, 1.25f, () => Skull(t)); t.M = m0;
        }

        /// <summary>The bone horned skull over the keystone (spec: 1.75 m wide with the horns, glowing red eyes).</summary>
        static void Skull(TwMesh t)
        {
            float x = -5.12f, zc = -0.23f;
            t.Sphere(Bone, 1f, V(x, 4.02f, zc), V(0.3f, 0.36f, 0.34f), 12, 8);                 // cranium
            t.Sphere(BoneDark, 1f, V(x + 0.05f, 4.1f, zc), V(0.27f, 0.32f, 0.36f), 10, 6);     // darker back of the skull
            t.Box(Bone, 1f, V(x - 0.15f, 3.98f, zc), V(0.12f, 0.08f, 0.5f));                   // brow ridge
            t.Sphere(Socket, 1f, V(x - 0.24f, 3.88f, zc + 0.12f), V(0.07f, 0.08f, 0.08f), 8, 5);
            t.Sphere(Socket, 1f, V(x - 0.24f, 3.88f, zc - 0.12f), V(0.07f, 0.08f, 0.08f), 8, 5);
            t.Sphere(EyeGlow, 1f, V(x - 0.29f, 3.88f, zc + 0.12f), Vector3.one * 0.04f, 6, 4);
            t.Sphere(EyeGlow, 1f, V(x - 0.29f, 3.88f, zc - 0.12f), Vector3.one * 0.04f, 6, 4);
            t.Box(Socket, 1f, V(x - 0.27f, 3.74f, zc), V(0.04f, 0.1f, 0.07f));                 // nose
            t.Box(Bone, 1f, V(x - 0.1f, 3.55f, zc), V(0.3f, 0.2f, 0.42f));                     // jaw
            for (int i = 0; i < 6; i++) t.Box(Bone, 1f, V(x - 0.26f, 3.6f, zc - 0.17f + i * 0.068f), V(0.03f, 0.13f, 0.05f)); // teeth
            foreach (float s in new[] { -1f, 1f })
            {
                // horns: out sideways from the temples, then curving up (tip span z -1.10..0.65)
                Vector3[] p = { V(x, 4.15f, zc + s * 0.26f), V(x - 0.02f, 4.18f, zc + s * 0.48f), V(x - 0.04f, 4.28f, zc + s * 0.68f), V(x - 0.05f, 4.43f, zc + s * 0.82f), V(x - 0.05f, 4.6f, zc + s * 0.87f) };
                float[] r = { 0.12f, 0.1f, 0.075f, 0.05f, 0.012f };
                for (int i = 0; i < p.Length - 1; i++) t.Cyl(i < 2 ? BoneDark : Bone, 1f, p[i], p[i + 1], r[i], r[i + 1], 8, i == p.Length - 2);
            }
        }

        static void Tier2(TwMesh t, float x0, float x1, float z0, float z1, bool front)
        {
            if (front) Masonry(t, x0, 4.0f, 6.2f, z0, z1, false, x1 - x0 - 0.02f);
            else
                for (int k = 0; k < 5; k++)
                {
                    float a = 4.0f + k * 0.44f, b = a + 0.44f, ym = (a + b) * 0.5f;
                    float zl = Mathf.Min(z1, TaperL(ym)), zr = Mathf.Max(z0, TaperR(ym)); if (zl - zr < 0.05f) continue;
                    t.BoxMinMax(Stone, 1.2f, V(x0 + 0.01f, a, zr), V(x1 - 0.01f, b, zl));
                }
            // string course at the tier joint (front only)
            if (front) t.BoxMinMax(StoneLight, 1f, V(x0 - 0.12f, 3.98f, Mathf.Max(z0, TaperR(4f))), V(x0 + 0.1f, 4.14f, Mathf.Min(z1, TaperL(4f))));
        }

        // ---------------- gallery, room, roof, turret ----------------
        static void Gallery(TwMesh t, float z0, float z1, DestructibleBlock b, bool left)
        {
            float x0 = -5.2f, x1 = -1.3f;
            t.BoxMinMax(Timber, 1f, V(x0, 6.2f, z0), V(x0 + 0.32f, 6.8f, z1));                    // thick front beam
            t.BoxMinMax(Timber, 1f, V(x1 - 0.3f, 6.2f, z0), V(x1, 6.8f, z1));
            if (left) t.BoxMinMax(Timber, 1f, V(x0, 6.2f, z1 - 0.3f), V(x1, 6.8f, z1)); else t.BoxMinMax(Timber, 1f, V(x0, 6.2f, z0), V(x1, 6.8f, z0 + 0.3f));
            for (float z = z0 + 0.02f; z < z1 - 0.1f; z += 0.32f) t.BoxMinMax(Hash((int)(z * 31f), 3) < 0.5f ? Plank : TimberLit, 1f, V(x0 + 0.3f, 6.7f, z), V(x1 - 0.3f, 6.8f, Mathf.Min(z1, z + 0.29f)));
            // bolted iron plates on the front beam
            for (float z = (left ? 0.05f : -3.0f); z < z1 - 0.2f; z += 0.71f)
            {
                if (z < z0) continue;
                t.Box(Iron, 1f, V(x0 - 0.015f, 6.5f, z + 0.18f), V(0.03f, 0.34f, 0.28f));
                for (int k = 0; k < 4; k++) t.Sphere(Gold, 1f, V(x0 - 0.04f, 6.5f + ((k & 1) == 0 ? -0.11f : 0.11f), z + 0.18f + ((k & 2) == 0 ? -0.09f : 0.09f)), Vector3.one * 0.03f, 5, 3);
            }
            // 7 corbel brackets under the front edge (stepped), against the stone
            for (int k = 0; k < 7; k++)
            {
                float z = -3.07f + 0.36f + k * 0.71f; if (z < z0 || z >= z1) continue;
                t.BoxMinMax(Timber, 1f, V(-5.18f, 5.98f, z - 0.12f), V(-4.7f, 6.2f, z + 0.12f));
                t.BoxMinMax(Timber, 1f, V(-5.02f, 5.76f, z - 0.11f), V(-4.7f, 5.98f, z + 0.11f));
                t.BoxMinMax(Timber, 1f, V(-4.88f, 5.55f, z - 0.1f), V(-4.7f, 5.76f, z + 0.1f));
            }
            // railing: posts every 0.7 m (0.8 m tall), top and mid rails, on the front and the open side
            for (int k = 0; k < 8; k++)
            {
                float z = -3.02f + k * 0.7f; if (z < z0 - 0.01f || z >= z1) continue;
                t.BoxMinMax(TimberLit, 1f, V(-5.18f, 6.8f, z - 0.05f), V(-5.06f, 7.28f, z + 0.05f));
                t.Box(TimberLit, 1f, V(-5.12f, 7.32f, z), V(0.14f, 0.08f, 0.14f));
            }
            t.BoxMinMax(TimberLit, 1f, V(-5.2f, 7.2f, z0), V(-5.04f, 7.28f, z1));
            t.BoxMinMax(Timber, 1f, V(-5.18f, 7.0f, z0), V(-5.08f, 7.05f, z1));
            // b26: torches at the gallery's front corners and warm lanterns hanging from the eave between the bays
            Torch(t, V(-5.12f, 7.28f, left ? z1 - 0.1f : z0 + 0.1f), 0.12f);
            for (float zl2 = (left ? -0.2f : -2.4f); zl2 < z1 - 0.2f && zl2 > z0 - 0.01f; zl2 += 1.1f)
            {
                t.Cyl(Iron, 1f, V(-4.95f, 7.62f, zl2), V(-4.95f, 7.48f, zl2), 0.012f, 0.012f, 4, false);
                t.Box(WindowGlow, 1f, V(-4.95f, 7.38f, zl2), V(0.14f, 0.2f, 0.14f));
                t.Box(Gold, 1f, V(-4.95f, 7.5f, zl2), V(0.17f, 0.03f, 0.17f));
            }
            float zs = left ? z1 - 0.06f : z0 + 0.06f;
            for (float x = -4.5f; x < -1.4f; x += 0.7f) t.BoxMinMax(TimberLit, 1f, V(x - 0.05f, 6.8f, zs - 0.05f), V(x + 0.05f, 7.28f, zs + 0.05f));
            t.BoxMinMax(TimberLit, 1f, V(-5.2f, 7.2f, zs - 0.08f), V(-1.3f, 7.28f, zs + 0.08f));
            // PM b24: one big red demon banner centred on the tower front, under the gallery, above the skull's horns
            if (left) ChainMast(t); else SkullBanner(t, -4.84f, -1.3f, 0.84f, 6.15f, 5.08f, 5.5f, true);
        }

        static void ChainMast(TwMesh t)
        {
            // the mast with two cross spars at z +1.62 (goal u 40) and chains down to the left tower (spec §4)
            var a = V(-4.0f, 6.8f, 1.62f); var top = V(-4.0f, 9.93f, 1.62f);
            t.Cyl(Timber, 0.8f, a, top, 0.09f, 0.07f, 8);
            t.Cyl(Gold, 1f, top, top + Vector3.up * 0.22f, 0.07f, 0f, 6, true);
            foreach (float y in new[] { 9.1f, 8.4f }) { t.Cyl(Timber, 0.8f, V(-4.0f, y, 1.1f), V(-4.0f, y, 2.15f), 0.05f, 0.05f, 6); t.Cyl(Gold, 1f, V(-4.0f, y, 2.15f), V(-4.0f, y, 2.25f), 0.06f, 0.0f, 6, true); }
            Chain(t, V(-4.0f, 8.4f, 2.12f), V(-5.0f, 5.75f, 2.7f), 0.55f);
            Chain(t, V(-4.0f, 9.1f, 2.12f), V(-4.95f, 6.2f, 3.3f), 0.7f);
        }

        internal static void Chain(TwMesh t, Vector3 a, Vector3 b, float sag)
        {
            int n = 16; Vector3 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float f = i / (float)n; var p = Vector3.Lerp(a, b, f) + Vector3.down * sag * 4f * f * (1f - f);
                var mid = (prev + p) * 0.5f;
                t.Cyl(Iron, 1f, prev, p, 0.025f, 0.025f, 5, false);
                t.Sphere(Iron, 1f, mid, Vector3.one * 0.04f, 5, 3);
                prev = p;
            }
        }

        static void Room(TwMesh t, float z0, float z1, DestructibleBlock b)
        {
            float x0 = -4.65f, x1 = -2.1f;   // b26: bays brought forward (0.5 m of deck in front) so the lit room reads from the low camera
            t.BoxMinMax(Timber, 1f, V(x0 + 0.25f, 6.8f, z0 + 0.02f), V(x1 - 0.02f, 7.7f, z1 - 0.02f));     // walls behind the bays
            t.BoxMinMax(Timber, 1f, V(x0, 7.52f, z0), V(x1, 7.7f, z1));                                // header beam
            t.BoxMinMax(Timber, 1f, V(x0, 6.8f, z0), V(x0 + 0.24f, 6.98f, z1));                        // sill
            // 3 bays across the front (edges z -2.86, -1.48, -0.1, 1.28): posts, glowing fire-lit openings, a flame in each
            float[] edges = { -2.86f, -1.48f, -0.1f, 1.28f };
            foreach (var e in edges) if (e >= z0 - 0.01f && e <= z1 + 0.01f) t.BoxMinMax(TimberLit, 1f, V(x0 - 0.04f, 6.8f, e - 0.1f), V(x0 + 0.18f, 7.7f, e + 0.1f));
            for (int i = 0; i < 3; i++)
            {
                float a = edges[i] + 0.1f, c = edges[i + 1] - 0.1f; if ((a + c) * 0.5f < z0 || (a + c) * 0.5f > z1) continue;
                t.BoxMinMax(WindowGlow, 1f, V(x0 + 0.22f, 6.9f, a), V(x0 + 0.26f, 7.55f, c));
                for (int w = 1; w < 3; w++) t.BoxMinMax(Timber, 1f, V(x0 + 0.17f, 6.9f, Mathf.Lerp(a, c, w / 3f) - 0.025f), V(x0 + 0.21f, 7.55f, Mathf.Lerp(a, c, w / 3f) + 0.025f)); // mullions
                Flame(t, V(x0 + 0.18f, 6.98f, (a + c) * 0.5f), 0.4f, 0.13f);
                t.Box(Gold, 1f, V(x0 - 0.05f, 7.6f, (a + c) * 0.5f), V(0.03f, 0.1f, 0.18f));
            }
            // side walls with one lit window each
            float zs = z1 > 0f ? z1 : z0;
            t.BoxMinMax(TimberLit, 1f, V(x0, 6.8f, zs - 0.05f), V(x1, 7.7f, zs + 0.05f));
            t.BoxMinMax(WindowGlow, 1f, V(-3.7f, 7.0f, zs + (z1 > 0f ? 0.051f : -0.06f)), V(-2.9f, 7.45f, zs + (z1 > 0f ? 0.06f : -0.051f)));
        }

        static List<Vector3[]> _roofPolys;
        /// <summary>Clip a convex polygon to zMin..zMax (Sutherland-Hodgman, two planes).</summary>
        static Vector3[] ClipZ(Vector3[] poly, float zMin, float zMax)
        {
            List<Vector3> Clip(List<Vector3> pin, float zc, bool keepAbove)
            {
                var o = new List<Vector3>();
                for (int i = 0; i < pin.Count; i++)
                {
                    var a = pin[i]; var b = pin[(i + 1) % pin.Count];
                    bool ia = keepAbove ? a.z >= zc : a.z <= zc, ib = keepAbove ? b.z >= zc : b.z <= zc;
                    if (ia) o.Add(a);
                    if (ia != ib) { float f = (zc - a.z) / (b.z - a.z); o.Add(Vector3.Lerp(a, b, f)); }
                }
                return o;
            }
            var l = Clip(new List<Vector3>(poly), zMin, true); if (l.Count < 3) return null;
            l = Clip(l, zMax, false); return l.Count < 3 ? null : l.ToArray();
        }

        /// <summary>A concave hip roof (pagoda) between an eave rectangle and a top rectangle: rows of tiles with darker lips,
        /// gold hip caps, eave fascia and upturned gold horn corners; only the parts inside zMin..zMax are added.</summary>
        static void HipRoof(TwMesh t, Rect eave, float yEave, Rect top, float yTop, int rows, float zMin, float zMax, float hornLen, float lipH)
        {
            Vector3 Corner(int c, float f) // c: 0 = (xmin,zmin) 1 = (xmax,zmin) 2 = (xmax,zmax) 3 = (xmin,zmax) ; x = Rect.x axis, z = Rect.y axis
            {
                float ex = c == 0 || c == 3 ? eave.xMin : eave.xMax, ez = c < 2 ? eave.yMin : eave.yMax;
                float tx = c == 0 || c == 3 ? top.xMin : top.xMax, tz = c < 2 ? top.yMin : top.yMax;
                float y = yEave + (yTop - yEave) * Mathf.Pow(f, 1.7f);  // concave: shallow at the eave, steep at the top
                return V(Mathf.Lerp(ex, tx, f), y, Mathf.Lerp(ez, tz, f));
            }
            void Add(Material m, params Vector3[] q) { var c = ClipZ(q, zMin, zMax); if (c == null) return; if (m == RoofTile || m == RoofTileDark) t.PolySlope(m, 1.2f, c); else t.Poly(m, 1f, c); }
            for (int r = 0; r < rows; r++)
            {
                float f0 = r / (float)rows, f1 = (r + 1) / (float)rows;
                for (int s = 0; s < 4; s++)
                {
                    int c0 = s, c1 = (s + 1) % 4;
                    var a0 = Corner(c0, f0); var a1 = Corner(c1, f0); var b1 = Corner(c1, f1 + 0.04f); var b0 = Corner(c0, f1 + 0.04f);
                    // faces outward: corners run counter-clockwise seen from above (xmin,zmin) -> (xmax,zmin) -> ..., so (a0, b0, b1, a1)
                    Add(r % 2 == 0 ? RoofTile : RoofTileDark, a0, b0, b1, a1);
                    // the tile lip along the lower edge of the row
                    var d = Vector3.down * lipH;
                    Add(RoofUnder, a0, a1, a1 + d, a0 + d);
                }
            }
            // flat top + soffit under the eaves
            Add(RoofTileDark, V(top.xMin, yTop, top.yMin), V(top.xMin, yTop, top.yMax), V(top.xMax, yTop, top.yMax), V(top.xMax, yTop, top.yMin));
            // gold hip caps and the eave fascia, horns at the corners
            for (int c = 0; c < 4; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    var p0 = Corner(c, r / (float)rows); var p1 = Corner(c, (r + 1) / (float)rows);
                    if ((p0.z + p1.z) * 0.5f < zMin || (p0.z + p1.z) * 0.5f > zMax) continue;
                    t.Cyl(Gold, 1f, p0 + Vector3.up * 0.04f, p1 + Vector3.up * 0.04f, 0.055f, 0.05f, 6);
                }
                var e0 = Corner(c, 0f); var e1 = Corner((c + 1) % 4, 0f);
                var mid = (e0 + e1) * 0.5f;
                var cl = ClipZ(new[] { e0, e1, e1 + Vector3.down * 0.01f }, zMin, zMax);
                if (cl != null)
                {
                    float za = Mathf.Max(zMin, Mathf.Min(e0.z, e1.z)), zb = Mathf.Min(zMax, Mathf.Max(e0.z, e1.z));
                    var q0 = e0; var q1 = e1;
                    if (Mathf.Abs(e1.z - e0.z) > 1e-3f) { q0 = Vector3.Lerp(e0, e1, Mathf.InverseLerp(e0.z, e1.z, Mathf.Clamp(e0.z, za, zb))); q1 = Vector3.Lerp(e0, e1, Mathf.InverseLerp(e0.z, e1.z, Mathf.Clamp(e1.z, za, zb))); }
                    t.Cyl(Gold, 1f, q0 + Vector3.down * 0.03f, q1 + Vector3.down * 0.03f, 0.05f, 0.05f, 6);
                }
                if (e0.z < zMin || e0.z > zMax) continue;
                var ctr = V((eave.xMin + eave.xMax) * 0.5f, yEave, (eave.yMin + eave.yMax) * 0.5f);
                var dir = (e0 - ctr); dir.y = 0f; dir.Normalize();
                Vector3[] hp = { e0, e0 + dir * hornLen * 0.35f + Vector3.up * hornLen * 0.05f, e0 + dir * hornLen * 0.7f + Vector3.up * hornLen * 0.25f, e0 + dir * hornLen * 0.85f + Vector3.up * hornLen * 0.62f };
                float[] hr = { 0.08f * hornLen / 0.6f, 0.06f * hornLen / 0.6f, 0.04f * hornLen / 0.6f, 0.006f };
                for (int i = 0; i < 3; i++) t.Cyl(Gold, 1f, hp[i], hp[i + 1], hr[i], hr[i + 1], 7, i == 2);
            }
        }

        static void MainRoof(TwMesh t, float zMin, float zMax, DestructibleBlock b)
        {
            // eaves 4.9 m wide at y 7.7 (x -5.0..-1.4, z -3.19..1.69), plateau under the turret at y 9.4; 5 tile rows (spec §4)
            // PM b24: Blender pagoda roof (swept upturned corners, rolled tiles, gold hips/eave band/horns), procedural fallback
            bool hero = HeroRoof(t, zMax > 0f ? "RoofL" : "RoofR");
            if (!hero) HipRoof(t, Rect.MinMaxRect(-5.0f, -3.19f, -1.4f, 1.69f), 7.7f, Rect.MinMaxRect(-3.8f, -1.62f, -2.6f, -0.38f), 9.4f, 5, zMin, zMax, 0.6f, 0.07f);
            // soffit under the eaves out to the room walls
            void Soffit(params Vector3[] q) { var c = ClipZ(q, zMin, zMax); if (c != null) t.Poly(RoofUnder, 1f, c); }
            float y = hero ? 7.6f : 7.69f; float ex0 = hero ? -5.15f : -5.0f, ex1 = hero ? -1.25f : -1.4f, ez0 = hero ? -3.35f : -3.19f, ez1 = hero ? 1.85f : 1.69f;
            Soffit(V(ex0, y, ez0), V(ex1, y, ez0), V(ex1, y, ez1), V(ex0, y, ez1));
            if (zMax > 0f)
            {
                // dormer_L on the front slope (z 0.0..0.78, y 8.15..8.83)
                Dormer(t, V(-4.55f, 8.15f, 0.39f), Vector3.left, 0.78f, 0.62f);
                Flag(t);
            }
            else Dormer(t, V(-3.4f, 8.62f, -2.12f), Vector3.back, 0.64f, 0.5f); // dormer_R on the right slope (z -2.43..-1.79)
        }

        /// <summary>The flag (goal u 36-47.5, v 1.5-9.5): pole rising from the roof at z +0.41, red cloth 1.8 m long drooping toward
        /// the screen left (+z) with a gentle wave, a gold sigil near the pole.</summary>
        static void Flag(TwMesh t)
        {
            float px = -3.6f, pz = 0.41f;
            t.Cyl(Timber, 0.8f, V(px, 8.55f, pz), V(px, 10.42f, pz), 0.05f, 0.04f, 7);
            t.Sphere(Gold, 1f, V(px, 10.47f, pz), Vector3.one * 0.07f, 6, 4);
            int n = 7; float len = 1.86f;
            Vector3 Top(int j) { float f = j / (float)n; return V(px - 0.13f * Mathf.Sin(f * 4.2f) * f, Mathf.Lerp(10.25f, 9.62f, f), pz + 0.04f + f * len); }
            Vector3 Bot(int j) { float f = j / (float)n; return V(px - 0.13f * Mathf.Sin(f * 4.2f) * f, Mathf.Lerp(9.5f, 8.85f, f) - (j == n ? 0.0f : 0f), pz + 0.04f + f * len); }
            for (int j = 0; j < n; j++) t.Poly2(j % 3 == 2 ? BannerFold : Banner, 1f, Top(j), Bot(j), Bot(j + 1), Top(j + 1));
            Sun(t, V(px - 0.12f, 9.86f, pz + 0.62f), Vector3.left, 0.12f, 0.22f, 8);
        }

        static void Dormer(TwMesh t, Vector3 baseCentre, Vector3 face, float w, float h)
        {
            var side = Vector3.Cross(Vector3.up, face).normalized; var c = baseCentre;
            var bodyC = c + Vector3.up * h * 0.35f - face * 0.15f;
            t.Box(Timber, 1f, bodyC, Abs(side * w + Vector3.up * h * 0.7f + face * 0.5f));
            t.Box(WindowGlow, 1f, bodyC + face * 0.26f, Abs(side * w * 0.45f + Vector3.up * h * 0.35f + face * 0.02f));
            t.Box(Gold, 1f, bodyC + face * 0.27f + Vector3.up * h * 0.22f, Abs(side * w * 0.6f + Vector3.up * 0.04f + face * 0.03f));
            // small gable roof: two tiled slabs meeting at a ridge that runs along the face direction, gold ridge cap
            var r0 = c + Vector3.up * h * 1.05f - face * 0.42f; var r1 = c + Vector3.up * h * 1.05f + face * 0.36f;
            foreach (float s in new[] { -1f, 1f })
            {
                var e = side * s * (w * 0.62f) + Vector3.down * h * 0.45f;
                t.PolySlope(RoofTile, 1.2f, r0, r1, r1 + e, r0 + e); t.PolySlope(RoofTile, 1.2f, r0 + e, r1 + e, r1, r0);
                t.Cyl(Gold, 1f, r1 + e * 0.02f, r1 + e, 0.025f, 0.025f, 5, false);
            }
            t.Cyl(Gold, 1f, r0, r1 + face * 0.02f, 0.04f, 0.04f, 6);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static void Turret(TwMesh t, DestructibleBlock b)
        {
            // plinth (goal u 52-60, v 6-8), open lantern room with 4 posts and warm light, small pagoda roof, finial
            t.BoxMinMax(Timber, 1f, V(-3.8f, 9.42f, -1.62f), V(-2.6f, 9.82f, -0.38f));
            t.BoxMinMax(Gold, 1f, V(-3.83f, 9.76f, -1.65f), V(-2.57f, 9.84f, -0.35f));
            t.BoxMinMax(WindowGlow, 1f, V(-3.45f, 9.84f, -1.3f), V(-2.95f, 10.12f, -0.6f));
            foreach (var p in new[] { V(-3.6f, 0f, -1.45f), V(-2.8f, 0f, -1.45f), V(-3.6f, 0f, -0.55f), V(-2.8f, 0f, -0.55f) })
                t.BoxMinMax(TimberLit, 1f, V(p.x - 0.05f, 9.84f, p.z - 0.05f), V(p.x + 0.05f, 10.13f, p.z + 0.05f));
            Flame(t, V(-3.5f, 9.84f, -1.2f), 0.2f, 0.06f); Flame(t, V(-3.5f, 9.84f, -0.75f), 0.2f, 0.06f);
            if (!HeroRoof(t, "Turret"))
            {
                HipRoof(t, Rect.MinMaxRect(-3.95f, -1.77f, -2.45f, -0.21f), 10.12f, Rect.MinMaxRect(-3.25f, -1.02f, -3.15f, -0.96f), 10.62f, 3, -99f, 99f, 0.3f, 0.05f);
                t.Cyl(Gold, 1f, V(-3.2f, 10.6f, -0.99f), V(-3.2f, 10.95f, -0.99f), 0.05f, 0.0f, 6, true);
                t.Sphere(Gold, 1f, V(-3.2f, 10.7f, -0.99f), Vector3.one * 0.07f, 6, 4);
            }
        }

        // ---------------- decorations on the rock ----------------
        static void Brazier(TwMesh t, Vector3 at) => Prop(t, at, 1.2f, () => BrazierBody(t, at));
        static void BrazierBody(TwMesh t, Vector3 at)
        {
            StoneK(t, StoneLight, 1f, at + V(0f, 0.1f, 0f), V(0.5f, 0.2f, 0.5f), (int)(at.z * 10f), 0.04f);
            t.BoxMinMax(Timber, 1f, at + V(-0.15f, 0.2f, -0.15f), at + V(0.15f, 1.5f, 0.15f));
            t.BoxMinMax(Iron, 1f, at + V(-0.17f, 0.6f, -0.17f), at + V(0.17f, 0.66f, 0.17f));
            t.BoxMinMax(Iron, 1f, at + V(-0.17f, 1.2f, -0.17f), at + V(0.17f, 1.26f, 0.17f));
            IronBowl(t, at + V(0f, 1.48f, 0f), 0.2f, 0.4f, 0.26f);
            Flame(t, at + V(0f, 1.62f, 0f), 0.8f, 0.33f);
        }

        internal static void BarrelBody(TwMesh t, Vector3 bottom, float h, float r, Material wood, Material hoop)
        {
            var prof = new[] { new Vector2(0f, r * 0.86f), new Vector2(h * 0.25f, r * 0.97f), new Vector2(h * 0.5f, r), new Vector2(h * 0.75f, r * 0.97f), new Vector2(h, r * 0.86f) };
            t.Lathe(wood, 0.6f, prof, 16, Matrix4x4.TRS(bottom, Quaternion.Euler(0f, 0f, 90f), Vector3.one));
            t.Cyl(wood, 0.6f, bottom + Vector3.up * (h - 0.01f), bottom + Vector3.up * h, r * 0.84f, r * 0.84f, 16, true);
            foreach (float f in new[] { 0.12f, 0.5f, 0.88f })
            {
                float rr = Mathf.Lerp(r * 0.86f, r, 1f - Mathf.Abs(f - 0.5f) * 2f) + 0.012f;
                t.Cyl(hoop, 1f, bottom + Vector3.up * (h * f - 0.035f), bottom + Vector3.up * (h * f + 0.035f), rr, rr, 16, false);
            }
        }

        static void FortBarrel(TwMesh t) => Prop(t, V(-5.6f, 0.82f, -3.25f), 1.1f, () => FortBarrelBody(t));
        static void FortBarrelBody(TwMesh t)
        {
            BarrelBody(t, V(-5.6f, 0.82f, -3.25f), 1.08f, 0.42f, BarrelWood, Iron);
            // red diamond with a gold sun on the cannon-facing side
            float x = -6.03f, y = 1.38f, z = -3.25f;
            t.Poly2(Banner, 1f, V(x, y + 0.24f, z), V(x, y, z + 0.2f), V(x, y - 0.24f, z), V(x, y, z - 0.2f));
            Sun(t, V(x - 0.006f, y, z), Vector3.left, 0.07f, 0.15f, 8);
        }

        /// <summary>Gold sun emblem (disc + rays) on a plane with the given normal.</summary>
        internal static void Sun(TwMesh t, Vector3 c, Vector3 normal, float rDisc, float rRay, int rays)
        {
            var u = Vector3.Cross(normal, Vector3.up); if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(normal, Vector3.right); u.Normalize(); var w = Vector3.Cross(u, normal).normalized;
            var ring = new Vector3[10];
            for (int i = 0; i < 10; i++) { float a = i * Mathf.PI * 2f / 10f; ring[i] = c + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * rDisc; }
            if (Vector3.Dot(Vector3.Cross(ring[1] - ring[0], ring[2] - ring[0]), normal) < 0f) System.Array.Reverse(ring);
            t.Poly(Gold, 1f, ring);
            for (int i = 0; i < rays; i++)
            {
                float a = i * Mathf.PI * 2f / rays; var d = u * Mathf.Cos(a) + w * Mathf.Sin(a); var s = Vector3.Cross(normal, d);
                float len = i % 2 == 0 ? rRay : rRay * 0.7f;
                var q = new[] { c + d * rDisc * 0.8f - s * rDisc * 0.3f, c + d * len, c + d * rDisc * 0.8f + s * rDisc * 0.3f };
                if (Vector3.Dot(Vector3.Cross(q[1] - q[0], q[2] - q[0]), normal) < 0f) System.Array.Reverse(q);
                t.Poly(Gold, 1f, q);
            }
        }

        // ---------------- archer towers ----------------
        static void Leg(TwMesh t, DestructibleBlock b, Vector3 c, Vector3 s)
        {
            float y0 = UnstretchY(c.y - s.y * 0.5f), y1 = UnstretchY(c.y + s.y * 0.5f);
            t.BoxMinMax(Timber, 1f, V(c.x - 0.15f, y0, c.z - 0.15f), V(c.x + 0.15f, y1, c.z + 0.15f));
            foreach (float f in new[] { 0.2f, 0.55f, 0.9f }) t.BoxMinMax(Iron, 1f, V(c.x - 0.17f, Mathf.Lerp(y0, y1, f) - 0.04f, c.z - 0.17f), V(c.x + 0.17f, Mathf.Lerp(y0, y1, f) + 0.04f, c.z + 0.17f));
            // X braces in the cannon-facing plane (front legs carry them) and the side plane, half per leg
            bool leftTower = b.blockId.StartsWith("L"); bool front = b.blockId.EndsWith("0"); bool outer = b.blockId.Contains("O");
            float zIn = leftTower ? 2.75f : -3.7f, zOut = leftTower ? 5.5f : -6.85f;
            float xF = leftTower ? -5.15f : -5.0f, xB = leftTower ? -2.45f : -2.35f;
            float deckLow = leftTower ? 2.5f : 2.0f, deckTop = leftTower ? 5.1f : 4.6f, groundIn = 0.8f, groundOut = leftTower ? 0.0f : -0.15f;
            float x = front ? xF : xB; float xo = front ? -0.17f : 0.17f;
            if (!outer)
            {
                t.Cyl(TimberLit, 1f, V(x + xo, deckLow + 0.5f, zIn), V(x + xo, deckTop, zOut), 0.08f, 0.08f, 6);
                t.Cyl(TimberLit, 1f, V(x + xo, groundIn + 0.2f, zIn), V(x + xo, deckLow, zOut), 0.08f, 0.08f, 6);
                if (front) t.Cyl(TimberLit, 1f, V(xF, deckLow + 0.5f, zIn), V(xB, deckTop, zIn), 0.07f, 0.07f, 6);
            }
            else
            {
                t.Cyl(TimberLit, 1f, V(x + xo, deckLow + 0.5f, zOut), V(x + xo, deckTop, zIn), 0.08f, 0.08f, 6);
                t.Cyl(TimberLit, 1f, V(x + xo, groundOut + 0.2f, zOut), V(x + xo, deckLow, zIn), 0.08f, 0.08f, 6);
                if (front) t.Cyl(TimberLit, 1f, V(xF, deckLow + 0.5f, zOut), V(xB, deckTop, zOut), 0.07f, 0.07f, 6);
            }
        }

        static void Deck(TwMesh t, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            t.BoxMinMax(Timber, 1f, V(x0, y0, z0), V(x0 + 0.24f, y1, z1));            // front edge beam
            t.BoxMinMax(Timber, 1f, V(x1 - 0.24f, y0, z0), V(x1, y1, z1));
            t.BoxMinMax(Timber, 1f, V(x0, y0, z0), V(x1, y1, z0 + 0.22f));
            t.BoxMinMax(Timber, 1f, V(x0, y0, z1 - 0.22f), V(x1, y1, z1));
            for (float z = z0 + 0.22f; z < z1 - 0.25f; z += 0.3f) t.BoxMinMax(Hash((int)(z * 17f), (int)(y1 * 10f)) < 0.5f ? Plank : TimberLit, 1f, V(x0 + 0.24f, y1 - 0.08f, z), V(x1 - 0.24f, y1, z + 0.27f));
            for (float z = z0 + 0.35f; z < z1 - 0.2f; z += 0.75f) // bolts on the front beam
            {
                t.Sphere(Gold, 1f, V(x0 - 0.02f, (y0 + y1) * 0.5f + 0.08f, z), Vector3.one * 0.035f, 5, 3);
                t.Sphere(Gold, 1f, V(x0 - 0.02f, (y0 + y1) * 0.5f - 0.08f, z), Vector3.one * 0.035f, 5, 3);
            }
        }

        static void Railing(TwMesh t, float x, float y, float h, float z0, float z1, int posts, bool stakes)
        {
            for (int i = 0; i < posts; i++)
            {
                float z = Mathf.Lerp(z0 + 0.08f, z1 - 0.08f, posts == 1 ? 0.5f : i / (float)(posts - 1));
                t.BoxMinMax(TimberLit, 1f, V(x - 0.07f, y, z - 0.07f), V(x + 0.07f, y + h, z + 0.07f));
                if (stakes) t.Cyl(TimberLit, 1f, V(x, y + h, z), V(x, y + h + 0.22f, z), 0.07f, 0f, 4, true);
                else t.Box(Timber, 1f, V(x, y + h + 0.04f, z), V(0.18f, 0.08f, 0.18f));
            }
            t.BoxMinMax(Timber, 1f, V(x - 0.08f, y + h - 0.1f, z0), V(x + 0.08f, y + h, z1));
            t.BoxMinMax(Timber, 1f, V(x - 0.06f, y + h * 0.45f - 0.04f, z0), V(x + 0.06f, y + h * 0.45f + 0.04f, z1));
            if (stakes)
                for (float z = z0 + 0.2f; z < z1 - 0.1f; z += 0.3f)
                {
                    t.BoxMinMax(TimberLit, 1f, V(x - 0.04f, y, z - 0.05f), V(x + 0.04f, y + h * 0.8f, z + 0.05f));
                    t.Cyl(TimberLit, 1f, V(x, y + h * 0.8f, z), V(x, y + h * 1.25f, z), 0.05f, 0f, 4, true);
                }
        }

        static void LeftLow(TwMesh t)
        {
            Deck(t, -5.0f, -2.6f, 2.5f, 3.0f, 2.4f, 6.9f);
            Railing(t, -4.92f, 3.0f, 0.46f, 2.4f, 6.9f, 7, false);
            Railing(t, -3.8f, 3.0f, 0.46f, 6.82f, 6.9f, 1, false);
            // fire pots: a small one on the left (goal u 9-14) and a big one at z 3.5 (goal u 25-31)
            IronBowl(t, V(-4.75f, 3.0f, 6.15f), 0.14f, 0.24f, 0.22f); Flame(t, V(-4.75f, 3.15f, 6.15f), 0.5f, 0.2f);
            Torch(t, V(-4.92f, 3.46f, 6.75f), 0.1f);
            IronBowl(t, V(-4.65f, 3.0f, 3.5f), 0.2f, 0.42f, 0.3f); Flame(t, V(-4.65f, 3.2f, 3.5f), 0.62f, 0.34f);
            // the ladder from the rocks to the lower deck, 9 rungs, leaning (goal u 17-23.5, v 38-52)
            var b0 = V(-6.05f, -0.15f, 4.85f); var b1 = V(-6.05f, -0.15f, 5.45f); var t0 = V(-5.12f, 3.05f, 4.6f); var t1 = V(-5.12f, 3.05f, 5.2f);
            t.Cyl(TimberLit, 1f, b0, t0, 0.055f, 0.055f, 6); t.Cyl(TimberLit, 1f, b1, t1, 0.055f, 0.055f, 6);
            for (int i = 1; i <= 9; i++) { float f = i / 10f; t.Cyl(Timber, 1f, Vector3.Lerp(b0, t0, f), Vector3.Lerp(b1, t1, f), 0.04f, 0.04f, 5, false); }
            Chain(t, V(-5.0f, 3.0f, 6.9f), V(-6.3f, 0.6f, 7.4f), 0.2f);
        }

        static void LeftTop(TwMesh t, DestructibleBlock b)
        {
            Deck(t, -5.0f, -2.6f, 5.1f, 5.65f, 2.6f, 5.66f);
            Railing(t, -4.92f, 5.65f, 0.53f, 2.6f, 5.66f, 5, false);
            Railing(t, -3.8f, 5.65f, 0.53f, 5.58f, 5.66f, 1, false);
            Torch(t, V(-4.92f, 6.18f, 4.73f), 0.12f);
            Prop(t, V(-3.95f, 5.65f, 3.6f), 1.3f, () => Archer(t, V(-3.95f, 5.65f, 3.6f), Vector3.back));
            IronBowl(t, V(-4.6f, 5.65f, 5.2f), 0.14f, 0.26f, 0.22f); Flame(t, V(-4.6f, 5.8f, 5.2f), 0.55f, 0.21f);   // fire basket
            SkullBanner(t, -5.04f, 3.69f, 4.75f, 5.08f, 3.51f, 3.95f, false);
        }

        static void RightLow(TwMesh t)
        {
            Deck(t, -4.85f, -2.5f, 2.0f, 2.42f, -7.3f, -4.16f);
            Railing(t, -4.77f, 2.42f, 0.42f, -7.3f, -4.16f, 5, true);
            EnemyCannon(t, V(-3.75f, 2.42f, -5.74f));
        }

        static void RightTop(TwMesh t)
        {
            Deck(t, -4.85f, -2.5f, 4.6f, 5.35f, -5.95f, -3.16f);
            Railing(t, -4.77f, 5.35f, 0.53f, -5.95f, -3.16f, 5, false);
            Torch(t, V(-4.77f, 5.88f, -3.65f), 0.12f);
            Prop(t, V(-3.95f, 5.35f, -4.96f), 1.3f, () => Archer(t, V(-3.95f, 5.35f, -4.96f), Vector3.back));
            IronBowl(t, V(-4.5f, 5.35f, -5.55f), 0.14f, 0.26f, 0.22f); Flame(t, V(-4.5f, 5.5f, -5.55f), 0.55f, 0.21f); // fire basket
            SkullBanner(t, -4.89f, -4.95f, -3.85f, 4.55f, 3.05f, 3.5f, false);                                      // PM b24: skull banner on the right tower too
        }

        static void Scaffold(TwMesh t)
        {
            foreach (float z in new[] { -2.25f, -3.3f }) t.BoxMinMax(Timber, 1f, V(-5.18f, 1.46f, z - 0.08f), V(-5.02f, 4.07f, z + 0.08f));
            foreach (float y in new[] { 1.9f, 3.0f, 3.95f }) t.BoxMinMax(TimberLit, 1f, V(-5.2f, y - 0.06f, -3.39f), V(-5.0f, y + 0.06f, -2.15f));
            t.Cyl(TimberLit, 1f, V(-5.12f, 1.9f, -3.3f), V(-5.12f, 3.95f, -2.25f), 0.06f, 0.06f, 5);
        }

        /// <summary>Black iron cannon on a wooden carriage, muzzle toward the camera and screen-right (goal u 79-93, v 32.5-40).</summary>
        static void EnemyCannon(TwMesh t, Vector3 deck) => Prop(t, deck, 1.25f, () => EnemyCannonBody(t, deck));
        static void EnemyCannonBody(TwMesh t, Vector3 deck)
        {
            var dir = new Vector3(-0.72f, 0.12f, -0.68f).normalized; var flat = new Vector3(dir.x, 0f, dir.z).normalized; var side = Vector3.Cross(Vector3.up, flat);
            var trunnion = deck + Vector3.up * 0.55f;
            var rot = Quaternion.FromToRotation(Vector3.right, dir);
            var prof = new[] { new Vector2(-0.75f, 0f), new Vector2(-0.75f, 0.14f), new Vector2(-0.68f, 0.26f), new Vector2(-0.5f, 0.27f), new Vector2(0.2f, 0.23f), new Vector2(0.62f, 0.21f), new Vector2(0.66f, 0.27f), new Vector2(0.8f, 0.27f), new Vector2(0.8f, 0.12f), new Vector2(0.6f, 0.12f), new Vector2(0.6f, 0f) };
            t.Lathe(Iron, 1f, prof, 16, Matrix4x4.TRS(trunnion, rot, Vector3.one));
            t.Sphere(Iron, 1f, trunnion - dir * 0.85f, Vector3.one * 0.08f, 6, 4);
            foreach (float s in new[] { -1f, 1f })
            {
                var cheek = deck + side * s * 0.3f + Vector3.up * 0.28f;
                t.Box(Timber, 1f, cheek, V(0.12f, 0.5f, 0.9f), Quaternion.LookRotation(flat) * Quaternion.Euler(0f, 90f, 0f));
                t.Cyl(Timber, 1f, deck + side * s * 0.42f + flat * 0.25f + Vector3.up * 0.2f, deck + side * s * 0.5f + flat * 0.25f + Vector3.up * 0.2f, 0.2f, 0.2f, 10);
                t.Cyl(Timber, 1f, deck + side * s * 0.42f - flat * 0.3f + Vector3.up * 0.16f, deck + side * s * 0.5f - flat * 0.3f + Vector3.up * 0.16f, 0.16f, 0.16f, 10);
            }
        }

        // ---------------- the crystal ----------------
        static void CrystalCluster(TwMesh t, DestructibleBlock b)
        {
            // stone plinth 0.5 m behind the arch face, the tall 6-sided shard (2.0 m) and 4 small shards, a bright core at the base
            t.Cyl(StoneLight, 1f, V(-3.7f, 0.8f, -0.31f), V(-3.7f, 0.98f, -0.31f), 0.6f, 0.55f, 8, true, true);
            t.Shard(Crystal, CrystalDark, V(-3.72f, 0.95f, -0.24f), V(-0.05f, 1f, 0.02f), 0.62f, 2.35f, 6, 0.34f, 0.2f);
            t.Shard(Crystal, CrystalDark, V(-3.75f, 0.95f, -0.75f), V(-0.15f, 1f, -0.35f), 0.28f, 1.25f, 6, 0.4f, 0.5f);
            t.Shard(Crystal, CrystalDark, V(-3.75f, 0.95f, 0.18f), V(-0.15f, 1f, 0.35f), 0.28f, 1.2f, 6, 0.4f, 0.1f);
            t.Shard(Crystal, CrystalDark, V(-3.95f, 0.95f, -0.5f), V(-0.35f, 1f, -0.15f), 0.11f, 0.6f, 5, 0.45f, 0.8f);
            t.Shard(Crystal, CrystalDark, V(-3.95f, 0.95f, 0.0f), V(-0.35f, 1f, 0.2f), 0.1f, 0.55f, 5, 0.45f, 0.3f);
            t.Cyl(CrystalCore, 1f, V(-3.72f, 0.96f, -0.28f), V(-3.72f, 1.02f, -0.28f), 0.52f, 0.36f, 10, true);
        }
    }
}
