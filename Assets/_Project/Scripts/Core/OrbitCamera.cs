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

        /// <summary>Additive feel offsets (shake, follow-shot) set by FeelDirector each frame; zero when Reduce motion/shake.</summary>
        public Vector3 feelOffset; public float feelRollDeg; public Vector3 followTarget; public float followWeight;

        public void Frame(Vector3 wrecker, Vector3 target)
        {
            var cc = CameraConfig.Current; // PM: camera angles/FOV/offsets are data
            distance = cc.distance; yaw = cc.yaw; pitch = cc.pitch;
            minPitch = cc.minPitch; maxPitch = cc.maxPitch; minYaw = cc.minYaw; maxYaw = cc.maxYaw;
            lookAt = Vector3.Lerp(wrecker, target, cc.lookLerpWreckerToTarget) + cc.lookOffset;
            var cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = cc.fov;
        }

        void LateUpdate()
        {
            if (Input.touchCount == 2)
            {
                Vector2 mid = (Input.GetTouch(0).position + Input.GetTouch(1).position) * 0.5f;
                if (_twoFinger) Orbit((mid - _lastMid) * CameraConfig.Current.orbitDegPerPx);
                _lastMid = mid; _twoFinger = true;
            }
            else _twoFinger = false;
            if (Input.GetMouseButton(1)) Orbit(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var look = followWeight > 0f ? Vector3.Lerp(lookAt, followTarget, followWeight) : lookAt;
            transform.position = look + rot * new Vector3(0f, 0f, -distance) + feelOffset;
            transform.LookAt(look);
            if (feelRollDeg != 0f) transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, feelRollDeg);
        }

        void Orbit(Vector2 d)
        {
            yaw = Mathf.Clamp(yaw + d.x, minYaw, maxYaw);
            pitch = Mathf.Clamp(pitch - d.y, minPitch, maxPitch);
        }
    }
}
