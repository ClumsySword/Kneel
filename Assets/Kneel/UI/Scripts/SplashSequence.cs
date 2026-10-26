using System.Collections;
using UnityEngine;

namespace Kneel.UI
{
    // The unskippable logo splash: black, logo fades in, holds while slowly growing, fades out, then the
    // preloaded main menu activates. It never reads input (the scene has no EventSystem), so nothing skips it.
    public class SplashSequence : MonoBehaviour
    {
        [SerializeField] private string nextScene = "MainMenu";

        [Header("Timings (unscaled seconds)")]
        [SerializeField] private float initialBlack = 0.5f;
        [SerializeField] private float fadeInTime = 1.5f;
        [SerializeField] private float holdTime = 2.5f;
        [SerializeField] private float fadeOutTime = 1f;

        [Header("Logo")]
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private RectTransform logo;
        [SerializeField] private float holdScaleFrom = 1f;
        [SerializeField] private float holdScaleTo = 1.03f;

        [Header("Embers (optional)")]
        [SerializeField] private ParticleSystem embers;

        private EmberFade emberFade;

        private IEnumerator Start()
        {
            Time.timeScale = 1f;
            AsyncOperation next = SceneLoader.Preload(nextScene);
            if (next == null)
            {
                Debug.LogError($"Splash can't load '{nextScene}': add it to the Build Profiles scene list.", this);
            }

            logoGroup.alpha = 0f;
            logo.localScale = Vector3.one * holdScaleFrom;
            if (embers != null)
            {
                emberFade = embers.GetComponent<EmberFade>();
            }

            yield return UITween.Wait(initialBlack);

            if (embers != null)
            {
                embers.Play();
            }

            yield return Fade(1f, fadeInTime);

            for (float t = 0f; t < holdTime; t += UITween.DeltaTime)
            {
                logo.localScale = Vector3.one * Mathf.Lerp(holdScaleFrom, holdScaleTo, UITween.Smooth(t / holdTime));
                yield return null;
            }

            logo.localScale = Vector3.one * holdScaleTo;
            if (embers != null)
            {
                embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            yield return Fade(0f, fadeOutTime);

            if (next == null)
            {
                yield break;
            }

            while (next.progress < 0.9f)
            {
                yield return null;
            }

            next.allowSceneActivation = true;
        }

        // Fades the logo and the embers together.
        private IEnumerator Fade(float to, float duration)
        {
            float from = logoGroup.alpha;
            for (float t = 0f; t < duration; t += UITween.DeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, UITween.Smooth(t / duration)));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            logoGroup.alpha = alpha;
            if (emberFade != null)
            {
                emberFade.Alpha = alpha;
            }
        }
    }
}
