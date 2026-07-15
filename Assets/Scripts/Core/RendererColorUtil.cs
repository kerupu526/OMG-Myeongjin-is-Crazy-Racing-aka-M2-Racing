using UnityEngine;

namespace M2.Core
{
    public static class RendererColorUtil
    {
        const string RuntimeLitTemplatePath = "Runtime/NetworkStageLit";

        static Material runtimeLitTemplate;

        // renderer.material (used here until this fix) auto-instantiates a per-renderer copy,
        // but doing that in an EDITOR SCRIPT (not Play mode) triggers Unity's own "Instantiating
        // material due to calling renderer.material during edit mode. This will leak materials
        // into the scene." warning — and it's not just a cosmetic warning: that "leak" means the
        // instantiated material doesn't get embedded as an owned sub-asset when the GameObject is
        // saved via PrefabUtility.SaveAsPrefabAsset (as opposed to just being saved into a
        // scene, which every other caller of this class does and where the leak is harmless).
        // NetworkPrefabBuilder is the one place in this project that saves a prefab ASSET this
        // way, and it silently produced a vehicle with every renderer's material slot empty
        // (confirmed by reading the saved .prefab's YAML directly) — playtester feedback:
        // "차량 텍스처도 깨져서 보라색으로 나옴". Manually cloning via `new Material(...)` and
        // assigning through sharedMaterial (not material) sidesteps the edit-mode special case
        // entirely, so it now works correctly whether the renderer ends up saved into a scene or
        // a prefab.
        static Material GetInstancedMaterial(Renderer renderer)
        {
            Material source = renderer.sharedMaterial;

            // Primitives created at runtime carry Unity's legacy Standard material.  That
            // material is not compatible with this URP project and becomes bright magenta in
            // a player build.  Use a serialized URP material as the fallback instead of only
            // Shader.Find: the resource also makes the shader a build dependency, so shader
            // stripping cannot leave the multiplayer-only stage geometry without a shader.
            if (RequiresUrpFallback(source)) source = GetRuntimeLitTemplate();

            Material instance = source != null
                ? new Material(source)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.sharedMaterial = instance;
            return instance;
        }

        static bool RequiresUrpFallback(Material material)
        {
            if (material == null || material.shader == null || !material.shader.isSupported) return true;

            string shaderName = material.shader.name;
            return shaderName == "Standard" || shaderName.StartsWith("Legacy Shaders/");
        }

        static Material GetRuntimeLitTemplate()
        {
            if (runtimeLitTemplate == null)
                runtimeLitTemplate = Resources.Load<Material>(RuntimeLitTemplatePath);
            return runtimeLitTemplate;
        }

        // Sets a color that works whether the active shader uses the legacy "_Color"
        // property or URP Lit's "_BaseColor". `doubleSided` guards against thin ring/plane
        // geometry rendering invisible from one side if its triangle winding ends up
        // backface-culled.
        public static void ApplyColor(Renderer renderer, Color color, bool doubleSided = false)
        {
            Material material = GetInstancedMaterial(renderer);
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
            Material material = GetInstancedMaterial(renderer);
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", tiling);
            if (doubleSided && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }
        }

        // Like ApplyColor, but also pushes an emission color so the surface reads as
        // glowing/hot (lava, fire) even without a light source hitting it — emission is
        // added to the final pixel regardless of scene lighting. No-ops on shaders without
        // an _EmissionColor property instead of throwing. Inlines ApplyColor's work instead of
        // calling it, since calling GetInstancedMaterial twice for the same renderer would
        // create two different instances and silently drop whichever one is set second.
        public static void ApplyEmissiveColor(Renderer renderer, Color baseColor, Color emissionColor)
        {
            Material material = GetInstancedMaterial(renderer);
            material.color = baseColor;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            if (!material.HasProperty("_EmissionColor")) return;

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }
}
