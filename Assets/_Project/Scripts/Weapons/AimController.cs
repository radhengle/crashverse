using UnityEngine;
using UnityEngine.EventSystems;

namespace Crashverse.Weapons
{
    /// <summary>
    /// Touch drag to aim (mouse fallback). Elevation 5–70°, yaw ±60° (Wrecker brief, unchanged in v2).
    /// QA-UI-003: a press that starts over any UI element (EventSystem raycast) never starts an aim drag.
    /// Fixed shot power, no charge (OQ-A1). Releasing a drag never fires; FIRE is a button tap (OQ-A2). Aim stays live during reload.
    /// Aim/cancel is free (Contract §2). Draws the trajectory arc and, when a hint is armed, a ghost arc (Addendum §1).
    /// </summary>
    public class AimController : MonoBehaviour
    {
        public Transform yawPivot, pitchPivot, projectileSpawn;
        public float yawDegPerDp = 0.25f, pitchDegPerDp = 0.15f; // OQ-A3 (data: wrecker.json aimYawDegPerDp/aimPitchDegPerDp)
        public float launchSpeed = 28f;
        public bool inputEnabled = true;

        float _yaw, _elevation = WreckerSpec.RestElevation;
        bool _dragging;
        int _fingerId = -1;
        Vector2 _last;
        LineRenderer _arc, _ghost;

        public bool Dragging => _dragging;
        public float Yaw => _yaw;
        public float Elevation => _elevation;

        public void Bind(Transform yaw, Transform pitch, Transform spawn)
        {
            yawPivot = yaw; pitchPivot = pitch; projectileSpawn = spawn;
            _arc = MakeLine("TrajectoryArc", new Color(1f, 1f, 1f, 0.9f), 0.12f);
            _ghost = MakeLine("HintGhostArc", new Color(0.13f, 0.83f, 0.93f, 0.45f), 0.2f);
            _ghost.enabled = false;
            Apply();
        }

        LineRenderer MakeLine(string n, Color c, float w)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 0; lr.startWidth = w; lr.endWidth = w * 0.5f; lr.startColor = c; lr.endColor = c;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            return lr;
        }

        public void SetAim(float yaw, float elevation)
        {
            _yaw = Mathf.Clamp(yaw, -WreckerSpec.MaxYaw, WreckerSpec.MaxYaw);
            _elevation = Mathf.Clamp(elevation, WreckerSpec.MinElevation, WreckerSpec.MaxElevation);
            Apply();
        }

        /// <summary>Shows a ghost arc for the suggested shot until the first shot (Addendum §1).</summary>
        public void ShowGhost(float yaw, float elevation)
        {
            float y = _yaw, e = _elevation;
            SetAim(yaw, elevation);
            DrawArc(_ghost);
            _ghost.enabled = true;
            SetAim(y, e);
        }
        public void HideGhost() { if (_ghost != null) _ghost.enabled = false; }

        public void CancelAim() { _dragging = false; _fingerId = -1; }

        public Vector3 LaunchVelocity => (projectileSpawn != null ? projectileSpawn.right : Vector3.right) * launchSpeed;
        public Vector3 SpawnPosition => projectileSpawn != null ? projectileSpawn.position : transform.position + Vector3.up * 1.5f;

        static float DpPerPixel => 160f / (Screen.dpi > 1f ? Screen.dpi : 160f);

        void Update()
        {
            if (!inputEnabled) { _dragging = false; DrawArc(_arc); return; }
            if (Input.touchCount > 0) HandleTouch();
            else HandleMouse();
            DrawArc(_arc);
        }

        void HandleTouch()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && !_dragging && Input.touchCount == 1)
                {
                    // QA-UI-003: ignore presses that start on UI
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) continue;
                    _dragging = true; _fingerId = t.fingerId; _last = t.position;
                }
                else if (t.fingerId == _fingerId)
                {
                    if (t.phase == TouchPhase.Moved) { Drag(t.position - _last); _last = t.position; }
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) CancelAim();
                }
            }
            if (Input.touchCount > 1) CancelAim(); // two fingers = camera orbit
        }

        void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                // QA-UI-003: EventSystem pointer-over-UI check replaces the old screen-percentage exclusion zone
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                _dragging = true; _last = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0)) CancelAim();
            if (_dragging && Input.GetMouseButton(0))
            {
                Vector2 p = Input.mousePosition;
                Drag(p - _last); _last = p;
            }
        }

        void Drag(Vector2 deltaPx)
        {
            var dp = deltaPx * DpPerPixel;
            SetAim(_yaw + dp.x * yawDegPerDp, _elevation + dp.y * pitchDegPerDp);
        }

        void Apply()
        {
            if (yawPivot != null) yawPivot.localRotation = Quaternion.Euler(0f, -_yaw, 0f);
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(0f, 0f, _elevation);
        }

        /// <summary>Seconds from launch to the first impact of the current aim (used by Splitstrike's 70% rule). 0 if none found.</summary>
        public float PredictedFlightTime { get; private set; }
        static readonly Vector3[] _pts = new Vector3[64];

        /// <summary>OQ-A3: arc up to the first impact point only (no bounces, nothing after impact).</summary>
        void DrawArc(LineRenderer lr)
        {
            if (lr == null || projectileSpawn == null) return;
            const int n = 64; const float dt = 0.05f;
            Vector3 p = SpawnPosition, v = LaunchVelocity; int count = 0; float t = 0f; PredictedFlightTime = 0f;
            _pts[count++] = p;
            for (int i = 1; i < n; i++)
            {
                var next = p + v * dt + Physics.gravity * (0.5f * dt * dt);
                var seg = next - p;
                if (Physics.Raycast(p, seg.normalized, out var hit, seg.magnitude) && !hit.collider.isTrigger)
                {
                    _pts[count++] = hit.point; PredictedFlightTime = t + dt * (hit.distance / Mathf.Max(1e-4f, seg.magnitude)); break;
                }
                v += Physics.gravity * dt; p = next; t += dt; _pts[count++] = p;
                if (p.y < -6f) { PredictedFlightTime = t; break; }
            }
            lr.positionCount = count;
            for (int i = 0; i < count; i++) lr.SetPosition(i, _pts[i]);
        }
    }
}
