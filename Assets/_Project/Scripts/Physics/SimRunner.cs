using System;
using Crashverse.Backend;
using UnityEngine;

namespace Crashverse.Sim
{
    /// <summary>
    /// Owns the sim clock. One FixedUpdate = one tick at 0.02 s. Per tick: (1) apply queued InputEvents for this tick in order,
    /// (2) raise Ticked (gameplay logic such as reload gating), (3) in ranked runs step PhysX via Physics.Simulate(0.02f).
    /// Ranked runs switch Physics.simulationMode to Script so nothing else advances physics. RNG is SimRng seeded from the
    /// attempt seed (AttemptToken.seed). Sim events for audio/anim go through Crashverse.Feel.AnimEventBus, never back into the sim.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SimRunner : MonoBehaviour
    {
        public const float Step = SimTime.Step;
        public static SimRunner Instance { get; private set; }

        /// <summary>Index of the next tick to simulate; 0 at BeginRun. Inputs recorded "now" carry this tick.</summary>
        public int Tick { get; private set; }
        public bool Running { get; private set; }
        public bool Ranked { get; private set; }
        public long Seed { get; private set; }
        public SimRng Rng { get; private set; } = new SimRng(0);
        /// <summary>Incremented by every BeginRun (fresh start and Retry), so systems can reset per run without guessing.</summary>
        public int RunId { get; private set; }
        /// <summary>Raised at the end of BeginRun (fresh start and Retry).</summary>
        public static event Action<SimRunner> RunStarted;

        /// <summary>Called for each input as it is applied, before physics for that tick.</summary>
        public event Action<int, InputEvent> InputApplied;
        /// <summary>Called first each tick: apply contact results gathered during the previous step (stable order).</summary>
        public event Action<int> ContactsFlush;
        /// <summary>Called once per tick after inputs, before Physics.Simulate.</summary>
        public event Action<int> Ticked;
        /// <summary>Called once per tick after Physics.Simulate (collision callbacks for this tick have run). Used to flush
        /// ImpactEvents in deterministic order. Presentation listeners must never write back into the sim.</summary>
        public event Action<int> Stepped;
        /// <summary>Raised first in each tick body, before inputs. Non-ranked impact events from the previous step flush here.</summary>
        public event Action<int> TickBegin;
        /// <summary>True while SimRunner's FixedUpdate tick body runs (ranked Physics.Simulate happens inside it).</summary>
        public bool InTick { get; private set; }

        readonly TickInputQueue<InputEvent> _inputs = new TickInputQueue<InputEvent>();
        SimulationMode _prevMode = SimulationMode.FixedUpdate;
        bool _ownsMode;

        public static SimRunner Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            var existing = host.GetComponent<SimRunner>();
            return existing != null ? existing : host.AddComponent<SimRunner>(); // no ?? on UnityEngine.Object
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            RestoreMode();
            if (Instance == this) Instance = null;
        }

        /// <summary>Start (or restart on Retry) a run. Clears inputs, resets tick and RNG.</summary>
        public void BeginRun(long seed, bool ranked)
        {
            Seed = seed; Ranked = ranked; Tick = 0; Running = true; RunId++;
            if (ImpactEventEmitter.Instance != null) ImpactEventEmitter.Instance.ResetRun();
            Rng = new SimRng(seed);
            _inputs.Clear();
            Time.fixedDeltaTime = Step;
            if (ranked)
            {
                if (!_ownsMode) { _prevMode = Physics.simulationMode; _ownsMode = true; }
                Physics.simulationMode = SimulationMode.Script;
                SimLayers.ApplyRankedMatrix();
            }
            else RestoreMode();
            RunStarted?.Invoke(this);
        }

        public void EndRun() { Running = false; RestoreMode(); }

        /// <summary>Queue an input for a future tick (live play uses Tick; replay uses the recorded tick).</summary>
        public bool Enqueue(InputEvent e) => e != null && _inputs.Enqueue(e.tick, e);

        void FixedUpdate()
        {
            if (!Running) return;
            int t = Tick;
            InTick = true;
            TickBegin?.Invoke(t);      // non-ranked: impact events from the previous auto-sim step (tagged t-1)
            ContactsFlush?.Invoke(t);  // debris damage queued from the previous step; resulting Crack/Break tagged t
            _inputs.Drain(t, (tick, e) => InputApplied?.Invoke(tick, e));
            Ticked?.Invoke(t);
            if (Ranked) Physics.Simulate(Step);
            Stepped?.Invoke(t);
            Tick = t + 1;
            InTick = false;
        }

        void RestoreMode()
        {
            if (!_ownsMode) return;
            Physics.simulationMode = _prevMode;
            _ownsMode = false;
        }
    }

    /// <summary>UnityEngine helpers over SimRng (kept out of the engine-free core).</summary>
    public static class SimRngUnity
    {
        public static Vector3 InsideUnitSphere(this SimRng r)
        {
            for (int i = 0; i < 32; i++)
            {
                var v = new Vector3(r.NextSigned(), r.NextSigned(), r.NextSigned());
                if (v.sqrMagnitude <= 1f) return v;
            }
            return Vector3.zero;
        }

        public static Quaternion Rotation(this SimRng r) =>
            Quaternion.Euler(r.NextFloat01() * 360f, r.NextFloat01() * 360f, r.NextFloat01() * 360f);
    }
}
