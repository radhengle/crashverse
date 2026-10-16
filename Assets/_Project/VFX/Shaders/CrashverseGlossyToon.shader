// CRASHVERSE glossy toon/PBR-lite lit shader (URP 14, mobile). Original. SRP-Batcher compatible:
// all material properties live in UnityPerMaterial; no per-material keywords except _DAMAGE (shared by destructibles).
// Look (Art v2): bright saturated albedo, 2-band toon diffuse with soft edge, tight glossy spec highlight, cool rim.
// Damage: darken + crack mask in 2 steps (art bible: damage shown in <=2 steps), driven by _DamageStep (0,1,2) via MaterialPropertyBlock-free
// per-instance approach: destructibles swap to one of 3 shared materials per step (keeps batching) -- see VFX_Shader_Spec_v1.md §3.
Shader "Crashverse/GlossyToon"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Base Colour", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.55,0.6,0.85,1)
        _ToonThreshold ("Toon Threshold", Range(-1,1)) = 0.1
        _ToonSoftness ("Toon Softness", Range(0.001,0.5)) = 0.06
        _Gloss ("Gloss (spec power)", Range(8,256)) = 96
        _SpecIntensity ("Spec Intensity", Range(0,2)) = 0.8
        _SpecSize ("Spec Band Cut", Range(0,1)) = 0.55
        _RimColor ("Rim Colour", Color) = (0.75,0.9,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimIntensity ("Rim Intensity", Range(0,2)) = 0.5
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        [NoScaleOffset] _CrackMap ("Crack Mask (R=step1, G=step2)", 2D) = "black" {}
        _CrackColor ("Crack Colour", Color) = (0.12,0.08,0.06,1)
        _DamageStep ("Damage Step 0-2", Range(0,2)) = 0
        _DarkenPerStep ("Darken per Step", Range(0,0.5)) = 0.15
        _HitFlash ("Hit Flash (0-1)", Range(0,1)) = 0
        _HitFlashColor ("Hit Flash Colour", Color) = (1,0.96,0.88,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadeColor, _RimColor, _EmissionColor, _CrackColor, _HitFlashColor;
            half _ToonThreshold, _ToonSoftness, _Gloss, _SpecIntensity, _SpecSize, _RimPower, _RimIntensity;
            half _DamageStep, _DarkenPerStep, _HitFlash;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_CrackMap); SAMPLER(sampler_CrackMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            // Variant budget: main-light shadows (on/cascade off) + fog only. No additional lights on mobile tiers (Spec §4).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionWS:TEXCOORD2;
                half fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half atten = L.shadowAttenuation;

                half ndl = dot(n, L.direction);
                half band = smoothstep(_ToonThreshold - _ToonSoftness, _ToonThreshold + _ToonSoftness, ndl * atten);
                half3 diffuse = albedo.rgb * lerp(_ShadeColor.rgb, L.color, band);

                half3 h = normalize(L.direction + v);
                half spec = pow(saturate(dot(n, h)), _Gloss);
                spec = smoothstep(_SpecSize - 0.05, _SpecSize + 0.05, spec) * band * _SpecIntensity; // glossy "candy" highlight
                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower) * _RimIntensity;

                half3 ambient = SampleSH(n) * albedo.rgb * 0.5h;
                half3 col = diffuse + ambient + spec * L.color + rim * _RimColor.rgb + _EmissionColor.rgb;

                // Damage: 2 steps (darken + crack decal)
                half2 crack = SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, i.uv).rg;
                half step1 = saturate(_DamageStep), step2 = saturate(_DamageStep - 1.0h);
                col *= 1.0h - _DarkenPerStep * _DamageStep;
                half crackMask = max(crack.r * step1, crack.g * step2);
                col = lerp(col, _CrackColor.rgb, crackMask);

                col = lerp(col, _HitFlashColor.rgb, _HitFlash); // flash peak capped x0.4 by C# under Reduce motion
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
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vertShadow(A i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 pws = TransformObjectToWorld(i.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(i.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pws, nws, _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 fragShadow() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vertDepth(A i) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(i); return TransformObjectToHClip(i.positionOS.xyz); }
            half4 fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
