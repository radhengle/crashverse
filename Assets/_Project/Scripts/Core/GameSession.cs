using Crashverse.Backend;
using Crashverse.Boss;
using Crashverse.Destruction;
using Crashverse.Level;
using Crashverse.Meta;
using Crashverse.PowerUps;
using Crashverse.Sim;
using Crashverse.Weapons;
using UnityEngine;

namespace Crashverse.Core
{
    public enum SessionState { Aiming, Resolving, BossTell, Won, Failed }

    /// <summary>
    /// One level attempt: shots, power-up charges, settle-gated win/fail (Contract §4), stars (SG-01/24), mastery (SG-03/04),
    /// combo + campaign score (Addendum), boss rules, Titan scoring and journal. All checks run after physics settles.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public LevelBuilder builder;
        public DestructionTracker tracker;
        public FireController fire;
        public AimController aim;
        public StructuralIntegrity integrity;
        public KillPlane killPlane;
        public Vector3 structureBase = new Vector3(10f, 0f, 0f);

        public RunContext Ctx { get; private set; }
        public SessionState State { get; private set; }
        public int ShotsLeft { get; private set; }
        public PowerUpRuntime PowerUps { get; private set; }
        public TidewardenController Boss { get; private set; }
        public RunResult Result { get; private set; }
        public bool HintVisible { get; private set; }
#if CRASHVERSE_DEV
        /// <summary>Set by the dev aim hook; such a session is never ranked (Security Admin #3).</summary>
        public bool DevHookUsed { get; set; }
#endif
        public int TitanDamageScore => Ctx != null && Ctx.titan != null && tracker.TotalObjectiveHp > 0
            ? Mathf.Min(Ctx.titan.maxDamageScore, Mathf.RoundToInt(Ctx.titan.maxDamageScore * tracker.DamageDealt / tracker.TotalObjectiveHp)) : 0;

        public event System.Action Changed;
        public event System.Action<RunResult> Ended;
        /// <summary>Power-ups v2 Shield: raised after settle when the shielded shot was refunded ("Step protected").</summary>
        public event System.Action StepProtected;
        public int ShieldRefunds { get; private set; }
        /// <summary>Daily Boss: HP shown as the share of the win threshold still standing (1 = full, 0 = defeated).</summary>
        public float BossHpFraction => Ctx != null && Ctx.dailyBoss ? Crashverse.Meta.DailyBossRules.HpFraction(tracker.DestroyedRatio, Ctx.level.winThreshold) : 1f;
        ShotModifiers _shotMods; float _ratioBeforeShot; int _bossHitsBefore; float _steerRefYaw;

        /// <summary>Content Roadmap §5: FIRE unlocks when settled AND ≥ 1.0 s since the shot; raises GAME_WRECKER_READY (reload-beat hook).</summary>
        public readonly Crashverse.Feel.ReloadGate Gate = new Crashverse.Feel.ReloadGate();
        /// <summary>Inputs for POST /score, fixed-point (1/1000) at capture; tick = physics step since Begin.</summary>
        public readonly InputRecorder Inputs = new InputRecorder();
        /// <summary>Sim tick (0 at Begin/Retry), owned by SimRunner. Never derived from Time.fixedTime.</summary>
        int Tick => _sim != null ? _sim.Tick : 0;
        SimRunner _sim;
        bool _firePending;
        Vector3 _baseGravity = new Vector3(0f, -9.81f, 0f);
        DestructibleBlock _hintBlock;
        RankedJournal _journal; System.Threading.Tasks.Task _cpTask = System.Threading.Tasks.Task.CompletedTask;

        public void Begin(RunContext ctx)
        {
            Ctx = ctx;
            if (_sim == null)
            {
                _sim = SimRunner.Ensure(gameObject);
                _sim.InputApplied += OnSimInput;
                _sim.Ticked += OnSimTick;
            }
            Physics.gravity = _baseGravity * Crashverse.Data.RuleModifiers.M(ctx.mod.gravityScale);
            Retry();
            if (ctx.mode == RunMode.TitanRanked) { _journal = new RankedJournal { token = ctx.ticket.token }; TitanRunJournal.Write(_journal); } // killed before first shot still submits 0 (SG-11)
        }

        /// <summary>Instant retry: authored layout, full shots and charges, no leftover debris (Contract §4).</summary>
        public void Retry()
        {
            if (fire != null) fire.CancelPending(); // a launch still in its 66 ms anticipation never happens
            foreach (var p in Projectile.Alive.ToArray()) if (p != null) Destroy(p.gameObject);
            Projectile.Alive.Clear();
            builder.Build(Ctx.layout, structureBase, Ctx.mod);
            var arr = new DestructibleBlock[builder.Blocks.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = builder.Blocks[i];
            tracker.Register(arr);
            integrity.Bind(arr);
            if (killPlane != null) killPlane.Bind(arr);
            if (Boss != null) Destroy(Boss);
            Boss = null;
            if (Ctx.level.IsBoss) { Boss = gameObject.AddComponent<TidewardenController>(); Boss.Bind(builder); Boss.Changed += () => Changed?.Invoke(); }
            var mp = Crashverse.Audio.MusicPlayer.Ensure();
            if (Ctx.titan != null) mp.PlayTitan(Ctx.titan.arenaId); else if (Boss != null) mp.PlayBase("tidewarden_p1"); else mp.PlayBase("gameplay_loop");
            PowerUps = new PowerUpRuntime(Ctx.loadout, AppServices.Content, Ctx.extraChargeSlot);
            PowerUps.Changed += () => Changed?.Invoke();
            ShotsLeft = Ctx.ShotBudget;
            Gate.Reset(); Inputs.Clear(); _firePending = false; _shotMods = null; ShieldRefunds = 0;
            _sim.BeginRun(Ctx.seed, Ctx.mode == RunMode.TitanRanked);
            Result = null;
            State = SessionState.Aiming;
            fire.CanFire = true;
            aim.inputEnabled = true;
            aim.SetAim(0f, WreckerSpec.RestElevation);
            aim.SnapVisualNow(); // deterministic reset: no barrel catch-up after Retry
            SetupHint();
            Changed?.Invoke();
        }

        void SetupHint()
        {
            HintVisible = false;
            if (_hintBlock != null) _hintBlock.ClearHighlight();
            _hintBlock = null;
            if (!Ctx.hintArmed || !Ctx.level.HasHint) return;
            Ctx.hintArmed = false; // next attempt only
            _hintBlock = builder.Find(Ctx.level.hintTarget);
            aim.ShowGhost(Ctx.level.hintYaw, Ctx.level.hintPitch);
            HintVisible = true;
        }

        /// <summary>
        /// Queues a fire input (fixed-point aim, 1/1000) for the next sim tick. The shot itself happens in OnSimInput, before
        /// physics for that tick, so live play and server replay consume the same tick-ordered InputEvents.
        /// </summary>
        public bool TryFire()
        {
            if (_sim == null || _firePending || State != SessionState.Aiming || ShotsLeft <= 0 || !Gate.Ready) return false;
            var e = new InputEvent { tick = _sim.Tick, type = "fire", x = InputRecorder.ToFixed(aim.Yaw), y = InputRecorder.ToFixed(aim.Elevation) };
            if (!_sim.Enqueue(e)) return false;
            _firePending = true;
            return true;
        }

        void OnSimInput(int tick, InputEvent e)
        {
            if (e.type != "fire") return;
            _firePending = false;
            if (State != SessionState.Aiming || ShotsLeft <= 0 || !Gate.Ready) return;
            aim.SetAim(InputRecorder.FromFixed(e.x), InputRecorder.FromFixed(e.y)); // fire from the quantized aim the server replays
            var mods = PowerUps.ConsumeForShot();
            _shotMods = mods; _ratioBeforeShot = tracker.DestroyedRatio; _bossHitsBefore = Boss != null ? Boss.CoreHits : 0; _steerRefYaw = InputRecorder.FromFixed(e.x);
            float yaw = InputRecorder.FromFixed(e.x), elev = InputRecorder.FromFixed(e.y); // frozen at the tap (quantized)
            // Anim Spec 01: launch is 66 ms after the tap (breech anticipation); record the tick of the REAL launch so a replay matches.
            if (!fire.Fire(mods, () => Inputs.Record(Tick, "fire", yaw, elev))) return;
            ShotsLeft--;
            if (Boss != null) Boss.OnShotFired();
            tracker.OnShotFired();
            Gate.OnShotFired();
            Crashverse.PowerUps.TimeFractureEffect.NotifyShotFired(); // OQ-P3: slow holds until this shot impacts
            State = SessionState.Resolving;
            fire.CanFire = false;
            if (HintVisible) { HintVisible = false; aim.HideGhost(); if (_hintBlock != null) _hintBlock.ClearHighlight(); }
            Changed?.Invoke();
        }

        /// <summary>Fire gating runs on the sim tick (fixed 0.02 s), not on render frames.</summary>
        void OnSimTick(int tick)
        {
            if (State != SessionState.Resolving) return;
            SteerGuided(tick);
            if (!Gate.Tick(SimRunner.Step, FireController.ActiveProjectiles == 0 && tracker.IsSettled)) return; // aim stays live; only FIRE waits
            ResolveShot();
        }

        void Update()
        {
            if (HintVisible && _hintBlock != null)
                _hintBlock.SetHighlight(Crashverse.Destruction.MaterialDefinition.Hex("F5C451"), 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            if (State == SessionState.BossTell)
            {
                if (Boss == null || !Boss.InTransition) { State = SessionState.Aiming; fire.CanFire = true; Changed?.Invoke(); }
                return;
            }
        }

        /// <summary>
        /// Guided Rocket (v2): for up to steerTicks after launch the player's drag (the live aim yaw) steers the rocket at up to
        /// turnRate deg/s. The per-tick turn is quantized and recorded as a "steer" InputEvent, so a replay applies the same values.
        /// </summary>
        void SteerGuided(int tick)
        {
            if (_shotMods == null || !_shotMods.guided) return;
            var p = Projectile.Latest;
            if (p == null || p.IsSpent || !p.mods.guided) return;
            float want = aim.Yaw - _steerRefYaw;
            float deg = Crashverse.Meta.PowerUpRulesV2.Quantize(Crashverse.Meta.PowerUpRulesV2.SteerForTick(want, p.TicksSinceLaunch, _shotMods.steerTicks, _shotMods.turnRate));
            if (deg == 0f) return;
            _steerRefYaw += deg;
            Inputs.Record(tick, "steer", deg, 0f);
            p.Steer(deg);
        }

        /// <summary>Shield (v2): after settle, refund the shot if it destroyed &lt; threshold of the objective (boss: &lt; 5% of max HP).</summary>
        void ResolveShield()
        {
            if (_shotMods == null || !_shotMods.shield) return;
            bool boss = Boss != null || Ctx.dailyBoss;
            float objFrac = tracker.DestroyedRatio - _ratioBeforeShot;
            float bossFrac = Boss != null ? (Boss.CoreHits - _bossHitsBefore) / (float)Crashverse.Boss.TidewardenController.CoreHitsToWin
                           : Ctx.dailyBoss ? objFrac / Mathf.Max(0.01f, Ctx.level.winThreshold) : objFrac;
            float thr = _shotMods.shieldThreshold; _shotMods = null;
            if (!Crashverse.Meta.PowerUpRulesV2.ShieldRefunds(boss, objFrac, bossFrac, thr)) return;
            ShotsLeft++; ShieldRefunds++;
            StepProtected?.Invoke();
        }

        void ResolveShot()
        {
            ResolveShield();
            bool won = Boss != null ? Boss.Defeated : WinRule.Meets(tracker.DestroyedRatio, Ctx.level.winThreshold); // BUG-17: same rule as the server
            if (Ctx.mode == RunMode.TitanRanked) CheckpointTitan(won);
            if (won) { End(true); return; }
            if (ShotsLeft <= 0) { End(false); return; }
            if (Boss != null)
            {
                float tell = Boss.OnShotResolved();
                if (tell > 0f) { State = SessionState.BossTell; Changed?.Invoke(); return; }
            }
            State = SessionState.Aiming;
            fire.CanFire = true;
            Changed?.Invoke();
        }

        /// <summary>Pause menu "Give Up" (Contract §4 fail path).</summary>
        public void GiveUp()
        {
            if (State == SessionState.Won || State == SessionState.Failed) return;
            End(false);
        }

        /// <summary>SEC-01: journal the settled shot locally, then report it; the server's receipt is stored when it arrives.</summary>
        void CheckpointTitan(bool killed)
        {
            var cp = new RankedCheckpoint { token = Ctx.ticket.token, shotIndex = Ctx.ShotBudget - ShotsLeft, damageScore = TitanDamageScore, shotsLeft = ShotsLeft, killed = killed };
            _journal.checkpoints.Add(cp);
            TitanRunJournal.Write(_journal);
            var prev = _cpTask;
            _cpTask = SendCheckpoint(prev, cp);
        }

        async System.Threading.Tasks.Task SendCheckpoint(System.Threading.Tasks.Task prev, RankedCheckpoint cp)
        {
            await prev; // keep shot order
            try
            {
                var signed = await AppServices.Backend.ReportRankedCheckpoint(cp);
                cp.receipt = signed.receipt;
                if (_journal != null) TitanRunJournal.Write(_journal);
            }
            catch (BackendException) { /* offline: stays unsigned; server checks it as an offline tail on finalize */ }
        }

        async void End(bool won)
        {
            State = won ? SessionState.Won : SessionState.Failed;
            if (_sim != null) _sim.EndRun(); // back to automatic physics for the result screen
#if CRASHVERSE_DEV
            Crashverse.DevHooks.DevAimHook.ReportDevAimResult(won); // CVTAP|RESULT|win|fail (opt-in only)
#endif
            fire.CanFire = false;
            aim.inputEnabled = false;
            var lvl = Ctx.level;
            int stars = StarRules.Compute(won, ShotsLeft, Ctx.ShotBudget, lvl.star3Pct, lvl.star2Pct,
                                          PowerUps.AnyUsed ? lvl.starCapIfPowerupUsed : 3);
            var music = Crashverse.Audio.MusicPlayer.Instance; // victory/fail stops level music (fade via Results snapshot) + stinger
            if (music != null && !(won && (Boss != null || Ctx.titan != null))) { music.StopAll(); music.PlayStinger(won ? (stars >= 3 ? "stinger_win_3star" : "stinger_win") : "stinger_fail"); }
            var stats = new RunStats { closedShutterHits = Boss != null ? Boss.ClosedShutterHits : 0, won = won, shotsUsed = Ctx.ShotBudget - ShotsLeft, shotsLeft = ShotsLeft, stars = stars,
                                       destroyedRatio = tracker.DestroyedRatio, powerUpUsed = PowerUps.AnyUsed, islandKillY = lvl.islandKillY };
            tracker.FillRoleStats(stats, builder.Blocks);
            bool mastery = MasteryEvaluator.Evaluate(lvl.mastery, stats);
            int combo = tracker.Combo.BestThisRun;
            var res = new RunResult { won = won, stars = stars, mastery = mastery, bestCombo = combo, ratio = tracker.DestroyedRatio, shotsLeft = ShotsLeft };
            Result = res;
#if CRASHVERSE_DEV
            // Security Admin #2: a run driven by the dev aim hook records and submits nothing (no save, queue, score or ranked call).
            if (DevHookUsed) { res.note = Crashverse.DevHooks.DevAimHookCore.NoSubmitNote; Changed?.Invoke(); Ended?.Invoke(res); return; }
#endif
            var backend = AppServices.Backend;
            try
            {
                switch (Ctx.mode)
                {
                    case RunMode.Campaign:
                        res.score = CampaignScore.Compute(won, res.ratio, ShotsLeft, combo, mastery);
                        res.firstClear = AppServices.Save.RecordResult(lvl.id, won, stars, mastery, res.score);
                        if (won)
                        {
                            // BUG-13/14: raw stats only, queued first; the server validates, scores, grants first-clear rewards and totals
                            CampaignResultQueue.Enqueue(new CampaignRunReport { levelId = lvl.id, won = true, destroyedRatio = res.ratio, shotsLeft = ShotsLeft, bestCombo = combo, mastery = mastery });
                            var sync = await CampaignResultQueue.Flush(backend);
                            if (CampaignResultQueue.Count > 0) res.note = "Offline: result saved and will sync to the leaderboard later";
                            else if (sync != null && !sync.accepted) res.note = sync.message;
                        }
                        break;
                    case RunMode.Event:
                        AppServices.Save.RecordHintAttempt(lvl.id, won); // BUG-08: hint streak counts in events
                        if (won) await backend.ReportEventStars(lvl.id, stars); // SG-23: best single run, server-side
                        break;
                    case RunMode.TitanRanked:
                        res.titanScore = TitanScoring.Score(TitanDamageScore, ShotsLeft, won, AppServices.Content.Titan);
                        await _cpTask;
                        _journal.final = true; TitanRunJournal.Write(_journal); // if offline, the next launch finalizes it as-is
                        res.titanSubmit = await backend.FinalizeRanked(_journal);
                        if (res.titanSubmit.status == SubmitStatus.Accepted) res.titanScore = res.titanSubmit.finalScore; // server is authoritative
                        TitanRunJournal.Clear(); _journal = null;
                        break;
                    case RunMode.TitanPractice:
                        AppServices.Save.RecordHintAttempt(lvl.id, won); // BUG-08
                        res.titanScore = TitanScoring.Score(TitanDamageScore, ShotsLeft, won, AppServices.Content.Titan);
                        res.note = "Practice run (not ranked)";
                        break;
                }
            }
            catch (BackendException e)
            {
                res.note = Ctx.mode == RunMode.TitanRanked ? "Offline: ranked result queued and will retry" : "Offline: " + e.Message;
            }
            Changed?.Invoke();
            Ended?.Invoke(res);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Ctx != null && Ctx.mode == RunMode.TitanRanked && _journal != null) TitanRunJournal.Write(_journal); // settled shots only; mid-flight damage is never journaled
        }

        void OnDestroy()
        {
            Physics.gravity = _baseGravity;
            if (_sim != null) { _sim.InputApplied -= OnSimInput; _sim.Ticked -= OnSimTick; _sim.EndRun(); }
        }
    }
}
