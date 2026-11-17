using UnityEngine;
using UnityEngine.Audio;

// The boss fight's score (The Master.md §10, open call 6): four slots on the Music mixer group, crossfaded as the
// fight moves on. Phase 1 and Phase 2 loop; the transition's and the finisher's held notes don't. Every slot is empty
// until the real tracks arrive, and an empty slot is silence (the previous track fades out under it).
public class MusicDirector : MonoBehaviour
{
    public enum Slot { None, Phase1, Transition, Phase2, Finisher }

    public AudioClip phase1;
    public AudioClip transitionNote;
    public AudioClip phase2;
    public AudioClip finisherNote;
    public float volume = 0.8f;
    public float crossfade = 1.5f;

    public Slot Current { get; private set; } = Slot.None;

    private AudioSource a;
    private AudioSource b;
    private bool aActive;
    private float fadeStart = -1f;

    private void Awake()
    {
        a = MakeSource("MusicA");
        b = MakeSource("MusicB");
    }

    private AudioSource MakeSource(string sourceName)
    {
        var source = new GameObject(sourceName).AddComponent<AudioSource>();
        source.transform.SetParent(transform, false);
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        var mixer = Resources.Load<AudioMixer>("KneelMixer");
        AudioMixerGroup[] music = mixer != null ? mixer.FindMatchingGroups("Music") : null;
        if (music != null && music.Length > 0)
            source.outputAudioMixerGroup = music[0];
        return source;
    }

    public void Play(Slot slot)
    {
        if (slot == Current)
            return;
        Current = slot;
        AudioClip clip = slot == Slot.Phase1 ? phase1 : slot == Slot.Transition ? transitionNote : slot == Slot.Phase2 ? phase2 : slot == Slot.Finisher ? finisherNote : null;
        AudioSource next = aActive ? b : a;
        aActive = !aActive;
        next.Stop();
        next.clip = clip;
        next.loop = slot == Slot.Phase1 || slot == Slot.Phase2;
        if (clip != null)
            next.Play();
        fadeStart = Time.unscaledTime;
    }

    public void Stop() => Play(Slot.None);

    private void Update()
    {
        if (fadeStart < 0f)
            return;
        float t = Mathf.Clamp01((Time.unscaledTime - fadeStart) / Mathf.Max(crossfade, 0.01f));
        AudioSource now = aActive ? a : b;
        AudioSource old = aActive ? b : a;
        now.volume = volume * t;
        old.volume = Mathf.Min(old.volume, volume * (1f - t));
        if (t >= 1f)
        {
            old.Stop();
            fadeStart = -1f;
        }
    }
}
