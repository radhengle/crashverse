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
            public float height = 4.5f;      // PM QA 31 Oct (L01.jpg): slightly behind + above the cannon; PM QA: lower pitch so the barrel reads from behind, not from the top
            public float cannonRear = 1.8f;  // the cannon envelope reaches this far behind its root (rear roller / carriage)
            public float cannonTop = 2.6f;   // barrel tip / booster top at rest elevation
            public float topMargin = 0.06f;  // minimum share of the screen height above the structure's top (the HUD band, see HudTopMargin)
            public float bottomMargin = 0.03f; // share left under the cannon's wheels
            public float sideFill = 0.80f;   // the structure's width may use up to this share of the screen width
            public float minFov = 18f, maxFov = 68f; // 18: small huts may zoom in to fill ~70-80% of the width
            public float cannonScale = 1f;   // render-only scale of the Kingdom cannon (CannonDresser); cannonRear already includes it
        }

        public static readonly Settings Default = new Settings();
        /// <summary>Compact levels (target 12-20 m away on its own island, L01.jpg): closer, lower eye and a wider view so the
        /// building fills ~70% of the width with water visible on both sides.</summary>
        /// PM 17 Nov (b16 QA): the bottom crop hid the carriage behind the FIRE / HINT buttons (bottom ~11% of the screen). The whole
        /// cannon, wheels and side cheeks included, now sits ~10% above the bottom edge (margin 0.09: 4:3 tablets keep the barrel below the building), just over the button row.
        /// PM 17 Nov (b17 QA, L01.jpg): the barrel must dominate the foreground. Compact levels dress a 1.25x cannon (render-only: pivots,
        /// spawn and hints unchanged) and frame its envelope from a little further back, so it is ~56% of a phone's width (was ~46%).
        /// PM b18 QA (L01.jpg): building ABOVE the muzzle (building ~41-77% of the height, barrel tip ~46%, below the building's base),
        /// the wide L1 house ~60% of the width: a higher eye from further back with a narrower lens (~35 deg on 19.5:9).
        public static readonly Settings Compact = new Settings { back = 18f, height = 7.5f, sideFill = 0.70f, bottomMargin = 0.09f,
                                                                 cannonScale = 1.25f, cannonRear = 1.8f * 1.25f, cannonTop = 2.6f * 1.25f };
        /// <summary>Height (m) of the Kingdom roof-top flag pole above the highest block; the run camera keeps its tip in frame.</summary>
        public const float RoofFlagPole = 1.2f;

        public static Settings For(Vector3 cannonRoot, Bounds structure) =>
            structure.center.x - cannonRoot.x < ArenaLayoutRules.CompactBelow + 2f ? Compact : Default;

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
            Solve(cannonRoot, Vector3.right, structure, aspect, s ?? For(cannonRoot, structure));

        /// <summary>Per-device solve: topMargin = the HUD band (HudTopMargin); extraAbove = points that must also stay below the HUD
        /// (the hint arc's apex and samples).</summary>
        public static CannonCameraPose Solve(Vector3 cannonRoot, Bounds structure, float aspect, float topMargin, Vector3[] extraAbove = null)
        {
            var d = For(cannonRoot, structure);
            var s = new Settings { back = d.back, height = d.height, cannonRear = d.cannonRear, cannonTop = d.cannonTop, topMargin = Mathf.Clamp(topMargin, d.topMargin, 0.4f),
                                   bottomMargin = d.bottomMargin, sideFill = d.sideFill, minFov = d.minFov, maxFov = d.maxFov, cannonScale = d.cannonScale };
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
            // a concept-style bottom crop (bottomMargin < 0) never hides more than the cannon's lowest ~0.85 m (MustShow keeps the
            // rest on screen), however wide the view gets on tall layouts
            float bm = s.bottomMargin;
            if (bm < 0f)
            {
                float aCrop = Mathf.Atan2(ground + 0.85f - cam.y, Mathf.Max(0.5f, s.back - s.cannonRear)) * Mathf.Rad2Deg;
                bm = Mathf.Max(bm, -(aCrop - aCannonLow) / Mathf.Max(1f, fovV));
                fovV = Mathf.Clamp(Mathf.Max(fovV, span / Mathf.Max(0.5f, 1f - s.topMargin - bm)), s.minFov, s.maxFov);
            }
            float pitchDown = -(lo + fovV * (0.5f - bm)); // positive = looking down
            // never tilt so far up that the building's top leaves the frame
            float topEdge = -pitchDown + fovV * (0.5f - s.topMargin);   // the HUD band starts here
            if (topEdge < aTop) pitchDown -= aTop - topEdge;
            var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitchDown, 0f, 0f);
            // the angle maths above is linear in degrees while the projection is linear in tan: refine on the real corners so the
            // building top never pokes into the HUD band (a few 1e-4 of the screen on tall-and-wide layouts)
            float limit = 1f - s.topMargin;
            for (int it = 0; it < 6; it++)
            {
                var pose = new CannonCameraPose { position = cam, rotation = rot, fov = fovV };
                float yMax = float.MinValue;
                for (int ci = 0; ci < 8; ci++)
                {
                    var w = new Vector3((ci & 1) == 0 ? structure.min.x : structure.max.x, (ci & 2) == 0 ? structure.min.y : structure.max.y, (ci & 4) == 0 ? structure.min.z : structure.max.z);
                    yMax = Mathf.Max(yMax, ToViewport(pose, aspect, w).y);
                }
                if (yMax <= limit - 0.0005f) break;
                pitchDown -= (yMax - limit + 0.001f) * fovV;
                rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitchDown, 0f, 0f);
            }
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

        /// <summary>The part of the cannon envelope that must be fully on screen: all of it, or (when the framing crops the cannon at the
        /// bottom, bottomMargin &lt; 0) everything above its lowest metre: the barrel and the top of the carriage.</summary>
        public static Bounds MustShow(Bounds cannon, Settings s)
        {
            if (s == null || s.bottomMargin >= 0f) return cannon;
            var b = new Bounds(); b.SetMinMax(new Vector3(cannon.min.x, cannon.min.y + 1f, cannon.min.z), cannon.max); return b;
        }

        /// <summary>The dressed cannon's envelope: the scale-1 envelope scaled by s.cannonScale about the cannon root (ground point).</summary>
        public static Bounds Envelope(Bounds cannonAtScale1, Vector3 cannonRoot, Settings s)
        {
            float k = s != null ? s.cannonScale : 1f; var b = new Bounds();
            b.SetMinMax(cannonRoot + (cannonAtScale1.min - cannonRoot) * k, cannonRoot + (cannonAtScale1.max - cannonRoot) * k); return b;
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
