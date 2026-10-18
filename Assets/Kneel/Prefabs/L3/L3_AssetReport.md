# L3 The Fallow Fields: asset report

Phases 0 (survey), 1 (assets) and 2 (blockout) of the build handover in `02-Design/Levels/L3 The Fallow Fields.md`. Written 2026-10-05 on branch `task/L3-FallowFields` (cut from `task/L2-HollowVillage`), last updated 2026-10-06. Nothing is committed.

**State: all three phases are done, and the level has since had three review rounds from the owner (2026-10-05 and 2026-10-06). The level scene `L3_FallowFields` is built through stage E. It now departs from the design note where the owner asked it to: the sail hazard is gone, the mill has a stair, the lanes are fenced rather than walled, and there are points of interest on the lanes. Read "Farmland pass" under Phase 2 first: it has the current counts and checks. Sections written before it are kept as the record of each stage and say so where they are out of date.**

- Phase 2 is under "Phase 2: blockout" below: what is in the scene, what goes beyond the note, and one entry per stage.

- 55 prefabs built, plus the existing footman: every row of section 14 except H05 (the sail hazard, cut by the owner), and three the owner's review added (K09, K10, S08).
- All 56 rows pass the read-back check (`Kneel > L3 > Check > Asset Prefabs`): colliders within 5 cm of the footprint, pivot at ground centre, layers, cover heights, no missing scripts, no broken materials.
- The hazards pass a scripted play-mode run in `L3_AssetReview` (`Kneel > L3 > Check > Run Hazard Playtest`, 42 s of game time since the sail was cut, results under "Hazard playtest" below). No console errors on entering play mode.
- The four decisions Phase 2 needed have been answered (see Questions 1 to 5).

## Owner review, 2026-10-05

**Decisions**

| Question | Answer |
|---|---|
| R (roll is 3 m, plan uses 2 m) | Keep as is: build section 15 as written. |
| Camera shows less than the plan assumes | Keep as is. |
| Level scene name | `L3_FallowFields`. |
| Shrine size | Use the existing L1 shrine. |

**Asset feedback and what changed.** The brief: this should feel like a triple-A game, so nothing on show is made of Unity primitives any more (enemy stand-ins, test dummies and marker shapes excepted).

| Feedback | Change |
|---|---|
| C11 looked like a well | It is the mill yard's stone trough. Rebuilt as a long basin of dressed stone blocks with water in it, with barrels behind. The well is C08, which already is the pack well L2 uses, scaled to the 2 m footprint. |
| Carcasses should be skeletons | C15 is now an ox skeleton on its side (ribs, spine, skull with horns, scraps of hide, a stain under it), generated the way L2 builds its human skeletons. The plough team's two oxen (C05) are skeletons collapsed in the yoke. |
| H01 and H02 looked bad | The crop is now wheat: the pack's reed meshes baked into drilled rows on a tilled bed, re-coloured through a wheat palette, swaying with the project's wind shader. Burning chars the same wheat inside the L2 flames; ash is burnt stubble on an ash bed with a few live embers. Three variants, so a row never repeats cell for cell. |
| Farmhouse looked bad; mix existing things in | Rebuilt from the pack's timber-framed houses at their own scale: their walls cut into panels and laid as the inner and outer skins of the 1 m walls, four of their roofs as a double-pile tiled roof, the pack chimney. Inside: a board floor, a hearth with the fire banked, a table laid for five, the farmer (an L2 corpse) in his chair facing the door, stores along the wall. The roof and south wall fade with the project's own occlusion fade when the player is inside. |
| (same standard applied elsewhere) | Stone roller and millstones are faceted stone drums. The bridge deck is ten courses of worn stone blocks; its piers and the footway's timbers are pack pieces. The sluice gate's leaf is planks, its posts and bar beams. The windmill has a conical cap, lattice sails of planks and stores stacked against it instead of box lean-tos. Gibbets have sack heads like the scarecrows at the end of L2. |

**Second round, same day**

| Feedback | Change |
|---|---|
| The hedges do not look good | K01, K02, K08 and C04 are no longer the pack's clipped garden hedge. Each is a thorn thicket the fire has been through: generated arching canes hung with dead leaves in ochre, rust and grey-olive, over a low core of charred pack bushes, with the pack's dead trees shrunk to thorn stems standing out of the top. They move with the wind shader. |
| M02 is invisible when looked at straight on | The gate's boards were L2's deck planks, which have no underside, so the leaf vanished from one side. Boards stood on edge are now two-sided (the gate leaf and the windmill sails). |
| The theme: gritty, a broken world, like the other levels | Two things, both done the way L1 and L2 do them. **Palettes:** the pack palette textures are re-graded copies now (`L3_Knights_Dawn`, `L3_Adventure_Dawn` and the charred, withered and earth variants): saturation cut by more than half, values pulled down, greens withered to olive-brown, roof tiles dried to the colour of old blood. The wheat is ash-dusted straw, not harvest gold. **Light and grade:** a new `L3LightingPass` (`Assets/Kneel/Lighting/L3`) gives the level's look and the review scene now uses it, so every asset is judged in it: a low sun in the east-south-east (30°, warm, long shadows to screen-left), cold boosted trilight ambient, smoke-coloured linear fog, a dim sallow sky, and a global volume with ACES, exposure +1.7, contrast +20, saturation −34, teal shadows against ember highlights, vignette, bloom and film grain. Fire and embers are the only saturated things on screen. |

Judged from Game-view captures with post-processing on (camera renders skip the grade). The review floor's grid is dark earth now, not pale grey.

**Third round, on the blockout**

| Feedback | Change |
|---|---|
| The level is very square; it should feel organic | Boundary runs wander, lean and vary in height; field corners are rounded; lanes are winding tracks with verges, carried across the fields by worn trails; the land outside rolls and is dressed with grass, stones, stubble, stumps and burnt trees; the ditch has reeds, silt and ragged lips. See "Organic pass" under Phase 2. |
| The mud should have a realistic texture, within the art style | The mud is painted into the ground with a wandering edge and carries a baked low-poly layer of standing pools, slick aprons, clods, hoof pocks and wheel ruts. Its volume and trigger are unchanged. |

**Fourth round, 2026-10-06, on the blockout**

| Feedback | Change |
|---|---|
| The roads must be cohesive; paths end abruptly | One road network instead of a track per lane and a trail per field: a single pale ribbon from the spawn to the knoll and down to the dyke, a second from the landing hollow to the causeway, and branches to the farmer's door, the grave and the mill stair. It is a smooth curve through hand-set points, breathes a little in width, and fades out only where a track really ends (the grave, the dead end of the dyke). |
| Remove the tyre tracks from the path | The painted ruts are gone from every lane, and the baked wheel ruts from the Wallow's mud. |
| Way more crops around; it should look organic | `L3Fields` sows the whole country round the level: rows 0.55 m apart that bend with the burnt strips, dead wheat where the fire went round, charred stubble in the black strips, gaps where the ground is bare. About 49,000 wheat plants and 20,000 stubble plants, baked into 11 meshes. The square stubble patches of the first dressing pass are gone. |
| Points of interest in the paths, so not everything is open | 14 added, all section 14 prefabs, each on a verge with the road bending round it: a rock fall in the sunken lane, abandoned and overturned carts (seven, each with what fell off it scattered round), a carcass, cairns and a boundary stone, a stub of hedge. They narrow the clear way to between 4.8 and 6 m where they stand. Positions are in `L3Plan.Places` under "Points of interest". |

**Fifth round, 2026-10-06**

| Feedback | Change |
|---|---|
| "I do not like the sail hazard. Please remove it." | Removed altogether, not just from the level: the prefab `L3_SailHazard` (H05), the components `SailHazard` and `SailTuning`, its tuning asset, its four materials and two textures, the `SailArc` NavMesh area, `HazardKind.Sail`, its lane in the review scene and its part of the hazard playtest. The scorched band is no longer painted in the mill yard. |
| A staircase up to the windmill | New prefab S08 `L3_MillStair`: twelve worn stone steps 3.6 m wide between stepped cheek walls, from the mill yard up the south face of the mound to a 2 m landing at the mill door. It can be walked. A path branches to its foot. See "Farmland pass" for what that needed. |
| Remove the fire on top of the windmill | Both fires are off the mill (the one on the cap and the one at the hub). It also has its fourth sail back, which had been left off for the hazard. |
| No mud under the hedges: just the hedge | The strip of leaf litter and trodden soil that every hedge piece carried under it is gone (K01, K02, K08, C04). A hedge is canes, leaves and stems, and stands on whatever ground it is put on. |
| The carcass: a bare skeleton, no mud or anything | C15 and the two oxen of C05 are bone only: the stain on the ground and the scraps of hide are gone. |
| Too many walls round the path; fences instead, it should be farmland | Every lane on the level now runs between post-and-rail field fences: 306 fence panels where there were 290 lengths of wall. Stone is kept where it holds something up or belongs to a building: the ramps, the crest and the knoll, the farmstead and mill yards, the causeway. Two new kit pieces, K09 and K10, are the same panel weathered (a rail hanging, a rail on the ground, posts leaning); about one panel in three is one of them. |
| Pull some of the organic fields into the player's path, just a bit | Where a lane is fenced the crop now comes right up to the rails, and in places through them: a fringe at the foot of the fence and here and there a tongue of the same rows reaching up to 3 m into the verge. About 500 plants, never on the road, never under a prop, a spawn point or the mud. |

**Sixth round, 2026-10-06**

| Feedback | Change |
|---|---|
| The brand stake looks weird with the sphere on top | H03's head was a Unity sphere. It is now a brand: pitch-soaked rag bound round the head of the post, held by two iron bands and four straps, its crown glowing under the flame, tails of rag hanging from the lower band. The post, the hit box and the behaviour are unchanged. |
| The mud wallow should use the reflection we get of water in the ground; make it look better | The pools are no longer flat grey polygons laid on the floor. `L3Ground` paints them into the ground's wetness and the project's ground shader fills the hollows of the earth with its own water, the same dark mirror that stands anywhere the ground is wet, with a shoreline that follows the bumps of the earth. The Wallow is about half pond now, in a few large lobed pools; the grave has water in its low places only. The mud between is darker, and `L3Mud` heaps clods along the water's edge. Volume, trigger and NavMesh area are untouched. |
| The water hole is very square and the walls round it do not look good | The ditch. Its flat box sides are colliders only now. What is seen is `L3Ditch`: earth banks that break away from the lip, slump toward the water and stand in it as a foot that wanders between 0.8 and 2.6 m in from each side, so the water is a winding channel 1 to 4 m wide; the cut shallows out and closes at both ends (outside the play space). The banks carry the ground's own material and paint, so the land rolls over the lip. The water is a sheet built for the project's water shader (it had been a stretched quad), in an L3 copy of the creek material with a colour that reads by day; it fades out on depth, so its shore is wherever a bank rises through it. Reeds stand in clumps at the foot of the banks at a third of their old size. Outline, depth, colliders and kill volume are the plan's. |
| (from the owner's own edit in the scene: a bush deleted from under a hedge) | The core of charred pack bushes at the foot of K01, K02 and C04 is gone as well as the litter strip: those lumps were the "mud" under the hedges. The thicket is grown half as thick again so a hedge is still not seen through. |
| (same fault as the brand, found while fixing it) | The three crows on H08 were Unity spheres and a cube. They are a generated faceted crow now (body, head, beak, folded wings, tail, legs). |

Checks after this round: 56 asset rows, 0 failing; read-back matches the plan; the flood finds no step into the void; in play mode stage B (the drop, the bridge, the gate, both fires) and stage C (the fall into the ditch and its kill volume, the walk through the Wallow, the house) pass with 0 console errors.

## Phase 2: blockout

Built 2026-10-05 after the owner's "continue work" on the twice-revised assets, which was taken as approval of Phase 1. All five stages of section 15 are built and checked. Stopped after stage E.

**The scene.** `Assets/Kneel/Scenes/Levels/L3_FallowFields.unity`. Everything is under `L3_Root` (origin, no rotation, no scale), grouped as `Floors`, `Boundaries`, `Props`, `Hazards`, `Markers`, `Spawns`, each with one child group per area ID, plus `_Lighting` from the L3 lighting pass. `Player` and `Main Camera` are root objects, as in L1 and L2.

| | Count |
|---|---|
| Floor pieces | 35 (F01 to F32; F18 is cut in two and F19 in three where the bridge and footway decks lie on them) |
| Boundary pieces | 1,023, all prefab instances: K01 × 219, K02 × 64, K03 × 459, K04 × 48, K05 × 190, K06 × 12, K07 × 29, K08 × 1 (966 before the organic pass, which sets hedges closer and adds corner pieces) |
| Unseen containment | 514 caps over low outer walls and 89 pads under boundary runs, in `Floors/<area>/_Containment` |
| Dressing and mud | 11 baked dressing meshes under `Dressing`; one baked mud mesh each for the Wallow and the grave, and silt in the ditch, under `Floors` |
| Props, hazards and structures from 15.4 | 50, plus the ditch kill volume and the descent's ignite trigger |
| Spawn markers | 20: 12 hounds, 4 brutes, 4 footmen, each with its enemy or stand-in as a child |
| Triggers, tells, pickups | 1 player spawn, 1 exit, 5 vista (V1 to V4 and VG), 5 arena volumes, 3 secret tells, 6 pickups |
| NavMesh | Three surfaces on `L3_Root` (Thrall, Brute, Hound), data in `Scenes/Levels/L3_FallowFields/` |

**How it is built.** Section 15 is typed in as data in `Assets/Kneel/Editor/L3Plan.cs`; `L3Level.cs` builds the scene from it (`Kneel > L3 > Build > Level (replaces the scene)`). The build starts from an empty scene every time, so **hand edits to the scene are lost if it is rerun**: once you start editing the scene by hand, stop using the menu item. `L3LevelChecks.cs` holds the edit-mode read-back and checks, `L3LevelPlay.cs` the play-mode ones (it walks the real player prefab along the route by writing its cached input).

**How to look at it.** Open the scene and press Play: the player starts at the spawn in the sunken lane. Marker volumes are magenta in the editor and hidden in play mode. Nothing spawns or fights: enemies are stand-ins at their markers, and the placed footmen have their AI switched off (see "Beyond the note" 13).

### Beyond the note

Things that are not in section 15 as written. None of these changes a position, a size, a count or a timing. (The owner's later review rounds do change the design; those are listed under "Farmland pass" below.)

1. **Painted ground instead of two flat colours.** Every floor uses one ground material (`L3_Ground`, the project's `Kneel/Ground` shader, as L1 and L2 do) painted from the plan: lanes are pale trodden chalk with cart ruts and are the lightest ground in frame, arena floors are dark stubble, and the land outside is burnt in strips. Also painted: the flail ring and the single furrow in E2, wet ground under the two mud volumes, lime round the grave mound and along the cart track. Flat lane and arena colours rendered as near-black slabs under the level's grade.
2. **A backdrop.** `Floors/_Backdrop` is a sheet with no collider, on `Default`, not baked, just under every floor. It rises to the top of the banks round raised ground, so the crest and the knoll read as hills and the sunken lane as a cutting, and the level does not hang in a void. The note leaves the vista terrain out of this handover; this is not that terrain and can be deleted without affecting anything.
3. **Diagonal lanes are trimmed to their neighbours.** F10 and F12 are the given centre, width and yaw, but their ends are cut along the edges of the floors they join instead of square. As plain rotated boxes they overlap one arena and leave a wedge of missing floor at the other end (about 1.8 m deep at the foot of the knoll ramp).
4. **Verges cut under the decks.** F18 and F19 are split where the bridge and footway decks lie on them, so no two walkable faces share a plane.
5. **Solid blocks.** The mill mound and the knoll bank are a box collider with a painted top, faced with K05 pieces inside their own outline, so the outline is exactly the one in 15.2.
6. **The sunken lane.** Its banks are two courses of K05 (Y −1 to 2 and 2 to 5), since one piece is 3 m tall. A K03 run closes its south end: the table gives none there, and without it the player walks off the south end of the level.
7. **Corners and odd lengths.** West and east runs carry on one piece past a free corner so the corner square is closed. Where a run is not a whole number of 2 m pieces, the pieces overlap. Two runs asking for the same piece keep one.
8. **Walls on ramps** are level pieces stepped down the slope, each at the ramp's height at its middle, standing on banks that follow the ramp.
9. **Kit pieces the table does not name:** the D1 path's sides (K01), the grave track (K02 south, K01 north), the house path's two 1 m gaps between the yard wall and the house (K03 south, K04 north), the 2 m of E5's north edge west of the mound (K01), and two K05 behind the cellar front as the bank it is cut into.
10. **`L3_IgniteTrigger`** (`Prefabs/L3/Hazards`, component `Kneel.Hazards.RowIgniteTrigger`) lights the demonstration row when the player enters x 60–68, y 44–46, as 15.4 asks. It is not a section 14 row.
11. **Mud materials.** `L3_GB_Mud` and `L3_GB_Damp` were flat colours, which the grade turned into black rectangles on the painted ground. They now use the ground shader, which changes how H04 looks in the review scene. In the level the mud's plane is not drawn at all since the organic pass: the paint and the mud meshes take its place. (The sail's two materials were treated the same way until the sail was cut.)
12. **Player and camera.** The scene carries the project's `Player` prefab and a `Main Camera` with `PlayerCamera` and the shared `PlayerCameraSettings` asset, plus `OcclusionFader`, exactly as the review scene and L2 do. No camera script or setting was changed. If a persistent systems scene will supply them, delete both.
13. **Placed footmen are switched off.** 15.5 asks for the enemy prefab as a child of each marker. The real footman would hunt the player from the first frame and has no NavMesh of its own agent type here, so its behaviours are disabled on the four instances. Hounds and brutes are the collider-only stand-ins.
14. **NavMesh details.** Boundary and cover pieces, the solid blocks, the trench and the ditch carry a Not Walkable modifier, so their tops do not bake as islands. Agent step height is 0.4 m for all three types. Adding the Thrall, Brute and Hound agent types changed `ProjectSettings/NavMeshAreas.asset` again.
15. **The demonstration row is at Y = 0** (15.4 gives no height), so it lies at the foot of the descent's bank: 3 m below the lane where the fire starts, level with it from y 58.
16. **Secret tells stand on posts.** The three tells use the prefab's optional post, at the given positions. D2's position (189.5, 269.5) is half a metre off the house wall, so a bare decal would hang in the air.
17. **VG** is a `L3_VistaTrigger` at the gate with the pan target (140, 3, 212) and 2 s. It is data: nothing connects it to `SluiceGate.Opened`.

### Organic pass (owner review of the blockout, 2026-10-05)

The owner's note on the first blockout: the level is very square and should feel organic, and the mud should have a realistic texture that fits the art style. Nothing in section 15 moved: floors, openings, props, hazards, markers and spawn points are where they were. What changed is everything the eye reads as a ruler.

| What read as square | What it is now |
|---|---|
| Boundary runs: identical pieces in dead-straight rows | Each run wanders outward along its length (hedges by up to 0.75 m, drystone by up to 0.3 m), never inward, and comes back to its line at both ends. Each piece turns with the run, leans a few degrees and sits at its own height; hedges are set closer together so turned pieces still meet. The piece at each end of a run is a gatepost and stands true, so openings keep their width. Kerb stones along the dyke are skewed, sunk and here and there missing. |
| Square field corners | A hedge piece set across each corner of a hedged field rounds it off. |
| Lanes as filled rectangles of one colour | A lane is a pale track that winds between its walls, with two cart ruts and a darker, trodden verge either side. Worn trails carry the track on across the fields from gate to gate (E1a, E1, the orchard, E2, the landing hollow and E3's middle lane, E4 and the branch to the house, E5 to the exit). |
| Field floors ending at the hedge line | The stubble spills past the hedges in an uneven edge. The burnt strips outside lean, swell and break instead of running as parallel bands. |
| Flat land outside | The backdrop rolls (up to about a metre) away from anything built. |
| Bare ground | `Dressing`: dead grass at the foot of every wall and hedge and in the lane verges, stones fallen from the drystone, pebbles; outside the walls, patches of burnt stubble and of dead wheat the fire went round (the level's own crop meshes), rocks, stumps, dead bushes, logs and bare burnt trees; reeds along both sides of the ditch, silt bars at its waterline and ragged grass and stones along its lips. About 4,100 clumps of grass, 900 stones, 310 stumps, bushes, logs and trees and 100 reeds, baked into 11 meshes (0.93 million triangles in all, a few bands in view at once) on the level's re-graded palettes, with a new straw-coloured grade for the grass. No colliders. Nothing taller than a clump of grass stands inside the play space, and no tree stands on the camera side of a floor. |

**The mud.** In the level the mud is no longer a rectangle laid on the floor. The ground paint darkens and wets the earth across the volume's footprint with a border that wanders by about a metre and thins out through the damp band, and `L3Mud` lays the solid part on it as one baked low-poly mesh per mud area: pools of standing water holding the grey sky, each ringed by a dark slick apron; faceted clods squeezed up round the pools; the pocks of hooves; and through the Wallow two wheel ruts with water lying in stretches of them. Flat-shaded and faceted like the rest of the level, three plain materials, no colliders. The mud volume itself (trigger, size, NavMesh area) is untouched, and its own plane and damp bands are simply not drawn in the level; in the review scene the prefab looks as before.

**Containment, and a hole the first blockout had.** Testing for this pass showed that the player can jump onto the 1.0 m lane walls: the jump is 0.9 m and the character controller steps up the rest. From the top of a wall the player walks off the level. This was true of the blockout as first delivered. Two kinds of unseen collider now close the level, both on `Ignore Raycast` so neither the mouse aim nor the NavMesh bake sees them, and both under `Floors/<area>/_Containment`:

- **Caps** (514): a box over each low outer piece (K02 and K03), from just under its top to 3 m above. The player can no longer get onto a low wall.
- **Pads** (89): ground under each boundary run that stands off the floor's edge, so a wall that has wandered 0.3 m outward does not leave a crack to fall into.

Making the walls taller, or blocking the jump near them, would do the same job; both change things this handover does not own.

**Checks after the pass**

| Check | Result |
|---|---|
| Read-back | 35 floors, 50 placed prefabs, 1,023 boundary pieces (K01 × 219, K02 × 64, K03 × 459, K04 × 48, K05 × 190, K06 × 12, K07 × 29, K08 × 1), 20 spawn markers and all triggers match the plan. |
| Containment | The old check walked the floor edges, which no longer coincide with the walls. The new one floods everywhere a body the player's size can walk from the spawn on a 0.2 m grid (stepping up 0.3 m, dropping any height): 8,296 m² reachable, nowhere more than 0.50 m beyond a floor edge, **no step into the void**. It found a real gap while this pass was being built: at one corner a duplicate filter had removed the end piece of a lane wall, and the trace led straight to it. That is closed. Pads were also trimmed so that none reaches past the end of its run. |
| Lanes are 8 m wide | Every lane is at least 8.00 m between walls at every sample (up to 8.6 m where a wall has wandered out); the causeway and the knoll's north ramp at least 4.00 m. Diagonal mouths as before (7.2 m). |
| Openings | All fifteen clear to full width, except the forecourt's west opening, where the two hedge ends of the grave track lean 5 to 7 cm into the 4 m gap. |
| South edges | As before: only K02 and K03 stand on south edges. The check also lists tall side hedges where they end at a south corner. |
| Stage C and D geometry | Unchanged: the farmhouse opens only at its door, and the Wallow's verge is still dry (Questions 14). (The sail checks made here no longer apply.) |
| NavMesh | Rebaked. Paths: spawn to Shrine A 233 to 237 m, F16 to the exit 259 to 264 m, Shrine B to E5 14.7 m. Mud and CropRow areas present. |
| Play mode | **Pass.** One run of the jump test and all five stages back to back (327 s of game time, with every marker, trigger and the dressing in the scene): 0 failed checks, 0 console errors. The editor held 2 to 4 ms a frame. |
| Jumping at a wall | **Pass.** Fifteen running jumps at five low boundaries (a lane's west and east walls, a field's south hedge, the knoll's kerb, the yard's south wall), taken 1.4, 1.0 and 0.6 m before the wall. None got onto or over it; every one ended standing on the ground at the foot of the wall. (A first version of the caps let the player come to rest perched on a wall's lip under the cap; the caps now carry a lip above head height that cuts a jump beside a wall short.) |

### Farmland pass (owner review, 2026-10-06)

The fourth and fifth review rounds, listed under "Owner review" at the top. This is the current state of the level.

**What now differs from the design note.** The note (sections 5, 12, 14.4, 15.3 to 15.5) still describes these as they were; it has not been edited.

1. **No sail hazard.** H05 and everything behind it are deleted. E5 is now a fight in an open yard with cover, the mill above it.
2. **The mill stair, S08.** Foot at (174, 334.6), climbing north to the mound's edge at y 340, landing from y 340 to 342 at the mill door. It is walkable: a ramp collider under the steps and the landing are on `Ground`, the cheek walls on `Obstacles`. The mound is a solid block, not a floor, and nothing closes its far sides, so two unseen walls along its south edge either side of the landing and caps over the landing's cheek walls keep the player on the stair (under `Floors/E5/_Containment`). The landing is a dead end at a shut door.
3. **E5's brute stands at the foot of the stair,** at (174.5, 331), not at the mill door (174.5, 337.5), which is now on the steps.
4. **Fences instead of walls** on F04, F05, F06, F10, F11 (south, west, east), F12, F16, F17 (west), F18a, F20, F22, F25, F26, F29 and F31. K03 stays on F01, F02, F03, F13, F14, F15 (raised), on the south sides of F23, F24 and F30, and on the causeway F32.
5. **14 points of interest** stand in lanes the note leaves empty.
6. **Crops inside the play space.** The wheat and stubble on the verges have no collider and do not burn. They are thinner and lower than a crop row and stand on no tilled bed, but they are the same plant as the rows that do burn (Questions 26).
7. **The backdrop meets level floors flush** (4 cm under them instead of 30), so there is no step in the ground behind a fence.

**Counts**

| | Count |
|---|---|
| Boundary pieces | 1,058: K01 × 220, K02 × 64, K03 × 169, K04 × 48, K05 × 190, K06 × 231, K07 × 60 (29 along the dyke, 31 new along the causeway), K08 × 1, K09 × 44, K10 × 31 |
| Unseen containment | 518 caps (every low outer hedge, wall and fence panel) and 89 pads, plus the six boxes at the mill stair |
| Placed prefabs | 64: the 50 of 15.4 less the sail, plus the stair and the 14 points of interest |
| Fields | 49,002 wheat plants and 20,408 stubble plants in 11 baked meshes (610,000 triangles), 512 of them on the lanes' verges |
| Dressing | 4,175 clumps of grass, 747 stones, 307 stumps, bushes, logs and trees, 104 reeds in 11 baked meshes (665,000 triangles) |

**Checks on this build**

| Check | Result |
|---|---|
| Read-back | 35 floors, 64 placed prefabs and 1,058 boundary pieces match the plan; every boundary piece is a prefab instance; no missing scripts or broken materials. |
| Containment | Flood from the spawn on a 0.2 m grid: 8,095 m² reachable, at most 0.50 m beyond a floor edge, **no step into the void**. |
| Openings | All fifteen clear to full width (the forecourt's west opening 3.8 m of 4, as before). |
| Lanes | Fenced lanes are 8.2 to 8.5 m between fences (a fence stands on the floor's edge, where a wall stood half a metre out). Past a point of interest the clear way is 4.8 to 6.0 m. Causeway 4.1 m, knoll north ramp 4.0 m. |
| The mill stair | Ground rises evenly from Y 0.00 at the foot to 3.02 on the landing, with head room all the way; the landing is clear; the mound top either side of it is closed. |
| Stage C | Unchanged: the farmhouse opens only at its door; the Wallow's verge is still dry (Questions 14). |
| Stage E | 20 spawn markers match the plan (with the brute's new place); 1 spawn, 1 exit, 5 vistas, 5 arena volumes, 3 tells, 6 pickups. NavMesh rebaked for the three agent types; Mud and CropRow areas present. Paths: spawn to Shrine A 235 to 238 m, F16 to the exit 261 to 264 m, Shrine B to E5 14.7 m. |
| Asset prefabs | 56 rows, 0 failing. |
| Hazard playtest | **Pass**, 0 failed checks, 0 console errors, 42 s of game time. |
| Play mode | **Pass.** The jump tests and all five stages back to back (382 s of game time): 0 console errors, and every walk, the drop off the knoll, the gate, both fires, the ditch, the house, the mill stair up and down (the landing's cheeks and the mill stop the player) and the causeway as expected. One jump test ran into a new cart before it reached its fence and was moved north of the cart; rerun, all 24 running jumps at eight low boundaries (two lane fences, the dyke lane's fence, both cheeks of the stair landing, a south hedge, the knoll's kerb, the yard wall) ended standing on the ground inside the level. |

The stage entries below are from the first build of each stage. Later passes re-ran every check; where a number changed, the tables above have the current one.

### Stage A: F01 to F14

| Check | Result |
|---|---|
| Read-back | 14 floors, 19 placed prefabs and 499 boundary pieces match the plan (position, layer, source prefab). No loose objects, missing scripts or broken materials. |
| The player can walk from the spawn to Shrine A | **Pass.** The player prefab ran spawn → crest → descent → E1a → E1 → E2 → knoll: 249 m in 67 s, ending at (140, 3.0, 207.4). The demonstration row was dry before the trigger and lit when the player crossed it. The orchard gap was walked through and back (K08 does not block). NavMesh paths spawn → Shrine A: Thrall 233 m, Brute 237 m, Hound 236 m. |
| Lanes are 8 m wide between walls | **Pass.** F01, F03, F04, F06, F13: 8.00 m at every sample. The diagonals F10 and F12 are 8.00 m between their own walls and 7.1 to 7.2 m at their mouths, where a diagonal lane meets a square 8 m gate. |
| Nothing on a south edge is taller than 1.2 m | **Pass.** South edges carry K02 (1.2 m) or K03 (1.0 m). The check also lists the D1 path's side hedges (K01), which end at the orchard's south corners; they stand on the path's east and west edges. |
| E2 has no prop inside 12 m of (114.5, 131) | **Pass for props:** none. The nearest prop is the plough team, 12.4 m to its nearest corner. Note that the north hedge is 11 m from the brute, as the plan places it. |
| Edges | A capsule the player's size was carried round every floor edge (6142 samples): no place where the player can step off a floor. All five listed openings are clear to their full width. |

### Stage B: F15 to F19, the ditch, bridge, footway and gate

| Check | Result |
|---|---|
| Read-back | 22 floor pieces, 30 placed prefabs, 630 boundary pieces match. Ditch x 88–182, y 232–238, bed at Y = −3, vertical sides, standing water, one `L3_DitchKill` from Y = −3 to −0.5. |
| Walking off the knoll's west edge lands on F16, and F14 cannot be reached again | **Pass.** The player walked off at y 206 and landed at (126.6, 0.0, 206.0). Walking back at the bank it stopped at x = 129.8, Y = 0. The bank's K05 pieces stand on the hollow (x 130–132), so the player steps off their top rather than off the floor edge itself. |
| The gate blocks the footway from the south | **Pass.** Down the north ramp and along the footway the player stopped at z = 239.6; the gate refused to open from there. |
| Both live rows burn south to north | **Pass.** West: first cell lit 1.9 s after the knock, last at 16.9 s. East: 1.9 s and 16.5 s. Cells lit in order; neither fire reached the other row. |
| The lanes between rows are 8 m wide | **Pass** by position: west wall at x 92, rows at x 100–102 and 110–112, east hedge at x 120. |
| Also | The fallen stake and the ash row west of the fence read back as placed (row started in Ash, stake started fallen). The rail on the south verge stops at the bridge, x 103–109. |

### Stage C: F20 to F24

| Check | Result |
|---|---|
| Read-back | 27 floor pieces, 39 placed prefabs, 747 boundary pieces match. 36 kerb stones at y 238.5, none on the two decks. |
| The dyke's south edge is open to the ditch, and falling in triggers the kill volume | **Pass.** Walking south off the lane at x 124 the player fell to Y = −3.0 and its health went from 100 to 0 through `IDamageable`. (Nothing in the project handles death, so it then regenerates.) |
| The mud volume covers the lane fully, so it cannot be walked round | **Fails as specified.** The mud is x 148–168, y 240–254 as written, and covers the dyke lane and the Wallow flat. The north verge F19 (y 238–240) runs along its south side and is not mud: 2.0 m of dry ground between the mud and the ditch edge, the whole length. See Questions 14. |
| The house can be entered only through the yard's east door | **Pass.** A flood from the middle of the house, held to the house and 0.6 m round it, leaves the walls only at x 189.5–189.9, y 270.3–273.7: the door. The player walked from the yard through the door to (195.4, 272) and back out. |
| Also | E4's three openings are clear to full width. The check on south edges lists the yard's east wall (K04) where it ends beside the house path; it is not on a south edge. |

### Stage D: F25 to F32, the mound and mill

| Check | Result |
|---|---|
| Read-back | 35 floor pieces, 50 placed prefabs, 966 boundary pieces match. |
| The sail's hit boxes lie inside the yard and no cover overlaps the band | No longer applies: the sail hazard was cut on 2026-10-06. (It passed when built.) |
| The exit can be reached by going round either end of the arc | No longer applies. The player now walks round the west of the yard past the foot of the mill stair, and round the east, to the north opening. |
| The causeway is 4 m wide | **Pass.** 4.00 m between walls at all 36 samples. |
| Shrine B and the cellar front | Placed as written, the shrine facing south like Shrine A. The shrine's plinth and torch stand directly in front of the cellar doorway, as expected from Phase 1 (Questions 5): it reads as a shrine set up at the cellar mouth, and the lane past it is clear (x 170–175). Turning the shrine does not move it out of the doorway, so it was left facing the camera. |
| Also | A first build left two 0.6 m holes in the forecourt's east wall beside the cellar front; the edge check found them and they are closed. D3's mud covers the whole arena; the trench is a dark block 0.3 m high. |

### Stage E: spawn markers, triggers, tells, pickups, NavMesh

| Check | Result |
|---|---|
| Counts match 15.5 | **Pass.** 12 hounds, 4 brutes, 4 footmen; every marker matches its row (type, encounter, position, facing south) and carries its enemy as a child. |
| 15.6 | 1 player spawn at (64, 2) facing +Z, 1 exit trigger, V1 to V4 and VG with their values, arena volumes on F07, F17, F23, F30 and F28 (6 m high), 3 tells, 6 pickups, the two fade markers on the farmhouse. |
| Surfaces | Thrall (0.5, 2.0), Brute (1.0, 2.6), Hound (0.4, 0.9), each collecting only `Ground` and `Obstacles` under `L3_Root`, generated links off. Mud and CropRow areas are present under both mud volumes and the two live rows. |
| A path exists from the spawn to Shrine A, and from F16 to the exit | **Pass.** Spawn → Shrine A: 233, 237, 236 m (Thrall, Brute, Hound). F16 → exit: 274, 273, 268 m. |
| None exists from F14 to F16, or across the footway while the gate is closed | **Pass**, checked in play mode, because the gate and the fire cells carve at runtime and are not baked in. With the gate shut, all three agent types get no complete path across the footway and none from the knoll to the landing hollow. |
| With the gate open, Shrine A to E4's south gate is 65–80 m | **Pass.** 76.8 m (Thrall). Across the footway itself the path is 7.0 m once the gate is open. |
| Shrine B to E5's south opening is 14–20 m | **Pass.** 14.7 m for all three. |
| No compile errors, no console errors in play mode | **Pass.** No compile errors. One play-mode run of all five stages back to back (281 s of game time: every walk, the drop, the gate, both fires, the ditch, the house, both ways round the arc, the causeway, with all spawn markers and triggers in the scene): 0 failed checks, 0 console errors. |

## How to review the assets (Phase 1)

1. Open `Assets/Kneel/Scenes/Levels/L3_AssetReview.unity`.
2. The rows run south to north in table order (14.2 at the bottom). Each item has its ID, prefab name and status on the floor in front of it. Each row starts with a 1.8 m capsule and a 2 m bar marked "1 R".
3. The hazard test lane is the northern third. Press Play: the player stands at the south end of the lane between the two crop rows.
   - Hit a brand stake with the sword (R to draw, left mouse to strike), or select it and press **Knock** in the inspector.
   - The gibbet on the right wakes when the player comes within 8 m.
   - Walk onto the footway from the south: the gate reads "Barred from the other side". Cross the stone bridge and press E at the gate's north side.
   - The magenta capsules are `HazardTestDummy` instances. Each logs every contact to the console with what it would mean for that kind of actor.
4. Everything is rebuilt by `Kneel > L3 > Build > Asset Prefabs` and `Kneel > L3 > Build > Asset Review Scene`. Both are safe to rerun; the tuning assets in `Assets/Kneel/Settings/L3` are never overwritten.

## Survey

**Engine and packages**

| | Found |
|---|---|
| Unity | 6000.6.0f1 |
| Render pipeline | URP 17.6.0 (`PC_RPAsset`, Forward+ renderer) |
| ProBuilder | **Not installed.** Floors and walls will be scaled cubes, as the note allows. |
| AI Navigation | Installed (2.0.14) |
| Cinemachine | Installed (through the Characters and Animation feature set), but not used: the gameplay camera is the project's own `PlayerCamera` script |
| TextMeshPro | Installed (in `com.unity.ugui` 2.6.0, Essentials imported) |

**Folder conventions.** The project's own level content lives under `Assets/Kneel`, split by kind and then by level: `Prefabs/L1`, `Prefabs/L2`, `Materials/L1`, `Meshes/L2`, `Settings/L2`, and so on. Runtime scripts are in `Assets/Kneel/Gameplay` (namespace `Kneel`). Each level is built by editor scripts in `Assets/Kneel/Editor` (`L1*.cs`, `L2*.cs`, namespace `Kneel.EditorTools`). Level scenes are in `Assets/Kneel/Scenes/Levels`. L3 follows the same pattern instead of the note's fallback `Assets/Kneel/Levels/L3`:

| The note's folder | Where it is |
|---|---|
| `Prefabs/Kit`, `Props`, `Structures`, `Hazards`, `Markers`, `Enemies` | `Assets/Kneel/Prefabs/L3/<same names>` (plus `Review` for the test dummy) |
| `Scripts` | `Assets/Kneel/Gameplay/Hazards` (namespace `Kneel.Hazards`) and `Assets/Kneel/Gameplay/Markers` (namespace `Kneel.Markers`) |
| `Materials` | `Assets/Kneel/Materials/L3` |
| `Data` | `Assets/Kneel/Settings/L3` |
| (builders) | `Assets/Kneel/Editor/L3Build.cs`, `L3Look.cs`, `L3Assets.cs`, `L3Mesh.cs`, `L3Bones.cs`, `L3Crops.cs`, `L3Hedges.cs`, `L3Farmhouse.cs`, `L3Review.cs`, `L3Checks.cs`, `L3Playtest.cs`, `L3HazardInspectors.cs` |
| (lighting) | `Assets/Kneel/Lighting/L3/Editor/L3LightingPass.cs` and `Volumes/L3_Global.asset`, as L1 and L2 keep theirs |
| This report | `Assets/Kneel/Prefabs/L3/L3_AssetReport.md` |

**Asset packs** (all under `Assets/SyntyStudios` unless noted; none were edited)

| Pack | Prefabs | Used for |
|---|---|---|
| PolygonKnights | 364 | Rock wall (K03, K04, K05, bridge parapets), rock pile (C03), hay cart and cart (C01, C13), well (C08), beams (stakes, gibbets, plough, roller), round tower pieces and door (S04), tree (S06), archway (S07), chimney (S03), iron gate as cage bars (C02) |
| PolygonAdventure | 283 | Hedge and bush (K01, K02, K08, C04), fence (K06), stone block (K07), carts, sacks, crates, baskets (C01, C02, C07, C10, C13, C14), dirt mound (C12), dead tree, log and stump (S05), scroll and flask (M05, M06) |
| PolygonPrototype | 502 | The apple (M07) |
| PolygonCity, PolygonStarter, PolygonSamples | 344, 58, 10 | Nothing: modern-city and sample content |
| `Thirdparty/Gridbox Prototype Materials` | — | Nothing (the review floor uses its own 2 m grid) |
| `Toon_RTS_demo` | 1 model | Nothing |

Not in any pack: animals (ox, hound, crow), a plough, a roller, a seed drill, millstones, a trough, a windmill, a cage cart, a cellar. Those rows are `BUILT`.

**Existing gameplay pieces**

| Piece | Found |
|---|---|
| Player | `Assets/Player/Player.prefab`: `CharacterController`, `PlayerMovement`, `PlayerCombat`, `PlayerEquipment`, `Health`. On the `Player` layer. |
| Roll | `PlayerCombatSettings.dodgeDistance` = **3 m**, 0.85 s long, invulnerable from 0.07 s to 0.43 s. The note assumes R = 2 m. |
| Player speed | Sprint 3.97 m/s (the note assumes 4). Walk 1.44 m/s forward, which matches the note's fire front at 1.5 m/s. |
| Damage interface | `IDamageable.TakeHit(DamageInfo)` in `Assets/Combat`, with `DamageInfo.isEnvironmental` for world damage. Also `IParryable`, `IStaggerable`. `Health` holds hit points; the player's regenerates to full after 3 s, so nothing dies yet. |
| Enemies | `Assets/Enemies/AshenFootman/AshenFootman.prefab` (state machine on a `NavMeshAgent`, radius 0.25, height 1.5; it has no collider). `TargetDummy`. **No hound and no brute.** |
| Shrine | `Assets/Kneel/Prefabs/L1/L1_CheckpointShrine.prefab`: looks only, no checkpoint logic. |
| Pickups | `L2_Loot_Heal`, `L2_Loot_Weapon`: looks only, no pickup logic. |
| Interaction | An `Interact` action (E) in `PlayerControls`. L2's `GateLever` reads it and opens a `ShortcutGate`. There is no general interaction system. |
| Respawn and reset | None. No death, checkpoint, save or reset code exists. |
| Fire | L2's `Kneel.FireHazard` (a static burning patch) and the `L2Fire` flame effect. |
| Fading | `Kneel.OcclusionFade` and `OcclusionFader` fade tall scenery that hides the player. |

**The camera** (read only; nothing was changed). `PlayerCamera` on Main Camera, tuned by `Assets/Player/Settings/PlayerCameraSettings.asset`: pitch 65°, yaw 0, distance 8.5 m (zoom 3.2 to 8.5), vertical field of view 60°.

- Up-screen is world **+Z**, as the note assumes.
- Ground in frame at full zoom-out and 16:9: **13.2 m deep and about 20 m wide at the player's row** (15.6 m wide at the bottom edge, 27 m at the top). It reaches 8.8 m up-screen of the player and 4.4 m down-screen. The player's feet are 46% of the way up the screen.
- Pushing the pointer against a screen edge pans the view up to 10 m that way.
- The note assumed about 32 × 20 m with 13 m up-screen. See Questions 2.

**Layers, tags, NavMesh**

| | Before | Added for L3 |
|---|---|---|
| Layers | 6 Ground, 7 Obstacles, 8 Enemy, 9 Player, 10 FadeProbe (plus Unity's own) | **11 Hazard** |
| Tags | Unity's defaults only | none |
| NavMesh agent types | Humanoid (radius 0.5, height 2, step 0.75, slope 45°) | **Thrall** (0.5, 2.0), **Brute** (1.0, 2.6), **Hound** (0.4, 0.9), step 0.4, slope 45°, added in Phase 2 |
| NavMesh areas | Walkable, Not Walkable, Jump | **3 Mud** (cost 3), **4 CropRow** (cost 2). (5 SailArc was added and removed again with the sail.) |

Adding the layer and the areas changed `ProjectSettings/TagManager.asset` and `ProjectSettings/NavMeshAreas.asset`.

## Layer and area mapping

| The note's layer | Project layer | Why |
|---|---|---|
| Walkable | `Ground` (6) | Existing equivalent. |
| Boundary | `Obstacles` (7) | Existing equivalent. |
| Cover | `Obstacles` (7) | The player's mouse aim only hits `Ground` and `Obstacles`, so cover on a new layer would be invisible to aiming. |
| Hazard | `Hazard` (11), new | No equivalent. Hazard triggers sit here; nothing is solid on it. |
| (hit by the sword) | `Enemy` (8) | The sword's `WeaponHitbox` only looks at this layer, so the brand stake's and crowed gibbet's hit boxes are triggers on it. Stand-ins and test dummies are on it too. |
| (marker volumes) | `Ignore Raycast` (2) | Vista, arena and exit triggers. |
| (not baked) | `Default` (0) | The kerb stone, the passable hedge, and the sluice gate's blocker, which must not be baked into the NavMesh. |

Hazards find actors on `Player` and `Enemy` (set in `L3_HazardTuning.actorMask`). The gibbet looks for the player on `Player` (`L3_GibbetTuning.playerMask`).

NavMesh areas are as in section 12. One detail for stage E: `NavMeshSurface` only collects a modifier volume if the volume's object is on a layer the surface bakes. Each hazard therefore carries its volume on a child called `NavArea` on `Ground`, so a surface that collects only `Ground` and `Obstacles` still picks it up. This was found and fixed in the review scene (the first bake had all three areas as plain Walkable).

## Asset table

Footprints are X × Z × height in metres. "Collider read back" is measured from the saved prefab by `L3Checks`.

### 14.2 Kit

| ID | Prefab | Status | Spec | Collider read back | Layer | Source | Deviations and notes |
|---|---|---|---|---|---|---|---|
| K01 | `Kit/L3_Hedge_Tall.prefab` | ADAPTED | 2 × 1 × 2.5 | 2 × 1 × 2.5 | Obstacles | Generated thorn thicket (L3Hedges); PolygonAdventure SM_Env_Bush_02-04 (core), SM_Env_TreeDead_01/02 (stems) | A burnt field hedgerow, not the pack's clipped garden hedge: a thicket of arching canes hung with dead leaves over a dark core of charred bushes, with bare thorn stems standing out of the top. It moves with the wind shader. |
| K02 | `Kit/L3_Hedge_Low.prefab` | ADAPTED | 2 × 1 × 1.2 | 2 × 1 × 1.2 | Obstacles | Generated thorn thicket (L3Hedges); PolygonAdventure SM_Env_Bush_02-04 (core), SM_Env_TreeDead_01/02 (stems) | The same hedgerow cut down to 1.2 m, for south edges. |
| K03 | `Kit/L3_StoneWall_Low.prefab` | ADAPTED | 2 × 1 × 1 | 2 × 1 × 1 | Obstacles | PolygonKnights SM_Bld_Rockwall_Straight_01 | The same rock wall as K04, scaled non-uniformly down to 2 × 1 × 0.85 so low and tall runs match. The mesh is 0.85 m thick inside the 1 m collider. |
| K04 | `Kit/L3_StoneWall_Tall.prefab` | ADAPTED | 2 × 1 × 2.5 | 2 × 1 × 2.5 | Obstacles | PolygonKnights SM_Bld_Rockwall_Straight_01 | Pack rock wall is 4.0 long × 2.7 × 1.5; turned a quarter and scaled non-uniformly to 2 × 2.5 × 0.9. |
| K05 | `Kit/L3_Bank.prefab` | ADAPTED | 2 × 2 × 3 | 2 × 2 × 3 | Obstacles | PolygonKnights SM_Bld_Rockwall_Straight_01 | The packs' cliffs are 18 m and more across, so the bank is the rock wall scaled non-uniformly to 2 × 2 × 3 and earth-tinted. |
| K06 | `Kit/L3_Rail.prefab` | ADAPTED | 2 × 0.2 × 1 | 2 × 0.2 × 1 | Obstacles | PolygonAdventure SM_Bld_Fence_02 | Pack fence is 1.6 × 1.25 × 0.2; lengthened to 2 m and lowered to 1 m. |
| K09 | `Kit/L3_Rail_Broken.prefab` | ADAPTED | (owner's review) | 2 × 0.2 × 1 | Obstacles | PolygonAdventure SM_Bld_Fence_02, taken apart | Not in the note. The same panel as K06 after a few winters: the top rail is off and lies at its foot, one rail hangs by one end, both posts lean. Same collider as K06, so a run stays closed. |
| K10 | `Kit/L3_Rail_Leaning.prefab` | ADAPTED | (owner's review) | 2 × 0.2 × 1 | Obstacles | PolygonAdventure SM_Bld_Fence_02, taken apart | Not in the note. As K09: the whole panel pushed over a little, one rail gone, the others slipped in their posts. |
| K07 | `Kit/L3_KerbStone.prefab` | ADAPTED | 1 × 0.5 × 0.3 | none | Default | PolygonAdventure SM_Prop_StoneBlock_01 | Pack stone block scaled non-uniformly to 1 × 0.5 × 0.3 and whitewashed. No collider, as specified. |
| K08 | `Kit/L3_Hedge_Passable.prefab` | ADAPTED | 2 × 1 × 2.5 | none | Default | Generated thorn thicket (L3Hedges); PolygonAdventure SM_Env_Bush_02-04 (core), SM_Env_TreeDead_01/02 (stems) | The same hedgerow burnt thin: no core, fewer canes, bare stems, gaps to see through. No collider, as specified; on Default so the NavMesh bake ignores it. |

### 14.3 Cover and props

| ID | Prefab | Status | Spec | Collider read back | Layer | Source | Deviations and notes |
|---|---|---|---|---|---|---|---|
| C01 | `Props/L3_HayWagon.prefab` | ADAPTED | 3 × 2 × 1.8 | 3 × 2 × 1.8 | Obstacles | PolygonKnights SM_Prop_CartHay_01; Adventure sacks, crate | Hay cart turned east-west at 0.93 scale (3.0 × 1.55 × 1.4), with sacks and a crate filling the 2 m depth. |
| C02 | `Props/L3_KennelWagon.prefab` | BUILT | 3 × 4 × 1.8 | 3 × 4 × 1.8 | Obstacles | PolygonAdventure SM_Prop_Cart_02; PolygonKnights iron gate (as cage bars) | No cage cart in the packs. A cart rolled onto its side with a cage made from iron-gate panels, and one panel bent outward. |
| C03 | `Props/L3_Cairn.prefab` | ADAPTED | 2 × 2 × 1.2 | 2 × 2 × 1.2 | Obstacles | PolygonKnights SM_Env_RockPile_03 | Pack rock pile (5 × 5 × 6) scaled non-uniformly down to 2 × 2 × 1.2. |
| C04 | `Props/L3_HedgeStub.prefab` | ADAPTED | 6 × 2 × 1.5 | 6 × 2 × 1.5 | Obstacles | Generated thorn thicket (L3Hedges); PolygonAdventure SM_Env_Bush_02-04 (core), SM_Env_TreeDead_01/02 (stems) | Six metres of the same hedgerow, two bushes deep, 1.5 m high. |
| C05 | `Props/L3_PloughTeam.prefab` | BUILT | 4 × 2 × 1.4 | 4 × 2 × 1.4 | Obstacles | Generated ox skeletons (L3Bones); PolygonKnights SM_Prop_Beam_01 | No ox or plough in the packs. Two ox skeletons collapsed in a beam yoke, with a beam plough behind them. |
| C06 | `Props/L3_StoneRoller.prefab` | BUILT | 2 × 2 × 1.2 | 2 × 2 × 1.2 | Obstacles | Generated faceted stone drum; PolygonKnights SM_Prop_Beam_01 | No roller in the packs. A faceted stone drum 1.2 m across in a beam frame. |
| C07 | `Props/L3_SeedDrill.prefab` | BUILT | 2 × 2 × 1.2 | 2 × 2 × 1.2 | Obstacles | PolygonAdventure SM_Prop_Cart_01, crate, cart wheel | No seed drill in the packs. A small tipped cart with a hopper crate and a broken wheel. |
| C08 | `Props/L3_Well.prefab` | ADAPTED | 2 × 2 × 1.2 | 2 × 2 × 1.2 | Obstacles | PolygonKnights SM_Bld_Village_Well_01 | Pack well is 3.1 across and 0.9 high; scaled to 2 m across and raised to 1.2 m so it works as cover. Convex cylinder collider. |
| C09 | `Props/L3_Millstones.prefab` | BUILT | 2 × 2 × 1.2 | 2 × 2 × 1.2 | Obstacles | Generated faceted stone discs | No millstone in the packs. A stack of three faceted stone discs with a fourth leaning on it. |
| C10 | `Props/L3_GrainCart.prefab` | ADAPTED | 3 × 2 × 1.5 | 3 × 2 × 1.5 | Obstacles | PolygonAdventure SM_Prop_Cart_01, sacks | Open cart turned east-west at 0.95 scale, loaded with sacks up to 1.4 m. |
| C11 | `Props/L3_Trough.prefab` | BUILT | 2 × 2 × 1.2 | 2 × 2 × 1.2 | Obstacles | L2PolyKit worn stone blocks; PolygonAdventure barrels, sack, basket | No trough in the packs. A long basin of dressed stone blocks with water in it, and barrels and a sack behind it that bring the cover up to 1.2 m. (The well is C08, which is the same pack well L2 uses.) |
| C12 | `Props/L3_LimeMound.prefab` | ADAPTED | 4 × 4 × 1.5 | 4 × 4 × 1.5 | Obstacles | PolygonAdventure SM_Env_DirtMound_01 | Pack dirt mound (7.9 × 2.8 × 7.4) scaled to 4 × 1.5 × 4 and given a plain lime-white material. Convex cylinder collider. |
| C13 | `Props/L3_GraveCart.prefab` | ADAPTED | 2 × 3 × 1.5 | 2 × 3 × 1.5 | Obstacles | PolygonKnights SM_Prop_Cart_01; Adventure crates, sack, basket | Cart at 0.95 scale (1.45 × 1.1 × 3.0), long side north-south, loaded with household goods; a sack and basket beside it fill the 2 m width. |
| C14 | `Props/L3_RequisitionCart.prefab` | ADAPTED | 2 × 3 × 1.5 | 2 × 3 × 1.5 | Obstacles | PolygonAdventure SM_Prop_Cart_01, sacks, cart wheel | Open cart turned upside down at 0.95 scale, long side north-south, with spilled sacks and a loose wheel. |
| C15 | `Props/L3_Carcass.prefab` | BUILT | 2 × 1 × 0.6 | none | Default | Generated ox skeleton (L3Bones) | No animal models in the packs. A bare skeleton lying on its side, built the way L2 builds its human skeletons: bone only, no hide and nothing on the ground under it (owner's review). Used for the ox, the feeding carcass and the dead hound. No collider, as specified. |

### 14.4 Hazards

| ID | Prefab | Status | Spec | Collider read back | Layer | Source | Deviations and notes |
|---|---|---|---|---|---|---|---|
| H01 | `Hazards/L3_FireCell.prefab` | BUILT | 2 × 2 × 2 | 2 × 2 × 2 | Hazard | Kneel.Hazards.FireCell; wheat baked from PolygonAdventure SM_Env_Reeds_01-03 (L3Crops); flames from L2Fire | 2 × 2 × 2 trigger. Dry is standing wheat about 0.8 m tall on a tilled bed (the note asks for a 0.6 m tan block), swaying with the wind shader; burning chars it; ash is burnt stubble. Contact is polled, not sent by trigger messages. |
| H02 | `Hazards/L3_CropRow_12.prefab` | BUILT | 2 × 24 × 2 | 2 × 24 × 2 | Hazard | Kneel.Hazards.CropRow; twelve nested L3_FireCell | Pivot at the south end; cells cover z 0 to 24. NavMesh modifier volume (child NavArea, on Ground so the bake collects it) uses the CropRow area. |
| H03 | `Hazards/L3_BrandStake.prefab` | BUILT | 0.3 × 0.3 × 1.5 | 0.3 × 0.3 × 1.5 | Default | Kneel.Hazards.BrandStake; PolygonKnights SM_Prop_Beam_01 | Takes hits through the project's IDamageable; its 0.3 × 0.3 × 1.5 hit box is a trigger on the Enemy layer, which is where the sword looks. Inspector button: Knock. |
| H04 | `Hazards/L3_MudVolume.prefab` | BUILT | 4 × 4 × 2 | 4 × 4 × 2 | Hazard | Kneel.Hazards.SurfaceVolume | Resizable through SurfaceVolume.Configure (the inspector's Size). Damp bands are 2 m strips outside the mud, switchable per side; west and east are on by default. |
| H05 | (removed) | CUT | 18 × 4 × 2.5 | | | | The sail hazard was cut by the owner on 2026-10-06. The prefab, its components and its assets are deleted; the number is not reused. |
| H06 | `Hazards/L3_DitchKill.prefab` | BUILT | 6 × 6 × 2.5 | 6 × 6 × 2.5 | Hazard | Kneel.Hazards.DitchKill | Resizable trigger (default 6 × 6 × 2.5, pivot at the bottom centre). |
| H07 | `Hazards/L3_Gibbet_Inert.prefab` | BUILT | 0.4 × 0.4 × 3 | 0.4 × 0.4 × 3 | Obstacles | PolygonKnights SM_Prop_Beam_01; Adventure sacks | Post, crossbar and a sack figure. Collider on the post only. |
| H08 | `Hazards/L3_Gibbet_Crowed.prefab` | BUILT | 0.4 × 0.4 × 3 | 0.4 × 0.4 × 3 | Obstacles | Kneel.Hazards.GibbetAmbush; as H07 plus three crow placeholders | Adds one extra collider to H07: a trigger on the Enemy layer round the figure, so the sword can strike it early. Inspector buttons: Trigger, Strike early. |

### 14.5 Structures

| ID | Prefab | Status | Spec | Collider read back | Layer | Source | Deviations and notes |
|---|---|---|---|---|---|---|---|
| S01 | `Structures/L3_StoneBridge.prefab` | BUILT | 6 × 10 × 1 | 6 × 10 × 1 | Default | L2PolyKit worn stone blocks (deck); PolygonKnights SM_Bld_Rockwall_Straight_01 (parapets, piers) | Flat deck (not humpbacked) so it bakes and walks cleanly. Deck 6 × 10 with its top at ground level on Ground; parapets 0.5 × 1 × 10 on Obstacles, leaving 5 m clear. |
| S02 | `Structures/L3_SluiceFootway.prefab` | BUILT | 4 × 10 × 1 | 4 × 10 × 1 | Default | L2 plank meshes; nested L3_Rail | Plank deck 4 × 10 with its top at ground level on Ground; five L3_Rail per side. |
| S03 | `Structures/L3_Farmhouse.prefab` | ADAPTED | 11 × 12 × 4 | 11 × 12 × 3.2 | Obstacles | PolygonKnights SM_Bld_House_Room_01/03/05/06 (walls and roofs cut from them), chimney; Adventure table, stores; L2 peasant corpse; L2PolyKit planks | Cut from the pack houses at their own scale: their walls laid as inner and outer skins of 1 m walls (2.3 m to the eaves, colliders 3.2 m), four of their roofs as a double-pile roof pressed down to a 4 m ridge. Door gap 4 m in the west wall. Carries its own board floor (interior 9 × 10 plus the threshold) on Ground. Roof and south wall are separate children with FadeOnEnterMarker and the project OcclusionFade, so they fade when they hide the player. Dressed inside: hearth, table laid for five, the farmer, stores. The chimney stands 0.7 m above the 4 m ridge. |
| S04 | `Structures/L3_Windmill.prefab` | BUILT | 10 × 8 × 14 | 10 × 8 × 14 | Obstacles | PolygonKnights round castle tower pieces, house door; Adventure log pile, barrels, sacks, crates; L2PolyKit planks (sails) | No windmill in the packs. A tapering tower of round tower pieces under a low conical cap (the pack spire roof, flattened), stores stacked against both sides, a hub on the south face and four static sail frames of planks. Not burning (owner's review). One solid box collider. |
| S08 | `Structures/L3_MillStair.prefab` | BUILT | (owner's review) | 4.7 × 7.4 × 4 overall | Default; ramp and landing on Ground, cheeks on Obstacles | L2PolyKit worn stone blocks | Not in the note. Twelve worn stone steps (3.6 m wide, 0.25 m risers, 0.45 m treads) between cheek walls that step up in half-metre courses, and a 2 m landing. Pivot at the middle of the foot; climbs toward +Z. What is walked on is one slope through the middle of the treads, so feet sink or float by up to 12 cm on a step. |
| S05 | `Structures/L3_BoundaryOak.prefab` | ADAPTED | 2 × 2 × 12 | 2 × 2 × 12 | Obstacles | PolygonAdventure SM_Env_TreeDead_01, SM_Env_TreeLog_01, SM_Env_TreeStump_01 | The pack's dead tree branches from 1.7 m, so it stands on a log trunk: lowest branch at 6.5 m, top at 12 m. Capsule collider on the trunk only. |
| S06 | `Structures/L3_OrchardTree.prefab` | FOUND | 0.5 × 0.5 × 3.5 | 0.5 × 0.5 × 3.5 | Obstacles | PolygonKnights SM_Env_Tree_01 | Used at its own size (3.55 m tall). Capsule collider on the trunk only. |
| S07 | `Structures/L3_CellarFront.prefab` | ADAPTED | 4 × 1 × 2.5 | 4 × 1 × 2.5 | Obstacles | PolygonKnights SM_Bld_Rockwall_Archway_01 | Pack archway turned a quarter and scaled non-uniformly to 4 × 2.5 × 1, with a dark door set in it. |

### 14.6 Markers

| ID | Prefab | Status | Spec | Collider read back | Layer | Source | Deviations and notes |
|---|---|---|---|---|---|---|---|
| M01 | `Markers/L3_Shrine.prefab` | FOUND | 1 × 1 × 1.5 | not measured | Default | Kneel L1_CheckpointShrine (nested) | Wraps the existing L1 shrine unchanged and adds a ShrineMarker and a 12 m ember column. The L1 shrine is larger than a 1 × 1 pillar (see its bounds in the checks). |
| M02 | `Markers/L3_SluiceGate.prefab` | BUILT | 4 × 0.3 × 2 | 4 × 0.3 × 2 | Default | Kneel.Hazards.SluiceGate (pattern from L2 ShortcutGate and GateLever) | L2's gate opens by a lever and has no barred prompt or Opened event, and existing gameplay code is read-only, so this is a new component. Interact (E) from the +Z side opens it. Inspector buttons: Open from +Z, Try from -Z. |
| M03 | `Markers/L3_SecretTell.prefab` | BUILT | 0.3 × 0.05 × 0.3 | none | Default | Kneel.Markers.SecretTell; generated handprint texture | A 0.3 × 0.3 quad 1.2 m up, facing the prefab's +Z. Carries an optional marker post (child 'Post', off by default) for tells that stand alone. |
| M04 | `Markers/L3_Pickup_Consumable.prefab` | FOUND | 0.7 × 0.5 × 0.5 | none | Default | Kneel L2_Loot_Heal (nested) | Wraps the existing L2 heal pickup and adds a PickupMarker and a glow. |
| M05 | `Markers/L3_Pickup_Lore.prefab` | ADAPTED | 0.8 × 0.5 × 0.2 | none | Default | PolygonAdventure SM_Prop_Scroll_02 | A scroll with a PickupMarker and a glow. |
| M06 | `Markers/L3_Pickup_HealCapacity.prefab` | ADAPTED | 0.5 × 0.5 × 0.8 | none | Default | PolygonAdventure SM_Item_Potion_04, SM_Env_Rock_015 | A large flask on a stone with a PickupMarker and a visibly brighter glow. |
| M07 | `Markers/L3_Pickup_Tonal.prefab` | ADAPTED | 0.15 × 0.15 × 0.15 | none | Default | PolygonPrototype SM_Icon_Apple_01 | The unburnt apple: the prototype pack's apple at 0.22 scale, with a PickupMarker and a faint glow. |
| M08 | `Markers/L3_VistaTrigger.prefab` | BUILT | 8 × 6 × 4 | 8 × 6 × 4 | Ignore Raycast | Kneel.Markers.VistaMarker | Box trigger (default 8 × 4 × 6) on Ignore Raycast. Resize with L3Build-style helpers or the collider; the magenta box is hidden in play mode. |
| M09 | `Markers/L3_ArenaCamVolume.prefab` | BUILT | 8 × 8 × 6 | 8 × 8 × 6 | Ignore Raycast | Kneel.Markers.ArenaMarker | Box trigger (default 8 × 6 × 8, sized to its arena floor when placed) on Ignore Raycast. |
| M10a | `Markers/L3_PlayerSpawn.prefab` | BUILT | none | none | Default | Kneel.Markers.PlayerSpawnMarker | An empty with a forward-arrow gizmo and a magenta arrow that is hidden in play mode. |
| M10b | `Markers/L3_ExitTrigger.prefab` | BUILT | 4 × 4 × 3 | 4 × 4 × 3 | Ignore Raycast | Kneel.Markers.ExitMarker | Box trigger (default 4 × 3 × 4) on Ignore Raycast. |
| M11 | `Markers/L3_FadeOnEnter.prefab` | BUILT | none | none | Default | Kneel.Markers.FadeOnEnterMarker | A data-only marker component. The farmhouse's roof and south wall carry it directly; this prefab is the same component on an empty. The project's own Kneel.OcclusionFade could do the fading (see Hooks). |
| M12 | `Markers/L3_SpawnMarker.prefab` | BUILT | none | none | Default | Kneel.Markers.SpawnMarker | An empty with a gizmo, a SpawnMarker and a magenta disc that is hidden in play mode. |

### 14.7 Enemies

| ID | Prefab | Status | Spec | Collider read back | Layer | Source | Deviations and notes |
|---|---|---|---|---|---|---|---|
| E01 | `Assets/Enemies/AshenFootman/AshenFootman.prefab` | FOUND | 1 × 1 × 2 | not measured | Default | Assets/Enemies/AshenFootman/AshenFootman.prefab | The existing enemy, used as it is (no L3 copy, no stand-in). It has a NavMeshAgent (radius 0.25, height 1.5) and no collider of its own. |
| E02 | `Enemies/L3_StandIn_Hound.prefab` | BUILT | 0.8 × 1.4 × 0.8 | 0.8 × 1.4 × 0.8 | Enemy | Primitives | No hound in the project. Horizontal capsule 1.4 long, 0.8 tall, radius 0.4, on the Enemy layer, dark with a bone-white collar. Collider only. |
| E03 | `Enemies/L3_StandIn_Brute.prefab` | BUILT | 2 × 2 × 2.6 | 2 × 2 × 2.6 | Enemy | Primitives; generated ring mesh | No brute in the project. Capsule 2.6 tall, radius 1.0, on the Enemy layer, with a ground ring 4 m in radius as a child. Collider only. |

**Status counts.** FOUND 4 (S06, M01, M04, E01). ADAPTED 22. BUILT 28. Most kit and cover rows are `ADAPTED` rather than `FOUND` because the pack pieces are not on a 2 m module: the hedge is 5.4 m long and the rock wall 4 m, so they are scaled non-uniformly to fit. `BUILT` rows are assembled from pack pieces, from the L2 kit meshes (planks, worn stone blocks) or from generated low-poly meshes (skeletons, wheat, stone drums). Only the enemy stand-ins, the test dummy, the crow placeholders and the marker shapes are still Unity primitives.

**Generated meshes** are under `Assets/Kneel/Meshes/L3`: `Bones` (two ox skeletons), `Crops` (three wheat cells, three stubble cells), `Hedges` (four thickets), `Farmhouse` (walls, south wall, roof, floor), `Props` and `Structures`. They are rebuilt by `Kneel > L3 > Build > Materials, Meshes and Data`.

**Data assets** in `Assets/Kneel/Settings/L3`: `L3_Wind` (0, 0, 1) and `L3_FireTuning` (cell length 2, ember tell 1.5, cell delay 1.33, burn time 20, damage tick 1), as specified. Three more, so that every hazard's numbers are tunable without code: `L3_MudTuning` (roll 0.5, move 0.85), `L3_GibbetTuning` (radius 8, wake 1.5, player mask) and `L3_HazardTuning` (actor mask, and the damage used for actors that only have `IDamageable`).

**Review scene.** It carries the level's lighting pass and grade (`Kneel > L3 > Lighting > Apply To Open Scene` reapplies it). `L3_AssetReview` is 96 × 146 m, not 80 × 50: fifteen props at 6 m do not fit an 80 m row, and the crop row alone is 24 m long. It has a baked NavMesh (Humanoid agent) so the fire cells and the gate can be seen carving it. The footman is shown with its AI and agent switched off, so it does not hunt the player around the review floor.

## Hazard playtest

Scripted run in `L3_AssetReview`, timed in game time. Result of the last run (2026-10-06, after the sail was cut; 42 s of game time): **PASS, 0 failed checks, 0 console errors.**

One thing to know: of the three full runs made under the new lighting, one logged two Unity asserts once each ("Invalid worldAABB. Object is too large or too far away from the origin." and "Invalid localAABB. Object transform is corrupt."), with every hazard check still passing. Unity gives no object or stack for them. A minute of monitored play that relit both rows, cycled the sail, struck the gibbet, teleported the player and cycled the gate did not reproduce them, no renderer or transform had invalid bounds afterwards, and the final run was clean. They are unexplained, not fixed. Separately, taking Game-view screenshots through the MCP during play logs "PlayerLoop internal function has been called recursively"; that comes from the capture, not the scene.

| Check from 14.8 | Measured |
|---|---|
| 5. No missing scripts, no pink materials, no console errors on entering play mode | None. |
| 6. A knocked stake burns its row south to north in about 16 s | First cell smoulders 0.36 s after the knock (the stake's fall). The front takes 14.6 s to reach the last cell, which lights 16.5 s after the knock. |
| 6. Each cell lasts 20 s | Every cell smouldered 1.50 s and burned 20.00 s. First cell is ash at 21.9 s, the whole row at 36.5 s. |
| 6. The fire does not spread to the other row | The east row stayed dry for the whole west burn. A dummy standing in it and one in the 8 m lane were never touched. |
| 7. The sail's glow, bar and hit boxes stay in step over ten cycles | No longer applies: the sail was cut on 2026-10-06 and its part of the playtest with it. (It passed when built: ten cycles of 8.000 s, tell 1.500 s, pass 1.000 s.) |
| 8. The gate opens from one side only | Refused from −Z, opened from +Z, `Opened` raised once. Shut, the NavMesh route between its two sides is 34 m (round by the stone bridge); open, it is 3 m. |

Also checked: an actor in a cell gets `Smoulder` once, `FireEnter` once when it lights and `FireTick` each second (19 in 20 s). A burning cell carves the NavMesh and a dry one does not. A hit through `IDamageable` knocks a stake; environmental damage does not. `ResetToDry` and the stake's reset restore the row. Mud sets 0.5 / 0.85 on entry and clears on exit. The ditch reports once. A struck gibbet raises `StruckEarly` and wakes 1.5 s later; triggered by proximity it wakes without it; reset returns it to dormant. The baked NavMesh has Mud and CropRow under the two hazards.

The unmodified player has no `IHazardTarget`, so it was stood in a burning cell to test the `IDamageable` route: it lost 80 of 100 health in 3.3 s (one hit on entry and one per second, 20 each).

## Hooks not connected

Hazards were written against section 14.4's `IHazardTarget`. Where an actor has none, they fall back to the project's `IDamageable` with environmental damage, so fire and the ditch already hurt the existing player without any change to player code.

| # | Hook | State |
|---|---|---|
| 1 | Player implements `IHazardTarget` | Missing. Until it does, the player gets flat damage from `L3_HazardTuning` (fire 20 per hit, ditch lethal) instead of "hits", and **mud does nothing to the player**: `SetSurface` has nowhere to go. Move speed could use the existing `PlayerMovement.moveSpeedMultiplier`; the roll needs a multiplier on `dodgeDistance` in `PlayerCombat`. |
| 2 | Death and respawn | Missing in the project. `Health` regenerates, so the ditch cannot kill yet. `L3_PlayerSpawn` and `ShrineMarker` are data only. |
| 3 | Reset on death or rest | `ILevelResettable` is implemented by `CropRow`, `FireCell`, `BrandStake` and `GibbetAmbush`. Nothing calls it yet. |
| 4 | Enemies implement `IHazardTarget` | Missing. The footman also has no collider, so hazards (and the sword) cannot see it at all. |
| 5 | `L3_StandIn_Hound`, `L3_StandIn_Brute` | Collider only, no behaviour. Enemy behaviour is outside this handover. |
| 6 | `GibbetAmbush.Woken` and `StruckEarly` | Raised (C# events and UnityEvents), with a child `SpawnPoint`. Nothing spawns or staggers. |
| 7 | `SluiceGate.Opened` to the VG camera pan | Raised; not connected. |
| 8 | `SluiceGate` saved state | `IsOpen` and `SetOpen(bool)` exist; there is no save system to call them. |
| 9 | Vista and arena volumes to cameras | Not connected, as the note says. |
| 10 | `FadeOnEnterMarker` | The marker is data only, but the farmhouse roof and south wall also carry the project's `Kneel.OcclusionFade`, so they already fade when they hide the player. That needs `Kneel.OcclusionFader` on the gameplay camera, as on the L2 camera; the review scene's camera has it. |
| 11 | `PickupMarker`, `SpawnMarker`, `ExitMarker` | Data only. |
| 12 | Per-agent area costs (`NavMeshAgent.SetAreaCost`) | Belongs to the enemy AI. The areas and default costs exist. |
| 13 | `WindField` | Read by `CropRow` when sorting its cells. Smoke does not read it yet; its direction is authored. |
| 14 | Descent demonstration row | Connected in Phase 2: `L3_IgniteTrigger` (`RowIgniteTrigger`) lights it when the player enters x 60–68, y 44–46. It implements `ILevelResettable`, which nothing calls yet (hook 3). |
| 15 | Spawn markers to enemies | The 20 markers carry type, encounter, initial state, aggro radius and leash note, with the enemy or stand-in as a child for scale. Nothing spawns, wakes or leashes; the four footmen have their behaviours switched off. |
| 16 | The farmhouse door | Section 10 says the door opens only out of combat. There is no door object in section 14 and none was built: the 4 m gap is always open. |
| 17 | VG pan | The `L3_VistaTrigger` at the gate holds the pan target and duration. Nothing links it to `SluiceGate.Opened`. |

## Questions

1. **R. Answered: keep as is.** The roll in the project is 3 m (`dodgeDistance`), not the 2 m this plan is authored in. Phase 2 builds section 15 as written, without the 1.5 scale.
2. **The camera shows less than the plan assumes. Answered: keep as is.** About 20 × 13 m of ground, 8.8 m up-screen of the player, against the assumed 32 × 20 m and 13 m. A hound at 7 m/s crosses 8.8 m in about 1.3 s, short of the 2 s first-sight rule, unless the player pans with the pointer (up to 10 m more). Nothing was changed.
3. **Level scene name. Answered:** `L3_FallowFields`, in `Assets/Kneel/Scenes/Levels`, like L1 and L2.
4. **Project settings were changed:** layer 11 `Hazard` and NavMesh areas Mud and CropRow. Say if these should be named or numbered differently before the level depends on them.
5. **The shrine is bigger than the row assumes. Answered: use the existing shrine.** It measures 4.3 × 2.7 × 3.9 m, against a 1 × 1 × 1.5 pillar. Shrine B at (178, 298) is 2.5 m from the cellar front at (180.5, 298), so the two overlap as specified. Phase 2 will place both as written, turn the shrine to its narrow side if that clears the doorway, and report what is left under stage D.
6. **Fallback damage.** One hazard "hit" is 20 damage for actors without `IHazardTarget`. With the player at 100 health that is five hits. Tune in `L3_HazardTuning`.
7. **Footman size.** The existing footman's agent is radius 0.25, height 1.5; section 15.7 wants a Thrall agent of radius 0.5, height 2.0. Phase 2 will bake for the note's sizes unless told otherwise.
8. **Flat bridge.** Section 5 calls the stone bridge humpbacked; 14.5 asks for a deck with its top at ground level. Built flat, per 14.5.
9. **Gibbet strike box.** `L3_Gibbet_Crowed` has one collider more than "collider on the post only": a trigger round the figure, so the sword can strike it.
10. **Looks.** Cover uses pack art on the re-graded L3 palettes rather than the greybox tan, following 14.1. Mud and markers use the greybox palette, darkened to sit in the grade: lane floor `#ADA693` (ashen chalk, still the lightest ground), arena floor `#3A3027`. Phase 2 floors will use these.
13. **The sun's direction.** Section 7 puts the dawn sun low in the east with shadows falling screen-left. It is at 30° elevation and 22° south of east: from due east or north of it, every wall the camera sees was in its own shadow and read as black.
11. **Crop height.** 14.4 asks for a dry cell 0.6 m high. The wheat stands about 0.8 m, with ears to 0.95 m, so a bedded hound is hidden to its collar. The trigger is unchanged.
12. **Farmhouse height.** The roof ridge is at the row's 4 m; the chimney stands 0.7 m above it. The eaves are at 2.3 m (the pack houses' own wall height), so the visible walls are lower than their 3.2 m colliders.

14. **The Wallow's mud can be walked round (stage C check fails).** 15.4 puts the mud at x 148–168, y 240–254. The north verge F19 (y 238–240) runs between it and the ditch and stays dry, so a player can pass the whole Wallow on a 2 m strip beside the kerb stones without touching mud. Built as written. The smallest fix is to start the mud at y 238 (one number in `L3Plan`, or the volume's Size in the inspector).
15. **Shrine B stands in the cellar doorway.** As expected from question 5: the existing shrine is 4.3 × 2.7 m and its centre is 2.5 m from the cellar front. It reads as a shrine set at the cellar mouth and does not narrow the lane. Moving it 2 m west would show the doorway.
16. **E2's "12 R of clear floor".** No prop is within 12 m of the brute, but the north hedge is 11 m from him, because 15.5 puts him at y 131 in a field that ends at y 142.
17. **The descent's demonstration row is 3 m below the lane where it starts.** 15.4 gives it no height, so it is at Y = 0, at the foot of the bank. Section 5 says the player sees it "1 R away" over a low wall; from the crest it is seen from above instead. Raising its first six cells with the ramp would match the text.
18. **Diagonal lanes are 7.1 m wide at their mouths.** A lane 8 m wide meeting a square 8 m gate at 31° or 30° is cut by the gate's far post. The lanes themselves are 8.00 m.
19. **Arena volumes tint the Game view in edit mode.** The magenta marker boxes are hidden in play mode, as 14.1 asks, but in the editor the gameplay camera sits inside the 6 m arena volumes and looks through their tops. Hide the `Markers` group to see the level's colours without pressing Play.
20. **The level's south and north ends are walled** (K03 at y 0 and at y 400), so nothing can walk off the end of the scene. The exit trigger is inside the north wall.

21. **The player can jump onto 1 m walls.** The jump (0.9 m) plus the controller's step puts the player on top of K03, and from there off the level. Closed with unseen caps (see "Organic pass"). If jumping stays in the game, the note's 1.0 m and 1.2 m boundary heights do not contain the player on their own.
22. **Weight of the dressing.** 0.93 million triangles in eleven baked meshes; the editor ran the play tests at 2 to 3 ms a frame with it. If that is too heavy for the target machine, `L3Dressing` has the densities in one place.
23. **The mud's look lives in the level, not the prefab.** `L3_MudVolume` in the review scene still shows its plain rectangle. Say if the prefab itself should carry a baked default look.

24. **The design note is out of date.** It still has the sail hazard (sections 5, 12, 14.4 H05, 15.4, 15.8), a burning mill with no way up, walls on every lane and empty lanes. The level follows the owner's review instead. The note was not edited.
25. **The mill stair is walkable and leads to a shut door.** It gives the player a high place to stand in E5 and a dead end. If the mill should be entered, or should hold something, that is a design decision; if the stair should only be looked at, a gate at its foot is one prefab.
26. **Wheat on the verges looks like the wheat that burns.** The level teaches that standing wheat is a fire hazard. The plants that have crept through the fences are the same plant, do not burn and have no collider. They are kept sparse, low and off the road; say if they should be stubble only, or should not come past the fence at all.
27. **Points of interest narrow the lanes** to between 4.8 and 6 m where they stand, against the note's 8 m. Each is cover (`Obstacles`, 1.2 to 1.5 m high), so they also give a hound or the player something to break line of sight with in what were open lanes.
28. **E5's brute was moved** 6.5 m south, off the stair (see "Farmland pass" 3).
29. **Fog at a distance.** The lighting pass has pale linear fog from 24 to 120 m with exposure +1.7. Close up it is right; anything further than about 60 m washes out to near white, which will matter for the vistas (section 7 has the fog starting beyond 60 m). Not changed: say if the vistas should be tuned now.
30. **A white patch that was a shader fault.** `Kneel/Ground` builds its tangent frame from world +X and returns NaN on a face that points due east or west; bloom spread it into white blobs at some floor edges. The floors no longer draw those side faces. The shader itself is the project's and was not touched; any other mesh that uses it with such a face will show the same fault.

31. **Unsaved edits in the scene were replaced.** Before the sixth round was built the open scene had two unsaved changes of the owner's: one bush deleted from under the hedge stub in E1, and the ditch's north side wall moved 0.3 m. Both were read as pointers (to the hedge cores and to the ditch) and acted on in the generator; rebuilding the scene replaced them.
32. **Primitives left on show:** the open trench in the grave is still a dark block 0.3 m high (15.4 asks for exactly that), and the cellar front's door is a dark slab. Enemy stand-ins and marker shapes are primitives by design.
33. **The trough (C11)** still uses a small flat quad of the creek water. It was not touched in the sixth round, which read "the water hole" as the ditch; say if the trough was meant.

**Where sections disagreed** (built to 14 and 15, as instructed)

- Section 12 lists `IFlammable` and a sail driven by clip events. 14.4 defines `IHazardTarget` and one timeline. Built to 14.4: `IFlammable` does not exist. (The sail, which ran on one clock in code, has since been cut.)
- Section 12 has the bake collect only a `Walkable` layer; 15.7 has it collect Walkable, Boundary and Cover. The review scene follows 15.7.
- 14.8 asks for an 80 × 50 m floor with items 6 m apart; the rows do not fit. See Review scene above.
- 15.3's table gives the sunken lane no south boundary and E3 a K02 south edge, while section 4's inset draws E3's south side as a bank. Built to the table, with a K03 across the sunken lane's south end (see "Beyond the note" 6).
- 15.3 gives lanes K03 on every free edge, while section 5 has the causeway running "between kerb stones". F32 has K03 walls, per the table.

## Blocked

Nothing is blocked.

- ProBuilder is missing; the named fallback (scaled cubes) is used.
- Unity touched two pack materials on load (`PolyKnights_Character_Mat_Black`, `PolygonPrototype_Texture_01`: URP copies `_BaseMap` into `_MainTex`). Both were restored from git, so the packs are unchanged.
