using UnityEngine;

namespace M2.Core
{
    // Runtime-safe placeholder sprite generator (works in Play mode, not just the Editor).
    // Everything that needs a billboard sprite before real illustrated art exists —
    // vehicles, items — should go through this so the look stays consistent.
    public static class PlaceholderSpriteFactory
    {
        public static Sprite CreateCircleSprite(Color fillColor, Color outlineColor, int size = 128, float pixelsPerUnit = 64f)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 4f;
            float innerRadius = outerRadius - 10f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    Color color;
                    if (dist <= innerRadius) color = fillColor;
                    else if (dist <= outerRadius) color = outlineColor;
                    else color = new Color(0f, 0f, 0f, 0f);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }
    }
}
