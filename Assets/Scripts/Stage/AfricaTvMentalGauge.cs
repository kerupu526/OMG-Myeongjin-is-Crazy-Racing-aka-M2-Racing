using System;
using System.Collections;
using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // 아프리카TV 게이지: 멘탈 (팬 응원 존에서 회복). 가득 차면 잠시 조작 불가.
    // CLAUDE.md는 초당 변화량을 명시하지 않음 — 기본값 0(공격 피격/방송사고 등 외부 트리거로만
    // 움직임)으로 두고 Inspector에서 밸런스 조정 가능하게 함 (플레이스홀더, 확정 필요).
    public class AfricaTvMentalGauge : StageGaugeSystem
    {
        [Header("Africa TV Mental")]
        [Tooltip("멘탈이 가득 찼을 때 조작 불가 상태가 지속되는 시간(초).")]
        public float lockoutDuration = 2f;

        public VehicleController vehicleController;

        // UI hooks into these to show/hide a "잠시 조작 불가" banner in sync with the lockout.
        public event Action OnLockoutStarted;
        public event Action OnLockoutEnded;

        protected override void Awake()
        {
            dangerAtMax = true;
            // StageGaugeSystem's field default (-2, meant for oxygen draining toward 0) would
            // otherwise apply here too and, combined with dangerAtMax's 0 starting value,
            // permanently clamp the gauge at 0 — it would never visibly move except on a hit.
            // Reset() would normally set a sane default, but that Editor-only callback never
            // fires when StageAssembler adds this component at runtime (AddComponent<>()).
            passiveRatePerSecond = 0f;
            base.Awake();

            if (vehicleController == null)
            {
                vehicleController = GetComponentInParent<VehicleController>();
            }
        }

        protected override void HandleDepleted()
        {
            OnLockoutStarted?.Invoke();
            if (vehicleController == null) return;
            vehicleController.SetInputLocked(true);
            StartCoroutine(LockoutRoutine());
        }

        IEnumerator LockoutRoutine()
        {
            yield return new WaitForSeconds(lockoutDuration);
            if (vehicleController != null) vehicleController.SetInputLocked(false);
            OnLockoutEnded?.Invoke();
        }

        protected override void HandleRecovered()
        {
            // 조작 잠금은 고정 시간(lockoutDuration) 뒤 자동 해제되므로 여기서 할 일 없음.
        }
    }
}
