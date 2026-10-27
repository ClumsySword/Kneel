using System.Collections;
using UnityEngine;

namespace Kneel.UI
{
    // Coroutine fades on unscaled time (menus must not care about hit-stop or a paused timeScale).
    public static class UITween
    {
        // A frame longer than this (a scene activation, a hitch) only advances a fade by this much, so fades
        // never jump.
        private const float MaxStep = 1f / 30f;

        public static float DeltaTime => Mathf.Min(Time.unscaledDeltaTime, MaxStep);

        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += DeltaTime)
            {
                yield return null;
            }
        }

        public static IEnumerator Fade(CanvasGroup group, float to, float duration)
        {
            float from = group.alpha;
            for (float t = 0f; t < duration; t += DeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, Smooth(t / duration));
                yield return null;
            }

            group.alpha = to;
        }
    }
}
