using M2.Core;
using M2.Items;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    /// <summary>
    /// The in-race presentation HUD.  This keeps the generated stage scenes compatible with
    /// their old RaceLabel reference while building the final card layout at runtime.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        public static readonly Vector2 GameplayReferenceResolution = new Vector2(1280f, 720f);
        public const float GameplayHudScale = 0.9f;

        static readonly Color Ink = new Color(0.102f, 0.063f, 0.188f, 0.94f);
        static readonly Color Yellow = new Color(1f, 0.851f, 0.239f);
        static readonly Color Pink = new Color(1f, 0.184f, 0.620f);
        static readonly Color Mint = new Color(0.714f, 0.953f, 0.420f);

        [Header("Race data")]
        public LapTracker lapTracker;
        public RaceTimer raceTimer;
        public ItemSlots itemSlots;
        public GameManager gameManager;

        [Header("Legacy generated-scene reference")]
        [Tooltip("기존 씬의 RaceLabel. 정식 HUD에서는 좌측 상단 바퀴 카드로 재사용한다.")]
        public Text label;

        Text lapLabel;
        Text timeLabel;
        Text versusLabel;
        Text primaryNameLabel;
        Text secondaryNameLabel;
        Text itemDetailLabel;
        Text itemHintLabel;
        Image primaryIcon;
        Image secondaryIcon;
        readonly List<GameObject> presentationRoots = new List<GameObject>();
        bool presentationVisible;
        bool presentationVisibilityInitialized;

        void Awake()
        {
            ConfigureCanvasScaling();
            // Stage UI can start on a child object, so install this before its Start callback
            // asks whether a formal gauge bar is available.
            if (GetComponent<StageGaugeHUD>() == null) gameObject.AddComponent<StageGaugeHUD>();
        }

        void ConfigureCanvasScaling()
        {
            ConfigureGameplayCanvasScaling(GetComponent<CanvasScaler>());
        }

        public static void ConfigureGameplayCanvasScaling(CanvasScaler scaler)
        {
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = GameplayReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        void Start()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (raceTimer == null) raceTimer = FindFirstObjectByType<RaceTimer>();
            if (lapTracker == null) lapTracker = FindFirstObjectByType<LapTracker>();
            if (itemSlots == null) itemSlots = FindFirstObjectByType<ItemSlots>();
            BuildLayout();
            HideDevelopmentTelemetry();
            StyleTransientMessages();
            SetPresentationVisible(false);

            if (gameManager != null)
            {
                gameManager.OnStateChanged += HandleRaceStateChanged;
                HandleRaceStateChanged(gameManager.CurrentState);
            }
            Canvas.ForceUpdateCanvases();
        }

        void OnDestroy()
        {
            if (gameManager != null) gameManager.OnStateChanged -= HandleRaceStateChanged;
        }

        void Update()
        {
            if (lapLabel == null) return;

            if (!presentationVisible) return;

            int lap = lapTracker != null ? lapTracker.LapCount : 0;
            int targetLap = gameManager != null ? gameManager.targetLapCount : 3;
            float timeLeft = gameManager != null ? gameManager.TimeRemaining : 0f;

            lapLabel.text = $"<size={ScaleGameplayHudFont(22)}>LAP · 바퀴</size>\n<size={ScaleGameplayHudFont(48)}><color=#{ColorUtility.ToHtmlStringRGB(Yellow)}>{lap}</color> / {targetLap}</size>";
            timeLabel.text = $"<size={ScaleGameplayHudFont(22)}>TIME · 남은 시간</size>\n<size={ScaleGameplayHudFont(48)}>{FormatTime(timeLeft)}</size>";
            versusLabel.text = BuildVersusText(lap);

            if (gameManager != null && gameManager.IsSpeedMode)
            {
                RefreshSpeedModeSupply();
            }
            else
            {
                RefreshSlot(itemSlots != null ? itemSlots.PrimarySlot : null, primaryIcon, primaryNameLabel, "1");
                RefreshSlot(itemSlots != null ? itemSlots.SecondarySlot : null, secondaryIcon, secondaryNameLabel, "2");
                RefreshItemDetail();
                if (itemHintLabel != null) itemHintLabel.text = "아이템 슬롯 · Ctrl 가속 · E 사용 · P C4 기폭";
            }
        }

        void BuildLayout()
        {
            if (lapLabel != null) return;

            lapLabel = label != null ? label : CreateText("RaceHud_LapText", transform, 24, Color.white, TextAnchor.MiddleCenter, UiFontRole.Display);
            presentationRoots.Add(PlaceTextInCard(lapLabel, transform, "RaceHud_LapCard", new Vector2(0f, 1f), new Vector2(16f, -16f),
                new Vector2(166f, 112f), Yellow, UiFontRole.Display));

            timeLabel = CreateText("RaceHud_TimeText", transform, 24, Color.white, TextAnchor.MiddleCenter, UiFontRole.Display);
            presentationRoots.Add(PlaceTextInCard(timeLabel, transform, "RaceHud_TimeCard", new Vector2(1f, 1f), new Vector2(-16f, -16f),
                new Vector2(198f, 112f), Pink, UiFontRole.Display));

            versusLabel = CreateText("RaceHud_VersusText", transform, 24, Color.white, TextAnchor.MiddleCenter);
            presentationRoots.Add(PlaceTextInCard(versusLabel, transform, "RaceHud_VersusCard", new Vector2(0.5f, 1f), new Vector2(0f, -16f),
                new Vector2(300f, 112f), Color.white));
            ConfigureText(versusLabel, 24, Color.white, TextAnchor.MiddleCenter);

            CreateItemCard("RaceHud_PrimarySlot", new Vector2(24f, 358f), out primaryIcon, out primaryNameLabel, "1");
            CreateItemCard("RaceHud_SecondarySlot", new Vector2(24f, 164f), out secondaryIcon, out secondaryNameLabel, "2");

            GameObject detailCard = CreateCard(transform, "RaceHud_ItemDetailCard", Ink, Yellow);
            presentationRoots.Add(detailCard);
            SetRect(detailCard.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                ScaleGameplayHud(new Vector2(216f, 130f)), ScaleGameplayHud(new Vector2(590f, 138f)));
            itemDetailLabel = CreateText("ItemDetail", detailCard.transform, 22, Color.white, TextAnchor.MiddleLeft);
            Stretch(itemDetailLabel.rectTransform, ScaleGameplayHud(new Vector2(20f, 12f)), ScaleGameplayHud(new Vector2(-20f, -12f)));

            GameObject slotHint = new GameObject("RaceHud_ItemHint", typeof(RectTransform));
            slotHint.transform.SetParent(transform, false);
            presentationRoots.Add(slotHint);
            itemHintLabel = slotHint.AddComponent<Text>();
            ConfigureText(itemHintLabel, 22, new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerLeft);
            itemHintLabel.text = "아이템 슬롯 · Ctrl 가속 · E 사용 · P C4 기폭";
            SetRect(itemHintLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                ScaleGameplayHud(new Vector2(216f, 282f)), ScaleGameplayHud(new Vector2(620f, 30f)));
        }

        void CreateItemCard(string name, Vector2 position, out Image icon, out Text itemName, string slotNumber)
        {
            GameObject card = CreateCard(transform, name, Ink, Yellow);
            presentationRoots.Add(card);
            SetRect(card.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, ScaleGameplayHud(position),
                ScaleGameplayHud(new Vector2(170f, 170f)));

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
            iconObject.transform.SetParent(card.transform, false);
            icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                ScaleGameplayHud(new Vector2(0f, 14f)), ScaleGameplayHud(new Vector2(106f, 106f)));

            itemName = CreateText("Name", card.transform, 20, Color.white, TextAnchor.LowerCenter);
            SetRect(itemName.rectTransform, Vector2.zero, Vector2.one, ScaleGameplayHud(new Vector2(12f, 6f)),
                ScaleGameplayHud(new Vector2(-12f, 42f)));

            GameObject badge = CreateCard(card.transform, "SlotNumber", Yellow, Ink);
            SetRect(badge.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                ScaleGameplayHud(new Vector2(-8f, 8f)), ScaleGameplayHud(new Vector2(36f, 36f)));
            Text badgeText = CreateText("Text", badge.transform, 22, Ink, TextAnchor.MiddleCenter, UiFontRole.Metric);
            badgeText.text = slotNumber;
            Stretch(badgeText.rectTransform, Vector2.zero, Vector2.zero);
        }

        void RefreshSlot(ItemDefinition definition, Image icon, Text nameLabel, string slotNumber)
        {
            if (icon == null || nameLabel == null) return;

            if (definition == null)
            {
                icon.enabled = false;
                nameLabel.text = $"{slotNumber} · 비어 있음";
                return;
            }

            bool found = ItemArt.TryGet(definition.id, out Sprite sprite, out Color tint);
            icon.enabled = found;
            icon.sprite = sprite;
            icon.color = tint;
            nameLabel.text = $"{slotNumber} · {definition.itemName}";
        }

        void RefreshItemDetail()
        {
            if (itemDetailLabel == null) return;
            ItemDefinition definition = itemSlots != null ? itemSlots.PrimarySlot ?? itemSlots.SecondarySlot : null;
            itemDetailLabel.text = definition == null
                ? "<color=#FFD93D>아이템 대기 중</color>\n트랙의 픽업을 획득하면 실제 스프라이트와 상세 설명이 표시됩니다."
                : $"<color=#FFD93D>{definition.itemName}</color>\n{definition.description}";
        }

        void RefreshSpeedModeSupply()
        {
            if (primaryIcon != null) primaryIcon.enabled = false;
            if (secondaryIcon != null) secondaryIcon.enabled = false;
            if (primaryNameLabel != null) primaryNameLabel.text = "기본 휘발유 · 자동";
            if (secondaryNameLabel != null) secondaryNameLabel.text = "랜덤 아이템 없음";

            float interval = gameManager != null
                ? Mathf.Max(0.05f, gameManager.speedModeGasolineInterval)
                : RaceModeRules.SpeedModeGasolineInterval;
            if (itemDetailLabel != null)
            {
                itemDetailLabel.text = $"<color=#FFD93D>기본 휘발유 자동 분사</color>\n" +
                    $"레이스 시작 시와 이후 {interval:0.#}초마다 자동으로 가속합니다. 아이템 슬롯을 차지하지 않습니다.";
            }
            if (itemHintLabel != null)
                itemHintLabel.text = $"스피드전 · 기본 휘발유 {interval:0.#}초마다 자동 분사";
        }

        string BuildVersusText(int localLap)
        {
            int opponentLap = 0;
            bool hasOpponent = false;
            if (gameManager != null)
            {
                for (int i = 0; i < gameManager.racers.Count; i++)
                {
                    LapTracker racer = gameManager.racers[i];
                    if (racer == null || racer == lapTracker) continue;
                    opponentLap = racer.LapCount;
                    hasOpponent = true;
                    break;
                }
            }

            if (!hasOpponent) return "<color=#B6F36B>연습</color> <color=#FFD93D>VS</color> 대기";
            string rank = localLap >= opponentLap ? "1위" : "2위";
            return $"<color=#B6F36B>나 · {rank}</color>  <color=#FFD93D>VS</color>  <color=#8BE2FF>상대 · {opponentLap}바퀴</color>";
        }

        void HideDevelopmentTelemetry()
        {
            VehicleDebugHUD debugHud = FindFirstObjectByType<VehicleDebugHUD>();
            if (debugHud != null) debugHud.SetVisible(false);
        }

        void HandleRaceStateChanged(RaceState state)
        {
            SetPresentationVisible(state == RaceState.Racing);
        }

        void StyleTransientMessages()
        {
            ItemUseNotifier notifier = FindFirstObjectByType<ItemUseNotifier>();
            if (notifier != null && notifier.label != null)
            {
                UiTypography.Apply(notifier.label, UiFontRole.Display);
                notifier.label.fontSize = ScaleGameplayHudFont(32);
                notifier.label.color = Ink;
                AddOutline(notifier.label.gameObject, Ink, new Vector2(2f, -2f));
            }

            VehicleStatusHUD status = FindFirstObjectByType<VehicleStatusHUD>();
            if (status != null && status.label != null)
            {
                UiTypography.Apply(status.label);
                status.label.fontSize = ScaleGameplayHudFont(22);
                status.label.color = Mint;
                status.label.alignment = TextAnchor.LowerRight;
                SetRect(status.label.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                    ScaleGameplayHud(new Vector2(-32f, 132f)), ScaleGameplayHud(new Vector2(380f, 104f)));
                AddOutline(status.label.gameObject, Ink, new Vector2(1.5f, -1.5f));
            }
        }

        void SetPresentationVisible(bool shouldBeVisible)
        {
            if (presentationVisibilityInitialized && presentationVisible == shouldBeVisible) return;
            presentationVisibilityInitialized = true;
            presentationVisible = shouldBeVisible;
            for (int i = 0; i < presentationRoots.Count; i++)
            {
                if (presentationRoots[i] != null) presentationRoots[i].SetActive(shouldBeVisible);
            }
        }

        static GameObject CreateCard(Transform parent, string name, Color background, Color border)
        {
            GameObject card = new GameObject(name, typeof(RectTransform));
            card.transform.SetParent(parent, false);
            Image image = card.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = false;
            AddOutline(card, border, new Vector2(3f, -3f));
            return card;
        }

        static Text CreateText(string name, Transform parent, int size, Color color, TextAnchor alignment,
            UiFontRole role = UiFontRole.Body)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            ConfigureText(text, size, color, alignment, role);
            return text;
        }

        static void ConfigureText(Text text, int size, Color color, TextAnchor alignment,
            UiFontRole role = UiFontRole.Body)
        {
            UiTypography.Apply(text, role);
            text.fontSize = ScaleGameplayHudFont(size);
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        static GameObject PlaceTextInCard(Text text, Transform cardParent, string cardName, Vector2 anchor,
            Vector2 position, Vector2 size, Color border, UiFontRole role = UiFontRole.Body)
        {
            GameObject card = CreateCard(cardParent, cardName, Ink, border);
            SetRect(card.GetComponent<RectTransform>(), anchor, anchor, ScaleGameplayHud(position), ScaleGameplayHud(size));
            text.transform.SetParent(card.transform, false);
            ConfigureText(text, 24, Color.white, TextAnchor.MiddleCenter, role);
            Stretch(text.rectTransform, ScaleGameplayHud(new Vector2(12f, 8f)), ScaleGameplayHud(new Vector2(-12f, -8f)));
            return card;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = ScaleGameplayHud(distance);
            outline.useGraphicAlpha = false;
        }

        static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00}";
        }

        public static Vector2 ScaleGameplayHud(Vector2 value)
        {
            return value * GameplayHudScale;
        }

        public static float ScaleGameplayHud(float value)
        {
            return value * GameplayHudScale;
        }

        public static int ScaleGameplayHudFont(int size)
        {
            return Mathf.RoundToInt(size * GameplayHudScale);
        }
    }
}
