using System;
using System.Collections;
using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // 아프리카TV 게이지: 멘탈 (팬 응원 존에서 회복). 가득 차면 잠시 조작 불가.
    // 31차 확정 기준: 패시브 변화 없음. 공격 피격/방송사고 같은 명시적 사건으로만 상승하고
    // 팬 응원 존에서 회복한다. 가득 차면 2초간 조작 불가.
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
