using System;
using UnityEngine;
using UnityEngine.Events;

namespace Kneel.Hazards
{
    // A scarecrow gibbet with something alive on it. The crows are the tell: they leave when the player comes
    // close, and a moment later the thrall tears free (Woken, at the spawn point). Hitting the gibbet before
    // that raises StruckEarly, which the enemy side turns into a long stagger. No enemy logic lives here.
    public class GibbetAmbush : MonoBehaviour, IDamageable, ILevelResettable
    {
        public enum State
        {
            Dormant,
            Waking,
            Woken,
        }

        [SerializeField]
        private GibbetTuning tuning;

        [SerializeField]
        private Transform[] crows;

        // Where the thrall lands when it tears free.
        [SerializeField]
        private Transform spawnPoint;

        [SerializeField]
        private UnityEvent woken;

        [SerializeField]
        private UnityEvent struckEarly;

        private readonly Collider[] buffer = new Collider[4];
        private Vector3[] crowRest;
        private Vector3[] crowFlight;
        private float wakeTime;

        public State Current { get; private set; }

        public bool WasStruckEarly { get; private set; }

        public Transform SpawnPoint => spawnPoint;

        public event Action<GibbetAmbush> Woken;
        public event Action<GibbetAmbush> StruckEarly;

        private void Awake()
        {
            crowRest = new Vector3[crows.Length];
            crowFlight = new Vector3[crows.Length];
            for (int i = 0; i < crows.Length; i++)
            {
                crowRest[i] = crows[i].localPosition;
                // Up and away, each its own way.
                float angle = (i * 137f + 40f) * Mathf.Deg2Rad;
                crowFlight[i] = new Vector3(Mathf.Cos(angle) * 2.5f, 3.5f, Mathf.Sin(angle) * 2.5f);
            }
        }

        // A hit while the crows are still perched (or still leaving) catches the thrall on the post.
        public void TakeHit(DamageInfo info)
        {
            if (info.isEnvironmental == true || Current == State.Woken || WasStruckEarly == true)
            {
                return;
            }

            WasStruckEarly = true;
            StruckEarly?.Invoke(this);
            struckEarly?.Invoke();
            Trigger();
        }

        // The crows leave; the thrall follows after the wake delay.
        public void Trigger()
        {
            if (Current != State.Dormant)
            {
                return;
            }

            Current = State.Waking;
            wakeTime = 0f;
        }

        public void ResetState()
        {
            Current = State.Dormant;
            WasStruckEarly = false;
            wakeTime = 0f;
            for (int i = 0; i < crows.Length; i++)
            {
                crows[i].gameObject.SetActive(true);
                crows[i].localPosition = crowRest[i];
            }
        }

        private void Update()
        {
            if (Current == State.Dormant)
            {
                if (Physics.OverlapSphereNonAlloc(transform.position, tuning.triggerRadius, buffer, tuning.playerMask, QueryTriggerInteraction.Ignore) > 0)
                {
                    Trigger();
                }

                return;
            }

            if (Current != State.Waking)
            {
                return;
            }

            wakeTime += Time.deltaTime;
            float t = Mathf.Clamp01(wakeTime / Mathf.Max(0.01f, tuning.wakeDelay));
            for (int i = 0; i < crows.Length; i++)
            {
                crows[i].localPosition = crowRest[i] + crowFlight[i] * (t * t);
            }

            if (wakeTime >= tuning.wakeDelay)
            {
                Current = State.Woken;
                foreach (var crow in crows)
                {
                    crow.gameObject.SetActive(false);
                }

                Woken?.Invoke(this);
                woken?.Invoke();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (tuning != null)
            {
                Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(transform.position, tuning.triggerRadius);
            }

            if (spawnPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(spawnPoint.position + Vector3.up, 0.5f);
            }
        }
    }
}
