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

            yield return TapNamed("Button_1");
            yield return WaitFor(() => AppServices.Router.Top is BriefingScreen, "L1 briefing");
            yield return TapNamed("Button_Play");
            yield return WaitFor(() => AppServices.Router.Top is HudScreen, "L1 HUD (run started)");
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

        [UnityTest]
        public IEnumerator Art_v2_age_gate_keeps_smoke_hooks_and_tap_targets()
        {
            var gate = (AgeGateScreen)AppServices.Router.Top;
            foreach (var n in new[] { "Button_Month", "Button_Year", "Button_Continue" })
                Assert.NotNull(UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(b => b.name == n && b.isActiveAndEnabled), n + " (smoke-script hook) present");
            Assert.AreEqual("Button_Month", gate.MonthButton.name); Assert.AreEqual("Button_Year", gate.YearButton.name);
            Canvas.ForceUpdateCanvases();
            var safe = AppServices.Router.SafeRoot;
            foreach (var b in new[] { gate.MonthButton, gate.YearButton, gate.ContinueButton })
            {
                var rt = (RectTransform)b.transform;
                Assert.GreaterOrEqual(rt.rect.height, 48f - 0.01f, b.name + " tap target ≥ 48dp");
                Assert.True(RectTransformUtility.RectangleContainsScreenPoint(safe, RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center)), null), b.name + " inside the safe area");
            }
            Assert.False(gate.ContinueButton.interactable, "no preselection: Continue starts disabled");
            Assert.AreEqual("Tap to choose", gate.MonthButton.GetComponentInChildren<Text>().text);
            Assert.AreEqual("Tap to choose", gate.YearButton.GetComponentInChildren<Text>().text);

            yield return Tap(gate.YearButton);
            yield return WaitFor(() => AppServices.Router.Top is ListModal, "year sheet (ListModal)");
            var sheet = (ListModal)AppServices.Router.Top;
            Assert.IsNull(sheet.SelectedLabel, "no year preselected on first open");
            foreach (var row in UnityEngine.Object.FindObjectsOfType<Button>().Where(b => b.GetComponentInParent<ScrollRect>() != null))
                Assert.GreaterOrEqual(((RectTransform)row.transform).rect.height, 48f - 0.01f, row.name + " row ≥ 48dp");
            yield return TapNamed("Button_" + (DateTime.UtcNow.Year - 30));
            Assert.False(gate.ContinueButton.interactable, "year alone keeps Continue disabled");
            Assert.AreEqual((DateTime.UtcNow.Year - 30).ToString(), gate.YearButton.GetComponentInChildren<Text>().text);

            yield return Tap(gate.YearButton); // reopen: chosen row shown as selected
            yield return WaitFor(() => AppServices.Router.Top is ListModal, "year sheet reopened");
            Assert.AreEqual((DateTime.UtcNow.Year - 30).ToString(), ((ListModal)AppServices.Router.Top).SelectedLabel);
            yield return TapNamed("Button_Close");
            Assert.True(AppServices.Router.Top is AgeGateScreen);
        }

        [UnityTest]
        public IEnumerator Future_date_shows_error_and_keeps_Continue_disabled()
        {
            var now = DateTime.UtcNow;
            if (now.Month == 12) Assert.Ignore("no future month in December");
            var gate = (AgeGateScreen)AppServices.Router.Top;
            yield return Tap(gate.MonthButton); yield return TapNamed("Button_December");
            yield return Tap(gate.YearButton); yield return TapNamed("Button_" + now.Year);
            Assert.False(gate.ContinueButton.interactable, "future date: Continue disabled");
            StringAssert.Contains("future", gate.Error);
            yield return Tap(gate.YearButton); yield return TapNamed("Button_" + (now.Year - 20));
            Assert.True(gate.ContinueButton.interactable, "changing the value clears the error");
        }

        [UnityTest]
        public IEnumerator Block_screen_has_no_back_retry_or_edit()
        {
            var gate = (AgeGateScreen)AppServices.Router.Top;
            yield return Tap(gate.MonthButton); yield return TapNamed("Button_June");
            yield return Tap(gate.YearButton); yield return TapNamed("Button_" + (DateTime.UtcNow.Year - 10));
            yield return Tap(gate.ContinueButton);
            yield return WaitFor(() => AppServices.Router.Top is BlockedScreen, "block screen");
            var names = UnityEngine.Object.FindObjectsOfType<Button>().Where(b => b.isActiveAndEnabled && b.transform.IsChildOf(AppServices.Router.Top.Root)).Select(b => b.name).ToArray();
            CollectionAssert.AreEquivalent(PrivacyConfig.HasUrl ? new[] { "Button_Privacy Policy" } : new string[0], names, "no back/retry/edit; privacy link only when a URL is configured");
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

        static IEnumerator WaitFor(Func<bool> cond, string what, float seconds = 10f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for: " + what); yield return null; }
        }
    }
}
#endif
