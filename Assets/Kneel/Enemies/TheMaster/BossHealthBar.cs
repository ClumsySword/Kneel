using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The boss bar (The Master.md §10; owner, 2026-11-10: top centre, as premium as the main and pause menus). "THE
// MASTER" in Cormorant Garamond between ember rules and diamonds; under it a long, thin ember bar between hairline
// rules: a deep ember fill with a flickering ember seam, a hot leading edge that sheds sparks, and a pale trail that
// catches up after damage. The same name in both phases. The prefab is built by MasterSetup (Kneel/Enemies/The
// Master/Build Boss Bar) in the menu builder's style; it draws on its own overlay canvas, so level post-processing
// never touches it, and its sparks are UI images (particles don't render on overlay canvases).
// Under it, the stagger meter (owner, 2026-11-15): a short gold bar between hairlines that fills out from the centre as
// his poise goes. When it's full he's staggered, and it turns ember red and drains back to the centre over the window,
// the only time the player's blows take his health.
public class BossHealthBar : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private Image fill;
    [SerializeField] private Image seam;
    [SerializeField] private Image trail;
    [SerializeField] private Image flash;
    [SerializeField] private RectTransform edge;
    [SerializeField] private Image[] flicker = new Image[0];
    [SerializeField] private Kneel.UI.UISparks edgeSparks;
    [SerializeField] private float width = 1100f;
    [SerializeField] private CanvasGroup staggerGroup;
    // Each pair: the left half (filling leftward from the centre) and the right half.
    [SerializeField] private Image[] staggerFill = new Image[0];
    [SerializeField] private Image[] staggerTrail = new Image[0];
    [SerializeField] private Image[] staggerHot = new Image[0];
    [SerializeField] private Color staggerGold = new Color(0.85f, 0.64f, 0.25f);
    [SerializeField] private Color staggerBroken = new Color(1f, 0.38f, 0.14f);
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.22f;
    [SerializeField] private float flickerSpeed = 2.2f;

    private float shown;
    private float target;
    private float fillFrom, fillTo, fillStart = -1f, fillLength;
    private float visible;
    private bool wantVisible;
    private float flashLeft;
    private float[] flickerBase;
    private float staggerTarget, staggerShown, staggerTrailValue, staggerHotLeft;
    private float staggerWindow = -1f;

    public static BossHealthBar Spawn(BossHealthBar prefab, string name)
    {
        BossHealthBar bar = Instantiate(prefab);
        bar.name = prefab.name;
        if (string.IsNullOrEmpty(name) == false && bar.title != null)
            bar.title.text = name;
        return bar;
    }

    private void Awake()
    {
        group.alpha = 0f;
        flickerBase = new float[flicker.Length];
        for (int i = 0; i < flicker.Length; i++)
            flickerBase[i] = flicker[i] != null ? flicker[i].color.a : 0f;
        Apply(0f, 0f);
    }

    public void Show() => wantVisible = true;

    public void Hide() => wantVisible = false;

    // Fills (or refills, with an ember sweep) from `from` to `to` over `seconds`.
    public void FillTo(float from, float to, float seconds)
    {
        fillFrom = from;
        fillTo = to;
        fillStart = Time.time;
        fillLength = Mathf.Max(seconds, 0.01f);
        shown = from;
        target = to;
    }

    public void Set(float fraction)
    {
        float next = Mathf.Clamp01(fraction);
        if (next < target - 0.0001f)
            flashLeft = 0.18f;
        target = next;
    }

    public bool Filling => fillStart >= 0f;

    // The stagger meter: `fill` (0-1) how near he is to staggering; `windowLeft` the share of a stagger still to come,
    // or below 0 when he isn't staggered.
    public void SetStagger(float fill, float windowLeft)
    {
        if (windowLeft >= 0f && staggerWindow < 0f)
            staggerHotLeft = 0.35f;
        staggerWindow = windowLeft;
        staggerTarget = Mathf.Clamp01(fill);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        visible = Mathf.MoveTowards(visible, wantVisible ? 1f : 0f, dt * 2f);
        group.alpha = visible;

        float trailValue;
        if (fillStart >= 0f)
        {
            float t = Mathf.Clamp01((Time.time - fillStart) / fillLength);
            shown = Mathf.Lerp(fillFrom, fillTo, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f)
                fillStart = -1f;
            trailValue = shown;
        }
        else
        {
            shown = target;

            // The trail waits a moment, then drains to the new value.
            trailValue = trail.fillAmount > shown ? Mathf.MoveTowards(trail.fillAmount, shown, Time.deltaTime * 0.35f) : shown;
        }

        Apply(shown, trailValue);
        UpdateStagger(dt);

        // A hit flashes the bar white-hot for an instant.
        flashLeft = Mathf.Max(0f, flashLeft - dt);
        if (flash != null)
        {
            flash.fillAmount = shown;
            Color c = flash.color;
            c.a = flashLeft / 0.18f * 0.3f;
            flash.color = c;
        }

        // The seam and the edge glow breathe like the menus' ember highlight.
        float time = Time.unscaledTime;
        for (int i = 0; i < flicker.Length; i++)
        {
            if (flicker[i] == null)
                continue;
            float n = Mathf.PerlinNoise(i * 7.31f, time * flickerSpeed);
            Color c = flicker[i].color;
            c.a = flickerBase[i] * (1f - flickerAmount + flickerAmount * 2f * n);
            flicker[i].color = c;
        }
    }

    private void UpdateStagger(float dt)
    {
        if (staggerFill.Length == 0)
            return;

        bool broken = staggerWindow >= 0f;
        float value;
        if (broken)
        {
            value = staggerWindow;
            staggerShown = value;
            staggerTrailValue = value;
        }
        else
        {
            // Rises quickly to a hit; the pale trail drains behind it as his poise comes back.
            staggerShown = staggerTarget > staggerShown ? Mathf.MoveTowards(staggerShown, staggerTarget, dt * 4f) : staggerTarget;
            staggerTrailValue = staggerTrailValue > staggerShown ? Mathf.MoveTowards(staggerTrailValue, staggerShown, dt * 0.5f) : staggerShown;
            value = staggerShown;
        }

        float pulse = broken ? 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;
        Color colour = broken ? staggerBroken * pulse : Color.Lerp(staggerGold, new Color(1f, 0.82f, 0.45f), Mathf.InverseLerp(0.75f, 1f, value));
        colour.a = 1f;
        foreach (Image image in staggerFill)
        {
            image.fillAmount = value;
            image.color = colour;
        }

        foreach (Image image in staggerTrail)
            image.fillAmount = staggerTrailValue;

        staggerHotLeft = Mathf.Max(0f, staggerHotLeft - dt);
        foreach (Image image in staggerHot)
        {
            image.fillAmount = broken ? 1f : value;
            Color c = image.color;
            c.a = staggerHotLeft / 0.35f * 0.9f;
            image.color = c;
        }

        if (staggerGroup != null)
            staggerGroup.alpha = 1f;
    }

    private void Apply(float value, float trailValue)
    {
        fill.fillAmount = value;
        if (seam != null)
            seam.fillAmount = value;
        trail.fillAmount = trailValue;
        if (edge != null)
        {
            edge.anchoredPosition = new Vector2((value - 0.5f) * width, edge.anchoredPosition.y);
            edge.gameObject.SetActive(value > 0.001f && value < 0.999f || Filling);
        }

        if (edgeSparks != null)
            edgeSparks.enabled = value > 0.001f;
    }
}
