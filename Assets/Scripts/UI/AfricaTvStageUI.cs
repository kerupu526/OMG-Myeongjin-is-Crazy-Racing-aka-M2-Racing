using M2.Stage;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // 멘탈 게이지 표시 + 조작불가 배너 + 방송사고 경고 배너. Text/Image 기반 플레이스홀더.
    public class AfricaTvStageUI : MonoBehaviour
    {
        public AfricaTvMentalGauge mentalGauge;

        public Text mentalLabel;

        public GameObject lockoutPanel;
        public Text lockoutText;

        public GameObject warningPanel;
        public Text warningText;
        [Tooltip("경고 배너가 자동으로 사라지기까지 걸리는 시간(초).")]
        public float warningDisplayDuration = 3f;

        Coroutine warningHideRoutine;

        void OnEnable()
        {
            if (mentalGauge != null)
            {
                mentalGauge.OnLockoutStarted += HandleLockoutStarted;
                mentalGauge.OnLockoutEnded += HandleLockoutEnded;
            }
            AccidentWarningZone.OnWarningEntered += HandleWarningEntered;
        }

        void OnDisable()
        {
            if (mentalGauge != null)
            {
                mentalGauge.OnLockoutStarted -= HandleLockoutStarted;
                mentalGauge.OnLockoutEnded -= HandleLockoutEnded;
            }
            AccidentWarningZone.OnWarningEntered -= HandleWarningEntered;
        }

        void Start()
        {
            SetActive(lockoutPanel, false);
            SetActive(warningPanel, false);
        }

        void Update()
        {
            if (mentalLabel == null || mentalGauge == null) return;
            mentalLabel.text = $"멘탈: {Mathf.CeilToInt(mentalGauge.CurrentValue)}/{Mathf.CeilToInt(mentalGauge.maxValue)}";
        }

        void HandleLockoutStarted()
        {
            SetActive(lockoutPanel, true);
            if (lockoutText != null) lockoutText.text = "멘탈 붕괴!\n잠시 조작 불가";
        }

        void HandleLockoutEnded() => SetActive(lockoutPanel, false);

        void HandleWarningEntered()
        {
            SetActive(warningPanel, true);
            if (warningText != null) warningText.text = "⚠ 방송사고 위험구역 ⚠";

            if (warningHideRoutine != null) StopCoroutine(warningHideRoutine);
            warningHideRoutine = StartCoroutine(HideWarningAfterDelay());
        }

        System.Collections.IEnumerator HideWarningAfterDelay()
        {
            yield return new WaitForSeconds(warningDisplayDuration);
            SetActive(warningPanel, false);
            warningHideRoutine = null;
        }

        static void SetActive(GameObject obj, bool active)
        {
            if (obj != null) obj.SetActive(active);
        }
    }
}
