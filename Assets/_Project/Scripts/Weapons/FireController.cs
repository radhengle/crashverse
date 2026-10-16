using Crashverse.Destruction;
using Crashverse.PowerUps;
using UnityEngine;

namespace Crashverse.Weapons
{
    /// <summary>A confirmed fire consumes one shot (GameSession). Aim/cancel never does.</summary>
    public class FireController : MonoBehaviour
    {
        public AimController aim;
        public float defaultDamage = 60f;
        public float projectileScale = 0.45f;
        public float projectileMass = 0.8f;
        public float splitSpreadDeg = 8f;
        public bool CanFire { get; set; } = true;
        public static int ActiveProjectiles => Projectile.Alive.Count;

        public event System.Action Fired;

        public bool Fire(ShotModifiers mods)
        {
            if (!CanFire || aim == null) return false;
            var go = CreateProjectileObject(projectileScale);
            go.GetComponent<Rigidbody>().mass = projectileMass;
            var p = go.GetComponent<Projectile>();
            p.splitSpreadDeg = splitSpreadDeg;
            p.Launch(aim.SpawnPosition, aim.LaunchVelocity, defaultDamage, mods);
            aim.CancelAim();
            Fired?.Invoke();
            return true;
        }

        public static GameObject CreateProjectileObject(float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.tag = "Projectile";
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<Rigidbody>();
            go.AddComponent<Projectile>();
            var r = go.GetComponent<Renderer>();
            var b = new MaterialPropertyBlock();
            var c = MaterialDefinition.Hex(WreckerSpec.Core);
            b.SetColor("_BaseColor", c); b.SetColor("_Color", c);
            r.SetPropertyBlock(b);
            return go;
        }
    }
}
