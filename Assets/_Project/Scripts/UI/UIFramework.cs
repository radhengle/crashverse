using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crashverse.UI
{
    /// <summary>
    /// QA-UI-001/002: canvas units are dp. CanvasScaler runs in constant-pixel mode with scaleFactor = Screen.dpi/160
    /// (fallback 160), so every size in UITheme is density-independent; it also shrinks if the safe area is narrower than 320dp.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class DpCanvasScaler : MonoBehaviour
    {
        public const float MinWidthDp = 320f;
        CanvasScaler _s; float _lastDpi = -1f; int _lastW;

        void Awake() { _s = GetComponent<CanvasScaler>(); _s.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; Apply(); }
        void Update() { if (Screen.dpi != _lastDpi || Screen.width != _lastW) Apply(); }

        public static float ScaleFor(float dpi, float safeWidthPx)
        {
            float s = (dpi > 1f ? dpi : 160f) / 160f;
            if (safeWidthPx / s < MinWidthDp) s = safeWidthPx / MinWidthDp; // keep a 320dp layout fully on screen
            return Mathf.Max(0.5f, s);
        }

        void Apply()
        {
            _lastDpi = Screen.dpi; _lastW = Screen.width;
            _s.scaleFactor = ScaleFor(Screen.dpi, Mathf.Min(Screen.safeArea.width, Screen.safeArea.height));
        }
    }

    /// <summary>QA-UI-001 / G-03: fits its RectTransform to Screen.safeArea (notch, punch-hole, gesture bar), re-applied on rotation.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform _rt; Rect _last; ScreenOrientation _lastO;
        void Awake() { _rt = GetComponent<RectTransform>(); Apply(); }
        void Update() { if (Screen.safeArea != _last || Screen.orientation != _lastO) Apply(); }
        void Apply()
        {
            _last = Screen.safeArea; _lastO = Screen.orientation;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _rt.anchorMin = new Vector2(_last.x / Screen.width, _last.y / Screen.height);
            _rt.anchorMax = new Vector2((_last.x + _last.width) / Screen.width, (_last.y + _last.height) / Screen.height);
            _rt.offsetMin = Vector2.zero; _rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>A full-screen page. OnBack returns true if handled; otherwise the router pops it.</summary>
    public abstract class UIScreen
    {
        public RectTransform Root { get; private set; }
        protected UIRouter Router { get; private set; }
        protected UITheme Th => Router.Theme;
        public virtual bool IsModal => false;
        public virtual bool BlocksWorldInput => true;

        internal void Attach(UIRouter r, RectTransform root) { Router = r; Root = root; Build(root); }
        protected abstract void Build(RectTransform root);
        public virtual void OnShow() { }
        public virtual void OnHide() { }
        public virtual void Tick() { }
        public virtual bool OnBack() => false;
    }

    /// <summary>
    /// Screen + modal stack with Android back handling (QA-UI-004 / G-07): back closes the top modal, otherwise asks the top
    /// screen (HUD = pause), otherwise pops one level; at the root it asks before quitting.
    /// </summary>
    public class UIRouter : MonoBehaviour
    {
        public UITheme Theme { get; private set; }
        public RectTransform SafeRoot { get; private set; }
        public Canvas Canvas { get; private set; }
        readonly List<UIScreen> _stack = new List<UIScreen>();
        UIToast _toast;
        public int Depth => _stack.Count;
        public UIScreen Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        public static UIRouter Create(UITheme theme)
        {
            var go = new GameObject("UI");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<DpCanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                DontDestroyOnLoad(es);
                var sys = es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                sys.pixelDragThreshold = Mathf.RoundToInt(10f * (Screen.dpi > 1 ? Screen.dpi : 160f) / 160f); // map pan ≥10dp cancels tap
            }
            var r = go.AddComponent<UIRouter>();
            r.Theme = theme; r.Canvas = canvas;
            var safe = UIFactory.Rect("SafeArea", go.transform);
            UIFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            r.SafeRoot = safe;
            r._toast = new UIToast(r);
            return r;
        }

        public T Push<T>(T screen) where T : UIScreen
        {
            if (!screen.IsModal && Top != null) { Top.Root.gameObject.SetActive(false); Top.OnHide(); }
            var root = UIFactory.Rect(screen.GetType().Name, SafeRoot);
            UIFactory.Stretch(root);
            screen.Attach(this, root);
            _stack.Add(screen);
            screen.OnShow();
            return screen;
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            var s = Top;
            _stack.RemoveAt(_stack.Count - 1);
            s.OnHide();
            Destroy(s.Root.gameObject);
            if (!s.IsModal && Top != null) { Top.Root.gameObject.SetActive(true); Top.OnShow(); }
        }

        public void PopTo<T>() where T : UIScreen { while (_stack.Count > 1 && !(Top is T)) Pop(); }
        public void Replace<T>(T s) where T : UIScreen { Pop(); Push(s); }
        public void PopAllAndPush(UIScreen s) { while (_stack.Count > 0) Pop(); Push(s); }

        public void Toast(string msg, float seconds = 2.5f) => _toast.Show(msg, seconds);

        void Update()
        {
            foreach (var s in _stack.ToArray()) s.Tick();
            _toast.Tick();
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack(); // Android system back / back gesture
        }

        public void HandleBack()
        {
            var top = Top;
            if (top == null) return;
            if (top.OnBack()) return;
            if (top.IsModal || _stack.Count > 1) { Pop(); return; }
            Push(new ConfirmModal("Quit CRASHVERSE?", "Your campaign progress is saved.", "Quit", Application.Quit));
        }

        /// <summary>True if any active screen/modal wants world input blocked (e.g. a modal is open).</summary>
        public bool WorldInputBlocked => Top != null && Top.IsModal;
    }

    public class UIToast
    {
        readonly Text _text; readonly GameObject _go; float _t;
        public UIToast(UIRouter r)
        {
            var bg = UIFactory.Panel("Toast", r.SafeRoot, r.Theme.navy900);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.1f, 0f); rt.anchorMax = new Vector2(0.9f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 96f); rt.sizeDelta = new Vector2(0f, 48f);
            _text = UIFactory.Label(bg.transform, "", r.Theme.bodySp, r.Theme.white, TextAnchor.MiddleCenter, r.Theme);
            UIFactory.Stretch(_text.rectTransform, 8f);
            bg.raycastTarget = false; _text.raycastTarget = false;
            _go = bg.gameObject; _go.SetActive(false);
        }
        public void Show(string m, float s) { _text.text = m; _t = s; _go.SetActive(true); _go.transform.SetAsLastSibling(); }
        public void Tick() { if (_t <= 0f) return; _t -= Time.unscaledDeltaTime; if (_t <= 0f) _go.SetActive(false); }
    }
}
