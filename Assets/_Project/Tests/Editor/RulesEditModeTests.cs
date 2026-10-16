#if UNITY_INCLUDE_TESTS
using Crashverse.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Crashverse.Tests.EditMode
{
    /// <summary>
    /// False-green fix (QA retro #9): real EditMode tests inside Assets so the Unity test runner discovers them.
    /// They live in Assembly-CSharp-Editor (no asmdef: game code is in Assembly-CSharp, which an asmdef can't reference);
    /// ProjectSettings playModeTestRunnerEnabled gives the predefined assemblies their NUnit references.
    /// The wider logic suite stays in Tools/Tests (dotnet, run in the fast-fail step).
    /// </summary>
    public class RulesEditModeTests
    {
        [Test] public void Win_is_exactly_85_percent_in_basis_points()
        {
            Assert.True(WinRule.Meets(0.85f, 0.85f));
            Assert.True(WinRule.Meets(0.84999996f, 0.85f), "float noise on a real 85% still wins");
            Assert.False(WinRule.Meets(0.8499f, 0.85f));
            Assert.AreEqual(6666, WinRule.FractionBp(2, 3));
        }

        [Test] public void Star_bands_exact_thresholds_pass()
        {
            Assert.AreEqual(3, StarRules.MinShotsLeft(5, 0.6f));
            Assert.AreEqual(60, StarRules.ToPctInt(0.6f));
        }

        [Test] public void Age_gate_under_13_and_invalid_dates()
        {
            Assert.AreEqual(AgeGate.Band.Under13, AgeGate.Classify(2018, 1, 2026, 10));
            Assert.AreEqual(AgeGate.Band.Unknown, AgeGate.Classify(2027, 1, 2026, 10), "future date");
            Assert.AreEqual(AgeGate.Band.Unknown, AgeGate.Classify(2000, 13, 2026, 10), "bad month");
            Assert.AreEqual(12, AgeGate.AgeYears(2013, 10, 2026, 10), "birthday month counts as not yet reached");
        }

        [Test] public void Input_manager_has_the_axes_the_UI_module_reads()
        {
            foreach (var axis in new[] { "Horizontal", "Vertical", "Submit", "Cancel" })
                Assert.DoesNotThrow(() => Input.GetAxisRaw(axis), axis);
        }
    }
}
#endif
