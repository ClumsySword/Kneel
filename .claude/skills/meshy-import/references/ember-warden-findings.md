# Ember Warden import (2026-10-27): measurements behind the skill's defaults

Source: Meshy `Meshy_AI_Ember_Warden_20261021_texture.fbx`, 623,126 tris / 331k verts (UInt32 indices in
Unity), 2048² base colour, metallic, roughness and normal maps. A re-generation of the character
already in the game (old 31k mesh, rigged in Blender with the Knight's bone names, Humanoid).

## Why decimate + bake (and not something cleverer)
- Meshy's look is hard facets, but the surface isn't actually planar: median dihedral angle between
  neighbouring faces is 7.7° at rest. Planar/limited dissolve removes nothing at 2°, ≤2% at 5°, 10% at 10°.
- Collapse decimation + a normal map baked from the full mesh restores the facet shading; the silhouette
  is what's left to lose, and it holds up surprisingly far down.

## Weight transfer onto the existing rig
- Data Transfer, vertex groups, POLYINTERP_NEAREST from the old rigged mesh, rig in REST, then clean
  (0.001) / limit 4 / normalize. Posed check (posecheck.run, combat + overhead poses) against the old
  mesh deformed the same way: median ~0.2 mm, p99 ~3 mm. Outliers were fingers and cape edges.
- Alignment: the new mesh only needed grounding (min z → 0). Its bbox matched the old one within mm.

## Low-poly versions (all from the full mesh, 2048² bakes, cage 1 cm, max ray 3 cm, ~3 s per bake)

| tris | Unity verts | rest dev. max | posed p99 | in-game px diff @3.17 m | @8.5 m | by eye |
|---|---|---|---|---|---|---|
| 60k | 42,644 | – | – | 1.9k* | 0.6k* | = full |
| 40k | 30,928 | – | – | 2.3k* | 0.8k* | = full |
| 30k | 24,842 | 1.3 mm | 2.8 mm | 3.3k | 1.0k | = full |
| 20k | 18,110 | 1.5 mm | – | 4.2k | 1.2k | = full |
| **15k (shipped)** | 14,360 | 1.9 mm | 4.8 mm | 4.5k | 1.3k | = 30k |
| 10k | 10,548 | 4.3 mm | 6.1 mm | 5.3k | 1.4k | slightly wavy trim edges |
| 5k | 6,407 | 7.0 mm | 11 mm (max 10 cm) | 7.2k | 1.7k | jagged trim, coarse outline |

Pixel diffs: >12/255 at 1920×1080 against the full mesh in the same paused Play session. Noise floor
between two captures of the same mesh in one session: 53 px; across sessions ~200 px.
\* from an earlier session with a different crop, so only comparable within their own rows.

The player camera looks down almost vertically (L1, yaw 270): the visible surface is mostly shoulders,
cape and helmet, so even 5k reads identically at the default 8.5 m distance.

## Textures
- `T_EmberWarden_BaseColor` = Meshy `<base>.png` unchanged (sRGB).
- `T_EmberWarden_MetallicSmoothness` = RGB metallic, A = 255 − roughness (linear); material
  `_SmoothnessTextureChannel` 0 (metallic alpha), `_Smoothness` 1.
- Normal: Meshy's is OpenGL convention, like Unity (flipGreenChannel off). The shipped map is the 15k bake.

## Gotchas hit along the way
- `bpy.ops.wm.read_homefile` + import in the same execute call fails (no context): split the calls.
- FBX_SCALE_NONE exported UnitScaleFactor 1 with object scale 100; FBX_SCALE_UNITS matches the original.
- Comparing FBX normals: they're IndexToDirect, resolve NormalsIndex first.
- vertex_group_limit_total under temp_override silently did nothing; select + make active instead.
- An in-game capture taken while the follow camera is still easing in differs by ~20k px. Wait ≥3.5 s.
- `AssetDatabase.SaveAssets()` also wrote an unrelated dirty asset (PlayerLanternSettings) to disk.
