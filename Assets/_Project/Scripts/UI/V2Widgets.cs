using System;
using Crashverse.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Crashverse.UI
{
    /// <summary>Target UI v2 widgets (glossy cartoon look): circle sprites, pills, star rows, icon tiles, ribbons, layout previews.</summary>
    public static class V2
    {
        public static readonly Color Ink = new Color(0.04f, 0.07f, 0.17f, 1f), Glass = new Color(0.05f, 0.09f, 0.22f, 0.82f);
        public static readonly Color Core = UITheme.H("22D3EE"), Module = UITheme.H("8B5CF6"), Booster = UITheme.H("FF7A1A");
        public static Color SlotColor(string slot) => slot == "Core" ? Core : slot == "Module" ? Module : Booster;

        static Sprite _circle, _round;
        /// <summary>Anti-aliased white disc (runtime texture; tinted by Image.color).</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeRound(128, 64f));
        /// <summary>Rounded rectangle for 9-slice panels and buttons.</summary>
        public static Sprite Rounded => _round != null ? _round : (_round = MakeRound(96, 24f, true));

        static Sprite MakeRound(int size, float radius, bool sliced = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size]; float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x - c) - (c - radius)), dy = Mathf.Max(0f, Mathf.Abs(y - c) - (c - radius));
                float d = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            t.SetPixels32(px); t.Apply(false, true);
            var b = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, b);
        }

        public static Image RoundPanel(string name, Transform parent, Color c, bool raycast = false)
        {
            var img = UIFactory.Panel(name, parent, c); img.sprite = Rounded; img.type = Image.Type.Sliced; img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Gold-rimmed glass card (target UI panels).</summary>
        public static Image Card(string name, Transform parent, UITheme t)
        {
            var rim = RoundPanel(name, parent, t.goldTrim, true);
            var fill = RoundPanel("Fill", rim.transform, Glass); UIFactory.Stretch(fill.rectTransform, 3f);
            return rim;
        }

        /// <summary>
        /// Screen body that never clips its primary action: a vertical scroll for the content and a fixed footer for the buttons
        /// (small or landscape windows, e.g. the batchmode test player, still reach Play/Continue).
        /// </summary>
        public static RectTransform Split(Transform parent, float footerH, out RectTransform footer, float pad = 12f)
        {
            var body = UIFactory.Rect("Body", parent); UIFactory.Stretch(body); body.offsetMin = new Vector2(0f, footerH);
            var hit = body.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0.001f);
            body.gameObject.AddComponent<RectMask2D>();
            var sr = body.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.viewport = body;
            var content = UIFactory.VStack(body, 8f, pad);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            footer = UIFactory.VStack(parent, 6f, pad, TextAnchor.LowerCenter);
            footer.anchorMin = Vector2.zero; footer.anchorMax = new Vector2(1f, 0f); footer.pivot = new Vector2(0.5f, 0f); footer.sizeDelta = new Vector2(0f, footerH); footer.anchoredPosition = Vector2.zero;
            return content;
        }

        public static Text Title(Transform parent, string s, float sp, Color c, UITheme t)
        {
            var l = UIFactory.Label(parent, s, sp, c, TextAnchor.MiddleCenter, t, true);
            l.fontStyle = FontStyle.Bold; UIArt.Pop(l, Ink, 2f);
            return l;
        }

        /// <summary>Glossy rounded button (green Play / Continue style). Keeps UIFactory.Button behaviour (48dp min, motion, label).</summary>
        public static Button Glossy(Transform parent, string label, Color c, UnityAction onClick, UITheme t, float h = 56f, float w = 0f, float sp = 0f, string name = null)
        {
            var b = UIFactory.Button(parent, label, c, onClick, t, h, w, sp > 0 ? sp : t.headingSp);
            b.image.sprite = Rounded; b.image.type = Image.Type.Sliced;
            var tx = b.GetComponentInChildren<Text>(); if (tx != null) { tx.fontStyle = FontStyle.Bold; UIArt.Pop(tx, Ink, 1.5f); }
            var shine = RoundPanel("Shine", b.transform, new Color(1f, 1f, 1f, 0.18f)); var rt = shine.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(4f, 0f); rt.offsetMax = new Vector2(-4f, -3f);
            shine.transform.SetSiblingIndex(0);
            if (name != null) b.gameObject.name = name;
            return b;
        }

        /// <summary>Round icon button (map nodes, pause). Sprite-less: tinted disc.</summary>
        public static Button RoundButton(Transform parent, string label, Color c, UnityAction onClick, UITheme t, float size, string name = null)
        {
            var b = UIFactory.Button(parent, label, c, onClick, t, size, size, t.headingSp);
            b.image.sprite = Circle; b.image.type = Image.Type.Simple;
            var tx = b.GetComponentInChildren<Text>(); if (tx != null) { tx.fontStyle = FontStyle.Bold; UIArt.Pop(tx, Ink, 1.5f); }
            ((RectTransform)b.transform).sizeDelta = new Vector2(Mathf.Max(t.minTap, size), Mathf.Max(t.minTap, size));
            if (name != null) b.gameObject.name = name;
            return b;
        }

        /// <summary>Currency pill: icon + value.</summary>
        public static Text Pill(Transform parent, string icon, string value, UITheme t, float w = 92f)
        {
            var bg = RoundPanel("Pill_" + icon, parent, new Color(0f, 0f, 0f, 0.45f));
            var le = bg.gameObject.AddComponent<LayoutElement>(); le.minWidth = le.preferredWidth = w; le.minHeight = le.preferredHeight = 34f;
            var ic = UIArt.Icon(bg.transform, icon, 30f); UIFactory.Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 30f), new Vector2(2f, 0f));
            var tx = UIFactory.Label(bg.transform, value, t.bodySp, Color.white, TextAnchor.MiddleCenter, t, true); tx.fontStyle = FontStyle.Bold;
            var rt = tx.rectTransform; UIFactory.Stretch(rt); rt.offsetMin = new Vector2(30f, 0f);
            return tx;
        }

        /// <summary>Row of 3 star sprites (earned on, rest off).</summary>
        public static RectTransform Stars(Transform parent, int earned, float size, float spacing = 2f) => Stars(parent, earned, size, spacing, out _);
        public static RectTransform Stars(Transform parent, int earned, float size, float spacing, out Image[] arr)
        {
            var row = UIFactory.Rect("Stars", parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            var le = row.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = size;
            arr = new Image[3];
            for (int i = 0; i < 3; i++) arr[i] = UIArt.Icon(row, i < earned ? UIArt.StarOn : UIArt.StarOff, size);
            row.sizeDelta = new Vector2(size * 3 + spacing * 2, size);
            return row;
        }

        public static void SetStar(Image img, bool on) { if (img != null) img.sprite = UIArt.Get(on ? UIArt.StarOn : UIArt.StarOff); }

        /// <summary>Power-up tile: icon with a slot-coloured ring (Core cyan / Module violet / Booster orange), lock overlay.</summary>
        public static Button PowerUpTile(Transform parent, PowerUpData p, bool unlocked, bool selected, UnityAction onClick, UITheme t, float size)
        {
            var b = UIFactory.Button(parent, "", selected ? t.goldTrim : SlotColor(p.slot), onClick, t, size, size, t.captionSp);
            b.gameObject.name = "Button_PU_" + p.id;
            b.image.sprite = Rounded; b.image.type = Image.Type.Sliced;
            var inner = RoundPanel("Inner", b.transform, Ink); UIFactory.Stretch(inner.rectTransform, selected ? 5f : 3f);
            var ic = UIArt.Icon(b.transform, UIArt.PowerUpIcon(p.id), size - 10f); UIFactory.Stretch(ic.rectTransform, 5f);
            if (!unlocked)
            {
                ic.color = new Color(0.35f, 0.35f, 0.4f, 1f);
                var lk = UIArt.Icon(b.transform, UIArt.Lock, size * 0.42f); UIFactory.Place(lk.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size * 0.42f, size * 0.42f), Vector2.zero);
            }
            if (selected)
            {
                var chk = UIFactory.Label(b.transform, "✓", t.headingSp, t.green, TextAnchor.UpperRight, t, true); UIArt.Pop(chk, Ink, 1.5f);
                UIFactory.Stretch(chk.rectTransform, 2f);
            }
            return b;
        }

        /// <summary>
        /// Level preview from the layout data: a front view (z across, y up) of every block, coloured by material, scaled to fit.
        /// The camera looks down +x from the Wrecker, so this is what the player sees, without rendering a 3D scene.
        /// </summary>
        public static RectTransform LayoutPreview(Transform parent, LayoutData lay, Vector2 size, bool dark)
        {
            var box = RoundPanel("Preview", parent, dark ? new Color(0.12f, 0.05f, 0.08f, 0.85f) : new Color(0.55f, 0.82f, 1f, 0.55f)).rectTransform;
            var le = box.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = size.y; le.preferredWidth = size.x;
            box.sizeDelta = size;
            if (lay?.blocks == null || lay.blocks.Length == 0) return box;
            float minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var b in lay.blocks)
            {
                if (b.p == null || b.s == null) continue;
                minZ = Mathf.Min(minZ, b.p[2] - b.s[2] / 2); maxZ = Mathf.Max(maxZ, b.p[2] + b.s[2] / 2);
                minY = Mathf.Min(minY, b.p[1] - b.s[1] / 2); maxY = Mathf.Max(maxY, b.p[1] + b.s[1] / 2);
            }
            float w = Mathf.Max(0.1f, maxZ - minZ), h = Mathf.Max(0.1f, maxY - minY);
            float k = Mathf.Min((size.x - 24f) / w, (size.y - 24f) / h);
            // back to front (largest x = farthest) so nearer pieces draw on top
            var order = (BlockData[])lay.blocks.Clone();
            Array.Sort(order, (a, c) => (c.p != null ? c.p[0] : 0f).CompareTo(a.p != null ? a.p[0] : 0f));
            foreach (var b in order)
            {
                if (b.p == null || b.s == null) continue;
                var r = UIFactory.Panel("B_" + b.id, box, MatColor(b, dark)); r.raycastTarget = false;
                var rt = r.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Mathf.Max(2f, b.s[2] * k), Mathf.Max(2f, b.s[1] * k));
                rt.anchoredPosition = new Vector2((b.p[2] - (minZ + w / 2)) * k, 12f + (b.p[1] - minY) * k);
            }
            return box;
        }

        static Color MatColor(BlockData b, bool dark)
        {
            Color c;
            if (b.role == "island" || b.indestructible || b.nonObj) c = UITheme.H("6BAF4C");
            else if (b.explosive) c = UITheme.H("E5484D");
            else switch (b.mat)
            {
                case "wood": c = UITheme.H("C98B4A"); break;
                case "brick": c = UITheme.H("B5523B"); break;
                case "steel": c = UITheme.H("9AA7B8"); break;
                case "glass": c = new Color(0.65f, 0.9f, 1f, 0.8f); break;
                case "stone": c = UITheme.H("8C8A85"); break;
                default: c = UITheme.H("D9B26F"); break;
            }
            if (!string.IsNullOrEmpty(b.color)) { var cc = UITheme.H(b.color.TrimStart('#')); if (cc.a > 0f) c = cc; }
            return dark ? Color.Lerp(c, Color.black, 0.45f) : c;
        }
    }
}
