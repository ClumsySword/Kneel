---
name: meshy-import
description: Bring a Meshy AI model (or any other very high-poly AI/scan mesh) into the Kneel Unity project at game-ready cost. Covers decimating it to a sensible triangle budget, baking the full mesh's detail into a normal map, packing Meshy's textures for URP Lit, optionally putting it on an existing rig (replacing a character's body mesh while keeping bones, avatar and GUIDs), importing it with the right settings, and comparing candidate budgets in game. Use this whenever the user mentions Meshy, a Meshy download/FBX, an AI-generated model, a model that's "too many polys" or "too heavy", wants to swap a character's mesh for a new version, or asks to import a generated prop/character into Kneel, even if they don't say "decimate" or "bake".
---

# Meshy import for Kneel

Meshy exports 300k–600k-triangle meshes with a faceted, hand-sculpted look, plus 2048² base colour,
metallic, roughness and normal maps. That's 20–100× too heavy for a game object. The fix that keeps the
look: **collapse-decimate a copy, then bake the full mesh's normals (including Meshy's own normal map)
into a new normal map for the low-poly**. The baked map carries the facet shading, so only the silhouette
gets coarser, and in Kneel's top-down camera the silhouette survives down to surprisingly low budgets.
On the Ember Warden, 15k triangles looked identical to 30k and the full 623k mesh. The measurements are in
`references/ember-warden-findings.md`; read it when choosing a budget or if something looks off.

Tools: the Blender MCP (`execute_blender_code`) and the Unity MCP (`execute_code`, `manage_camera`,
`read_console`). Python with Pillow and numpy for the image scripts.

Scripts (paths relative to this skill folder, `.claude/skills/meshy-import/` in the repo):
- `scripts/blender/lowpoly.py`: import, place/ground, weight transfer, decimate, deviation, bake, export
- `scripts/blender/posecheck.py`: poses the rig hard and measures skinning errors
- `scripts/blender/fbxcmp.py`: structural FBX diff (transforms, hierarchy, clusters, bind pose)
- `scripts/pack_textures.py`: Meshy textures → BaseColor / MetallicSmoothness (/ Normal)
- `scripts/compare_shots.py`: pixel-diff in-game captures and lay them out as a sheet
- `scripts/unity/*.cs`: bodies for `execute_code`. Each has `// EDIT` constants at the top: fill those
  in and send the file's text as the code. They're C# 6 so they compile under CodeDom.

In Blender: `import sys; sys.path.insert(0, r"<repo>\.claude\skills\meshy-import\scripts\blender"); import importlib, lowpoly; importlib.reload(lowpoly)`.

## Ground rules

These come from the project and from what went wrong before:
- **Blender is someone's live session.** Check `bpy.data.filepath` and `bpy.data.is_dirty` before
  anything. If it has unsaved work, ask before replacing the scene. When the session is yours, clearing
  it is fine, but do `read_homefile`/clearing and importing in separate calls (doing both in one call fails).
- **Work files go in the scratchpad**, never in `Assets/` (except the final files and a temporary
  `_Compare/` folder). Back up any asset you'll overwrite, with its `.meta`, before overwriting it.
- **Replacing an existing asset keeps its GUID**: copy the new file over the old one in the file system
  and leave the `.meta` alone. The `.meta` holds the avatar, the T-posed `humanDescription.skeleton` and the
  material remap. Every prefab reference survives.
- **Don't call `AssetDatabase.SaveAssets()`.** It also writes other people's unsaved edits to disk; it
  once flushed an unrelated `PlayerLanternSettings` change. `CreateAsset` already writes new files;
  use `SaveAssetIfDirty(obj)` for a specific asset.
- **Never save a scene** while capturing (they're owned by individual people), and don't touch Synty or other
  third-party assets. New team content goes under `Assets/Kneel/<feature>/`.
- **No commits** unless the user asks. Report what changed instead.
- `execute_code` blocks `AssetDatabase.DeleteAsset`: delete files and their `.meta` through the file system,
  then `AssetDatabase.Refresh()`.

## 1. Identify the case

Look at the download (`<base>.fbx` + `<base>.png`, `_metallic`, `_roughness`, `_normal`) and ask yourself
which of these it is. Ask the user if it's unclear.

- **A. New version of an existing rigged model** (like the Ember Warden): same character, same pose,
  regenerated at higher quality. Goal: replace the body mesh, keep bones, avatar, GUIDs, material.
- **B. Static prop or environment piece**: no rig. Goal: a new mesh + material under `Assets/Kneel/<feature>/`.
- **C. New character with no rig yet**: this needs rigging first (in Blender with the Knight's bone names,
  or Mixamo), which this skill doesn't do. Say so; you can still produce the low-poly + bake first,
  because rigging a 15k mesh is much easier than a 600k one.

## 2. Pick a budget

Start from the Ember Warden data: the camera looks almost straight down from 3.2–8.5 m.

| Asset | Start at | Notes |
|---|---|---|
| Player / hero character | 15k tris | = 30k by eye; 10k shows slightly wavy trim; 5k jagged trim |
| Enemy / NPC (many on screen) | 8–10k | several on screen at once, rarely zoomed on |
| Large prop / set piece | 3–8k | depends on silhouette complexity |
| Small prop | 0.5–2k | 1024² textures are usually plenty |

The prop rows are starting points, not measurements. If the user cares, or the asset is prominent, make
2–4 candidates around the guess and compare them in game (step 6) instead of arguing.

## 3. Blender: high-poly → low-poly + bake

```python
hp = lowpoly.import_fbx(r"<download>\<base>.fbx")   # returns new objects; Meshy's mesh is usually "Mesh_0"
```

**Case A (existing rig).** Also import the current game FBX, which brings the rig and the old mesh. Then:
```python
new = lowpoly.place_like(meshy, old_mesh, 'X_HP')     # into old_mesh's space, grounded to z=0
print(lowpoly.fit_report(new, old_mesh))              # bboxes within ~1 cm, median a few mm, or stop
lowpoly.transfer_weights(new, old_mesh, rig)          # nearest-face weights, cleaned to 4 influences
posecheck.run('X_HP', old_mesh.name, rig_name=rig.name)   # median ≲0.5 mm, p99 a few mm per pose
```
If the fit is off (different proportions, A-pose vs T-pose, a different generation), weight transfer
will produce garbage. Tell the user instead of forcing it. posecheck's pose dictionaries use the Knight's
bone names; they're fine for any Kneel humanoid.

**Then, for each budget** (both cases; pass `rig=` only for A):
```python
lp = lowpoly.make_lowpoly(hp_obj, 15000, name='LP_15k', rig=rig)
print(lowpoly.deviation(hp_obj, lp))                  # max sets the bake cage
print(lowpoly.bake_normal(hp_obj, lp, r"<scratch>\N_LP_15k.png", src_normal_png=r"<download>\<base>_normal.png", rig=rig))
```
Bakes take seconds. `bake_normal` picks the cage from the measured deviation and restores materials and
the render engine afterwards. Sanity-check a bake: open it, or compare its stats to another bake.
Mostly flat blue, or big empty islands, means rays missed: raise `cage`.

Why plain smooth normals and MikkTSpace: the normal map is only correct if Unity rebuilds the same tangent
basis Blender baked against. `make_lowpoly` clears custom normals and sets all faces smooth, and the Unity
importer imports those normals and calculates Mikk tangents. Don't "fix" either side alone.

**Export:**
```python
lowpoly.export_fbx(lp, r"<scratch>\X_15k.fbx", as_name='EmberWarden', rig=rig)   # as_name = the object name Unity expects
```
For case A, `as_name` must be the existing mesh object's name, so Unity's object path and material slot stay
the same. For case B, export the mesh **unparented, with rotation and scale applied** (`bpy.ops.object.transform_apply`).
A mesh that relied on a parent's or the importer's rotation arrives in Unity lying on its back. Then check the structure against the original:
```python
rep, A, B = fbxcmp.compare(r"<backup>\Original.fbx", r"<scratch>\X_15k.fbx", skip_geom=True); print(len(rep), rep[:10])
```
Zero diffs is the bar. `lowpoly.EXPORT` reproduces Kneel's existing exports exactly
(FBX_SCALE_UNITS, -Z forward, Y up, no leaf bones, NULL armature node).

## 4. Textures

`python -I scripts/pack_textures.py "<download>/<base>" --name <Name> --out <scratch>/tex`
gives `T_<Name>_BaseColor.png` and `T_<Name>_MetallicSmoothness.png` (RGB metallic, A = 1 − roughness).
The normal map is the **bake**, copied as `T_<Name>_Normal.png`, not Meshy's. Meshy's map belongs to
the full mesh's tangents. Run the script on the download with `-I` and don't run anything from inside
the download folder.

Case A usually keeps its existing base colour and metallic textures (same UVs, and the decimation keeps
them). Check that Meshy's base colour is byte-identical to the shipped one before skipping it.

## 5. Unity import

**Case A (replace in place):**
1. `scripts/unity/rig_check.cs` with `MODE="snapshot"` on the current asset (before anything changes).
2. Copy the low-poly FBX over the existing `.fbx`, and the bake over the existing normal PNG (file system,
   same names, `.meta` untouched).
3. `rig_check.cs` with `MODE="verify"`: 0 bad transforms, rotations 0°, bindposes within ~2e-5, avatar
   valid and human, maxBonesPerVertex 4.

**Case B (new asset):** create `Assets/Kneel/<feature>/` (or reuse the feature's folder), copy the FBX
(`SM_<Name>.fbx`) and the three textures in, then run `scripts/unity/import_assets.cs`. It sets texture
types, sRGB, the normal-map flag, importer normals and Mikk tangents, makes `M_<Name>.mat` (URP Lit,
metallic-alpha smoothness), and remaps the FBX's material slots to it. Also check what Kneel needs from a prop:
- Meshy often adds a ground plate or plinth under props. Remove it in Blender before decimating:
  props never bring their own ground.
- A prop meant to block the player and aim needs a collider and the `Obstacles` layer (7). Walkable
  surfaces go on `Ground` (6).
- Check the scale: Meshy's size is arbitrary. Compare the bbox to the player (≈1.9 m tall) and scale
  in Blender before export, so the FBX imports at scale 1. `import_assets.cs` prints the mesh size:
  its Y should be the height. If the height shows up in Z, the export lost a rotation.

Check `read_console` for import errors after each step.

## 6. Compare in game (when choosing between budgets, or to show the user)

Use the real scene and camera, because how the asset is viewed decides what's visible. Character:
swap the player's renderer. Prop: spawn it in front of the player for the session only.

1. Copy candidates into `<asset folder>/_Compare/` and run `scripts/unity/import_variants.cs`
   (for case B, ship the first candidate via step 5 first, so there's a reference material to copy).
2. `scripts/unity/capture_start.cs`: L1 at 1920×1080, player frozen on one idle frame, paused.
   Poll for `Temp/meshy_cap_ready.txt`. Run once with `CAM_DISTANCE=0` (default 8.5 m) and once with
   `3.1681416f` (closest zoom). Separate Play sessions, because the camera has to settle. A spawned prop
   lands `SPAWN_AHEAD` m in front of the player, and in L1 only the lantern's pool of light is bright.
   Look at the first screenshot, and move it closer (~0.8 m) if it sits in the dark.
3. Reference first: `manage_camera` `screenshot` to `Temp/meshy_shots/<prefix>_full.png`, then
   alternate `capture_swap.cs` (next variant; its Step flushes the previous async screenshot) and
   `screenshot`. Finish with the reference again as `<prefix>_full2` to measure the noise floor.
4. `scripts/unity/capture_end.cs` twice: stop Play, then restore the Game view size and the scene that was open.
5. `python -I scripts/compare_shots.py --dir Temp/meshy_shots --prefix near --ref full --variants ... --noise full2 --box <player crop> --scale 2 --out ...`.
   Find the crop by diffing the reference against the lowest variant. Look at the sheet yourself
   before sending it (`SendUserFile`). Pixel counts rank the variants; your eye and the user's decide.

Report it as a small table (tris, Unity verts, px diff at both distances, what's visibly different) plus
a recommendation. Don't just say "looks good".

## 7. Ship and clean up

- Copy the chosen FBX + bake into place (case A: over the originals; case B: as the final names), delete
  `_Compare/` and `_Compare.meta` through the file system, refresh.
- Re-run the verification (`rig_check` verify / import_assets' printout) and a short Play test: for
  characters, draw the sword and walk (the cape and equipment ride on bones, so a broken skeleton shows
  immediately). Check the console.
- Show the user the result and the `git status` of what changed. Leave committing to them.
