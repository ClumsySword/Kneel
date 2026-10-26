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
    private PlayerLantern playerLantern;

    [SerializeField]
    private PlayerCape playerCape;

    [SerializeField]
    private PlayerDamageVignette damageVignette;

    [SerializeField]
    private TargetDummy targetDummy;

    [SerializeField]
    private bool startOpen = false;

    private bool isOpen;
    private Vector2 scroll;

    private bool showCamera = false;
    private bool showMovement = false;
    private bool showCombat = true;
    private bool showLantern = false;
    private bool showCape = false;
    private bool showVignette = false;
    private bool previewVignette = false;
    private float previewHealth = 1f;
    private bool showDummy = true;

    private const float PanelWidth = 340f;

    // True while the pointer is over the open panel, so clicks there don't also swing the sword.
    public static bool IsPointerOverPanel { get; private set; }

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Debug.ToggleOverlay.performed += ctx => isOpen = isOpen == false;
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

        if (playerLantern == null)
        {
            playerLantern = FindAnyObjectByType<PlayerLantern>();
        }

        if (playerCape == null)
        {
            playerCape = FindAnyObjectByType<PlayerCape>();
        }

        if (damageVignette == null)
        {
            damageVignette = FindAnyObjectByType<PlayerDamageVignette>();
        }

        if (targetDummy == null)
        {
            targetDummy = FindAnyObjectByType<TargetDummy>();
        }

        isOpen = startOpen;
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

        if (playerLantern != null && playerLantern.Settings != null && Foldout("Lantern", ref showLantern))
        {
            DrawLanternSection(playerLantern.Settings);
        }

        if (playerCape != null && playerCape.Settings != null && Foldout("Cape", ref showCape))
        {
            DrawCapeSection(playerCape.Settings);
        }

        if (damageVignette != null && damageVignette.Settings != null && Foldout("Damage Vignette", ref showVignette))
        {
            DrawVignetteSection(damageVignette.Settings);
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
        string state = playerMovement.IsSprinting ? "sprinting" : "walking";
        GUILayout.Label($"Speed {playerMovement.CurrentSpeed:F2} m/s  ({state})");

        EditorDirtyCheck(s, () =>
        {
            s.walkSpeedForward = Slider("Walk forward", s.walkSpeedForward, 0.5f, 3f);
            s.walkSpeedForwardDiagonal = Slider("Walk fwd diagonal", s.walkSpeedForwardDiagonal, 0.5f, 3f);
            s.walkSpeedStrafe = Slider("Walk strafe", s.walkSpeedStrafe, 0.5f, 3f);
            s.walkSpeedBackwardDiagonal = Slider("Walk back diagonal", s.walkSpeedBackwardDiagonal, 0.5f, 3f);
            s.walkSpeedBackward = Slider("Walk backward", s.walkSpeedBackward, 0.5f, 3f);
            s.walkSpeedMultiplier = Slider("Walk speed x", s.walkSpeedMultiplier, 0.5f, 2f);
            s.acceleration = Slider("Acceleration", s.acceleration, 1f, 40f);
            s.deceleration = Slider("Deceleration", s.deceleration, 1f, 40f);
            s.aimTurnRate = Slider("Aim turn rate", s.aimTurnRate, 90f, 1440f);

            s.sprintSpeed = Slider("Sprint speed", s.sprintSpeed, 1f, 8f);
            s.sprintSpeedMultiplier = Slider("Sprint speed x", s.sprintSpeedMultiplier, 0.5f, 2f);
            s.sprintGrip = Slider("Sprint grip", s.sprintGrip, 90f, 2160f);
            s.sprintAcceleration = Slider("Sprint acceleration", s.sprintAcceleration, 1f, 20f);
            s.sprintTurnRate = Slider("Sprint turn rate", s.sprintTurnRate, 60f, 1080f);

            s.gravity = Slider("Gravity", s.gravity, 5f, 40f);
            s.jumpHeight = Slider("Jump height", s.jumpHeight, 0.2f, 2.5f);
            s.airControl = Slider("Air control", s.airControl, 0f, 1f);
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
        GUILayout.Label($"Poise {playerCombat.Poise.Current:0}/{playerCombat.Poise.maxPoise:0}");
        DrawBar(playerCombat.Poise.Fraction, new Color(0.95f, 0.75f, 0.2f));

        EditorDirtyCheck(s, () =>
        {
            for (int i = 0; i < s.attacks.Length; i++)
            {
                s.attacks[i].damage = Slider($"Hit {i + 1} damage", s.attacks[i].damage, 0f, 100f);
                s.attacks[i].poiseDamage = Slider($"Hit {i + 1} poise dmg", s.attacks[i].poiseDamage, 0f, 100f);
                s.attacks[i].lunge = Slider($"Hit {i + 1} lunge", s.attacks[i].lunge, 0f, 2f);
            }

            s.heavyAttack.damage = Slider("Heavy damage", s.heavyAttack.damage, 0f, 100f);
            s.heavyAttack.poiseDamage = Slider("Heavy poise dmg", s.heavyAttack.poiseDamage, 0f, 150f);
            s.heavyAttack.lunge = Slider("Heavy lunge", s.heavyAttack.lunge, 0f, 3f);
            s.heavyAttack.lungeTime = Slider("Heavy lunge at", s.heavyAttack.lungeTime, 0f, 2f);
            s.heavyBufferTime = Slider("Heavy buffer", s.heavyBufferTime, 0f, 1.5f);

            s.airAttack.damage = Slider("Air damage", s.airAttack.damage, 0f, 100f);
            s.airAttack.poiseDamage = Slider("Air poise dmg", s.airAttack.poiseDamage, 0f, 150f);
            s.airAttackCarry = Slider("Air carry speed", s.airAttackCarry, 0f, 8f);
            s.airAttack.lunge = Slider("Air land lunge", s.airAttack.lunge, 0f, 2f);
            s.airPlungeTime = Slider("Air hang time", s.airPlungeTime, 0f, 0.5f);
            s.airPlungeSpeed = Slider("Air plunge speed", s.airPlungeSpeed, 1f, 20f);
            s.airLandShake = Slider("Air land shake", s.airLandShake, 0f, 0.5f);

            s.criticalMultiplier = Slider("Crit multiplier", s.criticalMultiplier, 1f, 4f);
            s.attackSpeed = Slider("Attack speed", s.attackSpeed, 0.5f, 2f);
            s.comboBufferTime = Slider("Combo buffer", s.comboBufferTime, 0f, 1f);
            s.attackTurnWindow = Slider("Swing turn window", s.attackTurnWindow, 0f, 0.6f);
            s.drawSpeed = Slider("Draw speed", s.drawSpeed, 0.5f, 2.5f);

            s.dodgeDistance = Slider("Roll distance", s.dodgeDistance, 1f, 6f);
            s.dodgeDuration = Slider("Roll duration", s.dodgeDuration, 0.4f, 1.6f);
            s.dodgeInvulnerableStart = Slider("Roll i-frames from", s.dodgeInvulnerableStart, 0f, 1f);
            s.dodgeInvulnerableEnd = Slider("Roll i-frames to", s.dodgeInvulnerableEnd, 0f, 1.6f);
            s.dodgeBufferTime = Slider("Roll buffer", s.dodgeBufferTime, 0f, 1f);
            s.dodgeThroughEnemies = GUILayout.Toggle(s.dodgeThroughEnemies, " Roll through enemies");

            s.blockMoveMultiplier = Slider("Block move speed", s.blockMoveMultiplier, 0f, 1f);
            s.blockDamageReduction = Slider("Block reduction", s.blockDamageReduction, 0f, 1f);
            s.blockPoiseMultiplier = Slider("Block poise taken", s.blockPoiseMultiplier, 0f, 1f);
            s.parryWindow = Slider("Parry window", s.parryWindow, 0f, 0.6f);
            s.parryCooldown = Slider("Parry cooldown", s.parryCooldown, 0f, 1.5f);

            s.guardOffset.x = Slider("Guard side", s.guardOffset.x, -0.5f, 0.3f);
            s.guardOffset.y = Slider("Guard height", s.guardOffset.y, 0.8f, 1.6f);
            s.guardOffset.z = Slider("Guard reach", s.guardOffset.z, 0.3f, 0.8f);
            s.guardTilt = Slider("Guard tilt", s.guardTilt, -20f, 40f);
            s.guardMaxAimYaw = Slider("Guard aim limit", s.guardMaxAimYaw, 0f, 70f);
            s.blockHitRecoil = Slider("Block-hit recoil", s.blockHitRecoil, 0f, 0.3f);
            s.parryPush = Slider("Parry push", s.parryPush, 0f, 0.4f);
            s.parrySweep = Slider("Parry sweep", s.parrySweep, 0f, 70f);

            s.bashWindup = Slider("Bash wind-up", s.bashWindup, 0.02f, 0.3f);
            s.bashThrust = Slider("Bash thrust", s.bashThrust, 0.04f, 0.3f);
            s.bashRecover = Slider("Bash recover", s.bashRecover, 0.05f, 0.6f);
            s.bashReach = Slider("Bash reach", s.bashReach, 0.5f, 1.1f);
            s.bashLean = Slider("Bash lean", s.bashLean, 0f, 30f);
            s.bashLunge = Slider("Bash lunge", s.bashLunge, 0f, 1.5f);
            s.bashRadius = Slider("Bash radius", s.bashRadius, 0.2f, 1f);
            s.bashDamage = Slider("Bash damage", s.bashDamage, 0f, 30f);
            s.bashPoiseDamage = Slider("Bash poise dmg", s.bashPoiseDamage, 0f, 100f);

            s.hitStop = Slider("Hit-stop", s.hitStop, 0f, 0.3f);
            s.parryHitStop = Slider("Parry hit-stop", s.parryHitStop, 0f, 0.5f);
            s.hitShake = Slider("Hit shake", s.hitShake, 0f, 0.5f);
            s.hitStunDuration = Slider("Hit stun", s.hitStunDuration, 0f, 1.5f);
            s.staggerDuration = Slider("Stagger time", s.staggerDuration, 0f, 3f);

            s.maxPoise = Slider("Max poise", s.maxPoise, 1f, 200f);
            s.poiseRegenDelay = Slider("Poise regen delay", s.poiseRegenDelay, 0f, 5f);
            s.poiseRegenRate = Slider("Poise regen /s", s.poiseRegenRate, 0f, 100f);
        });

        if (GUILayout.Button("Reset combat to defaults"))
        {
            ResetToDefaults(s);
        }

        GUILayout.Space(10f);
    }

    private void DrawLanternSection(PlayerLanternSettings s)
    {
        EditorDirtyCheck(s, () =>
        {
            s.lightIntensity = Slider("Light intensity", s.lightIntensity, 0f, 15f);
            s.lightRange = Slider("Light range", s.lightRange, 1f, 20f);
            s.castShadows = GUILayout.Toggle(s.castShadows, " Light casts shadows");
            s.flickerAmount = Slider("Flicker amount", s.flickerAmount, 0f, 1f);
            s.flickerSpeed = Slider("Flicker speed", s.flickerSpeed, 0f, 20f);
            s.glassGlow = Slider("Glass glow", s.glassGlow, 0f, 10f);
            s.lightSmoothing = Slider("Light smoothing", s.lightSmoothing, 0f, 3f);
            s.lightClearance = Slider("Light clearance", s.lightClearance, 0f, 1f);

            s.pendulumLength = Slider("Pendulum length", s.pendulumLength, 0.05f, 0.6f);
            s.swayDamping = Slider("Sway damping", s.swayDamping, 0f, 15f);
            s.maxSwingAngle = Slider("Max swing angle", s.maxSwingAngle, 0f, 90f);
        });

        if (GUILayout.Button("Reset lantern to defaults"))
        {
            ResetToDefaults(s);
        }

        GUILayout.Space(10f);
    }

    private void DrawCapeSection(PlayerCapeSettings s)
    {
        EditorDirtyCheck(s, () =>
        {
            s.gravityScale = Slider("Gravity scale", s.gravityScale, 0f, 3f);
            s.swayDamping = Slider("Sway damping", s.swayDamping, 0f, 15f);
            s.airDrag = Slider("Air drag", s.airDrag, 0f, 10f);
            s.contactFriction = Slider("Contact friction", s.contactFriction, 0f, 40f);
            s.shoulderStiffness = Slider("Shoulder stiffness", s.shoulderStiffness, 0f, 1000f);
            s.hemStiffness = Slider("Hem stiffness", s.hemStiffness, 0f, 200f);
            s.spreadSlack = Slider("Spread slack", s.spreadSlack, 0f, 1f);
            s.clothRadius = Slider("Cloth radius", s.clothRadius, 0f, 0.15f);
        });

        if (GUILayout.Button("Reset cape to defaults"))
        {
            ResetToDefaults(s);
        }

        GUILayout.Space(10f);
    }

    private void DrawVignetteSection(PlayerDamageVignetteSettings s)
    {
        // Preview overrides the real health so the look can be tuned without taking hits. Not saved.
        previewVignette = GUILayout.Toggle(previewVignette, " Preview health");
        if (previewVignette == true)
        {
            previewHealth = Slider("Preview health", previewHealth, 0f, 1f);
        }

        damageVignette.previewHealth = previewVignette ? previewHealth : -1f;
        GUILayout.Label($"Coverage {damageVignette.Coverage:0.00}");

        EditorDirtyCheck(s, () =>
        {
            s.opacity = Slider("Opacity", s.opacity, 0f, 1f);
            s.edgeSoftness = Slider("Edge softness", s.edgeSoftness, 0.001f, 0.5f);
            s.deepColorDepth = Slider("Deep colour depth", s.deepColorDepth, 0.01f, 1f);
            s.noiseScale = Slider("Noise scale", s.noiseScale, 0.5f, 10f);
            s.noiseStrength = Slider("Noise strength", s.noiseStrength, 0f, 1f);
            s.noiseDrift = Slider("Noise drift", s.noiseDrift, 0f, 0.2f);
            s.shadowWidth = Slider("Shadow width", s.shadowWidth, 0f, 0.5f);
            s.shadowStrength = Slider("Shadow strength", s.shadowStrength, 0f, 1f);
            s.grain = Slider("Grain", s.grain, 0f, 1f);
            s.crustWidth = Slider("Crust width", s.crustWidth, 0.001f, 0.2f);
            s.crustDarken = Slider("Crust darken", s.crustDarken, 0f, 1f);

            s.creepSpeed = Slider("Creep speed", s.creepSpeed, 0.01f, 2f);
            s.hitPulseStrength = Slider("Hit pulse", s.hitPulseStrength, 0f, 5f);
            s.hitPulseDecay = Slider("Hit pulse decay", s.hitPulseDecay, 0.01f, 4f);
            s.heartbeatThreshold = Slider("Heartbeat below", s.heartbeatThreshold, 0f, 1f);
            s.heartbeatRate = Slider("Heartbeat rate", s.heartbeatRate, 0f, 3f);
            s.heartbeatStrength = Slider("Heartbeat strength", s.heartbeatStrength, 0f, 0.3f);
            s.deathFadeTime = Slider("Death fade time", s.deathFadeTime, 0.01f, 3f);
        });

        if (GUILayout.Button("Reset vignette to defaults"))
        {
            // Keep the shader reference: the defaults have none.
            Shader shader = s.shader;
            ResetToDefaults(s);
            s.shader = shader;
        }

        GUILayout.Space(10f);
    }

    private void DrawDummySection()
    {
        TargetDummy d = targetDummy;
        GUILayout.Label($"Hits {d.HitCount}   Total damage {d.TotalDamage:0}   {(d.IsStaggered ? "STAGGERED" : "")}");
        GUILayout.Label($"Poise {d.poise.Current:0}/{d.poise.maxPoise:0}");
        DrawBar(d.poise.Fraction, new Color(0.95f, 0.75f, 0.2f));

        d.attackEnabled = GUILayout.Toggle(d.attackEnabled, " Dummy attacks");
        d.attackDamage = Slider("Kick damage", d.attackDamage, 0f, 60f);
        d.kickPoiseDamage = Slider("Kick poise dmg", d.kickPoiseDamage, 0f, 100f);
        d.attackInterval = Slider("Kick interval", d.attackInterval, 0.5f, 6f);
        d.attackWindup = Slider("Kick windup", d.attackWindup, 0.1f, 2f);
        d.attackRange = Slider("Kick range", d.attackRange, 1f, 4f);
        d.staggerDuration = Slider("Stagger time", d.staggerDuration, 0.2f, 3f);
        d.poise.maxPoise = Slider("Max poise", d.poise.maxPoise, 1f, 200f);
        d.windupPoiseMultiplier = Slider("Wind-up poise x", d.windupPoiseMultiplier, 1f, 4f);
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
