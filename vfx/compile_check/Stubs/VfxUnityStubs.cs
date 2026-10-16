// VFX-only compile stubs (not Unity). Covers the ParticleSystem / Material / Gradient API the VFX runtime uses, on top of
// Tools/CompileCheck/Stubs (not edited by VFX). Signatures mirror Unity 2022.3; behaviour is empty.
namespace UnityEngine
{
    // Shader / Material come from Tools/CompileCheck/Stubs/UnityUI.cs; the extra Material calls VFX makes are extension stubs here
    // (in Unity the real instance methods win, so these are never bound outside the compile check).
    public class Texture : Object { }
    public class Texture2D : Texture { }
    public static class VfxStubMaterialExtensions
    {
        public static void SetTexture(this Material m, string n, Texture t) { }
        public static void SetFloat(this Material m, string n, float v) { }
    }
    public enum GradientMode { Blend = 0, Fixed = 1 }
    public struct GradientColorKey { public Color color; public float time; public GradientColorKey(Color c, float t) { color = c; time = t; } }
    public struct GradientAlphaKey { public float alpha, time; public GradientAlphaKey(float a, float t) { alpha = a; time = t; } }
    public class Gradient { public GradientMode mode; public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a) { } }
    public enum ParticleSystemShapeType { Sphere = 0, Hemisphere = 2, Cone = 4, Box = 5, Circle = 10 }
    public enum ParticleSystemRenderMode { Billboard = 0, Stretch = 1, HorizontalBillboard = 2, VerticalBillboard = 3, Mesh = 4 }
    public enum ParticleSystemSimulationSpace { Local = 0, World = 1 }
    public enum ParticleSystemAnimationType { WholeSheet = 0, SingleRow = 1 }
    public enum ParticleSystemAnimationRowMode { Custom = 0, Random = 1 }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear = 0, StopEmitting = 1 }
    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode; public float velocityScale, lengthScale, maxParticleSize, sortingFudge; public Material sharedMaterial;
    }
    public class ParticleSystem : Component
    {
        public struct MinMaxCurve
        {
            public MinMaxCurve(float c) { } public MinMaxCurve(float a, float b) { } public MinMaxCurve(float m, AnimationCurve c) { }
            public static implicit operator MinMaxCurve(float c) => new MinMaxCurve(c);
        }
        public struct MinMaxGradient { public Color color; public MinMaxGradient(Color c) { color = c; } public MinMaxGradient(Gradient g) { color = default; } }
        public struct Burst { public Burst(float t, short n) { } }
        public class MainModule
        {
            public float duration, startDelayMultiplier, gravityModifierMultiplier; public bool loop, playOnAwake, useUnscaledTime; public int maxParticles;
            public MinMaxCurve startDelay, startLifetime, startSpeed, startSize, startRotation, gravityModifier; public MinMaxGradient startColor;
            public ParticleSystemSimulationSpace simulationSpace;
        }
        public class EmissionModule { public bool enabled; public MinMaxCurve rateOverTime; public void SetBursts(Burst[] b) { } }
        public class ShapeModule { public bool enabled; public ParticleSystemShapeType shapeType; public float radius, angle; }
        public class ColorOverLifetimeModule { public bool enabled; public MinMaxGradient color; }
        public class SizeOverLifetimeModule { public bool enabled; public MinMaxCurve size; }
        public class LimitVelocityOverLifetimeModule { public bool enabled, multiplyDragByParticleSize; public MinMaxCurve drag; }
        public class TextureSheetAnimationModule
        {
            public bool enabled; public int numTilesX, numTilesY, rowIndex; public ParticleSystemAnimationType animation;
            public ParticleSystemAnimationRowMode rowMode; public MinMaxCurve frameOverTime, startFrame;
        }
        public MainModule main { get; } = new MainModule();
        public EmissionModule emission { get; } = new EmissionModule();
        public ShapeModule shape { get; } = new ShapeModule();
        public ColorOverLifetimeModule colorOverLifetime { get; } = new ColorOverLifetimeModule();
        public SizeOverLifetimeModule sizeOverLifetime { get; } = new SizeOverLifetimeModule();
        public LimitVelocityOverLifetimeModule limitVelocityOverLifetime { get; } = new LimitVelocityOverLifetimeModule();
        public TextureSheetAnimationModule textureSheetAnimation { get; } = new TextureSheetAnimationModule();
        public void Play(bool withChildren) { } public void Stop(bool withChildren, ParticleSystemStopBehavior b) { }
    }
}
