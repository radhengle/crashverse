// Minimal UnityEngine.SceneManagement stubs for CompileCheck (no Unity install on CI's fast path).
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name, path; public int buildIndex; }
    public enum LoadSceneMode { Single, Additive }
    public struct LoadSceneParameters { public LoadSceneParameters(LoadSceneMode m) { } }
    public static class SceneManager
    {
        public static event System.Action<Scene, LoadSceneMode> sceneLoaded;
        public static Scene GetActiveScene() => new Scene { name = "Bootstrap", path = "" };
        public static void LoadScene(int buildIndex) { sceneLoaded?.Invoke(GetActiveScene(), LoadSceneMode.Single); }
        public static void LoadScene(string name) { sceneLoaded?.Invoke(GetActiveScene(), LoadSceneMode.Single); }
    }
}
