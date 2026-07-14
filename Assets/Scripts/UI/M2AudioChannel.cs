using UnityEngine;

namespace M2.UI
{
    public enum M2AudioChannelType
    {
        Bgm,
        Sfx,
    }

    /// <summary>
    /// Attach this beside an AudioSource to classify it as BGM or SFX. The settings screen can
    /// then apply independent channel volumes without relying on an external mixer asset.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class M2AudioChannel : MonoBehaviour
    {
        public M2AudioChannelType channel = M2AudioChannelType.Sfx;
        [Range(0f, 1f)] public float authoredVolume = 1f;

        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            ApplySettings();
        }

        void OnEnable()
        {
            if (source == null) source = GetComponent<AudioSource>();
            ApplySettings();
        }

        public void ApplySettings()
        {
            if (source == null) source = GetComponent<AudioSource>();
            if (source == null) return;
            float channelVolume = channel == M2AudioChannelType.Bgm
                ? M2GameSettings.BgmVolume
                : M2GameSettings.SfxVolume;
            source.volume = Mathf.Clamp01(authoredVolume) * channelVolume;
        }
    }
}
