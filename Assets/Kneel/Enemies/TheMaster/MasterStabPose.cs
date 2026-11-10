using UnityEngine;

// The Unmaking's first beat (The Master.md §5, 0.5 s): kneeling, the Master turns the King's sword on himself. No clip
// has it, so it's built on the kneel (decided 2026-11-10): a two-bone IK takes both hands from wherever they are to a
// grip in front of his sternum in 0.35 s, and the blade is turned point-first into his chest. Lodge() leaves the blade
// in him (parented to the chest) and lets the hands go for the thread pull.
public class MasterStabPose : MonoBehaviour
{
    private Animator anim;
    private Transform sword;
    private Transform tip;
    private Transform chest;
    private float weight;
    private float target;
    private float blendTime = 0.35f;

    // How far in front of the sternum the fists grip (metres, on his 4.2 m body); the rest of the blade goes through.
    public float gripOut = 0.75f;

    public bool Lodged { get; private set; }

    public void Init(Animator animator, Transform swordTip)
    {
        anim = animator;
        tip = swordTip;
        sword = swordTip.parent;
        chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
    }

    // Hands to the sternum, blade turned in, over `seconds`.
    public void Drive(float seconds)
    {
        blendTime = Mathf.Max(seconds, 0.05f);
        target = 1f;
    }

    // The blade stays in his chest; the hands are free again (the thread pull plays on its own clip).
    public void Lodge()
    {
        if (Lodged || sword == null)
            return;
        Lodged = true;
        sword.SetParent(chest, true);
        target = 0f;
        blendTime = 0.4f;
    }

    private Vector3 Sternum => chest.position + transform.forward * 0.25f + Vector3.up * 0.1f;

    private void LateUpdate()
    {
        if (anim == null || chest == null)
            return;

        weight = Mathf.MoveTowards(weight, target, Time.deltaTime / blendTime);
        if (weight <= 0f)
            return;

        float w = Mathf.SmoothStep(0f, 1f, weight);
        Vector3 grip = Sternum + transform.forward * gripOut;
        Transform rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        SolveArm(anim.GetBoneTransform(HumanBodyBones.RightUpperArm), anim.GetBoneTransform(HumanBodyBones.RightLowerArm), rightHand,
            Vector3.Lerp(rightHand.position, grip, w));
        // The left fist closes over the right, a hand's width above it.
        SolveArm(anim.GetBoneTransform(HumanBodyBones.LeftUpperArm), anim.GetBoneTransform(HumanBodyBones.LeftLowerArm), leftHand,
            Vector3.Lerp(leftHand.position, grip + Vector3.up * 0.18f + transform.right * -0.05f, w));

        if (Lodged)
            return;

        // Point first, into him: the blade from the grip back through the sternum.
        Vector3 from = sword.position;
        Vector3 into = (Sternum - from).normalized;
        Vector3 now = (tip.position - from).normalized;
        sword.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, into), w) * sword.rotation;
    }

    // Two-bone IK: bends the elbow so the hand reaches the target, keeping the arm in the plane it was already bent in
    // (the Fool's off-hand solver).
    private static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target)
    {
        Vector3 a = upper.position, b = lower.position, c = hand.position;
        float upperLength = (b - a).magnitude, lowerLength = (c - b).magnitude;
        float reach = Mathf.Clamp((target - a).magnitude, 0.001f, upperLength + lowerLength - 0.001f);

        float shoulderNow = Vector3.Angle(c - a, b - a);
        float elbowNow = Vector3.Angle(a - b, c - b);
        float toTarget = Vector3.Angle(c - a, target - a);
        float shoulder = Mathf.Acos(Mathf.Clamp((lowerLength * lowerLength - upperLength * upperLength - reach * reach) / (-2f * upperLength * reach), -1f, 1f)) * Mathf.Rad2Deg;
        float elbow = Mathf.Acos(Mathf.Clamp((reach * reach - upperLength * upperLength - lowerLength * lowerLength) / (-2f * upperLength * lowerLength), -1f, 1f)) * Mathf.Rad2Deg;

        Vector3 bendAxis = Vector3.Cross(c - a, b - a).normalized;
        Vector3 swingAxis = Vector3.Cross(c - a, target - a).normalized;
        Quaternion upperWorld = upper.rotation, lowerWorld = lower.rotation;

        upper.localRotation *= Quaternion.AngleAxis(shoulder - shoulderNow, Quaternion.Inverse(upperWorld) * bendAxis);
        lower.localRotation *= Quaternion.AngleAxis(elbow - elbowNow, Quaternion.Inverse(lowerWorld) * bendAxis);
        if (swingAxis.sqrMagnitude > 0.5f)
            upper.localRotation *= Quaternion.AngleAxis(toTarget, Quaternion.Inverse(upperWorld) * swingAxis);
    }
}
