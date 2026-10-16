namespace Crashverse.Sim
{
    /// <summary>
    /// Single route from PHYS_* ImpactEvents to camera shake. Physics never moves the camera: it hands
    /// (physEvent, material, tier) to Animator's CameraShake.AddPhys(string physEvent, int material, int tier) from PR #29.
    /// TODO(#29): once #29 (anim/camera-shake) is on our base, bind the sink at boot:
    ///     PhysShakeHook.Sink = Crashverse.Feel.CameraShake.AddPhys;
    /// (or define CRASHVERSE_CAMERASHAKE, which binds it in ImpactEventEmitter). Until then Sink is null and shake is a no-op,
    /// so this compiles without #29. Engine-free; presentation only, never read by the sim.
    /// </summary>
    public static class PhysShakeHook
    {
        /// <summary>Matches CameraShake.AddPhys(string physEvent, int material, int tier) → bool (true = shake accepted).</summary>
        public static System.Func<string, int, int, bool> Sink;

        /// <summary>Arguments as documented in #29: (ImpactEvents.NameOf(e.kind), (int)e.materialTypeB, (int)e.tier).</summary>
        public static bool Submit(string physEvent, in ImpactEvent e)
        {
            var s = Sink;
            return s != null && s(physEvent, (int)e.materialTypeB, (int)e.tier);
        }
    }
}
