using UnityEngine;
using UnityEngine.Audio;

// The Master's tells, one grammar for both phases (The Master.md §1). Blows carry no light (owner, 2026-11-11: "instead
// of glows on attacks"): the pose and the sound tell them, a rasp and a high bell = parry, an inhaled drone = roll.
// Spells charge in his cracks instead: from the first frame of a spell's tell, the ember cracks of whichever body he's
// in brighten until it lands, then settle (Charge). Where a spell lands is drawn on the floor (SpellMark). The palm
// light is the Command's (a real shadow-casting light: what its shadow covers is safe); it lights the hall, not him.
// Sounds are slots that MasterSetup fills with the placeholder clips (TheMaster/Audio), on the SFX mixer group.
public class BossTell : MonoBehaviour
{
    private static readonly int GlintColorId = Shader.PropertyToID("_KneelGlintColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Rig")]
    public Transform leftHand;
    // Where the Command's shadow-casting light sits (P1: the left palm; the Colossus: his split mask). Defaults to leftHand.
    public Transform palmLightAnchor;
    public Transform swordTip;
    public float bodyHeight = 4.2f;

    [Header("Look")]
    public Color emberColor = new Color(1f, 0.32f, 0.08f);
    public Color palmColor = new Color(1f, 0.55f, 0.25f);
    public float palmLightIntensity = 60f;
    public float palmLightRange = 40f;
    // The cracks' resting glow (the material's emission intensity); darkness and phase scale it (EmberScale).
    public float crackGlow = 2.5f;
    // A spell's charge: the cracks climb to crackGlow x chargePeak by the moment it lands, with a slow pulse that
    // quickens as it nears, hold a beat, then settle over chargeFade.
    public float chargePeak = 2.2f;
    public float chargePulse = 0.08f;
    public float chargeFade = 0.6f;

    [Header("Sounds (placeholders)")]
    public AudioClip rasp;
    public AudioClip bell;
    public AudioClip drone;
    public AudioClip deepDrone;
    public AudioClip hiss;
    public AudioClip hum;
    public AudioClip impact;
    public AudioClip deflect;

    // Multiplies the crack glow and every tell (1 normal; the darkness wave raises it so darkness never hides a tell).
    public float EmberScale { get; set; } = 1f;

    // The darkness wave makes his cracks brighter (§8: the dark never hides a tell).
    public static float DarkBoost = 1f;

    private AudioSource source;
    private AudioSource loop;
    private Light palmLight;
    private Renderer[] bodyRenderers;
    private MaterialPropertyBlock block;

    private float palmStart = -1f, palmLength;
    private float chargeStart = -1f, chargeLength, chargeEnd = -1f, chargeFrom = 1f;

    // The cracks' current charge (1 at rest, chargePeak as a spell lands), for the playtest.
    public float ChargeLevel { get; private set; } = 1f;

    public Vector3 PalmLightPosition => palmLight != null ? palmLight.transform.position : transform.position + Vector3.up * bodyHeight;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0.6f;
        source.playOnAwake = false;
        source.minDistance = 6f;
        source.maxDistance = 60f;
        loop = gameObject.AddComponent<AudioSource>();
        loop.spatialBlend = 0.6f;
        loop.playOnAwake = false;
        loop.loop = true;
        var mixer = Resources.Load<AudioMixer>("KneelMixer");
        AudioMixerGroup[] sfx = mixer != null ? mixer.FindMatchingGroups("SFX") : null;
        if (sfx != null && sfx.Length > 0)
        {
            source.outputAudioMixerGroup = sfx[0];
            loop.outputAudioMixerGroup = sfx[0];
        }

        bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        block = new MaterialPropertyBlock();

        palmLight = MakeLight("PalmLight", palmLightAnchor != null ? palmLightAnchor : leftHand, palmColor, palmLightRange, true);
        palmLight.transform.localPosition = Vector3.zero;
    }

    private void OnDisable() => Shader.SetGlobalVector(GlintColorId, Vector4.zero);

    private Light MakeLight(string name, Transform parent, Color color, float range, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : transform, false);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = 0f;
        // The hall and Kael only (the Default layer, a code light's own): at his palm it would burn a hot patch into
        // his own arm and chest, and his body's only light is his cracks.
        // Only the Command's palm light casts shadows, and only while it's lit (the L5 atlas lesson: one at a time).
        light.shadows = LightShadows.None;
        light.enabled = false;
        if (shadows)
        {
            light.shadowStrength = 1f;
            light.shadowBias = 0.05f;
        }

        return light;
    }

    // --- Tells ---

    // Parry: the steel rasp, then the high bell. No light: the raised blade is the tell.
    public void Glint(float duration)
    {
        Play(rasp, 0.7f);
        Play(bell, 0.9f);
    }

    // Roll: an inhaled drone rises under the wind-up.
    public void Ember(float duration) => Loop(drone, duration);

    // A spell's charge: his cracks brighten from now until it lands `duration` from now, then settle.
    public void Charge(float duration)
    {
        chargeStart = Time.time;
        chargeLength = Mathf.Max(duration, 0.1f);
        chargeEnd = -1f;
    }

    // The Command: ember light swells in the palm and lights the hall (hard shadows), a deep drone under it. It's lit
    // from the first frame, so the safe shadows are drawn the whole tell, and the cracks charge with it.
    public void Palm(float duration)
    {
        palmStart = Time.time;
        palmLength = Mathf.Max(duration, 0.1f);
        palmLight.enabled = true;
        palmLight.shadows = LightShadows.Hard;
        Loop(deepDrone, duration);
        Charge(duration);
    }

    // Threads: a hiss as the hand flicks.
    public void Hiss(float duration) => Play(hiss, 0.9f);

    // The Regal Guard's low hum.
    public void Hum(float duration) => Loop(hum, duration);

    // The Command's release.
    public void Bell() => Play(bell, 1f);

    public void Deflect()
    {
        Play(deflect, 1f);
        if (swordTip != null)
            HitSpark.Spawn(swordTip.position, Vector3.up, new Color(1f, 0.85f, 0.5f), 14);
    }

    public void GuardSpark(Vector3 point)
    {
        HitSpark.Spawn(point, Vector3.up, new Color(1f, 0.85f, 0.5f), 18);
        Play(deflect, 0.8f);
    }

    public void GuardBroken()
    {
        HitSpark.Spawn(swordTip != null ? swordTip.position : transform.position + Vector3.up * 3f, Vector3.up, new Color(1f, 0.7f, 0.3f), 40);
        Play(impact, 1f);
    }

    public void Kneel() => Play(impact, 0.6f);

    public void Impact(Vector3 point)
    {
        HitSpark.Spawn(point, Vector3.up, emberColor, 40);
        Play(impact, 1f);
    }

    public void StopTells()
    {
        palmStart = -1f;
        palmLight.enabled = false;
        palmLight.shadows = LightShadows.None;
        loop.Stop();
        // A charge cut short (a broken Gather, a stagger) settles from wherever it got to.
        if (chargeStart >= 0f)
            SettleCharge();
        Shader.SetGlobalVector(GlintColorId, Vector4.zero);
    }

    private void SettleCharge()
    {
        chargeFrom = ChargeLevel;
        chargeStart = -1f;
        chargeEnd = Time.time;
    }

    private float Charged()
    {
        if (chargeStart >= 0f)
        {
            float elapsed = Time.time - chargeStart;
            if (elapsed >= chargeLength)
            {
                // Landed: hold the peak a beat, then settle.
                if (elapsed - chargeLength >= 0.12f)
                {
                    chargeFrom = chargePeak;
                    chargeStart = -1f;
                    chargeEnd = Time.time;
                }
                return chargePeak;
            }

            // Eased in, so the climb reads as building toward the release; a pulse that quickens as it nears.
            float t = elapsed / chargeLength;
            float k = t * t * (3f - 2f * t);
            float pulse = 1f + chargePulse * k * Mathf.Sin(elapsed * Mathf.Lerp(6f, 22f, t));
            return Mathf.Lerp(1f, chargePeak, k) * pulse;
        }

        if (chargeEnd >= 0f)
        {
            float t = (Time.time - chargeEnd) / Mathf.Max(chargeFade, 0.05f);
            if (t >= 1f)
            {
                chargeEnd = -1f;
                return 1f;
            }

            return Mathf.Lerp(chargeFrom, 1f, t * t * (3f - 2f * t));
        }

        return 1f;
    }

    private void Update()
    {
        float scale = EmberScale * DarkBoost;

        // Palm light: lit from the start of the Command's tell (the shadows are readable throughout), swelling to full.
        if (palmStart >= 0f)
        {
            float t = Mathf.Clamp01((Time.time - palmStart) / palmLength);
            palmLight.intensity = palmLightIntensity * scale * Mathf.Lerp(0.3f, 1f, t * t);
            if (Time.time - palmStart > palmLength + 0.25f)
            {
                palmStart = -1f;
                palmLight.enabled = false;
                palmLight.shadows = LightShadows.None;
            }
        }

        // The cracks: their resting glow, climbing while a spell charges.
        ChargeLevel = Charged();
        float glow = crackGlow * scale * ChargeLevel;
        foreach (Renderer r in bodyRenderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, Color.white * glow);
            r.SetPropertyBlock(block);
        }
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null)
            source.PlayOneShot(clip, volume);
    }

    private void Loop(AudioClip clip, float duration)
    {
        if (clip == null)
            return;
        loop.clip = clip;
        loop.Play();
        loop.SetScheduledEndTime(AudioSettings.dspTime + duration + 0.2f);
    }

    // True when nothing solid stands between the two points (his body and the player don't count). The Command's test.
    public static bool LineOfSight(Vector3 from, Vector3 to, Transform ignoreA, Transform ignoreB)
    {
        int mask = ~(LayerMask.GetMask("Enemy", "Player", "Ignore Raycast", "UI"));
        Vector3 d = to - from;
        RaycastHit[] hits = Physics.RaycastAll(from, d.normalized, d.magnitude, mask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.collider.transform;
            if ((ignoreA != null && t.IsChildOf(ignoreA)) || (ignoreB != null && t.IsChildOf(ignoreB)))
                continue;
            return false;
        }

        return true;
    }
}
