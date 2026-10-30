// CRASHVERSE stylised ocean (URP 14, mobile). Original. Opaque, no depth/opaque texture: shallow lagoon colour and the foam
// line come from the distance to the island ellipse (_Island = centre xz, radii xz), so it is one cheap pass on every tier.
// Vertex sine waves + analytic normal, Fresnel to the horizon colour, sun glint from the URP main light, fog.
Shader "Crashverse/Water"
{
    Properties
    {
        _ShallowColor ("Shallow (lagoon)", Color) = (0.18, 0.86, 0.84, 1)
        _MidColor ("Mid", Color) = (0.05, 0.58, 0.78, 1)
        _DeepColor ("Deep", Color) = (0.02, 0.26, 0.55, 1)
        _HorizonColor ("Horizon / Fresnel", Color) = (0.62, 0.82, 0.96, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _Island ("Island centre xz, radii xz", Vector) = (0, 0, 30, 12.5)
        _ShoreFade ("Lagoon width (ellipse units)", Float) = 0.9
        _FoamWidth ("Foam width (ellipse units)", Float) = 0.035
        _WaveAmp ("Wave amplitude (m)", Float) = 0.12
        _WaveLen ("Wave length (m)", Float) = 7
        _WaveSpeed ("Wave speed", Float) = 1.2
        _Glint ("Sun glint", Float) = 2.5
        _SkyTex ("Sky panorama for fake reflections (same as Crashverse/SkyPano)", 2D) = "black" {}
        _SkyRot ("Sky rotation (deg)", Float) = 200
        _Reflect ("Reflection strength", Range(0, 1)) = 0.55
        _Caustics ("Lagoon caustics", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _MidColor, _DeepColor, _HorizonColor, _FoamColor;
                float4 _Island; float _ShoreFade, _FoamWidth, _WaveAmp, _WaveLen, _WaveSpeed, _Glint;
                float4 _SkyTex_ST; float _SkyRot, _Reflect, _Caustics;
            CBUFFER_END
            TEXTURE2D(_SkyTex); SAMPLER(sampler_SkyTex);
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; half fog : TEXCOORD2; };

            float3 Waves(float3 p, out float3 n)
            {
                float t = _Time.y * _WaveSpeed; float k = 6.2831853 / max(0.5, _WaveLen);
                float2 d1 = normalize(float2(1.0, 0.35)), d2 = normalize(float2(-0.45, 1.0)), d3 = normalize(float2(0.8, -0.7));
                float a1 = _WaveAmp, a2 = _WaveAmp * 0.6, a3 = _WaveAmp * 0.35;
                float p1 = dot(d1, p.xz) * k + t, p2 = dot(d2, p.xz) * k * 1.7 + t * 1.3, p3 = dot(d3, p.xz) * k * 2.9 + t * 1.9;
                float h = a1 * sin(p1) + a2 * sin(p2) + a3 * sin(p3);
                float2 g = d1 * (a1 * k * cos(p1)) + d2 * (a2 * k * 1.7 * cos(p2)) + d3 * (a3 * k * 2.9 * cos(p3));
                n = normalize(float3(-g.x, 1.0, -g.y));
                return float3(p.x, p.y + h, p.z);
            }
            V vert(A i)
            {
                V o; float3 w = TransformObjectToWorld(i.positionOS.xyz); float3 n;
                w = Waves(w, n);
                o.posWS = w; o.nWS = n; o.positionCS = TransformWorldToHClip(w); o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 p = i.posWS; float t = _Time.y;
                // fine ripples for the glint
                float3 n = i.nWS;
                n.xz += 0.06 * float2(sin(p.x * 2.1 + t * 2.3) + sin(p.z * 3.3 - t * 1.7), cos(p.z * 2.7 + t * 2.1) + cos(p.x * 3.9 + t * 1.3));
                n = normalize(n);
                float3 v = normalize(GetWorldSpaceViewDir(p));
                float2 q = (p.xz - _Island.xy) / max(float2(0.1, 0.1), _Island.zw);
                float e = length(q);
                float ang = atan2(q.y, q.x);
                float shallow = saturate(1.0 - (e - 1.0) / max(0.05, _ShoreFade));
                half3 col = lerp(_DeepColor.rgb, _MidColor.rgb, saturate(1.0 - (e - 1.0) / max(0.05, _ShoreFade * 3.0)));
                col = lerp(col, _ShallowColor.rgb, shallow * shallow);
                // lagoon caustics: two drifting interference patterns, brightest in the shallows
                float c1 = sin(p.x * 1.7 + sin(p.z * 1.3 + t * 0.9) * 1.4 + t * 0.7) * sin(p.z * 1.9 + sin(p.x * 1.1 - t * 0.8) * 1.4 - t * 0.6);
                col += _Caustics * shallow * pow(saturate(c1), 3.0) * half3(0.55, 0.95, 0.85);
                float fres = pow(1.0 - saturate(dot(n, v)), 4.0);
                // fake sky reflection: reflect the view ray and sample the sky panorama (upper hemisphere, like SkyPano)
                float3 r = reflect(-v, n); r.y = abs(r.y);
                float elev = degrees(asin(clamp(r.y, 0.0, 1.0)));
                float2 suv = float2(atan2(r.x, r.z) / (2.0 * PI) + 0.5 + _SkyRot / 360.0, saturate((elev + 3.6) / 93.6));
                half3 sky = SAMPLE_TEXTURE2D_LOD(_SkyTex, sampler_SkyTex, suv, 0).rgb;
                float refl = _Reflect * saturate(0.15 + fres * 1.2) * (1.0 - shallow * 0.6);
                col = lerp(col, lerp(_HorizonColor.rgb, sky, step(0.001, dot(sky, 1))), saturate(refl));
                col = lerp(col, _HorizonColor.rgb, saturate(fres * 0.35));
                Light L = GetMainLight();
                float3 h = normalize(L.direction + v);
                col += L.color * pow(saturate(dot(n, h)), 220.0) * _Glint;
                float wob = 0.012 * sin(ang * 23.0 + t * 1.5) + 0.01 * sin(ang * 7.0 - t * 0.9);
                float foam = saturate(1.0 - abs(e - 1.0 - _FoamWidth - wob) / max(0.005, _FoamWidth));
                float foam2 = saturate(1.0 - abs(e - 1.0 - _FoamWidth * 3.2 - wob * 1.7 - 0.01 * sin(t * 0.8)) / max(0.005, _FoamWidth * 0.6)) * 0.5;
                // white-cap flecks a little further out
                float caps = saturate(sin(p.x * 0.9 + t * 1.1 + sin(p.z * 0.7)) * sin(p.z * 1.1 - t * 0.9 + sin(p.x * 0.5)) - 0.82) * 4.0 * saturate((e - 1.2) * 0.5);
                col = lerp(col, _FoamColor.rgb, saturate(foam + foam2 + caps * 0.6) * 0.9);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
