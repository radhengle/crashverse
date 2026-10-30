// CRASHVERSE foliage wind (URP 14, Android). Original. Shared by Crashverse/RealisticFoliage (all passes) so shadows/depth match colour.
//
// VERTEX COLOUR CONVENTION (3D Modeler, foliage meshes):
//   R = branch sway weight   0 at trunk/root -> 1 at branch tips (bends the whole branch along the wind direction)
//   G = leaf flutter weight  0 = rigid (trunk, thick branches) -> 1 = free leaf-card edges (small fast motion along the normal)
//   B = phase offset         0..1 random per branch / leaf cluster so neighbours do not move in sync
//   A = unused by wind (keep 1)
// A mesh with no vertex colours imports as white (1,1,1,1) = full sway + flutter: always paint trunks black in R/G.
//
// Globals (WindController.cs, Shader.SetGlobalVector, never per material, so SRP Batcher compatible):
//   _CV_WindDirection  xyz = world wind direction (normalised), w = strength (0 = wind off; Low tier always 0)
//   _CV_WindParams     x = branch sway frequency (Hz), y = leaf flutter frequency (Hz), z = gust 0..1, w = detail (0 off, 1 Mid, 2 High)
#ifndef CRASHVERSE_WIND_INCLUDED
#define CRASHVERSE_WIND_INCLUDED

float4 _CV_WindDirection;
float4 _CV_WindParams;

// positionWS/normalWS in world space; pivot = object origin (per-instance with GPU instancing). No loops; one uniform branch.
float3 CV_ApplyWind(float3 positionWS, float3 normalWS, half4 vcol, half swayScale, half flutterScale)
{
    float strength = _CV_WindDirection.w;
    UNITY_BRANCH
    if (strength <= 0.0)
        return positionWS;                                         // Low tier / no WindController: static foliage

    float3 dir = _CV_WindDirection.xyz;
    float3 pivot = UNITY_MATRIX_M._m03_m13_m23;
    float t = _Time.y;
    float phase = vcol.b * 6.2831853 + dot(pivot, float3(0.137, 0.0, 0.173));

    // Branch sway: low-frequency bend along the wind, gust-biased so trees lean downwind, scaled by height above the pivot.
    float sway = sin(t * _CV_WindParams.x * 6.2831853 + phase) * 0.5 + 0.5 * _CV_WindParams.z + 0.25;
    float height = saturate((positionWS.y - pivot.y) * 0.25);
    float3 offset = dir * (sway * strength * vcol.r * swayScale * (0.35 + height));

    // Leaf flutter along the normal. Mid: one sine. High: + a second, faster harmonic.
    float f = sin(t * _CV_WindParams.y * 6.2831853 + phase * 3.0);
    UNITY_BRANCH
    if (_CV_WindParams.w > 1.5)
        f = f * 0.7 + sin(t * _CV_WindParams.y * 15.7 + phase * 7.0) * 0.3;
    offset += normalWS * (f * strength * vcol.g * flutterScale);

    return positionWS + offset;
}

// LOD cross-fade (dithered). unity_LODFade.x is set by the engine for LODGroups with fadeMode = CrossFade.
void CV_LODFade(float4 positionCS)
{
    #if defined(LOD_FADE_CROSSFADE)
        LODDitheringTransition(uint2(positionCS.xy), unity_LODFade.x);
    #endif
}

#endif
