using Kneel;
using Kneel.Core;
using TMPro;
using UnityEngine;

// "E to Pickup" above the potion under the crosshair, while there's room on the belt. The prompt only marks
// what can be picked up; walking up to it and pressing Interact (PlayerPotions) does the picking. Names the key
// Interact is on now, so it follows a rebind. Builds its own overlay canvas at runtime, like the damage vignette.
public class PickupPrompt : MonoBehaviour
{
    // {0} is the Interact key.
    [SerializeField]
    private string labelFormat = "{0} to Pickup";

    // How close to a potion the crosshair has to be, on the ground, to count as hovering it.
    [SerializeField]
    private float hoverRadius = 0.5f;

    // Height above the potion's base the prompt sits at, in metres.
    [SerializeField]
    private float height = 0.45f;

    [SerializeField]
    private float fontSize = 22f;

    // Seconds to fade in and out.
    [SerializeField]
    private float fadeTime = 0.12f;

    // Above the damage vignette, so the prompt stays readable when hurt.
    private const int SortingOrder = 110;

    private PlayerMovement movement;
    private PlayerPotions potions;
    private GameObject canvasObject;
    private RectTransform canvasRect;
    private TextMeshProUGUI text;
    private float alpha = 0f;
    private Vector3 lastPosition;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        potions = GetComponent<PlayerPotions>();
        Build();
    }

    private void Build()
    {
        canvasObject = new GameObject("PickupPrompt");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        canvasRect = (RectTransform)canvasObject.transform;

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);
        text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.color = new Color(0.92f, 0.88f, 0.8f, 0f);
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(10, 8, 6, 255);

        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.sizeDelta = new Vector2(300f, 40f);
    }

    private void LateUpdate()
    {
        HealthPotionPickup hovered = potions != null && potions.Count < potions.Capacity ? Hovered() : null;
        if (hovered != null)
        {
            // Set as it appears, so a rebind made in the pause menu shows the next time.
            if (alpha <= 0f)
            {
                text.text = string.Format(labelFormat, KeybindService.KeyFor("Interact"));
            }

            lastPosition = hovered.transform.position + Vector3.up * height;
        }

        alpha = Mathf.MoveTowards(alpha, hovered != null ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));

        Camera cam = Camera.main;
        Vector3 screen = cam != null ? cam.WorldToScreenPoint(lastPosition) : Vector3.zero;
        bool visible = alpha > 0f && screen.z > 0f;
        text.enabled = visible;
        if (visible == false)
        {
            return;
        }

        // Screen pixels to canvas units (the overlay canvas may be scaled).
        float scale = canvasRect.lossyScale.x > 0f ? canvasRect.lossyScale.x : 1f;
        ((RectTransform)text.transform).anchoredPosition = new Vector2(screen.x, screen.y) / scale;
        Color c = text.color;
        c.a = alpha * 0.9f;
        text.color = c;
    }

    // The potion nearest the crosshair's spot on the ground, within hoverRadius.
    private HealthPotionPickup Hovered()
    {
        Vector3 aim = movement.AimPoint;
        HealthPotionPickup best = null;
        float bestDistance = hoverRadius;

        foreach (HealthPotionPickup pickup in HealthPotionPickup.All)
        {
            float distance = pickup.DistanceTo(aim);
            if (distance <= bestDistance)
            {
                best = pickup;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
        {
            Destroy(canvasObject);
        }
    }
}
