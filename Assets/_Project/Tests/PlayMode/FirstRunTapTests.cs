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
            foreach (var f in new[] { Crashverse.Meta.PlayerProgress.DefaultPath, Crashverse.Meta.CampaignSave.DefaultPath })
                if (System.IO.File.Exists(f)) System.IO.File.Delete(f); // v2: first run = no guest profile yet
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
            if (_swapped) { Crashverse.Backend.BackendLocator.Current = _prevBackend; _swapped = false; }
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
            // Security (PR #52): neutral start: the list opens mid-range (current year − 50), not at an eligibility threshold
            int mid = DateTime.UtcNow.Year - AgeGateScreen.YearListMiddle;
            Button ymid = null; yield return WaitFor(() => (ymid = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(x => x.name == "Button_" + mid)) != null, "mid-range year item");
            yield return null; Canvas.ForceUpdateCanvases();
            var vsr = ymid.GetComponentInParent<ScrollRect>(); var vp = vsr.viewport != null ? vsr.viewport : (RectTransform)vsr.transform;
            bool Visible(Button b) { var rt = (RectTransform)b.transform; return RectTransformUtility.RectangleContainsScreenPoint(vp, RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center)), null); }
            Assert.True(Visible(ymid), "year list opens mid-range (current year − 50)");
            var y18 = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(x => x.name == "Button_" + (DateTime.UtcNow.Year - 18));
            Assert.NotNull(y18, "current year − 18 still in the list");
            Assert.False(Visible(y18), "no anchor at the 18-year threshold");
            Assert.NotNull(UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(x => x.name == "Button_" + (DateTime.UtcNow.Year - 100)), "100-year range kept");
            yield return TapNamed("Button_" + (DateTime.UtcNow.Year - 25));
            Assert.True(gate.ContinueButton.interactable, "Continue enabled once both are set");
            Assert.AreEqual("", gate.Error);

            yield return Tap(gate.ContinueButton);
            // v2 target UI: Login (social sign-in "Coming soon", guest) → Create Profile → World Map → Level Select → HUD
            yield return WaitFor(() => AppServices.Router.Top is LoginScreen, "login after the age gate");
            foreach (var prov in new[] { "Google", "Facebook", "Instagram", "X", "Email" })
            {
                var sb = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(x => x.name == "Button_" + prov);
                Assert.NotNull(sb, prov + " sign-in shown"); Assert.False(sb.interactable, prov + " sign-in disabled (Coming soon)");
            }
            yield return TapNamed("Button_Guest");
            yield return WaitFor(() => AppServices.Router.Top is ProfileScreen, "create profile");
            var prof = (ProfileScreen)AppServices.Router.Top;
            StringAssert.StartsWith("Smasher_", prof.NameText, "default guest name");
            yield return TapNamed("Button_Avatar2");
            yield return TapNamed("Button_Badge3");
            prof.NameText = "Tap Tester";
            yield return TapNamed("Button_Continue");
            yield return WaitFor(() => AppServices.Router.Top is MapScreen, "map after creating the profile");
            Assert.True(AppServices.Progress.Data.profile.created, "profile saved");
            Assert.AreEqual("Tap Tester", AppServices.Progress.Data.profile.name);
            Assert.AreEqual(1, AppServices.Progress.Data.profile.avatar); Assert.AreEqual(2, AppServices.Progress.Data.profile.badge);
            AssertNoBrokenRenderers("world map");
            AssertMapNodesNeverUnderHeader();

            yield return TapNamed("Button_1");
            yield return WaitFor(() => AppServices.Router.Top is LevelSelectScreen, "L1 level select");
            yield return TapNamed("Button_Play");
            yield return WaitFor(() => AppServices.Router.Top is HudScreen, "L1 HUD (run started)");
            yield return null;
            AssertNoBrokenRenderers("L1 arena + structure");
            yield return null; // one LateUpdate with the locked pose
            AssertCannonFramedFromBehind();
            AssertSwipeAimsSameWayOnScreen();
            yield return PlayHintLineToResult();
            // v2 §6 Level Complete: rewards in the local wallet, Next Level → Level 2 select
            yield return WaitFor(() => AppServices.Router.Top is ResultsScreen, "level complete");
            Assert.True(((ResultsScreen)AppServices.Router.Top).Won);
            Assert.Greater(AppServices.Progress.Data.coins, 0, "first clear coins granted");
            yield return TapNamed("Button_NextLevel");
            yield return WaitFor(() => AppServices.Router.Top is LevelSelectScreen ls && ls.Level.number == 2, "L2 level select after Next Level");
        }

        /// <summary>QA MED (#55): no map node may render under the ★/coin/gem pills, the season ribbon or the dock, on any aspect ratio.
        /// The path viewport (RectMask2D) sits strictly between the header and the dock, so a node is clipped before it can overlap.
        /// Only Chapter 1 levels 1–10 are on the map (no next-season island, no Seasons button, no "More" stubs).</summary>
        static void AssertMapNodesNeverUnderHeader()
        {
            var map = (MapScreen)AppServices.Router.Top; Canvas.ForceUpdateCanvases();
            Rect W(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
            var vp = W(map.Viewport); var top = W(map.TopBar); var rib = W(map.Ribbon); var dock = W(map.Dock);
            Assert.LessOrEqual(vp.yMax, top.yMin + 0.5f, $"path viewport {vp} below the wallet bar {top}");
            Assert.LessOrEqual(vp.yMax, rib.yMin + 0.5f, $"path viewport {vp} below the season ribbon {rib}");
            Assert.GreaterOrEqual(vp.yMin, dock.yMax - 0.5f, $"path viewport {vp} above the dock {dock}");
            Assert.NotNull(map.Viewport.GetComponent<RectMask2D>(), "nodes are clipped to the viewport");
            var names = UnityEngine.Object.FindObjectsOfType<Button>().Select(b => b.name).ToList();
            for (int n = 1; n <= 10; n++) Assert.Contains("Button_" + n, names, "level " + n + " on the map");
            Assert.False(names.Contains("Button_11"), "no Seasons 2-4 levels");
            foreach (var hidden in new[] { "Button_NextSeason", "Button_Seasons", "Button_More", "Button_SeasonPass", "Button_Leaderboards", "Button_DailyLogin", "Button_Achievements" })
                Assert.False(names.Contains(hidden), hidden + " hidden (unfinished feature)");
            Assert.Contains("Button_DailyBoss", names, "Daily Boss on the map");
        }

        /// <summary>User 2026-10-30 / Target UI §5: locked camera behind the cannon. Through the real run camera: the whole cannon is
        /// on screen, centred at the bottom and big; the whole building is on screen above it; the aim line and its landing marker show.</summary>
        static void AssertCannonFramedFromBehind()
        {
            var s = GameplayDirector.Session; var cam = Camera.main; Assert.NotNull(cam, "run camera");
            Assert.True(GameplayDirector.RunCamera.locked, "camera angle locked (no orbit)");
            Rect VpRect(Bounds b)
            {
                float x0 = 9f, y0 = 9f, x1 = -9f, y1 = -9f;
                for (int i = 0; i < 8; i++)
                {
                    var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var v = cam.WorldToViewportPoint(p); Assert.Greater(v.z, 0f, "in front of the camera");
                    x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
                }
                return Rect.MinMaxRect(x0, y0, x1, y1);
            }
            var wrecker = s.aim.GetComponentsInChildren<Renderer>().Where(r => !(r is LineRenderer) && !(r is ParticleSystemRenderer) && r.enabled && r.name != "AimLandingMarker").Select(r => r.bounds).ToList();
            var cb = wrecker[0]; foreach (var b in wrecker) cb.Encapsulate(b);
            var c = VpRect(cb); var st = VpRect(GameplayDirector.StructureBounds(s, s.structureBase));
            Debug.Log($"CANNONCAM aspect={cam.aspect:F3} fov={cam.fieldOfView:F1} cannon={c} structure={st}");
            const float tol = 0.01f;
            Assert.True(c.xMin >= -tol && c.xMax <= 1f + tol && c.yMin >= -tol && c.yMax <= 1f + tol, "whole cannon on screen: " + c);
            Assert.True(st.xMin >= -tol && st.xMax <= 1f + tol && st.yMin >= -tol && st.yMax <= 1f + tol, "whole building on screen: " + st);
            Assert.AreEqual(0.5f, c.center.x, 0.03f, "cannon horizontally centred");
            Assert.Less(c.yMin, 0.1f, "cannon at the bottom");
            Assert.True(c.width > 0.22f || c.height > 0.3f, "cannon big (portrait: wide, landscape editor: tall): " + c);
            Assert.Greater(st.center.y, c.center.y + 0.15f, "building above the cannon");
            Assert.True(s.aim.Arc != null && s.aim.Arc.positionCount > 2, "aim line drawn");
        }

        /// <summary>P0 (dev-build-6): a left swipe must move the aim LEFT on screen, a right swipe RIGHT (was mirrored).
        /// Measured through the run camera: screen x of a point 10 m along the launch direction.</summary>
        static void AssertSwipeAimsSameWayOnScreen()
        {
            var s = GameplayDirector.Session; var cam = Camera.main; Assert.NotNull(cam, "run camera");
            float AimScreenX() => cam.WorldToScreenPoint(s.aim.SpawnPosition + s.aim.LaunchVelocity.normalized * 10f).x;
            s.aim.SetAim(0f, 20f); float x0 = AimScreenX(), y0 = s.aim.Yaw;
            s.aim.Drag(new Vector2(-120f, 0f)); float xl = AimScreenX(), yl = s.aim.Yaw;
            Assert.Less(xl, x0 - 1f, $"left swipe must aim left on screen (screen x {x0:F0} -> {xl:F0}, yaw {y0} -> {yl})");
            s.aim.SetAim(0f, 20f);
            s.aim.Drag(new Vector2(120f, 0f)); float xr = AimScreenX();
            Assert.Greater(xr, x0 + 1f, $"right swipe must aim right on screen (screen x {x0:F0} -> {xr:F0})");
            s.aim.CancelAim(); s.aim.SetAim(0f, 20f);
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

        // ---- Security #55: no server backend (release while the server stays offline) ----
        bool _swapped; Crashverse.Backend.IBackendService _prevBackend;
        void UseOfflineBackend() { _prevBackend = Crashverse.Backend.BackendLocator.Current; _swapped = true; Crashverse.Backend.BackendLocator.Current = new Crashverse.Backend.OfflineBackendService(); }

        IEnumerator PassGate(int yearsAgo)
        {
            var gate = (AgeGateScreen)AppServices.Router.Top;
            yield return Tap(gate.MonthButton); yield return TapNamed("Button_January");
            yield return Tap(gate.YearButton); yield return TapNamed("Button_" + (DateTime.UtcNow.Year - yearsAgo));
            yield return Tap(gate.ContinueButton);
            yield return WaitFor(() => AppServices.Router.Top is LoginScreen, "login after the gate");
        }

        [UnityTest]
        public IEnumerator Offline_age_band_is_final_across_relaunch()
        {
            UseOfflineBackend();
            yield return PassGate(15); // 13-17
            Assert.AreEqual(Crashverse.Meta.AgeGate.Band.Minor13to17, AgeGateScreen.Current);
            AgeGateScreen.PendingDob = null; // a relaunch drops the in-memory DOB
            GameBootstrap.RestartFirstRun();
            yield return WaitFor(() => AppServices.Router != null && AppServices.Router.Top != null, "relaunched");
            yield return null; yield return null;
            Assert.False(AppServices.Router.Top is AgeGateScreen, "B-55-2: no age gate again after relaunch offline");
            Assert.False(AgeGateScreen.Needed);
            Assert.AreEqual(Crashverse.Meta.AgeGate.Band.Minor13to17, AgeGateScreen.Current, "band still 13-17");
        }

        [UnityTest]
        public IEnumerator Offline_band_is_never_relaxed()
        {
            UseOfflineBackend();
            PlayerPrefs.SetInt(AgeGateScreen.BandKey + Crashverse.Meta.AgeGate.Version, (int)Crashverse.Meta.AgeGate.Band.Minor13to17); // stored 13-17
            yield return PassGate(30); // re-answered as adult
            Assert.AreEqual(Crashverse.Meta.AgeGate.Band.Minor13to17, AgeGateScreen.Current, "Confirm keeps the most restrictive band");
        }

        [UnityTest]
        public IEnumerator Offline_delete_wipes_every_file_and_pref()
        {
            UseOfflineBackend();
            yield return PassGate(30);
            yield return TapNamed("Button_Guest");
            yield return WaitFor(() => AppServices.Router.Top is ProfileScreen, "create profile");
            yield return TapNamed("Button_Continue");
            yield return WaitFor(() => AppServices.Router.Top is MapScreen, "map");
            var dir = Application.persistentDataPath;
            foreach (var f in new[] { "campaign_save.json", "progress_v2.json.tmp", "campaign_sync_queue.json" }) System.IO.File.WriteAllText(System.IO.Path.Combine(dir, f), "{}");
            Assert.True(System.IO.File.Exists(Crashverse.Meta.PlayerProgress.DefaultPath), "profile saved before deletion");
            PlayerPrefs.SetInt("cv_test_marker", 1); PlayerPrefs.Save();
            AppServices.Router.Push(new DeleteAccountScreen());
            yield return null; yield return null;
            Assert.NotNull(UnityEngine.Object.FindObjectsOfType<Text>().FirstOrDefault(t => t.text == DeleteAccountScreen.OfflineCopy), "offline copy shown");
            var hold = UnityEngine.Object.FindObjectOfType<HoldToConfirm>();
            hold.OnPointerDown(null);
            yield return WaitFor(() => AppServices.Router != null && AppServices.Router.Top is AgeGateScreen, "first-run age gate after deletion", 6f);
            foreach (var f in Crashverse.Core.AccountDeletion.LocalFiles) Assert.False(System.IO.File.Exists(System.IO.Path.Combine(dir, f)), f + " deleted");
            Assert.IsEmpty(System.IO.Directory.GetFiles(dir, "*.json"), "no CRASHVERSE json left");
            Assert.False(PlayerPrefs.HasKey("cv_test_marker"), "prefs cleared");
            Assert.False(PlayerPrefs.HasKey(AgeGateScreen.BandKey + Crashverse.Meta.AgeGate.Version), "age band cleared");
            Assert.False(PlayerPrefs.HasKey(Crashverse.Core.AccountDeletion.PendingKey), "no pending flag");
            Assert.True(AgeGateScreen.Needed, "first-run state");
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
            float y = content.rect.yMax - content.InverseTransformPoint(item.TransformPoint(item.rect.center)).y; // distance from the content top (any pivot)
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
