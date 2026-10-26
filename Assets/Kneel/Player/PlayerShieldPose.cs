using UnityEngine;

// Poses the left (shield) arm with humanoid IK. The Mixamo block clip holds the shield out to the left and
// edge-on, so while guarding this places the shield squarely in front of the chest, facing the crosshair. On
// top of that guard it plays the block-hit recoil, the parry deflection and the shield bash as procedural
// motions (there is no bash clip). While drinking it brings the potion's neck to the lips, which the drinking
// clip's hand never reaches on this body. Lives on the Animator object; PlayerCombat decides when, and the
// pose numbers are in PlayerCombatSettings ("Shield Pose Info", "Shield Bash Info" and "Potion Info").
// Runs before the cape and lantern so their LateUpdate sims see the leaned chest.
[DefaultExecutionOrder(-5)]
[RequireComponent(typeof(Animator))]
public class PlayerShieldPose : MonoBehaviour
{
    private Animator animator;
    private PlayerCombat combat;
    private PlayerMovement movement;
    private PlayerEquipment equipment;
    private PlayerPotions potions;
    private Transform root;
    private Transform head;
    private Transform hand;
    private Transform chest;
    private int upperBodyLayer;

    // How much the procedural pose overrides the animated arm (0 = the clip's arm, 1 = fully posed).
    private float weight = 0f;

    // This frame's target: where the held item's reference point goes (the shield's centre, or the bottle's
    // neck) and the item's facing, in world space, and the chest offsets in degrees.
    private Vector3 shieldPosition;
    private Quaternion shieldRotation;
    private bool drinking = false;
    private float chestLean;
    private float chestTwist;

    // The humanoid IK goal isn't exactly the hand bone. This is the goal's offset from the bone, in the hand's
    // own space (so it holds in any pose), learned while posing: each frame the hand's miss nudges it.
    private Quaternion goalFromHandRotation = Quaternion.identity;
    private Vector3 goalFromHandPosition = Vector3.zero;
    private Vector3 desiredHandPosition;
    private Quaternion desiredHandRotation;
    private bool ikApplied = false;

    // How fast the offset is learned (per second), and how far it may drift, so an out-of-reach target
    // can't wind it up.
    private const float LearnRate = 15f;
    private const float MaxGoalOffset = 0.15f;

    // The goal rotation offset learned on the Ember Warden, so the first pose is right before any learning.
    [SerializeField]
    private Vector3 initialGoalRotation = new Vector3(270f, 268.64f, 0f);

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combat = GetComponentInParent<PlayerCombat>();
        movement = GetComponentInParent<PlayerMovement>();
        equipment = GetComponentInParent<PlayerEquipment>();
        potions = GetComponentInParent<PlayerPotions>();
        root = combat.transform;
        goalFromHandRotation = Quaternion.Euler(initialGoalRotation);
    }

    private void Start()
    {
        hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        }

        upperBodyLayer = animator.GetLayerIndex("UpperBody");
    }

    private void Update()
    {
        PlayerCombatSettings s = combat.Settings;
        PlayerCombat.CombatState state = combat.State;
        bool bashing = state == PlayerCombat.CombatState.Bashing;

        drinking = state == PlayerCombat.CombatState.Drinking && potions != null && potions.HandSocket != null;
        if (drinking)
        {
            UpdateDrink(s);
            return;
        }

        // Reactions and rolls own the whole body; let the clips have the arm straight away.
        if (equipment.ShieldInHand == false || state == PlayerCombat.CombatState.HitStun
            || state == PlayerCombat.CombatState.Dodging || state == PlayerCombat.CombatState.Dead)
        {
            weight = 0f;
        }
        else
        {
            bool raised = combat.IsBlocking || bashing;
            float time = Mathf.Max(0.01f, raised ? s.guardRaiseTime : s.guardLowerTime);
            weight = Mathf.MoveTowards(weight, raised ? 1f : 0f, Time.deltaTime / time);
        }

        if (weight <= 0f)
        {
            return;
        }

        Vector3 offset = s.guardOffset;
        float yaw = bashing ? 0f : AimYaw(s.guardMaxAimYaw);
        float tilt = s.guardTilt;
        chestLean = 0f;
        chestTwist = 0f;

        // Blocked hit: knocked back and up at once, then settles.
        float hitT = (Time.time - combat.LastBlockHitTime) / Mathf.Max(0.01f, s.blockHitPoseTime);
        if (hitT >= 0f && hitT < 1f)
        {
            float k = (1f - hitT) * (1f - hitT);
            offset.z -= s.blockHitRecoil * k;
            tilt += s.blockHitTilt * k;
        }

        // Parry: punches out and sweeps outward (to the left, away from the body), quick out and slower back.
        float parryT = (Time.time - combat.LastParryTime) / Mathf.Max(0.01f, s.parryPoseTime);
        if (parryT >= 0f && parryT < 1f)
        {
            float k = Mathf.Sin(Mathf.PI * Mathf.Sqrt(parryT));
            offset.z += s.parryPush * k;
            offset.x -= 0.1f * k;
            yaw -= s.parrySweep * k;
            chestTwist -= s.parryChestTwist * k;
        }

        if (bashing)
        {
            ApplyBash(s, ref offset);
        }

        // Swing the guard round the body toward the aim, keeping its height.
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 flat = yawRotation * new Vector3(offset.x, 0f, offset.z);
        shieldPosition = root.TransformPoint(flat + Vector3.up * offset.y);
        shieldRotation = root.rotation * yawRotation * Quaternion.Euler(-tilt, 0f, 0f);
    }

    // The bottle's neck at the lips, tipped up so it pours, blended in as the clip's arm rises and out as it
    // lowers. Fingers wrap round toward the face.
    private void UpdateDrink(PlayerCombatSettings s)
    {
        float t = combat.StateTime * s.drinkSpeed;
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.drinkRaiseStart, s.drinkAtMouth, t));
        float fall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.drinkLowerStart, s.drinkLowered, t));
        weight = rise * (1f - fall);
        chestLean = 0f;
        chestTwist = 0f;

        if (weight <= 0f || head == null)
        {
            return;
        }

        // Bottle direction (base to neck) in the player's space: the base sits out to the left, forward and
        // above the lips.
        Vector3 neckDirection = root.TransformDirection(-s.drinkBottleBase.normalized);
        Vector3 fingers = Vector3.ProjectOnPlane(root.right, neckDirection);
        shieldPosition = head.position + root.TransformVector(s.drinkMouthOffset);
        shieldRotation = Quaternion.LookRotation(-fingers, neckDirection);
    }

    // Coil the shield in, drive it out to bashReach with a forward lean, then ease back to the guard.
    private void ApplyBash(PlayerCombatSettings s, ref Vector3 offset)
    {
        float t = combat.StateTime;
        float guardZ = s.guardOffset.z;
        float coiledZ = guardZ - s.bashPullBack;

        if (t < s.bashWindup)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, s.bashWindup));
            offset.z = Mathf.Lerp(guardZ, coiledZ, k);
            chestTwist = -s.bashCoil * k;
        }
        else if (t < s.bashWindup + s.bashThrust)
        {
            float x = (t - s.bashWindup) / Mathf.Max(0.01f, s.bashThrust);
            float k = 1f - (1f - x) * (1f - x);
            offset.z = Mathf.Lerp(coiledZ, s.bashReach, k);
            chestTwist = Mathf.Lerp(-s.bashCoil, 0f, k);
            chestLean = s.bashLean * k;
        }
        else
        {
            float k = Mathf.SmoothStep(0f, 1f, (t - s.bashWindup - s.bashThrust) / Mathf.Max(0.01f, s.bashRecover));
            offset.z = Mathf.Lerp(s.bashReach, guardZ, k);
            chestLean = Mathf.Lerp(s.bashLean, 0f, k);
        }
    }

    // Degrees from the body's facing to the crosshair, clamped so the arm never wraps round.
    private float AimYaw(float limit)
    {
        Vector3 toAim = root.InverseTransformDirection(movement.AimPoint - root.position);
        toAim.y = 0f;
        if (toAim.sqrMagnitude < 0.01f)
        {
            return 0f;
        }

        return Mathf.Clamp(Mathf.Atan2(toAim.x, toAim.z) * Mathf.Rad2Deg, -limit, limit);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != upperBodyLayer)
        {
            return;
        }

        ikApplied = false;

        if (weight <= 0f || (drinking == false && equipment.ShieldRenderer == null))
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            return;
        }

        // The chest lean and twist are applied after IK (LateUpdate) and carry the arm with them, so aim the
        // arm at where the target will be before they're applied.
        Vector3 targetPosition = shieldPosition;
        Quaternion targetRotation = shieldRotation;
        Quaternion chestOffset = ChestOffset();
        if (chest != null && chestOffset != Quaternion.identity)
        {
            Quaternion undo = Quaternion.Inverse(chestOffset);
            targetPosition = chest.position + undo * (shieldPosition - chest.position);
            targetRotation = undo * shieldRotation;
        }

        // Item point → socket → hand bone → IK goal. Each link is rigid, so last frame's transforms give them.
        Transform socket = drinking ? potions.HandSocket : equipment.ShieldHandSocket;
        Vector3 itemPoint = drinking ? potions.HandBottleNeck : equipment.ShieldRenderer.bounds.center;
        Vector3 centerInSocket = Vector3.Scale(socket.InverseTransformPoint(itemPoint), socket.lossyScale);
        Vector3 socketPosition = targetPosition - targetRotation * centerInSocket;

        Quaternion socketInHand = Quaternion.Inverse(hand.rotation) * socket.rotation;
        Vector3 socketOffset = Vector3.Scale(hand.InverseTransformPoint(socket.position), hand.lossyScale);
        Quaternion handRotation = targetRotation * Quaternion.Inverse(socketInHand);
        Vector3 handPosition = socketPosition - handRotation * socketOffset;

        desiredHandPosition = handPosition;
        desiredHandRotation = handRotation;
        Quaternion goalRotation = handRotation * goalFromHandRotation;
        Vector3 goalPosition = handPosition + handRotation * goalFromHandPosition;

        animator.SetIKPosition(AvatarIKGoal.LeftHand, goalPosition);
        animator.SetIKRotation(AvatarIKGoal.LeftHand, goalRotation);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
        Vector3 elbowHint = drinking ? combat.Settings.drinkElbowHint : combat.Settings.elbowHint;
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, root.TransformPoint(elbowHint));
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, weight);
        ikApplied = true;
    }

    private void LateUpdate()
    {
        // Fully posed this frame: correct the goal offset by however far the hand landed from where it was sent.
        if (ikApplied == true && weight >= 1f && animator.GetLayerWeight(upperBodyLayer) > 0.99f)
        {
            float learn = 1f - Mathf.Exp(-LearnRate * Time.deltaTime);
            Quaternion inverseDesired = Quaternion.Inverse(desiredHandRotation);

            Quaternion rotationMiss = inverseDesired * hand.rotation;
            goalFromHandRotation = Quaternion.Slerp(goalFromHandRotation, Quaternion.Inverse(rotationMiss) * goalFromHandRotation, learn);

            Vector3 positionMiss = inverseDesired * (hand.position - desiredHandPosition);
            goalFromHandPosition = Vector3.ClampMagnitude(goalFromHandPosition - positionMiss * learn, MaxGoalOffset);
        }

        // Bash lean and coil, parry twist. Applied after the animation, so it never accumulates.
        Quaternion chestOffset = ChestOffset();
        if (chest != null && chestOffset != Quaternion.identity)
        {
            chest.rotation = chestOffset * chest.rotation;
        }
    }

    // World rotation added to the chest this frame (identity when the shield isn't posed).
    private Quaternion ChestOffset()
    {
        if (weight <= 0f || (chestLean == 0f && chestTwist == 0f))
        {
            return Quaternion.identity;
        }

        return Quaternion.AngleAxis(chestTwist * weight, root.up) * Quaternion.AngleAxis(chestLean * weight, root.right);
    }
}
