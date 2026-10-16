using UnityEngine;
using Crashverse.Feel;

namespace Crashverse.Audio
{
    /// <summary>
    /// Streamed music player (mixing_unity.md "Music stem-voice rule", music/loop_points.md).
    /// Sources: Base A/B (B only for the sample-exact intro→loop / track handover), Layer A/B (1 s equal-power crossfade, sample-synced
    /// to the base via PlayScheduled + timeSamples computed from dspTime), Stinger. Audible streams never exceed 3.
    /// Clips: Resources/Music/&lt;id&gt; imported as Streaming Vorbis (MusicImportRules enforces it in the editor).
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }
        public UnityEngine.Audio.AudioMixerGroup baseGroup, layerGroup, stingerGroup; // Music/Base, Music/Layers, Music/Stingers
        AudioSource _baseA, _baseB, _layerA, _layerB, _stinger, _activeBase, _activeLayer, _oldLayer;
        MusicTrack _baseTrack; double _baseStartDsp, _nextHandoverDsp = -1; bool _p2Pending;
        readonly MusicVoiceState _v = new MusicVoiceState();

        public static MusicPlayer Ensure()
        {
            if (Instance != null) return Instance;
            MusicTracks.JsonSource = () => Resources.Load<TextAsset>("Audio/music_tracks")?.text;
            var go = new GameObject("Music"); DontDestroyOnLoad(go);
            return Instance = go.AddComponent<MusicPlayer>();
        }

        void Awake()
        {
            _baseA = Src("BaseA", baseGroup); _baseB = Src("BaseB", baseGroup);
            _layerA = Src("LayerA", layerGroup); _layerB = Src("LayerB", layerGroup); _stinger = Src("Stinger", stingerGroup);
            AnimEventBus.Raised += OnEvent;
        }
        void OnDestroy() { AnimEventBus.Raised -= OnEvent; if (Instance == this) Instance = null; }

        AudioSource Src(string n, UnityEngine.Audio.AudioMixerGroup g)
        {
            var s = new GameObject(n).AddComponent<AudioSource>(); s.transform.SetParent(transform, false);
            s.playOnAwake = false; s.spatialBlend = 0f; s.priority = 0; s.outputAudioMixerGroup = g; return s;
        }

        static AudioClip Load(string id)
        {
            var c = Resources.Load<AudioClip>("Music/" + id);
            if (c != null && c.loadType != AudioClipLoadType.Streaming) Debug.LogWarning($"[Music] {id} must be imported as Streaming (mixing_unity.md)");
            return c;
        }

        // ───────────── Public API ─────────────
        /// <summary>Beat clock of the playing base track (for beat-locked visuals). False when no music is playing.</summary>
        public bool TryBeatClock(out double nowDsp, out double baseStartDsp, out double bpm)
        {
            nowDsp = AudioSettings.dspTime; baseStartDsp = _baseStartDsp; bpm = _baseTrack?.bpm ?? 0;
            return _activeBase != null && _activeBase.isPlaying && bpm > 0;
        }
        /// <summary>Starts a base track on the next safe DSP time (intro plays once, then the loop region).</summary>
        public void PlayBase(string id, double atDsp = -1)
        {
            var t = MusicTracks.Get(id); var clip = Load(id);
            StopLayers(); _p2Pending = false;
            if (t == null || clip == null) { StopBase(); _baseTrack = null; return; }
            double at = atDsp > 0 ? atDsp : AudioSettings.dspTime + 0.1;
            if (_activeBase != null && _activeBase.isPlaying) _activeBase.SetScheduledEndTime(at);
            var src = _activeBase == _baseA ? _baseB : _baseA;
            src.clip = clip; src.timeSamples = 0; src.volume = 1f;
            src.loop = !t.HasIntro; // whole-file loops use native looping on one source
            src.PlayScheduled(at);
            _activeBase = src; _baseTrack = t; _baseStartDsp = at; _v.Base = id;
            _nextHandoverDsp = t.HasIntro ? at + t.loopEnd / (double)MusicTrack.SampleRate : -1; // intro track: first wrap at loopEnd
            if (t.HasIntro) { src.SetScheduledEndTime(_nextHandoverDsp); PrepareHandover(); }
        }

        /// <summary>Titan: arena base + cumulative intensity layer (0 = none, 1..3 = int25/50/75). Never stacks layers.</summary>
        public void PlayTitan(string arenaId) { PlayBase(MusicTracks.TitanArena(arenaId)); }
        public void SetTitanStage(int stage) => SetLayer(MusicTracks.TitanLayer(stage));

        public void SetLayer(string id)
        {
            double now = AudioSettings.dspTime;
            if (_baseTrack == null || !_v.SetLayer(id, now)) return;
            if (_oldLayer != null && _oldLayer.isPlaying && _oldLayer != _activeLayer) _oldLayer.Stop(); // snap a running crossfade to its end
            _oldLayer = _activeLayer;
            if (id == null) { _activeLayer = null; return; }
            var clip = Load(id); if (clip == null) { _activeLayer = null; return; }
            var src = _activeLayer == _layerA ? _layerB : _layerA;
            double at = now + 0.05;
            src.clip = clip; src.loop = true; src.volume = 0f;
            src.timeSamples = (int)(_baseTrack.SampleAt(at - _baseStartDsp) % clip.samples); // sample-locked to the base at the scheduled start
            src.PlayScheduled(at);
            _activeLayer = src; _v.CrossfadeStartDsp = at;
        }

        /// <summary>Stingers wait for an active layer crossfade to finish (≤ 3 voices).</summary>
        public void PlayStinger(string id)
        {
            var t = MusicTracks.Get(id); if (t == null) return;
            if (_v.RequestStinger(id, AudioSettings.dspTime, t.totalSamples / (double)MusicTrack.SampleRate)) StartStinger(id, AudioSettings.dspTime + 0.02);
        }

        /// <summary>Tidewarden: stinger_boss_phase2 at the end of the current P1 bar, P2 on the next bar.</summary>
        public void TidewardenPhase2()
        {
            if (_baseTrack == null || _baseTrack.id != "tidewarden_p1" || _p2Pending) return;
            _p2Pending = true;
            var plan = TidewardenMusic.Plan(_baseStartDsp, AudioSettings.dspTime);
            StopLayers();
            _activeBase.SetScheduledEndTime(plan.p1StopDsp); _nextHandoverDsp = -1;
            var other = _activeBase == _baseA ? _baseB : _baseA; if (other.isPlaying) other.SetScheduledEndTime(plan.p1StopDsp);
            var st = MusicTracks.Get("stinger_boss_phase2");
            _v.RequestStinger(st.id, plan.stingerDsp, st.totalSamples / (double)MusicTrack.SampleRate);
            StartStinger(st.id, plan.stingerDsp);
            var p2 = MusicTracks.Get("tidewarden_p2"); var clip = Load(p2.id);
            if (clip == null) return;
            var src = other; src.clip = clip; src.loop = true; src.timeSamples = 0; src.volume = 1f; src.PlayScheduled(plan.p2StartDsp);
            _activeBase = src; _baseTrack = p2; _baseStartDsp = plan.p2StartDsp; _v.Base = p2.id;
        }

        public void StopAll(float fadeSeconds = 0.3f) { StopBase(); StopLayers(); } // win/fail: mixer snapshot handles the 0.3 s fade

        // ───────────── internals ─────────────
        void StartStinger(string id, double at) { var c = Load(id); if (c == null) return; _stinger.clip = c; _stinger.loop = false; _stinger.PlayScheduled(at); }
        void StopBase() { _baseA.Stop(); _baseB.Stop(); _activeBase = null; _v.Base = null; _nextHandoverDsp = -1; }
        void StopLayers() { _layerA.Stop(); _layerB.Stop(); _activeLayer = _oldLayer = null; _v.SetLayer(null, AudioSettings.dspTime); _v.CrossfadeStartDsp = -1; }

        /// <summary>Intro+loop in one streamed clip: the idle base source is pre-seeked to loopStart and scheduled at the wrap.</summary>
        void PrepareHandover()
        {
            var next = _activeBase == _baseA ? _baseB : _baseA;
            next.clip = _activeBase.clip; next.loop = false; next.volume = 1f;
            next.timeSamples = _baseTrack.loopStart;
            next.PlayScheduled(_nextHandoverDsp);
            next.SetScheduledEndTime(_nextHandoverDsp + _baseTrack.LoopSeconds);
        }

        void Update()
        {
            double now = AudioSettings.dspTime;
            // Loop-region handover for intro tracks (one wrap scheduled ahead).
            if (_nextHandoverDsp > 0 && now >= _nextHandoverDsp - 0.02 && _baseTrack != null && _baseTrack.HasIntro)
            {
                _activeBase = _activeBase == _baseA ? _baseB : _baseA;
                _nextHandoverDsp += _baseTrack.LoopSeconds;
                PrepareHandover();
            }
            // Equal-power layer crossfade.
            float t = _v.CrossfadeT(now);
            if (_activeLayer != null) _activeLayer.volume = EqualPower.In(t);
            if (_oldLayer != null) { _oldLayer.volume = EqualPower.Out(t); if (t >= 1f) { _oldLayer.Stop(); _oldLayer = null; } }
            var st = _v.Tick(now, out _);
            if (st != null) StartStinger(st, now + 0.02);
        }

        void OnEvent(string e, object payload)
        {
            switch (e)
            {
                case AnimEvents.BossTitanStage: if (payload is int s) SetTitanStage(s); break;
                case AnimEvents.BossTidePhase2: TidewardenPhase2(); break;
                case AnimEvents.BossTitanDefeat: case AnimEvents.BossTideDefeat: PlayStinger("stinger_boss_defeat"); break;
            }
        }
    }
}
