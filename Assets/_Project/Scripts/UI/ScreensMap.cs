using System;
using System.Collections.Generic;
using Crashverse.Core;
using Crashverse.Data;
using Crashverse.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Crashverse.UI
{
    // ───────────────────────────── Target UI §3/§4: World Map (your journey) ─────────────────────────────
    /// <summary>
    /// Vertical scrolling journey over the art-pack map: winding dotted path, numbered nodes with stars, locks, a skull on every 10th
    /// (hard) level, the Daily Boss node beside the path, and the next season's locked island at the top. Season banner, wallet top bar,
    /// dock (Power-Ups, Seasons, Rewards, More). Class name kept: the smoke waits on CVTAP|BEGIN|MapScreen and taps Button_N.
    /// </summary>
    public class MapScreen : UIScreen
    {
        ScrollRect _sr; RectTransform _content, _bgLayer; Text _coins, _gems, _stars, _name; Image _avatar;
        readonly List<Image> _bgTiles = new List<Image>(); RectTransform _frontierNode; float _lastW = -1f;
        public override bool OnBack() => false; // root: router asks to quit

        protected override void Build(RectTransform root)
        {
            var c = AppServices.Content; var save = AppServices.Save; var ch = c.Chapter1; var meta = c.Meta;
            var fill = UIFactory.Panel("MapFill", root, UITheme.H("2E7BC4")); UIFactory.Stretch(fill.rectTransform); fill.raycastTarget = false;

            // scroll view (full screen; bars float on top)
            var view = UIFactory.Rect("MapView", root); UIFactory.Stretch(view);
            var vimg = view.gameObject.AddComponent<Image>(); vimg.color = new Color(0, 0, 0, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            _content = UIFactory.Rect("Path", view);
            _content.anchorMin = new Vector2(0f, 0f); _content.anchorMax = new Vector2(1f, 0f); _content.pivot = new Vector2(0.5f, 0f);
            int n = ch.levels.Length;
            float h = WorldMapRules.ContentHeight(n);
            _content.sizeDelta = new Vector2(0f, h); _content.anchoredPosition = Vector2.zero;
            _sr = view.gameObject.AddComponent<ScrollRect>();
            _sr.content = _content; _sr.viewport = view; _sr.horizontal = false; _sr.vertical = true; _sr.movementType = ScrollRect.MovementType.Clamped;
            _bgLayer = UIFactory.Rect("Bg", _content); UIFactory.Stretch(_bgLayer);

            // dotted path
            for (int i = 0; i + 1 < n; i++)
                for (int k = 1; k <= 4; k++)
                {
                    float t = k / 5f;
                    var dot = UIFactory.Panel("Dot", _content, new Color(1f, 0.96f, 0.8f, 0.95f)); dot.sprite = V2.Circle; dot.raycastTarget = false;
                    Pin(dot.rectTransform, Mathf.Lerp(WorldMapRules.X(i), WorldMapRules.X(i + 1), t), Mathf.Lerp(WorldMapRules.Y(i), WorldMapRules.Y(i + 1), t), new Vector2(10f, 10f));
                }

            // season banners where each season's levels begin (Seasons 2–4 themed by their bg_season_* art)
            if (meta.seasons != null)
                foreach (var s in meta.seasons)
                {
                    int at = Array.FindIndex(ch.levels, l => l.number == s.firstLevel); if (at <= 0) continue;
                    var ban = V2.RoundPanel("SeasonStart_" + s.number, _content, UITheme.H(string.IsNullOrEmpty(s.accent) ? "FFD34D" : s.accent.TrimStart('#')));
                    var bm = UIFactory.Rect("Mask", ban.transform); UIFactory.Stretch(bm, 3f); bm.gameObject.AddComponent<RectMask2D>();
                    UIArt.Background(bm, s.bg, Color.white);
                    var bl = V2.Title(bm, $"SEASON {s.number} · {s.name.ToUpperInvariant()}", Th.captionSp, Color.white, Th); UIFactory.Stretch(bl.rectTransform, 2f);
                    Pin(ban.rectTransform, 0.5f, (WorldMapRules.Y(at - 1) + WorldMapRules.Y(at)) * 0.5f, new Vector2(240f, 40f));
                }

            int frontier = WorldMapRules.Frontier(ch, save);
            for (int i = 0; i < n; i++) BuildNode(ch.levels[i], i, i == frontier, save);

            // Daily Boss beside the path (opens after Level 1)
            bool dbOpen = save.IsCleared(ch.levels[0].id);
            var db = V2.RoundButton(_content, "", dbOpen ? UITheme.H("7C3AED") : Th.locked, OpenDailyBoss, Th, 84f, "Button_DailyBoss");
            var dbi = UIArt.Icon(db.transform, UIArt.DailyBoss, 80f); UIFactory.Stretch(dbi.rectTransform, 2f);
            if (!dbOpen) dbi.color = new Color(0.45f, 0.45f, 0.5f, 1f);
            Pin((RectTransform)db.transform, WorldMapRules.DailyBossX(frontier), WorldMapRules.DailyBossY(frontier), new Vector2(84f, 84f));
            var dbl = V2.Title(db.transform, AppServices.Progress.HasDailyBossWin(DailyBossRules.DateKey(DateTime.Now)) ? "DEFEATED" : "DAILY BOSS", Th.captionSp, Color.white, Th);
            UIFactory.Place(dbl.rectTransform, new Vector2(0.5f, 0f), new Vector2(120f, 22f), new Vector2(0f, -14f));

            // next season locked island at the top
            SeasonTheme next = null; // the first season not open yet (all open: none)
            if (meta.seasons != null) foreach (var s in meta.seasons) if (!SeasonRules.IsOpen(s, ch, save)) { next = s; break; }
            var ns = V2.RoundButton(_content, "", new Color(0.1f, 0.1f, 0.2f, 0.75f), () => Router.Push(new SeasonsScreen()), Th, 96f, "Button_NextSeason");
            var lk = UIArt.Icon(ns.transform, UIArt.Lock, 64f); UIFactory.Place(lk.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(64f, 64f), Vector2.zero);
            Pin((RectTransform)ns.transform, 0.5f, h - 190f, new Vector2(96f, 96f));
            string nsTxt = next == null ? "MORE SEASONS\nComing soon" : "NEXT SEASON\n" + next.name + (next.comingSoon ? " · Coming soon" : $" · Clear Level {next.firstLevel - 1}");
            var nsl = V2.Title(ns.transform, nsTxt, Th.captionSp, Color.white, Th);
            UIFactory.Place(nsl.rectTransform, new Vector2(0.5f, 0f), new Vector2(220f, 40f), new Vector2(0f, -24f));

            BuildTopBar(root, meta);
            BuildDock(root);
        }

        static void Pin(RectTransform rt, float x01, float y, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(x01, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = new Vector2(0f, y);
        }

        void BuildNode(LevelEntry lvl, int i, bool isFrontier, CampaignSave save)
        {
            var kind = WorldMapRules.KindFor(lvl); var state = WorldMapRules.StateFor(lvl, save);
            float size = kind == MapNodeKind.Normal ? 64f : 76f;
            Color col = state == MapNodeState.Locked ? Th.locked : kind == MapNodeKind.Hard ? UITheme.H("B91C1C") : kind == MapNodeKind.Boss ? UITheme.H("7C3AED")
                      : isFrontier ? Th.goldFill : UITheme.H("2563EB");
            var b = V2.RoundButton(_content, lvl.number.ToString(), col, () => OpenLevel(lvl), Th, size, "Button_" + lvl.number);
            var rt = (RectTransform)b.transform; Pin(rt, WorldMapRules.X(i), WorldMapRules.Y(i), new Vector2(size, size));
            var ring = b.gameObject.AddComponent<Outline>(); ring.effectColor = new Color(1f, 1f, 1f, 0.95f); ring.effectDistance = new Vector2(3f, -3f);
            if (state == MapNodeState.Locked)
            {
                UIFactory.SetLabel(b, "");
                var lk = UIArt.Icon(b.transform, UIArt.Lock, size * 0.6f); UIFactory.Place(lk.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size * 0.6f, size * 0.6f), Vector2.zero);
            }
            if (kind == MapNodeKind.Hard)
            {
                var sk = UIArt.Icon(b.transform, UIArt.Skull, 46f); UIFactory.Place(sk.rectTransform, new Vector2(0.5f, 1f), new Vector2(46f, 46f), new Vector2(0f, 20f));
            }
            var stars = V2.Stars(b.transform, save.BestStars(lvl.id), 20f, 0f);
            UIFactory.Place(stars, new Vector2(0.5f, 0f), new Vector2(64f, 20f), new Vector2(0f, -12f));
            if (state == MapNodeState.Locked) stars.gameObject.SetActive(false);
            if (isFrontier) { _frontierNode = rt; b.gameObject.AddComponent<UiPulse>(); }
        }

        void BuildTopBar(RectTransform root, MetaV2Data meta)
        {
            var top = UIFactory.Rect("TopBar", root); UIFactory.Band(top, true, 56f);
            var row = UIFactory.HStack(top, 6f, 48f); UIFactory.Stretch(row, 4f);
            var pr = AppServices.Progress.Data.profile;
            var av = V2.RoundButton(row, "", Th.goldTrim, () => Router.Push(new ProfileScreen(true)), Th, 48f, "Button_Profile");
            _avatar = UIArt.Icon(av.transform, meta.avatars.Length > pr.avatar ? meta.avatars[pr.avatar] : "avatar_01", 44f); UIFactory.Stretch(_avatar.rectTransform, 2f);
            // QA V2-05: the row has no room for the name on ~360-410 dp phones (it collapsed to one letter per line behind the star
            // pill). The pills keep the bar; the name sits on its own line under the bar, right side (the season ribbon is left).
            UIFactory.Rect("Flex", row).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _name = UIFactory.Label(top, pr.name, Th.captionSp, Color.white, TextAnchor.MiddleRight, Th, true); UIArt.Pop(_name, V2.Ink, 1f);
            _name.horizontalOverflow = HorizontalWrapMode.Overflow; _name.verticalOverflow = VerticalWrapMode.Overflow;
            UIFactory.Place(_name.rectTransform, new Vector2(1f, 0f), new Vector2(170f, 22f), new Vector2(-8f, -26f));
            _stars = V2.Pill(row, UIArt.StarOn, "0", Th, 76f);
            _coins = V2.Pill(row, UIArt.Coin, "0", Th, 84f);
            _gems = V2.Pill(row, UIArt.Gem, "0", Th, 68f);
            UIFactory.Button(row, "☰", Th.navy500, () => Router.Push(new SettingsModal()), Th, 48, 48, Th.bodySp).gameObject.name = "Button_Settings";
#if CRASHVERSE_DEV || DEVELOPMENT_BUILD
            UIFactory.Button(row, "⚙", Th.navy500, () => Router.Push(new QADebugModal()), Th, 48, 48, Th.bodySp); // SEC-02: dev builds only
#endif
            // season banner (ribbon) under the bar
            var s = SeasonRules.Current(meta, AppServices.Content.Chapter1, AppServices.Save);
            var rib = V2.RoundPanel("SeasonBanner", root, UITheme.H("E11D48"));
            UIFactory.Place(rib.rectTransform, new Vector2(0f, 1f), new Vector2(170f, 56f), new Vector2(8f, -62f));
            var rl = V2.Title(rib.transform, s != null ? $"SEASON {s.number}\n{s.name.ToUpperInvariant()}" : "SEASON 1", Th.captionSp, Th.goldFill, Th); UIFactory.Stretch(rl.rectTransform, 2f);
        }

        void BuildDock(RectTransform root)
        {
            var dock = V2.RoundPanel("Dock", root, new Color(Th.navy900.r, Th.navy900.g, Th.navy900.b, 0.88f));
            UIFactory.Band(dock.rectTransform, false, 64f, 4f, 6f);
            var dr = UIFactory.HStack(dock.transform, Th.gap, 56f); UIFactory.Stretch(dr, 4f);
            V2.Glossy(dr, "Power-Ups", Th.violet, () => Router.Push(new PowerUpsScreen()), Th, 52f, 0f, Th.captionSp, "Button_PowerUps");
            V2.Glossy(dr, "Seasons", Th.cyan, () => Router.Push(new SeasonsScreen()), Th, 52f, 0f, Th.captionSp, "Button_Seasons");
            V2.Glossy(dr, "Rewards", Th.orange, () => Router.Push(new ProgressionScreen()), Th, 52f, 0f, Th.captionSp, "Button_Rewards");
            V2.Glossy(dr, "More", Th.navy500, OpenMore, Th, 52f, 0f, Th.captionSp, "Button_More");
        }

        void OpenMore()
        {
            bool live = AppServices.TimeSynced && AppServices.Backend != null && AppServices.Backend.IsOnline; // SG-20: time-gated online UI
            Action gate(Func<UIScreen> f) => () => { if (live) Router.Push(f()); else Router.Toast("Online features are coming soon"); };
            Router.Push(new ListModal("More", new List<(string, Action)>
            {
                ("Daily Titan (online)", gate(() => new TitanScreen())),
                ("Daily Login", () => Router.Push(new LocalDailyLoginScreen())),        // offline: local date, rollback-safe
                ("Season Pass (free)", () => Router.Push(new LocalSeasonPassScreen())),  // offline: free track only
                ("Leaderboard (local)", () => Router.Push(new LocalLeaderboardScreen())), // offline: your best vs practice rivals
                ("Store", () => Router.Push(new StoreScreen())),
                ("Settings", () => Router.Push(new SettingsModal())),
            }));
        }

        void OpenLevel(LevelEntry lvl)
        {
            if (UnlockRules.IsLevelUnlocked(lvl, AppServices.Save)) Router.Push(new LevelSelectScreen(lvl));
            else { var req = AppServices.Content.GetLevel(lvl.requires); Router.Toast(req != null ? $"Clear Level {req.number} to unlock" : "Locked"); }
        }

        void OpenDailyBoss()
        {
            if (!AppServices.Save.IsCleared(AppServices.Content.Chapter1.levels[0].id)) { Router.Toast("Clear Level 1 to unlock the Daily Boss"); return; }
            Router.Push(new DailyBossScreen());
        }

        public override void OnShow()
        {
            var p = AppServices.Progress.Data;
            _stars.text = UnlockRules.TotalStars(AppServices.Content.Chapter1, AppServices.Save) + "/" + AppServices.Content.Chapter1.levels.Length * 3;
            _coins.text = p.coins.ToString(); _gems.text = p.gems.ToString(); _name.text = UIFactory.Truncate(p.profile.name, 16);
            var meta = AppServices.Content.Meta; _avatar.sprite = UIArt.Get(meta.avatars.Length > p.profile.avatar ? meta.avatars[p.profile.avatar] : "avatar_01");
            Canvas.ForceUpdateCanvases(); LayoutBackground(); FocusFrontier();
        }

        public override void Tick() { if (_content != null && Mathf.Abs(_content.rect.width - _lastW) > 0.5f) { LayoutBackground(); FocusFrontier(); } }

        /// <summary>Cover the path with stacked copies of the map art (alternate copies mirrored so the seams match).</summary>
        void LayoutBackground()
        {
            float w = _content.rect.width; if (w <= 1f) return; _lastW = w;
            var sp = UIArt.Get(UIArt.WorldMap); float tileH = sp != null ? w * sp.rect.height / Mathf.Max(1f, sp.rect.width) : w * 2.1667f;
            int need = Mathf.CeilToInt(_content.rect.height / tileH);
            while (_bgTiles.Count < need)
            {
                var img = UIFactory.Panel("MapTile", _bgLayer, Color.white); img.sprite = sp; img.raycastTarget = false; _bgTiles.Add(img);
            }
            for (int i = 0; i < _bgTiles.Count; i++)
            {
                var rt = _bgTiles[i].rectTransform; rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(0f, tileH); rt.anchoredPosition = new Vector2(0f, tileH * (i + 0.5f));
                rt.localScale = new Vector3(1f, i % 2 == 0 ? 1f : -1f, 1f);
                _bgTiles[i].gameObject.SetActive(i < need);
            }
        }

        void FocusFrontier()
        {
            if (_sr == null || _frontierNode == null) return;
            float viewH = ((RectTransform)_sr.transform).rect.height, h = _content.rect.height;
            if (h <= viewH) { _sr.verticalNormalizedPosition = 0f; return; }
            float y = _frontierNode.anchoredPosition.y - viewH * 0.4f;
            _sr.verticalNormalizedPosition = Mathf.Clamp01(y / (h - viewH));
        }
    }

    /// <summary>Gentle scale pulse for the "you are here" node.</summary>
    public class UiPulse : MonoBehaviour
    {
        void Update() { float s = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f); transform.localScale = new Vector3(s, s, 1f); }
    }

    // ───────────────────────────── Target UI §5: Level Selection ─────────────────────────────
    /// <summary>
    /// Level N, stars, a front-view preview of the layout, steps, and a 4-tile power-up picker (carousel over the 8 power-ups).
    /// The loadout rule is unchanged: one Core, one Module, one Booster; tapping a tile equips it into its slot (tap again to remove).
    /// </summary>
    public class LevelSelectScreen : UIScreen
    {
        readonly LevelEntry _lvl; Loadout _lo; int _page; RectTransform _tiles; Text _slots, _pageLbl;
        const int PerPage = 4;
        public LevelSelectScreen(LevelEntry lvl) { _lvl = lvl; }
        public LevelEntry Level => _lvl;
        public Loadout Loadout => _lo;

        protected override void Build(RectTransform root)
        {
            var save = AppServices.Save; var db = AppServices.Content;
            _lo = Loadout.Resolve(_lvl, save.GetLoadout(_lvl.id) ?? save.Data.lastLoadout, db, save); // SG-09
            bool hard = HardLevelRules.IsHard(_lvl);
            var season = SeasonRules.ForLevel(db.Meta, _lvl.number);
            UIArt.Background(root, season?.bg ?? "bg_season_sunny_shores", hard ? new Color(0.45f, 0.3f, 0.35f, 1f) : new Color(0.75f, 0.8f, 0.9f, 1f));
            var card = V2.Card("LevelSelect", root, Th); UIFactory.Stretch(card.rectTransform, 14f);
            var v = V2.Split(card.transform, 132f, out var foot, 12f);
            var head = UIFactory.HStack(v, Th.gap, 52f);
            var prev = PrevNext(-1); var nxt = PrevNext(+1);
            UIFactory.Button(head, "◀", Th.navy500, () => Go(prev), Th, 48, 48, Th.headingSp).interactable = prev != null;
            var tl = V2.Title(head, (hard ? "HARD · " : "") + "Level " + _lvl.number, Th.titleSp, hard ? UITheme.H("FCA5A5") : Th.goldFill, Th);
            tl.GetComponent<LayoutElement>().flexibleWidth = 1f;
            UIFactory.Button(head, "▶", Th.navy500, () => Go(nxt), Th, 48, 48, Th.headingSp).interactable = nxt != null;
            UIFactory.Label(v, UIFactory.Truncate(_lvl.name, 32), Th.bodySp, Color.white, TextAnchor.MiddleCenter, Th, true);
            V2.Stars(v, save.BestStars(_lvl.id), 40f);
            V2.LayoutPreview(v, db.GetLayout(_lvl.layout), new Vector2(300f, 190f), hard);
            int shots = new RunContext { mode = RunMode.Campaign, level = _lvl }.ShotBudget;
            UIFactory.Label(v, $"Steps: {shots}   ·   Destroy {Mathf.RoundToInt(_lvl.winThreshold * 100)}%" + (hard ? "   ·   Hard: 1 fewer step" : ""), Th.bodySp, Color.white, TextAnchor.MiddleCenter, Th);
            int s3 = StarRules.MinShotsLeft(shots, _lvl.star3Pct), s2 = StarRules.MinShotsLeft(shots, _lvl.star2Pct);
            UIFactory.Label(v, $"★★★ {s3}+ steps left   ★★ {s2}+   ★ any win" + (_lvl.starCapIfPowerupUsed < 3 ? $"\nUsing a power-up caps this level at {_lvl.starCapIfPowerupUsed}★" : ""), Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            if (save.Get(_lvl.id)?.hintPending == true) UIFactory.Label(v, "💡 Free hint ready for this attempt", Th.captionSp, Th.goldFill, TextAnchor.MiddleCenter, Th);
            V2.Title(v, "Select Power Ups", Th.headingSp, Color.white, Th);
            var pick = UIFactory.HStack(v, 4f, 72f);
            UIFactory.Button(pick, "‹", Th.navy500, () => Page(-1), Th, 72, 32, Th.headingSp).gameObject.name = "Button_PUPrev";
            _tiles = UIFactory.HStack(pick, 6f, 72f); _tiles.GetComponent<LayoutElement>().flexibleWidth = 1f;
            UIFactory.Button(pick, "›", Th.navy500, () => Page(+1), Th, 72, 32, Th.headingSp).gameObject.name = "Button_PUNext";
            _slots = UIFactory.Label(v, "", Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            _pageLbl = _slots;
            V2.Glossy(foot, "Play", Th.green, Play, Th, 60f, 0f, Th.titleSp, "Button_Play");
            UIFactory.Button(foot, "Back", Th.navy500, () => Router.Pop(), Th, 48);
            Refresh();
        }

        LevelEntry PrevNext(int d)
        {
            var ls = AppServices.Content.Chapter1.levels; int i = Array.IndexOf(ls, _lvl) + d;
            return i >= 0 && i < ls.Length && UnlockRules.IsLevelUnlocked(ls[i], AppServices.Save) ? ls[i] : null;
        }
        void Go(LevelEntry l) { if (l != null) Router.Replace(new LevelSelectScreen(l)); }

        void Page(int d)
        {
            int pages = Mathf.Max(1, (PowerUpRulesV2.InUnlockOrder(AppServices.Content.PowerUps).Length + PerPage - 1) / PerPage);
            _page = (_page + d + pages) % pages; Refresh();
        }

        void Refresh()
        {
            var db = AppServices.Content; var save = AppServices.Save;
            for (int i = _tiles.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_tiles.GetChild(i).gameObject);
            var all = PowerUpRulesV2.InUnlockOrder(db.PowerUps);
            for (int i = _page * PerPage; i < Math.Min(all.Length, (_page + 1) * PerPage); i++)
            {
                var p = all[i]; var slot = Loadout.ParseSlot(p.slot);
                bool unlocked = UnlockRules.IsPowerUpUnlocked(p, db, save);
                bool sel = slot.HasValue && _lo[slot.Value] == p.id;
                V2.PowerUpTile(_tiles, p, unlocked, sel, () => Toggle(p), Th, 68f);
            }
            var parts = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var s = (Slot)i; var p = db.GetPowerUp(_lo[s]);
                parts.Add(Loadout.SlotNames[i] + ": " + (Loadout.IsSlotLocked(_lvl, s) ? "🔒" : p != null ? p.name + (Loadout.ForcedTile(_lvl, s) != null ? " (set)" : "") : "–"));
            }
            _slots.text = string.Join("   ", parts);
        }

        void Toggle(PowerUpData p)
        {
            var slot = Loadout.ParseSlot(p.slot); if (!slot.HasValue) return;
            var s = slot.Value; var db = AppServices.Content; var save = AppServices.Save;
            if (Loadout.IsSlotLocked(_lvl, s)) { Router.Toast(Loadout.SlotNames[(int)s] + " slot is locked on this level"); return; }
            if (Loadout.ForcedTile(_lvl, s) != null) { Router.Toast("This level sets the " + Loadout.SlotNames[(int)s]); return; }
            var err = _lo[s] == p.id ? _lo.TryEquip(s, null, _lvl, db, save) : _lo.TryEquip(s, p, _lvl, db, save);
            if (err != null) Router.Toast(err);
            Refresh();
        }

        void Play()
        {
            var save = AppServices.Save;
            save.SetLoadout(_lo.ToSaved(_lvl.id)); save.Data.lastLoadout = _lo.ToSaved(_lvl.id); save.Data.lastPlayedLevel = _lvl.id; save.Save();
            var ctx = new RunContext
            {
                mode = RunMode.Campaign, level = _lvl, layout = AppServices.Content.GetLayout(_lvl.layout), loadout = _lo,
                hintArmed = save.ConsumeHint(_lvl.id),
            };
            if (ctx.layout == null) { Router.Toast("Missing layout " + _lvl.layout); return; }
            Router.Pop();
            GameplayDirector.StartRun(ctx);
        }
    }

    // ───────────────────────────── Target UI §8: Daily Boss (changes every day) ─────────────────────────────
    /// <summary>A boss level seeded by the device's local date (offline; the server stays off in v2). First win of the day pays the reward.</summary>
    public class DailyBossScreen : UIScreen
    {
        LevelEntry _lvl; string _key;
        public LevelEntry Level => _lvl;
        protected override void Build(RectTransform root)
        {
            var db = AppServices.Content; _key = DailyBossRules.DateKey(DateTime.Now);
            _lvl = DailyBossRules.ForDate(_key, db.Meta.dailyBoss);
            UIArt.Background(root, "bg_season_primeval_peaks", new Color(0.55f, 0.3f, 0.35f, 1f));
            var card = V2.Card("DailyBoss", root, Th); UIFactory.Stretch(card.rectTransform, 14f);
            var v = V2.Split(card.transform, 132f, out var foot, 12f);
            var ic = UIArt.Icon(v, UIArt.DailyBoss, 96f);
            V2.Title(v, "DAILY BOSS", Th.titleSp, UITheme.H("FCA5A5"), Th);
            if (_lvl == null) { UIFactory.Label(v, "No boss today", Th.bodySp, Color.white, TextAnchor.MiddleCenter, Th); UIFactory.Button(foot, "Back", Th.navy500, () => Router.Pop(), Th, 48); return; }
            V2.Title(v, _lvl.name, Th.headingSp, Color.white, Th);
            UIFactory.Label(v, "Changes every day · " + DateTime.Now.ToString("ddd d MMM"), Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            V2.LayoutPreview(v, db.GetLayout(_lvl.layout), new Vector2(300f, 180f), true);
            UIFactory.Label(v, $"Steps: {_lvl.shots}   ·   Knock its HP to 0 (destroy {Mathf.RoundToInt(_lvl.winThreshold * 100)}%)", Th.bodySp, Color.white, TextAnchor.MiddleCenter, Th);
            bool done = AppServices.Progress.HasDailyBossWin(_key);
            var r = db.Meta.dailyBoss.reward;
            UIFactory.Label(v, done ? "Defeated today! Come back tomorrow for a new boss. (Replays give no reward.)" : $"Reward: {r.coins} coins · {r.gems} gems · {r.chests} chest", Th.bodySp, done ? Th.green : Th.goldFill, TextAnchor.MiddleCenter, Th);
            V2.Glossy(foot, done ? "Replay" : "Fight", UITheme.H("DC2626"), Fight, Th, 60f, 0f, Th.titleSp, "Button_Fight");
            UIFactory.Button(foot, "Back", Th.navy500, () => Router.Pop(), Th, 48);
        }

        void Fight()
        {
            var db = AppServices.Content; var save = AppServices.Save;
            var lo = Loadout.Resolve(_lvl, save.Data.lastLoadout, db, save);
            var ctx = new RunContext { mode = RunMode.DailyBoss, dailyBoss = true, level = _lvl, layout = db.GetLayout(_lvl.layout), loadout = lo };
            if (ctx.layout == null) { Router.Toast("Missing layout " + _lvl.layout); return; }
            Router.Pop();
            GameplayDirector.StartRun(ctx);
        }
    }
}
