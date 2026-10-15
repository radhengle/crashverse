using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// Sunnyville outskirts ground + lighting per arena_brief.md.
    /// Wrecker at x=-20; targets 25–40u away.
    /// </summary>
    public class ArenaBuilder : MonoBehaviour
    {
        public void Build()
        {
            // Ground plane 60×30
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform);
            ground.transform.position = new Vector3(0f, 0f, 0f);
            ground.transform.localScale = new Vector3(6f, 1f, 3f); // plane is 10u → 60×30
            SetColor(ground, Hex("6FAE5A"));

            // Sand pad under Wrecker
            var sand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sand.name = "SandPad";
            sand.transform.SetParent(transform);
            sand.transform.position = new Vector3(-20f, 0.05f, 0f);
            sand.transform.localScale = new Vector3(8f, 0.1f, 6f);
            SetColor(sand, Hex("E6C88F"));

            // Sea strip
            var sea = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sea.name = "Sea";
            sea.transform.SetParent(transform);
            sea.transform.position = new Vector3(0f, -0.2f, -18f);
            sea.transform.localScale = new Vector3(70f, 0.2f, 12f);
            SetColor(sea, Hex("2E6F8E"));
            Object.Destroy(sea.GetComponent<Collider>());

            SetupLighting();
            SetupCamera();
        }

        void SetupLighting()
        {
            // Key directional #FFD9A0 intensity 1.3, rotation (35, -40, 0)
            var keyGo = new GameObject("KeyLight");
            keyGo.transform.SetParent(transform);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = Hex("FFD9A0");
            key.intensity = 1.3f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.75f;
            keyGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);

            // Rim optional #8FB8FF at 0.3
            var rimGo = new GameObject("RimLight");
            rimGo.transform.SetParent(transform);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = Hex("8FB8FF");
            rim.intensity = 0.3f;
            rim.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(20f, 140f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("9FC4E8");
            RenderSettings.ambientEquatorColor = Hex("F2C99A");
            RenderSettings.ambientGroundColor = Hex("5E7A4A");
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("F6D7B0");
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 180f;

            Camera.main.backgroundColor = Hex("FFC98A");
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }
            // Angled view: Wrecker bottom-left ~20%, tower readable
            cam.transform.position = new Vector3(-12f, 12f, -18f);
            cam.transform.rotation = Quaternion.Euler(28f, 35f, 0f);
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("FFC98A");
        }

        static void SetColor(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            rend.SetPropertyBlock(block);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
