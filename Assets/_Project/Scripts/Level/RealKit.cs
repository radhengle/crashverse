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
            if (bd.s == null || bd.s.Length != 3) return null;
            if (bd.parts != null && bd.parts.Length > 0) return DressParts(block, bd);
            bool authored = !string.IsNullOrEmpty(bd.model);                    // Level Designer prefab map: explicit kit module
            var piece = authored ? ModuleName(bd.model) : RealPieceRules.Classify(bd); if (piece == null) return null;
            var man = Manifest; if (man == null) return null;
            var entry = man.Find(piece);
#if UNITY_EDITOR
            if ((entry == null || entry.model == null) && authored) entry = RealKitManifest.ResolveEditor(bd.model);
#endif
            if (entry == null || entry.model == null) { if (authored) Debug.LogWarning("[RealKit] module not in manifest: " + bd.model); return null; }
            bool real = System.Array.IndexOf(RealPieceRules.Pieces, piece) >= 0; // the 14 generic pieces; Kit_RealBakery_*/Stall/Bld_* keep their own materials
            var mats = real ? MaterialsFor(man, RealPieceRules.KitOf(piece)) : null; if (real && mats == null) return null;
            var size = new Vector3(bd.s[0], bd.s[1], bd.s[2]);
            var meshes = MeshesFor(entry, piece, size, bd.rotY, real); if (meshes == null || meshes.Length == 0) return null;

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
                var ms = real ? mats : SourceMaterials(piece, i);
                r.sharedMaterials = meshes[i].subMeshCount > 1 || ms.Length < 2 ? ms : new[] { ms[0] };
                r.shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                lods[i] = new LOD(cut[Mathf.Min(i, cut.Length - 1)], new Renderer[] { r });
            }
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lods); lg.RecalculateBounds();
            var own = block.GetComponent<MeshRenderer>(); if (own != null) own.enabled = false;
            Dressed++;
            return root;
        }

        /// <summary>Gap 6: all part FBX of a module at the module pivot (block bottom-centre), 1:1 or modelScale, yaw rotY; LODs merged per level.</summary>
        static GameObject DressParts(GameObject block, BlockData bd)
        {
            var man = Manifest; if (man == null) return null;
            var size = new Vector3(bd.s[0], bd.s[1], bd.s[2]);
            var scale = bd.modelScale != null && bd.modelScale.Length == 3 ? new Vector3(bd.modelScale[0], bd.modelScale[1], bd.modelScale[2]) : Vector3.one;
            var yaw = Quaternion.Euler(0f, bd.rotY, 0f);
            var pivot = new Vector3(0f, -0.5f * size.y, 0f);                       // block-local metres: base-footprint centre
            var perLod = new List<Renderer>[3]; for (int i = 0; i < 3; i++) perLod[i] = new List<Renderer>();
            var root = new GameObject("Real_Module_" + bd.id);
            root.transform.SetParent(block.transform, false);
            int placed = 0;
            foreach (var part in bd.parts)
            {
                var name = ModuleName(part); var e = man.Find(name);
#if UNITY_EDITOR
                if (e == null || e.model == null) e = RealKitManifest.ResolveEditor(part);
#endif
                if (e == null || e.model == null) { Debug.LogWarning($"[RealKit] {bd.id}: part not in manifest: {part}"); continue; }
                var src = Source(e, name);
                bool real = System.Array.IndexOf(RealPieceRules.Pieces, name) >= 0; var kit = real ? RealPieceRules.KitOf(name) : null;
                var kmats = real ? MaterialsFor(man, kit) : null;
                for (int i = 0; i < src.Length && i < 3; i++)
                {
                    string key = "P|" + name + "|" + i + "|" + size + "|" + scale + "|" + bd.rotY;
                    if (!_partCache.TryGetValue(key, out var mesh))
                        _partCache[key] = mesh = Place(src[i].mesh, src[i].m, scale, yaw, pivot, size, name + "_LOD" + i);
                    if (mesh == null) continue;
                    var go = new GameObject(name + "_LOD" + i); go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterials = real && kmats != null ? kmats : SourceMaterials(name, i);
                    r.shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    perLod[i].Add(r);
                }
                placed++;
            }
            if (placed == 0) { Object.Destroy(root); return null; }
            float[] cut = { 0.045f, 0.015f, 0.002f };
            var lods = new List<LOD>();
            for (int i = 0; i < 3; i++) if (perLod[i].Count > 0) lods.Add(new LOD(cut[i], perLod[i].ToArray()));
            var lg = root.AddComponent<LODGroup>(); lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();
            var own = block.GetComponent<MeshRenderer>(); if (own != null) own.enabled = false;
            Dressed++;
            return root;
        }

        static readonly Dictionary<string, Mesh> _partCache = new Dictionary<string, Mesh>();

        /// <summary>Part mesh in the block's unit cube: (yaw * (scale ⊙ M·v) + pivot) ⊘ s; authored UVs and materials kept.</summary>
        static Mesh Place(Mesh src, Matrix4x4 m, Vector3 scale, Quaternion yaw, Vector3 pivot, Vector3 size, string name)
        {
            if (src == null || !src.isReadable) { Debug.LogWarning("[RealKit] part mesh not readable: " + name); return null; }
            var v = src.vertices; var n = src.normals; var vo = new Vector3[v.Length]; var no = new Vector3[v.Length];
            var nm = m.inverse.transpose; bool hasN = n != null && n.Length == v.Length;
            for (int i = 0; i < v.Length; i++)
            {
                var w = yaw * Vector3.Scale(m.MultiplyPoint3x4(v[i]), scale) + pivot;
                vo[i] = new Vector3(w.x / size.x, w.y / size.y, w.z / size.z);
                var nn = hasN ? nm.MultiplyVector(n[i]) : Vector3.up;
                var nw = (yaw * new Vector3(nn.x / scale.x, nn.y / scale.y, nn.z / scale.z)).normalized;
                no[i] = new Vector3(nw.x * size.x, nw.y * size.y, nw.z * size.z).normalized;
            }
            var mesh = new Mesh { name = name, indexFormat = v.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vo; mesh.normals = no; mesh.uv = src.uv;
            if (src.colors32 != null && src.colors32.Length == v.Length) mesh.colors32 = src.colors32;
            mesh.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) mesh.SetTriangles(src.GetTriangles(s), s);
            mesh.RecalculateTangents(); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
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

        public static string ModuleName(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            var n = model.Replace('\\', '/'); int i = n.LastIndexOf('/'); if (i >= 0) n = n.Substring(i + 1);
            return n.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 4) : n;
        }

        static readonly Dictionary<string, Material[][]> _srcMats = new Dictionary<string, Material[][]>();
        /// <summary>Authored kit modules (stylized BuildingKit etc.) keep their importer-wired GlossyPBR materials.</summary>
        static Material[] SourceMaterials(string piece, int lod)
        {
            if (_srcMats.TryGetValue(piece, out var all) && lod < all.Length && all[lod] != null && all[lod].Length > 0)
            {
                var ok = true; foreach (var m in all[lod]) if (m == null || m.shader == null || !m.shader.isSupported) ok = false;
                if (ok) return all[lod];
            }
            return new[] { Crashverse.Core.RenderMaterials.Lit };
        }

        /// <summary>LOD meshes of the piece and their transform relative to the model root (incl. FBX axis conversion).</summary>
        static (Mesh mesh, Matrix4x4 m)[] Source(RealKitManifest.Piece e, string piece)
        {
            if (_src.TryGetValue(piece, out var s)) return s;
            var list = new List<(Mesh, Matrix4x4)>(); var mats = new List<Material[]>();
            var rootT = e.model.transform;
            Matrix4x4 rootInv = Matrix4x4.TRS(Vector3.zero, rootT.localRotation, Vector3.one).inverse * Matrix4x4.Translate(-rootT.localPosition);
            for (int lod = 0; lod < 3; lod++)
            {
                foreach (var mf in e.model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || !mf.name.EndsWith("_LOD" + lod) || mf.name.StartsWith("UCX_")) continue;
                    // root at identity, keep the root's own axis-conversion rotation: matrix = rootRot * (root^-1 * child)
                    var m = Matrix4x4.Rotate(rootT.localRotation) * rootInv * mf.transform.localToWorldMatrix;
                    list.Add((mf.sharedMesh, m)); mats.Add(mf.GetComponent<MeshRenderer>() != null ? mf.GetComponent<MeshRenderer>().sharedMaterials : null); break;
                }
            }
            if (list.Count == 0) // no _LODn naming: take the first non-collider mesh
                foreach (var mf in e.model.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && !mf.name.StartsWith("UCX_")) { list.Add((mf.sharedMesh, Matrix4x4.Rotate(rootT.localRotation) * rootInv * mf.transform.localToWorldMatrix)); mats.Add(mf.GetComponent<MeshRenderer>() != null ? mf.GetComponent<MeshRenderer>().sharedMaterials : null); break; }
            _srcMats[piece] = mats.ToArray();
            return _src[piece] = list.ToArray();
        }

        static Mesh[] MeshesFor(RealKitManifest.Piece e, string piece, Vector3 size, float rotY, bool real)
        {
            string key = piece + "|" + size.x.ToString("F3") + "|" + size.y.ToString("F3") + "|" + size.z.ToString("F3") + "|" + rotY.ToString("F1");
            if (_meshCache.TryGetValue(key, out var c)) return c;
            var src = Source(e, piece);
            if (src.Length == 0 || !src[0].mesh.isReadable) { if (src.Length > 0) Debug.LogWarning("[RealKit] " + piece + " mesh not readable"); return _meshCache[key] = null; }
            // fit frame from LOD0 bounds (all LODs share it so they don't pop)
            var b0 = TransformedBounds(src[0].mesh, src[0].m);
            // rotY (prefab map side walls): fit in the module's own frame, then yaw the metric shape into the block
            bool quarter = Mathf.Abs(Mathf.DeltaAngle(rotY, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(rotY, -90f)) < 1f;
            var fitSize = quarter ? new Vector3(size.z, size.y, size.x) : size;
            // authored modules keep their axes (Level Designer's yaw/scale), only generic pieces are auto-oriented
            int[] map = real ? RealPieceRules.AxisMap(b0.size, fitSize) : new[] { 0, 1, 2 };
            string kitName = real ? RealPieceRules.KitOf(piece) : null;
            float tile = !real || kitName == "Props" ? -1f : RealPieceRules.TileM(kitName); // Props atlas / stylized kit: keep authored UVs
            var yaw = Quaternion.Euler(0f, rotY, 0f);
            var outM = new Mesh[src.Length];
            for (int i = 0; i < src.Length; i++) outM[i] = Fit(src[i].mesh, src[i].m, b0, map, fitSize, size, yaw, tile, piece + "_LOD" + i);
            return _meshCache[key] = outM;
        }

        static Bounds TransformedBounds(Mesh mesh, Matrix4x4 m)
        {
            var v = mesh.vertices; var b = new Bounds(m.MultiplyPoint3x4(v[0]), Vector3.zero);
            for (int i = 1; i < v.Length; i++) b.Encapsulate(m.MultiplyPoint3x4(v[i]));
            return b;
        }

        /// <summary>Bake the piece into the block's unit cube: permute axes (map), normalise to [-0.5,0.5], rebuild UV0 = scaled pos / tile_m.</summary>
        static Mesh Fit(Mesh src, Matrix4x4 m, Bounds b0, int[] map, Vector3 fitSize, Vector3 size, Quaternion yaw, float tile, string name)
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
                var u = new Vector3(pp.x / d.x, pp.y / d.y, pp.z / d.z);              // unit cube of fitSize
                var w = yaw * Vector3.Scale(u, fitSize);                               // metres in the block (after rotY)
                vo[i] = new Vector3(w.x / size.x, w.y / size.y, w.z / size.z);         // block unit cube (parent scale = s)
                Vector3 nn = n != null && n.Length == v.Length ? nm.MultiplyVector(n[i]) : Vector3.up;
                var pn = new Vector3(nn[map[0]], nn[map[1]], nn[map[2]]);
                var nWorld = (yaw * new Vector3(pn.x * d.x / fitSize.x, pn.y * d.y / fitSize.y, pn.z * d.z / fitSize.z)).normalized; // inverse-transpose
                no[i] = new Vector3(nWorld.x * size.x, nWorld.y * size.y, nWorld.z * size.z).normalized;
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
        public static void ResetCaches() { _meshCache.Clear(); _src.Clear(); _mats.Clear(); _srcMats.Clear(); _partCache.Clear(); _loaded = false; _m = null; Dressed = 0; }
    }
}
