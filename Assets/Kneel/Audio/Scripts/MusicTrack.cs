using UnityEngine;
using UnityEngine.Audio;

namespace Kneel.Audio
{
    // A piece of score as two clips: an intro played once, then a loop body that repeats forever. The files are cut
    // so that intro -> loop is the original, uncut music and the loop's end runs straight into its own start (the
    // seam is crossfaded inside the file). Either half may be empty: intro only is a one-shot, loop only a plain loop.
    [System.Serializable]
    public class MusicTrack
    {
        public AudioClip intro;
        public AudioClip loop;

        public bool IsSet => intro != null || loop != null;

        public static MusicTrack Looping(AudioClip clip) => new MusicTrack { loop = clip };

        public static MusicTrack Once(AudioClip clip) => new MusicTrack { intro = clip };

        public bool Same(MusicTrack other) => other != null && other.intro == intro && other.loop == loop;
    }

    // Plays a MusicTrack on two sources: the intro, and the loop scheduled on the DSP clock to start on the intro's
    // last sample, so the join is sample-accurate. Volume is the owner's to set each frame.
    public class ScoreVoice
    {
        // Lead time for the first scheduled start, so both clips are queued before the mixer reaches it.
        private const double Lead = 0.1;

        private readonly AudioSource intro;
        private readonly AudioSource loop;
        private bool pending;
        private double introStart;
        private double loopStart;

        public MusicTrack Track { get; private set; }

        public bool IsPlaying => Track != null && (pending || intro.isPlaying || loop.isPlaying);

        public float Volume
        {
            get => intro.volume;
            set
            {
                intro.volume = value;
                loop.volume = value;
            }
        }

        public ScoreVoice(Transform parent, string name, AudioMixerGroup group)
        {
            intro = MakeSource(parent, name + "_Intro", group);
            loop = MakeSource(parent, name + "_Loop", group);
        }

        private static AudioSource MakeSource(Transform parent, string name, AudioMixerGroup group)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 0;
            source.bypassReverbZones = true;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }

        // Starts the track from its beginning (the clips are loaded first, then both starts are scheduled).
        public void Play(MusicTrack track)
        {
            Stop();
            if (track == null || track.IsSet == false)
            {
                return;
            }

            Track = track;
            intro.clip = track.intro;
            intro.loop = false;
            loop.clip = track.loop;
            loop.loop = true;
            Load(track.intro);
            Load(track.loop);
            pending = true;
            Tick();
        }

        public void Stop()
        {
            pending = false;
            Track = null;
            intro.Stop();
            loop.Stop();
            intro.clip = null;
            loop.clip = null;
        }

        // Call every frame: starts a pending track once its clips are in memory, and keeps the loop on the intro's end.
        public void Tick()
        {
            if (Track == null)
            {
                return;
            }

            if (pending)
            {
                if (Loaded(Track.intro) == false || Loaded(Track.loop) == false)
                {
                    return;
                }

                pending = false;
                double start = AudioSettings.dspTime + Lead;
                introStart = start;
                if (Track.intro != null)
                {
                    intro.PlayScheduled(start);
                    start += Length(Track.intro);
                }

                if (Track.loop != null)
                {
                    loop.PlayScheduled(start);
                    loopStart = start;
                }

                return;
            }

            // If the clock and the intro ever disagree (the player was suspended mid-intro, say), the loop would start
            // under the intro or after a gap: put it back on the intro's last sample.
            // timeSamples and dspTime both move in mixer blocks, hence the generous tolerance.
            if (Track.intro != null && Track.loop != null && intro.isPlaying && AudioSettings.dspTime > introStart + 0.5)
            {
                double end = AudioSettings.dspTime + (double)(intro.clip.samples - intro.timeSamples) / intro.clip.frequency;
                if (System.Math.Abs(end - loopStart) > 0.25)
                {
                    loop.Stop();
                    loop.PlayScheduled(end);
                    loopStart = end;
                }
            }
        }

        private static double Length(AudioClip clip) => (double)clip.samples / clip.frequency;

        private static void Load(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
        }

        private static bool Loaded(AudioClip clip) => clip == null || clip.loadState == AudioDataLoadState.Loaded || clip.loadState == AudioDataLoadState.Failed;
    }
}
