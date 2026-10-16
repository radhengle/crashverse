using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// PM: the 3D look is data-driven so large look changes after playtesting need no code. Colours (hex), lighting, fog,
    /// materials and island palette. Bake/edit at Resources/VisualConfig.asset (Crashverse ▸ Create Visual & Camera Config).
    /// Defaults are Art v2 (midday, floating island).
    /// </summary>
    [CreateAssetMenu(fileName = "VisualConfig", menuName = "Crashverse/Visual Config", order = 31)]
    public class VisualConfig : ScriptableObject
    {
        [Header("Materials (Art v2)")]
        public string woodHex = "E3A766", brickHex = "D2664C", steelHex = "AEB6C2", roofHex = "4D7BC4";
        [Header("Island / arena")]
        public string grassHex = "7ED957", cliffHex = "C98A4B", padHex = "E6C88F", stormGrassHex = "5E7A4A", stormCloudHex = "3A3F48";
        public string bridgeHex = "A8743F", nodeHex = "F5C451", bossNodeHex = "E5484D";
        public float grassSmoothness = 0.5f, cliffSmoothness = 0.15f;
        [Header("Lighting (midday)")]
        public string keyLightHex = "FFF2D6"; public float keyIntensity = 1.4f, keyShadowStrength = 0.6f; public Vector3 keyEuler = new Vector3(50f, -35f, 0f);
        public string rimLightHex = "FFFFFF"; public float rimIntensity = 0.35f; public Vector3 rimEuler = new Vector3(25f, 150f, 0f);
        public string ambientSkyHex = "8CC8FF", ambientEquatorHex = "DCEFFF", ambientGroundHex = "6FAE5A"; public float ambientIntensity = 1.15f;
        public bool fog = true; public string fogHex = "CFEAFF"; public float fogStart = 80f, fogEnd = 250f;
        public string skyClearHex = "7CC4FF";
        [Header("Boss tells (animation/05_bosses.md)")]
        [Tooltip("Tidewarden shutter tell pulse rate. 2.4 Hz = one pulse per 25 frames, beat-locked to 144 BPM.")]
        public float tideShutterPulseHz = 2.4f;

        static VisualConfig _current;
        public static VisualConfig Current
        {
            get { if (_current == null) { _current = Resources.Load<VisualConfig>("VisualConfig"); if (_current == null) _current = CreateInstance<VisualConfig>(); } return _current; }
            set => _current = value;
        }
        public static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
    }

    /// <summary>Camera angles, FOV and offsets (gameplay orbit + island map). Edit at Resources/CameraConfig.asset.</summary>
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "Crashverse/Camera Config", order = 32)]
    public class CameraConfig : ScriptableObject
    {
        [Header("Gameplay camera (Art v2: low behind, ~15° pitch, FOV 50)")]
        public float fov = 50f, distance = 22f, yaw = 72f, pitch = 15f;
        public float minPitch = 8f, maxPitch = 45f, minYaw = 30f, maxYaw = 120f, orbitDegPerPx = 0.15f;
        [Range(0, 1)] public float lookLerpWreckerToTarget = 0.45f; public Vector3 lookOffset = new Vector3(0f, 4f, 2.5f);
        [Header("Feel (07)")]
        public float followShotWeight = 0.2f;
        [Header("Island map")]
        public float mapFov = 50f, mapHeight = 26f, mapBack = -30f, mapPitch = 38f, mapPanPadding = 4f, mapPanUnitsPerDp = 0.06f;

        static CameraConfig _current;
        public static CameraConfig Current
        {
            get { if (_current == null) { _current = Resources.Load<CameraConfig>("CameraConfig"); if (_current == null) _current = CreateInstance<CameraConfig>(); } return _current; }
            set => _current = value;
        }
    }
}
