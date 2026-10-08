using System.Collections.Generic;
using UnityEngine;

namespace Kneel.Hazards
{
    // Polls a volume for actors and reports who entered and who left since the last poll. Polling (rather than
    // trigger messages) works for actors without a Rigidbody and for volumes that switch on and off.
    // An actor is the IHazardTarget above a collider, or failing that its IDamageable.
    public class HazardSensor
    {
        private readonly Collider[] buffer = new Collider[32];
        private HashSet<Component> inside = new HashSet<Component>();
        private HashSet<Component> previous = new HashSet<Component>();

        public readonly List<Component> Entered = new List<Component>();
        public readonly List<Component> Exited = new List<Component>();

        public HashSet<Component> Inside => inside;

        public void Poll(BoxCollider box, LayerMask mask)
        {
            Transform t = box.transform;
            Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(t.TransformPoint(box.center), half, buffer, t.rotation, mask, QueryTriggerInteraction.Collide);
            Collect(count);
        }

        public void PollSphere(Vector3 centre, float radius, LayerMask mask)
        {
            int count = Physics.OverlapSphereNonAlloc(centre, radius, buffer, mask, QueryTriggerInteraction.Collide);
            Collect(count);
        }

        // Forget everyone, so the next poll reports all occupants as entered.
        public void Clear()
        {
            inside.Clear();
            previous.Clear();
            Entered.Clear();
            Exited.Clear();
        }

        public static Component ActorOf(Collider collider)
        {
            var target = collider.GetComponentInParent<IHazardTarget>();
            if (target != null)
            {
                return target as Component;
            }

            return collider.GetComponentInParent<IDamageable>() as Component;
        }

        private void Collect(int count)
        {
            (inside, previous) = (previous, inside);
            inside.Clear();
            Entered.Clear();
            Exited.Clear();

            for (int i = 0; i < count; i++)
            {
                Component actor = ActorOf(buffer[i]);
                if (actor != null && inside.Add(actor) && !previous.Contains(actor))
                {
                    Entered.Add(actor);
                }
            }

            // Destroyed actors show up here too (as null); callers skip them.
            foreach (var actor in previous)
            {
                if (!inside.Contains(actor))
                {
                    Exited.Add(actor);
                }
            }
        }
    }
}
