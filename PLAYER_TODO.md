# Player character: features still to add

Compared against the ten-pager (`Kneel Ashes of Man TenPager.pdf`). Ranked easiest → hardest.
Sprint, Jump, Draw, the parry on press, poise instead of stamina, and damage numbers stay as the code has them.
There's no camera mouse look-ahead.

Before starting, merge `main` into the working branch. It has the Hollow Archer, Blighted Brute and L4/L5, and
Blind and shield bash target those enemies.

## Done already
Eight-direction move, roll with i-frames, held block with chip damage, parry window, light attack that can't be
cancelled with input buffering, poise/stagger, hit-stop, Interact (E).

## To do

- [ ] **1. Blood-vignette damage state** (*~2 hrs*)
  A URP Volume `Vignette` or a full-screen image, weighted by `Health.Fraction`, that pulses on hit from `PlayerCombat.TakeHit`.
  The PDF has no health bar, so this is how the player reads damage.
- [ ] **2. Healing consumable, F** (*~half a day*)
  - New `Heal` action in `PlayerControls.inputactions`; regenerate `PlayerControls.cs`.
  - A carried count on the player, a `Health.Heal()` method, and a pickup trigger prefab.
  - A short committed drink state that restricts movement like the other states.
- [ ] **3. Ash meter core** (*~half a day*)
  - A `PlayerAsh` component that tracks current Ash, total spent and per-ability use counts.
  - Kills fill most of it. That needs a death event on `Enemy` (for example, hook `Health.OnDepleted`).
  - Ash carries over between encounters and levels, so it has to survive scene loads.
- [ ] **4. Ash siphon from parry and roll-through** (*trivial once 3 exists*)
  - The parry branch of `TakeHit` adds Ash.
  - The early return on `IsInvulnerable` is the "rolled through an attack" hook, so it adds Ash too.
  - Holding block gives nothing.
- [ ] **5. Heavy attack, Q** (*~1 day*)
  - A new `AttackDefinition` with its own entry point, outside the combo chain, plus a new input.
  - Find a Mixamo clip, convert it to Humanoid, and measure hitStart/hitEnd/recovery.
  - Add a "breaks guard" flag to `DamageInfo` for the Iron Husk.
- [ ] **6. Respawn at checkpoint, keeping progress** (*~1 day*)
  - `LevelRestartOnDeath` currently reloads the scene, which wipes heals and Ash.
  - Persist the player's state and spawn at the last shrine. The shrine object is shared with level work.
- [ ] **7. Lock-on, Tab** (*~1–2 days*)
  - Target the nearest visible enemy.
  - The camera frames the player and the target, and facing tracks the target (override the aim point in `PlayerMovement`).
  - Clear the lock when the target dies or leaves range.
- [ ] **8. Shield bash, hold RMB + LMB** (*~1–2 days*)
  - A new branch in `OnStrikePressed` while blocking: a bash clip, a short overlap in front of the shield, and high poise damage.
  - It has to interrupt enemy wind-ups, so enemies must accept a stagger during wind-up. Coordinate that with Olivia.
- [ ] **9. Scar and wisps tell** (*~1–2 days + art*)
  - An emissive vein material on the arm, driven by the Ash fraction through a `MaterialPropertyBlock` (same pattern as the lantern glass).
  - A wisp particle trail, and a colour-grade shift at full Ash.
  - Needs a retextured arm.
- [ ] **10. Keybind prompts on a verb's first use** (*~half a day*)
  A hint shown once per verb, remembered in the save. It depends on the unlock flow.
- [ ] **11. Prologue "body doesn't answer" mode** (*~1–2 days*)
  An input-lock / scripted-drive mode on `PlayerMovement` and `PlayerCombat`: inputs are read but ignored or
  delayed, and the body follows a scripted path.
- [ ] **12. Ash abilities: Shift, select with 1–5, unlocked by use count** (*~3–5 days total*)
  - [ ] Surge: speed burst that cancels recovery (reuse `forcedVelocity`). Easy.
  - [ ] Ashfall: radial overlap that staggers through `IStaggerable`. Easy.
  - [ ] Rekindle: small heal (reuses 2). Easy.
  - [ ] Ember Strike: an overhead that breaks poise (reuses 5). Medium.
  - [ ] Blind: thrown projectile, plus a "blinded" state on the Hollow Archer. Hard, because it touches enemy code.
  - [ ] Selection and the unlock logic (3 / 10 / 20 / 30 uses).

## Suggested order
1 → 2 → 3 → 4 → 5 → 8 → 7 → 6 → 9 → 12 → 10 → 11. Each step reuses the one before it.
