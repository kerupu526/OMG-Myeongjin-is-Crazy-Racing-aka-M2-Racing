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

            // Palette pulled from "M2 레이싱 게임 UI 디자인" (the HTML HUD/lobby mockups' most
            // common hex colors: #1a1030 dark navy-purple bg, #ffd93d gold, #5fd8f5 cyan) so the
            // track itself reads as part of the same bright-cartoon-on-dark-background look as
            // the rest of the game's UI, instead of a realistic gray road.
            Color asphalt = new Color(0.102f, 0.063f, 0.188f);       // #1a1030
            Color asphaltFleck = new Color(0.141f, 0.090f, 0.251f);  // slightly lighter navy-purple
            Color edgeLine = new Color(0.373f, 0.847f, 0.961f);     // #5fd8f5 cyan
            Color centerLine = new Color(1.0f, 0.851f, 0.239f);      // #ffd93d gold

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

        // Checkerboard for the start/finish line marker (playtester feedback: "출발/도착선이
        // 없어"). Gold/navy instead of plain white/black to match the UI mockup palette — still
        // reads clearly as a checkered flag, just in the game's own colors. Point-filtered so
        // the checker edges stay crisp instead of blurring into gray at the small size this
        // gets stretched to.
        public static Texture2D CreateCheckeredFlagTexture(int width = 64, int height = 64, int checkerCount = 8)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;

            Color gold = new Color(1.0f, 0.851f, 0.239f);   // #ffd93d
            Color navy = new Color(0.102f, 0.063f, 0.188f); // #1a1030

            for (int y = 0; y < height; y++)
            {
                int cy = y * checkerCount / height;
                for (int x = 0; x < width; x++)
                {
                    int cx = x * checkerCount / width;
                    bool isGold = (cx + cy) % 2 == 0;
                    texture.SetPixel(x, y, isGold ? gold : navy);
                }
            }
            texture.Apply();

            return texture;
        }
    }
}
