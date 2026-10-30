using System.Collections.Generic;
using Crashverse.Destruction;
using Crashverse.VFX;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashverse.Visuals
{
    /// <summary>
    /// The King's hidden powder core (design/Season1_KingStory_v1.md §3). One structural piece per run hides a TNT crate, picked with a
    /// deterministic RNG seeded by runSeed ^ hash(levelId) among the inner load-bearing pieces. Exposure: the host drops to ≤ 66 % HP
    /// or a touching piece (support/dependent) breaks — the crate shows through with a pulsing glow and fuse sparks. Detonation: a
    /// direct hit on the exposed host, or the host breaking while exposed → 400 radial damage within 6 m, a strong outward impulse,
    /// stacked explosion VFX, a flash and camera shake (EXPLODE_CRATE). Host breaking before exposure = dud (small puff, no bonus).
    /// </summary>
    public class HiddenCore : MonoBehaviour
    {
        public const float Radius = 6f, Damage = 400f, Impulse = 1800f, ExposeHpFrac = 0.66f;

        public DestructibleBlock Host { get; private set; }
        public bool Exposed { get; private set; }
        public bool Spent { get; private set; }
        public static System.Func<VfxDirector> Vfx;

        Transform _crate; Renderer[] _glowR; MaterialPropertyBlock _mpb; ParticleSystem _fuse; Light _flash;
        bool _queued; float _flashT = -1f; readonly List<DestructibleBlock> _neighbours = new List<DestructibleBlock>();

        static uint Fnv(string s) { uint h = 2166136261u; if (s != null) foreach (char c in s) { h ^= c; h *= 16777619u; } return h; }

        public static DestructibleBlock Pick(IReadOnlyList<DestructibleBlock> blocks, int seed, string levelId)
        {
            var cand = new List<DestructibleBlock>(); Vector3 c = Vector3.zero; int n = 0;
            foreach (var b in blocks)
            {
                if (b == null || b.indestructible || b.explosive || !b.isObjectivePiece || b.isRoof || b.role == "island" || b.role == "roof") continue;
                var s = b.transform.localScale; if (Mathf.Min(s.x, Mathf.Min(s.y, s.z)) < 0.35f) continue;
                if (b.Supports.Count + b.Dependents.Count == 0) continue;
                cand.Add(b); c += b.transform.position; n++;
            }
            if (n == 0) return null;
            c /= n;
            cand.Sort((a, d) => string.CompareOrdinal(a.blockId, d.blockId)); // stable base order
            var inner = new List<DestructibleBlock>(cand);
            inner.Sort((a, d) => { int k = (a.transform.position - c).sqrMagnitude.CompareTo((d.transform.position - c).sqrMagnitude); return k != 0 ? k : string.CompareOrdinal(a.blockId, d.blockId); });
            int pool = Mathf.Max(1, Mathf.CeilToInt(inner.Count * 0.6f));
            var rng = new System.Random((int)((uint)seed ^ Fnv(levelId)));
            return inner[rng.Next(pool)];
        }

        public static HiddenCore Attach(IReadOnlyList<DestructibleBlock> blocks, int seed, string levelId, Transform parent)
        {
            var host = Pick(blocks, seed, levelId); if (host == null) return null;
            var go = new GameObject("HiddenCore"); go.transform.SetParent(parent, false);
            var hc = go.AddComponent<HiddenCore>(); hc.Bind(host, blocks); return hc;
        }

        void Bind(DestructibleBlock host, IReadOnlyList<DestructibleBlock> blocks)
        {
            Host = host;
            foreach (var b in host.Supports) if (b != null && !_neighbours.Contains(b)) _neighbours.Add(b);
            foreach (var b in host.Dependents) if (b != null && !_neighbours.Contains(b)) _neighbours.Add(b);
            host.Damaged += OnHostDamaged; host.Broken += OnHostBroken;
            host.Collapsed += _ => { if (!Exposed) Dud(); }; host.WentOutOfBounds += _ => { if (!Exposed) Dud(); };
            foreach (var nb in _neighbours) nb.Broken += _ => Expose();
            BuildCrate();
        }

        void BuildCrate()
        {
            var t = new GameObject("CoreCrate").transform; t.SetParent(Host.transform, false);
            var s = Host.transform.localScale;
            t.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            float size = Mathf.Clamp(Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * 0.8f, 0.4f, 0.75f);
            // sits proud of the cannon-facing face so it reads once exposed
            var box = new GameObject("TNT"); box.transform.SetParent(t, false);
            box.transform.localPosition = new Vector3(-s.x * 0.5f + size * 0.3f, 0f, 0f); box.transform.localScale = Vector3.one * size;
            box.AddComponent<MeshFilter>().sharedMesh = CvMesh.BoxCube(Vector3.one, 1f);
            var r = box.AddComponent<MeshRenderer>(); r.sharedMaterial = KingdomMaterials.Tnt; r.shadowCastingMode = ShadowCastingMode.Off;
            _glowR = new Renderer[] { r }; _mpb = new MaterialPropertyBlock();
            _crate = t; t.gameObject.SetActive(false);

            _fuse = MakeSparks(t, new Vector3(-s.x * 0.5f, size * 0.55f, 0f));
            var lg = new GameObject("CoreFlash"); lg.transform.SetParent(transform, false);
            _flash = lg.AddComponent<Light>(); _flash.type = LightType.Point; _flash.color = new Color(1f, 0.62f, 0.25f); _flash.range = 14f; _flash.intensity = 0f; _flash.enabled = false;
            _flash.shadows = LightShadows.None;
        }

        static Material _sparkMat;
        static ParticleSystem MakeSparks(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("FuseSparks"); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.45f, 0.1f));
            main.gravityModifier = 0.6f; main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            var em = ps.emission; em.rateOverTime = 40f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.02f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = go.GetComponent<ParticleSystemRenderer>(); pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.05f; pr.lengthScale = 2f;
            if (_sparkMat == null)
            {
                var set = VfxAssetSet.Load(); var a = set != null ? set.assets : null;
                if (a != null && a.particleShader != null)
                {
                    _sparkMat = new Material(a.particleShader) { name = "M_CoreSpark" };
                    _sparkMat.SetTexture("_BaseMap", a.Get(VfxSheet.SparkStreak));
                    _sparkMat.SetFloat("_SrcBlend", 1f); _sparkMat.SetFloat("_DstBlend", 1f); _sparkMat.SetFloat("_Intensity", 1.5f);
                }
            }
            if (_sparkMat != null) pr.sharedMaterial = _sparkMat;
            return ps;
        }

        void OnHostDamaged(DestructibleBlock b, float removed, bool direct)
        {
            if (Spent) return;
            if (Exposed) { if (direct) _queued = true; return; }
            if (!b.IsBroken && b.currentHp <= b.MaxHp * ExposeHpFrac) Expose();
        }

        void OnHostBroken(DestructibleBlock b) { if (Spent) return; if (Exposed) _queued = true; else Dud(); }

        void Expose()
        {
            if (Exposed || Spent || Host == null || Host.IsBroken) return;
            Exposed = true;
            if (_crate != null) _crate.gameObject.SetActive(true);
            if (_fuse != null) _fuse.Play();
            Crashverse.Feel.AnimEventBus.Raise("CORE_EXPOSED", Host.transform.position);
        }

        void Dud()
        {
            if (Spent) return; Spent = true;
            if (_crate != null) _crate.gameObject.SetActive(false);
            var v = Vfx?.Invoke(); if (v != null && Host != null) v.Play(VfxIds.CollapseDust, Host.transform.position, Vector3.up, false, 0u);
        }

        void Update()
        {
            if (Exposed && !Spent && _glowR != null)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                _mpb.SetColor("_EmissionColor", new Color(1f, 0.42f, 0.1f) * (0.6f + 2.2f * p));
                foreach (var r in _glowR) if (r != null) r.SetPropertyBlock(_mpb);
            }
            if (_flashT >= 0f)
            {
                _flashT += Time.deltaTime; float k = 1f - _flashT / 0.6f;
                if (k <= 0f) { _flash.enabled = false; _flashT = -1f; } else _flash.intensity = 9f * k * k;
            }
        }

        void FixedUpdate()
        {
            if (!_queued || Spent) return;
            _queued = false; Detonate();
        }

        void Detonate()
        {
            Spent = true;
            Vector3 at = _crate != null ? _crate.position : Host.transform.position;
            if (_crate != null) _crate.gameObject.SetActive(false);
            if (!Host.IsBroken) Host.Break();
            Explosion.Detonate(at, Radius, Damage, Host);
            foreach (var c in Physics.OverlapSphere(at, Radius * 1.4f))
            {
                var rb = c.attachedRigidbody; if (rb == null || rb.isKinematic) continue;
                rb.AddExplosionForce(Impulse, at, Radius * 1.6f, 0.8f, ForceMode.Impulse);
            }
            var v = Vfx?.Invoke();
            if (v != null)
            {
                v.Play(VfxIds.Explosion, at, Vector3.up, false, 0u);
                v.Play(VfxIds.Explosion, at + new Vector3(0.6f, 0.9f, 0.8f), Vector3.up, false, 0u);
                v.Play(VfxIds.Explosion, at + new Vector3(-0.4f, 0.4f, -0.9f), Vector3.up, false, 0u);
                v.Play(VfxIds.CollapseDust, at + Vector3.down * 0.5f, Vector3.up, false, 0u);
            }
            if (_flash != null) { _flash.transform.position = at + Vector3.left * 1.2f; _flash.enabled = true; _flashT = 0f; }
            Crashverse.Feel.AnimEventBus.Raise(Crashverse.Feel.ShakeRouter.ExplodeCrate, new VfxHit(at, VfxMaterial.Explosive));
        }
    }
}
