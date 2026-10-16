using UnityEngine;
using Crashverse.Core;
using Crashverse.Weapons;

namespace Crashverse.Feel
{
    /// <summary>PlayerPrefs-backed motion prefs (persisted, Roadmap §6).</summary>
    public class PlayerPrefsMotionPrefs : IMotionPrefs
    {
        public bool TryGet(string key, out bool v) { v = PlayerPrefs.GetInt(key, 0) == 1; return PlayerPrefs.HasKey(key); }
        public void Set(string key, bool v) { PlayerPrefs.SetInt(key, v ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>
    /// Unity glue for the pure feel logic (FeelCore). Drives the Wrecker/camera/Titan state machines from the run, applies the
    /// camera shake (ShakeStack, ×0 under Reduce shake/motion), hit stop and slow-mo (TimeScaleGovernor), and moves the barrel through
    /// the recoil. One component per run, no per-frame allocation. Visual only: it never changes gameplay results.
    /// </summary>
    public class FeelDirector : MonoBehaviour
    {
        public static void InitSettings()
        {
            bool os = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var cr = act.Call<AndroidJavaObject>("getContentResolver"))
                using (var g = new AndroidJavaClass("android.provider.Settings$Global"))
                    os = g.CallStatic<float>("getFloat", cr, "animator_duration_scale", 1f) == 0f;
            }
            catch { }
#endif
            MotionSettings.Current = new MotionSettings(new PlayerPrefsMotionPrefs(), os);
        }

        GameSession _s; OrbitCamera _cam; AimController _aim; Transform _barrel; Vector3 _barrelRest;
        readonly WreckerAnimStateMachine _wrecker = new WreckerAnimStateMachine();
        readonly CameraStateMachine _camSm = new CameraStateMachine();
        readonly ShakeStack _shake = new ShakeStack();
        readonly TimeScaleGovernor _time = new TimeScaleGovernor();
        TitanAnimStateMachine _titan;
        float _seed; int _lastTitanDmg;

        public void Bind(GameSession s, OrbitCamera cam, AimController aim)
        {
            _s = s; _cam = cam; _aim = aim;
            _barrel = aim != null ? aim.pitchPivot : null; if (_barrel != null) _barrelRest = _barrel.localPosition;
            var m = MotionSettings.Current;
            _wrecker.Settings = m; _camSm.Settings = m; _time.Settings = m;
            _titan = s.Ctx != null && s.Ctx.titan != null ? new TitanAnimStateMachine(s.Ctx.titan.maxDamageScore) : null;
            _seed = Random.value * 100f;
        }

        void OnEnable() { AnimEventBus.Raised += OnEvent; Projectile.FirstImpact += OnImpact; }
        void OnDisable() { AnimEventBus.Raised -= OnEvent; Projectile.FirstImpact -= OnImpact; Time.timeScale = 1f; }

        void OnEvent(string e, object _)
        {
            switch (e)
            {
                case AnimEvents.ReloadStart: _wrecker.GateReady = false; _wrecker.Fire(); _camSm.Fire(); _time.OnShotFired(); break;
                case AnimEvents.WreckerFire: _shake.Add(ShakeTable.Fire); break;
                case AnimEvents.WreckerReady: _wrecker.GateReady = true; _camSm.Settled(_s != null && (_s.State == SessionState.Won || _s.State == SessionState.Failed)); break;
            }
        }

        void OnImpact(Projectile p) { _camSm.FirstImpact(); _shake.Add(ShakeTable.ImpactWood); _time.HitStop(50f); }

        void Update()
        {
            if (_s == null) return;
            float rdt = Time.unscaledDeltaTime; var tier = Perf.QualityController.FeelTier; var m = MotionSettings.Current;
            _wrecker.Tier = tier;
            if (_aim != null) _wrecker.SetAiming(_aim.Dragging);
            _wrecker.Tick(rdt); _camSm.Tick(rdt);
            if (_titan != null && _s.TitanDamageScore != _lastTitanDmg) { _lastTitanDmg = _s.TitanDamageScore; _titan.OnDamage(_lastTitanDmg); } // only on change (was raising HIT every frame)

            bool ranked = _s.Ctx != null && _s.Ctx.mode == RunMode.TitanRanked;
            float ts = _time.Tick(rdt);
            if (!ranked) Time.timeScale = ts; // Titan ranked never alters time (OQ-C3)

            // Barrel recoil: 0.15 m back, out-expo in, out-quad return (02); none under Reduce motion.
            if (_barrel != null)
            {
                float k = 0f;
                if (!m.ReduceMotion)
                {
                    if (_wrecker.State == WreckerState.Recoil) k = Ease.OutExpo(_wrecker.ElapsedMs / WreckerAnimStateMachine.RecoilMs);
                    else if (_wrecker.State == WreckerState.Recover) k = 1f - Ease.OutQuad(_wrecker.ElapsedMs / WreckerAnimStateMachine.RecoverMs);
                }
                _barrel.localPosition = _barrelRest + Vector3.back * (0.15f * k);
            }

            if (_cam != null)
            {
                _shake.Scale = m.ShakeScale(tier);
                var (pos, rot) = _shake.Tick(rdt);
                float t = Time.unscaledTime * 25f + _seed;
                _cam.feelOffset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (2f * pos);
                _cam.feelRollDeg = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f * rot;
                bool follow = _camSm.State == CamState.FollowShot && Projectile.Latest != null && m.FollowShotEnabled;
                _cam.followWeight = follow ? CameraConfig.Current.followShotWeight : 0f;
                if (follow) _cam.followTarget = Projectile.Latest.transform.position;
            }
        }
    }
}
