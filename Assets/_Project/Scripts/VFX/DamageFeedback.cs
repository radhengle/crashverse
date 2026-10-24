using System.Collections.Generic;
using UnityEngine;
using Crashverse.Feel;

namespace Crashverse.VFX
{
    /// <summary>
    /// Per-piece damage feedback on CrashverseGlossyPBR: _HitFlash (capped 0.85, ×0.4 + ≥100 ms ramp under Reduce motion), _DamageStep 0/1/2
    /// with the wedge crack mask (R sparse, G dense), per-material darkening and crack colour (DamageRules). Presentation only: never
    /// touches HP or physics. Driven by VfxDirector from ImpactEvent (PHYS_IMPACT/PHYS_CRACK by pieceId) or directly via Hit()/SetDamageStep().
    /// Material instances (renderer.materials) keep SRP Batcher compatibility; MaterialPropertyBlocks would drop the piece off the batcher.
    /// </summary>
    public class DamageFeedback : MonoBehaviour
    {
        public VfxMaterial material = VfxMaterial.Wood;
        [Tooltip("Per-piece dust colour 0xRRGGBB for this piece's Stone FX (0 = recipe colours). Roof tiles: #D9A28C terracotta (PM). " +
                 "Only used while material is Stone, so a non-Stone material assigned in code never gets it.")]
        public uint dustOverride;
        [Tooltip("ImpactEvent.pieceId (PR #20: StableId.Of(blockId)). -1 = only driven directly.")] public int pieceId = -1;
        public Renderer[] renderers;
        [Tooltip("T_CrackMask_Wedge (R step 1 sparse, G step 2 dense). Optional if the material already has _CrackMap.")] public Texture2D crackMask;

        static readonly Dictionary<int, DamageFeedback> _byPiece = new Dictionary<int, DamageFeedback>();
        public static bool TryGet(int id, out DamageFeedback d) => _byPiece.TryGetValue(id, out d) && d != null;
        public static int RegisteredCount => _byPiece.Count;

        int _step; float _flashStart = -1f, _flashPeak; int _flashFrames; bool _flashRm; float _lastFlash = -1f;
        public int DamageStep => _step;

        const string PHitFlash = "_HitFlash", PHitFlashColor = "_HitFlashColor", PStep = "_DamageStep", PDarken = "_DarkenPerStep", PCrack = "_CrackColor", PCrackMap = "_CrackMap";

        /// <summary>Default material from the imported fractureClass (hero buildings) when the piece carries ImportedPhysicsHints.
        /// Runs before AddComponent returns, so an explicit assignment by the caller (PieceFeedbackWiring) still wins.</summary>
        void Awake()
        {
            material = MaterialFromHints(gameObject, material);
            if (dustOverride == 0) dustOverride = DustOverrideFromHints(gameObject);
        }

        /// <summary>Dust override from the object's ImportedPhysicsHints.fractureClass ("Roof" → #D9A28C), else 0.</summary>
        public static uint DustOverrideFromHints(GameObject go)
        {
            var h = go != null ? go.GetComponent<Crashverse.Import.ImportedPhysicsHints>() : null;
            return h != null ? VfxRules.DustOverrideForFractureClass(h.fractureClass) : 0u;
        }

        /// <summary>VfxMaterial from the object's ImportedPhysicsHints.fractureClass, else <paramref name="fallback"/>.</summary>
        public static VfxMaterial MaterialFromHints(GameObject go, VfxMaterial fallback)
        {
            var h = go != null ? go.GetComponent<Crashverse.Import.ImportedPhysicsHints>() : null;
            return h != null && VfxRules.TryFromFractureClass(h.fractureClass, out var m) ? m : fallback;
        }

        void OnEnable() { if (pieceId != -1) _byPiece[pieceId] = this; ApplyStatic(); }

        /// <summary>Change material at runtime and re-apply crack colour / darkening.</summary>
        public void SetMaterial(VfxMaterial m) { material = m; ApplyStatic(); }
        void OnDisable() { if (pieceId != -1 && _byPiece.TryGetValue(pieceId, out var d) && d == this) _byPiece.Remove(pieceId); }

        /// <summary>Set the piece id at spawn (pooled pieces) and register.</summary>
        public void Register(int id) { OnDisable(); pieceId = id; if (id != -1) _byPiece[id] = this; }

        /// <summary>Hit flash. impactTier 0 Light, 1 Medium, 2 Heavy.</summary>
        public void Hit(int impactTier)
        {
            bool rm = MotionSettings.Current != null && MotionSettings.Current.ReduceMotion;
            _flashFrames = DamageRules.FlashFrames(material); if (_flashFrames == 0) return;
            _flashPeak = DamageRules.FlashPeak(impactTier, rm); _flashRm = rm; _flashStart = Time.unscaledTime;
            SetColor(PHitFlashColor, VfxColor.From(DamageRules.FlashColor(material)));
        }

        /// <summary>0/1/2 (shader step). Only increases while the piece is alive.</summary>
        public void SetDamageStep(int shaderStep)
        {
            int s = DamageRules.Advance(_step, shaderStep); if (s == _step) return;
            _step = s; SetFloat(PStep, _step);
        }

        /// <summary>Pool reuse: back to undamaged. Re-applies the per-material crack colour/darkening (material may have been set after OnEnable).</summary>
        public void ResetFeedback() { _step = 0; _flashStart = -1f; ApplyStatic(); SetFloat(PHitFlash, 0f); _lastFlash = 0f; }

        void Update()
        {
            if (_flashStart < 0f) return;
            float v = DamageRules.FlashAt(Time.unscaledTime - _flashStart, _flashPeak, _flashFrames, _flashRm);
            if (v <= 0f) { _flashStart = -1f; v = 0f; }
            if (v != _lastFlash) { SetFloat(PHitFlash, v); _lastFlash = v; }
        }

        void ApplyStatic()
        {
            SetFloat(PDarken, DamageRules.DarkenPerStep(material));
            SetColor(PCrack, VfxColor.From(DamageRules.CrackColor(material)));
            SetFloat(PStep, _step);
#if UNITY_2019_1_OR_NEWER
            if (crackMask != null) foreach (var m in Mats()) m.SetTexture(PCrackMap, crackMask);
#endif
        }

#if UNITY_2019_1_OR_NEWER
        Material[] _mats;
        Material[] Mats()
        {
            if (_mats != null) return _mats;
            var list = new List<Material>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) if (r != null) list.AddRange(r.materials); // instances (SRP-batcher compatible)
            return _mats = list.ToArray();
        }
        void SetFloat(string n, float v) { foreach (var m in Mats()) m.SetFloat(n, v); }
        void SetColor(string n, Color c) { foreach (var m in Mats()) m.SetColor(n, c); }
        void OnDestroy() { if (_mats != null) foreach (var m in _mats) Destroy(m); }
#else
        // Compile-check path (stubs have no Renderer.materials): MaterialPropertyBlock. Not used in Unity builds.
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        void SetFloat(string n, float v) { if (renderers == null) return; foreach (var r in renderers) { r.GetPropertyBlock(_mpb); _mpb.SetFloat(n, v); r.SetPropertyBlock(_mpb); } }
        void SetColor(string n, Color c) { if (renderers == null) return; foreach (var r in renderers) { r.GetPropertyBlock(_mpb); _mpb.SetColor(n, c); r.SetPropertyBlock(_mpb); } }
#endif
    }
}
