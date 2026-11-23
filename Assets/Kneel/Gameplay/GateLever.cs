using System.Collections;
using Kneel.Core;
using TMPro;
using UnityEngine;

namespace Kneel
{
    // A lever the player pulls with Interact (E) while standing next to it; it opens its gate (ShortcutGate).
    // Like every script here it reads its own PlayerControls.
    // With showPrompt on, "E to Pull" floats over it while the player is in reach (set on the first lever only, to
    // teach it; the rest are left bare). Names the key Interact is on now, so it follows a rebind.
    public class GateLever : MonoBehaviour
    {
        [SerializeField]
        private bool showPrompt = false;

        // {0} is the Interact key.
        [SerializeField]
        private string labelFormat = "{0} to Pull";

        // Height of the prompt above the lever's base, in metres.
        [SerializeField]
        private float promptHeight = 1.8f;

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
        private GameObject canvasObject;
        private RectTransform canvasRect;
        private TextMeshProUGUI text;
        private float alpha;

        public bool Pulled => pulled;

        private void Awake()
        {
            controls = new PlayerControls();
            controls.Character.Interact.performed += ctx => TryPull();
            if (handle != null)
            {
                handleRest = handle.localRotation;
            }

            if (showPrompt)
            {
                BuildPrompt();
            }
        }

        private void BuildPrompt()
        {
            canvasObject = new GameObject("LeverPrompt");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            canvasRect = (RectTransform)canvasObject.transform;
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(canvasObject.transform, false);
            text = label.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.color = new Color(0.92f, 0.88f, 0.8f, 0f);
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(10, 8, 6, 255);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(300f, 40f);
        }

        private void LateUpdate()
        {
            if (text == null)
            {
                return;
            }

            bool show = pulled == false && InReach;
            // Set as it appears, so a rebind made in the pause menu shows the next time.
            if (show && alpha <= 0f)
            {
                text.text = string.Format(labelFormat, KeybindService.KeyFor("Interact"));
            }

            alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
            Camera cam = Camera.main;
            Vector3 screen = cam != null ? cam.WorldToScreenPoint(transform.position + Vector3.up * promptHeight) : Vector3.zero;
            text.enabled = alpha > 0f && screen.z > 0f;
            if (text.enabled == false)
            {
                return;
            }

            float scale = canvasRect.lossyScale.x > 0f ? canvasRect.lossyScale.x : 1f;
            ((RectTransform)text.transform).anchoredPosition = new Vector2(screen.x, screen.y) / scale;
            Color c = text.color;
            c.a = alpha * 0.9f;
            text.color = c;
        }

        private void OnDestroy()
        {
            controls?.Dispose();
            if (canvasObject != null)
            {
                Destroy(canvasObject);
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
