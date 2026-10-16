using UnityEngine;
using UnityEngine.UI;

namespace Crashverse.UI
{
    /// <summary>Art v2 age-gate palette and helpers (art/v2/age_gate/age_gate_spec.md, "Colours"). Live UI only: no baked text.</summary>
    public static class AgeGateStyle
    {
        public static readonly Color PanelTop = UITheme.H("1E2852"), PanelBottom = UITheme.H("0E122A");
        public static readonly Color FieldFill = new Color(0x0A / 255f, 0x0E / 255f, 0x22 / 255f, 0.92f);
        public static readonly Color Gold = UITheme.H("F4BE40"), GoldLight = UITheme.H("FFDE78"), GoldDark = UITheme.H("AA701E");
        public static readonly Color Cta = UITheme.H("FF7A1A"), CtaDisabled = UITheme.H("5C6480"), CtaDisabledText = UITheme.H("AAB0C4");
        public static readonly Color Cyan = UITheme.H("22D3EE"), Violet = UITheme.H("8B5CF6"), Charcoal = UITheme.H("262A36");
        public static readonly Color Error = UITheme.H("FF6B6B"), Muted = UITheme.H("C8D2EB"), Placeholder = UITheme.H("8C96B9");
        public static readonly Color Dim = new Color(PanelBottom.r, PanelBottom.g, PanelBottom.b, 0.66f);
        public static readonly Color Row = new Color(0, 0, 0, 0), RowSelected = UITheme.H("1E2852");
        public static readonly Color RowPressedTint = new Color(Cyan.r, Cyan.g, Cyan.b, 1f) * 0.24f + Color.white * 0.76f;

        /// <summary>Full-screen navy background; uses the Art v2 sky slice if it is present in Resources (top-centre anchor, cover-scaled).</summary>
        public static void Background(RectTransform root)
        {
            var bg = UIFactory.Panel("Bg", root, PanelBottom); UIFactory.Stretch(bg.rectTransform); bg.raycastTarget = true; // swallows stray taps
            var sprite = Resources.Load<Sprite>("AgeGate/bg_age_gate");
            if (sprite == null) return;
            var art = UIFactory.Panel("BgArt", bg.transform, Color.white); art.sprite = sprite; art.raycastTarget = false; art.preserveAspect = false;
            var rt = art.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            var fit = art.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        public static void Outline(GameObject go, float px)
        {
            var o = go.GetComponent<Outline>(); if (o == null) o = go.AddComponent<Outline>(); // no ?? on UnityEngine.Object (fake-null)
            o.effectColor = Gold; o.effectDistance = new Vector2(px, -px); o.useGraphicAlpha = false;
        }
        public static void SetOutline(GameObject go, Color c) { var o = go.GetComponent<Outline>(); if (o != null) o.effectColor = c; }
    }
}
