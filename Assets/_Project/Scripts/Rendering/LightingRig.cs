using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crashverse.Rendering
{
    /// <summary>Runtime: one global post Volume whose profile follows the quality tier (Low = no Bloom/Vignette), post-processing
    /// enabled on the main camera, and a bright default sun/ambient/skybox ONLY when the scene has none (ArenaBuilder's
    /// Art-driven KeyLight/ambient from VisualConfig wins when present).</summary>
    public class LightingRig : MonoBehaviour
    {
        static LightingRig _inst;
        Volume _vol; int _tier = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_inst) return;
            var go = new GameObject("[LightingRig]"); DontDestroyOnLoad(go);
            _inst = go.AddComponent<LightingRig>();
        }

        void Awake()
        {
            _vol = gameObject.AddComponent<Volume>(); _vol.isGlobal = true; _vol.priority = -10; // scene volumes can override
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => ApplyScene();
            ApplyScene();
        }

        void Update() { if (RenderTiers.IndexFor(QualitySettings.names[QualitySettings.GetQualityLevel()]) != _tier) ApplyTier(); }

        void ApplyTier()
        {
            _tier = RenderTiers.IndexFor(QualitySettings.names[QualitySettings.GetQualityLevel()]);
            var res = RenderTiers.All[_tier].bloom ? RenderTiers.VolumeFullResource : RenderTiers.VolumeLowResource;
            var p = Resources.Load<VolumeProfile>(res);
            if (p == null) Debug.LogError($"[LightingRig] missing volume profile Resources/{res} (run Crashverse/Rendering/Setup Lighting + Post)");
            _vol.sharedProfile = p;
            bool soft = RenderTiers.IsSoftwareRenderer(SystemInfo.graphicsDeviceName);
            SetSsao(RenderTiers.SsaoOn(_tier) && !soft);
            if (soft && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset ua)
            {
                ua.msaaSampleCount = 1; ua.renderScale = Mathf.Min(ua.renderScale, RenderTiers.SoftwareRenderScale);
                Debug.Log($"[LightingRig] software renderer ({SystemInfo.graphicsDeviceName}): SSAO/MSAA off, renderScale {ua.renderScale:0.00}");
            }
        }

        /// <summary>SSAO lives on the shared URP renderer (LightingSetup); off on the Low tier (mobile budget).</summary>
        static void SetSsao(bool on)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; if (asset == null) return;
            var fi = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (!(fi?.GetValue(asset) is ScriptableRendererData[] list)) return;
            foreach (var rd in list)
                if (rd != null) foreach (var f in rd.rendererFeatures) if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") f.SetActive(on);
        }

        void ApplyScene()
        {
            ApplyTier();
            var cam = Camera.main;
            if (cam && cam.TryGetComponent<UniversalAdditionalCameraData>(out var d)) d.renderPostProcessing = true;
            else if (cam) cam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            bool hasDir = false;
            foreach (var l in FindObjectsOfType<Light>()) if (l.type == LightType.Directional && l.enabled) { hasDir = true; break; }
            if (!hasDir)
            {
                var sun = new GameObject("DefaultSun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.color = RenderTiers.SunColor; sun.intensity = RenderTiers.SunIntensity;
                sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(RenderTiers.SunEuler);
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = RenderTiers.AmbientSky; RenderSettings.ambientEquatorColor = RenderTiers.AmbientEquator; RenderSettings.ambientGroundColor = RenderTiers.AmbientGround;
            }
            if (RenderSettings.skybox == null) RenderSettings.skybox = Resources.Load<Material>(RenderTiers.SkyboxResource);
        }
    }
}
