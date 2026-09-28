using TMPro;
using UnityEngine;

public enum DamageNumberStyle
{
    Normal,
    Critical,
    PlayerDamage,
    Block,
    Parry,
}

// Floating world-space text that pops, rises and fades. Built at runtime, so no prefab is needed.
public class DamageNumber : MonoBehaviour
{
    private const float Lifetime = 0.9f;
    private const float PunchTime = 0.12f;

    private TextMeshPro text;
    private Color baseColor;
    private float age = 0f;
    private float baseScale = 1f;
    private Vector3 velocity;

    public static DamageNumber Spawn(Vector3 position, string label, DamageNumberStyle style)
    {
        var go = new GameObject("DamageNumber");
        go.transform.position = position + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.1f, 0.1f));

        var number = go.AddComponent<DamageNumber>();
        number.Setup(label, style);
        return number;
    }

    private void Setup(string label, DamageNumberStyle style)
    {
        text = gameObject.AddComponent<TextMeshPro>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(20, 12, 8, 255);

        float size = 6f;
        switch (style)
        {
            case DamageNumberStyle.Critical:
                baseColor = new Color(1f, 0.82f, 0.2f);
                size = 8f;
                break;
            case DamageNumberStyle.PlayerDamage:
                baseColor = new Color(0.9f, 0.15f, 0.12f);
                break;
            case DamageNumberStyle.Block:
                baseColor = new Color(0.62f, 0.7f, 0.8f);
                size = 5f;
                break;
            case DamageNumberStyle.Parry:
                baseColor = new Color(1f, 0.7f, 0.1f);
                size = 8f;
                break;
            default:
                baseColor = new Color(0.95f, 0.93f, 0.88f);
                break;
        }

        text.fontSize = size;
        text.color = baseColor;
        velocity = Vector3.up * 2.2f;
        baseScale = 1f;
        FaceCamera();
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Rise quickly then settle.
        transform.position += velocity * Time.deltaTime;
        velocity = Vector3.Lerp(velocity, Vector3.up * 0.3f, 6f * Time.deltaTime);

        // Pop in larger, then snap to size.
        float punch = age < PunchTime ? Mathf.Lerp(1.6f, 1f, age / PunchTime) : 1f;
        transform.localScale = Vector3.one * baseScale * punch;

        // Fade over the last 40% of the lifetime.
        float fade = Mathf.InverseLerp(Lifetime, Lifetime * 0.6f, age);
        Color c = baseColor;
        c.a = fade;
        text.color = c;

        FaceCamera();
    }

    private void FaceCamera()
    {
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}
