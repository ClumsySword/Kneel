using UnityEngine;

// F1 toggles an in-game panel for tuning the camera, movement and combat while playing.
// Sliders write straight into the settings assets, so values found in Play mode are kept after stopping.
// Target dummy values are live-only (they live on the scene object, not an asset).
public class DebugTuningOverlay : MonoBehaviour
{
    private PlayerControls controls;

    [SerializeField]
    private PlayerCamera playerCamera;

    [SerializeField]
    private PlayerMovement playerMovement;

    [SerializeField]
    private PlayerCombat playerCombat;

    [SerializeField]
    private TargetDummy targetDummy;

    [SerializeField]
    private bool startOpen = false;

    private bool isOpen;
    private Vector2 scroll;

    private bool showCamera = false;
    private bool showMovement = false;
    private bool showCombat = true;
    private bool showDummy = true;

    private const float PanelWidth = 340f;

    // True while the pointer is over the open panel, so clicks there don't also swing the sword.
    public static bool IsPointerOverPanel { get; private set; }

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Debug.ToggleOverlay.performed += ctx => SetOpen(isOpen == false);
    }

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = FindAnyObjectByType<PlayerCamera>();
        }

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

        if (playerCombat == null)
        {
            playerCombat = FindAnyObjectByType<PlayerCombat>();
        }

        if (targetDummy == null)
        {
            targetDummy = FindAnyObjectByType<TargetDummy>();
        }

        SetOpen(startOpen);
    }

    private void Update()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (isOpen == false || mouse == null)
        {
            IsPointerOverPanel = false;
            return;
        }

        // IMGUI rects are measured from the top of the screen, pointer positions from the bottom.
        Vector2 pointer = mouse.position.ReadValue();
        IsPointerOverPanel = PanelRect().Contains(new Vector2(pointer.x, Screen.height - pointer.y));
    }

    private static Rect PanelRect()
    {
        return new Rect(10f, 10f, PanelWidth, Screen.height - 20f);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;

        if (playerCamera != null)
        {
            playerCamera.edgeLookEnabled = open == false;
        }
    }

    private void OnGUI()
    {
        if (isOpen == false)
        {
            GUI.Label(new Rect(10f, 10f, 200f, 22f), "F1: tuning");
            return;
        }

        GUILayout.BeginArea(PanelRect(), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);

        if (playerCamera != null && playerCamera.Settings != null && Foldout("Camera", ref showCamera))
        {
            DrawCameraSection(playerCamera.Settings);
        }

        if (playerMovement != null && playerMovement.Settings != null && Foldout("Movement", ref showMovement))
        {
            DrawMovementSection(playerMovement.Settings);
        }

        if (playerCombat != null && playerCombat.Settings != null && Foldout("Combat", ref showCombat))
        {
            DrawCombatSection(playerCombat.Settings);
        }

        if (targetDummy != null && Foldout("Target Dummy", ref showDummy))
        {
            DrawDummySection();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawCameraSection(PlayerCameraSettings s)
    {
        playerCamera.CurrentDistance = Slider("Current zoom", playerCamera.CurrentDistance, s.minDistance, s.maxDistance);

        EditorDirtyCheck(s, () =>
        {
            s.minDistance = Slider("Min distance", s.minDistance, 1f, 40f);
            s.maxDistance = Mathf.Max(Slider("Max distance", s.maxDistance, 1f, 60f), s.minDistance);
            s.startDistance = Mathf.Clamp(Slider("Start distance", s.startDistance, 1f, 60f), s.minDistance, s.maxDistance);
            s.zoomStep = Slider("Zoom step", s.zoomStep, 0.25f, 6f);
            s.pitch = Slider("Pitch", s.pitch, 30f, 90f);
            s.maxEdgeOffset = Slider("Edge pan distance", s.maxEdgeOffset, 0f, 25f);
            s.edgeSize = Slider("Edge band size", s.edgeSize, 0.01f, 0.5f);
            s.followSmoothTime = Slider("Follow smoothing", s.followSmoothTime, 0f, 0.5f);
        });

        if (GUILayout.Button("Reset camera to defaults"))
        {
            ResetToDefaults(s);
        }

        GUILayout.Space(10f);
    }

    private void DrawMovementSection(PlayerMovementSettings s)
    {
        string state = playerMovement.IsExhausted ? "EXHAUSTED" : playerMovement.IsSprinting ? "sprinting" : "walking";
        GUILayout.Label($"Speed {playerMovement.CurrentSpeed:F2} m/s  ({state})");
        DrawBar(playerMovement.Stamina01, playerMovement.IsExhausted ? Color.red : Color.green);

        EditorDirtyCheck(s, () =>
        {
            s.walkSpeedForward = Slider("Walk forward", s.walkSpeedForward, 0.5f, 3f);
            s.walkSpeedForwardDiagonal = Slider("Walk fwd diagonal", s.walkSpeedForwardDiagonal, 0.5f, 3f);
            s.walkSpeedStrafe = Slider("Walk strafe", s.walkSpeedStrafe, 0.5f, 3f);
            s.walkSpeedBackwardDiagonal = Slider("Walk back diagonal", s.walkSpeedBackwardDiagonal, 0.5f, 3f);
            s.walkSpeedBackward = Slider("Walk backward", s.walkSpeedBackward, 0.5f, 3f);
            s.acceleration = Slider("Acceleration", s.acceleration, 1f, 40f);
            s.deceleration = Slider("Deceleration", s.deceleration, 1f, 40f);
            s.aimTurnRate = Slider("Aim turn rate", s.aimTurnRate, 90f, 1440f);

            s.sprintSpeed = Slider("Sprint speed", s.sprintSpeed, 1f, 8f);
            s.sprintAcceleration = Slider("Sprint acceleration", s.sprintAcceleration, 1f, 20f);
            s.sprintTurnRate = Slider("Sprint turn rate", s.sprintTurnRate, 60f, 1080f);

            s.gravity = Slider("Gravity", s.gravity, 5f, 40f);
            s.jumpHeight = Slider("Jump height", s.jumpHeight, 0.2f, 2.5f);
            s.airControl = Slider("Air control", s.airControl, 0f, 1f);

            s.maxStamina = Slider("Max stamina", s.maxStamina, 10f, 300f);
            s.sprintStaminaDrain = Slider("Sprint drain /s", s.sprintStaminaDrain, 0f, 100f);
            s.staminaRegen = Slider("Regen /s", s.staminaRegen, 0f, 100f);
            s.staminaRegenDelay = Slider("Regen delay", s.staminaRegenDelay, 0f, 3f);
            s.exhaustedRecoverFraction = Slider("Exhausted until", s.exhaustedRecoverFraction, 0f, 1f);
        });

        if (GUILayout.Button("Reset movement to defaults"))
        {
            ResetToDefaults(s);
        }

        GUILayout.Space(10f);
    }

    private void DrawCombatSection(PlayerCombatSettings s)
    {
        Health health = playerCombat.Health;
        string guard = playerCombat.IsInvulnerable ? "I-FRAMES" : playerCombat.IsParryWindowOpen ? "PARRY WINDOW" : playerCombat.IsBlocking ? "blocking" : "";
        GUILayout.Label($"HP {health.Current:0}/{health.Max:0}   {playerCombat.State} {guard}");
        DrawBar(health.Fraction, new Color(0.8f, 0.15f, 0.1f));

        if (playerMovement != null)
        {
            DrawBar(playerMovement.Stamina01, playerMovement.IsExhausted ? Color.red : Color.green);
        }

        EditorDirtyCheck(s, () =>
        {
            for (int i = 0; i < s.attacks.Length; i++)
            {
                s.attacks[i].damage = Slider($"Hit {i + 1} damage", s.attacks[i].damage, 0f, 100f);
                s.attacks[i].staminaCost = Slider($"Hit {i + 1} stamina", s.attacks[i].staminaCost, 0f, 60f);
            }

            s.criticalMultiplier = Slider("Crit multiplier", s.criticalMultiplier, 1f, 4f);
            s.attackSpeed = Slider("Attack speed", s.attackSpeed, 0.5f, 2f);
            s.attackLunge = Slider("Attack lunge", s.attackLunge, 0f, 2f);
            s.comboBufferTime = Slider("Combo buffer", s.comboBufferTime, 0f, 1f);
            s.attackTurnWindow = Slider("Swing turn window", s.attackTurnWindow, 0f, 0.6f);
            s.drawSpeed = Slider("Draw speed", s.drawSpeed, 0.5f, 2.5f);

            s.dodgeDistance = Slider("Roll distance", s.dodgeDistance, 1f, 6f);
            s.dodgeDuration = Slider("Roll duration", s.dodgeDuration, 0.4f, 1.6f);
            s.dodgeStaminaCost = Slider("Roll stamina", s.dodgeStaminaCost, 0f, 60f);
            s.dodgeInvulnerableStart = Slider("Roll i-frames from", s.dodgeInvulnerableStart, 0f, 1f);
            s.dodgeInvulnerableEnd = Slider("Roll i-frames to", s.dodgeInvulnerableEnd, 0f, 1.6f);
            s.dodgeBufferTime = Slider("Roll buffer", s.dodgeBufferTime, 0f, 1f);
            s.dodgeThroughEnemies = GUILayout.Toggle(s.dodgeThroughEnemies, " Roll through enemies");

            s.blockMoveMultiplier = Slider("Block move speed", s.blockMoveMultiplier, 0f, 1f);
            s.blockDamageReduction = Slider("Block reduction", s.blockDamageReduction, 0f, 1f);
            s.blockStaminaPerDamage = Slider("Block stamina/dmg", s.blockStaminaPerDamage, 0f, 5f);
            s.parryWindow = Slider("Parry window", s.parryWindow, 0f, 0.6f);
            s.parryCooldown = Slider("Parry cooldown", s.parryCooldown, 0f, 1.5f);

            s.hitStop = Slider("Hit-stop", s.hitStop, 0f, 0.3f);
            s.parryHitStop = Slider("Parry hit-stop", s.parryHitStop, 0f, 0.5f);
            s.hitShake = Slider("Hit shake", s.hitShake, 0f, 0.5f);
            s.hitStunDuration = Slider("Hit stun", s.hitStunDuration, 0f, 1.5f);
        });

        if (GUILayout.Button("Reset combat to defaults"))
        {
            ResetToDefaults(s);
        }

        GUILayout.Space(10f);
    }

    private void DrawDummySection()
    {
        TargetDummy d = targetDummy;
        GUILayout.Label($"Hits {d.HitCount}   Total damage {d.TotalDamage:0}   {(d.IsStaggered ? "STAGGERED" : "")}");

        d.attackEnabled = GUILayout.Toggle(d.attackEnabled, " Dummy attacks");
        d.attackDamage = Slider("Kick damage", d.attackDamage, 0f, 60f);
        d.attackInterval = Slider("Kick interval", d.attackInterval, 0.5f, 6f);
        d.attackWindup = Slider("Kick windup", d.attackWindup, 0.1f, 2f);
        d.attackRange = Slider("Kick range", d.attackRange, 1f, 4f);
        d.staggerDuration = Slider("Stagger time", d.staggerDuration, 0.2f, 3f);
    }

    private static bool Foldout(string title, ref bool open)
    {
        if (GUILayout.Button((open ? "▼ " : "► ") + title, RichLabel()))
        {
            open = open == false;
        }

        return open;
    }

    private static float Slider(string label, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(130f));
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
        GUILayout.Label(value.ToString("0.##"), GUILayout.Width(45f));
        GUILayout.EndHorizontal();
        return value;
    }

    private static void DrawBar(float fill, Color color)
    {
        Rect rect = GUILayoutUtility.GetRect(PanelWidth - 30f, 12f);
        GUI.DrawTexture(rect, Texture2D.grayTexture);
        rect.width *= Mathf.Clamp01(fill);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static GUIStyle RichLabel()
    {
        return new GUIStyle(GUI.skin.label) { richText = true };
    }

    // Runs the slider block and, in the editor, marks the asset dirty if anything changed so it gets saved.
    private static void EditorDirtyCheck(ScriptableObject asset, System.Action drawSliders)
    {
        GUI.changed = false;
        drawSliders();

#if UNITY_EDITOR
        if (GUI.changed == true)
        {
            UnityEditor.EditorUtility.SetDirty(asset);
        }
#endif
    }

    private static void ResetToDefaults<T>(T asset) where T : ScriptableObject
    {
        T defaults = ScriptableObject.CreateInstance<T>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), asset);
        Destroy(defaults);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(asset);
#endif
    }

    private void OnEnable()
    {
        controls.Debug.Enable();
    }

    private void OnDisable()
    {
        controls.Debug.Disable();
    }
}
