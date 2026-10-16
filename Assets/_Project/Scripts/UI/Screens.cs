using System;
using System.Collections.Generic;
using Crashverse.Backend;
using Crashverse.Core;
using Crashverse.Data;
using Crashverse.Meta;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crashverse.UI
{
    // ───────────────────────────── Map (mockup 02) ─────────────────────────────
    /// <summary>Island map: node buttons projected on 3D anchors, drag-to-pan (≥10dp threshold via EventSystem), gates, live-ops entry points.</summary>
    public class MapScreen : UIScreen
    {
        public override bool BlocksWorldInput => false;
        readonly Dictionary<string, RectTransform> _nodes = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, RectTransform> _gates = new Dictionary<string, RectTransform>();
        RectTransform _layer; Text _stars, _wallet; Button _titanBtn, _dailyBtn, _seasonBtn;

        protected override void Build(RectTransform root)
        {
            var c = AppServices.Content; var save = AppServices.Save;
            var drag = UIFactory.Panel("PanSurface", root, new Color(0, 0, 0, 0)); UIFactory.Stretch(drag.rectTransform);
            drag.gameObject.AddComponent<MapPanSurface>();
            _layer = UIFactory.Rect("Nodes", root); UIFactory.Stretch(_layer);
            foreach (var l in c.Chapter1.levels)
            {
                var lvl = l;
                bool open = UnlockRules.IsLevelUnlocked(lvl, save);
                float size = lvl.IsBoss ? Th.bossNode : Th.mapNode;
                var b = UIFactory.Button(_layer, open ? lvl.number.ToString() : "🔒", open ? (lvl.IsBoss ? Th.red : Th.goldShade) : Th.locked,
                    () => { if (UnlockRules.IsLevelUnlocked(lvl, save)) Router.Push(new BriefingScreen(lvl, RunMode.Campaign, null)); else Router.Toast(LockReason(lvl)); },
                    Th, size, size);
                var rt = (RectTransform)b.transform; rt.sizeDelta = new Vector2(size, size + 16);
                var st = UIFactory.Label(b.transform, UIFactory.Stars(save.BestStars(lvl.id)) + (save.Get(lvl.id)?.mastery == true ? " ◆" : ""), Th.captionSp, Th.goldFill, TextAnchor.UpperCenter, Th);
                UIFactory.Place(st.rectTransform, new Vector2(0.5f, 0f), new Vector2(110, 20), new Vector2(0, -20));
                _nodes[lvl.id] = rt;
            }
            foreach (var isl in c.Chapter1.islands)
            {
                if (string.IsNullOrEmpty(isl.gateAfterLevel) || UnlockRules.IsIslandOpen(isl, save)) continue;
                var lab = UIFactory.Label(_layer, "🔒 Clear L" + c.GetLevel(isl.gateAfterLevel)?.number, Th.captionSp, Th.white, TextAnchor.MiddleCenter, Th);
                lab.rectTransform.sizeDelta = new Vector2(110, 24);
                _gates[isl.id] = lab.rectTransform;
            }
            // top bar
            var top = UIFactory.Panel("TopBar", root, new Color(Th.navy900.r, Th.navy900.g, Th.navy900.b, 0.85f));
            UIFactory.Band(top.rectTransform, true, 56f);
            var tr = UIFactory.HStack(top.transform, Th.gap, 56f); UIFactory.Stretch(tr, 4f);
            _stars = UIFactory.Label(tr, "", Th.bodySp, Th.goldFill, TextAnchor.MiddleLeft, Th, true);
            _wallet = UIFactory.Label(tr, "", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
            UIFactory.Button(tr, "Store", Th.violet, () => Router.Push(new StoreScreen()), Th, 48, 80, Th.bodySp);
            UIFactory.Button(tr, "Settings", Th.navy500, () => Router.Push(new SettingsModal()), Th, 48, 96, Th.captionSp);
#if CRASHVERSE_DEV || DEVELOPMENT_BUILD
            UIFactory.Button(tr, "⚙", Th.navy500, () => Router.Push(new QADebugModal()), Th, 48, 48, Th.bodySp); // SEC-02: dev builds only
#endif
            // bottom dock
            var dock = UIFactory.Panel("Dock", root, new Color(Th.navy900.r, Th.navy900.g, Th.navy900.b, 0.85f));
            UIFactory.Band(dock.rectTransform, false, 64f);
            var dr = UIFactory.HStack(dock.transform, Th.gap, 56f); UIFactory.Stretch(dr, 4f);
            _titanBtn = UIFactory.Button(dr, "Titan", Th.orange, () => Router.Push(new TitanScreen()), Th, 52, 0, Th.bodySp);
            _dailyBtn = UIFactory.Button(dr, "Daily", Th.green, () => Router.Push(new DailyRewardsScreen()), Th, 52, 0, Th.bodySp);
            _seasonBtn = UIFactory.Button(dr, "Season", Th.cyan, () => Router.Push(new SeasonScreen()), Th, 52, 0, Th.bodySp);
            UIFactory.Button(dr, "Ranks", Th.navy500, () => Router.Push(new LeaderboardScreen()), Th, 52, 0, Th.bodySp);
            UIFactory.Button(dr, "◎", Th.navy500, () => GameplayDirector.Map.FocusLevel(save.Data.lastPlayedLevel), Th, 52, 52, Th.bodySp); // recenter
        }

        string LockReason(LevelEntry l)
        {
            var req = AppServices.Content.GetLevel(l.requires);
            return req != null ? $"Clear Level {req.number} to unlock" : "Locked";
        }

        public override async void OnShow()
        {
            _stars.text = "★ " + UnlockRules.TotalStars(AppServices.Content.Chapter1, AppServices.Save) + "/30";
            try { var w = await AppServices.Backend.GetWallet(); _wallet.text = $"🪙{w.coins}  💎{w.shards}"; }
            catch (BackendException) { _wallet.text = "offline"; }
            bool live = AppServices.TimeSynced && AppServices.Backend.IsOnline; // SG-20: time-gated UI disabled while unsynced
            _titanBtn.interactable = live; _dailyBtn.interactable = live; _seasonBtn.interactable = live;
        }

        public override void Tick()
        {
            var cam = Camera.main; var map = GameplayDirector.Map;
            if (cam == null || map == null) return;
            float scale = Router.Canvas.scaleFactor;
            foreach (var kv in _nodes) Project(cam, map.NodePositions[kv.Key], kv.Value, scale);
            foreach (var kv in _gates) if (map.GatePositions.TryGetValue(kv.Key, out var p)) Project(cam, p, kv.Value, scale);
        }

        void Project(Camera cam, Vector3 world, RectTransform rt, float scale)
        {
            var sp = cam.WorldToScreenPoint(world);
            rt.gameObject.SetActive(sp.z > 0);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            var safe = Screen.safeArea;
            rt.anchoredPosition = new Vector2((sp.x - safe.x) / scale, (sp.y - safe.y) / scale);
        }

        public override bool OnBack() => false; // root: router asks to quit
    }

    /// <summary>Drag pans the map camera; EventSystem's drag threshold (10dp) keeps taps on nodes from panning.</summary>
    public class MapPanSurface : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e)
        {
            var map = GameplayDirector.Map; if (map == null) return;
            float dpi = Screen.dpi > 1 ? Screen.dpi : 160f;
            map.Pan(-e.delta.x / dpi * 160f * Crashverse.Core.CameraConfig.Current.mapPanUnitsPerDp);
        }
    }

    // ───────────────────────────── Briefing / loadout (mockup 03) ─────────────────────────────
    public class BriefingScreen : UIScreen
    {
        readonly LevelEntry _lvl; readonly RunMode _mode; readonly DailyTitanConfig _titan; RankedTicket _ticket;
        readonly RuleModifiers _mod;
        Loadout _lo; int _extraSlot = -1; Button[] _slotBtns = new Button[3]; Button _plusBtn;
        public BriefingScreen(LevelEntry lvl, RunMode mode, DailyTitanConfig titan, RuleModifiers mod = null, RankedTicket ticket = null)
        { _lvl = lvl; _mode = mode; _titan = titan; _mod = mod ?? titan?.mod ?? new RuleModifiers(); _ticket = ticket; }

        protected override void Build(RectTransform root)
        {
            var save = AppServices.Save; var db = AppServices.Content;
            _lo = Loadout.Resolve(_lvl, save.GetLoadout(_lvl.id) ?? save.Data.lastLoadout, db, save); // SG-09
            var bg = UIFactory.TrimPanel("Briefing", root, Th); UIFactory.Stretch(bg.rectTransform, 12f);
            var v = UIFactory.VStack(bg.transform, Th.gap, 16f); UIFactory.Stretch(v);
            UIFactory.Label(v, (_lvl.number > 0 ? $"Level {_lvl.number}: " : "") + UIFactory.Truncate(_lvl.name, 32), Th.titleSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            int shots = new RunContext { mode = _mode, level = _lvl, mod = _mod }.ShotBudget;
            UIFactory.Label(v, $"Shots: {shots}   Win: destroy {Mathf.RoundToInt(_lvl.winThreshold * 100)}%", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
            // star bands from per-level thresholds (SG-01)
            int s3 = StarRules.MinShotsLeft(shots, _lvl.star3Pct), s2 = StarRules.MinShotsLeft(shots, _lvl.star2Pct);
            UIFactory.Label(v, $"★★★ {s3}+ shots left   ★★☆ {s2}+   ★☆☆ any win" + (_lvl.starCapIfPowerupUsed < 3 ? $"\nUsing a power-up caps this level at {_lvl.starCapIfPowerupUsed}★" : ""), Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            if (_lvl.mastery != null) UIFactory.Label(v, "◆ Mastery: " + _lvl.mastery.text, Th.captionSp, Th.cyan, TextAnchor.MiddleCenter, Th);
            if (_titan != null) UIFactory.Label(v, "Rule: " + _titan.ruleText, Th.bodySp, Th.orange, TextAnchor.MiddleCenter, Th);
            bool hint = save.Get(_lvl.id)?.hintPending == true && !(_mode == RunMode.TitanRanked);
            if (hint) UIFactory.Label(v, "💡 Free hint ready for this attempt", Th.captionSp, Th.goldFill, TextAnchor.MiddleCenter, Th);
            UIFactory.Spacer(v, 8);
            var row = UIFactory.HStack(v, Th.gap, Th.loadoutSlot);
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                _slotBtns[i] = UIFactory.Button(row, "", Th.navy500, () => OpenPicker((Slot)idx), Th, Th.loadoutSlot, 0, Th.captionSp);
            }
            _plusBtn = UIFactory.Button(v, "+1 Charge", Th.violet, OpenExtraCharge, Th, 48, 0, Th.bodySp);
            _plusBtn.gameObject.SetActive(_mode != RunMode.TitanRanked);
            UIFactory.Spacer(v, 8);
            var go = UIFactory.HStack(v, Th.gap * 2, Th.buttonL);
            UIFactory.Button(go, "Back", Th.navy500, () => Router.Pop(), Th);
            UIFactory.Button(go, _mode == RunMode.TitanRanked ? "Start Ranked" : "Play", Th.green, Play, Th);
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                var s = (Slot)i;
                var p = AppServices.Content.GetPowerUp(_lo[s]);
                bool locked = Loadout.IsSlotLocked(_lvl, s); bool forced = Loadout.ForcedTile(_lvl, s) != null;
                string label = Loadout.SlotNames[i] + "\n" + (locked ? "🔒 Locked" : p != null ? p.name : "Empty") + (forced ? " (set)" : "") + (_extraSlot == i ? " +1" : "");
                UIFactory.SetLabel(_slotBtns[i], label);
                _slotBtns[i].interactable = !locked && !forced;
            }
        }

        void OpenPicker(Slot s)
        {
            var items = new List<(string, Action)> { ("Empty", () => { _lo.TryEquip(s, null, _lvl, AppServices.Content, AppServices.Save); Refresh(); }) };
            foreach (var p in AppServices.Content.PowerUps.powerups)
            {
                if (Loadout.ParseSlot(p.slot) != s) continue;
                var pu = p;
                bool unlocked = UnlockRules.IsPowerUpUnlocked(pu, AppServices.Content, AppServices.Save);
                items.Add(((unlocked ? "" : "🔒 ") + pu.name + $" ×{pu.charges}", () =>
                {
                    var err = _lo.TryEquip(s, pu, _lvl, AppServices.Content, AppServices.Save);
                    if (err != null) Router.Toast(err); Refresh();
                }));
            }
            Router.Push(new ListModal(Loadout.SlotNames[(int)s], items));
        }

        void OpenExtraCharge()
        {
            var items = new List<(string, Action)>();
            for (int i = 0; i < 3; i++)
            {
                int idx = i; var p = AppServices.Content.GetPowerUp(_lo[(Slot)i]);
                if (p != null) items.Add(($"{Loadout.SlotNames[i]}: {p.name}", () => { _extraSlot = idx; Refresh(); }));
            }
            if (items.Count == 0) { Router.Toast("Equip a power-up first"); return; }
            Router.Push(new ListModal("+1 Charge (1 per level)", items));
        }

        async void Play()
        {
            var save = AppServices.Save; var db = AppServices.Content;
            if (_mode == RunMode.Campaign) { save.SetLoadout(_lo.ToSaved(_lvl.id)); save.Data.lastLoadout = _lo.ToSaved(_lvl.id); save.Data.lastPlayedLevel = _lvl.id; save.Save(); }
            int extra = -1;
            if (_extraSlot >= 0)
            {
                try { if (await AppServices.Backend.ConsumeExtraCharge()) extra = _extraSlot; else Router.Toast("No +1 charges left"); }
                catch (BackendException) { Router.Toast("Offline: +1 charge unavailable"); }
            }
            if (_mode == RunMode.TitanRanked && _ticket == null)
            {
                Router.Push(new ConfirmModal("Ranked Attempt", "This uses today's only ranked attempt. If the app closes, your progress so far is submitted.", "Start", async () =>
                {
                    try { _ticket = await AppServices.Backend.BeginRankedRun(); Launch(extra); }
                    catch (BackendException e) { Router.Toast(e.Message); }
                }));
                return;
            }
            Launch(extra);
        }

        void Launch(int extra)
        {
            var ctx = new RunContext
            {
                mode = _mode, level = _lvl, layout = AppServices.Content.GetLayout(_lvl.layout), mod = _mod, loadout = _lo, extraChargeSlot = extra,
                hintArmed = _mode != RunMode.TitanRanked && AppServices.Save.ConsumeHint(_lvl.id), titan = _titan, ticket = _ticket
            };
            if (ctx.layout == null) { Router.Toast("Missing layout " + _lvl.layout); return; }
            Router.Pop();
            GameplayDirector.StartRun(ctx);
        }
    }

    /// <summary>Simple vertical picker modal.</summary>
    public class ListModal : UIScreen
    {
        readonly string _title; readonly List<(string label, Action act)> _items;
        public override bool IsModal => true;
        public ListModal(string title, List<(string, Action)> items) { _title = title; _items = items; }
        protected override void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.6f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("List", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(300, 420), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap, 12f); UIFactory.Stretch(v);
            UIFactory.Label(v, _title, Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            var list = UIFactory.Scroll(v, Th.gap, out var sr); sr.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            foreach (var it in _items) { var a = it.act; UIFactory.Button(list, it.label, Th.navy500, () => { Router.Pop(); a(); }, Th, 48, 0, Th.bodySp); }
            UIFactory.Button(v, "Close", Th.navy500, () => Router.Pop(), Th, 48);
        }
    }

    // ───────────────────────────── HUD (mockup 04) ─────────────────────────────
    public class HudScreen : UIScreen
    {
        readonly GameSession _s; Text _shots, _pct, _combo, _status; Image _bar; Button _fire; Button[] _pu = new Button[3];
        // 06 UI motion (unscaled time, Reduce Motion variants): combo pop, % bar fill + win tick pop, shots punch.
        readonly Crashverse.Feel.ComboUiStateMachine _comboSm = new Crashverse.Feel.ComboUiStateMachine();
        RectTransform _tick; Image _tickImg; Crashverse.Feel.FloatTween _fill; float _fillShown, _fillTarget = -1f, _tickMs = -1f, _shotMs = -1f;
        bool _tickHit; int _lastShots = -1;
        public override bool BlocksWorldInput => false;
        public HudScreen(GameSession s) { _s = s; }

        protected override void Build(RectTransform root)
        {
            var top = UIFactory.Panel("Top", root, new Color(0, 0, 0, 0)); UIFactory.Band(top.rectTransform, true, 64f, 0f, Th.edgeGestureInset);
            top.raycastTarget = false;
            var tr = UIFactory.HStack(top.transform, Th.gap, 56f); UIFactory.Stretch(tr);
            UIFactory.Button(tr, "II", Th.navy500, () => Router.Push(new PauseModal(_s)), Th, 48, 48);
            _shots = UIFactory.Label(tr, "", Th.hudNumberSp, Th.white, TextAnchor.MiddleCenter, Th, true);
            _pct = UIFactory.Label(tr, "", Th.hudNumberSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            var barBg = UIFactory.Panel("Bar", root, Th.navy700); UIFactory.Band(barBg.rectTransform, true, 10f, 64f, 24f); barBg.raycastTarget = false;
            _bar = UIFactory.Panel("Fill", barBg.transform, Th.green); UIFactory.Stretch(_bar.rectTransform); _bar.raycastTarget = false;
            _bar.type = Image.Type.Filled; _bar.fillMethod = Image.FillMethod.Horizontal;
            var tick = UIFactory.Panel("WinTick", barBg.transform, Th.white); tick.raycastTarget = false; _tickImg = tick; _tick = tick.rectTransform;
            var trt = tick.rectTransform; trt.anchorMin = new Vector2(_s.Ctx.level.winThreshold, 0); trt.anchorMax = new Vector2(_s.Ctx.level.winThreshold, 1); trt.sizeDelta = new Vector2(2, 6);
            _status = UIFactory.Label(root, "", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th); UIFactory.Band(_status.rectTransform, true, 24f, 80f);
            _combo = UIFactory.Label(root, "", Th.titleSp, Th.violet, TextAnchor.MiddleCenter, Th, true); // COMBO is violet (asset_list ui_label_combo)
            _combo.rectTransform.localScale = Vector3.zero; UIFactory.Place(_combo.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(240, 48), Vector2.zero);
            // bottom: power-ups left, FIRE right (thumb zone)
            var pu = UIFactory.HStack(root, Th.gap, 64f); UIFactory.Place(pu, new Vector2(0f, 0f), new Vector2(220, 64), new Vector2(Th.edgeGestureInset, Th.edgeGestureInset + 8));
            for (int i = 0; i < 3; i++) { int idx = i; _pu[i] = UIFactory.Button(pu, "", Th.navy500, () => _s.PowerUps.ToggleArm(idx), Th, 64, 64, Th.captionSp); }
            _fire = UIFactory.Button(root, "FIRE", Th.orange, () => _s.TryFire(), Th, 88, 88, Th.titleSp);
            UIFactory.Place((RectTransform)_fire.transform, new Vector2(1f, 0f), new Vector2(88, 88), new Vector2(-Th.edgeGestureInset, Th.edgeGestureInset + 8));
            _s.Changed += Refresh; _s.Ended += OnEnded;
            _s.tracker.ComboChanged += n => { if (n >= ComboTracker.ShowFrom) _combo.text = "COMBO x" + n; _comboSm.OnCombo(n); }; // shows from x3; pop sizes escalate at x5/x10/x20
            Refresh();
        }

        void Refresh()
        {
            if (_s == null || _s.Ctx == null) return;
            int total = _s.Ctx.ShotBudget;
            if (_lastShots >= 0 && _s.ShotsLeft < _lastShots) { _shotMs = 0f; _comboSm.OnShot(); Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.UiShotSpend); } // new shot: punch + combo out
            if (_lastShots >= 0 && _s.ShotsLeft > _lastShots) { _tickHit = false; _fillTarget = -1f; _tickImg.color = Th.white; } // Retry: tick re-arms
            _lastShots = _s.ShotsLeft;
            int stars = StarRules.Compute(true, _s.ShotsLeft, total, _s.Ctx.level.star3Pct, _s.Ctx.level.star2Pct);
            _shots.text = $"Shots {_s.ShotsLeft}/{total}  {UIFactory.Stars(stars)}";
            for (int i = 0; i < 3; i++)
            {
                var st = _s.PowerUps.Slots[i];
                _pu[i].gameObject.SetActive(st != null);
                if (st == null) continue;
                UIFactory.SetLabel(_pu[i], $"{UIFactory.Truncate(st.data.name, 9)}\n×{st.charges}");
                _pu[i].image.color = st.armed ? Th.goldShade : Th.navy500;
                _pu[i].interactable = _s.State == SessionState.Aiming && (st.charges > 0 || st.armed);
            }
            _fire.interactable = _s.State == SessionState.Aiming && _s.ShotsLeft > 0;
            _status.text = _s.State == SessionState.Resolving ? "Settling…" : _s.State == SessionState.BossTell ? "The Tidewarden shifts!" :
                _s.Boss != null ? $"Core hits {_s.Boss.CoreHits}/{Crashverse.Boss.TidewardenController.CoreHitsToWin} · Shutter {(_s.Boss.ShutterOpen ? "OPEN" : "closed")}" :
                _s.HintVisible ? "💡 Follow the ghost arc" : _s.Ctx.titan != null ? _s.Ctx.titan.ruleText : "";
        }

        public override void Tick()
        {
            if (_s == null || _s.tracker == null) return;
            float r = _s.tracker.DestroyedRatio;
            _pct.text = Mathf.FloorToInt(r * 100f) + "%" + (_s.Ctx.IsTitan ? $"  {_s.TitanDamageScore}" : "");
            float dt = Time.unscaledDeltaTime; bool rm = Crashverse.Feel.MotionSettings.Current.ReduceMotion;

            // % bar: 400 ms ease-out toward the latest ratio (re-targets on change, never allocates)
            if (r != _fillTarget) { if (_fillTarget < 0f) _fillShown = r; _fillTarget = r; _fill.Start(_fillShown, r, Crashverse.Feel.UiMotionMath.BarFillMs, Crashverse.Feel.EaseType.OutQuad); }
            _fillShown = _fill.Tick(dt); _bar.fillAmount = _fillShown;

            // 85% win tick (level winThreshold): pops 1→1.4→1 in 250 ms when the fill reaches it, then stays gold. Reduce Motion: colour only.
            float thr = _s.Ctx.level.winThreshold;
            if (!_tickHit && Crashverse.Feel.UiMotionMath.ReachedThreshold(_fillShown, thr))
            { _tickHit = true; _tickMs = 0f; _tickImg.color = Th.goldTrim; Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.UiThresholdHit); }
            if (_tickMs >= 0f) { float k = Crashverse.Feel.UiMotionMath.TickScale(_tickMs, rm); _tick.localScale = new Vector3(k, k, 1f); _tickMs += dt * 1000f; if (_tickMs >= Crashverse.Feel.UiMotionMath.TickPopMs) { _tickMs = -1f; _tick.localScale = Vector3.one; } }

            // COMBO xN pop (ComboUiStateMachine raises UI_COMBO_UP / UI_COMBO_TIER)
            _comboSm.Tick(dt);
            float cs = Crashverse.Feel.UiMotionMath.ComboScale(_comboSm.State, _comboSm.ElapsedMs, _comboSm.N, rm);
            _combo.rectTransform.localScale = new Vector3(cs, cs, 1f);

            // shots-left punch 1.25 (200 ms)
            if (_shotMs >= 0f) { float k = Crashverse.Feel.UiMotionMath.ShotPunchScale(_shotMs, rm); _shots.rectTransform.localScale = new Vector3(k, k, 1f); _shotMs += dt * 1000f; if (_shotMs >= Crashverse.Feel.UiMotionMath.ShotPunchMs) { _shotMs = -1f; _shots.rectTransform.localScale = Vector3.one; } }
        }

        void OnEnded(RunResult r) => Router.Push(new ResultsScreen(_s, r));
        public override bool OnBack() { if (_s.State == SessionState.Won || _s.State == SessionState.Failed) return true; Router.Push(new PauseModal(_s)); return true; } // QA-UI-004
        public override void OnHide() { }
    }

    public class PauseModal : UIScreen
    {
        readonly GameSession _s; float _prevScale;
        public override bool IsModal => true;
        public PauseModal(GameSession s) { _s = s; }
        protected override void Build(RectTransform root)
        {
            _prevScale = Time.timeScale; Time.timeScale = 0f;
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.6f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("Pause", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(280, 300), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap * 2, 16f); UIFactory.Stretch(v);
            UIFactory.Label(v, "Paused", Th.titleSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            UIFactory.Button(v, "Resume", Th.green, () => Router.Pop(), Th);
            UIFactory.Button(v, "Settings", Th.navy500, () => Router.Push(new SettingsModal()), Th);
            if (_s.Ctx.mode != RunMode.TitanRanked) UIFactory.Button(v, "Restart", Th.navy500, () => { Router.Pop(); _s.Retry(); }, Th);
            UIFactory.Button(v, _s.Ctx.mode == RunMode.TitanRanked ? "Finish Run" : "Give Up", Th.red, () =>
                Router.Push(new ConfirmModal("Give up?", _s.Ctx.mode == RunMode.TitanRanked ? "Your score so far will be submitted." : "This counts as a fail.", "Give Up", () => { Router.Pop(); _s.GiveUp(); })), Th);
        }
        public override void OnHide() { Time.timeScale = _prevScale <= 0f ? 1f : _prevScale; }
    }

    // ───────────────────────────── Results (mockups 05/06) ─────────────────────────────
    public class ResultsScreen : UIScreen
    {
        readonly GameSession _s; readonly RunResult _r;
        // 06 win sequence: stars pop one by one (0→1.3→1, 250 ms, 300 ms stagger), mastery tick, score count-up (600 ms). Unscaled; tap = skip.
        readonly Text[] _star = new Text[3]; Text _mastery, _score; string _scoreFmt; float _seqMs; bool _starRaised0, _starRaised1, _starRaised2, _masteryRaised;
        public override bool IsModal => true;
        public ResultsScreen(GameSession s, RunResult r) { _s = s; _r = r; }
        protected override void Build(RectTransform root)
        {
            var lvl = _s.Ctx.level; var mode = _s.Ctx.mode;
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.55f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("Results", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(320, 470), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap, 16f); UIFactory.Stretch(v);
            UIFactory.Label(v, _r.won ? (mode == RunMode.TitanRanked || mode == RunMode.TitanPractice ? "TITAN DOWN!" : "SMASHED!") : "So close!", Th.titleSp, _r.won ? Th.goldFill : Th.red, TextAnchor.MiddleCenter, Th, true);
            if (_r.won && !_s.Ctx.IsTitan)
            {
                var sr = UIFactory.HStack(v, Th.gap, 48f);
                for (int i = 0; i < 3; i++)
                {
                    bool earned = i < _r.stars;
                    _star[i] = UIFactory.Label(sr, earned ? "★" : "☆", 40, earned ? Th.goldFill : Th.navy700, TextAnchor.MiddleCenter, Th, true);
                    _star[i].rectTransform.localScale = Vector3.zero;
                }
            }
            UIFactory.Label(v, $"Destroyed {Mathf.FloorToInt(_r.ratio * 100)}% (need {Mathf.RoundToInt(lvl.winThreshold * 100)}%)   Shots left {_r.shotsLeft}", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
            if (lvl.mastery != null) _mastery = UIFactory.Label(v, (_r.mastery ? "◆ Mastery complete: " : "◇ Mastery: ") + lvl.mastery.text, Th.captionSp, _r.mastery ? Th.cyan : Th.body, TextAnchor.MiddleCenter, Th);
            if (_r.bestCombo >= ComboTracker.ShowFrom) UIFactory.Label(v, "Best combo x" + _r.bestCombo, Th.bodySp, Th.orange, TextAnchor.MiddleCenter, Th);
            if (mode == RunMode.Campaign && _r.won) { _scoreFmt = "  (best " + AppServices.Save.BestScore(lvl.id) + ")"; _score = UIFactory.Label(v, "Score 0" + _scoreFmt, Th.bodySp, Th.goldFill, TextAnchor.MiddleCenter, Th); }
            if (_s.Ctx.IsTitan) UIFactory.Label(v, $"Titan score {_r.titanScore}" + (_r.titanSubmit != null ? $"\n{_r.titanSubmit.status}: {_r.titanSubmit.message}" : ""), Th.bodySp, Th.orange, TextAnchor.MiddleCenter, Th);
            if (_r.firstClear && lvl.reward != null) foreach (var rw in lvl.reward) UIFactory.Label(v, $"+{rw.amount} {rw.type} {rw.id}", Th.captionSp, Th.green, TextAnchor.MiddleCenter, Th);
            if (!string.IsNullOrEmpty(_r.note)) UIFactory.Label(v, _r.note, Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            UIFactory.Spacer(v, 8);
            // Addendum: free hint from 2nd consecutive fail, never ranked, no ads
            if (!_r.won && HintRules.ShowHintButton(lvl, AppServices.Save.FailsInRow(lvl.id), mode == RunMode.TitanRanked)) // BUG-08: campaign, events, Titan practice; never ranked
                UIFactory.Button(v, "💡 Free Hint", Th.goldShade, () => { _s.Ctx.hintArmed = true; Router.Pop(); _s.Retry(); }, Th, 48);
            var row = UIFactory.HStack(v, Th.gap, Th.buttonL);
            UIFactory.Button(row, "Map", Th.navy500, () => GameplayDirector.ExitToMap(), Th);
            if (mode != RunMode.TitanRanked) UIFactory.Button(row, "Retry", Th.navy500, () => { Router.Pop(); _s.Retry(); }, Th);
            var next = NextLevel(lvl);
            if (_r.won && mode == RunMode.Campaign && next != null && UnlockRules.IsLevelUnlocked(next, AppServices.Save))
                UIFactory.Button(row, "Next", Th.green, () => { GameplayDirector.ExitToMap(); AppServices.Router.Push(new BriefingScreen(next, RunMode.Campaign, null)); }, Th);
        }
        public override void Tick()
        {
            if (_seqMs > 4000f) return; // sequence finished
            bool rm = Crashverse.Feel.MotionSettings.Current.ReduceMotion;
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0) _seqMs = Mathf.Max(_seqMs, 3000f); // tap skips to the final state (06)
            _seqMs += Time.unscaledDeltaTime * 1000f;
            for (int i = 0; i < 3; i++)
            {
                if (_star[i] == null) continue;
                float ms = Crashverse.Feel.UiMotionMath.MsIntoStar(_seqMs, i);
                bool earned = i < _r.stars;
                float k = Crashverse.Feel.UiMotionMath.StarScale(ms, earned, rm);
                _star[i].rectTransform.localScale = new Vector3(k, k, 1f);
                if (earned && ms >= Crashverse.Feel.UiMotionMath.StarPeakMs) RaiseStar(i);
            }
            if (_mastery != null)
            {
                float ms = _seqMs - Crashverse.Feel.WinSequence.Steps[4].startMs; // "Mastery" step (1200 ms)
                // v1 mastery row: appears at 1200 ms; if achieved, gold ping 1 → 1.2 → 1 (150 ms; Reduce Motion 1.1)
                float k = ms <= 0f ? 0f : _r.mastery && ms < 150f ? 1f + (rm ? 0.1f : 0.2f) * Mathf.Sin(Mathf.PI * ms / 150f) : 1f;
                _mastery.rectTransform.localScale = new Vector3(k, k, 1f);
                if (_r.mastery && ms >= 150f && !_masteryRaised) { _masteryRaised = true; Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.UiMasteryTick); }
            }
            if (_score != null)
            {
                float ms = _seqMs - Crashverse.Feel.WinSequence.Steps[5].startMs; // "Score" step (1500 ms)
                _score.text = "Score " + Crashverse.Feel.UiMotionMath.CountUp(0, _r.score, ms, Crashverse.Feel.UiMotionMath.ResultsScoreMs) + _scoreFmt;
            }
        }

        void RaiseStar(int i)
        {
            if (i == 0) { if (_starRaised0) return; _starRaised0 = true; } else if (i == 1) { if (_starRaised1) return; _starRaised1 = true; } else { if (_starRaised2) return; _starRaised2 = true; }
            Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.UiStar + (i + 1)); // UI_STAR_1..3 at the pop peak
        }

        static LevelEntry NextLevel(LevelEntry l)
        {
            var ls = AppServices.Content.Chapter1.levels;
            for (int i = 0; i < ls.Length - 1; i++) if (ls[i].id == l.id) return ls[i + 1];
            return null;
        }
        public override bool OnBack() { GameplayDirector.ExitToMap(); return true; }
    }

    // ───────────────────────────── Daily Titan (mockup 07) ─────────────────────────────
    public class TitanScreen : UIScreen
    {
        RectTransform _v; DailyTitanConfig _cfg; RankedStatus _st; Text _timer;
        protected override void Build(RectTransform root)
        {
            var bg = UIFactory.TrimPanel("Titan", root, Th); UIFactory.Stretch(bg.rectTransform, 12f);
            _v = UIFactory.VStack(bg.transform, Th.gap, 16f); UIFactory.Stretch(_v);
            UIFactory.Label(_v, "Emberforge Daily Titan", Th.titleSp, Th.orange, TextAnchor.MiddleCenter, Th, true);
            _timer = UIFactory.Label(_v, "Loading…", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
        }
        public override async void OnShow()
        {
            try { _cfg = await AppServices.Backend.GetDailyTitan(); _st = await AppServices.Backend.GetRankedStatus(); }
            catch (BackendException e) { _timer.text = "Offline: " + e.Message; UIFactory.Button(_v, "Back", Th.navy500, () => Router.Pop(), Th); return; }
            UIFactory.Label(_v, $"{_cfg.name} · {_cfg.arenaName}", Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            UIFactory.Label(_v, $"Rule: {_cfg.ruleText}\nShots: {_cfg.shots}   Mastery: {_cfg.mastery?.text}", Th.bodySp, Th.body, TextAnchor.MiddleCenter, Th);
            UIFactory.Label(_v, _st.attemptUsed ? $"Ranked attempt used today · {_st.status} · score {_st.score}" : "1 ranked attempt today", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
            var lvl = RunContext.TitanLevel(_cfg);
            var ranked = UIFactory.Button(_v, "Ranked Attempt", Th.orange, () => { Router.Pop(); Router.Push(new BriefingScreen(lvl, RunMode.TitanRanked, _cfg)); }, Th);
            ranked.interactable = !_st.attemptUsed;
            UIFactory.Button(_v, "Practice (unranked)", Th.navy500, () => { Router.Pop(); Router.Push(new BriefingScreen(lvl, RunMode.TitanPractice, _cfg)); }, Th);
            UIFactory.Button(_v, "Leaderboard", Th.cyan, () => Router.Push(new LeaderboardScreen(BoardType.DailyTitan)), Th, 48);
            UIFactory.Button(_v, "Back", Th.navy500, () => Router.Pop(), Th, 48);
        }
        public override void Tick()
        {
            if (_cfg == null) return;
            var left = DateTimeOffset.FromUnixTimeSeconds(_cfg.resetsAtUnixUtc).UtcDateTime - AppServices.ServerNow;
            _timer.text = $"New Titan in {UIFactory.FormatCountdown(left)} (resets {AppServices.LocalLabel(DateTimeOffset.FromUnixTimeSeconds(_cfg.resetsAtUnixUtc).UtcDateTime)})";
        }
    }

    // ───────────────────────────── Daily rewards (mockup 08) ─────────────────────────────
    public class DailyRewardsScreen : UIScreen
    {
        RectTransform _v;
        protected override void Build(RectTransform root)
        {
            var bg = UIFactory.TrimPanel("Daily", root, Th); UIFactory.Stretch(bg.rectTransform, 12f);
            _v = UIFactory.VStack(bg.transform, Th.gap, 16f); UIFactory.Stretch(_v);
        }
        public override async void OnShow()
        {
            foreach (Transform c in _v) UnityEngine.Object.Destroy(c.gameObject);
            UIFactory.Label(_v, "Daily Rewards", Th.titleSp, Th.green, TextAnchor.MiddleCenter, Th, true);
            DailyRewardState st;
            try { st = await AppServices.Backend.GetDailyRewardState(); }
            catch (BackendException e) { UIFactory.Label(_v, "Offline: " + e.Message, Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th); UIFactory.Button(_v, "Back", Th.navy500, () => Router.Pop(), Th); return; }
            for (int i = 0; i < st.days.Length; i++)
            {
                var d = st.days[i];
                string mark = i < st.streakIndex ? "✔" : i == st.streakIndex && st.canClaimToday ? "▶" : "·";
                UIFactory.Label(_v, $"{mark} Day {i + 1}: {d.amount} {d.type} {d.id}", Th.bodySp, i == st.streakIndex ? Th.goldFill : Th.body, TextAnchor.MiddleLeft, Th);
            }
            var claim = UIFactory.Button(_v, st.canClaimToday ? "Claim" : "Come back tomorrow", Th.green, async () =>
            {
                try { await AppServices.Backend.ClaimDailyReward(); Router.Toast("Claimed!"); OnShow(); }
                catch (BackendException e) { Router.Toast(e.Message); }
            }, Th);
            claim.interactable = st.canClaimToday;
            UIFactory.Label(_v, "Next reset " + AppServices.LocalLabel(DateTimeOffset.FromUnixTimeSeconds(st.nextResetUnixUtc).UtcDateTime), Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            UIFactory.Button(_v, "Back", Th.navy500, () => Router.Pop(), Th, 48);
        }
    }

    // ───────────────────────────── Season + weekly event (mockups 09/10) ─────────────────────────────
    public class SeasonScreen : UIScreen
    {
        RectTransform _v;
        protected override void Build(RectTransform root)
        {
            var bg = UIFactory.TrimPanel("Season", root, Th); UIFactory.Stretch(bg.rectTransform, 12f);
            _v = UIFactory.VStack(bg.transform, Th.gap, 12f); UIFactory.Stretch(_v);
        }
        public override async void OnShow()
        {
            foreach (Transform c in _v) UnityEngine.Object.Destroy(c.gameObject);
            var data = AppServices.Content.Season;
            SeasonState st;
            try { st = await AppServices.Backend.GetSeasonState(); }
            catch (BackendException e) { UIFactory.Label(_v, "Offline: " + e.Message, Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th); UIFactory.Button(_v, "Back", Th.navy500, () => Router.Pop(), Th); return; }
            UIFactory.Label(_v, st.name, Th.titleSp, Th.cyan, TextAnchor.MiddleCenter, Th, true);
            var ends = DateTimeOffset.FromUnixTimeSeconds(st.weekEndsUnixUtc).UtcDateTime;
            UIFactory.Label(_v, $"Tier {st.tier}/30 · {st.xp} XP · bonus chests {st.bonusChests}\nWeek {st.weekIndex + 1} ends in {UIFactory.FormatCountdown(ends - AppServices.ServerNow)}", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
            WeeklyEvent wk = data.weeks != null && st.weekIndex >= 0 && st.weekIndex < data.weeks.Length ? data.weeks[st.weekIndex] : null;
            if (wk != null)
            {
                UIFactory.Label(_v, $"{wk.name}: {wk.ruleText}", Th.bodySp, Th.orange, TextAnchor.MiddleCenter, Th);
                var row = UIFactory.HStack(_v, Th.gap, 52f);
                foreach (var l in wk.levels) { var lv = l; UIFactory.Button(row, lv.name, Th.orange, () => { Router.Pop(); Router.Push(new BriefingScreen(lv, RunMode.Event, null, wk.mod)); }, Th, 52, 0, Th.captionSp); }
            }
            var list = UIFactory.Scroll(_v, 4f, out var sr); sr.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            foreach (var t in data.tiers)
            {
                var tier = t; bool reached = st.tier >= tier.tier;
                var r = UIFactory.HStack(list, Th.gap, Th.seasonTier);
                UIFactory.Label(r, $"T{tier.tier} · {tier.xp}xp", Th.captionSp, reached ? Th.goldFill : Th.body, TextAnchor.MiddleLeft, Th);
                AddClaim(r, tier, false, st, reached); AddClaim(r, tier, true, st, reached);
            }
            UIFactory.Button(_v, "Back", Th.navy500, () => Router.Pop(), Th, 48);
        }
        void AddClaim(RectTransform r, SeasonTier tier, bool premium, SeasonState st, bool reached)
        {
            var rw = premium ? tier.premium : tier.free; if (rw == null) return;
            bool claimed = (premium ? st.claimedPremium : st.claimedFree).Contains(tier.tier);
            var b = UIFactory.Button(r, (claimed ? "✔ " : premium ? "👑 " : "") + $"{rw.amount} {rw.type}", premium ? Th.violet : Th.navy500, async () =>
            {
                try { await AppServices.Backend.ClaimSeasonTier(tier.tier, premium); OnShow(); } catch (BackendException e) { Router.Toast(e.Message); }
            }, Th, Th.seasonTier, 0, Th.captionSp);
            b.interactable = reached && !claimed && (!premium || st.premium);
        }
    }

    // ───────────────────────────── Leaderboard (mockup 11) ─────────────────────────────
    public class LeaderboardScreen : UIScreen
    {
        BoardType _board; BoardPeriod _period = BoardPeriod.Current; bool _friends; RectTransform _list; Text _me;
        public LeaderboardScreen(BoardType b = BoardType.DailyTitan) { _board = b; }
        protected override void Build(RectTransform root)
        {
            var bg = UIFactory.TrimPanel("Board", root, Th); UIFactory.Stretch(bg.rectTransform, 12f);
            var v = UIFactory.VStack(bg.transform, Th.gap, 12f); UIFactory.Stretch(v);
            UIFactory.Label(v, "Leaderboards", Th.titleSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            var tabs = UIFactory.HStack(v, 4f, Th.tabHeight);
            UIFactory.Button(tabs, "Titan", Th.orange, () => { _board = BoardType.DailyTitan; Load(); }, Th, Th.tabHeight, 0, Th.captionSp);
            UIFactory.Button(tabs, "Event", Th.cyan, () => { _board = BoardType.WeeklyEvent; Load(); }, Th, Th.tabHeight, 0, Th.captionSp);
            UIFactory.Button(tabs, "Season", Th.violet, () => { _board = BoardType.Season; Load(); }, Th, Th.tabHeight, 0, Th.captionSp);
            UIFactory.Button(tabs, "Campaign", Th.green, () => { _board = BoardType.Campaign; Load(); }, Th, Th.tabHeight, 0, Th.captionSp); // SC-13
            var opts = UIFactory.HStack(v, 4f, Th.tabHeight);
            UIFactory.Button(opts, "Now/Prev", Th.navy500, () => { _period = _period == BoardPeriod.Current ? BoardPeriod.Previous : BoardPeriod.Current; Load(); }, Th, Th.tabHeight, 0, Th.captionSp);
            UIFactory.Button(opts, "Global/Friends", Th.navy500, () => { _friends = !_friends; Load(); }, Th, Th.tabHeight, 0, Th.captionSp);
            _list = UIFactory.Scroll(v, 2f, out var sr); sr.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            _me = UIFactory.Label(v, "", Th.bodySp, Th.goldFill, TextAnchor.MiddleCenter, Th);
            UIFactory.Button(v, "Back", Th.navy500, () => Router.Pop(), Th, 48);
        }
        public override void OnShow() => Load();
        async void Load()
        {
            foreach (Transform c in _list) UnityEngine.Object.Destroy(c.gameObject);
            try
            {
                var page = await AppServices.Backend.GetLeaderboard(_board, _period, _friends);
                foreach (var e in page.top)
                    UIFactory.Label(_list, $"#{e.rank}  {UIFactory.Truncate(e.name, 16)}  {e.score}", Th.bodySp, e.isMe ? Th.goldFill : Th.white, TextAnchor.MiddleLeft, Th).GetComponent<LayoutElement>().minHeight = Th.leaderboardRow;
                _me.text = page.meIsGuestHidden ? "Link an account to appear on boards" : page.me != null ? $"You: #{page.me.rank} · {page.me.score}" : "No score yet";
                if (page.top.Count == 0) UIFactory.Label(_list, _friends ? "No friends on this board yet" : "No entries yet", Th.bodySp, Th.body, TextAnchor.MiddleCenter, Th);
            }
            catch (BackendException e) { _me.text = "Offline: " + e.Message; }
        }
    }

    // ───────────────────────────── Store (mockup 12, Addendum) ─────────────────────────────
    public class StoreScreen : UIScreen
    {
        protected override void Build(RectTransform root)
        {
            var bg = UIFactory.TrimPanel("Store", root, Th); UIFactory.Stretch(bg.rectTransform, 12f);
            var v = UIFactory.VStack(bg.transform, Th.gap, 12f); UIFactory.Stretch(v);
            UIFactory.Label(v, "Store", Th.titleSp, Th.violet, TextAnchor.MiddleCenter, Th, true);
            UIFactory.Label(v, "Cosmetics & currency only. Purchases are stubbed in this build.", Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            var list = UIFactory.Scroll(v, Th.gap, out var sr); sr.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            foreach (var it in AppServices.StoreItems) // validated (StoreValidator) — rejected SKUs never show
            {
                var item = it;
                var row = UIFactory.HStack(list, Th.gap, 56f);
                UIFactory.Label(row, $"{UIFactory.Truncate(item.name, 22)}\n{item.type}", Th.captionSp, Th.white, TextAnchor.MiddleLeft, Th);
                if (AgeGate.CanPurchase(AgeGateScreen.Current, AppServices.Account?.consentPurchases ?? false)) // E5/E6: buy hidden for minors without consent
                    UIFactory.Button(row, item.priceType == "iap" ? $"${item.price / 100f:0.00}" : $"{item.price} {item.priceType}", Th.green,
                        () => Router.Toast("Purchase stub: " + item.sku + " (IAP not wired)"), Th, 48, 110, Th.captionSp);
            }
            UIFactory.Button(v, "Back", Th.navy500, () => Router.Pop(), Th, 48);
        }
    }

    // ───────────────────────────── Settings (Feel OQ-C2, Deletion/AgeGate reqs A1, F4, F5) ─────────────────────────────
    // SettingsModal ships in every build; QA tools do not (SEC-02). Map → Settings → Delete account = 2 taps (A1).
    public class SettingsModal : UIScreen
    {
        public override bool IsModal => true; Text _shake, _motion, _hide;
        protected override void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.6f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("Settings", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330, 560), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap, 12f); UIFactory.Stretch(v);
            UIFactory.Label(v, "Settings", Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            var m = Crashverse.Feel.MotionSettings.Current;
            _shake = UIFactory.Button(v, "", Th.navy500, () => { m.SetReduceShake(!m.ReduceShake); Refresh(); }, Th, 48).GetComponentInChildren<Text>();
            _motion = UIFactory.Button(v, "", Th.navy500, () => { m.SetReduceMotion(!m.ReduceMotion); Refresh(); }, Th, 48).GetComponentInChildren<Text>();
            UIFactory.Label(v, "Privacy", Th.bodySp, Th.goldFill, TextAnchor.MiddleLeft, Th, true);
            _hide = UIFactory.Button(v, "", Th.navy500, ToggleHide, Th, 48).GetComponentInChildren<Text>();
            UIFactory.Label(v, "Account", Th.bodySp, Th.goldFill, TextAnchor.MiddleLeft, Th, true);
            UIFactory.Button(v, "Linked accounts", Th.navy500, () => Router.Push(new LinkedAccountsModal()), Th, 48);
            UIFactory.Button(v, AccountDeletion.Pending ? "Delete account (pending until online)" : "Delete account", Th.red, () => Router.Push(new DeleteAccountScreen()), Th, 48);
            UIFactory.Button(v, "Close", Th.navy500, () => Router.Pop(), Th, 48);
            Refresh();
        }
        async void ToggleHide()
        {
            try { var v = await AppServices.Backend.SetProfileVisibility(!(AppServices.Account?.hideName ?? false)); if (AppServices.Account != null) AppServices.Account.hideName = v.hideName; Refresh(); }
            catch (BackendException) { Router.Toast("You need to be online to change this."); }
        }
        void Refresh()
        {
            var m = Crashverse.Feel.MotionSettings.Current;
            _shake.text = "Reduce shake: " + (m.ReduceShake ? "ON" : "OFF");
            _motion.text = "Reduce motion: " + (m.ReduceMotion ? "ON" : "OFF") + (m.ReduceMotionIsAuto ? " (system)" : "");
            _hide.text = "Hide my name on leaderboards: " + ((AppServices.Account?.hideName ?? false) ? "ON" : "OFF");
        }
    }

    /// <summary>F4/F1: see and manage login links (separate from deletion). Linking is hidden for minors without consent (E5, C3).</summary>
    public class LinkedAccountsModal : UIScreen
    {
        public override bool IsModal => true; RectTransform _v;
        protected override void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.6f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("Links", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(320, 440), Vector2.zero);
            _v = UIFactory.VStack(p.transform, Th.gap, 12f); UIFactory.Stretch(_v);
            Load();
        }
        async void Load()
        {
            foreach (Transform c in _v) UnityEngine.Object.Destroy(c.gameObject);
            UIFactory.Label(_v, "Linked accounts", Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            try
            {
                var acc = AppServices.Account = await AppServices.Backend.GetAccount();
                var flags = await AppServices.Backend.GetFeatureFlags();
                if (acc.identities.Count == 0) UIFactory.Label(_v, "Guest account (no login linked)", Th.captionSp, Th.white, TextAnchor.MiddleCenter, Th);
                foreach (var id in acc.identities)
                {
                    var prov = (AuthProvider)Enum.Parse(typeof(AuthProvider), id);
                    UIFactory.Button(_v, "Unlink " + id, Th.navy500, () => Unlink(prov), Th, 48);
                }
                var band = AgeGate.FromServer(acc.ageBand);
                if (AgeGate.CanSocialSignIn(band == AgeGate.Band.Unknown ? AgeGateScreen.Current : band, acc.consentSocial))
                {
                    foreach (AuthProvider prov in Enum.GetValues(typeof(AuthProvider)))
                    {
                        if (acc.identities.Contains(prov.ToString()) || (prov == AuthProvider.X && !flags.xLoginEnabled)) continue; // X gated by server flag
                        var pv = prov;
                        if (pv == AuthProvider.Facebook) continue; // TODO: Facebook Login only after the F3 deletion callback ships (docs/Privacy_And_Account.md)
                        UIFactory.Button(_v, "Link " + pv, Th.green, () => Router.Toast("Sign-in with " + pv + " isn't wired yet"), Th, 48); // TODO(Dev): provider SDKs → LinkAccount(LinkCredential)
                    }
                }
                else UIFactory.Label(_v, "Linking a login needs a parent's permission.", Th.captionSp, Th.white, TextAnchor.MiddleCenter, Th);
            }
            catch (BackendException) { UIFactory.Label(_v, "You need to be online to manage linked accounts.", Th.captionSp, Th.white, TextAnchor.MiddleCenter, Th); }
            UIFactory.Button(_v, "Close", Th.navy500, () => Router.Pop(), Th, 48);
        }
        async void Unlink(AuthProvider p)
        {
            try
            {
                var tok = await AccountLifecycle.FreshReauthToken(p);
                if (string.IsNullOrEmpty(tok)) { Router.Toast("Please sign in again first."); return; }
                AppServices.Account = await AppServices.Backend.UnlinkAccount(p, tok); Load();
            }
            catch (BackendException e) { Router.Toast(e.Code == "LAST_LOGIN" ? "You must keep at least one login method." : "Couldn't unlink: " + e.Code); }
        }
    }

    /// <summary>A2/A3/A6: explains the consequences, needs a 2 s press-and-hold, re-auths linked accounts, queues when offline.</summary>
    public class DeleteAccountScreen : UIScreen
    {
        public const float HoldSeconds = 2f;
        public const string Copy =
            "Deleting your account removes your account, sign-in links, progress, wallet, leaderboard entries and profile.\n\n" +
            "We keep only pseudonymised purchase records (see the privacy notice, section 5).\n\n" +
            "Deletion is completed within 30 days. You can cancel by signing in within 7 days; after that it can't be undone.\n\n" +
            "Purchases are not refunded and are lost.";
        public override bool IsModal => true; Text _hold; HoldToConfirm _h;
        protected override void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.75f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("Delete", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330, 520), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap, 14f); UIFactory.Stretch(v);
            UIFactory.Label(v, "Delete account", Th.headingSp, Th.red, TextAnchor.MiddleCenter, Th, true);
            UIFactory.Label(v, Copy, Th.captionSp, Th.white, TextAnchor.UpperLeft, Th);
            if (AccountDeletion.Pending) UIFactory.Label(v, "Your request is pending until you're online.", Th.captionSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            var btn = UIFactory.Button(v, "", Th.red, () => { }, Th, 56);
            _hold = btn.GetComponentInChildren<Text>();
            _h = btn.gameObject.AddComponent<HoldToConfirm>(); _h.seconds = HoldSeconds; _h.Completed = Confirm; // single tap never confirms
            UIFactory.Button(v, "Keep my account", Th.navy500, () => Router.Pop(), Th, 48);
        }
        public override void Tick() { if (_hold != null) _hold.text = _h != null && _h.Progress > 0f ? $"Keep holding… {Mathf.CeilToInt((1f - _h.Progress) * HoldSeconds)}" : "Press and hold to delete"; }
        async void Confirm()
        {
            var b = AppServices.Backend; string reauth = null;
            try
            {
                var acc = AppServices.Account ?? await b.GetAccount();
                if (acc.linked) // A3: fresh re-auth (< 5 min) with a linked provider → reauthToken
                {
                    var prov = (AuthProvider)Enum.Parse(typeof(AuthProvider), acc.identities.Count > 0 ? acc.identities[0] : acc.provider);
                    reauth = await AccountLifecycle.FreshReauthToken(prov);
                    if (string.IsNullOrEmpty(reauth)) { Router.Toast("Please sign in again to confirm deletion."); return; }
                }
            }
            catch (BackendException e) when (!e.IsOffline) { Router.Toast("Couldn't verify your sign-in: " + e.Code); return; }
            catch (BackendException) { } // offline: queue below (re-auth is requested again when it's sent)
            var res = await AccountDeletion.Request(b, reauth);
            switch (res)
            {
                case AccountDeletion.Result.Scheduled: GameBootstrap.RestartFirstRun(); AppServices.Router.Toast("Your account is scheduled for deletion.", 4f); break;
                case AccountDeletion.Result.PendingOffline: Router.Pop(); Router.Toast("Deletion is pending until you're online.", 4f); break;
                case AccountDeletion.Result.ReauthRequired: Router.Toast("Please sign in again to confirm deletion.", 4f); break;
                default: AccountDeletion.Cancel(); Router.Toast("Couldn't delete your account. Please try again."); break;
            }
        }
    }

    /// <summary>Press-and-hold confirmation (A2: cannot confirm with a single tap). Unscaled time.</summary>
    public class HoldToConfirm : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float seconds = 2f; public Action Completed; bool _down, _fired; float _t;
        public float Progress => Mathf.Clamp01(_t / seconds);
        public void OnPointerDown(PointerEventData e) { _down = true; _t = 0f; _fired = false; }
        public void OnPointerUp(PointerEventData e) { _down = false; _t = 0f; }
        void Update()
        {
            if (!_down || _fired) return;
            _t += Time.unscaledDeltaTime;
            if (_t >= seconds) { _fired = true; _down = false; _t = 0f; Completed?.Invoke(); }
        }
    }

    // ───────────────────────────── Neutral age gate (§E1/E2) ─────────────────────────────
    /// <summary>Year + month pickers with no default; no hint of an eligible age. Stores only the band (13+) or a local blocked flag.</summary>
    public class AgeGateScreen : UIScreen
    {
        public const string BandKey = "cv_age_band_v", BlockedKey = "cv_age_blocked";
        public static bool Blocked => PlayerPrefs.GetInt(BlockedKey, 0) == 1;
        public const string SubmittedKey = "cv_age_submitted_v";
        /// <summary>Asked again if the band was never confirmed by the server (the DOB is not kept across launches).</summary>
        public static bool Needed => !Blocked && (PlayerPrefs.GetInt(BandKey + AgeGate.Version, 0) == 0 || (PlayerPrefs.GetInt(SubmittedKey + AgeGate.Version, 0) == 0 && !PendingDob.HasValue));
        /// <summary>Year/month held in memory only, for one POST /agegate (13+ only). Never written to disk.</summary>
        public static (int year, int month)? PendingDob;
        public static void OnServerBand(AgeGate.Band serverBand, bool requiresConsent)
        {
            PendingDob = null;
            var local = Current; var band = serverBand == AgeGate.Band.Unknown ? local : (local == AgeGate.Band.Unknown ? serverBand : (AgeGate.Band)Math.Min((int)local, (int)serverBand)); // most restrictive
            PlayerPrefs.SetInt(BandKey + AgeGate.Version, (int)band); PlayerPrefs.SetInt(SubmittedKey + AgeGate.Version, 1); PlayerPrefs.Save();
        }
        public static AgeGate.Band Current => (AgeGate.Band)PlayerPrefs.GetInt(BandKey + AgeGate.Version, 0);
        /// <summary>TODO(Legal): which age signal is acceptable (store country vs locale). Locale region for now.</summary>
        public static string Country() { try { return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName; } catch { return ""; } }

        static readonly string[] Months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        readonly Action<bool> _done; int _year, _month; Button _yearBtn, _monthBtn, _ok;
        public AgeGateScreen(Action<bool> done) { _done = done; }
        public override bool OnBack() => true; // must answer
        protected override void Build(RectTransform root)
        {
            var bg = UIFactory.Panel("Bg", root, Th.navy900); UIFactory.Stretch(bg.rectTransform);
            var v = UIFactory.VStack(root, Th.gap * 2, 24f); UIFactory.Place(v, new Vector2(0.5f, 0.5f), new Vector2(320, 380), Vector2.zero);
            UIFactory.Label(v, "Welcome to CRASHVERSE", Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            UIFactory.Label(v, "Please enter your date of birth.", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
            _monthBtn = UIFactory.Button(v, "Month", Th.navy500, PickMonth, Th, 56);
            _yearBtn = UIFactory.Button(v, "Year", Th.navy500, PickYear, Th, 56);
            _ok = UIFactory.Button(v, "Continue", Th.green, Confirm, Th, 56); _ok.interactable = false;
            UIFactory.Label(v, "We only keep an age range, never your date of birth.", Th.captionSp, Th.white, TextAnchor.MiddleCenter, Th);
        }
        void PickMonth()
        {
            var items = new List<(string, Action)>();
            for (int i = 0; i < 12; i++) { int m = i + 1; items.Add((Months[i], () => { _month = m; _monthBtn.GetComponentInChildren<Text>().text = Months[m - 1]; Validate(); })); }
            Router.Push(new ListModal("Month of birth", items));
        }
        void PickYear()
        {
            var items = new List<(string, Action)>(); int now = DateTime.UtcNow.Year;
            for (int y = now; y >= now - 100; y--) { int yy = y; items.Add((yy.ToString(), () => { _year = yy; _yearBtn.GetComponentInChildren<Text>().text = yy.ToString(); Validate(); })); } // newest first: no eligible-age default
            Router.Push(new ListModal("Year of birth", items));
        }
        void Validate() => _ok.interactable = _year > 0 && _month > 0;
        void Confirm()
        {
            var now = DateTime.UtcNow; // device date is fine here: no server contact is allowed before the gate
            var band = AgeGate.Classify(_year, _month, now.Year, now.Month);
            if (band == AgeGate.Band.Unknown) return;
            if (band == AgeGate.Band.Under13) { PlayerPrefs.SetInt(BlockedKey, 1); PlayerPrefs.Save(); _done?.Invoke(false); return; } // E2: no DOB stored
            PlayerPrefs.SetInt(BandKey + AgeGate.Version, (int)band); PlayerPrefs.Save();
            PendingDob = (_year, _month); // memory only; sent once by AccountLifecycle, then dropped
            _done?.Invoke(true);
        }
    }

    /// <summary>E2: neutral block screen. No back, no retry.</summary>
    public class BlockedScreen : UIScreen
    {
        public override bool OnBack() => true;
        protected override void Build(RectTransform root)
        {
            var bg = UIFactory.Panel("Bg", root, Th.navy900); UIFactory.Stretch(bg.rectTransform);
            var l = UIFactory.Label(root, "CRASHVERSE isn't available.", Th.headingSp, Th.white, TextAnchor.MiddleCenter, Th, true);
            UIFactory.Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(320, 120), Vector2.zero);
        }
    }

    // ───────────────────────────── QA / debug panel (SEC-02: dev builds only) ─────────────────────────────
#if CRASHVERSE_DEV || DEVELOPMENT_BUILD
    /// <summary>Test clock override (server stub), offline toggle, quality toggle. Development builds and editor only in production.</summary>
    public class QADebugModal : UIScreen
    {
        public override bool IsModal => true; Text _clock;
        protected override void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.6f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("QA", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(320, 480), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap, 12f); UIFactory.Stretch(v);
            UIFactory.Label(v, "QA Tools", Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            _clock = UIFactory.Label(v, "", Th.captionSp, Th.white, TextAnchor.MiddleCenter, Th);
#if CRASHVERSE_DEV
            var lb = AppServices.LocalBackend;
            if (lb != null)
            {
                var r1 = UIFactory.HStack(v, Th.gap, 48);
                UIFactory.Button(r1, "+1h", Th.navy500, () => Adv(TimeSpan.FromHours(1)), Th, 48, 0, Th.captionSp);
                UIFactory.Button(r1, "+1d", Th.navy500, () => Adv(TimeSpan.FromDays(1)), Th, 48, 0, Th.captionSp);
                UIFactory.Button(r1, "+7d", Th.navy500, () => Adv(TimeSpan.FromDays(7)), Th, 48, 0, Th.captionSp);
                UIFactory.Button(v, "Jump to 23:59:50 UTC", Th.navy500, () => { var n = lb.ServerNow; lb.SetTestClock(n.Date.AddHours(23).AddMinutes(59).AddSeconds(50)); Sync(); }, Th, 48, 0, Th.captionSp);
                UIFactory.Button(v, "Clear test clock", Th.navy500, () => { lb.ClearTestClock(); Sync(); }, Th, 48, 0, Th.captionSp);
                UIFactory.Button(v, "Toggle offline", Th.red, () => { lb.SimulateOffline = !lb.SimulateOffline; Router.Toast(lb.SimulateOffline ? "Backend offline" : "Backend online"); }, Th, 48, 0, Th.captionSp);
            }
#endif
            UIFactory.Button(v, "Toggle quality", Th.navy500, () => Crashverse.Perf.QualityController.Toggle(), Th, 48, 0, Th.captionSp);
            UIFactory.Button(v, "Close", Th.navy500, () => Router.Pop(), Th, 48);
        }
#if CRASHVERSE_DEV
        void Adv(TimeSpan t) { AppServices.LocalBackend.AdvanceTestClock(t); Sync(); }
#endif
        async void Sync() { await AppServices.SyncServerTime(); }
#if CRASHVERSE_DEV
        public override void Tick() { _clock.text = $"Server {AppServices.ServerNow:yyyy-MM-dd HH:mm:ss} UTC" + (AppServices.LocalBackend?.TestClockActive == true ? " (TEST)" : ""); }
#else
        public override void Tick() { _clock.text = $"Server {AppServices.ServerNow:yyyy-MM-dd HH:mm:ss} UTC"; }
#endif
        public override void OnHide() { (Router.Top as MapScreen)?.OnShow(); }
    }
#endif
}
