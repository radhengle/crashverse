#if CRASHVERSE_DEV || DEVELOPMENT_BUILD || UNITY_EDITOR
using Crashverse.Backend;
using Crashverse.Core;
using Crashverse.Data;
using Crashverse.Data.Dev;
using Crashverse.Destruction;
using Crashverse.Feel;
using Crashverse.Meta;
using Crashverse.Perf;
using Crashverse.Sim;
using Crashverse.UI;
using UnityEngine;

namespace Crashverse.DevTools.Perf
{
    /// <summary>
    /// DEV-ONLY driver for Scenes/Dev/L10_PerfFullBudget (vfx/PERF_TEST_L10.md). Boots the minimum services, starts the
    /// PerfL10Fixture run (Ch1 L10 + mixed wood/brick/steel/glass curtain, fixed seed), plays the scripted InputEvents
    /// (arm Chain Reactor at tick 25, fire at tick 50 with fixed aim/power), and logs PERFL10 lines:
    /// PERFL10_SUMMARY (per run: determinism hash of the PHYS_* stream, breaks, saturated_ms, peak_debris, materials) and
    /// PERFL10_DETERMINISM (MATCH/MISMATCH across runs). Compiled out of release players (whole file guarded); the scene is dev-build-only.
    /// Command line: -perfTier Low|Mid|High, -perfRuns N, -perfFillPowerUpLane.
    /// </summary>
    public class PerfL10Stage : MonoBehaviour
    {
        public enum TierChoice { Mid, High, Low }
        public TierChoice tier = TierChoice.Mid;
        [Tooltip("Runs (Retry between runs, same seed and inputs). 1 for on-device timing runs; 3+ for determinism checks.")]
        public int runs = 1;
        [Tooltip("Fill the 80-particle power-up lane with dummy particles (PerfDummyPowerUpLane) while power-up FX are unbuilt.")]
        public bool fillPowerUpLane = true;
        [Tooltip("Ticks after the last PHYS_COLLAPSE_END before a run is closed (150 = 3 s, matches the VFX probe window).")]
        public int tailTicks = 150;
        [Tooltip("Hard stop per run, ticks after the shot (750 = 15 s, matches the VFX probe).")]
        public int maxTicksAfterShot = 750;

        public static bool Active { get; private set; }
        public PerfL10Stats Stats { get; } = new PerfL10Stats();
        public int RunIndex { get; private set; }
        public bool Done { get; private set; }

        SimRunner _sim; GameSession _session; int _lastCollapseEndTick = -1; ulong _firstHash; bool _mismatch;

        void Awake()
        {
            Active = true;
            AutoBootstrap.SuppressForDevScene = true; // no map / age gate / backend flow in this scene
            ReadCommandLine();
            switch (tier) { case TierChoice.Low: QualityController.ApplyLow(); break; case TierChoice.High: QualityController.ApplyHigh(); break; default: QualityController.ApplyMid(); break; }
            AppServices.Content = ContentDatabase.LoadBundled();
            AppServices.Save = new CampaignSave();
            AppServices.Theme = UITheme.Load();
            AppServices.Router = UIRouter.Create(AppServices.Theme);
#if CRASHVERSE_DEV
            AppServices.Backend = BackendLocator.Current ?? new LocalBackendService(AppServices.Content);
            BackendLocator.Current = AppServices.Backend;
#endif
            if (Camera.main == null) { var c = new GameObject("Main Camera"); c.tag = "MainCamera"; c.AddComponent<Camera>(); c.AddComponent<AudioListener>(); }
            if (FindObjectOfType<QualityController>() == null) new GameObject("Quality").AddComponent<QualityController>();
            if (fillPowerUpLane) new GameObject("PerfDummyPowerUpLane").AddComponent<PerfDummyPowerUpLane>().Bind(tier == TierChoice.Low);
        }

        void Start()
        {
            var db = AppServices.Content;
            var l10 = db.GetLevel(PerfL10Fixture.BaseLevelId);
            var ctx = new RunContext
            {
                mode = RunMode.Campaign, // tier caps as shipped (Low 25 / Mid 60 / High 90); not a scored run
                level = PerfL10Fixture.Level(l10),
                layout = PerfL10Fixture.Layout(db.GetLayout(l10.layout)),
                seed = PerfL10Fixture.Seed,
            };
            ctx.loadout.Tiles[PerfL10Fixture.PowerUpSlot] = PerfL10Fixture.PowerUpId;
            AnimEventBus.Raised += OnBus;
            GameplayDirector.StartRun(ctx);
            _session = GameplayDirector.Session;
            _sim = SimRunner.Instance;
            _session.aim.launchSpeed = PerfL10Fixture.LaunchSpeed;
            _sim.InputApplied += OnInput;
            _sim.Ticked += OnTick;
            BeginRun();
        }

        void OnDestroy()
        {
            AnimEventBus.Raised -= OnBus;
            if (_sim != null) { _sim.InputApplied -= OnInput; _sim.Ticked -= OnTick; }
            Active = false;
        }

        void BeginRun()
        {
            Stats.Reset(); _lastCollapseEndTick = -1;
            foreach (var e in PerfL10Fixture.Script()) _sim.Enqueue(e); // after BeginRun (tick 0); GameSession consumes "fire"
            Debug.Log($"PERFL10_RUN_START,run={RunIndex},seed=0x{PerfL10Fixture.Seed:X16},tier={tier},blocks={_session.builder.Blocks.Count}");
        }

        void OnInput(int tick, InputEvent e)
        {
            if (e.type != PerfL10Fixture.ArmInput) return;
            var pu = _session.PowerUps;
            if (pu != null && pu.CanArm(e.x) && !pu.Slots[e.x].armed) pu.ToggleArm(e.x);
        }

        void OnBus(string name, object payload)
        {
            if (!(payload is ImpactEvent ev) || _sim == null) return;
            Stats.OnEvent(ev.tick, (int)ev.kind, ev.pieceId, ev.cascadeId, ev.materialTypeA.ToString(), PerfL10Fixture.FireTick);
            if (ev.kind == ImpactKind.CollapseEnd) _lastCollapseEndTick = ev.tick;
        }

        void OnTick(int tick)
        {
            if (Done) return;
            if (DebrisPool.Instance != null) Stats.SampleDebris(DebrisPool.Instance.ActiveCount);
            int since = tick - PerfL10Fixture.FireTick;
            bool tailDone = _lastCollapseEndTick >= 0 && tick - _lastCollapseEndTick >= tailTicks;
            if (since < 0 || !(tailDone || since >= maxTicksAfterShot)) return;
            Debug.Log(Stats.Summary(RunIndex, PerfL10Fixture.Seed, tier.ToString()));
            if (RunIndex == 0) _firstHash = Stats.Hash; else if (Stats.Hash != _firstHash) _mismatch = true;
            RunIndex++;
            if (RunIndex < runs) { _session.Retry(); _session.aim.launchSpeed = PerfL10Fixture.LaunchSpeed; BeginRun(); return; }
            Done = true;
            if (runs > 1) Debug.Log($"PERFL10_DETERMINISM,runs={runs},result={(_mismatch ? "MISMATCH" : "MATCH")},hash={_firstHash:X16}");
        }

        void ReadCommandLine()
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == "-perfTier" && i + 1 < a.Length && System.Enum.TryParse(a[i + 1], true, out TierChoice t)) tier = t;
                else if (a[i] == "-perfRuns" && i + 1 < a.Length && int.TryParse(a[i + 1], out int n)) runs = Mathf.Max(1, n);
                else if (a[i] == "-perfFillPowerUpLane") fillPowerUpLane = true;
                else if (a[i] == "-perfNoPowerUpLane") fillPowerUpLane = false;
            }
        }
    }
}
#endif
