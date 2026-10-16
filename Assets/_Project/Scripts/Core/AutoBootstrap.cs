using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// Ensures M1 boots even if the Bootstrap scene has no component wired yet.
    /// </summary>
    public static class AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        /// <summary>Set by a dev scene driver (e.g. the L10 perf stage) in Awake so the normal map/age-gate boot doesn't run there.</summary>
        public static bool SuppressForDevScene;

        static void Boot()
        {
            if (SuppressForDevScene) return;
            // Unity Test Framework PlayMode init scene: tests load their own scene (e.g. the L10 perf scene); don't boot the game.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("InitTestScene", System.StringComparison.Ordinal)) return;
            if (Object.FindObjectOfType<GameBootstrap>() != null) return;
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }
    }
}
