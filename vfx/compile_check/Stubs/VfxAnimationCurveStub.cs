// Only compiled when the shared stubs don't provide AnimationCurve yet (PR #18 adds Tools/CompileCheck/Stubs/AnimationCurveStub.cs).
namespace UnityEngine
{
    public class AnimationCurve { public static AnimationCurve Linear(float t0, float v0, float t1, float v1) => new AnimationCurve(); }
}
