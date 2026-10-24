using System;
using System.Collections.Generic;

namespace Crashverse.Sim
{
    /// <summary>Outcome of mapping an imported fractureClass string.</summary>
    public enum FractureClassStatus : byte { Mapped = 0, Empty = 1, Unknown = 2 }

    /// <summary>
    /// Engine-free mapping from the import pipeline's free-text <c>ImportedPhysicsHints.fractureClass</c>
    /// (hero buildings: models/HeroBuildings/Kit_&lt;B&gt;_stats.json modules[*].frac, or an FBX fracture_class) to the
    /// physics material family. Physics-owned (see physics/FractureClass_Mapping.md).
    ///
    /// Deterministic and culture-independent: the input is trimmed with string.Trim (char.IsWhiteSpace, no culture) and
    /// compared with StringComparison.OrdinalIgnoreCase against a fixed table. No ToLower/ToUpper, no CurrentCulture,
    /// so "ROOF" maps the same on a tr-TR device as in CI.
    ///
    /// Roof maps to Stone: every Roof module in the hero kits is a red terracotta-tile roof sitting on a stone module
    /// (Keep/Tower/Gate turret and tower roofs, House roof slope); PM already routes Roof to Stone FX with a terracotta dust
    /// tint, and wooden-shingle roofs are tagged "Wood" by the 3D Modeler. Using Stone keeps PHYS_* events, audio banks
    /// and VFX on one material family. No new enum value: MaterialType/MaterialKind are serialized and append-only.
    ///
    /// Empty / null = not authored: caller's fallback, no warning. Unknown = caller's fallback plus one warning per
    /// distinct value (ResolveWarnOnce).
    /// </summary>
    public static class FractureClassRules
    {
        /// <summary>Default when a value is missing or unknown: Wood, the same default LevelBuilder uses for an unknown "mat".</summary>
        public const MaterialType DefaultFallback = MaterialType.Wood;

        public const string Wood = "Wood", Brick = "Brick", Steel = "Steel", Glass = "Glass", Stone = "Stone", Roof = "Roof";

        // Fixed order; lookup is a linear ordinal scan (6 entries), so there is no hashing or culture involved.
        static readonly string[] Keys = { Wood, Brick, Steel, Glass, Stone, Roof };
        static readonly MaterialType[] Types = { MaterialType.Wood, MaterialType.Brick, MaterialType.Steel, MaterialType.Glass, MaterialType.Stone, MaterialType.Stone };

        /// <summary>Known class names in table order (copy).</summary>
        public static string[] KnownClasses() => (string[])Keys.Clone();

        /// <summary>Trimmed value, or "" for null / whitespace.</summary>
        public static string Normalize(string fractureClass) => fractureClass == null ? "" : fractureClass.Trim();

        /// <summary>True for "Roof" (any case, surrounding whitespace ignored).</summary>
        public static bool IsRoof(string fractureClass) => string.Equals(Normalize(fractureClass), Roof, StringComparison.OrdinalIgnoreCase);

        /// <summary>Maps a fractureClass. <paramref name="type"/> is <paramref name="fallback"/> unless the status is Mapped.</summary>
        public static FractureClassStatus Resolve(string fractureClass, out MaterialType type, MaterialType fallback = DefaultFallback)
        {
            string s = Normalize(fractureClass);
            if (s.Length == 0) { type = fallback; return FractureClassStatus.Empty; }
            for (int i = 0; i < Keys.Length; i++)
            {
                if (string.Equals(s, Keys[i], StringComparison.OrdinalIgnoreCase)) { type = Types[i]; return FractureClassStatus.Mapped; }
            }
            type = fallback;
            return FractureClassStatus.Unknown;
        }

        public static bool TryResolve(string fractureClass, out MaterialType type) =>
            Resolve(fractureClass, out type) == FractureClassStatus.Mapped;

        static readonly HashSet<string> Warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly object WarnLock = new object();

        /// <summary>
        /// Resolve, and for an Unknown value call <paramref name="warn"/> once per distinct (trimmed, case-insensitive) value
        /// for the lifetime of the domain. Empty never warns (not authored). Warnings never change the result.
        /// </summary>
        public static FractureClassStatus ResolveWarnOnce(string fractureClass, out MaterialType type, Action<string> warn,
                                                          MaterialType fallback = DefaultFallback)
        {
            var status = Resolve(fractureClass, out type, fallback);
            if (status == FractureClassStatus.Unknown && warn != null)
            {
                string s = Normalize(fractureClass);
                bool first;
                lock (WarnLock) first = Warned.Add(s);
                if (first) warn($"[Physics] Unknown fractureClass '{s}' (expected {string.Join("/", Keys)}); using the fallback material.");
            }
            return status;
        }

        /// <summary>Tests only: forget which unknown values have already warned.</summary>
        public static void ResetWarningsForTests() { lock (WarnLock) Warned.Clear(); }
    }
}
