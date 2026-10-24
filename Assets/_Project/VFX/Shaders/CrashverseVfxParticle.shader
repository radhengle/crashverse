// CRASHVERSE particle shader for impacts/dust/sparks (URP 14, mobile). Original. SRP-Batcher compatible.
// One shader, two blend setups chosen by material (_SrcBlend/_DstBlend): alpha-blended (dust/smoke) or additive (sparks/flash).
// Soft fade via vertex colour alpha only (no depth texture / soft particles -> no extra pass on Low/Mid). Flipbook via Unity's
// particle Texture Sheet Animation (UV0). _Intensity is the shared flash cap hook (x0.4 under Reduce motion, set on the material by VfxDirector).
Shader "Crashverse/VfxParticle"
{
    Properties
    {
        _BaseMap ("Sprite / Flipbook", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,4)) = 1
        _AlphaClip ("Alpha floor (kills fully transparent overdraw)", Range(0,0.2)) = 0.02
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half _Intensity, _AlphaClip, _SrcBlend, _DstBlend;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            V vert(A i)
            {
                V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color * _BaseColor; o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.color;
                clip(c.a - _AlphaClip);
                c.rgb *= _Intensity;
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
