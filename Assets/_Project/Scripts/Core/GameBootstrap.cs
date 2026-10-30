using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Destruction;
using Crashverse.Map;
using Crashverse.Meta;
using Crashverse.Perf;
using Crashverse.UI;
using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// Entry point (referenced by Bootstrap.unity; AutoBootstrap spawns it if the scene reference is missing).
    /// Loads content, campaign save, local backend, UI router; opens the island map; flushes any orphaned ranked run.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        static GameBootstrap _instance;

        void Awake() { _instance = this; QualityController.ApplyMid(); Init(); }

        /// <summary>A4: after a confirmed deletion the app restarts in first-run state without a process restart.</summary>
        public static void RestartFirstRun()
        {
            GameplayDirector.Reset();
            if (AppServices.Router != null) Destroy(AppServices.Router.gameObject);
            AppServices.Router = null; AppServices.Account = null; AppServices.SessionToken = null; // A4 sign-out
            _instance.Init();
        }

        async void Init()
        {
            AppServices.Content = ContentDatabase.LoadBundled();
            AppServices.Save = new CampaignSave();
            AppServices.Progress = new PlayerProgress();
            Crashverse.Meta.DailyLoginRules.Observe(AppServices.Progress.Data, Crashverse.Meta.DailyLoginRules.Key(System.DateTime.Now)); // rollback guard
            AppServices.Theme = UITheme.Load();
            AppServices.Router = UIRouter.Create(AppServices.Theme);
            if (Camera.main == null) { var c = new GameObject("Main Camera"); c.tag = "MainCamera"; c.AddComponent<Camera>(); }
            Crashverse.Audio.AudioListenerGuard.Ensure(); Crashverse.Audio.SfxPlayer.Ensure(); // P0 no-sound: exactly one listener + SFX router

            // §E1/E2: the age gate comes before any account, sign-in or backend traffic. Blocked installs never get further.
            if (AgeGateScreen.Blocked) { AppServices.Router.PopAllAndPush(new BlockedScreen()); return; }
            if (AgeGateScreen.Needed)
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                AppServices.Router.PopAllAndPush(new AgeGateScreen(ok => tcs.TrySetResult(ok)));
                if (!await tcs.Task) { AppServices.Router.PopAllAndPush(new BlockedScreen()); return; }
            }

#if CRASHVERSE_DEV
            AppServices.Backend = BackendLocator.Current ?? new LocalBackendService(AppServices.Content);
#else
            // Security Review §1: release builds never fall back to the local stub. integration/v2: the server stays offline, so without a
            // server client the release build uses OfflineBackendService (every call fails "offline"; grants and stores nothing).
            AppServices.Backend = BackendLocator.Current ?? new OfflineBackendService();
#endif
            BackendLocator.Current = AppServices.Backend;
            AppServices.StoreErrors = StoreValidator.Validate(AppServices.Content.Store, out AppServices.StoreItems);
            foreach (var e in AppServices.StoreErrors) Debug.LogError("[Store] rejected " + e);
            Crashverse.Feel.FeelDirector.InitSettings(); // persisted Reduce shake / Reduce motion (+ OS auto-on)
            if (FindObjectOfType<QualityController>() == null) new GameObject("Quality").AddComponent<QualityController>();
            GameplayDirector.ShowMap();
            await AppServices.SyncServerTime();
            await AccountLifecycle.OnConnected(); // E3 band → server, A6 queued deletion, A5 cancel offer, C2 inactivity warning
            var pending = await TitanRunJournal.FlushPending(AppServices.Backend);
            await CampaignResultQueue.Flush(AppServices.Backend); // BUG-14
            if (pending != null) AppServices.Router.Toast("Interrupted ranked run: " + pending.message, 4f);
        }

        async void OnApplicationPause(bool paused)
        {
            if (paused || AppServices.Backend == null) return;
            if (AgeGateScreen.Blocked || AgeGateScreen.Needed) return;
            await AppServices.SyncServerTime(); // SG-20: resync on resume
            await AccountLifecycle.OnConnected();
            if (GameplayDirector.Session == null) { await TitanRunJournal.FlushPending(AppServices.Backend); await CampaignResultQueue.Flush(AppServices.Backend); }
        }
    }

    /// <summary>Switches between the island map world and a gameplay run (single scene, built procedurally).</summary>
    public static class GameplayDirector
    {
        static GameObject _mapRoot, _runRoot;
        public static WorldMapBuilder Map { get; private set; }
        public static GameSession Session { get; private set; }
        public static OrbitCamera RunCamera { get; private set; }

        public static void Reset()
        {
            EndRun();
            if (_mapRoot != null) Object.Destroy(_mapRoot);
            _mapRoot = null; Map = null;
        }

        /// <summary>
        /// Target UI v2: the world map is a 2D scrolling path (MapScreen) over the art pack backgrounds, so no 3D map world is built.
        /// First run (no local guest profile yet) goes through Login → Create Profile first.
        /// </summary>
        /// <summary>QA V2-01 second line of defence: if the process dies mid-run (any relaunch Unity can't survive), the next launch
        /// reopens that level's Level Select (or the Daily Boss card) instead of dropping the player on the map. Cleared on every
        /// normal end of a run; only honoured on the first map after a launch.</summary>
        public const string ResumeKey = "cv_resume_run";
        static bool _resumeChecked;

        public static void ShowMap()
        {
            string resume = _resumeChecked ? "" : PlayerPrefs.GetString(ResumeKey, "");
            EndRun();
            Crashverse.Audio.MusicPlayer.Ensure().PlayBase("sunnyville_map_loop");
            var r = AppServices.Router;
            if (AppServices.Progress != null && !AppServices.Progress.Data.profile.created)
            {
                if (resume.Length > 0) PlayerPrefs.SetString(ResumeKey, resume); // keep it for the map after login
                if (!(r.Top is LoginScreen) && !(r.Top is ProfileScreen)) r.PopAllAndPush(new LoginScreen());
                return;
            }
            if (r.Top is MapScreen) return;
            r.PopAllAndPush(new MapScreen());
            if (!_resumeChecked) { _resumeChecked = true; TryResume(resume); }
#if CRASHVERSE_DEV
            DevOpenLevel();
#endif
        }

#if CRASHVERSE_DEV
        static bool _devLevelDone;
        /// <summary>Dev builds only (emulator smoke screenshots of L3/L8 models): launched with --es cv_dev_level c1_l03, the first map
        /// opens that level's Level Select regardless of unlocks. Release builds don't compile this.</summary>
        static void DevOpenLevel()
        {
            if (_devLevelDone) return; _devLevelDone = true;
            string id = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = act.Call<AndroidJavaObject>("getIntent"))
                    id = intent.Call<string>("getStringExtra", "cv_dev_level");
            }
            catch (System.Exception) { }
#endif
            var lvl = string.IsNullOrEmpty(id) ? null : AppServices.Content?.GetLevel(id);
            if (lvl == null) return;
            AppServices.Router.Push(new LevelSelectScreen(lvl));
            Debug.Log("[DEV] cv_dev_level opened " + id);
        }
#endif

        static void TryResume(string resume)
        {
            if (string.IsNullOrEmpty(resume)) return;
            var r = AppServices.Router; var db = AppServices.Content;
            if (resume == "dailyboss") { r.Push(new DailyBossScreen()); r.Toast("Back to the Daily Boss"); Debug.Log("[V2-01] resumed Daily Boss after restart"); return; }
            var lvl = db?.GetLevel(resume);
            if (lvl != null && Crashverse.Meta.UnlockRules.IsLevelUnlocked(lvl, AppServices.Save))
            { r.Push(new LevelSelectScreen(lvl)); r.Toast("Back to Level " + lvl.number); Debug.Log("[V2-01] resumed " + resume + " after restart"); }
        }

        /// <summary>Presentation hook after a run is fully built (Crashverse.Visuals.KingdomVisuals). Never read by the sim.</summary>
        public static event System.Action<GameObject, GameSession, Weapons.WreckerBuilder, RunContext> RunBuilt;

        public static void StartRun(RunContext ctx)
        {
            EndRun();
            _resumeChecked = true; // a run started normally: nothing to resume this launch
            var rk = ctx.mode == RunMode.DailyBoss ? "dailyboss" : ctx.mode == RunMode.Campaign && ctx.level != null ? ctx.level.id : "";
            if (rk.Length > 0) { PlayerPrefs.SetString(ResumeKey, rk); PlayerPrefs.Save(); }
            _runRoot = new GameObject("Run");
            var arena = new GameObject("Arena").AddComponent<ArenaBuilder>();
            arena.transform.SetParent(_runRoot.transform);
            arena.Build();
            if (ctx.IsHard || ctx.mode == RunMode.DailyBoss) ArenaBuilder.DarkTheme(0.55f); // v2: hard level / Daily Boss dark theme
            new GameObject("DebrisPool").AddComponent<DebrisPool>().transform.SetParent(_runRoot.transform);
            new GameObject("GlassShardFx").AddComponent<GlassShardFx>().transform.SetParent(_runRoot.transform); // presentation-only glass shards

            var wreckerGo = new GameObject("Wrecker");
            wreckerGo.transform.SetParent(_runRoot.transform);
            wreckerGo.transform.position = new Vector3(Weapons.WreckerSpec.WreckerX, 0f, 0f);
            var wb = wreckerGo.AddComponent<Weapons.WreckerBuilder>(); wb.Build();
            var aim = wreckerGo.AddComponent<Weapons.AimController>(); aim.Bind(wb.yawPivot, wb.pitchPivot, wb.projectileSpawn);
            var tune = AppServices.Content.Wrecker;
            aim.launchSpeed = tune.launchSpeed; aim.yawDegPerDp = tune.aimYawDegPerDp; aim.pitchDegPerDp = tune.aimPitchDegPerDp;
            var fire = wreckerGo.AddComponent<Weapons.FireController>();
            fire.aim = aim; fire.defaultDamage = tune.defaultDamage; fire.projectileScale = tune.projectileScale;
            fire.projectileMass = tune.projectileMass; fire.splitSpreadDeg = tune.splitSpreadDeg;

            var levelGo = new GameObject("Level"); levelGo.transform.SetParent(_runRoot.transform);
            var s = levelGo.AddComponent<GameSession>();
            s.builder = levelGo.AddComponent<Level.LevelBuilder>();
            s.tracker = levelGo.AddComponent<DestructionTracker>();
            s.integrity = levelGo.AddComponent<StructuralIntegrity>();
            s.killPlane = KillPlane.Instance;
            s.fire = fire; s.aim = aim;
            s.structureBase = new Vector3(10f, 0f, 0f); // 30u from the Wrecker (targets 25–40u away)
            Session = s;

            var cam = Camera.main;
            RunCamera = cam.GetComponent<OrbitCamera>();
            if (RunCamera == null) RunCamera = cam.gameObject.AddComponent<OrbitCamera>(); // no ?? on UnityEngine.Object
            RunCamera.enabled = true;
            RunCamera.Frame(wreckerGo.transform.position, s.structureBase);
            s.Begin(ctx);
            // User 2026-10-30 / Target UI §5: locked camera behind the cannon, framing the cannon (bottom centre) + the whole building
            RunCamera.FrameBehindCannon(wreckerGo.transform.position, StructureBounds(s, s.structureBase), HintArcPoints(s));
            int fx = RenderMaterials.FixAll(_runRoot); if (fx > 0) Debug.LogWarning($"[BUG-23] run: {fx} renderers had no usable material");
            // #18/#20: PHYS_* ImpactEvents (Impact/Crack/Break/JointSnap/CollapseStart/End with pieceIds) on AnimEventBus.
            // Added after Begin so it binds to the SimRunner that Begin created. Presentation only; never read by the sim.
            if (Crashverse.Sim.ImpactEventEmitter.Instance == null) levelGo.AddComponent<Crashverse.Sim.ImpactEventEmitter>();
            var feel = levelGo.AddComponent<Crashverse.Feel.FeelDirector>(); feel.Bind(s, RunCamera, aim); feel.BindWrecker(wb); // 01 cannon parts
            RunBuilt?.Invoke(_runRoot, s, wb, ctx);
            RenderMaterials.FixAll(_runRoot);
            AppServices.Router.Push(new HudScreen(s));
#if CRASHVERSE_DEV
            // BUG-24 automation: dev-only aim hook (inert without the cv_dev_aim launch extra; never in ranked).
            Crashverse.DevHooks.DevAimHook.Attach(levelGo, new Crashverse.DevHooks.DevAimTarget
            {
                maxYaw = Weapons.WreckerSpec.MaxYaw, minPitch = Weapons.WreckerSpec.MinElevation, maxPitch = Weapons.WreckerSpec.MaxElevation,
                isRanked = () => s.Ctx != null && s.Ctx.mode == RunMode.TitanRanked,
                readyToFire = () => s.State == SessionState.Aiming && s.Gate.Ready && s.ShotsLeft > 0,
                setAim = (y, p) => aim.SetAim(y, p),
                fire = () => s.TryFire(),
                markNonRanked = () => { s.DevHookUsed = true; if (s.Ctx.mode == RunMode.TitanRanked) s.Ctx.mode = RunMode.TitanPractice; },
            });
#endif
        }

        /// <summary>World bounds of the level's pieces as built (before any shot), or a 6 m box at the base if there are none.</summary>
        /// <summary>Samples of the level's hint arc (ghost) from the muzzle to the structure, so the locked camera keeps it below the HUD
        /// (L10's arc used to run under it). Same launch model as AimController (yaw +z = left, elevation about the barrel).</summary>
        public static Vector3[] HintArcPoints(GameSession s)
        {
            var lvl = s?.Ctx?.level; if (lvl == null || !lvl.HasHint || s.aim == null) return null;
            var start = s.aim.projectileSpawn != null ? s.aim.projectileSpawn.position : s.aim.transform.position + Vector3.up * 2f;
            float yaw = lvl.hintYaw * Mathf.Deg2Rad, el = lvl.hintPitch * Mathf.Deg2Rad, v = s.aim.launchSpeed;
            var vel = new Vector3(Mathf.Cos(el) * Mathf.Cos(yaw), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(yaw)) * v;
            var g = Physics.gravity; float endX = s.structureBase.x + 2f;
            var pts = new System.Collections.Generic.List<Vector3>();
            for (int i = 1; i <= 60; i++)
            {
                float t = i * 0.05f; var p = start + vel * t + g * (0.5f * t * t);
                pts.Add(p); if (p.x > endX || p.y < 0f) break;
            }
            return pts.ToArray();
        }

        public static Bounds StructureBounds(GameSession s, Vector3 fallbackBase)
        {
            bool any = false; var b = new Bounds(fallbackBase + Vector3.up * 3f, new Vector3(6f, 6f, 6f));
            if (s != null && s.builder != null)
                foreach (var blk in s.builder.Blocks)
                {
                    if (blk == null) continue;
                    // collider bounds: the box is the block (its cube renderer is disabled when RealKit dresses it)
                    var c = blk.GetComponent<Collider>(); var r = blk.GetComponent<Renderer>();
                    Bounds bb; if (c != null && c.enabled) bb = c.bounds; else if (r != null) bb = r.bounds; else continue;
                    if (!any) { b = bb; any = true; } else b.Encapsulate(bb);
                }
            return b;
        }

        public static void EndRun()
        {
            if (PlayerPrefs.HasKey(ResumeKey)) { PlayerPrefs.DeleteKey(ResumeKey); PlayerPrefs.Save(); }
            if (_runRoot != null) Object.Destroy(_runRoot);
            Time.timeScale = 1f;
            _runRoot = null; Session = null;
            if (RunCamera != null) RunCamera.enabled = false;
            MovingPart.GlobalSpeedScale = 1f;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
        }

        public static void ExitToMap() { ShowMap(); }
    }
}
