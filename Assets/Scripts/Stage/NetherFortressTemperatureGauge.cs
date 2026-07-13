using System;
using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // 네더요새 게이지: 체온 (오아시스 존에서 회복). 가득 차면 화상 → 즉시 게임오버.
    // 31차 확정 기준: 평상시 +1/초, 80%에서 경고, 100에서 즉시 화상 게임오버.
    public class NetherFortressTemperatureGauge : StageGaugeSystem
    {
        [Header("Nether Fortress Temperature")]
        [Tooltip("이 비율(0~1, maxValue 대비) 이상으로 올라갈 때마다 '화상 경고'로 취급.")]
        [Range(0f, 1f)]
        public float warningThresholdFraction = 0.8f;

        public VehicleController vehicleController;

        public event Action OnBurnGameOver;
        public event Action OnBurnWarning;

        bool aboveWarningThreshold;

        protected override void Awake()
        {
            dangerAtMax = true;
            // StageGaugeSystem's field default (-2, meant for oxygen draining toward 0) would
            // otherwise apply here too and, combined with dangerAtMax's 0 starting value,
            // permanently clamp the gauge at 0 — it would never rise on its own, contradicting
            // CLAUDE.md's "체온 게이지가 빠르게 상승". Reset() would normally set a sane
            // default, but that Editor-only callback never fires when StageAssembler adds
            // this component at runtime (AddComponent<>()). Confirmed +1/sec baseline —
            // the original +6/sec placeholder emptied the whole 100-point gauge in under 17s,
            // leaving no time to actually drive before an instant (no-grace-period) game over.
            passiveRatePerSecond = 1f;
            base.Awake();

            if (vehicleController == null)
            {
                vehicleController = GetComponentInParent<VehicleController>();
            }

            OnValueChanged += HandleValueChanged;
        }

        void HandleValueChanged(float current, float max)
        {
            float warningThreshold = max * warningThresholdFraction;
            bool isAboveNow = current >= warningThreshold;

            if (isAboveNow && !aboveWarningThreshold)
            {
                OnBurnWarning?.Invoke();
            }
            aboveWarningThreshold = isAboveNow;
        }

        protected override void HandleDepleted()
        {
            OnBurnGameOver?.Invoke();
            if (vehicleController != null)
            {
                vehicleController.SetInputLocked(true);
            }
        }

        protected override void HandleRecovered()
        {
            // 화상 게임오버는 즉시/영구 상태라 회복 처리가 필요 없음.
        }
    }
}
