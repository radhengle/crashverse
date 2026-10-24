using System.Collections.Generic;
using Crashverse.Feel;
using UnityEngine;
using UnityEngine.Audio;

namespace Crashverse.Audio
{
    /// <summary>Resolves mixer group paths (mixing_unity.md / audio/mixer_spec.md tree) on Resources/Audio/CrashverseMixer.
    /// If the .mixer asset is not in the build yet (Sound Manager, PR #32 follow-up), groups are null and audio goes to the master.</summary>
    public static class MixerRouting
    {
        public const string MixerResource = "Audio/CrashverseMixer";
        static AudioMixer _mixer; static bool _tried;
        static readonly Dictionary<string, AudioMixerGroup> _cache = new Dictionary<string, AudioMixerGroup>();
        public static AudioMixer Mixer { get { if (!_tried) { _tried = true; _mixer = Resources.Load<AudioMixer>(MixerResource); } return _mixer; } }

        /// <summary>Most specific match first ("SFX/Destruction/Impacts" → "SFX/Destruction" → "SFX"), null when no mixer.</summary>
        public static AudioMixerGroup Group(string path)
        {
            if (string.IsNullOrEmpty(path) || Mixer == null) return null;
            if (_cache.TryGetValue(path, out var g)) return g;
            for (var p = path; !string.IsNullOrEmpty(p); p = p.Contains("/") ? p.Substring(0, p.LastIndexOf('/')) : null)
            {
                var m = Mixer.FindMatchingGroups(p);
                if (m != null && m.Length > 0) { g = m[0]; break; }
            }
            return _cache[path] = g;
        }
    }

    /// <summary>Ensures exactly one AudioListener (P0: Bootstrap.unity camera had none). Idempotent. Re-runs on every scene load
    /// (SceneManager.sceneLoaded, event-driven, no per-frame scan) so a scene/camera that brings its own listener collapses back to one,
    /// and a listener lost with an unloaded camera is replaced. Code that adds a camera at runtime can call Ensure() again (cheap no-op if 1).</summary>
    public static class AudioListenerGuard
    {
        static bool _hooked;
        public static AudioListener Ensure()
        {
            if (!_hooked) { _hooked = true; UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => Ensure(); }
            var all = Object.FindObjectsOfType<AudioListener>();
            var main = Camera.main;
            var kept = ListenerRule.Apply<AudioListener>(all, l => main != null && l.gameObject == main.gameObject, main != null,
                () => main.gameObject.AddComponent<AudioListener>(),
                () => { var go = new GameObject("AudioListener"); Object.DontDestroyOnLoad(go); return go.AddComponent<AudioListener>(); },
                l => { l.enabled = false; Object.Destroy(l); }); // disable now: Destroy is deferred to end of frame
            if (all.Length > 1) Debug.LogWarning($"[Audio] {all.Length} AudioListeners found; kept the one on '{kept.gameObject.name}'");
            return kept;
        }
    }

    /// <summary>
    /// SFX router + pooled player (P0 no-sound). Subscribes to AnimEventBus (ANIM_*, UI_*, BOSS_*, PU_*, and PHYS_* once PR #18 lands) and
    /// plays clips from audio/sfx_routing.json (PR #32; copied into Resources/Audio/sfx_routing.json by SfxRoutingSync at build time)
    /// on an AudioSource pool sized by ImpactAudioProfile voices (12/24/32 per device tier). Honors priority (stealing per mixing_unity.md)
    /// and cooldown_ticks; skips visual/silent routes; warns once per route when a clip has no files yet.
    /// Code-owned routes (owner "code", e.g. reload_start, BOSS_TIDE_ARMOR_HIT, UI_COIN_BURST) are triggered via <see cref="PlayCode"/>.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        public const string RoutingResource = "Audio/sfx_routing";
        public static SfxPlayer Instance { get; private set; }
        public int voicesLow = SfxVoicePool.DefaultLow, voicesMid = SfxVoicePool.DefaultMid, voicesHigh = SfxVoicePool.DefaultHigh; // ImpactAudioProfile values

        SfxRouter _router; SfxVoicePool _pool; readonly List<AudioSource> _sources = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        public static SfxPlayer Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("SFX"); DontDestroyOnLoad(go);
            return Instance = go.AddComponent<SfxPlayer>();
        }

        void Awake()
        {
            if (MissingClipLog.Sink == null) MissingClipLog.Sink = m => Debug.LogWarning(m);
            var ta = Resources.Load<TextAsset>(RoutingResource);
            if (ta == null) Debug.LogWarning("[SFX] Resources/" + RoutingResource + ".json not found (needs PR #32 + a build/sync); SFX disabled");
            _router = new SfxRouter(SfxRoutingTable.Parse(ta != null ? ta.text : null, m => Debug.LogWarning(m)), m => Debug.LogWarning(m))
                { TickSeconds = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02 };
            _pool = new SfxVoicePool(1); ResizePool();
            Perf.QualityController.TierChanged += ResizePool;
            AnimEventBus.Raised += OnEvent;
        }
        void OnDestroy() { AnimEventBus.Raised -= OnEvent; Perf.QualityController.TierChanged -= ResizePool; if (Instance == this) Instance = null; }

        void ResizePool()
        {
            int n = SfxVoicePool.SizeFor(Perf.QualityController.FeelTier, voicesLow, voicesMid, voicesHigh);
            foreach (var v in _pool.Voices) if (v.Index >= n && v.Index < _sources.Count) _sources[v.Index].Stop();
            _pool.Resize(n);
            while (_sources.Count < n)
            {
                var s = new GameObject("Voice" + _sources.Count).AddComponent<AudioSource>(); s.transform.SetParent(transform, false);
                s.playOnAwake = false; s.spatialBlend = 0f; _sources.Add(s);
            }
        }

        void Update()
        {
            foreach (var v in _pool.Voices) if (v.Busy && v.Index < _sources.Count && !_sources[v.Index].isPlaying) _pool.Release(v);
        }

        void OnEvent(string name, object payload) => Route(name, payload);

        /// <summary>Gameplay API for code-owned routes (owner "code"): e.g. PlayCode("reload_start"), PlayCode("BOSS_TIDE_ARMOR_HIT"), PlayCode("UI_COIN_BURST").</summary>
        public static bool PlayCode(string routeEvent, IDictionary<string, string> key = null) => Instance != null && Instance.Route(routeEvent, key) > 0;

        int Route(string name, object payload)
        {
            var plays = _router.Resolve(name, payload, Time.unscaledTimeAsDouble, out var route);
            if (route != null && route.IsStop) { StopLoops(); return 0; }
            int n = 0;
            foreach (var p in plays) if (PlayClip(p.Clip, p.Priority)) n++;
            return n;
        }

        AudioClip Load(string path)
        {
            if (_clips.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>(path);
            if (c == null) MissingClipLog.Report("SFX", path, path);
            return _clips[path] = c;
        }

        bool PlayClip(SfxClipRef c, int priority)
        {
            var clip = Load(c.Variations[Random.Range(0, c.Variations.Count)]); if (clip == null) return false;
            string group = string.IsNullOrEmpty(c.Group) ? (c.SfxId.StartsWith("sfx_ui_") ? "UI" : "SFX") : c.Group;
            bool loop = c.SfxId.EndsWith("_loop") || c.SfxId == "sfx_boss_tw_core_hum";
            var v = _pool.Acquire(c.SfxId, priority, Time.unscaledTimeAsDouble, 1f, group == "UI"); if (v == null) return false;
            v.Loop = loop;
            var s = _sources[v.Index];
            s.Stop(); s.clip = clip; s.loop = loop; s.volume = 1f; s.priority = priority;
            s.outputAudioMixerGroup = MixerRouting.Group(group); s.Play();
            return true;
        }

        void StopLoops()
        {
            foreach (var v in _pool.Voices) if (v.Busy && v.Loop && v.Index < _sources.Count) { _sources[v.Index].Stop(); _pool.Release(v); }
        }
    }
}
