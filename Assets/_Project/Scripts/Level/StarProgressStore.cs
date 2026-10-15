using UnityEngine;

namespace Crashverse.Level
{
    /// <summary>Persists best star count per level id. Lower retry never overwrites higher best.</summary>
    public static class StarProgressStore
    {
        const string Prefix = "crashverse.bestStars.";

        public static int GetBest(string levelId)
        {
            return PlayerPrefs.GetInt(Prefix + levelId, 0);
        }

        public static void Submit(string levelId, int stars)
        {
            if (stars <= 0) return;
            int best = GetBest(levelId);
            if (stars > best)
            {
                PlayerPrefs.SetInt(Prefix + levelId, stars);
                PlayerPrefs.Save();
            }
        }
    }
}
