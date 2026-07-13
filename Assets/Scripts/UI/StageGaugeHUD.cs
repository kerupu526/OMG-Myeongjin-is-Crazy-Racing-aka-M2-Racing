using M2.Stage;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    /// <summary>Single bottom-bar presentation for every StageGaugeSystem implementation.</summary>
    public class StageGaugeHUD : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.102f, 0.063f, 0.188f, 0.94f);
        static readonly Color Oxygen = new Color(0.373f, 0.847f, 0.961f);
        static readonly Color Mental = new Color(1f, 0.184f, 0.620f);
        static readonly Color Temperature = new Color(1f, 0.420f, 0.118f);

        StageGaugeSystem gauge;
        Text titleLabel;
        Text valueLabel;
        Image fill;
        readonly List<GameObject> presentationRoots = new List<GameObject>();
        bool presentationVisible;
        bool presentationVisibilityInitialized;

        void Start()
        {
            BuildLayout();
            SetPresentationVisible(false);
            M2.Core.GameManager gameManager = FindFirstObjectByType<M2.Core.GameManager>();
            if (gameManager != null)
            {
                gameManager.OnStateChanged += HandleRaceStateChanged;
                HandleRaceStateChanged(gameManager.CurrentState);
            }
            Canvas.ForceUpdateCanvases();
        }

        void OnDestroy()
        {
            M2.Core.GameManager gameManager = FindFirstObjectByType<M2.Core.GameManager>();
            if (gameManager != null) gameManager.OnStateChanged -= HandleRaceStateChanged;
        }

        void Update()
        {
            if (gauge == null) gauge = FindFirstObjectByType<StageGaugeSystem>();
            if (gauge == null || fill == null) return;

            if (!presentationVisible) return;

            float fraction = gauge.maxValue <= 0f ? 0f : Mathf.Clamp01(gauge.CurrentValue / gauge.maxValue);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMax = new Vector2(fraction, 1f);
            fill.color = IsDangerous(gauge, fraction) ? Color.red : AccentFor(gauge);

            titleLabel.text = $"{TitleFor(gauge)} <color=#D7D0EA>{SubtitleFor(gauge)}</color>";
            valueLabel.text = $"{Mathf.CeilToInt(gauge.CurrentValue)} / {Mathf.CeilToInt(gauge.maxValue)}";
        }

        void BuildLayout()
        {
            if (fill != null) return;
            GameObject container = CreateImage("StageGaugeContainer", transform, Ink);
            presentationRoots.Add(container);
            RectTransform containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 0f);
            containerRect.offsetMin = new Vector2(28f, 26f);
            containerRect.offsetMax = new Vector2(-28f, 104f);
            AddOutline(container, Color.white, new Vector2(2f, -2f));

            titleLabel = CreateText("Title", container.transform, 22, Color.white, TextAnchor.UpperLeft);
            titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleLabel.rectTransform.anchorMax = new Vector2(0.65f, 1f);
            titleLabel.rectTransform.offsetMin = new Vector2(16f, -34f);
            titleLabel.rectTransform.offsetMax = new Vector2(0f, -4f);
            valueLabel = CreateText("Value", container.transform, 22, new Color(1f, 0.851f, 0.239f), TextAnchor.UpperRight);
            valueLabel.rectTransform.anchorMin = new Vector2(0.65f, 1f);
            valueLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            valueLabel.rectTransform.offsetMin = new Vector2(0f, -34f);
            valueLabel.rectTransform.offsetMax = new Vector2(-16f, -4f);

            GameObject barBackground = CreateImage("BarBackground", container.transform, new Color(0.04f, 0.02f, 0.08f));
            RectTransform barRect = barBackground.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.offsetMin = new Vector2(16f, 10f);
            barRect.offsetMax = new Vector2(-16f, 38f);

            GameObject fillObject = CreateImage("Fill", barBackground.transform, Oxygen);
            fill = fillObject.GetComponent<Image>();
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.raycastTarget = false;
        }

        static bool IsDangerous(StageGaugeSystem stageGauge, float fraction)
        {
            return stageGauge.dangerAtMax ? fraction >= 0.8f : fraction <= 0.2f;
        }

        static Color AccentFor(StageGaugeSystem stageGauge)
        {
            if (stageGauge is BikiniCityOxygenGauge) return Oxygen;
            if (stageGauge is AfricaTvMentalGauge) return Mental;
            return Temperature;
        }

        static string TitleFor(StageGaugeSystem stageGauge)
        {
            if (stageGauge is BikiniCityOxygenGauge) return "💧 산소 게이지";
            if (stageGauge is AfricaTvMentalGauge) return "💥 멘탈 게이지";
            return "🔥 체온 게이지";
        }

        static string SubtitleFor(StageGaugeSystem stageGauge)
        {
            if (stageGauge is BikiniCityOxygenGauge) return "— 실시간 감소 중";
            if (stageGauge is AfricaTvMentalGauge) return "— 가득 차면 조작 불가";
            return "— 80%부터 화상 경고";
        }

        static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            Image image = obj.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return obj;
        }

        static Text CreateText(string name, Transform parent, int size, Color color, TextAnchor alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            Text text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = false;
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

        void HandleRaceStateChanged(M2.Core.RaceState state)
        {
            SetPresentationVisible(state == M2.Core.RaceState.Racing);
        }
    }
}
