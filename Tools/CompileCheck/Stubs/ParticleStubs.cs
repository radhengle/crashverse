namespace UnityEngine
{
    // Minimal ParticleSystem surface used by the dev-only PerfDummyPowerUpLane (compile check only).
    public class ParticleSystem : Component
    {
        public struct MinMaxCurve { public float constant; public static implicit operator MinMaxCurve(float v) => new MinMaxCurve { constant = v }; }
        public struct MainModule { public bool loop, playOnAwake; public int maxParticles; public MinMaxCurve startLifetime, startSpeed, startSize; }
        public struct EmissionModule { public bool enabled; }
        public MainModule main => default;
        public EmissionModule emission => default;
        public int particleCount;
        public void Emit(int count) { particleCount += count; }
    }
    public class ParticleSystemRenderer : Renderer { public Material sharedMaterial; }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name, path; public int buildIndex; }
    public enum LoadSceneMode { Single, Additive }
    public struct LoadSceneParameters { public LoadSceneParameters(LoadSceneMode m) { } }
    public static class SceneManager { public static Scene GetActiveScene() => new Scene { name = "Bootstrap", path = "" }; public static void LoadScene(int buildIndex) { } public static event System.Action<Scene, LoadSceneMode> sceneLoaded; static void Touch() => sceneLoaded?.Invoke(default, default); }
}
