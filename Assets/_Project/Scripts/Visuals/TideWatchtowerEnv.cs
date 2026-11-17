using UnityEngine;

namespace Crashverse.Visuals
{
    /// <summary>
    /// L1 v2 environment around the Tide Watchtower (design/L01_v2_spec.md §2 foreground, §6 background): the cannon deck with its
    /// railing, posts, lantern, barrels, cannonball crate, rope, cloths and corner banners; the turquoise lagoon; the far cliffs with a
    /// beach, the waterfall and the white castle with blue spires. Presentation only (no colliders), never read by the sim.
    /// </summary>
    public static class TideWatchtowerEnv
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Material M(string k, string tex, Color c, float metal = 0f, float rough = 0.9f, Color emission = default) => KingdomMaterials.Make("Tw" + k, tex, c, metal, rough, Vector2.one, emission);
        static Material DeckLit => M("DeckLit", "T_K_Planks", new Color(1.75f, 1.45f, 1.25f));   // #E28D55
        static Material DeckMid => M("DeckMid", "T_K_Planks", new Color(1.35f, 1.05f, 0.95f));   // #8A503C
        static Material DeckGap => M("DeckGap", null, new Color(0.2f, 0.1f, 0.07f), 0f, 1f);     // #351912
        static Material RailWood => M("RailWood", "T_K_Wood", new Color(1.75f, 1.45f, 1.35f));
        static Material Brass => M("Brass", null, new Color(0.80f, 0.56f, 0.24f), 0.4f, 0.4f);
        static Material LanternGlow => M("LanternGlow", null, new Color(1f, 0.94f, 0.24f), 0f, 1f, new Color(3.2f, 2.6f, 0.7f)); // #FEF03C
        static Material OrangeStave => M("OrangeStave", "T_K_Planks", new Color(1.8f, 1.3f, 0.62f));  // #D57321
        static Material Steel => M("Steel", null, new Color(0.45f, 0.47f, 0.5f), 0.6f, 0.4f);
        static Material Ball => M("Ball", null, new Color(0.19f, 0.13f, 0.15f), 0.3f, 0.45f);        // #312227
        static Material Crate => M("Crate", "T_K_Planks", new Color(1.3f, 1.0f, 0.8f));
        static Material RopeMat => M("RopeEnv", "T_K_Bark", new Color(1.35f, 1.1f, 0.95f));          // #91532F
        static Material RedCloth => M("RedCloth", null, new Color(0.47f, 0.07f, 0.08f), 0f, 0.85f);   // #791314
        static Material GoldCloth => M("GoldCloth", null, new Color(0.93f, 0.68f, 0.18f), 0f, 0.7f);
        static Material CliffRock => M("CliffRock", "T_K_Rock", new Color(0.72f, 0.95f, 1.25f));      // hazy #3B607E
        static Material CliffDark => M("CliffDark", "T_K_Rock", new Color(0.55f, 0.72f, 0.95f));
        static Material CliffGrass => M("CliffGrass", "T_K_Grass", new Color(2.0f, 1.9f, 1.6f));
        static Material Beach => M("Beach", "T_K_Sand", new Color(1.35f, 1.3f, 1.2f));
        static Material CastleWall => M("CastleWall", null, new Color(0.93f, 0.92f, 0.9f), 0f, 0.8f);
        static Material CastleShade => M("CastleShade", null, new Color(0.72f, 0.76f, 0.85f), 0f, 0.85f);
        static Material Spire => M("Spire", null, new Color(0.2f, 0.42f, 0.85f), 0f, 0.6f);
        static Material Waterfall => M("Waterfall", null, new Color(0.85f, 0.95f, 1f), 0f, 0.5f, new Color(0.35f, 0.45f, 0.55f));

        public static void Build(Transform root, Vector3 cannon)
        {
            var go = new GameObject("TideWatchtowerEnv").transform; go.SetParent(root, false);
            var t = new TwMesh(); Deck(t, cannon); t.Build(go, "TwDeck", true);
            var p = new TwMesh(); Props(p, cannon); p.Build(go, "TwProps", true);
            var g = new TwMesh(); Background(g); g.Build(go, "TwBackground", false, false);
            // the lantern's warm light on the rail and the deck
            var lg = new GameObject("LanternLight"); lg.transform.SetParent(go, false); lg.transform.position = V(-17.55f, 1.95f, 3.0f);
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.4f); l.intensity = 2.2f; l.range = 3.5f; l.shadows = LightShadows.None;
        }

        /// <summary>Warm planks running away from the camera (0.35 m wide) from behind the camera to the rail at x -17.55, the rail of
        /// two thick beams (y 0.81-1.30) with bolted posts at z +/-2.0 (to y 1.69), and a deck edge over the sea.</summary>
        static void Deck(TwMesh t, Vector3 c)
        {
            float x0 = -27f, x1 = -17.3f;
            t.BoxMinMax(DeckGap, 1f, V(x0, -0.5f, -5.2f), V(x1, -0.06f, 5.2f));
            int i = 0;
            for (float z = -5.2f; z < 5.2f; z += 0.35f, i++)
            {
                float h = (i * 7919 % 13) / 13f;
                var mat = h < 0.45f ? DeckLit : DeckMid;
                // planks in two runs with a butt joint, so they read as boards
                float j = x0 + 3.5f + h * 3f;
                t.BoxMinMax(mat, 1.6f, V(x0, -0.08f, z + 0.015f), V(j - 0.02f, 0f, z + 0.335f));
                t.BoxMinMax(mat, 1.6f, V(j + 0.02f, -0.08f, z + 0.015f), V(x1, 0f, z + 0.335f));
                if (i % 3 == 0) { t.Sphere(Steel, 1f, V(x1 - 0.25f, 0.005f, z + 0.17f), V(0.03f, 0.015f, 0.03f), 5, 2); t.Sphere(Steel, 1f, V(j + 0.15f, 0.005f, z + 0.17f), V(0.03f, 0.015f, 0.03f), 5, 2); }
            }
            t.BoxMinMax(RailWood, 1.2f, V(-17.4f, -0.6f, -5.2f), V(-17.1f, 0.02f, 5.2f)); // deck edge beam over the water
            // the rail: two thick beams across the whole width
            t.BoxMinMax(RailWood, 1.2f, V(-17.68f, 0.81f, -5.2f), V(-17.42f, 1.02f, 5.2f));
            t.BoxMinMax(RailWood, 1.2f, V(-17.7f, 1.08f, -5.2f), V(-17.4f, 1.30f, 5.2f));
            for (float z = -4.8f; z < 5f; z += 1.2f) t.BoxMinMax(RailWood, 1f, V(-17.62f, 0f, z - 0.07f), V(-17.48f, 0.81f, z + 0.07f));
            foreach (float z in new[] { -2.0f, 2.0f, -4.6f, 4.6f })
            {
                t.BoxMinMax(RailWood, 1f, V(-17.76f, 0f, z - 0.21f), V(-17.34f, 1.69f, z + 0.21f));
                t.BoxMinMax(RailWood, 1f, V(-17.79f, 1.66f, z - 0.24f), V(-17.31f, 1.76f, z + 0.24f));
                t.Box(Steel, 1f, V(-17.775f, 1.05f, z), V(0.02f, 0.34f, 0.3f));
                for (int k = 0; k < 4; k++) t.Sphere(Steel, 1f, V(-17.79f, 1.05f + ((k & 1) == 0 ? -0.11f : 0.11f), z + ((k & 2) == 0 ? -0.09f : 0.09f)), Vector3.one * 0.03f, 5, 3);
            }
        }

        static void Props(TwMesh t, Vector3 c)
        {
            // brass hexagonal lantern on the rail top at z 3.0 (goal 0.5-12, 55-69), ~0.7 m wide, 1.3 m tall
            var lb = V(-17.55f, 1.30f, 3.0f);
            t.Cyl(Brass, 1f, lb, lb + Vector3.up * 0.12f, 0.36f, 0.33f, 6, true, true);
            t.Cyl(LanternGlow, 1f, lb + Vector3.up * 0.12f, lb + Vector3.up * 0.78f, 0.26f, 0.29f, 6, true, true);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f; var d = V(Mathf.Cos(a), 0f, Mathf.Sin(a));
                t.Cyl(Brass, 1f, lb + d * 0.31f + Vector3.up * 0.1f, lb + d * 0.34f + Vector3.up * 0.82f, 0.03f, 0.03f, 4);
            }
            t.Cyl(Brass, 1f, lb + Vector3.up * 0.8f, lb + Vector3.up * 0.9f, 0.37f, 0.37f, 6, true, true);
            t.Cyl(Brass, 1f, lb + Vector3.up * 0.9f, lb + Vector3.up * 1.15f, 0.34f, 0.08f, 6, true, true);
            t.Cyl(Brass, 1f, lb + Vector3.up * 1.15f, lb + Vector3.up * 1.22f, 0.08f, 0.08f, 6, true);
            for (int i = 0; i < 8; i++) { float a0 = i * Mathf.PI / 8f, a1 = (i + 1) * Mathf.PI / 8f; t.Cyl(Brass, 1f, lb + V(0f, 1.22f + Mathf.Sin(a0) * 0.12f, Mathf.Cos(a0) * 0.12f), lb + V(0f, 1.22f + Mathf.Sin(a1) * 0.12f, Mathf.Cos(a1) * 0.12f), 0.015f, 0.015f, 4, false); }
            // orange barrel, left (deck point x -18.2, z 2.9; 0.9 m wide, 1.1 m tall, 3 steel hoops)
            TideWatchtower.BarrelBody(t, V(-18.25f, 0f, 2.9f), 1.1f, 0.45f, OrangeStave, Steel);
            // cannonball crate (x -18.2, z 1.35..2.4): 5 black balls, 3 at the bottom and 2 on top
            t.BoxMinMax(Crate, 0.8f, V(-18.5f, 0f, 1.35f), V(-17.9f, 0.45f, 2.4f));
            t.BoxMinMax(Steel, 1f, V(-18.52f, 0.36f, 1.33f), V(-17.88f, 0.4f, 2.42f));
            for (int i = 0; i < 3; i++) t.Sphere(Ball, 1f, V(-18.2f, 0.58f, 1.55f + i * 0.33f), Vector3.one * 0.17f, 10, 7);
            for (int i = 0; i < 2; i++) t.Sphere(Ball, 1f, V(-18.2f, 0.86f, 1.71f + i * 0.33f), Vector3.one * 0.17f, 10, 7);
            // rope coil on a barrel, right (z -2.95), a loop hanging down the side and a cannonball on top
            TideWatchtower.BarrelBody(t, V(-18.25f, 0f, -2.95f), 0.95f, 0.45f, Crate, Steel);
            for (int turn = 0; turn < 3; turn++)
            {
                float r = 0.36f - turn * 0.02f, y = 1.0f + turn * 0.075f;
                for (int i = 0; i < 16; i++)
                {
                    float a0 = i * Mathf.PI / 8f, a1 = (i + 1) * Mathf.PI / 8f;
                    t.Cyl(RopeMat, 0.4f, V(-18.25f + Mathf.Cos(a0) * r, y, -2.95f + Mathf.Sin(a0) * r), V(-18.25f + Mathf.Cos(a1) * r, y, -2.95f + Mathf.Sin(a1) * r), 0.045f, 0.045f, 5, false);
                }
            }
            for (int i = 0; i < 8; i++)
            {
                float f0 = i / 8f, f1 = (i + 1) / 8f;
                Vector3 L(float f) => V(-18.25f - 0.38f - 0.08f * Mathf.Sin(f * Mathf.PI), 1.0f - 0.75f * Mathf.Sin(f * Mathf.PI), -2.95f - 0.25f + f * 0.5f);
                t.Cyl(RopeMat, 0.4f, L(f0), L(f1), 0.04f, 0.04f, 5, false);
            }
            t.Sphere(Ball, 1f, V(-18.25f, 1.32f, -2.95f), Vector3.one * 0.17f, 10, 7);
            // red cloth draped over the rail between the left post and the cannon (goal 25-37, 70-75), gold cloth on the right
            Drape(t, RedCloth, -17.72f, 1.32f, 0.95f, 1.85f, 0.75f);
            Drape(t, GoldCloth, -17.72f, 1.32f, -1.95f, -1.6f, 0.32f);
            // corner banners: red cloth on gold-capped poles at both bottom corners (deck x -20.2, z +/-2.34)
            foreach (float z in new[] { 2.34f, -2.34f })
            {
                var b = V(-20.2f, 0f, z);
                t.Cyl(TideWatchtower.Timber, 0.8f, b, b + Vector3.up * 2.1f, 0.05f, 0.045f, 7);
                t.Sphere(Brass, 1f, b + Vector3.up * 2.15f, Vector3.one * 0.08f, 6, 4);
                t.Cyl(Brass, 1f, b + V(-0.02f, 2.0f, -0.35f), b + V(-0.02f, 2.0f, 0.35f), 0.03f, 0.03f, 5);
                for (int j = 0; j < 4; j++)
                {
                    float z0 = -0.33f + j * 0.165f, z1 = z0 + 0.165f, w0 = 0.04f * Mathf.Sin(j * 1.7f), w1 = 0.04f * Mathf.Sin((j + 1) * 1.7f);
                    t.Poly2(j % 2 == 0 ? RedCloth : TideWatchtower.Banner, 1f, b + V(w0, 1.98f, z0), b + V(w0, 0.75f + 0.08f * j, z0), b + V(w1, 0.75f + 0.08f * (j + 1), z1), b + V(w1, 1.98f, z1));
                }
                t.Poly2(TideWatchtower.Gold, 1f, b + V(-0.01f, 0.83f, -0.33f), b + V(-0.01f, 0.75f, -0.33f), b + V(-0.01f, 1.07f, 0.33f), b + V(-0.01f, 1.15f, 0.33f));
            }
        }

        static void Drape(TwMesh t, Material m, float x, float yTop, float z0, float z1, float drop)
        {
            int n = 6;
            for (int j = 0; j < n; j++)
            {
                float f0 = j / (float)n, f1 = (j + 1) / (float)n;
                float s0 = Mathf.Sin(f0 * Mathf.PI), s1 = Mathf.Sin(f1 * Mathf.PI);
                var a0 = V(x - 0.03f * Mathf.Sin(f0 * 9f), yTop, Mathf.Lerp(z0, z1, f0)); var a1 = V(x - 0.03f * Mathf.Sin(f1 * 9f), yTop, Mathf.Lerp(z0, z1, f1));
                t.Poly2(m, 1f, a0, a0 + V(-0.05f, -drop * (0.55f + 0.45f * s0), 0f), a1 + V(-0.05f, -drop * (0.55f + 0.45f * s1), 0f), a1);
            }
            t.Poly2(m, 1f, V(x + 0.3f, yTop + 0.005f, z0), V(x - 0.04f, yTop + 0.005f, z0), V(x - 0.04f, yTop + 0.005f, z1), V(x + 0.3f, yTop + 0.005f, z1));
        }

        /// <summary>Far background at ~400 m (spec §6): left cliffs + beach (goal u 0-25, v 14-29), the right cliff (u 72-100, v 6-28)
        /// with the waterfall (u 85-88, v 11-27) and the white castle with blue spires on top (u 80-93, v 0-8).</summary>
        static void Background(TwMesh t)
        {
            // right cliff: stacked rough masses rising toward the right, grass caps
            Cliff(t, 31, V(400f, 0f, -72f), V(440f, 0f, -158f), 6, 26f, 46f, 62f, 98f);
            // left headland and the mountain behind it, with a beach at the waterline
            Cliff(t, 41, V(410f, 0f, 160f), V(400f, 0f, 84f), 5, 24f, 36f, 40f, 66f);
            Cliff(t, 47, V(470f, 0f, 140f), V(470f, 0f, 112f), 2, 22f, 26f, 48f, 52f);
            t.Slab(Beach, 3f, V(385f, -0.6f, 112f), V(16f, 1.6f, 70f), 5, 0.05f);
            // waterfall down the right cliff's face, foam at its foot
            float wz = -116f;
            for (int i = 0; i < 3; i++)
            {
                float z0 = wz - 2.6f + i * 1.7f;
                t.Poly2(Waterfall, 4f, V(381f - i * 0.3f, 78f, z0), V(381f - i * 0.3f, 78f, z0 + 1.6f), V(380f - i * 0.3f, 0f, z0 + 2.2f), V(380f - i * 0.3f, 0f, z0 - 0.4f));
            }
            t.Slab(TideWatchtower.Foam, 4f, V(378f, -0.4f, wz), V(10f, 2.2f, 12f), 9, 0.25f);
            // castle on the cliff top: white walls and towers with blue conical spires
            float cx = 425f, cy = 92f;
            t.BoxMinMax(CastleShade, 4f, V(cx - 8f, cy - 6f, -128f), V(cx + 8f, cy + 7f, -96f));
            t.BoxMinMax(CastleWall, 4f, V(cx - 9f, cy - 6f, -126f), V(cx - 7.5f, cy + 8f, -98f));
            float[] tz = { -129f, -121f, -112f, -104f, -95f }; float[] th = { 14f, 20f, 27f, 18f, 12f };
            for (int i = 0; i < tz.Length; i++)
            {
                var b = V(cx - 6f, cy - 4f, tz[i]); float r = i == 2 ? 2.6f : 2.0f;
                t.Cyl(CastleWall, 4f, b, b + Vector3.up * th[i], r, r, 10, true);
                t.Cyl(Spire, 4f, b + Vector3.up * th[i], b + Vector3.up * (th[i] + r * 3.2f), r * 1.25f, 0f, 10, true);
                for (int k = 0; k < 3; k++) t.Box(TideWatchtower.Socket, 1f, b + V(-r - 0.05f, th[i] * (0.4f + 0.2f * k), 0f), V(0.1f, 1.2f, 0.6f));
            }
        }

        static void Cliff(TwMesh t, int seed, Vector3 a, Vector3 b, int n, float w0, float w1, float h0, float h1)
        {
            for (int i = 0; i < n; i++)
            {
                float f = n == 1 ? 0.5f : i / (float)(n - 1);
                var p = Vector3.Lerp(a, b, f); float h = Mathf.Lerp(h0, h1, f) * (0.85f + 0.3f * H(seed, i)); float w = Mathf.Lerp(w0, w1, H(seed + 1, i));
                var rot = Quaternion.Euler(0f, H(seed + 2, i) * 40f - 20f, 0f);
                t.Slab(H(seed + 3, i) < 0.5f ? CliffRock : CliffDark, 9f, V(p.x, h * 0.5f - 3f, p.z), V(w, h + 3f, w * 0.9f), seed * 13 + i, 0.12f, rot);
                t.Slab(CliffGrass, 6f, V(p.x, h - 1.5f, p.z), V(w * 0.95f, 4f, w * 0.85f), seed * 17 + i, 0.15f, rot);
                // a lower ledge in front with its own grass
                t.Slab(CliffDark, 9f, V(p.x - w * 0.45f, h * 0.22f - 3f, p.z + (H(seed + 4, i) - 0.5f) * w * 0.5f), V(w * 0.6f, h * 0.45f + 3f, w * 0.6f), seed * 19 + i, 0.15f, rot);
                t.Slab(CliffGrass, 6f, V(p.x - w * 0.45f, h * 0.45f - 1f, p.z + (H(seed + 4, i) - 0.5f) * w * 0.5f), V(w * 0.55f, 2.5f, w * 0.55f), seed * 23 + i, 0.15f, rot);
            }
        }

        static float H(int a, int b) { uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h % 10000u) / 10000f; }
    }
}
