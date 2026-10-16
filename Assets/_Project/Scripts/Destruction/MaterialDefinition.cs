using UnityEngine;

namespace Crashverse.Destruction
{
    public enum MaterialKind
    {
        Wood = 0,
        Brick = 1,
        Steel = 2
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
                    def.jointDamageMultiplier = 2.0f; // Contract §3
                    def.jointBreakForce = 2200f;
                    def.jointBreakTorque = 2200f;
                    def.debrisDamageTakenPermille = 800;
                    def.maxDebrisChunks = 3;
                    def.baseColor = Hex(Crashverse.Core.VisualConfig.Current.steelHex);
                    def.darkColor = Hex("6B7684");
                    def.breakFxColor = Hex("FFD27A");
                    break;
            }
            return def;
        }

        public float smoothness = 0.25f;

        public static MaterialKind? Parse(string s)
        {
            switch (s) { case "wood": return MaterialKind.Wood; case "brick": return MaterialKind.Brick; case "steel": return MaterialKind.Steel; }
            return null;
        }

        /// <summary>Runtime material with data-driven rule modifiers (Titan/event rules). 0 = unchanged.</summary>
        public static MaterialDefinition CreateRuntime(MaterialKind kind, Crashverse.Data.RuleModifiers mod)
        {
            var d = CreateRuntime(kind);
            d.smoothness = kind == MaterialKind.Steel ? 0.7f : 0.5f;
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
