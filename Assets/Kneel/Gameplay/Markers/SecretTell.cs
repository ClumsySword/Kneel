using UnityEngine;

namespace Kneel.Markers
{
    // The scorched handprint that marks every secret: black, with a faint ember edge that pulses slowly.
    public class SecretTell : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public string secretId;

        [SerializeField]
        private Renderer handprint;

        [SerializeField, ColorUsage(false, true)]
        private Color ember = new Color(1.6f, 0.5f, 0.1f);

        // Seconds per pulse.
        [SerializeField]
        private float period = 2.4f;

        [SerializeField, Range(0f, 1f)]
        private float dimmest = 0.25f;

        private MaterialPropertyBlock block;

        private void Update()
        {
            if (handprint == null)
            {
                return;
            }

            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, period));
            handprint.GetPropertyBlock(block);
            block.SetColor(EmissionColor, ember * Mathf.Lerp(dimmest, 1f, wave));
            handprint.SetPropertyBlock(block);
        }
    }
}
