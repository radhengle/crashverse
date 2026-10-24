namespace Crashverse.Gameplay
{
    /// <summary>
    /// Gameplay-side piece ids for VFX #31 / Physics #20: pieceId = StableId.Of(blockId).
    /// With CRASHVERSE_SIM_IMPACT defined (after #18/#20 merge) this forwards to Crashverse.Sim.StableId so there is exactly one
    /// implementation. Until then it uses a byte-identical FNV-1a 32 copy (pinned by tests to #20's values) so ids already match.
    /// Engine-free.
    /// </summary>
    public static class PieceIds
    {
        public const int None = -1;

        public static int Of(string blockId)
        {
#if CRASHVERSE_SIM_IMPACT
            return Crashverse.Sim.StableId.Of(blockId);
#else
            return Fnv(blockId);
#endif
        }

        /// <summary>Copy of #20 StableId.Of. Delete once CRASHVERSE_SIM_IMPACT is on for all builds.</summary>
        internal static int Fnv(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
                int r = (int)(h & 0x7FFFFFFF);
                return r <= 1 ? r + 2 : r;
            }
        }

        /// <summary>MaterialKind (int) → VfxMaterial (int) per VfxCore: Wood 0, Brick 1, Steel 2, SteelJoint 3, Explosive 5, Glass 6, Stone 7.
        /// MaterialKind: Wood 0, Brick 1, Steel 2, Glass 3, Stone 4.</summary>
        public static int VfxMaterialOf(int materialKind, bool steelJoint, bool explosive)
        {
            if (explosive) return 5;
            if (steelJoint) return 3;
            switch (materialKind) { case 1: return 1; case 2: return 2; case 3: return 6; case 4: return 7; default: return 0; }
        }
    }
}
