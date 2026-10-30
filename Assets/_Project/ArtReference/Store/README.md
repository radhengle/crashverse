# CRASHVERSE Play Store assets (v1)
Art generated in Canva in the v2 style (prompts below), finished with PIL. Original art, no third-party IP.

| File | Spec |
|---|---|
| icon_512.png | **Primary app icon.** 512x512 PNG, 32-bit RGBA, fully opaque, full-bleed square (no rounded corners, no shadow; Google Play applies the mask). Max 1024 KB. No text. |
| icon_512_var_punchy.png | Variant: +15% saturation, +8% contrast |
| icon_512_var_zoom.png | Variant: cropped in so the Wrecker reads better at small sizes |
| feature_1024x500.png / .jpg | **Feature graphic.** 1024x500, 24-bit RGB, no alpha. CRASHVERSE lockup + tagline, Wrecker, floating island, collapsing castle. Key content kept inside the centre ~80% (see src/feature_safezone_preview.png). |
| feature_1024x500_var_notagline.png | Variant without the tagline ("AIM. BLAST. WRECK IT ALL." is a placeholder that still needs PM/marketing sign-off) |
| src/ | Canva sources (icon_canva_a.jpg, icon_source_1264.png, feature_canva_a.jpg), clean art without the title, mask and safe-zone previews |
| make_feature.py | Builds the feature graphic |

Canva media: icon MAHYJXRyiyY, feature MAHYJQai5bo.
Icon prompt: glossy 3D chunky Wrecker cannon (navy barrel, gold bands, cyan core) blasting a stone castle tower into bricks, sky-blue background, bold centred silhouette, no text.
Feature prompt: wide key art, Wrecker on the left firing a cyan blast at a collapsing castle on a floating island on the right, clear sky in the top centre for the title, subjects kept away from the edges, no text.
