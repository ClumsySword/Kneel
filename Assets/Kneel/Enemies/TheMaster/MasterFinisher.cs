using System.Collections;
using Kneel.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Last Command (The Master.md §7). MasterBoss runs it when the Colossus falls.
// 1. He's down on both knees. Ash sloughs off him; the light in the split mask dims. From here Kael takes no damage
//    from anything; the ash storm fades; the fire still burns and blocks paths but hurts no one.
// 2. He raises the claw, palm out, one last time (3.5 s). No line of sight now: the hall's light has gone out of him.
// 3. If the palm completes before Kael is within 1.5 R, Kael is forced to kneel 2 s (no damage) and the palm rises
//    again. The fight can't be lost here.
// 4. Within 1.5 R, the palm comes down. "Go on." (a subtitle; a voice clip later). He waits.
// 5. Kael's next blow that connects ends him: he crumbles to ash, the two halves of his mask fall and break (MaskShatter),
//    and the King's sword drops out of the ash onto the floor.
// 6. OnBossDefeated; about 4 s of stillness; a slow fade to black; the save records "GameCompleted"; "The End"; the
//    main menu. (The ending cinematic slots in here later.)
public class MasterFinisher : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public float palmTime = 3.5f;
    public float lowerTime = 0.8f;
    public float reach = 4.5f;
    public float kneelTime = 2f;
    public float stillness = 4f;
    public float fadeTime = 2.5f;
    public float endCardTime = 3.5f;
    public string line = "Go on.";
    public AudioClip lineVoice;
    public TMP_FontAsset font;
    public Material ashMaterial;
    public Material emberMaterial;

    // For the playtest: stay on the end card instead of loading the main menu.
    public static bool HoldMainMenu;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => HoldMainMenu = false;

    public bool Running { get; private set; }
    public bool PalmRaised { get; private set; }
    public bool PalmDown { get; private set; }
    public bool Ended { get; private set; }
    public int PalmsCompleted { get; private set; }
    // The mask's halves (null until he crumbles).
    public MaskShatter Mask { get; private set; }
    public bool SubtitleShowing => subtitle != null && subtitle.color.a > 0.01f;

    private GameObject canvasObject;
    private TextMeshProUGUI subtitle;
    private Image black;
    private TextMeshProUGUI endCard;

    public IEnumerator Run(MasterBoss boss)
    {
        Running = true;
        Ended = false;
        PalmsCompleted = 0;
        AshColossus colossus = boss.colossus;
        PlayerCombat player = FindAnyObjectByType<PlayerCombat>();
        PlayerCamera cam = FindAnyObjectByType<PlayerCamera>();
        BuildUi();

        // Nothing hurts Kael now; the storm fades; the fire is only fire.
        player.Shield(9999f);
        FloorFire.Harmless = true;
        FindAnyObjectByType<AshStorm>()?.Stop();
        cam?.SetFraming(colossus.transform, 11f);

        // Ash sloughing off him, the mask's light going down.
        ParticleSystem slough = BodyAsh(colossus, 45f, 0.8f, 1.6f);
        Light maskLight = colossus.mask != null ? colossus.mask.GetComponentInChildren<Light>() : null;
        float maskFrom = maskLight != null ? maskLight.intensity : 0f;
        float start = Time.time;
        while (Time.time - start < 1.6f)
        {
            if (maskLight != null)
                maskLight.intensity = Mathf.Lerp(maskFrom, 0.6f, (Time.time - start) / 1.6f);
            yield return null;
        }

        // The palm, as many times as it takes Kael to reach him.
        while (true)
        {
            PalmRaised = true;
            colossus.SetMoveSpeed(colossus.lastCommandLength / palmTime);
            colossus.anim.CrossFadeInFixedTime("LastCommand", 0.3f, 0, 0f);
            colossus.bossTell?.Palm(palmTime);
            start = Time.time;
            bool reached = false;
            while (Time.time - start < palmTime)
            {
                if (Near(colossus, player))
                {
                    reached = true;
                    break;
                }

                yield return null;
            }

            // Down again, either way (backwards through the raise).
            colossus.bossTell?.StopTells();
            colossus.SetMoveSpeed(-colossus.lastCommandLength / lowerTime);
            PalmRaised = false;
            if (reached)
                break;

            // It completed: he makes Kael kneel one more time. It does nothing else.
            PalmsCompleted++;
            colossus.bossTell?.Bell();
            player.ForceKneel(kneelTime);
            yield return new WaitForSeconds(kneelTime + 0.2f);
            if (Near(colossus, player))
                break;
        }

        // "Go on." He waits for the blow.
        PalmDown = true;
        colossus.FinisherOpen = true;
        yield return new WaitForSeconds(Mathf.Min(lowerTime, 0.6f));
        colossus.SetMoveSpeed(0f);
        StartCoroutine(Say(line, 3.5f));
        if (lineVoice != null)
            AudioSource.PlayClipAtPoint(lineVoice, colossus.transform.position + Vector3.up * 4f, 1f);

        bool struck = false;
        colossus.OnFinisherBlow += () => struck = true;
        while (struck == false)
            yield return null;

        // He crumbles from the hands inward; the King's sword drops out of the ash.
        if (slough != null)
            slough.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        BodyAsh(colossus, 0f, 1.6f, 3f, burst: 900);
        Transform sword = colossus.swordTip != null ? colossus.swordTip.parent : null;
        yield return new WaitForSeconds(0.35f);
        // The two halves of the mask, cut from his skin as it stands, fall from the ash and break on the floor.
        var skin = colossus.GetComponentInChildren<SkinnedMeshRenderer>();
        Transform head = colossus.anim.GetBoneTransform(HumanBodyBones.Head);
        if (skin != null && head != null && colossus.mask != null)
            Mask = MaskShatter.Drop(transform, skin, head, colossus.mask, colossus.transform, ashMaterial);
        foreach (Renderer r in colossus.GetComponentsInChildren<Renderer>())
        {
            if (sword == null || r.transform.IsChildOf(sword) == false)
                r.enabled = false;
        }

        if (maskLight != null)
            maskLight.enabled = false;
        colossus.GetComponent<Collider>().enabled = false;
        if (colossus.agent.enabled)
            colossus.agent.enabled = false;
        if (sword != null)
            StartCoroutine(DropSword(sword, colossus.transform.position.y));
        boss.RaiseBossDefeated();

        // Stillness, then the fade, the save, the card, the menu.
        yield return new WaitForSeconds(stillness);
        start = Time.time;
        while (Time.time - start < fadeTime)
        {
            black.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, (Time.time - start) / fadeTime));
            yield return null;
        }

        black.color = Color.black;
        SaveFlags.Set("GameCompleted");
        start = Time.time;
        while (Time.time - start < endCardTime)
        {
            float t = Time.time - start;
            endCard.color = new Color(0.92f, 0.86f, 0.76f, Mathf.Clamp01(t / 1.2f) * Mathf.Clamp01((endCardTime - t) / 0.8f));
            yield return null;
        }

        Ended = true;
        Running = false;
        if (HoldMainMenu == false && Kneel.UI.SceneLoader.CanLoad("MainMenu"))
            Kneel.UI.SceneLoader.Instance.LoadScene("MainMenu", 0.5f, 1f);
    }

    private bool Near(AshColossus colossus, PlayerCombat player) =>
        Vector3.ProjectOnPlane(player.transform.position - colossus.transform.position, Vector3.up).magnitude <= reach;

    private IEnumerator Say(string text, float seconds)
    {
        subtitle.text = text;
        float start = Time.time;
        while (Time.time - start < seconds)
        {
            float t = Time.time - start;
            subtitle.color = new Color(0.93f, 0.88f, 0.8f, Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((seconds - t) / 0.8f));
            yield return null;
        }

        subtitle.color = new Color(0.93f, 0.88f, 0.8f, 0f);
    }

    // Ash from his body: a steady sloughing (rate) or one burst, from the skin itself.
    private ParticleSystem BodyAsh(AshColossus colossus, float rate, float sizeMin, float sizeMax, int burst = 0)
    {
        var skin = colossus.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null || ashMaterial == null)
            return null;
        var go = new GameObject("FinisherAsh");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = burst > 0 ? 0.6f : 30f;
        main.loop = burst == 0;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, burst > 0 ? 2.5f : 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.38f, 0.35f, 0.85f), new Color(0.6f, 0.52f, 0.46f, 0.7f));
        main.gravityModifier = burst > 0 ? 0.25f : 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = burst > 0 ? burst + 10 : 600;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        if (burst > 0)
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(burst * 0.6f)), new ParticleSystem.Burst(0.3f, (short)(burst * 0.4f)) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
        shape.skinnedMeshRenderer = skin;
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.6f, 0.35f), 0f), new GradientColorKey(Color.white, 0.25f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var material = new Material(ashMaterial);
        material.SetColor("_BaseColor", Color.white);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // The sword falls out of the ash, point first, and comes to rest on its side: an ordinary sword again.
    private IEnumerator DropSword(Transform sword, float floorY)
    {
        swordHome = sword.parent;
        swordLocalPosition = sword.localPosition;
        swordLocalRotation = sword.localRotation;
        swordDropped = sword;
        sword.SetParent(null, true);
        Vector3 from = sword.position;
        Quaternion fromRotation = sword.rotation;
        Vector3 flat = Vector3.ProjectOnPlane(sword.up, Vector3.up);
        Quaternion rest = Quaternion.LookRotation(Vector3.up, flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward) * Quaternion.Euler(-90f, 0f, 0f);
        float fall = Mathf.Max(0.3f, Mathf.Sqrt(2f * Mathf.Max(0.1f, from.y - floorY) / 20f));
        float start = Time.time;
        while (Time.time - start < fall)
        {
            float t = (Time.time - start) / fall;
            sword.SetPositionAndRotation(new Vector3(from.x, Mathf.Lerp(from.y, floorY + 0.15f, t * t), from.z), Quaternion.Slerp(fromRotation, rest, t));
            yield return null;
        }

        sword.SetPositionAndRotation(new Vector3(from.x, floorY + 0.15f, from.z), rest);
        FindAnyObjectByType<PlayerCamera>()?.AddShake(0.12f, 0.25f);
    }

    private void BuildUi()
    {
        if (canvasObject != null)
            return;
        canvasObject = new GameObject("FinisherUI");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
        black.transform.SetParent(canvasObject.transform, false);
        var blackRect = (RectTransform)black.transform;
        blackRect.anchorMin = Vector2.zero;
        blackRect.anchorMax = Vector2.one;
        blackRect.offsetMin = blackRect.offsetMax = Vector2.zero;
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;

        subtitle = Label("Subtitle", 44f, new Vector2(0.5f, 0.16f));
        endCard = Label("TheEnd", 96f, new Vector2(0.5f, 0.5f));
        endCard.text = "THE END";
        endCard.characterSpacing = 18f;
    }

    private TextMeshProUGUI Label(string name, float size, Vector2 anchor)
    {
        var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(canvasObject.transform, false);
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.color = new Color(0.93f, 0.88f, 0.8f, 0f);
        text.outlineWidth = 0.15f;
        text.outlineColor = new Color32(8, 6, 5, 255);
        var rect = (RectTransform)text.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(1400f, 160f);
        return text;
    }

    private Transform swordHome;
    private Transform swordDropped;
    private Vector3 swordLocalPosition;
    private Quaternion swordLocalRotation;

    public void ResetState()
    {
        StopAllCoroutines();
        if (swordDropped != null && swordHome != null)
        {
            swordDropped.SetParent(swordHome, false);
            swordDropped.SetLocalPositionAndRotation(swordLocalPosition, swordLocalRotation);
            swordDropped = null;
        }

        if (Mask != null)
            Destroy(Mask.gameObject);
        Mask = null;
        Running = false;
        PalmRaised = false;
        PalmDown = false;
        Ended = false;
        FloorFire.Harmless = false;
        if (canvasObject != null)
            Destroy(canvasObject);
        foreach (Transform child in transform)
        {
            if (child.name == "FinisherAsh")
                Destroy(child.gameObject);
        }
    }
}
