using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    /// <summary>
    /// L1 v2 (Tide Watchtower): a small procedural mesh accumulator. Parts are appended straight into one vertex list per material
    /// (no temporary meshes), in the space of the transform they are built under (metres), with box-projected UVs (metres / tile)
    /// so tiling textures never stretch. Build() makes one renderer per material and registers the meshes with RuntimeMeshCleanup.
    /// </summary>
    public sealed class TwMesh
    {
        sealed class Part { public readonly List<Vector3> v = new List<Vector3>(); public readonly List<Vector3> n = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int> t = new List<int>(); public float tile = 1f; }
        readonly Dictionary<Material, Part> _parts = new Dictionary<Material, Part>();
        readonly List<Material> _order = new List<Material>();
        public Matrix4x4 M = Matrix4x4.identity; // extra transform applied to everything appended (push/pop by the caller)

        Part P(Material m, float tile)
        {
            if (!_parts.TryGetValue(m, out var p)) { p = new Part { tile = tile }; _parts[m] = p; _order.Add(m); }
            return p;
        }

        static Vector2 BoxUv(Vector3 p, Vector3 n, float tile)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            var uv = ax >= ay && ax >= az ? new Vector2(p.z, p.y) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
            return uv / Mathf.Max(0.01f, tile);
        }

        /// <summary>One flat convex polygon; its front (rendered) side is the one Cross(q1 - q0, q2 - q0) points to.</summary>
        public void Poly(Material m, float tile, params Vector3[] q)
        {
            if (m == null || q.Length < 3) return;
            var p = P(m, tile); int s = p.v.Count;
            var w = new Vector3[q.Length]; for (int i = 0; i < q.Length; i++) w[i] = M.MultiplyPoint3x4(q[i]);
            var nn = Vector3.Cross(w[1] - w[0], w[2] - w[0]); if (nn.sqrMagnitude < 1e-12f && q.Length > 3) nn = Vector3.Cross(w[2] - w[0], w[3] - w[0]); nn = nn.normalized;
            for (int i = 0; i < w.Length; i++) { p.v.Add(w[i]); p.n.Add(nn); p.uv.Add(BoxUv(w[i], nn, p.tile)); }
            for (int i = 1; i < w.Length - 1; i++) { p.t.Add(s); p.t.Add(s + i); p.t.Add(s + i + 1); }
        }

        /// <summary>Two-sided flat polygon (cloth, flags, banners).</summary>
        public void Poly2(Material m, float tile, params Vector3[] q)
        {
            Poly(m, tile, q); var r = (Vector3[])q.Clone(); System.Array.Reverse(r); Poly(m, tile, r);
        }

        /// <summary>A hexahedron from its 8 corners (index bits: x=1, y=2, z=4), flat-shaded.</summary>
        public void Hexa(Material m, float tile, Vector3[] c)
        {
            Poly(m, tile, c[0], c[2], c[3], c[1]); // -z
            Poly(m, tile, c[4], c[5], c[7], c[6]); // +z
            Poly(m, tile, c[0], c[4], c[6], c[2]); // -x
            Poly(m, tile, c[1], c[3], c[7], c[5]); // +x
            Poly(m, tile, c[2], c[6], c[7], c[3]); // +y
            Poly(m, tile, c[0], c[1], c[5], c[4]); // -y
        }

        public void Box(Material m, float tile, Vector3 centre, Vector3 size, Quaternion rot = default)
        {
            if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f) rot = Quaternion.identity;
            var c = new Vector3[8]; var h = size * 0.5f;
            for (int i = 0; i < 8; i++) c[i] = centre + rot * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            Hexa(m, tile, c);
        }

        /// <summary>Box between two corners (min/max), axis-aligned.</summary>
        public void BoxMinMax(Material m, float tile, Vector3 a, Vector3 b) => Box(m, tile, (a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z)));

        /// <summary>A rough stone / rock: a box whose corners are jittered by up to jitter x size (seeded), flat-shaded.</summary>
        public void Slab(Material m, float tile, Vector3 centre, Vector3 size, int seed, float jitter = 0.18f, Quaternion rot = default)
        {
            if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f) rot = Quaternion.identity;
            var c = new Vector3[8]; var h = size * 0.5f; uint s = (uint)(seed * 2654435761u + 12345u);
            float R() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s % 10000u) / 10000f * 2f - 1f; }
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                p += new Vector3(R() * size.x, R() * size.y * ((i & 2) != 0 ? 1f : 0.3f), R() * size.z) * jitter;
                c[i] = centre + rot * p;
            }
            Hexa(m, tile, c);
        }

        /// <summary>Cylinder / frustum between a and b (radii ra, rb), n sides, smooth sides, optional caps.</summary>
        public void Cyl(Material m, float tile, Vector3 a, Vector3 b, float ra, float rb, int n = 10, bool caps = true, bool flat = false)
        {
            if (m == null) return;
            var d = b - a; float len = d.magnitude; if (len < 1e-4f) return;
            var ax = d / len; var u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.95f ? Vector3.up : Vector3.right).normalized; var w = Vector3.Cross(ax, u);
            var p = P(m, tile);
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0); var d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
                var q0 = M.MultiplyPoint3x4(a + d0 * ra); var q1 = M.MultiplyPoint3x4(a + d1 * ra); var q2 = M.MultiplyPoint3x4(b + d1 * rb); var q3 = M.MultiplyPoint3x4(b + d0 * rb);
                var slope = ax * ((ra - rb) / len);
                var n0 = M.MultiplyVector(flat ? (d0 + d1).normalized + slope : d0 + slope).normalized; var n1 = M.MultiplyVector(flat ? (d0 + d1).normalized + slope : d1 + slope).normalized;
                int s = p.v.Count;
                p.v.Add(q0); p.v.Add(q1); p.v.Add(q2); p.v.Add(q3);
                p.n.Add(n0); p.n.Add(n1); p.n.Add(n1); p.n.Add(n0);
                float uu0 = (float)i / n * 2f * Mathf.PI * Mathf.Max(ra, rb) / tile, uu1 = (float)(i + 1) / n * 2f * Mathf.PI * Mathf.Max(ra, rb) / tile;
                p.uv.Add(new Vector2(uu0, 0f)); p.uv.Add(new Vector2(uu1, 0f)); p.uv.Add(new Vector2(uu1, len / tile)); p.uv.Add(new Vector2(uu0, len / tile));
                p.t.Add(s); p.t.Add(s + 1); p.t.Add(s + 2); p.t.Add(s); p.t.Add(s + 2); p.t.Add(s + 3);
            }
            if (caps)
            {
                var ca = new Vector3[n]; var cb = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float an = i * Mathf.PI * 2f / n; var dd = u * Mathf.Cos(an) + w * Mathf.Sin(an);
                    ca[n - 1 - i] = a + dd * ra; cb[i] = b + dd * rb;
                }
                if (ra > 1e-4f) Poly(m, tile, ca);
                if (rb > 1e-4f) Poly(m, tile, cb);
            }
        }

        /// <summary>Surface of revolution around the local x axis: profile = (x, radius) points, n sides.</summary>
        public void Lathe(Material m, float tile, Vector2[] profile, int n = 24, Matrix4x4? local = null)
        {
            var keep = M; if (local.HasValue) M = M * local.Value;
            for (int i = 0; i < profile.Length - 1; i++)
            {
                var a = new Vector3(profile[i].x, 0f, 0f); var b = new Vector3(profile[i + 1].x, 0f, 0f);
                if (Mathf.Abs(profile[i + 1].x - profile[i].x) < 1e-4f) Ring(m, tile, profile[i].x, profile[i].y, profile[i + 1].y, n);
                else Cyl(m, tile, a, b, profile[i].y, profile[i + 1].y, n, false);
            }
            M = keep;
        }

        /// <summary>Flat annulus (or disc when r0 = 0) in the plane x = at, facing -x when r1 &gt; r0 is the outer edge going backwards.</summary>
        void Ring(Material m, float tile, float at, float r0, float r1, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var d0 = new Vector3(0f, Mathf.Cos(a0), Mathf.Sin(a0)); var d1 = new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1)); var c = new Vector3(at, 0f, 0f);
                if (r1 > r0) Poly(m, tile, c + d0 * r0, c + d1 * r0, c + d1 * r1, c + d0 * r1);
                else Poly(m, tile, c + d0 * r0, c + d0 * r1, c + d1 * r1, c + d1 * r0);
            }
        }

        /// <summary>Low-poly ellipsoid (lat/long), smooth.</summary>
        public void Sphere(Material m, float tile, Vector3 c, Vector3 r, int seg = 10, int rings = 6)
        {
            if (m == null) return;
            var p = P(m, tile); int s = p.v.Count;
            for (int j = 0; j <= rings; j++)
            {
                float th = Mathf.PI * j / rings;
                for (int i = 0; i <= seg; i++)
                {
                    float ph = 2f * Mathf.PI * i / seg;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    var q = M.MultiplyPoint3x4(c + Vector3.Scale(d, r));
                    p.v.Add(q); p.n.Add(M.MultiplyVector(new Vector3(d.x / Mathf.Max(1e-3f, r.x), d.y / Mathf.Max(1e-3f, r.y), d.z / Mathf.Max(1e-3f, r.z))).normalized);
                    p.uv.Add(new Vector2((float)i / seg * 2f * Mathf.PI * r.x / tile, th * r.y / tile));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = s + j * (seg + 1) + i, b = a + 1, cc = a + seg + 1, d = cc + 1;
                    p.t.Add(a); p.t.Add(b); p.t.Add(cc); p.t.Add(b); p.t.Add(d); p.t.Add(cc);
                }
        }

        /// <summary>Faceted crystal shard: an n-sided prism body with a pointed top and a short pointed base, along up from base.</summary>
        public void Shard(Material side, Material dark, Vector3 basePt, Vector3 up, float radius, float height, int n = 6, float tipFrac = 0.32f, float twist = 0f)
        {
            up = up.normalized; var u = Vector3.Cross(up, Mathf.Abs(up.y) < 0.95f ? Vector3.up : Vector3.right).normalized; var w = Vector3.Cross(up, u);
            float body = height * (1f - tipFrac);
            var tip = basePt + up * height; var bot = basePt - up * radius * 0.4f;
            for (int i = 0; i < n; i++)
            {
                float a0 = twist + i * Mathf.PI * 2f / n, a1 = twist + (i + 1) * Mathf.PI * 2f / n;
                var d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0); var d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
                var b0 = basePt + d0 * radius; var b1 = basePt + d1 * radius; var t0 = basePt + up * body + d0 * radius * 0.92f; var t1 = basePt + up * body + d1 * radius * 0.92f;
                var mat = (i % 2 == 0) ? side : dark;
                Poly(mat, 1f, b0, b1, t1, t0);
                Poly(mat == side ? dark : side, 1f, t0, t1, tip);
                Poly(dark, 1f, b0, bot, b1);
            }
        }

        public bool Empty => _parts.Count == 0;

        public void Build(Transform parent, string name, bool shadows = true, bool receive = true)
        {
            foreach (var mat in _order)
            {
                var p = _parts[mat]; if (p.v.Count == 0) continue;
                var mesh = new Mesh { name = name + "_" + mat.name, indexFormat = p.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(p.v); mesh.SetNormals(p.n); mesh.SetUVs(0, p.uv); mesh.SetTriangles(p.t, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
                var go = new GameObject(name + "_" + mat.name); go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; RuntimeMeshCleanup.Own(parent, mesh);
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = receive;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            _parts.Clear(); _order.Clear();
        }
    }
}
