// Minimal API-shaped stubs (signatures follow Unity 2022.3 docs) so the import pipeline type-checks without Unity.
using System;
namespace UnityEngine
{
    public class Object { public string name; public static implicit operator bool(Object o) => o != null;
        public static Object Instantiate(Object o) => o; public static Object Instantiate(Object o, Transform p) => o; public static void DestroyImmediate(Object o) { } }
    public class Component : Object { public GameObject gameObject; public Transform transform => null; public T GetComponent<T>() => default; }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    public class GameObject : Object { public GameObject(string n) { } public Transform transform => null; public string tag;
        public T GetComponent<T>() => default; public T AddComponent<T>() where T : Component => default; public T[] GetComponentsInChildren<T>(bool inc) => null; }
    public class Transform : Component { public Vector3 position; public Quaternion rotation; public Vector3 localScale; public Vector3 lossyScale => default; public int childCount => 0;
        public Transform root => null; public Transform GetChild(int i) => null; public void SetParent(Transform p, bool w) { } public void SetPositionAndRotation(Vector3 p, Quaternion r) { }
        public int GetSiblingIndex() => 0; public void SetSiblingIndex(int i) { } public Vector3 InverseTransformPoint(Vector3 p) => p; public T[] GetComponentsInChildren<T>(bool inc) => null; }
    public class Renderer : Component { public Bounds bounds; public Material[] sharedMaterials; }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class Collider : Component { }
    public class MeshCollider : Collider { public Mesh sharedMesh; public bool convex; }
    public class BoxCollider : Collider { public Vector3 center, size; }
    public enum LODFadeMode { None, CrossFade, SpeedTree }
    public struct LOD { public float screenRelativeTransitionHeight; public Renderer[] renderers; public LOD(float h, Renderer[] r) { screenRelativeTransitionHeight = h; renderers = r; } }
    public class LODGroup : Component { public LODFadeMode fadeMode; public void SetLODs(LOD[] l) { } public void RecalculateBounds() { } }
    public class Mesh : Object { public Bounds bounds; public int subMeshCount; public uint GetIndexCount(int s) => 0; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
    public class Shader : Object { public static Shader Find(string n) => null; }
    public class Texture : Object { public int width, height; }
    public class Texture2D : Texture { }
    public class Material : Object { public Material(Shader s) { } public Shader shader; public bool HasProperty(string n) => true; public void SetTexture(string n, Texture t) { }
        public Texture GetTexture(string n) => null; public void SetFloat(string n, float v) { } public float GetFloat(string n) => 0; public Color GetColor(string n) => default; public void SetColor(string n, Color c) { } }
    public struct Color { public float r, g, b, a; public float maxColorComponent => 0; public static Color white => default; public static Color operator *(Color c, float f) => c; }
    public struct Vector2 { public float x, y; }
    public struct Vector3 { public float x, y, z; public float sqrMagnitude => 0; public float magnitude => 0; public static Vector3 Scale(Vector3 a, Vector3 b) => a;
        public static Vector3 Cross(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a; }
    public struct Quaternion { public static float Angle(Quaternion a, Quaternion b) => 0; }
    public struct Bounds { public Vector3 center, size; public void Encapsulate(Bounds b) { } }
    public static class Mathf { public static float Max(float a, float b) => a > b ? a : b; }
    public struct Hash128 { public static Hash128 Compute(string s) => default; }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) { } }
}
namespace UnityEditor
{
    using UnityEngine;
    public class AssetImporter : Object { public static AssetImporter GetAtPath(string p) => null; }
    public class AssetImportContext { public void DependsOnCustomDependency(string name) { } }
    [AttributeUsage(AttributeTargets.Class)] public class InitializeOnLoadAttribute : Attribute { }
    [Flags] public enum ImportAssetOptions { Default = 0, ForceUpdate = 1, ForceSynchronousImport = 8, ImportRecursive = 256 }
    public class AssetPostprocessor { public string assetPath; public AssetImporter assetImporter; public AssetImportContext context; public virtual int GetPostprocessOrder() => 0; public virtual uint GetVersion() => 0; }
    public enum ModelImporterMeshCompression { Off, Low, Medium, High }
    public enum ModelImporterNormals { Import, Calculate, None }
    public enum ModelImporterTangents { Import, CalculateLegacy, CalculateLegacyWithSplitTangents, CalculateMikk, None }
    public enum ModelImporterAnimationType { None, Legacy, Generic, Human }
    public enum ModelImporterMaterialImportMode { None, ImportStandard, ImportViaMaterialDescription }
    public enum ModelImporterMaterialLocation { External, InPrefab }
    public enum ModelImporterMaterialName { BasedOnTextureName, BasedOnMaterialName, BasedOnModelNameAndMaterialName }
    public enum ModelImporterMaterialSearch { Local, RecursiveUp, Everywhere }
    public class ModelImporter : AssetImporter { public float globalScale; public bool useFileScale, bakeAxisConversion, isReadable, optimizeMeshPolygons, optimizeMeshVertices, weldVertices,
        importBlendShapes, importCameras, importLights, importVisibility, addCollider, generateSecondaryUV, importAnimation; public float secondaryUVPackMargin, secondaryUVHardAngle;
        public ModelImporterMeshCompression meshCompression; public ModelImporterNormals importNormals; public ModelImporterTangents importTangents; public ModelImporterAnimationType animationType;
        public ModelImporterMaterialImportMode materialImportMode; public ModelImporterMaterialLocation materialLocation; public ModelImporterMaterialName materialName; public ModelImporterMaterialSearch materialSearch; }
    public enum TextureImporterType { Default, NormalMap }
    public enum TextureImporterAlphaSource { None, FromInput, FromGrayScale }
    public enum TextureImporterCompression { Uncompressed, Compressed, CompressedHQ, CompressedLQ }
    public enum TextureImporterFormat { Automatic, ASTC_4x4, ASTC_5x5, ASTC_6x6, ASTC_8x8 }
    public class TextureImporterPlatformSettings { public bool overridden; public int maxTextureSize; public TextureImporterFormat format; public int compressionQuality; }
    public class TextureImporter : AssetImporter { public TextureImporterType textureType; public bool sRGBTexture, mipmapEnabled, streamingMipmaps, isReadable; public TextureImporterAlphaSource alphaSource;
        public int anisoLevel, maxTextureSize; public TextureImporterCompression textureCompression;
        public TextureImporterPlatformSettings GetPlatformTextureSettings(string p) => null; public void SetPlatformTextureSettings(TextureImporterPlatformSettings s) { } }
    public static class AssetDatabase { public static T LoadAssetAtPath<T>(string p) where T : Object => null; public static void CreateAsset(Object o, string p) { } public static void SaveAssets() { }
        public static void RegisterCustomDependency(string n, Hash128 h) { } public static void Refresh() { } public static string[] GetSubFolders(string p) => null;
        public static void ImportAsset(string p, ImportAssetOptions o) { }
        public static string[] FindAssets(string f, string[] d) => null; public static string GUIDToAssetPath(string g) => null; public static bool IsValidFolder(string p) => true; public static string CreateFolder(string a, string b) => null; }
    public static class EditorUtility { public static void SetDirty(Object o) { } }
    public static class PrefabUtility { public static GameObject SaveAsPrefabAsset(GameObject g, string p) => g; }
    public static class Selection { public static string[] assetGUIDs; }
    [Flags] public enum StaticEditorFlags { ContributeGI = 1, OccluderStatic = 2, BatchingStatic = 4, NavigationStatic = 8, OccludeeStatic = 16, OffMeshLinkGeneration = 32, ReflectionProbeStatic = 64 }
    public static class GameObjectUtility { public static void SetStaticEditorFlags(GameObject g, StaticEditorFlags f) { } }
    [AttributeUsage(AttributeTargets.Method)] public class MenuItem : Attribute { public MenuItem(string p) { } }
}
namespace UnityEditorInternal { public static class InternalEditorUtility { public static string[] tags; } }
