using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// Gameplay camera (Art v2): starts low behind the Wrecker, ~15° pitch, FOV 50, looking toward the target so the
    /// Wrecker sits bottom-left. Two-finger drag (or right mouse) orbits within limits; never consumes shots.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 lookAt;
        public float distance = 22f, yaw = 72f, pitch = 15f;
        public float minPitch = 8f, maxPitch = 45f, minYaw = 30f, maxYaw = 120f;
        Vector2 _lastMid; bool _twoFinger;

        public void Frame(Vector3 wrecker, Vector3 target)
        {
            lookAt = Vector3.Lerp(wrecker, target, 0.45f) + new Vector3(0f, 4f, 2.5f);
            var cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = 50f;
        }

        void LateUpdate()
        {
            if (Input.touchCount == 2)
            {
                Vector2 mid = (Input.GetTouch(0).position + Input.GetTouch(1).position) * 0.5f;
                if (_twoFinger) Orbit((mid - _lastMid) * 0.15f);
                _lastMid = mid; _twoFinger = true;
            }
            else _twoFinger = false;
            if (Input.GetMouseButton(1)) Orbit(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = lookAt + rot * new Vector3(0f, 0f, -distance);
            transform.LookAt(lookAt);
        }

        void Orbit(Vector2 d)
        {
            yaw = Mathf.Clamp(yaw + d.x, minYaw, maxYaw);
            pitch = Mathf.Clamp(pitch - d.y, minPitch, maxPitch);
        }
    }
}
