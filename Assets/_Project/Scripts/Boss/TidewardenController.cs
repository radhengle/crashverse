using System.Collections.Generic;
using Crashverse.Destruction;
using Crashverse.Level;
using UnityEngine;

namespace Crashverse.Boss
{
    /// <summary>
    /// L10 Guardian (DP §1, SG-05). Phase 1: wood armour over 2 steel shoulder joints; breaking both drops the arms and
    /// opens the chest. Phase 2: core exposed, shutter starts OPEN and toggles after every shot (open, closed, open...).
    /// Tell: the shutter glows orange for 1s before it closes. Core needs 2 direct hits while open. Shots into the closed
    /// shutter hit indestructible brick (no core damage). All rules readable from visuals; no hidden HP.
    /// </summary>
    public class TidewardenController : MonoBehaviour
    {
        public const float TellSeconds = 1f;
        public const int CoreHitsToWin = 2;
        public int Phase => _rule.Phase;
        readonly Crashverse.Meta.TideShutterRule _rule = new Crashverse.Meta.TideShutterRule();
        public int ClosedShutterHits => _rule.ClosedShutterHits;
        /// <summary>GameSession calls this when a shot is actually fired (before it resolves).</summary>
        public void OnShotFired() => _rule.OnShotFired();
        public bool ShutterOpen { get; private set; }
        public int CoreHits { get; private set; }
        public bool Defeated { get; private set; }
        public bool InTransition => _tellTimer > 0f;
        public event System.Action Changed;

        readonly List<DestructibleBlock> _shoulders = new List<DestructibleBlock>();
        readonly List<DestructibleBlock> _armor = new List<DestructibleBlock>();
        readonly List<DestructibleBlock> _all = new List<DestructibleBlock>();
        DestructibleBlock _core, _shutter;
        Vector3 _shutterClosedPos;
        float _tellTimer;

        public void Bind(LevelBuilder b)
        {
            foreach (var x in b.Blocks)
            {
                _all.Add(x);
                switch (x.role)
                {
                    case "shoulder": _shoulders.Add(x); x.Broken += _ => CheckPhase(); break;
                    case "armor": _armor.Add(x); break;
                    case "core": _core = x; x.Damaged += OnCoreDamaged; break;
                    case "shutter": _shutter = x; _shutterClosedPos = x.transform.position; x.Damaged += OnShutterDamaged; break;
                }
            }
            SetShutter(false); // phase 1: chest is covered by armour; shutter parked closed
        }

        void CheckPhase()
        {
            if (Phase != 1) return;
            foreach (var s in _shoulders) if (!s.IsBroken) return;
            _rule.EnterPhase2();
            foreach (var a in _armor) if (!a.IsBroken) a.Break(); // chest opens
            Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.BossTidePhase2); // music: stinger at bar end → P2
            SetShutter(true); // SG-05: phase 2 opens with the shutter open
            Changed?.Invoke();
        }

        void OnShutterDamaged(DestructibleBlock b, float _, bool direct) { if (direct) { _rule.OnDirectHit(); Changed?.Invoke(); } }

        void OnCoreDamaged(DestructibleBlock b, float _, bool direct)
        {
            if (direct && Phase == 2 && !ShutterOpen) _rule.OnDirectHit();
            if (!direct || Phase != 2 || !ShutterOpen || Defeated) return;
            CoreHits++;
            if (CoreHits >= CoreHitsToWin) Defeat();
            Changed?.Invoke();
        }

        void Defeat()
        {
            Defeated = true;
            foreach (var x in _all) if (x != _shutter && x.isObjectivePiece && !x.IsBroken) x.ForceCollapse(); // collapse cinematic
            if (_core != null) _core.SetHighlight(Color.white, 1f);
        }

        /// <summary>Called by GameSession after each shot resolves in phase 2. Returns the tell duration to wait (0 if opening).</summary>
        public float OnShotResolved()
        {
            if (Phase != 2 || Defeated) return 0f;
            if (!_rule.OnShotResolved()) return 0f; // SG-05: the shot that ended phase 1 doesn't toggle the shutter
            if (!_rule.Open) { _tellTimer = TellSeconds; _tellT = 0f; Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.BossTideTell); return TellSeconds + Crashverse.Feel.TideShutter.CloseMs / 1000f; } // tell 1 s, then 300 ms close tween
            SetShutter(true);
            return 0f;
        }

        float _tellT, _tweenMs = -1f; bool _tweenOpening;

        void Update()
        {
            if (_shutter == null) return;
            bool rm = Crashverse.Feel.MotionSettings.Current.ReduceMotion;
            if (_tellTimer > 0f)
            {
                _tellTimer -= Time.deltaTime; _tellT += Time.deltaTime;
                _shutter.SetHighlight(MaterialDefinition.Hex("FF7A1A"), Crashverse.Feel.TideShutter.TellIntensity(_tellT, rm)); // exactly 2 Hz
                if (_tellTimer <= 0f) { _shutter.ClearHighlight(); SetShutter(false); }
            }
            if (_tweenMs >= 0f)
            {
                _tweenMs += Time.deltaTime * 1000f;
                _shutter.transform.position = _shutterClosedPos + new Vector3(0f, Crashverse.Feel.TideShutter.Height(_tweenOpening, _tweenMs, rm), 0f);
                if (Crashverse.Feel.TideShutter.Done(_tweenOpening, _tweenMs))
                {
                    _tweenMs = -1f;
                    if (!_tweenOpening) Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.BossTideShutterClose); // thunk at end of 300 ms slam
                }
            }
        }

        /// <summary>Gameplay state flips immediately; the visual pose tweens (no teleport). Phase 1 parks it closed without a tween.</summary>
        void SetShutter(bool open)
        {
            bool tween = Phase == 2 && open != ShutterOpen;
            ShutterOpen = open;
            if (_shutter != null)
            {
                if (tween) { _tweenOpening = open; _tweenMs = 0f; if (open) Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.BossTideShutterOpen); }
                else _shutter.transform.position = _shutterClosedPos + (open ? new Vector3(0f, Crashverse.Feel.TideShutter.Travel, 0f) : Vector3.zero);
            }
            Changed?.Invoke();
        }
    }
}
