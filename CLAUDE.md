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
- If Play-mode timings look stretched, check `Time.unscaledDeltaTime`. The Editor sometimes drops to about 1 fps, and gameplay `deltaTime` then caps at 0.33, which makes the results meaningless.

## Design docs

The team's Obsidian vault is a sibling folder, `../Kneel-Obsidian`. The source of truth for design:
- `10-TenPager/*.tex`: the LaTeX ten-pager. `03Character.tex` has the **controls table**. Bind new verbs to the key it specifies.
- `02-Design/Mechanics/*.md`: per-mechanic feel targets (Combat Roll, Parry, Poise & Stagger).
- `04-Tech/Combat Feel Spec.md`: impact stack, and the rule that committed actions can't be cancelled but inputs are buffered.

The code currently diverges from the ten-pager in a few places:
- Sprint is on Shift, which the ten-pager reserves for the Ash ability, and it specifies "no walk/run toggle".
- Jump (Space) and Draw (R) aren't in its table.
- Parry triggers on the block *press*, while its prose says "time the release" (its table says "tap").

## Layout

- The project's own code and assets:
  - `Assets/Player/`: the player.
  - `Assets/Combat/`: shared combat code.
  - `Assets/Enemies/`: `TargetDummy`.
- Everything else under `Assets/` is imported and shouldn't be modified: the asset packs (`SyntyStudios/`, `Thirdparty/`, `URPDefaultResources/`) and the imported TMP Essentials (`TextMesh Pro/`).
- Physics layers: `Enemy` (8) is enemy hurtboxes, and it is what the sword's `WeaponHitbox` hits. `Player` (9) is the Player root, which is what enemy attacks overlap.
- `Assets/Scenes/Sandbox_S.unity`: small movement test scene. `Assets/Scenes/Arena_S.unity`: a 30×30 walled arena (pillars, low walls, crates, a ramped platform) using `PlayerCamera`. Both follow the same layer convention: walkable surfaces on `Ground` (layer 6) and props on `Obstacles` (layer 7). The Player's `aimLayerMask` covers both, so new geometry must be on one of those layers for mouse aim to hit it.
- `Assets/Settings/`: URP pipeline assets, with separate `PC_*` and `Mobile_*` renderer/RP assets.

## Player architecture

The player is `Assets/Player/Player.prefab`: a `CharacterController`, `PlayerMovement`, and a child Animator driven by `AC_Player.controller`.

- **Input**: `PlayerControls.inputactions` is the source of truth. `PlayerControls.cs` is **auto-generated** from it by the Input System ("Generate C# Class"). Never edit it by hand. Change the `.inputactions` asset and regenerate instead.
  - The `Character` map defines Movement, Aim (mouse screen position), Sprint, Jump, Zoom (scroll), Strike (LMB), Draw (R), Block (RMB), and Dodge (Ctrl).
  - The `Debug` map holds ToggleOverlay (F1).
  - Each script creates its own `new PlayerControls()`.
- **Tuning lives in ScriptableObjects** in `Assets/Player/Settings/`: `PlayerMovementSettings.asset`, `PlayerCameraSettings.asset`, and `PlayerCombatSettings.asset`. Scripts read them every frame, so edits apply live. `DebugTuningOverlay` (F1, on the `Debug` object in `Arena_S`) has sliders that write into those assets, so values tuned in Play mode persist. Add a slider there when adding a tuning field.
- **`PlayerMovement.cs`** owns all player logic in `Update()`. Input callbacks only cache values. Each frame it:
  1. Aims: raycasts the mouse against `aimLayerMask` and moves the `aim` dot to the hit point.
  2. Resolves sprint and stamina.
  3. Rotates at a capped turn rate: toward the aim point when walking, toward the move direction when sprinting.
  4. Moves planar velocity toward the target with accel/decel, and applies gravity and jump (`sqrt(2gh)`).
  5. Moves via `CharacterController.Move` (no Rigidbody, no root motion).
  6. Feeds the animator.

  Walk speed depends on direction relative to facing, interpolated by angle across 5 speeds. Sprint velocity follows facing, scaled by how aligned facing is with the input, so turning bleeds speed.
- **Animator contract**: the code sets `xVelocity` and `zVelocity` to the **actual local planar velocity in m/s**, plus `IsGrounded` (bool) and `Jump` (trigger).
  - The `Locomotion` state is a Freeform Directional 2D tree whose child positions are each clip's real travel speed on the Knight. That is what keeps the feet from sliding.
  - If you add a clip or change a movement speed default, place the clip at its measured speed and keep the `PlayerMovementSettings` defaults in sync.
  - Renaming parameters means updating the string literals in `PlayerMovement.cs`.
- **`PlayerCamera.cs`** (on Main Camera) is an angled top-down follow camera in `LateUpdate`. It reads `Zoom` and `Aim` (pointer position, for edge panning). It finds the player through `PlayerMovement` if `target` is unset. `AddShake()` is used for impacts.
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
    - `FullBody`: Slash1, Slash2, HitReact, GuardBreak.
  - All timings are clip-seconds in `PlayerCombatSettings`: each attack's `hitStart/hitEnd/comboWindow/recoveryEnd`, plus draw grab and sheathe release. They were measured from blade-tip speed and hand-to-hilt distance on the Knight. Re-measure them if you swap a clip.
  - Each frame it restricts movement through `PlayerMovement`'s public `moveSpeedMultiplier / turnRateMultiplier / rotationLocked / sprintBlocked / jumpBlocked`, which reset every frame. Stamina goes through `TrySpendStamina` / `DrainStamina`.
  - Incoming hits resolve in this order: parry (first `parryWindow` of a fresh RMB press, from the front), then block (from the front: reduced damage, drains stamina; guard break at 0), then full damage plus hit-stun.
- **`PlayerEquipment.cs`** moves the sword and shield between sockets: `Socket_Sheath` on the scabbard, `Socket_ShieldBack` on the upper spine, `Socket_SwordHand` / `Socket_ShieldHand` on the hands.
  - Sockets carry a ×100 scale because the Knight's bones are in centimetres. They define the item pose exactly, so items sit at identity.
  - The sword has a `WeaponHitbox`: a capsule from `BladeBase` to `BladeTip`, swept between frames, hitting each target once per swing.
- **Shared combat** (`Assets/Combat/`):
  - `DamageInfo`, plus the interfaces `IDamageable` / `IParryable` / `IStaggerable`.
  - `Health`: optional regenerate-to-full, so nobody dies yet.
  - `HitStop`: global `Time.timeScale` freeze.
  - `DamageNumber`: runtime-built world-space TMP text.
  - `HitSpark`: runtime-built particle burst.
- **`TargetDummy`** is a Synty `Character_Dummy_Male_01` humanoid playing the Mixamo impact/kick clips via `AC_TargetDummy`.
  - Its "attacks" toggle (in the F1 overlay) makes it telegraph (red tint), turn and kick, with an overlap sphere against the `Player` layer.
  - Being parried staggers it, and hits on a staggered dummy are critical.
- **Clips**: Mixamo FBX files in `Assets/Player/SnS-Anims/`.
  - The Humanoid ones copy their avatar from `Mixamo_POLYGON_Guy_Naked.fbx`. The combat clips in use have been converted and renamed (`draw-sword-1`, `attack-4`, `slash-1`, `block-idle`, `impact-1`, `kick`, …). The other `sword and shield *` clips are still Generic and must be switched to Humanoid (same avatar source) before use.
  - `draw sword 1`/`2` and `sheath sword 1`/`2` are two halves of one motion (reach to the hilt, then pull out; and the reverse).
  - Mixamo FBX files downloaded "with skin" can contain an extra `Take 001` bind-pose take. Pick the `mixamo.com` take when building `clipAnimations` (see `Stand To Roll.fbx`).
  - File names can mislead: `run-forwards-right` / `run-backwards-left` are right/left strafe runs, and `sword and shield run` is the forward run (clip `sprint-forwards`, 3.97 m/s).
  - Measure a clip's speed from its `RootT.x/z` curve delta over its length. Don't use `AnimationClip.averageSpeed`, which is skewed by the run's forward lean.

## Repo conventions

- Every asset needs its `.meta` file committed alongside it. Move or rename assets in Unity (or move the `.meta` along with them) so GUID references don't break.
- `.gitattributes` routes binary assets (`*.fbx`, `*.png`, etc.) through Git LFS and marks Unity YAML for `unityyamlmerge`. `git-lfs` must be installed locally for those rules to apply.
- Work happens on `task/<Name>` branches and merges into `main` through PRs. `CODEOWNERS` assigns everything to `@ClumsySword/Core`.
