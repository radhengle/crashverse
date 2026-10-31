using Crashverse.Core;
using Crashverse.VFX;
using UnityEngine;

namespace Crashverse.Visuals
{
    /// <summary>
    /// Season 1 "Kingdom of Ironvale" look (Phase 1, design/Season1_KingStory_v1.md): wires the presentation layer into a run through
    /// two hooks — ArenaBuilder.PostBuild (lighting, sky, hide the greybox island renderers; colliders stay) and GameplayDirector.RunBuilt
    /// (ocean/island/deck/horizon, the navy-and-gold cannon, royal re-theme of the structure, pooled VFX director, hidden core).
    /// Presentation only: never read by the sim; the headless sim/tests don't load this assembly path.
    /// </summary>
    public static class KingdomVisuals
    {
        public static bool Enabled = true;
        static VfxDirector _vfx;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (!Enabled) return;
            ArenaBuilder.KingdomLook = true;
            ArenaBuilder.PostBuild -= OnArena; ArenaBuilder.PostBuild += OnArena;
            GameplayDirector.RunBuilt -= OnRun; GameplayDirector.RunBuilt += OnRun;
            HiddenCore.Vfx = () => _vfx;
        }

        static void OnArena(ArenaBuilder a)
        {
            foreach (var n in new[] { "FloatingIsland", "IslandRock", "WreckerPad" })
            {
                var t = a.transform.Find(n); if (t == null) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            Lighting();
        }

        /// <summary>Warm late-morning sun from behind-left of the cannon, cool sky fill, deeper shade (concept art look).</summary>
        public static void Lighting()
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.color = new Color(1f, 0.90f, 0.74f); sun.intensity = 1.8f;
                sun.transform.rotation = Quaternion.Euler(40f, 65f, 0f); sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f; sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f;
            }
            foreach (var l in Object.FindObjectsOfType<Light>()) if (l.name == "RimLight") { l.intensity = 0.25f; l.color = new Color(0.75f, 0.85f, 1f); }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.62f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.60f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.25f, 0.18f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = KingdomMaterials.HorizonColor; RenderSettings.fogStartDistance = 55f; RenderSettings.fogEndDistance = 380f; // atmospheric haze over the far islands
            var sky = KingdomMaterials.Sky;
            // skybox does not depend on the camera existing yet (PostBuild can run before the run camera is created); the run hook
            // re-applies the clear flags on every camera (SkyOnCameras)
            if (sky != null) RenderSettings.skybox = sky;
            SkyOnCameras();
            var shSky = Shader.Find("Crashverse/SkyPano");
            Debug.Log($"[KingdomSky] sky={(sky != null ? sky.shader.name : "null")} shader={(shSky != null ? (shSky.isSupported ? "ok" : "unsupported") : "missing")} tex={(Resources.Load<Texture2D>("Kingdom/Textures/T_K_Sky") != null)} cam={(Camera.main != null ? Camera.main.name + ":" + Camera.main.clearFlags : "none")}");
            Mood(false);
        }

        /// <summary>Hard level / Daily Boss: stormy dusk over the King's palace (L10 concept) — darker sky and sea, navy fog.</summary>
        static void SkyOnCameras()
        {
            if (RenderSettings.skybox == null || KingdomMaterials.Sky == null) return;
            foreach (var c in Camera.allCameras) if (c != null && c.cameraType == CameraType.Game && c.targetTexture == null && c.clearFlags != CameraClearFlags.Depth && c.clearFlags != CameraClearFlags.Nothing) c.clearFlags = CameraClearFlags.Skybox;
        }

        static void Mood(bool dark)
        {
            var sky = KingdomMaterials.Sky; var w = KingdomMaterials.Water;
            var hz = dark ? new Color(0.30f, 0.33f, 0.42f) : KingdomMaterials.HorizonColor;
            if (sky != null) { sky.SetFloat("_Exposure", dark ? 0.6f : 1.05f); sky.SetColor("_HorizonColor", hz); }
            if (w != null)
            {
                w.SetColor("_HorizonColor", hz);
                w.SetColor("_ShallowColor", dark ? new Color(0.10f, 0.42f, 0.45f) : new Color(0.22f, 0.95f, 0.86f));
                w.SetColor("_MidColor", dark ? new Color(0.04f, 0.25f, 0.36f) : new Color(0.04f, 0.72f, 0.82f));
                w.SetColor("_DeepColor", dark ? new Color(0.02f, 0.10f, 0.22f) : new Color(0.02f, 0.42f, 0.68f));
                w.SetFloat("_WaveAmp", dark ? 0.22f : 0.12f);
            }
            if (dark) { RenderSettings.fogColor = hz; RenderSettings.fogStartDistance = 45f; RenderSettings.fogEndDistance = 300f; }
        }

        /// <summary>Crack decals + hit flash on every structure piece, including the dressed architecture meshes (DamageFeedback is
        /// otherwise only attached when CRASHVERSE_VFX_DAMAGE is defined). The crack mask is the shipped T_CrackMask_Wedge.</summary>
        static void WireDamage(System.Collections.Generic.IReadOnlyList<Crashverse.Destruction.DestructibleBlock> blocks)
        {
            var set = VfxAssetSet.Load(); var mask = set != null ? set.crackMask : null;
            foreach (var b in blocks)
            {
                if (b == null || b.indestructible || b.role == "island") continue;
                var fb = b.GetComponent<DamageFeedback>(); if (fb == null) fb = b.gameObject.AddComponent<DamageFeedback>();
                fb.renderers = b.GetComponentsInChildren<Renderer>(false); fb.crackMask = mask;
                int mk = b.material != null ? (int)b.material.kind : 0;
                fb.material = (VfxMaterial)Crashverse.Gameplay.PieceIds.VfxMaterialOf(mk, b.isSteelJoint, b.explosive);
                fb.Register(Crashverse.Gameplay.PieceIds.Of(b.blockId));
                fb.ResetFeedback();
            }
        }

        static void OnRun(GameObject root, GameSession s, Crashverse.Weapons.WreckerBuilder wb, RunContext ctx)
        {
            if (root == null || s == null) return;
            string levelId = ctx?.level != null ? ctx.level.id : (ctx != null && ctx.mode == RunMode.DailyBoss ? "dailyboss" : "");
            bool dark = ctx != null && (ctx.IsHard || ctx.mode == RunMode.DailyBoss);
            bool palace = levelId.EndsWith("l10");
            Mood(dark || palace); SkyOnCameras();
            Debug.Log($"[KingdomSky] run: skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")} cam={(Camera.main != null ? Camera.main.name + ":" + Camera.main.clearFlags : "none")}");
            if (palace && !dark && RenderSettings.sun != null) { RenderSettings.sun.intensity = 1.2f; RenderSettings.sun.color = new Color(0.85f, 0.88f, 1f); }
            try
            {
                KingdomEnvironment.Build(root.transform, wb != null ? wb.transform.position : new Vector3(-20f, 0f, 0f), s.structureBase, palace, dark, GameplayDirector.StructureBounds(s, s.structureBase));
                CannonDresser.Dress(wb);
                if (s.builder != null) KingdomTheme.Apply(s.builder.Blocks, levelId);

                var go = new GameObject("VfxDirector"); go.SetActive(false); go.transform.SetParent(root.transform, false);
                _vfx = go.AddComponent<VfxDirector>();
                _vfx.tier = SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3000 ? Crashverse.Feel.QualityTier.Low : Crashverse.Feel.QualityTier.Mid;
                _vfx.muzzleSocket = wb != null ? wb.projectileSpawn : null;
                go.SetActive(true);

                var fx = KingdomFx.Create(root.transform, _vfx.tier == Crashverse.Feel.QualityTier.Low);
                if (fx != null && s.builder != null) fx.Watch(s.builder.Blocks);
                if (s.builder != null) WireDamage(s.builder.Blocks);

                if (ctx != null && ctx.mode == RunMode.Campaign && s.builder != null)
                    HiddenCore.Attach(s.builder.Blocks, (int)(ctx.seed ^ (ctx.seed >> 32)), levelId, root.transform);
            }
            catch (System.Exception e) { Debug.LogError("[Kingdom] run dressing failed: " + e); }
        }
    }
}
