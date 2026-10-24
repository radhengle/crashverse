using System;
using System.Collections.Generic;
using System.IO;
using Crashverse.Feel;

/// <summary>Animation Spec v1 (animation/01–07): easing/tween maths, aim spring, cannon motion curves. Engine-free, runs in CI (dotnet run --project Tools/Tests).</summary>
class AnimMemPrefs : IMotionPrefs
{
    readonly Dictionary<string, bool> _d = new Dictionary<string, bool>();
    public bool TryGet(string key, out bool v) => _d.TryGetValue(key, out v);
    public void Set(string key, bool v) => _d[key] = v;
}

static class AnimTests
{
    static bool Near(float a, float b, float eps = 1e-3f) => Math.Abs(a - b) <= eps;
    static string Src(string rel) => File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "../../..", rel));

    public static void Register()
    {
        T.Add("SHAKE_cap_pool_and_decay", () =>
        {
            var st = new ShakeStack();
            for (int i = 0; i < 50; i++) st.Add(ShakeTable.BossPhase);
            T.True(st.Count == ShakeStack.Capacity, "fixed pool, never grows");
            var (p, r) = st.Tick(0.001f); T.True(Near(0.15f, p, 1e-4f) && Near(1.5f, r, 1e-4f), "combined cap 0.15 m / 1.5°");
            var one = new ShakeStack(); one.Add(ShakeTable.Fire);
            var (p0, r0) = one.Tick(0f); T.True(Near(0.06f, p0) && Near(0.6f, r0), "fire 0.06 m / 0.6° at t0");
            var (p1, _) = one.Tick(0.09f); T.True(p1 < 0.06f && p1 > 0f, "decays (ease-out quad)");
            one.Tick(0.1f); T.Eq(0, one.Count, "fire shake gone after 180 ms");
            var two = new ShakeStack(); two.Add(ShakeTable.Fire); two.Add(ShakeTable.ImpactWood);
            var (ps, rs) = two.Tick(0f); T.True(Near(0.08f, ps) && Near(0.8f, rs), "shakes add below the cap");
            T.True(two.Frequency > 9f && two.Frequency < 14f, "amplitude-weighted frequency");
            var off = new ShakeStack { Scale = 0f }; off.Add(ShakeTable.Fire); T.Eq(0, off.Count, "scale 0 (Reduce shake/motion) adds nothing");
            var live = new ShakeStack(); live.Add(ShakeTable.Fire); live.Scale = 0f; var (pz, rz) = live.Tick(0.01f); T.True(pz == 0f && rz == 0f, "toggling mid-shake zeroes it at once");
            var full = new ShakeStack(); for (int i = 0; i < ShakeStack.Capacity; i++) full.Add(ShakeTable.ImpactWood); full.Add(ShakeTable.BossPhase);
            var (pf, _) = full.Tick(0f); T.True(pf >= 0.12f, "when full, a big shake replaces the weakest");
        });
        T.Add("SHAKE_router_table_07", () =>
        {
            ShakeSpec s;
            T.True(ShakeRouter.TryGet(AnimEvents.WreckerFire, out s) && s.pos == 0.06f && s.rot == 0.6f && s.freq == 9 && s.durationMs == 180, "fire 0.06/0.6°/9 Hz/180 ms");
            T.True(ShakeRouter.TryGet("IMPACT_WOOD", out s) && s.durationMs == 100, "impact wood");
            T.True(ShakeRouter.TryGet("BREAK_BRICK", out s) && s.pos == 0.04f, "brick break");
            T.True(ShakeRouter.TryGet("BREAK_STEEL_JOINT", out s) && s.pos == 0.06f && s.freq == 12, "steel joint");
            T.True(ShakeRouter.TryGet(AnimEvents.CollapseLand, out s) && s.durationMs == 250, "collapse land");
            T.True(ShakeRouter.TryGet("EXPLODE_CRATE", out s) && s.pos == 0.10f && ShakeRouter.TryGet("EXPLODE_BARREL", out _) && ShakeRouter.TryGet("PU_CHAIN_PULSE", out _), "explosion / chain reactor (+ deprecated barrel alias)");
            T.True(ShakeRouter.TryGet("PU_GRAVITY_BURST", out s) && s.pos == 0.08f, "gravity burst");
            T.True(ShakeRouter.TryGet(AnimEvents.BossTidePhase2, out s) && s.durationMs == 600 && ShakeRouter.TryGet(AnimEvents.BossTitanDefeat, out _), "boss phase / defeat");
            T.True(!ShakeRouter.TryGet(AnimEvents.UiBtnPress, out _) && !ShakeRouter.TryGet(AnimEvents.WreckerFireAnticipate, out _), "UI / anticipation don't shake");
        });
        T.Add("SHAKE_router_covers_all_impacts", () =>
        {
            ShakeSpec s; ShakeFamily f;
            string[] all = { "IMPACT_WOOD", "BREAK_WOOD", "IMPACT_BRICK", "BREAK_BRICK", "IMPACT_STEEL", "IMPACT_STEEL_JOINT", "BREAK_STEEL_JOINT", "IMPACT_TERRAIN", "EXPLODE_CRATE",
                AnimEvents.CollapseLand, "PU_CHAIN_PULSE", "PU_GRAVITY_BURST", "PU_PHASE_HIT", AnimEvents.BossTidePhase2, AnimEvents.BossTideDefeat, "BOSS_TIDE_JOINT_BREAK",
                AnimEvents.BossTideCoreHit, AnimEvents.BossTitanStage, AnimEvents.BossTitanHit, AnimEvents.BossTitanDefeat, "PHYS_JOINT_SNAP", "PHYS_COLLAPSE_END", "PHYS_BREAK" };
            foreach (var e in all) T.True(ShakeRouter.TryGet(e, out s, out f) && f != ShakeFamily.None && s.pos > 0f, "routed: " + e);
            T.True(ShakeRouter.TryGet("IMPACT_BRICK", out s) && s.pos == 0.02f && s.durationMs == 120, "02 brick ±0.02 m 120 ms");
            T.True(ShakeRouter.TryGet("IMPACT_STEEL", out s) && s.pos == 0.01f && s.freq == 6 && s.durationMs == 200, "02 steel ring 6 Hz 200 ms 0.01 m");
            T.True(!ShakeRouter.TryGetPhys("PHYS_IMPACT", ShakeRouter.MatWood, ShakeRouter.TierLight, out _, out _), "light physics impacts don't rattle");
            T.True(ShakeRouter.TryGetPhys("PHYS_IMPACT", ShakeRouter.MatSteel, ShakeRouter.TierHeavy, out s, out f) && s.freq == 6 && f == ShakeFamily.Impact, "phys impact by material");
            T.True(ShakeRouter.TryGetPhys("PHYS_BREAK", ShakeRouter.MatBrick, ShakeRouter.TierMedium, out s, out _) && s.pos == 0.04f, "phys break by material");
            T.True(!ShakeRouter.TryGetPhys("PHYS_CRACK", 0, 2, out _, out _) && !ShakeRouter.TryGetPhys("PHYS_COLLAPSE_START", 0, 2, out _, out _), "crack / collapse start silent");
            T.True(ShakeRouter.TryGet("IMPACT_GLASS", out s, out f) && s.Equals(ShakeTable.ImpactWood) && f == ShakeFamily.Impact && ShakeRouter.TryGet("BREAK_GLASS", out s) && s.Equals(ShakeTable.ImpactWood), "glass light like wood");
            T.True(ShakeRouter.TryGetPhys("PHYS_BREAK", ShakeRouter.MatGlass, 2, out s, out _) && s.Equals(ShakeTable.ImpactWood), "phys glass break light");
            T.True(ShakeRouter.IsImpact("IMPACT_TERRAIN") && !ShakeRouter.IsImpact("BREAK_STEEL_JOINT"), "impact family");
        });
        T.Add("SHAKE_dispatcher_dedupe_and_haptics", () =>
        {
            var st = new ShakeStack(); var h = new RecHaptics(); var prefs = new AnimMemPrefs(); var m = new MotionSettings(prefs, false);
            var d = new ShakeDispatcher(st) { Haptics = h, Settings = m };
            T.True(d.Submit("BREAK_STEEL_JOINT", 1, 0f), "joint shake"); T.True(!d.Submit("PHYS_JOINT_SNAP", 1, 0f), "PHYS_JOINT_SNAP same frame deduped");
            T.True(d.Submit(AnimEvents.CollapseLand, 1, 0f) && !d.Submit("PHYS_COLLAPSE_END", 1, 0f), "COLLAPSE_LAND + PHYS_COLLAPSE_END → one"); d.Tick(50f); // flush collapse haptic window
            T.True(d.Submit("IMPACT_WOOD", 2, 100f) && d.Submit(AnimEvents.BossTideCoreHit, 2, 100f) && !d.Submit("IMPACT_WOOD", 2, 100f), "impact family: stronger may replace, weaker dropped");
            T.True(d.Submit("BREAK_STEEL_JOINT", 3, 200f), "next frame allowed"); T.Eq(5, st.Count, "5 shakes in the pool");
            T.Eq(5, h.Calls, "haptics driven by the same accepted events"); T.Eq(HapticBuckets.For(0.4f), h.Last, "joint 0.06/0.6° → strength 0.4 bucket");
            m.SetReduceMotion(true); st.Scale = m.ShakeScale(QualityTier.Mid); d.Submit("EXPLODE_CRATE", 4, 300f);
            T.Eq(5, h.Calls, "Reduce motion: no camera shake and no haptics (haptics.md)");
            m.SetReduceMotion(false); m.SetHaptics(false); d.Submit("EXPLODE_CRATE", 5, 400f); T.Eq(5, h.Calls, "Haptics OFF → no pulse");
            T.True(new MotionSettings(new AnimMemPrefs(), true).HapticsEnabled, "Haptics default ON (even with OS reduce-animations)");
        });
        T.Add("HAPTICS_toggle_and_reduce_motion", () =>
        {
            var prefs = new AnimMemPrefs(); var m = new MotionSettings(prefs, false); var h = new RecHaptics();
            var d = new ShakeDispatcher(new ShakeStack()) { Haptics = h, Settings = m };
            T.True(m.HapticsEffective && d.Submit("EXPLODE_CRATE", 1, 0f), "defaults"); T.Eq(1, h.Calls, "fires normally"); T.Eq(3, h.Last, "explosion bucket 3");
            m.SetReduceMotion(true); T.True(!m.HapticsEffective && m.HapticsEnabled && m.HapticsLockedByReduceMotion, "RM overrides, toggle preference kept");
            d.Submit("EXPLODE_CRATE", 2, 100f); d.Submit("IMPACT_WOOD", 3, 200f); T.Eq(1, h.Calls, "suppressed while Reduce motion ON");
            var auto = new MotionSettings(new AnimMemPrefs(), true); T.True(auto.ReduceMotionIsAuto && !auto.HapticsEffective, "Android auto Reduce motion also suppresses");
            m.SetReduceMotion(false); m.SetHaptics(false); T.True(!m.HapticsEffective && !m.HapticsLockedByReduceMotion, "toggle OFF");
            d.Submit("EXPLODE_CRATE", 4, 300f); T.Eq(1, h.Calls, "suppressed when Haptics toggle OFF");
            m.SetHaptics(true); d.Submit("EXPLODE_CRATE", 5, 400f); T.Eq(2, h.Calls, "fires again when both allow");
            var g = new GatedRec(); var d2 = new ShakeDispatcher(new ShakeStack()) { Haptics = g, Settings = new MotionSettings(new AnimMemPrefs(), false) };
            d2.Submit("IMPACT_WOOD", 1, 0f); d2.Submit("BREAK_BRICK", 2, 10f); d2.Submit("IMPACT_BRICK", 3, 30f); T.Eq(2, g.Fired, "0 fires, 10 stronger cuts in, 30 dropped (<40 ms)");
            d2.Submit("IMPACT_WOOD", 5, 49f); T.Eq(2, g.Fired, "still inside 40 ms of the last pulse"); d2.Submit("IMPACT_WOOD", 6, 50f); T.Eq(3, g.Fired, "40 ms rate limit still applies; fires again at +40 ms");
        });
        T.Add("HAPTICS_collapse_merge_material_priority", () =>
        {
            T.True(CollapseHapticMerge.Rank("steel") > CollapseHapticMerge.Rank("stone") && CollapseHapticMerge.Rank("stone") > CollapseHapticMerge.Rank("brick")
                && CollapseHapticMerge.Rank("brick") > CollapseHapticMerge.Rank("glass") && CollapseHapticMerge.Rank("glass") > CollapseHapticMerge.Rank("wood"), "steel > stone > brick > glass > wood");
            var mg = new CollapseHapticMerge(); mg.Add("wood", 0f); mg.Add("steel", 15f); mg.Add("glass", 39f);
            T.Eq(0, mg.Flush(39.9f), "window still open"); T.Eq(CollapseHapticMerge.Rank("steel"), mg.WinnerRank, "steel wins regardless of order");
            T.Eq(4, mg.Flush(40f), "one pulse at steel strength"); T.Eq(0, mg.Flush(100f), "only once");
            var h = new RecHaptics(); var d = new ShakeDispatcher(new ShakeStack()) { Haptics = h, Settings = new MotionSettings(new AnimMemPrefs(), false) };
            d.SubmitCollapse("COLLAPSE_LAND", "wood", 1, 0f); d.SubmitCollapse("COLLAPSE_LAND", "stone", 1, 5f); d.SubmitCollapse("COLLAPSE_LAND", "glass", 2, 30f);
            d.Tick(20f); T.Eq(0, h.Calls, "no pulse inside the window");
            d.Tick(41f); T.Eq(1, h.Calls, "3 landings in 40 ms → 1 pulse"); T.Eq(3, h.Last, "stone strength (bucket 3)");
            d.SubmitCollapse("COLLAPSE_LAND", "wood", 3, 100f); d.Tick(140f); T.Eq(2, h.Calls, "next window"); T.Eq(2, h.Last, "wood alone = bucket 2");
            d.SubmitCollapse("COLLAPSE_LAND", "steel", 4, 200f); d.Tick(240f); T.Eq(4, h.Last, "steel alone = bucket 4"); d.SubmitCollapse("COLLAPSE_LAND", "wood", 5, 250f); d.Tick(290f);
            T.Eq(4, h.Calls, "landings 50 ms apart → separate pulses");
        });
        T.Add("HAPTICS_lead_offset_clamp", () =>
        {
            T.Eq(-20f, HapticTiming.LeadMs, "default lead −20 ms");
            T.Eq(1000f, HapticTiming.FireAtMs(1000f), "synchronous event: −20 ms clamped, never before the event");
            T.Eq(1080f, HapticTiming.FireAtMs(1000f, 100f), "known +100 ms peak → 20 ms before the peak");
            T.Eq(1000f, HapticTiming.FireAtMs(1000f, 10f), "peak 10 ms after event: clamped to the event");
            T.Eq(1030f, HapticTiming.FireAtMs(1000f, 0f, 30f), "positive lead (lag) honoured");
        });
        T.Add("HAPTICS_animator_timing", () =>
        {
            RecHapticsLog h; ShakeDispatcher D() { h = new RecHapticsLog(); return new ShakeDispatcher(new ShakeStack()) { Haptics = h, Settings = new MotionSettings(new AnimMemPrefs(), false) }; }
            var d = D(); d.Submit("PU_GRAVITY_BURST", 1, 1500f); T.Eq("1@1500", h.Log, "gravity: light tick at implode start");
            d.Tick(1560f); T.Eq("1@1500", h.Log, "peak not yet"); d.Tick(1580f); T.Eq("1@1500 3@1580", h.Log, "peak 1600 ms with −20 ms lead → 1580");
            d.Tick(2000f); T.Eq("1@1500 3@1580", h.Log, "peak once");
            d = D(); d.Submit("PU_CHAIN_PULSE", 1, 0f); for (int i = 1; i <= 7; i++) d.Submit("PU_CHAIN_LINK", 1 + i, i * 80f);
            T.Eq("2@0 1@80 1@160 1@240 1@320 1@400", h.Log, "chain: medium pulse + light tick per link, max 5");
            d.Submit("PU_CHAIN_PULSE", 20, 1000f); d.Submit("PU_CHAIN_LINK", 21, 1080f); T.True(h.Log.EndsWith("2@1000 1@1080"), "link cap resets per activation");
            d = D(); T.True(d.Submit("BOSS_TIDE_DEFEAT", 1, 0f) && d.Submit(AnimEvents.BossTideDefeatCollapse, 2, 3500f), "defeat + new collapse event routed");
            T.Eq("4@0 2@3500", h.Log, "boss defeat: strong then medium at +3500 ms");
            T.True(ShakeRouter.TryGet("BREAK_STEEL", out var s, out var f) && s.pos == 0.03f && s.rot == 0.3f && s.durationMs == 150 && f == ShakeFamily.Break, "BREAK_STEEL route 0.03 m / 0.3° 150 ms");
            d = D(); d.Submit("BREAK_STEEL", 1, 0f); T.Eq("1@0", h.Log, "BREAK_STEEL light haptic");
            var st = new ShakeStack(); var d2 = new ShakeDispatcher(st) { Haptics = h = new RecHapticsLog(), Settings = new MotionSettings(new AnimMemPrefs(), false) };
            T.True(d2.Submit("IMPACT_TERRAIN", 1, 0f) && st.Count == 1 && ShakeRouter.TryGet("IMPACT_TERRAIN", out s) && s.pos == 0.01f && s.durationMs == 80, "terrain still shakes 0.01 m 80 ms");
            T.True(d2.SubmitPhys("PHYS_IMPACT", ShakeRouter.MatTerrain, 2, 2, 100f), "phys terrain shakes"); T.Eq("", h.Log, "terrain: no haptic");
            T.True(!Src("Assets/_Project/Scripts/Feel/FeelCore.cs").Contains("flagged for Animator"), "terrain no longer provisional");
        });
        T.Add("TIDE_defeat_collapse_timer", () =>
        {
            int n = 0; Action<string, object> on = (e, _) => { if (e == AnimEvents.BossTideDefeatCollapse) n++; };
            AnimEventBus.Raised += on;
            try
            {
                var t = new TideDefeatCollapseTimer(); T.True(!t.Tick(5000f) && n == 0, "not armed → nothing");
                t.Start(); for (int i = 0; i < 208; i++) t.Tick(1000f / 60f); T.Eq(0, n, "not before 3500 ms (3466 ms)");
                t.Start(); T.True(!t.Tick(1000f / 60f) && n == 0, "re-Start while armed doesn't reset (frame 209)");
                t.Tick(1000f / 60f); T.Eq(1, n, "fires at 3500 ms (frame 210)");
                for (int i = 0; i < 300; i++) t.Tick(16f); t.Start(); t.Tick(4000f); T.Eq(1, n, "fires once per defeat");
                var c = new TideDefeatCollapseTimer(); c.Start(); c.Tick(3000f); c.Cancel(); c.Tick(1000f); T.Eq(1, n, "cancelled on exit/reset");
                c.Start(); c.Tick(3499f); T.Eq(1, n, "re-armed after reset, not yet"); c.Tick(1f); T.Eq(2, n, "new defeat after reset fires");
                var src = Src("Assets/_Project/Scripts/Boss/TidewardenController.cs");
                T.True(src.Contains("_defeatCollapse.Start()") && src.Contains("Time.unscaledDeltaTime") && src.Contains("_defeatCollapse.Cancel();") && src.Contains("TODO(anim)"), "wired: defeat start, unscaled tick, cancel on exit, TODO");
            }
            finally { AnimEventBus.Raised -= on; }
        });
        T.Add("TIDE_defeat_raised_once", () =>
        {
            int n = 0; Action<string, object> on = (e, _) => { if (e == AnimEvents.BossTideDefeat) n++; };
            AnimEventBus.Raised += on;
            try
            {
                TideDefeatEvent.Reset();
                T.True(TideDefeatEvent.RaiseOnce(), "Defeat() raises"); T.Eq(1, n, "raised");
                var sm = new TidewardenAnimStateMachine(); sm.CoreHit(); sm.CoreHit();
                T.Eq(1, n, "state machine CoreHit path skips: exactly once per defeat"); T.True(sm.State == TideAnimState.Defeat, "SM still enters Defeat");
                T.True(!TideDefeatEvent.RaiseOnce(), "second Defeat() call no-op"); T.Eq(1, n, "still once");
                TideDefeatEvent.Reset(); var sm2 = new TidewardenAnimStateMachine(); sm2.CoreHit(); sm2.CoreHit(); T.Eq(2, n, "after reset: SM-first path raises");
                TideDefeatEvent.RaiseOnce(); T.Eq(2, n, "then Defeat() skips");
                var src = Src("Assets/_Project/Scripts/Boss/TidewardenController.cs");
                T.True(src.Contains("Defeated = true;\n            Crashverse.Feel.TideDefeatEvent.RaiseOnce();") && src.Contains("TideDefeatEvent.Reset()") && src.Contains("moves to the rig defeat clip"), "Defeat() wired, reset on bind/exit, TODO");
            }
            finally { AnimEventBus.Raised -= on; TideDefeatEvent.Reset(); }
        });
        T.Add("HAPTICS_buckets_and_gate", () =>
        {
            T.Eq(1, HapticBuckets.For(ShakeDispatcher.Strength(ShakeTable.ImpactWood))); T.Eq(2, HapticBuckets.For(ShakeDispatcher.Strength(ShakeTable.Fire)));
            T.Eq(3, HapticBuckets.For(ShakeDispatcher.Strength(ShakeTable.Explosion))); T.Eq(4, HapticBuckets.For(ShakeDispatcher.Strength(ShakeTable.BossPhase)));
            T.Eq(0, HapticBuckets.For(0f), "no pulse at 0");
            for (int b = 1; b < HapticBuckets.Count; b++) T.True(HapticBuckets.Amplitude(b) < HapticBuckets.Amplitude(b + 1) && HapticBuckets.DurationMs(b) <= HapticBuckets.DurationMs(b + 1), "scales with strength");
            T.True(HapticBuckets.Amplitude(4) <= 255 && HapticBuckets.Amplitude(1) >= 1, "Android amplitude 1–255");
            var g = new HapticGate(); T.True(g.Allow(2, 0f), "first pulse"); T.True(!g.Allow(2, 20f), "40 ms rate limit"); T.True(g.Allow(4, 25f), "stronger cuts in"); T.True(g.Allow(1, 70f), "after 40 ms");
            var src = Src("Assets/_Project/Scripts/Feel/AndroidHaptics.cs");
            T.True(src.Contains("#if UNITY_ANDROID && !UNITY_EDITOR") && src.Contains("createOneShot") && src.Contains("_args[bucket]"), "device-only JNI, cached per-bucket effects + args");
            T.True(Src("Assets/Plugins/Android/AndroidManifest.xml").Contains("android.permission.VIBRATE"), "VIBRATE permission");
            T.True(Src("Assets/_Project/Scripts/UI/Screens.cs").Contains("\"Haptics: \""), "Settings Haptics row");
        });
        T.Add("SHAKE_settings_reduce_shake_vs_motion", () =>
        {
            var prefs = new AnimMemPrefs();
            var m = new MotionSettings(prefs, false);
            T.True(!m.ReduceShake && !m.ReduceMotion, "both OFF by default");
            T.Eq(1f, m.ShakeScale(QualityTier.Mid), "full shake"); T.Eq(0.7f, m.ShakeScale(QualityTier.Low), "low tier ×0.7");
            m.SetReduceShake(true); T.Eq(0f, m.ShakeScale(QualityTier.Mid), "Reduce shake → shake 0"); T.True(!m.ReduceMotion && m.SlowMoAllowed(false, false), "…and nothing else");
            m.SetReduceShake(false); m.SetReduceMotion(true);
            T.True(m.ReduceShakeEffective && m.ReduceShakeLocked && !m.ReduceShake, "Reduce motion implies + locks Reduce shake, stored value untouched");
            T.Eq(0f, m.ShakeScale(QualityTier.High), "Reduce motion forces shake off");
            m.SetReduceMotion(false); T.True(!m.ReduceShakeEffective && !m.ReduceShakeLocked, "turning Reduce motion off restores the stored Reduce shake");
            var auto = new MotionSettings(new AnimMemPrefs(), true);
            T.True(auto.ReduceMotion && auto.ReduceMotionIsAuto && auto.ShakeScale(QualityTier.Mid) == 0f, "auto-on when Android animator duration scale = 0");
            auto.SetReduceMotion(false); T.True(!auto.ReduceMotion && !auto.ReduceMotionIsAuto, "player can turn it off; explicit choice wins");
            var sc = Src("Assets/_Project/Scripts/UI/Screens.cs"); T.True(sc.Contains("_shakeBtn.interactable = !m.ReduceShakeLocked"), "settings row greyed while locked");
            var fd = Src("Assets/_Project/Scripts/Feel/FeelDirector.cs"); T.True(fd.Contains("animator_duration_scale"), "OS auto-on read on Android");
        });
        T.Add("ANIM_ease_endpoints_all_types", () =>
        {
            foreach (EaseType e in Enum.GetValues(typeof(EaseType)))
            {
                T.True(Near(0f, EaseFn.Evaluate(e, 0f)), $"{e}(0)=0"); T.True(Near(1f, EaseFn.Evaluate(e, 1f)), $"{e}(1)=1");
                T.True(Near(EaseFn.Evaluate(e, 1f), EaseFn.Evaluate(e, 2f)) && Near(EaseFn.Evaluate(e, 0f), EaseFn.Evaluate(e, -1f)), $"{e} clamps t");
            }
        });
        T.Add("ANIM_ease_shapes", () =>
        {
            T.True(EaseFn.Evaluate(EaseType.OutQuad, 0.5f) > 0.5f && EaseFn.Evaluate(EaseType.InQuad, 0.5f) < 0.5f, "out fast / in slow");
            T.True(Near(0.75f, EaseFn.Evaluate(EaseType.OutQuad, 0.5f)), "outQuad(0.5)=0.75");
            T.True(Near(0.125f, EaseFn.Evaluate(EaseType.InCubic, 0.5f)), "inCubic(0.5)=0.125");
            T.True(Near(0.5f, EaseFn.Evaluate(EaseType.InOutCubic, 0.5f)) && Near(0.5f, EaseFn.Evaluate(EaseType.InOutSine, 0.5f)), "in-out symmetric");
            float peak = 0f; for (int i = 0; i <= 100; i++) peak = Math.Max(peak, EaseFn.Evaluate(EaseType.OutBack, i / 100f));
            T.True(peak > 1.05f && peak < 1.15f, "out-back overshoots ~10%");
            float dip = 0f; for (int i = 0; i <= 100; i++) dip = Math.Min(dip, EaseFn.Evaluate(EaseType.InBack, i / 100f));
            T.True(dip < -0.05f, "in-back anticipates below 0");
            T.Eq(EaseType.OutQuad, EaseFn.Reduced(EaseType.OutBack, true), "Reduce Motion: out-back → out-quad");
            T.Eq(EaseType.OutBack, EaseFn.Reduced(EaseType.OutBack, false), "unchanged when off");
            for (int i = 0; i <= 100; i++) T.True(EaseFn.Evaluate(EaseFn.Reduced(EaseType.OutBack, true), i / 100f) <= 1f + 1e-6f, "reduced never overshoots");
        });
        T.Add("ANIM_float_tween_unscaled_ms", () =>
        {
            var tw = new FloatTween(); tw.Start(0f, 10f, 250f, EaseType.Linear);
            T.True(tw.Running && Near(0f, tw.Value), "starts at from");
            T.True(Near(4f, tw.Tick(0.1f)), "100/250 ms linear = 4");
            tw.Tick(1f); T.True(tw.Done && Near(10f, tw.Value), "finishes exactly at to");
            var z = new FloatTween(); z.Start(1f, 2f, 0f, EaseType.OutBack); T.True(z.Done && Near(2f, z.Value), "zero duration = instant");
            var b = new FloatTween(); b.Start(0f, 1.3f, 150f, EaseType.OutBack); float mx = 0; for (int i = 0; i < 20; i++) mx = Math.Max(mx, b.Tick(0.01f)); T.True(mx > 1.3f, "out-back tween overshoots its target");
        });
        T.Add("ANIM_event_names_current", () =>
        {
            T.Eq("ANIM_PROJ_SPLIT_POINT", AnimEvents.ProjSplitPoint); T.Eq("PU_SPLIT", AnimEvents.PuSplit);
            var root = System.IO.Path.Combine(UnityEngine.Resources.Root, "../../..");
            foreach (var f in System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Assets"), "*.cs", System.IO.SearchOption.AllDirectories))
            { var t = System.IO.File.ReadAllText(f); T.True(!t.Contains("ANIM_PROJ_APEX") && !t.Contains("PU_SPLIT_APEX") && !t.Contains("\"BOSS_TITAN_SURVIVE\""), "no retired event names in " + System.IO.Path.GetFileName(f)); }
        });
        T.Add("ANIM_spec_constants_muzzle_glass", () =>
        {
            T.Eq("22D3EE", Crashverse.Weapons.WreckerSpec.MuzzleFlash, "muzzle flash = Core cyan");
            T.Eq(0, AnimMaterial.Wood); T.Eq(5, AnimMaterial.Explosive, "0–5 unchanged"); T.Eq(6, AnimMaterial.Glass, "glass appended as 6");
        });
        T.Add("ANIM_aim_spring_critically_damped", () =>
        {
            float x = 0f, v = 0f; var w = AimSpring.OmegaMid; float prev = 0f; bool mono = true;
            for (int i = 0; i < 120; i++) { AimSpring.Step(ref x, ref v, 10f, w, 1f / 60f, 0f); if (x < prev - 1e-5f || x > 10f + 1e-4f) mono = false; prev = x; }
            T.True(mono, "no overshoot, monotonic (critically damped)"); T.True(Near(10f, x, 0.01f), "converges within 2 s");
            x = 0; v = 0; for (int i = 0; i < 7; i++) AimSpring.Step(ref x, ref v, 1f, w, 1f / 60f, 0f); // ≈ 117 ms
            T.True(x > 0.82f && x < 0.92f, $"ω=30: ~87% at ~120 ms (got {x:0.00})");
            T.True(Near(0.1581f, AimSpring.SettleTime95(30f), 0.002f), "95% at ~0.16 s for ω=30");
            x = 0; v = 0; AimSpring.Step(ref x, ref v, 60f, 1000f, 0.1f); T.True(x <= 24f + 1e-3f, "max 240°/s cap");
            float a = 0, va = 0, c = 0, vc = 0; AimSpring.Step(ref a, ref va, 5f, w, 0.1f, 0f); for (int i = 0; i < 10; i++) AimSpring.Step(ref c, ref vc, 5f, w, 0.01f, 0f);
            T.True(Near(a, c, 1e-3f), "exact solution: frame-rate independent (1×100 ms == 10×10 ms)");
            T.Eq(24f, AimSpring.Omega(QualityTier.Low), "low tier ω 24"); T.Eq(30f, AimSpring.Omega(QualityTier.High), "mid/high ω 30");
        });
        T.Add("ANIM_cannon_fire_cycle_curves", () =>
        {
            T.Eq(649f, WreckerAnimStateMachine.AnticipationMs + 33f + WreckerAnimStateMachine.RecoilMs + WreckerAnimStateMachine.RecoverMs, "66+33+200+350 = 649 ms");
            T.True(Near(0f, WreckerMotion.SlideBack(WreckerState.FireAnticipation, 0f, false)) && Near(0.03f, WreckerMotion.SlideBack(WreckerState.FireAnticipation, 66f, false)), "anticipation pulls 0.03 m");
            T.True(Near(0.03f, WreckerMotion.SlideBack(WreckerState.Recoil, 0f, false)), "recoil continues from the pull (no pop)");
            T.True(Near(0.25f, WreckerMotion.SlideBack(WreckerState.Recoil, 200f, false)), "recoil slide 0.25 m");
            T.True(WreckerMotion.SlideBack(WreckerState.Recoil, 50f, false) > 0.2f, "ease-out expo: most travel in the first frames");
            float minRec = 1f; for (int i = 0; i <= 350; i += 5) minRec = Math.Min(minRec, WreckerMotion.SlideBack(WreckerState.Recover, i, false));
            T.True(minRec >= -0.02f - 1e-5f, "recover overshoot ≤ 0.02 m");
            T.True(Near(0f, WreckerMotion.SlideBack(WreckerState.Recover, 350f, false)), "back to rest");
            float minRm = 1f; for (int i = 0; i <= 350; i += 5) minRm = Math.Min(minRm, WreckerMotion.SlideBack(WreckerState.Recover, i, true));
            T.True(minRm >= -1e-5f, "Reduce Motion: recoil kept, no overshoot");
            T.True(Near(4f, WreckerMotion.KickDeg(WreckerState.Recoil, 200f, false)) && Near(0f, WreckerMotion.KickDeg(WreckerState.Recover, 350f, false)), "pitch kick +4° then 0");
            T.True(Near(0f, WreckerMotion.KickDeg(WreckerState.Idle, 10f, false)), "no kick outside recoil");
        });
        T.Add("ANIM_cannon_idle_ready_slotswap", () =>
        {
            float mx = 0; for (int i = 0; i < 2400; i += 10) mx = Math.Max(mx, Math.Abs(WreckerMotion.IdleBreathDeg(i, false)));
            T.True(Near(0.6f, mx, 0.01f), "idle breath ±0.6°"); T.True(Near(WreckerMotion.IdleBreathDeg(100f, false), WreckerMotion.IdleBreathDeg(2500f, false)), "2400 ms loop");
            T.True(Near(0.3f, Math.Abs(WreckerMotion.IdleBreathDeg(600f, true)), 0.01f), "Reduce Motion ×0.5");
            T.True(Near(-1.5f, WreckerMotion.ReadyDipDegAt(0f, false)) && Near(0f, WreckerMotion.ReadyDipDegAt(300f, false)), "ready dip −1.5° → 0 in 300 ms");
            T.True(Near(1.4f, WreckerMotion.ReadyGlow(150f, 1f)) && Near(1f, WreckerMotion.ReadyGlow(0f, 1f)) && Near(1f, WreckerMotion.ReadyGlow(300f, 1f)), "glow 1.0→1.4→1.0");
            T.True(Near(1.16f, WreckerMotion.ReadyGlow(150f, 0.4f)), "Reduce Motion glow ×0.4");
            T.True(Near(1f, WreckerMotion.SlotSwapScale(0f, false)) && Near(0f, WreckerMotion.SlotSwapScale(150f, false)), "old part 1→0 by 150 ms");
            float pk = 0, mn = 1; for (int i = 0; i <= 450; i += 2) { float sc = WreckerMotion.SlotSwapScale(i, false); pk = Math.Max(pk, sc); mn = Math.Min(mn, sc); }
            T.True(Near(1.15f, pk, 0.01f), "new part peaks 1.15"); T.True(mn >= 0f, "never negative");
            T.True(Near(1f, WreckerMotion.SlotSwapScale(450f, false)), "settles at 1 by 450 ms");
            float pkRm = 0; for (int i = 0; i <= 450; i += 2) pkRm = Math.Max(pkRm, WreckerMotion.SlotSwapScale(i, true)); T.True(pkRm <= 1f + 1e-5f, "Reduce Motion: no overshoot");
            T.True(!WreckerMotion.SlotSwapPastMid(149f) && WreckerMotion.SlotSwapPastMid(150f), "swap the look at 150 ms");
        });
        T.Add("ANIM_slot_in_event_at_150ms", () =>
        {
            AnimEventBus.ResetForTests(); var got = new List<string>(); AnimEventBus.Raised += (n, _) => got.Add(n);
            var sm = new WreckerAnimStateMachine(); sm.SlotSwap();
            T.True(got.Contains(AnimEvents.WreckerSlotOut) && !got.Contains(AnimEvents.WreckerSlotIn), "SLOT_OUT at 0");
            sm.Tick(0.149f); T.True(!got.Contains(AnimEvents.WreckerSlotIn), "not before 150 ms"); sm.Tick(0.002f); T.True(got.Contains(AnimEvents.WreckerSlotIn), "SLOT_IN at 9 f / 150 ms");
            sm.Tick(0.4f); int n = 0; foreach (var e in got) if (e == AnimEvents.WreckerSlotIn) n++; T.Eq(1, n, "raised once"); T.Eq(WreckerState.Idle, sm.State, "back to idle after 450 ms");
            AnimEventBus.ResetForTests(); var sm2 = new WreckerAnimStateMachine(); int c = 0; AnimEventBus.Raised += (e, _) => { if (e == AnimEvents.WreckerSlotIn) c++; }; sm2.SlotSwap(); sm2.Tick(1f); T.Eq(1, c, "big dt still raises SLOT_IN once");
            AnimEventBus.ResetForTests();
        });
        T.Add("ANIM_fire_launch_delay_wired_once", () =>
        {
            var fc = Src("Assets/_Project/Scripts/Weapons/FireController.cs"); var gs = Src("Assets/_Project/Scripts/Core/GameSession.cs");
            T.True(fc.Contains("launchDelayMs = Crashverse.Feel.WreckerAnimStateMachine.AnticipationMs"), "66 ms delay from the anim constant (single source)");
            T.True(fc.Contains("Projectile.Alive.Count + _pendingCount"), "pending launch keeps the reload gate closed");
            T.True(gs.Contains("fire.CancelPending()"), "Retry drops a pending launch");
            T.True(gs.Contains("Inputs.Record(Tick, \"fire\", yaw, elev)") && !gs.Contains("Inputs.Record(Tick, \"fire\", aim.Yaw, aim.Elevation)"), "input tick recorded at the real launch with tap-frozen aim");
            var ac = Src("Assets/_Project/Scripts/Weapons/AimController.cs");
            T.True(ac.Contains("Time.unscaledDeltaTime") && ac.Contains("AimSpring.Step"), "aim spring on unscaled time");
        });
    }
}

sealed class GatedRec : Crashverse.Feel.IHaptics { Crashverse.Feel.HapticGate _g; public int Fired; public void Pulse(int bucket, float nowMs) { if (_g.Allow(bucket, nowMs)) Fired++; } }
sealed class RecHapticsLog : Crashverse.Feel.IHaptics { public string Log = ""; public void Pulse(int b, float t) { Log += (Log.Length > 0 ? " " : "") + b + "@" + t; } }
sealed class RecHaptics : Crashverse.Feel.IHaptics { public int Calls, Last; public void Pulse(int bucket, float nowMs) { Calls++; Last = bucket; } }
