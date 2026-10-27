using UnityEngine;

namespace Kneel.Core
{
    // The pointer used wherever the OS cursor shows (the menus, the pause menu, the F1 overlay): a plain ring with a
    // dot, white with a dark outline so it reads on any background. In play the cursor is hidden and the aim ember
    // stands in for it (AimEmber). Drawn at runtime, so there is no texture asset to keep import settings on.
    public static class MenuCursor
    {
        private const int Size = 32;
        private const float Radius = 9f;

        private static Texture2D texture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (texture == null)
            {
                texture = Build();
            }

            Cursor.SetCursor(texture, new Vector2(Size / 2, Size / 2), CursorMode.Auto);

            // The project enters Play mode without a domain reload: never subscribe twice.
            Application.quitting -= Restore;
            Application.quitting += Restore;
        }

        // Give the Editor its own pointer back when Play mode ends.
        private static void Restore()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private static Texture2D Build()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "MenuCursor",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[Size * Size];
            Vector2 centre = new Vector2(Size / 2, Size / 2);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                    float ring = Mathf.Abs(d - Radius);

                    // Coverage, anti-aliased over a pixel: a 2 px white stroke inside a 1 px dark outline.
                    float white = Mathf.Max(Mathf.Clamp01(1.5f - ring), Mathf.Clamp01(1.75f - d));
                    float outline = Mathf.Max(Mathf.Clamp01(2.5f - ring), Mathf.Clamp01(2.75f - d)) * 0.6f;

                    float alpha = white + outline * (1f - white);
                    float shade = alpha > 0f ? white / alpha : 0f;
                    pixels[y * Size + x] = new Color(shade, shade, shade, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
