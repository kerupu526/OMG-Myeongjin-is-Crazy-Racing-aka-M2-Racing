using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // Small runtime-safe helpers for building placeholder Text/Image-based UI (no final art).
    // Shared by TestTrackBuilder (editor build time) and StageAssembler (runtime stage
    // switching) so panel/text construction isn't duplicated between the two.
    public static class SimpleUIFactory
    {
        public static GameObject CreateFullscreenPanel(Transform parent, string name, Color backgroundColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = backgroundColor;
            bg.raycastTarget = false;

            return panel;
        }

        public static Text CreateCenteredText(Transform parent, string name, int fontSize, Color color)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            Text text = textObj.AddComponent<Text>();
            UiTypography.Apply(text);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(40f, 40f);
            rect.offsetMax = new Vector2(-40f, -40f);

            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image bg = buttonObj.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.9f);

            Button button = buttonObj.AddComponent<Button>();

            Text text = CreateCenteredText(buttonObj.transform, "Label", 28, Color.black);
            text.text = label;

            return button;
        }

        public static Text CreateCornerText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, TextAnchor alignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            Text text = textObj.AddComponent<Text>();
            UiTypography.Apply(text);
            text.fontSize = 22;
            text.color = Color.cyan;
            text.alignment = alignment;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(260f, 40f);

            return text;
        }
    }
}
