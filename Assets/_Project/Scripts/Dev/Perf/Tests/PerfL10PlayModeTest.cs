#if UNITY_EDITOR // editor / batchmode PlayMode only (EditorSceneManager); also keeps the release guard scan happy
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Crashverse.DevTools.Perf.Tests
{
    /// <summary>
    /// PlayMode (editor/batchmode) runs of Scenes/Dev/L10_PerfFullBudget on Mid, 3 runs each, same seed and inputs.
    /// Main test: scene reload between runs (default stage mode); asserts the scripted 8° shot hits the curtain, break-FX
    /// saturation ≥ 1000 ms and peak debris ≥ 60 on every run, and MATCH across runs.
    /// Variant test: {auto-simulation, ranked Script/Physics.Simulate} × {InPlace Retry, Restart (new run tree), Reload};
    /// logs PERFL10_DETERMINISM per variant (retry-divergence diagnosis).
    /// Reflection because PerfL10Stage lives in Assembly-CSharp (asmdefs can't reference it).
    /// </summary>
    public class PerfL10PlayModeTest
    {
        const string ScenePath = "Assets/_Project/Scenes/Dev/L10_PerfFullBudget.unity";
        const int Runs = 3; const float TimeoutSec = 300f;

        static Type StageType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Crashverse.DevTools.Perf.PerfL10Stage")).FirstOrDefault(t => t != null);

        sealed class Outcome { public bool done, det; public IList results; public string variant; }

        static IEnumerator Run(string retryMode, bool ranked, Outcome o)
        {
            var t = StageType; Assert.IsNotNull(t, "PerfL10Stage type");
            t.GetMethod("ResetStatics", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            t.GetField("RunsOverride", BindingFlags.Public | BindingFlags.Static).SetValue(null, Runs);
            t.GetField("TierOverride", BindingFlags.Public | BindingFlags.Static).SetValue(null, "Mid");
            var modeType = t.GetNestedType("RetryMode");
            t.GetField("RetryModeOverride", BindingFlags.Public | BindingFlags.Static).SetValue(null, Enum.Parse(modeType, retryMode));
            t.GetField("RankedOverride", BindingFlags.Public | BindingFlags.Static).SetValue(null, ranked);
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
            var done = t.GetProperty("Done"); float start = Time.realtimeSinceStartup;
            UnityEngine.Object stage = null;
            while (Time.realtimeSinceStartup - start < TimeoutSec)
            {
                stage = UnityEngine.Object.FindObjectOfType(t); // a new instance after every reload
                if (stage != null && (bool)done.GetValue(stage)) break;
                yield return null;
            }
            Assert.IsNotNull(stage, "PerfL10Stage in scene");
            o.done = (bool)done.GetValue(stage);
            o.results = (IList)t.GetField("Results").GetValue(stage);
            o.det = (bool)t.GetProperty("Determinism").GetValue(stage);
            o.variant = (string)t.GetProperty("Variant").GetValue(stage);
            foreach (var s in (System.Collections.Generic.List<string>)t.GetField("Summaries").GetValue(stage)) Debug.Log(s);
            Debug.Log($"PERFL10_DETERMINISM,variant={o.variant},runs={o.results.Count},result={(o.det ? "MATCH" : "MISMATCH")}");
            t.GetMethod("ResetStatics", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        // Game UI/aim/camera scripts call legacy UnityEngine.Input while the project runs the Input System, which logs an exception
        // per frame in batchmode (pre-existing, reported to Developer). It doesn't touch the sim. Set in SetUp too: the previous
        // test's scene keeps logging until the next scene load, before the test body runs (CI run 38952901718).
        [SetUp] public void IgnoreLegacyInputLogs() => LogAssert.ignoreFailingMessages = true;

        static T F<T>(object r, string n) => (T)r.GetType().GetField(n).GetValue(r);

        [UnityTest, Timeout(400000)]
        public IEnumerator L10_FullBudget_Mid_HitsCapsAndIsDeterministic()
        {
            // Game UI/aim scripts call legacy UnityEngine.Input while the project runs the Input System, which logs an exception per
            // frame in batchmode (pre-existing, reported to Developer). It doesn't touch the sim; don't let it fail the perf check.
            LogAssert.ignoreFailingMessages = true;
            var o = new Outcome();
            yield return Run("Reload", false, o);
            Assert.IsTrue(o.done, $"finished {Runs} runs within {TimeoutSec}s (got {o.results.Count})");
            for (int i = 0; i < o.results.Count; i++)
            {
                var r = o.results[i];
                Assert.IsTrue(F<bool>(r, "hit"), $"run {i}: scripted shot hits the curtain");
                Assert.That(F<int>(r, "hitRow"), Is.InRange(0, 5), $"run {i}: hit on a lower/middle row");
                Assert.GreaterOrEqual(F<int>(r, "saturatedMs"), 1000, $"run {i}: 3 breaks/100 ms saturated ≥ 1 s");
                Assert.GreaterOrEqual(F<int>(r, "peakDebris"), 60, $"run {i}: ≥ 60 debris rigidbodies on Mid");
            }
            Assert.IsTrue(o.det, "scene reload between runs: identical PHYS_* hash");
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator L10_RetryVariants_Determinism()
        {
            LogAssert.ignoreFailingMessages = true;
            var table = new System.Text.StringBuilder("PERFL10_VARIANTS");
            foreach (var ranked in new[] { false, true })
                foreach (var mode in new[] { "InPlace", "Restart", "Reload" })
                {
                    var o = new Outcome();
                    yield return Run(mode, ranked, o);
                    table.Append($",{o.variant}={(o.done ? (o.det ? "MATCH" : "MISMATCH") : "TIMEOUT")}");
                }
            Debug.Log(table.ToString());
            // Diagnosis only for now: the result table is the output. Ranked assertions are added once the fix lands.
        }
    }
}
#endif
