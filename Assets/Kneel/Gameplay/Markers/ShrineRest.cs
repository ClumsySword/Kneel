using Kneel;
using Kneel.Core;
using TMPro;
using UnityEngine;

// A shrine Kael can rest at (every level's shrines): within reach, "E to Rest" over it; Interact kneels him for a
// moment, heals him, saves the shrine (SaveFlags "<level>.<id>", and SaveData.lastShrine), and puts the level's
// resettable things back. Only the Last Shrine (L5's Shrine C, The Master.md §11) also refills his potions: the
// others are checkpoints, and potions are found in the world. Once it's the last shrine rested at, a reload of the
// level (the death screen's Restart, Continue) puts Kael in front of it instead of at the level's start.
public class ShrineRest : MonoBehaviour
{
    // The flag saved when rested at ("L5.ShrineC").
    public string flag = "L5.ShrineC";
    public float reach = 2.6f;
    public float kneelTime = 2.2f;
    // Fill the belt on resting. Only the Last Shrine does.
    public bool refillPotions = false;
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
        // Arriving through the seam from the level before, he stays where the seam put him.
        if (player != null && IsRespawnShrine() && LevelTransition.IsArriving == false)
            PlaceAtShrine(player);
    }

    // Rested at, and the last shrine rested at (a level can have two). A save from before lastShrine existed only
    // has the flag. The dev tools' flag sandbox never writes lastShrine, so there the flag alone decides.
    private bool IsRespawnShrine()
    {
        if (SaveFlags.Has(flag) == false)
            return false;
        if (SaveFlags.Sandbox != null)
            return true;
        string last = SaveServices.Current.Load()?.lastShrine;
        return string.IsNullOrEmpty(last) || last == flag;
    }

    // Makes this the shrine a reload puts Kael at (the flag and lastShrine), without the rest itself. Rest calls it;
    // public for scripted tests.
    public void SaveAsRespawn()
    {
        SaveFlags.Set(flag);
        if (SaveFlags.Sandbox == null)
            SaveServices.Update(data => data.lastShrine = flag);
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
        Kneel.Audio.Sfx.Play(Kneel.Audio.SfxId.ShrineRest, transform.position + Vector3.up);
        Health health = player.GetComponent<Health>();
        if (health != null)
            health.SetMax(health.Max, true);
        var potions = player.GetComponent<PlayerPotions>();
        while (refillPotions && potions != null && potions.TryAdd())
        {
        }

        SaveAsRespawn();
        // The rest healed him: that (and the potions he has now) is what he comes back here with (the level's rule
        // over the state he carried into it).
        string scene = gameObject.scene.name;
        CarriedState rested = CarriedStates.Capture(player.gameObject, scene);
        if (SaveServices.Current.Load() != null)
        {
            SaveServices.Update(data =>
            {
                data.sceneName = scene;
                data.entry = rested;
            });
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
