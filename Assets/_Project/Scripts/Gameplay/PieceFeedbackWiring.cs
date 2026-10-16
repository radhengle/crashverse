using Crashverse.Destruction;
using UnityEngine;

namespace Crashverse.Gameplay
{
    /// <summary>
    /// Wires VFX #31 hooks onto runtime-built destructibles (LevelBuilder builds blocks in code; there are no destructible prefabs).
    /// Every DestructibleBlock gets a DamageFeedback registered under PieceIds.Of(blockId), so VfxDirector can find it by
    /// ImpactEvent.pieceId. DamageFeedback lives in #31: until it is on main this compiles to a no-op unless CRASHVERSE_VFX_DAMAGE is set.
    /// Presentation only: never touches HP or physics.
    /// </summary>
    public static class PieceFeedbackWiring
    {
        public static bool Enabled
        {
            get
            {
#if CRASHVERSE_VFX_DAMAGE
                return true;
#else
                return false;
#endif
            }
        }

        public static void Attach(DestructibleBlock block)
        {
            if (block == null) return;
#if CRASHVERSE_VFX_DAMAGE
            var fb = block.GetComponent<Crashverse.VFX.DamageFeedback>();
            if (fb == null) fb = block.gameObject.AddComponent<Crashverse.VFX.DamageFeedback>();
            int mk = block.material != null ? (int)block.material.kind : 0;
            fb.material = (Crashverse.VFX.VfxMaterial)PieceIds.VfxMaterialOf(mk, block.isSteelJoint, block.explosive);
            fb.ResetFeedback();
            fb.Register(PieceIds.Of(block.blockId));
#endif
        }
    }
}
