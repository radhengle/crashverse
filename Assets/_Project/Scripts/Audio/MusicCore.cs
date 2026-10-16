using System;
using System.Collections.Generic;

namespace Crashverse.Audio
{
    /// <summary>Track metadata from audio/music/loop_points.md (44.1 kHz sample offsets) + BPM from audio/music/README.md.</summary>
    public class MusicTrack
    {
        public string id; public int totalSamples, loopStart, loopEnd; public double bpm; public int beatsPerBar = 4; public bool oneShot;
        public const int SampleRate = 44100;
        public bool HasIntro => !oneShot && loopStart > 0;
        public double BarSeconds => bpm > 0 ? 60.0 / bpm * beatsPerBar : 0;
        public double IntroSeconds => loopStart / (double)SampleRate;
        public double LoopSeconds => (loopEnd - loopStart) / (double)SampleRate;

        /// <summary>Sample position after playing for t seconds from sample 0 (intro once, then the loop region).</summary>
        public long SampleAt(double t)
        {
            long s = (long)Math.Round(t * SampleRate);
            if (oneShot || s < loopEnd) return Math.Max(0, Math.Min(s, totalSamples));
            long len = loopEnd - loopStart; return loopStart + (s - loopStart) % len;
        }
    }

    public static class MusicTracks
    {
        /// <summary>Raw JSON of Resources/Audio/music_tracks.json (generated from loop_points.md by Tools/audio/gen_music_tracks.py).
        /// MusicPlayer sets this to the TextAsset; tests read the file. No sample offsets are hard-coded here.</summary>
        public static Func<string> JsonSource;
        static Dictionary<string, MusicTrack> _all; static List<string> _excluded = new List<string>();
        public static Dictionary<string, MusicTrack> All { get { if (_all == null) Load(JsonSource?.Invoke()); return _all; } }
        public static IReadOnlyList<string> Excluded { get { var _ = All; return _excluded; } }
        class Doc { public List<MusicTrack> tracks; public List<string> excluded; }
        public static void Load(string json)
        {
            _all = new Dictionary<string, MusicTrack>();
            if (string.IsNullOrEmpty(json)) return; // silent: no data → no music, never wrong loop points
            var d = Newtonsoft.Json.JsonConvert.DeserializeObject<Doc>(json);
            _excluded = d?.excluded ?? new List<string>();
            foreach (var t in d?.tracks ?? new List<MusicTrack>()) if (!_excluded.Contains(t.id)) _all[t.id] = t;
        }
        public static MusicTrack Get(string id) => All.TryGetValue(id, out var t) ? t : null;
        public static string TitanArena(string arenaId) => "titan_" + arenaId;
        public static string TitanLayer(int stage) => stage <= 0 ? null : "titan_int" + (stage >= 3 ? 75 : stage == 2 ? 50 : 25);
    }

    /// <summary>Equal-power crossfade gains (sum of squares = 1).</summary>
    public static class EqualPower
    {
        public static float Out(float t) => (float)Math.Cos(Clamp01(t) * Math.PI * 0.5);
        public static float In(float t) => (float)Math.Sin(Clamp01(t) * Math.PI * 0.5);
        static double Clamp01(float t) => t < 0 ? 0 : t > 1 ? 1 : t;
    }

    public static class BarClock
    {
        /// <summary>DSP time of the next beat (60/bpm) of a track started at startDsp, at or after now.</summary>
        public static double NextBeat(double startDsp, double nowDsp, double bpm)
        {
            if (bpm <= 0) return nowDsp;
            double beat = 60.0 / bpm, n = Math.Ceiling((nowDsp - startDsp) / beat - 1e-9);
            return startDsp + Math.Max(0, n) * beat;
        }
        /// <summary>DSP time of the next bar line of a track that started at startDsp (min lead time so scheduling is safe).</summary>
        public static double NextBar(double startDsp, double nowDsp, double barSeconds, double minLead = 0.05)
        {
            if (barSeconds <= 0) return nowDsp + minLead;
            double n = Math.Ceiling((nowDsp + minLead - startDsp) / barSeconds);
            return startDsp + Math.Max(0, n) * barSeconds;
        }
    }

    /// <summary>
    /// Music voice rule (mixing_unity.md, 2026-10-16): ≤ 3 streams — Base, one Layer, Stinger. A layer change crossfades two Layer
    /// sources for 1.0 s (Base + 2 Layers = 3), so a stinger requested during a crossfade waits until it ends.
    /// Pure state machine; MusicPlayer applies it to AudioSources.
    /// </summary>
    public class MusicVoiceState
    {
        public const double CrossfadeSeconds = 1.0;
        public string Base, Layer, FadingOutLayer; public double CrossfadeStartDsp = -1; public string Stinger; public double StingerEndDsp;
        readonly Queue<string> _pendingStingers = new Queue<string>();
        public bool Crossfading(double now) => CrossfadeStartDsp >= 0 && now < CrossfadeStartDsp + CrossfadeSeconds;
        public float CrossfadeT(double now) => CrossfadeStartDsp < 0 ? 1f : (float)Math.Min(1.0, (now - CrossfadeStartDsp) / CrossfadeSeconds);
        public int Voices(double now) => (Base != null ? 1 : 0) + (Layer != null ? 1 : 0) + (Crossfading(now) && FadingOutLayer != null ? 1 : 0) + (Stinger != null && now < StingerEndDsp ? 1 : 0);

        /// <summary>Swap the cumulative layer (null = none). Returns false if unchanged. Snaps any running crossfade to its end first.</summary>
        public bool SetLayer(string layer, double now)
        {
            if (layer == Layer) return false;
            FadingOutLayer = Layer; Layer = layer; CrossfadeStartDsp = now;
            if (Stinger != null && now < StingerEndDsp) { /* never 4 voices: the stinger keeps playing; the old layer is cut instead */ FadingOutLayer = null; }
            return true;
        }
        /// <summary>Stinger requested: plays now unless a crossfade is running (then queued until it ends).</summary>
        public bool RequestStinger(string id, double now, double lengthSeconds)
        {
            if (Crossfading(now)) { _pendingStingers.Enqueue(id + "|" + lengthSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)); return false; }
            Stinger = id; StingerEndDsp = now + lengthSeconds; return true;
        }
        /// <summary>Call every frame; returns a stinger that may now start (after the crossfade), or null.</summary>
        public string Tick(double now, out double lengthSeconds)
        {
            lengthSeconds = 0;
            if (CrossfadeStartDsp >= 0 && !Crossfading(now)) { FadingOutLayer = null; CrossfadeStartDsp = -1; }
            if (Stinger != null && now >= StingerEndDsp) Stinger = null;
            if (!Crossfading(now) && _pendingStingers.Count > 0)
            {
                var parts = _pendingStingers.Dequeue().Split('|');
                lengthSeconds = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                Stinger = parts[0]; StingerEndDsp = now + lengthSeconds; return Stinger;
            }
            return null;
        }
        public int PendingStingers => _pendingStingers.Count;
    }

    /// <summary>Tidewarden P1→P2 plan: stinger at the end of the current P1 bar, P2 starts on the following bar.</summary>
    public struct PhaseTransitionPlan { public double stingerDsp, p2StartDsp, p1StopDsp; }
    public static class TidewardenMusic
    {
        public static PhaseTransitionPlan Plan(double p1StartDsp, double nowDsp)
        {
            var p1 = MusicTracks.Get("tidewarden_p1");
            double bar = p1.BarSeconds;
            double end = BarClock.NextBar(p1StartDsp, nowDsp, bar);
            return new PhaseTransitionPlan { p1StopDsp = end, stingerDsp = end, p2StartDsp = end + bar };
        }
    }
}
