using UnityEngine;
using UnityEngine.UIElements;

namespace M2.UI
{
    /// <summary>
    /// Shared UI Toolkit avatar renderer.  Menu, lobby, HUD, and result surfaces all feed the
    /// same profile payload through this builder so cosmetic combinations cannot drift apart.
    /// The caller only needs an avatar root; missing face parts are created on demand.
    /// </summary>
    public static class M2AvatarVisual
    {
        const string CrownResourcePath = "M2UI/Icons/crown";
        const string QuestionResourcePath = "M2UI/Icons/question";
        static readonly Color WaitingOpponentColor = new Color32(223, 241, 247, 255);
        static VectorImage crownVectorImage;
        static VectorImage questionVectorImage;

        public static void Apply(VisualElement avatar, M2AvatarAppearance appearance)
        {
            if (avatar == null) return;

            AvatarParts parts = EnsureParts(avatar);
            appearance = M2PlayerProfile.NormalizeAppearance(appearance);
            Color color = M2PlayerProfile.ResolveAvatarColor(appearance.BodyColorIndex);

            SetPartVisible(avatar.Q<Image>("avatar-question-icon"), false);
            parts.body.style.backgroundColor = color;
            SetPartVisible(parts.leftEar, appearance.HasEars);
            SetPartVisible(parts.rightEar, appearance.HasEars);
            parts.leftEar.style.backgroundColor = color;
            parts.rightEar.style.backgroundColor = color;

            SetPartVisible(parts.eyes, true);
            ApplyEyes(parts.eyes, appearance.Eyes);
            SetPartVisible(parts.cheeks, appearance.HasCheeks);
            SetPartVisible(parts.mouth, true);
            ApplyMouth(parts.mouth, appearance.Mouth);

            bool capVisible = appearance.Hat == M2AvatarHat.Cap;
            SetPartVisible(parts.capTop, capVisible);
            SetPartVisible(parts.capBrim, capVisible);
            if (parts.crown.vectorImage == null)
            {
                crownVectorImage ??= Resources.Load<VectorImage>(CrownResourcePath);
                parts.crown.vectorImage = crownVectorImage;
                parts.crown.scaleMode = ScaleMode.ScaleToFit;
            }
            SetPartVisible(parts.crown, appearance.Hat == M2AvatarHat.Crown);
            SetPartVisible(parts.plate, true);
            parts.plate.text = M2PlayerProfile.ResolvePlateLabel(appearance.PlateIndex);
        }

        /// <summary>
        /// Draws the intentionally anonymous opponent slot used before a room guest connects.
        /// The avatar shell stays in place so the lobby layout does not jump, while all saved
        /// facial/profile details are replaced by the supplied question-mark VectorImage.
        /// </summary>
        public static void ApplyWaitingOpponent(VisualElement avatar)
        {
            if (avatar == null) return;

            AvatarParts parts = EnsureParts(avatar);
            parts.body.style.backgroundColor = WaitingOpponentColor;
            SetPartVisible(parts.leftEar, false);
            SetPartVisible(parts.rightEar, false);
            SetPartVisible(parts.eyes, false);
            SetPartVisible(parts.cheeks, false);
            SetPartVisible(parts.mouth, false);
            SetPartVisible(parts.capTop, false);
            SetPartVisible(parts.capBrim, false);
            SetPartVisible(parts.crown, false);
            SetPartVisible(parts.plate, false);

            Image question = EnsureImage(avatar, "avatar-question-icon", "avatar-question-icon");
            if (question.vectorImage == null)
            {
                questionVectorImage ??= Resources.Load<VectorImage>(QuestionResourcePath);
                question.vectorImage = questionVectorImage;
                question.scaleMode = ScaleMode.ScaleToFit;
            }
            SetPartVisible(question, true);
        }

        static void ApplyEyes(VisualElement eyes, M2AvatarEyes appearance)
        {
            eyes.RemoveFromClassList("avatar-eyes--happy");
            eyes.RemoveFromClassList("avatar-eyes--cool");
            if (appearance == M2AvatarEyes.Happy) eyes.AddToClassList("avatar-eyes--happy");
            if (appearance == M2AvatarEyes.Cool) eyes.AddToClassList("avatar-eyes--cool");

            bool cool = appearance == M2AvatarEyes.Cool;
            int lensIndex = 0;
            foreach (VisualElement lens in eyes.Children())
            {
                if (cool)
                {
                    lens.style.backgroundColor = lensIndex == 0
                        ? new Color(0.545f, 0.886f, 1f)
                        : new Color(1f, 0.616f, 0.878f);
                }
                else
                {
                    lens.style.backgroundColor = StyleKeyword.Null;
                }
                lensIndex++;
            }
        }

        static void ApplyMouth(VisualElement mouth, M2AvatarMouth appearance)
        {
            mouth.RemoveFromClassList("avatar-mouth--open");
            mouth.RemoveFromClassList("avatar-mouth--flat");
            if (appearance == M2AvatarMouth.Open) mouth.AddToClassList("avatar-mouth--open");
            if (appearance == M2AvatarMouth.Flat) mouth.AddToClassList("avatar-mouth--flat");
        }

        static AvatarParts EnsureParts(VisualElement avatar)
        {
            avatar.AddToClassList("avatar");
            VisualElement body = EnsureElement(avatar, "avatar-body", "avatar-body");
            VisualElement leftEar = EnsureElement(avatar, "avatar-ear-left", "avatar-ear", "avatar-ear--left");
            VisualElement rightEar = EnsureElement(avatar, "avatar-ear-right", "avatar-ear", "avatar-ear--right");
            VisualElement eyes = EnsureElement(avatar, "avatar-eyes", "avatar-eyes");
            EnsureEyes(eyes);
            VisualElement cheeks = EnsureElement(avatar, "avatar-cheeks", "avatar-cheeks");
            EnsureCheeks(cheeks);
            VisualElement mouth = EnsureElement(avatar, "avatar-mouth", "avatar-mouth");
            VisualElement hat = EnsureElement(avatar, "avatar-hat", "avatar-hat");
            VisualElement capTop = EnsureElement(hat, "avatar-hat-cap-top", "avatar-hat-cap-top");
            VisualElement capBrim = EnsureElement(hat, "avatar-hat-cap-brim", "avatar-hat-cap-brim");
            Image crown = EnsureImage(hat, "avatar-hat-crown", "avatar-hat-crown");
            Label plate = EnsureLabel(avatar, "avatar-plate", "avatar-plate");
            return new AvatarParts(body, leftEar, rightEar, eyes, cheeks, mouth, capTop, capBrim, crown, plate);
        }

        static void EnsureEyes(VisualElement eyes)
        {
            while (eyes.childCount < 2)
            {
                VisualElement eye = new VisualElement();
                eye.AddToClassList("avatar-eye");
                VisualElement shine = new VisualElement();
                shine.AddToClassList("avatar-eye-shine");
                eye.Add(shine);
                eyes.Add(eye);
            }
        }

        static void EnsureCheeks(VisualElement cheeks)
        {
            while (cheeks.childCount < 2)
            {
                VisualElement cheek = new VisualElement();
                cheek.AddToClassList("avatar-cheek");
                cheeks.Add(cheek);
            }
        }

        static VisualElement EnsureElement(VisualElement parent, string name, params string[] classes)
        {
            VisualElement element = parent.Q<VisualElement>(name);
            if (element == null)
            {
                element = new VisualElement { name = name };
                parent.Add(element);
            }
            for (int i = 0; i < classes.Length; i++) element.AddToClassList(classes[i]);
            return element;
        }

        static Image EnsureImage(VisualElement parent, string name, params string[] classes)
        {
            Image image = parent.Q<Image>(name);
            if (image == null)
            {
                image = new Image { name = name };
                parent.Add(image);
            }
            for (int i = 0; i < classes.Length; i++) image.AddToClassList(classes[i]);
            return image;
        }

        static Label EnsureLabel(VisualElement parent, string name, params string[] classes)
        {
            Label label = parent.Q<Label>(name);
            if (label == null)
            {
                label = new Label { name = name };
                parent.Add(label);
            }
            for (int i = 0; i < classes.Length; i++) label.AddToClassList(classes[i]);
            return label;
        }

        static void SetPartVisible(VisualElement part, bool visible)
        {
            if (part == null) return;
            part.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        readonly struct AvatarParts
        {
            public readonly VisualElement body;
            public readonly VisualElement leftEar;
            public readonly VisualElement rightEar;
            public readonly VisualElement eyes;
            public readonly VisualElement cheeks;
            public readonly VisualElement mouth;
            public readonly VisualElement capTop;
            public readonly VisualElement capBrim;
            public readonly Image crown;
            public readonly Label plate;

            public AvatarParts(VisualElement body, VisualElement leftEar, VisualElement rightEar,
                VisualElement eyes, VisualElement cheeks, VisualElement mouth, VisualElement capTop,
                VisualElement capBrim, Image crown, Label plate)
            {
                this.body = body;
                this.leftEar = leftEar;
                this.rightEar = rightEar;
                this.eyes = eyes;
                this.cheeks = cheeks;
                this.mouth = mouth;
                this.capTop = capTop;
                this.capBrim = capBrim;
                this.crown = crown;
                this.plate = plate;
            }
        }
    }
}
