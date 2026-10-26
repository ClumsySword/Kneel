using UnityEngine;

namespace Kneel
{
    // Moves the lava: scrolls the crust texture slowly along the ravine and lets its glow breathe. Per renderer
    // through a property block, so the shared material asset is never written at runtime.
    public class LavaFlow : MonoBehaviour
    {
        [SerializeField]
        private Vector2 flow = new Vector2(0.004f, 0.01f);

        [SerializeField]
        private float tiling = 0.025f;

        [SerializeField]
        private Color emission = new Color(2.4f, 0.85f, 0.22f);

        [SerializeField]
        private float pulse = 0.18f;

        [SerializeField]
        private float pulseSpeed = 0.6f;

        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private Renderer[] renderers;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            float t = Time.time;
            var st = new Vector4(tiling, tiling, flow.x * t, flow.y * t);
            var glow = emission * (1f + pulse * Mathf.Sin(t * pulseSpeed) + pulse * 0.5f * Mathf.Sin(t * pulseSpeed * 2.7f + 1.3f));
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetVector(BaseMapST, st);
                block.SetColor(EmissionColor, glow);
                r.SetPropertyBlock(block);
            }
        }
    }
}
