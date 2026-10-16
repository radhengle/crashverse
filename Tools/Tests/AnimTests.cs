using System;
using System.Collections.Generic;
using System.IO;
using Crashverse.Feel;

/// <summary>Animation Spec v1 (animation/01–07): easing/tween maths, aim spring, cannon motion curves. Engine-free, runs in CI (dotnet run --project Tools/Tests).</summary>
static class AnimTests
{
    static bool Near(float a, float b, float eps = 1e-3f) => Math.Abs(a - b) <= eps;
    static string Src(string rel) => File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "../../..", rel));

    public static void Register()
    {
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
        T.Add("ANIM_aim_spring_critically_damped", () =>
        {
            float x = 0f, v = 0f; var w = AimSpring.OmegaMid; float prev = 0f; bool mono = true;
            for (int i = 0; i < 120; i++) { AimSpring.Step(ref x, ref v, 10f, w, 1f / 60f, 0f); if (x < prev - 1e-5f || x > 10f + 1e-4f) mono = false; prev = x; }
            T.True(mono, "no overshoot, monotonic (critically damped)"); T.True(Near(10f, x, 0.01f), "converges within 2 s");
            x = 0; v = 0; for (int i = 0; i < 7; i++) AimSpring.Step(ref x, ref v, 1f, w, 1f / 60f, 0f); // ≈ 117 ms
            T.True(x > 0.55f && x < 0.7f, $"ω=18: ~63% at ~120 ms (got {x:0.00})");
            T.True(Near(0.2636f, AimSpring.SettleTime95(18f), 0.002f), "95% at ~0.26 s for ω=18");
            x = 0; v = 0; AimSpring.Step(ref x, ref v, 60f, 1000f, 0.1f); T.True(x <= 24f + 1e-3f, "max 240°/s cap");
            float a = 0, va = 0, c = 0, vc = 0; AimSpring.Step(ref a, ref va, 5f, w, 0.1f, 0f); for (int i = 0; i < 10; i++) AimSpring.Step(ref c, ref vc, 5f, w, 0.01f, 0f);
            T.True(Near(a, c, 1e-3f), "exact solution: frame-rate independent (1×100 ms == 10×10 ms)");
            T.Eq(14f, AimSpring.Omega(QualityTier.Low), "low tier ω 14"); T.Eq(18f, AimSpring.Omega(QualityTier.High), "mid/high ω 18");
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
