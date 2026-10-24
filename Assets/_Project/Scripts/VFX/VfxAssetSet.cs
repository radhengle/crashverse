using UnityEngine;

namespace Crashverse.VFX
{
    /// <summary>
    /// Runtime handle to the generated VFX textures + particle shader (Resources/VFX/VfxAssetSet), so a VfxDirector created from code
    /// (dev scene bootstrap, VfxPerfProbe) gets real materials. Also keeps the shader/textures in every build that references it.
    /// </summary>
    [CreateAssetMenu(menuName = "Crashverse/VFX Asset Set", fileName = "VfxAssetSet")]
    public class VfxAssetSet : ScriptableObject
    {
        public VfxAssets assets = new VfxAssets();
        public Texture2D crackMask;
        public const string ResourcePath = "VFX/VfxAssetSet";
        static VfxAssetSet _cached;
        public static VfxAssetSet Load() => _cached != null ? _cached : (_cached = Resources.Load<VfxAssetSet>(ResourcePath));
    }
}
