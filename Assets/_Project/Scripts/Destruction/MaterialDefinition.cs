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

        [Header("Greybox colour (Art arena_brief)")]
        public Color baseColor = new Color(0.788f, 0.541f, 0.294f); // wood #C98A4B
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
                    def.baseColor = Hex("C98A4B");
                    def.darkColor = Hex("8A5A34");
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
                    def.baseColor = Hex("B5523B");
                    def.darkColor = Hex("8E3F2E");
                    def.breakFxColor = Hex("D9A37E");
                    break;
                case MaterialKind.Steel:
                    def.name = "Steel_Runtime";
                    def.maxHp = 120f;
                    def.mass = 4.0f;
                    def.jointDamageMultiplier = 2.0f; // Contract §3
                    def.jointBreakForce = 2200f;
                    def.jointBreakTorque = 2200f;
                    def.maxDebrisChunks = 3;
                    def.baseColor = Hex("6B7684");
                    def.darkColor = Hex("3A3F48");
                    def.breakFxColor = Hex("FFD27A");
                    break;
            }
            return def;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
