using UnityEngine;

// Briefly slows the whole game on impact so hits feel heavy. Overlapping calls extend the freeze.
public static class HitStop
{
    private const float FrozenTimeScale = 0.05f;

    private static HitStopRunner runner;

    public static void Freeze(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        if (runner == null)
        {
            var go = new GameObject("HitStop");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<HitStopRunner>();
        }

        runner.Freeze(duration, FrozenTimeScale);
    }

    private class HitStopRunner : MonoBehaviour
    {
        private float endRealtime;
        private bool frozen = false;

        public void Freeze(float duration, float scale)
        {
            endRealtime = Mathf.Max(endRealtime, Time.unscaledTime + duration);
            frozen = true;
            Time.timeScale = scale;
        }

        private void Update()
        {
            if (frozen == true && Time.unscaledTime >= endRealtime)
            {
                frozen = false;
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (frozen == true)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
