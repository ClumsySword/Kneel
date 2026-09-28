using System;
using System.Collections.Generic;
using UnityEngine;

// Blade hit detection. While active, it checks a capsule along the blade each frame, plus in-between
// blade positions since last frame so fast swings can't skip past a target. Each target is hit once per swing.
public class WeaponHitbox : MonoBehaviour
{
    [SerializeField]
    private Transform bladeBase;

    [SerializeField]
    private Transform bladeTip;

    [SerializeField]
    private float radius = 0.12f;

    [SerializeField]
    private LayerMask hitMask;

    // Extra samples between last and current blade position.
    [SerializeField]
    private int sweepSteps = 4;

    private readonly Collider[] overlapBuffer = new Collider[16];
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

    private bool active = false;
    private Vector3 lastBase;
    private Vector3 lastTip;

    // Fired once per target per swing with the target, its collider and the contact point.
    public event Action<IDamageable, Collider, Vector3> OnHit;

    public bool IsActive => active;

    public void Configure(Transform baseTransform, Transform tipTransform, LayerMask mask)
    {
        bladeBase = baseTransform;
        bladeTip = tipTransform;
        hitMask = mask;
    }

    public void Begin()
    {
        active = true;
        hitThisSwing.Clear();
        lastBase = bladeBase.position;
        lastTip = bladeTip.position;
        Sweep();
    }

    public void End()
    {
        active = false;
    }

    // After animation has posed the bones this frame.
    private void LateUpdate()
    {
        if (active == true)
        {
            Sweep();
        }
    }

    private void Sweep()
    {
        Vector3 currentBase = bladeBase.position;
        Vector3 currentTip = bladeTip.position;

        for (int step = 1; step <= sweepSteps; step++)
        {
            float t = (float)step / sweepSteps;
            CheckCapsule(Vector3.Lerp(lastBase, currentBase, t), Vector3.Lerp(lastTip, currentTip, t));
        }

        lastBase = currentBase;
        lastTip = currentTip;
    }

    private void CheckCapsule(Vector3 start, Vector3 end)
    {
        int count = Physics.OverlapCapsuleNonAlloc(start, end, radius, overlapBuffer, hitMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = overlapBuffer[i];
            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();

            if (target == null || hitThisSwing.Contains(target))
            {
                continue;
            }

            hitThisSwing.Add(target);
            Vector3 point = hitCollider.ClosestPoint((start + end) * 0.5f);
            OnHit?.Invoke(target, hitCollider, point);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (bladeBase == null || bladeTip == null)
        {
            return;
        }

        Gizmos.color = active ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(bladeBase.position, radius);
        Gizmos.DrawWireSphere(bladeTip.position, radius);
        Gizmos.DrawLine(bladeBase.position, bladeTip.position);
    }
}
