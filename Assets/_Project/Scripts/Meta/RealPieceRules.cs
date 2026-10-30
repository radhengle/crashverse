using System;
using Crashverse.Data;
using UnityEngine;

namespace Crashverse.Meta
{
    /// <summary>
    /// 3D Modeler realistic v1 (models/Realistic_Scope_L1-10.md, Tools/Blender/realistic/scope.py classify): which Kit_Real* piece
    /// dresses a layout block. Visual only: the block keeps its box collider, mass, joints and chunk layout (ranked determinism).
    /// Island rock / plinth blocks are held (keep the procedural rock look), as in the scope.
    /// </summary>
    public static class RealPieceRules
    {
        public static readonly string[] Pieces =
        {
            "Kit_RealBrick_Block", "Kit_RealSteel_Block", "Kit_RealWood_Block", "Kit_RealWood_Joint", "Kit_RealWood_Floor", "Kit_RealBrick_Wall",
            "Kit_RealBrick_Floor", "Kit_RealWood_Wall", "Kit_RealRoof_Roof", "Kit_RealSteel_Floor", "Kit_RealProps_Crate", "Kit_RealProps_Barrel",
            "Kit_RealSteel_Joint", "Kit_RealSteel_Wall",
        };

        /// <summary>Kit folder (Brick/Wood/Steel/Roof/Props) of a piece name.</summary>
        public static string KitOf(string piece) => piece == null ? null : piece.Substring(8, piece.IndexOf('_', 8) - 8);

        /// <summary>UV tile size in metres per kit (models/HeroBuildings/Kit_Real*_stats.json tile_m).</summary>
        public static float TileM(string kit) => kit == "Brick" ? 1.75f : kit == "Wood" ? 1.4f : kit == "Steel" ? 1.5f : kit == "Roof" ? 2.0f : 1.0f;

        public static string Classify(BlockData b)
        {
            if (b == null || b.s == null || b.s.Length != 3) return null;
            if (b.role == "island") return null;                       // held: island rock stays procedural
            string m = b.mat == "brick" ? "Brick" : b.mat == "steel" ? "Steel" : b.mat == "wood" ? "Wood" : null;
            if (m == null) return null;                                // glass etc.: no realistic piece yet
            if (b.id == "barrelDecor") return "Kit_RealProps_Barrel";
            if (b.explosive || b.id == "crate") return "Kit_RealProps_Crate";
            if (b.role == "roof") return "Kit_RealRoof_Roof";
            var s = b.s; float[] o = { s[0], s[1], s[2] }; Array.Sort(o);
            float a = o[0], bb = o[1], c = o[2];
            int thin = s[0] == a ? 0 : s[1] == a ? 1 : 2;
            if (bb <= 0.65f && c >= 1.8f * bb) return $"Kit_Real{m}_Joint";
            if (a <= 0.5f && a <= 0.4f * bb) return thin == 1 ? $"Kit_Real{m}_Floor" : $"Kit_Real{m}_Wall";
            return $"Kit_Real{m}_Block";
        }

        /// <summary>
        /// Axis map for fitting a piece whose (axis-converted) size is <paramref name="piece"/> onto a block of size
        /// <paramref name="block"/>: result[i] = the piece axis that goes to block axis i, so the thin axis of a wall goes to the block's
        /// thin axis and a joint's long axis to the block's longest. Ties keep y on y.
        /// </summary>
        public static int[] AxisMap(Vector3 piece, Vector3 block)
        {
            int[] pr = Rank(piece), br = Rank(block), map = new int[3];
            for (int i = 0; i < 3; i++) map[br[i]] = pr[i];
            return map;
        }

        /// <summary>Axes sorted by size ascending; near-equal sizes (5%) keep y last-of-equals so y-up survives for cubes.</summary>
        static int[] Rank(Vector3 v)
        {
            var idx = new[] { 0, 2, 1 };
            Array.Sort(idx, (x, y) =>
            {
                float a = v[x], b = v[y];
                if (Mathf.Abs(a - b) <= 0.05f * Mathf.Max(a, b)) return Order(x).CompareTo(Order(y));
                return a.CompareTo(b);
            });
            return idx;
        }
        static int Order(int axis) => axis == 1 ? 1 : 0;

        /// <summary>Metric box projection after scaling (3D Modeler: uv = pos / tile_m on the dominant normal axis).</summary>
        public static Vector2 BoxUv(Vector3 posMetres, Vector3 normal, float tileM)
        {
            float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
            Vector2 uv = ax >= ay && ax >= az ? new Vector2(posMetres.z, posMetres.y) : ay >= az ? new Vector2(posMetres.x, posMetres.z) : new Vector2(posMetres.x, posMetres.y);
            return uv / Mathf.Max(0.01f, tileM);
        }
    }
}
