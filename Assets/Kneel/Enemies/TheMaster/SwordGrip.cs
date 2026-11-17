using UnityEngine;

// Holds the sword hand's wrist where the clips were never made for a sword (owner, 2026-11-15: "he is holding his sword
// very wrong", the Master's Guard hand "rotates in a weird way"). Runs after the Animator and turns the hand bone:
//  - Forearm: the wrist held straight, rolled so the blade leaves the fist toward his front: the sword moves with
//    the forearm like a blade held in a stiff grip (the Colossus's Mutant clips curl and flap an open claw).
//  - Upright: the blade straight up in front of him, the edge toward the foe, whatever the arm does (the Master's Guard,
//    a two-handed greatsword block played with one hand).
// The body sets Mode and Weight each frame; the weight eases so changes never snap.
[DefaultExecutionOrder(100)]
public class SwordGrip : MonoBehaviour
{
    public enum Hold { Forearm, Upright }

    public Hold Mode { get; set; }

    // 0 leaves the clip's hand alone, 1 holds it fully.
    public float Weight { get; set; }

    private Animator anim;
    private Transform hand;
    private Transform forearm;
    private Transform socket;
    private MasterBody body;
    private float weight;

    public static SwordGrip On(MasterBody body)
    {
        SwordGrip grip = body.GetComponent<SwordGrip>() ?? body.gameObject.AddComponent<SwordGrip>();
        grip.body = body;
        grip.anim = body.anim;
        grip.hand = body.anim.GetBoneTransform(HumanBodyBones.RightHand);
        grip.forearm = body.anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        grip.socket = body.swordTip != null ? body.swordTip.parent.parent : null;
        return grip;
    }

    private void LateUpdate()
    {
        weight = Mathf.MoveTowards(weight, Weight, Time.deltaTime * 4f);
        if (body.BodyVisible == false)
            return;
        Apply();
    }

    // At full Weight at once (for previews outside Play mode).
    public void ApplyNow()
    {
        weight = Weight;
        Apply();
    }

    private void Apply()
    {
        if (weight <= 0.001f || hand == null || forearm == null || socket == null)
            return;

        Vector3 up = transform.up;
        Vector3 front = transform.forward;
        Vector3 blade, edge;
        if (Mode == Hold.Upright)
        {
            blade = (up + front * 0.25f - transform.right * 0.12f).normalized;
            edge = Vector3.ProjectOnPlane(front, blade).normalized;
        }
        else
        {
            // The forearm's line (elbow to wrist); the blade square to it, toward his front and a little up.
            Vector3 along = (hand.position - forearm.position).normalized;
            blade = Vector3.ProjectOnPlane(front + up * 0.35f, along);
            if (blade.sqrMagnitude < 1e-3f)
                blade = Vector3.ProjectOnPlane(up, along);
            blade.Normalize();
            // The edge the way the knuckles point: along the forearm.
            edge = Vector3.ProjectOnPlane(along, blade).normalized;
        }

        if (edge.sqrMagnitude < 1e-3f)
            return;

        // The socket's frame (EnemyRig.Socket, MasterSetupP2.FistSocket): +Y down the blade, the edge on its X side.
        Quaternion socketWorld = Quaternion.LookRotation(Vector3.Cross(edge, blade), blade);
        Quaternion socketLocal = Quaternion.Inverse(hand.rotation) * socket.rotation;
        Quaternion handWant = socketWorld * Quaternion.Inverse(socketLocal);
        hand.rotation = Quaternion.Slerp(hand.rotation, handWant, weight);
    }
}
