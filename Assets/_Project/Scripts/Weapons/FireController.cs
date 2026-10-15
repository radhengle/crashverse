using UnityEngine;

namespace Crashverse.Weapons
{
    /// <summary>Confirmed fire consumes one move. Aim/cancel does not.</summary>
    public class FireController : MonoBehaviour
    {
        public AimController aim;
        public GameObject projectilePrefab;
        public float projectileScale = 0.45f;

        public event System.Action Fired;

        public bool CanFire { get; set; } = true;

        public void Fire()
        {
            if (!CanFire || aim == null) return;
            GameObject go;
            if (projectilePrefab != null)
                go = Instantiate(projectilePrefab) as GameObject;
            else
                go = CreateDefaultProjectile();
            if (go == null) return;
            go.transform.localScale = Vector3.one * projectileScale;
            var proj = go.GetComponent<Projectile>();
            if (proj == null) proj = go.AddComponent<Projectile>();
            proj.Launch(aim.GetSpawnPosition(), aim.GetLaunchVelocity());
            aim.CancelAim();
            Fired?.Invoke();
        }

        GameObject CreateDefaultProjectile()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.tag = "Projectile";
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.8f;
            rb.useGravity = true;
            var rend = go.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            Color cyan = new Color(0.133f, 0.827f, 0.933f);
            block.SetColor("_BaseColor", cyan);
            block.SetColor("_Color", cyan);
            rend.SetPropertyBlock(block);
            return go;
        }
    }
}
