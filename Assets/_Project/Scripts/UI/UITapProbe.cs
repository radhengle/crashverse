#if CRASHVERSE_DEV || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;

namespace Crashverse.UI
{
    /// <summary>
    /// Dev/QA only: logs the screen centre of every active button when the top screen changes, in Android `input tap`
    /// coordinates (origin top-left), so the emulator smoke script can tap the real layout. Lines: "CVTAP|Screen|Button_X|x|y|interactable".
    /// Never compiled into release builds.
    /// </summary>
    public class UITapProbe : MonoBehaviour
    {
        UIRouter _r; object _lastTop; int _lastCount; float _t;
        void Awake() { _r = GetComponent<UIRouter>(); Debug.Log($"CVTAP|META|screen|{Screen.width}|{Screen.height}|dpi={Screen.dpi}"); }
        void LateUpdate()
        {
            if ((_t += Time.unscaledDeltaTime) < 0.5f) return; _t = 0f;
            var top = _r != null ? _r.Top : null; var btns = GetComponentsInChildren<Button>(false);
            int sig = btns.Length; // changes on screen switch, enable/disable and scroll
            foreach (var b in btns) { var q = b.transform.position; sig = unchecked(sig * 31 + (b.IsInteractable() ? 1 : 0) + Mathf.RoundToInt(q.x) * 7 + Mathf.RoundToInt(q.y) * 13); }
            if (ReferenceEquals(top, _lastTop) && sig == _lastCount) return;
            _lastTop = top; _lastCount = sig;
            string name = top != null ? top.GetType().Name : "none";
            Debug.Log("CVTAP|BEGIN|" + name);
            foreach (var b in btns)
            {
                var rt = (RectTransform)b.transform; var p = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                if (p.x < 0 || p.y < 0 || p.x > Screen.width || p.y > Screen.height) continue; // scrolled out of view
                Debug.Log($"CVTAP|{name}|{b.name}|{Mathf.RoundToInt(p.x)}|{Mathf.RoundToInt(Screen.height - p.y)}|{(b.IsInteractable() ? 1 : 0)}");
            }
        }
    }
}
#endif
