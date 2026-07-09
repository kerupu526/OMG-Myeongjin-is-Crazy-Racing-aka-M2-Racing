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
    }
}
