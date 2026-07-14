#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace M2.Editor
{
    /// <summary>
    /// Installs the user-provided M2 mark as the Standalone executable icon.
    /// It intentionally does not expose the texture to any runtime UI.
    /// </summary>
    public static class M2GameIconInstaller
    {
        const string IconPath = "Assets/Resources/M2UI/M2Logo.png";

        [MenuItem("M2/Install Standalone Game Icon")]
        public static void Install()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                throw new System.InvalidOperationException($"M2 game icon was not found at {IconPath}.");
            }

            Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Application);
            if (icons == null || icons.Length == 0)
            {
                // Unity 6 expects the eight Standalone icon slots to be supplied together.
                icons = new Texture2D[8];
            }

            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
            AssetDatabase.SaveAssets();
            Debug.Log("[M2] Installed M2.png as the Standalone game icon.");
        }
    }
}
#endif
