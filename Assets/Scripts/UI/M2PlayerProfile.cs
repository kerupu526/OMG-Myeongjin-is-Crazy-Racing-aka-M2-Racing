using UnityEngine;

namespace M2.UI
{
    /// <summary>
    /// Small local profile shared by the menu and lobby presentation. The network payload for
    /// opponents is intentionally left for the online profile-sync follow-up; this keeps the
    /// local player's selection persistent without inventing a Relay data contract prematurely.
    /// </summary>
    public static class M2PlayerProfile
    {
        const string DisplayNameKey = "M2.Profile.DisplayName";
        const string AvatarColorKey = "M2.Profile.AvatarColor";

        public const string DefaultDisplayName = "레이서 #001";

        static readonly Color[] AvatarColors =
        {
            new Color(1f, 0.184f, 0.620f),
            new Color(0.373f, 0.847f, 0.961f),
            new Color(0.714f, 0.953f, 0.420f),
        };

        public static string DisplayName => NormalizeDisplayName(PlayerPrefs.GetString(DisplayNameKey, DefaultDisplayName));

        public static int AvatarColorIndex => NormalizeAvatarColorIndex(PlayerPrefs.GetInt(AvatarColorKey, 0));

        public static Color AvatarColor => ResolveAvatarColor(AvatarColorIndex);

        public static void Save(string displayName, int avatarColorIndex)
        {
            PlayerPrefs.SetString(DisplayNameKey, NormalizeDisplayName(displayName));
            PlayerPrefs.SetInt(AvatarColorKey, NormalizeAvatarColorIndex(avatarColorIndex));
            PlayerPrefs.Save();
        }

        public static string NormalizeDisplayName(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? DefaultDisplayName : value.Trim();
            return normalized.Length <= 12 ? normalized : normalized.Substring(0, 12);
        }

        public static Color ResolveAvatarColor(int index)
        {
            return AvatarColors[NormalizeAvatarColorIndex(index)];
        }

        public static int NormalizeAvatarColorIndex(int index) => NormalizeColorIndex(index);

        static int NormalizeColorIndex(int index)
        {
            int count = AvatarColors.Length;
            int normalized = index % count;
            return normalized < 0 ? normalized + count : normalized;
        }
    }
}
