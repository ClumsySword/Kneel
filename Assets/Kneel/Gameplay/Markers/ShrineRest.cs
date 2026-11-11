using Kneel;
using Kneel.Core;
using TMPro;
using UnityEngine;

// A shrine Kael can rest at (L5's Shrine C, the Last Shrine, The Master.md §11): within reach, "E to Rest" over it;
// Interact kneels him for a moment, heals him, refills his potions, saves the shrine (SaveFlags "<level>.<id>"), and
// puts the level's resettable things back. Once it's been rested at, a reload of the level (the death screen's
// Restart, Continue) puts Kael in front of it instead of at the level's start.
public class ShrineRest : MonoBehaviour
{
    // The flag saved when rested at ("L5.ShrineC").
    public string flag = "L5.ShrineC";
    public float reach = 2.6f;
    public float kneelTime = 2.2f;
    // Where Kael stands when he comes back here: this far from it along respawnFacing (world), facing that way.
    public float respawnOut = 2.2f;
    public Vector3 respawnFacing = Vector3.forward;
    public string labelFormat = "{0} to Rest";

    public bool Rested { get; private set; }

    // Raised when Kael rests here.
    public event System.Action OnRested;

    private PlayerCombat player;
    private PlayerControls controls;
    private GameObject canvasObject;
    private RectTransform canvasRect;
    private TextMeshProUGUI text;
    private float alpha;

    public Vector3 RespawnPoint => transform.position + Facing * respawnOut;

    private Vector3 Facing => Vector3.ProjectOnPlane(respawnFacing, Vector3.up).normalized;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Character.Interact.performed += _ => TryRest();
        BuildPrompt();
    }

    private void OnEnable() => controls.Character.Interact.Enable();

    private void OnDisable() => controls.Character.Interact.Disable();

    private void Start()
    {
        player = FindAnyObjectByType<PlayerCombat>();
        if (player != null && SaveFlags.Has(flag))
            PlaceAtShrine(player);
    }

    // Kael beside the shrine, facing on (into the hall he came back for).
    public void PlaceAtShrine(PlayerCombat combat)
    {
        var controller = combat.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        Vector3 at = RespawnPoint;
        if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 8f, LayerMask.GetMask("Default", "Ground", "Obstacles"), QueryTriggerInteraction.Ignore))
            at = ground.point;
        combat.transform.SetPositionAndRotation(at + Vector3.up * 0.05f, Quaternion.LookRotation(Facing));
        if (controller != null) controller.enabled = true;
    }

    public bool InReach => player != null && player.IsDead == false && Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).magnitude <= reach;

    private void TryRest()
    {
        if (InReach == false || player.IsKneeling || player.IsRestricted)
            return;
        Rest();
    }

    public void Rest()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerCombat>();

        // He kneels at it, as he will at the end.
        player.ForceKneel(kneelTime);
        Health health = player.GetComponent<Health>();
        if (health != null)
            health.SetMax(health.Max, true);
        var potions = player.GetComponent<PlayerPotions>();
        while (potions != null && potions.TryAdd())
        {
        }

        SaveFlags.Set(flag);
        SaveData data = SaveServices.Current.Load();
        if (data != null)
        {
            data.sceneName = gameObject.scene.name;
            data.savedAtUtc = System.DateTime.UtcNow.ToString("o");
            SaveServices.Current.Save(data);
        }

        FindAnyObjectByType<Kneel.Hazards.LevelReset>()?.ResetAll();
        Rested = true;
        OnRested?.Invoke();
    }

    private void BuildPrompt()
    {
        canvasObject = new GameObject("ShrinePrompt");
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
        bool show = InReach && player.IsKneeling == false && player.IsRestricted == false;
        if (show && alpha <= 0f)
            text.text = string.Format(labelFormat, KeybindService.KeyFor("Interact"));
        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
        Camera cam = Camera.main;
        Vector3 screen = cam != null ? cam.WorldToScreenPoint(transform.position + Vector3.up * 2.2f) : Vector3.zero;
        text.enabled = alpha > 0f && screen.z > 0f;
        if (text.enabled == false)
            return;
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
            Destroy(canvasObject);
    }
}
