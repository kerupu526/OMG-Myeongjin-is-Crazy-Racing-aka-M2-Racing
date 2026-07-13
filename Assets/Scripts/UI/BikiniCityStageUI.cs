using M2.Player;
using M2.Stage;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // 산소 게이지 표시 + 고갈 경고(화면 붉어짐) + 게임오버 UI. Text/Image 기반 플레이스홀더.
    public class BikiniCityStageUI : MonoBehaviour
    {
        public BikiniCityOxygenGauge oxygenGauge;
        public VehicleController vehicleController;

        public Text oxygenLabel;
        [Tooltip("고갈 경고 동안 표시되는 전체화면 붉은 오버레이.")]
        public Image warningOverlay;
        public GameObject gameOverPanel;
        public Text gameOverText;

        void OnEnable()
        {
            if (oxygenGauge == null) return;
            oxygenGauge.OnDepletionWarningStarted += HandleWarningStarted;
            oxygenGauge.OnDepletionWarningCancelled += HandleWarningCancelled;
            oxygenGauge.OnOxygenGameOver += HandleGameOver;
        }

        void OnDisable()
        {
            if (oxygenGauge == null) return;
            oxygenGauge.OnDepletionWarningStarted -= HandleWarningStarted;
            oxygenGauge.OnDepletionWarningCancelled -= HandleWarningCancelled;
            oxygenGauge.OnOxygenGameOver -= HandleGameOver;
        }

        void Start()
        {
            UiTypography.Apply(oxygenLabel);
            UiTypography.Apply(gameOverText, UiFontRole.Display);
            SetActive(warningOverlay != null ? warningOverlay.gameObject : null, false);
            SetActive(gameOverPanel, false);
            if (FindFirstObjectByType<StageGaugeHUD>() != null && oxygenLabel != null)
            {
                oxygenLabel.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (oxygenLabel == null || oxygenGauge == null) return;
            oxygenLabel.text = $"산소: {Mathf.CeilToInt(oxygenGauge.CurrentValue)}/{Mathf.CeilToInt(oxygenGauge.maxValue)}";
        }

        void HandleWarningStarted() => SetActive(warningOverlay != null ? warningOverlay.gameObject : null, true);

        void HandleWarningCancelled() => SetActive(warningOverlay != null ? warningOverlay.gameObject : null, false);

        void HandleGameOver()
        {
            SetActive(warningOverlay != null ? warningOverlay.gameObject : null, false);
            SetActive(gameOverPanel, true);
            if (gameOverText != null) gameOverText.text = "산소 부족\nGAME OVER";
            if (vehicleController != null) vehicleController.SetInputLocked(true);
        }

        static void SetActive(GameObject obj, bool active)
        {
            if (obj != null) obj.SetActive(active);
        }
    }
}
