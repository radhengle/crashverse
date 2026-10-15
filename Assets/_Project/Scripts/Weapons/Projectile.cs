using Crashverse.Destruction;
using UnityEngine;

namespace Crashverse.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public float damage = 35f;
        public float lifeSeconds = 6f;
        public float radius = 0.35f;

        Rigidbody _rb;
        float _life;
        bool _spent;

        public void Launch(Vector3 position, Vector3 velocity)
        {
            transform.position = position;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.velocity = velocity;
            _rb.angularVelocity = Vector3.zero;
            _life = lifeSeconds;
            _spent = false;
            gameObject.SetActive(true);
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) Despawn();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_spent) return;
            _spent = true;

            var block = collision.collider.GetComponentInParent<DestructibleBlock>();
            if (block != null)
            {
                bool asJoint = block.isSteelJoint ||
                               collision.collider.name.ToLowerInvariant().Contains("ring") ||
                               collision.contacts.Length > 0 && IsJointishContact(block, collision);
                block.ApplyDamage(damage, asJoint);
                // Splash small damage to neighbours
                var cols = Physics.OverlapSphere(transform.position, radius * 2.5f);
                foreach (var c in cols)
                {
                    var b = c.GetComponentInParent<DestructibleBlock>();
                    if (b != null && b != block && !b.IsBroken)
                        b.ApplyDamage(damage * 0.35f, b.isSteelJoint);
                }
            }
            Despawn();
        }

        bool IsJointishContact(DestructibleBlock block, Collision collision)
        {
            // Prefer joint damage if hitting steel material on a compact block
            return block.isSteelJoint;
        }

        void Despawn()
        {
            gameObject.SetActive(false);
            Destroy(gameObject, 0.05f);
        }
    }
}
