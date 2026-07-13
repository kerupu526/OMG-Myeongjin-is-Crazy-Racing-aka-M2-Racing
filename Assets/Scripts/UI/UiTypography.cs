using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    /// <summary>
    /// Loads the three typefaces used by the supplied UI design and applies them consistently
    /// to legacy uGUI Text components. The fonts live under Resources so runtime-built stage
    /// and network UI can use the same assets without scene-only references.
    /// </summary>
    public static class UiTypography
    {
        const string BodyResourcePath = "Fonts/Jua-Regular";
        const string DisplayResourcePath = "Fonts/BlackHanSans-Regular";
        const string MetricResourcePath = "Fonts/Fredoka";

        static Font body;
        static Font display;
        static Font metric;
        static Font fallback;

        public static Font Body => Resolve(ref body, BodyResourcePath);
        public static Font Display => Resolve(ref display, DisplayResourcePath);
        public static Font Metric => Resolve(ref metric, MetricResourcePath);

        public static void Apply(Text text, UiFontRole role = UiFontRole.Body)
        {
            if (text == null) return;

            Font font = role switch
            {
                UiFontRole.Display => Display,
                UiFontRole.Metric => Metric,
                _ => Body,
            };

            if (font != null) text.font = font;
        }

        static Font Resolve(ref Font cache, string resourcePath)
        {
            if (cache != null) return cache;

            cache = Resources.Load<Font>(resourcePath);
            if (cache != null) return cache;

            if (fallback == null)
                fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fallback;
        }
    }

    public enum UiFontRole
    {
        Body,
        Display,
        Metric,
    }
}
