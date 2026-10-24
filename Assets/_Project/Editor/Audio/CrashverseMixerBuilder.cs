// Builds Assets/_Project/Resources/Audio/CrashverseMixer.mixer exactly per audio/mixer_spec.md.
// Unity has no public API for authoring AudioMixers, so this uses reflection on the internal
// UnityEditor.Audio.AudioMixerController / AudioMixerGroupController / AudioMixerEffectController /
// AudioMixerSnapshotController (UnityCsReference 2022.3: Editor/Mono/Audio/Mixer/Bindings,
// Modules/AudioEditor/ScriptBindings). If any member is missing it logs exactly which one and exits 1.
//
// Run: Unity -batchmode -projectPath . -executeMethod CrashverseMixerBuilder.BuildMixer [-cvForceRebuild] -quit
// Idempotent: if the asset already has every group, effect, snapshot and exposed param it is left untouched.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public static class CrashverseMixerBuilder
{
    public const string AssetPath = "Assets/_Project/Resources/Audio/CrashverseMixer.mixer";
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    // ---------------- Spec (audio/mixer_spec.md) ----------------
    // name, parent, default dB
    static readonly (string name, string parent, float db)[] Groups =
    {
        ("Music", "Master", -3f), ("Base", "Music", 0f), ("Layers", "Music", 0f), ("Stingers", "Music", 0f),
        ("SFX", "Master", 0f), ("Destruction", "SFX", -2f), ("Weapon", "SFX", 0f), ("PowerUps", "SFX", -1f),
        ("Boss", "SFX", 0f), ("Rewards", "SFX", -2f), ("UI", "Master", -4f),
    };
    static readonly string[] DuckTargets = { "Base", "Layers" };          // Duck Volume before Attenuation
    static readonly string[] DuckSources = { "Weapon", "Boss", "Stingers" }; // Send (post-fader) -> each duck
    // Duck Volume settings (times are seconds in the effect definition). Tune Threshold for ~-5 dB on a nominal cannon hit.
    static readonly (string p, float v)[] DuckParams =
    {
        ("Threshold", -25f), ("Ratio", 4f), ("Attack Time", 0.010f), ("Release Time", 0.300f),
        ("Make-up Gain", 0f), ("Knee", 10f), ("Sidechain Mix", 1f),
    };
    static readonly string[] LowpassNames = { "Lowpass Simple" };
    static readonly string[] CutoffParamNames = { "Cutoff freq", "Cutoff Frequency", "Cutoff" };
    // Exposed params: name -> group (volume) or "PowerUps:LPF"
    static readonly (string exposed, string group, bool lpf)[] Exposed =
    {
        ("MasterVol", "Master", false), ("MusicVol", "Music", false), ("SfxVol", "SFX", false),
        ("UiVol", "UI", false), ("PowerUpLPF", "PowerUps", true),
    };
    // Snapshot deltas in dB relative to Default (spec "Music -4 dB" = 4 dB below its default). lpf = absolute Hz.
    static readonly (string name, (string g, float d)[] vol, float lpf)[] Snapshots =
    {
        ("SlowMo",       new[] { ("Music", -4f), ("SFX", -1f) }, 2500f),
        ("TimeFracture", new[] { ("Music", -6f), ("Destruction", -3f) }, 1800f),
        ("Paused",       new[] { ("Music", -10f), ("SFX", -80f) }, 22000f),
        ("Results",      new[] { ("Destruction", -12f) }, 22000f),
        ("Muted",        new[] { ("Master", -80f) }, 22000f),
    };

    // ---------------- Reflection helpers ----------------
    static Type _ctrlT, _groupT, _effectT, _snapT, _groupPathT, _effectPathT;
    static Type FindType(string fullName)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType(fullName, false);
            if (t != null) return t;
        }
        throw new MissingMemberException($"[MixerBuilder] internal type not found: {fullName} (Unity {Application.unityVersion})");
    }
    static void Init()
    {
        _ctrlT = FindType("UnityEditor.Audio.AudioMixerController");
        _groupT = FindType("UnityEditor.Audio.AudioMixerGroupController");
        _effectT = FindType("UnityEditor.Audio.AudioMixerEffectController");
        _snapT = FindType("UnityEditor.Audio.AudioMixerSnapshotController");
        _groupPathT = FindType("UnityEditor.Audio.AudioGroupParameterPath");
        _effectPathT = FindType("UnityEditor.Audio.AudioEffectParameterPath");
    }
    static object Call(object target, string method, params object[] args)
    {
        var t = target as Type ?? target.GetType();
        var inst = target is Type ? null : target;
        var m = t.GetMethods(All).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
        if (m == null) throw new MissingMemberException($"[MixerBuilder] {t.FullName}.{method}({args.Length} args) not found (Unity {Application.unityVersion})");
        return m.Invoke(inst, args);
    }
    static object Get(object target, string member)
    {
        var t = target.GetType();
        var p = t.GetProperty(member, All);
        if (p != null) return p.GetValue(target);
        var f = t.GetField(member, All);
        if (f != null) return f.GetValue(target);
        throw new MissingMemberException($"[MixerBuilder] {t.FullName}.{member} not found");
    }
    static void Set(object target, string member, object value)
    {
        var t = target.GetType();
        var p = t.GetProperty(member, All);
        if (p != null && p.CanWrite) { p.SetValue(target, value); return; }
        var f = t.GetField(member, All);
        if (f != null) { f.SetValue(target, value); return; }
        throw new MissingMemberException($"[MixerBuilder] settable {t.FullName}.{member} not found");
    }
    static object NewObj(Type t, params object[] args)
    {
        var c = t.GetConstructors(All).FirstOrDefault(x => x.GetParameters().Length == args.Length);
        if (c == null) throw new MissingMemberException($"[MixerBuilder] ctor {t.FullName}({args.Length}) not found");
        return c.Invoke(args);
    }
    static bool IsEmpty(GUID g) => g.Empty();

    // ---------------- Entry point ----------------
    [MenuItem("Crashverse/Audio/Build Crashverse Mixer")]
    public static void BuildMixer()
    {
        bool force = Environment.GetCommandLineArgs().Contains("-cvForceRebuild");
        try
        {
            Init();
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetPath);
            if (existing != null && !force)
            {
                var problems = Verify(existing);
                if (problems.Count == 0) { Debug.Log($"[MixerBuilder] {AssetPath} already matches mixer_spec.md; nothing to do."); Finish(0); return; }
                Debug.Log($"[MixerBuilder] existing mixer incomplete ({string.Join("; ", problems)}); rebuilding.");
            }
            if (existing != null) AssetDatabase.DeleteAsset(AssetPath);
            Build();
            var after = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetPath);
            var errs = after == null ? new List<string> { "asset not created" } : Verify(after);
            if (errs.Count > 0) throw new Exception("[MixerBuilder] verification failed: " + string.Join("; ", errs));
            Debug.Log($"[MixerBuilder] OK: built {AssetPath} (Unity {Application.unityVersion}).");
            Finish(0);
        }
        catch (Exception e)
        {
            var inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
            Debug.LogError($"[MixerBuilder] FAILED: {inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}");
            Finish(1);
        }
    }

    static void Finish(int code) { if (Application.isBatchMode) EditorApplication.Exit(code); }

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    static void Build()
    {
        EnsureFolder("Assets/_Project/Resources/Audio");
        var ctrl = Call(_ctrlT, "CreateMixerControllerAtPath", AssetPath);
        if (ctrl == null) throw new Exception("CreateMixerControllerAtPath returned null");
        var master = Get(ctrl, "masterGroup");
        var snap = Get(ctrl, "TargetSnapshot");
        ((UnityEngine.Object)snap).name = "Default";

        var groups = new Dictionary<string, object> { ["Master"] = master };
        foreach (var (name, parent, db) in Groups)
        {
            var g = Call(ctrl, "CreateNewGroup", name, false);
            Call(ctrl, "AddChildToParent", g, groups[parent]);
            groups[name] = g;
            Call(g, "SetValueForVolume", ctrl, snap, db);
        }

        // Duck Volume on Base and Layers (index 0, before Attenuation)
        var ducks = new Dictionary<string, object>();
        foreach (var t in DuckTargets)
        {
            var duck = AddEffect(ctrl, groups[t], "Duck Volume", 0);
            foreach (var (p, v) in DuckParams) SetParam(ctrl, snap, duck, p, v);
            ducks[t] = duck;
        }
        // Sends (post-fader, appended) from Weapon/Boss/Stingers to each duck, 0 dB
        foreach (var s in DuckSources)
            foreach (var t in DuckTargets)
            {
                var g = groups[s];
                var send = AddEffect(ctrl, g, "Send", ((Array)Get(g, "effects")).Length);
                Set(send, "sendTarget", ducks[t]);
                Call(send, "SetValueForMixLevel", ctrl, snap, 0f);
            }
        // Lowpass Simple on PowerUps (after Attenuation)
        var pu = groups["PowerUps"];
        var lpf = AddEffect(ctrl, pu, ResolveEffectName(LowpassNames), ((Array)Get(pu, "effects")).Length);
        string cutoff = ResolveParam(lpf, CutoffParamNames);
        SetParam(ctrl, snap, lpf, cutoff, 22000f);

        // Exposed parameters, renamed to the contract names
        foreach (var (exposed, group, isLpf) in Exposed)
        {
            var g = groups[group];
            object path; GUID guid;
            if (isLpf) { guid = (GUID)Call(lpf, "GetGUIDForParameter", cutoff); path = NewObj(_effectPathT, g, lpf, guid); }
            else { guid = (GUID)Call(g, "GetGUIDForVolume"); path = NewObj(_groupPathT, g, guid); }
            if (IsEmpty(guid)) throw new Exception($"empty GUID for exposed param {exposed}");
            Call(ctrl, "AddExposedParameter", path);
            RenameExposed(ctrl, guid, exposed);
        }

        // Snapshots: clone Default, then apply deltas
        var defaults = Groups.ToDictionary(x => x.name, x => x.db); defaults["Master"] = 0f;
        foreach (var (name, vol, lpfHz) in Snapshots)
        {
            Set(ctrl, "TargetSnapshot", snap);
            Call(ctrl, "CloneNewSnapshotFromTarget", false);
            var s = Get(ctrl, "TargetSnapshot");
            ((UnityEngine.Object)s).name = name;
            foreach (var (g, d) in vol) Call(groups[g], "SetValueForVolume", ctrl, s, Mathf.Max(-80f, defaults[g] + d));
            SetParam(ctrl, s, lpf, cutoff, lpfHz);
        }
        Set(ctrl, "TargetSnapshot", snap);
        Set(ctrl, "startSnapshot", snap);

        EditorUtility.SetDirty((UnityEngine.Object)ctrl);
        foreach (var g in groups.Values) EditorUtility.SetDirty((UnityEngine.Object)g);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
    }

    static object AddEffect(object ctrl, object group, string effectName, int index)
    {
        var e = NewObj(_effectT, effectName);
        AssetDatabase.AddObjectToAsset((UnityEngine.Object)e, (UnityEngine.Object)ctrl);
        Call(e, "PreallocateGUIDs");
        Call(group, "InsertEffect", e, index);
        var actual = (string)Get(e, "effectName");
        if (actual != effectName) throw new Exception($"effect '{effectName}' created as '{actual}'");
        return e;
    }

    static string ResolveEffectName(string[] candidates)
    {
        var defs = FindType("UnityEditor.Audio.MixerEffectDefinitions");
        var list = (string[])Call(defs, "GetEffectList");
        foreach (var c in candidates) if (list.Contains(c)) return c;
        throw new Exception($"none of [{string.Join(", ", candidates)}] in effect list: {string.Join(", ", list)}");
    }

    static string ResolveParam(object effect, string[] candidates)
    {
        foreach (var c in candidates)
            if (!IsEmpty((GUID)Call(effect, "GetGUIDForParameter", c))) return c;
        string known = "";
        try
        {
            var defs = FindType("UnityEditor.Audio.MixerEffectDefinitions");
            var ps = (Array)Call(defs, "GetEffectParameters", (string)Get(effect, "effectName"));
            known = string.Join(", ", ps.Cast<object>().Select(p => (string)p.GetType().GetField("name").GetValue(p)));
        }
        catch { }
        throw new Exception($"no param [{string.Join(", ", candidates)}] on {Get(effect, "effectName")}; available: {known}");
    }

    static void SetParam(object ctrl, object snap, object effect, string p, float v)
    {
        if (IsEmpty((GUID)Call(effect, "GetGUIDForParameter", p)))
            throw new Exception($"parameter '{p}' not found on {Get(effect, "effectName")}");
        Call(effect, "SetValueForParameter", ctrl, snap, p, v);
    }

    static void RenameExposed(object ctrl, GUID guid, string name)
    {
        var arr = (Array)Get(ctrl, "exposedParameters");
        for (int i = 0; i < arr.Length; i++)
        {
            var p = arr.GetValue(i);
            var gf = p.GetType().GetField("guid"); var nf = p.GetType().GetField("name");
            if ((GUID)gf.GetValue(p) == guid) { nf.SetValue(p, name); arr.SetValue(p, i); Set(ctrl, "exposedParameters", arr); return; }
        }
        throw new Exception($"exposed param for {name} not found after AddExposedParameter");
    }

    // ---------------- Verification (public API only) ----------------
    static List<string> Verify(AudioMixer m)
    {
        var errs = new List<string>();
        foreach (var path in new[] { "Master" }.Concat(Groups.Select(g => GroupPath(g.name))))
            if (!m.FindMatchingGroups(path).Any(g => g.name == path.Split('/').Last())) errs.Add("missing group " + path);
        foreach (var s in new[] { "Default" }.Concat(Snapshots.Select(x => x.name)))
            if (m.FindSnapshot(s) == null || m.FindSnapshot(s).name != s) errs.Add("missing snapshot " + s);
        try
        {
            var names = ((Array)Get(m, "exposedParameters")).Cast<object>().Select(p => (string)p.GetType().GetField("name").GetValue(p)).ToList();
            foreach (var e in Exposed) if (!names.Contains(e.exposed)) errs.Add("missing exposed param " + e.exposed);
        }
        catch (Exception e) { errs.Add("exposed param check failed: " + e.Message); }
        // Effects (internal): ducks + sends
        try
        {
            var all = (System.Collections.IEnumerable)Call(m, "GetAllAudioGroupsSlow");
            foreach (var g in all)
            {
                var n = ((UnityEngine.Object)g).name;
                var fx = ((Array)Get(g, "effects")).Cast<object>().Select(e => (string)Get(e, "effectName")).ToList();
                if (DuckTargets.Contains(n) && fx.IndexOf("Duck Volume") != 0) errs.Add($"{n}: Duck Volume not first ({string.Join(",", fx)})");
                if (DuckSources.Contains(n) && fx.Count(x => x == "Send") != DuckTargets.Length) errs.Add($"{n}: expected {DuckTargets.Length} Sends");
                if (n == "PowerUps" && !fx.Any(x => LowpassNames.Contains(x))) errs.Add("PowerUps: no lowpass");
            }
        }
        catch (Exception e) { errs.Add("effect check failed: " + e.Message); }
        return errs;
    }

    static string GroupPath(string name)
    {
        var parents = Groups.ToDictionary(g => g.name, g => g.parent);
        var path = name;
        while (parents.TryGetValue(name, out var p)) { path = p + "/" + path; name = p; }
        return path;
    }
}
