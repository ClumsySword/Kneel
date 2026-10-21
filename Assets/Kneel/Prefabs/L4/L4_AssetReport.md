# L4 The Burning Gate: asset report

Built from `../Obsidian/02-Design/Levels/L4 The Burning Gate.md` (the "Stronghold" plan, revised 2026-10-18 for Iron Husk and Cursed Twin). The note has no build handover, so this report derives one from its sections 4–12, following L3's phases:

| Phase | Work | Output | Gate |
|---|---|---|---|
| 0. Survey | Read-only look at the project | The Survey section below | Done 2026-10-18 |
| 1. Assets | Find or build every row of the asset manifest. Lay them out in `L4_AssetReview` | The review scene and this report | Done 2026-10-18. **Stopped for the owner's approval** |
| 2. Blockout | Build the level from the blockout spec, one stage at a time (the note's greybox order, section 12) | `L4_BurningGate.unity`, a baked NavMesh, an entry per stage | Built 2026-10-19 through stage G; zigzag and side paths approved; finished and checked the same day (see "Phase 2: the blockout as built" and "Phase 2 finished"). Nothing committed |
| 3. Refinement | The owner's call, 2026-10-19 ("I want refinement phase now"): 3a the surroundings, so the stronghold stands in a land rather than in fog; 3b an area-by-area visual pass judged in the Game view (dressing, damage, light); 3c the open checks (the level's Play-mode playtest) | The level scene, look-dev captures per step | 3a and 3b built 2026-10-19 (see "Phase 3: refinement"); 3c (the level's Play-mode playtest) open. Nothing committed |

**Owner decisions, 2026-10-18**

1. Work in L3's phases, with the specs derived here rather than written into the note.
2. R = 2 m as written (the roll is 3 m; same call as L3). No 1.5 scale.
3. Newcomers are [[Iron Husk]] and [[Cursed Twin]]; the note's Shield Knight and Oil-Thrower are cut. The encounter redesign was written into the vault note (decisions 5, 6, 17, 18 and sections 1–13).
4. The heavy weapon is a pickup before the first Husk (E1).
5. Oil is lit by the environment (cressets, lamps, braziers), not by an enemy.
6. Enemies are spawn markers with stand-ins this pass. Behaviour comes later.
7. Art base: the PolygonKnights castle kit, re-graded and broken up, with look-dev on one yard before going level-wide.
8. Branch `task/L4-BurningGate`, cut from `task/L3-FallowFields` (L3 is not yet on `main`; PR #8 brings it there).
9. Look-dev review: the look is right ("very Elden Ring-ish"); no drawn circles on the ground. Murder holes are marked by pitch painted into the floor.
10. After the review: add wall damage, fix the sky, then build the Phase 1 prefabs and the review scene (done).
11. Asset review, 2026-10-18/19: C01, C04a/b, C06, C12, F06, S02, S05 and S09 revised (rows below): coffins modelled as hollow joiner-made boxes, the tumbril dead laid out under a shroud, the statue broken at the thighs, the gatewarden's wall behind him, a stone guardroom with an arcade, a chapel with buttresses, octagonal columns, an arcade and a chancel, the throne portal of three orders between towers. Then: "a lot of holes in all the bricks" seen straight on. Every course of blocks (`L4Kit.Courses`), every arch (`GateArch`) and every flagged floor (`Flags`) now has a dark mortar core set back from its faces, so the joints read as grout, not daylight; the throne door has a wall behind its arched frame.
12. Blockout review, 2026-10-19: "more interesting rather than just a straight line". The route is a zigzag spine (see Phase 2); short down-screen legs are allowed where needed (none so far, so never in an arena); built directly, the vault note follows.
13. Side paths, 2026-10-19: "even more paths to the side of the corridor like map. Dead ends with Lore or health pickups. around 3 of those." Three dead ends added off the spine, in plain sight (no handprint): D4 the ravine lookout off the apron, D5 the postern guardroom off the west lane, D6 the infirmary garth off the cloister (see Phase 2).
16. Refinement, 2026-10-19: "Make the Scene feel Epic and grand. Completely the castle at the end. It should like a final boss building. Refine blackout so it looks polished - like a triple A game. Continue refinement until this criteria is met." Built as 3b below: the citadel, the burning, the sky and grade, the aftermath.
15. Refinement phase, 2026-10-19: "I want refinement phase now. And I also want something along the edges of the stronghold so it does not look like the stronghold is floating in an abyss." Phase 3 starts with the surroundings (3a), shown as look-dev before going further.
14. Side paths approved, 2026-10-19 ("Looks good. Continue"): Phase 2 finished. The 15.4 NavMesh rules, the vista values and the stage checks, plus the fixes they turned up (see "Phase 2 finished").

**Rules carried over from L3's handover** (rules 1–11 of L3's note), with this level's names: the camera is final; plan x = Unity X, plan y = Unity Z, height = Unity Y, one root `L4_Root` at the origin; packs and existing gameplay code are read-only; prefabs, not loose objects (floors, ramps and the ravine are scene geometry); size from the note, looks from the pack; read back what is made; no silent substitutes; design changes go under Questions; no commits unless the owner asks. Plus the owner's standing art rules (no visible Unity primitives on props, no prop brings its own ground, organic rather than ruler-straight, look-dev shown early, judged in the Game view under the level's grade).

## Survey

The engine, packages, folder conventions, layers, NavMesh agents and gameplay hooks are as L3's survey records them (`Assets/Kneel/Prefabs/L3/L3_AssetReport.md`, "Survey"). Re-checked on 2026-10-18, with what is new or matters for L4:

| | Found |
|---|---|
| Unity / URP | 6000.6.0f1, URP 17.6, `PC_RPAsset` Forward+ |
| ProBuilder | Not installed. Named fallback: scaled kit pieces and scaled cubes for unseen collision. |
| AI Navigation | 2.0.14 |
| Cinemachine | Installed but unused. The camera is `PlayerCamera`. The note's confiners, vcams and blends become data-only marker volumes, as in L3. |
| Camera | Unchanged since KAN-12 (`PlayerCameraSettings`: pitch 65°, yaw 0, distance 8.5 m, FOV 60°). Up-screen is +Z. At full zoom-out it shows **about 20 × 13 m of ground, 8.8 m up-screen** of the player. The note assumes 32 × 20 m by default and a 48 × 30 m arena framing. See Questions 1. |
| Roll | 3 m (`dodgeDistance`), 0.85 s. |
| Heavy attack | **Not in the player.** `PlayerCombat` has Strike only. The ten-pager binds Heavy attack to Q. The heavy-weapon pickup is a marker this pass. |
| Enemies | `AshenFootman` (state machine on a `NavMeshAgent`, no collider). `TargetDummy`. L3's hound and brute stand-ins (collider only). Nothing else. |
| Gameplay pieces to reuse | L3: `ILevelResettable`, `HazardContact`/`HazardSensor`/`IHazardTarget`, `HazardTuning`, `GibbetAmbush` (pattern for the kneeler ambush), `SluiceGate` (pattern for one-time gates), `DitchKill` (ravine and breach), `FireCell` and L2's `L2Fire` flames (burning oil), all the `Kneel.Markers` components. L2: `GateLever` and `ShortcutGate` (Interact on E), `OcclusionFade` / `OcclusionFader` (the note's dissolve). L1: the checkpoint shrine. |
| Layers | 6 Ground, 7 Obstacles, 8 Enemy, 9 Player, 10 FadeProbe, 11 Hazard. The note's `Occluder` maps to L2's `OcclusionFade` on `Obstacles` with fade probes on `FadeProbe`. The note's `CameraBounds` layer is not needed while the camera has no confiner. |

**PolygonKnights castle kit** (measured from the prefabs; X × height × Z in metres)

| Piece | Size | Use in L4 |
|---|---|---|
| `SM_Bld_Castle_Wall_01` | 5.4 × 5.1 × 2.2 | Curtain faces. Its 5.1 m height is T+1's 5 m. |
| `SM_Bld_Castle_Wall_Gate_01` | 5.4 × 5.1 × 2.4 | Gate walls, doors through curtains |
| `SM_Bld_Castle_Tower_Wall_Top_01` | 5.0 × 1.4 × 2.5 | Merlons and crenels on outer parapets |
| `SM_Bld_Castle_Tower_01`–`04`, `_Base`, `_Top`, `Round_*` | 2.8 × 3.0 × 2.8 stackable | Turrets, the keep, the spire stack |
| `SM_Bld_Castle_Roof_Spire_01` | 3.0 × 5.5 × 3.0 | The keep spire's cap (scaled) |
| `SM_Bld_Castle_Wood_Battlement_01`–`04` | 5.3 × 3.7 × 4.7 and 3.1 × 3.6 × 2.1 | The E2 hoarding, hoardings over murder holes |
| `SM_Bld_Castle_Iron_Gate_01` | 2.5 × 2.9 × 0.1 | Portcullis grid (widened and repeated), grilles |
| `SM_Bld_Castle_Door_01` | 3.4 × 3.1 × 0.7 | Postern, guardroom and tower doors |
| `SM_Bld_Castle_Pillar_01` | 0.8 × 5.1 × 0.6 | Chapel colonnade, loggia arcade |
| `SM_Bld_Castle_Arrow_Slit_01` | 0.4 × 1.1 × 0.2 | Tower dressing |
| `SM_Bld_Church_Room_01`–`03`, `Tower_*`, `Door`, `Window`, `Extension` | rooms 6.4 × 6.4 × 8.8 | Chapel walls (cut, roofless), the split belfry |
| `SM_Bld_StairsLarge_01`/`02`/`Corner` | 2.9 × 2.0 × 3.9 | Every stair (scaled to 4 m wide and 2.5 m rise per flight) |
| `SM_Bld_Rockwall_*` | 1.5–1.8 × 2.7 × 4.0 | Ravine walls, rubble, causeway |
| `SM_Prop_Brazier_01` | 0.5 × 1.9 × 0.5 | Braziers (E6c tripods, the throne-hall doors) |
| `SM_Prop_Statue_01`, `_Base_01` | 1.3 × 3.8 × 1.7 | Toppled statue (E6a), broken statues (E6c) |
| `SM_Prop_Banner_01`–`03`, `Grate_01`, `Trapdoor_01`, `Beam_01`, carts, crates | | Dressing, murder-hole grates, the crypt trapdoor |
| `SM_Bld_Tent_01`–`03` | 7.5–11.5 m | The foreign siege camp (vista V3b) |

**Earlier levels' pieces to reuse.** L1: kneeling, slumped and fallen corpses in both colours (the kneelers and the Kneeling Court), `L1_BatteringRam`, `L1_SiegeLadder`, `L1_Torch`, wagon barricades, the checkpoint shrine. L2: skeletons, peasant corpses, `L2Fire` flames, `L2PolyKit` worn stone and planks, the loot pickups, debris. L3: carts, the well, the gibbet, the shrine wrapper, `L3Bones` (for a horse skeleton), the markers.

**Not in any pack:** cauldron, bell, ballista, coffin, pew, chandelier, mantlet, gallows, scaffold, cresset, a large portcullis, kennels, a cracked church bell, coin piles. Those rows are `BUILT`.

## Look-dev (2026-10-18, approved by the owner)

`Kneel > L4 > Look-dev > Build Courtyard Corner` builds one corner of the E3 courtyard in `L4_AssetReview` (group `LookDev`), from nothing each time: the north curtain with its wall-walk, merlons and three machicolations, the NE tower, the east wall under the terrace parapet, rubble, a cart, a brazier and three pour rings, with the player and a camera at the gameplay framing. Captures are in `Temp/L4LookDev`.

- **Stone.** PolygonKnights palette re-graded to a faintly cool grey (`L4Look`, `L4_Knights_Stone`; `L4_Knights_Soot` for burnt timber). Curtain faces are the pack wall at about its own scale (6 m pieces, closed with a 4 m cut).
- **Yards.** Generated dressed flagstones in courses (`L4Paving`, 4 m tile) on `Kneel/Ground`, breaking into ash-brown trodden earth at wall feet, round the pour rings and in worn patches (a per-floor paving mask, with the layout colour following it). L2's cobbles read as crazy paving here and were dropped.
- **Ruin.** Pitch runs down the wall under each machicolation, soot along the wall feet, fallen dressed blocks and dark stones in clusters at the feet.
- **Light** (`L4LightingPass`). Sun at 52° from the south-south-west (a true 60°+ noon left the walls the camera sees half as bright as the yards), grey smoke ambient, fog from 34 m, ACES with a grey-blue / pale-gold split tone, light vignette.

**Owner's review, 2026-10-18:** the look is right ("very Elden Ring-ish"), but no circles on the ground. The pour rings became splashes of old pitch painted into the floor material (`L4Paving.Splash`: an uneven lobed core, flecks thrown outward, a wet sheen through the shader's wetness), each turned and sized differently. Their reach is still the pour's 1.5 R. No transparent decal lies on a floor: one drew a straight-edged veil across the yard in the Game view (cause not pinned down; it was not shadows, SSAO or the decal's own texture). Wall decals (pitch runs, soot) do not show the problem.

Known gaps: the walls are still whole (no broken merlons, cracks or patched breaches yet); the sky renders black in wide shots; the earth carries L2's pale pebbles.

## Asset manifest (Phase 1)

Footprints are X × Z × height in metres, as L3's table. Every arena dimension is a multiple of 2 m (section 12). Status after Phase 1 is one of FOUND, ADAPTED, BUILT, CUT. "Where" gives the encounter or area from the note.

### 14.1 Look

Grey stone, black pitch, ember-orange oil fire, brazier gold at the end, under harsh noon light through smoke (frontmatter palette). The pack palette is re-graded (HSV, as `L2Look` and `L3Look` do), not tinted. The review scene carries the level's lighting pass and grade from the first capture: a high, slightly southern sun so camera-facing faces are lit, cold smoke-coloured fog only beyond the walls, warm fire as the only saturated colour. Soot and scorch on wall bases, rubble at every wall foot, broken merlons, burned timber; no ruler-straight runs of identical pieces.

The manifest as built: 113 rows, one prefab each except H11 (a component). Spec is the manifest footprint; read back is measured from the saved prefab's renderers (it includes overhangs, particles excluded, and for wrapped L1/L3 set pieces their own size). Prefabs are under `Assets/Kneel/Prefabs/L4/<folder>`; generated meshes under `Assets/Kneel/Meshes/L4` (`Kit` baked per prefab, `Figures` for the baked Synty poses); materials under `Assets/Kneel/Materials/L4`. Builders: `L4Assets` (+ `L4Props`, `L4Hazards`, `L4Structures`), `L4Kit`, `L4Shapes`, `L4Paving`, `L4Look`, `L4Review`, `L4Playtest`; runtime code in `Assets/Kneel/Gameplay/Hazards` (`Kneel.Hazards`).

### 14.2 Wall kit

| ID | Prefab | Status | Spec (x × z × h) | Read back (renderers, x × z × h) | Layer | Source | Notes |
|---|---|---|---|---|---|---|---|
| W01a | `Kit/L4_CurtainFace_6.prefab` | ADAPTED | 6 × 2 × 5 | 6.2 × 2.0 × 5.0 | Obstacles | PolygonKnights SM_Bld_Castle_Wall_01 | The pack curtain wall (5.4 x 5.1 x 2.2) fitted to 6 x 5 x 2, x1.11 along the run; runs close with the 4 m and 2 m cuts. Smoke soot along the foot of the yard face. |
| W01b | `Kit/L4_CurtainFace_4.prefab` | ADAPTED | 4 × 2 × 5 | 4.2 × 2.0 × 5.0 | Obstacles | PolygonKnights SM_Bld_Castle_Wall_01 | The pack curtain wall (5.4 x 5.1 x 2.2) fitted to 4 x 5 x 2, x0.74 along the run; runs close with the 4 m and 2 m cuts. Smoke soot along the foot of the yard face. |
| W01c | `Kit/L4_CurtainFace_2.prefab` | ADAPTED | 2 × 2 × 5 | 2.2 × 2.0 × 5.0 | Obstacles | PolygonKnights SM_Bld_Castle_Wall_01 | The pack curtain wall (5.4 x 5.1 x 2.2) fitted to 2 x 5 x 2, x0.37 along the run; runs close with the 4 m and 2 m cuts. Smoke soot along the foot of the yard face. |
| W02a | `Kit/L4_Merlon_Whole.prefab` | BUILT | 2 × 0.6 × 1.8 | 2.0 × 0.6 × 1.8 | Obstacles | L4Kit worn stone blocks (L2PolyKit.Tread) in a palette sampled from the graded castle stone | One 2 m module of the outer parapet: a 1.2 m merlon on a 0.9 m crenel sill. Whole. The collider is the full 1.8 m on every variant, so the player cannot jump the parapet where it is broken. |
| W02b | `Kit/L4_Merlon_Chipped.prefab` | BUILT | 2 × 0.6 × 1.8 | 2.0 × 0.6 × 1.6 | Obstacles | L4Kit worn stone blocks (L2PolyKit.Tread) in a palette sampled from the graded castle stone | One 2 m module of the outer parapet: a 1.2 m merlon on a 0.9 m crenel sill. Top course knocked about. The collider is the full 1.8 m on every variant, so the player cannot jump the parapet where it is broken. |
| W02c | `Kit/L4_Merlon_Stub.prefab` | BUILT | 2 × 0.6 × 1.8 | 2.0 × 0.6 × 1.4 | Obstacles | L4Kit worn stone blocks (L2PolyKit.Tread) in a palette sampled from the graded castle stone | One 2 m module of the outer parapet: a 1.2 m merlon on a 0.9 m crenel sill. Broken to one course above the sill. The collider is the full 1.8 m on every variant, so the player cannot jump the parapet where it is broken. |
| W02d | `Kit/L4_Merlon_Gone.prefab` | BUILT | 2 × 0.6 × 1.8 | 2.0 × 0.6 × 0.9 | Obstacles | L4Kit worn stone blocks (L2PolyKit.Tread) in a palette sampled from the graded castle stone | One 2 m module of the outer parapet: a 1.2 m merlon on a 0.9 m crenel sill. Merlon gone, sill only. The collider is the full 1.8 m on every variant, so the player cannot jump the parapet where it is broken. |
| W03a | `Kit/L4_ParapetLow.prefab` | BUILT | 2 × 0.5 × 0.9 | 2.0 × 0.5 × 0.9 | Obstacles | L4Kit worn stone blocks | Two courses of blocks, 0.9 m. An unseen cap (Ignore Raycast) raises its collider to 2 m: the player can jump 1 m walls. |
| W03b | `Kit/L4_ParapetLow_Broken.prefab` | BUILT | 2 × 0.5 × 0.9 | 2.0 × 0.5 × 0.9 | Obstacles | L4Kit worn stone blocks | As W03a with the top course knocked about: blocks missing and shifted. Same collider and cap. |
| W04 | `Kit/L4_CutWall.prefab` | BUILT | 2 × 2 × 5 | 2.0 × 2.0 × 5.0 | Obstacles | L4Kit worn stone blocks; PolygonKnights SM_Bld_Castle_Wall_01 (shadows only) | Camera-side yard wall: 0.9 m of blocks with a broken top edge is what renders; the full 5 m wall above it is a shadows-only renderer, so the yard keeps its shadows. The collider is the whole wall. |
| W05 | `Kit/L4_RubbleLow.prefab` | BUILT | 2 × 2 × 0.8 | 1.9 × 2.2 × 1.0 | Obstacles | L4Kit fallen blocks; PolygonAdventure rocks | A heap of fallen dressed blocks and dark stones, 0.8 m. Collider 0.8 m plus an unseen cap to 2 m. |
| W07 | `Kit/L4_TowerShell.prefab` | ADAPTED | 10 × 10 × 9 | 10.0 × 10.3 × 10.8 | Obstacles | PolygonKnights SM_Bld_Castle_Wall_01 (two storeys), SM_Bld_Castle_Door_01; L4Kit merlons | Four faces of curtain wall in two storeys round a 10 m square, broken merlons on top, a door in the south face. Colliders on the walls only. Every renderer carries the project's OcclusionFade, so the tower fades when it hides the player. |
| W08a | `Kit/L4_GateArch_8.prefab` | BUILT | 12 × 2.4 × 8 | 12.0 × 2.6 × 8.1 | Obstacles | L4Kit voussoirs and courses | An 8 m gateway: 2 m piers, a segmental arch springing at 4 m and rising 2.5 m (6.5 m clear at the crown), haunches coursed to 8 m. A chalk-white studded slot groove across the opening. The pack's gate wall has a 2 m door and would not scale. |
| W08b | `Kit/L4_GateArch_12.prefab` | BUILT | 16 × 2.4 × 9 | 16.0 × 2.6 × 9.1 | Obstacles | L4Kit voussoirs and courses | The 12 m outer arch of the barbican: springing at 4.5 m, rising 3 m (7.5 m clear), coursed to 9 m. Slot groove as W08a. |
| W09a | `Kit/L4_Stair2R.prefab` | ADAPTED | 4 × 7.8 × 5 | 4.0 × 7.8 × 5.0 | Ground | PolygonKnights SM_Bld_StairsLarge_01 (two flights) | Two flights of the pack stair, each widened to 4 m and raised to 2.5 m: 5 m over 7.8 m (33 degrees). Climbs toward +Z. Visual steps, one ramp collider. |
| W09b | `Kit/L4_GrandStair.prefab` | ADAPTED | 16 × 7.8 × 5 | 16.0 × 7.8 × 5.0 | Ground | PolygonKnights SM_Bld_StairsLarge_01 (four across, two flights) | The grand stair to the Kneeling Court: four W09a widths side by side, 16 m. |
| W10 | `Kit/L4_Machicolation.prefab` | ADAPTED | 2 × 2.1 × 3.6 | 2.0 × 2.1 × 3.6 | Obstacles | PolygonKnights SM_Bld_Castle_Wood_Battlement_04; SM_Prop_Grate_01 | A timber box corbelled out from the wall top over a pour, sooted, its floor slot barred with an iron grate. Origin at the wall's top edge on the yard face; it hangs toward -Z. A SteamSocket child marks the slot for the pour's tell. |
| W11 | `Kit/L4_Hoarding.prefab` | ADAPTED | 2 × 2.1 × 3.6 | 2.0 × 2.1 × 3.6 | Ground | PolygonKnights SM_Bld_Castle_Wood_Battlement_04; L2 planks | A 2 m bay of timber gallery on the wall top, hung toward -Z: plank floor (walkable) and the battlement's front as its rail. Floor collider on Ground, rail collider on Obstacles. |
| W12 | `Kit/L4_WallWalkDeck.prefab` | BUILT | 2 × 4 × 0.3 | 2.0 × 4.0 × 0.3 | Ground | L4Kit worn stone flags | A 2 x 4 m bay of wall-walk paving, flags 0.3 m thick laid in courses across the walk. |
| W06 | `Kit/L4_BreachEdge.prefab` | BUILT | 2 × 4 × 1.4 | 2.6 × 3.4 × 1.6 | Ground | L4Kit broken merlons and fallen blocks; Kneel.Hazards.EdgeGuard, DitchKill; L1 smoke and ember particles | A 2 m segment of the breach: merlon stumps, a 0.3 m broken lip with no collider at z 0, a 3 m rubble buffer inward (-Z, loose stones, no colliders), smoke and ember glow rising from below, an EdgeGuard on the lip and a kill volume 1 m beyond it. Carries an EdgeBuffer NavMesh area over the buffer. |

### 14.3 Cover and props

| ID | Prefab | Status | Spec (x × z × h) | Read back (renderers, x × z × h) | Layer | Source | Notes |
|---|---|---|---|---|---|---|---|
| C01 | `Props/L4_Mantlet.prefab` | BUILT | 3 × 1 × 1.8 | 3.0 × 2.1 × 1.8 | Obstacles | L2 planks (L2PolyKit.Plank) | No mantlet in the packs. A wall of upright planks on two skids, braced behind and leaning back, with crossbow bolts stuck in its face. |
| C02 | `Props/L4_DeadHorse.prefab` | BUILT | 3 × 2 × 1.2 | 3.1 × 2.8 × 1.6 | Obstacles | L3 ox skeleton (L3Bones) as the horse; PolygonAdventure SM_Prop_Cart_01 | No horse in the packs, and the owner wants carcasses as bare skeletons: the L3 quadruped skeleton lying in its shafts, the supply cart it pulled tipped behind it for the height. |
| C03 | `Props/L4_SupplyCart.prefab` | ADAPTED | 3 × 2 × 1.5 | 3.2 × 2.2 × 1.1 | Obstacles | PolygonAdventure SM_Prop_Cart_01, sacks, barrel, crate | Open cart turned east-west, loaded with sacks, a barrel and a crate. |
| C04a | `Props/L4_Tumbril_Turned.prefab` | ADAPTED | 2 × 3 × 1.5 | 1.4 × 3.0 × 1.2 | Obstacles | PolygonKnights SM_Prop_Cart_01; PolygonKnights Character_Soldier_01/02 baked laid out (L1Bakers.BakeStanding); generated canvas shroud; TextMeshPro chalk | An execution cart with two of the condemned in royal colours laid head to foot under a canvas shroud, TURNED chalked on its tail board. |
| C04b | `Props/L4_Tumbril_Traitor.prefab` | ADAPTED | 2 × 3 × 1.5 | 1.4 × 3.0 × 1.2 | Obstacles | PolygonKnights SM_Prop_Cart_01; PolygonKnights Character_Soldier_01/02 baked laid out (L1Bakers.BakeStanding); generated canvas shroud; TextMeshPro chalk | The same cart chalked TRAITOR. The bodies in the two carts look exactly alike (section 11, vignette 4). |
| C05 | `Props/L4_Well.prefab` | FOUND | 2 × 2 × 1.2 | 2.0 × 2.0 × 1.2 | Obstacles | L3_Well (PolygonKnights SM_Bld_Village_Well_01) | L3's well, re-graded to L4's stone. |
| C06 | `Props/L4_CoffinStack.prefab` | BUILT | 3.6 × 2.6 × 1.8 | 3.6 × 2.6 × 1.8 | Obstacles | Generated coffins (L4Shapes.CoffinShell) in L2's wood palette; L1 Vanguard corpse | No coffin in the packs. Joiner-made six-sided coffins (hollow box, shaped lid with a raised panel, iron handles and nails): two side by side, one on top with its lid knocked aside, one leaning on the stack, one open on the ground with its dead in it. |
| C07 | `Props/L4_Ballista.prefab` | BUILT | 3 × 3 × 1.6 | 2.4 × 2.8 × 1.7 | Obstacles | PolygonKnights SM_Prop_Beam_01; L2 planks; generated cord and bolt | No ballista in the packs. A beam stock on a plank turntable, bow arms and cord, a bolt laid in the groove; aimed toward -Z, down into the yard (section 11, vignette 5). |
| C08 | `Props/L4_Cauldron.prefab` | BUILT | 1.5 × 1.5 × 1.2 | 1.6 × 1.6 × 1.1 | Obstacles | Generated iron cauldron (L4Shapes.Lathe) on L4Kit stones | No cauldron in the packs. A faceted iron cauldron on three short legs in a ring of hearth stones, cold pitch skinned over inside. |
| C09 | `Props/L4_Font.prefab` | BUILT | 2 × 2 × 1.2 | 1.6 × 1.6 × 1.2 | Obstacles | Generated stone font (L4Shapes.Lathe) in the L4 stone palette | No font in the packs. An eight-sided stone basin on a stepped pedestal. |
| C10 | `Props/L4_FallenBell.prefab` | BUILT | 4 × 4 × 2 | 3.1 × 3.5 × 2.0 | Obstacles | Generated bronze bell (L4Shapes.Lathe); PolygonKnights SM_Prop_Beam_01 (headstock) | No bell in the packs. The great bell lying on its side, split down one flank, its burnt headstock beside it. Collider round the bell only. |
| C11 | `Props/L4_ToppledStatue.prefab` | ADAPTED | 4 × 2 × 1.5 | 4.4 × 1.7 × 1.4 | Obstacles | PolygonKnights SM_Prop_Statue_01, SM_Prop_Statue_Base_01 | The pack statue lying face down off its broken base. |
| C12 | `Props/L4_BrokenStatue.prefab` | ADAPTED | 2 × 4.8 × 2.2 | 1.9 × 4.5 × 2.2 | Obstacles | PolygonKnights SM_Prop_Statue_01 (cut with L3Mesh.Clip, capped with L4Shapes.CapCut), SM_Prop_Statue_Base_01 | The bronze statue broken at the thighs: its legs still on the pedestal, the rest of it on its back at the pedestal's foot, sword arm flung out, with fallen stones. |
| C13 | `Props/L4_Wagon.prefab` | ADAPTED | 3 × 2 × 1.5 | 2.9 × 2.3 × 1.6 | Obstacles | PolygonAdventure SM_Prop_Cart_02, barrels | A covered-frame wagon turned east-west with barrels. |
| C14 | `Props/L4_FallenPortcullis.prefab` | ADAPTED | 8 × 1.2 × 0.6 | 8.2 × 1.5 × 0.4 | Obstacles | PolygonKnights SM_Bld_Castle_Iron_Gate_01 (three panels) | A buckled portcullis grid lying along a wall, three panels kinked against each other. |
| C15a | `Props/L4_Barricade_Out.prefab` | BUILT | 8 × 2 × 1.6 | 7.6 × 2.3 × 1.5 | Obstacles | PolygonAdventure carts, crates; PolygonKnights beams (stakes) | The outer barricade of the gatehouse passage, facing the causeway: an overturned cart, crates and sharpened stakes. |
| C15b | `Props/L4_Barricade_In.prefab` | BUILT | 8 × 2 × 1.6 | 8.7 × 2.4 × 1.5 | Obstacles | PolygonAdventure carts, crates; PolygonKnights beams (stakes) | The inner barricade, built against the garrison's own gate guard, with the 4 m gap the Husk holds. Colliders either side of the gap. |
| C16 | `Props/L4_Ram.prefab` | FOUND | 10 × 2 × 2 | 9.2 × 13.8 × 7.0 | Obstacles | L1_BatteringRam | L1's battering ram, buried in the burnt gate. |
| C17 | `Props/L4_RefugeeCart.prefab` | FOUND | 2 × 3 × 1.5 | 2.0 × 3.0 × 1.2 | Obstacles | L3_GraveCart | L3's household cart, turned back toward the causeway. |
| C18 | `Props/L4_Pew.prefab` | BUILT | 4 × 1 × 1 | 4.3 × 0.6 × 1.2 | Obstacles | L2 planks | No pew in the packs. Seat, back and three end boards; one end has given way. |
| C19 | `Props/L4_TreasureChest.prefab` | ADAPTED | 1.2 × 0.7 × 0.7 | 1.2 × 1.1 × 0.7 | Obstacles | PolygonAdventure SM_Prop_Chest_01; generated coin pile | The pack chest, open, its coin spilled at its foot. |
| C20 | `Props/L4_OilJars.prefab` | BUILT | 2 × 1 × 1 | 1.9 × 1.1 × 1.0 | Obstacles | Generated clay jars (L4Shapes.Lathe); PolygonAdventure barrel | Fat clay oil jars stoppered with pitch, and a barrel, stencilled for the causeway defences. |
| C21 | `Props/L4_WeaponRack.prefab` | BUILT | 2 × 0.6 × 1.8 | 2.6 × 0.7 × 1.8 | Obstacles | PolygonKnights SM_Prop_Beam_01; pack weapons | A beam rack with swords and axes; two slots empty. |
| C22a | `Props/L4_Kennel.prefab` | BUILT | 4 × 3 × 2.5 | 4.0 × 3.2 × 2.6 | Obstacles | L4Kit stone courses; PolygonKnights SM_Bld_Castle_Iron_Gate_01; L2 planks | An open stone bay in a side wall, its iron grate swung back, a chain on the floor. Opens toward -Z. Colliders on the walls. |
| C22b | `Props/L4_Kennel_DeadHound.prefab` | BUILT | 4 × 3 × 2.5 | 4.0 × 3.2 × 2.6 | Obstacles | As C22a; L3_Carcass | As C22a with a dead hound's skeleton in it (the east kennel of E6c). |
| C23 | `Props/L4_ExecutionBlock.prefab` | BUILT | 1 × 1 × 0.5 | 1.0 × 0.8 × 1.3 | Obstacles | L4Kit stone block; PolygonAdventure SM_Wep_Axe_01 | A squat block of dressed stone, its top darkened, the headsman's axe left in it. |
| C24 | `Props/L4_SiegeCampRuin.prefab` | ADAPTED | 20 × 14 × 6 | 20.2 × 16.4 × 12.4 | Default | PolygonKnights SM_Bld_Tent_01; L1_TentWreck, L1_SiegeTowerWreck_A | The neighbours' broken siege camp, seen across the ravine from V3b. Backdrop: no collider. |

### 14.3 Figures

| ID | Prefab | Status | Spec (x × z × h) | Read back (renderers, x × z × h) | Layer | Source | Notes |
|---|---|---|---|---|---|---|---|
| F01a | `Figures/L4_Kneeler_Slumped_Soldier.prefab` | FOUND | 1 × 1.4 × 1.1 | 0.8 × 1.2 × 1.2 | Default | L1_Corpse_Vanguard_Soldier_01_Slumped | The dead who knelt: slumped forward on their knees, royal colours. |
| F01b | `Figures/L4_Kneeler_Slumped_Knight.prefab` | FOUND | 1 × 1.4 × 1.1 | 0.9 × 1.3 × 1.2 | Default | L1_Corpse_Vanguard_Knight_02_Slumped | As F01a, in armour. |
| F02 | `Figures/L4_Kneeler_Upright.prefab` | FOUND | 1 × 1.2 × 1.4 | 0.6 × 1.0 × 1.5 | Default | L1_Corpse_Vanguard_Soldier_01_Kneeling | Kneeling upright (decision 8): the look of a waiting thrall. The ambush wraps this. |
| F03a | `Figures/L4_Hanged_Soldier.prefab` | BUILT | 0.8 × 0.8 × 2.4 | 0.7 × 0.5 × 2.3 | Default | PolygonKnights Character_Soldier_01 baked in a new hanging pose (L1Bakers.BakeStanding); generated rope | A hanged soldier in royal colours. Origin at the beam: the rope runs down 0.5 m to the noose. |
| F03b | `Figures/L4_Hanged_Captain.prefab` | BUILT | 0.8 × 0.8 × 2.4 | 0.9 × 0.5 × 2.2 | Default | PolygonKnights Character_Knight_02 baked hanging; generated rope and sash | The tribunal's captain on the last noose, a pale sash across him (vignette 4). |
| F04 | `Figures/L4_GateCaptain.prefab` | ADAPTED | 2 × 2.2 × 0.6 | 1.8 × 1.9 × 0.5 | Default | L1_Corpse_Vanguard_Knight_02_OnBack; PolygonKnights SM_Wep_Broadsword_01 (as a two-handed blade) | The gate guard's captain on his back just inside the breach, his heavy blade across him: the heavy-weapon pickup lies here (decision 6). |
| F05 | `Figures/L4_Cook.prefab` | FOUND | 1 × 1.4 × 1.1 | 0.8 × 1.2 × 1.2 | Default | L2_Corpse_Peasant_Slumped | The cook, slumped among his oil jars on the hoarding (vignette 3). |
| F06 | `Figures/L4_Gatewarden.prefab` | BUILT | 2.6 × 1.6 × 2.4 | 2.6 × 1.6 × 2.4 | Default | PolygonKnights Character_Soldier_02 baked sitting in chains (L1Bakers.BakeStanding); L4Kit wall; generated rings, chain links, manacles | The gatewarden sat against his cell wall, arms chained up to two rings, head fallen (D3). A body, not a skeleton: he died after the gate was opened. |
| F07 | `Figures/L4_DeadScout.prefab` | FOUND | 2 × 1 × 0.6 | 1.7 × 1.0 × 0.7 | Default | L1_Corpse_Enemy_Soldier_02_OnSide | The neighbours' scout, dead on his side where he wrote (D2), in the enemy's colours. |

### 14.4 Hazards

| ID | Prefab | Status | Spec (x × z × h) | Read back (renderers, x × z × h) | Layer | Source | Notes |
|---|---|---|---|---|---|---|---|
| H01 | `Hazards/L4_MurderHole.prefab` | BUILT | 6 × 6 × 5.5 | 1.1 × 1.1 × 5.5 | Hazard | Kneel.Hazards.MurderHole; L1 smoke and ember particles; generated pour column | The pour reaches 1.5 R (3 m). Its mark is painted into the floor where it is placed (L4Paving.Splash), never a decal. Tell: steam from the slot 5.5 m up and hissing drips for 1.5 s; pour: a column of boiling oil for 1 s; then steam off the scalding floor for 2 s. Carries a PourRing NavMesh area. Sequential by default (order 0 of 3); set order and rings per placement, or External for the gate passage. |
| H02 | `Hazards/L4_OilSlick.prefab` | BUILT | 4 × 4 × 1.5 | 3.7 × 3.3 × 0.0 | Hazard | Kneel.Hazards.OilSlick; generated slick (opaque, glossy); L2Fire flames | 2 R across. Unlit: a glossy black pool with an oily sheen at its edge, an opaque mesh 1 cm proud of the floor (no transparent decal on floors). Burning: L2's flames over the whole pool with its light, carving the NavMesh. Spent: a charred, matte stain. Carries an OilSlick NavMesh area. |
| H03 | `Hazards/L4_Cresset_Jib.prefab` | BUILT | 1 × 3 × 4 | 0.8 × 3.0 × 4.0 | Hazard | Kneel.Hazards.HangingLight; PolygonKnights beams; generated iron basket and chain; L2Fire | A post with a jib arm reaching 2.4 m toward +Z, an iron fire basket hanging on a chain from its end over the slick. Strike collider on the post (Enemy layer). Falls to 'Landed' at +Z 2.4 m. Link its slick when placed. |
| H03b | `Hazards/L4_Corona.prefab` | BUILT | 3.2 × 3.2 × 9 | 6.5 × 3.2 × 7.7 | Hazard | Kneel.Hazards.HangingLight; generated iron ring, cups and chain; L2Fire candles; PolygonKnights beam | The chapel's corona: an iron ring of candle cups 3 m across hanging 4 m up from a burnt roof beam, its chain run down to a cleat 3 m to the -X side (put the cleat on a column). Strike collider on the cleat. |
| H03c | `Hazards/L4_TripodBrazier.prefab` | ADAPTED | 0.8 × 0.8 × 2.2 | 0.6 × 0.6 × 2.2 | Hazard | Kneel.Hazards.HangingLight; PolygonKnights SM_Prop_Brazier_01; L2Fire | The pack brazier raised to 2.2 m on its tripod, burning. Struck or rammed, it wobbles and topples toward +Z into the slick beside it. |
| H04a | `Hazards/L4_Portcullis_8.prefab` | BUILT | 8 × 2 × 6 | 8.0 × 0.3 × 6.0 | Hazard | Kneel.Hazards.PortcullisCycle; PolygonKnights SM_Bld_Castle_Iron_Gate_01 (3 x 2 panels); generated spikes | The gate passage's cycling portcullis (E6b): 8 m grid, iron spikes along its foot, a 1 R slot that kills. Cycle mode. Dust and chain shudder while it rattles. |
| H04b | `Hazards/L4_Portcullis_12.prefab` | BUILT | 12 × 2 × 6 | 12.0 × 0.3 × 6.0 | Hazard | As H04a, 5 x 2 panels | The barbican's outer arch (BA): one-shot demo; up until its DemoTrigger, then down for good. |
| H04c | `Hazards/L4_Portcullis_6.prefab` | BUILT | 6 × 2 × 6 | 6.0 × 0.3 × 6.0 | Hazard | As H04a, 2 x 2 panels | The west-lane gate (decision 16): the same gate in Winch mode. Starts down; its winch raises it for good. |
| H05 | `Hazards/L4_Winch.prefab` | BUILT | 1.6 × 1.2 × 1.3 | 1.9 × 0.6 × 2.2 | Obstacles | Kneel.Hazards.Winch; generated drum; PolygonKnights beams | A timber winch: a drum between two posts, a crank, chain wound on it. Interact (E) within 2.5 m. Link its gate when placed. |
| H05b | `Hazards/L4_CapstanBay.prefab` | BUILT | 4 × 4 × 3 | 4.0 × 4.1 × 3.0 | Obstacles | L4Kit stone courses; PolygonKnights iron gate (bars); generated capstan; baked Synty gatekeeper | The gatekeeper's barred bay beside the gate passage: a capstan and the gatekeeper working it, seen through bars on its -Z face. The bay's back door (+Z) leads to the forecourt. |
| H06a | `Hazards/L4_Scaffold.prefab` | BUILT | 6 × 2 × 5 | 9.7 × 8.2 × 5.6 | Hazard | Kneel.Hazards.ScaffoldCollapse, ScaffoldStrut; PolygonKnights beams; L2 planks | A masons' scaffold against a wall (+Z): uprights, ledgers, braces, two plank lifts. Its raking strut stands 2 m beyond the collapse zone on -Z. Struck, it creaks and sags 1.5 s and falls forward (-Z) over its chalk-marked zone (footprint + 1 R): two hits under it, death on it. The debris then blocks and carves. |
| H06b | `Hazards/L4_Scaffold_Demo.prefab` | BUILT | 6 × 2 × 5 | 9.7 × 7.6 × 5.6 | Hazard | As H06a; PolygonAdventure barrels (the casks) | The rampart-stair demo (5:30): the same scaffold over two oil casks inside its chalk marks, played once by its DemoTrigger and never again. |
| H07 | `Hazards/L4_ChoirLoft.prefab` | BUILT | 24 × 4 × 6 | 26.1 × 9.5 × 6.8 | Hazard | Kneel.Hazards.ScaffoldCollapse, ScaffoldStrut; L2 planks; PolygonKnights beams; L2Fire candles | The chapel's choir loft at T+1 (+5 m): a timber floor across the apse on posts, a rail with burning lamps, a strut at each end. Either strut brings it down forward (-Z); its lamps light the slick linked to it. |
| H08a | `Hazards/L4_KneelerAmbush.prefab` | BUILT | 1 × 1.4 × 1.2 | 0.6 × 1.0 × 1.5 | Hazard | Kneel.Hazards.KneelerAmbush; L4_Kneeler_Upright (F02) | An upright kneeler (decision 8): the kneeling figure, a hit collider on the Enemy layer, the 3 R wake radius, a SpawnPoint for the enemy it stands in for. |
| H08b | `Hazards/L4_KneelerAmbush_Twin.prefab` | BUILT | 1 × 1.4 × 1.2 | 0.6 × 1.0 × 1.5 | Hazard | As H08a; generated ember brand | As H08a with the Cursed Twins' ember brand on its chest. Link the pair as partners when placed: striking one wakes both. |
| H09 | `Hazards/L4_KillVolume.prefab` | BUILT | 6 × 6 × 2.5 | — | Hazard | Kneel.Hazards.DitchKill | A resizable kill trigger (pivot at its bottom centre): the ravine, the breach drop. |
| H10 | `Hazards/L4_PourTrough.prefab` | BUILT | 6 × 2 × 1.6 | 6.0 × 2.9 × 1.9 | Obstacles | Kneel.Hazards.PourTender; L4Kit stone; L4_Cauldron; baked Synty thrall | Six metres of the pour trough on the north wall-walk, a cauldron, and the chained pour-tender tipping it. One hit kills him and the linked murder holes stop until the level resets. |
| H11 | (component on N04) | BUILT | 0 × 0 × 0 | — | Hazard | Kneel.Hazards.OnScreenGate | A component, not a prefab: it sits on the crossbowman stand-in (N04) and on every ranged attacker the AI will spawn. |

### 14.6 Markers

| ID | Prefab | Status | Spec (x × z × h) | Read back (renderers, x × z × h) | Layer | Source | Notes |
|---|---|---|---|---|---|---|---|
| M01 | `Markers/L4_Shrine.prefab` | FOUND | 4.3 × 2.7 × 3.9 | 4.3 × 2.7 × 3.9 | Default | L3_Shrine (the L1 checkpoint shrine) | Shrine A and B. |
| M02 | `Markers/L4_SecretTell.prefab` | FOUND | 0.3 × 0.1 × 0.3 | 0.3 × 0.2 × 1.6 | Default | L3_SecretTell (the scorched handprint) | One tell for the whole game (decision 7). |
| M03 | `Markers/L4_Pickup_HeavyWeapon.prefab` | ADAPTED | 2 × 0.4 × 0.3 | 1.4 × 1.9 × 0.1 | Default | PolygonKnights SM_Wep_Broadsword_01 (x2.1); PickupMarker | The gate captain's heavy blade (decision 6), lying in the sun with a faint glint. Unlocks Heavy Attack (Q) once the player has the verb. |
| M04 | `Markers/L4_Pickup_Key.prefab` | BUILT | 0.3 × 0.1 × 0.2 | 0.3 × 0.1 × 0.0 | Default | Generated iron key; PickupMarker | The tribunal key (decision 15), on the courtyard Husk's belt; it opens the rampart grille (M06b) through KeyRing. |
| M05a | `Markers/L4_Pickup_Consumable.prefab` | FOUND | 0.7 × 0.5 × 0.5 | 0.7 × 0.5 × 0.5 | Default | L3_Pickup_Consumable | Healing consumable (D3, D2, D5, D6). |
| M05b | `Markers/L4_Pickup_Lore.prefab` | FOUND | 0.8 × 0.5 × 0.2 | 0.7 × 0.6 × 0.1 | Default | L3_Pickup_Lore | Lore fragments (D3, D1, D4, D6; D2's is the scout's account). |
| M05c | `Markers/L4_Pickup_Cordial.prefab` | ADAPTED | 0.5 × 0.5 × 0.8 | 0.4 × 0.4 × 0.7 | Default | PolygonAdventure SM_Item_Potion_04; PickupMarker | The steward's cordial (D1): refills every healing charge once. |
| M06a | `Markers/L4_Postern.prefab` | BUILT | 3 × 1 × 3 | 2.6 × 0.8 × 3.3 | Obstacles | Kneel.Hazards.OneTimeGate (Barred); PolygonKnights SM_Bld_Castle_Door_01, beam | The shortcut (z): a door that opens only from its barred side (+Z, the tower). From -Z: "Barred from the other side". Opens once for good. |
| M06b | `Markers/L4_KeyGrille.prefab` | BUILT | 3 × 0.4 × 3 | 3.0 × 0.7 × 3.3 | Obstacles | Kneel.Hazards.OneTimeGate (Key); PolygonKnights SM_Bld_Castle_Iron_Gate_01 | The rampart stair's grille (Y): opens with the tribunal key, from either side; lifts and stays up. |
| M07 | `Markers/L4_VistaTrigger.prefab` | FOUND | 8 × 6 × 4 | 8.0 × 6.0 × 4.0 | Default | L3_VistaTrigger | V1-V5, V3b, VZ; their framings are set per placement. |
| M08 | `Markers/L4_ArenaCamVolume.prefab` | FOUND | 8 × 8 × 6 | 8.0 × 8.0 × 6.0 | Default | L3_ArenaCamVolume | Sized to each arena when placed. |
| M09 | `Markers/L4_EncounterLock.prefab` | BUILT | 0.5 × 0.5 × 0.5 | — | Default | Kneel.Markers.EncounterMarker | An encounter's framing and lock (section 7). Data only. |
| M10a | `Markers/L4_PlayerSpawn.prefab` | FOUND | 1 × 1 × 1 | 1.0 × 2.1 × 0.1 | Default | L3_PlayerSpawn | S (60, 0). |
| M10b | `Markers/L4_ExitTrigger.prefab` | FOUND | 4 × 4 × 3 | 4.0 × 4.0 × 3.0 | Default | L3_ExitTrigger | X, the throne-hall doors. |
| M11 | `Markers/L4_RoofHider.prefab` | BUILT | 8 × 8 × 4 | — | Ignore Raycast | Kneel.Markers.RoofHiderMarker | A volume listing the roofs that switch to shadows only while the player is inside. Data only. |
| M12 | `Markers/L4_SpawnMarker.prefab` | FOUND | 1 × 1 × 1 | 0.9 × 1.5 × 0.1 | Default | L3_SpawnMarker | Every enemy in section 6. |
| M13 | `Markers/L4_DemoTrigger.prefab` | BUILT | 6 × 4 × 3 | — | Ignore Raycast | Kneel.Hazards.DemoTrigger | Plays a one-time demo (portcullis, scaffold) the first time the player walks in; link its target when placed. |

### 14.5 Structures

| ID | Prefab | Status | Spec (x × z × h) | Read back (renderers, x × z × h) | Layer | Source | Notes |
|---|---|---|---|---|---|---|---|
| S01 | `Structures/L4_OuterGatehouse.prefab` | BUILT | 44 × 28 × 9 | 43.0 × 31.7 × 11.1 | Obstacles | L4 kit (W08b, W07, W05, W01); PolygonKnights SM_Bld_Castle_Wall_01, beams | Origin: the centre of the passage's outer (south) face. The 12 m outer arch over the west 12 m of the 16 m passage, the ram breach (6 m) to its east through the passage's east wall, flanking towers, 7 m passage walls running 24 m north to the rubble wall with its 2 R gap (where the Husk stands). The roof is burnt off: charred beams across the top. Arch and towers fade when they hide the player. |
| S02 | `Structures/L4_Guardroom.prefab` | BUILT | 17 × 9.4 × 6.3 | 17.0 × 9.7 × 6.4 | Obstacles | L4 kit (W04, W03a); L4Kit gate arches, flags, merlons; PolygonKnights SM_Bld_Castle_Wall_01, Wall_Gate_01; L4 boards; PolygonAdventure barrels, crate; C21, F01b | Shrine A's room over the inner gatehouse, at T+1 (origin on its floor). A stone room: cut wall on the camera side (south); curtain walls east and west, 4.5 m, each with a pack gateway (2.2 m opening; east from the wall-walk, west to the west wall-walk); on the north an arcade of four round arches on square piers, a 0.9 m rail across each bay, open over the courtyard. A flagged stone roof with merlons that fades when it hides the player. Inside: the guards' table and benches (one knocked over), barrels and a crate, the weapon rack, a dead guard. |
| S03 | `Structures/L4_TowerNE.prefab` | BUILT | 10 × 13 × 6.5 | 10.0 × 13.2 × 7.0 | Obstacles | PolygonKnights SM_Bld_Castle_Wall_01, Wall_Gate_01; L4Kit merlons, flags, coursed stair turret, spiral steps | At T+1 (origin on its floor). Doorways south (from the terrace) and west (to the north walk); in the north face a broken doorway into a stair turret behind the wall (an octagon of coursed stone), a 1 R spiral up to the roof (+5 m, the V3b lookout). Walls 5 m, so the doorways clear the player; broken merlons. Fades when it hides the player. |
| S04 | `Structures/L4_TowerNW.prefab` | BUILT | 10 × 10 × 6.5 | 10.0 × 10.0 × 6.8 | Obstacles | As S03, with the postern (M06a) | At T+1. The postern in its south doorway (barred inside: opens from the tower), doorways east (from the north walk) and north (to the stair down to the inner ward). Fades when it hides the player. |
| S05 | `Structures/L4_Chapel.prefab` | BUILT | 28 × 32 × 18.7 | 28.1 × 32.0 × 18.6 | Obstacles | PolygonKnights SM_Bld_Castle_Wall_01 (walls, buttresses), Wall_Gate_01, Church_Window_01 (x2.4), Church_TowerBase_01 and Tower_02 (x2.2, split with L3Mesh.Clip), beams; generated octagonal columns (L4Shapes.Lathe); L4Kit arches, flags, steps; C10, C18 | Origin: the centre of the nave floor. Roofless. Side walls 7 m with stepped buttresses outside and tall lancets inside, the west door halfway up the west aisle. Two rows of octagonal columns every 4 m carry a round-arched arcade (6 R nave, 3 R aisles). North: the chancel a step up, the altar under three lancets. South (camera side): the facade cut to 0.9 m either side of a portal of two recessed arches, which fades. The belfry at the north-east corner split down its middle, its bell on the floor; charred trusses, one fallen into the nave; pews thrown about; the crypt grille at the north end of the east aisle. |
| S06 | `Structures/L4_Crypt.prefab` | BUILT | 8 × 12 × 3 | 8.0 × 11.5 × 4.2 | Obstacles | L4Kit stone steps and courses; L2Fire candle | Origin: the top of its stair (floor level). Twelve steps down 3 m behind a half-bricked opening, then a low vault 8 x 6 m lit by one candle (the noble's stash, D1). |
| S07 | `Structures/L4_InnerGate.prefab` | BUILT | 12 × 16 × 7 | 15.1 × 16.6 × 12.0 | Obstacles | L4 kit (W08a x2, H04a, H01 x2, H05b, W10); PolygonKnights walls, iron gate | Origin: the centre of the passage floor. The 4 R passage between 7 m walls: the south arch with its wrecked portcullis jammed up, the north arch with the cycling portcullis, two murder holes in the vault fired by the gate's down phase, the capstan bay behind bars in the east wall. The vault fades when it hides the player. |
| S08 | `Structures/L4_GrandStair.prefab` | BUILT | 18 × 10 × 5 | 18.0 × 8.8 × 6.0 | Ground | L4 kit (W09b); L4Kit courses (cheeks, balustrade) | Origin: the foot of the stair, centre. The 16 m grand stair from the forecourt to the Kneeling Court (T+1), cheek walls each side and balustrades at the landing's ends. |
| S09 | `Structures/L4_ThroneDoors.prefab` | BUILT | 21.2 × 6 × 17.2 | 21.7 × 6.4 × 17.2 | Obstacles | L4Kit gate arches (three orders), merlons, flags, steps; PolygonKnights SM_Bld_Castle_Door_01 (x2.5), Castle_Tower_01/02/04 and Tower_Top_01 (x1.6), SM_Prop_Banner_01 (x2), SM_Prop_Brazier_01; L2Fire | Origin: the threshold, centre. The throne-hall doors, 8.4 m across (the note's inset), shut in a portal of three recessed round-arched orders between two square towers with royal banners and cressets. Two steps up to a 0.4 m dais; a great brazier on a stone plinth either side (brazier gold at the end). Belongs under the L4L5_Threshold root. |
| S10 | `Structures/L4_PrisonCells.prefab` | BUILT | 14 × 8 × 3 | 14.4 × 7.6 × 3.0 | Obstacles | L4Kit courses; PolygonKnights iron gates; L2 planks; F06; TextMeshPro scratching | Origin: the lane's mouth, centre (the lane runs toward -X). A 2 R lane, roofed and unlit, three cells on its north side behind iron gates; the gatewarden in the last, his scratching on its wall. |
| S11 | `Structures/L4_Gallows.prefab` | BUILT | 10 × 9 × 6 | 10.0 × 10.6 × 6.0 | Ground | PolygonKnights beams; L2 planks; L4Kit steps; F03a x3, F03b | Origin: the platform's centre on the ground. A 10 x 8 m platform at +2 m on posts, steps up its south side, a gallows frame with four nooses: three of the condemned and the tribunal's captain on the last. The crossbowman's perch. |
| S12 | `Structures/L4_CausewaySpan.prefab` | BUILT | 9 × 10 × 13 | 9.0 × 17.8 × 13.3 | Ground | L4Kit flags, gate arch (as a bridge arch); L4 kit (W03) | Origin: the deck's centre. Ten metres of the causeway, 8 m wide between 0.9 m parapets, on a stone arch over the dry ravine 12 m below. Repeat it along the approach. |
| S13 | `Structures/L4_KeepSpire.prefab` | ADAPTED | 40 × 30 × 70 | 47.3 × 25.8 × 68.4 | Default | PolygonKnights tower pieces and spire (x3), Church_Room_01 (x2.2) | The level landmark ~40 m beyond the doors: a 70 m keep of stacked tower storeys under a spire, the throne hall's roofs in front of it. Backdrop: no collider. |
| S14 | `Structures/L4_Cloister.prefab` | BUILT | 14 × 2 × 4.6 | 14.4 × 2.9 × 4.9 | Obstacles | L4Kit gate arches (as arcade bays), fallen blocks | Origin: the arcade's centre. Four bays of a broken arcade, one fallen in. Placed along the cloister walk. |

### 14.7 Enemies

| ID | Prefab | Status | Spec (x × z × h) | Read back (renderers, x × z × h) | Layer | Source | Notes |
|---|---|---|---|---|---|---|---|
| N01 | `Assets/Enemies/AshenFootman/AshenFootman.prefab` | FOUND | 1 × 1 × 2 | — | Default | Assets/Enemies/AshenFootman/AshenFootman.prefab | The existing enemy, used as it is; shown with its AI switched off in the review. |
| N02 | `Enemies/L4_StandIn_IronHusk.prefab` | BUILT | 1.2 × 1.2 × 2.2 | 1.0 × 0.5 × 2.1 | Enemy | PolygonKnights Character_Knight_02 baked standing (x1.15), enemy black iron; generated plates | No Husk in the project. An armoured man a head taller than the player, black iron, three heavy plates marked on shoulders and chest (the armour that breaks to heavies). Capsule on the Enemy layer. Carries its SpawnMarker. |
| N03 | `Enemies/L4_StandIn_CursedTwin.prefab` | BUILT | 1 × 1 × 1.9 | 0.7 × 0.5 × 1.8 | Enemy | PolygonKnights Character_Soldier_02 baked standing; generated ember brand | One of the Twins: gate-guard colours, the shared ember brand on the chest. Capsule on the Enemy layer; SpawnMarker. |
| N04 | `Enemies/L4_StandIn_Crossbowman.prefab` | BUILT | 1 × 1 × 1.9 | 0.7 × 1.0 × 1.9 | Enemy | PolygonKnights Character_Soldier_01 baked aiming; generated crossbow; Kneel.Hazards.OnScreenGate | The Hollow Archer with a crossbow (decision 4), aiming. Carries the OnScreenGate (H11). Capsule; SpawnMarker. |
| N05 | `Enemies/L4_StandIn_Fool.prefab` | BUILT | 1 × 1 × 1.9 | 1.2 × 0.5 × 1.8 | Enemy | PolygonKnights Character_Soldier_02 baked standing; SM_Wep_Broadsword_01 (x1.5) | The Fool with his long blade. Capsule; SpawnMarker. |
| N06a | `Assets/Kneel/Prefabs/L3/Enemies/L3_StandIn_Hound.prefab` | FOUND | 0.8 × 1.4 × 0.8 | 0.8 × 1.4 × 0.8 | Enemy | L3 stand-in | L3's hound stand-in. |
| N06b | `Assets/Kneel/Prefabs/L3/Enemies/L3_StandIn_Brute.prefab` | FOUND | 2 × 2 × 2.6 | 8.0 × 8.0 × 2.6 | Enemy | L3 stand-in | L3's brute stand-in. |
| N07 | `Enemies/L4_Steward.prefab` | BUILT | 1 × 1 × 1.8 | 0.7 × 1.1 × 1.8 | Default | PolygonKnights Character_Soldier_01 baked bent over work | The steward feeding the braziers on the Kneeling Court (vignette 7): harmless, ignores the player. (The gatekeeper and the pour-tender are inside H05b and H10.) |

**Status counts.** ADAPTED 23, BUILT 70, FOUND 20.

## How to review the assets (Phase 1)

1. Open `Assets/Kneel/Scenes/Levels/L4_AssetReview.unity`.
2. **Rows** (south of the look-dev corner, group `Review`): one row per manifest table, read west to east: wall kit, cover and props, figures, hazards, markers, enemies, structures. Each item has its ID, prefab name and status on the floor in front of it; each row starts with a 1.8 m figure and a 2 m bar marked "1 R". The hanged figures (F03) hang from 2.6 m.
3. **Look-dev corner** (group `LookDev`, north of the rows): the E3 courtyard corner the look was approved on.
4. **Hazard test lane** (group `TestLane`, west, around x -110): press Play; the player starts at the lane's south end with the project's gameplay camera following.
   - Murder holes: three rings pour in turn, 4 s apart; hit the pour-tender on the wall above (sword) to stop them.
   - Oil: strike the cresset's post or the brazier: 1.8 s swing, then the slick under it burns for 6 s.
   - Gate passage: the portcullis cycles on its own; its pour lands in the down phase.
   - Turn the winch [E] to raise the lane gate. The postern opens only from its +Z (north) side; the grille needs the tribunal key (`KeyRing.Give("TribunalKey")`).
   - Walk south of the scaffold into its demo trigger (or strike its raking strut).
   - Kneelers: strike a Twin and both rise; walk within 3 R of the footman kneeler.
   - Magenta capsules are L3's `HazardTestDummy`; they log every contact to the console with its consequence.
   - `TestLane/LevelReset` → context menu "Reset Level" puts every hazard back (gates, winch and demos stay done).
5. Rebuild: `Kneel > L4 > Build > Asset Prefabs`, then `Kneel > L4 > Build > Asset Review Scene`. Both are safe to rerun; tuning lives in `Assets/Kneel/Settings/L4` (`L4_StrongholdTuning`, `L4_HazardTuning`) and is never overwritten. `Kneel > L4 > Look-dev > Build Courtyard Corner` rebuilds the look-dev corner only.

## Hazard playtest

`Kneel > L4 > Check > Run Hazard Playtest` in Play mode (runs at time scale 2, about 20 s). Last run, 2026-10-19 (after the asset revisions and the mortar cores): **PASS, 42 checks, 0 failed, 0 console errors.** Run it without polling the Editor while it plays: an Editor command that compiles on the main thread stalls a frame and stretches the measured times by about 0.4 s. Times in game seconds against `L4_StrongholdTuning`:

| Check | Expected | Measured |
|---|---|---|
| Rings' first pours (order 1, 2, 0) | 4.00, 8.00, 12.00 | 3.99, 8.00, 11.98 |
| Tell before a pour | 1.50 | 1.48 |
| Pour contacts on a dummy in ring 0 | one per pour | one per pour |
| Light swing + fall, slick lit on landing (cresset by `Strike`, brazier by a sword hit) | 2.10 | 2.11, 2.11 |
| Contact on catching / burning ticks in 6 s | 1 / 5 | 1 / 5 |
| Portcullis up, rattle, drop, down, rise | 6.0, 1.5, 0.3, 4.0, 2.5 | 6.01, 1.51, 0.31, 4.00, 2.51 |
| Brute dummy in the slot | killed on each drop | 3 drops, 3 kills |
| Passage pour after the gate comes down | 1.50 | 1.51 |
| Winch raises the lane gate | 1.50 | 1.50 |
| Scaffold demo: starts on walking in; creak + fall; footman under it | 0; 2.10; crushed once | 0.01; 2.15; once |
| Twins: struck one rises after 1 s, its partner with it | 1.00, 1.00 | 1.00, 1.00 |
| Footman kneeler wakes within 3 R | ~1 s after arriving | 1.02 |
| Postern from -Z / +Z; grille without / with the key | shut / opens; shut / opens | as expected |
| Pour-tender killed | no pours until reset | 0 pours |
| Level reset | hazards and kneelers back; demo, winch gate and one-time gates stay done | as expected; rings pour again after |
| Edge guard clamp | 0.25 m short of the lip | 0.25 |

Fire was also checked by eye in Play mode: both lights fell and their pools burned with L2's flame effect.

## Hooks not connected

| # | Hook | State |
|---|---|---|
| 1 | Heavy Attack (Q) | Missing from the player. M03 is a pickup marker; the Husk's armour read needs the verb. |
| 2 | Enemy behaviour | Out of scope (owner, 2026-10-18). Stand-ins N02–N05 and L3's hound and brute are collider-only, with `SpawnMarker`s. `EdgeGuard.ClampEnemyMove` / `ReportTeeter`, `OnScreenGate.CanStartWindUp` / `WindUpStarted`, `KneelerAmbush.Woken` / `StruckEarly` and `HangingLight.Strike` (for a charge into a post) are the AI's entry points. |
| 3 | Player `IHazardTarget` | As in L3: the player takes `L4_HazardTuning`'s flat damage per "hit" through `IDamageable`. |
| 4 | Death, respawn, rest | Not in the project. `LevelReset.ResetAll` does the reset when something calls it. |
| 5 | Keys and saves | `KeyRing` is a static set with no inventory or save behind it. One-time gates, the winch and played demos expose `SetOpen` / `SetTurned` / `SetFinished` for a save system. |
| 6 | Vista pans, arena framings, encounter locks, roof hiders | Data only (`VistaMarker`, `ArenaMarker`, `EncounterMarker`, `RoofHiderMarker`); the camera is final. Vista values and roof lists are filled in (Phase 2 finished). The project's `OcclusionFade` already fades towers, roofs and vaults that hide the player. |
| 9 | Fade probes and the player | The `FadeProbe` layer collides with `Player` in the physics matrix, so each faded piece's exact-mesh probe is also a wall for the player. That is harmless where the mesh matches the piece's own collider, but it closed the tower doors (fixed by height). Making `FadeProbe` collide with nothing would be the general fix. It is a project setting that L2 and L3 share, so it is left to the owner. |
| 7 | Pickups | `PickupMarker` data only, as in L3. |
| 8 | Audio | None: the tells are visual only (steam, dust, sparks, creak shudder). |

## Blockout spec (Phase 2)

Derived from the note's section 4 insets (1 m columns, 2 m rows; read row labels as the bottom edge of their row) and the section 6 encounter table, which wins where they differ. Phase 2 encodes this as data (`L4Plan`, as `L3Plan` does) and builds the scene from it, one stage at a time.

### 15.1 Frame

- Plan x = Unity X, plan y = Unity Z, metres, origin at the world origin; one root `L4_Root`; Shrine B, the Kneeling Court and the doors under `L4_Root/L4L5_Threshold`.
- Tiers: T0 at y 0, T+1 at y 5 (floor tops), T-1 at y -3. Gallows deck +2.
- Floors are scene geometry: slabs on `Ground` with box colliders, drawn with a per-floor copy of `L4_Ground` (flagstones; earth at wall feet and in worn patches; pitch splashes painted at every murder hole, `L4Paving.Splash`). No transparent decal on any floor.
- Wall glyphs map to the kit: `#` W01 curtain faces (W07/S-pieces where a tower or building stands), `_` W04 cut wall, `=` edge W03 parapet on the yard side of a T+1 floor and W02 merlons on its outer side, `%` structures that fade.
- Low boundaries (W03, W04 tops, W05) carry their unseen caps; the level adds caps wherever a floor edge is lower than 2 m (L3's containment rule).

### 15.2 Floors

| ID | Area | x | y | Tier | Built as / notes |
|---|---|---|---|---|---|
| F01 | S causeway | 56–64 | 0–40 | T0 | Four S12 spans (y 0–40) over the dry ravine; H09 kill volume along the ravine floor (-12) |
| F02 | BA apron | 44–76 | 40–54 | T0 | Ravine edges east and west behind W03 |
| F03 | E1 gatehouse passage | 54–70 | 58–84 | T0 | S01 at origin (62, 57); inner wall y 84–86 with the 4 m gap x 60–64 |
| F04 | E2 the Lists | 46–74 | 86–108 | T0 | |
| F05 | D3 prison lane | 32–46 | 102–106 | T0 | S10 at its mouth (46, 104), lane toward -X; unlit |
| F06 | WW inner curtain walk + hoarding | 44–76 | 108–112 | T+1 | Hoarding W11 bays on its yard face x 50–72 |
| F07 | A guardroom + loggia | 52–68 | 112–124 | T+1 | S02 at (60, 118) |
| F08 | West wall-walk (shortcut) | 42–46 | 112–156 | T+1 | |
| F09 | E3 courtyard | 46–74 | 126–154 | T0 | Shrine A stair W09a x 56–60 down from 124; rampart stair W09a from the grille (63, 127) climbing east to a landing 72–76 |
| F10 | Gallows deck | 64–74 | 138–146 | +2 | S11 at (69, 142) |
| F11 | E4 east terrace | 76–88 | 126–156 | T+1 | Core x 76–84, buffer 84–87, lip at x 87 along y 144–156 (12 m); merlons elsewhere on the outer edge; inner parapet W03 with the 4 m drop gap at y 138–142 |
| F12 | North wall-walk | 46–76 | 154–158 | T+1 | H10 trough segments x 48–68 above the rings |
| F13 | NE tower | 77–87 | 156–166 | T+1 | S03 at (82, 161); roof +4 (V3b) |
| F14 | NW tower | 38–48 | 154–164 | T+1 | S04 at (43, 159); stair W09a north from its north door down to the inner ward, foot at (42, 166) |
| F15 | IW inner ward | 36–76 | 158–166 | T0 | |
| F16 | WL west lane | 40–46 | 166–196 | T0 | H04c lane gate at y 179, H05 winch at (44, 187) |
| F17 | E5 chapel | 48–72 | 168–194 | T0 | S05 at (60, 180) |
| F18 | D1 crypt | 70–80 | 190–200 | T-1 | S06 at the grille (71, 192), yaw 90 (down toward +X) |
| F19 | CL cloister walk | 38–76 | 196–204 | T0 | S14 along its north side |
| F20 | E6a barbican yard | 46–74 | 204–224 | T0 | |
| F21 | E6a wall-walk | 46–56, 64–74 | 224–228 | T+1 | Either side of the passage |
| F22 | E6b gate passage | 56–64 | 224–238 | T0 | S07 at (60, 231) |
| F23 | E6c forecourt | 48–72 | 238–253 | T0 | Kennels C22a (49, 248) and C22b (71, 248) in the side walls |
| F24 | Grand stair | 52–68 | 253–261 | T0→T+1 | S08 at (60, 253) |
| F25 | B Kneeling Court | 48–72 | 261–266 | T+1 | `L4L5_Threshold`; S09 doors at y 266 (8 m, per the inset) |

### 15.3 Placements

**Encounters** (section 6; spawn markers M12 with their stand-in, facing as written):

| Enc. | Enemies | Hazards and linked pieces |
|---|---|---|
| E1 | Iron Husk N02 (62, 80) facing south; heavy weapon M03 on the gate captain F04 at (56, 62) | Four dry pitch splashes painted at (58, 76), (62, 77), (66, 76), (62, 73); barricades C15a (62, 60) and C15b in the inner wall's gap line |
| E2 | Footman kneeler H08a (54, 98); Twins H08b (66, 102), (68, 96) as partners | Slicks H02 at (57, 99) and (67, 104); cressets H03 on the hoarding over each, posts 1 R outside; mantlet C01 (60, 90); dead horse C02 (50, 96); supply cart C03 (71, 92) |
| E3 | Iron Husk N02 (60, 140) with the key M04; crossbowman N04 on the gallows (69, 145) | Rings H01 at (50, 151), (58, 151), (66, 151), sequential, order 0–2, supplied by the H10 pour-tender on the north walk; tumbrils C04a (58, 130), C04b by the gallows (64, 135); well C05 (50, 141); coffins C06 (70, 131); scaffold demo H06b at (72, 150) with its trigger at the rampart stair foot; block C23 and slumped kneelers F01 at the block (52, 147) |
| E4 | Fool N05 (82, 150); Iron Husk N02 (82, 155) at the tower door | Breach W06 ×6 along x 87, y 144–156; ballista C07 (78, 146) aimed at the yard; cauldron C08 (78, 135) |
| E5 | Twins H08b (57, 176), (63, 178) as partners; footman kneeler H08a (60, 186) | Slicks H02 (60, 174) (no light: never burns), (58, 180) under the corona H03b (cleat on the NW crossing column), (62, 188) linked to the loft H07 (struts (52, 188), (68, 188)); font C09 (60, 171); bell C10 (60, 181); pews C18 and chests C19 with slumped nobles; crypt grille (71, 192) |
| E6a | Iron Husk N02 (60, 220); footmen N01 (52, 212), (68, 214); crossbowman N04 on the wall-walk (52, 226) | Toppled statue C11 (60, 206); wagon C13 (49, 214); mantlet C01 (71, 216) |
| E6b | Brute N06b (60, 231) | In S07: portcullis H04a (north, y 237, cycle) firing rings H01 (60, 229) and (60, 233); capstan bay H05b with the gatekeeper |
| E6c | Hound N06a in the west kennel (49, 248); Twins N03 at the stair foot (57, 253), (63, 253) | Slicks H02 (56, 247) and (64, 247), a tripod brazier H03c at the edge of each; broken statues C12 (54, 243), (66, 243) |

**Shrines, gates, demos, secrets, vignettes.** Shrine A M01 (60, 122); Shrine B M01 (60, 261). Postern M06a in S04's south door (44, 154). Key grille M06b at (63, 127). Portcullis demo H04b in S01's outer arch, its DemoTrigger on the apron. Handprints M02 at (45, 104), (71, 192), (86, 164). Pickups: consumables M05a in D3 and D2, lore M05b ×3 (D3, D1, D2's scout F07), the cordial M05c in D1. Vignettes per section 11: refugee carts C17 on the causeway facing south; slumped kneelers F01 at the gate's foot (y 50–53) with bolts in them; the ram C16 in the outer arch; the cook F05 among oil jars C20 on the hoarding; hanged figures in S11; the gatewarden in S10; the Kneeling Court rows of F01 facing the doors with the steward N07 and two braziers; the keep S13 at (60, 306); the siege camp C24 beyond the ravine east of D2.

**Markers.** Player spawn M10a (60, 2) facing north; exit M10b at the doors (60, 267). Vistas M07: V1 (60, 4), V2 (60, 120), V3 (82, 138), V3b (86, 164) at +9, VZ on the postern, V4 (64, 202), V5 (60, 261), with section 7's pitch and FOV in their data. Arena volumes M08 and encounter locks M09 for E1–E6c (E4 offset 3 R toward the breach, E6b default framing). Roof hiders M11 in S02, S03, S04.

### 15.4 NavMesh

- Agent types (project settings): Thrall 0.5 / 2.0 (exists, from L3), Husk 0.6 / 2.2, Brute 1.0 / 2.6 (exists), Hound 0.4 / 0.9 (exists), Perch 0.5 / 2.0. Step 0.45 m, slope 40°.
- One surface per agent over `Ground` and `Obstacles`. Perch bakes only inside perch volumes (the gallows deck, the E6a wall-walk segment) so crossbowmen cannot leave them.
- Areas (added in Phase 1): EdgeBuffer 4 (W06), PourRing (H01), OilSlick (H02), GateSlot (H04), ScaffoldZone (H06, H07). Per-agent costs (hounds avoid pours 3 and slicks 6) belong to the AI.
- Links: one-way kennel sills (hound only); no link for the E4 drop; the grand stair excluded from the hound surface; E3's Husk barred from the courtyard stairs by a Not Walkable volume for the Husk agent.

### 15.5 Stages

| Stage | Builds | Checks before the next |
|---|---|---|
| A | Metrics map (section 12's list) in its own group outside the level | Roll vs buffer, charge lane, pour timing |
| B | Floors F01–F25, tiers, stairs, the ravine | Containment flood from the spawn (0.2 m capsule sweep); every floor reachable |
| C | Walls and structures (S01–S14, kit runs), fade and cutaway | No wind-up hidden: capsules at every spawn visible from the arena framing; containment again |
| D | E1–E3 and Shrine A: encounters, hazards, links | Hazard playtest on the level's own pieces; runback E3/E4 lengths |
| E | E4, the shortcut, the drop | Breach buffer measured; postern from both sides; drop one-way |
| F | E5, E6, Shrine B and the threshold | Lane gate + winch; gate passage cycle; boss runback length |
| G | Secrets, vignettes, dressing, lighting pass | Secret tells visible from where section 10 says; Game-view captures of every arena |

## Phase 2: the blockout as built (2026-10-19)

`Kneel > L4 > Build > Level (replaces the scene)` builds `Assets/Kneel/Scenes/Levels/L4_BurningGate.unity` from nothing, every stage up to G: `L4Plan` holds 15.2-15.5 as data (floors, edge overrides, stairs, placements, enemies, volumes) and `L4Level` builds it. Generated meshes go to `Meshes/L4/Level`, each floor's own ground material and paving textures to `Materials/L4/Level` and `Materials/L4/Textures`, the NavMesh to `Scenes/Levels/L4_BurningGate/`. Rebuilding replaces any hand edit to the scene.

**How edges are decided.** Every floor side is sampled every 0.5 m. Just past the edge: a floor at the same height is open; a lower floor makes this side a raised edge (a curtain face W01 under it, the low parapet W03 on top); nothing is the castle's boundary (T0: curtain W01, or the cut wall W04 on a camera-side south edge, or W03 on the ravine; T+1: face plus merlons W02). Listed spans override: Skip (a structure stands there), NoTop (a stair arrives, the E4 drop), Close (merlons on a same-height join the route must not take), Hoarding (W11 over the Lists), Breach (W06 along the terrace's 12 m). Every boundary run stands on a footing down to the ravine bed at -12, where one kill volume covers the whole ravine.

| Stage | Built | Check |
|---|---|---|
| A | Metrics map at x -90: a 2 m-module pad, six W06 breach segments with their buffer, a pour H01 under a machicolation, a 6 R lane between curtain faces, a stair | Built; measured by eye only |
| B | 32 floors (slabs on `Ground`, box colliders), four W09a stairs, the ravine | NavMesh baked (Thrall, Husk, Brute, Hound) |
| C | 353 edge pieces, the 18 structures (S01-S14, the causeway as five S12 spans) | Reachability from the spawn; NavMesh leak check |
| D | E1-E3 and Shrine A: the outer portcullis demo, the gate captain and the heavy blade, barricade, the Lists' slicks, cressets and kneelers, the key grille, three pours under three machicolations with the pour-tender, tumbrils, well, coffins, both scaffolds and the demo trigger | 7 links |
| E | E4: ballista, cauldron, the Fool and the terrace Husk; the shortcut (S04's postern) and the drop (the parapet gap over the gallows) | |
| F | E5 (slicks, corona, choir loft, kneelers, font, chests, the lane gate and winch), E6a-c (statues, wagon, mantlet, kennels, slicks with tripod braziers), Shrine B, the exit | 14 links in all |
| G | Handprints, pickups, refugee carts, the ram, the kneelers at the gate and on the court, the cook among his jars, the dead scout, the siege camp in the ravine, the keep | |

**The zigzag spine (owner's review, 2026-10-19).** "Could we make it more interesting rather than just a straight line?" The owner chose a zigzag spine (short down-screen legs allowed, none used yet) and to build it directly. Every arena now sits to one side of the one before it, joined by a sideways leg:

| Leg | Area | Plan x | Plan y | Travel |
|---|---|---|---|---|
| S, BA, E1 | causeway, apron, gatehouse | 54-70 | 0-86 | up the middle to the rubble gap (x 60-64) |
| E2 | the Lists (mirrored from the spec) | 36-64 | 86-108 | up and left to the stair at the west end |
| D3 | prison lane, going east from the north-east corner | 64-78 | 98-106 | optional |
| WW | curtain walk over the Lists, the hoarding | 36-80 | 108-112 | right, 28 m, to the guardroom |
| A | guardroom, Shrine A (66, 121) | 52-80 | 112-124 | down the shrine stair at x 62-66 |
| E3 | courtyard; gallows at (75, 142) | 52-80 | 124-154 | rampart stair east to the landing |
| E4 | east terrace, breach at x 93 | 80-93 | 124-154 | up to the NE tower (88, 159) |
| NW | north walk to the NW tower (52.5, 159) | 57.5-83 | 154-158 | left |
| IW | inner ward under the north wall | 57.5-83 | 158-168 | down the NW stair into the lane stub, then right |
| E5 | chapel (78, 182), crypt from (87, 191) | 66-90 | 168-196 | up, out the west door |
| WL | side yard and west lane, gate at y 178, winch (55.5, 186) | 47.4-65.4 | 164-196.6 | left, then up |
| CL | cloister walk | 44-92 | 196.6-206 | up |
| E6a-c, B | barbican yard, gate passage (54, 233), forecourt, Kneeling Court, doors (54, 268) | 40-68 | 206-268 | up |

Where the swings left spaces between walls, solid bastions fill them (M02 east of the Lists, M03/M04 either side of the chapel side yard; raised, unreachable, their faces walls) and an enclosed outer bailey sits behind the Lists' cut wall (M01), so the frame never looks down into the ravine inside the castle.

**Side paths (owner's review, 2026-10-19).** "Dead ends with Lore or health pickups, around 3 of those." Three short dead ends, each leaving a corridor sideways and ending against the outer wall, so none opens a pit inside the castle. They are in plain sight, not secrets: no handprint, no enemies.

| # | Off | Floors (plan) | What is there | Reward |
|---|---|---|---|---|
| D4 | the apron's west end (BA) | F02w, x 34-44, y 46-54, a ravine ledge with low parapets | a refugee cart C17 and the coffins C06 the families left at the gate | lore M05b (36.2, 51.8) |
| D5 | the west lane by the NW stair's foot (WL) | F16p passage x 42-47.4, y 172-175; F16q guardroom x 35-42, y 169-178; cut south walls | the postern barricaded from inside C15b, its last guard kneeling at it F01a | healing M05a (38.4, 170.6) |
| D6 | the cloister walk, beside E6a (CL) | F19n, x 70-78, y 206-216; its west wall is the yard's east curtain | two pews C18 as cots, a slumped knight F01b, the surgeon's chest C19 and jars C20 | healing M05a (76.4, 210.8), lore M05b (73.6, 215.2) |

**Checks after G** (42 floors, 425 edge pieces, as finished). Everything is reachable from the spawn on the Thrall NavMesh except the two crossbow wall-walks (F21w/e), which have no way up by design. No reachable NavMesh lies outside a floor, a stair, the causeway, the gallows, the grand stair, the crypt or the hoarding. Gates (both posterns, the key grille, the lane gate, the portcullises) are baked open and carve the NavMesh while shut, so the AI can use them once they open. In Play mode the player spawns on the causeway at (60, 0, 2) and stands. 119 prefabs placed (with the side paths and D2's pickup), 14 hazard links, 14 enemies (stand-ins with their spawn markers set to their encounter), 17 vistas, arena volumes and roof hiders.

**Where the build departs from 15.2-15.4** besides the zigzag (the spec's numbers did not meet the built prefabs; the topology is kept):
- **Towers.** Their doors land on the floors they serve: NE south onto the terrace, west onto the north walk, north the broken doorway into the D2 turret; NW south the postern from the west walk, east onto the north walk, north to the stair down into the west lane. The tower builder had an east door on the NE tower (onto the ravine) and no D2 doorway, and its walls were solid colliders across every doorway; both fixed (`L4Structures.Tower`).
- **Terrace and rampart.** The terrace meets the courtyard and the gallows deck, so the drop gap at y 138-142 lands on the gallows; the rampart stair climbs to a 2 m landing; merlons close the landing from the guardroom level and the terrace from the north walk. The breach is y 142-154.
- **Courtyard** starts at y 124, the foot of the T+1 block, so both stairs stand in it. Tumbril C04a stands clear of the shrine stair.
- **Guardroom (S02).** Its arcade's second bay has no rail: Shrine A's stair goes down through it. Pier colliders replace the one box across the arcade.
- **Chapel (S05) and crypt (S06).** The split belfry stands out over the NE corner, the crypt grille hangs open against the aisle wall, and the crypt stair goes down north through a hole in the chapel floor; the vault lies under the north wall and the cloister. The cracked bell is the one under the belfry (east aisle), not in the crossing.
- **Cloister arcade (S14)** stands along the cloister's south side (its collider would have closed the walk on the north side).
- **Kneeling Court** starts 0.2 m into the grand stair's ramp (a 10 cm crack broke the NavMesh).
- **Not built at first:** the Perch agent, the kennel links, the Husk leash and the vista values. All of these were added when Phase 2 was finished, below.

## Phase 2 finished (2026-10-19)

After the side paths were approved, the rest of 15.4 and the stage checks of 15.5 were built, with a player-sized sweep of the whole route added to the checks. The sweep and the captures turned up four real faults, which are fixed.

**Fixed**

| What | Fault | Fix |
|---|---|---|
| Tower doorways (S03, S04) | The pack gateway scaled into a 4 m wall leaves 1.75 m under its arch, and its fade-probe mesh collider blocks the `Player` layer, so the player (1.95 m) could not pass any tower door: the terrace into the NE tower, the north walk, the NW stair. The NavMesh does not see fade probes, so the reachability check passed. | Tower walls 5 m (2.19 m under the arch). The roofs are 1 m higher (T+1 + 5). |
| D2, the NE tower stair turret | The broken north doorway was on the wrong half of the wall: it opened onto a void behind the tower, while the turret stood behind the other half with no colliders. The roof had no collider either, and the dead scout lay inside the turret's wall at T+1. | North doorway moved to the turret. The turret is rebuilt as an octagon of coursed stone with a newel and 20 treads (one turn, with colliders) up to a landing level with the wall-walk. The north merlons stop where it comes out. The roof has a collider. The scout and D2's healing pickup lie on the roof at T+1 + 5, with V3b beside them. The 4 m gap behind the tower, between the ward and the chapel, is solid masonry (M05, a T+1 Mass) under the turret. A player-sized capsule climbs from the tower floor to the roof (24 of 24 waypoints). |
| D1, the crypt stair | The chapel floor's north edge had no override over the stair hole, so a 2 m curtain piece stood across the stair head and the crypt was cut off. | `Skip` on F17's north edge over the hole (x 86-88). |
| The E4 drop | The gallows steps ran into the landing scaffold (H06a), so a player who took the drop was trapped on the deck. | Scaffold 1 m south (y 131). Terrace, deck, steps, yard, shrine stair and Shrine A walk clean (about 44 m, 12 s). The drop is one-way: the deck is 3 m below the terrace. |
| Marker volumes (all M07, M08, M11) | Their box colliders sit on the nested marker prefab, and the builder sized only the instance root, so every vista, arena and roof hider was the prefab's default 8 × 6 × 8 m. | Sized on the nested box. |

**Added**

- **Perch** agent (0.5 / 2.0), baked only inside two boxes: the gallows deck (x 70-80, y 138-146) and the E6a west wall-walk (F21w). Each perch is its own surface with `CollectObjects.Volume`, so a crossbowman's mesh is its leash. One full build in four left the gallows perch empty, and the same bake run again was fine. The bake now checks that each perch has a mesh and bakes it once more if not.
- **Agent-only Not Walkable volumes** (`NavMeshModifierVolume` limited to one agent, on the `Ground` layer, because the surfaces only collect volumes inside their layer mask):
  - the Husk off the Shrine A stair and the rampart stair (E3's leash);
  - the hound off the grand stair;
  - the hound off both kennel sills (a 0.8 m strip across each kennel mouth).
- **One-way kennel links** (`NavMeshLink`, hound only), from inside each kennel out over the sill. A hound comes out and never goes back in.
- **Agent rules** in the level check, 14 paths:
  - the Husk cannot reach the guardroom or the landing from the courtyard, but a Thrall can;
  - a crossbowman can walk his deck and his wall-walk, and cannot leave either;
  - a hound gets out of both kennels, cannot get back in, and cannot climb the grand stair, but a Thrall can;
  - a Thrall can enter the kennel.

  All 14 pass.
- **Vista values** written into each `VistaMarker` (id, pitch, FOV, pull-back, pan, note), tuned from captures taken with the gameplay camera's own maths (pitch 65°, 8.5 m, FOV 60), with whatever would fade around the player hidden:

  | Vista | Pitch | FOV | Pull-back | Change from section 7 |
  |---|---|---|---|---|
  | V1 | 10° | 44° | +12 m | Lower, so the spire stands in the upper third |
  | V2 | 34° | — | 0 | Steeper. At 24° the camera sat inside the guardroom's south wall; now it looks through the arcade over the yard |
  | V3 | 20° | — | 0 | As specified |
  | V3b | 10° | — | −2 m | Volume moved onto the open roof, with the camera brought in past the south merlons. The siege camp is seen from the roof's east side and the terrace, not in this framing (the east merlons hide the ravine floor) |
  | VZ | — | — | — | A 2 s pan to Shrine A (66, 5, 121) |
  | V4 | 14° | — | −4 m | At 8.5 m the camera was inside the bastion behind the cloister. Volume moved 1.5 m north. It frames the gate, the doors through it and E6a's enemies |
  | V5 | 12° | 50° | +6 m | Pulled back so the doors fill the upper third |

- **Arena ids** set on every `ArenaMarker`.
- **Roof hiders filled.** Each tower and the guardroom lists what goes shadows-only while the player is inside: its roof flags and merlons (baked together), and on the NE tower the scout and pickup. The project's fade would otherwise leave those two hanging over the player.

**Checks**

- **Player sweep.** A `CharacterController` on the `Player` layer, sized like the player (r 0.2, h 1.95, step 0.3), walks the critical path leg by leg along the Thrall NavMesh, out to every side path and back, and down into D1. Gate blockers are lifted and enemy stand-ins walked through. It covers 1,068 m. Its one stall is Shrine B's plinth at the top of the grand stair, which the player steps round. The plinth is on `Default`, so the AI's NavMesh does not know it is there.
- **Runbacks** on the built NavMesh at 3.8 m/s:

  | Run | Built | Note | Live along it |
  |---|---|---|---|
  | Shrine A to E3 | 15 m, 4 s | 7 m | — |
  | Shrine A to E4 | 36 m, 9 s | 23 m | E3 |
  | Shrine A to E5 (shortcut) | 98 m, 26 s | 75 m | — (the chapel moved east) |
  | Shrine A to E6 (shortcut, lane gate up) | 101 m, 26 s | 97 m | — |
  | E4 to Shrine A by the drop | about 44 m, 12 s | 34 m | E3 (walked with the player capsule) |
  | Postern to Shrine A | 45 m, 12 s | 50 m | — |
  | Start to E1 | 65 m, 17 s | 62 m | — |
  | Start to E2 | 88 m, 23 s | 90 m | E1 |

  Shrine B to the doors is 4 m, as specified.
- **Captures** of every arena in an arena framing and of every vista, with stand-ins visible, and the D1, D2 and D3 handprints from the gameplay camera. Each handprint reads as an ember speck beside its doorway.
- **Not checked in Play mode:**
  - the hazards on the level's own pieces;
  - the lane gate and its winch;
  - the gate passage's cycle;
  - the postern from both sides.

  The same prefabs pass `L4Playtest` (42/42) in the review scene. A level playtest needs scripted input through the level, which is the next piece of work if wanted.

**Scratch tools** (not in the project): the sweep, the climb, the runbacks and the captures run as `unity command eval_file` scripts.

## Phase 3: refinement

### 3a. The surroundings (look-dev, 2026-10-19)

The castle stood on a flat fog plane at -12, its footings ending in nothing. `L4Surroundings` (`Kneel > L4 > Build > Surroundings`, which replaces only its own group; it also runs in the level build) gives it the note's setting: a rock crag under the castle, cliffs down to the dry ravine the causeway crosses, and banks rising to burnt land.

- **Ground.** One heightfield: 2 m cells, flat-shaded, and coloured from a land palette (`L4_Palette_Land`) the way the packs are, by slope, height and broad patches. The ground falls away by distance from the castle's footprint:
  - a 2-4 m rock shelf past the wall footings;
  - cliffs 5-10 m deep, with strata;
  - the ravine floor at about -15, scree against the cliff feet;
  - banks 30-40 m out, rising to rolling burnt land that climbs into hills.

  The south bank meets the causeway's south end exactly (y -10) and the road runs on south at deck height. The keep's rock (x 36-72, y 262-332) is part of the crag. There are 54 tiles under `Surroundings/Ground`.
- **Cliffs.** The PolygonKnights cliff mesas (25 × 14 × 25 m), squeezed to 9.5-13.5 m across, ring the crag 3.5-5.5 m outside the walls. Their tops sit 1-2.5 m under the wall feet and their bases on the ravine floor. They are in the graded castle stone, and none stands under the causeway.
- **Rocks.** PolygonAdventure rocks are scattered on the scree at the cliff feet and across the banks, in `L4_Adventure_Stone`.
- **Siege camp.** It now stands on the east bank (150, 172) at ground height, not on the ravine floor.
- **Nothing is walkable.** The surroundings have no colliders and are not baked. Falling is unchanged in effect:
  - the ravine kill volume now tops out at -4.5 (under the crypt floor at -3.45), so a fall ends soon after it starts rather than after a long drop through rock;
  - a second box catches the drop off the E4 breach from -1.5.

  The flat ravine bed is gone. The full checks still pass: 14 of 14 agent rules, every floor reachable, no leaks.
- **Cost.** About 655,000 triangles: 88,000 in the ground, the rest mostly in the cliff mesas. That is fine for PC. A mobile build would want the far rocks thinned and the cliffs given LODs.
- **Since:** the land palette was darkened (the ravine read as pale sand under the lower sun), and the smoke now comes from 3b's columns. Vegetation and dead trees on the banks are still not built.

### 3b. Epic and polished (2026-10-19)

Four builders, each replacing only its own group. They all run in the level build (stage G), and each has its own `Kneel > L4 > Build` menu item.

**The citadel** (`L4Citadel`, under `L4L5_Threshold/Citadel`). It replaces the lone keep spire S13, which is no longer placed; its manifest row stays for the review scene. It stands on a new T+1 bastion (`M06`, `M06w`, `M06e`: x 26-82, y 255-352, Mass). Merlons now close the court's outer sides where they meet the bastion. South to north:
- **The throne hall.** The pack's lancet church bay at 4×: 26 m wide, 35 m long, ridge 26 m. Every lancet burns from inside (an emissive pane on the glass), with transepts and an apse in the dark castle stone.
- **Twin west towers.** Four church-tower stages at 3.4× with dark spires (no crosses) and royal banners.
- **The great keep.** A 70 m castle tower at 4.2× with four round turrets and spires, the spire at 4× and a beacon fire on top. It is the level's landmark from the causeway.
- **Six corner and mid towers.** Four of them have spires and banners; the two mid towers carry beacon fires.
- **The curtain.** A 7.6 m curtain joins the towers.
- **Two colossi.** The pack's knight statue at 4.2× (16 m) on 5 m plinths flanks the Kneeling Court, swords raised over the kneeling, with braziers at their feet.

Exterior only: no colliders, nothing walkable (L5 is the inside). 168,000 triangles and 7 fires.

**The burning** (`L4Atmosphere`, `Atmosphere`). Nothing here is a hazard, and no fire sits on or over a slick (decision 17).
- **13 smoke columns.** They come from the ravine under the E4 breach, the west and east ravine, the siege camp, the chapel's split belfry, and far out in the burning land and behind the citadel. Each is a slow dark column that widens and pales as it rises and leans north (decision 10).
- **13 fires.** They burn on the outer gatehouse's flanking towers (first tried on the passage walls, where they burned right under the camera), on two refugee carts (smoulders), in the ram's ashes, on the ravine floor under each near column, and in the siege camp.
- **5 ember fields.** Sparse embers drift north over the whole route.

**The sky and grade** (`L4LightingPass`). Afternoon now, not noon:
- **Sun:** 47° (was 52°), warmer.
- **Sky:** a smoke sky with a burnt-orange horizon, made from the procedural sky's tint, atmosphere 2.7 and a dark ground colour.
- **Grade:** saturation -16 (was -30) so fire carries, contrast 30, bloom 0.55, vignette 0.3.
- **Fog:** warmer and pushed out to 45-250 m so the citadel reads from the causeway.
- **Tried and rejected:** a 40° sun, which threw the south half of every arena into its wall's shadow.

**The aftermath** (`L4Dressing`, `Aftermath`). Per floor, seeded:
- rubble at the wall feet (273 pieces);
- the dead of both sides on the fighting floors (38, the L1 corpse prefabs);
- dropped blades and shields (77).

All of it is under 0.5 m with no colliders. It keeps clear of every placed piece, hazard (4 m), marker, spawn, stair and the route's narrow joins. Each spot is confirmed by a raycast onto the floor itself.

**Checks after the full rebuild:**
- 14 of 14 agent rules pass.
- Every floor is reachable except the crossbow walks, and there are no NavMesh leaks.
- The player sweep walks 1,068 m with the same single stall (Shrine B's plinth).
- The D2 climb reaches 24 of 24 waypoints.
- In Play mode the player spawns and stands, at about 9.5 ms a frame in the Editor.
- The scene totals about 2.8 million triangles: 0.7 M ground, 0.17 M citadel, 0.17 M aftermath. It has 69 point lights (none casting shadows) and 209 particle systems. That is fine for PC; a mobile build needs the far rocks thinned, the cliff mesas given LODs and the ember fields halved.

**Captures** are now taken with the camera's URP post-processing on, the way the Game view renders (the earlier ones skipped the grade), with particles simulated 12 s in and with fading pieces hidden as `OcclusionFade` would.

**Left for the owner's eye:**
- Gameplay views of the arenas with the new grade: the walls' shadows are deeper.
- The colossi's material: the graded pack stone reads tan-brown against the dark castle stone.
- Whether the forecourt should show more of the keep (the throne doors' towers hide its lower half from the stair).

### 3b, second pass: the owner's review (2026-10-19)

The owner reviewed 3b ("screams: this is a game, not realistic... the stronghold should feel used and like something that has function") with a list. Each point and what answers it:

| Note | Built |
|---|---|
| Raised squares look like ground pushed up; make outposts with guardrails, reached by stairs | `L4Plan`: `M02` and `M03` split round stairs cut into them (`Stair_Lookout` from the Lists, `Stair_GuardPost` from the west lane, both W09a). `M04` is reached over a timber bridge (floor `F16t`, hidden, collider only). New `Floor.Outpost` gives them parapets and merlons. `L4Stronghold` dresses them as a lookout (ballista, lean-to, stores, brazier, watchtower), a guard post and a signal post. |
| Better stairs, from the pack if it has them | W09a/W09b use `SM_Bld_StairsLarge_02` (block-built), the upper flight on coursed masonry. |
| The causeway in the yards' cobbles | Floor `F01` over the S12 spans (they keep parapets and arches; their flagstones are gone). |
| Statues more menacing | Black basalt, red eye slits with a red light, a red seam and crack in the plinth, red fire (`L4Citadel.Hellfire`), a red spot washing up each statue. |
| Flags on the tops of the towers | Every spire carries its flag at the tip (`TipFlag`), as do the NE turret, both tower roofs and the watchtowers. |
| More watchtowers | Eight timber watchtowers (posts, braces, ladder, the pack's octagonal hoarding as the hut, lamplight) on the cliff pinnacles east, west and north of the walls, never south of a yard; two more on the outposts. |
| More fires and hazards | Eight burning wrecks at yard corners (`Kneel.FireHazard`, burns on contact) and eight fire vents (`Kneel.Hazards.FireVent`: a lava crack that glows, spits sparks for 1.2 s, then bursts for 1.6 s, every 6.8 s; contact through `HazardContact`), in the apron, courtyard, terrace, barbican yard and inner ward. They are scene objects, not manifest prefabs, and are not yet playtested. |
| Soldiers to skeletons; fallen long ago | `L2Skeletons` gained Kneeling and Hanging poses. F01a/b, F02, F03a/b, F04, F05, F06 and F07 are skeletons now, and the aftermath's dead are L2 skeletons. The ambush kneelers (H08, wrapping F02) kneel among them as bones. |
| Lava round the stronghold | `L4Lava`: a faceted sheet at y -12.8 where the ground lies under it. Its tileable crust has cracks along the zero lines of warped fractal noise, opening into glowing channels where a large-scale heat runs high. `Kneel.LavaFlow` drifts it. 20 glow lights, breakouts and sparks. The ravine fires and smoke stand on the crust. |
| Spire on the tower, the church tower complete, the middle roof ruined, not scaffolded | The NE stair turret rises 2.8 m above the roof under a spire (its south side open where the spiral exits). The belfry was made whole, then replaced by a square castle tower (below). The chapel's trusses became a ruined roof (`RuinedRoof`): dark stone slates over the whole hall, burnt through at the south end and over the nave, with charred broken rafters and a crow-stepped north gable. `RoofHider_Chapel` hides it while the player is inside. |
| Later in the day, ambient with the fires | Dusk: sun 28° from the west-south-west, #FFAA6C at 2.2. Violet-brown ambient, exposure 1.22, saturation -2, bloom 0.8. Sky tint #5C4640 at thickness 2.3. A red sky tint turned the procedural sky green. A 21° sun was night-dark. |
| More red banners for contrast; then "more crimson like the soldiers" | `L4_BannerRed` #511A1E (the Vanguard's dried-blood crimson, a shade lifted). 55 banners hang down the yards' walls (raycast to each face, one every 8-9 m, clear of everything placed), plus big ones on the hall, towers, keep and gate. |
| A smooth start from L3's farmland, no hard lines | Floor `F00`, a fenced farm track (z -44 to -10), is the new spawn at (60, -38). `L4Surroundings` now has `Farm`/`FieldAt`: L3's patchwork of wheat, stubble, pasture and plough fades into burnt land in ragged tongues. `L4Farmland` places L3 wheat beside the track (stubble further out, charred toward the ravine), L3 rails as the track's boundary, broken rails along the fields, and L3's farmhouse, well, mill, wagons, orchard and oaks, plus a burning rick. |
| A grand first gate | S01 has a gate keep to 16 m over the arch: merlons, three machicolations, spired bartizans with flags, and two great banners. Braziers flank the gate. Two pillar braziers stand where the causeway meets the apron. |
| Cut walls look weird; make them walls that fade | W04 is now a whole 5 m bay with merlons and `OcclusionFade` (it was 0.9 m with a shadows-only wall). This covers the Lists, the postern and the guardroom's south walls. The hall's front is a full fading wall too. |
| The chapel does not belong in the stronghold | S05 is now the garrison hall, on the same plan. It has: <ul><li>arrow slits and flat pilasters;</li><li>square piers under timber beams;</li><li>a gated, merloned front that fades;</li><li>a square castle tower;</li><li>a dais with the high table under the master's banner;</li><li>trestle tables where the pews stood (the same cover), weapon racks and stores.</li></ul> The font (C09) is replaced by the cauldron (C08). |

Editor tidy-up: the magenta marker volumes are hidden in the scene. They are hidden in play anyway, and `Kneel > L4 > Show/Hide Marker Volumes` toggles them.

**Checks after the full rebuild:**
- 14 of 14 agent rules pass.
- Every floor is reachable except the crossbow walks, including all the outposts, the bridge and the farm track. There are no NavMesh leaks.
- The player sweep now starts on the farm track and goes up both outposts and across the bridge. It walks 1,227 m with the same single stall (Shrine B's plinth).
- In Play mode the player spawns on the track at about 4 ms a frame in the Editor. The first try was 24 ms: the L3 wheat clumps are 6,500 triangles each and had been packed at 1.7 m, 12.6 M triangles. The wheat is now standing only beside the track and stubble further out.
- The scene is 4.4 M triangles, 420 particle systems and 133 lights (none casting shadows). That is fine for PC; mobile needs the farm and the ground thinned.

**Third look (owner, 2026-10-19).** The owner reviewed the second pass:
- "Towers are floating in air and it is not symmetrical. Make the side towers taller as well." The outer gate is now symmetric about its arch. The ram breach east of the arch is gone (the ram lies in the arch, at (65, 54)). The flanking towers stand 13 m either side and rise from 9 m to 18 m, with merlons, a deck where the fires burn, flags and banners. The bartizans stand on the gate wall over the piers; they had hung off its corners.
- "The old chapel roof now has floating elements, just remove the roof." The hall is open to the sky, `RuinedRoof` and `RoofHider_Chapel` are gone, and two fallen rafters lie inside. The arch piers of the hall's front got colliders: their fade probes stopped the player where the NavMesh had a way through.
- "You removed the glyphs from the statues. I want them back." The glyph strokes and the foot band are back on the plinths, under the red seam. They are measured in the plinth's own frame now, so they lie on its face and do not overhang it.
- "Make sure lighting on lanterns is correctly placed." The colossi's braziers are 3 m tall; their fire and light had sat at 1.45 m, half-way down the pole. Every brazier's fire is now placed from the piece's own box, in its basket: the colossi, the outposts and the gate.

After a full rebuild: agent rules 14/14, every floor reachable except the crossbow walks, no leaks, and the sweep walks 1,228 m with the one known stall.

**The start, redone (owner, 2026-10-20):** "The start of the level looks bad. Please find images of lvl 3 and then make it look like the farmlands but add a nice transition." The first farm start had coloured the land's flat-shaded facets in field tones, with sparse heavy L3 crop-row wheat. `L4Farmland` now builds the start the way L3 builds itself, from L3 captures taken for reference:
- **Ground.** A patch over x -10..130, z -100..-8, on the surroundings' own 2 m grid and heights (`L4Surroundings` skips its facets there and lies nearly flat under the fields). It is painted onto a copy of L3's ground material (`L4_FarmGround`, the Kneel/Ground shader) from L3's palette and its strips of straw and ash (`L3Ground.Burnt`). A pale road ribbon winds down the track onto the causeway, and the patch's border fades into the burnt land's colour.
- **Wheat.** L3's own plants (`L3Fields.Plant` and `Stub`, now internal) in rows that lean with the strips, about 13,000 plants on L3's wind materials.
- **Track.** L3's low stone walls along the southern half, as at L3's own start, then rails toward the bank.
- **Transition.** A ragged fire front (`L4Farmland.Scorch`) turns the strips to ash and stubble toward the ravine, with cinders on the ground, smouldering fields, a burning rick and the first skeletons, before the lava.

Then (owner, same day): "I would like the wheats here to be black indicating they have been burned. Also remove the farmer house close by." All of the start's wheat now uses L3's charred crop materials: the swaying stalks on `L3_Wheat_Charred`, the still ones and the stubble on `L3_Wheat_Stubble`. The farmhouse by the track and its side track are gone.

The L3 reference captures showed L3's re-graded stone walls and its castle backdrop rendering pink in a script camera, although their materials and textures check out. That needs a look in L3 itself.

**The farm's edge and an endgame grade (owner, 2026-10-21).**
- "The line between farmlands ground and the rest of map is very harsh." The cause was a shading clash: the farm patch was smooth, textured ground beside flat-shaded palette facets. All the open land in `L4Surroundings` is now a smooth mesh on the same ground shader, painted from one shared colour function (`LandPaint`, in L3's palette: scorched straw, ash, soot and cinders). Faceted rock stays only on the cliffs, the scree, the ravine and the ledge at the walls' feet. The farm patch fades into `LandPaint` over its last 24 m, and both meshes take their normals from the same height samples, so neither the colour nor the shading breaks at the edge.
- "Feel like endgame dark souls level... gritty and hardcore." `L4LightingPass` gives an ashen, overcast end of the world:
  - a dull sun at 18° (#E2A47A, 1.9) smothered in grey ash;
  - cold slate ambient;
  - a pale ash haze at 34-220 m;
  - saturation -30 and contrast 28, so only fire, lava, the crimson banners and the statues' eyes keep colour;
  - split toning to slate shadows and muted amber highlights;
  - vignette 0.44, grain 0.36 and exposure 1.5.

  `L4Atmosphere` adds ash falling over the route and the farm track. The character fill is up to 0.6 to keep fights readable. The first try, with a dark haze at 26 m and exposure 1.08, was close to black.

**Lore change:** the note's "three days after it fell" is now "fell long ago", per the owner. The vault note's refinement section says so.

**Not yet checked in Play mode:** the fire vents and burning wrecks (contact and damage), the fading walls in the live camera, and the gates and hazards from before.

## Questions

1. **The camera shows less than the note assumes.** About 20 × 13 m of ground and 8.8 m up-screen, against the note's assumed 32 × 20 m default and 48 × 30 m arena framings. L4's arenas are 16–28 m wide, so most yards will not fit in frame, and E3's crossbowman and E6a's wall-walk can be off-screen. L3's answer was "keep the camera as is"; L4 relies on `OnScreenGate` (section 12, decision 9) for ranged fairness. Built as written unless the owner says otherwise.
2. **The heavy attack does not exist yet.** The Husk's whole read depends on it. The pickup is a marker; the verb belongs to player code, which this handover treats as read-only.
3. **Wall modules.** The pack curtain wall is 5.4 m long; the note's kit is on a 2 m module. Runs are built with 6 m pieces (×1.11) closed with 4 m and 2 m cuts, so the stonework is not squashed. Approved in look-dev.
4. **Scene layout.** One level scene, `L4_BurningGate.unity`, with Shrine B, the Kneeling Court and the doors under their own root `L4L5_Threshold` so they can move to their own scene later. There is no persistent systems scene in the project yet.
5. **The design note still says "ring"** for the murder holes (sections 6, 9 and 12, "pour ring", "the ring is scorched into the floor"). They are built as pitch splashes of the same reach, per the owner's review.
6. **Project settings changed.** NavMesh areas 5–9 added: EdgeBuffer (cost 4), PourRing, OilSlick, GateSlot, ScaffoldZone. No new layers or tags.
7. **L3's hazard and marker code was extended, additively.** `HazardKind` gains Pour, OilBurnEnter, OilBurnTick, Crush, GateSlot, Fall; `HazardTuning` gains pour, crush and oil hit counts (L3's asset picks up the defaults); `HazardTestDummy` describes the new kinds; `PickupKind` and `EnemyType` gain L4 values appended at the end, so L3's serialized values keep their meaning. No player, enemy, combat or camera script was changed.
8. **Throne doors are 8 m**, as the E6 inset draws them (an earlier draft of this manifest said 12). Shrine B stands 4 m in front of them (walkthrough and inset); the overview's legend says 8 m.
9. **The gatehouse's inner end is a tall wall broken by the 2 R gap** (the inset's `#`). A low rubble line there would let the player hop past the Husk, which rule bend 10 forbids.
10. **A few pieces stand outside their row**, kept: the dead horse 1.6 m (its cart tipped for the height), the broken statue 1.77 m, the execution axe's haft to 1.3 m (all under the 1.8 m cover limit); the wrapped L1 ram and siege-camp pieces are their own size (13.8 m, 12 m).
11. **Flames read pale at noon.** L2's flame effect was made for night; under L4's grade it looks yellow-white. Tune it in the lighting pass, or give L4 its own flame tint.
12. **The review camera carries the project's `PlayerCamera`** (unchanged, with `PlayerCameraSettings.asset`) so the test lane can be played by hand. Nothing in the camera's code or settings changed.
13. **Gate-passage pours.** The pour column is built for a 5.5 m slot; S07's vault is at 7 m. The column is stretched to the vault at blockout.
14. **The gatewarden's words** are scratched on his cell wall in TextMeshPro, faint. Say if they should only be a lore pickup.
15. **Enemy stand-ins are baked Synty figures**, not primitives: armoured in black iron for the Husk, gate-guard colours with the ember brand for the Twins, aiming with a crossbow for the crossbowman. They carry colliders and spawn markers only.

## Blocked

Nothing is blocked.
