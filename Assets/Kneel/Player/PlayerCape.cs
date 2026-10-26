using UnityEngine;

// Cape cloth. The cape is skinned to a row of bone chains hanging from the upper back (Cape_<column>_<segment>).
// Each chain is simulated as a string of particles: the top one rides the back, and the rest swing under
// gravity on springs toward the cape's modelled drape, keep their segment lengths, stay tied to the
// neighbouring chains so the sheet holds together, and are pushed out of capsules that follow the body.
// The bones are then aimed along the particles.
// The drape follows the upper back at the shoulders but only the body's facing at the hem, so leaning and
// twisting the torso doesn't swing the whole cape around like a board.
// Humanoid clips don't animate these bones, so every frame starts from the modelled drape.
// Tuning values live in a PlayerCapeSettings asset.
public class PlayerCape : MonoBehaviour
{
    [SerializeField]
    private PlayerCapeSettings settings;

    // First bone of each chain, in order across the back.
    [SerializeField]
    private Transform[] chainRoots = new Transform[0];

    [Header("Collision")]
    [SerializeField]
    private PlayerLantern.BodyCapsule[] bodyCapsules = new PlayerLantern.BodyCapsule[0];

    // Sim step, so the cape behaves the same at any frame rate.
    private const float MaxStep = 1f / 120f;

    // Constraint passes per step.
    private const int Iterations = 4;

    // Neighbouring chains can't bunch closer than this fraction of their rest spacing.
    private const float MinSpacing = 0.5f;

    // Body turns faster than this (°/s) are snaps, like facing the roll direction, not real turns.
    // The cape is carried around with them instead of being flung.
    private const float SnapTurnRate = 1500f;

    private class Chain
    {
        public Transform[] bones;
        public Quaternion[] restRotations;
        // Where the hem end of the last bone sits, in that bone's space (the last bone has no child to aim at).
        public Vector3 tipLocal;
        public float[] lengths;
        // Each particle's drape offset from the top particle, in the body's space.
        public Vector3[] bodyOffsets;

        // One particle per bone head, plus the tip. Particle 0 is pinned to the back.
        public Vector3[] positions;
        public Vector3[] velocities;
        public Vector3[] targets;
        public Vector3[] targetsBefore;
        public Vector3[] stepStart;
        public bool[] touching;
    }

    private Transform body;
    private Vector3 lastBodyPosition;
    private Quaternion lastBodyRotation;
    private Chain[] chains;
    private float[] spacing;
    private bool simulating = false;

    // Capsule ends last frame and this frame, so each sim step can move them in between.
    private Vector3[] capsuleStartsBefore;
    private Vector3[] capsuleEndsBefore;
    private Vector3[] capsuleStarts;
    private Vector3[] capsuleEnds;

    public PlayerCapeSettings Settings => settings;

    private void Awake()
    {
        if (settings == null)
        {
            Debug.LogWarning("PlayerCape has no settings asset assigned, using defaults.", this);
            settings = ScriptableObject.CreateInstance<PlayerCapeSettings>();
        }

        var movement = GetComponentInParent<PlayerMovement>();
        body = movement != null ? movement.transform : transform.root;

        chains = new Chain[chainRoots.Length];
        for (int c = 0; c < chainRoots.Length; c++)
        {
            chains[c] = BuildChain(chainRoots[c]);
        }

        // Rest distance between neighbouring chains, per particle.
        int particles = chains.Length > 0 ? chains[0].positions.Length : 0;
        spacing = new float[particles];
        if (chains.Length > 1)
        {
            for (int k = 0; k < particles; k++)
            {
                spacing[k] = Vector3.Distance(chains[0].targets[k], chains[1].targets[k]);
            }
        }

        capsuleStartsBefore = new Vector3[bodyCapsules.Length];
        capsuleEndsBefore = new Vector3[bodyCapsules.Length];
        capsuleStarts = new Vector3[bodyCapsules.Length];
        capsuleEnds = new Vector3[bodyCapsules.Length];
    }

    private void OnEnable()
    {
        simulating = false;
    }

    private Chain BuildChain(Transform root)
    {
        var bones = new System.Collections.Generic.List<Transform>();
        for (Transform bone = root; bone != null; bone = bone.childCount > 0 ? bone.GetChild(0) : null)
        {
            bones.Add(bone);
        }

        var chain = new Chain();
        chain.bones = bones.ToArray();
        int count = chain.bones.Length;
        chain.restRotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            chain.restRotations[i] = chain.bones[i].localRotation;
        }

        // Continue the last segment's direction and length past the last bone.
        Transform last = chain.bones[count - 1];
        Vector3 previous = count > 1 ? chain.bones[count - 2].position : last.parent.position;
        chain.tipLocal = last.InverseTransformPoint(last.position + (last.position - previous));

        chain.positions = new Vector3[count + 1];
        chain.velocities = new Vector3[count + 1];
        chain.targets = new Vector3[count + 1];
        chain.targetsBefore = new Vector3[count + 1];
        chain.stepStart = new Vector3[count + 1];
        chain.touching = new bool[count + 1];
        chain.lengths = new float[count + 1];
        chain.bodyOffsets = new Vector3[count + 1];

        // The prefab holds the cape in its modelled drape, so this is the rest shape.
        ReadBackDrape(chain);
        for (int k = 1; k <= count; k++)
        {
            chain.lengths[k] = Vector3.Distance(chain.targets[k - 1], chain.targets[k]);
            chain.bodyOffsets[k] = body.InverseTransformVector(chain.targets[k] - chain.targets[0]);
        }

        return chain;
    }

    // Puts the chain back in its modelled drape on the upper back and records where its particles sit.
    private static void ReadBackDrape(Chain chain)
    {
        int count = chain.bones.Length;
        for (int i = 0; i < count; i++)
        {
            chain.bones[i].localRotation = chain.restRotations[i];
        }

        for (int i = 0; i < count; i++)
        {
            chain.targets[i] = chain.bones[i].position;
        }

        chain.targets[count] = chain.bones[count - 1].TransformPoint(chain.tipLocal);
    }

    // The drape to pull toward: riding the upper back at the shoulders, keeping the body's facing at the hem.
    private void ReadDrape(Chain chain)
    {
        ReadBackDrape(chain);
        int count = chain.targets.Length;
        for (int k = 1; k < count; k++)
        {
            Vector3 hanging = chain.targets[0] + body.TransformVector(chain.bodyOffsets[k]);
            chain.targets[k] = Vector3.Lerp(chain.targets[k], hanging, Depth(k, count));
        }
    }

    // 0 at the first free particle below the shoulders, 1 at the hem.
    private static float Depth(int k, int count)
    {
        return (float)(k - 1) / Mathf.Max(1, count - 2);
    }

    private void LateUpdate()
    {
        Simulate(Time.deltaTime);
    }

    private void Simulate(float deltaTime)
    {
        foreach (Chain chain in chains)
        {
            ReadDrape(chain);
        }

        for (int c = 0; c < bodyCapsules.Length; c++)
        {
            capsuleStarts[c] = bodyCapsules[c].start.position;
            capsuleEnds[c] = bodyCapsules[c].end.position;
        }

        // Start in the drape, and snap back after a teleport (respawn, scene placement).
        bool teleported = chains.Length > 0 && (chains[0].targets[0] - chains[0].targetsBefore[0]).sqrMagnitude > 4f;
        if (simulating == false || teleported)
        {
            foreach (Chain chain in chains)
            {
                System.Array.Copy(chain.targets, chain.positions, chain.targets.Length);
                System.Array.Copy(chain.targets, chain.targetsBefore, chain.targets.Length);
                System.Array.Clear(chain.velocities, 0, chain.velocities.Length);
            }

            System.Array.Copy(capsuleStarts, capsuleStartsBefore, capsuleStarts.Length);
            System.Array.Copy(capsuleEnds, capsuleEndsBefore, capsuleEnds.Length);
            lastBodyPosition = body.position;
            lastBodyRotation = body.rotation;
            simulating = true;
        }

        CarrySnapTurn(deltaTime);

        if (deltaTime > 0f)
        {
            deltaTime = Mathf.Min(deltaTime, 0.1f);
            int steps = Mathf.CeilToInt(deltaTime / MaxStep);
            float step = deltaTime / steps;
            Vector3 bodyVelocity = (body.position - lastBodyPosition) / deltaTime;

            for (int i = 1; i <= steps; i++)
            {
                Step(step, (float)i / steps, bodyVelocity);
            }
        }

        lastBodyPosition = body.position;
        lastBodyRotation = body.rotation;

        foreach (Chain chain in chains)
        {
            AimBones(chain);
            System.Array.Copy(chain.targets, chain.targetsBefore, chain.targets.Length);
        }

        System.Array.Copy(capsuleStarts, capsuleStartsBefore, capsuleStarts.Length);
        System.Array.Copy(capsuleEnds, capsuleEndsBefore, capsuleEnds.Length);
    }

    // If the body snapped round since last frame, turn the cloth (and the sim's memory of last frame) with it.
    private void CarrySnapTurn(float deltaTime)
    {
        Quaternion turn = body.rotation * Quaternion.Inverse(lastBodyRotation);
        turn.ToAngleAxis(out float angle, out _);
        if (angle > 180f)
        {
            angle = 360f - angle;
        }

        if (deltaTime <= 0f || angle / deltaTime < SnapTurnRate)
        {
            return;
        }

        Vector3 pivot = body.position;
        foreach (Chain chain in chains)
        {
            for (int k = 0; k < chain.positions.Length; k++)
            {
                chain.positions[k] = pivot + turn * (chain.positions[k] - pivot);
                chain.targetsBefore[k] = pivot + turn * (chain.targetsBefore[k] - pivot);
                chain.velocities[k] = turn * chain.velocities[k];
            }
        }

        for (int c = 0; c < bodyCapsules.Length; c++)
        {
            capsuleStartsBefore[c] = pivot + turn * (capsuleStartsBefore[c] - pivot);
            capsuleEndsBefore[c] = pivot + turn * (capsuleEndsBefore[c] - pivot);
        }
    }

    private void Step(float step, float t, Vector3 bodyVelocity)
    {
        Vector3 gravity = Physics.gravity * settings.gravityScale;
        float swayDamping = Mathf.Exp(-settings.swayDamping * step);
        float airDrag = Mathf.Exp(-settings.airDrag * step);
        float friction = Mathf.Exp(-settings.contactFriction * step);

        foreach (Chain chain in chains)
        {
            int count = chain.positions.Length;
            System.Array.Copy(chain.positions, chain.stepStart, count);
            chain.positions[0] = Vector3.Lerp(chain.targetsBefore[0], chain.targets[0], t);

            for (int k = 1; k < count; k++)
            {
                // Spring toward the drape: stiff at the shoulders, loose at the hem.
                Vector3 drape = Vector3.Lerp(chain.targetsBefore[k], chain.targets[k], t);
                float stiffness = Mathf.Lerp(settings.shoulderStiffness, settings.hemStiffness, Depth(k, count));
                Vector3 velocity = chain.velocities[k] + (gravity + (drape - chain.positions[k]) * stiffness) * step;

                // Damp the swing relative to the body's travel (more where it rubs on the body), and drag against the air.
                // The body's travel, not the back's: the back whips around during rolls and attacks.
                float damping = chain.touching[k] ? swayDamping * friction : swayDamping;
                velocity = bodyVelocity + (velocity - bodyVelocity) * damping;
                chain.velocities[k] = velocity * airDrag;
                chain.positions[k] += chain.velocities[k] * step;
                chain.touching[k] = false;
            }
        }

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            foreach (Chain chain in chains)
            {
                KeepLengths(chain);
            }

            TieNeighbours();
            PushOutOfBody(t);
        }

        // The velocity is whatever the constraints let the particles actually do this step.
        foreach (Chain chain in chains)
        {
            for (int k = 1; k < chain.positions.Length; k++)
            {
                chain.velocities[k] = (chain.positions[k] - chain.stepStart[k]) / step;
            }
        }
    }

    // Each segment keeps its length, moving the lower particle (the one nearer the hem).
    private static void KeepLengths(Chain chain)
    {
        for (int k = 1; k < chain.positions.Length; k++)
        {
            Vector3 offset = chain.positions[k] - chain.positions[k - 1];
            float distance = offset.magnitude;
            if (distance > 0.0001f)
            {
                chain.positions[k] = chain.positions[k - 1] + offset * (chain.lengths[k] / distance);
            }
        }
    }

    // Neighbouring chains stay within their rest spacing (plus slack) and don't bunch up, so the sheet holds together.
    private void TieNeighbours()
    {
        for (int c = 0; c < chains.Length - 1; c++)
        {
            Vector3[] a = chains[c].positions;
            Vector3[] b = chains[c + 1].positions;
            for (int k = 1; k < a.Length; k++)
            {
                Vector3 offset = b[k] - a[k];
                float distance = offset.magnitude;
                float limit = Mathf.Clamp(distance, spacing[k] * MinSpacing, spacing[k] * (1f + settings.spreadSlack));
                if (distance > 0.0001f && limit != distance)
                {
                    Vector3 correction = offset * ((distance - limit) / distance * 0.5f);
                    a[k] += correction;
                    b[k] -= correction;
                }
            }
        }
    }

    private void PushOutOfBody(float t)
    {
        for (int c = 0; c < bodyCapsules.Length; c++)
        {
            Vector3 start = Vector3.Lerp(capsuleStartsBefore[c], capsuleStarts[c], t);
            Vector3 end = Vector3.Lerp(capsuleEndsBefore[c], capsuleEnds[c], t);
            float reach = bodyCapsules[c].radius + settings.clothRadius;

            foreach (Chain chain in chains)
            {
                for (int k = 1; k < chain.positions.Length; k++)
                {
                    Vector3 point = chain.positions[k];
                    Vector3 closest = Vector3.Lerp(start, end, ClosestOnSegment(start, end, point));
                    Vector3 away = point - closest;
                    float distance = away.magnitude;
                    if (distance < reach && distance > 0.0001f)
                    {
                        chain.positions[k] = closest + away * (reach / distance);
                        chain.touching[k] = true;
                    }
                }
            }
        }
    }

    // Rotates each bone, top to bottom, so it points at the next particle.
    private static void AimBones(Chain chain)
    {
        int count = chain.bones.Length;
        for (int i = 0; i < count; i++)
        {
            Transform bone = chain.bones[i];
            Vector3 current = (i + 1 < count ? chain.bones[i + 1].position : bone.TransformPoint(chain.tipLocal)) - bone.position;
            Vector3 wanted = chain.positions[i + 1] - bone.position;
            if (current.sqrMagnitude > 0.000001f && wanted.sqrMagnitude > 0.000001f)
            {
                bone.rotation = Quaternion.FromToRotation(current, wanted) * bone.rotation;
            }
        }
    }

    private static float ClosestOnSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.000001f)
        {
            return 0f;
        }

        return Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
        foreach (PlayerLantern.BodyCapsule capsule in bodyCapsules)
        {
            if (capsule.start == null || capsule.end == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(capsule.start.position, capsule.radius);
            Gizmos.DrawWireSphere(capsule.end.position, capsule.radius);
            Gizmos.DrawLine(capsule.start.position, capsule.end.position);
        }

        if (chains == null)
        {
            return;
        }

        Gizmos.color = new Color(0.6f, 0.5f, 1f, 0.8f);
        foreach (Chain chain in chains)
        {
            for (int k = 1; k < chain.positions.Length; k++)
            {
                Gizmos.DrawLine(chain.positions[k - 1], chain.positions[k]);
                Gizmos.DrawWireSphere(chain.positions[k], settings != null ? settings.clothRadius : 0.04f);
            }
        }
    }
}
