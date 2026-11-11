using System.Collections.Generic;
using UnityEngine;

// The darkness wave (The Master.md §8; the transition, P2 every 35-45 s and after a completed Gather): the torches
// gutter in a wave from the grand doors to the throne over 1.5 s and the hall stays dark for 8 s. Torches, sconces,
// candelabras and chandeliers fade to 0; the fixtures' own glow and the ambient light to 25%. Fire, his cracks and
// tells, Kael's lantern and scar stay lit, and his glints and embers are ×1.5 brighter (BossTell.DarkBoost): the
// dark never hides a tell. Everything animates; nothing toggles. The hall comes back the same way.
public class DarknessWave : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public Vector3 doors = new Vector3(54f, 9f, 407.5f);
    public Rect hall = Rect.MinMaxRect(31f, 405f, 77f, 476f);
    public float waveTime = 1.5f;
    public float fadeEach = 0.4f;
    public float holdTime = 8f;
    public float fixtureLevel = 0.25f;
    public float ambientLevel = 0.25f;
    public float flameLevel = 0.15f;
    public float tellBoost = 1.5f;

    // 0 lit, 1 dark (at the throne end; the wave runs ahead of it from the doors).
    public float Darkness { get; private set; }

    public bool Active => started >= 0f;

    private struct Lamp
    {
        public Light light;
        public Kneel.Lighting.LightFlicker flicker;
        public float intensity;
        public float level;
        public float delay;
    }

    private struct Flame
    {
        public Transform transform;
        public Vector3 scale;
        public float delay;
    }

    private readonly List<Lamp> lamps = new List<Lamp>();
    private readonly List<Flame> flames = new List<Flame>();
    private readonly Dictionary<Light, int> lampIndex = new Dictionary<Light, int>();
    private readonly Dictionary<Light, float> levels = new Dictionary<Light, float>();
    private float started = -1f;
    private float hold;
    private float ambientIntensity;
    private Color ambientSky, ambientEquator, ambientGround, ambientFlat;
    private bool gathered;

    // The hall's lights and flames, found once (a level's fixtures don't change; the fight's own fires and lights
    // are left out).
    private void Gather()
    {
        gathered = true;
        lamps.Clear();
        lampIndex.Clear();
        flames.Clear();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional || Inside(light.transform) == false || Excluded(light.transform))
                continue;
            lamps.Add(new Lamp
            {
                light = light,
                flicker = light.GetComponent<Kneel.Lighting.LightFlicker>(),
                intensity = light.intensity,
                level = light.name.Contains("FixtureGlow") ? fixtureLevel : 0f,
                delay = Delay(light.transform.position),
            });
            lampIndex[light] = lamps.Count - 1;
        }

        foreach (ParticleSystemRenderer r in FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
        {
            if (Inside(r.transform) == false || Excluded(r.transform) || r.sharedMaterial == null)
                continue;
            string m = r.sharedMaterial.name;
            if (m.Contains("Flame") == false && m.Contains("Halo") == false && m.Contains("Glow") == false)
                continue;
            flames.Add(new Flame { transform = r.transform, scale = r.transform.localScale, delay = Delay(r.transform.position) });
        }

        ambientIntensity = RenderSettings.ambientIntensity;
        ambientSky = RenderSettings.ambientSkyColor;
        ambientEquator = RenderSettings.ambientEquatorColor;
        ambientGround = RenderSettings.ambientGroundColor;
        ambientFlat = RenderSettings.ambientLight;
    }

    private bool Inside(Transform t) => hall.Contains(new Vector2(t.position.x, t.position.z));

    private static bool Excluded(Transform t)
    {
        return t.GetComponentInParent<FloorFire>() != null || t.GetComponentInParent<MasterP1>() != null
            || t.GetComponentInParent<PlayerCombat>() != null || t.GetComponentInParent<BossTell>() != null
            || t.GetComponentInParent<HallSeal>() != null
            // Braziers are fire (fire stays lit in the dark), and they put their own light out when they go over.
            || t.GetComponentInParent<BrazierTopple>() != null;
    }

    private float Delay(Vector3 p) => Mathf.Clamp01((p.z - doors.z) / Mathf.Max(hall.yMax - doors.z, 1f)) * waveTime;

    // The wave starts now; the dark holds `seconds` (default holdTime) once it reaches the throne.
    public void Begin(float seconds = -1f)
    {
        if (gathered == false)
            Gather();
        started = Time.time;
        hold = seconds > 0f ? seconds : holdTime;
    }

    private void Update()
    {
        if (started < 0f)
            return;

        float t = Time.time - started;
        float back = waveTime + fadeEach + hold;
        foreach (Lamp lamp in lamps)
        {
            if (lamp.light == null)
                continue;
            float level = Mathf.Lerp(1f, lamp.level, Amount(t, lamp.delay, back));
            levels[lamp.light] = level;

            // A flickering torch sets its own intensity every frame: it's dimmed through its flicker instead.
            if (lamp.flicker != null && lamp.flicker.enabled)
                lamp.flicker.Dim = level;
            else
                lamp.light.intensity = lamp.intensity * level;
        }

        foreach (Flame flame in flames)
        {
            if (flame.transform != null)
                flame.transform.localScale = flame.scale * Mathf.Lerp(1f, flameLevel, Amount(t, flame.delay, back));
        }

        Darkness = Amount(t, waveTime, back);
        float ambient = Mathf.Lerp(1f, ambientLevel, Darkness);
        RenderSettings.ambientIntensity = ambientIntensity * ambient;
        RenderSettings.ambientSkyColor = ambientSky * ambient;
        RenderSettings.ambientEquatorColor = ambientEquator * ambient;
        RenderSettings.ambientGroundColor = ambientGround * ambient;
        RenderSettings.ambientLight = ambientFlat * ambient;
        BossTell.DarkBoost = Mathf.Lerp(1f, tellBoost, Darkness);

        if (t > back + waveTime + fadeEach)
            Restore();
    }

    // 0 to 1 and back for a light whose wave reaches it after `delay`; the return runs in the same order from `back`.
    private float Amount(float t, float delay, float back)
    {
        float down = Mathf.Clamp01((t - delay) / fadeEach);
        float up = Mathf.Clamp01((t - back - delay) / fadeEach);
        return Mathf.SmoothStep(0f, 1f, down) * (1f - Mathf.SmoothStep(0f, 1f, up));
    }

    // A script that animates one of the hall's lights itself (a falling chandelier's) tells the wave the light's new
    // undimmed intensity, so the wave dims and restores that one and not the one it found when it gathered the hall:
    // a chandelier that fell in the dark came back at its 12 m hanging brightness, on the floor. Returns the light's
    // current dim level (1 when the hall is lit), for the caller to apply.
    public float Rebase(Light light, float intensity)
    {
        if (light != null && lampIndex.TryGetValue(light, out int i) && i < lamps.Count && lamps[i].light == light)
        {
            Lamp lamp = lamps[i];
            lamp.intensity = intensity;
            lamps[i] = lamp;
        }

        return light != null && levels.TryGetValue(light, out float level) ? level : 1f;
    }

    private void Restore()
    {
        started = -1f;
        levels.Clear();
        Darkness = 0f;
        foreach (Lamp lamp in lamps)
        {
            if (lamp.flicker != null)
                lamp.flicker.Dim = 1f;
            else if (lamp.light != null)
                lamp.light.intensity = lamp.intensity;
        }

        foreach (Flame flame in flames)
        {
            if (flame.transform != null)
                flame.transform.localScale = flame.scale;
        }

        if (gathered)
        {
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientLight = ambientFlat;
        }

        BossTell.DarkBoost = 1f;
    }

    public void ResetState()
    {
        if (started >= 0f)
            Restore();
    }

    private void OnDisable() => ResetState();
}
