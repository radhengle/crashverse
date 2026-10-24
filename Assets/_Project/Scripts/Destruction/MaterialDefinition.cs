using UnityEngine;

namespace Crashverse.Destruction
{
    public enum MaterialKind
    {
        Wood = 0,
        Brick = 1,
        Steel = 2,
        Glass = 3, // Neon District (chapter 3) and later only — Game designer ruling (Physics_Feel_Review_v1 (d))
        Stone = 4  // append only: values are serialized in assets
    }

    /// <summary>
    /// Tunable material data. M1 values come from Level Design Contract v0.1 §3.
    /// Do not hardcode HP/mass in gameplay logic — always read from this asset / runtime defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "Material_", menuName = "Crashverse/Material Definition", order = 10)]
    public class MaterialDefinition : ScriptableObject
    {
        public MaterialKind kind = MaterialKind.Wood;

        [Header("Combat (Contract §3)")]
        public float maxHp = 30f;
        public float mass = 1f;
        [Tooltip("Multiplier applied when damage hits a steel joint (Contract: steel joints 2.0×).")]
        public float jointDamageMultiplier = 1f;

        [Header("Physical (starting tunables; data only — not yet applied to colliders)")]
        [Tooltip("Reference density kg/m³ (content/mass derivation). mass above stays the authoritative per-block mass.")]
        public float densityKgM3 = 600f;
        [Range(0f, 1f)] public float restitution = 0.1f;
        [Range(0f, 1.5f)] public float friction = 0.6f;

        [Header("Joints")]
        public float jointBreakForce = 800f;
        public float jointBreakTorque = 800f;

        [Header("Debris")]
        public int maxDebrisChunks = 6;
        public float debrisLifetimeSeconds = 2.5f;
        /// <summary>Ruling #7a: multiplier on debris damage this material TAKES, in permille (1000 = 1.0). Wood 1.0, brick 1.1, steel 0.8.</summary>
        public int debrisDamageTakenPermille = 1000;

        [Header("Greybox colour (Art arena_brief)")]
        public Color baseColor = new Color(0.890f, 0.655f, 0.400f); // wood #E3A766 (Art v2)
        public Color darkColor = new Color(0.541f, 0.353f, 0.204f);
        public Color breakFxColor = new Color(0.890f, 0.690f, 0.478f);

        public static MaterialDefinition CreateRuntime(MaterialKind kind)
        {
            var def = CreateInstance<MaterialDefinition>();
            def.kind = kind;
            switch (kind)
            {
                case MaterialKind.Wood:
                    def.name = "Wood_Runtime";
                    def.maxHp = 30f;
                    def.mass = 1.0f;
                    def.densityKgM3 = 600f; def.restitution = 0.1f; def.friction = 0.6f;
                    def.jointDamageMultiplier = 1f;
                    def.jointBreakForce = 450f;
                    def.jointBreakTorque = 450f;
                    def.maxDebrisChunks = 6;
                    def.debrisDamageTakenPermille = 1000;
                    def.baseColor = Hex(Crashverse.Core.VisualConfig.Current.woodHex);
                    def.darkColor = Hex("A8743F");
                    def.breakFxColor = Hex("E3B07A");
                    break;
                case MaterialKind.Brick:
                    def.name = "Brick_Runtime";
                    def.maxHp = 60f;
                    def.mass = 2.5f;
                    def.densityKgM3 = 1900f; def.restitution = 0.05f; def.friction = 0.7f;
                    def.jointDamageMultiplier = 1f;
                    def.jointBreakForce = 900f;
                    def.jointBreakTorque = 900f;
                    def.maxDebrisChunks = 4;
                    def.debrisDamageTakenPermille = 1100;
                    def.baseColor = Hex(Crashverse.Core.VisualConfig.Current.brickHex);
                    def.darkColor = Hex("7A3326");
                    def.breakFxColor = Hex("D9A37E");
                    break;
                case MaterialKind.Steel:
                    def.name = "Steel_Runtime";
                    def.maxHp = 120f;
                    def.mass = 4.0f;
                    def.densityKgM3 = 7850f; def.restitution = 0.15f; def.friction = 0.5f;
                    def.jointDamageMultiplier = 2.0f; // Contract §3
                    def.jointBreakForce = 2200f;
                    def.jointBreakTorque = 2200f;
                    def.debrisDamageTakenPermille = 800;
                    def.maxDebrisChunks = 3;
                    def.baseColor = Hex(Crashverse.Core.VisualConfig.Current.steelHex);
                    def.darkColor = Hex("6B7684");
                    def.breakFxColor = Hex("FFD27A");
                    break;
                // Glass and Stone: every value below is a STARTING TUNABLE (Game designer / Level Designer to tune).
                case MaterialKind.Glass:
                    def.name = "Glass_Runtime";
                    def.maxHp = 12f;             // brittle: low HP
                    def.mass = 1.5f;
                    def.densityKgM3 = 2500f;
                    def.restitution = 0.05f; def.friction = 0.4f;
                    def.jointDamageMultiplier = 1f;
                    def.jointBreakForce = 250f;  // snaps easily
                    def.jointBreakTorque = 200f;
                    def.maxDebrisChunks = 10;    // shatters into many small shards
                    def.debrisLifetimeSeconds = 1.5f;
                    def.debrisDamageTakenPermille = Crashverse.Sim.DebrisMaterialMult.Glass; // ×1.5
                    def.baseColor = Hex("A8E6F0"); def.darkColor = Hex("5FA8B8"); def.breakFxColor = Hex("E6FBFF");
                    break;
                case MaterialKind.Stone:
                    def.name = "Stone_Runtime";
                    def.maxHp = 90f;             // heavy and tough
                    def.mass = 3.3f;             // ≈ brick 2.5 × (2500 / 1900 kg/m³)
                    def.densityKgM3 = 2500f;
                    def.restitution = 0.05f; def.friction = 0.8f;
                    def.jointDamageMultiplier = 1f;
                    def.jointBreakForce = 1500f;
                    def.jointBreakTorque = 1300f;
                    def.maxDebrisChunks = 5;     // crumbles into a few chunks
                    def.debrisLifetimeSeconds = 3f;
                    def.debrisDamageTakenPermille = Crashverse.Sim.DebrisMaterialMult.Stone; // ×0.9
                    def.baseColor = Hex("9A958C"); def.darkColor = Hex("5E5A54"); def.breakFxColor = Hex("C9C2B6");
                    break;
            }
            return def;
        }

        public float smoothness = 0.25f;

        public static MaterialKind? Parse(string s)
        {
            switch (s) { case "wood": return MaterialKind.Wood; case "brick": return MaterialKind.Brick; case "steel": return MaterialKind.Steel;
                       case "glass": return MaterialKind.Glass; case "stone": return MaterialKind.Stone; }
            return null;
        }

        /// <summary>Physics material family (Crashverse.Sim.MaterialType) to MaterialKind. Terrain has no kind: <paramref name="fallback"/>.</summary>
        public static MaterialKind KindOf(Crashverse.Sim.MaterialType t, MaterialKind fallback = MaterialKind.Wood) => t switch
        {
            Crashverse.Sim.MaterialType.Wood => MaterialKind.Wood, Crashverse.Sim.MaterialType.Brick => MaterialKind.Brick,
            Crashverse.Sim.MaterialType.Steel => MaterialKind.Steel, Crashverse.Sim.MaterialType.Glass => MaterialKind.Glass,
            Crashverse.Sim.MaterialType.Stone => MaterialKind.Stone, _ => fallback
        };

        /// <summary>
        /// ImportedPhysicsHints.fractureClass (hero buildings) to MaterialKind via Crashverse.Sim.FractureClassRules:
        /// trimmed, case-insensitive, ordinal (no culture). Wood/Brick/Steel/Glass/Stone map to themselves; Roof (terracotta
        /// tile) maps to Stone (physics/FractureClass_Mapping.md). Empty = not authored: <paramref name="fallback"/>, silent.
        /// Unknown: <paramref name="fallback"/> plus one Debug.LogWarning per distinct value.
        /// </summary>
        public static MaterialKind FromFractureClass(string fractureClass, MaterialKind fallback = MaterialKind.Wood)
        {
            var status = Crashverse.Sim.FractureClassRules.ResolveWarnOnce(fractureClass, out var t, m => Debug.LogWarning($"{m} MaterialKind fallback: {fallback}."));
            return status == Crashverse.Sim.FractureClassStatus.Mapped ? KindOf(t, fallback) : fallback;
        }

        /// <summary>Lower-case content key ("wood", "glass", ...) used by level JSON and DebrisBudget.</summary>
        public static string KeyOf(MaterialKind k) => k switch
        {
            MaterialKind.Brick => "brick", MaterialKind.Steel => "steel", MaterialKind.Glass => "glass", MaterialKind.Stone => "stone", _ => "wood"
        };

        /// <summary>Glass is Neon District (chapter 3) and later only.</summary>
        public static bool AllowedInChapter(MaterialKind k, int chapter) => chapter >= Crashverse.Sim.MaterialRules.MinChapter(KeyOf(k));

        /// <summary>Runtime material with data-driven rule modifiers (Titan/event rules). 0 = unchanged.</summary>
        public static MaterialDefinition CreateRuntime(MaterialKind kind, Crashverse.Data.RuleModifiers mod)
        {
            var d = CreateRuntime(kind);
            d.smoothness = kind == MaterialKind.Glass ? 0.9f : kind == MaterialKind.Steel ? 0.7f : 0.5f;
            if (mod == null) return d;
            switch (kind)
            {
                case MaterialKind.Wood: d.maxHp *= Crashverse.Data.RuleModifiers.M(mod.woodHpMult); break;
                case MaterialKind.Brick: d.maxHp *= Crashverse.Data.RuleModifiers.M(mod.brickHpMult); break;
                case MaterialKind.Steel:
                    d.maxHp *= Crashverse.Data.RuleModifiers.M(mod.steelHpMult);
                    if (mod.steelJointMult > 0) d.jointDamageMultiplier = mod.steelJointMult;
                    break;
            }
            return d;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
