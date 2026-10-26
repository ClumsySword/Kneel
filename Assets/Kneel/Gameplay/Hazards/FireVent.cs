using System.Collections.Generic;
using UnityEngine;

namespace Kneel.Hazards
{
    // A fissure in the paving where the fire under the stronghold breaks through (refinement, 2026-10-19). It runs
    // on a fixed loop: quiet (a dull glow in the crack), the tell (the crack brightens, sparks and a hiss), the
    // burst (a column of flame that burns whoever stands on it), then quiet again. Each vent has its own offset
    // into the loop, so a row of them can ripple. Contact goes through HazardContact like every L4 hazard.
    [RequireComponent(typeof(BoxCollider))]
    public class FireVent : MonoBehaviour
    {
        [SerializeField]
        private HazardTuning contact;

        [SerializeField]
        private float quiet = 4f;

        [SerializeField]
        private float tell = 1.2f;

        [SerializeField]
        private float burst = 1.6f;

        [SerializeField]
        private float damageTick = 0.6f;

        // Seconds into the loop at time zero.
        [SerializeField]
        private float offset;

        [SerializeField]
        private GameObject tellVisual;

        [SerializeField]
        private GameObject burstVisual;

        [SerializeField]
        private Light glow;

        [SerializeField]
        private float glowQuiet = 0.6f;

        [SerializeField]
        private float glowBurst = 6f;

        private readonly HazardSensor sensor = new HazardSensor();
        private readonly Dictionary<Component, float> nextTick = new Dictionary<Component, float>();
        private BoxCollider area;
        private int phase = -1;

        public enum Phase { Quiet, Tell, Burst }

        public Phase Current => (Phase)Mathf.Max(phase, 0);

        private float Loop => quiet + tell + burst;

        private void Awake()
        {
            area = GetComponent<BoxCollider>();
            area.isTrigger = true;
        }

        private void Update()
        {
            float t = Mathf.Repeat(Time.time + offset, Loop);
            int next = t < quiet ? 0 : t < quiet + tell ? 1 : 2;
            if (next != phase)
            {
                phase = next;
                if (tellVisual != null)
                {
                    tellVisual.SetActive(phase == 1);
                }

                if (burstVisual != null)
                {
                    burstVisual.SetActive(phase == 2);
                }

                sensor.Clear();
                nextTick.Clear();
            }

            if (glow != null)
            {
                float target = phase == 2 ? glowBurst : phase == 1 ? Mathf.Lerp(glowQuiet, glowBurst * 0.5f, (t - quiet) / tell) : glowQuiet;
                glow.intensity = Mathf.MoveTowards(glow.intensity, target, Time.deltaTime * glowBurst * 4f);
            }
        }

        private void FixedUpdate()
        {
            if (phase != 2 || contact == null)
            {
                return;
            }

            sensor.Poll(area, contact.actorMask);
            foreach (var actor in sensor.Entered)
            {
                HazardContact.Report(actor, HazardKind.FireEnter, contact, gameObject);
                nextTick[actor] = Time.time + damageTick;
            }

            foreach (var actor in sensor.Exited)
            {
                nextTick.Remove(actor);
            }

            foreach (var actor in sensor.Inside)
            {
                if (nextTick.TryGetValue(actor, out float due) && Time.time >= due)
                {
                    HazardContact.Report(actor, HazardKind.FireTick, contact, gameObject);
                    nextTick[actor] = due + damageTick;
                }
            }
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(1f, 0.3f, 0.05f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
