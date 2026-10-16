using UnityEngine;

namespace Crashverse.UI
{
    /// <summary>
    /// UI Design System v1 tokens (/workspace/art/v1/ui_system.md + art_bible_v1.md), converted from the 1080-wide
    /// reference canvas (1080px ≈ 360dp, so px/3) and raised to QA minimums (tap ≥48dp, text ≥12sp; HUD numerics ≥14sp).
    /// Bake to an asset with Crashverse ▸ Create UI Theme; runtime falls back to CreateDefault().
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "Crashverse/UI Theme", order = 30)]
    public class UITheme : ScriptableObject
    {
        [Header("Palette (art bible)")]
        public Color navy900 = H("0B1530"), navy700 = H("14285C"), navy500 = H("22408A");
        public Color goldTrim = H("F5C451"), goldFill = H("FFD23F"), goldShade = H("E09A00");
        public Color orange = H("FF7A1A"), cyan = H("22D3EE"), violet = H("8B5CF6"), green = H("3FC12E"), red = H("E5484D");
        public Color body = H("CFE3FF"), white = Color.white, disabled = H("5B6472"), locked = H("3A3F48");

        [Header("Sizes (dp/sp) — design px / 3, clamped to QA minimums")]
        public float minTap = 48f;          // QA G-06
        public float buttonL = 52f;          // 130–150px → 43–50dp → 52dp
        public float buttonIcon = 48f;       // 100px → 33dp → 48dp min
        public float tabHeight = 48f;        // 96px → 32dp → 48dp min
        public float mapNode = 52f, bossNode = 64f; // 120px/180px
        public float loadoutSlot = 80f;      // 230px
        public float leaderboardRow = 48f;   // 118px → 40dp → 48dp
        public float rewardTile = 110f;      // 330px
        public float seasonTier = 48f;       // 78px → 26dp → 48dp min
        public float pillHeight = 32f;       // 76px (non-interactive), its "+" is a 48dp target
        public float edgeGestureInset = 16f; // QA G-06
        public float gap = 8f;               // QA G-06 spacing min; spacing scale 8/16/24/30/40/60 px

        [Header("Type (sp)")]
        public float titleSp = 26f;          // 60–76px display
        public float headingSp = 20f;
        public float bodySp = 14f;           // body ≥12sp (G-04)
        public float captionSp = 12f;        // labels ≥11sp; we use 12
        public float hudNumberSp = 18f;      // HUD numerics ≥14sp
        [Tooltip("Production: Lilita One (display) / Nunito ExtraBold (body). Runtime falls back to the built-in font until the font assets are imported.")]
        public Font displayFont, bodyFont;
        public string displayFontName = "Lilita One", bodyFontName = "Nunito ExtraBold";

        public static UITheme CreateDefault() => CreateInstance<UITheme>();
        public static UITheme Load() { var t = Resources.Load<UITheme>("UITheme"); return t != null ? t : CreateDefault(); }

        public Font Display => displayFont != null ? displayFont : Fallback;
        public Font Body => bodyFont != null ? bodyFont : Fallback;
        static Font _fallback;
        static Font Fallback => _fallback != null ? _fallback : (_fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Color H(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
    }
}
