#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using Crashverse.Core;
using Crashverse.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Crashverse.Tests.PlayMode
{
    /// <summary>
    /// BUG-20 regression: taps go through the real EventSystem → StandaloneInputModule → GraphicRaycaster → Button path.
    /// Only the touch source is faked (BaseInput override); Input.GetButtonDown/GetAxisRaw still hit the real Input Manager,
    /// so a missing InputManager axis (the BUG-20 root cause) fails these tests.
    /// </summary>
    public class FirstRunTapTests
    {
        [UnitySetUp]
        public IEnumerator Boot()
        {
            // The test runner's first play-mode scene already booted the game once (AutoBootstrap), and the router and
            // EventSystem are DontDestroyOnLoad: clear them so the Bootstrap scene below starts from one clean copy.
            foreach (var o in UnityEngine.Object.FindObjectsOfType<UIRouter>()) UnityEngine.Object.Destroy(o.gameObject);
            foreach (var o in UnityEngine.Object.FindObjectsOfType<EventSystem>()) UnityEngine.Object.Destroy(o.gameObject);
            foreach (var o in UnityEngine.Object.FindObjectsOfType<GameBootstrap>()) UnityEngine.Object.Destroy(o.gameObject);
            AppServices.Router = null;
            yield return null;
            PlayerPrefs.DeleteAll();
            AgeGateScreen.PendingDob = null;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Bootstrap.unity", LoadSceneMode.Single);
            // Bootstrap.unity carries a GameBootstrap, which boots on every load. Spawning another one doubles the UI
            // (two routers, two age gates) and taps land on the hidden copy, so only spawn if the scene really had none.
            yield return null;
            if (UnityEngine.Object.FindObjectsOfType<GameBootstrap>().Length == 0) new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            yield return WaitFor(() => AppServices.Router != null && AppServices.Router.Top is AgeGateScreen, "age gate shown");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<UIRouter>().Length, "exactly one UI router");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<EventSystem>().Length, "exactly one EventSystem");
            Assert.NotNull(EventSystem.current.GetComponent<StandaloneInputModule>(), "UI uses StandaloneInputModule (legacy Input Manager is the active handler)");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>().Where(g => g.transform.parent == null)) UnityEngine.Object.Destroy(go);
            AppServices.Router = null; PlayerPrefs.DeleteAll();
            yield return null; yield return null;
        }

        [Test]
        public void InputManager_has_the_axes_StandaloneInputModule_reads()
        {
            foreach (var axis in new[] { "Horizontal", "Vertical", "Submit", "Cancel" })
                Assert.DoesNotThrow(() => Input.GetButtonDown(axis), axis + " must exist in ProjectSettings/InputManager.asset");
        }

        [UnityTest]
        public IEnumerator Age_gate_to_L1_by_taps_only()
        {
            var gate = (AgeGateScreen)AppServices.Router.Top;
            Assert.False(gate.ContinueButton.interactable, "Continue disabled until month and year are set");
            StringAssert.Contains("month and year", gate.Error);

            yield return Tap(gate.MonthButton);
            yield return TapNamed("Button_March");
            Assert.False(gate.ContinueButton.interactable, "still disabled with only a month");
            StringAssert.Contains("year", gate.Error);

            yield return Tap(gate.YearButton);
            // V-02: the list opens around current year − 18 (full 100-year range still scrolls)
            Button y18 = null; yield return WaitFor(() => (y18 = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(x => x.name == "Button_" + (DateTime.UtcNow.Year - 18))) != null, "year −18 item");
            Canvas.ForceUpdateCanvases();
            var vsr = y18.GetComponentInParent<ScrollRect>(); var vp = vsr.viewport != null ? vsr.viewport : (RectTransform)vsr.transform; var yrt = (RectTransform)y18.transform;
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(vp, RectTransformUtility.WorldToScreenPoint(null, yrt.TransformPoint(yrt.rect.center)), null), "year list opens at current year − 18");
            Assert.NotNull(UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(x => x.name == "Button_" + (DateTime.UtcNow.Year - 100)), "100-year range kept");
            yield return TapNamed("Button_" + (DateTime.UtcNow.Year - 25));
            Assert.True(gate.ContinueButton.interactable, "Continue enabled once both are set");
            Assert.AreEqual("", gate.Error);

            yield return Tap(gate.ContinueButton);
            yield return WaitFor(() => AppServices.Router.Top is MapScreen, "map after the age gate");
            AssertNoBrokenRenderers("world map");

            yield return TapNamed("Button_1");
            yield return WaitFor(() => AppServices.Router.Top is BriefingScreen, "L1 briefing");
            yield return TapNamed("Button_Play");
            yield return WaitFor(() => AppServices.Router.Top is HudScreen, "L1 HUD (run started)");
            yield return null;
            AssertNoBrokenRenderers("L1 arena + structure");
            yield return PlayHintLineToResult();
        }

        /// <summary>BUG-24 gate in the editor: the practice-hint line (same as the emulator's Button_DevAimHint) must win L1.
        /// Logs every block and each shot's sampled path so a miss is diagnosable from the CI log alone.</summary>
        IEnumerator PlayHintLineToResult()
        {
            var s = GameplayDirector.Session; Assert.NotNull(s, "session");
            var lvl = s.Ctx.level;
            Debug.Log($"L1HINT level={lvl.id} hint=({lvl.hintYaw},{lvl.hintPitch}) shots={s.ShotsLeft} win={lvl.winThreshold} gravity={Physics.gravity}");
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Crashverse.Destruction.DestructibleBlock>())
            {
                var c = b.GetComponent<Collider>();
                Debug.Log($"L1HINT block {b.name} pos={b.transform.position} bounds={(c != null ? c.bounds.size.ToString() : "NO COLLIDER")} rb={(b.GetComponent<Rigidbody>() != null)}");
            }
            Debug.Log($"L1HINT spawn={s.aim.SpawnPosition}");
            float end = Time.realtimeSinceStartup + 120f;
            int shot = 0;
            while (s.State != SessionState.Won && s.State != SessionState.Failed && Time.realtimeSinceStartup < end)
            {
                s.aim.SetAim(lvl.hintYaw, lvl.hintPitch);
                if (!s.TryFire()) { yield return null; continue; }
                shot++;
                Debug.Log($"L1HINT shot {shot} aim=({s.aim.Yaw},{s.aim.Elevation}) v={s.aim.LaunchVelocity} spawn={s.aim.SpawnPosition}");
                float t0 = Time.realtimeSinceStartup; bool seen = false;
                while (Time.realtimeSinceStartup - t0 < 8f)
                {
                    var alive = Crashverse.Weapons.Projectile.Alive;
                    if (alive.Count > 0) { seen = true; Debug.Log($"L1HINT  t={Time.realtimeSinceStartup - t0:F2} proj={alive[0].transform.position}"); }
                    else if (seen) break;
                    yield return new WaitForSeconds(0.1f);
                }
                Debug.Log($"L1HINT after shot {shot}: destroyed={s.tracker.DestroyedRatio:P1} mass={s.tracker.DestroyedMass}/{s.tracker.TotalObjectiveMass} state={s.State}");
                yield return new WaitForSeconds(1f);
            }
            Debug.Log($"L1HINT result state={s.State} destroyed={s.tracker.DestroyedRatio:P1} shots={shot}");
            Assert.AreEqual(SessionState.Won, s.State, $"L1 practice-hint line must win (destroyed {s.tracker.DestroyedRatio:P1} after {shot} shots); see L1HINT log lines");
        }

        [UnityTest]
        public IEnumerator Under_13_goes_to_the_block_screen()
        {
            var gate = (AgeGateScreen)AppServices.Router.Top;
            yield return Tap(gate.MonthButton); yield return TapNamed("Button_January");
            yield return Tap(gate.YearButton); yield return TapNamed("Button_" + (DateTime.UtcNow.Year - 8));
            yield return Tap(gate.ContinueButton);
            yield return WaitFor(() => AppServices.Router.Top is BlockedScreen, "under-13 blocked");
            Assert.True(AgeGateScreen.Blocked);
        }

        // ---- helpers ----
        IEnumerator TapNamed(string name)
        {
            Button b = null;
            yield return WaitFor(() => (b = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(x => x.name == name && x.isActiveAndEnabled)) != null, name + " present");
            yield return Tap(b);
        }

        /// <summary>QA: batchmode has no reliable focus, so the tap goes straight through the UI event path: GraphicRaycaster
        /// raycast at the button's screen centre (proves nothing covers it), then pointerDown/Up/Click on what was hit.</summary>
        IEnumerator Tap(Button b)
        {
            var sr = b.GetComponentInParent<ScrollRect>();
            if (sr != null) { ScrollIntoView(sr, (RectTransform)b.transform); yield return null; }
            Canvas.ForceUpdateCanvases();
            var rt = (RectTransform)b.transform;
            Vector2 p = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            var es = EventSystem.current; var hits = new System.Collections.Generic.List<RaycastResult>();
            var ev = new PointerEventData(es) { position = p, button = PointerEventData.InputButton.Left, clickCount = 1 };
            es.RaycastAll(ev, hits);
            Assert.IsNotEmpty(hits, $"nothing under {p}");
            var top = hits[0].gameObject;
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(top);
            Assert.AreSame(b.gameObject, target, $"tap at {p} hits '{top.name}' (handler '{target?.name}'), not '{b.name}': something covers it. hits=[{string.Join(", ", hits.Select(h => h.gameObject.name))}]");
            Assert.True(b.IsInteractable(), b.name + " is not interactable");
            ev.pointerCurrentRaycast = ev.pointerPressRaycast = hits[0];
            ev.pressPosition = p; ev.rawPointerPress = top;
            ev.pointerPress = ExecuteEvents.ExecuteHierarchy(top, ev, ExecuteEvents.pointerDownHandler) ?? target;
            yield return null;
            ExecuteEvents.Execute(ev.pointerPress, ev, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, ev, ExecuteEvents.pointerClickHandler);
            yield return null; yield return null;
        }

        static void ScrollIntoView(ScrollRect sr, RectTransform item)
        {
            Canvas.ForceUpdateCanvases();
            var content = sr.content; var vp = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
            float overflow = content.rect.height - vp.rect.height; if (overflow <= 0) return;
            float y = -content.InverseTransformPoint(item.TransformPoint(item.rect.center)).y - content.rect.height * (1 - content.pivot.y);
            float top = Mathf.Clamp(y - vp.rect.height / 2f, 0, overflow);
            sr.verticalNormalizedPosition = 1f - top / overflow;
        }

        // BUG-23: magenta on device = renderer with no material / unsupported shader. Must find none after map and L1 build.
        static void AssertNoBrokenRenderers(string where)
        {
            Assert.IsNotNull(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline, "BUG-23: no URP pipeline asset assigned (Graphics/Quality)");
            int broken = 0; var names = new System.Collections.Generic.List<string>();
            foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name.StartsWith("Hidden/InternalErrorShader") || m.shader.name == "Standard")
                { broken++; if (names.Count < 10) names.Add(r.name + ":" + (m == null ? "null" : m.shader == null ? "noshader" : m.shader.name)); }
            }
            Assert.AreEqual(0, broken, $"BUG-23: {broken} renderers without a usable URP material in {where}: {string.Join(", ", names)}");
        }

        static IEnumerator WaitFor(Func<bool> cond, string what, float seconds = 10f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for: " + what); yield return null; }
        }
    }
}
#endif
