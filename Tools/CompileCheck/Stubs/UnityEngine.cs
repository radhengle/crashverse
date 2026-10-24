using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object obj) { }
        public static void Destroy(Object obj, float t) { }
        public static void DestroyImmediate(Object obj) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static T FindObjectOfType<T>() where T : Object => null;
        public static Object Instantiate(Object original) => original is GameObject g ? new GameObject(g.name) : original;
        public static Object Instantiate(Object original, Transform parent) => Instantiate(original);
    }

    public class GameObject : Object
    {
        public Transform transform { get; } = new Transform(); public T[] GetComponentsInChildren<T>(bool inc = false) => new T[0];
        public string tag;
        public int layer;
        public bool activeSelf = true;
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public GameObject(string name, params Type[] comps) { this.name = name; }
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject(type.ToString());
        public T AddComponent<T>() where T : Component, new() { var c = new T(); c.gameObject = this; return c; }
        public T GetComponent<T>() where T : class => null;
        public T GetComponentInChildren<T>() where T : class => null;
        public T GetComponentInParent<T>() where T : class => null;
        public T[] GetComponents<T>() where T : class => Array.Empty<T>();
        public void SetActive(bool v) { activeSelf = v; }
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public string tag { get => gameObject != null ? gameObject.tag : null; set { if (gameObject != null) gameObject.tag = value; } }
        public Transform transform => gameObject != null ? gameObject.transform : new Transform();
        public T GetComponent<T>() where T : class => gameObject?.GetComponent<T>();
        public T GetComponentInChildren<T>() where T : class => null;
        public T GetComponentInParent<T>() where T : class => null;
        public T[] GetComponents<T>() where T : class => Array.Empty<T>();
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class => Array.Empty<T>();
    }

    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour
    {
        public void Invoke(string methodName, float time) { }
    }

    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 localPosition;
        public Vector3 localScale = Vector3.one;
        public Quaternion rotation = Quaternion.identity;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 right => localRotation * Vector3.right;
        public Vector3 TransformPoint(Vector3 p) => p;
        public void SetAsFirstSibling() { }
        public Vector3 forward => localRotation * Vector3.forward;
        public Vector3 up => localRotation * Vector3.up;
        public int childCount => 0;
        public Transform parent;
        public void SetParent(Transform p, bool world = true) { parent = p; }
        public Transform GetChild(int i) => null;
        public void LookAt(Vector3 t) { }
        public void SetAsLastSibling() { }
        public void RotateAround(Vector3 p, Vector3 a, float d) { }
        public System.Collections.IEnumerator GetEnumerator() { yield break; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 back => new Vector3(0, 0, -1);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 right => new Vector3(1, 0, 0);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
        public Vector3 normalized { get { float m = (float)Math.Sqrt(sqrMagnitude); return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : zero; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static float Min(float a, float b) => Math.Min(a, b);
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity => new Quaternion { w = 1 };
        public static Quaternion Euler(float x, float y, float z) => identity;
        public static Quaternion FromToRotation(Vector3 a, Vector3 b) => identity;
        public static Quaternion AngleAxis(float d, Vector3 a) => identity;
        public static Vector3 operator *(Quaternion q, Vector3 v) => v;
        public static Quaternion operator *(Quaternion a, Quaternion b) => a;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f);
        public static Color cyan => new Color(0, 1, 1);
        public static Color Lerp(Color a, Color b, float t) => a;
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string html, out Color color) { color = Color.white; return true; }
    }

    public static class Mathf
    {
        public static float PerlinNoise(float x, float y) => 0.5f;
        public static int CeilToInt(float f) => (int)System.Math.Ceiling(f);
        public static float Clamp(float v, float min, float max) => Math.Min(max, Math.Max(min, v));
        public static int Clamp(int v, int min, int max) => Math.Min(max, Math.Max(min, v));
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public const float PI = (float)Math.PI;
        public static float Sin(float v) => (float)Math.Sin(v); public static float Cos(float v) => (float)Math.Cos(v);
        public static int RoundToInt(float v) => (int)Math.Round(v); public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }

    public static class Time
    {
        public static float unscaledTime;
        public static float deltaTime = 0.016f;
        public static float fixedDeltaTime = 0.02f;
        public static float fixedTime;
        public static float timeScale = 1f, time, unscaledDeltaTime = 0.016f;
    }

    public static class Input
    {
        public static Vector3 mousePosition;
        public static bool GetMouseButton(int b) => false;
        public static bool GetMouseButtonDown(int b) => false;
        public static bool GetMouseButtonUp(int b) => false;
        public static float GetAxis(string name) => 0f;
        public static int touchCount;
        public static Touch GetTouch(int i) => default;
        public static bool GetKeyDown(KeyCode k) => false;
    }

    public static class Screen { public static int width = 1080; public static int height = 1920; public static float dpi = 420; public static Rect safeArea = new Rect(0, 0, 1080, 1920); public static ScreenOrientation orientation; }
    public static class Application
    {
        public static int targetFrameRate;
        public static string persistentDataPath = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "crashverse_test")).FullName; // exists on fresh CI runners
        public static void Quit() { }
        public static RuntimePlatform platform = RuntimePlatform.LinuxEditor;
    }
    public enum RuntimePlatform { LinuxEditor, Android }
    public class TextAsset : Object { public string text; }
    public static class Resources
    {
        public static string Root = "../../Assets/_Project/Resources";
        public static T Load<T>(string path) where T : Object
        {
            var f = System.IO.Path.Combine(Root, path + ".json");
            if (!System.IO.File.Exists(f)) return null;
            object ta = new TextAsset { text = System.IO.File.ReadAllText(f) };
            return ta as T;
        }
        public static T GetBuiltinResource<T>(string n) where T : Object, new() => new T();
    }
    public static class JsonUtility
    {
        static readonly System.Text.Json.JsonSerializerOptions O = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        public static T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, O);
        public static string ToJson(object o) => System.Text.Json.JsonSerializer.Serialize(o, o.GetType(), O);
        public static string ToJson(object o, bool pretty) => ToJson(o);
    }
    public static class QualitySettings
    {
        public static string[] names = { "Low", "Mid" };
        public static void SetQualityLevel(int i, bool a) { }
    }
    public struct RaycastHit { public Vector3 point; public float distance; public Collider collider; }
    public static class Physics
    {
        public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit hit, float max) { hit = default; return false; }
        public static Vector3 gravity = new Vector3(0, -9.81f, 0);
        public static float sleepThreshold;
        public static int defaultSolverIterations;
        public static Collider[] OverlapSphere(Vector3 p, float r) => Array.Empty<Collider>();
        public static SimulationMode simulationMode = SimulationMode.FixedUpdate;
        public static void Simulate(float step) { }
        public static void IgnoreLayerCollision(int a, int b, bool ignore) { }
    }
    public enum SimulationMode { FixedUpdate, Update, Script }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrderAttribute : Attribute { public DefaultExecutionOrderAttribute(int order) { } }
    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode;
        public static Color ambientSkyColor, ambientEquatorColor, ambientGroundColor, ambientLight, fogColor;
        public static float ambientIntensity, fogStartDistance, fogEndDistance;
        public static bool fog;
        public static FogMode fogMode;
    }
    public enum FogMode { Linear }
    public enum PrimitiveType { Cube, Sphere, Cylinder, Plane, Capsule }
    public enum LightType { Directional, Point, Spot }
    public enum LightShadows { None, Soft, Hard }
    public enum CameraClearFlags { Skybox, SolidColor, Depth, Nothing }
    public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }
    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum Space { World, Self }
    public enum ForceMode { Force, Impulse, VelocityChange, Acceleration }

    public class Rigidbody : Component
    {
        public float mass = 1;
        public bool isKinematic;
        public bool detectCollisions = true;
        public bool useGravity = true;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public CollisionDetectionMode collisionDetectionMode;
        public RigidbodyInterpolation interpolation;
        public float sleepThreshold;
        public void WakeUp() { }
        public Vector3 position;
        public void AddForce(Vector3 f, ForceMode m = ForceMode.Force) { }
        public void AddExplosionForce(float f, Vector3 p, float r, float up = 0f, ForceMode m = ForceMode.Force) { }
    }

    public class Collider : Component { public bool enabled = true; public bool isTrigger; public Rigidbody attachedRigidbody; }
    public class MeshCollider : Collider { public Mesh sharedMesh; public bool convex; }
    public class Renderer : Component
    {
        public bool enabled = true; public Material sharedMaterial; public Material material;
        public void GetPropertyBlock(MaterialPropertyBlock b) { }
        public void SetPropertyBlock(MaterialPropertyBlock b) { }
    }
    public class MaterialPropertyBlock
    {
        public void SetColor(string n, Color c) { }
        public void SetFloat(string n, float f) { }
    }
    public class Joint : Component { public Rigidbody connectedBody; public float breakForce, breakTorque; }
    public class FixedJoint : Joint { }
    public class Light : Behaviour
    {
        public LightType type;
        public Color color;
        public float intensity;
        public LightShadows shadows;
        public float shadowStrength;
    }
    public class Camera : Behaviour
    {
        public static Camera main;
        public float fieldOfView;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public Vector3 WorldToScreenPoint(Vector3 p) => p;
    }
    public class AudioListener : Behaviour { }
    public class Collision
    {
        public Collider collider; public Rigidbody rigidbody; public GameObject gameObject; public Vector3 relativeVelocity;
        public ContactPoint[] contacts = Array.Empty<ContactPoint>();
    }
    public struct ContactPoint { public Vector3 point; }
    public static class Gizmos
    {
        public static Color color;
        public static void DrawLine(Vector3 a, Vector3 b) { }
    }
    public static class Debug { public static void Log(object m) { } public static void LogError(object m) { System.Console.WriteLine("ERR " + m); } public static void LogWarning(object m) { } }
    public static class Random
    {
        public static float value => 0.5f;
        public static Vector3 insideUnitSphere => Vector3.one;
        public static Quaternion rotation => Quaternion.identity;
    }
    public static class PlayerPrefs
    {
        public static bool HasKey(string k) => false;
        public static void DeleteAll() { }
        public static void DeleteKey(string k) { }
        static Dictionary<string, int> _i = new Dictionary<string, int>();
        public static int GetInt(string k, int d = 0) => _i.TryGetValue(k, out var v) ? v : d;
        public static void SetInt(string k, int v) => _i[k] = v;
        public static void Save() { }
    }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }
    public class SerializeFieldAttribute : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type t) { } }
    public class CreateAssetMenuAttribute : Attribute
    {
        public string fileName; public string menuName; public int order;
    }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, SubsystemRegistration, BeforeSplashScreen }
    public class GUIStyle
    {
        public int fontSize; public FontStyle fontStyle;
        public GUIStyleState normal = new GUIStyleState();
        public GUIStyle() { }
        public GUIStyle(GUIStyle s) { }
    }
    public class GUIStyleState { public Color textColor; }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float xMin => x; public float yMin => y; public float xMax => x + width; public float yMax => y + height;
        public Vector2 center => new Vector2(x + width / 2, y + height / 2);
        public static bool operator ==(Rect a, Rect b) => a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
        public static bool operator !=(Rect a, Rect b) => !(a == b);
        public override bool Equals(object o) => o is Rect r && r == this; public override int GetHashCode() => 0;
    }
    public class GUIContent { public static GUIContent none = new GUIContent(); }
    public static class GUI
    {
        public static bool enabled = true;
        public static GUISkin skin = new GUISkin();
        public static void Box(Rect r, GUIContent c) { }
        public static void Label(Rect r, string s) { }
        public static void Label(Rect r, string s, GUIStyle st) { }
        public static bool Button(Rect r, string s) => false;
        public static bool Button(Rect r, string s, GUIStyle st) => false;
    }
    public class GUISkin { public GUIStyle label = new GUIStyle(); public GUIStyle button = new GUIStyle(); }
    public enum AudioClipLoadType { DecompressOnLoad, CompressedInMemory, Streaming }
    public class AudioClip : Object { public int samples; public int frequency = 44100; public AudioClipLoadType loadType; }
    public static class AudioSettings { public static double dspTime; }
    public class AudioSource : Behaviour
    {
        public AudioClip clip; public bool loop, playOnAwake, isPlaying; public float volume = 1f, spatialBlend; public int priority, timeSamples;
        public UnityEngine.Audio.AudioMixerGroup outputAudioMixerGroup;
        public void PlayScheduled(double t) { isPlaying = true; } public void SetScheduledEndTime(double t) { } public void Stop() { isPlaying = false; }
    }

}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
}
namespace UnityEngine.Audio { public class AudioMixerGroup : UnityEngine.Object { } }
