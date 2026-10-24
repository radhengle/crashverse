using System;

namespace Crashverse.VFX
{
    /// <summary>Which generated texture a layer samples (Assets/_Project/VFX/Textures, vfx/tools/gen_vfx_textures.py).</summary>
    public enum VfxSheet : byte { Puff4x4 = 0, SparkStreak = 1, Ring = 2, DebrisChunks = 3, DebrisShards = 4, MuzzleFlash = 5 }
    public enum VfxBlend : byte { Alpha = 0, Additive = 1 }
    public enum VfxShape : byte { Point = 0, Sphere = 1, Hemisphere = 2, Cone = 3, FlatRing = 4 }
    public enum VfxRender : byte { Billboard = 0, Stretched = 1, Horizontal = 2 }

    /// <summary>
    /// One ParticleSystem layer, pure data (no UnityEngine), turned into a ParticleSystem by VfxParticleBuilder at runtime or by the
    /// editor menu (Crashverse/VFX/Build Effect Prefabs). Colours are 0xRRGGBB; Steps = 2-3 flat toon value steps over lifetime
    /// (Gradient mode Fixed), so puffs read as stepped toon shapes, not smoke.
    /// </summary>
    [Serializable]
    public struct VfxLayer
    {
        public string Name; public VfxSheet Sheet; public VfxBlend Blend; public VfxShape Shape; public VfxRender Render;
        public int Count;                 // burst count on Mid/High (Low via VfxRules.ParticleCount)
        public float Delay;               // s after the trigger event
        public float LifeMin, LifeMax;    // s
        public float SpeedMin, SpeedMax;  // m/s
        public float SizeMin, SizeMax;    // m
        public float SizeEnd;             // size multiplier at end of life (puff grow > 1, sparks shrink < 1)
        public float Gravity, Drag;       // gravity modifier, velocity damping 0-1
        public float ShapeRadius, ShapeAngle; // m, degrees (cone)
        public uint[] Steps;              // colour steps over lifetime (Fixed gradient), 1-3 entries
        public float Alpha;               // peak alpha (Reduce motion caps flash layers at 0.5)
        public float Intensity;           // shader _Intensity (additive/emissive); flash layers scale x0.4 under Reduce motion
        public bool IsFlash;              // flash/glow layer: Reduce motion scales intensity and alpha, stretches to ≥100 ms
        public bool LowTierOff;           // dropped on Low tier (animation/01 Perf: muzzle sparks 6->0 on Low)
        public bool RandomRotation;
        public int ScaleUpFrames;         // muzzle: 0.6->1.3 over N frames
    }

    [Serializable]
    public struct VfxRecipe
    {
        public string Id; public float Duration; public VfxLayer[] Layers;
        public int TotalParticles(int tier)
        {
            int n = 0; if (Layers == null) return 0;
            foreach (var l in Layers) if (!(tier == 0 && l.LowTierOff)) n += VfxRules.ParticleCount(l.Count, tier);
            return n;
        }
    }

    /// <summary>Effect ids (stable strings; used as pool keys and prefab names VFX_&lt;Id&gt;).</summary>
    public static class VfxIds
    {
        public const string Explosion = "Explosion", DustWood = "Dust_Wood", DustBrick = "Dust_Brick", DustTerrain = "Dust_Terrain",
            DustGlass = "Dust_Glass", SparksSteel = "Sparks_Steel", SparksJoint = "Sparks_Joint", GlassShards = "Glass_Shards",
            ImpactWood = "Impact_Wood", ImpactBrick = "Impact_Brick", MuzzleFlash = "Muzzle_Wrecker", CollapseDust = "Collapse_Dust";
        public static readonly string[] All = { Explosion, DustWood, DustBrick, DustTerrain, DustGlass, SparksSteel, SparksJoint, GlassShards, ImpactWood, ImpactBrick, MuzzleFlash, CollapseDust };
    }

    /// <summary>
    /// The CRASHVERSE effect table (VFX_Shader_Spec_v1 §5b). Colours: Art &amp; UI sign-off 16 Oct + arena_brief_v2; timings: animation/01-03;
    /// counts fit the 150-particle impact budget (Mid) and the per-material counts in animation/02.
    /// </summary>
    public static class VfxRecipes
    {
        static VfxLayer Puff(string n, int count, float life, float size, uint lit, uint mid, uint shade, float speed = 1.2f, float delay = 0f)
            => new VfxLayer { Name = n, Sheet = VfxSheet.Puff4x4, Blend = VfxBlend.Alpha, Shape = VfxShape.Hemisphere, Render = VfxRender.Billboard,
                Count = count, Delay = delay, LifeMin = life * 0.8f, LifeMax = life, SpeedMin = speed * 0.4f, SpeedMax = speed, SizeMin = size * 0.7f,
                SizeMax = size, SizeEnd = 1.6f, Gravity = -0.05f, Drag = 0.35f, ShapeRadius = 0.25f, Steps = new[] { lit, mid, shade }, Alpha = 1f,
                Intensity = 1f, RandomRotation = true };

        static VfxLayer Sparks(string n, int count, float life, uint hot, uint cool, bool lowOff = false)
            => new VfxLayer { Name = n, Sheet = VfxSheet.SparkStreak, Blend = VfxBlend.Additive, Shape = VfxShape.Hemisphere, Render = VfxRender.Stretched,
                Count = count, LifeMin = life * 0.6f, LifeMax = life, SpeedMin = 4f, SpeedMax = 9f, SizeMin = 0.05f, SizeMax = 0.09f, SizeEnd = 0.3f,
                Gravity = 1.2f, Drag = 0.1f, ShapeRadius = 0.05f, Steps = new[] { 0xFFFFFFu, hot, cool }, Alpha = 1f, Intensity = 1.6f, LowTierOff = lowOff };

        static VfxLayer Debris(string n, VfxSheet sheet, int count, float life, uint body, uint face, float size = 0.14f)
            => new VfxLayer { Name = n, Sheet = sheet, Blend = VfxBlend.Alpha, Shape = VfxShape.Hemisphere, Render = VfxRender.Billboard,
                Count = count, LifeMin = life * 0.7f, LifeMax = life, SpeedMin = 2.5f, SpeedMax = 5.5f, SizeMin = size * 0.6f, SizeMax = size, SizeEnd = 0.8f,
                Gravity = 2.2f, Drag = 0.05f, ShapeRadius = 0.1f, Steps = new[] { face, body }, Alpha = 1f, Intensity = 1f, RandomRotation = true };

        static VfxLayer Flash(string n, VfxSheet sheet, float life, float size, uint col, float intensity)
            => new VfxLayer { Name = n, Sheet = sheet, Blend = VfxBlend.Additive, Shape = VfxShape.Point, Render = VfxRender.Billboard, Count = 1,
                LifeMin = life, LifeMax = life, SizeMin = size, SizeMax = size, SizeEnd = 1.3f, Steps = new[] { col }, Alpha = 1f, Intensity = intensity, IsFlash = true };

        static VfxLayer Ring(string n, float life, float size, uint col, float alpha)
            => new VfxLayer { Name = n, Sheet = VfxSheet.Ring, Blend = VfxBlend.Additive, Shape = VfxShape.Point, Render = VfxRender.Horizontal, Count = 1,
                LifeMin = life, LifeMax = life, SizeMin = size * 0.2f, SizeMax = size * 0.2f, SizeEnd = 5f, Steps = new[] { col, col }, Alpha = alpha, Intensity = 1f, IsFlash = true };

        public static VfxRecipe Get(string id)
        {
            switch (id)
            {
                // EXPLODE_CRATE @ 6 f/100 ms after swell (02): red/white fireball 400 ms (toon puffs, 3 value steps), ring, debris, smoke 1.2 s.
                case VfxIds.Explosion: return R(id, 1.2f,
                    Flash("core", VfxSheet.Puff4x4, 0.10f, 1.6f, 0xFFF6E0, 1.8f),
                    Puff("fireball", 10, 0.40f, 1.1f, 0xFFE27A, 0xFF7A1A, 0xE0402A, speed: 3.0f),
                    Ring("shock", 0.30f, 4.0f, 0xFFF6E0, 0.8f),
                    Debris("chunks", VfxSheet.DebrisChunks, 8, 0.9f, 0x6B4226, 0xF5C994),
                    Puff("smoke", 8, 1.2f, 1.3f, 0xE8E2DA, 0xB9B3AE, 0x8C8A96, speed: 0.8f, delay: 0.15f));
                case VfxIds.DustWood: return R(id, 0.8f, Puff("dust", 6, 0.8f, 0.6f, 0xE6C79A, 0xCFA779, 0xA9845C));
                case VfxIds.DustBrick: return R(id, 0.8f, Puff("dust", 6, 0.8f, 0.6f, 0xEBC3A3, 0xD9A37E, 0xB07B5C));
                case VfxIds.DustTerrain: return R(id, 0.6f, Puff("dust", 6, 0.4f, 0.5f, 0xE0AE75, 0xC98A4B, 0x9E6A38));
                case VfxIds.DustGlass: return R(id, 0.5f, Puff("glint", 4, 0.4f, 0.3f, 0xFFFFFF, 0xE6F7FF, 0xBFE3F2, speed: 0.6f));
                // 02: wood 12 particles 400 ms; brick 16 / 600 ms; steel 10 / 300 ms (BREAK_STEEL 24); joint 24 + gold ring 4 f.
                case VfxIds.ImpactWood: return R(id, 0.5f, Flash("hit", VfxSheet.Puff4x4, 0.033f, 0.5f, 0xFFF6E0, 1f),
                    Debris("splinters", VfxSheet.DebrisChunks, 6, 0.4f, 0xF5C994, 0xFFE3BC, 0.12f), Puff("dust", 6, 0.4f, 0.45f, 0xE6C79A, 0xCFA779, 0xA9845C));
                case VfxIds.ImpactBrick: return R(id, 0.7f, Flash("hit", VfxSheet.Puff4x4, 0.033f, 0.5f, 0xFFF6E0, 1f),
                    Debris("chunks", VfxSheet.DebrisChunks, 8, 0.6f, 0xD2664C, 0xE9997C), Puff("dust", 8, 0.6f, 0.5f, 0xEBC3A3, 0xD9A37E, 0xB07B5C));
                case VfxIds.SparksSteel: return R(id, 0.4f, Flash("hit", VfxSheet.Puff4x4, 0.033f, 0.4f, 0xFFFFFF, 1.2f),
                    Sparks("sparks", 7, 0.3f, 0xFFD27A, 0xFF9A3A), Debris("bits", VfxSheet.DebrisChunks, 3, 0.3f, 0x3A3F48, 0x6A7380, 0.06f));
                case VfxIds.SparksJoint: return R(id, 0.4f, Ring("gold_ring", 0.066f, 1.2f, 0xE8B04B, 1f),
                    Sparks("sparks", 24, 0.3f, 0xFFD27A, 0xE8B04B));
                // Glass (new material family, ImpactEvent MaterialType.Glass): glossy shards with a white glint edge + tiny glint puff. Art: no glow, intensity < bloom threshold 1.0.
                case VfxIds.GlassShards: return R(id, 0.7f, Flash("hit", VfxSheet.Puff4x4, 0.033f, 0.4f, 0xFFFFFF, 0.9f),
                    Debris("shards", VfxSheet.DebrisShards, 10, 0.55f, 0x9FD8EE, 0xE6FAFF, 0.12f), Puff("glint", 4, 0.3f, 0.25f, 0xFFFFFF, 0xE6F7FF, 0xBFE3F2, speed: 0.6f));
                // ANIM_WRECKER_FIRE f0: 3 frames (50 ms) scale 0.6->1.3, additive quad + 6 sparks (01). Art: Core cyan body, white-hot centre, gold rim.
                case VfxIds.MuzzleFlash:
                {
                    var f = Flash("flash", VfxSheet.MuzzleFlash, 0.05f, 0.9f, 0xFFFFFF, 2.5f); f.ScaleUpFrames = 3; f.SizeEnd = 1.3f / 0.6f;
                    var s = Sparks("sparks", 6, 0.2f, 0xE0FBFF, 0x22D3EE, lowOff: true); s.Shape = VfxShape.Cone; s.ShapeAngle = 18f; s.Gravity = 0.3f;
                    var smoke = Puff("smoke", 3, 0.5f, 0.35f, 0xF2F6FA, 0xCAD3DD, 0x9AA3AF, speed: 0.6f, delay: 0.033f); // ANIM_WRECKER_MUZZLE_SMOKE 2 f
                    return R(id, 0.6f, f, s, smoke);
                }
                case VfxIds.CollapseDust: { var p = Puff("ring_dust", 18, 1.2f, 0.9f, 0xFFFFFF, 0xD9D9D9, 0xA6A6A6, speed: 2.2f) /* grey steps, tinted by DustTint */; p.Shape = VfxShape.FlatRing; p.ShapeRadius = 1.2f; return R(id, 1.2f, p); }
                default: return default;
            }
        }

        static VfxRecipe R(string id, float duration, params VfxLayer[] layers) => new VfxRecipe { Id = id, Duration = duration, Layers = layers };

        /// <summary>Dust recipe tinted per material (COLLAPSE_LAND / DEBRIS_BURST).</summary>
        public static string DustFor(VfxMaterial m) => m switch
        {
            VfxMaterial.Wood => VfxIds.DustWood, VfxMaterial.Brick => VfxIds.DustBrick, VfxMaterial.Glass => VfxIds.DustGlass, _ => VfxIds.DustTerrain
        };

        /// <summary>Material dust tint for the grey-stepped collapse ring (Art: wood #CFA779, brick #D9A37E, steel bits #3A3F48 -> light grey dust, terrain #C98A4B).</summary>
        public static uint DustTint(VfxMaterial m) => m switch
        {
            VfxMaterial.Wood => 0xCFA779, VfxMaterial.Brick => 0xD9A37E, VfxMaterial.Steel => 0xB8BEC6, VfxMaterial.SteelJoint => 0xB8BEC6,
            VfxMaterial.Glass => 0xE6F7FF, _ => 0xC98A4B
        };

        /// <summary>Impact/break effect per material.</summary>
        public static string ImpactFor(VfxMaterial m, bool isBreak) => m switch
        {
            VfxMaterial.Wood => VfxIds.ImpactWood, VfxMaterial.Brick => VfxIds.ImpactBrick, VfxMaterial.Steel => VfxIds.SparksSteel,
            VfxMaterial.SteelJoint => VfxIds.SparksJoint, VfxMaterial.Glass => VfxIds.GlassShards, VfxMaterial.Explosive => VfxIds.Explosion,
            _ => VfxIds.DustTerrain
        };

        /// <summary>Reduce motion for a flash layer (animation/07): intensity x0.4, alpha ≤0.5, lifetime ≥100 ms ramp. Counts unchanged.</summary>
        public static VfxLayer ApplyReduceMotion(VfxLayer l, bool reduceMotion)
        {
            if (!reduceMotion || !l.IsFlash) return l;
            l.Intensity *= VfxRules.FlashPeak(true); l.Alpha = Math.Min(l.Alpha, VfxRules.RmMaxAlpha);
            l.LifeMin = Math.Max(l.LifeMin, 0.1f); l.LifeMax = Math.Max(l.LifeMax, 0.1f); l.ScaleUpFrames = 0; l.SizeEnd = Math.Min(l.SizeEnd, 1.1f);
            return l;
        }
    }
}
