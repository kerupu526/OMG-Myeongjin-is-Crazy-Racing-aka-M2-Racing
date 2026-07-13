using M2.Player;
using M2.Stage;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // 체온 게이지 표시 + 화상 경고 플래시 + 화상 게임오버 패널. Text/Image 기반 플레이스홀더.
    public class NetherFortressStageUI : MonoBehaviour
    {
        public NetherFortressTemperatureGauge temperatureGauge;
        public VehicleController vehicleController;

        public Text temperatureLabel;

        public Image warningFlash;
        [Tooltip("화상 경고 플래시가 얼마나 오래 보이는지(초).")]
        public float warningFlashDuration = 0.5f;

        public GameObject gameOverPanel;
        public Text gameOverText;

        Coroutine warningFlashRoutine;

        void OnEnable()
        {
            if (temperatureGauge == null) return;
            temperatureGauge.OnBurnWarning += HandleBurnWarning;
            temperatureGauge.OnBurnGameOver += HandleBurnGameOver;
        }

        void OnDisable()
        {
            if (temperatureGauge == null) return;
            temperatureGauge.OnBurnWarning -= HandleBurnWarning;
            temperatureGauge.OnBurnGameOver -= HandleBurnGameOver;
        }

        void Start()
        {
            SetActive(warningFlash != null ? warningFlash.gameObject : null, false);
            SetActive(gameOverPanel, false);
            if (FindFirstObjectByType<StageGaugeHUD>() != null && temperatureLabel != null)
            {
                temperatureLabel.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (temperatureLabel == null || temperatureGauge == null) return;
            temperatureLabel.text = $"체온: {Mathf.CeilToInt(temperatureGauge.CurrentValue)}/{Mathf.CeilToInt(temperatureGauge.maxValue)}";
        }

        void HandleBurnWarning()
        {
            if (warningFlash == null) return;

            if (warningFlashRoutine != null) StopCoroutine(warningFlashRoutine);
            warningFlashRoutine = StartCoroutine(FlashWarning());
        }

        System.Collections.IEnumerator FlashWarning()
        {
            warningFlash.gameObject.SetActive(true);
            yield return new WaitForSeconds(warningFlashDuration);
            warningFlash.gameObject.SetActive(false);
            warningFlashRoutine = null;
        }

        void HandleBurnGameOver()
        {
            SetActive(gameOverPanel, true);
            if (gameOverText != null) gameOverText.text = "게임 오버!\n(화상)";
            if (vehicleController != null) vehicleController.SetInputLocked(true);
        }

        static void SetActive(GameObject obj, bool active)
        {
            if (obj != null) obj.SetActive(active);
        }
    }
}
