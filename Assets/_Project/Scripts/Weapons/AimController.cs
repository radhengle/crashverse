using UnityEngine;

namespace Crashverse.Weapons
{
    /// <summary>
    /// Touch-drag aim (mouse fallback). Barrel elevation 5–70°, yaw ±60° (wrecker_brief).
    /// Aim / cancel is free — does not consume shots (Contract §2).
    /// </summary>
    public class AimController : MonoBehaviour
    {
        public Transform yawPivot;
        public Transform pitchPivot;
        public Transform projectileSpawn;

        public float minElevation = 5f;
        public float maxElevation = 70f;
        public float minYaw = -60f;
        public float maxYaw = 60f;
        public float sensitivity = 0.15f;
        public float launchSpeed = 28f;

        float _yaw;
        float _elevation = 25f;
        bool _dragging;
        Vector3 _lastPos;
        bool _aiming;

        public bool IsAiming => _aiming;
        public float Yaw => _yaw;
        public float Elevation => _elevation;

        public void Bind(Transform yaw, Transform pitch, Transform spawn)
        {
            yawPivot = yaw;
            pitchPivot = pitch;
            projectileSpawn = spawn;
            ApplyRotations();
        }

        public void BeginAim()
        {
            _aiming = true;
        }

        public void CancelAim()
        {
            _aiming = false;
            _dragging = false;
            // Free — no shot consumed
        }

        public Vector3 GetLaunchVelocity()
        {
            if (projectileSpawn == null) return Vector3.forward * launchSpeed;
            return projectileSpawn.right.normalized * launchSpeed; // barrel along +X of pitch pivot
        }

        public Vector3 GetSpawnPosition()
        {
            return projectileSpawn != null ? projectileSpawn.position : transform.position + Vector3.up;
        }

        void Update()
        {
            // Mouse fallback (editor) + touch
            if (Input.GetMouseButtonDown(0) && !IsPointerOverFireUi())
            {
                _dragging = true;
                _aiming = true;
                _lastPos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
            }

            if (_dragging && Input.GetMouseButton(0))
            {
                Vector3 delta = Input.mousePosition - _lastPos;
                _lastPos = Input.mousePosition;
                _yaw += delta.x * sensitivity;
                _elevation += delta.y * sensitivity;
                _yaw = Mathf.Clamp(_yaw, minYaw, maxYaw);
                _elevation = Mathf.Clamp(_elevation, minElevation, maxElevation);
                ApplyRotations();
            }
        }

        void ApplyRotations()
        {
            if (yawPivot != null)
                yawPivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            if (pitchPivot != null)
                // Elevation around Z so barrel (+X) lifts up
                pitchPivot.localRotation = Quaternion.Euler(0f, 0f, _elevation);
        }

        bool IsPointerOverFireUi()
        {
            // Simple screen-rect check: fire button lower-right
            var p = Input.mousePosition;
            return p.x > Screen.width * 0.72f && p.y < Screen.height * 0.28f;
        }

        void OnDrawGizmos()
        {
            if (projectileSpawn == null) return;
            Gizmos.color = Color.cyan;
            Vector3 p = projectileSpawn.position;
            Vector3 v = GetLaunchVelocity();
            Vector3 prev = p;
            float dt = 0.05f;
            Vector3 vel = v;
            for (int i = 0; i < 40; i++)
            {
                vel += Physics.gravity * dt;
                p += vel * dt;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
