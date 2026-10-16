#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Crashverse.Core.Tests
{
    /// <summary>
    /// Regression for the in-process Retry divergence (PERFL10 runs 38950649389 and earlier: MISMATCH across in-place retries).
    /// Casual Retry must rebuild the debris pool (new object, old one destroyed). Ranked StartRun after another run in the
    /// same scene, and ranked Retry, must reload the scene and start the run there (fresh run tree, ranked SimRunner at tick 0).
    /// Reflection: game code lives in Assembly-CSharp, which asmdefs can't reference.
    /// </summary>
    public class RetryDeterminismPlayModeTest
    {
        const string Bootstrap = "Assets/_Project/Scenes/Bootstrap.unity";
        static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(n)).FirstOrDefault(t => t != null);
        static object SP(Type t, string n) { var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Static); return p != null ? p.GetValue(null) : t.GetField(n, BindingFlags.Public | BindingFlags.Static).GetValue(null); }
        static object P(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        static void F(object o, string n, object v) => o.GetType().GetField(n).SetValue(o, v);

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) { yield return null; LogAssert.ignoreFailingMessages = true; } }

        [UnityTest, Timeout(180000)]
        public IEnumerator CasualRetryRebuildsPool_RankedRetryReloadsScene()
        {
            LogAssert.ignoreFailingMessages = true; // legacy-Input log from UI scripts (temporary until the Input System move)
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Bootstrap, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) { yield return null; LogAssert.ignoreFailingMessages = true; }
            Type app = T("Crashverse.Core.AppServices"), dir = T("Crashverse.Core.GameplayDirector"), ctxT = T("Crashverse.Core.RunContext"),
                 modeT = T("Crashverse.Core.RunMode"), pool = T("Crashverse.Destruction.DebrisPool"), sim = T("Crashverse.Sim.SimRunner"), ticketT = T("Crashverse.Backend.RankedTicket");
            float t0 = Time.realtimeSinceStartup;
            while (SP(app, "Content") == null && Time.realtimeSinceStartup - t0 < 30f) yield return Frames(1);
            var db = SP(app, "Content"); Assert.IsNotNull(db, "content loaded");
            var ch1 = P(db, "Chapter1"); var level = ((Array)ch1.GetType().GetField("levels").GetValue(ch1)).GetValue(0);
            var layout = db.GetType().GetMethod("GetLayout").Invoke(db, new[] { level.GetType().GetField("layout").GetValue(level) });
            object Ctx(string mode)
            {
                var c = Activator.CreateInstance(ctxT);
                F(c, "mode", Enum.Parse(modeT, mode)); F(c, "level", level); F(c, "layout", layout); F(c, "seed", 0x5EED1234L);
                if (mode == "TitanRanked") { var tk = Activator.CreateInstance(ticketT); F(tk, "token", "regression-test"); F(c, "ticket", tk); }
                return c;
            }
            var start = dir.GetMethod("StartRun");

            // Casual: Retry replaces the debris pool.
            start.Invoke(null, new[] { Ctx("Campaign") });
            yield return Frames(3);
            var session = SP(dir, "Session"); Assert.IsNotNull(session, "casual session");
            var pool1 = (UnityEngine.Object)SP(pool, "Instance"); Assert.IsTrue(pool1 != null, "pool exists");
            session.GetType().GetMethod("Retry").Invoke(session, null);
            var pool2 = (UnityEngine.Object)SP(pool, "Instance");
            Assert.IsTrue(pool1 == null, "old pool destroyed on casual Retry");
            Assert.IsTrue(pool2 != null && !ReferenceEquals(pool1, pool2), "new pool on casual Retry");
            Assert.AreEqual(0, (int)SP(dir, "RankedReloads"), "casual Retry never reloads");
            yield return Frames(3);

            // Ranked start after a casual run in this scene: scene reload, then the run starts there.
            start.Invoke(null, new[] { Ctx("TitanRanked") });
            t0 = Time.realtimeSinceStartup;
            while (((int)SP(dir, "RankedReloads") < 1 || SP(dir, "Session") == null) && Time.realtimeSinceStartup - t0 < 30f) yield return Frames(1);
            Assert.AreEqual(1, (int)SP(dir, "RankedReloads"), "ranked start reloaded the scene");
            var rs = SP(dir, "Session"); Assert.IsNotNull(rs, "ranked session after reload");
            var runner = SP(sim, "Instance"); Assert.IsTrue((bool)P(runner, "Ranked"), "SimRunner ranked");

            // Ranked Retry: reload again, new session.
            rs.GetType().GetMethod("Retry").Invoke(rs, null);
            t0 = Time.realtimeSinceStartup;
            while (((int)SP(dir, "RankedReloads") < 2 || SP(dir, "Session") == null) && Time.realtimeSinceStartup - t0 < 30f) yield return Frames(1);
            Assert.AreEqual(2, (int)SP(dir, "RankedReloads"), "ranked Retry reloaded the scene");
            Assert.IsFalse(ReferenceEquals(rs, SP(dir, "Session")), "ranked Retry gives a new session");
            Assert.Less((int)P(SP(sim, "Instance"), "Tick"), 200, "ranked run restarted near tick 0");
            dir.GetMethod("Reset").Invoke(null, null);
            yield return Frames(1);
        }
    }
}
#endif
