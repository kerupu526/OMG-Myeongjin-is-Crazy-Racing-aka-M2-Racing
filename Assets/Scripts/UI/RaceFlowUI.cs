using M2.Core;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace M2.UI
{
    /// <summary>
    /// Subscribes to GameManager events to toggle briefing, countdown, and result panels.
    /// All panels are simple Text-based placeholders — not final UI design.
    /// </summary>
    public class RaceFlowUI : MonoBehaviour
    {
        [Header("References")]
        public GameManager gameManager;
        public RaceTimer raceTimer;
        [Tooltip("스테이지별 별점 상태 — 현재 씬의 스테이지에 맞는 것 하나만 채워두면 결과화면에 별점이 표시됨.")]
        public M2.Stage.BikiniCityStageState bikiniCityStageState;
        public M2.Stage.AfricaTvStageState africaTvStageState;
        public M2.Stage.NetherFortressStageState netherFortressStageState;

        [Header("Briefing Panel")]
        public GameObject briefingPanel;
        public Text briefingText;
        [TextArea(3, 6)]
        public string briefingMessage = "조작법 안내\n←/→: 조향 | ↑/↓: 가속/감속\nCtrl: 가속 아이템 | E: 공격/방어 아이템\nShift: 브레이크";
        [Tooltip("gameManager.waitForManualStart가 true일 때만 의미 있음 — 누르면 Briefing을 끝내고 카운트다운 시작.")]
        public Button startButton;

        [Header("Countdown Panel")]
        public GameObject countdownPanel;
        public Text countdownText;

        [Header("Result Panel")]
        public GameObject resultPanel;
        public Text resultText;

        GameObject briefingCard;
        GameObject resultCard;

        void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.OnStateChanged += HandleStateChanged;
            gameManager.OnCountdownTick += HandleCountdownTick;
            gameManager.OnRaceWon += HandleRaceWon;
            gameManager.OnRaceDraw += HandleRaceDraw;
        }

        void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.OnStateChanged -= HandleStateChanged;
            gameManager.OnCountdownTick -= HandleCountdownTick;
            gameManager.OnRaceWon -= HandleRaceWon;
            gameManager.OnRaceDraw -= HandleRaceDraw;
        }

        void Start()
        {
            ApplyPresentationStyle();
            // Hide everything initially
            SetPanelActive(briefingPanel, false);
            SetPanelActive(countdownPanel, false);
            SetPanelActive(resultPanel, false);

            if (briefingText != null)
            {
                briefingText.text = briefingMessage;
            }

            if (startButton != null && gameManager != null)
            {
                startButton.onClick.AddListener(gameManager.RequestStart);
            }

            // Sync with current GameManager state — if GameManager.Start() already
            // fired and set a state (e.g. Briefing), we need to show the right panel
            // now, because our HandleStateChanged already ran and was then undone by
            // the SetPanelActive(false) calls above.
            if (gameManager != null)
            {
                HandleStateChanged(gameManager.CurrentState);
            }
        }

        void Update()
        {
            // Space as a quick alternative to clicking startButton — RequestStart() is a
            // harmless no-op unless GameManager is actually in manual-start Briefing wait.
            if (gameManager != null && gameManager.CurrentState == RaceState.Briefing &&
                Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                gameManager.RequestStart();
            }
        }

        void HandleStateChanged(RaceState state)
        {
            SetPanelActive(briefingPanel, state == RaceState.Briefing);
            SetPanelActive(countdownPanel, state == RaceState.Countdown);
            // Result panel is shown by the win/draw handlers, not by state change.
        }

        void HandleCountdownTick(int tick)
        {
            if (countdownText == null) return;

            if (tick > 0)
            {
                countdownText.text = tick.ToString();
            }
            else
            {
                countdownText.text = "GO!";
                // Hide countdown shortly after "GO!" displays
                StartCoroutine(HideCountdownAfterDelay(0.8f));
            }
        }

        System.Collections.IEnumerator HideCountdownAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetPanelActive(countdownPanel, false);
        }

        void HandleRaceWon(LapTracker winner)
        {
            SetPanelActive(resultPanel, true);
            if (resultText == null) return;

            string stats = BuildStatsString();
            string starLine = BuildStarLine();
            resultText.text = $"<size=48><color=#FFD93D>🏆 승리!</color></size>\n" +
                $"<size=28>{winner.gameObject.name}</size>\n" +
                $"<color=#B6F36B>{BuildRuleLine()}</color>\n\n{BuildPlacings()}\n{stats}{starLine}";
        }

        string BuildStarLine()
        {
            if (raceTimer == null) return "";

            if (bikiniCityStageState != null)
            {
                int missedStars = bikiniCityStageState.ComputeMissedStars();
                int timeStars = bikiniCityStageState.ComputeTimeStars(raceTimer.ElapsedTime);
                return $"\n★ {missedStars + timeStars}/6 (비법 {missedStars}★ + 시간 {timeStars}★, 놓친 비법 {bikiniCityStageState.MissedRecipeCount}회)";
            }

            if (africaTvStageState != null)
            {
                int missedStars = africaTvStageState.ComputeMissedStars();
                int timeStars = africaTvStageState.ComputeTimeStars(raceTimer.ElapsedTime);
                return $"\n★ {missedStars + timeStars}/6 (별풍선 {missedStars}★ + 시간 {timeStars}★, 놓친 별풍선 {africaTvStageState.MissedStarBalloonCount}회)";
            }

            if (netherFortressStageState != null)
            {
                int warningStars = netherFortressStageState.ComputeWarningStars();
                int timeStars = netherFortressStageState.ComputeTimeStars(raceTimer.ElapsedTime);
                return $"\n★ {warningStars + timeStars}/6 (화상경고 {warningStars}★ + 시간 {timeStars}★, 화상 경고 {netherFortressStageState.BurnWarningCount}회)";
            }

            return "";
        }

        void HandleRaceDraw(string reason)
        {
            SetPanelActive(resultPanel, true);
            if (resultText == null) return;

            string stats = BuildStatsString();
            resultText.text = $"<size=48><color=#FF6BAA>무승부</color></size>\n" +
                $"<size=24>{reason}</size>\n<color=#B6F36B>{BuildRuleLine()}</color>\n\n" +
                $"{BuildPlacings()}\n{stats}";
        }

        string BuildRuleLine()
        {
            if (gameManager == null) return "레이스 결과";
            string mode = gameManager.IsSpeedMode ? "스피드전 · 5바퀴 · 100km/h" : "아이템전";
            string victory = gameManager.victoryCondition == VictoryCondition.StarBet ? "별점 내기" : "단순 완주";
            return $"{mode} · {victory}";
        }

        string BuildPlacings()
        {
            if (gameManager == null || gameManager.LastRaceResults.Count == 0) return "결과 데이터를 정리하는 중입니다.";

            var results = new List<RaceFinishResult>(gameManager.LastRaceResults);
            results.Sort((left, right) =>
            {
                if (left.finished != right.finished) return left.finished ? -1 : 1;
                if (gameManager.victoryCondition == VictoryCondition.StarBet && left.stars != right.stars)
                    return right.stars.CompareTo(left.stars);
                return left.finishTime.CompareTo(right.finishTime);
            });

            var builder = new StringBuilder("<color=#FFD93D>최종 순위</color>\n");
            for (int i = 0; i < results.Count; i++)
            {
                RaceFinishResult result = results[i];
                string name = result.racer != null ? result.racer.gameObject.name : "알 수 없는 레이서";
                string finish = result.finished ? FormatTime(result.finishTime) : "미완주";
                string star = gameManager.victoryCondition == VictoryCondition.StarBet ? $" · ★ {result.stars}/6" : string.Empty;
                builder.AppendLine($"{i + 1}위  {name} · {finish}{star}");
            }
            return builder.ToString();
        }

        string BuildStatsString()
        {
            if (raceTimer == null) return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"총 시간: {FormatTime(raceTimer.ElapsedTime)}");

            var splits = raceTimer.LapSplits;
            for (int i = 0; i < splits.Count; i++)
            {
                sb.AppendLine($"Lap {i + 1}: {FormatTime(splits[i])}");
            }
            return sb.ToString();
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.00}";
        }

        static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        void ApplyPresentationStyle()
        {
            StylePanel(briefingPanel, new Color(0.102f, 0.063f, 0.188f, 0.88f));
            StylePanel(countdownPanel, new Color(0.102f, 0.063f, 0.188f, 0.35f));
            StylePanel(resultPanel, new Color(0.102f, 0.063f, 0.188f, 0.92f));

            StyleText(briefingText, 30, Color.white, Color.black);
            StyleText(countdownText, 112, new Color(1f, 0.851f, 0.239f), new Color(0.102f, 0.063f, 0.188f));
            StyleText(resultText, 38, Color.white, new Color(0.102f, 0.063f, 0.188f));
            briefingCard = EnsureModalCard(briefingPanel, briefingText, "BriefingCard", new Vector2(720f, 460f));
            resultCard = EnsureModalCard(resultPanel, resultText, "ResultCard", new Vector2(760f, 560f));
            PlaceBriefingButtonInsideCard();

            if (startButton != null)
            {
                Image background = startButton.GetComponent<Image>();
                if (background != null) background.color = new Color(1f, 0.184f, 0.620f);
                Outline outline = startButton.GetComponent<Outline>();
                if (outline == null) outline = startButton.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.102f, 0.063f, 0.188f);
                outline.effectDistance = new Vector2(3f, -3f);

                Text buttonText = startButton.GetComponentInChildren<Text>(true);
                StyleText(buttonText, 28, Color.white, new Color(0.102f, 0.063f, 0.188f));
                if (buttonText != null)
                {
                    buttonText.text = "레이스 시작";
                    buttonText.rectTransform.anchorMin = Vector2.zero;
                    buttonText.rectTransform.anchorMax = Vector2.one;
                    buttonText.rectTransform.offsetMin = Vector2.zero;
                    buttonText.rectTransform.offsetMax = Vector2.zero;
                }
                EnsurePresentationButtonLabel(startButton.transform);
            }
        }

        void PlaceBriefingButtonInsideCard()
        {
            if (briefingCard == null || startButton == null) return;
            if (startButton.transform.parent != briefingCard.transform)
                startButton.transform.SetParent(briefingCard.transform, false);

            RectTransform rect = startButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 36f);
            rect.sizeDelta = new Vector2(260f, 68f);
        }

        static GameObject EnsureModalCard(GameObject panel, Text content, string name, Vector2 size)
        {
            if (panel == null || content == null) return null;
            Transform existing = panel.transform.Find(name);
            GameObject card = existing != null ? existing.gameObject : CreateCard(panel.transform, name,
                new Color(0.102f, 0.063f, 0.188f, 0.96f), new Color(1f, 0.851f, 0.239f));
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 20f);
            cardRect.sizeDelta = size;

            if (content.transform.parent != card.transform) content.transform.SetParent(card.transform, false);
            content.rectTransform.anchorMin = Vector2.zero;
            content.rectTransform.anchorMax = Vector2.one;
            content.rectTransform.offsetMin = new Vector2(42f, 38f);
            content.rectTransform.offsetMax = new Vector2(-42f, -38f);
            content.alignment = TextAnchor.MiddleCenter;
            return card;
        }

        static GameObject CreateCard(Transform parent, string name, Color fill, Color outlineColor)
        {
            GameObject card = new GameObject(name, typeof(RectTransform));
            card.transform.SetParent(parent, false);
            Image image = card.AddComponent<Image>();
            image.color = fill;
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(3f, -3f);
            return card;
        }

        static void EnsurePresentationButtonLabel(Transform buttonTransform)
        {
            Transform existing = buttonTransform.Find("PresentationLabel");
            Text label = existing != null ? existing.GetComponent<Text>() : null;
            if (label == null)
            {
                GameObject labelObject = new GameObject("PresentationLabel", typeof(RectTransform));
                labelObject.transform.SetParent(buttonTransform, false);
                label = labelObject.AddComponent<Text>();
            }

            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "레이스 시작";
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            StyleText(label, 28, Color.white, new Color(0.102f, 0.063f, 0.188f));
        }

        static void StylePanel(GameObject panel, Color color)
        {
            if (panel == null) return;
            Image image = panel.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        static void StyleText(Text text, int size, Color color, Color outlineColor)
        {
            if (text == null) return;
            text.fontSize = size;
            text.color = color;
            text.supportRichText = true;
            Outline outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }
}
