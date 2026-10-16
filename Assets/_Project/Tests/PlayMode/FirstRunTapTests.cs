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
        class FakeTouchInput : BaseInput
        {
            public Touch? Current;
            public override bool touchSupported => true;
            public override bool mousePresent => false;
            public override int touchCount => Current.HasValue ? 1 : 0;
            public override Touch GetTouch(int index) => Current.Value;
        }

        FakeTouchInput _fake;

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
            var module = EventSystem.current.GetComponent<StandaloneInputModule>();
            Assert.NotNull(module, "UI uses StandaloneInputModule (legacy Input Manager is the active handler)");
            _fake = EventSystem.current.gameObject.AddComponent<FakeTouchInput>();
            module.inputOverride = _fake;
            // Batchmode editor has no window focus; StandaloneInputModule drops all events while unfocused in the editor.
            EventSystem.current.SendMessage("OnApplicationFocus", true);
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

        // ---- helpers ----
        IEnumerator TapNamed(string name)
        {
            Button b = null;
            yield return WaitFor(() => (b = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(x => x.name == name && x.isActiveAndEnabled)) != null, name + " present");
            yield return Tap(b);
        }

        IEnumerator Tap(Button b)
        {
            var sr = b.GetComponentInParent<ScrollRect>();
            if (sr != null) { ScrollIntoView(sr, (RectTransform)b.transform); yield return null; }
            Canvas.ForceUpdateCanvases();
            var rt = (RectTransform)b.transform;
            Vector2 p = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            int clicks = 0; UnityEngine.Events.UnityAction count = () => clicks++;
            b.onClick.AddListener(count);
            _fake.Current = new Touch { fingerId = 0, position = p, phase = TouchPhase.Began, tapCount = 1, type = TouchType.Direct };
            yield return null;
            _fake.Current = new Touch { fingerId = 0, position = p, phase = TouchPhase.Ended, tapCount = 1, type = TouchType.Direct };
            yield return null;
            _fake.Current = null;
            yield return null;
            if (b == null) yield break; // the tap navigated away and destroyed the button: it was received
            b.onClick.RemoveListener(count);
            if (clicks != 1)
            {
                var es = EventSystem.current; var hits = new System.Collections.Generic.List<RaycastResult>();
                es.RaycastAll(new PointerEventData(es) { position = p }, hits);
                Assert.Fail($"tap at {p} did not reach '{b.name}'. screen={Screen.width}x{Screen.height} focused={es.isFocused} module={es.currentInputModule?.GetType().Name ?? "none"} " +
                            $"interactable={b.IsInteractable()} hits=[{string.Join(", ", hits.Select(h => h.gameObject.name + "@" + h.module?.GetType().Name))}] " +
                            $"btnRect={RectTransformUtility.PixelAdjustRect(rt, b.GetComponentInParent<Canvas>())} canvasScale={b.GetComponentInParent<Canvas>().scaleFactor}");
            }
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
