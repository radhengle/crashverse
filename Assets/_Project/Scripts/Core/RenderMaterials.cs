using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// BUG-23: every procedural renderer gets the one shared URP Simple Lit material (Resources, so it and its shader
    /// ship in the build). Colours stay per-renderer via MaterialPropertyBlock (_BaseColor). A renderer with no material,
    /// or with the built-in Default-Material (CreatePrimitive), renders magenta under URP.
    /// </summary>
    public static class RenderMaterials
    {
        static Material _lit;
        public static Material Lit
        {
            get
            {
                if (_lit != null) return _lit;
                _lit = Resources.Load<Material>("Materials/CV_SimpleLit");
                if (_lit == null || _lit.shader == null || !_lit.shader.isSupported)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                    Debug.LogError("[RenderMaterials] CV_SimpleLit missing or unsupported; falling back to " + (sh != null ? sh.name : "none"));
                    if (sh != null) _lit = new Material(sh) { name = "CV_SimpleLit_Fallback" };
                }
                return _lit;
            }
        }

        static bool _nullLogged;
        public static void Apply(Renderer r)
        {
            if (r == null || r is LineRenderer || r is ParticleSystemRenderer) return;
            var m = Lit;
            if (m == null) { if (!_nullLogged) { _nullLogged = true; Debug.LogError("[RenderMaterials] BUG-23: no usable URP material (CV_SimpleLit and fallbacks all null); '" + r.name + "' and later renderers will draw magenta"); } return; }
            r.sharedMaterial = m;
        }
        public static void Apply(GameObject go) { if (go != null) Apply(go.GetComponent<Renderer>()); }

        /// <summary>Safety net after a world is built: any mesh renderer without a usable material gets the shared one.</summary>
        public static int FixAll(GameObject root)
        {
            int fixedCount = 0; if (root == null) return 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name == "Standard") { r.sharedMaterial = Lit; fixedCount++; }
            }
            return fixedCount;
        }

        /// <summary>For tests: renderers that would draw magenta.</summary>
        public static int CountBroken(GameObject root)
        {
            int n = 0; if (root == null) return 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name == "Standard" || m.shader.name.StartsWith("Hidden/InternalErrorShader")) n++;
            }
            return n;
        }
    }
}
