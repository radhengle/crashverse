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
        // 01 cannon parts (WreckerBuilder): Module pod on the barrel top (slot swap + bob), Core orb (ready glow).
        Transform _pod, _core; Vector3 _podRestPos, _podRestScale; Renderer _coreRenderer; MaterialPropertyBlock _coreMpb; Color _coreBase;
        float _idleMs, _readyMs = -1f; bool[] _armedLast = new bool[3]; PowerUps.PowerUpRuntime _puSeen;
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

        /// <summary>Optional: hands the procedural Wrecker's slot parts to the director (cannon idle bob, slot swap, ready glow).</summary>
        public void BindWrecker(WreckerBuilder wb)
        {
            if (wb == null) return;
            _pod = wb.modulePod; _core = wb.coreOrb;
            if (_pod != null) { _podRestPos = _pod.localPosition; _podRestScale = _pod.localScale; }
            if (_core != null)
            {
                _coreRenderer = _core.GetComponent<Renderer>();
                _coreMpb = new MaterialPropertyBlock(); // allocated once; reused every frame
                _coreBase = Crashverse.Destruction.MaterialDefinition.Hex(WreckerSpec.Core);
            }
        }

        void OnEnable() { AnimEventBus.Raised += OnEvent; Projectile.FirstImpact += OnImpact; }
        void OnDisable() { AnimEventBus.Raised -= OnEvent; Projectile.FirstImpact -= OnImpact; Time.timeScale = 1f; }

        void OnEvent(string e, object _)
        {
            switch (e)
            {
                case AnimEvents.ReloadStart: _wrecker.GateReady = false; _wrecker.Fire(); _camSm.Fire(); _time.OnShotFired(); break;
                case AnimEvents.WreckerFire: _shake.Add(ShakeTable.Fire); break;
                case AnimEvents.WreckerReady: _readyMs = 0f; _wrecker.GateReady = true; _camSm.Settled(_s != null && (_s.State == SessionState.Won || _s.State == SessionState.Failed)); break;
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

            bool rm = m.ReduceMotion;
            TickCannon(rdt, rm);

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
        /// <summary>
        /// 01 cannon motion, code-driven until rigs exist. Barrel slide (anticipation 0.03 → recoil 0.25 m → recover, overshoot ≤ 0.02 m),
        /// pitch kick +4° (clamped to 70° by AimController), idle breathing ±0.6° / 2400 ms, Ready dip −1.5° + Core glow 1.0→1.4→1.0
        /// (300 ms), Module pod slot swap 450 ms on arm/disarm. Reduce Motion: recoil kept without overshoot, breathing ×0.5, glow ×0.4.
        /// </summary>
        void TickCannon(float rdt, bool rm)
        {
            var st = _wrecker.State; float ms = _wrecker.ElapsedMs;
            if (_barrel != null) _barrel.localPosition = _barrelRest + Vector3.back * WreckerMotion.SlideBack(st, ms, rm);

            bool idle = st == WreckerState.Idle || st == WreckerState.Fidget;
            _idleMs = idle ? _idleMs + rdt * 1000f : 0f;
            float pitch = WreckerMotion.KickDeg(st, ms, rm) + (idle ? WreckerMotion.IdleBreathDeg(_idleMs, rm) : 0f);
            if (_readyMs >= 0f)
            {
                pitch += WreckerMotion.ReadyDipDegAt(_readyMs, rm);
                _readyMs += rdt * 1000f;
                if (_readyMs >= WreckerMotion.ReadyMs) _readyMs = -1f;
                SetCoreGlow(_readyMs < 0f ? 1f : WreckerMotion.ReadyGlow(_readyMs, MotionSettings.Current.FlashScale)); // ends exactly on the base colour
            }
            if (_aim != null) _aim.visualPitchOffsetDeg = pitch;

            // Slot swap: any arm/disarm change on the power-up slots (PowerUpRuntime.ToggleArm) → 450 ms swap on the Module pod.
            var pu = _s.PowerUps;
            if (pu != _puSeen) { _puSeen = pu; for (int i = 0; i < _armedLast.Length; i++) _armedLast[i] = Armed(pu, i); } // Retry rebuilds the runtime: no swap
            else if (pu != null)
                for (int i = 0; i < _armedLast.Length; i++) { bool a = Armed(pu, i); if (a != _armedLast[i]) { _armedLast[i] = a; _wrecker.SlotSwap(); } }

            if (_pod != null)
            {
                float sc = st == WreckerState.SlotSwap ? WreckerMotion.SlotSwapScale(ms, rm) : 1f;
                _pod.localScale = _podRestScale * sc; // scales about the pod's own centre on the barrel top (silhouette rule 4)
                _pod.localPosition = _podRestPos + Vector3.up * (idle ? WreckerMotion.PodBobMeters(_idleMs, rm) : 0f);
            }
        }

        /// <summary>Core "glow" until a real emissive material exists: lerps the orb toward white (×(g−1)). Keeps WreckerBuilder's other block values.</summary>
        void SetCoreGlow(float g)
        {
            if (_coreRenderer == null) return;
            _coreRenderer.GetPropertyBlock(_coreMpb);
            var c = Color.Lerp(_coreBase, Color.white, g - 1f);
            _coreMpb.SetColor("_BaseColor", c); _coreMpb.SetColor("_Color", c);
            _coreRenderer.SetPropertyBlock(_coreMpb);
        }

        static bool Armed(PowerUps.PowerUpRuntime pu, int i) => pu != null && pu.Slots != null && i < pu.Slots.Length && pu.Slots[i] != null && pu.Slots[i].armed;
    }
}
