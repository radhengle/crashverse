#if CRASHVERSE_DEV
using System;
using System.IO;
using System.Linq;
using Crashverse.DevHooks;
using Crashverse.Weapons;

/// <summary>BUG-24 dev aim hook: strict parse, limits, opt-in, once-per-id, ranked suppression, release stripping.</summary>
static class DevAimHookTests
{
    const float MY = WreckerSpec.MaxYaw, MinP = WreckerSpec.MinElevation, MaxP = WreckerSpec.MaxElevation;
    static bool P(string j, out DevAimCommand c, out string e) => DevAimCommandParser.TryParse(j, MY, MinP, MaxP, out c, out e);

    class Fake
    {
        public bool ranked, ready = true, fireOk = true, nonRanked; public int fires; public float yaw, pitch;
        public DevAimTarget T => new DevAimTarget { maxYaw = MY, minPitch = MinP, maxPitch = MaxP, isRanked = () => ranked, readyToFire = () => ready,
            setAim = (y, p) => { yaw = y; pitch = p; }, fire = () => { if (fireOk) fires++; return fireOk; }, markNonRanked = () => nonRanked = true };
    }
    const string Hint = "{\"yaw\":1.0,\"pitch\":11.0,\"fire\":true,\"id\":1}";

    public static void Register()
    {
        T.Add("devaim_parse_hint", () =>
        {
            T.True(P(Hint, out var c, out _), "hint parses");
            T.Eq(1, c.id, "id"); T.Eq(1.0f, c.yaw, "yaw"); T.Eq(11.0f, c.pitch, "pitch"); T.True(c.fire, "fire");
            T.True(P(" { \"id\" : 7 , \"yaw\" : -60 , \"pitch\" : 70 , \"fire\" : false } \n", out c, out _) && !c.fire && c.yaw == -60f, "whitespace + limits inclusive");
        });
        T.Add("devaim_reject_bad_input", () =>
        {
            string[] bad = {
                "{\"yaw\":1.0,\"pitch\":-30,\"fire\":true,\"id\":2}",          // pitch below 5° min: rejected, never clamped
                "{\"yaw\":61,\"pitch\":11,\"fire\":true,\"id\":2}",            // yaw beyond ±60
                "{\"yaw\":1,\"pitch\":71,\"fire\":true,\"id\":2}",
                "{\"yaw\":1,\"pitch\":11,\"fire\":true,\"id\":2,\"path\":1}",  // unknown key
                "{\"yaw\":1,\"pitch\":11,\"fire\":true}",                      // missing id
                "{\"yaw\":1,\"yaw\":2,\"pitch\":11,\"fire\":true,\"id\":2}",   // duplicate
                "{\"yaw\":\"1\",\"pitch\":11,\"fire\":true,\"id\":2}",         // string value
                "{\"yaw\":NaN,\"pitch\":11,\"fire\":true,\"id\":2}",
                "{\"yaw\":1e999,\"pitch\":11,\"fire\":true,\"id\":2}",
                "{\"yaw\":1,\"pitch\":11,\"fire\":1,\"id\":2}",
                "{\"yaw\":1,\"pitch\":11,\"fire\":true,\"id\":-2}",
                "{\"yaw\":1,\"pitch\":11,\"fire\":true,\"id\":2.5}",
                "{\"yaw\":{\"a\":1},\"pitch\":11,\"fire\":true,\"id\":2}",
                "{\"yaw\":1,\"pitch\":11,\"fire\":true,\"id\":2} x",
                "[1,2]", "", "{}", "{\"yaw\":1,\"pitch\":11,\"fire\":true,\"id\":2" + new string(' ', 600) + "}",
            };
            foreach (var b in bad) T.True(!P(b, out _, out _), "rejects " + (b.Length > 60 ? b.Substring(0, 60) : b));
            P(bad[0], out _, out var e); T.Eq("pitch_out_of_limits", e, "pitch -30 reason");
        });
        T.Add("devaim_optin_required", () =>
        {
            var f = new Fake(); var core = new DevAimHookCore(false);
            T.Eq(null, core.Offer(Hint, f.T), "no log"); T.Eq(null, core.Tick(f.T), "no tick");
            T.Eq(0, f.fires, "no fire without opt-in"); T.True(!f.nonRanked, "untouched");
        });
        T.Add("devaim_apply_fire_once_per_id", () =>
        {
            var f = new Fake(); var core = new DevAimHookCore(true);
            T.Eq(null, core.Offer(Hint, f.T), "accepted");
            T.Eq("CRASHVERSE_DEV_AIM applied id=1 yaw=1.0 pitch=11.0", core.Tick(f.T), "log line");
            T.Eq(1, f.fires, "fired"); T.Eq(1f, f.yaw, "yaw set"); T.Eq(11f, f.pitch, "pitch set"); T.True(f.nonRanked, "marked non-ranked");
            T.True(core.Offer(Hint, f.T).Contains("duplicate_id"), "same id ignored"); core.Tick(f.T);
            T.Eq(1, f.fires, "still one fire");
            core.Offer(Hint.Replace("\"id\":1", "\"id\":2"), f.T); core.Tick(f.T);
            T.Eq(2, f.fires, "new id fires");
        });
        T.Add("devaim_waits_for_gate", () =>
        {
            var f = new Fake { ready = false }; var core = new DevAimHookCore(true);
            core.Offer(Hint, f.T);
            T.Eq(null, core.Tick(f.T), "waits while resolving"); T.Eq(0, f.fires, "no fire yet"); T.True(core.HasPending, "kept");
            f.ready = true; T.True(core.Tick(f.T).StartsWith("CRASHVERSE_DEV_AIM applied id=1"), "fires once ready"); T.Eq(1, f.fires, "fired");
        });
        T.Add("devaim_ranked_suppressed", () =>
        {
            var f = new Fake { ranked = true }; var core = new DevAimHookCore(true);
            T.Eq("CRASHVERSE_DEV_AIM rejected reason=ranked", core.Offer(Hint, f.T), "rejected in ranked");
            core.Tick(f.T); T.Eq(0, f.fires, "never fires in ranked"); T.Eq(0f, f.yaw, "aim untouched");
            var f2 = new Fake { ready = false }; var c2 = new DevAimHookCore(true); c2.Offer(Hint, f2.T);
            f2.ranked = true; f2.ready = true; T.Eq("CRASHVERSE_DEV_AIM rejected reason=ranked", c2.Tick(f2.T), "pending dropped if ranked"); T.Eq(0, f2.fires, "no fire");
        });
        T.Add("devaim_result_lines", () =>
        {
            T.Eq("CVTAP|RESULT|win", DevAimHookCore.ResultLine(true)); T.Eq("CVTAP|RESULT|fail", DevAimHookCore.ResultLine(false));
            T.Eq("dev_aim_cmd.json", DevAimHookCore.FileName);
        });
        T.Add("devaim_release_stripped_source", () =>
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/_Project/Scripts"));
            var dir = Path.Combine(root, "DevHooks");
            foreach (var f in Directory.GetFiles(dir, "*.cs"))
            {
                var l = File.ReadAllLines(f).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                T.True(l[0] == "#if CRASHVERSE_DEV" && l[l.Length - 1] == "#endif", Path.GetFileName(f) + " wholly behind bare #if CRASHVERSE_DEV");
            }
            var asm = File.ReadAllText(Path.Combine(dir, "Crashverse.DevHooks.asmdef"));
            T.True(System.Text.RegularExpressions.Regex.IsMatch(asm, "\"defineConstraints\"\\s*:\\s*\\[\\s*\"CRASHVERSE_DEV\"\\s*\\]"), "asmdef defineConstraints [CRASHVERSE_DEV]");
            foreach (var f in new[] { "Core/GameBootstrap.cs", "Core/GameSession.cs" })
            {
                var src = File.ReadAllLines(Path.Combine(root, f)); string guard = null;
                for (int i = 0; i < src.Length; i++)
                {
                    var s = src[i].Trim();
                    if (s.StartsWith("#if")) guard = s; else if (s.StartsWith("#endif") || s.StartsWith("#else")) guard = null;
                    else if (src[i].Contains("DevHooks") || src[i].Contains("DevHookUsed")) T.True(guard == "#if CRASHVERSE_DEV", f + ":" + (i + 1) + " hook ref behind bare #if CRASHVERSE_DEV");
                }
            }
        });
    }
}
#endif
