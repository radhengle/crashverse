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
        /// <summary>Default anchor relative to the structure base: behind it (+x, away from the Wrecker), on the ground.</summary>
        public static readonly float[] DefaultPos = { 9f, 0f, 0f };

        public static string Validate(HeroSlot s)
        {
            if (s == null) return null; // empty slot is fine
            if (System.Array.IndexOf(Kinds, s.kind) < 0) return "hero slot kind must be keep or gatehouse";
            if (!string.IsNullOrEmpty(s.fbx) && !s.fbx.StartsWith(FbxPrefix)) return "hero building must be a " + FbxPrefix + "* model";
            if (s.pos != null && s.pos.Length != 3) return "hero slot pos needs x,y,z";
            return null;
        }

        public static float[] Position(HeroSlot s) => s?.pos != null && s.pos.Length == 3 ? s.pos : DefaultPos;
    }
}
