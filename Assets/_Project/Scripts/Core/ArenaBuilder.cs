using Crashverse.Destruction;
using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// Art v2 arena: floating island (top 60×25u ellipse at y=0, tapered rock underside), sky all around, kill plane below,
    /// late-morning lighting from arena_brief_v2 / CHANGES.md. Post (bloom/grade) needs a URP Volume profile in-editor (not code).
    /// </summary>
    public class ArenaBuilder : MonoBehaviour
    {
        public const float IslandLength = 60f, IslandWidth = 25f, KillY = -6f;
        public static float GrassY => 0f;

        public void Build()
        {
            var island = new GameObject("FloatingIsland");
            island.transform.SetParent(transform, false);
            var mesh = ProceduralMesh.IslandTop(IslandLength * 0.5f, IslandWidth * 0.5f, 0.6f, 48);
            island.AddComponent<MeshFilter>().sharedMesh = mesh;
            Paint(island.AddComponent<MeshRenderer>(), "7ED957", 0.5f);
            island.AddComponent<MeshCollider>().sharedMesh = mesh;

            var rock = new GameObject("IslandRock");
            rock.transform.SetParent(transform, false);
            rock.AddComponent<MeshFilter>().sharedMesh = ProceduralMesh.Cone(IslandLength * 0.5f, IslandWidth * 0.5f, -0.6f, -16f, 48);
            Paint(rock.AddComponent<MeshRenderer>(), "C98A4B", 0.15f); // cliff colour (indestructible, Art v2)

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "WreckerPad";
            pad.transform.SetParent(transform, false);
            pad.transform.position = new Vector3(Crashverse.Weapons.WreckerSpec.WreckerX, 0.02f, 0f);
            pad.transform.localScale = new Vector3(6f, 0.02f, 5f);
            Destroy(pad.GetComponent<Collider>());
            Paint(pad.GetComponent<Renderer>(), "E6C88F", 0.3f);

            var kp = new GameObject("KillPlane").AddComponent<KillPlane>();
            kp.transform.SetParent(transform, false);
            kp.killY = KillY;

            Lighting();
        }

        public static void Lighting()
        {
            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.type = LightType.Directional; key.color = MaterialDefinition.Hex("FFF2D6"); key.intensity = 1.4f;
            key.shadows = LightShadows.Soft; key.shadowStrength = 0.6f;
            key.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.type = LightType.Directional; rim.color = Color.white; rim.intensity = 0.35f; rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(25f, 150f, 0f); // back-left
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = MaterialDefinition.Hex("8CC8FF");
            RenderSettings.ambientEquatorColor = MaterialDefinition.Hex("DCEFFF");
            RenderSettings.ambientGroundColor = MaterialDefinition.Hex("6FAE5A");
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = MaterialDefinition.Hex("CFEAFF");
            RenderSettings.fogStartDistance = 80f; RenderSettings.fogEndDistance = 250f;
            if (Camera.main != null) { Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = MaterialDefinition.Hex("7CC4FF"); }
        }

        static void Paint(Renderer r, string hex, float smooth)
        {
            var b = new MaterialPropertyBlock();
            var c = MaterialDefinition.Hex(hex);
            b.SetColor("_BaseColor", c); b.SetColor("_Color", c); b.SetFloat("_Smoothness", smooth);
            r.SetPropertyBlock(b);
        }
    }

    public static class ProceduralMesh
    {
        /// <summary>Elliptical slab: top cap at y=0, bottom at -thickness.</summary>
        public static Mesh IslandTop(float rx, float rz, float thickness, int seg)
        {
            var v = new Vector3[seg * 2 + 2]; var tris = new int[seg * 12];
            v[0] = Vector3.zero; v[1] = new Vector3(0f, -thickness, 0f);
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v[2 + i] = new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
                v[2 + seg + i] = new Vector3(Mathf.Cos(a) * rx, -thickness, Mathf.Sin(a) * rz);
            }
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % seg, a2 = a + seg, b2 = b + seg;
                tris[t++] = 0; tris[t++] = b; tris[t++] = a;          // top
                tris[t++] = 1; tris[t++] = a2; tris[t++] = b2;        // bottom
                tris[t++] = a; tris[t++] = b; tris[t++] = a2;         // side
                tris[t++] = b; tris[t++] = b2; tris[t++] = a2;
            }
            var m = new Mesh { name = "IslandTop" };
            m.vertices = v; m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Tapered rock: ellipse at yTop narrowing to a point at yTip.</summary>
        public static Mesh Cone(float rx, float rz, float yTop, float yTip, int seg)
        {
            var v = new Vector3[seg + 1]; var tris = new int[seg * 3];
            v[0] = new Vector3(0f, yTip, 0f);
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v[1 + i] = new Vector3(Mathf.Cos(a) * rx, yTop, Mathf.Sin(a) * rz);
            }
            for (int i = 0; i < seg; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = 1 + i; tris[i * 3 + 2] = 1 + (i + 1) % seg; }
            var m = new Mesh { name = "IslandRock" };
            m.vertices = v; m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
