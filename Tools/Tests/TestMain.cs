using System;
using System.Collections.Generic;
using System.IO;
using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Meta;

static class T
{
    static int _pass, _fail;
    public static void Eq<X>(X exp, X act) => Eq(exp, act, $"{exp}");
    public static void Eq<X>(X exp, X act, string name)
    {
        if (EqualityComparer<X>.Default.Equals(exp, act)) _pass++;
        else { _fail++; Console.WriteLine($"FAIL {name}: expected {exp}, got {act}"); }
    }
    public static void True(bool c, string name) => Eq(true, c, name);
    static readonly List<(string, Action)> Cases = new List<(string, Action)>();
    public static void Add(string n, Action a) => Cases.Add((n, a));

    static int Main()
    {
        UnityEngine.Resources.Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/_Project/Resources"));
        Tests.Register(); SfxTests.Register();
        AnimTests.Register(); // Animation Spec v1 implementation (anim/* PRs)
        foreach (var (n, a) in Cases)
        {
            try { a(); } catch (Exception e) { _fail++; Console.WriteLine($"FAIL {n}: threw {e.GetType().Name}: {e.Message}"); }
        }
        Console.WriteLine($"{_pass} passed, {_fail} failed ({Cases.Count} cases)");
        return _fail == 0 ? 0 : 1;
    }
}
