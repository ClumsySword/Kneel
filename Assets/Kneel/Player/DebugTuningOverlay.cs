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
    private PlayerAsh playerAsh;

    [SerializeField]
    private bool startOpen = false;

    private bool isOpen;
    private Vector2 scroll;
    private string scrollToSection;

    private bool showCamera = false;
    private bool showMovement = false;
    private bool showCombat = true;
    private bool showLantern = false;
    private bool showCape = false;
    private bool showVignette = false;
    private bool previewVignette = false;
    private float previewHealth = 1f;
    private bool showDummy = true;
    private bool showAsh = true;
    private bool showAshTuning = false;
    private bool showEnemies = true;

    // Enemy spawner: pick a type, click in the world, and it appears frozen until released.
    private EnemySpawnRoster roster;
    private Enemy placing;
    private bool keepPlacing = false;
    private bool faceThePlayer = true;
    private string placeMessage = "";
    private readonly System.Collections.Generic.List<Enemy> spawned = new System.Collections.Generic.List<Enemy>();

    // Everything but the player and the enemies, so a click lands on the ground under them.
    private const int PlayerLayer = 9;
    private const int EnemyLayer = 8;

    private bool showMaster = false;
    private MasterP1 master;

    private const float PanelWidth = 340f;

    // Where the open panel ends on the left of the screen (the boss lab, MasterDevTool, keeps clear of it).
    public static float PanelRight => 10f + PanelWidth;

    // True while the pointer is over the open panel, or a click is about to place an enemy, so the click doesn't also
    // swing the sword.
    public static bool IsPointerOverPanel { get; private set; }

    // True while the pointer is over the open panel itself; the camera leaves the scroll wheel to the panel meanwhile.
    public static bool IsPointerOverPanelArea { get; private set; }

    // True while the panel is open; the aim ember hands over to the menu cursor meanwhile (AimEmber).
    public static bool IsOpen { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Levels without one placed get one, so the F1 tools (the Ash unlocks among them) work everywhere while testing.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        AddIfMissing();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        AddIfMissing();
    }

    private static void AddIfMissing()
    {
        if (FindAnyObjectByType<DebugTuningOverlay>() == null && FindAnyObjectByType<PlayerCombat>() != null)
        {
            new GameObject("Debug").AddComponent<DebugTuningOverlay>();
        }
    }
#endif

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

        if (playerAsh == null)
        {
            playerAsh = FindAnyObjectByType<PlayerAsh>();
        }

        roster = EnemySpawnRoster.Load();
        master = FindAnyObjectByType<MasterP1>();

        isOpen = startOpen;
    }

    private void Update()
    {
        IsOpen = isOpen;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (isOpen == false || mouse == null)
        {
            IsPointerOverPanel = false;
            IsPointerOverPanelArea = false;
            placing = null;
            return;
        }

        // IMGUI rects are measured from the top of the screen, pointer positions from the bottom.
        Vector2 pointer = mouse.position.ReadValue();
        bool overPanel = PanelRect().Contains(new Vector2(pointer.x, Screen.height - pointer.y));
        IsPointerOverPanelArea = overPanel;

        if (placing != null && overPanel == false && Kneel.UI.PauseMenu.IsPaused == false)
        {
            if (mouse.rightButton.wasPressedThisFrame)
            {
                placing = null;
                placeMessage = "";
            }
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                TryPlace(placing, pointer);
                if (keepPlacing == false)
                    placing = null;

                // This frame's click was already kept from the sword (the flag was up since the type was picked).
                IsPointerOverPanel = true;
                return;
            }
        }

        IsPointerOverPanel = overPanel || placing != null;
    }

    private void TryPlace(Enemy prefab, Vector2 pointer)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            placeMessage = "No main camera.";
            return;
        }

        int mask = ~((1 << PlayerLayer) | (1 << EnemyLayer) | (1 << 2));
        if (Physics.Raycast(cam.ScreenPointToRay(pointer), out RaycastHit hit, 500f, mask, QueryTriggerInteraction.Ignore) == false)
        {
            placeMessage = "Nothing under the cursor.";
            return;
        }

        // Onto the NavMesh this type walks on (L3/L4 use other agent types), or it can't path once released.
        Vector3 position = hit.point;
        var prefabAgent = prefab.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (prefabAgent != null)
        {
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = prefabAgent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            if (UnityEngine.AI.NavMesh.SamplePosition(hit.point, out UnityEngine.AI.NavMeshHit onMesh, 2f, filter) == false)
            {
                placeMessage = "No NavMesh there for " + prefab.name + ".";
                return;
            }

            position = onMesh.position;
        }

        Quaternion rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y + 180f, 0f);
        if (faceThePlayer && playerMovement != null)
        {
            Vector3 toPlayer = playerMovement.transform.position - position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                rotation = Quaternion.LookRotation(toPlayer);
        }

        // Awake has run once Instantiate returns and Start hasn't, so it's frozen before its first state ever ticks.
        Enemy enemy = Instantiate(prefab, position, rotation);
        enemy.name = prefab.name + " (spawned)";
        enemy.Frozen = true;
        enemy.ClearPatrol();
        spawned.Add(enemy);
        placeMessage = "Placed " + prefab.name + ".";
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

        if (playerAsh != null && playerAsh.Settings != null && Foldout("Ash", ref showAsh))
        {
            DrawAshSection(playerAsh, playerAsh.Settings);
        }

        if (roster != null && Foldout("Enemies", ref showEnemies))
        {
            DrawEnemiesSection();
        }

        if (targetDummy != null && Foldout("Target Dummy", ref showDummy))
        {
            DrawDummySection();
        }

        if (master != null && master.settings != null && Foldout("Master", ref showMaster))
        {
            DrawMasterSection(master.settings);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // The meter and the use count are live state (AshProgress), not tuning; the sliders below them write the asset.
    private void DrawAshSection(PlayerAsh ash, AshSettings s)
    {
        AshMeter meter = ash.GetComponent<AshMeter>();
        int uses = Kneel.Core.AshProgress.Uses;

        GUILayout.Label($"Ash <b>{meter.Ash:0}</b> / {meter.Max:0}{(meter.IsFull ? "  FULL" : "")}   Uses <b>{uses}</b>"
            + (meter.Granted ? "" : "   (not granted in this level)")
            + (Kneel.Core.AshProgress.Practice ? "   practice run" : ""), RichLabel());
        DrawBar(meter.Fraction, new Color(1f, 0.45f, 0.15f));

        string unlocked = "";
        foreach (PlayerAsh.Ability ability in System.Enum.GetValues(typeof(PlayerAsh.Ability)))
        {
            int key = (int)ability + 1;
            unlocked += ash.IsUnlocked(ability)
                ? $"{key} {PlayerAsh.Names[key - 1]}   "
                : $"<color=#777>{key} {PlayerAsh.Names[key - 1]} ({ash.UnlockUses(ability)})</color>   ";
        }

        GUILayout.Label(unlocked, RichLabel());

        float ashValue = Slider("Ash", meter.Ash, 0f, meter.Max);
        if (Mathf.Abs(ashValue - meter.Ash) > 0.01f)
        {
            meter.SetAsh(ashValue);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill meter"))
        {
            meter.SetAsh(meter.Max);
        }

        if (GUILayout.Button("Empty"))
        {
            meter.SetAsh(0f);
        }

        GUILayout.EndHorizontal();

        // Instant unlocks: each button sets the use count to what that ability needs.
        GUILayout.Label("Unlock instantly", RichLabel());
        GUILayout.BeginHorizontal();
        foreach (PlayerAsh.Ability ability in System.Enum.GetValues(typeof(PlayerAsh.Ability)))
        {
            if (ability == PlayerAsh.Ability.AshenSword)
            {
                continue;
            }

            GUI.enabled = ash.IsUnlocked(ability) == false;
            if (GUILayout.Button(PlayerAsh.Names[(int)ability]))
            {
                ash.SetUses(ash.UnlockUses(ability));
            }
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Unlock all"))
        {
            ash.UnlockAll();
        }

        if (GUILayout.Button("Reset uses"))
        {
            ash.SetUses(0);
        }

        GUILayout.EndHorizontal();

        int newUses = Mathf.RoundToInt(Slider("Uses", uses, 0f, 40f));
        if (newUses != uses)
        {
            ash.SetUses(newUses);
        }

        Kneel.Core.AshProgress.DebugGrant = GUILayout.Toggle(Kneel.Core.AshProgress.DebugGrant, " Meter in L1 too");
        AshMeter.DummyGivesAsh = GUILayout.Toggle(AshMeter.DummyGivesAsh, " Dummy gives Ash when felled");

        showAshTuning = GUILayout.Toggle(showAshTuning, " Tuning");
        if (showAshTuning == false)
        {
            return;
        }

        EditorDirtyCheck(s, () =>
        {
            s.footmanKill = Slider("Footman kill", s.footmanKill, 0f, 100f);
            s.archerKill = Slider("Archer kill", s.archerKill, 0f, 100f);
            s.bruteKill = Slider("Brute kill", s.bruteKill, 0f, 100f);
            s.foolKill = Slider("Fool kill", s.foolKill, 0f, 100f);
            s.parryGain = Slider("Parry", s.parryGain, 0f, 50f);
            s.evadeGain = Slider("Roll-through", s.evadeGain, 0f, 30f);
            s.castBufferTime = Slider("Cast buffer", s.castBufferTime, 0f, 1f);

            s.ashenSwordDuration = Slider("Ashen Sword time", s.ashenSwordDuration, 1f, 30f);
            s.ashenSwordDamageMultiplier = Slider("Ashen Sword dmg x", s.ashenSwordDamageMultiplier, 1f, 3f);
            s.blindDuration = Slider("Blind time", s.blindDuration, 0.5f, 10f);
            s.blindRange = Slider("Blind range", s.blindRange, 3f, 25f);
            s.surgeDistance = Slider("Surge distance", s.surgeDistance, 1f, 8f);
            s.surgeDuration = Slider("Surge time", s.surgeDuration, 0.1f, 0.8f);
            s.ashfallRadius = Slider("Ashfall radius", s.ashfallRadius, 1f, 10f);
            s.rekindleHeal = Slider("Rekindle heal", s.rekindleHeal, 0f, 1f);

            s.fillingIntensity = Slider("Glow filling", s.fillingIntensity, 0f, 3f);
            s.fullIntensity = Slider("Glow full", s.fullIntensity, 0f, 5f);
            s.handLightIntensity = Slider("Hand light", s.handLightIntensity, 0f, 5f);
            s.handLightRange = Slider("Hand light range", s.handLightRange, 0.5f, 4f);
            s.emberRate = Slider("Embers /s", s.emberRate, 0f, 60f);
            s.wispRate = Slider("Wisps /s", s.wispRate, 0f, 30f);
            s.gradeStrength = Slider("Grade", s.gradeStrength, 0f, 1f);
            s.volume = Slider("Volume", s.volume, 0f, 1f);
            s.humVolume = Slider("Hum volume", s.humVolume, 0f, 1f);
        });
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
            s.drawReachStart = Slider("Draw reach from", s.drawReachStart, 0f, 0.6f);
            s.drawGrabTime = Slider("Draw grab", s.drawGrabTime, 0.1f, 1f);
            s.drawPullTime = Slider("Draw pull time", s.drawPullTime, 0.05f, 0.8f);
            s.drawPullDistance = Slider("Draw pull dist", s.drawPullDistance, 0f, 1.2f);
            s.drawHandBackTime = Slider("Draw hand back", s.drawHandBackTime, 0.05f, 0.8f);
            s.shieldHandUpTime = Slider("Shield hand up", s.shieldHandUpTime, 0f, 0.6f);
            s.shieldGrabTime = Slider("Shield grab", s.shieldGrabTime, 0.05f, 0.8f);
            s.shieldLiftedTime = Slider("Shield lifted", s.shieldLiftedTime, 0.1f, 1f);
            s.shieldOverTime = Slider("Shield over", s.shieldOverTime, 0.2f, 1.2f);
            s.shieldHandBackTime = Slider("Shield hand back", s.shieldHandBackTime, 0.05f, 0.8f);
            s.shieldStowStart = Slider("Stow from", s.shieldStowStart, 0f, 1f);
            s.shieldStowSideTime = Slider("Stow beside", s.shieldStowSideTime, 0.1f, 1.2f);
            s.shieldStowLiftTime = Slider("Stow over", s.shieldStowLiftTime, 0.2f, 1.4f);
            s.backShieldReleaseTime = Slider("Back sheathe: shield off", s.backShieldReleaseTime, 0f, 1.3f);
            s.shieldStowDoneTime = Slider("Stow on back", s.shieldStowDoneTime, 0.3f, 1.6f);
            s.shieldArc = Slider("Shield arc", s.shieldArc, 0f, 0.6f);
            s.shieldMaxReach = Slider("Shield reach", s.shieldMaxReach, 0.3f, 0.7f);
            s.sheathEnterStart = Slider("Sheathe reach from", s.sheathEnterStart, 0f, 1.5f);
            s.sheathInsertStart = Slider("Sheathe insert", s.sheathInsertStart, 0f, 1.5f);
            s.sheathReleaseTime = Slider("Sheathe release", s.sheathReleaseTime, 0.2f, 1.7f);
            s.sheathLetGoTime = Slider("Sheathe let go", s.sheathLetGoTime, 0.05f, 0.8f);
            s.swordSettleTime = Slider("Sword settle", s.swordSettleTime, 0f, 0.5f);
            s.shieldSettleTime = Slider("Shield settle", s.shieldSettleTime, 0f, 1f);
            s.drawHandRotationWeight = Slider("Draw hand turn", s.drawHandRotationWeight, 0f, 1f);
            s.shieldReachRotationWeight = Slider("Shield hand turn", s.shieldReachRotationWeight, 0f, 1f);

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

            s.potionHeal = Slider("Potion heal", s.potionHeal, 0f, 100f);
            s.drinkSpeed = Slider("Drink speed", s.drinkSpeed, 0.5f, 3f);
            s.drinkMoveMultiplier = Slider("Drink move speed", s.drinkMoveMultiplier, 0f, 1f);

            s.hitStop = Slider("Hit-stop", s.hitStop, 0f, 0.3f);
            s.parryHitStop = Slider("Parry hit-stop", s.parryHitStop, 0f, 0.5f);
            s.hitShake = Slider("Hit shake", s.hitShake, 0f, 0.5f);
            s.impactShove = Slider("Impact shove", s.impactShove, 0f, 2f);
            s.impactShoveTime = Slider("Impact shove time", s.impactShoveTime, 0.05f, 0.5f);
            s.blockShoveMultiplier = Slider("Block shove", s.blockShoveMultiplier, 0f, 1f);
            s.hitStunDuration = Slider("Hit stun", s.hitStunDuration, 0f, 1.5f);
            s.staggerDuration = Slider("Stagger time", s.staggerDuration, 0f, 3f);
            s.kneelDownTime = Slider("Kneel down time", s.kneelDownTime, 0.1f, 1.5f);
            s.kneelUpTime = Slider("Kneel up time", s.kneelUpTime, 0.1f, 1.5f);

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

    private void DrawEnemiesSection()
    {
        spawned.RemoveAll(e => e == null);

        GUILayout.Label(placing != null
            ? $"<color=#ffcc55>Click the ground to place {placing.name}. Right-click cancels.</color>"
            : "Pick an enemy, then click the ground.", RichLabel());

        foreach (Enemy prefab in roster.prefabs)
        {
            if (prefab == null)
                continue;

            bool armed = placing == prefab;
            if (GUILayout.Button((armed ? "● " : "") + prefab.name))
            {
                placing = armed ? null : prefab;
                placeMessage = "";
            }
        }

        keepPlacing = GUILayout.Toggle(keepPlacing, " Keep placing (several per pick)");
        faceThePlayer = GUILayout.Toggle(faceThePlayer, " Face the player");

        if (placeMessage.Length > 0)
            GUILayout.Label(placeMessage);

        int frozen = spawned.FindAll(e => e.Frozen).Count;
        GUILayout.Label($"Spawned {spawned.Count}   Frozen {frozen}");

        GUI.enabled = frozen > 0;
        if (GUILayout.Button("Unfreeze all spawned"))
        {
            foreach (Enemy e in spawned)
                e.Frozen = false;
        }

        GUI.enabled = spawned.Count > 0;
        if (GUILayout.Button("Remove all spawned"))
        {
            foreach (Enemy e in spawned)
                Destroy(e.gameObject);
            spawned.Clear();
        }

        GUI.enabled = true;
        GUILayout.Space(10f);
    }

    // The L5 boss (MasterSettings). Writes into the asset, so values found while fighting him are kept.
    private void DrawMasterSection(MasterSettings s)
    {
        GUILayout.Label($"HP {master.health.Current:0}/{master.health.Max:0}   Poise {master.poise.Current:0}/{master.poise.maxPoise:0}   {master.stateMachine.currentState?.GetType().Name}");
        DrawBar(master.health.Fraction, new Color(0.9f, 0.3f, 0.12f));
        s.maxHealth = Slider("Health", s.maxHealth, 50f, 1500f);
        s.maxPoise = Slider("Poise", s.maxPoise, 10f, 400f);
        s.walkSpeed = Slider("Walk speed", s.walkSpeed, 0.5f, 5f);
        s.strideSpeed = Slider("Stride speed", s.strideSpeed, 1f, 9f);
        s.pauseMin = Slider("Pause min", s.pauseMin, 0f, 4f);
        s.pauseMax = Mathf.Max(Slider("Pause max", s.pauseMax, 0f, 5f), s.pauseMin);
        s.windupDamageMultiplier = Slider("Wind-up damage x", s.windupDamageMultiplier, 0f, 1f);
        s.parryKneelTime = Slider("Parried reel", s.parryKneelTime, 0.3f, 2f);
        s.parryPoise = Slider("Parry stagger fill", s.parryPoise, 0f, 100f);
        s.poiseStaggerTime = Slider("Stagger (kneel)", s.poiseStaggerTime, 1.5f, 6f);
        s.decreeTell = Slider("Decree tell", s.decreeTell, 0.3f, 2f);
        s.decreeDamage = Slider("Decree damage", s.decreeDamage, 0f, 100f);
        s.sentenceTell1 = Slider("Sentence tell 1", s.sentenceTell1, 0.3f, 2f);
        s.sentenceTell2 = Slider("Sentence tell 2", s.sentenceTell2, 0.3f, 2f);
        s.sentenceTell3 = Slider("Sentence tell 3", s.sentenceTell3, 0.3f, 2.5f);
        s.sentenceDamage = Slider("Sentence damage", s.sentenceDamage, 0f, 100f);
        s.commandTell = Slider("Command tell", s.commandTell, 1f, 4f);
        s.commandTellFirst = Slider("Command tell (first)", s.commandTellFirst, 1f, 5f);
        s.commandCooldown = Slider("Command cooldown", s.commandCooldown, 2f, 30f);
        s.commandRadius = Slider("Command circle radius", s.commandRadius, 0.8f, 4f);
        s.commandLock = Slider("Command circle lock", s.commandLock, 0.1f, 1f);
        s.threadsTell = Slider("Threads tell", s.threadsTell, 0.3f, 2f);
        s.threadSpeed = Slider("Thread speed", s.threadSpeed, 3f, 20f);
        s.threadsLock = Slider("Threads lane lock", s.threadsLock, 0f, 1f);
        s.lashLock = Slider("Lash lane lock", s.lashLock, 0f, 1f);
        s.guardMaxTime = Slider("Guard max time", s.guardMaxTime, 0.5f, 6f);
        s.thrustTell = Slider("Thrust tell", s.thrustTell, 0.3f, 2f);
        s.thrustDamage = Slider("Thrust damage", s.thrustDamage, 0f, 100f);
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

    private bool Foldout(string title, ref bool open)
    {
        if (GUILayout.Button((open ? "▼ " : "► ") + title, RichLabel()))
        {
            open = open == false;

            // The open sections easily run past the bottom of the screen (Combat alone does), so a section opened
            // below the fold is brought up to the top instead of opening out of sight.
            if (open)
                scrollToSection = title;
        }

        if (scrollToSection == title && Event.current.type == EventType.Repaint)
        {
            scroll.y = GUILayoutUtility.GetLastRect().y - 4f;
            scrollToSection = null;
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
        IsOpen = false;
        IsPointerOverPanel = false;
        IsPointerOverPanelArea = false;
        placing = null;
    }
}
