using UnityEngine;

namespace Crashverse.Rendering
{
    /// <summary>Single source of truth for the per-tier "bright glossy mobile" look. UrpSetup (editor) applies it to URP_{Low,Mid,High};
    /// tests assert the generated assets match. Change values here, not in the assets.</summary>
    public struct RenderTier
    {
        public string name;
        public bool hdr; public int msaa; public float renderScale;
        public bool mainShadows; public int shadowResolution; public float shadowDistance; public int cascades; public bool softShadows;
        public int additionalLights; // 0 = disabled, else per-pixel with this per-object limit
        public bool srpBatcher;
        public bool bloom; public bool vignette;
    }

    public static class RenderTiers
    {
        public static readonly RenderTier[] All =
        {
            new RenderTier { name = "Low",  hdr = false, msaa = 1, renderScale = 0.75f, mainShadows = false, shadowResolution = 512,  shadowDistance = 15f, cascades = 1, softShadows = false, additionalLights = 0, srpBatcher = true, bloom = false, vignette = false },
            new RenderTier { name = "Mid",  hdr = true,  msaa = 2, renderScale = 0.9f,  mainShadows = true,  shadowResolution = 1024, shadowDistance = 30f, cascades = 1, softShadows = false, additionalLights = 2, srpBatcher = true, bloom = true,  vignette = true  },
            new RenderTier { name = "High", hdr = true,  msaa = 4, renderScale = 1.0f,  mainShadows = true,  shadowResolution = 2048, shadowDistance = 50f, cascades = 2, softShadows = true,  additionalLights = 4, srpBatcher = true, bloom = true,  vignette = true  },
        };

        public static int IndexFor(string qualityName)
        {
            var n = (qualityName ?? "").ToLowerInvariant();
            return n.Contains("low") ? 0 : n.Contains("high") ? 2 : 1;
        }

        public const string VolumeLowResource = "Lighting/VP_Global_Low";
        public const string VolumeFullResource = "Lighting/VP_Global";
        public const string SkyboxResource = "Lighting/M_Sky_Bright";
        public static readonly Color SunColor = new Color(1f, 0.96f, 0.88f);
        public const float SunIntensity = 1.25f;
        public static readonly Vector3 SunEuler = new Vector3(50f, -35f, 0f);
        public static readonly Color AmbientSky = new Color(0.78f, 0.86f, 1f), AmbientEquator = new Color(0.86f, 0.84f, 0.80f), AmbientGround = new Color(0.55f, 0.52f, 0.48f);
    }
}
