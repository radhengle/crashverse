using UnityEngine;

namespace Crashverse.Weapons
{
    /// <summary>
    /// Procedural greybox Wrecker per wrecker_brief.md: L4 × W2 × H2.4.
    /// Core cyan #22D3EE, Module violet #8B5CF6, Booster orange #FF7A1A.
    /// Projectile spawn = booster centre + 0.2 forward.
    /// </summary>
    public class WreckerBuilder : MonoBehaviour
    {
        public Transform yawPivot;
        public Transform pitchPivot;
        public Transform muzzle;          // booster tip
        public Transform projectileSpawn; // booster + 0.2 forward

        public void Build()
        {
            // Clear children
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            // Root at ground centre of chassis
            // Chassis 3.6 × 1.8 × 0.6, top at y=1.0 → centre y=0.7
            var chassis = Prim("Chassis", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(3.6f, 0.6f, 1.8f), Hex("2B2D33"));

            // Wheels: 4 per side, Ø0.8, width 0.3, axle y=0.4
            float[] zx = { -1.2f, 1.2f };
            float[] zz = { -0.75f, 0.75f };
            foreach (var x in zx)
            foreach (var z in zz)
            {
                var w = Prim($"Wheel_{x}_{z}", PrimitiveType.Cylinder, new Vector3(x, 0.4f, z), new Vector3(0.8f, 0.15f, 0.8f), Hex("1B1B1F"));
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // Yaw pivot at chassis centre
            var yawGo = new GameObject("YawPivot");
            yawGo.transform.SetParent(transform, false);
            yawGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            yawPivot = yawGo.transform;

            // Turret trapezoid approx as box 1.6 × 1.4 × 0.7
            var turret = Prim("Turret", PrimitiveType.Cube, new Vector3(-0.2f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 1.4f), Hex("5B6472"));
            turret.transform.SetParent(yawPivot, false);

            // Pitch pivot = barrel pivot at core (y≈1.5 world → local under yaw ≈ 0.5)
            var pitchGo = new GameObject("PitchPivot");
            pitchGo.transform.SetParent(yawPivot, false);
            pitchGo.transform.localPosition = new Vector3(0.3f, 0.5f, 0f);
            pitchPivot = pitchGo.transform;
            pitchPivot.localRotation = Quaternion.Euler(0f, 0f, 25f); // rest elevation ~25°

            // Core sphere Ø0.9
            var core = Prim("Core", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.9f, Hex("22D3EE"));
            core.transform.SetParent(pitchPivot, false);

            // Barrel cylinder Ø0.4 length 2.6 — along +X of pitch pivot
            var barrel = Prim("Barrel", PrimitiveType.Cylinder, new Vector3(1.3f, 0f, 0f), new Vector3(0.4f, 1.3f, 0.4f), Hex("5B6472"));
            barrel.transform.SetParent(pitchPivot, false);
            barrel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // cylinder default is Y-up

            // Module pod 0.6×0.5×0.4 at 0.8 along barrel
            var module = Prim("Module", PrimitiveType.Cube, new Vector3(0.8f, 0.35f, 0.35f), new Vector3(0.6f, 0.4f, 0.5f), Hex("8B5CF6"));
            module.transform.SetParent(pitchPivot, false);

            // Booster ring at barrel tip (length 2.6 from pivot → x=2.6)
            var booster = Prim("Booster", PrimitiveType.Cylinder, new Vector3(2.6f, 0f, 0f), new Vector3(0.6f, 0.15f, 0.6f), Hex("FF7A1A"));
            booster.transform.SetParent(pitchPivot, false);
            booster.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            muzzle = booster.transform;

            // Projectile spawn = booster centre + 0.2 forward (along barrel +X)
            var spawnGo = new GameObject("ProjectileSpawn");
            spawnGo.transform.SetParent(pitchPivot, false);
            spawnGo.transform.localPosition = new Vector3(2.8f, 0f, 0f); // 2.6 + 0.2
            projectileSpawn = spawnGo.transform;
        }

        GameObject Prim(string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            // Remove collider on cosmetic parts except chassis (keep simple)
            if (name != "Chassis")
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            var rend = go.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            rend.SetPropertyBlock(block);
            return go;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
