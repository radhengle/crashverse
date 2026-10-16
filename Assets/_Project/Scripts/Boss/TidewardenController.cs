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
        public int Phase { get; private set; } = 1;
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
                    case "shutter": _shutter = x; _shutterClosedPos = x.transform.position; break;
                }
            }
            SetShutter(false); // phase 1: chest is covered by armour; shutter parked closed
        }

        void CheckPhase()
        {
            if (Phase != 1) return;
            foreach (var s in _shoulders) if (!s.IsBroken) return;
            Phase = 2;
            foreach (var a in _armor) if (!a.IsBroken) a.Break(); // chest opens
            SetShutter(true); // SG-05: phase 2 opens with the shutter open
            Changed?.Invoke();
        }

        void OnCoreDamaged(DestructibleBlock b, float _, bool direct)
        {
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
            if (ShutterOpen) { _tellTimer = TellSeconds; return TellSeconds; } // glow, then close
            SetShutter(true);
            return 0f;
        }

        void Update()
        {
            if (_tellTimer <= 0f || _shutter == null) return;
            _tellTimer -= Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
            _shutter.SetHighlight(MaterialDefinition.Hex("FF7A1A"), 0.6f + 0.4f * pulse);
            if (_tellTimer <= 0f) { _shutter.ClearHighlight(); SetShutter(false); }
        }

        void SetShutter(bool open)
        {
            ShutterOpen = open;
            if (_shutter != null) _shutter.transform.position = _shutterClosedPos + (open ? new Vector3(0f, 1.7f, 0f) : Vector3.zero);
            Changed?.Invoke();
        }
    }
}
