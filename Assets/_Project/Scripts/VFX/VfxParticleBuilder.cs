using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.VFX
{
    /// <summary>Generated textures + particle shader used by the builder (assign once on VfxDirector, or the editor menu fills it).</summary>
    [System.Serializable]
    public class VfxAssets
    {
        public Shader particleShader;              // Crashverse/VfxParticle
        public Texture2D puffFlipbook;             // T_VFX_PuffFlipbook_4x4
        public Texture2D sparkStreak;              // T_VFX_SparkStreak
        public Texture2D ring;                     // T_VFX_Ring
        public Texture2D debrisAtlas;              // T_VFX_DebrisAtlas_4x2 (row 0 chunks/splinters, row 1 glass shards)
        public Texture2D muzzleFlash;              // T_VFX_MuzzleFlash_Core
        public Texture2D Get(VfxSheet s) => s switch
        {
            VfxSheet.Puff4x4 => puffFlipbook, VfxSheet.SparkStreak => sparkStreak, VfxSheet.Ring => ring,
            VfxSheet.MuzzleFlash => muzzleFlash, _ => debrisAtlas
        };
    }

    public static class VfxColor
    {
        public static Color From(uint rgb, float a = 1f) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
    }

    /// <summary>
    /// VfxRecipe → ParticleSystem hierarchy (one child per layer). Used at runtime by VfxDirector pools and by the editor menu that saves
    /// prefabs. Mobile rules: bursts only (no rate emission), world space, unscaled time (hit stop / slow-mo never stretch FX),
    /// maxParticles = burst count, maxParticleSize 0.5 screen to cap overdraw, no lights, no collision, no sub-emitters.
    /// </summary>
    public static class VfxParticleBuilder
    {
        static readonly Dictionary<long, Material> _materials = new Dictionary<long, Material>();

        public static GameObject Build(in VfxRecipe recipe, VfxAssets assets, int tier, bool reduceMotion, Transform parent = null, List<ParticleSystem> systems = null)
        {
            var root = new GameObject("VFX_" + recipe.Id);
            if (parent != null) root.transform.SetParent(parent, false);
            if (recipe.Layers == null) return root;
            foreach (var src in recipe.Layers)
            {
                if (tier == 0 && src.LowTierOff) continue;
                var l = VfxRecipes.ApplyReduceMotion(src, reduceMotion);
                var go = new GameObject(l.Name); go.transform.SetParent(root.transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                Configure(ps, go.GetComponent<ParticleSystemRenderer>(), l, recipe.Duration, tier, assets);
                systems?.Add(ps);
            }
            return root;
        }

        public static void Configure(ParticleSystem ps, ParticleSystemRenderer r, in VfxLayer l, float duration, int tier, VfxAssets assets)
        {
            if (ps == null) return;
            int count = VfxRules.ParticleCount(l.Count, tier);
            var main = ps.main;
            main.duration = Mathf.Max(0.05f, duration); main.loop = false; main.playOnAwake = false;
            main.startDelay = l.Delay;
            main.startLifetime = new ParticleSystem.MinMaxCurve(l.LifeMin, l.LifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(l.SpeedMin, l.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(l.SizeMin, l.SizeMax);
            main.startRotation = l.RandomRotation ? new ParticleSystem.MinMaxCurve(0f, 6.283f) : new ParticleSystem.MinMaxCurve(0f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, l.Alpha));
            main.gravityModifier = l.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.maxParticles = Mathf.Max(1, count);

            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var sh = ps.shape;
            sh.enabled = l.Shape != VfxShape.Point;
            sh.shapeType = l.Shape switch
            {
                VfxShape.Sphere => ParticleSystemShapeType.Sphere, VfxShape.Cone => ParticleSystemShapeType.Cone,
                VfxShape.FlatRing => ParticleSystemShapeType.Circle, _ => ParticleSystemShapeType.Hemisphere
            };
            sh.radius = Mathf.Max(0.001f, l.ShapeRadius); sh.angle = l.ShapeAngle;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(SteppedGradient(l.Steps, l.Alpha));

            var size = ps.sizeOverLifetime; size.enabled = System.Math.Abs(l.SizeEnd - 1f) > 0.01f;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, l.SizeEnd));

            if (l.Drag > 0f) { var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = l.Drag; lv.multiplyDragByParticleSize = false; }

            var tsa = ps.textureSheetAnimation;
            if (l.Sheet == VfxSheet.Puff4x4)
            {
                tsa.enabled = true; tsa.numTilesX = 4; tsa.numTilesY = 4; tsa.animation = ParticleSystemAnimationType.WholeSheet;
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            else if (l.Sheet == VfxSheet.DebrisChunks || l.Sheet == VfxSheet.DebrisShards)
            {
                tsa.enabled = true; tsa.numTilesX = 4; tsa.numTilesY = 2; tsa.animation = ParticleSystemAnimationType.SingleRow;
                tsa.rowMode = ParticleSystemAnimationRowMode.Custom; tsa.rowIndex = l.Sheet == VfxSheet.DebrisShards ? 1 : 0;
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f); tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f); // random cell, no animation
            }
            else tsa.enabled = false;

            if (r != null)
            {
                r.renderMode = l.Render switch
                {
                    VfxRender.Stretched => ParticleSystemRenderMode.Stretch, VfxRender.Horizontal => ParticleSystemRenderMode.HorizontalBillboard,
                    _ => ParticleSystemRenderMode.Billboard
                };
                if (l.Render == VfxRender.Stretched) { r.velocityScale = 0.04f; r.lengthScale = 1.5f; }
                r.maxParticleSize = 0.5f;
                r.sortingFudge = l.Blend == VfxBlend.Additive ? -10f : 0f; // additive draws after alpha puffs
                r.sharedMaterial = MaterialFor(l, assets);
            }
        }

        /// <summary>Fixed-mode gradient: flat colour steps (toon), alpha held then cut at end of life.</summary>
        public static Gradient SteppedGradient(uint[] steps, float alpha)
        {
            int n = steps == null || steps.Length == 0 ? 1 : Mathf.Min(3, steps.Length);
            var ck = new GradientColorKey[n];
            for (int i = 0; i < n; i++) ck[i] = new GradientColorKey(VfxColor.From(steps == null || steps.Length == 0 ? 0xFFFFFFu : steps[i]), (i + 1f) / n);
            var g = new Gradient { mode = GradientMode.Fixed };
            g.SetKeys(ck, new[] { new GradientAlphaKey(alpha, 0.85f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>Shared materials per (sheet, blend, intensity): ≤ ~10 materials total, SRP-batcher friendly.</summary>
        public static Material MaterialFor(in VfxLayer l, VfxAssets assets)
        {
            if (assets == null || assets.particleShader == null) return null;
            long key = ((long)l.Sheet << 40) | ((long)l.Blend << 32) | (uint)Mathf.RoundToInt(l.Intensity * 100f);
            if (_materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(assets.particleShader) { name = $"M_VFX_{l.Sheet}_{l.Blend}_{l.Intensity:0.##}" };
            m.SetTexture("_BaseMap", assets.Get(l.Sheet));
            m.SetFloat("_Intensity", l.Intensity);
            bool add = l.Blend == VfxBlend.Additive;
            m.SetFloat("_SrcBlend", add ? 1f : 5f);   // One : SrcAlpha
            m.SetFloat("_DstBlend", add ? 1f : 10f);  // One : OneMinusSrcAlpha
            m.SetFloat("_AlphaClip", add ? 0.02f : 0.1f); // toon puffs: hard silhouette, kills transparent overdraw
            _materials[key] = m;
            return m;
        }

        public static void ClearMaterialCache() => _materials.Clear();
    }
}
