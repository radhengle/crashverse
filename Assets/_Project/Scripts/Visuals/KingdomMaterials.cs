using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.Visuals
{
    /// <summary>
    /// Season 1 "Kingdom of Ironvale" look (design/Season1_KingStory_v1.md, concept art design/level_concepts_v1). One cached material per
    /// surface on Crashverse/GlossyPBR (always-included, proven on device + emulator) with graded CC0 Poly Haven textures from
    /// Resources/Kingdom/Textures. The GlossyPBR shade tint is darkened so unlit sides read (the default lit them at ~65 %, which washed
    /// everything out to pale yellow).
    /// </summary>
    public static class KingdomMaterials
    {
        public const string TexRoot = "Kingdom/Textures/";
        public static readonly Color ShadeColor = new Color(0.16f, 0.2f, 0.34f, 1f);
        public const float ShadeStrength = 1f, Rim = 0.18f;

        static readonly Dictionary<string, Material> _m = new Dictionary<string, Material>();
        static Shader _pbr; static bool _pbrChecked;

        public static Shader Pbr
        {
            get
            {
                if (!_pbrChecked) { _pbrChecked = true; _pbr = Shader.Find("Crashverse/GlossyPBR"); if (_pbr != null && !_pbr.isSupported) _pbr = null; }
                return _pbr;
            }
        }

        public static Texture2D Tex(string name)
        {
            var t = Resources.Load<Texture2D>(TexRoot + name);
            if (t != null) { t.wrapMode = TextureWrapMode.Repeat; t.anisoLevel = Mathf.Max(t.anisoLevel, 2); }
            return t;
        }

        /// <summary>Darker stylised shade + softer rim on any GlossyPBR material (kit materials too).</summary>
        public static void Grade(Material m)
        {
            if (m == null) return;
            if (m.HasProperty("_ShadeColor")) m.SetColor("_ShadeColor", ShadeColor);
            if (m.HasProperty("_ShadeStrength")) m.SetFloat("_ShadeStrength", ShadeStrength);
            if (m.HasProperty("_RimIntensity")) m.SetFloat("_RimIntensity", Rim);
        }

        public static Material Make(string key, string tex, Color tint, float metallic, float roughness, Vector2 tiling, Color emission = default)
        {
            if (_m.TryGetValue(key, out var hit) && hit != null) return hit;
            Material m;
            if (Pbr != null)
            {
                m = new Material(Pbr) { name = "MK_" + key };
                m.SetFloat("_Metallic", metallic); m.SetFloat("_Roughness", roughness);
                m.SetFloat("_OcclusionStrength", 0f);
                if (emission.maxColorComponent > 0f) m.SetColor("_EmissionColor", emission);
                Grade(m);
            }
            else
            {
                m = new Material(Crashverse.Core.RenderMaterials.Lit) { name = "MK_" + key + "_fallback" };
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - roughness);
            }
            var t = string.IsNullOrEmpty(tex) ? null : Tex(tex);
            if (t != null) { m.SetTexture("_BaseMap", t); m.SetTextureScale("_BaseMap", tiling); }
            if (t != null && Pbr != null && m.shader == Pbr)
            {   // real CC0 PBR set (Poly Haven nor_gl + arm = AO/rough/metal, same packing as _ORMMap): relief and grime on every surface
                var n = Tex(tex + "_N"); var orm = Tex(tex + "_ORM");
                if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1.1f); }
                if (orm != null) { m.SetTexture("_ORMMap", orm); m.SetFloat("_OcclusionStrength", 0.85f); m.SetFloat("_Roughness", Mathf.Clamp01(roughness + 0.1f)); }
            }
            m.SetColor("_BaseColor", tint); if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            m.enableInstancing = false;
            if (m.HasProperty("_EdgeWear"))
            {   // rubbed timber edges / chipped masonry arrises (box-built architecture carries per-face edge data)
                bool wood = key == "Planks" || key == "Wood" || key == "Bark";
                bool stone = key == "Stone" || key == "Brick" || key == "Rock" || key == "Roof";
                if (wood || stone)
                {
                    m.SetFloat("_EdgeWear", wood ? 0.85f : 0.75f); m.SetFloat("_EdgeWidth", wood ? 0.045f : 0.07f);
                    m.SetColor("_EdgeWearColor", wood ? new Color(0.80f, 0.68f, 0.52f) : new Color(0.78f, 0.74f, 0.68f));
                }
            }
            return _m[key] = m;
        }

        // Surfaces (texture tiles are authored per metre in the meshes: box UVs = metres / tile)
        public static Material Grass => Make("Grass", "T_K_Grass", Color.white, 0f, 0.95f, Vector2.one);
        public static Material Sand => Make("Sand", "T_K_Sand", Color.white, 0f, 0.95f, Vector2.one);
        public static Material Planks => Make("Planks", "T_K_Planks", Color.white, 0f, 0.8f, Vector2.one);
        public static Material Wood => Make("Wood", "T_K_Wood", Color.white, 0f, 0.8f, Vector2.one);
        public static Material Stone => Make("Stone", "T_K_Stone", Color.white, 0f, 0.85f, Vector2.one);
        public static Material Brick => Make("Brick", "T_K_Brick", Color.white, 0f, 0.85f, Vector2.one);
        public static Material Roof => Make("Roof", "T_K_Roof", Color.white, 0f, 0.7f, Vector2.one);
        public static Material Rock => Make("Rock", "T_K_Rock", Color.white, 0f, 0.9f, Vector2.one);
        public static Material Bark => Make("Bark", "T_K_Bark", Color.white, 0f, 0.9f, Vector2.one);
        public static Material Leaf => Make("Leaf", null, new Color(0.22f, 0.62f, 0.16f), 0f, 0.6f, Vector2.one);
        public static Material Banner => Make("Banner", "T_K_Banner", Color.white, 0f, 0.75f, Vector2.one);
        public static Material Tnt => Make("Tnt", "T_K_TNT", Color.white, 0f, 0.7f, Vector2.one, new Color(1.6f, 0.45f, 0.05f) * 0f);
        public static Material Navy => Make("Navy", "T_K_Metal", new Color(0.13f, 0.15f, 0.20f), 0.75f, 0.45f, new Vector2(1.5f, 1.5f)); // worn gunmetal iron (concept barrel)
        public static Material Gold => Make("Gold", "T_K_Metal", new Color(0.88f, 0.64f, 0.30f), 0.9f, 0.35f, new Vector2(2f, 2f)); // worn brass bands and fittings
        public static Material Iron => Make("Iron", "T_K_Metal", new Color(0.18f, 0.18f, 0.20f), 0.8f, 0.55f, new Vector2(2f, 2f));
        public static Material Bore => Make("Bore", null, new Color(0.02f, 0.02f, 0.025f), 0f, 1f, Vector2.one);
        public static Material FlagRed => Make("FlagRed", null, new Color(0.72f, 0.06f, 0.08f), 0f, 0.7f, Vector2.one);

        /// <summary>Surface material for a mesh-bundle object name suffix (Cannon_Barrel_Navy -> Navy).</summary>
        public static Material ForTag(string objectName)
        {
            int i = objectName.LastIndexOf('_'); string tag = i >= 0 ? objectName.Substring(i + 1) : objectName;
            switch (tag)
            {
                case "Navy": return Navy; case "Gold": return Gold; case "Iron": return Iron; case "Bore": return Bore;
                case "Wood": return Wood; case "Trunk": return Bark; case "Leaf": return Leaf; case "Stone": return Stone;
                case "Roof": return Roof; case "Flag": return FlagRed; case "Grass": return Grass; case "Rock": return Rock;
                default: return Stone;
            }
        }

        /// <summary>Box-UV tile (m) per tag, or 0 = keep authored UVs.</summary>
        public static float TileFor(string objectName)
        {
            if (objectName.EndsWith("_Wood")) return 1.2f;
            if (objectName.EndsWith("_Navy") || objectName.EndsWith("_Gold") || objectName.EndsWith("_Iron")) return 1.0f; // metal detail maps need UVs
            if (objectName.EndsWith("_Trunk")) return 1.5f;
            if (objectName.EndsWith("_Stone")) return 4f;
            if (objectName.EndsWith("_Roof")) return 2.5f;
            if (objectName.EndsWith("_Rock")) return 6f;
            if (objectName.EndsWith("_Grass")) return 5f;
            return 0f;
        }

        static Material _sky, _water;
        public static Material Sky
        {
            get
            {
                if (_sky != null) return _sky;
                var sh = Shader.Find("Crashverse/SkyPano"); var t = Tex("T_K_Sky");
                if (sh == null || !sh.isSupported || t == null) return null;
                t.wrapModeU = TextureWrapMode.Repeat; t.wrapModeV = TextureWrapMode.Clamp;
                _sky = new Material(sh) { name = "MK_Sky" }; _sky.SetTexture("_MainTex", t);
                _sky.SetFloat("_Rotation", 0f); // big cumulus bank straight ahead of the cannon (+x) _sky.SetFloat("_Exposure", 1.05f); _sky.SetFloat("_BottomElev", -3.6f);
                _sky.SetColor("_HorizonColor", HorizonColor);
                return _sky;
            }
        }
        public static readonly Color HorizonColor = new Color(0.74f, 0.85f, 0.95f, 1f);

        public static Material Water
        {
            get
            {
                if (_water != null) return _water;
                var sh = Shader.Find("Crashverse/Water");
                if (sh == null || !sh.isSupported) return null;
                _water = new Material(sh) { name = "MK_Water" };
                _water.SetColor("_HorizonColor", HorizonColor);
                var sky = Tex("T_K_Sky"); if (sky != null) { sky.wrapModeV = TextureWrapMode.Clamp; _water.SetTexture("_SkyTex", sky); _water.SetFloat("_SkyRot", 0f); }
                return _water;
            }
        }
    }
}
