using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The boss bar (The Master.md §10; owner, 2026-11-10: top centre, as premium as the main and pause menus). "THE
// MASTER" in Cormorant Garamond between ember rules and diamonds; under it a long, thin ember bar between hairline
// rules: a deep ember fill with a flickering ember seam, a hot leading edge that sheds sparks, and a pale trail that
// catches up after damage. The same name in both phases. The prefab is built by MasterSetup (Kneel/Enemies/The
// Master/Build Boss Bar) in the menu builder's style; it draws on its own overlay canvas, so level post-processing
// never touches it, and its sparks are UI images (particles don't render on overlay canvases).
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
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.22f;
    [SerializeField] private float flickerSpeed = 2.2f;

    private float shown;
    private float target;
    private float fillFrom, fillTo, fillStart = -1f, fillLength;
    private float visible;
    private bool wantVisible;
    private float flashLeft;
    private float[] flickerBase;

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
