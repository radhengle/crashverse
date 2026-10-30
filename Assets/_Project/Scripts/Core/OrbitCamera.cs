using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// Gameplay camera. Runs use the LOCKED behind-the-cannon pose (user 2026-10-30, Target UI §5): the camera sits behind the cannon on
    /// the firing line, the cannon is big and fully visible at the bottom centre and the building fills the view above it
    /// (CannonCameraRules.Solve, re-solved when the screen aspect changes). No orbit while locked. The old orbit camera (Art v2:
    /// yaw 72°, Wrecker bottom-left, two-finger orbit) is kept for Frame() callers that don't lock.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 lookAt;
        public float distance = 22f, yaw = 72f, pitch = 15f;
        public float minPitch = 8f, maxPitch = 45f, minYaw = 30f, maxYaw = 120f;
        Vector2 _lastMid; bool _twoFinger;
        /// <summary>Locked behind-the-cannon framing (no orbit). Set by FrameBehindCannon.</summary>
        public bool locked;
        public Crashverse.Meta.CannonCameraPose Pose { get; private set; }
        Vector3 _cannonRoot; Bounds _structure; float _aspect = -1f; Camera _cam;

        /// <summary>Additive feel offsets (shake, follow-shot) set by FeelDirector each frame; zero when Reduce motion/shake.</summary>
        public Vector3 feelOffset; public float feelRollDeg; public Vector3 followTarget; public float followWeight;

        public void Frame(Vector3 wrecker, Vector3 target)
        {
            locked = false;
            var cc = CameraConfig.Current; // PM: camera angles/FOV/offsets are data
            distance = cc.distance; yaw = cc.yaw; pitch = cc.pitch;
            minPitch = cc.minPitch; maxPitch = cc.maxPitch; minYaw = cc.minYaw; maxYaw = cc.maxYaw;
            lookAt = Vector3.Lerp(wrecker, target, cc.lookLerpWreckerToTarget) + cc.lookOffset;
            var cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = cc.fov;
        }

        /// <summary>Locks the camera behind the cannon, framing the cannon (bottom centre) and the whole structure. Re-solves when the
        /// screen aspect changes (rotation is locked to portrait, but split-screen / foldables can still resize).</summary>
        public void FrameBehindCannon(Vector3 cannonRoot, Bounds structure)
        {
            locked = true; _cannonRoot = cannonRoot; _structure = structure; _aspect = -1f; _cam = GetComponent<Camera>();
            lookAt = structure.center; Solve();
        }

        void Solve()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            float aspect = _cam != null ? _cam.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);
            _aspect = aspect;
            Pose = Crashverse.Meta.CannonCameraRules.Solve(_cannonRoot, _structure, aspect);
            if (_cam != null) _cam.fieldOfView = Pose.fov;
            Apply();
        }

        void Apply()
        {
            if (!locked) return;
            var rot = Pose.rotation;
            if (followWeight > 0f) // FeelDirector follow-shot: a gentle look toward the projectile, never a re-frame
                rot = Quaternion.Slerp(rot, Quaternion.LookRotation((followTarget - Pose.position).sqrMagnitude > 1e-4f ? followTarget - Pose.position : Pose.Forward, Vector3.up), followWeight * 0.35f);
            transform.SetPositionAndRotation(Pose.position + feelOffset, rot);
            if (feelRollDeg != 0f) transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, feelRollDeg);
        }

        bool _rmb; Vector2 _lastMouse;

        void LateUpdate()
        {
            if (locked)
            {
                float a = _cam != null ? _cam.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);
                if (Mathf.Abs(a - _aspect) > 0.001f) Solve(); else Apply();
                return; // angle locked: two-finger / right-drag orbit disabled
            }
            if (Input.touchCount == 2)
            {
                Vector2 mid = (Input.GetTouch(0).position + Input.GetTouch(1).position) * 0.5f;
                if (_twoFinger) Orbit((mid - _lastMid) * CameraConfig.Current.orbitDegPerPx);
                _lastMid = mid; _twoFinger = true;
            }
            else _twoFinger = false;
            // Editor right-drag orbit from the mouse position delta: no InputManager axes needed (BUG-20).
            Vector2 mp = Input.mousePosition;
            if (Input.GetMouseButton(1) && _rmb) Orbit((mp - _lastMouse) * CameraConfig.Current.orbitDegPerPx);
            _rmb = Input.GetMouseButton(1); _lastMouse = mp;
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
