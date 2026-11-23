# L1 Monastery report

The monastery from the prologue cutscene is now the start of L1 "The Broken Line". Kael wakes beside the broken relic in the flooded sanctuary, walks out through the inner door, the vestibule and the corridor of dead monks, and leaves by the main door onto L1's old start. Branch `task/L1Monastery`, cut from `origin/main` (b1919a73). Nothing is committed.

Status: Phase 0 (survey) and Phase 1 (the monastery) are done. Phase 2 (the wake-up) waits for the go-ahead.

Phase 1 review (2026-11-23):
- **Round sanctuary.** The owner asked for a much bigger, round sanctuary with the relic in the middle and the pillars, as in the cutscene's wide shot. It is now a round hall 32 m across, with a ring of 16 pillars. The corridor ("the hall is fine") is unchanged.
- **Flicker fixed.** The sharp light flicker came from too many shadow-casting spots: 11 monastery window spots plus Kael's lantern needed about 17 shadow slices in a 2048 atlas that holds 16 at URP's medium tier, so shadows dropped out frame to frame. Only the dusk door spot casts shadows now: 3 shadow-casting lights in the whole level.

Phase 1 review 2 (2026-11-23):
- **Candles removed.** The owner traced the remaining flicker to the candles; every candle stub is gone, and the prickets stand empty.
- **The hall is a tad bigger and grand:** 38 m across inside its wall, 20 clustered pillars 11 m to their capitals, the wall 11 m high with larger lancets, and a higher beam onto the altar.
- **Floors:** the flagstone floors are now one textured sheet instead of thousands of blocks, whose hairline joints crawled as the camera moved.
- **The battlefield is at the blue hour, matching the owner's reference image:** a cold key light, blue ambient and fog, a twilight sky, a cooler grade, cold ground mist, and puddles that catch the light as L2's do. See "Battlefield twilight" below.

Review shots, in the vault at `x/L1/Monastery/` (post-processing on):
- `00_Overview.png`: an overview from 40 m (not the gameplay distance), showing the hall's shape.
- From the gameplay camera (pitch 65, yaw 270, 8.5 m):
  - `01_Altar.png`: Kael at the wake spot beside the altar.
  - `05_HallFar.png`: Kael by the up-screen arcade.
  - `06_HallDoor.png`: at the inner door (the 32 m hall).
  - `10_Battlefield_E1.png`, `11_Battlefield_E2.png`, `12_Battlefield_Start.png`: the battlefield at twilight.
  - `02_Corridor.png`: mid-corridor.
  - `03_OuterDoor.png`: on the main door's threshold.
  - `04_Start.png`: the façade from L1's old start.

## Survey

- **Checkout and scenes.** Unity has `C:\dev\ClumsySword\Kneel` open; the OneDrive checkout is stale. On `main` the scenes are where the brief says:
  - Patrick's source scene: `Assets/Kneel/Levels/L1/Scenes/L1_BrokenLine.unity`.
  - The playable copy: `Assets/Kneel/Scenes/Levels/L1_BrokenLine_Playable.unity` (it was `Scenes/L1_BrokenLine_Playable_L.unity` until the level-music merge, #55, moved it).
  - The unmerged `task/LevelMusic` branch moves the playable copy to `Scenes/Levels/L1_BrokenLine_Playable.unity`. Git's rename detection should carry these edits over when the two meet.
- **L1Layout.**
  - Start is a circle at (0, 0), radius 6.5.
  - The painted ground is x from −52 (width 114) and z from −26 (length 412).
  - Camera: pitch 65, yaw 270, distance 8.5. Screen-up is −X, the camera sits on +X, and +Z runs left to right.
  - `MaxPropHeight(p) = 1 + tan 65° × d` (capped at 1.2 m near arenas). Camera-side walls use the brief's flat 0.9 m instead.
- **Framing.** At the target the frame covers about 8.8 m up-screen, 4.4 m down-screen and ±8.7 m along Z. In the middle of the sanctuary, the top of the frame meets the far wall only about 1.3 m above the floor. So high windows and vaults are mostly off-screen: the look comes from the floor, the water, pier bases, low lancets and light shafts.
- **L1 tools.**
  - Rebake NavMesh + Validate, and Generate Lighting: rerun.
  - Realism, Wayfinding, Scale, Look/Repaint Ground and Apply Lighting Pass: not rerun. Each would act on the new capsules (bodies scattered into the sanctuary, torches at the new bends) or undo later in-place edits (the seam road paint, the exit's red glow the transitions turned off).
  - No tool in the repo builds `L1_Ground`, `BoundaryColliders` or `Wreckage_Band`, so the new scene tool edits them in place.
- **Lighting.** L1 has baked GI off: 80 realtime lights and two reflection probes. "Baking lighting" means reflection probes and ambient only.
- **Pack assets.** Everything used is URP Lit; none of it is on CLAUDE.md's list of non-URP materials. The packs have no gothic pieces, no candles and no spear (details under Assets used).
- **Water.** The project's `Kneel/Water` shader (L2's creek) works under the PC renderer (Forward+, opaque and depth textures on). The Synty water materials are plain URP Lit.
- **Spawn, death and checkpoints.**
  - The scene's Player stands at (0, 0); `_Transition/PlayerSpawn` is there too.
  - On death, the pause menu's death screen offers Restart, which reloads the scene. `LevelRestartOnDeath` (on `LevelFlow`) only acts where there's no pause menu.
  - The L1 shrine (`ShrineRest`, flag `L1.Shrine`) is a checkpoint: once rested at, reloads put Kael there.
  - `SaveFlags` holds one-time story beats in the save (L5's reveal uses it).
- **Clips.** Kael's animator already has `KnockdownUp` (`knockdown-getup`, 2.7 s): it starts on his back, propped on his elbows, and he stands by 1.4 s. It also has `KneelDown/Idle/Up` (the shrine rest) and `KnockdownFall`. There's no get-up from lying on the side.

## Layout changes

All coordinates are world metres. The monastery lies on the −Z side of Start.

| Entry (L1Layout) | Shape | Notes |
|---|---|---|
| `Sanctuary` | Circle at (0, −52.5), r 15.2 | Inside the ring of pillars (r 16.2). The hall's wall is 40-sided, its inner face 19 m from the centre |
| `SanctuaryDoor` | Capsule (0, −37.5) → (0, −30.5), hw 1.25 | The inner door, 2.5 m wide at z −33 |
| `MonasteryCorridor` | Capsule (0, −31.5) → (0, −11), hw 1.4 | Vestibule x ±3.25 (z −32.5 … −28); corridor x ±2.25 (z −28 … −10.5) |
| `MonasteryExit` | Capsule (0, −11.5) → (0, −5), hw 1.25 | The main door at z −10, joining Start |
| `AltarFootprint` | Rect x −2.3 … 2.3, z −54.8 … −50.2 | The altar at (0, −52.5); nothing stands on it |
| `MonasteryHallCentre`, `MonasteryHallRadius` | Circle at (0, −52.5), r 20.1 | The hall's outer wall |
| `MonasteryBlocks` | 2 rects | The vestibule (x ±4.25, z −33.4 … −28) and the corridor (x ±3.25, z −28 … −9.5) |

- **Capsule names.** `Capsule` now takes an optional name; the old 5-argument constructor is unchanged.
- **Wake spot.** (1.4, 0, −49.6), beside the altar on the door side, facing +Z toward the inner door. It's the `WakeSpot` child of the prefab.
- **Walk.** The route from the wake spot to Start is 50 m on the NavMesh. At the committed walk speed (1.44 × 1.2 = 1.7 m/s) that's about 29 s, plus the wake-up.
- **The hall under the gameplay camera:**
  - **Up-screen (x < −4):** clustered pillars 11 m to their capitals carry pointed arches. The wall behind them is 11 m high with its lancets.
  - **Camera side:** the other pillars are broken stumps with their fallen drums, and the wall is cut to 0.9 m, stepping down as it comes round towards x 0.
  - **Why only up-screen pillars are tall:** a tall pillar can only stand between the camera and Kael if it is on his camera side. For a point inside a convex ring, that only happens on the ring's camera half.
  - **Screens:** low stone screens between the tall pillars and at both ends of their run close the ambulatory behind them, so Kael can't walk behind a pillar.
  - **Checked:** at every one of 16,472 walkable points (0.25 m grid), the gameplay camera sees his chest or head. None are hidden.
- **Ground.** `GroundMinZ` and `GroundLength` are unchanged, because they describe the painted layout texture. The `L1_Ground` mesh (shared by both scenes) now continues as the same 1 m grid down to z −96, mirroring the heights and texture north of z −26.
  - It dips to the floor within 6.5 m of the outer walls.
  - 1,330 one-metre cells are cut away under the floor.
  - The untouched original is kept as `Meshes/L1_Ground_PreMonastery.asset`; every run starts from it.
- **Corridor heights.** The far (up-screen) walls are 6.5 m. The camera-side walls are 0.9 m, each topped by an unseen cap (Ignore Raycast), because Kael can jump a 0.9 m wall. The façade stands 6.3 m only well up-screen and steps down before the door, because walls across the level are seen from right beside the camera. The doors are plain 2.5 m gaps, each with one heavy leaf standing open.
- **Each scene (by the tool):**
  - Removed 81 pieces of battlefield dressing inside the walls and in front of the door.
  - Lowered 72 pieces with the ground.
  - Removed 3 boundary boxes in front of the door; two new ones (`Boundary/MonasteryBoundary`) close either side of the façade.

## Assets used

The monastery prefab is `Assets/Kneel/Levels/L1/Prefabs/L1_Monastery.prefab`, built in world coordinates and placed at the origin under `L1_BrokenLine/Monastery` in both scenes. Its parts are in `Levels/L1/Prefabs/Monastery/`:

| Part prefab | What | Made from |
|---|---|---|
| `L1_Mon_Sanctuary` | The round hall: a 40-sided wall with lancets, 20 clustered pillars (tall with pointed arches up-screen, broken stumps with fallen drums elsewhere), screens closing the ambulatory | L4's coursed-stone kit (`L4Kit.Courses`, `L2PolyKit.Tread`); new pointed arches and clustered pillars (`L1MonasteryKit`, `L1Monastery.Hall.cs`) |
| `L1_Mon_Corridor` | Vestibule, corridor with four lancets and pilasters, the façade | The same kit |
| `L1_Mon_Floors` | One flat floor sheet (the round hall, thresholds, vestibule, corridor, the landing outside the door) under a generated, mipmapped flagstone texture (`L1_Mon_Flagstones.png`) | Generated (`L1Monastery.Floor.cs`) |
| `L1_Mon_Altar` | The octagonal two-step altar and basin, the relic split in two with shards | Generated (faceted lathe); convex collider |
| `L1_Mon_InnerDoor`, `L1_Mon_OuterDoor` | One plank leaf each with iron straps, studs and a ring, standing open | Plank boards (`L2PolyKit.Plank`), iron |
| `L1_Mon_Windows` | Glazed panes with iron cames | Generated |
| `L1_Mon_Dressing` | Empty pricket stands (one fallen), dust drifts, fallen stones. No candles (owner) | Generated, `L4Kit.Fallen` |
| `L1_Mon_Water` | One round sheet of still water over the hall and into the door, 8 cm above the floor, no collider, layer Water. No light touches it: it is alone on rendering layer 4, because lights left hard white glints on it. Its sheen is the vaulted reflection | `Kneel/Water` (copy `L1_Mon_Water.mat`, tuned teal, still and glassy) |
| `L1_Mon_Bodies` | Five monks and the guard with his spear | Variants below |
| `L1_Mon_Lighting` | A cold beam from above onto the altar (the hall's focal point; the relic itself gives none), window spots, light-shaft cards, the dusk door (the only shadowed light), cold fills, dust motes, the grade volume, the sanctuary reflection probe | URP lights; `L1_SoftShaft.png`; `L1_FX_Ash` |
| `L1_Mon_Colliders` | Walls on Obstacles at their seen height (NotWalkable), caps on Ignore Raycast, floors on Ground | Box colliders |

- **Dead bodies** (variants of L2's baked commoners and a skeleton, recoloured):
  - `L1_Mon_Monk_Slumped`: `L2_Corpse_Peasant_Slumped` (hooded) in dusty grey wool.
  - `L1_Mon_Monk_SlumpedB`: `L2_Corpse_Shopkeeper_Slumped`.
  - `L1_Mon_Monk_OnSide`: `L2_Corpse_Shopkeeper_OnSide`.
  - `L1_Mon_Monk_Bones`: `L2_Skeleton_Slumped` in dust.
  - `L1_Mon_Guard`: `L2_Corpse_Peasant_OnBack` (plain tunic, not dusted).
  - Placement: two monks against corridor pilasters, one against the low wall, the skeleton in the façade's corner, one on his side in the vestibule. The guard lies in front of the inner door, on the vestibule side.
- **Spear** (`L1_Mon_Spear`): PolygonAdventure `SM_Wep_Staff_01` with a faceted iron head. No pack has a spear.
- **Materials** (`Levels/L1/Materials/Monastery/`): cold blue-grey stone and floor palettes (the cutscene's), wood, iron, wax, dust, the obsidian relic, glass, two shaft materials, and dust-graded copies of the L2 commoner texture and bone.
- **Grade:** `Lighting/Volumes/L1_Zone_Monastery.asset`, a local volume (priority 3) that blends on Kael's position.
- **Reflection** (`Lighting/Textures/L1_Mon_SanctuaryReflection.exr`): the water reflects a closed, vaulted hall the camera never sees. For the bake only, the hall gets a full ring of tall pillars and walls (mirrored), a ribbed dome rising to an oculus, a vaulted ambulatory, and fan ribs from every pillar. The probe keeps that cubemap as a custom texture, so Generate Lighting leaves it alone.
- **Meshes** (`Levels/L1/Meshes/Monastery/`): the hall is 108k triangles and the floors 74; each part is baked into one mesh per material.

## Tools

New (`Kneel/L1/Monastery` menu, `Assets/Kneel/Editor/L1/`):
- `L1Monastery.cs`: **Build Prefabs** (materials, parts, baking, the prefab).
- `L1MonasteryKit.cs`: the gothic pieces.
- `L1MonasteryScene.cs`: **Place In Open Scene** (ground, dressing, boundary, the prefab; safe to rerun) and **Reshape Ground Only**.
- `L1MonasteryReflection.cs`: **Bake Sanctuary Reflection** (rerun after changing the sanctuary).
- `L1MonasteryShots.cs`: **Capture Review Shots** (to `Temp/Monastery`).

Rerun, in the source scene:
- **Rebake NavMesh + Validate.**
  - Paths: 22/22.
  - Sight-lines: 1423/1426 clear. The 3 blocked are the E3 dead tree from before; none are in the monastery.
  - Fog intrusion 0.
- **Generate Lighting** (17 s, probes and ambient only). Then both scenes were saved; the playable copy shares the NavMesh and lighting data.

One edit to an existing tool: L1's validator (`L1LevelTools.Validate`) no longer counts transparent renderers as occluders. Its own rule was that light-shaft cards never hide anything, but it only exempted those under `_Lighting`. Shaft cards and water never hide Kael.

Checked:
- **NavMesh.** Every room is reachable from Start (the far side of the hall is 52 m away). The outside of the monastery isn't.
- **Flood-fill.** A capsule of Kael's size (radius 0.21) reached 892 m² and nothing outside: no seam leaks.
- **Camera sight.** At every walkable point, the gameplay camera sees Kael's chest or head: 0 of 12,237 hidden.
- **Lights.** 3 point and spot lights in the whole level cast shadows (the dusk door, Kael's lantern, one of L1's own).
- **Walking lines.** The corridor, both doors and the exit are clear of props.
- **Console.** No errors.

## Battlefield twilight

The owner's reference: a cold blue-grey twilight over the field, mist, puddles holding the light, and Kael's own light the only warmth. `Kneel/L1/Look/Apply Twilight` (`L1Twilight.cs`) sets it, and is safe to rerun.
- **In each L1 scene:**
  - The key light is cold (#A3BAE0), soft (0.75, shadow strength 0.5) and higher (32°, still from the north-west). The old one was an amber sun 4° up.
  - Blue trilight ambient.
  - Blue-grey linear fog from 7 m to 62 m.
- **Shared assets:**
  - The twilight sky (`L1_DuskSky`).
  - The global grade: blue split-toning, white balance −16, saturation −22.
  - The Start zone's exposure lifted from +1.05 to +1.3, so outside the monastery door is no longer the darkest spot.
  - The ground mist cards cold blue-grey.
  - L1's look in `LevelTransitions.asset` (L2's arrival leans toward it).
- **Puddles, made as L2's** (the owner: "make them like in L2"). L1's ground now uses L2's shader, `Kneel/Ground`, instead of URP Lit:
  - **How they're drawn.** The shader raises a water level over the mud and fills its hollows, so each puddle has a crisp, ragged waterline at screen resolution, not at the layout's 11 cm texels. Inside: flat, glossy water (smoothness 0.88) that mirrors the lights, a darker damp halo round it, and a faint cold sky sheen (`_PuddleSky`), so a pool still reads away from any light.
  - **Where.** The layout's alpha is now wetness, as that shader reads it. It is dry (0.04) everywhere except the pools painted in the terrain's dips and over some flats (0.14, 8.5% of the ground) and a damp shore round them (0.11). `_PuddleAmount` 1 turns off the shader's own random scatter, so water stands only where it's painted.
  - **Dry ground must stay under the shader's wet threshold (0.08).** Above it, the water rises into every pebble hollow, a glossy speck per hollow across the whole field. The first pass did this.
  - **Mud.** `L1_Mud_Soft.png` is L2's mud texture with its colour pulled 65% toward its mean (the height in alpha, which shapes the waterlines, is kept), and its bumps are at 0.15. At L2's full strength, Kael's lantern grazes the pebbles into a rash of bubbles. The layout's colour is divided by the mud's mean, which the shader multiplies back in, so L1 keeps its own colours.
  - **No cobbles or moss:** no paving mask, and `_MossAmount` 0.
  - **Painted from an untouched copy.** That copy is `Editor/L1/LayoutSources~/L1_Ground_Layout_PrePuddles.png`. If the layout is ever repainted (`L1Look`, or Transitions' Build Seam 1), delete that copy and rerun.
- **Kept:** the fires and torches (wayfinding) keep their warm light; the monastery keeps its own grade.

## Wake-up implementation

Done (2026-11-23). Shot: `x/L1/Monastery/13_WakeUp.png` (the opening frame, and Kael from lying to standing).
- **Where.** A new script, `Levels/L1/Scripts/L1WakeUp.cs` (namespace `Kneel.Levels`), on `LevelFlow/WakeUp` in the playable scene only. It points at the prefab's `WakeSpot` (1.4, 0, −49.6) and faces the altar (`Altar/AltarCollider`). Patrick's source scene has nothing added.
- **Sequence** (about 7.2 s, on game time, so the pause menu freezes it):
  1. **0 s.** Black, fading out over 3 s. Kael lies on his back in the water beside the relic, feet toward it, with his scar glowing faintly and pulsing slowly like breath.
  2. **Opening shot.** A wide shot of the round hall at the follow camera's own yaw (270°): pitch 58°, 19 m out, pushing in to 15 m while he lies there. It was 27 → 21 m at first, which showed too much of the ground outside the walls (owner).
  3. **5 s.** He gets up. This is `knockdown-getup` from 0.3 s to 1.8 s, slowed to 2.2 s. The camera eases down into the gameplay follow over 3 s, and the scar dims.
  4. **The last 0.45 s** blends into his standing idle. Control returns, and the save flag is set.
- **Hooks, no gameplay code changed.** It drives the player from outside:
  - **`PlayerCombat.Restrict(…, 0)`** each frame ignores every action and stops walking.
  - **`PlayerMovement`'s per-frame flags** lock turning, jump and sprint. Without the turn lock, the mouse would spin him on the floor.
  - **The `FullBody` layer's weight** holds `PlayerCombat`'s own `KnockdownUp` state.
  - **`[DefaultExecutionOrder(-5)]`** puts these writes between `PlayerCombat` (−10, which writes the layer weights) and `PlayerMovement` (0, which reads the flags).
  - **`PlayerScar.RestIntensity`** for the glow, and **`PlayerCamera.SetOverride` / `ClearOverride`** for the shot.
  - **The fade** is its own overlay canvas (order 400: over the HUD, under the pause menu).
  - **The state is started by normalized time.** A fixed-time offset into a state frozen at speed 0 comes out as 0, so the first build started the get-up from the clip's beginning, and Kael was still crouched when control came back.
- **Retries.** The save flag `L1.WokeUp` (`SaveFlags`) is set when he stands. A death reload, Restart, Continue or the Levels screen then skips the wake-up, and he starts where the scene already puts him: L1's old start, or the L1 shrine he rested at. It's also skipped when arriving through a level seam (`LevelTransition.IsArriving`). New Game deletes the save, so it plays again. Leaving mid-wake releases the camera and the scar.
- **Tested in Play mode**, with the flag in the `SaveFlags` sandbox (the real save was compared before and after, and is unchanged):
  - He's placed at the wake spot and held there.
  - The clip holds at 0.30 s, then runs 0.30 → 1.72 s over the get-up.
  - The layer blends 1 → 0, and control returns at 7.2 s.
  - Forward input then walks him 2.3 m.
  - A scene reload after waking skips it: he starts at (0, 0, 0), unrestricted, with no camera override and no fade.
  - No console errors.
- **Tuning** is on the component: `fadeIn`, `lieTime`, `getUpTime`, `blendOut`, `openingShot` (on/off), `shotPitch`, `shotDistanceStart/End`, `cameraRelease` and `scarGlow`.

## New Game intro

Added 2026-11-23. Owner: New Game plays `kneel_intro.mp4` first, then a fade to black, text on screen, then Kael waking up in L1.
- **Flow.** New Game → `Intro` scene:
  1. The prologue film, 50.8 s. It ends on Kael lying on his back in the sanctuary's water.
  2. A fade to black.
  3. A card: "You awaken some time later" (the owner's words).
  4. L1, where the wake-up fades in on the same pose.
- **Skipping.** Pressing a skip key once shows "Press again to skip", and a second press skips the film. The card always plays.
- **Files.**
  - `UI/Scripts/IntroSequence.cs` and `Editor/UI/IntroSceneBuilder.cs` (menu `Kneel/UI/Build Intro Scene`) are new.
  - `UI/Scenes/Intro.unity` is generated by the builder.
  - The film is `UI/Video/kneel_intro.mp4`, 122 MB, through Git LFS.
  - Two one-line edits: `MainMenuController` (New Game goes to `introScene`, falling back to L1 if it's missing from the build) and `PauseMenu` (no pause menu in the intro).
- **Tested in Play mode** (muted, flags sandboxed, the real save unchanged):
  - The film plays: frames checked at 3 s and 20 s, and it shows full-screen.
  - A full run: the film plays all 50.8 s and fades 1.5 s before its end, then the card, then L1 activates. The loader's black lifts and the wake-up fades in on Kael lying by the relic.
  - Two bugs found and fixed:
    - The level stayed black after the card. The fade back ran on the intro object, which is destroyed with its scene.
    - The film could be skipped at once, because the video reports not playing for a moment after `Play()`.
  - The cursor is hidden, and there's no pause menu in the intro.
  - Not run through the real main menu, because New Game there would delete the owner's save.
- **Gaps.**
  - Windows' video decoder warns that the film's colour primaries are unknown, so colours may shift slightly. Re-encoding the mp4 with BT.709 tags, or transcoding it in the importer, would settle it.
  - 122 MB is large for the repo. A lower-bitrate encode would cut it.

## Water steps and murk

Added 2026-11-23 (owner: ripples where he steps in the monastery's water, the water murkier, like the cutscene, and a sound for stepping in water and puddles).
- **Murky teal water.** In `L1_Mon_Water.mat` (and its builder, `L1Monastery.cs`):
  - The colour is teal, `_DeepColor` (0.075, 0.27, 0.28).
  - `_Murk` is 38 per metre, up from 6, so the flags are hidden a few centimetres down.
  - It still mirrors the windows and the oculus beam.
- **Ripples and splashes.** `L1WaterSteps` (new, `Levels/L1/Scripts/`) is on `LevelFlow/L1_WaterSteps` in the playable scene only.
  - **When.** It splashes on each of Kael's steps, and on landings, in the same frame as his footstep sound. It reads `PlayerMovement`'s private stride counter by reflection and triggers when that resets, so the player's code is unchanged. If the field is ever renamed, it counts strides itself.
  - **Where.** It checks the lower foot:
    - The hall's water: a 19.4 m circle, plus the strip into the inner door.
    - The battlefield's puddles: `L1_WetMask.png`, baked from the ground layout's wetness (8.2% of the ground, a 44 cm grid).
  - **What.**
    - One soft, broken ring per step, widening to about 1.3 m and fading over 1.1–1.4 s.
    - 4–11 droplets thrown up that fall back to the surface.
    - A splash sound over the step. Puddles get 70% of the hall's strength.
- **The sound.** Ten single steps cut from four CC0 Freesound recordings of footsteps in puddles and shallow water, which the owner downloaded (credited in `Audio/CREDITS.md`). They are `Levels/L1/Audio/FootstepWater/L1_WaterStep_*_fs<id>.wav`, each starting on the foot's contact, faded and matched in level, 44.1 kHz mono. They play through a sound event, `L1_FootstepWater.asset` (SFX group, flat like the footsteps, ±10% pitch). They replaced a first, synthesized set.
  - The cuts are five squishy puddle steps and four puddle steps (patchytherat), a shallow-water splash (ChristopherJngs), and a deeper step in mud and water (Alex_hears_things).
  - Two other cuts were dropped: one was mostly background raised by the levelling, and one had the next step in its tail.
- **Tool.** `Kneel/L1/Water Steps/Build` (`Editor/L1/L1WaterStepsBuild.cs`) rebuilds the sound event from the clips in that folder, the ring and droplet textures and materials (URP unlit particles), the mask and `Prefabs/L1_WaterSteps.prefab`, and places it in the playable scene. It is safe to rerun. Rerun it if the ground layout's puddles are repainted (`L1Twilight`).
- **Tested in Play mode** (muted, flags sandboxed, the real save unchanged):
  - Walking through the hall: 18 splashes in 6 s, two per stride, all at the water's surface (y 0.08).
  - None in the dry field.
  - In a puddle on the E2 field: a ring and droplets, at the ground plus 1.2 cm.
  - No console errors.

## Gaps and placeholders

- **Only the up-screen half of the ring stands tall.** Under the gameplay camera the full ring can't be seen at once (from the middle of the hall the frame reaches about 9 m up-screen), and the camera-side pillars have to be cut down. The overview shot shows the whole shape. The wake-up's high opening shot shows the up-screen arcade, its lancets and the altar under the oculus beam; the cutscene's full ring and fan vaults live only in the water's reflection.
- **Wake-up details.** He makes no sound as he wakes, no water drips from him, and the water doesn't ripple round him. The lantern and cape aren't simulated against the floor, so while he lies on his back they hang through it, hidden under the floor.
- **No arches on the doors.** The doorways are plain gaps with one open leaf, because walls across the level are cut down where the camera passes them.
- **Water steps.**
  - **Water sounds in the mix.** The steps were chosen and levelled from waveforms and spectrograms; I can't hear them, so judge their level against the earth step in game (the event's `volume`, 0.6).
  - **The step's own earth sound** still plays under the splash. `PlayerMovement` picks earth or stone for the whole level; skipping the dry step in water would mean editing it.
  - **No ripples while he gets up in the wake-up:** he takes no steps then.
- **Footsteps and ambience.** These are still L1's battlefield earth and score inside the monastery.
- **Exterior.** Beyond the low camera-side walls the ground lies in the walls' dusk shadow, and reads as a dark band at the foot of the frame in the corridor.
- **The door at night.** The main door's warm light leads the corridor, but the battlefield outside keeps L1's dark Start grade.
- **Battlefield extras.** The reference also has spears and banners standing in the field, crows, and ruins on the horizon. None of these were added: this pass covered the lighting and the ground.

## Questions

1. **Opening shot. Answered (2026-11-23): keep it.** The wake-up opens on a high, wide shot of the round hall and settles into the gameplay camera as Kael gets up, through the camera override. `openingShot` on `LevelFlow/WakeUp` turns it off.
2. **Wake pose.** The brief says both "on his back" and "on his side". He lies on his back, because that's how `knockdown-getup` starts. Download a get-up from the side only if you want that.
3. **Sound.** Should the L1 score wait until he's outside (it plays from the first frame)? That would mean a small hook into `LevelAudio`.
