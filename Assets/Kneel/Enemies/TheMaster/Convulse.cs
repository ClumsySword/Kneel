using UnityEngine;

// Jolts through a body from inside (the Unmaking, owner 2026-11-15: "like something inside of him is crackling"):
// each Kick throws the spine, chest, neck and head off their animated pose by a few degrees about a random axis, and
// they spring back over a fraction of a second. Applied after the Animator, on top of whatever clip is playing.
[DefaultExecutionOrder(90)]
public class Convulse : MonoBehaviour
{
    private Transform[] bones;
    private float[] share;
    private Vector3[] offset;
    private Vector3[] velocity;

    // Spring back: stiff and barely damped, so each jolt shudders once or twice before it settles.
    private const float Stiffness = 260f;
    private const float Damping = 14f;

    public static Convulse On(Animator anim)
    {
        Convulse c = anim.GetComponent<Convulse>() ?? anim.gameObject.AddComponent<Convulse>();
        HumanBodyBones[] chain = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder };
        float[] weights = { 0.5f, 0.8f, 1f, 0.6f, 0.7f, 0.9f, 0.9f };
        var bones = new System.Collections.Generic.List<Transform>();
        var shares = new System.Collections.Generic.List<float>();
        for (int i = 0; i < chain.Length; i++)
        {
            Transform bone = anim.GetBoneTransform(chain[i]);
            if (bone == null)
                continue;
            bones.Add(bone);
            shares.Add(weights[i]);
        }

        c.bones = bones.ToArray();
        c.share = shares.ToArray();
        c.offset = new Vector3[c.bones.Length];
        c.velocity = new Vector3[c.bones.Length];
        return c;
    }

    // A jolt of about `degrees` (a little different in each bone).
    public void Kick(float degrees)
    {
        Vector3 axis = Random.onUnitSphere;
        for (int i = 0; i < bones.Length; i++)
        {
            Vector3 jitter = Random.insideUnitSphere * 0.35f;
            velocity[i] += (axis + jitter).normalized * degrees * share[i] * 22f;
        }
    }

    public void Stop()
    {
        for (int i = 0; i < bones.Length; i++)
        {
            offset[i] = Vector3.zero;
            velocity[i] = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (bones == null)
            return;
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        for (int i = 0; i < bones.Length; i++)
        {
            velocity[i] += (-Stiffness * offset[i] - Damping * velocity[i]) * dt;
            offset[i] += velocity[i] * dt;
            if (offset[i].sqrMagnitude < 1e-4f && velocity[i].sqrMagnitude < 1e-3f)
                continue;
            bones[i].rotation = Quaternion.Euler(offset[i]) * bones[i].rotation;
        }
    }
}
