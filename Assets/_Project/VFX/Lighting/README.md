# VFX Lighting assets

## T_SkyFallback_Cube.png (generated, original)
Bright gradient-sky fallback cubemap: sky #8CC8FF (up) / equator #DCEFFF / ground #6FAE5A (down), blended in linear.
6x1 horizontal strip, 128 px per face, face order +X -X +Y -Y +Z -Z. Regenerate with `python3 vfx/tools/gen_sky_cubemap.py`.

### Import settings (Unity 2022.3)
- Texture Type: Default · Texture Shape: **Cube** · Mapping: **Auto** (detects the 6x1 strip)
- sRGB: on · Generate Mip Maps: on (needed for rough reflections) · Convolution Type: **Specular (Glossy Reflection)**, Convolution Samples 64
- Filter: Trilinear · Max Size 128 · Compression: Normal (ASTC 6x6 on Android) · Fixup Edge Seams: on

### Use (spec §3 "Reflection probes per arena")
- Lighting window > Environment > Environment Reflections: Source **Custom**, Cubemap = T_SkyFallback_Cube, Intensity Multiplier 1.
- This is the fallback every object sees outside a baked probe, so metallic steel never reflects black.
- Every arena still needs its baked probes; re-bake when the layout changes.
