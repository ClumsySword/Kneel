using UnityEngine;

// Sword and shield combat. R draws/sheathes, left click swings a 2-hit combo, right click blocks
// (the first moments of a fresh press parry), Ctrl rolls. Drives the animator layers directly and
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
    }

    private PlayerControls controls;

    private PlayerMovement movement;
    private PlayerEquipment equipment;
    private Health health;
    private Animator animator;
    private PlayerCamera playerCamera;

    [SerializeField]
    private PlayerCombatSettings settings;

    // Height above the feet where damage numbers and popups appear.
    [SerializeField]
    private float popupHeight = 2.1f;

    private int upperBodyLayer;
    private int fullBodyLayer;
    private float upperBodyWeight = 0f;
    private float fullBodyWeight = 0f;
    private float upperBodyTarget = 0f;
    private float fullBodyTarget = 0f;

    private float stateTime = 0f;
    private bool itemsSwapped = false;

    private int comboStep = 0;
    private bool attackQueued = false;
    private bool hitStarted = false;
    private bool hitEnded = false;

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

    public bool IsInvulnerable => State == CombatState.Dodging
        && stateTime >= settings.dodgeInvulnerableStart
        && stateTime <= settings.dodgeInvulnerableEnd;

    public bool IsParryWindowOpen => IsBlocking && pressCanParry && Time.time - blockPressTime <= settings.parryWindow;

    public Health Health => health;

    public PlayerCombatSettings Settings => settings;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Character.Draw.performed += ctx => OnDrawPressed();
        controls.Character.Strike.performed += ctx => OnStrikePressed();
        controls.Character.Block.performed += ctx => OnBlockPressed();
        controls.Character.Block.canceled += ctx => blockHeld = false;
        controls.Character.Dodge.performed += ctx => dodgeRequestTime = Time.time;

        if (settings == null)
        {
            Debug.LogWarning("PlayerCombat has no settings asset assigned, using defaults.", this);
            settings = ScriptableObject.CreateInstance<PlayerCombatSettings>();
        }

        movement = GetComponent<PlayerMovement>();
        equipment = GetComponent<PlayerEquipment>();
        health = GetComponent<Health>();
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        playerCamera = FindAnyObjectByType<PlayerCamera>();

        upperBodyLayer = animator.GetLayerIndex("UpperBody");
        fullBodyLayer = animator.GetLayerIndex("FullBody");

        health.SetMax(settings.maxHealth, true);

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
            TryStartAttack(0);
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
        }

        TryBufferedDodge();
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

    private bool TryStartAttack(int step)
    {
        AttackDefinition attack = settings.attacks[step];

        if (movement.TrySpendStamina(attack.staminaCost) == false)
        {
            return false;
        }

        SetState(CombatState.Attacking);
        comboStep = step;
        attackQueued = false;
        hitStarted = false;
        hitEnded = false;

        fullBodyTarget = 1f;
        animator.SetFloat("AttackSpeed", settings.attackSpeed);
        animator.CrossFadeInFixedTime(attack.stateName, 0.08f, fullBodyLayer, 0f);

        // Step into the swing. v = sqrt(2·a·d) covers the lunge distance while decelerating to a stop.
        float lungeSpeed = Mathf.Sqrt(2f * movement.Settings.deceleration * settings.attackLunge);
        movement.AddImpulse(transform.forward * lungeSpeed);
        return true;
    }

    private void QueueNextAttack()
    {
        AttackDefinition attack = settings.attacks[comboStep];
        float clipTime = stateTime * settings.attackSpeed;
        float timeUntilWindow = (attack.comboWindow - clipTime) / settings.attackSpeed;

        if (timeUntilWindow <= settings.comboBufferTime)
        {
            attackQueued = true;
        }
    }

    private void UpdateAttacking()
    {
        AttackDefinition attack = settings.attacks[comboStep];
        float clipTime = stateTime * settings.attackSpeed;

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

        if (attackQueued == true && clipTime >= attack.comboWindow)
        {
            equipment.SwordHitbox.End();
            int nextStep = (comboStep + 1) % settings.attacks.Length;

            if (TryStartAttack(nextStep) == true)
            {
                return;
            }

            attackQueued = false;
        }

        if (clipTime >= attack.recoveryEnd)
        {
            equipment.SwordHitbox.End();
            SetState(CombatState.Armed);
            fullBodyTarget = 0f;
        }
    }

    private void OnSwordHit(IDamageable target, Collider hitCollider, Vector3 point)
    {
        if ((Object)target == this)
        {
            return;
        }

        bool critical = target is IStaggerable staggerable && staggerable.IsStaggered;
        float damage = settings.attacks[comboStep].damage * (critical ? settings.criticalMultiplier : 1f);

        Vector3 direction = hitCollider.transform.position - transform.position;
        direction.y = 0f;

        target.TakeHit(new DamageInfo
        {
            amount = damage,
            point = point,
            direction = direction.normalized,
            source = gameObject,
            isCritical = critical,
        });

        HitSpark.Spawn(point, direction, critical ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.55f, 0.25f), critical ? 24 : 14);
        HitStop.Freeze(settings.hitStop);
        Shake(settings.hitShake * (critical ? 1.5f : 1f));
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
        if (movement.TrySpendStamina(settings.dodgeStaminaCost) == false)
        {
            return;
        }

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
                TryStartAttack(0);
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
        if (IsInvulnerable == true)
        {
            return;
        }

        Vector3 toAttacker = -info.direction;
        toAttacker.y = 0f;
        bool fromFront = Vector3.Angle(transform.forward, toAttacker) <= settings.blockAngle;
        Vector3 popup = transform.position + Vector3.up * popupHeight;

        // Fire and other world damage burns through the guard but never stuns or freezes time, so a damage
        // tick can't lock the player in place inside the thing that is hurting them.
        if (info.isEnvironmental == true)
        {
            health.ApplyDamage(info.amount);
            DamageNumber.Spawn(popup, Mathf.RoundToInt(info.amount).ToString(), DamageNumberStyle.PlayerDamage);
            Shake(settings.hitShake * 0.4f);
            return;
        }

        if (fromFront == true && IsParryWindowOpen == true)
        {
            if (info.source != null && info.source.TryGetComponent(out IParryable parried))
            {
                parried.OnParried(gameObject);
            }

            DamageNumber.Spawn(popup, "PARRY", DamageNumberStyle.Parry);
            HitSpark.Spawn(ShieldPoint(), toAttacker, new Color(1f, 0.85f, 0.4f), 30);
            animator.CrossFadeInFixedTime("Parry", 0.05f, upperBodyLayer, 0f);
            HitStop.Freeze(settings.parryHitStop);
            Shake(settings.hitShake * 1.5f);
            return;
        }

        if (fromFront == true && IsBlocking == true)
        {
            float absorbed = info.amount * settings.blockDamageReduction;
            bool guardBroken = movement.DrainStamina(absorbed * settings.blockStaminaPerDamage);

            if (guardBroken == true)
            {
                health.ApplyDamage(info.amount);
                DamageNumber.Spawn(popup, "GUARD BREAK " + Mathf.RoundToInt(info.amount), DamageNumberStyle.PlayerDamage);
                StartHitStun(settings.guardBreakDuration, "GuardBreak");
                Shake(settings.hitShake * 2f);
                return;
            }

            float taken = info.amount - absorbed;
            health.ApplyDamage(taken);
            DamageNumber.Spawn(popup, "BLOCK " + Mathf.RoundToInt(taken), DamageNumberStyle.Block);
            HitSpark.Spawn(ShieldPoint(), toAttacker, new Color(0.8f, 0.85f, 1f), 10);
            animator.CrossFadeInFixedTime("BlockHit", 0.05f, upperBodyLayer, 0f);
            HitStop.Freeze(settings.hitStop);
            Shake(settings.hitShake * 0.6f);
            return;
        }

        health.ApplyDamage(info.amount);
        DamageNumber.Spawn(popup, Mathf.RoundToInt(info.amount).ToString(), DamageNumberStyle.PlayerDamage);
        StartHitStun(settings.hitStunDuration, "HitReact");
        HitStop.Freeze(settings.hitStop);
        Shake(settings.hitShake * 1.5f);
    }

    private void StartHitStun(float duration, string reaction)
    {
        if (State == CombatState.Dodging)
        {
            // Caught in the roll's vulnerable tail.
            movement.forcedVelocity = null;
            SetEnemyCollision(true);
            animator.CrossFadeInFixedTime("Locomotion", 0.1f, 0);
        }

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
            case CombatState.HitStun:
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

    // Roughly where the raised shield is, for block and parry sparks.
    private Vector3 ShieldPoint()
    {
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
        controls.Character.Block.Enable();
        controls.Character.Dodge.Enable();
    }

    private void OnDisable()
    {
        controls.Character.Draw.Disable();
        controls.Character.Strike.Disable();
        controls.Character.Block.Disable();
        controls.Character.Dodge.Disable();

        // Never leave the layer collision switched off if disabled mid-roll.
        SetEnemyCollision(true);
    }
}
