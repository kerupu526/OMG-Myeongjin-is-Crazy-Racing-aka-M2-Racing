using M2.Core;
using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // 네더요새-specific race progress: 화상 경고 횟수 + 별점 + 용암 근처 피격 콤보 보너스 +
    // 용암 안에 있는 동안의 패시브 가열.
    // CLAUDE.md: "용암 근처에서 공격 피격 시 정지 중 체온 게이지 평소보다 훨씬 빠르게 상승
    // (콤보 전략 유도)." 목표(3★) 화상 경고 횟수 5/2/0회 이하. 추가목표(3★) 1:00/1:10/1:20 이내 완주.
    public class NetherFortressStageState : MonoBehaviour
    {
        public VehicleController vehicleController;
        public NetherFortressTemperatureGauge temperatureGauge;
        public LavaZone lavaZone;

        [Tooltip("평소 피격 시 체온 게이지 상승량 (플레이스홀더 수치).")]
        public float normalHitTempBonus = 15f;
        [Tooltip("용암 근처(LavaZone 안)에서 피격 시 체온 게이지 상승량 — '훨씬 빠르게'를 반영한 플레이스홀더 수치.")]
        public float lavaHitTempBonus = 40f;
        [Tooltip("용암 안에 머무는 동안(피격 여부와 무관하게) 초당 추가로 오르는 체온 — 플레이스홀더 " +
            "수치. 원래는 '피격 시에만' 콤보 보너스가 붙는 구조였는데, 플레이테스트에서 용암에 " +
            "그냥 들어가만 있어도 아무 반응이 없다는 피드백(\"용암존 들어갔을 때 온도 상승을 안함\")이 " +
            "나와서 추가 — StageGaugeSystem의 기본 패시브 상승(1/초)과는 별개로 이 값이 더해짐.")]
        public float lavaZonePassiveHeatPerSecond = 8f;

        [Header("화상 경고 횟수 별점 기준 (이하일 때 별 획득, 오름차순)")]
        public int warningThreshold1Star = 5;
        public int warningThreshold2Star = 2;
        public int warningThreshold3Star = 0;

        [Header("완주 시간 별점 기준 (초, 이내일 때 별 획득)")]
        public float timeThreshold1Star = 80f; // 1:20
        public float timeThreshold2Star = 70f; // 1:10
        public float timeThreshold3Star = 60f; // 1:00

        public int BurnWarningCount { get; private set; }

        GameManager gameManager;

        void Awake()
        {
            if (vehicleController == null)
            {
                vehicleController = GetComponentInParent<VehicleController>();
            }
            // StageGaugeSystem gates its own passive tick to RaceState.Racing (13차 작업 — 안
            // 그러면 Briefing/Countdown 중에도 게이지가 참). ModifyValue 자체는 그 게이트를
            // 안 거치므로, 여기서 직접 호출하는 이 패시브 가열도 같은 게이트를 걸어줘야 같은
            // 버그가 재현되지 않음.
            gameManager = FindFirstObjectByType<GameManager>();
        }

        void Update()
        {
            if (temperatureGauge == null || lavaZone == null || !lavaZone.IsPlayerInside) return;
            if (gameManager != null && gameManager.CurrentState != RaceState.Racing) return;

            temperatureGauge.ModifyValue(lavaZonePassiveHeatPerSecond * Time.deltaTime);
        }

        void OnEnable()
        {
            if (vehicleController != null)
            {
                vehicleController.OnHitByAttackItem += HandleHitByAttackItem;
            }
            if (temperatureGauge != null)
            {
                temperatureGauge.OnBurnWarning += HandleBurnWarning;
            }
        }

        void OnDisable()
        {
            if (vehicleController != null)
            {
                vehicleController.OnHitByAttackItem -= HandleHitByAttackItem;
            }
            if (temperatureGauge != null)
            {
                temperatureGauge.OnBurnWarning -= HandleBurnWarning;
            }
        }

        void HandleBurnWarning() => BurnWarningCount++;

        void HandleHitByAttackItem()
        {
            if (temperatureGauge == null) return;

            bool nearLava = lavaZone != null && lavaZone.IsPlayerInside;
            temperatureGauge.ModifyValue(nearLava ? lavaHitTempBonus : normalHitTempBonus);
        }

        public int ComputeWarningStars()
        {
            int stars = 0;
            if (BurnWarningCount <= warningThreshold1Star) stars++;
            if (BurnWarningCount <= warningThreshold2Star) stars++;
            if (BurnWarningCount <= warningThreshold3Star) stars++;
            return stars;
        }

        public int ComputeTimeStars(float finishTimeSeconds)
        {
            int stars = 0;
            if (finishTimeSeconds <= timeThreshold1Star) stars++;
            if (finishTimeSeconds <= timeThreshold2Star) stars++;
            if (finishTimeSeconds <= timeThreshold3Star) stars++;
            return stars;
        }

        public int ComputeTotalStars(float finishTimeSeconds) =>
            ComputeWarningStars() + ComputeTimeStars(finishTimeSeconds);
    }
}
