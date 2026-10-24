namespace UnityEngine
{
    // Compile-check stub only (linear 2-key approximation).
    public class AnimationCurve
    {
        float _t0, _v0, _t1, _v1;
        public static AnimationCurve Linear(float t0, float v0, float t1, float v1) => new AnimationCurve { _t0 = t0, _v0 = v0, _t1 = t1, _v1 = v1 };
        public static AnimationCurve EaseInOut(float t0, float v0, float t1, float v1) => Linear(t0, v0, t1, v1);
        public float Evaluate(float t) { if (_t1 == _t0) return _v0; float a = (t - _t0) / (_t1 - _t0); a = a < 0 ? 0 : a > 1 ? 1 : a; return _v0 + (_v1 - _v0) * a; }
    }
}
