using System.Collections;
using UnityEngine;

namespace Kneel.UI
{
    public enum MenuCue
    {
        Move,
        Confirm,
        Back,
    }

    // Menu music and UI sounds. Every clip is optional: an empty slot simply plays nothing.
    public class MenuAudio : MonoBehaviour
    {
        private static MenuAudio instance;

        [Header("Clips (optional)")]
        [SerializeField] private AudioClip music;
        [SerializeField] private AudioClip moveClip;
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip backClip;

        [Header("Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Music")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.8f;
        [SerializeField] private float musicFadeIn = 3f;

        private Coroutine musicFade;

        public static void Play(MenuCue cue)
        {
            if (instance == null || instance.sfxSource == null)
            {
                return;
            }

            AudioClip clip = cue == MenuCue.Move ? instance.moveClip : cue == MenuCue.Confirm ? instance.confirmClip : instance.backClip;
            if (clip != null)
            {
                instance.sfxSource.PlayOneShot(clip);
            }
        }

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void StartMusic()
        {
            if (music == null || musicSource == null)
            {
                return;
            }

            musicSource.clip = music;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.Play();
            FadeMusic(musicVolume, musicFadeIn);
        }

        public void FadeOutMusic(float duration)
        {
            FadeMusic(0f, duration);
        }

        private void FadeMusic(float to, float duration)
        {
            if (musicSource == null || !musicSource.isPlaying)
            {
                return;
            }

            if (musicFade != null)
            {
                StopCoroutine(musicFade);
            }

            musicFade = StartCoroutine(FadeRoutine(to, duration));
        }

        private IEnumerator FadeRoutine(float to, float duration)
        {
            float from = musicSource.volume;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            musicSource.volume = to;
            musicFade = null;
        }
    }
}
