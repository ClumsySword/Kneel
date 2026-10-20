using System.Collections.Generic;
using UnityEngine;

namespace Kneel
{
    // A passable patch of fire: a trigger volume that burns anything damageable standing in it, in heavy, slow
    // ticks (few ticks, each one felt). The damage is environmental, so it can't be parried or blocked and never
    // causes hit-stun. The visual bed is built to exactly this trigger's bounds.
    // Solid, impassable fire (route blockers) uses ordinary colliders instead of this component.
    [RequireComponent(typeof(BoxCollider))]
    public class FireHazard : MonoBehaviour
    {
        [SerializeField]
        private float damagePerTick = 10f;

        [SerializeField]
        private float tickInterval = 0.8f;

        // Grace before the first tick, so clipping the edge of the fire doesn't always cost health.
        [SerializeField]
        private float firstTickDelay = 0.25f;

        private readonly Dictionary<IDamageable, float> nextTick = new Dictionary<IDamageable, float>();
        private readonly List<IDamageable> expired = new List<IDamageable>();
        private BoxCollider area;

        private void Awake()
        {
            area = GetComponent<BoxCollider>();
            area.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null && !nextTick.ContainsKey(target))
            {
                nextTick[target] = Time.time + firstTickDelay;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null || !nextTick.TryGetValue(target, out float due) || Time.time < due)
            {
                return;
            }

            nextTick[target] = Time.time + tickInterval;
            Vector3 center = area.bounds.center;
            Vector3 away = other.transform.position - center;
            away.y = 0f;
            target.TakeHit(new DamageInfo
            {
                amount = damagePerTick,
                point = other.ClosestPoint(center),
                direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward,
                source = gameObject,
                isEnvironmental = true,
            });
        }

        private void OnTriggerExit(Collider other)
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                nextTick.Remove(target);
            }
        }

        private void OnDisable()
        {
            nextTick.Clear();
        }

        // Targets destroyed while inside never send OnTriggerExit.
        private void LateUpdate()
        {
            expired.Clear();
            foreach (var target in nextTick.Keys)
            {
                if (target as Object == null)
                {
                    expired.Add(target);
                }
            }

            foreach (var target in expired)
            {
                nextTick.Remove(target);
            }
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
