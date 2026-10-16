namespace UnityEngine
{
    // Minimal ParticleSystem surface used by the dev-only PerfDummyPowerUpLane (compile check only).
    public class ParticleSystem : Component
    {
        public struct MinMaxCurve { public float constant; public static implicit operator MinMaxCurve(float v) => new MinMaxCurve { constant = v }; }
        public struct MainModule { public bool loop, playOnAwake; public int maxParticles; public MinMaxCurve startLifetime, startSpeed, startSize; }
        public struct EmissionModule { public bool enabled; }
        public MainModule main => default;
        public EmissionModule emission => default;
        public int particleCount;
        public void Emit(int count) { particleCount += count; }
    }
    public class ParticleSystemRenderer : Renderer { public Material sharedMaterial; }
}
