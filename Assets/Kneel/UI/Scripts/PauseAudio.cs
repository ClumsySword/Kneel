using System.Collections.Generic;
using UnityEngine;

namespace Kneel.UI
{
    // The pause menu's effect on the level's sound. Music and ambience keep playing, muffled by a low-pass filter
    // added to their sources; every other sound (one-shot effects) pauses until the game resumes.
    // Music and ambience are sources routed to a mixer group named Music or Ambience, or unrouted looping sources.
    public class PauseAudio
    {
        private const float OpenCutoff = 22000f;

        private readonly List<AudioLowPassFilter> filters = new List<AudioLowPassFilter>();
        private readonly List<AudioSource> paused = new List<AudioSource>();
        private float pausedCutoff = 800f;

        // Takes over the playing sources, except those under ignore (the menu's own sounds). Starts unfiltered.
        public void Capture(Transform ignore, float cutoff)
        {
            Release();
            pausedCutoff = cutoff;
            var filtered = new HashSet<GameObject>();
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            {
                if (!source.isPlaying || source.transform.IsChildOf(ignore))
                {
                    continue;
                }

                if (!IsMusicOrAmbience(source))
                {
                    source.Pause();
                    paused.Add(source);
                }
                else if (filtered.Add(source.gameObject))
                {
                    // A filter acts on every source on its object, so one per object.
                    var filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                    filter.cutoffFrequency = OpenCutoff;
                    filters.Add(filter);
                }
            }
        }

        // 0 = open, 1 = fully muffled; logarithmic in between, so the sweep sounds even.
        public void SetAmount(float amount)
        {
            float cutoff = OpenCutoff * Mathf.Pow(pausedCutoff / OpenCutoff, Mathf.Clamp01(amount));
            foreach (AudioLowPassFilter filter in filters)
            {
                if (filter != null)
                {
                    filter.cutoffFrequency = cutoff;
                }
            }
        }

        public void Release()
        {
            foreach (AudioLowPassFilter filter in filters)
            {
                if (filter != null)
                {
                    Object.Destroy(filter);
                }
            }

            foreach (AudioSource source in paused)
            {
                if (source != null)
                {
                    source.UnPause();
                }
            }

            filters.Clear();
            paused.Clear();
        }

        private static bool IsMusicOrAmbience(AudioSource source)
        {
            if (source.outputAudioMixerGroup == null)
            {
                return source.loop;
            }

            string group = source.outputAudioMixerGroup.name;
            return group == "Music" || group == "Ambience";
        }
    }
}
