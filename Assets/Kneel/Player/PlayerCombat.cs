using UnityEngine;

// Sword and shield combat. R draws/sheathes, left click swings a 2-hit combo, Q swings a slow heavy
// roundhouse, either one in the air plunges into an air attack, right click blocks (the first moments
// of a fresh press parry), left click while blocking shield-bashes, Ctrl rolls, F drinks a potion from the
// belt (sheathed only, see PlayerPotions). The shield arm itself is
// posed by PlayerShieldPose from the state exposed here. Drives the animator layers directly and
// tells PlayerMovement what it may do each frame. Tuning lives in PlayerCombatSettings.
// Runs before PlayerMovement so the movement rules it sets apply on the same frame.
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerEquipment))]
[RequireComponent(typeof(Health))]
public class PlayerCombat : MonoBehaviour, IDamageable
{
    public enum CombatState
    {
        Sheathed,
        Drawing,
        Armed,
        Sheathing,
        Attacking,
        HitStun,
        Dodging,
        Bashing,
        Drinking,
        Dead,
    }

    private PlayerControls controls;

    private PlayerMovement movement;
    private PlayerEquipment equipment;
    private PlayerPotions potions;
    private Health health;
    private Animator animator;
    private PlayerCamera playerCamera;

    [SerializeField]
    private PlayerCombatSettings settings;

    private int upperBodyLayer;
    private int fullBodyLayer;
    private float upperBodyWeight = 0f;
    private float fullBodyWeight = 0f;
    private float upperBodyTarget = 0f;
    private float fullBodyTarget = 0f;

    private float stateTime = 0f;
    private bool itemsSwapped = false;

    // The swing playing, and its place in the light combo (-1 for the heavy and air attacks, which don't chain).
    private AttackDefinition currentAttack;
    private int comboStep = -1;
    private bool attackQueued = false;
    private bool hitStarted = false;
    private bool hitEnded = false;
    private bool lungeDone = false;

    // Air attack still falling: hangs, then plunges so it lands on the clip's touchdown.
    private bool airborne = false;

    // Most downward speed the plunge will use to land on time.
    private const float MaxPlungeSpeed = 25f;

    private float heavyRequestTime = -999f;

    private bool sipped = false;

    private bool bashLunged = false;
    private readonly System.Collections.Generic.HashSet<IDamageable> bashed = new System.Collections.Generic.HashSet<IDamageable>();
    private readonly Collider[] bashBuffer = new Collider[16];
    private int enemyMask;

    private float stunDuration = 0f;

    private bool blockHeld = false;
    private float blockPressTime = -999f;
    private bool pressCanParry = false;

    private float dodgeRequestTime = -999f;
    private float attackRequestTime = -999f;
    private float dodgeClipLength = 1f;
    private Vector3 dodgeDirection;
    private float dodgeTravelled = 0f;
    private bool dodgeStoppedByWall = false;
    private float lastDodgeStep = 0f;

    public CombatState State { get; private set; } = CombatState.Sheathed;

    public bool IsBlocking { get; private set; }

    public bool IsDead => State == CombatState.Dead;

    // Raised once when health reaches zero (LevelRestartOnDeath listens).
    public event System.Action OnDied;

    public bool IsInvulnerable => State == CombatState.Dodging
        && stateTime >= settings.dodgeInvulnerableStart
        && stateTime <= settings.dodgeInvulnerableEnd;

    public bool IsParryWindowOpen => IsBlocking && pressCanParry && Time.time - blockPressTime <= settings.parryWindow;

    public Health Health => health;

    // Synced from settings every frame so overlay edits apply live.
    public Poise Poise { get; } = new Poise();

    public PlayerCombatSettings Settings => settings;

    public float StateTime => stateTime;

    // When the shield last took a blocked hit or made a parry (Time.time), for PlayerShieldPose's reactions.
    public float LastBlockHitTime { get; private set; } = -999f;

    public float LastParryTime { get; private set; } = -999f;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Character.Draw.performed += ctx => OnDrawPressed();
        controls.Character.Strike.performed += ctx => OnStrikePressed();
        controls.Character.HeavyAttack.performed += ctx => OnHeavyPressed();
        controls.Character.Heal.performed += ctx => OnHealPressed();
        controls.Character.Block.performed += ctx => OnBlockPressed();
        controls.Character.Block.canceled += ctx => blockHeld = false;
        controls.Character.Dodge.performed += ctx => dodgeRequestTime = Time.time;

        if (settings == null)
        {
            Debug.LogWarning("PlayerCombat has no settings asset assigned, using defaults.", this);
            settings = ScriptableObject.CreateInstance<PlayerCombatSettings>();
        }

        enemyMask = LayerMask.GetMask("Enemy");
        movement = GetComponent<PlayerMovement>();
        equipment = GetComponent<PlayerEquipment>();
        potions = GetComponent<PlayerPotions>();
        health = GetComponent<Health>();

        SyncPoiseSettings();
        Poise.Reset();
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        playerCamera = FindAnyObjectByType<PlayerCamera>();

        upperBodyLayer = animator.GetLayerIndex("UpperBody");
        fullBodyLayer = animator.GetLayerIndex("FullBody");

        health.SetMax(settings.maxHealth, true);
        health.OnDepleted += Die;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "dodge-roll")
            {
                dodgeClipLength = clip.length;
            }
        }

        equipment.SetSwordInHand(false);
        equipment.SetShieldInHand(false);
        equipment.SwordHitbox.OnHit += OnSwordHit;
    }

    private void OnDrawPressed()
    {
        if (State == CombatState.Sheathed)
        {
            StartDraw();
        }
        else if (State == CombatState.Armed && IsBlocking == false)
        {
            StartSheath();
        }
    }

    private void OnStrikePressed()
    {
        if (DebugTuningOverlay.IsPointerOverPanel == true)
        {
            return;
        }

        if (State == CombatState.Sheathed)
        {
            // Attacking with the sword away draws it first.
            StartDraw();
        }
        else if (State == CombatState.Armed && blockHeld == false)
        {
            // Checks the button, not IsBlocking, so release-block-and-swing works on the same frame.
            if (movement.IsGrounded == false)
            {
                StartAirAttack();
            }
            else
            {
                StartAttack(settings.attacks[0], 0);
            }
        }
        else if (State == CombatState.Armed && blockHeld == true)
        {
            StartBash();
        }
        else if (State == CombatState.Attacking)
        {
            QueueNextAttack();
        }
        else if (State == CombatState.Dodging)
        {
            // Swing as soon as the roll finishes.
            attackRequestTime = Time.time;
        }
    }

    private void OnHeavyPressed()
    {
        if (DebugTuningOverlay.IsPointerOverPanel == true)
        {
            return;
        }

        if (State == CombatState.Sheathed)
        {
            StartDraw();
        }
        else if (State == CombatState.Armed && blockHeld == false)
        {
            StartHeavyAttack();
        }
        else
        {
            // Busy (swinging, rolling, drawing): swing it as soon as that ends, since actions can't be cancelled.
            heavyRequestTime = Time.time;
        }
    }

    private void StartHeavyAttack()
    {
        heavyRequestTime = -999f;

        if (movement.IsGrounded == false)
        {
            StartAirAttack();
        }
        else
        {
            StartAttack(settings.heavyAttack);
        }
    }

    private void TryBufferedHeavy()
    {
        if (Time.time - heavyRequestTime > settings.heavyBufferTime)
        {
            return;
        }

        if (State != CombatState.Armed || blockHeld == true)
        {
            return;
        }

        StartHeavyAttack();
    }

    private void OnBlockPressed()
    {
        blockHeld = true;

        // Only a fresh press gets a parry window, so mashing block doesn't parry everything.
        pressCanParry = Time.time - blockPressTime >= settings.parryCooldown;
        blockPressTime = Time.time;
    }

    private void Update()
    {
        stateTime += Time.deltaTime;

        SyncPoiseSettings();
        Poise.Tick(Time.deltaTime);

        switch (State)
        {
            case CombatState.Drawing:
                UpdateDrawing();
                break;
            case CombatState.Sheathing:
                UpdateSheathing();
                break;
            case CombatState.Attacking:
                UpdateAttacking();
                break;
            case CombatState.HitStun:
                UpdateHitStun();
                break;
            case CombatState.Dodging:
                UpdateDodging();
                break;
            case CombatState.Bashing:
                UpdateBashing();
                break;
            case CombatState.Drinking:
                UpdateDrinking();
                break;
        }

        TryBufferedDodge();
        TryBufferedHeavy();
        UpdateBlocking();
        ApplyMovementRules();
        UpdateLayerWeights();
    }

    private void SetState(CombatState next)
    {
        State = next;
        stateTime = 0f;
    }

    // --- Draw / sheathe ---

    private void StartDraw()
    {
        SetState(CombatState.Drawing);
        itemsSwapped = false;
        upperBodyTarget = 1f;
        animator.SetFloat("DrawSpeed", settings.drawSpeed);
        animator.CrossFadeInFixedTime("Draw1", 0.1f, upperBodyLayer, 0f);
    }

    private void UpdateDrawing()
    {
        float clipTime = stateTime * settings.drawSpeed;

        if (itemsSwapped == false && clipTime >= settings.drawGrabTime)
        {
            itemsSwapped = true;
            equipment.SetSwordInHand(true);
            equipment.SetShieldInHand(true);
        }

        if (clipTime >= settings.drawDuration)
        {
            SetState(CombatState.Armed);
        }
    }

    private void StartSheath()
    {
        SetState(CombatState.Sheathing);
        itemsSwapped = false;
        animator.SetFloat("DrawSpeed", settings.drawSpeed);
        animator.CrossFadeInFixedTime("Sheath1", 0.1f, upperBodyLayer, 0f);
    }

    private void UpdateSheathing()
    {
        float clipTime = stateTime * settings.drawSpeed;

        if (itemsSwapped == false && clipTime >= settings.sheathReleaseTime)
        {
            itemsSwapped = true;
            equipment.SetSwordInHand(false);
            equipment.SetShieldInHand(false);
        }

        if (clipTime >= settings.sheathDuration)
        {
            SetState(CombatState.Sheathed);
            upperBodyTarget = 0f;
        }
    }

    // --- Attacks ---

    private void StartAttack(AttackDefinition attack, int comboIndex = -1)
    {
        SetState(CombatState.Attacking);
        currentAttack = attack;
        comboStep = comboIndex;
        attackQueued = false;
        hitStarted = false;
        hitEnded = false;
        lungeDone = false;
        airborne = false;

        fullBodyTarget = 1f;
        animator.SetFloat("AttackSpeed", settings.attackSpeed);
        animator.CrossFadeInFixedTime(attack.stateName, 0.08f, fullBodyLayer, 0f);
    }

    private void StartAirAttack()
    {
        StartAttack(settings.airAttack);
        airborne = true;
        movement.forcedVerticalVelocity = 0f;
    }

    private void QueueNextAttack()
    {
        // Only the light combo chains.
        if (comboStep < 0)
        {
            return;
        }

        float clipTime = stateTime * settings.attackSpeed;
        float timeUntilWindow = (currentAttack.comboWindow - clipTime) / settings.attackSpeed;

        if (timeUntilWindow <= settings.comboBufferTime)
        {
            attackQueued = true;
        }
    }

    private void UpdateAttacking()
    {
        if (airborne == true)
        {
            UpdateAirborne();
        }

        AttackDefinition attack = currentAttack;
        float clipTime = stateTime * settings.attackSpeed;

        // Step into the swing. v = sqrt(2·a·d) covers the lunge distance while decelerating to a stop.
        if (lungeDone == false && clipTime >= attack.lungeTime)
        {
            lungeDone = true;
            if (attack.lunge > 0f)
            {
                float lungeSpeed = Mathf.Sqrt(2f * movement.Settings.deceleration * attack.lunge);
                movement.AddImpulse(transform.forward * lungeSpeed);
            }
        }

        if (hitStarted == false && clipTime >= attack.hitStart)
        {
            hitStarted = true;
            equipment.SwordHitbox.Begin();
        }

        if (hitStarted == true && hitEnded == false && clipTime >= attack.hitEnd)
        {
            hitEnded = true;
            equipment.SwordHitbox.End();
        }

        if (attackQueued == true && comboStep >= 0 && clipTime >= attack.comboWindow)
        {
            equipment.SwordHitbox.End();
            int next = (comboStep + 1) % settings.attacks.Length;
            StartAttack(settings.attacks[next], next);
            return;
        }

        if (clipTime >= attack.recoveryEnd)
        {
            equipment.SwordHitbox.End();
            SetState(CombatState.Armed);
            fullBodyTarget = 0f;
        }
    }

    // The air attack's fall: carry forward, hang briefly at the top, then plunge at whatever speed lands
    // on the clip's touchdown. If the ground is further than that, the clip waits just before touchdown.
    private void UpdateAirborne()
    {
        if (movement.IsGrounded == true)
        {
            LandAirAttack();
            return;
        }

        movement.forcedVelocity = transform.forward * settings.airAttackCarry;

        float clipTime = stateTime * settings.attackSpeed;
        if (clipTime < settings.airPlungeTime)
        {
            movement.forcedVerticalVelocity = 0f;
            return;
        }

        if (clipTime >= settings.airLandTime)
        {
            stateTime = settings.airLandTime / settings.attackSpeed;
            animator.SetFloat("AttackSpeed", 0f);
        }

        float timeLeft = (settings.airLandTime - stateTime * settings.attackSpeed) / settings.attackSpeed;
        float plungeSpeed = timeLeft > 0.01f ? DistanceToGround() / timeLeft : MaxPlungeSpeed;
        movement.forcedVerticalVelocity = -Mathf.Clamp(plungeSpeed, settings.airPlungeSpeed, MaxPlungeSpeed);
    }

    private void LandAirAttack()
    {
        StopAirborne();

        // Down early: skip ahead to the touchdown so the impact plays now.
        if (stateTime * settings.attackSpeed < settings.airLandTime)
        {
            stateTime = settings.airLandTime / settings.attackSpeed;
            animator.CrossFadeInFixedTime(currentAttack.stateName, 0.05f, fullBodyLayer, settings.airLandTime);
        }

        Shake(settings.airLandShake);
    }

    // Hands the fall back to normal movement (landed, or interrupted by a hit or death).
    private void StopAirborne()
    {
        if (airborne == false)
        {
            return;
        }

        airborne = false;
        movement.forcedVelocity = null;
        movement.forcedVerticalVelocity = null;
        animator.SetFloat("AttackSpeed", settings.attackSpeed);
    }

    private float DistanceToGround()
    {
        int notPlayer = ~(1 << gameObject.layer);
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 50f, notPlayer, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(0f, hit.distance - 0.1f);
        }

        return 50f;
    }

    private void OnSwordHit(IDamageable target, Collider hitCollider, Vector3 point)
    {
        if ((Object)target == this)
        {
            return;
        }

        bool critical = target is IStaggerable staggerable && staggerable.IsStaggered;
        float multiplier = critical ? settings.criticalMultiplier : 1f;
        AttackDefinition attack = currentAttack;

        Vector3 direction = hitCollider.transform.position - transform.position;
        direction.y = 0f;

        target.TakeHit(new DamageInfo
        {
            amount = attack.damage * multiplier,
            poiseDamage = attack.poiseDamage * multiplier,
            point = point,
            direction = direction.normalized,
            source = gameObject,
            isCritical = critical,
        });

        HitSpark.Spawn(point, direction, critical ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.55f, 0.25f), critical ? 24 : 14);
        HitStop.Freeze(settings.hitStop);
        Shake(settings.hitShake * (critical ? 1.5f : 1f));
    }

    // --- Potion ---

    // Only with the weapons away. The bottle leaves the belt at once, so being knocked out of the drink
    // before the sip wastes it.
    private void OnHealPressed()
    {
        if (State != CombatState.Sheathed || potions == null || potions.TryTake() == false)
        {
            return;
        }

        SetState(CombatState.Drinking);
        sipped = false;
        potions.ShowInHand(true);
        upperBodyTarget = 1f;
        animator.SetFloat("DrinkSpeed", settings.drinkSpeed);
        animator.CrossFadeInFixedTime("Drink", 0.15f, upperBodyLayer, 0f);
    }

    private void UpdateDrinking()
    {
        float clipTime = stateTime * settings.drinkSpeed;

        if (sipped == false && clipTime >= settings.drinkSipTime)
        {
            sipped = true;
            health.Heal(settings.potionHeal);
        }

        if (clipTime >= settings.drinkDuration)
        {
            potions.ShowInHand(false);
            SetState(CombatState.Sheathed);
            upperBodyTarget = 0f;
        }
    }

    // --- Shield bash ---

    // Block + left click: a quick shove with the shield. Short wind-up, a thrust that hits whatever is in
    // front of the shield and cancels attacks still winding up, then a short recovery back to guard.
    private void StartBash()
    {
        SetState(CombatState.Bashing);
        bashLunged = false;
        bashed.Clear();
    }

    private void UpdateBashing()
    {
        float thrustEnd = settings.bashWindup + settings.bashThrust;

        if (bashLunged == false && stateTime >= settings.bashWindup)
        {
            bashLunged = true;
            float lungeSpeed = Mathf.Sqrt(2f * movement.Settings.deceleration * settings.bashLunge);
            movement.AddImpulse(transform.forward * lungeSpeed);
        }

        if (stateTime >= settings.bashWindup && stateTime <= thrustEnd)
        {
            BashOverlap();
        }

        if (stateTime >= settings.BashDuration)
        {
            SetState(CombatState.Armed);
        }
    }

    private void BashOverlap()
    {
        Vector3 center = ShieldPoint() + transform.forward * 0.15f;
        int count = Physics.OverlapSphereNonAlloc(center, settings.bashRadius, bashBuffer, enemyMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            IDamageable target = bashBuffer[i].GetComponentInParent<IDamageable>();
            if (target == null || (Object)target == this || bashed.Add(target) == false)
            {
                continue;
            }

            Vector3 direction = bashBuffer[i].transform.position - transform.position;
            direction.y = 0f;
            Vector3 point = bashBuffer[i].ClosestPoint(center);

            target.TakeHit(new DamageInfo
            {
                amount = settings.bashDamage,
                poiseDamage = settings.bashPoiseDamage,
                point = point,
                direction = direction.normalized,
                source = gameObject,
                interrupts = true,
            });

            HitSpark.Spawn(point, direction, new Color(0.85f, 0.85f, 0.9f), 18);
            HitStop.Freeze(settings.hitStop);
            Shake(settings.bashShake);
        }
    }

    // --- Dodge roll ---

    // Rolls are only started from a free state. Pressed during another action, the roll waits
    // (buffered) and fires as soon as that action finishes, since committed actions can't be cancelled.
    private void TryBufferedDodge()
    {
        if (Time.time - dodgeRequestTime > settings.dodgeBufferTime)
        {
            return;
        }

        if (State != CombatState.Sheathed && State != CombatState.Armed)
        {
            return;
        }

        dodgeRequestTime = -999f;
        StartDodge();
    }

    private void StartDodge()
    {
        // Roll where the player is steering, or straight ahead with no input. Snap to face it.
        dodgeDirection = movement.MoveDirection;
        dodgeDirection.y = 0f;
        if (dodgeDirection.sqrMagnitude < 0.01f)
        {
            dodgeDirection = transform.forward;
        }

        dodgeDirection.Normalize();
        transform.rotation = Quaternion.LookRotation(dodgeDirection);

        SetState(CombatState.Dodging);
        dodgeTravelled = 0f;
        dodgeStoppedByWall = false;
        lastDodgeStep = 0f;
        attackRequestTime = -999f;
        equipment.SwordHitbox.End();
        attackQueued = false;
        IsBlocking = false;

        // The roll is a full-body base layer motion; drop the overlay layers immediately.
        upperBodyTarget = 0f;
        upperBodyWeight = 0f;
        fullBodyTarget = 0f;
        fullBodyWeight = 0f;

        animator.SetFloat("DodgeSpeed", dodgeClipLength / settings.dodgeDuration);
        animator.CrossFadeInFixedTime("Dodge", 0.05f, 0, 0f);

        if (settings.dodgeThroughEnemies == true)
        {
            SetEnemyCollision(false);
        }
    }

    private void UpdateDodging()
    {
        // Rolling into a wall stops short rather than sliding along it. Only a head-on block counts
        // (less than half of last frame's step made it through); grazing a corner keeps rolling.
        bool hitSide = (movement.LastCollisionFlags & CollisionFlags.Sides) != 0;
        float progressMade = Vector3.Dot(movement.LastMoveDelta, dodgeDirection);
        if (hitSide == true && lastDodgeStep > 0.01f && progressMade < lastDodgeStep * 0.5f)
        {
            dodgeStoppedByWall = true;
        }

        float progress = Mathf.Clamp01(stateTime / settings.dodgeDuration);
        float target = settings.dodgeTravelCurve.Evaluate(progress) * settings.dodgeDistance;
        float step = target - dodgeTravelled;
        dodgeTravelled = target;
        lastDodgeStep = dodgeStoppedByWall ? 0f : step;

        bool canMove = dodgeStoppedByWall == false && Time.deltaTime > 0f;
        movement.forcedVelocity = canMove ? dodgeDirection * (step / Time.deltaTime) : Vector3.zero;

        if (progress >= 1f)
        {
            EndDodge();

            // A click made during the roll swings now.
            if (State == CombatState.Armed && Time.time - attackRequestTime <= settings.comboBufferTime + settings.dodgeDuration)
            {
                attackRequestTime = -999f;
                StartAttack(settings.attacks[0], 0);
            }
        }
    }

    private void EndDodge()
    {
        movement.forcedVelocity = null;
        SetEnemyCollision(true);
        animator.CrossFadeInFixedTime("Locomotion", 0.15f, 0);

        if (equipment.SwordInHand == true)
        {
            SetState(CombatState.Armed);
            upperBodyTarget = 1f;
            animator.CrossFadeInFixedTime("ArmedIdle", 0.15f, upperBodyLayer);
        }
        else
        {
            SetState(CombatState.Sheathed);
        }
    }

    private void SetEnemyCollision(bool collide)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            Physics.IgnoreLayerCollision(gameObject.layer, enemyLayer, collide == false);
        }
    }

    // --- Blocking and getting hit ---

    private void UpdateBlocking()
    {
        bool shouldBlock = blockHeld && State == CombatState.Armed;

        if (shouldBlock == IsBlocking)
        {
            return;
        }

        IsBlocking = shouldBlock;
        animator.CrossFadeInFixedTime(IsBlocking ? "Block" : "ArmedIdle", 0.12f, upperBodyLayer);
    }

    public void TakeHit(DamageInfo info)
    {
        if (IsInvulnerable == true || IsDead == true)
        {
            return;
        }

        Vector3 toAttacker = -info.direction;
        toAttacker.y = 0f;
        bool fromFront = Vector3.Angle(transform.forward, toAttacker) <= settings.blockAngle;

        // Fire and other world damage burns through the guard but never stuns or freezes time, so a damage
        // tick can't lock the player in place inside the thing that is hurting them.
        if (info.isEnvironmental == true)
        {
            health.ApplyDamage(info.amount);
            Shake(settings.hitShake * 0.4f);
            return;
        }

        if (fromFront == true && IsParryWindowOpen == true)
        {
            if (info.source != null && info.source.TryGetComponent(out IParryable parried))
            {
                parried.OnParried(gameObject);
            }

            LastParryTime = Time.time;
            HitSpark.Spawn(ShieldPoint(), toAttacker, new Color(1f, 0.85f, 0.4f), 30);
            animator.CrossFadeInFixedTime("Parry", 0.05f, upperBodyLayer, 0f);
            HitStop.Freeze(settings.parryHitStop);
            Shake(settings.hitShake * 1.5f);
            return;
        }

        if (fromFront == true && IsBlocking == true)
        {
            // The shield soaks most of the poise damage too; what gets through can still break the guard.
            if (Poise.ApplyDamage(info.poiseDamage * settings.blockPoiseMultiplier) == true)
            {
                health.ApplyDamage(info.amount);
                StartHitStun(settings.staggerDuration, "GuardBreak");
                HitStop.Freeze(settings.hitStop);
                Shake(settings.hitShake * 2f);
                return;
            }

            LastBlockHitTime = Time.time;
            float taken = info.amount * (1f - settings.blockDamageReduction);
            health.ApplyDamage(taken);
            HitSpark.Spawn(ShieldPoint(), toAttacker, new Color(0.8f, 0.85f, 1f), 10);
            animator.CrossFadeInFixedTime("BlockHit", 0.05f, upperBodyLayer, 0f);
            HitStop.Freeze(settings.hitStop);
            Shake(settings.hitShake * 0.6f);
            return;
        }

        bool poiseBroken = Poise.ApplyDamage(info.poiseDamage);
        health.ApplyDamage(info.amount);
        HitStop.Freeze(settings.hitStop);

        if (poiseBroken == true)
        {
            StartHitStun(settings.staggerDuration, "GuardBreak");
            Shake(settings.hitShake * 2f);
            return;
        }

        Shake(settings.hitShake * 1.5f);

        // Poise held: a committed action (swing, draw, sheathe, roll) carries on through the hit.
        if (IsInCommittedAction == false)
        {
            StartHitStun(settings.hitStunDuration, "HitReact");
        }
    }

    private bool IsInCommittedAction => State == CombatState.Attacking
        || State == CombatState.Bashing
        || State == CombatState.Drinking
        || State == CombatState.Drawing
        || State == CombatState.Sheathing
        || State == CombatState.Dodging;

    private void SyncPoiseSettings()
    {
        Poise.maxPoise = settings.maxPoise;
        Poise.regenDelay = settings.poiseRegenDelay;
        Poise.regenRate = settings.poiseRegenRate;
    }

    // Health reached zero. Everything stops; the scene decides what happens next (see OnDied).
    private void Die()
    {
        if (IsDead == true)
        {
            return;
        }

        if (State == CombatState.Dodging)
        {
            movement.forcedVelocity = null;
            SetEnemyCollision(true);
        }

        StopAirborne();
        potions?.ShowInHand(false);
        equipment.SwordHitbox.End();
        attackQueued = false;
        IsBlocking = false;
        SetState(CombatState.Dead);

        upperBodyTarget = 0f;
        upperBodyWeight = 0f;
        fullBodyTarget = 1f;
        fullBodyWeight = 1f;
        animator.CrossFadeInFixedTime("Death", 0.1f, fullBodyLayer, 0f);

        OnDied?.Invoke();
    }

    private void StartHitStun(float duration, string reaction)
    {
        // The killing blow already switched to Dead (health's OnDepleted fires inside ApplyDamage).
        if (IsDead == true)
        {
            return;
        }

        if (State == CombatState.Dodging)
        {
            // Caught in the roll's vulnerable tail.
            movement.forcedVelocity = null;
            SetEnemyCollision(true);
            animator.CrossFadeInFixedTime("Locomotion", 0.1f, 0);
        }

        StopAirborne();
        potions?.ShowInHand(false);
        equipment.SwordHitbox.End();
        attackQueued = false;
        IsBlocking = false;

        // A draw or sheathe cut short keeps whatever the hands were holding at that moment.
        SetState(CombatState.HitStun);
        stunDuration = duration;
        fullBodyTarget = 1f;
        fullBodyWeight = 1f;
        animator.CrossFadeInFixedTime(reaction, 0.05f, fullBodyLayer, 0f);
    }

    private void UpdateHitStun()
    {
        if (stateTime < stunDuration)
        {
            return;
        }

        fullBodyTarget = 0f;

        if (equipment.SwordInHand == true)
        {
            SetState(CombatState.Armed);
            upperBodyTarget = 1f;
            animator.CrossFadeInFixedTime("ArmedIdle", 0.15f, upperBodyLayer);
        }
        else
        {
            SetState(CombatState.Sheathed);
            upperBodyTarget = 0f;
        }
    }

    // --- Shared ---

    private void ApplyMovementRules()
    {
        float moveMultiplier = 1f;
        bool rotationLocked = false;
        bool sprintBlocked = false;
        bool jumpBlocked = false;

        switch (State)
        {
            case CombatState.Attacking:
                moveMultiplier = 0f;
                rotationLocked = stateTime > settings.attackTurnWindow;
                sprintBlocked = true;
                jumpBlocked = true;
                break;
            case CombatState.Bashing:
                moveMultiplier = 0f;
                rotationLocked = stateTime > settings.attackTurnWindow;
                sprintBlocked = true;
                jumpBlocked = true;
                break;
            case CombatState.Drinking:
                moveMultiplier = settings.drinkMoveMultiplier;
                sprintBlocked = true;
                jumpBlocked = true;
                break;
            case CombatState.HitStun:
            case CombatState.Dead:
                moveMultiplier = 0f;
                rotationLocked = true;
                sprintBlocked = true;
                jumpBlocked = true;
                break;
            case CombatState.Drawing:
            case CombatState.Sheathing:
                sprintBlocked = true;
                break;
            case CombatState.Dodging:
                rotationLocked = true;
                sprintBlocked = true;
                jumpBlocked = true;
                break;
        }

        if (IsBlocking == true)
        {
            moveMultiplier = settings.blockMoveMultiplier;
            sprintBlocked = true;
            jumpBlocked = true;
        }

        movement.moveSpeedMultiplier = moveMultiplier;
        movement.rotationLocked = rotationLocked;
        movement.sprintBlocked = sprintBlocked;
        movement.jumpBlocked = jumpBlocked;
    }

    private void UpdateLayerWeights()
    {
        // Snap into actions quickly, ease back out.
        float fullSpeed = fullBodyTarget > fullBodyWeight ? 12f : 5f;
        fullBodyWeight = Mathf.MoveTowards(fullBodyWeight, fullBodyTarget, fullSpeed * Time.deltaTime);
        upperBodyWeight = Mathf.MoveTowards(upperBodyWeight, upperBodyTarget, 6f * Time.deltaTime);

        animator.SetLayerWeight(fullBodyLayer, fullBodyWeight);
        animator.SetLayerWeight(upperBodyLayer, upperBodyWeight);
    }

    // Centre of the shield in hand, for the bash and for block and parry sparks.
    private Vector3 ShieldPoint()
    {
        if (equipment.ShieldInHand == true && equipment.ShieldRenderer != null)
        {
            return equipment.ShieldRenderer.bounds.center;
        }

        return transform.position + transform.forward * 0.5f + Vector3.up * 1.2f;
    }

    private void Shake(float strength)
    {
        if (playerCamera != null)
        {
            playerCamera.AddShake(strength, 0.18f);
        }
    }

    private void OnEnable()
    {
        controls.Character.Draw.Enable();
        controls.Character.Strike.Enable();
        controls.Character.HeavyAttack.Enable();
        controls.Character.Heal.Enable();
        controls.Character.Block.Enable();
        controls.Character.Dodge.Enable();
    }

    private void OnDisable()
    {
        controls.Character.Draw.Disable();
        controls.Character.Strike.Disable();
        controls.Character.HeavyAttack.Disable();
        controls.Character.Heal.Disable();
        controls.Character.Block.Disable();
        controls.Character.Dodge.Disable();

        // Never leave the layer collision switched off if disabled mid-roll, or the fall overridden mid-plunge.
        SetEnemyCollision(true);
        if (movement != null)
        {
            movement.forcedVelocity = null;
            movement.forcedVerticalVelocity = null;
        }
    }
}
