using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Crashverse.Audio
{
    // Engine-free SFX routing core (P0 no-sound fix). Unity glue lives in SfxPlayer.cs. Tested by Tools/Tests.
    // Data: audio/sfx_routing.json (Sound Manager, PR #32 follow-up), shipped as Resources/Audio/sfx_routing.json.
    // Mixer paths: audio/mixer_spec.md / mixing_unity.md group tree (Master, Music/*, SFX/*, UI, Ambience).

    /// <summary>One clip of a route: sfx id, Resources variation paths (no extension), mixer group, Unity priority (0 = most important).</summary>
    public sealed class SfxClipRef
    {
        public string SfxId; public List<string> Variations = new List<string>(); public string Group = ""; public int Priority = 128;
        public bool Missing => Variations.Count == 0;
    }

    /// <summary>One row of audio/sfx_routing.json (PR #32). Owner: anim | phys | code (gameplay calls SfxPlayer.PlayCode) | visual/silent (skipped).</summary>
    public sealed class SfxRoute
    {
        public string Event, Owner = "anim", Notes = "";
        public Dictionary<string, string> Key = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public int CooldownTicks;
        public List<SfxClipRef> Clips = new List<SfxClipRef>();
        public bool Silent => Owner == "visual" || Owner == "silent" || Owner == "-";
        public bool IsStop => Clips.Count == 0 && Notes.IndexOf("stop", StringComparison.OrdinalIgnoreCase) >= 0;
        public string CooldownKey => Event + "|" + string.Join(",", Key.Values);
    }

    /// <summary>Tolerant loader for audio/sfx_routing.json: unknown keys ignored, bad rows skipped with a warning, never throws.</summary>
    public sealed class SfxRoutingTable
    {
        readonly Dictionary<string, List<SfxRoute>> _byEvent = new Dictionary<string, List<SfxRoute>>(StringComparer.Ordinal);
        public readonly List<SfxRoute> All = new List<SfxRoute>();
        public readonly Dictionary<string, int> SourcePriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Hardness = new List<string> { "steel", "stone", "brick", "glass", "wood", "terrain" };
        public int Count => All.Count;

        public void Add(SfxRoute r)
        {
            if (r == null || string.IsNullOrEmpty(r.Event)) return;
            All.Add(r);
            if (!_byEvent.TryGetValue(r.Event, out var l)) _byEvent[r.Event] = l = new List<SfxRoute>();
            l.Add(r);
        }
        public IReadOnlyList<SfxRoute> For(string ev) => ev != null && _byEvent.TryGetValue(ev, out var l) ? l : (IReadOnlyList<SfxRoute>)Array.Empty<SfxRoute>();

        /// <summary>Best route for an event + payload fields. Key values "any"/missing are wildcards; "count" accepts "a-b" / "n+" ranges.
        /// Exact matches score higher than wildcards; ties keep file order.</summary>
        public SfxRoute Match(string ev, IDictionary<string, string> fields)
        {
            SfxRoute best = null; int bestScore = -1;
            foreach (var r in For(ev))
            {
                int score = 0; bool ok = true;
                foreach (var kv in r.Key)
                {
                    if (string.IsNullOrEmpty(kv.Value) || kv.Value.Equals("any", StringComparison.OrdinalIgnoreCase)) continue;
                    if (fields == null || !fields.TryGetValue(kv.Key, out var v) || string.IsNullOrEmpty(v)) continue; // payload doesn't carry it: wildcard
                    if (kv.Key.Equals("count", StringComparison.OrdinalIgnoreCase) ? InRange(kv.Value, v) : kv.Value.Equals(v, StringComparison.OrdinalIgnoreCase)) score++;
                    else { ok = false; break; }
                }
                if (ok && score > bestScore) { best = r; bestScore = score; }
            }
            return best;
        }

        public static bool InRange(string range, string value)
        {
            if (!int.TryParse(value, out var n)) return false;
            range = range.Trim();
            if (range.EndsWith("+")) return int.TryParse(range.TrimEnd('+'), out var lo) && n >= lo;
            var p = range.Split('-');
            if (p.Length == 2 && int.TryParse(p[0], out var a) && int.TryParse(p[1], out var b)) return n >= a && n <= b;
            return int.TryParse(range, out var e) && n == e;
        }

        /// <summary>Harder material wins a mixed pair (phys_hardness, steel first).</summary>
        public string Harder(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b; if (string.IsNullOrEmpty(b)) return a;
            int ia = Hardness.IndexOf(a.ToLowerInvariant()), ib = Hardness.IndexOf(b.ToLowerInvariant());
            if (ia < 0) return b; if (ib < 0) return a; return ia <= ib ? a : b;
        }

        public static SfxRoutingTable Parse(string json, Action<string> warn = null)
        {
            var t = new SfxRoutingTable();
            if (string.IsNullOrWhiteSpace(json)) { warn?.Invoke("[SFX] sfx_routing.json is missing or empty: no SFX routes loaded"); return t; }
            JToken root;
            try { root = JToken.Parse(json); }
            catch (Exception e) { warn?.Invoke("[SFX] sfx_routing.json is not valid JSON: " + e.Message); return t; }
            JToken routes = root;
            if (root is JObject o)
            {
                routes = o["routes"];
                if (o["phys_source_priority"] is JObject sp) foreach (var p in sp.Properties()) if (int.TryParse(p.Value.ToString(), out var v)) t.SourcePriority[p.Name] = v;
                if (o["phys_hardness"] is JArray h && h.Count > 0) { t.Hardness.Clear(); foreach (var x in h) t.Hardness.Add(x.ToString().ToLowerInvariant()); }
            }
            if (!(routes is JArray arr)) { warn?.Invoke("[SFX] sfx_routing.json has no 'routes' array"); return t; }
            int i = 0;
            foreach (var it in arr)
            {
                i++;
                try
                {
                    if (!(it is JObject ro)) continue;
                    var r = new SfxRoute { Event = (string)ro["event"], Owner = ((string)ro["owner"] ?? "anim").Trim().ToLowerInvariant(), Notes = (string)ro["notes"] ?? "" };
                    if (string.IsNullOrEmpty(r.Event)) { warn?.Invoke($"[SFX] route #{i} has no event, skipped"); continue; }
                    if (ro["key"] is JObject k) foreach (var p in k.Properties()) r.Key[p.Name] = p.Value.ToString();
                    int.TryParse(ro["cooldown_ticks"]?.ToString(), out r.CooldownTicks); r.CooldownTicks = Math.Max(0, r.CooldownTicks);
                    if (ro["clips"] is JArray ca)
                        foreach (var c in ca.OfType<JObject>())
                        {
                            var cr = new SfxClipRef { SfxId = (string)c["sfx_id"] ?? "", Group = (string)c["group"] ?? "" };
                            if (int.TryParse(c["priority"]?.ToString(), out var pr)) cr.Priority = Math.Max(0, Math.Min(256, pr));
                            if (c["variations"] is JArray va) foreach (var v in va) { var s = v.ToString(); if (!string.IsNullOrWhiteSpace(s)) cr.Variations.Add(s); }
                            r.Clips.Add(cr);
                        }
                    t.Add(r);
                }
                catch (Exception e) { warn?.Invoke($"[SFX] route #{i} unreadable ({e.Message}), skipped"); }
            }
            return t;
        }
    }

    /// <summary>Reads PHYS_* payload fields (ImpactEvent, PR #18) by reflection, so Audio has no compile-time dependency on Crashverse.Sim.</summary>
    public static class PhysPayload
    {
        public const string Prefix = "PHYS_";
        static object Get(object o, Type t, params string[] names)
        {
            foreach (var n in names)
            {
                var f = t.GetField(n); if (f != null) return f.GetValue(o);
                var p = t.GetProperty(n); if (p != null) return p.GetValue(o);
            }
            return null;
        }

        /// <summary>Fields for <see cref="SfxRoutingTable.Match"/>: material (harder of A/B wins), kind, size, tier, count; plus source.</summary>
        public static Dictionary<string, string> Fields(string ev, object payload, SfxRoutingTable table)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["kind"] = KindOf(ev);
            if (payload == null) return d;
            if (payload is IDictionary<string, string> given) { foreach (var kv in given) d[kv.Key] = kv.Value; return d; }
            var t = payload.GetType();
            string S(params string[] n) => Get(payload, t, n)?.ToString();
            var mat = S("material", "Material", "dominantMaterial", "DominantMaterial");
            var ma = S("materialA", "MaterialA"); var mb = S("materialB", "MaterialB");
            if (ma != null || mb != null) mat = table != null ? table.Harder(ma, mb) : (ma ?? mb);
            if (mat != null) d["material"] = mat.ToLowerInvariant();
            var size = S("size", "Size", "sizeClass", "SizeClass"); if (size != null) d["size"] = size.ToUpperInvariant();
            var tier = S("tier", "Tier"); if (tier != null) d["tier"] = tier.ToLowerInvariant();
            var cnt = S("count", "cascadeCount", "CascadeCount"); if (cnt != null) d["count"] = cnt;
            var src = S("source", "Source"); if (src != null) d["source"] = src;
            return d;
        }

        public static string KindOf(string ev) => ev switch
        {
            "PHYS_IMPACT" => "impact", "PHYS_CRACK" => "crack", "PHYS_BREAK" => "break", "PHYS_JOINT_SNAP" => "jointsnap",
            "PHYS_RATTLE" => "rattle", "PHYS_COLLAPSE_END" => "collapse", _ => null
        };
    }

    /// <summary>Routing decision (engine-free, testable): which clips to play, at what priority, honoring silent/missing/cooldown.</summary>
    public sealed class SfxRouter
    {
        public readonly SfxRoutingTable Table; readonly SfxCooldowns _cd = new SfxCooldowns();
        readonly HashSet<string> _warned = new HashSet<string>();
        public Action<string> Warn;
        public double TickSeconds = 0.02; // cooldown_ticks are sim ticks (Time.fixedDeltaTime)
        public SfxRouter(SfxRoutingTable t, Action<string> warn = null) { Table = t; Warn = warn; }

        public struct Play { public SfxRoute Route; public SfxClipRef Clip; public int Priority; }

        /// <summary>Returns the plays for this event (empty if unrouted, silent, on cooldown, or all clips missing). Stop routes return Route with no clips.</summary>
        public List<Play> Resolve(string ev, object payload, double now, out SfxRoute route)
        {
            var result = new List<Play>(); route = null;
            if (string.IsNullOrEmpty(ev)) return result;
            var fields = ev.StartsWith(PhysPayload.Prefix, StringComparison.Ordinal) ? PhysPayload.Fields(ev, payload, Table) : payload as IDictionary<string, string>;
            route = Table.Match(ev, fields);
            if (route == null || route.Silent) { route = route != null && route.Silent ? null : route; return result; }
            if (route.Clips.Count == 0) return result;
            if (!_cd.TryPass(route.CooldownKey, (int)Math.Round(route.CooldownTicks * TickSeconds * 1000.0), now)) return result;
            int srcPr = -1;
            if (fields != null && fields.TryGetValue("source", out var src) && Table.SourcePriority.TryGetValue(src, out var sp)) srcPr = sp;
            foreach (var c in route.Clips)
            {
                if (c.Missing) { if (_warned.Add(route.CooldownKey + ":" + c.SfxId)) Warn?.Invoke($"[SFX] missing clip '{c.SfxId}' for route {route.Event} (no variation files yet), skipped"); continue; }
                result.Add(new Play { Route = route, Clip = c, Priority = srcPr >= 0 ? srcPr : c.Priority });
            }
            return result;
        }
    }

    /// <summary>AudioSource pool sizing (ImpactAudioProfile voicesLow/Mid/High = 12/24/32) and priority stealing per mixing_unity.md.</summary>
    public sealed class SfxVoicePool
    {
        public const int DefaultLow = 12, DefaultMid = 24, DefaultHigh = 32;
        public static int SizeFor(Crashverse.Feel.QualityTier tier, int low = DefaultLow, int mid = DefaultMid, int high = DefaultHigh)
            => tier == Crashverse.Feel.QualityTier.Low ? low : tier == Crashverse.Feel.QualityTier.High ? high : mid;

        public sealed class Voice { public int Index; public bool Busy; public int Priority; public double StartedAt; public float Volume; public string Id; public bool Protected; public bool Loop; }
        readonly List<Voice> _voices = new List<Voice>();
        public int Capacity => _voices.Count;
        public int BusyCount { get { int n = 0; foreach (var v in _voices) if (v.Busy) n++; return n; } }
        public IReadOnlyList<Voice> Voices => _voices;

        public SfxVoicePool(int size) { Resize(size); }
        public void Resize(int size) { size = Math.Max(1, size); while (_voices.Count < size) _voices.Add(new Voice { Index = _voices.Count }); if (_voices.Count > size) _voices.RemoveRange(size, _voices.Count - size); }

        /// <summary>Free voice first; otherwise steal the least important (largest priority number), tie-break oldest then quietest.
        /// Never steals protected (UI/Stingers) voices; drops the request (returns null) if it is less important than everything playing.</summary>
        public Voice Acquire(string id, int priority, double now, float volume = 1f, bool isProtected = false)
        {
            Voice pick = null;
            foreach (var v in _voices) if (!v.Busy) { pick = v; break; }
            if (pick == null)
                foreach (var v in _voices)
                {
                    if (v.Protected || v.Priority < priority) continue;
                    if (pick == null || v.Priority > pick.Priority || (v.Priority == pick.Priority && (v.StartedAt < pick.StartedAt || (v.StartedAt == pick.StartedAt && v.Volume < pick.Volume)))) pick = v;
                }
            if (pick == null) return null;
            pick.Busy = true; pick.Priority = priority; pick.StartedAt = now; pick.Volume = volume; pick.Id = id; pick.Protected = isProtected; pick.Loop = false;
            return pick;
        }
        public void Release(Voice v) { if (v != null) { v.Busy = false; v.Id = null; v.Protected = false; v.Loop = false; } }
    }

    /// <summary>Per-key cooldown gate (ms on an injected clock).</summary>
    public sealed class SfxCooldowns
    {
        readonly Dictionary<string, double> _last = new Dictionary<string, double>();
        public bool TryPass(string key, int cooldownMs, double nowSeconds)
        {
            if (cooldownMs <= 0) return true;
            if (_last.TryGetValue(key, out var t) && (nowSeconds - t) * 1000.0 < cooldownMs) return false;
            _last[key] = nowSeconds; return true;
        }
    }

    /// <summary>Warn once per missing clip id (no silent failures; P0 diagnosis fix list).</summary>
    public static class MissingClipLog
    {
        static readonly HashSet<string> _seen = new HashSet<string>();
        public static Action<string> Sink;
        public static int Count => _seen.Count;
        public static bool Report(string category, string id, string path)
        {
            if (!_seen.Add(category + ":" + id)) return false;
            Sink?.Invoke($"[{category}] missing clip '{id}' (Resources/{path}). Is PR #32 (audio library) in this build?");
            return true;
        }
        public static void ResetForTests() { _seen.Clear(); Sink = null; }
    }

    /// <summary>Code-owned boss SFX triggers (sfx_routing.json owner "code").</summary>
    public static class BossSfxRules
    {
        /// <summary>BOSS_TIDE_ARMOR_HIT: a direct hit on a Tidewarden armour plate during phase 1 (not the phase-2 auto-break, not debris, not after defeat).</summary>
        public static bool ArmorHit(int phase, bool directHit, bool defeated) => phase == 1 && directHit && !defeated;
    }

    /// <summary>Exactly-one AudioListener rule (P0 diagnosis item 1).</summary>
    public static class ListenerRule
    {
        public enum Action { None, AddToMainCamera, AddToNewObject, RemoveExtras }
        public static Action Decide(int listenerCount, bool hasMainCamera)
            => listenerCount == 0 ? (hasMainCamera ? Action.AddToMainCamera : Action.AddToNewObject) : listenerCount > 1 ? Action.RemoveExtras : Action.None;

        /// <summary>Applies the rule to the current listeners and returns the one kept. Idempotent: with exactly one listener it changes nothing.
        /// Extras collapse to the main camera's listener if it has one, else the first; <paramref name="remove"/> is called for each extra.</summary>
        public static T Apply<T>(IList<T> listeners, Func<T, bool> onMainCamera, bool hasMainCamera, Func<T> addToMainCamera, Func<T> addToNewObject, Action<T> remove) where T : class
        {
            switch (Decide(listeners.Count, hasMainCamera))
            {
                case Action.AddToMainCamera: return addToMainCamera();
                case Action.AddToNewObject: return addToNewObject();
                case Action.RemoveExtras:
                {
                    T keep = null;
                    foreach (var l in listeners) if (onMainCamera(l)) { keep = l; break; }
                    keep ??= listeners[0];
                    foreach (var l in new List<T>(listeners)) if (!ReferenceEquals(l, keep)) remove(l);
                    return keep;
                }
                default: return listeners[0];
            }
        }
    }
}
