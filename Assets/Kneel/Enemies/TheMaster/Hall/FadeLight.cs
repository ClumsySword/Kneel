using UnityEngine;

// A light that flashes to `peak` and fades out over `seconds` (a lava burst).
public class FadeLight : MonoBehaviour
{
    private Light lamp;
    private float peak;
    private float seconds = 0.5f;
    private float started;

    public void Begin(float peakIntensity, float duration)
    {
        lamp = GetComponent<Light>();
        peak = peakIntensity;
        seconds = Mathf.Max(duration, 0.05f);
        started = Time.time;
        lamp.intensity = peak;
    }

    private void Update()
    {
        if (lamp == null)
            return;
        float t = (Time.time - started) / seconds;
        lamp.intensity = peak * Mathf.Clamp01(1f - t) * Mathf.Clamp01(1f - t);
        if (t >= 1f)
            lamp.enabled = false;
    }
}
