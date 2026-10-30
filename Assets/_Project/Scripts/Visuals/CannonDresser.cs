using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    /// <summary>
    /// The Wrecker as in the concept art: navy barrel with gold bands, muzzle swell and cascabel on a gold-trimmed wooden truck carriage
    /// with spoked wheels (Resources/Kingdom/Meshes/Cannon_*.bytes, built by Tools/Blender/kingdom/build_assets.py). The procedural
    /// greybox parts keep their transforms, colliders and FeelDirector bindings; only their renderers are switched off. The carriage
    /// follows the yaw pivot (a real carriage turns with the barrel) and the barrel follows the pitch pivot (aim + recoil).
    /// </summary>
    public static class CannonDresser
    {
        public const float VisualScale = 1.12f;

        public static bool Dress(Crashverse.Weapons.WreckerBuilder wb)
        {
            if (wb == null || wb.yawPivot == null || wb.pitchPivot == null) return false;
            var barrel = CvMesh.Load("Kingdom/Meshes/Cannon_Barrel"); var carriage = CvMesh.Load("Kingdom/Meshes/Cannon_Carriage");
            if (barrel.Count == 0 || carriage.Count == 0) return false;
            foreach (var r in wb.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;

            var bRoot = new GameObject("Kingdom_Barrel").transform; bRoot.SetParent(wb.pitchPivot, false);
            bRoot.localScale = Vector3.one * VisualScale;
            foreach (var kv in barrel) Part(kv.Key, kv.Value, bRoot, 0f);

            var cRoot = new GameObject("Kingdom_Carriage").transform; cRoot.SetParent(wb.yawPivot, false);
            cRoot.localPosition = new Vector3(0f, -wb.yawPivot.localPosition.y, 0f) * VisualScale; // ground under the pivot
            cRoot.localPosition = new Vector3(0f, -wb.yawPivot.localPosition.y * VisualScale, 0f);
            cRoot.localScale = Vector3.one * VisualScale;
            foreach (var kv in carriage) Part(kv.Key, kv.Value, cRoot, kv.Key.EndsWith("_Wood") ? 0.9f : 0f);
            return true;
        }

        static void Part(string name, Mesh mesh, Transform parent, float boxTile)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = boxTile > 0f ? CvMesh.WithBoxUv(mesh, boxTile, Vector3.one) : mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = KingdomMaterials.ForTag(name);
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
        }
    }
}
