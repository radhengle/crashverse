using System.Collections.Generic;
using Crashverse.Data;
using Crashverse.Destruction;
using Crashverse.Level;
using Crashverse.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Crashverse.Tests.Gameplay
{
    /// <summary>
    /// EditMode, real PhysX (SimulationMode.Script, 0.02 s steps): JointBreakPolicy keeps Level Designer's sim rules.
    /// Joints never break from resting load or debris; only the damage path (Break) and SupportRule remove them.
    /// </summary>
    public class JointBreakPolicyPhysicsTests
    {
        GameObject _root; readonly List<GameObject> _extra = new List<GameObject>();
        SimulationMode _prevMode; float _prevStep, _prevSleep; int _prevIter;

        [SetUp] public void SetUp()
        {
            _prevMode = Physics.simulationMode; _prevStep = Time.fixedDeltaTime; _prevIter = Physics.defaultSolverIterations; _prevSleep = Physics.sleepThreshold;
            Physics.simulationMode = SimulationMode.Script;
            Crashverse.Perf.QualityController.LockPhysics();
            DebrisDamageQueue.ResetForTests();
        }

        [TearDown] public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (var g in _extra) if (g != null) Object.DestroyImmediate(g);
            _extra.Clear();
            DebrisDamageQueue.ResetForTests();
            Physics.simulationMode = _prevMode; Time.fixedDeltaTime = _prevStep; Physics.defaultSolverIterations = _prevIter; Physics.sleepThreshold = _prevSleep;
        }

        static void Step(int ticks) { for (int i = 0; i < ticks; i++) Physics.Simulate(SimTime.Step); }

        LevelBuilder Build(params BlockData[] blocks)
        {
            _root = new GameObject("JBP");
            var lb = _root.AddComponent<LevelBuilder>();
            lb.Build(new LayoutData { id = "jbp", blocks = blocks }, Vector3.zero, new RuleModifiers());
            return lb;
        }

        static BlockData B(string id, string mat, float y, params string[] sup) =>
            new BlockData { id = id, mat = mat, p = new[] { 0f, y, 0f }, s = new[] { 1f, 1f, 1f }, sup = sup.Length > 0 ? sup : null };

        static int JointsOn(DestructibleBlock b) => b.GetComponents<Joint>().Length;

        static void AssertAllJointsInfinite(LevelBuilder lb)
        {
            foreach (var b in lb.Blocks)
                foreach (var j in b.GetComponents<Joint>())
                    Assert.IsTrue(JointBreakPolicy.Complies(j.breakForce, j.breakTorque), $"{b.blockId}: {j.breakForce}/{j.breakTorque}");
        }

        [Test] public void RankedPhysicsLock_Unchanged()
        {
            Assert.AreEqual(0.02f, Crashverse.Perf.QualityController.FixedStep);
            Assert.AreEqual(6, Crashverse.Perf.QualityController.SolverIterations);
            Assert.AreEqual(0.08f, Crashverse.Perf.QualityController.SleepThreshold);
            Assert.AreEqual(0.02f, Time.fixedDeltaTime, 1e-6f);
            Assert.AreEqual(6, Physics.defaultSolverIterations);
            Assert.AreEqual(0.08f, Physics.sleepThreshold, 1e-6f);
        }

        [Test] public void BlankKitJointValues_MapToInfinity()
        {
            var jnt = new GameObject("JNT_Twr_frag_01__Twr_frag_02"); _extra.Add(jnt);
            var h = jnt.AddComponent<Crashverse.Import.ImportedPhysicsHints>(); // blank: 0 = not authored
            Assert.IsTrue(JointBreakPolicy.IsBlank(h.breakForce) && JointBreakPolicy.IsBlank(h.breakTorque));
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.ResolveBreakForce(h.breakForce)));
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.ResolveBreakTorque(h.breakTorque)));
            h.breakForce = 450f; h.breakTorque = 450f; // explicit: ignored
            Assert.IsTrue(float.IsPositiveInfinity(JointBreakPolicy.ResolveBreakForce(h.breakForce)));
        }

        [Test] public void EveryMaterial_BuiltJoint_IsInfinite()
        {
            var lb = Build(B("g", "stone", 0f), B("w", "wood", 1f, "g"), B("br", "brick", 2f, "w"), B("st", "steel", 3f, "br"),
                           B("gl", "glass", 4f, "st"), B("sn", "stone", 5f, "gl"));
            Assert.AreEqual(5, CountJoints(lb));
            AssertAllJointsInfinite(lb);
        }

        static int CountJoints(LevelBuilder lb) { int n = 0; foreach (var b in lb.Blocks) n += JointsOn(b); return n; }

        [Test] public void StackedTower_AtRestUnderGravity_KeepsJoints_500Ticks()
        {
            // Stone tower module, 6 high. Worst case: every piece above the base made dynamic so joints carry the full stack load.
            var lb = Build(B("t0", "stone", 0.5f), B("t1", "stone", 1.5f, "t0"), B("t2", "stone", 2.5f, "t1"),
                           B("t3", "glass", 3.5f, "t2"), B("t4", "wood", 4.5f, "t3"), B("t5", "brick", 5.5f, "t4"));
            for (int i = 1; i < lb.Blocks.Count; i++) { lb.Blocks[i].Body.isKinematic = false; lb.Blocks[i].Body.mass *= 50f; }
            var y0 = new float[lb.Blocks.Count]; for (int i = 0; i < y0.Length; i++) y0[i] = lb.Blocks[i].transform.position.y;
            int before = CountJoints(lb);
            Step(500); // 10 s at 50 Hz
            Assert.AreEqual(before, CountJoints(lb), "a resting-load joint snapped");
            AssertAllJointsInfinite(lb);
            for (int i = 0; i < y0.Length; i++)
            {
                Assert.IsFalse(lb.Blocks[i].IsBroken || lb.Blocks[i].IsCollapsed, lb.Blocks[i].blockId);
                Assert.AreEqual(y0[i], lb.Blocks[i].transform.position.y, 0.25f, $"{lb.Blocks[i].blockId} sagged");
            }
        }

        [Test] public void DebrisImpact_AtMidTierCap_DoesNotBreakUndamagedJoint()
        {
            var lb = Build(B("base", "stone", 0.5f), B("top", "glass", 1.5f, "base")); // glass: the weakest legacy joint (250 N)
            var top = lb.Find("top"); top.Body.isKinematic = false; // worst case: dynamic, joint carries every hit
            int cap = Crashverse.Feel.DebrisBudget.Cap(Crashverse.Feel.QualityTier.Mid);
            Assert.AreEqual(60, cap);
            for (int i = 0; i < cap; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Cube); _extra.Add(d);
                d.transform.localScale = Vector3.one * 0.3f;
                d.transform.position = new Vector3(-2.5f - 0.35f * (i % 6), 1.5f + 0.35f * (i / 6 % 2), -0.6f + 0.35f * (i / 12));
                var rb = d.AddComponent<Rigidbody>(); rb.mass = 3f; rb.velocity = new Vector3(25f, 0f, 0f);
            }
            Step(150);
            Assert.AreEqual(1, JointsOn(top), "debris impact snapped an undamaged joint");
            AssertAllJointsInfinite(lb);
            Assert.IsFalse(top.IsBroken, "no damage was applied in this test (queue not drained): the joint path alone must hold");
        }

        [Test] public void DamagePath_StillBreaksJoint_AndSupportRuleDecidesCollapse()
        {
            // a ─ b ─ c   (c sits on b only)      d ─ e (e sits on b AND d)
            var lb = Build(B("a", "stone", 0.5f), B("b", "brick", 1.5f, "a"), B("c", "wood", 2.5f, "b"),
                           B("d", "stone", 0.5f), B("e", "wood", 2.5f, "b", "d"));
            var b = lb.Find("b"); var c = lb.Find("c"); var e = lb.Find("e");
            Assert.AreEqual(1, JointsOn(b)); Assert.AreEqual(1, JointsOn(c)); Assert.AreEqual(2, JointsOn(e));
            b.ApplyDamage(b.MaxHp * 10f, directHit: true);
            Assert.IsTrue(b.IsBroken);
            Assert.AreEqual(0, JointsOn(b), "broken piece keeps no joints");
            Assert.AreEqual(0, JointsOn(c), "joint to the broken support removed; c lost all supports");
            Assert.IsTrue(c.IsCollapsed, "SupportRule: all supports gone → collapse");
            Assert.AreEqual(1, JointsOn(e), "only the joint to b removed");
            Assert.AreEqual(lb.Find("d").Body, e.GetComponent<Joint>().connectedBody);
            Assert.IsFalse(e.IsCollapsed, "SupportRule: one support left → stays up");
        }
    }
}
