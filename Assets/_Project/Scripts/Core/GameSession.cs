using Crashverse.Backend;
using Crashverse.Boss;
using Crashverse.Destruction;
using Crashverse.Level;
using Crashverse.Meta;
using Crashverse.PowerUps;
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
        public int TitanDamageScore => Ctx != null && Ctx.titan != null && tracker.TotalObjectiveHp > 0
            ? Mathf.Min(Ctx.titan.maxDamageScore, Mathf.RoundToInt(Ctx.titan.maxDamageScore * tracker.DamageDealt / tracker.TotalObjectiveHp)) : 0;

        public event System.Action Changed;
        public event System.Action<RunResult> Ended;

        float _sinceFire;
        Vector3 _baseGravity = new Vector3(0f, -9.81f, 0f);
        DestructibleBlock _hintBlock;
        RankedJournal _journal; System.Threading.Tasks.Task _cpTask = System.Threading.Tasks.Task.CompletedTask;

        public void Begin(RunContext ctx)
        {
            Ctx = ctx;
            Physics.gravity = _baseGravity * Crashverse.Data.RuleModifiers.M(ctx.mod.gravityScale);
            Retry();
            if (ctx.mode == RunMode.TitanRanked) { _journal = new RankedJournal { token = ctx.ticket.token }; TitanRunJournal.Write(_journal); } // killed before first shot still submits 0 (SG-11)
        }

        /// <summary>Instant retry: authored layout, full shots and charges, no leftover debris (Contract §4).</summary>
        public void Retry()
        {
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
            PowerUps = new PowerUpRuntime(Ctx.loadout, AppServices.Content, Ctx.extraChargeSlot);
            PowerUps.Changed += () => Changed?.Invoke();
            ShotsLeft = Ctx.ShotBudget;
            Result = null;
            State = SessionState.Aiming;
            fire.CanFire = true;
            aim.inputEnabled = true;
            aim.SetAim(0f, WreckerSpec.RestElevation);
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

        public bool TryFire()
        {
            if (State != SessionState.Aiming || ShotsLeft <= 0) return false;
            var mods = PowerUps.ConsumeForShot();
            if (!fire.Fire(mods)) return false;
            ShotsLeft--;
            tracker.OnShotFired();
            _sinceFire = 0f;
            State = SessionState.Resolving;
            fire.CanFire = false;
            if (HintVisible) { HintVisible = false; aim.HideGhost(); if (_hintBlock != null) _hintBlock.ClearHighlight(); }
            Changed?.Invoke();
            return true;
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
            if (State != SessionState.Resolving) return;
            _sinceFire += Time.deltaTime;
            if (_sinceFire < 0.3f || FireController.ActiveProjectiles > 0 || !tracker.IsSettled) return;
            ResolveShot();
        }

        void ResolveShot()
        {
            bool won = Boss != null ? Boss.Defeated : tracker.DestroyedRatio + 1e-5f >= Ctx.level.winThreshold;
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
            fire.CanFire = false;
            aim.inputEnabled = false;
            var lvl = Ctx.level;
            int stars = StarRules.Compute(won, ShotsLeft, Ctx.ShotBudget, lvl.star3Pct, lvl.star2Pct,
                                          PowerUps.AnyUsed ? lvl.starCapIfPowerupUsed : 3);
            var stats = new RunStats { won = won, shotsUsed = Ctx.ShotBudget - ShotsLeft, shotsLeft = ShotsLeft, stars = stars,
                                       destroyedRatio = tracker.DestroyedRatio, powerUpUsed = PowerUps.AnyUsed, islandKillY = lvl.islandKillY };
            tracker.FillRoleStats(stats, builder.Blocks);
            bool mastery = MasteryEvaluator.Evaluate(lvl.mastery, stats);
            int combo = tracker.Combo.BestThisRun;
            var res = new RunResult { won = won, stars = stars, mastery = mastery, bestCombo = combo, ratio = tracker.DestroyedRatio, shotsLeft = ShotsLeft };
            Result = res;
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
                        res.titanScore = LocalBackendService.TitanScore(TitanDamageScore, ShotsLeft, won, AppServices.Content.Titan);
                        await _cpTask;
                        _journal.final = true; TitanRunJournal.Write(_journal); // if offline, the next launch finalizes it as-is
                        res.titanSubmit = await backend.FinalizeRanked(_journal);
                        if (res.titanSubmit.status == SubmitStatus.Accepted) res.titanScore = res.titanSubmit.finalScore; // server is authoritative
                        TitanRunJournal.Clear(); _journal = null;
                        break;
                    case RunMode.TitanPractice:
                        AppServices.Save.RecordHintAttempt(lvl.id, won); // BUG-08
                        res.titanScore = LocalBackendService.TitanScore(TitanDamageScore, ShotsLeft, won, AppServices.Content.Titan);
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

        void OnDestroy() { Physics.gravity = _baseGravity; }
    }
}
