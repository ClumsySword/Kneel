using System;
using System.Collections;
using UnityEngine;

// Glowing lava cracks on the throne hall's floor (owner, 2026-11-10: "lava cracks", not oil): a decal quad on the
// Kneel/LavaCrack shader, whose texture holds the cracks (red) and the order they spread in (green). Two uses:
// - a floor panel giving way (HallFloor drives it): the cracks run out over the panel, glow brighter through the tell,
//   then burst as the floor opens;
// - the Ash Colossus's Hammerfall (Erupt): the cracks race out from the impact, brighten and brighten, burst (hurting
//   anyone standing on one), then fade.
// `map` is a readable copy of the crack texture, for asking whether a point stands on a crack.
public class LavaCracks : MonoBehaviour
{
    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");

    public Texture2D map;

    // The flash at the burst (the playtest waits for it).
    public const float BurstIntensity = 9f;

    public float Reveal { get; set; }
    public float Intensity { get; set; } = 1f;
    public float Pulse { get; set; }

    private Renderer quad;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        quad = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        Apply();
    }

    private void LateUpdate() => Apply();

    public void Apply()
    {
        if (quad == null)
            return;
        quad.GetPropertyBlock(block);
        block.SetFloat(RevealId, Reveal);
        block.SetFloat(IntensityId, Intensity);
        block.SetFloat(PulseId, Pulse);
        quad.SetPropertyBlock(block);
    }

    // Whether a floor point stands on a crack that has reached it (within `margin` metres of one).
    public bool Covers(Vector3 world, float margin, float threshold = 0.35f)
    {
        if (map == null || isActiveAndEnabled == false)
            return false;

        // The quad lies flat (rotated 90 degrees about x): its local x, y are the floor's, -0.5 to 0.5.
        Vector3 local = transform.InverseTransformPoint(world);
        Vector2 uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
        float size = Mathf.Max(transform.lossyScale.x, 0.01f);
        float m = margin / size;
        Vector2[] offsets = { Vector2.zero, new Vector2(m, 0f), new Vector2(-m, 0f), new Vector2(0f, m), new Vector2(0f, -m), new Vector2(m, m) * 0.7f, new Vector2(-m, -m) * 0.7f, new Vector2(m, -m) * 0.7f, new Vector2(-m, m) * 0.7f };
        foreach (Vector2 o in offsets)
        {
            Vector2 p = uv + o;
            if (p.x < 0f || p.x > 1f || p.y < 0f || p.y > 1f)
                continue;
            Color c = map.GetPixelBilinear(p.x, p.y);
            if (c.r >= threshold && c.g <= Reveal)
                return true;
        }

        return false;
    }

    // A flat crack decal of `radius` at a floor point, turned `yaw` degrees.
    public static LavaCracks Spawn(Material material, Texture2D map, Vector3 at, float radius, float yaw)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "LavaCracks";
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetPositionAndRotation(at + Vector3.up * 0.04f, Quaternion.Euler(90f, yaw, 0f));
        go.transform.localScale = Vector3.one * radius * 2f;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var cracks = go.AddComponent<LavaCracks>();
        cracks.map = map;
        cracks.Reveal = 0f;
        cracks.Intensity = 1f;
        return cracks;
    }

    // The Hammerfall's cracks: out from the impact over `grow` s, brighter and brighter over `brighten` s, a burst
    // (onBurst), then fading over `fade` s. Destroys itself.
    public IEnumerator Erupt(float grow, float brighten, float fade, Action onBurst)
    {
        float start = Time.time;
        while (Time.time - start < grow)
        {
            float t = (Time.time - start) / grow;
            Reveal = 1.05f * (1f - (1f - t) * (1f - t));
            Intensity = Mathf.Lerp(1.2f, 1.5f, t);
            yield return null;
        }

        Reveal = 1.05f;
        start = Time.time;
        while (Time.time - start < brighten)
        {
            float t = (Time.time - start) / brighten;
            Intensity = Mathf.Lerp(1.5f, 5f, t * t);
            Pulse = t;
            yield return null;
        }

        Intensity = BurstIntensity;
        Pulse = 0f;
        onBurst?.Invoke();
        start = Time.time;
        while (Time.time - start < fade)
        {
            float t = (Time.time - start) / fade;
            Intensity = Mathf.Lerp(BurstIntensity, 0f, Mathf.Sqrt(t));
            yield return null;
        }

        Destroy(gameObject);
    }
}
