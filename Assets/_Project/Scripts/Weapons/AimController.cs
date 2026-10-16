using UnityEngine;
using UnityEngine.EventSystems;

namespace Crashverse.Weapons
{
    /// <summary>
    /// Touch drag to aim (mouse fallback). Elevation 5–70°, yaw ±60° (Wrecker brief, unchanged in v2).
    /// QA-UI-003: a press that starts over any UI element (EventSystem raycast) never starts an aim drag.
    /// Fixed shot power, no charge (OQ-A1). Releasing a drag never fires; FIRE is a button tap (OQ-A2). Aim stays live during reload.
    /// Aim/cancel is free (Contract §2). Draws the trajectory arc and, when a hint is armed, a ghost arc (Addendum §1).
    /// Animation Spec 01: the visible barrel follows the TARGET aim through a critically damped spring (Feel.AimSpring, unscaled time);
    /// the arc, LaunchVelocity and SpawnPosition are computed from the target angles, so smoothing never changes where a shot goes.
    /// </summary>
    public class AimController : MonoBehaviour
    {
        public Transform yawPivot, pitchPivot, projectileSpawn;
        public float yawDegPerDp = 0.25f, pitchDegPerDp = 0.15f; // OQ-A3 (data: wrecker.json aimYawDegPerDp/aimPitchDegPerDp)
        public float launchSpeed = 28f;
        public bool inputEnabled = true;

        float _yaw, _elevation = WreckerSpec.RestElevation;
        // Visible (smoothed) pose + spring velocities; visualPitchOffsetDeg is additive cosmetic pitch (idle breath, recoil kick, ready dip).
        float _visYaw, _visPitch = WreckerSpec.RestElevation, _velYaw, _velPitch, _snapLeft;
        Vector3 _pitchRestLocal;
        bool _clampedLast;
        public float visualPitchOffsetDeg;
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
            _pitchRestLocal = pitch != null ? pitch.localPosition : Vector3.zero;
            _arc = MakeLine("TrajectoryArc", new Color(1f, 1f, 1f, 0.9f), 0.12f);
            _ghost = MakeLine("HintGhostArc", new Color(0.13f, 0.83f, 0.93f, 0.45f), 0.2f);
            _ghost.enabled = false;
            SnapVisualNow();
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

        /// <summary>Sets the TARGET aim (clamped: yaw ±60°, elevation 5–70°). The visible barrel catches up in LateUpdate.</summary>
        public void SetAim(float yaw, float elevation)
        {
            _yaw = Mathf.Clamp(yaw, -WreckerSpec.MaxYaw, WreckerSpec.MaxYaw);
            _elevation = Mathf.Clamp(elevation, WreckerSpec.MinElevation, WreckerSpec.MaxElevation);
        }

        /// <summary>Visible barrel jumps to the target (Bind, Retry: deterministic reset, no catch-up).</summary>
        public void SnapVisualNow() { _visYaw = _yaw; _visPitch = _elevation; _velYaw = _velPitch = 0f; _snapLeft = 0f; Apply(); }

        /// <summary>Closes the remaining visual gap linearly over <paramref name="seconds"/> (FIRE: inside the 66 ms anticipation, 01).</summary>
        public void SnapVisual(float seconds) { if (seconds <= 0f) SnapVisualNow(); else _snapLeft = seconds; }

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

        /// <summary>Barrel rotation for the TARGET aim, relative to the yaw pivot's parent (same chain WreckerBuilder builds: yaw about Y, elevation about Z).</summary>
        Quaternion TargetLocalRotation => Quaternion.Euler(0f, -_yaw, 0f) * Quaternion.Euler(0f, 0f, _elevation);

        public Vector3 LaunchVelocity
        {
            get
            {
                if (projectileSpawn == null || yawPivot == null) return (projectileSpawn != null ? projectileSpawn.right : Vector3.right) * launchSpeed;
                var dirLocal = TargetLocalRotation * Vector3.right;
                var par = yawPivot.parent;
                return (par != null ? par.TransformDirection(dirLocal) : dirLocal) * launchSpeed;
            }
        }

        public Vector3 SpawnPosition
        {
            get
            {
                if (projectileSpawn == null) return transform.position + Vector3.up * 1.5f;
                if (yawPivot == null) return projectileSpawn.position;
                var local = yawPivot.localPosition + Quaternion.Euler(0f, -_yaw, 0f) * (_pitchRestLocal + Quaternion.Euler(0f, 0f, _elevation) * projectileSpawn.localPosition);
                var par = yawPivot.parent;
                return par != null ? par.TransformPoint(local) : local;
            }
        }

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
            float wantYaw = _yaw + dp.x * yawDegPerDp, wantEl = _elevation + dp.y * pitchDegPerDp;
            SetAim(wantYaw, wantEl);
            bool clamped = wantYaw != _yaw || wantEl != _elevation;
            if (clamped && !_clampedLast) Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.WreckerAimClamp); // once per contact
            _clampedLast = clamped;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime; // aim stays responsive under hit stop / slow-mo / Time Fracture (01)
            if (_snapLeft > 0f)
            {
                float k = Mathf.Min(1f, dt / _snapLeft);
                _visYaw += (_yaw - _visYaw) * k; _visPitch += (_elevation - _visPitch) * k; _velYaw = _velPitch = 0f;
                _snapLeft -= dt;
            }
            else
            {
                float w = Crashverse.Feel.AimSpring.Omega(Crashverse.Perf.QualityController.FeelTier);
                Crashverse.Feel.AimSpring.Step(ref _visYaw, ref _velYaw, _yaw, w, dt);
                Crashverse.Feel.AimSpring.Step(ref _visPitch, ref _velPitch, _elevation, w, dt);
            }
            Apply();
        }

        /// <summary>Applies the VISIBLE pose. Cosmetic pitch offset never pushes the barrel past the 5–70° limits.</summary>
        void Apply()
        {
            if (yawPivot != null) yawPivot.localRotation = Quaternion.Euler(0f, -_visYaw, 0f);
            if (pitchPivot != null)
                pitchPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(_visPitch + visualPitchOffsetDeg, WreckerSpec.MinElevation, WreckerSpec.MaxElevation));
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
