using M2.Player;
using M2.Core;
using UnityEngine;

namespace M2.Stage
{
    // 아프리카TV-specific race progress: 별풍선(star balloon) 손실 카운트 + 별점.
    // CLAUDE.md: "공격 아이템 피격 시 별풍선 손실 + 멘탈 게이지 추가 상승 (이중 타격)."
    // 목표(3★) 별풍선 놓친 횟수 15/5/0회 이하. 추가목표(3★) 2:30/2:50/3:15 이내 완주.
    public class AfricaTvStageState : MonoBehaviour, IRaceStarProvider
    {
        public VehicleController vehicleController;
        public AfricaTvMentalGauge mentalGauge;

        [Tooltip("공격 아이템에 맞았을 때 멘탈 게이지에 추가로 가하는 상승량 (별풍선 손실과는 별개의 이중 타격 효과, 플레이스홀더 수치).")]
        public float mentalBonusOnHit = 20f;

        [Tooltip("방송사고 존에 들어갔을 때 멘탈 게이지 상승량 (플레이스홀더 수치).")]
        public float mentalBonusOnAccidentZone = 15f;

        [Header("놓친 별풍선 개수 별점 기준 (이하일 때 별 획득, 오름차순)")]
        public int missedThreshold1Star = 15;
        public int missedThreshold2Star = 5;
        public int missedThreshold3Star = 0;

        [Header("완주 시간 별점 기준 (초, 이내일 때 별 획득)")]
        public float timeThreshold1Star = 195f; // 3:15
        public float timeThreshold2Star = 170f; // 2:50
        public float timeThreshold3Star = 150f; // 2:30

        public int MissedStarBalloonCount { get; private set; }

        void Awake()
        {
            if (vehicleController == null)
            {
                vehicleController = GetComponentInParent<VehicleController>();
            }
        }

        void OnEnable()
        {
            if (vehicleController != null)
            {
                vehicleController.OnHitByAttackItem += HandleHitByAttackItem;
            }
            BroadcastAccidentZone.OnAccidentEntered += HandleAccidentEntered;
        }

        void OnDisable()
        {
            if (vehicleController != null)
            {
                vehicleController.OnHitByAttackItem -= HandleHitByAttackItem;
            }
            BroadcastAccidentZone.OnAccidentEntered -= HandleAccidentEntered;
        }

        void HandleHitByAttackItem()
        {
            // 이중 타격: 별풍선 손실(놓친 횟수 증가) + 멘탈 게이지 추가 상승.
            MissedStarBalloonCount++;
            if (mentalGauge != null)
            {
                mentalGauge.ModifyValue(mentalBonusOnHit);
            }
        }

        void HandleAccidentEntered()
        {
            if (mentalGauge != null)
            {
                mentalGauge.ModifyValue(mentalBonusOnAccidentZone);
            }
        }

        public int ComputeMissedStars()
        {
            int stars = 0;
            if (MissedStarBalloonCount <= missedThreshold1Star) stars++;
            if (MissedStarBalloonCount <= missedThreshold2Star) stars++;
            if (MissedStarBalloonCount <= missedThreshold3Star) stars++;
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
            ComputeMissedStars() + ComputeTimeStars(finishTimeSeconds);
    }
}
