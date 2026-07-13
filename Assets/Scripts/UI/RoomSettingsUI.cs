using M2.Core;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    /// <summary>Host-side room rules selector shown before a Relay session is created.</summary>
    [DisallowMultipleComponent]
    public class RoomSettingsUI : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.102f, 0.063f, 0.188f, 0.94f);
        static readonly Color Yellow = new Color(1f, 0.851f, 0.239f);
        static readonly Color Pink = new Color(1f, 0.184f, 0.620f);

        public GameManager gameManager;
        public RaceMode selectedMode = RaceMode.Item;
        public int selectedItemLapCount = 3;
        public VictoryCondition selectedItemVictoryCondition = VictoryCondition.SimpleFinish;

        GameObject panel;
        Text title;
        Button modeButton;
        Button lapButton;
        Button victoryButton;

        void Start()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                selectedMode = gameManager.raceMode;
                selectedItemLapCount = gameManager.targetLapCount;
                selectedItemVictoryCondition = gameManager.victoryCondition;
            }
            BuildLayout();
            RefreshLabels();
        }

        public void ApplyTo(GameManager manager)
        {
            if (manager == null) return;
            gameManager = manager;
            manager.ConfigureRoomSettings(selectedMode, selectedItemLapCount, selectedItemVictoryCondition);
            RefreshLabels();
        }

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        public void ToggleMode()
        {
            selectedMode = selectedMode == RaceMode.Item ? RaceMode.Speed : RaceMode.Item;
            ApplyTo(gameManager);
        }

        public void CycleItemLapCount()
        {
            if (selectedMode == RaceMode.Speed) return;
            selectedItemLapCount = selectedItemLapCount == 1 ? 3 : selectedItemLapCount == 3 ? 5 : 1;
            ApplyTo(gameManager);
        }

        public void CycleItemVictoryCondition()
        {
            if (selectedMode == RaceMode.Speed) return;
            selectedItemVictoryCondition = selectedItemVictoryCondition == VictoryCondition.SimpleFinish
                ? VictoryCondition.StarBet
                : VictoryCondition.SimpleFinish;
            ApplyTo(gameManager);
        }

        void BuildLayout()
        {
            if (panel != null) return;

            panel = new GameObject("RoomSettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            Image background = panel.AddComponent<Image>();
            background.color = Ink;
            background.raycastTarget = false;
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = Yellow;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-20f, 118f);
            rect.sizeDelta = new Vector2(430f, 222f);

            title = CreateText(panel.transform, "Title", 24, Yellow, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(16f, -54f);
            title.rectTransform.offsetMax = new Vector2(-16f, -10f);

            modeButton = CreateSettingButton("ModeButton", 64f, ToggleMode);
            lapButton = CreateSettingButton("LapButton", 6f, CycleItemLapCount);
            victoryButton = CreateSettingButton("VictoryButton", -52f, CycleItemVictoryCondition);
        }

        Button CreateSettingButton(string name, float y, UnityEngine.Events.UnityAction action)
        {
            Button button = SimpleUIFactory.CreateButton(panel.transform, name, "", new Vector2(0f, y), new Vector2(390f, 46f));
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = Pink;
            Text buttonText = button.GetComponentInChildren<Text>(true);
            if (buttonText != null)
            {
                buttonText.fontSize = 24;
                buttonText.color = Color.white;
                buttonText.raycastTarget = false;
                buttonText.rectTransform.offsetMin = Vector2.zero;
                buttonText.rectTransform.offsetMax = Vector2.zero;
            }
            button.onClick.AddListener(action);
            return button;
        }

        void RefreshLabels()
        {
            if (title != null)
            {
                title.text = selectedMode == RaceMode.Speed
                    ? "방 설정 · 스피드전\n5바퀴 · 최고 100km/h · 5초 자동 분사"
                    : "방 설정 · 아이템전";
            }

            SetButtonText(modeButton, selectedMode == RaceMode.Speed ? "모드: 스피드전  ↻" : "모드: 아이템전  ↻");
            bool itemMode = selectedMode == RaceMode.Item;
            if (lapButton != null) lapButton.interactable = itemMode;
            if (victoryButton != null) victoryButton.interactable = itemMode;
            SetButtonText(lapButton, itemMode ? $"바퀴: {RaceModeRules.NormalizeItemLapCount(selectedItemLapCount)}  ↻" : "바퀴: 5 (스피드전 고정)");
            SetButtonText(victoryButton, itemMode
                ? $"승리: {(selectedItemVictoryCondition == VictoryCondition.StarBet ? "별점 내기" : "단순 완주")}  ↻"
                : "승리: 단순 완주 (스피드전 고정)");
        }

        static void SetButtonText(Button button, string value)
        {
            if (button == null) return;
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = value;
        }

        static Text CreateText(Transform parent, string name, int fontSize, Color color, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
