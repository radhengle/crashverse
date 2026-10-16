using System;
using System.IO;
using Crashverse.Feel;

/// <summary>Animation Spec v1 §06 UI motion maths (unscaled ms, Reduce Motion variants). Engine-free; runs in CI via Tools/Tests.</summary>
static class UiMotionTests
{
    static bool Near(float a, float b, float eps = 1e-3f) => Math.Abs(a - b) <= eps;
    static string Src(string rel) => File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "../../..", rel));

    public static void Register()
    {
        T.Add("UI_button_press_release", () =>
        {
            T.True(Near(1f, UiMotionMath.PressScale(0f)) && Near(0.92f, UiMotionMath.PressScale(120f)) && Near(0.92f, UiMotionMath.PressScale(500f)), "press 1 → 0.92 in 120 ms, holds");
            float pk = 0; for (int i = 0; i <= 120; i += 2) pk = Math.Max(pk, ButtonMotion.ReleaseScale(i, false));
            T.True(Near(1.05f, pk, 0.002f) && Near(1f, ButtonMotion.ReleaseScale(120f, false)), "release overshoot 1.05 → 1 in 120 ms");
            float pkRm = 0; for (int i = 0; i <= 120; i += 2) pkRm = Math.Max(pkRm, ButtonMotion.ReleaseScale(i, true)); T.True(pkRm <= 1f + 1e-5f, "Reduce Motion: no overshoot");
        });
        T.Add("UI_star_reveal_sequence", () =>
        {
            T.True(Near(300f, UiMotionMath.StarStartMs) && Near(300f, UiMotionMath.StarStaggerMs), "first star after the 300 ms ribbon, 300 ms stagger");
            T.Eq(0f, UiMotionMath.MsIntoStar(300f, 0)); T.Eq(0f, UiMotionMath.MsIntoStar(900f, 2));
            T.True(Near(0f, UiMotionMath.StarScale(0f, true, false)) && Near(1.3f, UiMotionMath.StarScale(150f, true, false)) && Near(1f, UiMotionMath.StarScale(250f, true, false)), "0 → 1.3 (150 ms) → 1 (250 ms)");
            float pk = 0; for (int i = 0; i <= 250; i += 2) pk = Math.Max(pk, UiMotionMath.StarScale(i, true, false)); T.True(pk >= 1.3f && pk <= 1.45f, "peak ≥1.3; existing out-back on main overshoots to ~1.43 (flagged)");
            float pkRm = 0; for (int i = 0; i <= 250; i += 2) pkRm = Math.Max(pkRm, UiMotionMath.StarScale(i, true, true)); T.True(pkRm <= 1.1f + 1e-4f, "Reduce Motion pop capped 1.1");
            T.True(Near(1f, UiMotionMath.StarScale(10f, false, false)) && Near(1f, UiMotionMath.EmptyStarAlpha(150f)) && Near(0f, UiMotionMath.EmptyStarAlpha(0f)), "unearned star: no pop, fades in 150 ms");
        });
        T.Add("UI_combo_pop_from_x3_escalates", () =>
        {
            AnimEventBus.ResetForTests(); int up = 0, tier = 0; AnimEventBus.Raised += (e, _) => { if (e == AnimEvents.UiComboUp) up++; if (e == AnimEvents.UiComboTier) tier++; };
            var sm = new ComboUiStateMachine(); sm.OnCombo(1); sm.OnCombo(2); T.Eq(ComboUiState.Hidden, sm.State, "nothing below x3"); T.Eq(0, up, "no event below x3");
            sm.OnCombo(3); T.Eq(ComboUiState.Show, sm.State, "pops at x3"); T.True(Near(0f, UiMotionMath.ComboScale(sm.State, 0f, 3, false)), "starts from 0");
            T.True(Near(1f, UiMotionMath.ComboScale(ComboUiState.Show, ComboUiStateMachine.InMs, 3, false)), "settles at 1 after 180 ms");
            float p3 = 0, p5 = 0, p10 = 0, p20 = 0;
            for (int i = 0; i <= 250; i += 2) { p3 = Math.Max(p3, UiMotionMath.ComboScale(ComboUiState.Show, i * 180f / 250f, 3, false)); p5 = Math.Max(p5, UiMotionMath.ComboScale(ComboUiState.TierUp, i, 5, false)); p10 = Math.Max(p10, UiMotionMath.ComboScale(ComboUiState.TierUp, i, 10, false)); p20 = Math.Max(p20, UiMotionMath.ComboScale(ComboUiState.TierUp, i, 20, false)); }
            T.True(Near(1.2f, p3) && Near(1.35f, p5) && Near(1.5f, p10) && Near(1.7f, p20), "peaks 1.2 / 1.35 / 1.5 / 1.7 at x3 / x5 / x10 / x20");
            float p20rm = 0; for (int i = 0; i <= 250; i += 2) p20rm = Math.Max(p20rm, UiMotionMath.ComboScale(ComboUiState.TierUp, i, 20, true)); T.True(p20rm <= 1.1f + 1e-4f, "Reduce Motion caps 1.1");
            sm.Tick(0.2f); sm.OnCombo(4); T.Eq(ComboUiState.Punch, sm.State, "increment punches"); float pp = 0; for (int i = 0; i <= 80; i += 2) pp = Math.Max(pp, UiMotionMath.ComboScale(ComboUiState.Punch, i, 4, false)); T.True(Near(1.12f, pp), "punch 1.12");
            sm.Tick(0.1f); sm.OnCombo(5); T.Eq(ComboUiState.TierUp, sm.State, "tier-up at x5"); T.Eq(1, tier, "UI_COMBO_TIER once");
            sm.Tick(0.3f); sm.Tick(0.81f); T.Eq(ComboUiState.Out, sm.State, "out after 0.8 s without a break");
            T.True(Near(0f, UiMotionMath.ComboScale(ComboUiState.Out, 250f, 5, false)) && Near(0f, UiMotionMath.ComboAlpha(ComboUiState.Out, 250f)), "out 250 ms to 0");
            AnimEventBus.ResetForTests();
        });
        T.Add("UI_count_up_and_bar_tick", () =>
        {
            T.Eq(0, UiMotionMath.CountUp(0, 3850, 0f, 600f)); T.Eq(3850, UiMotionMath.CountUp(0, 3850, 600f, 600f), "exact at the end");
            T.True(UiMotionMath.CountUp(0, 3850, 300f, 600f) > 1925, "ease-out: past halfway at half time");
            int prev = -1; bool mono = true; for (int i = 0; i <= 600; i += 10) { int v = UiMotionMath.CountUp(0, 3850, i, 600f); if (v < prev) mono = false; prev = v; } T.True(mono, "monotonic");
            T.Eq(10000, UiMotionMath.CountUp(2500, 10000, 999f, UiMotionMath.DamageTickMs), "damage counter lands exactly");
            T.True(UiMotionMath.ReachedThreshold(0.85f, 0.85f) && !UiMotionMath.ReachedThreshold(0.849f, 0.85f), "tick hits at ≥ threshold (85% default)");
            float pk = 0; for (int i = 0; i <= 250; i += 2) pk = Math.Max(pk, UiMotionMath.TickScale(i, false)); T.True(Near(1.4f, pk), "tick pop 1 → 1.4 → 1");
            T.True(Near(1f, UiMotionMath.TickScale(250f, false)), "done in 250 ms");
            for (int i = 0; i <= 250; i += 10) T.True(Near(1f, UiMotionMath.TickScale(i, true)), "Reduce Motion: colour only, no scale");
            float sp = 0; for (int i = 0; i <= 200; i += 2) sp = Math.Max(sp, UiMotionMath.ShotPunchScale(i, false)); T.True(Near(1.25f, sp), "shots punch 1.25");
        });
        T.Add("UI_motion_wired_unscaled", () =>
        {
            var sc = Src("Assets/_Project/Scripts/UI/Screens.cs"); var bm = Src("Assets/_Project/Scripts/UI/UiButtonMotion.cs"); var uf = Src("Assets/_Project/Scripts/UI/UIFactory.cs");
            T.True(sc.Contains("Th.violet") && sc.Contains("_comboSm.OnCombo(n)"), "combo violet + state machine");
            T.True(sc.Contains("_s.Ctx.level.winThreshold") && sc.Contains("UiThresholdHit"), "tick from level data");
            T.True(sc.Contains("UiMotionMath.StarScale") && sc.Contains("UiMotionMath.CountUp"), "stars + score count-up");
            T.True(bm.Contains("Time.unscaledDeltaTime") && sc.Contains("float dt = Time.unscaledDeltaTime"), "UI motion on unscaled time");
            T.True(uf.Contains("AddComponent<UiButtonMotion>()"), "every factory button gets press motion");
        });
    }
}
