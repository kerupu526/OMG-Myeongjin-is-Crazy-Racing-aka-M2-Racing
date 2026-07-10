using UnityEngine;

namespace M2.Core
{
    // Runtime-safe placeholder texture generator for the track surface — same idea as
    // PlaceholderSpriteFactory, but for a tileable road texture instead of a sprite. Exists so
    // the track doesn't have to stay flat-colored while waiting for real art (no Kenney pack
    // ships a plain tileable asphalt texture for a custom procedural mesh — their kits are all
    // modular pre-textured/vertex-colored models, not raw materials).
    public static class TrackTextureFactory
    {
        // The texture tiles along U (track length) — CreateTrackSurface's UVs already bake in
        // the repeat count, so this only needs to be one repeat unit. V spans the track's full
        // width (0 = inner edge, 1 = outer edge), not tiled.
        public static Texture2D CreateAsphaltTexture(int width = 64, int height = 64)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color asphalt = new Color(0.15f, 0.15f, 0.17f);
            Color asphaltFleck = new Color(0.19f, 0.19f, 0.21f);
            Color edgeLine = new Color(0.9f, 0.9f, 0.85f);
            Color centerLine = new Color(0.95f, 0.75f, 0.15f);

            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    Color color = ((x * 7 + y * 13) % 5 == 0) ? asphaltFleck : asphalt;

                    if (v < 0.06f || v > 0.94f)
                    {
                        color = edgeLine; // curb stripe along both edges
                    }
                    else if (v > 0.47f && v < 0.53f && u < 0.5f)
                    {
                        color = centerLine; // dashed center line — u < 0.5 leaves a gap per repeat
                    }

                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();

            return texture;
        }
    }
}
