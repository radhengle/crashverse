using System;

namespace Crashverse.VFX
{
    /// <summary>
    /// Pure damage-feedback rules for DamageFeedback (GlossyPBR _HitFlash / _DamageStep / _DarkenPerStep / _CrackColor).
    /// Art 16 Oct: cracks use the material's own dark colour; step 1 sparse edge cracks (mask R), step 2 dense break (mask G) + ~20% darkening.
    /// animation/02: hit flash 2 f (joint 4 f); damage shown in ≤2 steps. animation/07: flash ×0.4, ≥100 ms ramp under Reduce motion.
    /// </summary>
    public static class DamageRules
    {
        /// <summary>Hard cap on _HitFlash (matches the shader's own min(_HitFlash, 0.85) so screens stay readable).</summary>
        public const float FlashCap = 0.85f;
        /// <summary>Total darkening at step 2 (Art ~20%); shader applies _DarkenPerStep × step.</summary>
        public const float DarkenAtStep2 = 0.20f;
        public static float DarkenPerStep(VfxMaterial m) => m == VfxMaterial.Glass ? 0.05f : DarkenAtStep2 / 2f; // glass: tint, not mud

        /// <summary>Crack colour (0xRRGGBB): wood #6B4226, brick mortar #7A3326, steel #3A3F48 (Art); glass white-ish fracture lines (proposal).</summary>
        public static uint CrackColor(VfxMaterial m) => m switch
        {
            VfxMaterial.Wood => 0x6B4226, VfxMaterial.Brick => 0x7A3326, VfxMaterial.Steel => 0x3A3F48, VfxMaterial.SteelJoint => 0x3A3F48,
            VfxMaterial.Glass => 0xE6FAFF, _ => 0x6B4226
        };

        /// <summary>Hit-flash colour: warm white wood, white steel/glass, gold joint ring (02).</summary>
        public static uint FlashColor(VfxMaterial m) => m switch
        {
            VfxMaterial.Wood => 0xFFF6E0, VfxMaterial.SteelJoint => 0xE8B04B, _ => 0xFFFFFF
        };

        public static int FlashFrames(VfxMaterial m) => m == VfxMaterial.SteelJoint ? 4 : m == VfxMaterial.Terrain ? 0 : 2;

        /// <summary>Peak flash: tier Light 0.5 / Medium 0.7 / Heavy 0.85 (cap), ×0.4 under Reduce motion.</summary>
        public static float FlashPeak(int impactTier, bool reduceMotion)
        {
            float p = impactTier >= 2 ? FlashCap : impactTier == 1 ? 0.7f : 0.5f;
            return Math.Min(FlashCap, p * VfxRules.FlashPeak(reduceMotion));
        }

        /// <summary>Flash value at t seconds after the hit: hold-free linear decay over 2 f (or 4 f joint); RM: ≥100 ms ease-out ramp.</summary>
        public static float FlashAt(float t, float peak, int frames, bool reduceMotion)
        {
            float dur = VfxRules.FlashSeconds(frames, reduceMotion);
            if (dur <= 0f || t < 0f || t >= dur) return 0f;
            float u = t / dur;
            return reduceMotion ? peak * (1f - u) * (1f - u) : peak * (1f - u);
        }

        /// <summary>
        /// Physics crack step (PR #20 CrackSteps: 1..3 at 33/66/90% damage) to the shader's _DamageStep 0/1/2 (art bible: ≤2 visible steps).
        /// 1 → 1 (sparse), 2 and 3 → 2 (dense break). AnimEvents DAMAGE_STEP payload (1|2) passes through.
        /// </summary>
        public static int ShaderStep(int physicsStep) => physicsStep <= 0 ? 0 : physicsStep == 1 ? 1 : 2;

        /// <summary>Damage step only ever increases for a live piece (pool reset sets it back to 0).</summary>
        public static int Advance(int current, int incoming) => Math.Max(current, Math.Min(2, Math.Max(0, incoming)));

        /// <summary>ImpactEvent MaterialType (Crashverse.Sim: Wood0 Brick1 Steel2 Glass3 Terrain4) to VfxMaterial.</summary>
        public static VfxMaterial FromSimMaterial(int simMaterialType) => simMaterialType switch
        {
            0 => VfxMaterial.Wood, 1 => VfxMaterial.Brick, 2 => VfxMaterial.Steel, 3 => VfxMaterial.Glass, _ => VfxMaterial.Terrain
        };
    }
}
