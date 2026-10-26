using System.Collections.Generic;
using UnityEngine;

namespace Kneel
{
    // A healing potion lying in the level. The player takes it onto their belt with Interact (E) while standing
    // next to it (PlayerPotions picks the nearest one in reach). Picked-up potions are switched off, not destroyed.
    // A faint pulsing light under it helps it read in the dark levels.
    public class HealthPotionPickup : MonoBehaviour
    {
        // Every potion currently lying in the level.
        public static readonly List<HealthPotionPickup> All = new List<HealthPotionPickup>();

        [SerializeField]
        private float reach = 1.6f;

        [Header("Glow")]
        [SerializeField]
        private Light glow;

        [SerializeField]
        private float glowIntensity = 0.35f;

        // Fraction of the intensity the glow breathes by, and breaths per second.
        [SerializeField]
        private float glowPulse = 0.3f;

        [SerializeField]
        private float glowPulseSpeed = 0.4f;

        private float pulseOffset;

        public float Reach => reach;

        private void Awake()
        {
            // Out of step with each other, so a cluster doesn't breathe in unison.
            pulseOffset = Random.value * 10f;
        }

        private void Update()
        {
            if (glow != null)
            {
                float breath = Mathf.Sin((Time.time + pulseOffset) * glowPulseSpeed * Mathf.PI * 2f);
                glow.intensity = glowIntensity * (1f + glowPulse * breath);
            }
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        // Flat distance, so a potion on a step or a crate still counts.
        public float DistanceTo(Vector3 position)
        {
            Vector3 d = position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // Public so scripted tests can pick it up without input. False if the belt is full or it's out of reach.
        public bool TryPickUp(PlayerPotions potions)
        {
            if (potions == null || DistanceTo(potions.transform.position) > reach || potions.TryAdd() == false)
            {
                return false;
            }

            gameObject.SetActive(false);
            return true;
        }
    }
}
