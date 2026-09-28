# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Kneel is a Unity game project (Unity **6000.6.0f1**, URP 17, new Input System only: `activeInputHandler: 1`). There is no CLI build, lint or test pipeline. Building, running, and testing all happen in the Unity Editor. `com.unity.test-framework` is installed, but the project has no tests or `.asmdef` files yet, so all game code compiles into `Assembly-CSharp`.

Unity MCP (`com.coplaydev.unity-mcp`) is in `Packages/manifest.json`. When the Editor is open with it running, use it to inspect scenes, prefabs, and the console, and to trigger recompiles, rather than hand-editing Unity YAML. The package serves HTTP at `http://127.0.0.1:8080/mcp`. Register it with `claude mcp add --scope local --transport http unityMCP http://127.0.0.1:8080/mcp`.

## Layout

- `Assets/Player/`: the project's own code and assets. Everything else under `Assets/` (`SyntyStudios/`, `Thirdparty/`, `URPDefaultResources/`) is imported asset packs. Don't modify those.
- `Assets/Scenes/Sandbox_S.unity`: small movement test scene. `Assets/Scenes/Arena_S.unity`: a 30×30 walled arena (pillars, low walls, crates, a ramped platform) using `PlayerCamera`. Both follow the same layer convention: walkable surfaces on `Ground` (layer 6) and props on `Obstacles` (layer 7). The Player's `aimLayerMask` covers both, so new geometry must be on one of those layers for mouse aim to hit it.
- `Assets/Settings/`: URP pipeline assets, with separate `PC_*` and `Mobile_*` renderer/RP assets.

## Player architecture

The player is `Assets/Player/Player.prefab`: a `CharacterController`, `PlayerMovement`, and a child Animator driven by `AC_Player.controller`.

- **Input**: `PlayerControls.inputactions` is the source of truth. `PlayerControls.cs` is **auto-generated** from it by the Input System ("Generate C# Class"). Never edit it by hand. Change the `.inputactions` asset and regenerate instead. The `Character` action map defines Movement, Aim (mouse screen position), Sprint, Jump, and Strike (not wired up yet).
- **`PlayerMovement.cs`** owns all player logic in `Update()`. Input callbacks only cache values. Each frame it applies manual gravity and jump velocity (no Rigidbody), moves via `CharacterController.Move`, rotates the player toward a raycast from the mouse against `aimLayerMask` (and moves the `aim` transform to the hit point), then pushes animator parameters.
- **Animator contract**: the code sets `xVelocity` and `zVelocity` (floats; movement direction projected onto the player's local axes, feeding the Idle/Walk blend tree), `IsGrounded` (bool), and `Jump` (trigger). Renaming parameters in `AC_Player.controller` means updating the string literals in `PlayerMovement.cs` too.
- **`PlayerCamera.cs`** (on Main Camera) is an angled top-down follow camera in `LateUpdate`. It creates its own `PlayerControls` instance and reads `Zoom` (mouse scroll) and `Aim` (pointer position, used for edge panning). It finds the player through `PlayerMovement` if `target` is unset.
- Animation clips are Mixamo-style FBX files in `Assets/Player/SnS-Anims/` (sword-and-shield set).

## Repo conventions

- Every asset needs its `.meta` file committed alongside it. Move or rename assets in Unity (or move the `.meta` along with them) so GUID references don't break.
- `.gitattributes` routes binary assets (`*.fbx`, `*.png`, etc.) through Git LFS and marks Unity YAML for `unityyamlmerge`. `git-lfs` must be installed locally for those rules to apply.
- Work happens on `task/<Name>` branches and merges into `main` through PRs. `CODEOWNERS` assigns everything to `@ClumsySword/Core`.
