using Crashverse.Data;

namespace Crashverse.Meta
{
    /// <summary>
    /// Hero buildings (Bld_* FBX in Assets/_Project/Models/HeroBuildings/, from the 3D Modeler) sit in a slot behind the structure:
    /// one keep or gatehouse per level at most. The slot exists on every level (empty until a model is assigned).
    /// </summary>
    public static class HeroSlotRules
    {
        public static readonly string[] Kinds = { "keep", "gatehouse" };
        public const string FbxPrefix = "Bld_";
        /// <summary>GD ruling: hero buildings stay clear of the shot lanes. Shots travel along +x from the Wrecker over |z| up to the
        /// widest island (9 m deep, so 4.5 m) plus the yaw spread; anything nearer the lane centre than this is pushed out sideways.</summary>
        public const float LaneClearZ = 8f;
        /// <summary>Default anchor relative to the structure base: behind it (+x, away from the Wrecker), on the ground, off to the side.</summary>
        public static readonly float[] DefaultPos = { 9f, 0f, LaneClearZ };

        public static string Validate(HeroSlot s)
        {
            if (s == null) return null; // empty slot is fine
            if (System.Array.IndexOf(Kinds, s.kind) < 0) return "hero slot kind must be keep or gatehouse";
            if (!string.IsNullOrEmpty(s.fbx) && !s.fbx.StartsWith(FbxPrefix)) return "hero building must be a " + FbxPrefix + "* model";
            if (s.pos != null && s.pos.Length != 3) return "hero slot pos needs x,y,z";
            return null;
        }

        public static float[] Position(HeroSlot s)
        {
            var p = s?.pos != null && s.pos.Length == 3 ? (float[])s.pos.Clone() : (float[])DefaultPos.Clone();
            if (System.Math.Abs(p[2]) < LaneClearZ) p[2] = p[2] < 0f ? -LaneClearZ : LaneClearZ; // never in a shot lane
            return p;
        }
        public static bool ClearOfLanes(float[] p) => p != null && p.Length == 3 && System.Math.Abs(p[2]) >= LaneClearZ;
    }
}
