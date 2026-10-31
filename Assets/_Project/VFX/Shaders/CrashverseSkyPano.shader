// CRASHVERSE panoramic sky (URP 14, mobile). Original. Samples the upper part of an equirectangular photo sky
// (Poly Haven CC0, cropped zenith..just below the horizon) by view direction. Used as RenderSettings.skybox.
Shader "Crashverse/SkyPano"
{
    Properties
    {
        _MainTex ("Sky (top = zenith, bottom = _BottomElev)", 2D) = "white" {}
        _Rotation ("Rotation (deg)", Range(0, 360)) = 0
        _BottomElev ("Elevation of the image bottom (deg)", Range(-20, 10)) = -3.6
        _Exposure ("Exposure", Range(0, 3)) = 1
        _HorizonColor ("Below the horizon", Color) = (0.72, 0.84, 0.95, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float _Rotation, _BottomElev, _Exposure; half4 _HorizonColor;
            CBUFFER_END
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.dir = i.positionOS.xyz; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float elev = degrees(asin(clamp(d.y, -1.0, 1.0)));
                float u = atan2(d.x, d.z) / (2.0 * PI) + 0.5 + _Rotation / 360.0;
                float v = saturate((elev - _BottomElev) / (90.0 - _BottomElev));
                half3 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(u, v), 0).rgb;
                half below = saturate((_BottomElev + 2.0 - elev) / 4.0);
                c = lerp(c, _HorizonColor.rgb, below);
                return half4(c * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
