using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Kneel.Hazards
{
    // The barred gate on the sluice footway: the shortcut back to the shrine. Its bar is on the +Z side, so
    // it only opens from there (Interact, E); from the -Z side it says so. Once open it stays open. While shut
    // it blocks the way and carves the NavMesh. Like every script here it reads its own PlayerControls.
    public class SluiceGate : MonoBehaviour
    {
        public const string OpenPrompt = "Lift the bar  [E]";
        public const string BarredPrompt = "Barred from the other side";

        [SerializeField]
        private Collider blocker;

        // The swinging leaf; its pivot is the hinge.
        [SerializeField]
        private Transform leaf;

        // The bar across the +Z face; lifted away when the gate opens.
        [SerializeField]
        private GameObject bar;

        // Saved disabled so it never carves in the editor; a shut gate carves at runtime.
        [SerializeField]
        private NavMeshObstacle obstacle;

        [SerializeField]
        private TMP_Text prompt;

        [SerializeField]
        private float reach = 2.5f;

        [SerializeField]
        private float openAngle = 100f;

        [SerializeField]
        private float openDuration = 1.2f;

        [SerializeField]
        private UnityEvent opened;

        private PlayerControls controls;
        private Transform player;
        private Quaternion closedRotation;

        public bool IsOpen { get; private set; }

        // Raised once, when the bar is lifted. The vista pan and the save listen to this.
        public event Action<SluiceGate> Opened;

        private void Awake()
        {
            controls = new PlayerControls();
            controls.Character.Interact.performed += ctx => TryOpen();
            if (leaf != null)
            {
                closedRotation = leaf.localRotation;
            }

            if (obstacle != null)
            {
                obstacle.enabled = true;
            }

            if (prompt != null)
            {
                prompt.gameObject.SetActive(false);
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

        // True if the point is on the side with the bar.
        public bool IsOnBarSide(Vector3 worldPoint)
        {
            return transform.InverseTransformPoint(worldPoint).z > 0f;
        }

        public bool InReach(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;
            return d.magnitude <= reach;
        }

        public void TryOpen()
        {
            if (player != null)
            {
                TryOpenFrom(player.position);
            }
        }

        // Public so scripted tests and the inspector buttons can try it from either side. True if it opened.
        public bool TryOpenFrom(Vector3 worldPoint)
        {
            if (IsOpen == true || InReach(worldPoint) == false || IsOnBarSide(worldPoint) == false)
            {
                return false;
            }

            IsOpen = true;
            Free();
            StartCoroutine(Swing());
            Opened?.Invoke(this);
            opened?.Invoke();
            return true;
        }

        // For the save: put the gate in a known state without the swing or the event.
        public void SetOpen(bool open)
        {
            StopAllCoroutines();
            IsOpen = open;
            if (open == true)
            {
                Free();
            }
            else
            {
                blocker.enabled = true;
                if (obstacle != null)
                {
                    obstacle.enabled = true;
                }

                if (bar != null)
                {
                    bar.SetActive(true);
                }
            }

            if (leaf != null)
            {
                leaf.localRotation = open ? closedRotation * Quaternion.Euler(0f, openAngle, 0f) : closedRotation;
            }
        }

        private void Free()
        {
            // The way through is free as soon as the gate starts to move.
            blocker.enabled = false;
            if (obstacle != null)
            {
                obstacle.enabled = false;
            }

            if (bar != null)
            {
                bar.SetActive(false);
            }
        }

        private IEnumerator Swing()
        {
            if (leaf == null)
            {
                yield break;
            }

            Quaternion open = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / openDuration)
            {
                // Heavy: slow to start, a little settle at the end.
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                leaf.localRotation = Quaternion.Slerp(closedRotation, open, eased);
                yield return null;
            }

            leaf.localRotation = open;
        }

        private void LateUpdate()
        {
            if (prompt == null)
            {
                return;
            }

            bool show = IsOpen == false && player != null && InReach(player.position);
            if (prompt.gameObject.activeSelf != show)
            {
                prompt.gameObject.SetActive(show);
            }

            if (show == false)
            {
                return;
            }

            prompt.text = IsOnBarSide(player.position) ? OpenPrompt : BarredPrompt;
            var cam = Camera.main;
            if (cam != null)
            {
                prompt.transform.rotation = cam.transform.rotation;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, reach);
            // The side it opens from.
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 1.5f);
        }
    }
}
