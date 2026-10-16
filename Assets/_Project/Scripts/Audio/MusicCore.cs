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
        static MusicTrack L(string id, int total, int ls, int le, double bpm) => new MusicTrack { id = id, totalSamples = total, loopStart = ls, loopEnd = le, bpm = bpm };
        static MusicTrack S(string id, int total) => new MusicTrack { id = id, totalSamples = total, loopStart = 0, loopEnd = total, oneShot = true };
        public static readonly Dictionary<string, MusicTrack> All = new Dictionary<string, MusicTrack>();
        static MusicTracks()
        {
            foreach (var t in new[] {
                L("main_theme", 3402000, 378000, 3402000, 112), L("sunnyville_map_loop", 3256615, 0, 3256615, 104), L("gameplay_loop", 2822400, 0, 2822400, 120),
                L("season1_wrecking_frontier_hub", 2919724, 0, 2919724, 116),
                L("tidewarden_p1", 2976750, 330750, 2976750, 128), L("tidewarden_p2", 2646000, 0, 2646000, 128),
                L("gameplay_harbor_isle", 3271418, 192436, 3271418, 110), L("gameplay_mill_isle", 3102207, 182483, 3102207, 0),
                L("gameplay_clocktower_isle", 2998800, 176400, 2998800, 0), L("gameplay_storm_isle", 2205000, 441000, 2205000, 0),
                L("results_loop", 846720, 0, 846720, 100),
                L("titan_foundry_crater", 2646000, 0, 2646000, 128), L("titan_cinder_bridge", 2646000, 0, 2646000, 128),
                L("titan_ash_citadel", 2646000, 0, 2646000, 128), L("titan_obsidian_forge", 2646000, 0, 2646000, 128),
                L("titan_int25", 2646000, 0, 2646000, 128), L("titan_int50", 2646000, 0, 2646000, 128), L("titan_int75", 2646000, 0, 2646000, 128),
                S("stinger_win", 166778), S("stinger_fail", 132300), S("stinger_win_3star", 186824), S("stinger_boss_defeat", 274522),
                S("stinger_boss_phase2", 109148), S("stinger_chapter_clear", 224910) })
                All[t.id] = t;
            // Excluded from the base APK (DLC / not shipped): season2_stormfront_hub, titan_preview_obsidian_full.
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
