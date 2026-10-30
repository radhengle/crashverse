using UnityEngine;

namespace Crashverse.Meta
{
    /// <summary>Locked behind-the-cannon pose: world position, rotation and vertical FOV (degrees).</summary>
    public struct CannonCameraPose
    {
        public Vector3 position; public Quaternion rotation; public float fov;
        public Vector3 Forward => rotation * Vector3.forward;
    }

    /// <summary>
    /// User (2026-10-30): "cannon doesn't come in picture clearly and angle also". Target UI §5: the camera sits BEHIND the cannon, the
    /// cannon is big and fully visible at the bottom centre, and the building fills the view above it. The angle is locked (no orbit).
    /// Pure maths so EditMode tests can check every Ch1 layout and the Daily Boss on several phone aspect ratios.
    /// Inputs: cannon root (ground point under the carriage), the cannon's own envelope behind/ahead of the root, the structure's
    /// world bounds and the screen aspect (width / height). The camera stays on the cannon→structure line (no yaw offset), so the
    /// cannon is horizontally centred and +yaw (world +z) stays screen-LEFT, as the mirrored-aim P0 fix and its test expect.
    /// </summary>
    public static class CannonCameraRules
    {
        [System.Serializable]
        public class Settings
        {
            public float back = 20f;         // metres behind the cannon root, along the firing line (PM QA 31 Oct: concept framing, muzzle ~40% up)
            public float height = 8.0f;      // camera height above the cannon's ground (looks down on the cannon so it no longer covers the target)
            public float cannonRear = 1.8f;  // the cannon envelope reaches this far behind its root (rear roller / carriage)
            public float cannonTop = 2.6f;   // barrel tip / booster top at rest elevation
            public float topMargin = 0.06f;  // minimum share of the screen height above the structure's top (the HUD band, see HudTopMargin)
            public float bottomMargin = 0.03f; // share left under the cannon's wheels
            public float sideFill = 0.92f;   // the structure's width may use up to this share of the screen width
            public float minFov = 24f, maxFov = 68f;
        }

        public static readonly Settings Default = new Settings();

        /// <summary>HUD band at the top of the screen (UI Developer: HUD bottom ≈ safe-area top inset + 104 dp; ~390 px on 1080x2340)
        /// plus an 8 dp breathing buffer (PM 2026-10-30), as a share of the screen height:
        /// max(0.06, (safeTopInsetPx + (104 + 8) dp · dpi/160) / screenHeightPx). dpi 0 → 160.</summary>
        public const float HudBottomDp = 104f, HudBufferDp = 8f;
        public static float HudTopMargin(float screenHeightPx, float safeTopInsetPx, float dpi)
        {
            if (screenHeightPx <= 0f) return Default.topMargin;
            float d = dpi > 0f ? dpi : 160f;
            return Mathf.Max(Default.topMargin, (Mathf.Max(0f, safeTopInsetPx) + (HudBottomDp + HudBufferDp) * d / 160f) / screenHeightPx);
        }

        /// <summary>Solve the locked pose. aspect = width / height (portrait phones ~0.42–0.56).</summary>
        public static CannonCameraPose Solve(Vector3 cannonRoot, Bounds structure, float aspect, Settings s = null) =>
            Solve(cannonRoot, Vector3.right, structure, aspect, s);

        /// <summary>Per-device solve: topMargin = the HUD band (HudTopMargin); extraAbove = points that must also stay below the HUD
        /// (the hint arc's apex and samples).</summary>
        public static CannonCameraPose Solve(Vector3 cannonRoot, Bounds structure, float aspect, float topMargin, Vector3[] extraAbove = null)
        {
            var d = Default;
            var s = new Settings { back = d.back, height = d.height, cannonRear = d.cannonRear, cannonTop = d.cannonTop, topMargin = Mathf.Clamp(topMargin, d.topMargin, 0.4f),
                                   bottomMargin = d.bottomMargin, sideFill = d.sideFill, minFov = d.minFov, maxFov = d.maxFov };
            return Solve(cannonRoot, Vector3.right, structure, aspect, s, extraAbove);
        }

        /// <summary>firingDir = the cannon's yaw-0 direction (Wrecker: world +x). The camera stays on that line even when a layout is
        /// off-centre, so the cannon is always centred and the swipe→yaw mapping never changes between levels.</summary>
        public static CannonCameraPose Solve(Vector3 cannonRoot, Vector3 firingDir, Bounds structure, float aspect, Settings s = null, Vector3[] extraAbove = null)
        {
            s ??= Default;
            aspect = Mathf.Clamp(aspect, 0.3f, 2.5f);
            firingDir.y = 0f;
            var dir = firingDir.sqrMagnitude > 1e-6f ? firingDir.normalized : Vector3.right;
            var side = Vector3.Cross(Vector3.up, dir); // horizontal, perpendicular to the firing line
            float ground = cannonRoot.y;
            var cam = cannonRoot - dir * s.back + Vector3.up * s.height;

            // structure extents in the camera's horizontal frame (along = depth, across = width)
            float nearAlong = float.MaxValue, farAlong = float.MinValue, halfW = 0f;
            var e = structure.extents; var c = structure.center;
            for (int i = 0; i < 8; i++)
            {
                var p = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                var d = p - cam; float along = Vector3.Dot(d, dir), across = Mathf.Abs(Vector3.Dot(d, side));
                nearAlong = Mathf.Min(nearAlong, along); farAlong = Mathf.Max(farAlong, along); halfW = Mathf.Max(halfW, across);
            }
            nearAlong = Mathf.Max(1f, nearAlong);

            // angles above (+) / below (-) the horizon, worst case over the structure's depth
            float top = structure.max.y, bottom = structure.min.y;
            float aTop = Mathf.Atan2(top - cam.y, nearAlong) * Mathf.Rad2Deg;
            if (extraAbove != null)
                foreach (var p in extraAbove)
                {
                    float along = Vector3.Dot(p - cam, dir); if (along < 1f) continue;
                    aTop = Mathf.Max(aTop, Mathf.Atan2(p.y - cam.y, along) * Mathf.Rad2Deg);
                }
            float aStructBottom = Mathf.Atan2(bottom - cam.y, farAlong) * Mathf.Rad2Deg;
            float aCannonLow = Mathf.Atan2(ground - cam.y, Mathf.Max(0.5f, s.back - s.cannonRear)) * Mathf.Rad2Deg;
            float hi = Mathf.Max(aTop, aStructBottom), lo = Mathf.Min(aCannonLow, aStructBottom);

            // vertical FOV so the whole cannon + the whole building fit with margins
            float span = hi - lo;
            float fovV = span / Mathf.Max(0.5f, 1f - s.topMargin - s.bottomMargin);
            // horizontal: the structure's width must fit (it may not be clipped at the sides)
            float needHalfH = Mathf.Atan2(halfW / Mathf.Max(0.1f, s.sideFill), nearAlong);
            float fovFromWidth = 2f * Mathf.Atan(Mathf.Tan(needHalfH) / aspect) * Mathf.Rad2Deg;
            fovV = Mathf.Clamp(Mathf.Max(fovV, fovFromWidth), s.minFov, s.maxFov);

            // pitch: put the cannon's lowest point bottomMargin above the bottom edge (bottom-anchored framing, like the mockup)
            float pitchDown = -(lo + fovV * (0.5f - s.bottomMargin)); // positive = looking down
            // never tilt so far up that the building's top leaves the frame
            float topEdge = -pitchDown + fovV * (0.5f - s.topMargin);   // the HUD band starts here
            if (topEdge < aTop) pitchDown -= aTop - topEdge;
            var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitchDown, 0f, 0f);
            return new CannonCameraPose { position = cam, rotation = rot, fov = fovV };
        }

        /// <summary>Viewport point (x, y in 0..1 when on screen; z = depth) of a world point for a pose — the same projection a Unity
        /// perspective camera uses, so tests can check framing without a Camera.</summary>
        public static Vector3 ToViewport(CannonCameraPose pose, float aspect, Vector3 world)
        {
            var local = Quaternion.Inverse(pose.rotation) * (world - pose.position);
            if (local.z <= 1e-4f) return new Vector3(-1f, -1f, local.z);
            float t = Mathf.Tan(pose.fov * 0.5f * Mathf.Deg2Rad);
            return new Vector3(0.5f + 0.5f * local.x / (local.z * t * aspect), 0.5f + 0.5f * local.y / (local.z * t), local.z);
        }

        /// <summary>True when all 8 corners of b are inside the viewport (with a tiny tolerance).</summary>
        public static bool FullyVisible(CannonCameraPose pose, float aspect, Bounds b, float tol = 0.005f)
        {
            for (int i = 0; i < 8; i++)
            {
                var p = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x, (i & 2) == 0 ? -b.extents.y : b.extents.y, (i & 4) == 0 ? -b.extents.z : b.extents.z);
                var v = ToViewport(pose, aspect, p);
                if (v.z <= 0f || v.x < -tol || v.x > 1f + tol || v.y < -tol || v.y > 1f + tol) return false;
            }
            return true;
        }

        /// <summary>Screen-space rect (viewport units) covered by b.</summary>
        public static Rect ViewportRect(CannonCameraPose pose, float aspect, Bounds b)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var p = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x, (i & 2) == 0 ? -b.extents.y : b.extents.y, (i & 4) == 0 ? -b.extents.z : b.extents.z);
                var v = ToViewport(pose, aspect, p); x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }
}
