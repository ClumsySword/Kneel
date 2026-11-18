using System;
using Kneel.Core;
using UnityEngine;

// Keys 1-5 cast Kael's Ash abilities, each spending a full meter (AshMeter). Pressed early, it fizzles and nothing
// is spent; a locked one taps. Each cast counts as a use, and uses unlock the rest: Ashen Sword with the meter,
// then Blind, Surge, Ashfall and Rekindle (AshSettings). A press during another action casts when it ends, after a
// roll but before a heavy (PlayerCombat calls TryBufferedCast between them). Surge may also cut a recovery short.
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(AshMeter))]
public class PlayerAsh : MonoBehaviour
{
    public enum Ability
    {
        AshenSword,
        Blind,
        Surge,
        Ashfall,
        Rekindle,
    }

    public static readonly string[] Names = { "Ashen Sword", "Blind", "Surge", "Ashfall", "Rekindle" };

    private static readonly int AshSpeedId = Animator.StringToHash("AshSpeed");

    private PlayerControls controls;
    private PlayerCombat combat;
    private PlayerMovement movement;
    private AshMeter meter;
    private int enemyMask;
    private int obstacleMask;

    private Ability requested;
    private AshenSwordFlames swordFlames;
    private float ashenSwordUntil = -999f;
    private float requestTime = -999f;
    private float requestWindow;

    public AshSettings Settings => meter.Settings;

    public PlayerCombat Combat => combat;

    // A press that did nothing (meter not full, or no target), one on a locked ability, a cast that started, and an
    // ability unlocking.
    public event Action<Ability> Fizzled;

    public event Action<Ability> Locked;

    public event Action<Ability> Cast;

    public event Action<Ability> Unlocked;

    // Moments inside a cast that the presenter shows: the throw leaving the hand, the slam, the surge, the heal.
    public event Action<Vector3> BlindThrown;

    public event Action<Vector3> AshfallLanded;

    public event Action Surged;

    public event Action Rekindled;

    // True while the blade burns (Ashen Sword).
    public bool SwordIgnited => Time.time < ashenSwordUntil;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        movement = GetComponent<PlayerMovement>();
        meter = GetComponent<AshMeter>();
        enemyMask = LayerMask.GetMask("Enemy");
        obstacleMask = LayerMask.GetMask("Obstacles");

        combat.OnDied += EndAshenSword;
        combat.AttackLanded += OnAttackLanded;

        controls = new PlayerControls();
        controls.Character.Ash1.performed += ctx => OnPressed(Ability.AshenSword);
        controls.Character.Ash2.performed += ctx => OnPressed(Ability.Blind);
        controls.Character.Ash3.performed += ctx => OnPressed(Ability.Surge);
        controls.Character.Ash4.performed += ctx => OnPressed(Ability.Ashfall);
        controls.Character.Ash5.performed += ctx => OnPressed(Ability.Rekindle);
    }

    private void OnEnable()
    {
        controls.Character.Ash1.Enable();
        controls.Character.Ash2.Enable();
        controls.Character.Ash3.Enable();
        controls.Character.Ash4.Enable();
        controls.Character.Ash5.Enable();
    }

    private void Update()
    {
        // The multiplier follows the burn, so it's back to 1 the moment it runs out.
        combat.SwordDamageMultiplier = SwordIgnited ? Settings.ashenSwordDamageMultiplier : 1f;
    }

    private void OnDisable()
    {
        EndAshenSword();
        controls.Character.Ash1.Disable();
        controls.Character.Ash2.Disable();
        controls.Character.Ash3.Disable();
        controls.Character.Ash4.Disable();
        controls.Character.Ash5.Disable();
    }

    public int UnlockUses(Ability ability)
    {
        AshSettings s = Settings;
        switch (ability)
        {
            case Ability.Blind:
                return s.blindUnlock;
            case Ability.Surge:
                return s.surgeUnlock;
            case Ability.Ashfall:
                return s.ashfallUnlock;
            case Ability.Rekindle:
                return s.rekindleUnlock;
            default:
                return 0;
        }
    }

    public bool IsUnlocked(Ability ability) => AshProgress.Uses >= UnlockUses(ability);

    // The F1 overlay's button: enough uses for everything.
    public void UnlockAll()
    {
        int most = 0;
        foreach (Ability ability in Enum.GetValues(typeof(Ability)))
        {
            most = Mathf.Max(most, UnlockUses(ability));
        }

        SetUses(Mathf.Max(AshProgress.Uses, most));
    }

    // Sets the use count, announcing anything it unlocks.
    public void SetUses(int uses)
    {
        int before = AshProgress.Uses;
        AshProgress.Uses = Mathf.Max(0, uses);

        foreach (Ability ability in Enum.GetValues(typeof(Ability)))
        {
            int needed = UnlockUses(ability);
            if (needed > before && needed <= AshProgress.Uses)
            {
                Unlocked?.Invoke(ability);
            }
        }
    }

    private void OnPressed(Ability ability)
    {
        // A scripted hold (the Master's palm) ignores every action.
        if (combat.IsDead || combat.IsRestricted || meter.Granted == false)
        {
            return;
        }

        if (IsUnlocked(ability) == false)
        {
            Locked?.Invoke(ability);
            return;
        }

        if (meter.IsFull == false)
        {
            Fizzled?.Invoke(ability);
            return;
        }

        if (TryStart(ability))
        {
            return;
        }

        // Busy: cast as soon as the current action allows it. Ashen Sword with the sword away draws it and waits.
        requested = ability;
        requestTime = Time.time;
        requestWindow = Settings.castBufferTime;

        if (ability == Ability.AshenSword && combat.State == PlayerCombat.CombatState.Sheathed)
        {
            combat.DrawWeapon();
            requestWindow += combat.DrawTime;
        }
    }

    // Called by PlayerCombat each frame, after a buffered roll has had its chance.
    public void TryBufferedCast()
    {
        if (Time.time - requestTime > requestWindow)
        {
            return;
        }

        if (combat.IsDead || meter.IsFull == false)
        {
            requestTime = -999f;
            return;
        }

        if (TryStart(requested))
        {
            requestTime = -999f;
        }
    }

    // Starts the ability if the current state allows it. True when the press is dealt with (cast, or fizzled for
    // want of a target); false to keep it buffered.
    private bool TryStart(Ability ability)
    {
        bool free = combat.State == PlayerCombat.CombatState.Sheathed || combat.State == PlayerCombat.CombatState.Armed;
        bool grounded = combat.IsGrounded;

        switch (ability)
        {
            case Ability.AshenSword:
                // Instant, mid-swing included; it only needs the sword in hand.
                if (combat.SwordInHand == false || combat.State == PlayerCombat.CombatState.Sheathing
                    || combat.State == PlayerCombat.CombatState.Drawing
                    || combat.IsKneeling || combat.IsKnockedDown || combat.IsRestricted)
                {
                    return false;
                }

                Spend(ability);
                IgniteSword();
                return true;

            case Ability.Blind:
                if (free == false || grounded == false)
                {
                    return false;
                }

                Transform target = FindBlindTarget(out IBlindable blindable);
                if (target == null)
                {
                    Fizzled?.Invoke(ability);
                    return true;
                }

                Spend(ability);
                combat.StartCast(new BlindCast(this, target, blindable));
                return true;

            case Ability.Surge:
                if ((free == false && combat.InCancellableRecovery == false) || grounded == false)
                {
                    return false;
                }

                Spend(ability);
                combat.StartCast(new SurgeCast(this));
                return true;

            case Ability.Ashfall:
                if (free == false || grounded == false)
                {
                    return false;
                }

                Spend(ability);
                combat.StartCast(new AshfallCast(this));
                return true;

            case Ability.Rekindle:
                if (free == false || grounded == false)
                {
                    return false;
                }

                if (combat.Health.Current >= combat.Health.Max)
                {
                    Fizzled?.Invoke(ability);
                    return true;
                }

                Spend(ability);
                combat.StartCast(new RekindleCast(this));
                return true;
        }

        return false;
    }

    private void Spend(Ability ability)
    {
        meter.TrySpendAll();
        SetUses(AshProgress.Uses + 1);
        Cast?.Invoke(ability);
    }

    private void IgniteSword()
    {
        float seconds = Settings.ashenSwordDuration;
        ashenSwordUntil = Time.time + seconds;
        combat.SwordDamageMultiplier = Settings.ashenSwordDamageMultiplier;
        swordFlames = AshEffects.IgniteSword(combat.Sword, Settings);
        swordFlames.Ignite(seconds);
    }

    private void EndAshenSword()
    {
        ashenSwordUntil = -999f;
        if (combat != null)
        {
            combat.SwordDamageMultiplier = 1f;
        }

        if (swordFlames != null)
        {
            swordFlames.Extinguish();
        }
    }

    // A burning blade throws extra fire where it bites.
    private void OnAttackLanded(AttackDefinition attack, Vector3 point)
    {
        if (SwordIgnited)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            HitSpark.Spawn(point, direction.normalized, new Color(1f, 0.45f, 0.12f), 22);
        }
    }

    // The enemy nearest the crosshair within blindAimRadius of it, or failing that the nearest one ahead, within range.
    private Transform FindBlindTarget(out IBlindable blindable)
    {
        AshSettings s = Settings;
        Vector3 aim = movement.AimPoint;
        Transform best = null;
        blindable = null;
        float bestScore = float.MaxValue;

        foreach (Collider hit in Physics.OverlapSphere(transform.position, s.blindRange, enemyMask, QueryTriggerInteraction.Collide))
        {
            IBlindable candidate = hit.GetComponentInParent<IBlindable>();
            if (candidate == null || candidate is Enemy enemy && enemy.health.Current <= 0f)
            {
                continue;
            }

            Transform body = ((Component)candidate).transform;
            Vector3 toBody = body.position - transform.position;
            toBody.y = 0f;
            if (Physics.Linecast(transform.position + Vector3.up * 1.2f, body.position + Vector3.up * 1.2f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            Vector3 fromAim = body.position - aim;
            fromAim.y = 0f;
            float score;
            if (fromAim.magnitude <= s.blindAimRadius)
            {
                score = fromAim.magnitude;
            }
            else if (Vector3.Angle(transform.forward, toBody) <= 30f)
            {
                score = 100f + toBody.magnitude;
            }
            else
            {
                continue;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = body;
                blindable = candidate;
            }
        }

        return best;
    }

    private Vector3 HandPosition()
    {
        Transform hand = combat.Animator != null ? combat.Animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        return hand != null ? hand.position : transform.position + Vector3.up * 1.2f;
    }

    private void Shake(float strength)
    {
        PlayerCamera playerCamera = FindAnyObjectByType<PlayerCamera>();
        if (playerCamera != null)
        {
            playerCamera.AddShake(strength, 0.25f);
        }
    }

    // --- The casts ---

    // A handful of ash thrown at one enemy's eyes from the right hand. Kael can walk at half speed and keeps turning.
    private class BlindCast : AshCast
    {
        private readonly PlayerAsh ash;
        private readonly Transform target;
        private readonly IBlindable blindable;
        private bool thrown;

        public BlindCast(PlayerAsh ash, Transform target, IBlindable blindable)
        {
            this.ash = ash;
            this.target = target;
            this.blindable = blindable;
            MoveMultiplier = ash.Settings.throwMoveMultiplier;
            RotationLocked = false;
        }

        public override void Begin()
        {
            ash.combat.Animator.SetFloat(AshSpeedId, ash.Settings.throwSpeed);
            ash.combat.PlayUpperBody("AshThrow", 0.1f);
        }

        public override bool Tick(float time)
        {
            AshSettings s = ash.Settings;
            float clipTime = time * s.throwSpeed;

            if (thrown == false && clipTime >= s.throwReleaseTime)
            {
                thrown = true;
                Vector3 from = ash.HandPosition();
                AshEffects.ThrowBlind(from, target, blindable, s);
                ash.BlindThrown?.Invoke(from);
            }

            return clipTime < s.throwDuration;
        }
    }

    // A burst along where Kael is steering, or straight ahead. It can cut short the tail of a swing, a bash or a roll.
    // No i-frames: the roll is for that.
    private class SurgeCast : AshCast
    {
        private readonly PlayerAsh ash;
        private Vector3 direction;
        private float travelled;
        private float lastStep;
        private bool stopped;

        public SurgeCast(PlayerAsh ash)
        {
            this.ash = ash;
        }

        public override void Begin()
        {
            direction = ash.movement.MoveDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = ash.transform.forward;
            }

            direction.Normalize();
            ash.transform.rotation = Quaternion.LookRotation(direction);
            ash.Surged?.Invoke();
        }

        public override bool Tick(float time)
        {
            AshSettings s = ash.Settings;
            PlayerMovement movement = ash.movement;

            // Stops at a wall it meets head on, as the roll does.
            bool hitSide = (movement.LastCollisionFlags & CollisionFlags.Sides) != 0;
            if (hitSide && lastStep > 0.01f && Vector3.Dot(movement.LastMoveDelta, direction) < lastStep * 0.5f)
            {
                stopped = true;
            }

            float progress = Mathf.Clamp01(time / s.surgeDuration);
            float target = s.surgeCurve.Evaluate(progress) * s.surgeDistance;
            float step = target - travelled;
            travelled = target;
            lastStep = stopped ? 0f : step;

            bool canMove = stopped == false && Time.deltaTime > 0f;
            movement.forcedVelocity = canMove ? direction * (step / Time.deltaTime) : Vector3.zero;

            return progress < 1f;
        }
    }

    // A slam of the scarred hand into the ground: everything within reach and in the open staggers.
    private class AshfallCast : AshCast
    {
        private readonly PlayerAsh ash;
        private bool landed;

        public AshfallCast(PlayerAsh ash)
        {
            this.ash = ash;
        }

        public override void Begin()
        {
            ash.combat.Animator.SetFloat(AshSpeedId, ash.Settings.ashfallSpeed);
            ash.combat.PlayFullBody("Ashfall", 0.08f);
        }

        public override bool Tick(float time)
        {
            AshSettings s = ash.Settings;
            float clipTime = time * s.ashfallSpeed;

            if (landed == false && clipTime >= s.ashfallImpactTime)
            {
                landed = true;
                Land(s);
            }

            return clipTime < s.ashfallDuration;
        }

        private void Land(AshSettings s)
        {
            Vector3 center = ash.transform.position;
            var struck = new System.Collections.Generic.HashSet<Component>();

            foreach (Collider hit in Physics.OverlapSphere(center, s.ashfallRadius, ash.enemyMask, QueryTriggerInteraction.Collide))
            {
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || (UnityEngine.Object)damageable == ash.combat || struck.Add((Component)damageable) == false)
                {
                    continue;
                }

                Transform body = ((Component)damageable).transform;
                Vector3 chest = body.position + Vector3.up * 1f;
                if (Physics.Linecast(center + Vector3.up * 1f, chest, ash.obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                Vector3 direction = body.position - center;
                direction.y = 0f;

                damageable.TakeHit(new DamageInfo
                {
                    amount = s.ashfallDamage,
                    poiseDamage = 0f,
                    point = chest,
                    direction = direction.normalized,
                    source = ash.gameObject,
                });

                if (damageable is IForceStaggerable staggerable)
                {
                    staggerable.ForceStagger();
                }
            }

            AshEffects.AshfallRing(center, s);
            HitStop.Freeze(s.ashfallHitStop);
            ash.Shake(s.ashfallShake);
            ash.AshfallLanded?.Invoke(center);
        }
    }

    // The arm's fire drawn back into the body. A poise break before the hands come up wastes it.
    private class RekindleCast : AshCast
    {
        private readonly PlayerAsh ash;
        private bool healed;

        public RekindleCast(PlayerAsh ash)
        {
            this.ash = ash;
            MoveMultiplier = ash.Settings.rekindleMoveMultiplier;
            RotationLocked = false;
        }

        public override void Begin()
        {
            ash.combat.Animator.SetFloat(AshSpeedId, ash.Settings.rekindleSpeed);
            ash.combat.PlayUpperBody("Rekindle", 0.15f);
        }

        public override bool Tick(float time)
        {
            AshSettings s = ash.Settings;
            float clipTime = time * s.rekindleSpeed;

            if (healed == false && clipTime >= s.rekindleHealTime)
            {
                healed = true;
                Health health = ash.combat.Health;
                health.Heal(health.Max * s.rekindleHeal);
                ash.Rekindled?.Invoke();
            }

            return clipTime < s.rekindleDuration;
        }
    }
}
