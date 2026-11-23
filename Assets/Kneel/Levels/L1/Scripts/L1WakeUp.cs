using Kneel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Kneel.Levels
{
    // L1's opening (the prologue's last shot, played): Kael wakes lying in the water beside the broken relic, in the
    // monastery's round hall, gets up, and the player walks him out to the battlefield.
    //
    // Out of black, a high, wide shot of the hall. He lies on his back, still, with his scar glowing faintly. After
    // lieTime he gets up (the knockdown get-up, slowed), the camera eases down into the gameplay follow, and control
    // comes back as he finds his feet. Once he stands, the save remembers it (SaveFlags "L1.WokeUp"), so a death,
    // Restart or Continue starts him where the level always has (its start, or the shrine he last rested at).
    //
    // It drives the player from outside, through their public hooks, with no change to their code:
    //   - PlayerCombat.Restrict at speed 0 ignores every action and stops him walking.
    //   - The movement's per-frame flags lock his turning (the mouse would spin him on the floor).
    //   - The animator's FullBody layer holds PlayerCombat's KnockdownUp state: frozen on its first frame (flat on
    //     his back), then played. The script runs between PlayerCombat (-10, which writes the layer weights each
    //     frame) and PlayerMovement (0, which reads the flags), so its writes are the ones that count.
    //   - PlayerScar.RestIntensity for the glow, PlayerCamera.SetOverride for the opening shot (same yaw as the
    //     follow, so it only lowers and closes in).
    // Game time drives it, so the pause menu freezes it.
    [DefaultExecutionOrder(-5)]
    public class L1WakeUp : MonoBehaviour
    {
        public const string Flag = "L1.WokeUp";

        [Tooltip("Where he lies (his hips). Its position only; he faces faceToward.")]
        public Transform wakeSpot;
        [Tooltip("What he faces as he gets up: the broken relic.")]
        public Transform faceToward;

        [Header("Timing (s)")]
        public float fadeIn = 3f;
        // From the first frame to the get-up starting (the fade included).
        public float lieTime = 5f;
        // The get-up's length; the combat knockdown's is 1.4 s.
        public float getUpTime = 2.2f;
        // The last part of the get-up, blended into his standing idle.
        public float blendOut = 0.45f;

        [Header("Opening shot")]
        public bool openingShot = true;
        // Pitch and distance at the start and at the get-up (a slow push in while he lies), around a point between
        // him and the relic.
        public float shotPitch = 58f;
        public float shotDistanceStart = 19f;
        public float shotDistanceEnd = 15f;
        // Easing back into the follow, from the get-up's start.
        public float cameraRelease = 3f;

        [Header("Scar")]
        public float scarGlow = 1.1f;

        // For playtests: run it even when the save says he has woken.
        [System.NonSerialized] public static bool ForceForTests;

        public bool Running { get; private set; }
        public float Elapsed => elapsed;

        private const string StateName = "KnockdownUp";
        private const string SpeedParameter = "KnockdownSpeed";
        private const string GetUpClip = "knockdown-getup";

        private PlayerCombat combat;
        private PlayerMovement movement;
        private PlayerScar scar;
        private PlayerCamera playerCamera;
        private Animator animator;
        private int fullBodyLayer = -1;
        private int upperBodyLayer = -1;
        private float elapsed;
        private bool gettingUp;
        private float scarRestBefore;
        private Image black;
        private GameObject canvasObject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ForceForTests = false;

        private void Start()
        {
            if (ShouldRun() == false)
            {
                enabled = false;
                return;
            }

            combat = FindAnyObjectByType<PlayerCombat>();
            movement = combat != null ? combat.GetComponent<PlayerMovement>() : null;
            animator = combat != null ? combat.GetComponentInChildren<Animator>() : null;
            if (combat == null || movement == null || animator == null || wakeSpot == null)
            {
                Debug.LogWarning("L1WakeUp: no player or wake spot; skipped.");
                enabled = false;
                return;
            }

            fullBodyLayer = animator.GetLayerIndex("FullBody");
            upperBodyLayer = animator.GetLayerIndex("UpperBody");
            scar = combat.GetComponentInChildren<PlayerScar>();
            playerCamera = FindAnyObjectByType<PlayerCamera>();

            Vector3 at = wakeSpot.position;
            if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 5f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
            {
                at = ground.point;
            }

            Vector3 facing = faceToward != null ? Vector3.ProjectOnPlane(faceToward.position - at, Vector3.up) : Vector3.forward;
            movement.Teleport(at, Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.forward));

            // Flat on his back: the get-up's first frame, held. Started by normalized time: at speed 0 the state
            // has no length, and a fixed-time offset into it comes out as 0.
            animator.SetFloat(SpeedParameter, 0f);
            float clipLength = GetUpClipLength();
            animator.Play(StateName, fullBodyLayer, clipLength > 0f ? combat.Settings.knockdownUpClipStart / clipLength : 0f);
            if (scar != null)
            {
                scarRestBefore = scar.RestIntensity;
                scar.RestIntensity = scarGlow;
            }

            BuildBlack();
            Running = true;
            Hold();
            if (openingShot && playerCamera != null)
            {
                SetShot(0f, 0.01f);
            }
        }

        // The KnockdownUp state's clip (knockdown-getup), found by name among the controller's clips.
        private float GetUpClipLength()
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == GetUpClip)
                {
                    return clip.length;
                }
            }

            Debug.LogWarning("L1WakeUp: no " + GetUpClip + " clip on the player's controller.");
            return 0f;
        }

        private bool ShouldRun()
        {
            if (ForceForTests)
            {
                return true;
            }

            if (LevelTransition.IsArriving)
            {
                return false;
            }

            return SaveFlags.Has(Flag) == false;
        }

        private void Update()
        {
            if (Running == false)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (black != null)
            {
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(fadeIn, 0.01f)));
                black.color = new Color(0f, 0f, 0f, a);
                if (a <= 0f)
                {
                    Destroy(canvasObject);
                    black = null;
                }
            }

            if (gettingUp == false)
            {
                // A slow push in while he lies there.
                if (openingShot && playerCamera != null)
                {
                    SetShot(Mathf.Clamp01(elapsed / lieTime), 0.01f);
                }

                if (scar != null)
                {
                    // A slow, faint pulse, like breath.
                    scar.RestIntensity = scarGlow * (0.8f + 0.2f * Mathf.Sin(elapsed * 1.6f));
                }

                if (elapsed >= lieTime)
                {
                    gettingUp = true;
                    animator.SetFloat(SpeedParameter, combat.Settings.knockdownUpClip / Mathf.Max(getUpTime, 0.1f));
                    if (openingShot && playerCamera != null)
                    {
                        playerCamera.ClearOverride(cameraRelease);
                    }
                }

                Hold();
                SetWeight(1f);
                return;
            }

            float up = elapsed - lieTime;
            if (scar != null)
            {
                scar.RestIntensity = Mathf.Lerp(scarGlow * 0.8f, scarRestBefore, Mathf.Clamp01(up / getUpTime));
            }

            Hold();
            SetWeight(Mathf.Clamp01((getUpTime - up) / Mathf.Max(blendOut, 0.01f)));
            if (up >= getUpTime)
            {
                Finish();
            }
        }

        // Every action ignored, no walking, no turning, no jumping, for this frame and the next.
        private void Hold()
        {
            combat.Restrict(0.25f, 0f);
            movement.rotationLocked = true;
            movement.jumpBlocked = true;
            movement.sprintBlocked = true;
            movement.moveSpeedMultiplier = 0f;
        }

        private void SetWeight(float weight)
        {
            if (fullBodyLayer >= 0)
            {
                animator.SetLayerWeight(fullBodyLayer, weight);
            }

            if (upperBodyLayer >= 0)
            {
                animator.SetLayerWeight(upperBodyLayer, 0f);
            }
        }

        private void Finish()
        {
            Running = false;
            enabled = false;
            combat.ReleaseRestriction();
            combat.ClearBufferedInput();
            movement.ClearInput();
            if (scar != null)
            {
                scar.RestIntensity = scarRestBefore;
            }

            if (canvasObject != null)
            {
                Destroy(canvasObject);
            }

            SaveFlags.Set(Flag);
        }

        // The shot: the follow's yaw, higher and further, over a point between him and the relic.
        private void SetShot(float push, float blend)
        {
            Vector3 focus = combat.transform.position;
            if (faceToward != null)
            {
                focus = Vector3.Lerp(focus, faceToward.position, 0.4f);
            }

            focus.y = 0.5f;
            Quaternion rotation = Quaternion.Euler(shotPitch, playerCamera.Settings.yaw, 0f);
            float distance = Mathf.Lerp(shotDistanceStart, shotDistanceEnd, Mathf.SmoothStep(0f, 1f, push));
            playerCamera.SetOverride(focus - rotation * Vector3.forward * distance, rotation, blend);
        }

        private void BuildBlack()
        {
            canvasObject = new GameObject("L1WakeUp_Fade");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Over the HUD, under the pause menu (500).
            canvas.sortingOrder = 400;
            var image = new GameObject("Black").AddComponent<Image>();
            image.transform.SetParent(canvasObject.transform, false);
            image.raycastTarget = false;
            image.color = Color.black;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            black = image;
        }

        private void OnDisable()
        {
            // Leaving mid-wake (a scene load): never leave the camera held or the scar lit.
            if (Running)
            {
                Running = false;
                if (playerCamera != null)
                {
                    playerCamera.ClearOverride(0.01f);
                }

                if (scar != null)
                {
                    scar.RestIntensity = scarRestBefore;
                }
            }
        }
    }
}
