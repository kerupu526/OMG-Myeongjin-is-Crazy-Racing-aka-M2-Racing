using UnityEngine;

namespace M2.UI
{
    /// <summary>
    /// Persistent, player-local display and sound preferences exposed by the menu settings UI.
    /// The values are deliberately small in scope so they work both in the Editor and standalone
    /// builds without relying on a scene-specific audio or display manager.
    /// </summary>
    public static class M2GameSettings
    {
        const string MasterVolumeKey = "M2.Settings.MasterVolume";
        const string FullscreenKey = "M2.Settings.Fullscreen";

        public const float DefaultMasterVolume = 0.8f;
        public const bool DefaultFullscreen = true;

        public static float MasterVolume => NormalizeVolume(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));

        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, DefaultFullscreen ? 1 : 0) != 0;

        public static void Save(float masterVolume, bool fullscreen)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, NormalizeVolume(masterVolume));
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static float NormalizeVolume(float value)
        {
            return Mathf.Clamp01(value);
        }

        public static void ApplyRuntime()
        {
            AudioListener.volume = MasterVolume;

            // Changing the Editor window while editing a scene is disruptive. The same saved
            // value is applied to the actual game window in standalone builds instead.
            if (!Application.isEditor)
            {
                Screen.fullScreen = Fullscreen;
            }
        }
    }
}
