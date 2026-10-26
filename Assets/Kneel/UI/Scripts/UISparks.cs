using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kneel.UI
{
    // MenuGlow's sparks drawn as UI images, for overlay canvases (the pause menu), where particle systems don't
    // render. Same look as the particle version: a few small embers lift off the line, drift, cool from white-gold
    // to red and fade. Unscaled time, so they keep moving while the game is paused. Sizes are reference pixels.
    public class UISparks : MonoBehaviour
    {
        private class Spark
        {
            public RectTransform rect;
            public Image image;
            public float age;
            public float life;
            public float size;
            public Vector2 velocity;
            public float seed;
        }

        [SerializeField] private Sprite sprite;
        [SerializeField] private Material material;
        [SerializeField] private float rate = 5f;
        [SerializeField] private Vector2 size = new Vector2(2f, 3.5f);
        [SerializeField] private Vector2 lifetime = new Vector2(0.6f, 1.3f);
        [SerializeField] private Vector2 rise = new Vector2(20f, 45f);
        [SerializeField] private float drift = 10f;
        [SerializeField] private float wander = 40f;

        private static readonly Color Hot = Color.white;
        private static readonly Color Warm = new Color(1f, 0x9A / 255f, 0x5A / 255f);
        private static readonly Color Cool = new Color(0xB0 / 255f, 0x30 / 255f, 0x1A / 255f);

        private readonly List<Spark> sparks = new List<Spark>();
        private float pending;

        // Emitter width (MenuGlow sets it to the selected words' width).
        public float Width { get; set; } = 300f;

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            pending += rate * dt;
            while (pending >= 1f)
            {
                pending -= 1f;
                Emit();
            }

            float time = Time.unscaledTime;
            foreach (Spark spark in sparks)
            {
                if (!spark.image.enabled)
                {
                    continue;
                }

                spark.age += dt;
                float t = spark.age / spark.life;
                if (t >= 1f)
                {
                    spark.image.enabled = false;
                    continue;
                }

                spark.velocity.x += (Mathf.PerlinNoise(spark.seed, time * 2f) - 0.5f) * wander * dt;
                spark.rect.anchoredPosition += spark.velocity * dt;
                spark.rect.sizeDelta = Vector2.one * spark.size * Mathf.Lerp(1f, 0.3f, t);

                Color color = t < 0.45f ? Color.Lerp(Hot, Warm, t / 0.45f) : Color.Lerp(Warm, Cool, (t - 0.45f) / 0.55f);
                color.a = t < 0.1f ? Mathf.Lerp(0f, 0.9f, t / 0.1f) : t < 0.55f ? Mathf.Lerp(0.9f, 0.5f, (t - 0.1f) / 0.45f) : Mathf.Lerp(0.5f, 0f, (t - 0.55f) / 0.45f);
                spark.image.color = color;
            }
        }

        private void Emit()
        {
            Spark spark = sparks.Find(s => !s.image.enabled);
            if (spark == null)
            {
                var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                spark = new Spark { rect = (RectTransform)go.transform, image = go.GetComponent<Image>() };
                spark.image.sprite = sprite;
                spark.image.material = material;
                spark.image.raycastTarget = false;
                sparks.Add(spark);
            }

            spark.age = 0f;
            spark.life = Random.Range(lifetime.x, lifetime.y);
            spark.size = Random.Range(size.x, size.y);
            spark.velocity = new Vector2(Random.Range(-drift, drift), Random.Range(rise.x, rise.y));
            spark.seed = Random.value * 100f;
            spark.rect.anchoredPosition = new Vector2(Random.Range(-0.5f, 0.5f) * Width, Random.Range(-1.5f, 1.5f));
            spark.rect.sizeDelta = Vector2.one * spark.size;
            spark.image.color = Color.clear;
            spark.image.enabled = true;
        }
    }
}
