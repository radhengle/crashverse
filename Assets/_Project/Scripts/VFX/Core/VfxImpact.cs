namespace Crashverse.VFX
{
    /// <summary>
    /// VFX-side copy of the fields VFX reads from Crashverse.Sim.ImpactEvent (PR #18/#20, AnimEventBus "PHYS_*"). Kept in VFX so this
    /// code compiles and tests without the physics PRs; ImpactEventAdapter fills it from the real payload. Ints mirror the Sim enums:
    /// Kind 0 Impact, 1 Crack, 2 Break, 3 JointSnap, 4 CollapseStart, 5 CollapseEnd; Tier 0 Light, 1 Medium, 2 Heavy;
    /// Source 0 Cannon, 1 PowerUp, 2 Debris, 3 Explosion.
    /// </summary>
    public struct VfxImpact
    {
        public int Kind, Tier, Source, PieceId, DamageStep;
        public VfxMaterial MaterialA, MaterialB, Dominant;
        public float X, Y, Z, Impulse;
        public const int KImpact = 0, KCrack = 1, KBreak = 2, KJointSnap = 3, KCollapseStart = 4, KCollapseEnd = 5;
        public const int SrcCannon = 0, SrcPowerUp = 1, SrcDebris = 2, SrcExplosion = 3;
    }

    public struct VfxRoute
    {
        public string EffectId; public VfxMaterial Material; public bool RateLimited; public bool HitFlash; public int DamageStep; public uint Tint; // 0 = untinted
        public static readonly VfxRoute None = default;
    }

    /// <summary>Pure routing ImpactEvent → effect + damage feedback. Unit-tested in Crashverse.VFX.Tests.</summary>
    public static class VfxImpactRouting
    {
        /// <summary>Per-piece dust override for a routed effect: only when the effect is for the piece's own material and that material is Stone
        /// (Roof = Stone + terracotta dust, PM). A material assigned in code that isn't Stone therefore never picks up a roof tint. 0 = none.</summary>
        public static uint DustOverrideFor(in VfxRoute r, VfxMaterial pieceMaterial, uint pieceDustOverride) =>
            r.EffectId != null && pieceDustOverride != 0 && pieceMaterial == VfxMaterial.Stone && r.Material == VfxMaterial.Stone ? pieceDustOverride : 0u;

        /// <summary>Struck material: A unless A is ground/terrain (then B).</summary>
        public static VfxMaterial Struck(in VfxImpact e) => e.MaterialA == VfxMaterial.Terrain ? e.MaterialB : e.MaterialA;

        public static VfxRoute Route(in VfxImpact e)
        {
            var m = Struck(e);
            switch (e.Kind)
            {
                case VfxImpact.KImpact:
                    // Light debris rattle: no particles (budget + readability); audio still plays. Hit flash only for cannon/power-up/explosion.
                    if (e.Source == VfxImpact.SrcDebris && e.Tier == 0) return VfxRoute.None;
                    return new VfxRoute { EffectId = e.Source == VfxImpact.SrcDebris ? VfxRecipes.DustFor(m) : VfxRecipes.ImpactFor(m, false), Material = m,
                                          RateLimited = e.Source == VfxImpact.SrcDebris, HitFlash = e.Source != VfxImpact.SrcDebris };
                case VfxImpact.KCrack:
                    return new VfxRoute { Material = m, DamageStep = DamageRules.ShaderStep(e.DamageStep) };
                case VfxImpact.KBreak:
                    return new VfxRoute { EffectId = VfxRecipes.ImpactFor(m, true), Material = m, RateLimited = true }; // 03 beat 4: 3 per 100 ms
                case VfxImpact.KJointSnap:
                    return new VfxRoute { EffectId = VfxIds.SparksJoint, Material = VfxMaterial.SteelJoint, RateLimited = true, HitFlash = true };
                case VfxImpact.KCollapseStart:
                    return new VfxRoute { EffectId = VfxRecipes.DustFor(e.Dominant), Material = e.Dominant, RateLimited = true };
                case VfxImpact.KCollapseEnd:
                    return new VfxRoute { EffectId = VfxIds.CollapseDust, Material = e.Dominant, RateLimited = false, Tint = VfxRecipes.DustTint(e.Dominant) };
                default: return VfxRoute.None;
            }
        }
    }
}
