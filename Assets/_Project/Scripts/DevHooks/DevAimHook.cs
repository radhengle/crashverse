#if CRASHVERSE_DEV
// Security Admin: dev-only aim hook runner. See DevAimCommand.cs for the guard notes.
using System.IO;
using UnityEngine;

namespace Crashverse.DevHooks
{
    /// <summary>Polls Application.persistentDataPath/dev_aim_cmd.json every 0.25 s (also mid-level), reads + deletes it,
    /// and applies it through AimController/GameSession. Inert unless the app was launched with --ez cv_dev_aim true.</summary>
    public class DevAimHook : MonoBehaviour
    {
        const float PollSeconds = 0.25f;
        static DevAimHookCore _core;
        DevAimTarget _target;
        float _next;

        static DevAimHookCore Core => _core ??= new DevAimHookCore(ReadOptIn());

        public static void Attach(GameObject host, DevAimTarget target)
        {
            if (!Core.OptIn) return;
            host.AddComponent<DevAimHook>()._target = target;
            Debug.Log(DevAimHookCore.LogTag + " armed path=" + CommandPath);
        }

        public static void ReportDevAimResult(bool won)
        {
            if (_core != null && _core.OptIn) Debug.Log(DevAimHookCore.ResultLine(won));
        }

        static string CommandPath => Path.Combine(Application.persistentDataPath, DevAimHookCore.FileName);

        static bool ReadOptIn()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = act.Call<AndroidJavaObject>("getIntent"))
                    return intent.Call<bool>("getBooleanExtra", DevAimHookCore.OptInExtra, false);
            }
            catch { return false; }
#else
            return System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-" + DevAimHookCore.OptInExtra) >= 0;
#endif
        }

        void Update()
        {
            if (_target == null) return;
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + PollSeconds;
                string json = null;
                if (DevAimFileReader.TryReadAndDelete(CommandPath, out json, out var err) && err != null)
                    Debug.Log(DevAimHookCore.LogTag + " rejected reason=" + err);
                var l = _core.Offer(json, _target);
                if (l != null) Debug.Log(l);
            }
            var line = _core.Tick(_target);
            if (line != null) Debug.Log(line);
        }
    }
}
#endif
