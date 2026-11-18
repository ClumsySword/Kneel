using UnityEngine;

// Hands the shield between the back and the left hand during the back draw and sheathe. The motion is in the clips
// (BackDraw / BackSheath, keyframed on the Ember Warden in Blender and checked frame by frame for joint limits, speed
// and clipping; see CLAUDE.md), so there's no IK here: the shield follows its baked path in the left hand socket
// ("Back Draw Items" in PlayerCombatSettings, written with the clips). Draw: the hand takes the top rim overhand
// behind the shoulder, lifts it over, tosses it up a little and catches it in the grip. Sheathe: the reverse, then
// it's let go onto the back. The sword's hand-offs are PlayerCombat's (drawGrabTime / sheathReleaseTime).
// Lives on the Animator object, beside PlayerShieldPose, which leaves the left hand alone while IsPosing.
[RequireComponent(typeof(Animator))]
public class PlayerDrawPose : MonoBehaviour
{
    private PlayerCombat combat;
    private PlayerEquipment equipment;

    private PlayerCombat.CombatState lastState;
    private float lastTime;
    private bool wasPosing;

    private bool shieldTaken;
    private bool shieldSeated;
    private bool shieldReleased;

    private void Awake()
    {
        combat = GetComponentInParent<PlayerCombat>();
        equipment = GetComponentInParent<PlayerEquipment>();
    }

    // True while the draw or sheathe owns the hands (PlayerShieldPose keeps off the left meanwhile).
    public bool IsPosing => combat.State == PlayerCombat.CombatState.Drawing || combat.State == PlayerCombat.CombatState.Sheathing;

    // After the animation, so the shield rides this frame's hand.
    private void LateUpdate()
    {
        PlayerCombatSettings s = combat.Settings;
        float t = combat.StateTime * s.drawSpeed;

        if (combat.State != lastState || t < lastTime)
        {
            shieldTaken = false;
            shieldSeated = false;
            shieldReleased = false;
        }

        lastState = combat.State;
        lastTime = t;

        if (IsPosing == false)
        {
            if (wasPosing)
            {
                SettleShield(s);
            }

            wasPosing = false;
            return;
        }

        wasPosing = true;
        Transform shield = equipment.Shield;
        float frame = t * s.backClipFps;

        if (combat.State == PlayerCombat.CombatState.Drawing)
        {
            int first = s.backDrawShieldFirstFrame;
            int last = first + s.backDrawShieldPositions.Length - 1;

            if (shieldTaken == false && frame >= first && s.backDrawShieldPositions.Length > 0)
            {
                // The hand has the top rim: into the hand socket, then along the baked path.
                equipment.HoldShieldFree(equipment.ShieldHandSocket);
                shield.localScale = Vector3.one;
                shieldTaken = true;
            }

            if (shieldTaken && shieldSeated == false)
            {
                if (frame >= last)
                {
                    equipment.SetShieldInHand(true);
                    shieldSeated = true;
                }
                else
                {
                    Sample(s.backDrawShieldPositions, s.backDrawShieldRotations, frame - first, shield);
                }
            }

            return;
        }

        // Sheathing: along the baked path from the grip (toss, rim hold, over the shoulder), then onto the back.
        if (shieldReleased == false && shield.parent == equipment.ShieldHandSocket && s.backSheathShieldPositions.Length > 0)
        {
            Sample(s.backSheathShieldPositions, s.backSheathShieldRotations, frame, shield);
        }

        if (shieldReleased == false && t >= s.backShieldReleaseTime)
        {
            equipment.SetShieldInHand(false, s.backShieldStowSettleTime);
            shieldReleased = true;
        }
    }

    // The shield's pose in the hand socket at a fractional frame of a baked path.
    private static void Sample(Vector3[] positions, Quaternion[] rotations, float frame, Transform shield)
    {
        int n = positions.Length;
        float f = Mathf.Clamp(frame, 0f, n - 1);
        int i = Mathf.Min((int)f, n - 2 < 0 ? 0 : n - 2);
        float u = n > 1 ? f - i : 0f;
        int j = Mathf.Min(i + 1, n - 1);
        shield.localPosition = Vector3.Lerp(positions[i], positions[j], u);
        shield.localRotation = Quaternion.Slerp(rotations[i], rotations[j], u);
    }

    // A draw or sheathe that ended or was cut short: the shield goes where the sword is (in hand, or on the back).
    private void SettleShield(PlayerCombatSettings s)
    {
        if (equipment.SwordInHand && (equipment.ShieldInHand == false || equipment.Shield.localPosition != Vector3.zero))
        {
            equipment.SetShieldInHand(true, s.shieldSettleTime);
        }
        else if (equipment.SwordInHand == false && (equipment.ShieldInHand || equipment.Shield.parent != equipment.ShieldBackSocket))
        {
            equipment.SetShieldInHand(false, s.shieldSettleTime);
        }
    }
}
