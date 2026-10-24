using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Crashverse.UI
{
    /// <summary>
    /// Art pack v2 sprites from Resources/UI (see Editor/UiArtImportRules). Falls back to a Texture2D import (Sprite.Create)
    /// so a project imported without the rules still shows the art. Missing art returns null and callers keep a flat colour.
    /// </summary>
    public static class UIArt
    {
        public const string StarOn = "star_on", StarOff = "star_off", Coin = "coin", Gem = "gem", Chest = "chest", Lock = "lock",
            Skull = "skull_hard_level", DailyBoss = "badge_daily_boss", WorldMap = "bg_world_map";
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>Power-up id → icon file (art pack names = v2 canon ids).</summary>
        public static string PowerUpIcon(string id) => string.IsNullOrEmpty(id) ? null : id;

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("UI/" + name);
            if (s == null)
            {
                var t = Resources.Load<Texture2D>("UI/" + name);
                if (t != null) s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            if (s == null) Debug.LogWarning("[UIArt] missing UI/" + name);
            _cache[name] = s;
            return s;
        }

        /// <summary>Image showing a sprite (aspect kept). Never takes taps unless asked.</summary>
        public static Image Icon(Transform parent, string name, float size, bool raycast = false)
        {
            var img = UIFactory.Panel("Icon_" + name, parent, Color.white);
            img.sprite = Get(name); img.preserveAspect = true; img.raycastTarget = raycast;
            if (img.sprite == null) img.color = new Color(1f, 1f, 1f, 0.15f);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minWidth = le.preferredWidth = size; le.minHeight = le.preferredHeight = size;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>Full-bleed background (cover: fills the rect, crops the overflow).</summary>
        public static Image Background(RectTransform parent, string name, Color tint)
        {
            var img = UIFactory.Panel("Bg_" + name, parent, tint);
            img.sprite = Get(name); img.preserveAspect = false; img.raycastTarget = false;
            UIFactory.Stretch(img.rectTransform);
            var sp = img.sprite;
            if (sp != null)
            {
                var f = img.gameObject.AddComponent<AspectRatioFitter>();
                f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; f.aspectRatio = sp.rect.width / Mathf.Max(1f, sp.rect.height);
            }
            return img;
        }

        /// <summary>Cartoon outline + drop shadow on a label (target UI look).</summary>
        public static Text Pop(Text t, Color outline, float px = 2f)
        {
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = outline; o.effectDistance = new Vector2(px, -px);
            var s = t.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0f, 0f, 0.5f); s.effectDistance = new Vector2(0f, -px * 1.5f);
            return t;
        }
    }
}
