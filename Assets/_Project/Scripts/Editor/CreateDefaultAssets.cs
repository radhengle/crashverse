#if UNITY_EDITOR
using Crashverse.Destruction;
using Crashverse.Level;
using UnityEditor;
using UnityEngine;

namespace Crashverse.Editor
{
    public static class CreateDefaultAssets
    {
        [MenuItem("Crashverse/Create Default Material & Level Assets")]
        public static void Create()
        {
            EnsureDir("Assets/_Project/Data/Materials");
            EnsureDir("Assets/_Project/Data/Levels");

            SaveMat(MaterialDefinition.CreateRuntime(MaterialKind.Wood), "Assets/_Project/Data/Materials/Mat_Wood.asset");
            SaveMat(MaterialDefinition.CreateRuntime(MaterialKind.Brick), "Assets/_Project/Data/Materials/Mat_Brick.asset");
            SaveMat(MaterialDefinition.CreateRuntime(MaterialKind.Steel), "Assets/_Project/Data/Materials/Mat_Steel.asset");

            var level = LevelConfig.CreateM1ClockworkTower();
            AssetDatabase.CreateAsset(level, "Assets/_Project/Data/Levels/Level_m1_clockwork_tower.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("Crashverse default assets created.");
        }

        static void SaveMat(MaterialDefinition mat, string path)
        {
            AssetDatabase.CreateAsset(mat, path);
        }

        static void EnsureDir(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }
        }
    }
}
#endif
