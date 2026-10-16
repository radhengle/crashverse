#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using C = Crashverse.EditorTools.Import.ImportConventions;

namespace Crashverse.EditorTools.Import
{
    /// <summary>
    /// Menu tool: one prefab per model (LOD files grouped) under Assets/_Project/Prefabs/Models/&lt;Folder&gt;/.
    /// LODGroup, colliders from UCX_/_COL hulls (convex), fallbacks, sockets preserved, fragments flagged by tag only.
    /// No physics components (Rigidbody, joints, destruction) are added: that is the Physics team's job.
    /// Prefabs are plain (unlinked) copies; re-run the menu after a model changes.
    /// </summary>
    public static class CrashversePrefabBuilder
    {
        [MenuItem("Crashverse/Import/Build Prefabs (all models)")]
        public static void BuildAll()
        {
            int n = 0;
            foreach (var g in ModelGroups()) if (Build(g.Key, g.Value) != null) n++;
            AssetDatabase.SaveAssets();
            Debug.Log($"[CrashverseImport] Built {n} prefabs in {C.PrefabRoot}.");
        }

        [MenuItem("Crashverse/Import/Build Prefabs (selected models)")]
        public static void BuildSelected()
        {
            var sel = new HashSet<string>(Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath));
            foreach (var g in ModelGroups())
                if (g.Value.Values.Any(sel.Contains) || sel.Any(s => g.Value.Values.Any(v => v.StartsWith(s + "/")))) Build(g.Key, g.Value);
            AssetDatabase.SaveAssets();
        }

        public static IEnumerable<string> ModelDirs() =>
            AssetDatabase.FindAssets("t:Model", new[] { C.ModelsRoot.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .Select(p => Path.GetDirectoryName(p).Replace('\\', '/')).Distinct();

        /// <summary>key = dir/base name, value = LOD level (-1 = single file) -> fbx path.</summary>
        public static Dictionary<string, Dictionary<int, string>> ModelGroups()
        {
            var groups = new Dictionary<string, Dictionary<int, string>>();
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { C.ModelsRoot.TrimEnd('/') }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                int lvl = C.LodLevel(C.FileStem(p), out string b);
                string key = Path.GetDirectoryName(p).Replace('\\', '/') + "/" + b;
                if (!groups.TryGetValue(key, out var d)) groups[key] = d = new Dictionary<int, string>();
                d[lvl] = p;
            }
            return groups;
        }

        public static GameObject Build(string key, Dictionary<int, string> files)
        {
            string baseName = Path.GetFileName(key);
            string lod0Path = files.TryGetValue(0, out var l0) ? l0 : files.TryGetValue(-1, out var s) ? s : null;
            if (lod0Path == null) { Debug.LogWarning($"[CrashverseImport] {key}: no LOD0 or single FBX, skipped."); return null; }
            var cls = C.Classify(lod0Path);

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(lod0Path);
            var root = (GameObject)Object.Instantiate(src);
            root.name = baseName;
            try
            {
                // Per-file LODs (v3: Wrecker_v2_LOD1.fbx) become children "LOD1"/"LOD2" with helpers and hulls stripped.
                var lodRenderers = new List<Renderer>[3] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
                foreach (var kv in files.Where(f => f.Key >= 1))
                {
                    var lodGo = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value), root.transform);
                    lodGo.name = "LOD" + kv.Key;
                    StripNonRender(lodGo.transform);
                    lodRenderers[kv.Key].AddRange(lodGo.GetComponentsInChildren<Renderer>(true));
                }

                BuildColliders(root.transform);

                // In-file LOD suffixes (v1: Part_LOD0/Part_LOD1) or plain renderers (LOD0).
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (lodRenderers[1].Contains(r) || lodRenderers[2].Contains(r)) continue;
                    int lvl = C.LodLevel(r.name, out _);
                    lodRenderers[lvl < 0 ? 0 : lvl].Add(r);
                }

                var h = C.LodHeights(cls);
                var lods = new List<LOD>();
                for (int i = 0; i < 3; i++) if (lodRenderers[i].Count > 0) lods.Add(new LOD(h[i], lodRenderers[i].ToArray()));
                if (lods.Count > 1)
                {
                    // Cull height always sits on the last LOD present.
                    var last = lods[lods.Count - 1]; last.screenRelativeTransitionHeight = h[2]; lods[lods.Count - 1] = last;
                    var lg = root.GetComponent<LODGroup>(); if (lg == null) lg = root.AddComponent<LODGroup>();
                    lg.fadeMode = LODFadeMode.None;      // no cross-fade on mobile (extra variants + overdraw)
                    lg.SetLODs(lods.ToArray());
                    lg.RecalculateBounds();
                }

                FlagFragments(root.transform);
                SortOrdinal(root.transform);
                if (C.IsStatic(cls))
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);

                string folder = $"{C.PrefabRoot}/{Path.GetFileName(Path.GetDirectoryName(lod0Path))}";
                EnsureFolder(folder);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{baseName}.prefab");
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>LOD1/LOD2 copies keep only renderers: hulls, sockets and joints live on the LOD0 hierarchy.</summary>
        static void StripNonRender(Transform t)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true).Where(x => x != t).ToArray())
                if (c != null && (C.ColliderTarget(c.name) != null || C.IsHelper(c.name))) Object.DestroyImmediate(c.gameObject);
        }

        static void BuildColliders(Transform root)
        {
            var hulls = root.GetComponentsInChildren<Transform>(true).Where(t => C.ColliderTarget(t.name) != null).ToArray();
            var hasCol = new HashSet<Transform>();
            foreach (var hull in hulls)
            {
                var mf = hull.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                string tname = C.ColliderTarget(hull.name);
                var target = tname == root.name ? root : CrashverseImportPostprocessor.FindDeep(root, tname);
                if (target == null)
                {
                    // No root fallback (Physics review): a misnamed hull must not silently collide the whole model.
                    Debug.LogError($"[CrashverseImport] {root.name}: hull {hull.name} targets '{tname}', which does not exist. Skipped (no collider).");
                    Object.DestroyImmediate(hull.gameObject);
                    continue;
                }
                bool samePose = (hull.position - target.position).sqrMagnitude < 1e-8f && Quaternion.Angle(hull.rotation, target.rotation) < 0.01f && (hull.lossyScale - target.lossyScale).sqrMagnitude < 1e-8f;
                var host = samePose ? target : NewChild(target, "COL_" + target.name, hull);
                var mc = host.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
                hasCol.Add(target);
                Object.DestroyImmediate(hull.gameObject);
            }

            // Fallbacks: fragments get a convex hull of their own mesh; anything else with no collider gets one root BoxCollider.
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (C.IsFragment(mf.name) && C.LodLevel(mf.name, out _) <= 0 && !hasCol.Contains(mf.transform) && mf.GetComponent<Collider>() == null)
                { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.convex = true; hasCol.Add(mf.transform); }

            if (hasCol.Count == 0)
            {
                var rs = root.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) return;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var bc = root.gameObject.AddComponent<BoxCollider>();
                bc.center = root.InverseTransformPoint(b.center);
                bc.size = b.size;   // root is at identity scale/rotation per the models README
            }
        }

        static Transform NewChild(Transform parent, string name, Transform pose)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            go.transform.localScale = pose.localScale;
            return go.transform;
        }

        /// <summary>Fragments and JNT_ empties are reordered by ordinal name within each parent (deterministic sibling order for Physics).
        /// They take the sibling slots they already occupied, so other children keep their positions.</summary>
        static void SortOrdinal(Transform root)
        {
            foreach (var parent in root.GetComponentsInChildren<Transform>(true))
            {
                var kids = new List<Transform>();
                for (int i = 0; i < parent.childCount; i++) kids.Add(parent.GetChild(i));
                var picked = kids.Where(k => C.IsFragment(k.name) || C.IsJoint(k.name)).ToList();
                if (picked.Count < 2) continue;
                var slots = picked.Select(k => k.GetSiblingIndex()).OrderBy(i => i).ToList();
                var sorted = picked.OrderBy(k => C.IsJoint(k.name) ? 1 : 0).ThenBy(k => k.name, System.StringComparer.Ordinal).ToList();
                for (int i = 0; i < sorted.Count; i++) sorted[i].SetSiblingIndex(slots[i]);
            }
        }

        static void FlagFragments(Transform root)
        {
            bool tagExists = InternalEditorUtility.tags.Contains(C.FragmentTag);
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (C.IsFragment(t.name) && C.LodLevel(t.name, out _) <= 0) { n++; if (tagExists) t.gameObject.tag = C.FragmentTag; }
            if (n > 0 && !tagExists)
                Debug.LogWarning($"[CrashverseImport] {root.name}: {n} fragments found by name (*_frag_##) but tag '{C.FragmentTag}' is not in TagManager; ask Developer to add it. Names remain the marker.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
#endif
