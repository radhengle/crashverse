using Crashverse.Core;
using Crashverse.Weapons;
using UnityEngine;

namespace Crashverse.UI
{
    /// <summary>
    /// Minimal runtime HUD (IMGUI) — shots, % destroyed, stars, fire, retry.
    /// Art/UI can replace with uGUI prefabs later; this satisfies M1 playability.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        GameSession _session;
        FireController _fire;
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _btn;
        bool _stylesReady;

        public void Bind(GameSession session, FireController fire)
        {
            _session = session;
            _fire = fire;
            _session.HudDirty += () => { };
            _session.StateChanged += () => { };
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Hex("E8B04B") }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                normal = { textColor = Color.white }
            };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            _stylesReady = true;
        }

        void OnGUI()
        {
            if (_session == null || _session.config == null) return;
            EnsureStyles();

            float pad = 16f;
            GUI.Box(new Rect(pad, pad, 340, 150), GUIContent.none);
            GUI.Label(new Rect(pad + 12, pad + 8, 320, 32), "CRASHVERSE", _title);
            GUI.Label(new Rect(pad + 12, pad + 44, 320, 28),
                $"{_session.config.displayName}  |  Shots {_session.MovesRemaining}/{_session.config.moveBudget}", _body);

            float pct = _session.tracker != null ? _session.tracker.DestroyedRatio * 100f : 0f;
            GUI.Label(new Rect(pad + 12, pad + 74, 320, 28),
                $"Destroyed {pct:0.0}%  (win ≥ {_session.config.winThreshold * 100f:0}%)", _body);

            string stars = StarString(_session.State == SessionState.Won ? _session.LastStars : 0);
            string best = StarString(_session.BestStars);
            GUI.Label(new Rect(pad + 12, pad + 104, 320, 28),
                $"Stars {stars}   Best {best}", _body);

            // Fire button — lower right
            float bw = 160f, bh = 80f;
            Rect fireRect = new Rect(Screen.width - bw - pad, Screen.height - bh - pad, bw, bh);
            GUI.enabled = _session.State == SessionState.Playing && _session.MovesRemaining > 0;
            if (GUI.Button(fireRect, "FIRE", _btn))
                _fire?.Fire();
            GUI.enabled = true;

            // Quality toggle
            if (GUI.Button(new Rect(Screen.width - 200 - pad, pad, 200, 48), "Quality Mid/Low", _btn))
                Perf.QualityController.Toggle();

            if (_session.State == SessionState.Won || _session.State == SessionState.Failed)
            {
                float w = 420f, h = 220f;
                Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
                GUI.Box(panel, GUIContent.none);
                string headline = _session.State == SessionState.Won ? "TOWER DOWN!" : "OUT OF SHOTS";
                GUI.Label(new Rect(panel.x + 24, panel.y + 24, w - 48, 40), headline, _title);
                if (_session.State == SessionState.Won)
                    GUI.Label(new Rect(panel.x + 24, panel.y + 70, w - 48, 30),
                        $"Clear with shots to spare: {StarString(_session.LastStars)}", _body);
                else
                    GUI.Label(new Rect(panel.x + 24, panel.y + 70, w - 48, 30),
                        "Retry restores full budget. No ads in M1.", _body);

                if (GUI.Button(new Rect(panel.x + 24, panel.y + 130, w - 48, 60), "RETRY", _btn))
                    _session.Retry();
            }

            // Give up
            if (_session.State == SessionState.Playing || _session.State == SessionState.Settling)
            {
                if (GUI.Button(new Rect(pad, Screen.height - 56 - pad, 140, 56), "Give Up", _btn))
                    _session.GiveUp();
            }
        }

        static string StarString(int n)
        {
            n = Mathf.Clamp(n, 0, 3);
            return new string('★', n) + new string('☆', 3 - n);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
