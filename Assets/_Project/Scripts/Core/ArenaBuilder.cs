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
            var vc = VisualConfig.Current;
            Paint(island.AddComponent<MeshRenderer>(), vc.grassHex, vc.grassSmoothness);
            island.AddComponent<MeshCollider>().sharedMesh = mesh;

            var rock = new GameObject("IslandRock");
            rock.transform.SetParent(transform, false);
            rock.AddComponent<MeshFilter>().sharedMesh = ProceduralMesh.Cone(IslandLength * 0.5f, IslandWidth * 0.5f, -0.6f, -16f, 48);
            Paint(rock.AddComponent<MeshRenderer>(), vc.cliffHex, vc.cliffSmoothness); // cliff colour (indestructible, Art v2)

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder); RenderMaterials.Apply(pad);
            pad.name = "WreckerPad";
            pad.transform.SetParent(transform, false);
            pad.transform.position = new Vector3(Crashverse.Weapons.WreckerSpec.WreckerX, 0.02f, 0f);
            pad.transform.localScale = new Vector3(6f, 0.02f, 5f);
            Destroy(pad.GetComponent<Collider>());
            Paint(pad.GetComponent<Renderer>(), vc.padHex, 0.3f);

            var kp = new GameObject("KillPlane").AddComponent<KillPlane>();
            kp.transform.SetParent(transform, false);
            kp.killY = KillY;

            AddIslandDetails(transform, vc);
            Lighting();
        }

        /// <summary>First visual art pass: layered waterfall, satellite islets, stylized palms and soft cloud clusters.</summary>
        static void AddIslandDetails(Transform root, VisualConfig vc)
        {
            // Waterfall ribbons cascade from the north-west rim; deliberately outside the firing lane.
            for (int i = 0; i < 3; i++)
            {
                var fall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                RenderMaterials.Apply(fall);
                fall.name = "WaterfallRibbon_" + i;
                fall.transform.SetParent(root, false);
                fall.transform.position = new Vector3(-13.5f + i * 0.7f, -2.4f, 10.8f + i * 0.2f);
                fall.transform.rotation = Quaternion.Euler(0f, 0f, -4f + i * 4f);
                fall.transform.localScale = new Vector3(0.55f, 5.1f, 0.12f);
                Paint(fall.GetComponent<Renderer>(), i == 1 ? "#E6FBFF" : "#5CC8F0", 0.72f);
                var fc = fall.GetComponent<Collider>(); if (fc != null) Destroy(fc);
            }

            // Small floating islands add depth behind the playable platform.
            AddSatellite(root, new Vector3(-22f, -1.8f, 17f), new Vector3(4.4f, 1.2f, 2.5f), vc);
            AddSatellite(root, new Vector3(18f, -2.5f, 18.5f), new Vector3(3.3f, 0.95f, 2f), vc);
            AddSatellite(root, new Vector3(26f, -3.2f, -12f), new Vector3(2.6f, 0.8f, 1.6f), vc);

            // Palm-like trees frame the arena perimeter; the central target lane remains clear.
            Vector3[] trees =
            {
                new Vector3(-25f, 0f, -7.5f), new Vector3(-18f, 0f, 8.5f),
                new Vector3(-5f, 0f, 9.5f), new Vector3(8f, 0f, 9.8f),
                new Vector3(21f, 0f, 6.5f), new Vector3(26f, 0f, -4.5f)
            };
            for (int i = 0; i < trees.Length; i++) AddPalm(root, trees[i], i);

            // Puffy cloud clusters provide a readable horizon silhouette.
            AddCloud(root, new Vector3(-20f, 8.5f, 19f), 1.0f);
            AddCloud(root, new Vector3(2f, 10f, 22f), 1.2f);
            AddCloud(root, new Vector3(22f, 7.8f, 18f), 0.9f);
        }

        static void AddSatellite(Transform root, Vector3 pos, Vector3 scale, VisualConfig vc)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            RenderMaterials.Apply(rock); rock.name = "SatelliteIsland_Rock";
            rock.transform.SetParent(root, false); rock.transform.position = pos;
            rock.transform.localScale = scale;
            Paint(rock.GetComponent<Renderer>(), vc.cliffHex, vc.cliffSmoothness);
            var c = rock.GetComponent<Collider>(); if (c != null) Destroy(c);

            var turf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            RenderMaterials.Apply(turf); turf.name = "SatelliteIsland_Grass";
            turf.transform.SetParent(root, false);
            turf.transform.position = pos + Vector3.up * (scale.y * 0.42f);
            turf.transform.localScale = new Vector3(scale.x * 0.98f, scale.y * 0.34f, scale.z * 0.98f);
            Paint(turf.GetComponent<Renderer>(), vc.grassHex, vc.grassSmoothness);
            c = turf.GetComponent<Collider>(); if (c != null) Destroy(c);
        }

        static void AddPalm(Transform root, Vector3 pos, int index)
        {
            float height = 2.1f + (index % 3) * 0.35f;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RenderMaterials.Apply(trunk); trunk.name = "PalmTrunk";
            trunk.transform.SetParent(root, false); trunk.transform.position = pos + Vector3.up * height * 0.5f;
            trunk.transform.localScale = new Vector3(0.22f, height * 0.5f, 0.22f);
            trunk.transform.rotation = Quaternion.Euler(0f, 0f, (index % 2 == 0) ? -7f : 7f);
            Paint(trunk.GetComponent<Renderer>(), "#C98A4B", 0.25f);
            var c = trunk.GetComponent<Collider>(); if (c != null) Destroy(c);

            for (int leaf = 0; leaf < 5; leaf++)
            {
                float angle = leaf * 72f + index * 19f;
                var frond = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                RenderMaterials.Apply(frond); frond.name = "PalmFrond";
                frond.transform.SetParent(root, false);
                frond.transform.position = pos + Vector3.up * (height + 0.05f) +
                    new Vector3(Mathf.Cos(angle * (Mathf.PI / 180f)) * 0.72f, -0.18f, Mathf.Sin(angle * (Mathf.PI / 180f)) * 0.72f);
                frond.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(angle * (Mathf.PI / 180f)) * 22f);
                frond.transform.localScale = new Vector3(0.95f, 0.18f, 0.34f);
                Paint(frond.GetComponent<Renderer>(), leaf % 2 == 0 ? "#7BE36A" : "#2F7D32", 0.32f);
                c = frond.GetComponent<Collider>(); if (c != null) Destroy(c);
            }
        }

        static void AddCloud(Transform root, Vector3 pos, float scale)
        {
            for (int i = 0; i < 4; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                RenderMaterials.Apply(puff); puff.name = "CloudPuff";
                puff.transform.SetParent(root, false);
                puff.transform.position = pos + new Vector3((i - 1.5f) * 1.05f * scale, (i % 2) * 0.38f * scale, 0f);
                puff.transform.localScale = new Vector3(1.35f, 0.72f, 0.62f) * scale;
                Paint(puff.GetComponent<Renderer>(), i == 0 ? "#CFE3F7" : "#FFFFFF", 0.12f);
                var c = puff.GetComponent<Collider>(); if (c != null) Destroy(c);
            }
        }

        public static void Lighting()
        {
            var vc = VisualConfig.Current; // PM: lighting is data (VisualConfig)
            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.type = LightType.Directional; key.color = VisualConfig.C(vc.keyLightHex); key.intensity = vc.keyIntensity;
            key.shadows = LightShadows.Soft; key.shadowStrength = vc.keyShadowStrength;
            key.transform.rotation = Quaternion.Euler(vc.keyEuler.x, vc.keyEuler.y, vc.keyEuler.z);
            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.type = LightType.Directional; rim.color = VisualConfig.C(vc.rimLightHex); rim.intensity = vc.rimIntensity; rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(vc.rimEuler.x, vc.rimEuler.y, vc.rimEuler.z);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = VisualConfig.C(vc.ambientSkyHex);
            RenderSettings.ambientEquatorColor = VisualConfig.C(vc.ambientEquatorHex);
            RenderSettings.ambientGroundColor = VisualConfig.C(vc.ambientGroundHex);
            RenderSettings.ambientIntensity = vc.ambientIntensity;
            RenderSettings.fog = vc.fog; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = VisualConfig.C(vc.fogHex);
            RenderSettings.fogStartDistance = vc.fogStart; RenderSettings.fogEndDistance = vc.fogEnd;
            if (Camera.main != null) { Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = VisualConfig.C(vc.skyClearHex); }
        }

        static void Paint(Renderer r, string hex, float smooth)
        {
            RenderMaterials.Apply(r);
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
