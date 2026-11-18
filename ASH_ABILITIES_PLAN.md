# Ash Abilities: Implementation Plan

Status: **implemented** on `task/AshAbilities` (2026-11-03), all five abilities and the grade. Where the build differs from this plan: Blind with no target in reach fizzles and spends nothing; the unlock counts stay at 3/10/20/30 with F1 buttons to unlock instantly; the F1 overlay adds itself to every level in the editor and development builds; Ember Strike's timings are 0.46-0.62 s (measured in the animator, see CLAUDE.md). Revision 3, 2026-11-03:
- Revision 2 was corrected after one adversarial review (changelog in §11).
- Revision 3 applies Lucas's answers to the decisions (§2) and drops the arm model edit (§5.1).

Sources:
- The ten-pager source, `../Kneel-Obsidian/10-TenPager/*.tex`. There's no built PDF in the vault; the `.tex` is the same text.
- The vault's design and tech notes.
- `main` at `cd7453b`, plus the unmerged `DeathScreen` branch (`18decef`).

**Deadline:** the ten-pager's projected ship date is **1 December 2026**, four weeks out. The plan is cut to fit (§8).

---

## 1. What the design asks for

Ten-pager page numbers are in brackets. Where Lucas's decisions (§2) change the text, the change is noted in *italics*.

**The meter [3, 4, 7]**
- The Master's own power, arriving down the channel that ties him to Kael. Both ways of filling it are theft.
- **Gains:** a **kill** releases "most of the meter". A **parry** or **rolling through an attack** "skims a little off the channel". **Holding block takes nothing.**
- Carries over **between encounters and between levels**. *It empties on death (D3).*
- Granted in **Level 2** [4].
- At zero Ash, Kael is "a soldier with a sword". Spending is "mechanically rewarded and no system ever punishes the player for it".
- **Read off the character, never off a bar:** "As the meter fills, the scar brightens and wisps trail off him." The meter "hums as it fills" [5].
- **Signature moment [6]:** when the meter first fills mid-fight, "the veins flare, the colour grade shifts, and Kael suddenly feels much more dangerous". The glow **stays** after the encounter.

**The scar [3, 5]**
- The scar runs up his arm, where the relic caught his binding.
- "The scar is a material swap, a retextured arm with an emissive vein shader, not a mesh edit." *The first version uses effects on the arm instead and leaves the model untouched (§5.1).*
- Together with enemy wind-up flashes, it is "the only saturated things on screen" [5].
- Concept art: the **right (sword) forearm and hand**, charred, with glowing orange crack-veins and loose sparks.

**Progression [7]:** "Each ability unlocks on a running count of Ash spent, and a use-count cannot be farmed or bought." The table counts **uses**.

| Ability | Ten-pager cost | Effect | Unlocks after |
|---|---|---|---|
| Ember Strike | Low | A poise-breaking overhead; staggers a guarding enemy | Granted with the meter (L2) |
| Blind | Low | A thrown ash projectile that blinds one enemy, suppressing its ranged attacks for several seconds | 3 uses |
| Surge | Medium | A brief speed burst that closes or breaks distance and cancels recovery | 10 uses |
| Ashfall | Medium | A short-range radial shockwave that staggers every adjacent enemy | 20 uses |
| Rekindle | High | A small heal | 30 uses |

*Under D8 every ability costs a full meter, so the cost column is ignored.*

**Controls [3]:** "Ash ability: Shift (select with 1–5), Level 2." *Under D1, keys 1–5 cast directly and Shift stays Sprint.*

**Where the abilities matter [8]:**
- Hollow Archer (L2): "**Blind it with Ash** and advance safely, or close the gap on foot."
- Cursed Twin (L4): "Ashfall to stagger both at once."
- The Master: in phase 2, "shield bash plus Ash to force the opening". In phase 3, "Ash spent to a plan".
- Outline: at **hour 1** (L3 or early L4), "two or three Ash abilities are in hand".

**Failure [2, 7]:** "Death puts the player back at the last checkpoint with everything still in hand." *Uses and unlocks are kept; the meter itself empties (D3).*

**Bonus [9]:** *Ashen Run*: "Ash that decays out of combat". Out of scope; leave a hook.

**Constraints from other docs and from Lucas**
- Combat Feel Spec: committed actions can't be cancelled, and inputs are buffered. **Surge is the named exception**: it "cancels recovery".
- Design Pillars: "No flashy spell VFX over enemies". Anything that hides a telegraph is a bug.
- Standing rule: **all gameplay feedback is diegetic**. No HUD, no floating text. The only exceptions are interaction prompts and "keybinds are shown on screen briefly when the verb is first introduced" [4].
- Shift stays Sprint. Parry stays on the press. No stamina.
- **Don't edit the Ember Warden model** for this (Lucas, revision 3).

---

## 2. Decisions (answered by Lucas)

| # | Question | Decision |
|---|---|---|
| D1 | How to cast | **Keys 1–5 each cast their ability directly.** |
| D2 | Uses vs Ash spent | **Count committed casts** of any ability. A cast counts once it has started, even if it misses. |
| D3 | Ash on death | **The meter empties to 0 on death. Uses and unlocks are kept.** Only **New Game** resets the use count. |
| D4 | Blind against melee enemies | **A blinded enemy can't *start* any attack.** |
| D5 | Surge i-frames | None. The roll is the evasion verb. |
| D6 | What Surge can cancel | The recovery tail of the player's own grounded attacks (`clipTime ≥ hitEnd`), a bash after its thrust, and a roll after its i-frames. |
| D7 | LEVELS-menu jump | **A practice session** that never writes the save. It's seeded with uses only (L2: 0, L3: 3, L4: 10, L5: 20), and the meter starts empty. |
| D8 | **Costs** | **Every ability needs a full meter, and casting empties it.** One cast per fill. The ten-pager's Low/Medium/High costs are dropped. |
| D9 | Does the dummy give Ash? | Only with the F1 switch on, once per "death" cycle. |
| D10 | Environmental kills | **Credit any thrall that dies while engaged with the player**, whatever killed it. |
| D11 | Blind in L2 | **Keep 3 uses.** Blind may arrive late in L2 or in L3, and an L2 player without it closes on the archer on foot, which the ten-pager also allows. |
| D12 | The arm | **No model change.** Effects on and around the arm, plus sound (§5.1). |

**Follow-ups from these answers:**
- **Quit and Continue:** the meter isn't saved, so Continue starts at 0. That matches a death and leaves nothing to farm. Between levels in one session the meter carries over (the ten-pager's "carries between levels").
- **Feedback becomes binary:** under D8 the player only needs to know whether the meter is full. That makes the diegetic readout much easier than the cost steps in revision 2 (§5.4).
- **The unlock pace changes:** one cast per fill means fewer casts in total, so the yields (§4.3) are set so that roughly two kills fill the meter. Under that rate, 20 and 30 uses may be out of reach in a single playthrough. That's checked in Phase 9 against the final enemy counts (§8.2).

---

## 3. What the codebase gives us

Each of these was checked.

**Death and restart: currently split across a branch**
- `main`'s `PauseMenu.cs` has **no death screen**. It's on the **unmerged `DeathScreen` branch**.
- On `main`, `Core/LevelRestartOnDeath.cs` reloads with `SceneManager.LoadScene`, and it's only in `Arena_S` and `L1_BrokenLine_Playable_L`. Dying in L2–L5 does nothing on `main`.
- `Gameplay/Hazards/LevelReset.cs` (with `ILevelResettable`) shows the *intended* future: reset the level **in place** without reloading, on death or rest. "Nothing calls this yet."
- **So the death reset can't be tied to "a scene loaded fresh".** It needs one entry point that every death and reset path calls (§4.2).

**Body and rendering**
- `Player_EmberWarden.fbx`: one SkinnedMeshRenderer, 14 360 verts, one submesh, one material (URP Lit, no emission), only UV0, no vertex colours, 69 bones.
- The UVs are a Meshy auto-unwrap: a mosaic of hundreds of tiny islands. A painted emission mask on them would bleed at the seams.
- Right-arm bones: `Shoulder_R` → `Elbow_R` → `Hand_R` → fingers with the `_1` suffix. Skinning is clean (dominant-bone vertex counts: `Elbow_R` 447, `Hand_R` 153, fingers about 140).
- **On-screen size:** the camera is pitched 65°, at 3.2–8.5 m, starting at the far end, with FOV about 60°. At 1080p that's roughly 110 px per metre, so the **forearm and hand are about 45 px long and 8 px wide**. Fine detail on the skin is sub-pixel at default zoom. Light, glow, particles and sound read; texture doesn't.
- **The lantern hangs on the right hip** and casts a warm, flickering light (1/0.62/0.3, intensity 4) onto the same hand. The arm's glow must not look like lantern light.
- `PC_Renderer` is **Forward+** and HDR. Its only renderer feature is **SSAO**. Every level has Bloom, with different thresholds, so HDR emission above about 1.5 glows everywhere.
- **Level colour grades are absolute values:** L2 has contrast 22 and saturation −38, L3 20/−34, L4 28/−30. **A Volume can't add to them.** URP overrides are absolute, and volume weight is clamped to 0..1.
- No Shader Graph or VFX Graph assets. Effects are Shuriken (`HitSpark` is built at runtime). `Kneel/Shaders/` holds hand-written shaders.

**Combat**
- `PlayerCombat` (1140 lines): states `Sheathed, Drawing, Armed, Sheathing, Attacking, HitStun, Dodging, Bashing, Drinking, Dead`. Attack timings are **clip-seconds**; the code compares `clipTime = stateTime * settings.attackSpeed`. Inputs are buffered with `*RequestTime`. Two overlay layers.
- `OnHeavyPressed` from `Sheathed` **only draws**. It doesn't buffer the heavy.
- `Die()` and `StartHitStun()` clear `forcedVelocity` and restore enemy collision **only for `Dodging`**. `UpdateShove()` overwrites or nulls `forcedVelocity`. Any new forced-motion state has to join those cleanups.
- Siphon hook points:
  - the parry branch of `TakeHit`
  - the i-frame early return in `TakeHit`
  - **but `HollowArcher/Arrow.cs` checks `IsInvulnerable` itself and never calls `TakeHit` while rolling**, so arrows would never feed the roll siphon in L2, the archer level
- `AshenFootman.TakeHit` already breaks the guard on `info.interrupts || …` (the bash).
- Four enemy types with their own `TakeHit`/`StaggerState`: Footman, Archer, Brute, Fool. The **Cursed Twin isn't implemented**. There's no shared "killed" event. `TargetDummy` isn't an `Enemy`.
- Playtest tools: `FootmanPlaytest`, `ArcherPlaytest`, `BrutePlaytest`, `FoolPlaytest`.

**Persistence and input**
- `SaveData` = `sceneName`, `savedAtUtc`, `playtimeSeconds`. The `Playtime` static is loaded once at startup, and auto-save writes memory.
- `PlayerControls.inputactions` binds no number keys and nothing for a gamepad. `KeybindService` and `ControlsPanel.Layout` handle rebinds.

**Code ownership:** Olivia, Patrick and Lucas have all committed to the enemy scripts. The §4.6 changes need a heads-up and a review.

---

## 4. Gameplay systems

### 4.1 Overview

```
  kills (Enemy.Killed)    parry (PlayerCombat.Parried)    evade (PlayerCombat.NotifyEvaded ← TakeHit, Arrow)
             \                       |                       /
              v                      v                      v
  AshSettings ──► AshMeter (on Player) ◄──► AshProgress (static; uses + unlocks in the save)
                    │ Changed / Gained(origin) / BecameFull / Spent / Unlocked / Fizzled
        ┌───────────┼──────────────────────────────┐
        v           v                              v
    PlayerAsh    AshPresenter                   AshAudio
  (1–5 cast,   (arm glow, hand light,          (hum, full cue, siphon,
   buffering)   embers, wisps, motes, grade)    spend, fizzle, unlock)
        │
        v
  PlayerCombat: CombatState.Casting; Ember Strike rides the Attacking path
```

New files:
- `Kneel/Player/Ash/`: `AshMeter`, `AshSettings` (ScriptableObject + `Player/Settings/AshSettings.asset`), `PlayerAsh`, `AshAbility` + one class per ability, `AshPresenter`, `AshAudio`, `AshSiphonMotes`, `AshArmGlow`, `VFX/` (particle prefabs, materials).
- `Kneel/Core/Progress/AshProgress.cs`
- `Kneel/Shaders/KneelEmberGlow.shader` (the arm's glow shell, §5.1) and `KneelAshGrade.shader` (the full-screen pass, §5.3).
- `Kneel/Combat/`: `IBlindable` and `IForceStaggerable`.

Nothing in `Player/Models/` changes.

### 4.2 AshProgress: persistent state and the death reset (`Kneel.Core`)

- **State:**
  - `float Ash` (live, never saved)
  - `int Uses`, `bool MeterGranted`, `int HintsSeen` (a bitmask for the once-per-save key hints), saved
  - `bool PracticeSession` (D7), not saved
- **Unlocks are derived from `Uses`** against each ability's `unlockUses`, so they can't get out of step with the count.
- **One reset entry point** (D3): `AshProgress.OnPlayerDeathReset()` sets `Ash = 0` and keeps everything else. It's a static `GameFlow.PlayerDeathReset` event that every death and reset path raises, so a new reset path can't forget it:
  - the death screen's Restart and Return to Main Menu
  - `LevelRestartOnDeath` (kept as the fallback)
  - `LevelReset.ResetAll()` once checkpoint shrines use it
- **The save** gains `ashUses`, `ashGranted` and `ashHints`. Not the meter.
  - **Continue** reloads these from the save, because the statics aren't trusted across menu round trips. The meter starts at 0.
  - **New Game** resets everything, including uses.
  - **A LEVELS jump** sets `PracticeSession`, applies the seed (uses only), and all auto-saves skip writing while it's set.
  - **A level transition** in one session keeps the live meter (the statics carry over).
- Old saves without the fields load as zeros. Continue into a level at or after L2 with `ashGranted` false grants the meter.
- `ResetStatics` on `SubsystemRegistration` (no domain reload).
- Levels are identified through `LevelCatalog` (scene → index), so no level scene needs an object added.

**Dependency:** this needs the **DeathScreen branch merged first** (Phase 0).

### 4.3 AshMeter: gains and spending

- Clamped to `0..maxAsh` (100). `IsFull => Ash >= maxAsh`. `TrySpendAll()` empties the meter if it's full and fails without any change if not.
- Gains while full are discarded. There's no overflow.
- **Gains are credited immediately**, on the event. Only the *visual* (motes flying to the arm, §5.2) lags behind. That avoids losing in-flight Ash on pause, and a cast straight after the filling kill works.
- Events: `Changed`, `Gained(source, amount, origin)`, **`BecameFull`**, `Spent(ability)`, `AbilityUnlocked`, `Fizzled`.
- **Before the meter is granted** (L1), gains are ignored and nothing presents.
- **Starting yields** (tunable, aiming at about two kills per fill):

  | Source | Ash |
  |---|---|
  | Footman kill | 50 |
  | Archer kill | 40 |
  | Brute or Fool kill | 100 (fills) |
  | Parry | 15 |
  | Roll-through | 5 |

- **Kill:**
  - a static `Enemy.Killed(Enemy)` raised from the base's `health.OnDepleted`, **once per enemy** (guarded)
  - credited per D10: the enemy was engaged with the player (in aggression range or chasing) when it died
  - each enemy type has an `ashYield` field
  - the dummy raises the same event, once per cycle, when the F1 switch is on (its `Health` raises `OnDepleted` on every hit at 0 HP, so it needs the guard)
- **Parry:** a `PlayerCombat.Parried(attacker)` event in the parry branch.
- **Roll-through:** a public `PlayerCombat.NotifyEvaded(GameObject source)`:
  - called from the i-frame branch of `TakeHit` and from `Arrow.cs`'s rolling early return (a one-line change in the archer's code)
  - deduplicated per source over `evadeDedupWindow` (0.5 s), because overlap attacks call `TakeHit` on several frames
  - environmental damage doesn't count
- **Block:** nothing.
- **Ashen Run hook:** `decayPerSecondOutOfCombat` = 0. Not built.

### 4.4 PlayerAsh: casting

- **Input:** new actions `Ash1`–`Ash5` on keys 1–5. Edit `PlayerControls.inputactions` and regenerate `PlayerControls.cs` (never hand-edit). Add the lines to `ControlsPanel.Layout`. No gamepad for now.
- **On a press**, checked in order:
  1. Is the meter granted and the ability unlocked? If not: a dull tap sound, nothing else.
  2. Is the meter full? If not: **fizzle** (§5.4). Nothing is spent.
  3. Is the combat state allowed for this ability? → start.
  4. Busy but the ability may follow? → **buffer** for `castBufferTime` (0.3 s). The meter empties at the actual start, not on the press.
- **Buffer priority** when several buffered inputs are live at the moment a state frees: **dodge > Ash cast > heavy > light**. Safety first, then the deliberate spend.
- **On start:** `TrySpendAll`, `Uses++`, raise `Spent`, then check unlocks. Unlocks are permanent.
- **Framework:**
  - an abstract `AshAbility`: `unlockUses`, `CanStartFrom(state, clipTime)`, `Begin/Tick/End`, `ApplyRestrictions`, `Interruptible`
  - **PlayerCombat** gets `CombatState.Casting`, `TryStartAbility`, and Update/restriction delegation
  - the **forced-motion cleanup** is refactored into one `EndForcedMotion()` (clears `forcedVelocity`, restores enemy collision, stops the shove). It's called by `Die`, `StartHitStun` and ability ends, for both `Dodging` **and** `Casting`.
  - `Dead` ends any ability, with no refund
- **Ember Strike from `Sheathed`:** draw, and **buffer the cast through the draw**, with a buffer window of the draw's length. This is new behaviour; the heavy doesn't do it today.

### 4.5 The five abilities

Every number is a starting value in `AshSettings`, tuned live through the F1 overlay. Because each cast takes a whole fill, each ability should feel worth a fill. The numbers are set a bit stronger than they would have been at the old Low costs.

#### Ember Strike: granted in L2. **Must-have.**
- An overhead that **breaks guard** and deals big poise damage. It's an `AttackDefinition` (`EmberStrike`: damage 30, poise 100) through the existing `StartAttack`, so hitbox, hit-stop and buffering just work, with an optional `hitModifier` (`breaksGuard`, `impact 0.6`, `ignite`).
- **Guard break reuses the existing path:** in the Footman (and any guarding enemy), the condition becomes `info.interrupts || info.breaksGuard`, so the bash and Ember Strike share one rule. `breaksGuard` doesn't imply `interrupts`, so the Brute's bash-specific poise multiplier stays bash-only.
- From `Armed`, grounded. From `Sheathed`: draw, then the buffered cast (§4.4).
- **Clip:** audition `sword and shield slash (2)`, `(5)` and `attack (3)` for an overhead. Convert to Humanoid and measure `hitStart/hitEnd/recoveryEnd` from blade-tip speed, on the FullBody layer.
- **Presentation:** the arm's glow runs into the hand, the blade's emission ramps up during the wind-up (a property block on the sword), an ember trail on `BladeTip` during the swing, a big ember `HitSpark` and extra hit-stop on impact.

#### Blind: unlocks after 3 uses (D11). **Must-have.**
- Throws ash at the enemy nearest the aim point (within 2.5 m of it, 12 m of the player). Light homing. A pooled projectile (`Enemies/ObjectPool`). A sphere-cast against `Enemy | Obstacles | Ground`.
- A hit calls `IBlindable.Blind(4 s)`: **the enemy can't start attacks** (D4). One check in `Enemy`, `CanStartAttack => !IsBlind`, used where each type's chase state enters its attack (about one line per type). Archers lower the bow. Melee enemies hold position and turn at a quarter speed.
- `UpperBody` layer, walking at 50 %. Release at the clip's measured throw frame. **Clip:** audition `casting (2)` and `kick`.
- **Presentation:** a grey ash puff from the hand. A tumbling ash cluster with a faint ember core. On the enemy, a **small** cloud **above and in front of the face**, kept off the shoulders (the Footman's tell is there). A procedural head wobble.

#### Ashfall: unlocks after 20 uses. **Should-have** (its enemy, the Cursed Twin, isn't built yet).
- A ground slam. Staggers every enemy within 3 m that has a clear line past `Obstacles`, through `IForceStaggerable.ForceStagger(duration)`, which ignores poise and guard (bosses can opt out). 5 damage, impact 0.8.
- FullBody, committed, rooted. **Clip:** `power up` or `casting`, trimmed to a 0.35 s wind-up.
- **Presentation:** a radial **dust and ash particle ring** (Shuriken, circle shape, horizontal velocity, ground height from a raycast), embers up from the impact point and a big shake. All under 0.4 s.

#### Surge: unlocks after 10 uses. **Could-have.**
- About 4 m in 0.3 s along the steering direction, by `forcedVelocity` on an authored `surgeCurve`, with the roll's wall-stop rule. No i-frames.
- **Cancels per D6**, compared in **clip time** (`stateTime * attackSpeed ≥ hitEnd`). Not from the air.
- **Joins `EndForcedMotion()`**, so a hit or death mid-surge can't leave enemy collision off. `UpdateShove` is suppressed while Surge drives `forcedVelocity`.
- No clip: Locomotion at sprint speed plus a spine lean.
- **Presentation:** ash afterimages from a **pool of 4 pre-allocated meshes** (`BakeMesh` into them, no allocation per cast), drawn **opaque with dither fade**. Wisp burst, dust kick, whoosh.

#### Rekindle: unlocks after 30 uses. **Could-have.**
- Heals 35 % of max health at the end of a 0.8 s channel. UpperBody, 30 % walk. A poise break interrupts it: the meter is spent and there's no heal (as with the potion). At full health it fizzles and spends nothing.
- **Clip:** the Drinking pose without the potion, or `power up`.
- **Presentation:** the arm's glow runs back up into the chest; the blood vignette recedes by itself.

### 4.6 Enemy-side changes

| Change | Where | For |
|---|---|---|
| `DamageInfo.breaksGuard`, sharing the `interrupts` guard-break branch | `DamageInfo`, `AshenFootman.TakeHit` (and the Brute/Fool if they guard) | Ember Strike |
| `IForceStaggerable.ForceStagger(float)` | each `Enemy` type → its `staggerState`; the dummy | Ashfall |
| `IBlindable`, `blindTimer`, `CanStartAttack` | `Enemy` base + one line in each type's chase→attack transition; the Archer's bow pose | Blind |
| `Enemy.Killed` (once per enemy), `ashYield`, engaged-with-player check | `Enemy` base; the dummy | Kill siphon |
| A `NotifyEvaded` call on the rolling early return | `HollowArcher/Arrow.cs` | Roll siphon from arrows |

All additive. One PR per phase that touches enemies, with a heads-up to Olivia and Patrick first.

---

## 5. Presentation: the diegetic interface

**The design constraint, from §3:** the forearm is about **45 × 8 px** at the default zoom, next to a warm, flickering lantern. The signals have to read at that size: **how bright the arm glows, light thrown onto the ground and sword, embers and wisps, and sound.** Under D8 the one thing the player *must* read is **"full or not"**, and that gets the strongest cues: a sound, a flare and a steady state (§5.4).

### 5.1 The arm without a model change (D12)

**Options considered:**

| | Approach | Model change? | Verdict |
|---|---|---|---|
| **G** | **Effects only:** an additive glow shell on the forearm and hand, a hand light, embers on the arm, wisps, all driven by the meter. The body keeps its material. | None | **Recommended.** It's the simplest, it's isolated from the body's rendering, and at 45 × 8 px a glow, a light and particles read better than skin texture anyway. |
| H | **Runtime arm mask:** at startup, copy the body's mesh in memory, write an arm mask and a hand-to-shoulder gradient into its vertex colours from the bone weights, and swap the body to a Lit copy with masked, procedural cracks | None (the FBX is untouched; the copy only exists at runtime) | **The later upgrade** if close-zoom cracks on the skin are wanted. It's the ten-pager's "material swap", but it puts a custom Lit shader on the whole body. |
| F | Split the arm into its own material slot with clean UVs in Blender | Yes | Dropped (D12). |
| B | A painted emission mask on the existing Meshy UVs | No, but the texture changes | The UV mosaic means seams and bleeding. |
| E | A decal on the forearm | None | Decals swim on skinned meshes, and there's no decal feature. |

**G, in detail** (`AshArmGlow`, added by `AshPresenter` at runtime, so the prefab needs only the component):

1. **Glow shell.** Two capsule meshes built in code: a forearm capsule from `Elbow_R` to `Hand_R` and a smaller one over the hand. Each is parented to its bone, so it follows the animation for free, with no skinning.
   - Radius: the arm's skin radius (measured from vertex distances to the bone, as the lantern's `bodyCapsules` were) **plus about 1 cm**, so it sits just outside the skin.
   - Shader `Kneel/Ember Glow` (hand-written, unlit, in `Kneel/Shaders/`): additive, ZWrite off, ZTest LEqual, no shadows.
     - **Fresnel rim** (bright at the silhouette edges, near zero face-on). It reads as heat coming off the arm, not a painted tube, and it avoids a flat-coloured sleeve.
     - **Scrolling 3D noise in the capsule's own space**, thresholded into thin bright lines. That gives a vein-like crawl along the arm that reads at close zoom. It's in object space, so it doesn't swim.
     - `_Fill` (0..1) reveals the glow from the hand up toward the elbow as the meter fills. `_Intensity`, `_Pulse` and `_Flicker` are set through a `MaterialPropertyBlock`.
     - HDR colour: a steady, saturated red-orange (e.g. 3.0/0.9/0.25 at full), with **low flicker** (±15 %), so it reads as unlike the lantern's paler, strongly flickering amber.
   - The capsules stop short of the elbow joint and the knuckles, so a bent elbow or a fist doesn't show the shape. The grip and the sword stay outside them.
2. **Hand light:** a small **non-shadow-casting** point light on `Hand_R`, the glow's colour, from 0 at empty to about 1.5 at full, range 1.5 m. It **throws ember light on the sword, shield and ground**, which reads at any zoom and from any angle. Forward+ handles the extra light cheaply. Shadowless, after the lantern's lesson (self-shadows sweeping across the body).
3. **Arm embers:** a Shuriken emitter on the forearm capsule's shape (mesh shape, parented to `Elbow_R`): small hot sparks that cling briefly in local space and then let go. Their rate scales with the meter.
4. **Skin tint (optional):** a `MaterialPropertyBlock` on the body can't target only the arm (one material). So charring the skin is H's job, not G's. G accepts that the skin itself stays unmarked.
5. **Fallback for H later:** data in vertex colours and UV2/UV3, set with `SetUVs(index)` explicitly (Unity's `Mesh.uv4` is channel 3). **Never TEXCOORD4+**, which `ObjectMotionVectors.hlsl` reads as the previous-frame skinned position. Copy `LitInput` into a shared include used by every pass, so the SRP Batcher stays compatible.

### 5.2 Wisps and motes (Shuriken prefabs under `Player/Ash/VFX/`)

- **Idle wisps:** emitted along `Elbow_R`→`Hand_R`, in world space so they trail. The rate scales with the meter (0 at empty, about 8/s at full), and they're **bigger than detail**: thick dark curls with bright sparks, so density reads at 8.5 m. They drift north-east like the levels' ash.
- **Siphon motes:** on a gain, 8–14 motes for a kill, 2–3 for a parry, 1–2 for an evade, flying from the origin (a corpse's chest, the attacker's weapon) to `Hand_R` over 0.4–0.8 s. Script-driven (`SetParticles`, pooled). Each one landing pulses the glow (`_Pulse`). Small and dark with a hot core. Their path rises first and then comes down, so they never cross an enemy's chest height in front of the camera.
- **While full:** heavier wisps, and embers drop from the fingertips now and then.

### 5.3 Colour grade: the signature moment

- **Not a Volume** (§3: level grades are absolute, so a Volume would *re*-saturate L2–L4).
- Instead, URP's built-in **`FullScreenPassRendererFeature`** on **both** PC and Mobile renderers, injected after post-processing, with a small `KneelAshGrade.shader`. It applies **relative** changes on top of whatever the level produced:
  - extra desaturation of everything **except** reds and oranges, so the arm and the enemy wind-ups become even more the only colour on screen
  - a slight warm lift in the shadows
  - a faint vignette in an ember tint
- Driven by one material float, `_AshGrade`. Under D8 it's simply **on while the meter is full**: 0 → 1 over 0.6 s on `BecameFull` (with an overshoot to 1.4 the first time per session), back to 0 over 0.4 s on a spend or death.
- The feature is skipped (zero cost) while `_AshGrade` is 0. Check it in every level; the overlay UI canvases aren't affected.

### 5.4 The "full" moment and affordability

Under D8 there is one question: **can I cast?** The answer is a sequence of cues on `BecameFull`, and then a steady state while it stays full.

**On `BecameFull`** (all together, under half a second):
- **The full cue** (Lucas's request): a distinct sound, an ember ignition rising into a low swell. It's the most important new sound, and it has to be recognisable through combat noise. It plays every time the meter fills. The first time in a session it's longer and louder, with the grade overshoot.
- The arm flares from hand to elbow (`_Pulse` 1 → 0 over 0.5 s), the hand light spikes, and an ember burst comes off the forearm.

**While full** (a steady state that answers "can I cast?" at a glance and without sound):
- The glow is at full brightness, the hand light is on, heavy wisps, embers drip from the fingers.
- The hum loop is at its loudest with a slow tremolo (§5.5).
- The colour grade is on.

**While filling** (progress, which is useful but less important):
- The glow climbs from the hand toward the elbow and brightens. The hand light stays **off** until full, so "light on the ground = ready" is a clean binary signal.
- Wisps increase. The hum rises in volume and pitch.

**Pressing too early (fizzle):** the glow stutters, dead grey ash falls from the fingers, a dry crackle plays. Nothing starts, nothing is spent.

**Pressing a locked ability:** a dull tap, nothing else.

**Spending:** everything drops to empty at once with the spend crack. The emptying itself is the feedback that the meter is gone.

**First-time key hints** (the allowed exception): when the meter is granted and when each ability unlocks, show a `KeybindService.KeyFor("Ash1")`-based prompt in the `PickupPrompt` style for about 4 s, once per save (the `HintsSeen` bitmask).

### 5.5 Audio (placeholders until Patrick's pass)

| Cue | Behaviour |
|---|---|
| **Full** | An ember ignition into a low swell. Every fill; bigger the first time in a session. **Priority 1.** |
| **Hum** | A loop. Inaudible at 0, rising in volume and +3 semitones in pitch as it fills, loudest with a slow tremolo while full. Paused with the game by `PauseAudio`. **Priority 2.** |
| Fizzle | A dry crackle |
| Locked key | A dull tap |
| Siphon arrive | A breathy rush per landing; bigger for kills |
| Spend | A shared ignition crack under each ability's own sound |
| Unlock | A deep tone with the arm's flare |
| Per ability | Ember Strike: ignition + heavy impact. Blind: puff + the enemy coughing. Ashfall: slam + rumble. Surge: whoosh. Rekindle: warm inhale. |

The cues are on the SFX mixer group. The hum is a looping source on SFX, so `PauseAudio` pauses it rather than low-passing it.

### 5.6 The unlock moment

`AbilityUnlocked` (usually mid-fight, on the cast that reaches the count) does two things together:
- a wrist-to-shoulder flare of the glow shell, bigger than the full flare
- a deep tone

Then the first-time key hint. No pause, no slow motion.

---

## 6. Tuning and debug

- **`AshSettings.asset`:**
  - the meter max, yields, the D10 engaged rule, the evade dedup
  - the practice seeds (uses per level)
  - each ability's unlock count, timings, radii, damage and poise
  - glow intensity and colour, shell radius offset, hand light, grade strength, wisp and ember rates
  - audio clips and levels
- **F1 overlay**, a new "Ash" section:
  - an Ash slider, a "fill" button, a uses slider
  - grant meter / unlock all / dummy gives Ash
  - the most-tuned sliders
  - a text readout of Ash and uses (F1 is the only text UI allowed)
- **Gizmos:** the glow capsules, Blind's aim radius, Ashfall's radius, Surge's path.
- **`AshPlaytest`** editor tool (the `FootmanPlaytest` pattern), writes `Temp/AshPlaytest.txt`.

---

## 7. Verification

Play-mode tests follow CLAUDE.md: `runInBackground`, invoke handlers by reflection, time with `Time.unscaledTime`, watch for editor stalls.

| Check | How |
|---|---|
| Gains | `Arena_S` with a Footman, an Archer and the dummy: player kill → +yield at once; kill in fire while engaged → +yield; unengaged fire death → 0; parry → +15; **rolling through an arrow** and a sword swing → +5 each, once per attack; blocked hit → 0; gains at full → discarded |
| Full rule | At 99 → press → fizzle, still 99. At 100 → press → cast, 0. Kill that fills, then an instant press → casts |
| Unlocks | Uses 2→3 unlocks Blind; 9→10, 19→20, 29→30. Unlocks follow `Uses` after a reload |
| Death reset | Die at 70 → Restart → **0, uses kept**. Die → Main Menu → Continue → 0, uses kept. Pause → Quit → Continue → 0, uses kept |
| Save | Continue restores uses, grant and hints; **New Game zeroes uses**; LEVELS jump to L4 → 10 uses, empty meter, and **the save file is unchanged after Quit**; an old save file loads |
| Level carry | Full meter at the end of L2 → L3 starts full |
| Abilities | Ember Strike on a guarding Footman → stagger; Blind on an Archer → no shot for 4 s, on a Footman → no swing for 4 s; Ashfall with 3 enemies, one behind a wall → 2 staggered; Surge before/after `hitEnd` in clip time; **Surge, then a hit mid-burst → enemy collision restored**; Rekindle interrupted → spent, no heal; Rekindle at full health → fizzle, not spent |
| Buffer | Cast during a swing → fires after; cast + dodge buffered together → dodge wins |
| **Readability (day 1 of Phase 2)** | Screenshots at **max zoom (8.5 m), at 1080p**, at 0/50/99/100 Ash, in L1, L2 and L5, with the lantern on. A non-team viewer can tell **full from not full** without sound, and the glow doesn't read as lantern light. The shell stays on the arm in idle, slash, block, roll and death, and doesn't show its capsule shape at the elbow. The left arm is untouched. No magenta on the Mobile renderer. The full cue is audible over a fight |
| Grade | L2 at full stays *less* saturated than without, while the arm and a Footman's wind-up stay saturated |
| Performance | Profiler: the glow shell, the grade pass, particles with 5 simultaneous kills |
| Regression | `FootmanPlaytest`, `ArcherPlaytest`, `BrutePlaytest`, `FoolPlaytest`, the death screen, sprint/dodge/heavy/bash |

---

## 8. Phases and cut line

### 8.1 Phases

Four weeks to 1 December, shared with everything else.

| Phase | Content | Days | Tier |
|---|---|---|---|
| **0** | **Merge the `DeathScreen` branch.** Heads-up to Olivia and Patrick about the enemy-side changes | ½ | Must |
| **1. Meter core** | `AshProgress` (save fields, `PlayerDeathReset`, practice sessions), `AshMeter` with the full rule, the kill/parry/evade sources incl. `Arrow.cs`, `AshSettings`, the F1 section | 2 | Must |
| **2. Arm and sound** | Day 1: the readability spike (§7) with a crude shell and light. Then `Kneel/Ember Glow`, `AshArmGlow` (shell, hand light, arm embers), `AshPresenter`, wisps, the full cue and the hum | 2 | Must |
| **3. Casting + Ember Strike** | Input + regenerate + ControlsPanel, `PlayerAsh`, `Casting`, the `EndForcedMotion` refactor, buffering with priority, fizzle, the Ember Strike clip and timings, `breaksGuard`, the key hints | 2–3 | Must |
| **4. Blind** | Projectile, `IBlindable` + `CanStartAttack`, the archer's bow pose, the cloud | 2 | Must |
| **5. Siphon motes + grade + unlock flare** | `AshSiphonMotes`, the full-screen grade pass, the unlock flare | 2 | Should |
| **6. Ashfall** | `IForceStaggerable` across four types + dummy, clip, dust ring | 2 | Should |
| **7. Surge** | Cancel rules, curve, pooled afterimages | 1–2 | Could |
| **8. Rekindle** | Channel, interrupt, drain visual | 1 | Could |
| **9. Tuning + playtest** | Yields and unlock counts on a real L1→L4 run, per-level bloom and grade check | 1–2 | Must |

**Cut line:** Must = **≈ 10–12 days** (down from 11–14, since the Blender work is gone). Phases 0–4 and 9 ship the meter, the arm, the full cue, Ember Strike and Blind: the ten-pager's floor ("refuse it and the player finishes with Ember Strike") plus L2's answer to the archer. Should and Could only if the Must work lands by about 22 November. If they're cut, Surge, Ashfall and Rekindle stay out of `AshSettings`' unlock table, so no player ever reaches an unlock that doesn't exist.

### 8.2 Do the numbers hit the outline?

With about two kills per fill (§4.3):
- L2's roughly 8–10 kills, plus parries, give about 4–6 fills. A player who spends every fill gets **Blind (3 uses) in mid-to-late L2**.
- **Surge (10)** needs about 20 Footman kills' worth, so late L3 or early L4: "two or three abilities by hour 1".
- **Ashfall (20)** and **Rekindle (30)** need 40–60 kills' worth. That's probably more than the game has. In Phase 9, once Patrick's L2–L4 enemy counts are final, either lower those two counts (e.g. 15 and 20), or raise the yields. Both are numbers in `AshSettings`; nothing else changes.

---

## 9. Risks

| Risk | Mitigation |
|---|---|
| The arm is unreadable at gameplay zoom | Day-1 spike before any polish. The hand light, which only comes on at full, plus the full cue and the hum, are the primary "can I cast" signals; the shell's glow is secondary |
| The glow looks like lantern light | Steady saturated red-orange vs the lantern's flicker; the hand light's colour; checked in the spike |
| The shell reads as a tube, or clips at the elbow and fingers | Fresnel rim instead of a solid fill; capsules stop short of the joints; checked in idle, slash, block, roll and death |
| Later wish for cracks on the skin | Approach H, without a model change (§5.1); the gameplay doesn't depend on the arm's technique |
| A death or reset path misses the reset | The single `GameFlow.PlayerDeathReset` event; a §7 test for each path |
| 20/30-use unlocks are out of reach | Retune the counts or the yields in Phase 9 (§8.2) |
| Effects hide telegraphs | Blinded enemies can't attack; clouds off shoulders; motes route above; short, low effects |
| Enemy code conflicts with teammates' branches | Additive changes, one PR per phase, heads-up first |
| Deadline | The cut line in §8.1; the Must tier alone is a complete L2 verb |

---

## 10. Out of scope

The Master's Ash requirement (with the boss), checkpoint shrines (the reset event is ready for them), Ashen Run decay, gamepad, lock-on, skin cracks (approach H).

---

## 11. Changelog

### Revision 3: Lucas's decisions

| Change | Where |
|---|---|
| Every ability needs a full meter and empties it (D8); costs dropped; yields retuned to about two kills per fill | §1, §2, §4.3, §4.5, §8.2 |
| The meter empties on death; uses and unlocks are kept; only New Game resets uses; the meter isn't saved (D3) | §2, §4.2, §7 |
| A full cue, and the hand light only at full, as the "can I cast" readout; the cost brightness steps removed | §5.4, §5.5 |
| Blind stays at 3 uses (D11) | §1, §2, §8.2 |
| No model change (D12): approach G (glow shell, hand light, embers) recommended; the runtime arm mask H as the later upgrade; the Blender split dropped | §5.1, §8, §9 |
| The grade is on while full | §5.3 |
| Practice seeds are uses only | D7 |

### Revision 2: the adversarial review

One review pass was done, and every finding was checked against the code before acting on it.

| Finding | Change |
|---|---|
| A Volume can't deepen absolute level grades, and weight clamps at 1 | §5.3 is a relative `FullScreenPassRendererFeature` grade; overshoot animates the value |
| Death screen only on a branch; restore tied to scene loads; quit/continue farm; restoring uses un-does unlocks | Phase 0 merges the branch; one `PlayerDeathReset` event for all paths; uses are never taken back |
| Arrows skip `TakeHit` during a roll | `NotifyEvaded`, called from `Arrow.cs` too |
| The Lit copy isn't the `KneelLitWind` pattern; SRP Batcher; TEXCOORD4 collides with motion vectors; `Mesh.uv4` naming | Kept as the rules for approach H |
| Arm ≈ 45 × 8 px; sub-pixel detail; lantern on the same hand | Light, glow, particles and sound as the readout; steady saturated colour unlike the lantern; readability spike on day 1 |
| Selection tint won't read; red clashes with the enemy telegraph | Direct cast on 1–5 |
| Surge: clip time vs state time; Dodging-only cleanup; `UpdateShove`; air attack; Ember Strike's draw-buffer claim was wrong | Clip-time comparisons; `EndForcedMotion()` refactor; shove suppressed; no air cancel; buffered cast through the draw specified as new behaviour |
| Blind's last-known-position AI is large and hides tells | Blinded enemies can't start attacks (one gate); cloud kept off the shoulders |
| Environmental kills paid nothing | D10: credit engaged deaths |
| LEVELS-jump seeding would overwrite the campaign save | D7: practice sessions never write the save; Continue reloads from the save |
| Four weeks to ship vs 16–22 days | §8 tiers and a cut line |
| Minor: dummy re-crediting, duplicate guard-break flag, delayed credit lost on quit, hints field, buffer priority, afterimage cost, ring quad on slopes, missing playtests | Once-per-cycle guard; `breaksGuard` shares the `interrupts` branch; immediate credit; `HintsSeen` in the save; dodge > cast > heavy > light; pooled meshes + dithered opaque; particle ring with raycast height; Brute and Fool playtests added |
