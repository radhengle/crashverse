using System.Collections.Generic;
using System.Linq;
using Crashverse.Data;
using Crashverse.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Crashverse.Backend.Tests
{
    /// <summary>
    /// User 2026-10-30 ("cannon doesn't come in picture clearly and angle also") / Target UI §5: locked behind-the-cannon camera.
    /// For every Chapter 1 level 1–10 and every Daily Boss layout, on common phone aspect ratios: the whole cannon and the whole
    /// building are on screen, the cannon is centred at the bottom and big, and the building sits above it filling the view.
    /// </summary>
    public class CannonCameraEditModeTests
    {
        static readonly Vector3 CannonRoot = new Vector3(-20f, 0f, 0f), StructureBase = new Vector3(10f, 0f, 0f);
        // Wrecker v2 envelope around its root (WreckerSpec: carriage from -1.75, barrel/booster tip ~+1.95 at rest, 2.6 m tall, 2 m wide)
        static readonly Bounds Cannon = new Bounds(new Vector3(-19.875f, 1.3f, 0f), new Vector3(3.75f, 2.6f, 2.0f));
        // 1080x2340 (19.5:9, the CI emulator + Nothing Phone 3a), 20:9, 21:9, 16:9, 18:9 and a 4:3 tablet in portrait
        static readonly float[] Aspects = { 1080f / 2340f, 1080f / 2400f, 1080f / 2520f, 9f / 16f, 0.5f, 0.75f };

        static IEnumerable<(string id, Bounds b)> Layouts()
        {
            var db = ContentDatabase.LoadBundled();
            var ids = db.Chapter1.levels.Select(l => l.layout).Concat(db.Meta.dailyBoss.layouts ?? new string[0]).Distinct();
            foreach (var id in ids)
            {
                var lay = db.GetLayout(id); Assert.NotNull(lay, id);
                var b = new Bounds(); bool any = false;
                foreach (var bd in lay.blocks)
                {
                    var bb = new Bounds(StructureBase + new Vector3(bd.p[0], bd.p[1], bd.p[2]), new Vector3(bd.s[0], bd.s[1], bd.s[2]));
                    if (!any) { b = bb; any = true; } else b.Encapsulate(bb);
                }
                yield return (id, b);
            }
        }

        [Test]
        public void Every_level_and_boss_frames_the_whole_cannon_and_building()
        {
            int n = 0;
            foreach (var (id, b) in Layouts())
                foreach (var a in Aspects)
                {
                    var pose = CannonCameraRules.Solve(CannonRoot, b, a); n++;
                    string at = $"{id} @ aspect {a:F3} (fov {pose.fov:F1})";
                    Assert.True(CannonCameraRules.FullyVisible(pose, a, b), "whole building on screen: " + at);
                    Assert.True(CannonCameraRules.FullyVisible(pose, a, Cannon), "whole cannon on screen: " + at);
                    var c = CannonCameraRules.ViewportRect(pose, a, Cannon); var s = CannonCameraRules.ViewportRect(pose, a, b);
                    Assert.AreEqual(0.5f, c.center.x, 0.02f, "cannon horizontally centred: " + at);
                    Assert.Less(c.yMin, 0.08f, "cannon at the bottom: " + at);
                    Assert.Greater(c.width, 0.35f, "cannon big (>= 35% of the width): " + at);
                    Assert.Less(c.yMax, 0.6f, "cannon stays in the lower part (barrel tip may reach the building base): " + at);
                    Assert.Greater(s.center.y, c.center.y + 0.15f, "building above the cannon: " + at);
                    Assert.True(s.width >= 0.65f || s.yMax >= 0.7f, $"building fills the view (width {s.width:F2} >= 0.65 or top {s.yMax:F2} >= 0.70): " + at);
                }
            Assert.Greater(n, 10 * Aspects.Length - 1, "all 10 Chapter 1 levels (and the Daily Boss) checked");
        }

        [Test]
        public void Camera_is_on_the_firing_line_so_plus_yaw_stays_screen_left()
        {
            var b = new Bounds(StructureBase + Vector3.up * 4f, new Vector3(4f, 8f, 6f));
            var pose = CannonCameraRules.Solve(CannonRoot, b, 1080f / 2340f);
            Assert.AreEqual(0f, pose.position.z, 1e-3f, "camera behind the cannon on the firing line");
            Assert.Less(pose.position.x, CannonRoot.x, "camera behind the cannon");
            Assert.Greater(pose.position.y, Cannon.max.y, "camera above the barrel");
            var left = CannonCameraRules.ToViewport(pose, 0.46f, new Vector3(10f, 2f, 2f)).x;   // world +z (what +yaw turns toward)
            var right = CannonCameraRules.ToViewport(pose, 0.46f, new Vector3(10f, 2f, -2f)).x;
            Assert.Less(left, right, "+z is screen-left: the mirrored-aim P0 mapping (finger right = yaw down) still holds");
        }

        [Test]
        public void Pose_is_deterministic_and_resolves_per_aspect()
        {
            var b = new Bounds(StructureBase + Vector3.up * 5f, new Vector3(4f, 10f, 14f));
            var p1 = CannonCameraRules.Solve(CannonRoot, b, 0.46f); var p2 = CannonCameraRules.Solve(CannonRoot, b, 0.46f);
            Assert.AreEqual(p1.position, p2.position); Assert.AreEqual(p1.fov, p2.fov);
            var wide = CannonCameraRules.Solve(CannonRoot, b, 0.75f);
            Assert.LessOrEqual(wide.fov, p1.fov + 1e-3f, "a wider screen never needs a wider vertical FOV");
        }
    }
}
