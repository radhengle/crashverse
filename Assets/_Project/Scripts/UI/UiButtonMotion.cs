using Crashverse.Feel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crashverse.UI
{
    /// <summary>
    /// 06 button press: scale 1 → 0.92 on press (120 ms out-quad), 1.05 overshoot → 1 on release (120 ms; Reduce Motion: no overshoot).
    /// Raises UI_BTN_PRESS with payload type confirm / back / error (error = tapping a disabled button, no scale). Unscaled time,
    /// no allocation; idle buttons return immediately from Update.
    /// </summary>
    public class UiButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public const string TypeConfirm = "confirm", TypeBack = "back", TypeError = "error";
        public string pressType = TypeConfirm;
        Button _button; Transform _t; float _ms = -1f; bool _down; float _fromScale = 1f;

        void Awake() { _button = GetComponent<Button>(); _t = transform; }

        public void OnPointerDown(PointerEventData e)
        {
            if (_button != null && !_button.interactable) { AnimEventBus.Raise(AnimEvents.UiBtnPress, TypeError); return; }
            AnimEventBus.Raise(AnimEvents.UiBtnPress, pressType);
            _down = true; _ms = 0f;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down) return;
            _down = false; _ms = 0f; _fromScale = _t != null ? _t.localScale.x : 1f;
        }

        void OnDisable() { _down = false; _ms = -1f; if (_t != null) _t.localScale = Vector3.one; }

        void Update()
        {
            if (_ms < 0f || _t == null) return;
            _ms += Time.unscaledDeltaTime * 1000f;
            float s;
            if (_down) s = UiMotionMath.PressScale(_ms); // holds at 0.92 after 120 ms
            else
            {
                bool rm = MotionSettings.Current.ReduceMotion;
                // release starts from wherever the press got to (quick taps never reach 0.92)
                float k = (ButtonMotion.ReleaseScale(_ms, rm) - ButtonMotion.Pressed) / (1f - ButtonMotion.Pressed);
                s = _fromScale + (1f - _fromScale) * k;
                if (_ms >= ButtonMotion.Ms) { s = 1f; _ms = -1f; }
            }
            _t.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>UI_BTN_PRESS type from the button's role (close/back/map buttons sound as "back").</summary>
        public static string TypeForLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return TypeConfirm;
            var l = label.Trim();
            return l == "Close" || l == "Back" || l == "Map" || l == "X" || l == "✕" || l == "◀" || l.StartsWith("Back ") ? TypeBack : TypeConfirm;
        }
    }
}
