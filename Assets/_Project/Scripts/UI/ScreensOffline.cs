using System;
using System.Collections.Generic;
using Crashverse.Core;
using Crashverse.Data;
using Crashverse.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Crashverse.UI
{
    // integration/v2 offline replacements for the online stubs. Everything here is on-device data and says so.

    static class OfflineUI
    {
        public static void Clear(RectTransform list) { for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject); }

        public static RectTransform List(RectTransform v)
        {
            var l = UIFactory.VStack(v, 4f, 0f); l.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; return l;
        }

        /// <summary>One row: icon, two-line text, and either a status label or a button.</summary>
        public static void Row(RectTransform list, UITheme th, string name, string icon, string title, string sub, Color bg, string status, Color statusCol, string button = null, Action onClick = null)
        {
            var row = V2.RoundPanel(name, list, bg);
            var le = row.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 56f;
            var hr = UIFactory.HStack(row.transform, 8f, 52f); UIFactory.Stretch(hr, 4f);
            if (!string.IsNullOrEmpty(icon)) UIArt.Icon(hr, icon, 44f);
            var t = UIFactory.Label(hr, string.IsNullOrEmpty(sub) ? title : $"{title}\n<size=11>{sub}</size>", th.captionSp, Color.white, TextAnchor.MiddleLeft, th, true);
            t.GetComponent<LayoutElement>().flexibleWidth = 1f;
            if (button != null && onClick != null) V2.Glossy(hr, button, th.green, () => onClick(), th, 44f, 96f, th.captionSp, "Button_Claim_" + name);
            else UIFactory.Label(hr, status ?? "", th.bodySp, statusCol, TextAnchor.MiddleRight, th, true).GetComponent<LayoutElement>().minWidth = 64f;
        }

        public static string Bundle(RewardBundle r)
        {
            if (r == null || r.IsEmpty) return "";
            var parts = new List<string>();
            if (r.coins > 0) parts.Add($"{r.coins} coins"); if (r.gems > 0) parts.Add($"{r.gems} gems"); if (r.chests > 0) parts.Add(r.chests > 1 ? $"{r.chests} chests" : "1 chest");
            return string.Join(" + ", parts);
        }
    }

    /// <summary>Daily login: 7-day cycle on this device's local date (no server). Clock rollback can't re-claim.</summary>
    public class LocalDailyLoginScreen : UIScreen
    {
        RectTransform _list; Text _head;
        protected override void Build(RectTransform root)
        {
            var v = V2Screens.Frame(root, "DAILY LOGIN", Th, () => Router.Pop());
            _head = UIFactory.Label(v, "", Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th);
            _list = OfflineUI.List(v);
        }
        public override void OnShow()
        {
            var p = AppServices.Progress; var m = AppServices.Content.Meta; var key = DailyLoginRules.Key(DateTime.Now);
            DailyLoginRules.Observe(p.Data, key);
            bool can = DailyLoginRules.CanClaim(p.Data, key); int next = DailyLoginRules.NextDay(p.Data, key, m.dailyLogin.Length);
            _head.text = DailyLoginRules.RolledBack(p.Data, key) ? "Your device clock is behind your last visit. Rewards resume when it catches up."
                       : can ? "Log in every day for bigger rewards. Miss a day and the week starts over. Saved on this device."
                             : "Today's reward is claimed. Come back tomorrow.";
            OfflineUI.Clear(_list);
            for (int i = 0; i < m.dailyLogin.Length; i++)
            {
                int day = i; bool today = can && i == next;
                bool done = can ? i < next : p.Data.loginDay >= 0 && i <= p.Data.loginDay;
                OfflineUI.Row(_list, Th, "Login_" + (i + 1), i == m.dailyLogin.Length - 1 ? UIArt.Chest : UIArt.Coin, $"Day {i + 1}", OfflineUI.Bundle(m.dailyLogin[i]),
                    today ? new Color(0.12f, 0.35f, 0.2f, 0.95f) : new Color(0.08f, 0.12f, 0.28f, 0.9f), done ? "✓" : "", Th.green,
                    today ? "Claim" : null, today ? (Action)(() => Claim()) : null);
            }
        }
        void Claim()
        {
            var m = AppServices.Content.Meta; var p = AppServices.Progress;
            var r = DailyLoginRules.Claim(p, m.dailyLogin, DailyLoginRules.Key(DateTime.Now), m.seasonPass, SeasonRules.Current(m, AppServices.Content.Chapter1, AppServices.Save)?.id);
            Router.Toast(r.IsEmpty ? "Already claimed today" : "Daily reward: +" + OfflineUI.Bundle(r));
            OnShow();
        }
    }

    /// <summary>Season pass, free track only. XP from play on this device; rewards are coins, earned gems and chests. Nothing to buy.</summary>
    public class LocalSeasonPassScreen : UIScreen
    {
        RectTransform _list; Text _head; SeasonTheme _s;
        protected override void Build(RectTransform root)
        {
            var m = AppServices.Content.Meta; _s = SeasonRules.Current(m, AppServices.Content.Chapter1, AppServices.Save);
            var v = V2Screens.Frame(root, "SEASON PASS", Th, () => Router.Pop(), _s?.bg ?? "bg_season_sunny_shores");
            _head = UIFactory.Label(v, "", Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th);
            _list = OfflineUI.List(v);
        }
        public override void OnShow()
        {
            var m = AppServices.Content.Meta; var def = m.seasonPass; var p = AppServices.Progress;
            if (_s == null || def?.tiers == null || def.tiers.Length == 0) { _head.text = "No season pass this season."; return; }
            var sp = SeasonPassRules.For(p.Data, _s.id); int reached = SeasonPassRules.TierReached(sp.xp, def);
            int into = reached >= def.tiers.Length ? def.xpPerTier : sp.xp - reached * def.xpPerTier;
            _head.text = $"Season {_s.number}: {_s.name} · Free track\nTier {reached}/{def.tiers.Length} · {into}/{def.xpPerTier} XP to the next tier\nEarn XP by winning levels, stars, the Daily Boss and daily logins.";
            OfflineUI.Clear(_list);
            for (int t = 0; t < def.tiers.Length; t++)
            {
                int tier = t; bool claimed = sp.claimed.Contains(t); bool can = SeasonPassRules.CanClaim(sp, t, def);
                OfflineUI.Row(_list, Th, "Tier_" + (t + 1), def.tiers[t].chests > 0 ? UIArt.Chest : def.tiers[t].gems > 0 ? UIArt.Gem : UIArt.Coin, $"Tier {t + 1}", OfflineUI.Bundle(def.tiers[t]),
                    claimed ? new Color(0.1f, 0.4f, 0.2f, 0.9f) : can ? new Color(0.12f, 0.35f, 0.2f, 0.95f) : new Color(0.08f, 0.12f, 0.28f, 0.9f),
                    claimed ? "✓" : t < reached ? "" : $"{(t + 1) * def.xpPerTier} XP", claimed ? Th.green : Th.body, can ? "Claim" : null, can ? (Action)(() => Claim(tier)) : null);
            }
        }
        void Claim(int tier)
        {
            var r = SeasonPassRules.Claim(AppServices.Progress, _s.id, tier, AppServices.Content.Meta.seasonPass);
            if (!r.IsEmpty) Router.Toast($"Tier {tier + 1}: +" + OfflineUI.Bundle(r));
            OnShow();
        }
    }

    /// <summary>Local leaderboard: your best score per level against practice rivals. Not online; nothing leaves the device.</summary>
    public class LocalLeaderboardScreen : UIScreen
    {
        RectTransform _list; Text _lvlLabel; int _idx;
        protected override void Build(RectTransform root)
        {
            var ch = AppServices.Content.Chapter1; _idx = Math.Max(0, Math.Min(ch.levels.Length - 1, WorldMapRules.Frontier(ch, AppServices.Save)));
            for (int i = _idx; i >= 0; i--) if (AppServices.Save.BestScore(ch.levels[i].id) > 0) { _idx = i; break; }
            var v = V2Screens.Frame(root, "LEADERBOARD", Th, () => Router.Pop());
            UIFactory.Label(v, "Local leaderboard: your best on this device vs. practice rivals (bots). Online rankings are coming soon.", Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            var hr = UIFactory.HStack(v, Th.gap, 52f);
            UIFactory.Button(hr, "◀", Th.navy500, () => Step(-1), Th, 48, 48, Th.headingSp).gameObject.name = "Button_LbPrev";
            _lvlLabel = V2.Title(hr, "", Th.headingSp, Th.goldFill, Th); _lvlLabel.GetComponent<LayoutElement>().flexibleWidth = 1f;
            UIFactory.Button(hr, "▶", Th.navy500, () => Step(+1), Th, 48, 48, Th.headingSp).gameObject.name = "Button_LbNext";
            _list = OfflineUI.List(v);
        }
        void Step(int d) { var n = AppServices.Content.Chapter1.levels.Length; _idx = (_idx + d + n) % n; OnShow(); }
        public override void OnShow()
        {
            var l = AppServices.Content.Chapter1.levels[_idx]; var best = AppServices.Save.BestScore(l.id);
            _lvlLabel.text = $"Level {l.number} · {l.name}";
            OfflineUI.Clear(_list);
            foreach (var r in LocalLeaderboard.ForLevel(l, best, AppServices.Progress.Data.profile.name, AppServices.Content.Meta.rivalNames))
                OfflineUI.Row(_list, Th, "Lb_" + r.rank, null, $"#{r.rank}  {r.name}", r.isPlayer ? "You" : "Practice rival", r.isPlayer ? new Color(0.45f, 0.32f, 0.05f, 0.95f) : new Color(0.08f, 0.12f, 0.28f, 0.9f), r.score.ToString("N0"), r.isPlayer ? Th.goldFill : Color.white);
            if (best <= 0) OfflineUI.Row(_list, Th, "Lb_none", null, "Clear this level to post your score", "", new Color(0f, 0f, 0f, 0.35f), "", Color.white);
        }
    }
}
