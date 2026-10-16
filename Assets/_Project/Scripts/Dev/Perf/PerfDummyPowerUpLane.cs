#if CRASHVERSE_DEV || DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;

namespace Crashverse.DevTools.Perf
{
    /// <summary>
    /// DEV-ONLY STUB: fills the reserved power-up particle lane (80, Low 40; VfxRules.PowerUpLaneBudget on vfx/particles-damage)
    /// with long-lived dummy particles so the L10 perf run exercises the full 230 budget before power-up FX exist.
    /// vfx/particles-damage (3c56e26) has no power-up lane API yet, so this is a standalone ParticleSystem and VfxDirector
    /// does NOT count it: VfxPerfProbe must add PerfDummyPowerUpLane.LiveParticles to its particle total (see PERF_L10_STAGING.md).
    /// Replace with VfxDirector's power-up lane once VFX adds it. Whole file compiled out of release players.
    /// </summary>
    public class PerfDummyPowerUpLane : MonoBehaviour
    {
        public const int Lane = 80, LaneLow = 40;
        static PerfDummyPowerUpLane _instance;
        ParticleSystem _ps; int _target;
        /// <summary>Live dummy particles (0 when the toggle is off). For VfxPerfProbe's particle total.</summary>
        public static int LiveParticles => _instance != null && _instance._ps != null ? _instance._ps.particleCount : 0;

        public void Bind(bool lowTier)
        {
            _instance = this;
            _target = lowTier ? LaneLow : Lane;
            _ps = gameObject.AddComponent<ParticleSystem>();
            var main = _ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = _target;
            main.startLifetime = 3600f; main.startSpeed = 0f; main.startSize = 0.6f;
            var em = _ps.emission; em.enabled = false;
            var r = GetComponent<ParticleSystemRenderer>();
            var sh = Shader.Find("Sprites/Default"); // always-included transparent shader: realistic alpha overdraw
            if (r != null && sh != null) r.sharedMaterial = new Material(sh);
            transform.position = new Vector3(10f, 4f, 0f); // over the structure (GameplayDirector structureBase)
        }

        void Update()
        {
            if (_ps == null) return;
            int missing = _target - _ps.particleCount;
            if (missing > 0) _ps.Emit(missing);
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
#endif
