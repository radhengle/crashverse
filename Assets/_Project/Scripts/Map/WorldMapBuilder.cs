using System.Collections.Generic;
using Crashverse.Core;
using Crashverse.Data;
using Crashverse.Destruction;
using UnityEngine;

namespace Crashverse.Map
{
    /// <summary>
    /// Island world map (DP §1, mockup 02): 4 floating islands from chapter data, numbered node anchors along one winding
    /// path, rope-bridge gates between islands, storm ring around the boss isle. UI node buttons are projected onto these
    /// anchors by MapScreen. Camera pans horizontally within limits.
    /// </summary>
    public class WorldMapBuilder : MonoBehaviour
    {
        public readonly Dictionary<string, Vector3> NodePositions = new Dictionary<string, Vector3>();
        public readonly Dictionary<string, Vector3> GatePositions = new Dictionary<string, Vector3>();
        public float minX, maxX, camX;
        Camera _cam;

        public void Build(ChapterData ch)
        {
            var islands = new Dictionary<string, IslandData>();
            foreach (var isl in ch.islands)
            {
                islands[isl.id] = isl;
                var c = V(isl.pos);
                bool storm = isl.id == "storm";
                var top = new GameObject("Island_" + isl.id);
                top.transform.SetParent(transform, false);
                top.transform.position = c;
                top.AddComponent<MeshFilter>().sharedMesh = ProceduralMesh.IslandTop(8f, 6f, 0.5f, 32);
                Paint(top.AddComponent<MeshRenderer>(), storm ? "5E7A4A" : "7ED957");
                var rock = new GameObject("Rock");
                rock.transform.SetParent(top.transform, false);
                rock.AddComponent<MeshFilter>().sharedMesh = ProceduralMesh.Cone(8f, 6f, -0.5f, -9f, 32);
                Paint(rock.AddComponent<MeshRenderer>(), "C98A4B");
                if (storm)
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8f;
                        var cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        Destroy(cloud.GetComponent<Collider>());
                        cloud.transform.SetParent(top.transform, false);
                        cloud.transform.localPosition = new Vector3(Mathf.Cos(a) * 10f, 3f + Mathf.Sin(a * 3f), Mathf.Sin(a) * 8f);
                        cloud.transform.localScale = Vector3.one * 2.2f;
                        Paint(cloud.GetComponent<Renderer>(), "3A3F48");
                    }
                if (!string.IsNullOrEmpty(isl.gateAfterLevel)) GatePositions[isl.id] = c + new Vector3(-11f, 0.5f, 0f);
            }
            // bridges between consecutive islands
            for (int i = 1; i < ch.islands.Length; i++)
            {
                Vector3 a = V(ch.islands[i - 1].pos), b = V(ch.islands[i].pos);
                var br = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(br.GetComponent<Collider>());
                br.name = "Bridge_" + ch.islands[i].id;
                br.transform.SetParent(transform, false);
                br.transform.position = (a + b) * 0.5f;
                br.transform.localScale = new Vector3((b - a).magnitude - 14f, 0.2f, 1.2f);
                br.transform.rotation = Quaternion.FromToRotation(Vector3.right, (b - a).normalized);
                Paint(br.GetComponent<Renderer>(), "A8743F");
            }
            // nodes: spread along each island in level order
            var perIsland = new Dictionary<string, List<LevelEntry>>();
            foreach (var l in ch.levels) { if (!perIsland.ContainsKey(l.island)) perIsland[l.island] = new List<LevelEntry>(); perIsland[l.island].Add(l); }
            foreach (var kv in perIsland)
            {
                var c = V(islands[kv.Key].pos);
                int n = kv.Value.Count;
                for (int i = 0; i < n; i++)
                {
                    float t = n == 1 ? 0.5f : i / (float)(n - 1);
                    var p = c + new Vector3(Mathf.Lerp(-5f, 5f, t), 0.6f, Mathf.Sin(t * Mathf.PI * 2f) * 2.5f);
                    NodePositions[kv.Value[i].id] = p;
                    var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(m.GetComponent<Collider>());
                    m.transform.SetParent(transform, false);
                    m.transform.position = p - new Vector3(0f, 0.45f, 0f);
                    m.transform.localScale = new Vector3(1.6f, 0.1f, 1.6f);
                    Paint(m.GetComponent<Renderer>(), kv.Value[i].IsBoss ? "E5484D" : "F5C451");
                }
            }
            minX = V(ch.islands[0].pos).x; maxX = V(ch.islands[ch.islands.Length - 1].pos).x;
            ArenaBuilder.Lighting();
        }

        public void FocusLevel(string levelId)
        {
            camX = NodePositions.TryGetValue(levelId ?? "", out var p) ? p.x : minX;
            Apply();
        }

        public void Pan(float dx) { camX = Mathf.Clamp(camX + dx, minX - 4f, maxX + 4f); Apply(); }

        void Apply()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            _cam.transform.position = new Vector3(camX, 26f, -30f);
            _cam.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
            _cam.fieldOfView = 50f;
        }

        static Vector3 V(float[] a) => a != null && a.Length == 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
        static void Paint(Renderer r, string hex)
        {
            var b = new MaterialPropertyBlock(); var c = MaterialDefinition.Hex(hex);
            b.SetColor("_BaseColor", c); b.SetColor("_Color", c); r.SetPropertyBlock(b);
        }
    }
}
