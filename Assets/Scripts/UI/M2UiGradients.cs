using System.Collections.Generic;
using UnityEngine;

namespace M2.UI
{
    /// <summary>
    /// Procedural gradient textures for the UI Toolkit screens. The Figma frames use CSS
    /// linear-gradients; the SVG VectorImage import renders those unreliably (solid or washed
    /// fills depending on the Library import), so the menus and HUD draw these textures instead.
    /// Angles follow the CSS convention: 0deg points up, angles grow clockwise.
    /// </summary>
    public static class M2UiGradients
    {
        public readonly struct Stop
        {
            public readonly float Position;
            public readonly Color Color;

            public Stop(float position, Color color)
            {
                Position = position;
                Color = color;
            }
        }

        const int TextureSize = 96;

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Linear(string key, float cssAngleDegrees, params Stop[] stops)
        {
            if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"M2Gradient_{key}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float radians = cssAngleDegrees * Mathf.Deg2Rad;
            // Direction of increasing t in CSS page coordinates (x right, y down).
            var direction = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
            float extent = Mathf.Abs(direction.x) + Mathf.Abs(direction.y);
            if (extent < 0.0001f) extent = 1f;

            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                // Texture rows run bottom-up while the CSS gradient math runs top-down.
                float pageY = 1f - y / (float)(TextureSize - 1);
                for (int x = 0; x < TextureSize; x++)
                {
                    float pageX = x / (float)(TextureSize - 1);
                    float t = ((pageX - 0.5f) * direction.x + (pageY - 0.5f) * direction.y) / extent + 0.5f;
                    pixels[y * TextureSize + x] = Evaluate(stops, Mathf.Clamp01(t));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        /// <summary>Diagonal repeating stripes used by the stage gauge fill.</summary>
        public static Texture2D Stripes(string key, Color first, Color second, int stripePixels = 12)
        {
            if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"M2Stripes_{key}",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    bool firstBand = (x + y) / stripePixels % 2 == 0;
                    pixels[y * TextureSize + x] = firstBand ? first : second;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        static Color Evaluate(Stop[] stops, float t)
        {
            if (stops == null || stops.Length == 0) return Color.magenta;
            if (t <= stops[0].Position) return stops[0].Color;
            for (int i = 1; i < stops.Length; i++)
            {
                if (t > stops[i].Position) continue;
                float span = Mathf.Max(0.0001f, stops[i].Position - stops[i - 1].Position);
                return Color.Lerp(stops[i - 1].Color, stops[i].Color, (t - stops[i - 1].Position) / span);
            }
            return stops[stops.Length - 1].Color;
        }
    }
}
