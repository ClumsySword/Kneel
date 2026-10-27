# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Kneel is a Unity game project (Unity **6000.6.0f1**, URP 17, new Input System only: `activeInputHandler: 1`). There is no CLI build, lint or test pipeline. Building, running, and testing all happen in the Unity Editor. `com.unity.test-framework` is installed, but the project has no tests or `.asmdef` files yet, so all game code compiles into `Assembly-CSharp`.

Unity MCP (`com.coplaydev.unity-mcp`) is in `Packages/manifest.json`. When the Editor is open with it running, use it to inspect scenes, prefabs, and the console, and to trigger recompiles, rather than hand-editing Unity YAML. The package serves HTTP at `http://127.0.0.1:8080/mcp`. Register it with `claude mcp add --scope local --transport http unityMCP http://127.0.0.1:8080/mcp`. Only the `core` tool group is on by default. `manage_animation` needs `manage_tools(action="activate", group="animation")` each session, or enabling it in the Unity MCP window. Blend-tree child positions aren't exposed by `manage_animation`; set them with `execute_code` and `UnityEditor.Animations`.

Simulated input through `InputSystem.QueueStateEvent` is unreliable while the Editor is unfocused: events arrive late and get routed to the Editor. To script Play-mode tests:
- Set `Application.runInBackground = true`.
- Disable each component's `controls`.
- From an `EditorApplication.update` callback, write the cached input fields by reflection (`moveInput`, `sprintHeld`, `aimInput`, `blockHeld`), or invoke the handlers (`PlayerCombat.OnDrawPressed/OnStrikePressed/OnBlockPressed`).
- Time the test with `Time.unscaledTime`, because hit-stop changes `Time.timeScale`.

Other MCP gotchas:
- After adding a new `.cs` file, refresh with `mode: force` and check `read_console` for errors. A non-forced refresh can skip the import, and the next Play mode silently fails to start.
- `execute_code` blocks `AssetDatabase.DeleteAsset`.
- URP's material validation copies `_BaseMap` into the legacy `_MainTex` slot when a material loads. If a third-party material suddenly shows up as modified with only a `_MainTex` change, that's this, and it's safe to commit (as was done for `PolyKnights_Mat_01` and `PolygonPrototype_Texture_01`).
- About 18 Synty materials (PolygonPrototype `*_Global_Grid_*`, `*_Glass_*`, FX, and PolygonStarter/City sky and steam) still use Synty's custom or Built-in legacy shaders and the pre-2018 file format. They aren't URP-ready and will render magenta if used; convert them before relying on them.
- Movement bugs can depend on frame rate: builds run uncapped (vSync is off), and hit-stop shrinks `deltaTime` 20×. Reproduce high-fps behaviour in Play mode with `Time.captureFramerate = N` (reset to 0 after). The player's `CharacterController.minMoveDistance` must stay 0: at 0.001, the tiny steps of walking from rest at ≥300 fps were dropped, and the wall clamp then pinned the player at 0.1 m/s until they jumped.
- If Play-mode timings look stretched, check `Time.unscaledDeltaTime`. The Editor sometimes drops to about 1 fps, and gameplay `deltaTime` then caps at 0.33, which makes the results meaningless.

## Design docs

The team's Obsidian vault is a sibling folder, `../Kneel-Obsidian`. The source of truth for design:
- `10-TenPager/*.tex`: the LaTeX ten-pager. `03Character.tex` has the **controls table**. Bind new verbs to the key it specifies.
- `02-Design/Mechanics/*.md`: per-mechanic feel targets (Combat Roll, Parry, Poise & Stagger).
- `04-Tech/Combat Feel Spec.md`: impact stack, and the rule that committed actions can't be cancelled but inputs are buffered.

The code diverges from the ten-pager in a few places. **These are intentional: keep the code's behaviour and don't "fix" them to match the PDF.**
- Sprint is on Shift, which the ten-pager reserves for the Ash ability, and it specifies "no walk/run toggle".
- Jump (Space) and Draw (R) aren't in its table.
- Parry triggers on the block *press*, while its prose says "time the release" (its table says "tap").
- Block and roll cost no stamina (poise replaced stamina). The camera has no mouse look-ahead.
- Damage numbers and the "PARRY" popups stay, although the ten-pager calls for no interface.

`PLAYER_TODO.md` (repo root) tracks the player features from the ten-pager that are still missing, ranked by ease.

## Layout

- **All team-made content lives under `Assets/Kneel/`, organized by feature.** Don't add new top-level `Assets/` folders.
  - `Kneel/Player/`: the player, including `Models/EmberWarden/` (the current body), `Models/Player.fbx` (the old Synty Knight, kept as the scabbard mesh's source and for comparisons), `Animations/` (Mixamo clips) and `Settings/`.
  - `Kneel/Combat/`: shared combat code.
  - `Kneel/Enemies/`: `AI/` (enemy base and state machine), `AshenFootman/` (with its own `Animations/`), and `TargetDummy/`.
  - `Kneel/Levels/L1/`: Patrick's level (`Scenes/L1_BrokenLine.unity` plus its lighting and NavMesh folder, and `Materials`, `Meshes`, `Prefabs`, `Lighting`, `Animations`, `Settings`).
  - `Kneel/Scenes/`: team and test scenes that aren't a level.
  - `Kneel/Core/`: game flow (`LevelRestartOnDeath`).
  - `Kneel/Gameplay/`: level interactables and hazards (`FireHazard`, `GateLever` (uses Interact, E), `ShortcutGate`, `OcclusionFade`, `Hazards/`).
  - `Kneel/Editor/`: level-building tools: `L1/` (the `Kneel/L1` menu), plus the `L2*` and `L3*` scripts. The L1 tools hardcode `Assets/Kneel/Levels/L1/...` paths, so update those if L1 content moves.
  - `Kneel/Shaders/`: shared shaders.
- Everything else under `Assets/` is imported and shouldn't be modified: the asset packs (`SyntyStudios/`, `Thirdparty/` including `Toon_RTS_demo`, `URPDefaultResources/`) and the imported TMP Essentials (`TextMesh Pro/`).
- Move assets inside Unity (or with `AssetDatabase.MoveAsset`), never in the file browser, so the `.meta` GUIDs travel with them. When scripting moves, don't wrap `AssetDatabase.CreateFolder` in `StartAssetEditing`: folders created inside the batch aren't registered, and retries produce numbered duplicates (`Levels 1`, `Levels 2`, …).
- Physics layers: `Enemy` (8) is enemy hurtboxes, and it is what the sword's `WeaponHitbox` hits. `Player` (9) is the Player root, which is what enemy attacks overlap.
- **Scenes are owned by one person**, named with their initial (`_L` is Lucas); see the vault's `04-Tech/Scene Ownership.md`. Don't edit someone else's scene: copy it.
- `Assets/Kneel/Scenes/L1_BrokenLine_Playable_L.unity` (Build Settings index 0) is the playable level. It's a copy of Patrick's `Levels/L1/Scenes/L1_BrokenLine.unity`, with five Ashen Footmen under `_Gameplay/Enemies` (one per `Encounter_E1`–`E5`, at its first `EnemySpawn`, patrolling a ring inside the arena), the F1 `Debug` overlay, and `LevelFlow`. It shares Patrick's baked NavMesh and lighting data, so re-bakes of his scene carry over.
- `Assets/Kneel/Scenes/Sandbox_S.unity`: small movement test scene. `Assets/Kneel/Scenes/Arena_S.unity`: a 30×30 walled arena (pillars, low walls, crates, a ramped platform) using `PlayerCamera`. Both follow the same layer convention: walkable surfaces on `Ground` (layer 6) and props on `Obstacles` (layer 7). The Player's `aimLayerMask` covers both, so new geometry must be on one of those layers for mouse aim to hit it.
- `Assets/Settings/`: URP pipeline assets, with separate `PC_*` and `Mobile_*` renderer/RP assets.

## Player architecture

The player is `Assets/Kneel/Player/Player.prefab`: a `CharacterController`, `PlayerMovement`, and a child Animator driven by `AC_Player.controller`.

- **Body**: the `EmberWarden` child is `Models/EmberWarden/Player_EmberWarden.fbx`, a Meshy mesh rigged in Blender with the Knight's bone names (`Hips`, `Spine_01..03`, `Hand_R`, …, right fingers with the `_1` suffix). It's Humanoid with its own avatar.
  - The mesh is in an A-pose, so the importer's `humanDescription.skeleton` was set to a T-pose. Re-importing with "Create From This Model" defaults loses that, and arms then retarget at an angle.
  - The instance is scaled ×1.037 so its `Animator.humanScale` matches the Knight's. The clip speeds and timings below were measured on the Knight, so they stay valid.
  - The body and the `Scabbard` (the Knight's sheath mesh, on `Hips`) render on rendering layers Default + Characters, like the level overrides expect.
  - The cape is skinned to 5 chains of 5 bones, `Cape_<column>_<segment>` under `Spine_03`. The top of the cape blends into the spine, so it stays attached at the shoulders. These bones aren't humanoid, so clips never move them.
  - After re-exporting the FBX, rebuild `humanDescription.skeleton` from every transform in the model, T-posing the arms. New bones missing from it break the avatar.

- **Input**: `PlayerControls.inputactions` is the source of truth. `PlayerControls.cs` is **auto-generated** from it by the Input System ("Generate C# Class"). Never edit it by hand. Change the `.inputactions` asset and regenerate instead.
  - The `Character` map defines Movement, Aim (mouse screen position), Sprint, Jump, Zoom (scroll), Strike (LMB), Draw (R), Block (RMB), and Dodge (Ctrl).
  - The `Debug` map holds ToggleOverlay (F1).
  - Each script creates its own `new PlayerControls()`.
- **Tuning lives in ScriptableObjects** in `Assets/Kneel/Player/Settings/`: `PlayerMovementSettings.asset`, `PlayerCameraSettings.asset`, `PlayerCombatSettings.asset`, `PlayerLanternSettings.asset` and `PlayerCapeSettings.asset`. Scripts read them every frame, so edits apply live. `DebugTuningOverlay` (F1, on the `Debug` object in `Arena_S`) has sliders that write into those assets, so values tuned in Play mode persist. Add a slider there when adding a tuning field.
- **`PlayerMovement.cs`** owns all player logic in `Update()`. Input callbacks only cache values. Each frame it:
  1. Aims: raycasts the mouse against `aimLayerMask` and moves the `aim` dot to the hit point.
  2. Resolves sprint (no stamina; sprint is unlimited).
  3. Rotates at a capped turn rate: toward the aim point when walking, toward the move direction when sprinting.
  4. Moves planar velocity toward the target with accel/decel, and applies gravity and jump (`sqrt(2gh)`).
  5. Moves via `CharacterController.Move` (no Rigidbody, no root motion).
  6. Feeds the animator.

  Walk speed depends on direction relative to facing, interpolated by angle across 5 speeds. The speed fields hold the clips' measured speeds, and `walkSpeedMultiplier` / `sprintSpeedMultiplier` scale them. The sprint steers speed and direction separately: speed builds with the accel rates and drops when facing and input disagree (turning bleeds speed), while the travel direction swings to the facing at `sprintGrip` °/s, so the run follows the body instead of sliding through turns.
- **Animator contract**: the code sets `xVelocity` and `zVelocity` to the **actual local planar velocity in m/s divided by `LocomotionSpeed`**, plus `LocomotionSpeed` (the `Locomotion` state's speed multiplier: 1 at rest, ramping to the walk multiplier at full walk speed in the current direction, then to the sprint multiplier at full sprint), `IsGrounded` (bool) and `Jump` (trigger). So the tree always sees the clips' own speeds, and the clips play faster to match the faster movement.
  - The `Locomotion` state is a Freeform Directional 2D tree whose child positions are each clip's real travel speed on the Knight. That is what keeps the feet from sliding.
  - If you add a clip or change a movement speed default, place the clip at its measured speed and keep the `PlayerMovementSettings` defaults in sync.
  - Renaming parameters means updating the string literals in `PlayerMovement.cs`.
- **`PlayerCamera.cs`** (on Main Camera) is an angled top-down follow camera in `LateUpdate`. It reads only `Zoom` (edge panning was removed). It finds the player through `PlayerMovement` if `target` is unset. `AddShake()` is used for impacts.
- **`PlayerCombat.cs`** is a code-driven state machine: `Sheathed → Drawing → Armed ⇄ Attacking / HitStun / Dodging → Sheathing`, with blocking as a flag while Armed. It runs before `PlayerMovement` (`[DefaultExecutionOrder(-10)]`).
  - The combo is two slashes (`Slash1` = `slash-1`, `Slash2` = `slash-3`). The pommel strike `attack-4` was dropped.
  - **Dodge roll** (base-layer `Dodge` state, trimmed `Stand To Roll` clip, frames 33–117, sped up to `dodgeDuration`):
    - Moves by code: `PlayerMovement.forcedVelocity` follows `dodgeTravelCurve`, which was measured from the clip's root motion so the body stays in sync.
    - Only starts from Sheathed/Armed; a roll pressed during another action is buffered (`dodgeBufferTime`).
    - Invulnerable between `dodgeInvulnerableStart/End`.
    - Stops short on a head-on wall hit (less than half the intended step got through).
    - Optionally rolls through enemies, via `Physics.IgnoreLayerCollision(Player, Enemy)`.
  - It doesn't use animator transitions or animation events. It `CrossFadeInFixedTime`s states on two layers and sets their weights itself:
    - `UpperBody`, masked by `AM_UpperBody.mask` (arms and head): ArmedIdle, Draw1/2, Sheath1/2, Block, BlockHit, Parry.
    - `FullBody`: Slash1, Slash2, HitReact, GuardBreak, Death.
  - All timings are clip-seconds in `PlayerCombatSettings`: each attack's `hitStart/hitEnd/comboWindow/recoveryEnd`, plus draw grab and sheathe release. They were measured from blade-tip speed and hand-to-hilt distance on the Knight. Re-measure them if you swap a clip.
  - Each frame it restricts movement through `PlayerMovement`'s public `moveSpeedMultiplier / turnRateMultiplier / rotationLocked / sprintBlocked / jumpBlocked`, which reset every frame.
  - Incoming hits resolve in this order: parry (first `parryWindow` of a fresh RMB press, from the front), then block (from the front: reduced damage, `blockPoiseMultiplier` of the poise damage; guard break when poise breaks), then full damage plus full poise damage. A broken poise staggers (`GuardBreak`, `staggerDuration`). An intact poise means a short `HitReact`, unless mid-action (attack/draw/sheathe/roll), which carries on.
- **`PlayerEquipment.cs`** moves the sword and shield between sockets: `Socket_Sheath` on the scabbard, `Socket_ShieldBack` on the upper spine, `Socket_SwordHand` / `Socket_ShieldHand` on the hands.
  - Sockets define the item pose exactly, so items sit at identity. Their scale cancels the body's ×1.037, keeping items at world scale 1.
  - `Socket_SwordHand` puts the grip centre (9 cm below the guard) just behind the middle knuckle, on the palm side. The blade points out of the index/thumb side and the edge faces the punch direction. It was built from the `Hand_R` / `Finger_01_1` / `IndexFinger_01_1` positions, not copied from the Knight, whose hand bone sits differently.
  - The sword has a `WeaponHitbox`: a capsule from `BladeBase` to `BladeTip`, swept between frames, hitting each target once per swing.
- **`PlayerLantern.cs`** (on `Hips/Socket_Lantern/Lantern`, on the outside of the right hip, just ahead of the cape's edge) hangs the lantern from the belt.
  - The `Lantern` transform is the hook, on the belt surface; a short `Strap` cube runs from it to the lantern's handle. In `LateUpdate` it simulates the lantern as a damped pendulum (1/120 s sub-steps, so it's frame-rate independent). It only damps motion relative to the hook, so walking doesn't make it trail; starts, stops and turns do.
  - Body collision is code-only, with no physics colliders (extra colliders on the `Player` layer would interfere with enemy hit overlaps). `bodyCapsules` are bone-to-bone capsules (waist, right thigh, right shin, right forearm), sized from the Warden's skin (median vertex distance from each bone). `lanternSpheres` approximate the lantern along its hanging line. Each step pushes the spheres out of the capsules and removes velocity into the body. Select the `Lantern` object to see both as gizmos.
  - It also flickers a point light and the glass's emission (via a `MaterialPropertyBlock` on material slot 1). Tuning is in `PlayerLanternSettings.asset`, with a "Lantern" section in the F1 overlay.
  - The light doesn't ride the swinging lantern directly. It follows the flame's position smoothed over `lightSmoothing` (1.5 s, in the body's space, so turns carry it at once), and is held at least `lightClearance` (0.5 m) out from the body's centre line.
    - Riding the pendulum right against the hip blew out the thigh and swept hard self-shadows across the body with every swing.
    - Excluding the player from the lantern's shadow casters via URP custom shadow layers isn't possible: the player must stay on the Default rendering layer, because most level lights only light Default.
  - The mesh `Lantern/SM_PlayerLantern.asset` is Synty's `SM_Item_Lantern_01` split into two submeshes: the frame (Synty atlas) and the glass (`M_PlayerLanternGlass`, emissive). The model doesn't cast shadows, because the light sits inside it.
- **`PlayerCape.cs`** (on the `EmberWarden` body) simulates the cape chains in `LateUpdate` (1/120 s sub-steps).
  - Each chain is a string of particles. The top one rides the upper back. The rest have gravity, a spring toward the modelled drape (`shoulderStiffness` → `hemStiffness`, in 1/s²), fixed segment lengths, and spacing limits to the neighbouring chains, so the sheet neither splits nor bunches.
  - The drape follows the upper back at the shoulders, blending to the body's facing at the hem, so torso leans don't swing the whole cape. Swing is damped relative to the body's travel (the `PlayerMovement` root), not the cape's anchor on the back, which whips around during rolls. `airDrag` makes it stream back when running; lower values read as heavier cloth.
  - Body turns faster than `SnapTurnRate` (1500 °/s) carry the cloth rigidly with them. `PlayerCombat.StartDodge` snaps the player to face the roll direction in one frame, and without this the cape was flung up to 60 cm and flailed through the roll.
  - Body collision is code-only, with its own `bodyCapsules` (back, hips, thighs, shins, upper arms, forearms). They were sized to the skin, but kept clear of the cape's rest drape so it doesn't start out pressed into the body.
  - Tuning is in `PlayerCapeSettings.asset`, with a "Cape" section in the F1 overlay. In idle the hem sways about ±0.15 m: it follows the idle clip's chest twist at the cape's top. That's attachment, not instability; damping doesn't change it.
  - Known gap: when the cape lifts (sprinting), it passes over the lower edge of the shield on the back.
- **Shared combat** (`Assets/Kneel/Combat/`):
  - `DamageInfo`, plus the interfaces `IDamageable` / `IParryable` / `IStaggerable`.
  - `Health`: optional regenerate-to-full (only the dummy uses it). The player and enemies can die. Player death (`PlayerCombat` `Dead` state, `OnDied`) reloads the scene through `LevelRestartOnDeath`, which only works for scenes in Build Settings.
  - `Poise` (plain serializable class, owned by the player, `Enemy` and `TargetDummy`): depletes from `DamageInfo.poiseDamage`, regenerates after a delay, refills when it breaks. Hits during an enemy wind-up deal `windupPoiseMultiplier`× poise damage.
  - `HitStop`: global `Time.timeScale` freeze.
  - `DamageNumber`: runtime-built world-space TMP text.
  - `HitSpark`: runtime-built particle burst.
- **`TargetDummy`** is a Synty `Character_Dummy_Male_01` humanoid playing the Mixamo impact/kick clips via `AC_TargetDummy`.
  - Its "attacks" toggle (in the F1 overlay) makes it telegraph (red tint), turn and kick, with an overlap sphere against the `Player` layer.
  - Being parried staggers it, and hits on a staggered dummy are critical.
- **Ashen Footman** (Olivia's, `Kneel/Enemies/AshenFootman`, base `Kneel/Enemies/AI/Enemy.cs`):
  - It's a plain-C# state machine: one `EnemyState` subclass per behaviour, and each state sets an animator **bool** of the same name.
  - The controller follows one pattern: Entry → state when its bool is true, state → Exit when it's false. Add new states the same way.
  - States: Idle → Move (patrol) → Recovery (the pointing clip, which is also the post-attack punish window) → Chase → Attack, plus Hit and Dead.
  - **Animation events** drive it: `AnimationTrigger` ends the Recovery and Attack clips, and `AttackHit` (on `Attack_B.anim` at 0.30 s) deals damage with an overlap sphere on the `Player` layer.
  - Attack plays at 0.6× so the wind-up is at least 300 ms, with a red tint telegraph.
  - The Hit and Dead states use `stateTimer` instead of events, because their clips (Mixamo `impact-1`, `death-1`) are shared with the player, whose animator has no receiver for them.
  - It implements `IDamageable` / `IParryable` / `IStaggerable`:
    - Hits that break its poise (30) stagger it (`StaggerState`, `Stagger` animator state on `impact-2` at 0.7×); hits that don't make it flinch (`Hit`), except during an attack, which carries on.
    - A parry staggers it, and hits during the stagger are crits.
    - It has 40 HP, which is 3 hits of the 12→20 combo.
    - When it dies, its agent and collider switch off and the body stays.
- **Clips**: Mixamo FBX files in `Assets/Kneel/Player/Animations/`.
  - The Humanoid ones copy their avatar from `Mixamo_POLYGON_Guy_Naked.fbx`. The combat clips in use have been converted and renamed (`draw-sword-1`, `attack-4`, `slash-1`, `block-idle`, `impact-1`, `kick`, …). The other `sword and shield *` clips are still Generic and must be switched to Humanoid (same avatar source) before use.
  - `draw sword 1`/`2` and `sheath sword 1`/`2` are two halves of one motion (reach to the hilt, then pull out; and the reverse).
  - Mixamo FBX files downloaded "with skin" can contain an extra `Take 001` bind-pose take. Pick the `mixamo.com` take when building `clipAnimations` (see `Stand To Roll.fbx`).
  - File names can mislead: `run-forwards-right` / `run-backwards-left` are right/left strafe runs, and `sword and shield run` is the forward run (clip `sprint-forwards`, 3.97 m/s).
  - Measure a clip's speed from its `RootT.x/z` curve delta over its length. Don't use `AnimationClip.averageSpeed`, which is skewed by the run's forward lean.

## Repo conventions

- Every asset needs its `.meta` file committed alongside it. Move or rename assets in Unity (or move the `.meta` along with them) so GUID references don't break.
- `.gitattributes` routes binary assets (`*.fbx`, `*.png`, etc.) through Git LFS and marks Unity YAML for `unityyamlmerge`. `git-lfs` must be installed locally for those rules to apply.
- Work happens on `task/<Name>` branches and merges into `main` through PRs. `CODEOWNERS` assigns everything to `@ClumsySword/Core`.
