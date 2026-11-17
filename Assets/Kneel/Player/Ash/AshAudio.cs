using UnityEngine;

// Placeholder sounds for the Ash meter until the audio pass, synthesised once at startup. Any clip set in AshSettings
// is used instead.
public static class AshAudio
{
    private const int Rate = 44100;

    private static AudioClip full;
    private static AudioClip hum;
    private static AudioClip fizzle;
    private static AudioClip locked;
    private static AudioClip spend;
    private static AudioClip siphon;
    private static AudioClip unlock;
    private static AudioClip slam;
    private static AudioClip whoosh;
    private static AudioClip puff;

    // An ember catching, then a low swell: the meter is full and an ability can be cast.
    public static AudioClip Full(AshSettings s) => s.fullClip != null ? s.fullClip : full ??= Make("AshFull", 1.3f, FullSample);

    // Loops seamlessly: every partial is a whole number of cycles over its length.
    public static AudioClip Hum(AshSettings s) => s.humClip != null ? s.humClip : hum ??= Make("AshHum", 2f, HumSample);

    public static AudioClip Fizzle(AshSettings s) => s.fizzleClip != null ? s.fizzleClip : fizzle ??= Make("AshFizzle", 0.35f, FizzleSample);

    public static AudioClip Locked(AshSettings s) => s.lockedClip != null ? s.lockedClip : locked ??= Make("AshLocked", 0.12f, LockedSample);

    public static AudioClip Spend(AshSettings s) => s.spendClip != null ? s.spendClip : spend ??= Make("AshSpend", 0.45f, SpendSample);

    public static AudioClip Siphon(AshSettings s) => s.siphonClip != null ? s.siphonClip : siphon ??= Make("AshSiphon", 0.35f, SiphonSample);

    public static AudioClip Unlock(AshSettings s) => s.unlockClip != null ? s.unlockClip : unlock ??= Make("AshUnlock", 1.6f, UnlockSample);

    public static AudioClip Slam() => slam ??= Make("AshSlam", 0.8f, SlamSample);

    public static AudioClip Whoosh() => whoosh ??= Make("AshWhoosh", 0.4f, WhooshSample);

    public static AudioClip Puff() => puff ??= Make("AshPuff", 0.3f, PuffSample);

    private delegate float Sample(float t, float length, ref Noise noise);

    private static AudioClip Make(string name, float seconds, Sample sample)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        var data = new float[count];
        var noise = new Noise(name.GetHashCode());
        for (int i = 0; i < count; i++)
        {
            data[i] = Mathf.Clamp(sample(i / (float)Rate, seconds, ref noise), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Sine(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t);

    private static float FullSample(float t, float length, ref Noise n)
    {
        // Crackle of catching embers over the first half, under a swell at 55 and 110 Hz with a fifth above.
        float crackle = n.Crackle(0.004f + 0.02f * Mathf.Clamp01(1f - t * 2f)) * Mathf.Exp(-t * 3f) * 0.5f;
        float swell = Mathf.SmoothStep(0f, 1f, t / 0.35f) * Mathf.Exp(-Mathf.Max(0f, t - 0.35f) * 2.2f);
        float tone = Sine(t, 55f) * 0.55f + Sine(t, 110f) * 0.35f + Sine(t, 165f) * 0.15f + n.Smooth(0.02f) * 0.2f;
        return crackle + tone * swell * 0.7f;
    }

    private static float HumSample(float t, float length, ref Noise n)
    {
        float tone = Sine(t, 60f) * 0.5f + Sine(t, 120f) * 0.3f + Sine(t, 180.5f) * 0.08f;
        float beat = 0.85f + 0.15f * Sine(t, 1.5f);
        return tone * beat * 0.5f;
    }

    private static float FizzleSample(float t, float length, ref Noise n)
    {
        return n.Crackle(0.08f) * Mathf.Exp(-t * 12f) * 0.7f + n.White() * Mathf.Exp(-t * 25f) * 0.2f;
    }

    private static float LockedSample(float t, float length, ref Noise n)
    {
        return Sine(t, 90f - t * 200f) * Mathf.Exp(-t * 40f) * 0.6f;
    }

    private static float SpendSample(float t, float length, ref Noise n)
    {
        float crack = n.White() * Mathf.Exp(-t * 30f) * 0.6f;
        float thump = Sine(t, 70f - t * 60f) * Mathf.Exp(-t * 9f) * 0.7f;
        return crack + thump;
    }

    private static float SiphonSample(float t, float length, ref Noise n)
    {
        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length));
        return n.Smooth(0.15f) * env * env * 0.5f;
    }

    private static float UnlockSample(float t, float length, ref Noise n)
    {
        float env = Mathf.SmoothStep(0f, 1f, t / 0.2f) * Mathf.Exp(-t * 1.6f);
        return (Sine(t, 41.2f) * 0.6f + Sine(t, 82.4f) * 0.4f + Sine(t, 123.6f) * 0.2f) * env * 0.8f;
    }

    private static float SlamSample(float t, float length, ref Noise n)
    {
        float hit = n.White() * Mathf.Exp(-t * 20f) * 0.6f;
        float rumble = (Sine(t, 45f - t * 20f) * 0.7f + n.Smooth(0.01f) * 0.5f) * Mathf.Exp(-t * 4f);
        return hit + rumble * 0.8f;
    }

    private static float WhooshSample(float t, float length, ref Noise n)
    {
        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length));
        return n.Smooth(0.35f * env + 0.05f) * env * 0.7f;
    }

    private static float PuffSample(float t, float length, ref Noise n)
    {
        return n.Smooth(0.25f) * Mathf.Exp(-t * 14f) * 0.8f;
    }

    // A small deterministic noise source: white, low-passed (higher `amount` is brighter), and sparse crackle.
    private struct Noise
    {
        private uint state;
        private float smooth;

        public Noise(int seed)
        {
            state = (uint)seed | 1u;
            smooth = 0f;
        }

        public float White()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / (float)0x800000 - 1f;
        }

        public float Smooth(float amount)
        {
            smooth += (White() - smooth) * amount;
            return smooth * 2f;
        }

        public float Crackle(float chance)
        {
            float w = White();
            return (w * 0.5f + 0.5f) < chance ? White() : 0f;
        }
    }
}
