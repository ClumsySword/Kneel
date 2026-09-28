using UnityEngine;

namespace Kneel.Lighting
{
    // Slow, organic fire flicker: layered noise on intensity with a small matching range breath.
    // Visual only; used on the checkpoint shrine's light.
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField]
        private float baseIntensity = 6f;

        // Fraction of baseIntensity the flicker swings by (0.2 = +/-20%).
        [SerializeField]
        [Range(0f, 0.6f)]
        private float amplitude = 0.18f;

        // How fast the flame breathes; higher is more nervous.
        [SerializeField]
        private float speed = 1.3f;

        // Fraction of the range that breathes with the intensity.
        [SerializeField]
        [Range(0f, 0.3f)]
        private float rangeBreath = 0.05f;

        private Light flickerLight;
        private float baseRange;
        private float seed;

        private void Awake()
        {
            flickerLight = GetComponent<Light>();
            baseRange = flickerLight.range;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.time * speed + seed;

            // A slow swell plus faster crackle, centred on 0.
            float noise = (Mathf.PerlinNoise(t * 0.6f, seed) - 0.5f) * 1.4f + (Mathf.PerlinNoise(t * 3.1f, seed + 17f) - 0.5f) * 0.6f;
            float k = 1f + noise * amplitude * 2f;

            flickerLight.intensity = baseIntensity * k;
            flickerLight.range = baseRange * (1f + (k - 1f) * rangeBreath / Mathf.Max(amplitude, 0.001f));
        }
    }
}
