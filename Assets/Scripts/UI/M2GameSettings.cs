using System;
using UnityEngine;

namespace M2.UI
{
    public enum M2GraphicsQuality
    {
        Low,
        Medium,
        High,
    }

    public enum M2Language
    {
        Korean,
        English,
    }

    /// <summary>
    /// Persistent, player-local sound and display preferences. Values are kept independent of a
    /// particular scene so menu, race HUD, and future audio sources receive the same setting.
    /// </summary>
    public static class M2GameSettings
    {
        const string MasterVolumeKey = "M2.Settings.MasterVolume";
        const string BgmVolumeKey = "M2.Settings.BgmVolume";
        const string SfxVolumeKey = "M2.Settings.SfxVolume";
        const string FullscreenKey = "M2.Settings.Fullscreen";
        const string GraphicsQualityKey = "M2.Settings.GraphicsQuality";
        const string LanguageKey = "M2.Settings.Language";

        public const float DefaultMasterVolume = 0.8f;
        public const float DefaultBgmVolume = 0.7f;
        public const float DefaultSfxVolume = 0.9f;
        public const bool DefaultFullscreen = true;
        public const M2GraphicsQuality DefaultGraphicsQuality = M2GraphicsQuality.High;
        public const M2Language DefaultLanguage = M2Language.Korean;
        public static readonly Vector2Int FixedWindowResolution = new Vector2Int(1280, 720);

        /// <summary>Raised after a saved language choice is applied to the running UI.</summary>
        public static event Action<M2Language> LanguageChanged;

        public static float MasterVolume => NormalizeVolume(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));

        public static float BgmVolume => NormalizeVolume(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolume));

        public static float SfxVolume => NormalizeVolume(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));

        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, DefaultFullscreen ? 1 : 0) != 0;

        public static M2GraphicsQuality GraphicsQuality => NormalizeGraphicsQuality(
            (M2GraphicsQuality)PlayerPrefs.GetInt(GraphicsQualityKey, (int)DefaultGraphicsQuality));

        public static M2Language Language => NormalizeLanguage(
            (M2Language)PlayerPrefs.GetInt(LanguageKey, (int)DefaultLanguage));

        /// <summary>Compatibility overload retained for the first settings implementation.</summary>
        public static void Save(float masterVolume, bool fullscreen)
        {
            Save(masterVolume, BgmVolume, SfxVolume, GraphicsQuality, fullscreen, Language);
        }

        public static void Save(float masterVolume, float bgmVolume, float sfxVolume,
            M2GraphicsQuality graphicsQuality, bool fullscreen, M2Language language)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, NormalizeVolume(masterVolume));
            PlayerPrefs.SetFloat(BgmVolumeKey, NormalizeVolume(bgmVolume));
            PlayerPrefs.SetFloat(SfxVolumeKey, NormalizeVolume(sfxVolume));
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(GraphicsQualityKey, (int)NormalizeGraphicsQuality(graphicsQuality));
            PlayerPrefs.SetInt(LanguageKey, (int)NormalizeLanguage(language));
            PlayerPrefs.Save();
            ApplyRuntime();
            LanguageChanged?.Invoke(Language);
        }

        public static float NormalizeVolume(float value) => Mathf.Clamp01(value);

        public static M2GraphicsQuality NormalizeGraphicsQuality(M2GraphicsQuality value) =>
            (M2GraphicsQuality)NormalizeEnumIndex((int)value, 3);

        public static M2Language NormalizeLanguage(M2Language value) =>
            (M2Language)NormalizeEnumIndex((int)value, 2);

        public static int ResolveUnityQualityLevel(M2GraphicsQuality quality, int availableLevels)
        {
            if (availableLevels <= 0) return -1;
            return quality switch
            {
                M2GraphicsQuality.Low => 0,
                // This project currently ships two Unity quality assets. Medium and High use
                // the desktop asset, then ApplyGraphicsQuality differentiates their runtime
                // shadow/LOD/anti-aliasing profile. Mapping Medium to the mobile asset made it
                // visually identical to Low in a two-level build.
                M2GraphicsQuality.Medium => Mathf.Min(1, availableLevels - 1),
                _ => availableLevels - 1,
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySavedRuntimeSettingsOnStartup()
        {
            ApplyRuntime();
        }

        public static void ApplyRuntime()
        {
            AudioListener.volume = MasterVolume;

            int qualityLevel = ResolveUnityQualityLevel(GraphicsQuality, QualitySettings.names.Length);
            if (qualityLevel >= 0 && QualitySettings.GetQualityLevel() != qualityLevel)
            {
                QualitySettings.SetQualityLevel(qualityLevel, applyExpensiveChanges: true);
            }
            ApplyGraphicsQuality(GraphicsQuality);

            foreach (M2AudioChannel channel in UnityEngine.Object.FindObjectsByType<M2AudioChannel>(FindObjectsSortMode.None))
            {
                channel.ApplySettings();
            }

            // Editor Game View dimensions are user tooling, not the shipped game window. A
            // standalone build is locked to 16:9 when windowed; ProjectSettings also has
            // resizableWindow: 0, so players cannot distort the HUD by dragging its frame.
            if (!Application.isEditor)
            {
                if (Fullscreen)
                {
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                }
                else
                {
                    Screen.SetResolution(FixedWindowResolution.x, FixedWindowResolution.y,
                        FullScreenMode.Windowed);
                }
            }
        }

        static void ApplyGraphicsQuality(M2GraphicsQuality quality)
        {
            // The values intentionally remain modest for this stylized game, while still making
            // every option materially different in the standalone build (the old saved enum had
            // no distinct Medium profile on a two-quality-level project).
            switch (quality)
            {
                case M2GraphicsQuality.Low:
                    QualitySettings.shadowDistance = 0f;
                    QualitySettings.lodBias = 0.7f;
                    QualitySettings.antiAliasing = 0;
                    break;
                case M2GraphicsQuality.Medium:
                    QualitySettings.shadowDistance = 24f;
                    QualitySettings.lodBias = 1.25f;
                    QualitySettings.antiAliasing = 0;
                    break;
                default:
                    QualitySettings.shadowDistance = 60f;
                    QualitySettings.lodBias = 2f;
                    QualitySettings.antiAliasing = 2;
                    break;
            }
        }

        static int NormalizeEnumIndex(int value, int count)
        {
            return Mathf.Clamp(value, 0, count - 1);
        }
    }
}
