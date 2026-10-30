namespace Crashverse.Sim
{
    /// <summary>
    /// The one physics-owned joint break policy (physics/Joint_Break_Policy.md). Matches Level Designer's solvability sim:
    /// a structural joint never breaks from a force or torque threshold; it goes only through the damage path
    /// (HP / Contract §3 steel-joint 2× rule → DestructibleBlock.Break) or when the piece it is attached to stops supporting
    /// (broken, collapsed or out of bounds → DestructibleBlock drops the joint, SupportRule decides collapse).
    ///
    /// Kit JNT_ break_force_N / break_torque_Nm: blank (missing, 0, negative, NaN) maps to infinity. Explicit finite values are
    /// IGNORED, not clamped: any finite threshold is a force-break path the sim does not model, and a clamp would still be one.
    /// The authored number stays on ImportedPhysicsHints as data; the import validator warns about it.
    /// Engine-free (Crashverse.Physics.Core): float.PositiveInfinity == Mathf.Infinity.
    /// </summary>
    public static class JointBreakPolicy
    {
        /// <summary>Joint.breakForce for every structural joint (N).</summary>
        public const float BreakForce = float.PositiveInfinity;
        /// <summary>Joint.breakTorque for every structural joint (N·m).</summary>
        public const float BreakTorque = float.PositiveInfinity;

        public enum AuthoredValueHandling { Ignored = 0 }
        /// <summary>What happens to an explicit kit break_force_N / break_torque_Nm: ignored (see summary).</summary>
        public const AuthoredValueHandling ExplicitKitValues = AuthoredValueHandling.Ignored;

        /// <summary>True when the kit left the value blank (missing/0/negative/NaN). Positive infinity counts as authored-unbreakable.</summary>
        public static bool IsBlank(float authored) => !(authored > 0f);

        /// <summary>True when an explicit finite value was authored (it is ignored; validator warns).</summary>
        public static bool IsExplicitFinite(float authored) => authored > 0f && !float.IsPositiveInfinity(authored);

        /// <summary>Break force to put on a joint, for any authored value (blank or explicit). Always infinity.</summary>
        public static float ResolveBreakForce(float authored) => BreakForce;
        /// <summary>Break torque to put on a joint, for any authored value (blank or explicit). Always infinity.</summary>
        public static float ResolveBreakTorque(float authored) => BreakTorque;

        /// <summary>CI/test predicate: a joint complies only when both thresholds are +infinity.</summary>
        public static bool Complies(float breakForce, float breakTorque) =>
            float.IsPositiveInfinity(breakForce) && float.IsPositiveInfinity(breakTorque);
    }
}
