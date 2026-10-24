using System;
using System.Collections.Generic;
using Crashverse.Core;
using Crashverse.Data;
using Crashverse.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Crashverse.UI
{
    static class V2Screens
    {
        public static RectTransform Frame(RectTransform root, string title, UITheme th, Action back, string bg = "bg_season_sunny_shores")
        {
            UIArt.Background(root, bg, new Color(0.45f, 0.5f, 0.65f, 1f));
            var head = UIFactory.Rect("Head", root); UIFactory.Band(head, true, 60f);
            var hr = UIFactory.HStack(head, th.gap, 52f); UIFactory.Stretch(hr, 4f);
            UIFactory.Button(hr, "◀", th.navy500, () => back(), th, 48, 48, th.headingSp).gameObject.name = "Button_Back";
            V2.Title(hr, title, th.titleSp, th.goldFill, th).GetComponent<LayoutElement>().flexibleWidth = 1f;
            UIFactory.Spacer(hr, 48f);
            var body = UIFactory.Rect("Body", root); UIFactory.Stretch(body); body.offsetMax = new Vector2(-8f, -66f); body.offsetMin = new Vector2(8f, 8f);
            var sr = body.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
            body.gameObject.AddComponent<RectMask2D>(); var hit = body.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0.001f);
            var content = UIFactory.VStack(body, th.gap, 6f);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f); content.anchoredPosition = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.viewport = body;
            return content;
        }

        public static GridLayoutGroup Grid(Transform parent, Vector2 cell, int cols, float spacing, float rowsHeight)
        {
            var g = UIFactory.Rect("Grid", parent);
            var le = g.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = rowsHeight;
            var gl = g.gameObject.AddComponent<GridLayoutGroup>(); gl.cellSize = cell; gl.spacing = new Vector2(spacing, spacing);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = cols; gl.childAlignment = TextAnchor.UpperCenter;
            return gl;
        }
    }

    // ───────────────────────────── Target UI §10: Power Ups grid ─────────────────────────────
    public class PowerUpsScreen : UIScreen
    {
        protected override void Build(RectTransform root)
        {
            var db = AppServices.Content; var save = AppServices.Save;
            var v = V2Screens.Frame(root, "POWER UPS", Th, () => Router.Pop());
            UIFactory.Label(v, "Equip one Core, one Module and one Booster before each level.", Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th);
            var all = PowerUpRulesV2.InUnlockOrder(db.PowerUps);
            var g = V2Screens.Grid(v, new Vector2(84f, 132f), 4, 6f, 2 * 132f + 6f);
            foreach (var p in all)
            {
                bool un = UnlockRules.IsPowerUpUnlocked(p, db, save);
                var cell = UIFactory.Rect("PU_" + p.id, g.transform);
                var cv = UIFactory.VStack(cell, 2f, 0f); UIFactory.Stretch(cv);
                var pu = p;
                V2.PowerUpTile(cv, p, un, false, () => Router.Toast(pu.name + ": " + (UnlockRules.IsPowerUpUnlocked(pu, db, save) ? PowerUpRulesV2.Tagline(pu.effect) + " · " + pu.slot : pu.unlockText)), Th, 72f);
                var nl = UIFactory.Label(cv, p.name, Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th, true); UIArt.Pop(nl, V2.Ink, 1f);
                UIFactory.Label(cv, un ? PowerUpRulesV2.Tagline(p.effect) : "🔒 " + p.unlockText, Th.captionSp * 0.85f, un ? V2.SlotColor(p.slot) : Th.body, TextAnchor.UpperCenter, Th);
            }
            UIFactory.Label(v, "Core  ·  Module  ·  Booster", Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
        }
    }

    // ───────────────────────────── Target UI §11: Seasons (4 themes) ─────────────────────────────
    public class SeasonsScreen : UIScreen
    {
        protected override void Build(RectTransform root)
        {
            var m = AppServices.Content.Meta; var ch = AppServices.Content.Chapter1; var save = AppServices.Save;
            var v = V2Screens.Frame(root, "SEASONS", Th, () => Router.Pop());
            UIFactory.Label(v, "A new world theme every season", Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th);
            foreach (var s in m.seasons ?? new SeasonTheme[0])
            {
                bool open = SeasonRules.IsOpen(s, ch, save);
                var rim = V2.RoundPanel("Season_" + s.id, v, UITheme.H(string.IsNullOrEmpty(s.accent) ? "FFD34D" : s.accent.TrimStart('#')), true);
                var le = rim.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 112f;
                var mask = UIFactory.Rect("Mask", rim.transform); UIFactory.Stretch(mask, 3f); mask.gameObject.AddComponent<RectMask2D>();
                UIArt.Background(mask, s.bg, open ? Color.white : new Color(0.5f, 0.5f, 0.55f, 1f));
                var shade = UIFactory.Panel("Shade", mask, new Color(0f, 0f, 0f, 0.25f)); UIFactory.Stretch(shade.rectTransform); shade.raycastTarget = false;
                var tl = V2.Title(mask, $"Season {s.number}\n{s.name}", Th.headingSp, Color.white, Th); tl.alignment = TextAnchor.MiddleLeft;
                UIFactory.Stretch(tl.rectTransform, 12f);
                int lv = SeasonRules.LevelsInSeason(s, ch);
                string st = s.comingSoon || lv == 0 ? "Coming soon" : open ? $"★ {SeasonRules.StarsInSeason(s, ch, save)}/{lv * 3}" : $"Clear Level {s.firstLevel - 1}";
                var sl = V2.Title(mask, st, Th.bodySp, open ? Th.goldFill : Color.white, Th); sl.alignment = TextAnchor.LowerRight; UIFactory.Stretch(sl.rectTransform, 10f);
                if (!open) { var lk = UIArt.Icon(mask, UIArt.Lock, 44f); UIFactory.Place(lk.rectTransform, new Vector2(1f, 1f), new Vector2(44f, 44f), new Vector2(-30f, -30f)); }
            }
        }
    }

    // ───────────────────────────── Target UI §12: Progression & Rewards ─────────────────────────────
    /// <summary>Local progression hub: wallet, chests, star collection, offline season pass / leaderboard / daily login, achievements with rewards.</summary>
    public class ProgressionScreen : UIScreen
    {
        Text _wallet, _chests; RectTransform _list;
        protected override void Build(RectTransform root)
        {
            var v = V2Screens.Frame(root, "PROGRESSION", Th, () => Router.Pop());
            _wallet = V2.Title(v, "", Th.headingSp, Th.goldFill, Th);
            var g = V2Screens.Grid(v, new Vector2(104f, 112f), 3, 6f, 2 * 112f + 6f);
            Tile(g.transform, UIArt.Chest, "Level Rewards", "Coins, Gems, Chests", OpenChest, "Button_OpenChest");
            Tile(g.transform, UIArt.StarOn, "Star Collection", "Stars from every level", () => Router.Toast(StarsLine()), "Button_StarCollection");
            Tile(g.transform, "badge_03_gold", "Season Pass", "Free track", () => Router.Push(new LocalSeasonPassScreen()), "Button_SeasonPass");
            Tile(g.transform, "badge_02_silver", "Leaderboard", "Local", () => Router.Push(new LocalLeaderboardScreen()), "Button_Leaderboards");
            Tile(g.transform, UIArt.Gem, "Daily Login", "7-day rewards", () => Router.Push(new LocalDailyLoginScreen()), "Button_DailyLogin");
            Tile(g.transform, "badge_01_bronze", "Achievements", "Claim rewards", null, "Button_Achievements");
            _chests = UIFactory.Label(v, "", Th.bodySp, Color.white, TextAnchor.MiddleCenter, Th);
            V2.Title(v, "Achievements", Th.headingSp, Color.white, Th);
            _list = UIFactory.VStack(v, 4f, 0f);
            _list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void Tile(Transform parent, string icon, string title, string sub, Action onClick, string name)
        {
            var b = V2.Glossy(parent, "", new Color(0.12f, 0.16f, 0.34f, 0.95f), () => onClick?.Invoke(), Th, 112f, 104f, 0f, name);
            var cv = UIFactory.VStack(b.transform, 0f, 4f); UIFactory.Stretch(cv);
            UIArt.Icon(cv, icon, 48f);
            var t = UIFactory.Label(cv, title, Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th, true); UIArt.Pop(t, V2.Ink, 1f);
            UIFactory.Label(cv, sub, Th.captionSp * 0.8f, Th.body, TextAnchor.MiddleCenter, Th);
        }

        string StarsLine() => "★ " + UnlockRules.TotalStars(AppServices.Content.Chapter1, AppServices.Save) + "/" + AppServices.Content.Chapter1.levels.Length * 3 + " stars collected";

        void OpenChest()
        {
            var r = AppServices.Progress.OpenChest(AppServices.Content.Meta.chest);
            if (r.IsEmpty) Router.Toast("No chests yet: clear every 5th level to earn one");
            else Router.Toast($"Chest opened: +{r.coins} coins" + (r.gems > 0 ? $", +{r.gems} gems" : ""));
            OnShow();
        }

        void ClaimAchievement(AchievementDef a)
        {
            var r = AchievementRules.Claim(a, AppServices.Content.Chapter1, AppServices.Save, AppServices.Progress);
            if (!r.IsEmpty) Router.Toast(a.name + ": +" + OfflineUI.Bundle(r));
            OnShow();
        }

        public override void OnShow()
        {
            var p = AppServices.Progress.Data; var ch = AppServices.Content.Chapter1; var save = AppServices.Save;
            _wallet.text = $"{p.coins} coins   ·   {p.gems} gems   ·   {StarsLine()}";
            _chests.text = p.chests > 0 ? $"{p.chests} chest{(p.chests > 1 ? "s" : "")} to open: tap Level Rewards" : $"Chests opened: {p.chestsOpened}";
            for (int i = _list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_list.GetChild(i).gameObject);
            foreach (var a in AppServices.Content.Meta.achievements ?? new AchievementDef[0])
            {
                int prog = Math.Min(a.target, AchievementRules.Progress(a, ch, save, AppServices.Progress));
                bool done = AchievementRules.Done(a, ch, save, AppServices.Progress), claimed = AchievementRules.Claimed(a, AppServices.Progress);
                var ach = a; string rw = OfflineUI.Bundle(a.reward);
                OfflineUI.Row(_list, Th, "Ach_" + a.id, string.IsNullOrEmpty(a.icon) ? "badge_01_bronze" : a.icon, a.name, a.text + (rw.Length > 0 ? " · " + rw : ""),
                    done ? new Color(0.1f, 0.4f, 0.2f, 0.9f) : new Color(0.08f, 0.12f, 0.28f, 0.9f), claimed ? "✓" : $"{prog}/{a.target}", claimed ? Th.green : Th.goldFill,
                    done && !claimed ? "Claim" : null, done && !claimed ? (Action)(() => ClaimAchievement(ach)) : null);
            }
        }
    }
}
