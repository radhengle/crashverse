using System;
using System.Collections.Generic;
using Crashverse.Backend;
using Crashverse.Core;
using Crashverse.Data;
using Crashverse.Destruction;
using Crashverse.Meta;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crashverse.UI
{
    // ───────────────────────────── Map (mockup 02) ─────────────────────────────
    /// <summary>Island map: node buttons projected on 3D anchors, drag-to-pan (≥10dp threshold via EventSystem), gates, live-ops entry points.</summary>
    // MapScreen moved to ScreensMap.cs (target UI v2: 2D scrolling journey).

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
        readonly string _title; readonly List<(string label, Action act)> _items; readonly int _start; ScrollRect _sr;
        public override bool IsModal => true;
        /// <param name="startIndex">item scrolled to the top when the list opens (the whole list stays reachable)</param>
        public ListModal(string title, List<(string, Action)> items, int startIndex = 0) { _title = title; _items = items; _start = startIndex; }
        public override void OnShow()
        {
            if (_sr == null || _start <= 0) return;
            Canvas.ForceUpdateCanvases();
            var vp = _sr.viewport != null ? _sr.viewport : (RectTransform)_sr.transform;
            float overflow = _sr.content.rect.height - vp.rect.height; if (overflow <= 0) return;
            float per = _sr.content.rect.height / Mathf.Max(1, _items.Count);
            _sr.verticalNormalizedPosition = 1f - Mathf.Clamp01(_start * per / overflow);
        }
        protected override void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.6f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("List", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(300, 420), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap, 12f); UIFactory.Stretch(v);
            UIFactory.Label(v, _title, Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            var list = UIFactory.Scroll(v, Th.gap, out var sr); sr.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1; _sr = sr;
            foreach (var it in _items) { var a = it.act; UIFactory.Button(list, it.label, Th.navy500, () => { Router.Pop(); a(); }, Th, 48, 0, Th.bodySp); }
            UIFactory.Button(v, "Close", Th.navy500, () => Router.Pop(), Th, 48);
        }
    }

    // ───────────────────────────── HUD (mockup 04) ─────────────────────────────
    public class HudScreen : UIScreen
    {
        readonly GameSession _s; Text _shots, _pct, _combo, _status; Image _bar, _bossBar; Button _fire; Button[] _pu = new Button[3]; Image[] _puIcon = new Image[3];
        Image[] _hudStars; RectTransform _strip; Image[] _stripTiles; List<DestructibleBlock>[] _bands; float _stripT;
        // 06 UI motion (unscaled time, Reduce Motion variants): combo pop, % bar fill + win tick pop, shots punch.
        readonly Crashverse.Feel.ComboUiStateMachine _comboSm = new Crashverse.Feel.ComboUiStateMachine();
        RectTransform _tick; Image _tickImg; Crashverse.Feel.FloatTween _fill; float _fillShown, _fillTarget = -1f, _tickMs = -1f, _shotMs = -1f;
        bool _tickHit; int _lastShots = -1;
        public override bool BlocksWorldInput => false;
        public HudScreen(GameSession s) { _s = s; }
        const int StripBands = 5;

        // Target UI §6/§7/§8: Steps Left box, destruction % bar, building strip (right), pause (bottom-left), power-ups, FIRE; boss HP bar on the Daily Boss.
        protected override void Build(RectTransform root)
        {
            bool dark = _s.Ctx.IsHard || _s.Ctx.mode == RunMode.DailyBoss;
            if (dark) { var tint = UIFactory.Panel("HardTint", root, new Color(0.22f, 0f, 0.06f, 0.16f)); UIFactory.Stretch(tint.rectTransform); tint.raycastTarget = false; }
            float inset = Th.edgeGestureInset;
            // Steps Left
            var box = V2.Card("StepsLeft", root, Th); box.raycastTarget = false;
            UIFactory.Place(box.rectTransform, new Vector2(0f, 1f), new Vector2(96f, 92f), new Vector2(inset, -6f));
            var bv = UIFactory.VStack(box.transform, 0f, 4f); UIFactory.Stretch(bv);
            UIFactory.Label(bv, "Steps Left", Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th, true);
            _shots = UIFactory.Label(bv, "", Th.hudNumberSp, Color.white, TextAnchor.MiddleCenter, Th, true); _shots.fontStyle = FontStyle.Bold; UIArt.Pop(_shots, V2.Ink, 2f);
            V2.Stars(bv, 3, 18f, 1f, out _hudStars);
            // destruction % bar
            var barBg = V2.RoundPanel("Bar", root, new Color(0f, 0f, 0f, 0.55f));
            var brt = barBg.rectTransform; brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(inset + 104f, -40f); brt.offsetMax = new Vector2(-inset - 8f, -14f);
            _bar = UIFactory.Panel("Fill", barBg.transform, Th.goldFill); UIFactory.Stretch(_bar.rectTransform, 3f); _bar.raycastTarget = false;
            _bar.type = Image.Type.Filled; _bar.fillMethod = Image.FillMethod.Horizontal;
            var tick = UIFactory.Panel("WinTick", barBg.transform, Th.white); tick.raycastTarget = false; _tickImg = tick; _tick = tick.rectTransform;
            var trt = tick.rectTransform; trt.anchorMin = new Vector2(_s.Ctx.level.winThreshold, 0); trt.anchorMax = new Vector2(_s.Ctx.level.winThreshold, 1); trt.sizeDelta = new Vector2(3, 4);
            _pct = UIFactory.Label(barBg.transform, "", Th.bodySp, Color.white, TextAnchor.MiddleRight, Th, true); UIFactory.Stretch(_pct.rectTransform, 2f); _pct.rectTransform.offsetMax = new Vector2(-8f, 0f);
            _pct.fontStyle = FontStyle.Bold; UIArt.Pop(_pct, V2.Ink, 1.5f);
            float below = 46f;
            if (_s.Ctx.mode == RunMode.DailyBoss)
            {
                var hp = V2.RoundPanel("BossHp", root, new Color(0f, 0f, 0f, 0.6f)); var hrt = hp.rectTransform;
                hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
                hrt.offsetMin = new Vector2(inset + 104f, -76f); hrt.offsetMax = new Vector2(-inset - 8f, -48f);
                _bossBar = UIFactory.Panel("Hp", hp.transform, UITheme.H("DC2626")); UIFactory.Stretch(_bossBar.rectTransform, 3f); _bossBar.raycastTarget = false;
                _bossBar.type = Image.Type.Filled; _bossBar.fillMethod = Image.FillMethod.Horizontal; _bossBar.fillAmount = 1f;
                var sk = UIArt.Icon(hp.transform, UIArt.Skull, 30f); UIFactory.Place(sk.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 30f), new Vector2(-6f, 0f));
                var hl = UIFactory.Label(hp.transform, "BOSS HP · " + UIFactory.Truncate(_s.Ctx.level.name, 18), Th.captionSp, Color.white, TextAnchor.MiddleCenter, Th, true); UIFactory.Stretch(hl.rectTransform); UIArt.Pop(hl, V2.Ink, 1f);
                below = 82f;
            }
            _status = UIFactory.Label(root, "", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th); UIFactory.Band(_status.rectTransform, true, 24f, below + 50f); UIArt.Pop(_status, V2.Ink, 1f);
            _combo = UIFactory.Label(root, "", Th.titleSp, Th.violet, TextAnchor.MiddleCenter, Th, true); // COMBO is violet (asset_list ui_label_combo)
            _combo.rectTransform.localScale = Vector3.zero; UIFactory.Place(_combo.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(240, 48), Vector2.zero);
            // building strip (right edge): the structure in 5 height bands, top band first; each tile fades as its band falls
            _strip = UIFactory.Rect("BuildingStrip", root); UIFactory.Place(_strip, new Vector2(1f, 0.5f), new Vector2(46f, StripBands * 50f), new Vector2(-inset - 4f, 40f));
            var sv = _strip.gameObject.AddComponent<VerticalLayoutGroup>(); sv.spacing = 4f; sv.childControlHeight = sv.childControlWidth = true; sv.childForceExpandHeight = sv.childForceExpandWidth = false;
            _stripTiles = new Image[StripBands];
            for (int i = 0; i < StripBands; i++)
            {
                var t = V2.RoundPanel("Band" + (StripBands - i), _strip, new Color(0.95f, 0.75f, 0.35f, 0.9f)); var le = t.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 46f; le.minWidth = le.preferredWidth = 46f;
                var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.04f, 0.07f, 0.17f, 0.8f); o.effectDistance = new Vector2(2f, -2f);
                _stripTiles[i] = t;
            }
            // pause bottom-left; power-ups bottom centre; FIRE bottom-right (thumb zone)
            var pause = V2.RoundButton(root, "II", Th.navy500, () => Router.Push(new PauseModal(_s)), Th, 52f, "Button_II");
            UIFactory.Place((RectTransform)pause.transform, new Vector2(0f, 0f), new Vector2(52, 52), new Vector2(inset, inset + 8));
            var pu = UIFactory.HStack(root, Th.gap, 64f); UIFactory.Place(pu, new Vector2(0f, 0f), new Vector2(212, 64), new Vector2(inset + 60f, inset + 8));
            for (int i = 0; i < 3; i++)
            {
                int idx = i; _pu[i] = UIFactory.Button(pu, "", Th.navy500, () => _s.PowerUps.ToggleArm(idx), Th, 64, 64, Th.captionSp);
                _pu[i].gameObject.name = "Button_PowerUp" + (i + 1); _pu[i].image.sprite = V2.Rounded; _pu[i].image.type = Image.Type.Sliced;
                var tx = _pu[i].GetComponentInChildren<Text>(); tx.alignment = TextAnchor.LowerRight; UIArt.Pop(tx, V2.Ink, 1.5f);
                _puIcon[i] = UIArt.Icon(_pu[i].transform, null, 56f); UIFactory.Stretch(_puIcon[i].rectTransform, 4f); _puIcon[i].transform.SetAsFirstSibling();
            }
            _fire = V2.RoundButton(root, "FIRE", Th.orange, () => _s.TryFire(), Th, 88f);
            UIFactory.Place((RectTransform)_fire.transform, new Vector2(1f, 0f), new Vector2(88, 88), new Vector2(-inset, inset + 8));
#if CRASHVERSE_DEV || DEVELOPMENT_BUILD
            // BUG-24: dev-only "aim at practice hint" so CI smoke can play a known-winning L1 line deterministically. Never in release.
            var hint = UIFactory.Button(root, "HINT", Th.navy500, () => _s.aim.SetAim(_s.Ctx.level.hintYaw, _s.Ctx.level.hintPitch), Th, 64, 64, Th.captionSp);
            hint.gameObject.name = "Button_DevAimHint";
            UIFactory.Place((RectTransform)hint.transform, new Vector2(1f, 0f), new Vector2(64, 64), new Vector2(-inset - 104, inset + 8));
#endif
            _s.Changed += Refresh; _s.Ended += OnEnded;
            _s.StepProtected += () => Router.Toast("🛡 Shield: step protected!");
            _s.tracker.ComboChanged += n => { if (n >= ComboTracker.ShowFrom) _combo.text = "COMBO x" + n; _comboSm.OnCombo(n); }; // shows from x3; pop sizes escalate at x5/x10/x20
            Refresh();
        }

        void Refresh()
        {
            if (_s == null || _s.Ctx == null) return;
            int total = _s.Ctx.ShotBudget;
            if (_lastShots >= 0 && _s.ShotsLeft < _lastShots) { _shotMs = 0f; _comboSm.OnShot(); Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.AnimEvents.UiShotSpend); } // new shot: punch + combo out
            if (_lastShots >= 0 && _s.ShotsLeft > _lastShots) { _tickHit = false; _fillTarget = -1f; _tickImg.color = Th.white; } // Retry / shield refund: tick re-arms
            _lastShots = _s.ShotsLeft;
            int stars = StarRules.Compute(true, _s.ShotsLeft, total, _s.Ctx.level.star3Pct, _s.Ctx.level.star2Pct);
            _shots.text = _s.ShotsLeft.ToString();
            for (int i = 0; i < 3; i++) V2.SetStar(_hudStars[i], i < stars);
            for (int i = 0; i < 3; i++)
            {
                var st = _s.PowerUps.Slots[i];
                _pu[i].gameObject.SetActive(st != null);
                if (st == null) continue;
                UIFactory.SetLabel(_pu[i], "×" + st.charges);
                _puIcon[i].sprite = UIArt.Get(UIArt.PowerUpIcon(st.data.id)); _puIcon[i].color = st.charges > 0 || st.armed ? Color.white : new Color(0.4f, 0.4f, 0.45f, 1f);
                _pu[i].image.color = st.armed ? Th.goldTrim : V2.SlotColor(st.data.slot);
                _pu[i].interactable = _s.State == SessionState.Aiming && (st.charges > 0 || st.armed);
            }
            _fire.interactable = _s.State == SessionState.Aiming && _s.ShotsLeft > 0;
            _status.text = _s.State == SessionState.Resolving ? "Settling…" : _s.State == SessionState.BossTell ? "The Tidewarden shifts!" :
                _s.Boss != null ? $"Core hits {_s.Boss.CoreHits}/{Crashverse.Boss.TidewardenController.CoreHitsToWin} · Shutter {(_s.Boss.ShutterOpen ? "OPEN" : "closed")}" :
                _s.HintVisible ? "💡 Follow the ghost arc" : _s.Ctx.titan != null ? _s.Ctx.titan.ruleText : _s.Ctx.IsHard ? "HARD LEVEL" : "";
        }

        /// <summary>Building strip: 5 height bands of objective pieces (bottom→top); a tile's alpha follows the share of its band still standing.</summary>
        void TickStrip()
        {
            var pieces = _s.tracker.Pieces;
            if (_bands == null)
            {
                if (pieces == null || pieces.Count == 0) return;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var b in pieces) { if (b == null || !b.isObjectivePiece) continue; lo = Mathf.Min(lo, b.InitialY); hi = Mathf.Max(hi, b.InitialY); }
                _bands = new List<DestructibleBlock>[StripBands]; for (int i = 0; i < StripBands; i++) _bands[i] = new List<DestructibleBlock>();
                float span = Mathf.Max(0.01f, hi - lo);
                foreach (var b in pieces) { if (b == null || !b.isObjectivePiece) continue; int k = Mathf.Clamp((int)((b.InitialY - lo) / span * StripBands), 0, StripBands - 1); _bands[k].Add(b); }
            }
            if ((_stripT -= Time.unscaledDeltaTime) > 0f) return; _stripT = 0.25f;
            for (int i = 0; i < StripBands; i++)
            {
                var band = _bands[StripBands - 1 - i]; float tot = 0f, up = 0f; // tile 0 = top band
                foreach (var b in band) { if (b == null) continue; tot += b.Mass; if (!DestructionTracker.CountsDestroyed(b)) up += b.Mass; }
                bool any = tot > 0f; float f = any ? up / tot : 0f;
                _stripTiles[i].gameObject.SetActive(any);
                _stripTiles[i].color = f > 0.6f ? new Color(0.95f, 0.75f, 0.35f, 0.95f) : f > 0.25f ? new Color(0.95f, 0.45f, 0.2f, 0.9f) : new Color(0.35f, 0.35f, 0.4f, 0.6f);
            }
        }

        public override void Tick()
        {
            if (_s == null || _s.tracker == null) return;
            float r = _s.tracker.DestroyedRatio;
            _pct.text = Mathf.FloorToInt(r * 100f) + "%" + (_s.Ctx.IsTitan ? $"  {_s.TitanDamageScore}" : "");
            if (_bossBar != null) _bossBar.fillAmount = _s.BossHpFraction;
            TickStrip();
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
        public bool Won => _r != null && _r.won; // dev tap probe / smoke script
        readonly GameSession _s; readonly RunResult _r;
        // 06 win sequence: stars pop one by one (0→1.3→1, 250 ms, 300 ms stagger), mastery tick, score count-up (600 ms). Unscaled; tap = skip.
        Image[] _star = new Image[3]; Text _mastery, _score; string _scoreFmt; float _seqMs; bool _starRaised0, _starRaised1, _starRaised2, _masteryRaised;
        public override bool IsModal => true;
        public ResultsScreen(GameSession s, RunResult r) { _s = s; _r = r; }
        /// <summary>QA C-1/C-3 (smoke + probe): the win sequence (stars, mastery, score count-up) has reached its final frame.</summary>
        public bool SequenceDone => _seqMs >= Crashverse.Feel.WinSequence.Steps[5].startMs + Crashverse.Feel.UiMotionMath.ResultsScoreMs + 100f;
        int _doneFrame = -1;
        /// <summary>QA C-1b/C-3b: the logic finishing isn't enough on a slow GPU (the API 36 frame lagged ~1 s): final values are in
        /// the labels (score text, earned stars at scale 1) AND 10 more frames have been rendered since.</summary>
        public bool SettledOnScreen
        {
            get
            {
                if (!SequenceDone) return false;
                if (_score != null && _score.text != ScoreLabelFinal) return false;
                for (int i = 0; i < 3; i++) if (_star[i] != null && i < _r.stars && System.Math.Abs(_star[i].rectTransform.localScale.x - 1f) > 0.01f) return false;
                if (_doneFrame < 0) _doneFrame = Time.frameCount;
                return Time.frameCount - _doneFrame >= 10;
            }
        }
        public string ScoreLabel => _score != null ? _score.text : "";
        string ScoreLabelFinal => "Score " + _r.score + _scoreFmt;
        public int Score => _r != null ? _r.score : 0; public int Stars => _r != null ? _r.stars : 0; public float Ratio => _r != null ? _r.ratio : 0f;
        /// <summary>QA C-4 (PX-8): "So close!" only when the run was actually close; a 0% or far-off run doesn't get it.</summary>
        public static string FailTitle(float ratio, float threshold) => ratio >= threshold * 0.6f ? "So close!" : "Out of shots!";
        protected override void Build(RectTransform root)
        {
            var lvl = _s.Ctx.level; var mode = _s.Ctx.mode;
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.55f)); UIFactory.Stretch(dim.rectTransform);
            var p = UIFactory.TrimPanel("Results", root, Th); UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(320, 470), Vector2.zero);
            var v = UIFactory.VStack(p.transform, Th.gap, 16f); UIFactory.Stretch(v);
            // Target UI §6: big stars, "Level N Completed!", rewards (coins, gems, chest), Next Level
            if (_r.won && !_s.Ctx.IsTitan)
            {
                V2.Stars(v, _r.stars, 64f, 6f, out _star);
                for (int i = 0; i < 3; i++) _star[i].rectTransform.localScale = Vector3.zero;
            }
            string title = !_r.won ? FailTitle(_r.ratio, lvl.winThreshold) : _s.Ctx.IsTitan ? "TITAN DOWN!" : mode == RunMode.DailyBoss ? "BOSS DEFEATED!"
                         : mode == RunMode.Campaign && lvl.number > 0 ? $"Level {lvl.number}\nCompleted!" : "SMASHED!";
            V2.Title(v, title, Th.titleSp, _r.won ? Th.goldFill : Th.red, Th).GetComponent<LayoutElement>().minHeight = _r.won && lvl.number > 0 ? 76f : 44f;
            UIFactory.Label(v, $"Destroyed {Mathf.FloorToInt(_r.ratio * 100)}% (need {Mathf.RoundToInt(lvl.winThreshold * 100)}%)   Shots left {_r.shotsLeft}", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
            if (lvl.mastery != null) _mastery = UIFactory.Label(v, (_r.mastery ? "◆ Mastery complete: " : "◇ Mastery: ") + lvl.mastery.text, Th.captionSp, _r.mastery ? Th.cyan : Th.body, TextAnchor.MiddleCenter, Th);
            if (_r.bestCombo >= ComboTracker.ShowFrom) UIFactory.Label(v, "Best combo x" + _r.bestCombo, Th.bodySp, Th.orange, TextAnchor.MiddleCenter, Th);
            if (mode == RunMode.Campaign && _r.won) { _scoreFmt = "  (best " + AppServices.Save.BestScore(lvl.id) + ")"; _score = UIFactory.Label(v, "Score 0" + _scoreFmt, Th.bodySp, Th.goldFill, TextAnchor.MiddleCenter, Th); }
            if (_s.Ctx.IsTitan) UIFactory.Label(v, $"Titan score {_r.titanScore}" + (_r.titanSubmit != null ? $"\n{_r.titanSubmit.status}: {_r.titanSubmit.message}" : ""), Th.bodySp, Th.orange, TextAnchor.MiddleCenter, Th);
            if (_r.firstClear && lvl.reward != null) foreach (var rw in lvl.reward) UIFactory.Label(v, rw.type == "powerup" ? "Unlocked: " + (AppServices.Content.GetPowerUp(rw.id)?.name ?? rw.id) : $"+{rw.amount} {rw.type} {rw.id}", Th.captionSp, Th.green, TextAnchor.MiddleCenter, Th);
            if (_r.rewards != null && !_r.rewards.IsEmpty)
            {
                V2.Title(v, "Rewards", Th.headingSp, Color.white, Th);
                var rr = UIFactory.HStack(v, Th.gap, 40f); rr.GetComponent<LayoutElement>().flexibleHeight = 0f;
                if (_r.rewards.coins > 0) V2.Pill(rr, UIArt.Coin, "+" + _r.rewards.coins, Th, 88f).transform.parent.name = "Reward_Coins";
                if (_r.rewards.gems > 0) V2.Pill(rr, UIArt.Gem, "+" + _r.rewards.gems, Th, 72f).transform.parent.name = "Reward_Gems";
                if (_r.rewards.chests > 0) V2.Pill(rr, UIArt.Chest, "+" + _r.rewards.chests, Th, 72f).transform.parent.name = "Reward_Chest";
            }
            if (!string.IsNullOrEmpty(_r.note)) UIFactory.Label(v, _r.note, Th.captionSp, Th.body, TextAnchor.MiddleCenter, Th);
            UIFactory.Spacer(v, 8);
            // Addendum: free hint from 2nd consecutive fail, never ranked, no ads
            if (!_r.won && HintRules.ShowHintButton(lvl, AppServices.Save.FailsInRow(lvl.id), mode == RunMode.TitanRanked)) // BUG-08: campaign, events, Titan practice; never ranked
                UIFactory.Button(v, "💡 Free Hint", Th.goldShade, () => { _s.Ctx.hintArmed = true; Router.Pop(); _s.Retry(); }, Th, 48);
            var row = UIFactory.HStack(v, Th.gap, Th.buttonL);
            row.GetComponent<LayoutElement>().flexibleHeight = 0f; // QA C-4: the force-expanding HStack soaked up the panel's spare height
            UIFactory.Button(row, "Map", Th.navy500, () => GameplayDirector.ExitToMap(), Th);
            if (mode != RunMode.TitanRanked) UIFactory.Button(row, "Retry", Th.navy500, () => { Router.Pop(); _s.Retry(); }, Th);
            var next = NextLevel(lvl);
            if (_r.won && mode == RunMode.Campaign && next != null && UnlockRules.IsLevelUnlocked(next, AppServices.Save))
                V2.Glossy(v, "Next Level", Th.green, () => { GameplayDirector.ExitToMap(); AppServices.Router.Push(new LevelSelectScreen(next)); }, Th, Th.buttonL, 0f, Th.headingSp, "Button_NextLevel");
            // QA C-4b: the panel hugs its content (the fail panel kept the win panel's 470 height, leaving a big empty area)
            LayoutRebuilder.ForceRebuildLayoutImmediate(v);
            float h = LayoutUtility.GetPreferredHeight(v);
            if (h > 0f) p.rectTransform.sizeDelta = new Vector2(320f, Mathf.Clamp(h, 240f, 600f));
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
        // #45: Audio (Music/SFX volume), Graphics quality, Motion (Reduce shake locked under Reduce motion), Haptics (#29/#37).
        public override bool IsModal => true; Text _shake, _motion, _haptics, _hide, _music, _sfx, _quality; Button _shakeBtn;
        protected override void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0, 0, 0, 0.6f)); UIFactory.Stretch(dim.rectTransform);
            // root is the SafeArea rect (UIFramework), so the stretched panel never sits under a notch / nav bar.
            var p = UIFactory.TrimPanel("Settings", root, Th); UIFactory.Stretch(p.rectTransform, 16f);
            var outer = UIFactory.VStack(p.transform, Th.gap, 12f); UIFactory.Stretch(outer);
            UIFactory.Label(outer, "Settings", Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            var v = UIFactory.Scroll(outer, Th.gap, out var sr); sr.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1; // small phones: scroll, rows stay 48dp
            var g = Crashverse.Core.GameSettings.Current; var m = Crashverse.Feel.MotionSettings.Current;

            UIFactory.Label(v, "Audio", Th.bodySp, Th.goldFill, TextAnchor.MiddleLeft, Th, true);
            _music = Stepper(v, () => g.SetMusicPercent(g.MusicPercent - Crashverse.Core.GameSettings.Step), () => g.SetMusicPercent(g.MusicPercent + Crashverse.Core.GameSettings.Step));
            _sfx = Stepper(v, () => g.SetSfxPercent(g.SfxPercent - Crashverse.Core.GameSettings.Step), () => g.SetSfxPercent(g.SfxPercent + Crashverse.Core.GameSettings.Step));

            UIFactory.Label(v, "Graphics", Th.bodySp, Th.goldFill, TextAnchor.MiddleLeft, Th, true);
            _quality = UIFactory.Button(v, "", Th.navy500, () => { g.SetQuality(g.NextQuality()); Crashverse.Perf.QualityController.ApplySaved(); Refresh(); }, Th, 48).GetComponentInChildren<Text>();

            UIFactory.Label(v, "Motion", Th.bodySp, Th.goldFill, TextAnchor.MiddleLeft, Th, true);
            _shakeBtn = UIFactory.Button(v, "", Th.navy500, () => { if (!m.ReduceShakeLocked) m.SetReduceShake(!m.ReduceShake); Refresh(); }, Th, 48);
            _shake = _shakeBtn.GetComponentInChildren<Text>();
            _motion = UIFactory.Button(v, "", Th.navy500, () => { m.SetReduceMotion(!m.ReduceMotion); Refresh(); }, Th, 48).GetComponentInChildren<Text>();
            _haptics = UIFactory.Button(v, "", Th.navy500, () => { m.SetHaptics(!m.HapticsEnabled); Refresh(); }, Th, 48).GetComponentInChildren<Text>(); // default ON; suppressed while Reduce motion is ON (haptics.md)

            UIFactory.Label(v, "Privacy", Th.bodySp, Th.goldFill, TextAnchor.MiddleLeft, Th, true);
            _hide = UIFactory.Button(v, "", Th.navy500, ToggleHide, Th, 48).GetComponentInChildren<Text>();
            UIFactory.Label(v, "Account", Th.bodySp, Th.goldFill, TextAnchor.MiddleLeft, Th, true);
            UIFactory.Button(v, "Linked accounts", Th.navy500, () => Router.Push(new LinkedAccountsModal()), Th, 48);
            UIFactory.Button(v, AccountDeletion.Pending ? "Delete account (pending until online)" : "Delete account", Th.red, () => Router.Push(new DeleteAccountScreen()), Th, 48);
            UIFactory.Button(outer, "Close", Th.navy500, () => Router.Pop(), Th, 48);
            Refresh();
        }
        /// <summary>[ − ]  label  [ + ] row; both buttons 48×48dp.</summary>
        Text Stepper(RectTransform parent, Action down, Action up)
        {
            var row = UIFactory.HStack(parent, Th.gap, 48f);
            UIFactory.Button(row, "\u2212", Th.navy500, () => { down(); Refresh(); }, Th, 48, 48);
            var t = UIFactory.Label(row, "", Th.captionSp, Th.white, TextAnchor.MiddleCenter, Th);
            UIFactory.Button(row, "+", Th.navy500, () => { up(); Refresh(); }, Th, 48, 48);
            return t;
        }
        async void ToggleHide()
        {
            try { var v = await AppServices.Backend.SetProfileVisibility(!(AppServices.Account?.hideName ?? false)); if (AppServices.Account != null) AppServices.Account.hideName = v.hideName; Refresh(); }
            catch (BackendException) { Router.Toast("You need to be online to change this."); }
        }
        void Refresh()
        {
            var g = Crashverse.Core.GameSettings.Current; var m = Crashverse.Feel.MotionSettings.Current;
            _music.text = $"Music: {g.MusicPercent}%";
            _sfx.text = $"Sound effects: {g.SfxPercent}%";
            _quality.text = "Graphics quality: " + Crashverse.Core.GameSettings.QualityName(g.Quality);
            // 07: Reduce motion implies Reduce shake → row shows ON, greyed and locked; the stored choice comes back when Reduce motion is turned off.
            _shake.text = "Reduce shake: " + (m.ReduceShakeEffective ? "ON" : "OFF") + (m.ReduceShakeLocked ? " (included in Reduce motion)" : "");
            _shakeBtn.interactable = !m.ReduceShakeLocked;
            _motion.text = "Reduce motion: " + (m.ReduceMotion ? "ON" : "OFF") + (m.ReduceMotionIsAuto ? " (system)" : "");
            _haptics.text = "Haptics: " + (m.HapticsEnabled ? "ON" : "OFF") + (m.HapticsLockedByReduceMotion ? " (off while Reduce motion is on)" : "");
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
        readonly Action<bool> _done; int _year, _month; Button _yearBtn, _monthBtn, _ok; Text _err;
        public string Error => _err != null ? _err.text : "";
        public AgeGateScreen(Action<bool> done) { _done = done; }
        public override bool OnBack() => true; // must answer
        protected override void Build(RectTransform root)
        {
            var bg = UIFactory.Panel("Bg", root, Th.navy900); UIFactory.Stretch(bg.rectTransform);
            var v = UIFactory.VStack(root, Th.gap * 2, 24f); UIFactory.Place(v, new Vector2(0.5f, 0.5f), new Vector2(320, 440), Vector2.zero); // QA C-7: room for the 2-line privacy note
            UIFactory.Label(v, "Welcome to CRASHVERSE", Th.headingSp, Th.goldFill, TextAnchor.MiddleCenter, Th, true);
            UIFactory.Label(v, "Please enter your date of birth.", Th.bodySp, Th.white, TextAnchor.MiddleCenter, Th);
            _monthBtn = UIFactory.Button(v, "Month", Th.navy500, PickMonth, Th, 56);
            _yearBtn = UIFactory.Button(v, "Year", Th.navy500, PickYear, Th, 56);
            _ok = UIFactory.Button(v, "Continue", Th.green, Confirm, Th, 56); _ok.interactable = false;
            _err = UIFactory.Label(v, "Choose your birth month and year.", Th.captionSp, Th.goldFill, TextAnchor.MiddleCenter, Th);
            var priv = UIFactory.Label(v, "We only keep an age range, never your date of birth.", Th.captionSp, Th.white, TextAnchor.MiddleCenter, Th);
            TwoLines(_err); TwoLines(priv); // QA C-7: the wrapped second line was clipped
        }
        static void TwoLines(Text t)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var le = t.gameObject.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = (float)System.Math.Ceiling(t.fontSize * 2.6f);
        }
        void PickMonth()
        {
            var items = new List<(string, Action)>();
            for (int i = 0; i < 12; i++) { int m = i + 1; items.Add((Months[i], () => { _month = m; _monthBtn.GetComponentInChildren<Text>().text = Months[m - 1]; Validate(); })); }
            Router.Push(new ListModal("Month of birth", items));
        }
        /// <summary>Index the year list opens at: middle of now..now−100 (neutral, not tied to any age threshold).</summary>
        public const int YearListMiddle = 50;
        void PickYear()
        {
            var items = new List<(string, Action)>(); int now = DateTime.UtcNow.Year;
            for (int y = now; y >= now - 100; y--) { int yy = y; items.Add((yy.ToString(), () => { _year = yy; _yearBtn.GetComponentInChildren<Text>().text = yy.ToString(); Validate(); })); } // newest first: no eligible-age default
            // Security (PR #52): neutral start, no eligible-age anchor: opens at the middle of the 100-year range (or the year
            // already picked, on reopen); every year still scrolls. Smoke + tap tests select the year by name.
            Router.Push(new ListModal("Year of birth", items, _year > 0 ? now - _year : YearListMiddle));
        }
        /// <summary>Continue stays disabled until both are picked and the date isn't in the future; the caption says what's missing.</summary>
        void Validate()
        {
            var now = DateTime.UtcNow;
            string e = _month == 0 && _year == 0 ? "Choose your birth month and year."
                     : _month == 0 ? "Choose your birth month."
                     : _year == 0 ? "Choose your birth year."
                     : (_year == now.Year && _month > now.Month) ? "That date is in the future. Check the month and year." : "";
            _err.text = e; _ok.interactable = e.Length == 0;
        }
        /// <summary>Test hooks (PlayMode tap tests).</summary>
        public Button MonthButton => _monthBtn; public Button YearButton => _yearBtn; public Button ContinueButton => _ok;
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
