using Kneel.Core;
using TMPro;
using UnityEngine;

namespace Kneel.Hazards
{
    // A key lying in the level (the tribunal key, on the courtyard Husk's belt in L4): within reach, "E to Take" over
    // it; Interact puts it on the KeyRing, where a OneTimeGate with the same key id asks for it. A faint glow helps it
    // read in the smoke. Taken once per run: a level reload (death, Continue) shows it again only if the ring doesn't
    // already hold it.
    public class KeyPickup : MonoBehaviour
    {
        [SerializeField]
        private string keyId = "TribunalKey";

        // Flat distance from the player to the key.
        [SerializeField]
        private float reach = 1.8f;

        [SerializeField]
        private string labelFormat = "{0} to Take";

        [Header("Glow")]
        [SerializeField]
        private Light glow;

        [SerializeField]
        private float glowIntensity = 0.6f;

        [SerializeField]
        private float glowPulse = 0.35f;

        [SerializeField]
        private float glowPulseSpeed = 0.5f;

        private PlayerControls controls;
        private PlayerCombat player;
        private GameObject canvasObject;
        private RectTransform canvasRect;
        private TextMeshProUGUI text;
        private float alpha;

        public string KeyId => keyId;

        private void Awake()
        {
            controls = new PlayerControls();
            controls.Character.Interact.performed += _ => TryTake();
            BuildPrompt();
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerCombat>();
            if (KeyRing.Has(keyId))
            {
                gameObject.SetActive(false);
            }
        }

        private void OnEnable() => controls.Character.Interact.Enable();

        private void OnDisable()
        {
            controls.Character.Interact.Disable();
            if (text != null)
            {
                text.enabled = false;
            }
        }

        public bool InReach
        {
            get
            {
                if (player == null || player.IsDead)
                {
                    return false;
                }

                Vector3 d = player.transform.position - transform.position;
                d.y = 0f;
                return d.magnitude <= reach;
            }
        }

        // Public so scripted tests can take it without input. True if it was taken.
        public bool TryTake()
        {
            if (isActiveAndEnabled == false || InReach == false || player.IsRestricted)
            {
                return false;
            }

            KeyRing.Give(keyId);
            Kneel.Audio.Sfx.PlayFlat(Kneel.Audio.SfxId.Pickup);
            gameObject.SetActive(false);
            return true;
        }

        private void Update()
        {
            if (glow != null)
            {
                float breath = Mathf.Sin(Time.time * glowPulseSpeed * Mathf.PI * 2f);
                glow.intensity = glowIntensity * (1f + glowPulse * breath);
            }
        }

        private void BuildPrompt()
        {
            canvasObject = new GameObject("KeyPrompt");
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
            text.enabled = false;
        }

        private void LateUpdate()
        {
            bool show = InReach && player.IsRestricted == false;
            // Set as it appears, so a rebind made in the pause menu shows the next time.
            if (show && alpha <= 0f)
            {
                text.text = string.Format(labelFormat, KeybindService.KeyFor("Interact"));
            }

            alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
            Camera cam = Camera.main;
            Vector3 screen = cam != null ? cam.WorldToScreenPoint(transform.position + Vector3.up * 0.6f) : Vector3.zero;
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
    }
}
