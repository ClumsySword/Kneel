using Kneel.Audio;
using UnityEngine;

namespace Kneel.Levels
{
    // Kael's steps in water, in L1: in the monastery's flooded hall, and in the battlefield's puddles. Each step
    // there sends rings out from his foot, flicks up a few droplets and adds a splash over the step's own sound.
    //
    // The steps are the player's own (PlayerMovement plays one every stride and on landing): this reads its private
    // stride counter by reflection and splashes on the frame it resets, so the splash lands with the step it belongs
    // to, with no change to the player's code. If that field is ever renamed, it counts strides itself instead.
    //
    // Where the water is (Kneel/L1/Water Steps/Build sets these): the hall's sheet (a circle, and the strip into the
    // door), and the puddles, a mask baked from the ground layout's wetness (white = standing water) over the
    // ground's rectangle. Built and placed by L1WaterStepsBuild, in the playable scene only.
    [DefaultExecutionOrder(50)]
    public class L1WaterSteps : MonoBehaviour
    {
        [Header("Effects")]
        public ParticleSystem rings;
        public ParticleSystem droplets;
        public SoundEvent splash;

        [Header("The hall's water")]
        public Vector2 hallCentre = new Vector2(0f, -52.5f);
        public float hallRadius = 19.4f;
        // The strip from the hall into the inner door: x/z min and size.
        public Rect doorStrip = new Rect(-1.95f, -52.5f, 3.9f, 20.2f);
        public float waterY = 0.08f;

        [Header("Puddles")]
        public Texture2D wetMask;
        // The ground layout's rectangle (x/z min and size), which the mask covers.
        public Rect groundRect = new Rect(-52f, -26f, 114f, 412f);
        [Range(0f, 1f)] public float wetThreshold = 0.5f;

        [Header("Feel")]
        public float hallStrength = 1f;
        public float puddleStrength = 0.7f;

        // Raised on every splash (for tests): where, and how strong.
        public event System.Action<Vector3, float> Splashed;

        private PlayerMovement movement;
        private Transform leftFoot;
        private Transform rightFoot;
        private System.Reflection.FieldInfo strideField;
        private float lastStride;
        private float ownStride;
        private bool wasGrounded = true;

        private void Start()
        {
            movement = FindAnyObjectByType<PlayerMovement>();
            if (movement == null)
            {
                enabled = false;
                return;
            }

            var animator = movement.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            strideField = typeof(PlayerMovement).GetField("stepTravelled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (strideField != null && strideField.FieldType != typeof(float))
            {
                strideField = null;
            }
        }

        private void LateUpdate()
        {
            if (StepThisFrame() == false)
            {
                return;
            }

            Vector3 foot = PlantedFoot();
            if (InWater(foot, out float surfaceY, out float strength))
            {
                Splash(new Vector3(foot.x, surfaceY, foot.z), strength);
            }
        }

        // True on the frame the player's step sound plays.
        private bool StepThisFrame()
        {
            if (strideField != null)
            {
                float stride = (float)strideField.GetValue(movement);
                bool reset = stride < lastStride - 0.05f;
                lastStride = stride;
                return reset && movement.IsGrounded;
            }

            // Fallback: the same stride rule, counted here.
            bool grounded = movement.IsGrounded;
            bool landed = grounded && wasGrounded == false;
            wasGrounded = grounded;
            if (grounded == false || movement.forcedVelocity.HasValue)
            {
                return false;
            }

            ownStride += movement.LastMoveDelta.magnitude;
            if (landed || (ownStride >= 0.55f + 0.13f * movement.CurrentSpeed && movement.CurrentSpeed > 0.3f))
            {
                ownStride = 0f;
                return true;
            }

            return false;
        }

        // The foot on the ground: the lower one.
        private Vector3 PlantedFoot()
        {
            if (leftFoot == null || rightFoot == null)
            {
                return movement.transform.position;
            }

            return leftFoot.position.y <= rightFoot.position.y ? leftFoot.position : rightFoot.position;
        }

        public bool InWater(Vector3 at, out float surfaceY, out float strength)
        {
            var xz = new Vector2(at.x, at.z);
            if ((xz - hallCentre).sqrMagnitude <= hallRadius * hallRadius || doorStrip.Contains(xz))
            {
                surfaceY = waterY;
                strength = hallStrength;
                return true;
            }

            surfaceY = 0f;
            strength = 0f;
            if (wetMask == null || groundRect.Contains(xz) == false)
            {
                return false;
            }

            float u = (xz.x - groundRect.xMin) / groundRect.width;
            float v = (xz.y - groundRect.yMin) / groundRect.height;
            if (wetMask.GetPixelBilinear(u, v).r < wetThreshold)
            {
                return false;
            }

            surfaceY = at.y;
            if (Physics.Raycast(at + Vector3.up * 0.6f, Vector3.down, out RaycastHit ground, 1.5f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
            {
                surfaceY = ground.point.y;
            }

            surfaceY += 0.012f;
            strength = puddleStrength;
            return true;
        }

        // Rings, droplets and the splash sound, at a point on the water's surface.
        public void Splash(Vector3 at, float strength)
        {
            float pace = movement != null ? Mathf.Clamp01(movement.CurrentSpeed / 5f) : 0.5f;
            float s = strength * Mathf.Lerp(0.75f, 1.15f, pace);

            if (rings != null)
            {
                var ring = new ParticleSystem.EmitParams
                {
                    position = at + Vector3.up * 0.002f,
                    applyShapeToPosition = false,
                    startSize = 0.24f * s,
                    startLifetime = Random.Range(1.1f, 1.4f),
                };
                rings.Emit(ring, 1);
            }

            if (droplets != null)
            {
                int count = Mathf.RoundToInt(Mathf.Lerp(4f, 11f, s * 0.8f));
                for (int i = 0; i < count; i++)
                {
                    Vector2 out2 = Random.insideUnitCircle.normalized * Random.Range(0.25f, 0.9f) * s;
                    float up = Random.Range(0.9f, 1.7f) * Mathf.Sqrt(s);
                    var drop = new ParticleSystem.EmitParams
                    {
                        position = at + new Vector3(out2.x, 0f, out2.y) * 0.08f + Vector3.up * 0.01f,
                        applyShapeToPosition = false,
                        velocity = new Vector3(out2.x, up, out2.y),
                        // Up and back down to the surface.
                        startLifetime = 2f * up / 9.81f,
                        startSize = Random.Range(0.03f, 0.05f),
                    };
                    droplets.Emit(drop, 1);
                }
            }

            if (splash != null)
            {
                Sfx.Play(splash, at, Mathf.Lerp(0.5f, 0.85f, pace) * strength, true);
            }

            Splashed?.Invoke(at, strength);
        }
    }
}
