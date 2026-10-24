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
        object _doneLogged; float _resultsT, _lastYaw = 9999f; // not NaN: NaN never compares > 0.5, so nothing was logged
        void Awake() { _r = GetComponent<UIRouter>(); Debug.Log($"CVTAP|META|screen|{Screen.width}|{Screen.height}|dpi={Screen.dpi}"); }
        void LateUpdate()
        {
            if ((_t += Time.unscaledDeltaTime) < 0.5f) return; _t = 0f;
            var top = _r != null ? _r.Top : null;
            // QA C-1/C-3: one line once the results sequence is final, so the smoke screenshots the settled panel and can assert score/stars
            // QA LOW: real-touch swipe direction check in the smoke: log the TARGET yaw on the HUD whenever it moves
            if (top is HudScreen) { var ses = Crashverse.Core.GameplayDirector.Session; if (ses != null && ses.aim != null && System.Math.Abs(ses.aim.Yaw - _lastYaw) > 0.5f) { _lastYaw = ses.aim.Yaw; Debug.Log($"CVTAP|AIM|yaw={ses.aim.Yaw:F1}"); } }
            // Results are static and the smoke clears logcat before each tap: re-dump them every 2 s so a clear can't swallow the
            // only RESULT/Retry lines (API 36 calibration, run 40072849533). RESULTDONE rides along once the sequence is final.
            bool resultsRepeat = false;
            if (top is ResultsScreen) { if ((_resultsT += 0.5f) >= 2f) { _resultsT = 0f; resultsRepeat = true; } } else _resultsT = 0f;
            if (top is ResultsScreen done && done.SettledOnScreen && (resultsRepeat || !ReferenceEquals(done, _doneLogged))) { _doneLogged = done; Debug.Log($"CVTAP|RESULTDONE|{(done.Won ? "win" : "fail")}|score={done.Score}|stars={done.Stars}|pct={Mathf.FloorToInt(done.Ratio * 100)}|label={done.ScoreLabel.Replace(' ', '_').Replace('|', '/')}"); } var btns = GetComponentsInChildren<Button>(false);
            int sig = btns.Length; // changes on screen switch, enable/disable and scroll
            foreach (var b in btns) { var q = b.transform.position; sig = unchecked(sig * 31 + (b.IsInteractable() ? 1 : 0) + Mathf.RoundToInt(q.x) * 7 + Mathf.RoundToInt(q.y) * 13); }
            if (ReferenceEquals(top, _lastTop) && sig == _lastCount && !resultsRepeat) return;
            _lastTop = top; _lastCount = sig;
            Canvas.ForceUpdateCanvases(); // log settled layout, not the pre-layout frame
            string name = top != null ? top.GetType().Name : "none";
            Debug.Log("CVTAP|BEGIN|" + name);
            if (top is ResultsScreen rs) Debug.Log("CVTAP|RESULT|" + (rs.Won ? "win" : "fail"));
            foreach (var b in btns)
            {
                var rt = (RectTransform)b.transform; var p = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                if (p.x < 0 || p.y < 0 || p.x > Screen.width || p.y > Screen.height) continue; // off screen
                var sr = b.GetComponentInParent<ScrollRect>(); // scrolled out of a list's viewport (masked)
                if (sr != null && !RectTransformUtility.RectangleContainsScreenPoint(sr.viewport != null ? sr.viewport : (RectTransform)sr.transform, p, null)) continue;
                Debug.Log($"CVTAP|{name}|{b.name}|{Mathf.RoundToInt(p.x)}|{Mathf.RoundToInt(Screen.height - p.y)}|{(b.IsInteractable() ? 1 : 0)}");
            }
        }
    }
}
#endif
