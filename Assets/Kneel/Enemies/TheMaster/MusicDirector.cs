using Kneel.Audio;
using UnityEngine;
using UnityEngine.Audio;

// The boss fight's score (The Master.md §10, open call 6): four slots on the Music mixer group, crossfaded as the
// fight moves on. Phase 1 and Phase 2 loop; the transition's and the finisher's held notes don't. An empty slot is
// silence (the previous track fades out under it).
// A phase's score (phase1Score / phase2Score: an intro, then a loop body) takes precedence over its single clip; the
// level's LevelAudio fills both from the SoundBank, and lends the combat drums to any slot still empty.
public class MusicDirector : MonoBehaviour
{
    public enum Slot { None, Phase1, Transition, Phase2, Finisher }

    public AudioClip phase1;
    public AudioClip transitionNote;
    public AudioClip phase2;
    public AudioClip finisherNote;
    public MusicTrack phase1Score = new MusicTrack();
    public MusicTrack phase2Score = new MusicTrack();
    public float volume = 0.8f;
    public float crossfade = 1.5f;

    public Slot Current { get; private set; } = Slot.None;

    // Three voices, so a change of slot in the middle of a crossfade (a skipped Unmaking) never cuts the track still
    // fading out: the new track takes the quietest voice.
    private readonly ScoreVoice[] voices = new ScoreVoice[3];
    private int current = -1;

    private void Awake()
    {
        var mixer = Resources.Load<AudioMixer>("KneelMixer");
        AudioMixerGroup[] music = mixer != null ? mixer.FindMatchingGroups("Music") : null;
        AudioMixerGroup group = music != null && music.Length > 0 ? music[0] : null;
        for (int i = 0; i < voices.Length; i++)
            voices[i] = new ScoreVoice(transform, "Music" + (char)('A' + i), group);
    }

    public void Play(Slot slot)
    {
        if (slot == Current)
            return;
        Current = slot;
        int next = -1;
        for (int i = 0; i < voices.Length; i++)
        {
            if (i != current && (next < 0 || voices[i].Volume < voices[next].Volume))
                next = i;
        }

        current = next;
        voices[next].Volume = 0f;
        voices[next].Play(TrackFor(slot));
    }

    public void Stop() => Play(Slot.None);

    private MusicTrack TrackFor(Slot slot)
    {
        switch (slot)
        {
            case Slot.Phase1:
                return phase1Score != null && phase1Score.IsSet ? phase1Score : MusicTrack.Looping(phase1);
            case Slot.Phase2:
                return phase2Score != null && phase2Score.IsSet ? phase2Score : MusicTrack.Looping(phase2);
            case Slot.Transition:
                return MusicTrack.Once(transitionNote);
            case Slot.Finisher:
                return MusicTrack.Once(finisherNote);
            default:
                return null;
        }
    }

    // The current voice fades up to volume and every other one down, over crossfade seconds; a silent one stops.
    private void Update()
    {
        float step = volume * Time.unscaledDeltaTime / Mathf.Max(crossfade, 0.01f);
        for (int i = 0; i < voices.Length; i++)
        {
            ScoreVoice voice = voices[i];
            voice.Tick();
            if (voice.Track == null)
                continue;
            voice.Volume = Mathf.MoveTowards(voice.Volume, i == current ? volume : 0f, step);
            if (i != current && voice.Volume <= 0f)
                voice.Stop();
        }
    }
}
