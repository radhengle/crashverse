using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// Ensures M1 boots even if the Bootstrap scene has no component wired yet.
    /// </summary>
    public static class AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindObjectOfType<GameBootstrap>() != null) return;
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }
    }
}
