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
                sun.color = new Color(1f, 0.92f, 0.80f); sun.intensity = 1.55f;
                sun.transform.rotation = Quaternion.Euler(40f, 65f, 0f); sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.75f;
            }
            foreach (var l in Object.FindObjectsOfType<Light>()) if (l.name == "RimLight") { l.intensity = 0.25f; l.color = new Color(0.75f, 0.85f, 1f); }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.55f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.46f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.18f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = KingdomMaterials.HorizonColor; RenderSettings.fogStartDistance = 70f; RenderSettings.fogEndDistance = 420f;
            var sky = KingdomMaterials.Sky;
            if (sky != null && Camera.main != null) { RenderSettings.skybox = sky; Camera.main.clearFlags = CameraClearFlags.Skybox; }
            Mood(false);
        }

        /// <summary>Hard level / Daily Boss: stormy dusk over the King's palace (L10 concept) — darker sky and sea, navy fog.</summary>
        static void Mood(bool dark)
        {
            var sky = KingdomMaterials.Sky; var w = KingdomMaterials.Water;
            var hz = dark ? new Color(0.30f, 0.33f, 0.42f) : KingdomMaterials.HorizonColor;
            if (sky != null) { sky.SetFloat("_Exposure", dark ? 0.6f : 1.05f); sky.SetColor("_HorizonColor", hz); }
            if (w != null)
            {
                w.SetColor("_HorizonColor", hz);
                w.SetColor("_ShallowColor", dark ? new Color(0.10f, 0.42f, 0.45f) : new Color(0.18f, 0.86f, 0.84f));
                w.SetColor("_MidColor", dark ? new Color(0.04f, 0.25f, 0.36f) : new Color(0.05f, 0.58f, 0.78f));
                w.SetColor("_DeepColor", dark ? new Color(0.02f, 0.10f, 0.22f) : new Color(0.02f, 0.26f, 0.55f));
                w.SetFloat("_WaveAmp", dark ? 0.22f : 0.12f);
            }
            if (dark) { RenderSettings.fogColor = hz; RenderSettings.fogStartDistance = 45f; RenderSettings.fogEndDistance = 300f; }
        }

        static void OnRun(GameObject root, GameSession s, Crashverse.Weapons.WreckerBuilder wb, RunContext ctx)
        {
            if (root == null || s == null) return;
            string levelId = ctx?.level != null ? ctx.level.id : (ctx != null && ctx.mode == RunMode.DailyBoss ? "dailyboss" : "");
            bool dark = ctx != null && (ctx.IsHard || ctx.mode == RunMode.DailyBoss);
            bool palace = levelId.EndsWith("l10");
            Mood(dark || palace);
            if (palace && !dark && RenderSettings.sun != null) { RenderSettings.sun.intensity = 1.2f; RenderSettings.sun.color = new Color(0.85f, 0.88f, 1f); }
            try
            {
                KingdomEnvironment.Build(root.transform, wb != null ? wb.transform.position : new Vector3(-20f, 0f, 0f), s.structureBase, palace, dark);
                CannonDresser.Dress(wb);
                if (s.builder != null) KingdomTheme.Apply(s.builder.Blocks, levelId);

                var go = new GameObject("VfxDirector"); go.SetActive(false); go.transform.SetParent(root.transform, false);
                _vfx = go.AddComponent<VfxDirector>();
                _vfx.tier = SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3000 ? Crashverse.Feel.QualityTier.Low : Crashverse.Feel.QualityTier.Mid;
                _vfx.muzzleSocket = wb != null ? wb.projectileSpawn : null;
                go.SetActive(true);

                if (ctx != null && ctx.mode == RunMode.Campaign && s.builder != null)
                    HiddenCore.Attach(s.builder.Blocks, (int)(ctx.seed ^ (ctx.seed >> 32)), levelId, root.transform);
            }
            catch (System.Exception e) { Debug.LogError("[Kingdom] run dressing failed: " + e); }
        }
    }
}
