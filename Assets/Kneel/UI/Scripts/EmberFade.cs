using UnityEngine;

namespace Kneel.UI
{
    // Fades every live particle of a menu particle system at once (CanvasGroups don't reach particle
    // renderers). Drives the Kneel/UI/Additive shader's _Tint alpha through a property block.
    [RequireComponent(typeof(ParticleSystemRenderer))]
    public class EmberFade : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        [SerializeField, Range(0f, 1f)] private float alpha = 1f;

        private ParticleSystemRenderer particleRenderer;
        private MaterialPropertyBlock block;

        public float Alpha
        {
            get => alpha;
            set
            {
                alpha = value;
                Push();
            }
        }

        private void Awake()
        {
            particleRenderer = GetComponent<ParticleSystemRenderer>();
            block = new MaterialPropertyBlock();
            Push();
        }

        private void Push()
        {
            if (particleRenderer == null)
            {
                return;
            }

            block.SetColor(TintId, new Color(1f, 1f, 1f, alpha));
            particleRenderer.SetPropertyBlock(block);
        }
    }
}
