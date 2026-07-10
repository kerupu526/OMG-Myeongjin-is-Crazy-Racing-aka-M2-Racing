using UnityEngine;

namespace M2.Core
{
    public static class RendererColorUtil
    {
        // Sets a color that works whether the active shader uses the legacy "_Color"
        // property or URP Lit's "_BaseColor". `doubleSided` guards against thin ring/plane
        // geometry rendering invisible from one side if its triangle winding ends up
        // backface-culled.
        public static void ApplyColor(Renderer renderer, Color color, bool doubleSided = false)
        {
            Material material = renderer.material;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (doubleSided && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }
        }

        // Same legacy/_BaseColor dance as ApplyColor, but for a UV-mapped texture instead of a
        // flat color. `tiling` repeats the texture along the mesh's U axis (e.g. track length)
        // so a single small texture can cover an arbitrarily long strip without stretching.
        public static void ApplyTexture(Renderer renderer, Texture texture, Vector2 tiling, bool doubleSided = false)
        {
            Material material = renderer.material;
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", tiling);
            if (doubleSided && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }
        }
    }
}
