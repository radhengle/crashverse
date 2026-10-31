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
        // ArchitectureDresser builds many repeated timber boards, beams and stone blocks. Keep one immutable unit-box mesh
        // per quantised dimensions/tile tuple instead of allocating a native Mesh for every repeated architectural component.
        readonly struct BoxKey : System.IEquatable<BoxKey>
        {
            readonly int x, y, z, tile;
            public BoxKey(Vector3 size, float metresPerTile)
            {
                x = Mathf.RoundToInt(size.x * 1000f); y = Mathf.RoundToInt(size.y * 1000f);
                z = Mathf.RoundToInt(size.z * 1000f); tile = Mathf.RoundToInt(metresPerTile * 1000f);
            }
            public bool Equals(BoxKey other) => x == other.x && y == other.y && z == other.z && tile == other.tile;
            public override bool Equals(object obj) => obj is BoxKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { int h = x; h = h * 397 ^ y; h = h * 397 ^ z; return h * 397 ^ tile; }
            }
        }
        static readonly Dictionary<BoxKey, Mesh> _boxCubes = new Dictionary<BoxKey, Mesh>();

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
            var key = new BoxKey(size, tile);
            if (_boxCubes.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = new Mesh { name = "KCube" };
            var v = new List<Vector3>(); var nn = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            var edge = new List<Vector4>(); // per-face metres (u, v) + face size (w, h) for the GlossyPBR edge wear
            void Face(Vector3 n, Vector3 a, Vector3 b)
            {
                int s = v.Count; var c = n * 0.5f;
                Vector3[] q = { c - a * 0.5f - b * 0.5f, c + a * 0.5f - b * 0.5f, c + a * 0.5f + b * 0.5f, c - a * 0.5f + b * 0.5f };
                foreach (var p in q)
                {
                    v.Add(p); nn.Add(n);
                    var w = Vector3.Scale(p, size);
                    uv.Add(new Vector2(Vector3.Dot(w, a), Vector3.Dot(w, b)) / Mathf.Max(0.01f, tile) + new Vector2(0.37f, 0.11f));
                    float fw = Mathf.Abs(Vector3.Dot(size, a)), fh = Mathf.Abs(Vector3.Dot(size, b));
                    edge.Add(new Vector4((Vector3.Dot(p, a) + 0.5f) * fw, (Vector3.Dot(p, b) + 0.5f) * fh, fw, fh));
                }
                t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
            }
            Face(Vector3.right, Vector3.forward, Vector3.up); Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.forward); Face(Vector3.down, Vector3.right, Vector3.back);
            Face(Vector3.forward, Vector3.left, Vector3.up); Face(Vector3.back, Vector3.right, Vector3.up);
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetUVs(2, edge); m.SetTriangles(t, 0); m.RecalculateBounds(); m.RecalculateTangents();
            _boxCubes[key] = m;
            return m;
        }

        static int I(byte[] d, ref int o) { int r = System.BitConverter.ToInt32(d, o); o += 4; return r; }
        static float F(byte[] d, ref int o) { float r = System.BitConverter.ToSingle(d, o); o += 4; return r; }
    }

    /// <summary>
    /// Owns meshes created at runtime for one gameplay run. Unity does not destroy meshes made with new Mesh,
    /// Instantiate or CombineMeshes when their GameObjects are destroyed; retaining them leaks native memory on retries.
    /// Only register per-run meshes here. Resource-loaded and static cached meshes must never be registered.
    /// </summary>
    public sealed class RuntimeMeshCleanup : MonoBehaviour
    {
        readonly HashSet<Mesh> _owned = new HashSet<Mesh>();

        public static RuntimeMeshCleanup Attach(Transform runRoot)
        {
            if (runRoot == null) return null;
            var cleaner = runRoot.GetComponent<RuntimeMeshCleanup>();
            return cleaner != null ? cleaner : runRoot.gameObject.AddComponent<RuntimeMeshCleanup>();
        }

        public static void Own(Transform context, Mesh mesh)
        {
            if (context == null || mesh == null) return;
            var cleaner = context.GetComponentInParent<RuntimeMeshCleanup>();
            if (cleaner == null) cleaner = Attach(context.root);
            cleaner?._owned.Add(mesh);
        }

        void OnDestroy()
        {
            foreach (var mesh in _owned) if (mesh != null) Destroy(mesh);
            _owned.Clear();
        }
    }
}
