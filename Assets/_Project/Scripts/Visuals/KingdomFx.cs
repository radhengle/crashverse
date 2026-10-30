using System.Collections.Generic;
using Crashverse.Destruction;
using Crashverse.VFX;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    /// <summary>
    /// Season 1 destruction dressing (concept art: flying planks, sparks, fire and smoke). Six pooled world-space particle systems on the
    /// shipped Crashverse/VfxParticle sheets: flying plank chunks (mesh particles, no rigidbodies so the sim never sees them), sparks,
    /// fire, smoke plumes, a fireball and a ground shockwave ring. Pieces breaking emit bursts; some wood breaks keep burning a few
    /// seconds. The hidden core uses Core(): fireball + shockwave + sparks + a plank storm + a smoke column. Presentation only.
    /// Budget: ≤ 600 live particles, 6 draw calls, Low tier halves every count.
    /// </summary>
    public class KingdomFx : MonoBehaviour
    {
        public static KingdomFx Instance { get; private set; }
        ParticleSystem _planks, _sparks, _fire, _smoke, _ball, _ring;
        struct Burn { public Vector3 at; public float until, next; }
        readonly List<Burn> _burns = new List<Burn>();
        float _scale = 1f; System.Random _rng = new System.Random(7);

        public static KingdomFx Create(Transform parent, bool low)
        {
            var a = VfxAssetSet.Load(); if (a == null || a.assets == null || a.assets.particleShader == null) return null;
            var go = new GameObject("KingdomFx"); go.transform.SetParent(parent, false);
            var fx = go.AddComponent<KingdomFx>(); fx._scale = low ? 0.5f : 1f; fx.Build(a.assets);
            Instance = fx; return fx;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        Material Mat(VfxAssets a, Texture2D tex, bool add, Color tint, float intensity)
        {
            var m = new Material(a.particleShader) { name = "MK_Fx_" + (tex != null ? tex.name : "none") };
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint); m.SetFloat("_Intensity", intensity);
            m.SetFloat("_SrcBlend", add ? 1f : 5f); m.SetFloat("_DstBlend", add ? 1f : 10f); m.SetFloat("_AlphaClip", add ? 0.02f : 0.06f);
            return m;
        }

        ParticleSystem Sys(string name, Material m, int max, float gravity, ParticleSystemRenderMode mode, bool flipbook)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = 1f; main.maxParticles = Mathf.Max(8, (int)(max * _scale));
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = gravity; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
            if (flipbook) { var tsa = ps.textureSheetAnimation; tsa.enabled = true; tsa.numTilesX = 4; tsa.numTilesY = 4; }
            var r = go.GetComponent<ParticleSystemRenderer>(); r.renderMode = mode; r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        void Build(VfxAssets a)
        {
            // flying plank chunks: real planks texture on stretched cubes
            _planks = Sys("Planks", KingdomMaterials.Planks, 160, 1.4f, ParticleSystemRenderMode.Mesh, false);
            var pr = _planks.GetComponent<ParticleSystemRenderer>(); var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pr.mesh = cube.GetComponent<MeshFilter>().sharedMesh; Destroy(cube);
            pr.shadowCastingMode = ShadowCastingMode.On;
            var pm = _planks.main; pm.startSize3D = true; pm.startRotation3D = true;
            var rot = _planks.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f); rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f); rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            _sparks = Sys("Sparks", Mat(a, a.sparkStreak, true, new Color(1f, 0.75f, 0.35f), 2.5f), 220, 0.9f, ParticleSystemRenderMode.Stretch, false);
            var sr = _sparks.GetComponent<ParticleSystemRenderer>(); sr.velocityScale = 0.06f; sr.lengthScale = 1.5f;
            _fire = Sys("Fire", Mat(a, a.puffFlipbook, true, new Color(1f, 0.55f, 0.15f), 2.2f), 140, -0.15f, ParticleSystemRenderMode.Billboard, true);
            var fc = _fire.colorOverLifetime; fc.enabled = true; fc.color = Grad(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.25f, 0.05f), 1f, 0f);
            var fs = _fire.sizeOverLifetime; fs.enabled = true; fs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));
            _smoke = Sys("Smoke", Mat(a, a.puffFlipbook, false, new Color(0.30f, 0.29f, 0.28f, 0.85f), 1f), 160, -0.05f, ParticleSystemRenderMode.Billboard, true);
            var sc = _smoke.colorOverLifetime; sc.enabled = true; sc.color = Grad(new Color(0.35f, 0.33f, 0.31f), new Color(0.55f, 0.55f, 0.56f), 0.8f, 0f);
            var ss = _smoke.sizeOverLifetime; ss.enabled = true; ss.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.6f));
            _ball = Sys("Fireball", Mat(a, a.puffFlipbook, true, new Color(1f, 0.6f, 0.2f), 3.2f), 80, -0.1f, ParticleSystemRenderMode.Billboard, true);
            var bc = _ball.colorOverLifetime; bc.enabled = true; bc.color = Grad(new Color(1f, 0.95f, 0.7f), new Color(0.9f, 0.2f, 0.05f), 1f, 0f);
            var bs = _ball.sizeOverLifetime; bs.enabled = true; bs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.6f));
            _ring = Sys("Shockwave", Mat(a, a.ring, true, new Color(1f, 0.85f, 0.6f), 2f), 4, 0f, ParticleSystemRenderMode.HorizontalBillboard, false);
            var rc = _ring.colorOverLifetime; rc.enabled = true; rc.color = Grad(Color.white, new Color(1f, 0.6f, 0.3f), 1f, 0f);
            var rs = _ring.sizeOverLifetime; rs.enabled = true; rs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f));
            foreach (var p in new[] { _planks, _sparks, _fire, _smoke, _ball, _ring }) p.Play();
        }

        static Gradient Grad(Color a, Color b, float a0, float a1)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) }, new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(a0, 0.6f), new GradientAlphaKey(a1, 1f) });
            return g;
        }

        float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        Vector3 Dir(float up) { var d = new Vector3(R(-1f, 1f), R(0.2f, 1f) * up, R(-1f, 1f)); return d.normalized; }
        int N(int n) => Mathf.Max(1, Mathf.RoundToInt(n * _scale));

        void Emit(ParticleSystem ps, Vector3 at, int n, float spread, float speedMin, float speedMax, float sizeMin, float sizeMax, float lifeMin, float lifeMax, float up = 1f)
        {
            if (ps == null) return;
            var e = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < n; i++)
            {
                e.position = at + new Vector3(R(-spread, spread), R(-spread, spread) * 0.6f, R(-spread, spread));
                e.velocity = Dir(up) * R(speedMin, speedMax);
                e.startSize = R(sizeMin, sizeMax); e.startLifetime = R(lifeMin, lifeMax); e.rotation = R(0f, 360f);
                ps.Emit(e, 1);
            }
        }

        void EmitPlanks(Vector3 at, int n, float speed)
        {
            var e = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < n; i++)
            {
                e.position = at + new Vector3(R(-0.3f, 0.3f), R(-0.2f, 0.3f), R(-0.3f, 0.3f));
                e.velocity = Dir(1.2f) * R(speed * 0.4f, speed);
                e.startSize3D = new Vector3(R(0.35f, 0.8f), R(0.05f, 0.09f), R(0.1f, 0.2f));
                e.rotation3D = new Vector3(R(0f, 360f), R(0f, 360f), R(0f, 360f));
                e.startLifetime = R(1.6f, 2.6f); e.startColor = Color.Lerp(new Color(0.85f, 0.75f, 0.6f), Color.white, R(0f, 1f));
                _planks.Emit(e, 1);
            }
        }

        /// <summary>A structure piece broke at <paramref name="at"/> (materialKind: 0 wood, 1 brick, 2 steel).</summary>
        public void Break(Vector3 at, int materialKind, float size)
        {
            float k = Mathf.Clamp(size, 0.6f, 2.5f);
            if (materialKind == 0)
            {
                EmitPlanks(at, N((int)(6 * k)), 7f);
                if (_rng.NextDouble() < 0.35 && _burns.Count < 6) _burns.Add(new Burn { at = at, until = Time.time + R(2.5f, 4.5f) });
            }
            else Emit(_sparks, at, N(10), 0.2f, 4f, 9f, 0.06f, 0.12f, 0.3f, 0.7f);
            Emit(_smoke, at, N(3), 0.3f, 0.3f, 0.9f, 0.9f * k, 1.6f * k, 2f, 3.2f, 1.5f);
        }

        /// <summary>The hidden core: a huge fireball, a ground shockwave, sparks, a storm of planks and a smoke column.</summary>
        public void Core(Vector3 at)
        {
            Emit(_ball, at, N(36), 0.9f, 1.5f, 5f, 2.2f, 4.2f, 0.55f, 1.0f);
            Emit(_sparks, at, N(80), 0.4f, 8f, 18f, 0.08f, 0.16f, 0.5f, 1.2f);
            EmitPlanks(at, N(40), 14f);
            Emit(_smoke, at, N(18), 1.2f, 0.8f, 2.4f, 2.5f, 4.5f, 3f, 5f, 2.5f);
            var e = new ParticleSystem.EmitParams { applyShapeToPosition = false, position = new Vector3(at.x, Mathf.Max(0.15f, at.y - 1.2f), at.z), velocity = Vector3.zero, startSize = 16f, startLifetime = 0.55f };
            _ring.Emit(e, 1);
            for (int i = 0; i < 3 && _burns.Count < 8; i++) _burns.Add(new Burn { at = at + new Vector3(R(-1.5f, 1.5f), R(-1f, 0.5f), R(-1.5f, 1.5f)), until = Time.time + R(3f, 5f) });
        }

        void Update()
        {
            float t = Time.time;
            for (int i = _burns.Count - 1; i >= 0; i--)
            {
                var b = _burns[i];
                if (t > b.until) { _burns.RemoveAt(i); continue; }
                if (t < b.next) continue;
                b.next = t + 0.08f; _burns[i] = b;
                Emit(_fire, b.at, 1, 0.25f, 0.6f, 1.6f, 0.5f, 1.0f, 0.5f, 0.9f, 2.5f);
                if (_rng.NextDouble() < 0.35) Emit(_smoke, b.at + Vector3.up * 0.6f, 1, 0.2f, 0.6f, 1.2f, 0.8f, 1.4f, 2.5f, 3.5f, 3f);
            }
        }

        /// <summary>Wire every structure piece's Broken event to Break().</summary>
        public void Watch(IReadOnlyList<DestructibleBlock> blocks)
        {
            if (blocks == null) return;
            foreach (var b in blocks)
            {
                if (b == null || b.indestructible || b.role == "island") continue;
                b.Broken += x =>
                {
                    if (x == null) return;
                    var s = x.transform.localScale; int kind = x.material != null ? (int)x.material.kind : 0;
                    Break(x.transform.position, kind, (s.x + s.y + s.z) / 3f);
                };
            }
        }
    }
}
