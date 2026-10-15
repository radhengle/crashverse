using Crashverse.Destruction;
using Crashverse.Level;
using Crashverse.Weapons;
using UnityEngine;

namespace Crashverse.Core
{
    public enum SessionState
    {
        Playing,
        Settling,
        Won,
        Failed
    }

    /// <summary>
    /// Owns move budget, win/fail/retry per Contract §4–§5.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public LevelConfig config;
        public DestructionTracker tracker;
        public FireController fire;
        public LevelBuilder builder;
        public StructuralIntegrity integrity;

        public SessionState State { get; private set; } = SessionState.Playing;
        public int MovesRemaining { get; private set; }
        public int LastStars { get; private set; }
        public int BestStars { get; private set; }

        public event System.Action StateChanged;
        public event System.Action HudDirty;

        Vector3 _towerBase;

        public void Begin(LevelConfig cfg, Vector3 towerBase)
        {
            config = cfg;
            _towerBase = towerBase;
            BestStars = StarProgressStore.GetBest(cfg.levelId);
            Retry();
        }

        public void Retry()
        {
            DebrisPool.Instance?.ClearAll();
            builder.Clear();
            builder.BuildClockworkTower(_towerBase);
            tracker.UnregisterAll();
            tracker.RegisterStructure(builder.Blocks);
            var arr = new Crashverse.Destruction.DestructibleBlock[builder.Blocks.Count];
            for (int i = 0; i < builder.Blocks.Count; i++) arr[i] = builder.Blocks[i];
            integrity.Bind(arr);

            MovesRemaining = config.moveBudget;
            LastStars = 0;
            State = SessionState.Playing;
            fire.CanFire = true;
            fire.Fired -= OnFired;
            fire.Fired += OnFired;
            tracker.Settled -= OnSettled;
            tracker.Settled += OnSettled;
            tracker.RatioChanged -= OnRatio;
            tracker.RatioChanged += OnRatio;
            StateChanged?.Invoke();
            HudDirty?.Invoke();
        }

        void OnFired()
        {
            if (State != SessionState.Playing) return;
            MovesRemaining = MovesRemaining > 0 ? MovesRemaining - 1 : 0;
            State = SessionState.Settling;
            fire.CanFire = false;
            HudDirty?.Invoke();
            StateChanged?.Invoke();
            // If already settled with no motion, settle event may need nudge
            Invoke(nameof(NudgeSettleCheck), 0.85f);
        }

        void NudgeSettleCheck()
        {
            if (State != SessionState.Settling) return;
            if (tracker.IsSettled) OnSettled();
        }

        void OnRatio(float _)
        {
            HudDirty?.Invoke();
        }

        void OnSettled()
        {
            if (State != SessionState.Settling && State != SessionState.Playing) return;

            float ratio = tracker.DestroyedRatio;
            bool win = ratio >= config.winThreshold;

            if (win)
            {
                State = SessionState.Won;
                LastStars = config.ComputeStars(true, MovesRemaining);
                StarProgressStore.Submit(config.levelId, LastStars);
                BestStars = StarProgressStore.GetBest(config.levelId);
                fire.CanFire = false;
            }
            else if (MovesRemaining <= 0)
            {
                State = SessionState.Failed;
                LastStars = 0;
                fire.CanFire = false;
            }
            else
            {
                State = SessionState.Playing;
                fire.CanFire = true;
            }
            HudDirty?.Invoke();
            StateChanged?.Invoke();
        }

        public void GiveUp()
        {
            if (State == SessionState.Won) return;
            State = SessionState.Failed;
            LastStars = 0;
            fire.CanFire = false;
            HudDirty?.Invoke();
            StateChanged?.Invoke();
        }
    }
}
