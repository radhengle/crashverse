using System.Collections.Generic;
using Crashverse.Data;
using Crashverse.Meta;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Level
{
    /// <summary>
    /// Realistic v1 dressing (3D Modeler, branch 3d-modeler/realistic-v1): every L1-10 / Daily Boss block gets its Kit_Real* piece as a
    /// visual-only child (LOD0-2, Crashverse/GlossyPBR, UVs rebuilt from the scaled positions so textures don't stretch). The block keeps
    /// its box collider, rigidbody, joints (Physics Developer's break forces) and chunk layout, so physics and ranked determinism are
    /// unchanged. If the kit isn't available (manifest missing) the procedural cube stays, nothing fails.
    /// </summary>
    public static class RealKit
    {
        public const string ManifestPath = "RealKit/RealKitManifest";
        public const string ModelsRoot = "Assets/_Project/Models/Realistic/";
        public static bool Enabled = true;

        static RealKitManifest _m; static bool _loaded;
        static readonly Dictionary<string, Material[]> _mats = new Dictionary<string, Material[]>();
        static readonly Dictionary<string, Mesh[]> _meshCache = new Dictionary<string, Mesh[]>();
        static readonly Dictionary<string, (Mesh mesh, Matrix4x4 m)[]> _src = new Dictionary<string, (Mesh, Matrix4x4)[]>();

        public static RealKitManifest Manifest
        {
            get
            {
                if (_loaded) return _m;
                _loaded = true;
                _m = Resources.Load<RealKitManifest>(ManifestPath);
#if UNITY_EDITOR
                if (_m == null) _m = RealKitManifest.FromAssetDatabase(); // editor / PlayMode: straight from Models/Realistic
#endif
                if (_m == null) Debug.LogWarning("[RealKit] no manifest: blocks keep the procedural cube look");
                return _m;
            }
        }

        public static int Dressed { get; private set; }

        /// <summary>Adds the realistic piece under <paramref name="block"/> (unit-cube local space, parent scale = block size). Returns the visual root or null.</summary>
        public static GameObject Dress(GameObject block, BlockData bd)
        {
            if (!Enabled || block == null || bd == null) return null;
            var piece = RealPieceRules.Classify(bd); if (piece == null) return null;
            var man = Manifest; if (man == null) return null;
            var entry = man.Find(piece); if (entry == null || entry.model == null) return null;
            var mats = MaterialsFor(man, RealPieceRules.KitOf(piece)); if (mats == null) return null;
            var size = new Vector3(bd.s[0], bd.s[1], bd.s[2]);
            var meshes = MeshesFor(entry, piece, size); if (meshes == null || meshes.Length == 0) return null;

            var root = new GameObject("Real_" + piece);
            root.transform.SetParent(block.transform, false);
            var lods = new LOD[meshes.Length];
            float[] cut = { 0.045f, 0.015f, 0.002f }; // blocks are 0.05-0.2 of the screen from the locked camera: LOD0 for the structure, LOD1-2 for tiny bits / far zoom
            for (int i = 0; i < meshes.Length; i++)
            {
                var go = new GameObject("LOD" + i);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = meshes[i].subMeshCount > 1 ? mats : new[] { mats[0] };
                r.shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                lods[i] = new LOD(cut[Mathf.Min(i, cut.Length - 1)], new Renderer[] { r });
            }
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lods); lg.RecalculateBounds();
            var own = block.GetComponent<MeshRenderer>(); if (own != null) own.enabled = false;
            Dressed++;
            return root;
        }

        static Material[] MaterialsFor(RealKitManifest man, string kit)
        {
            if (_mats.TryGetValue(kit, out var m)) return m;
            var k = man.FindKit(kit); if (k == null) return _mats[kit] = null;
            var sh = Shader.Find("Crashverse/GlossyPBR");
            Material Make(string n, Texture2D a, Texture2D nrm, Texture2D orm, bool inner)
            {
                Material mat;
                if (sh != null && sh.isSupported)
                {
                    mat = new Material(sh) { name = n };
                    if (a != null) mat.SetTexture("_BaseMap", a);
                    if (nrm != null) mat.SetTexture("_BumpMap", nrm);
                    if (orm != null) mat.SetTexture("_ORMMap", orm);
                    mat.SetFloat("_InnerFace", inner ? 1f : 0f);
                }
                else
                {
                    mat = new Material(Crashverse.Core.RenderMaterials.Lit) { name = n };
                    if (a != null) mat.SetTexture("_BaseMap", a);
                }
                mat.enableInstancing = false; // SRP Batcher
                return mat;
            }
            return _mats[kit] = new[]
            {
                Make("M_Real" + kit, k.albedo, k.normal, k.orm, false),
                Make("M_Real" + kit + "_Inner", k.innerAlbedo != null ? k.innerAlbedo : k.albedo, k.innerNormal != null ? k.innerNormal : k.normal, k.innerOrm != null ? k.innerOrm : k.orm, true),
            };
        }

        /// <summary>LOD meshes of the piece and their transform relative to the model root (incl. FBX axis conversion).</summary>
        static (Mesh mesh, Matrix4x4 m)[] Source(RealKitManifest.Piece e, string piece)
        {
            if (_src.TryGetValue(piece, out var s)) return s;
            var list = new List<(Mesh, Matrix4x4)>();
            var rootT = e.model.transform;
            Matrix4x4 rootInv = Matrix4x4.TRS(Vector3.zero, rootT.localRotation, Vector3.one).inverse * Matrix4x4.Translate(-rootT.localPosition);
            for (int lod = 0; lod < 3; lod++)
            {
                foreach (var mf in e.model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || !mf.name.EndsWith("_LOD" + lod) || mf.name.StartsWith("UCX_")) continue;
                    // root at identity, keep the root's own axis-conversion rotation: matrix = rootRot * (root^-1 * child)
                    var m = Matrix4x4.Rotate(rootT.localRotation) * rootInv * mf.transform.localToWorldMatrix;
                    list.Add((mf.sharedMesh, m)); break;
                }
            }
            if (list.Count == 0) // no _LODn naming: take the first non-collider mesh
                foreach (var mf in e.model.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && !mf.name.StartsWith("UCX_")) { list.Add((mf.sharedMesh, Matrix4x4.Rotate(rootT.localRotation) * rootInv * mf.transform.localToWorldMatrix)); break; }
            return _src[piece] = list.ToArray();
        }

        static Mesh[] MeshesFor(RealKitManifest.Piece e, string piece, Vector3 size)
        {
            string key = piece + "|" + size.x.ToString("F3") + "|" + size.y.ToString("F3") + "|" + size.z.ToString("F3");
            if (_meshCache.TryGetValue(key, out var c)) return c;
            var src = Source(e, piece);
            if (src.Length == 0 || !src[0].mesh.isReadable) { if (src.Length > 0) Debug.LogWarning("[RealKit] " + piece + " mesh not readable"); return _meshCache[key] = null; }
            // fit frame from LOD0 bounds (all LODs share it so they don't pop)
            var b0 = TransformedBounds(src[0].mesh, src[0].m);
            int[] map = RealPieceRules.AxisMap(b0.size, size);
            string kitName = RealPieceRules.KitOf(piece); float tile = kitName == "Props" ? -1f : RealPieceRules.TileM(kitName); // Props use a 512 atlas: keep authored UVs
            var outM = new Mesh[src.Length];
            for (int i = 0; i < src.Length; i++) outM[i] = Fit(src[i].mesh, src[i].m, b0, map, size, tile, piece + "_LOD" + i);
            return _meshCache[key] = outM;
        }

        static Bounds TransformedBounds(Mesh mesh, Matrix4x4 m)
        {
            var v = mesh.vertices; var b = new Bounds(m.MultiplyPoint3x4(v[0]), Vector3.zero);
            for (int i = 1; i < v.Length; i++) b.Encapsulate(m.MultiplyPoint3x4(v[i]));
            return b;
        }

        /// <summary>Bake the piece into the block's unit cube: permute axes (map), normalise to [-0.5,0.5], rebuild UV0 = scaled pos / tile_m.</summary>
        static Mesh Fit(Mesh src, Matrix4x4 m, Bounds b0, int[] map, Vector3 size, float tile, string name)
        {
            var v = src.vertices; var n = src.normals; var srcUv = src.uv;
            var vo = new Vector3[v.Length]; var no = new Vector3[v.Length]; var uv = new Vector2[v.Length];
            Vector3 ext = b0.size; Vector3 c = b0.center;
            Vector3 d = new Vector3(Mathf.Max(1e-4f, ext[map[0]]), Mathf.Max(1e-4f, ext[map[1]]), Mathf.Max(1e-4f, ext[map[2]]));
            var nm = m.inverse.transpose;
            for (int i = 0; i < v.Length; i++)
            {
                var p = m.MultiplyPoint3x4(v[i]) - c;
                var pp = new Vector3(p[map[0]], p[map[1]], p[map[2]]);                // permute
                var u = new Vector3(pp.x / d.x, pp.y / d.y, pp.z / d.z);              // unit cube
                vo[i] = u;
                var w = Vector3.Scale(u, size);                                        // metres in the block
                Vector3 nn = n != null && n.Length == v.Length ? nm.MultiplyVector(n[i]) : Vector3.up;
                var pn = new Vector3(nn[map[0]], nn[map[1]], nn[map[2]]);
                var nUnit = new Vector3(pn.x * d.x, pn.y * d.y, pn.z * d.z).normalized;  // inverse-transpose of the unit scale
                no[i] = nUnit;
                var nWorld = new Vector3(nUnit.x / size.x, nUnit.y / size.y, nUnit.z / size.z).normalized;
                uv[i] = tile > 0f || srcUv == null || srcUv.Length != v.Length ? RealPieceRules.BoxUv(w, nWorld, tile > 0f ? tile : 1f) : srcUv[i];
            }
            bool flip = Parity(map) < 0;                                               // odd permutation mirrors: flip winding
            var mesh = new Mesh { name = name, indexFormat = v.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vo; mesh.normals = no; mesh.uv = uv;
            if (src.colors32 != null && src.colors32.Length == v.Length) mesh.colors32 = src.colors32; // damage mask / flex
            mesh.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s);
                if (flip) for (int k = 0; k < tri.Length; k += 3) { var tmp = tri[k + 1]; tri[k + 1] = tri[k + 2]; tri[k + 2] = tmp; }
                mesh.SetTriangles(tri, s);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        static int Parity(int[] p) { int inv = 0; for (int i = 0; i < 3; i++) for (int j = i + 1; j < 3; j++) if (p[i] > p[j]) inv++; return inv % 2 == 0 ? 1 : -1; }

        /// <summary>Tests / level reloads: drop cached meshes and materials.</summary>
        public static void ResetCaches() { _meshCache.Clear(); _src.Clear(); _mats.Clear(); _loaded = false; _m = null; Dressed = 0; }
    }
}
