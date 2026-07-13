using M2.Core;
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
            resultText.text = $"🏆 승리!\n{winner.gameObject.name}\n\n{stats}{starLine}";
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
            resultText.text = $"무승부\n{reason}\n\n{stats}";
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
