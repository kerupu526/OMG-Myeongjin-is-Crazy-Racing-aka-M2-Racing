using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    /// <summary>
    /// A small code-native portrait used by the profile and lobby cards. Keeping its parts as
    /// regular UGUI controls means every saved cosmetic choice remains visible without relying
    /// on an external sprite sheet or a scene-specific prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class M2AvatarPortrait : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.102f, 0.063f, 0.188f);
        static readonly Color CheekPink = new Color(1f, 0.38f, 0.56f, 0.9f);

        Image body;
        Text initials;
        Text eyes;
        Text mouth;
        Text hat;
        Text plate;
        Text leftCheek;
        Text rightCheek;
        Text leftEar;
        Text rightEar;

        public void Apply(M2AvatarAppearance appearance, string displayName)
        {
            EnsureBuilt();
            appearance = M2PlayerProfile.NormalizeAppearance(appearance);
            Color bodyColor = M2PlayerProfile.ResolveAvatarColor(appearance.BodyColorIndex);
            body.color = bodyColor;

            // The portrait itself now carries the visual identity; the full racer name remains
            // in the neighbouring card label, so a large monogram no longer obscures the face.
            if (initials != null)
            {
                initials.text = BuildInitials(displayName);
                initials.enabled = false;
            }

            eyes.text = appearance.Eyes switch
            {
                M2AvatarEyes.Happy => "⌒   ⌒",
                M2AvatarEyes.Cool => "▰   ▰",
                _ => "●   ●",
            };
            mouth.text = appearance.Mouth switch
            {
                M2AvatarMouth.Open => "●",
                M2AvatarMouth.Flat => "—",
                _ => "⌣",
            };
            hat.text = appearance.Hat switch
            {
                M2AvatarHat.Cap => "CAP",
                M2AvatarHat.Crown => "♛",
                _ => string.Empty,
            };
            hat.color = appearance.Hat == M2AvatarHat.Crown ? new Color(1f, 0.851f, 0.239f) : Ink;
            hat.enabled = appearance.Hat != M2AvatarHat.None;
            plate.text = M2PlayerProfile.ResolvePlateLabel(appearance.PlateIndex);

            bool cheeks = appearance.HasCheeks;
            leftCheek.enabled = cheeks;
            rightCheek.enabled = cheeks;
            bool ears = appearance.HasEars;
            leftEar.enabled = ears;
            rightEar.enabled = ears;
            Color earColor = Color.Lerp(bodyColor, Ink, 0.32f);
            leftEar.color = earColor;
            rightEar.color = earColor;
        }

        void EnsureBuilt()
        {
            if (body != null) return;
            body = GetComponent<Image>();
            initials = transform.Find("Initials")?.GetComponent<Text>() ??
                transform.Find("Initial")?.GetComponent<Text>();
            if (initials == null)
            {
                initials = FindOrCreateText("Initials", 18, Color.white, TextAnchor.MiddleCenter,
                    new Vector2(0.2f, 0.08f), new Vector2(0.8f, 0.25f));
            }
            eyes = FindOrCreateText("FaceEyes", 24, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.78f));
            mouth = FindOrCreateText("FaceMouth", 22, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.25f, 0.30f), new Vector2(0.75f, 0.51f));
            hat = FindOrCreateText("FaceHat", 16, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.25f, 0.78f), new Vector2(0.75f, 0.98f));
            plate = FindOrCreateText("FacePlate", 14, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.12f, 0.03f), new Vector2(0.88f, 0.20f));
            leftCheek = FindOrCreateText("FaceCheekLeft", 17, CheekPink, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.27f), new Vector2(0.31f, 0.47f));
            rightCheek = FindOrCreateText("FaceCheekRight", 17, CheekPink, TextAnchor.MiddleCenter,
                new Vector2(0.69f, 0.27f), new Vector2(0.92f, 0.47f));
            leftEar = FindOrCreateText("FaceEarLeft", 18, Ink, TextAnchor.MiddleCenter,
                new Vector2(-0.12f, 0.48f), new Vector2(0.10f, 0.72f));
            rightEar = FindOrCreateText("FaceEarRight", 18, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.90f, 0.48f), new Vector2(1.12f, 0.72f));
            leftCheek.text = "●";
            rightCheek.text = "●";
            leftEar.text = "●";
            rightEar.text = "●";
        }

        Text FindOrCreateText(string name, int fontSize, Color color, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            Text text = transform.Find(name)?.GetComponent<Text>();
            if (text == null)
            {
                GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(transform, false);
                text = textObject.GetComponent<Text>();
            }

            UiTypography.Apply(text, UiFontRole.Body);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        static string BuildInitials(string displayName)
        {
            string normalized = M2PlayerProfile.NormalizeDisplayName(displayName).Replace(" ", string.Empty);
            return normalized.Length <= 2 ? normalized : normalized.Substring(0, 2);
        }
    }
}
