using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.Visuals
{
    /// <summary>
    /// Loads a CVM1 mesh bundle (Resources/Kingdom/Meshes/*.bytes, written by Tools/Blender/kingdom/cvm_export.py): named meshes with
    /// positions, normals, UV0 and vertex colours, already in Unity axes. No importer involved, so no FBX material/axis surprises.
    /// </summary>
    public static class CvMesh
    {
        static readonly Dictionary<string, Dictionary<string, Mesh>> _cache = new Dictionary<string, Dictionary<string, Mesh>>();

        public static Dictionary<string, Mesh> Load(string resource)
        {
            if (_cache.TryGetValue(resource, out var hit)) return hit;
            var res = new Dictionary<string, Mesh>();
            var ta = Resources.Load<TextAsset>(resource);
            if (ta == null) { Debug.LogWarning("[Kingdom] missing mesh bundle " + resource); return _cache[resource] = res; }
            var d = ta.bytes; int o = 0;
            try
            {
                if (d.Length < 8 || d[0] != (byte)'C' || d[1] != (byte)'V' || d[2] != (byte)'M' || d[3] != (byte)'1') throw new System.Exception("bad magic");
                o = 4; int n = I(d, ref o);
                for (int k = 0; k < n; k++)
                {
                    int ln = I(d, ref o); string name = System.Text.Encoding.UTF8.GetString(d, o, ln); o += ln;
                    int nv = I(d, ref o), ni = I(d, ref o);
                    var pos = new Vector3[nv]; var nrm = new Vector3[nv]; var uv = new Vector2[nv]; var col = new Color32[nv]; var idx = new int[ni];
                    for (int i = 0; i < nv; i++) pos[i] = new Vector3(F(d, ref o), F(d, ref o), F(d, ref o));
                    for (int i = 0; i < nv; i++) nrm[i] = new Vector3(F(d, ref o), F(d, ref o), F(d, ref o));
                    for (int i = 0; i < nv; i++) uv[i] = new Vector2(F(d, ref o), F(d, ref o));
                    for (int i = 0; i < nv; i++) { col[i] = new Color32(d[o], d[o + 1], d[o + 2], d[o + 3]); o += 4; }
                    for (int i = 0; i < ni; i++) idx[i] = I(d, ref o);
                    var m = new Mesh { name = name, indexFormat = nv > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                    m.vertices = pos; m.normals = nrm; m.uv = uv; m.colors32 = col; m.triangles = idx;
                    m.RecalculateBounds(); m.RecalculateTangents(); m.UploadMeshData(false);
                    res[name] = m;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Kingdom] bad mesh bundle " + resource + ": " + e.Message); }
            return _cache[resource] = res;
        }

        /// <summary>Box-projected UVs from object-space positions (metres / tile), so a tiling texture never stretches.</summary>
        public static Mesh WithBoxUv(Mesh src, float tile, Vector3 scale)
        {
            var m = Object.Instantiate(src); m.name = src.name + "_box";
            var v = m.vertices; var n = m.normals; var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var p = Vector3.Scale(v[i], scale); var nn = n != null && n.Length == v.Length ? n[i] : Vector3.up;
                float ax = Mathf.Abs(nn.x), ay = Mathf.Abs(nn.y), az = Mathf.Abs(nn.z);
                uv[i] = (ax >= ay && ax >= az ? new Vector2(p.z, p.y) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y)) / Mathf.Max(0.01f, tile);
            }
            m.uv = uv; m.RecalculateTangents(); return m;
        }

        /// <summary>Unit cube (-0.5..0.5) whose UVs tile every <paramref name="tile"/> metres once scaled to <paramref name="size"/>.</summary>
        public static Mesh BoxCube(Vector3 size, float tile)
        {
            var m = new Mesh { name = "KCube" };
            var v = new List<Vector3>(); var nn = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            void Face(Vector3 n, Vector3 a, Vector3 b)
            {
                int s = v.Count; var c = n * 0.5f;
                Vector3[] q = { c - a * 0.5f - b * 0.5f, c + a * 0.5f - b * 0.5f, c + a * 0.5f + b * 0.5f, c - a * 0.5f + b * 0.5f };
                foreach (var p in q)
                {
                    v.Add(p); nn.Add(n);
                    var w = Vector3.Scale(p, size);
                    uv.Add(new Vector2(Vector3.Dot(w, a), Vector3.Dot(w, b)) / Mathf.Max(0.01f, tile) + new Vector2(0.37f, 0.11f));
                }
                t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
            }
            Face(Vector3.right, Vector3.forward, Vector3.up); Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.forward); Face(Vector3.down, Vector3.right, Vector3.back);
            Face(Vector3.forward, Vector3.left, Vector3.up); Face(Vector3.back, Vector3.right, Vector3.up);
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        static int I(byte[] d, ref int o) { int r = System.BitConverter.ToInt32(d, o); o += 4; return r; }
        static float F(byte[] d, ref int o) { float r = System.BitConverter.ToSingle(d, o); o += 4; return r; }
    }
}
