using System;
using UnityEngine;

namespace Kneel.Hazards
{
    // The chained thrall on the north wall-walk tipping cauldrons into the trough that feeds E3's murder holes
    // (section 5, step 11). He never fights. One hit kills him and the pours stop until the next death or rest.
    public class PourTender : MonoBehaviour, IDamageable, ILevelResettable
    {
        [SerializeField]
        private MurderHole[] holes;

        // Shown alive, and slumped once killed.
        [SerializeField]
        private GameObject aliveVisual;

        [SerializeField]
        private GameObject deadVisual;

        public bool Dead { get; private set; }

        public event Action<PourTender> Killed;

        private void Awake()
        {
            ResetState();
        }

        public void TakeHit(DamageInfo info)
        {
            if (Dead || info.isEnvironmental)
            {
                return;
            }

            Dead = true;
            Apply();
            Killed?.Invoke(this);
        }

        public void ResetState()
        {
            Dead = false;
            Apply();
        }

        private void Apply()
        {
            if (holes != null)
            {
                foreach (var hole in holes)
                {
                    if (hole != null)
                    {
                        hole.SetSupplied(Dead == false);
                    }
                }
            }

            if (aliveVisual != null)
            {
                aliveVisual.SetActive(Dead == false);
            }

            if (deadVisual != null)
            {
                deadVisual.SetActive(Dead);
            }
        }
    }
}
