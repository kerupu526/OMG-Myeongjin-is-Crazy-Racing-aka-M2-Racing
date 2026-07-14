using UnityEngine;

namespace M2.UI
{
    public enum M2AvatarEyes
    {
        Round,
        Happy,
        Cool,
    }

    public enum M2AvatarMouth
    {
        Smile,
        Open,
        Flat,
    }

    public enum M2AvatarHat
    {
        None,
        Cap,
        Crown,
    }

    /// <summary>
    /// Compact, player-owned avatar choices. This is deliberately plain data so the same values
    /// can be persisted locally and copied into the online lobby/result profile payload.
    /// </summary>
    public readonly struct M2AvatarAppearance
    {
        public int BodyColorIndex { get; }
        public M2AvatarEyes Eyes { get; }
        public M2AvatarMouth Mouth { get; }
        public bool HasCheeks { get; }
        public bool HasEars { get; }
        public M2AvatarHat Hat { get; }
        public int PlateIndex { get; }

        public M2AvatarAppearance(int bodyColorIndex, M2AvatarEyes eyes, M2AvatarMouth mouth,
            bool hasCheeks, bool hasEars, M2AvatarHat hat, int plateIndex)
        {
            BodyColorIndex = M2PlayerProfile.NormalizeAvatarColorIndex(bodyColorIndex);
            Eyes = M2PlayerProfile.NormalizeEyes(eyes);
            Mouth = M2PlayerProfile.NormalizeMouth(mouth);
            HasCheeks = hasCheeks;
            HasEars = hasEars;
            Hat = M2PlayerProfile.NormalizeHat(hat);
            PlateIndex = M2PlayerProfile.NormalizePlateIndex(plateIndex);
        }

        public M2AvatarAppearance WithBodyColor(int value) => new M2AvatarAppearance(value, Eyes, Mouth,
            HasCheeks, HasEars, Hat, PlateIndex);

        public M2AvatarAppearance WithEyes(M2AvatarEyes value) => new M2AvatarAppearance(BodyColorIndex, value,
            Mouth, HasCheeks, HasEars, Hat, PlateIndex);

        public M2AvatarAppearance WithMouth(M2AvatarMouth value) => new M2AvatarAppearance(BodyColorIndex, Eyes,
            value, HasCheeks, HasEars, Hat, PlateIndex);

        public M2AvatarAppearance WithCheeks(bool value) => new M2AvatarAppearance(BodyColorIndex, Eyes, Mouth,
            value, HasEars, Hat, PlateIndex);

        public M2AvatarAppearance WithEars(bool value) => new M2AvatarAppearance(BodyColorIndex, Eyes, Mouth,
            HasCheeks, value, Hat, PlateIndex);

        public M2AvatarAppearance WithHat(M2AvatarHat value) => new M2AvatarAppearance(BodyColorIndex, Eyes,
            Mouth, HasCheeks, HasEars, value, PlateIndex);

        public M2AvatarAppearance WithPlate(int value) => new M2AvatarAppearance(BodyColorIndex, Eyes, Mouth,
            HasCheeks, HasEars, Hat, value);
    }

    /// <summary>
    /// Persistent local racer profile shared by menu, lobby, HUD, and result presentation.
    /// </summary>
    public static class M2PlayerProfile
    {
        const string DisplayNameKey = "M2.Profile.DisplayName";
        const string AvatarColorKey = "M2.Profile.AvatarColor";
        const string AvatarEyesKey = "M2.Profile.AvatarEyes";
        const string AvatarMouthKey = "M2.Profile.AvatarMouth";
        const string AvatarCheeksKey = "M2.Profile.AvatarCheeks";
        const string AvatarEarsKey = "M2.Profile.AvatarEars";
        const string AvatarHatKey = "M2.Profile.AvatarHat";
        const string AvatarPlateKey = "M2.Profile.AvatarPlate";

        public const string DefaultDisplayName = "레이서 #001";

        static readonly Color[] AvatarColors =
        {
            new Color(1f, 0.851f, 0.239f),
            new Color(1f, 0.184f, 0.620f),
            new Color(0.373f, 0.847f, 0.961f),
            new Color(0.714f, 0.953f, 0.420f),
            new Color(0.604f, 0.420f, 1f),
            new Color(1f, 0.502f, 0.306f),
        };

        static readonly string[] PlateLabels = { "#001", "#077", "#999" };

        public static string DisplayName => NormalizeDisplayName(PlayerPrefs.GetString(DisplayNameKey, DefaultDisplayName));

        public static M2AvatarAppearance Appearance => new M2AvatarAppearance(
            PlayerPrefs.GetInt(AvatarColorKey, 0),
            (M2AvatarEyes)PlayerPrefs.GetInt(AvatarEyesKey, (int)M2AvatarEyes.Round),
            (M2AvatarMouth)PlayerPrefs.GetInt(AvatarMouthKey, (int)M2AvatarMouth.Smile),
            PlayerPrefs.GetInt(AvatarCheeksKey, 1) != 0,
            PlayerPrefs.GetInt(AvatarEarsKey, 1) != 0,
            (M2AvatarHat)PlayerPrefs.GetInt(AvatarHatKey, (int)M2AvatarHat.Cap),
            PlayerPrefs.GetInt(AvatarPlateKey, 0));

        public static int AvatarColorIndex => Appearance.BodyColorIndex;

        public static Color AvatarColor => ResolveAvatarColor(AvatarColorIndex);

        /// <summary>Compatibility overload retained for older menu callers and tests.</summary>
        public static void Save(string displayName, int avatarColorIndex)
        {
            Save(displayName, Appearance.WithBodyColor(avatarColorIndex));
        }

        public static void Save(string displayName, M2AvatarAppearance appearance)
        {
            M2AvatarAppearance normalized = NormalizeAppearance(appearance);
            PlayerPrefs.SetString(DisplayNameKey, NormalizeDisplayName(displayName));
            PlayerPrefs.SetInt(AvatarColorKey, normalized.BodyColorIndex);
            PlayerPrefs.SetInt(AvatarEyesKey, (int)normalized.Eyes);
            PlayerPrefs.SetInt(AvatarMouthKey, (int)normalized.Mouth);
            PlayerPrefs.SetInt(AvatarCheeksKey, normalized.HasCheeks ? 1 : 0);
            PlayerPrefs.SetInt(AvatarEarsKey, normalized.HasEars ? 1 : 0);
            PlayerPrefs.SetInt(AvatarHatKey, (int)normalized.Hat);
            PlayerPrefs.SetInt(AvatarPlateKey, normalized.PlateIndex);
            PlayerPrefs.Save();
        }

        public static M2AvatarAppearance NormalizeAppearance(M2AvatarAppearance appearance)
        {
            return new M2AvatarAppearance(appearance.BodyColorIndex, appearance.Eyes, appearance.Mouth,
                appearance.HasCheeks, appearance.HasEars, appearance.Hat, appearance.PlateIndex);
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

        public static string ResolvePlateLabel(int index) => PlateLabels[NormalizePlateIndex(index)];

        public static int AvatarColorCount => AvatarColors.Length;

        public static int PlateCount => PlateLabels.Length;

        public static int NormalizeAvatarColorIndex(int index) => NormalizeIndex(index, AvatarColors.Length);

        public static int NormalizePlateIndex(int index) => NormalizeIndex(index, PlateLabels.Length);

        public static M2AvatarEyes NormalizeEyes(M2AvatarEyes value) =>
            (M2AvatarEyes)NormalizeIndex((int)value, 3);

        public static M2AvatarMouth NormalizeMouth(M2AvatarMouth value) =>
            (M2AvatarMouth)NormalizeIndex((int)value, 3);

        public static M2AvatarHat NormalizeHat(M2AvatarHat value) =>
            (M2AvatarHat)NormalizeIndex((int)value, 3);

        static int NormalizeIndex(int index, int count)
        {
            int normalized = index % count;
            return normalized < 0 ? normalized + count : normalized;
        }
    }
}
