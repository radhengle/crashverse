#if CRASHVERSE_DEV
// Security Admin: dev-only aim hook (BUG-24 automation). Whole file behind CRASHVERSE_DEV and in the
// Crashverse.DevHooks asmdef (defineConstraints CRASHVERSE_DEV). Tools/ci/check_release_guards.py fails the
// release guard if any hook symbol appears outside this guard or in a built global-metadata.dat.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Crashverse.DevHooks
{
    public sealed class DevAimCommand
    {
        public int id; public float yaw, pitch; public bool fire;
    }

    /// <summary>Strict parser: a flat JSON object with exactly id (int ≥ 0), yaw, pitch (finite numbers within the
    /// AimController limits) and fire (bool). Unknown/duplicate/missing keys, strings, nesting, out-of-limit values: rejected.</summary>
    public static class DevAimCommandParser
    {
        public const int MaxBytes = 512;
        static readonly string[] Keys = { "id", "yaw", "pitch", "fire" };

        public static bool TryParse(string json, float maxYaw, float minPitch, float maxPitch, out DevAimCommand cmd, out string error)
        {
            cmd = null; error = null;
            if (json == null || json.Length == 0 || json.Length > MaxBytes) { error = "empty_or_too_large"; return false; }
            var vals = new Dictionary<string, string>();
            int i = 0;
            Ws(json, ref i);
            if (i >= json.Length || json[i] != '{') { error = "not_object"; return false; }
            i++; Ws(json, ref i);
            if (i < json.Length && json[i] == '}') { error = "missing_keys"; return false; }
            while (true)
            {
                Ws(json, ref i);
                if (i >= json.Length || json[i] != '"') { error = "bad_key"; return false; }
                int ks = ++i;
                while (i < json.Length && json[i] != '"') { if (json[i] == '\\') { error = "bad_key"; return false; } i++; }
                if (i >= json.Length) { error = "bad_key"; return false; }
                string key = json.Substring(ks, i - ks); i++;
                if (Array.IndexOf(Keys, key) < 0) { error = "unknown_key"; return false; }
                if (vals.ContainsKey(key)) { error = "duplicate_key"; return false; }
                Ws(json, ref i);
                if (i >= json.Length || json[i] != ':') { error = "bad_json"; return false; }
                i++; Ws(json, ref i);
                int vs = i;
                while (i < json.Length && (char.IsLetterOrDigit(json[i]) || json[i] == '-' || json[i] == '+' || json[i] == '.')) i++;
                if (i == vs) { error = "bad_value"; return false; }
                vals[key] = json.Substring(vs, i - vs);
                Ws(json, ref i);
                if (i < json.Length && json[i] == ',') { i++; continue; }
                if (i < json.Length && json[i] == '}') { i++; break; }
                error = "bad_json"; return false;
            }
            Ws(json, ref i);
            if (i != json.Length) { error = "trailing_data"; return false; }
            foreach (var k in Keys) if (!vals.ContainsKey(k)) { error = "missing_" + k; return false; }

            if (!int.TryParse(vals["id"], NumberStyles.None, CultureInfo.InvariantCulture, out int id)) { error = "bad_id"; return false; }
            if (!Num(vals["yaw"], out float yaw) || !Num(vals["pitch"], out float pitch)) { error = "bad_number"; return false; }
            bool fire;
            if (vals["fire"] == "true") fire = true; else if (vals["fire"] == "false") fire = false; else { error = "bad_fire"; return false; }
            if (yaw < -maxYaw || yaw > maxYaw) { error = "yaw_out_of_limits"; return false; }
            if (pitch < minPitch || pitch > maxPitch) { error = "pitch_out_of_limits"; return false; }
            cmd = new DevAimCommand { id = id, yaw = yaw, pitch = pitch, fire = fire };
            return true;
        }

        static bool Num(string s, out float v)
        {
            v = 0f;
            if (!double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double d)) return false;
            if (double.IsNaN(d) || double.IsInfinity(d)) return false;
            v = (float)d; return true;
        }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    }

    /// <summary>What the hook may touch. Wired only by the CRASHVERSE_DEV registration in GameplayDirector.StartRun.</summary>
    public sealed class DevAimTarget
    {
        public float maxYaw, minPitch, maxPitch;
        public Func<bool> isRanked;      // TitanRanked: hook never runs (Security Admin #3)
        public Func<bool> readyToFire;   // Aiming + reload gate ready + shots left
        public Action<float, float> setAim;
        public Func<bool> fire;
        public Action markNonRanked;     // session that used the hook can never be ranked
    }

    /// <summary>Pure logic (unit-tested in Tools/Tests): opt-in, ranked suppression, once-per-id, apply when ready.</summary>
    public sealed class DevAimHookCore
    {
        public const string FileName = "dev_aim_cmd.json";
        public const string OptInExtra = "cv_dev_aim";
        public const string LogTag = "CRASHVERSE_DEV_AIM";

        readonly bool _optIn;
        readonly HashSet<int> _seen = new HashSet<int>();
        DevAimCommand _pending;
        public DevAimHookCore(bool optIn) { _optIn = optIn; }
        public bool OptIn => _optIn;
        public bool HasPending => _pending != null;

        /// <summary>Feed the raw file text (already read and deleted by the caller). Returns a log line or null.</summary>
        public string Offer(string json, DevAimTarget t)
        {
            if (!_optIn || json == null) return null;
            if (t.isRanked()) return LogTag + " rejected reason=ranked";
            if (!DevAimCommandParser.TryParse(json, t.maxYaw, t.minPitch, t.maxPitch, out var c, out var err)) return LogTag + " rejected reason=" + err;
            if (_seen.Contains(c.id)) return LogTag + " ignored id=" + c.id + " reason=duplicate_id";
            _seen.Add(c.id);
            _pending = c;
            return null;
        }

        /// <summary>Call every frame. Applies the pending command once the session can take it.</summary>
        public string Tick(DevAimTarget t)
        {
            if (!_optIn || _pending == null) return null;
            if (t.isRanked()) { _pending = null; return LogTag + " rejected reason=ranked"; }
            if (_pending.fire && !t.readyToFire()) return null; // wait for settle/reload gate; aim applied with the shot
            var c = _pending; _pending = null;
            t.markNonRanked();
            t.setAim(c.yaw, c.pitch);
            if (c.fire && !t.fire()) return LogTag + " fire_failed id=" + c.id;
            return AppliedLine(c);
        }

        public static string AppliedLine(DevAimCommand c) =>
            LogTag + " applied id=" + c.id.ToString(CultureInfo.InvariantCulture) + " yaw=" + F(c.yaw) + " pitch=" + F(c.pitch);
        public static string ResultLine(bool won) => won ? "CVTAP|RESULT|win" : "CVTAP|RESULT|fail";
        static string F(float v) => v.ToString("0.0##", CultureInfo.InvariantCulture);
    }
}
#endif
