using UnityEngine;

namespace Crashverse.Feel
{
    /// <summary>
    /// Android vibrator: one short VibrationEffect.createOneShot per strength bucket, built once and cached (API 26+); API &lt; 26 falls back to
    /// vibrate(long). Per call: one cached object[] args array + the JNI call itself, no managed allocation. Needs android.permission.VIBRATE.
    /// Everything JNI is compiled only on device (UNITY_ANDROID &amp;&amp; !UNITY_EDITOR); elsewhere this is a silent no-op.
    /// </summary>
    public sealed class AndroidHaptics : IHaptics
    {
        HapticGate _gate;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _vibrator; readonly AndroidJavaObject[] _effects = new AndroidJavaObject[HapticBuckets.Count + 1];
        readonly object[][] _args = new object[HapticBuckets.Count + 1][]; bool _ok, _amp, _api26;

        public AndroidHaptics()
        {
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (_vibrator == null || !_vibrator.Call<bool>("hasVibrator")) return;
                using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) _api26 = ver.GetStatic<int>("SDK_INT") >= 26;
                _amp = _api26 && _vibrator.Call<bool>("hasAmplitudeControl");
                for (int b = 1; b <= HapticBuckets.Count; b++)
                {
                    long ms = HapticBuckets.DurationMs(b);
                    if (_api26)
                    {
                        using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                            _effects[b] = ve.CallStatic<AndroidJavaObject>("createOneShot", ms, _amp ? HapticBuckets.Amplitude(b) : -1); // -1 = DEFAULT_AMPLITUDE
                        _args[b] = new object[] { _effects[b] };
                    }
                    else _args[b] = new object[] { ms }; // boxed once
                }
                _ok = true;
            }
            catch { _ok = false; }
        }

        public void Pulse(int bucket, float nowMs)
        {
            if (!_ok || bucket <= 0 || bucket > HapticBuckets.Count || !_gate.Allow(bucket, nowMs)) return;
            if (!_amp && bucket < 3) return; // no amplitude control: only strong events vibrate (haptics.md ★ fallback)
            try { _vibrator.Call("vibrate", _args[bucket]); } catch { _ok = false; }
        }
#else
        public void Pulse(int bucket, float nowMs) { _gate.Allow(bucket, nowMs); }
#endif
    }
}
