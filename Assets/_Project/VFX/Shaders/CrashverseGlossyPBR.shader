// CRASHVERSE glossy stylised PBR lit shader (URP 14, Android). Original.
// High/Mid: GGX (URP BRDF via Lighting.hlsl) + normal map + ORM (R occlusion, G roughness, B metallic) + soft shadows + SSAO +
// reflection probes (box projection, blending) + emissive + damage cracks + hit flash, with a soft stylised terminator, Art's shade tint
// multiply (#5B6FA8) and a back-left rim (#FFFFFF ~0.35).
// Low: SubShader LOD 150 (selected with Shader.globalMaximumLOD = 150): no normal map / ORM sampling, stepped Blinn spec, hard shadows only.
// SRP Batcher: one UnityPerMaterial CBUFFER shared by both SubShaders; no MaterialPropertyBlocks.
// Vertex colour: R = damage mask (cracks only where R>0), A = flex weight (hit wobble). Fragments' M_<Mat>_Inner materials set _InnerFace = 1.
Shader "Crashverse/GlossyPBR"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Base Colour", Color) = (1,1,1,1)
        [Normal][NoScaleOffset] _BumpMap ("Normal (OpenGL Y+)", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        [NoScaleOffset] _ORMMap ("ORM (R AO, G Rough, B Metal)", 2D) = "white" {}
        _Roughness ("Roughness x", Range(0,1)) = 1
        _Metallic ("Metallic x", Range(0,1)) = 1
        _MetalReflectFloor ("Metal Reflect Floor (never-dark steel, Art)", Color) = (0.604,0.639,0.686,1)
        _OcclusionStrength ("AO Strength", Range(0,1)) = 1
        [HDR] _EmissionColor ("Emission (HDR, Core ~2.5)", Color) = (0,0,0,1)
        [NoScaleOffset] _EmissionMap ("Emission Mask", 2D) = "white" {}

        _Terminator ("Stylised Terminator Softness (0 = physical)", Range(0,1)) = 0.35
        _TerminatorOffset ("Terminator Offset", Range(-0.5,0.5)) = 0.0
        _ShadeColor ("Shade Tint (#5B6FA8)", Color) = (0.357,0.435,0.659,1)
        _ShadeStrength ("Shade Strength (0.55-0.65)", Range(0,1)) = 0.6
        _RimColor ("Rim Colour", Color) = (1,1,1,1)
        _RimIntensity ("Rim Intensity", Range(0,2)) = 0.35
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimDirWS ("Rim Dir (toward back-left light)", Vector) = (-0.6,0.35,0.7,0)

        [NoScaleOffset] _CrackMap ("Crack Mask (R step1, G step2)", 2D) = "black" {}
        _CrackColor ("Crack Colour (wood #6B4226, mortar #7A3326, steel #3A3F48)", Color) = (0.42,0.259,0.149,1)
        _DamageStep ("Damage Step 0-2", Range(0,2)) = 0
        _DarkenPerStep ("Darken per Step", Range(0,0.5)) = 0.1
        _HitFlash ("Hit Flash", Range(0,1)) = 0
        _HitFlashColor ("Hit Flash Colour", Color) = (1,0.96,0.88,1)
        _FlexAmount ("Flex (x vertex A)", Range(0,0.1)) = 0

        [Toggle] _InnerFace ("Inner Face (fragment interior)", Float) = 0
        _InnerColor ("Inner Colour", Color) = (0.42,0.26,0.15,1)
        _InnerRoughness ("Inner Roughness", Range(0,1)) = 0.9

        _LowSpec ("Low tier spec intensity", Range(0,2)) = 0.5
        _LowRoughness ("Low tier roughness (no ORM)", Range(0,1)) = 0.45
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor, _EmissionColor, _ShadeColor, _RimColor, _RimDirWS, _CrackColor, _HitFlashColor, _InnerColor, _MetalReflectFloor;
        half _BumpScale, _Roughness, _Metallic, _OcclusionStrength;
        half _Terminator, _TerminatorOffset, _ShadeStrength, _RimIntensity, _RimPower;
        half _DamageStep, _DarkenPerStep, _HitFlash, _FlexAmount, _InnerFace, _InnerRoughness, _LowSpec, _LowRoughness;
    CBUFFER_END
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    TEXTURE2D(_BumpMap); TEXTURE2D(_ORMMap); TEXTURE2D(_EmissionMap); TEXTURE2D(_CrackMap);

    float3 FlexOS(float3 p, float3 n, half a) { return p + n * (_FlexAmount * a); }

    // shared surface helpers
    half3 ApplyDamage(half3 col, float2 uv, half mask)
    {
        half2 crack = SAMPLE_TEXTURE2D(_CrackMap, sampler_BaseMap, uv).rg;
        half s1 = saturate(_DamageStep), s2 = saturate(_DamageStep - 1.0h);
        col *= 1.0h - _DarkenPerStep * _DamageStep * mask;
        return lerp(col, _CrackColor.rgb, max(crack.r * s1, crack.g * s2) * mask);
    }
    half3 ApplyFlash(half3 col) { return lerp(col, _HitFlashColor.rgb, min(_HitFlash, 0.85h)); } // capped (Art); x0.4 RM in C#
    half Rim(half3 n, half3 v)
    {
        return pow(1.0h - saturate(dot(n, v)), _RimPower) * _RimIntensity * saturate(dot(n, normalize(_RimDirWS.xyz)) + 0.25h);
    }
    ENDHLSL

    // ───────────── High / Mid ─────────────
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            // Variant budget (see spec §4): shadows 3 × soft 2 × SSAO 2 × probe 2 × lightmap 2 × fog = ≤ 96 before stripping; URP asset strips unused.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; half4 color:COLOR;
                float2 uv:TEXCOORD0; float2 uv2:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1;
                half3 normalWS:TEXCOORD2; half4 tangentWS:TEXCOORD3; half4 color:TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
                half fog:TEXCOORD6; UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o = (Varyings)0; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                VertexPositionInputs p = GetVertexPositionInputs(FlexOS(i.positionOS.xyz, i.normalOS, i.color.a));
                VertexNormalInputs nrm = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = nrm.normalWS; o.tangentWS = half4(nrm.tangentWS, i.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.color = i.color;
                OUTPUT_LIGHTMAP_UV(i.uv2, unity_LightmapST, o.lightmapUV);
                OUTPUT_SH(o.normalWS, o.vertexSH);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 orm = SAMPLE_TEXTURE2D(_ORMMap, sampler_BaseMap, i.uv).rgb;
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, i.uv), _BumpScale);
                half3 bit = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                half3 n = NormalizeNormalPerPixel(TransformTangentToWorld(nTS, half3x3(i.tangentWS.xyz, bit, i.normalWS)));
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                half rough = orm.g * _Roughness, metal = orm.b * _Metallic;
                half ao = lerp(1.0h, orm.r, _OcclusionStrength);
                // Inner face (fragment interiors): own colour, rough, non-metal, no normal detail
                albedo.rgb = lerp(albedo.rgb, _InnerColor.rgb * albedo.rgb, _InnerFace);
                rough = lerp(rough, _InnerRoughness, _InnerFace); metal *= 1.0h - _InnerFace;

                BRDFData brdf; half alpha = 1;
                InitializeBRDFData(albedo.rgb, metal, half3(0,0,0), 1.0h - rough, alpha, brdf);

                float2 ssUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ssao = GetScreenSpaceAmbientOcclusion(ssUV);
                    ao = min(ao, ssao.indirectAmbientOcclusion);
                    half directAO = ssao.directAmbientOcclusion;
                #else
                    half directAO = 1.0h;
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half4 shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);
                Light L = GetMainLight(shadowCoord, i.positionWS, shadowMask);
                half3 bakedGI = SAMPLE_GI(i.lightmapUV, i.vertexSH, n);
                MixRealtimeAndBakedGI(L, n, bakedGI);

                // Indirect: SH/lightmap diffuse + GlossyEnvironmentReflection (box projection / blending via keywords)
                // Expanded URP GlobalIllumination so metals get a reflection floor (Art: steel never renders dark).
                half3 reflWS = reflect(-v, n);
                half3 envSpec = GlossyEnvironmentReflection(reflWS, i.positionWS, brdf.perceptualRoughness, ao);
                envSpec = lerp(envSpec, max(envSpec, _MetalReflectFloor.rgb * ao), metal);
                half fresnelTerm = Pow4(1.0h - saturate(dot(n, v)));
                half3 gi = EnvironmentBRDF(brdf, bakedGI * ao, envSpec, fresnelTerm);

                // Direct: GGX spec from URP + stylised terminator on the diffuse/irradiance term
                half ndlRaw = dot(n, L.direction);
                half physical = saturate(ndlRaw);
                half styl = smoothstep(_TerminatorOffset - _Terminator * 0.5h, _TerminatorOffset + _Terminator * 0.5h + 1e-3h, ndlRaw);
                half ndl = lerp(physical, styl, step(1e-3h, _Terminator));
                half atten = L.shadowAttenuation * L.distanceAttenuation * directAO;
                half lit = ndl * atten;
                half3 shade = lerp(1.0h, _ShadeColor.rgb, _ShadeStrength); // Art multiply tint in the unlit side
                half3 diffuse = brdf.diffuse * L.color * lerp(shade, 1.0h, lit);
                half3 spec = brdf.specular * DirectBRDFSpecular(brdf, n, L.direction, v) * L.color * lit;

                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, i.uv).rgb * _EmissionColor.rgb;
                half3 col = gi + diffuse + spec + Rim(n, v) * _RimColor.rgb * ao + emission;

                col = ApplyDamage(col, i.uv, i.color.r);
                col = ApplyFlash(col);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragNull
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define CV_SHADOW_PASS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vertShadow(A i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 pws = TransformObjectToWorld(FlexOS(i.positionOS.xyz, i.normalOS, i.color.a));
                float3 nws = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - pws);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pws, nws, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 fragNull() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragNull
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vertDepth(A i) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(i); return TransformObjectToHClip(FlexOS(i.positionOS.xyz, i.normalOS, i.color.a)); }
            half4 fragNull() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            // Needed for SSAO (depth-normals prepass)
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; half4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half3 n:TEXCOORD1; half4 t:TEXCOORD2; };
            V vertDN(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
                o.positionCS = TransformObjectToHClip(FlexOS(i.positionOS.xyz, i.normalOS, i.color.a));
                VertexNormalInputs nrm = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.n = nrm.normalWS; o.t = half4(nrm.tangentWS, i.tangentOS.w); o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }
            half4 fragDN(V i) : SV_Target
            {
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, i.uv), _BumpScale);
                half3 b = i.t.w * cross(i.n, i.t.xyz);
                half3 n = normalize(TransformTangentToWorld(nTS, half3x3(i.t.xyz, b, i.n)));
                return half4(n, 0);
            }
            ENDHLSL
        }
    }

    // ───────────── Low (LOD 150) ─────────────
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 150

        Pass
        {
            Name "ForwardLitLow"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS   // hard shadows only (or off on Low URP asset)
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; half3 n:TEXCOORD2; half4 color:TEXCOORD3; half3 sh:TEXCOORD4; half fog:TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.n = TransformObjectToWorldNormal(i.normalOS); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.color = i.color;
                o.sh = SampleSH(o.n); o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                albedo.rgb = lerp(albedo.rgb, _InnerColor.rgb * albedo.rgb, _InnerFace);
                half3 n = normalize(i.n); half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(n, L.direction);
                half band = smoothstep(_TerminatorOffset - 0.1h, _TerminatorOffset + 0.1h, ndl) * L.shadowAttenuation;
                half3 shade = lerp(1.0h, _ShadeColor.rgb, _ShadeStrength);
                half3 h = normalize(L.direction + v);
                half gloss = 1.0h - _LowRoughness; // Low tier: scalar roughness (no ORM fetch)
                half spec = step(0.5h, pow(saturate(dot(n, h)), exp2(gloss * 8.0h + 2.0h))) * band * _LowSpec * (1.0h - _InnerFace);
                half3 col = albedo.rgb * (L.color * lerp(shade, 1.0h, band) + lerp(i.sh * 0.6h, max(i.sh * 0.6h, _MetalReflectFloor.rgb), _Metallic)) + spec + Rim(n, v) * _RimColor.rgb + _EmissionColor.rgb;
                col = ApplyDamage(col, i.uv, i.color.r);
                col = ApplyFlash(col);
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragNull
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define CV_SHADOW_PASS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vertShadow(A i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 pws = TransformObjectToWorld(FlexOS(i.positionOS.xyz, i.normalOS, i.color.a));
                float3 nws = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - pws);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pws, nws, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 fragNull() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragNull
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vertDepth(A i) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(i); return TransformObjectToHClip(FlexOS(i.positionOS.xyz, i.normalOS, i.color.a)); }
            half4 fragNull() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    // Low devices that fail LOD 150 too fall back to the original toon shader.
    FallBack "Crashverse/GlossyToon"
}
