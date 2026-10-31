using UnityEngine;

namespace Crashverse.Meta
{
    /// <summary>
    /// PM 31 Oct (L01.jpg): per-level cannon→target distance. Default 30 m (the original arena: one 60×25 m island). A "compact"
    /// level (distance below CompactBelow) puts the target on its own small island close to the cannon, with water on both
    /// sides; the cannon stands on its harbour deck. Pure maths, shared by GameBootstrap, the arena, the camera and tests.
    /// </summary>
    public static class ArenaLayoutRules
    {
        public const float DefaultDistance = 30f, MinDistance = 12f, MaxDistance = 40f, CompactBelow = 20f;
        public const float WreckerX = -20f; // = WreckerSpec.WreckerX (Weapons is not visible from Meta)
        public const float BigCx = 0f, BigRx = 30f, BigRz = 12.5f; // the original island (ArenaBuilder.IslandLength/Width)

        public static float Distance(float levelDistance) => levelDistance <= 0f ? DefaultDistance : Mathf.Clamp(levelDistance, MinDistance, MaxDistance);
        public static bool IsCompact(float levelDistance) => Distance(levelDistance) < CompactBelow;
        public static Vector3 StructureBase(float levelDistance) => new Vector3(WreckerX + Distance(levelDistance), 0f, 0f);

        /// <summary>Half extents (x depth, z width) of a layout's blocks around the structure base (local coordinates).</summary>
        public static Vector2 FootprintHalf(Crashverse.Data.LayoutData lay)
        {
            float hx = 2f, hz = 2f;
            if (lay?.blocks != null)
                foreach (var b in lay.blocks)
                {
                    if (b?.p == null || b.s == null || b.p.Length < 3 || b.s.Length < 3) continue;
                    hx = Mathf.Max(hx, Mathf.Abs(b.p[0]) + b.s[0] * 0.5f); hz = Mathf.Max(hz, Mathf.Abs(b.p[2]) + b.s[2] * 0.5f);
                }
            return new Vector2(hx, hz);
        }

        /// <summary>Island ellipse (centre x, radius x, radius z). Compact: hugs the layout footprint (local, relative to the
        /// structure base) with a sand margin, never reaching back to the cannon deck; otherwise the original big island.</summary>
        public static Vector3 Island(float levelDistance, Vector2 footprintHalf)
        {
            if (!IsCompact(levelDistance)) return new Vector3(BigCx, BigRx, BigRz);
            var b = StructureBase(levelDistance);
            float rx = Mathf.Clamp(footprintHalf.x + 3.0f, 4.5f, Distance(levelDistance) - 6f);
            float rz = Mathf.Clamp(footprintHalf.y * 0.55f + 2.0f, 3.2f, 7f); // decks / roofs may overhang the beach (stilts stand on it)
            return new Vector3(b.x, rx, rz);
        }
    }
}
