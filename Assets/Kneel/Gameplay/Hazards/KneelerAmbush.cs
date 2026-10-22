using System;
using UnityEngine;

namespace Kneel.Hazards
{
    // A figure still kneeling upright among the dead who fell forward: a thrall waiting (decision 8). It wakes
    // when struck, when the player comes within 3 R, or when the player crosses its encounter's line (Trigger from
    // outside). Struck before it rises it takes a long stagger (StruckEarly); otherwise it rises in 1 s and
    // raises Woken for the enemy it stands in for. Partners (the Cursed Twins) wake together.
    public class KneelerAmbush : MonoBehaviour, IDamageable, ILevelResettable
    {
        public enum State
        {
            Kneeling,
            Rising,
            Awake,
        }

        [SerializeField]
        private StrongholdTuning tuning;

        [SerializeField]
        private KneelerAmbush[] partners;

        [SerializeField]
        private Transform spawnPoint;

        private float riseTime;

        public State Current { get; private set; }

        public bool WasStruckEarly { get; private set; }

        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

        public KneelerAmbush[] Partners
        {
            get => partners;
            set => partners = value;
        }

        public event Action<KneelerAmbush> StruckEarly;

        public event Action<KneelerAmbush> Woken;

        private void Awake()
        {
            ResetState();
        }

        public void TakeHit(DamageInfo info)
        {
            if (info.isEnvironmental || Current == State.Awake)
            {
                return;
            }

            if (Current == State.Kneeling)
            {
                WasStruckEarly = true;
                StruckEarly?.Invoke(this);
            }

            Trigger();
        }

        public void Trigger()
        {
            if (Current != State.Kneeling)
            {
                return;
            }

            Current = State.Rising;
            riseTime = 0f;
            if (partners != null)
            {
                foreach (var partner in partners)
                {
                    if (partner != null && partner != this)
                    {
                        partner.Trigger();
                    }
                }
            }
        }

        public void ResetState()
        {
            Current = State.Kneeling;
            WasStruckEarly = false;
            riseTime = 0f;
        }

        private void Update()
        {
            if (tuning == null)
            {
                return;
            }

            if (Current == State.Kneeling)
            {
                if (Physics.CheckSphere(transform.position, tuning.kneelerRadius, tuning.playerMask, QueryTriggerInteraction.Ignore))
                {
                    Trigger();
                }

                return;
            }

            if (Current == State.Rising)
            {
                riseTime += Time.deltaTime;
                if (riseTime >= tuning.kneelerRise)
                {
                    Current = State.Awake;
                    Woken?.Invoke(this);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, tuning != null ? tuning.kneelerRadius : 6f);
        }
    }
}
