using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Crashverse.UI
{
    /// <summary>Code-built uGUI widgets. All sizes in dp/sp (see DpCanvasScaler). Buttons never go below theme.minTap.</summary>
    public static class UIFactory
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Anchor a box to a corner/edge. a = anchor (0..1), size in dp, pos offset in dp.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>Full-width band at top (y from top) or bottom.</summary>
        public static RectTransform Band(RectTransform rt, bool top, float height, float offset = 0f, float sideInset = 0f)
        {
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f); rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(-2f * sideInset, height);
            rt.anchoredPosition = new Vector2(0f, top ? -offset : offset);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color c)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        /// <summary>Gold-trimmed navy panel (art bible: navy gradient, 6px gold border ≈ 2dp).</summary>
        public static Image TrimPanel(string name, Transform parent, UITheme t)
        {
            var border = Panel(name, parent, t.goldTrim);
            var inner = Panel("Fill", border.transform, t.navy700);
            Stretch(inner.rectTransform, 2f);
            return border;
        }

        public static Text Label(Transform parent, string s, float sp, Color c, TextAnchor a, UITheme t, bool display = false)
        {
            var rt = Rect("Text", parent);
            var tx = rt.gameObject.AddComponent<Text>();
            tx.font = display ? t.Display : t.Body;
            tx.fontSize = Mathf.RoundToInt(Mathf.Max(12f, sp)); // QA-UI-002: never below 12sp
            tx.color = c; tx.alignment = a; tx.text = s;
            tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Truncate;
            tx.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = tx.fontSize * 1.4f;
            return tx;
        }

        public static Button Button(Transform parent, string label, Color bg, UnityAction onClick, UITheme t, float height = 0f, float width = 0f, float sp = 0f)
        {
            float h = Mathf.Max(t.minTap, height <= 0f ? t.buttonL : height);
            var img = Panel("Button_" + label, parent, bg);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors; colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.7f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); b.colors = colors;
            if (onClick != null) b.onClick.AddListener(onClick);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
            if (width > 0f) { le.minWidth = Mathf.Max(t.minTap, width); le.preferredWidth = le.minWidth; }
            img.rectTransform.sizeDelta = new Vector2(Mathf.Max(t.minTap, width > 0 ? width : 160f), h);
            var tx = Label(img.transform, label, sp > 0 ? sp : t.headingSp, t.white, TextAnchor.MiddleCenter, t, display: true);
            Stretch(tx.rectTransform, 4f);
            tx.resizeTextForBestFit = true; tx.resizeTextMinSize = 12; tx.resizeTextMaxSize = tx.fontSize;
            return b;
        }

        public static void SetLabel(Button b, string s) { var t = b.GetComponentInChildren<Text>(); if (t != null) t.text = s; }

        public static RectTransform VStack(Transform parent, float spacing, float padding = 0f, TextAnchor align = TextAnchor.UpperCenter)
        {
            var rt = Rect("VStack", parent);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align;
            v.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return rt;
        }

        public static RectTransform HStack(Transform parent, float spacing, float height)
        {
            var rt = Rect("HStack", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlHeight = true; h.childControlWidth = true; h.childForceExpandHeight = true; h.childForceExpandWidth = true;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height;
            return rt;
        }

        /// <summary>Vertical scroll list; returns the content transform (VStack).</summary>
        public static RectTransform Scroll(Transform parent, float spacing, out ScrollRect sr)
        {
            var view = Panel("Scroll", parent, new Color(0f, 0f, 0f, 0.15f));
            sr = view.gameObject.AddComponent<ScrollRect>();
            view.gameObject.AddComponent<RectMask2D>();
            var content = VStack(view.transform, spacing, 8f);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        public static void Spacer(Transform parent, float h)
        {
            var le = Rect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
        }

        /// <summary>Filled ★ vs outline ☆ so stars differ by shape, not only colour (QA §4).</summary>
        public static string Stars(int n, int of = 3)
        {
            n = Mathf.Clamp(n, 0, of);
            return new string('★', n) + new string('☆', of - n);
        }

        /// <summary>Ellipsis truncation for long names (QA §7).</summary>
        public static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, Mathf.Max(1, max - 1)) + "…";

        public static string FormatCountdown(System.TimeSpan t) =>
            t.TotalSeconds <= 0 ? "now" : $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }

    /// <summary>Generic confirm dialog (quit, give up, consume ranked attempt).</summary>
    public class ConfirmModal : UIScreen
    {
        readonly string _title, _body, _ok; readonly System.Action _onOk; readonly System.Action _onCancel;
        public override bool IsModal => true;
        public ConfirmModal(string title, string body, string ok, System.Action onOk, System.Action onCancel = null)
        { _title = title; _body = body; _ok = ok; _onOk = onOk; _onCancel = onCancel; }

        protected override void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0f, 0f, 0f, 0.6f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("Dialog", root, Th);
            UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(300f, 230f), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap * 2, 16f); UIFactory.Stretch(v);
            UIFactory.Label(v, _title, Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            UIFactory.Label(v, _body, Th.bodySp, Th.body, TextAnchor.MiddleCenter, Th).GetComponent<LayoutElement>().minHeight = 48f;
            var row = UIFactory.HStack(v, Th.gap * 2, Th.buttonL);
            UIFactory.Button(row, "Cancel", Th.navy500, () => { Router.Pop(); _onCancel?.Invoke(); }, Th);
            UIFactory.Button(row, _ok, Th.red, () => { Router.Pop(); _onOk?.Invoke(); }, Th);
        }
    }
}
