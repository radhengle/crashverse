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
            AppServices.Theme = UITheme.Load();
            AppServices.Router = UIRouter.Create(AppServices.Theme);
            if (Camera.main == null) { var c = new GameObject("Main Camera"); c.tag = "MainCamera"; c.AddComponent<Camera>(); c.AddComponent<AudioListener>(); }

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
            // Security Review §1: release builds never fall back to the local stub.
            if (BackendLocator.Current == null) throw new System.InvalidOperationException("Release build has no server backend (BackendLocator.Current unset); local fallback is dev-only.");
            AppServices.Backend = BackendLocator.Current;
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

        public static void ShowMap()
        {
            EndRun();
            if (_mapRoot == null)
            {
                _mapRoot = new GameObject("MapWorld");
                Map = _mapRoot.AddComponent<WorldMapBuilder>();
                Map.Build(AppServices.Content.Chapter1);
            }
            _mapRoot.SetActive(true);
            Crashverse.Audio.MusicPlayer.Ensure().PlayBase("sunnyville_map_loop");
            Map.FocusLevel(AppServices.Save.Data.lastPlayedLevel);
            var r = AppServices.Router;
            if (r.Top is MapScreen) return;
            r.PopAllAndPush(new MapScreen());
        }

        public static void StartRun(RunContext ctx)
        {
            EndRun();
            if (_mapRoot != null) _mapRoot.SetActive(false);
            _runRoot = new GameObject("Run");
            var arena = new GameObject("Arena").AddComponent<ArenaBuilder>();
            arena.transform.SetParent(_runRoot.transform);
            arena.Build();
            new GameObject("DebrisPool").AddComponent<DebrisPool>().transform.SetParent(_runRoot.transform);

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
            // #18/#20: PHYS_* ImpactEvents (Impact/Crack/Break/JointSnap/CollapseStart/End with pieceIds) on AnimEventBus.
            // Added after Begin so it binds to the SimRunner that Begin created. Presentation only; never read by the sim.
            if (Crashverse.Sim.ImpactEventEmitter.Instance == null) levelGo.AddComponent<Crashverse.Sim.ImpactEventEmitter>();
            levelGo.AddComponent<Crashverse.Feel.FeelDirector>().Bind(s, RunCamera, aim);
            AppServices.Router.Push(new HudScreen(s));
        }

        public static void EndRun()
        {
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
