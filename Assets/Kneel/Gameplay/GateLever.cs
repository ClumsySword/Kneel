using System.Collections;
using UnityEngine;

namespace Kneel
{
    // A lever the player pulls with Interact (E) while standing next to it; it opens its gate (ShortcutGate).
    // Like every script here it reads its own PlayerControls.
    public class GateLever : MonoBehaviour
    {
        [SerializeField]
        private ShortcutGate gate;

        // The handle that tips over when pulled; its pivot is the lever's fulcrum.
        [SerializeField]
        private Transform handle;

        [SerializeField]
        private float reach = 1.8f;

        [SerializeField]
        private float pullAngle = 70f;

        [SerializeField]
        private float pullDuration = 0.45f;

        private PlayerControls controls;
        private Transform player;
        private bool pulled;
        private Quaternion handleRest;

        public bool Pulled => pulled;

        private void Awake()
        {
            controls = new PlayerControls();
            controls.Character.Interact.performed += ctx => TryPull();
            if (handle != null)
            {
                handleRest = handle.localRotation;
            }
        }

        private void Start()
        {
            var movement = FindAnyObjectByType<PlayerMovement>();
            player = movement != null ? movement.transform : null;
        }

        private void OnEnable()
        {
            controls.Character.Interact.Enable();
        }

        private void OnDisable()
        {
            controls.Character.Interact.Disable();
        }

        public bool InReach
        {
            get
            {
                if (player == null)
                {
                    return false;
                }

                Vector3 d = player.position - transform.position;
                d.y = 0f;
                return d.magnitude <= reach;
            }
        }

        // Public so scripted tests can pull it without input.
        public void TryPull()
        {
            if (pulled || !InReach)
            {
                return;
            }

            pulled = true;
            StartCoroutine(Pull());
        }

        // Back to unpulled, with its gate shut (scripted tests reuse the gate after a walk has opened it).
        public void ResetLever()
        {
            StopAllCoroutines();
            pulled = false;
            if (handle != null)
            {
                handle.localRotation = handleRest;
            }

            if (gate != null)
            {
                gate.Close();
            }
        }

        private IEnumerator Pull()
        {
            if (handle != null)
            {
                Quaternion start = handle.localRotation;
                Quaternion end = start * Quaternion.Euler(pullAngle, 0f, 0f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / pullDuration)
                {
                    handle.localRotation = Quaternion.Slerp(start, end, t * t);
                    yield return null;
                }

                handle.localRotation = end;
            }

            if (gate != null)
            {
                gate.Open();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
