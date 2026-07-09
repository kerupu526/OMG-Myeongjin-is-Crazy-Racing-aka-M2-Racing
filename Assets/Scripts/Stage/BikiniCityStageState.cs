using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // Tracks 비키니시티-specific race progress that isn't part of the shared gauge:
    // "비법" drops (missed-collectible count) and the resulting star rating.
    // CLAUDE.md: 목표(3★) 비법 놓친 횟수 10/3/0회 이하, 추가목표(3★) 완주시간 1:45/2:00/2:30 이내.
    public class BikiniCityStageState : MonoBehaviour
    {
        public VehicleController vehicleController;

        [Header("놓친 비법 개수 별점 기준 (이하일 때 별 획득, 오름차순)")]
        public int missedThreshold1Star = 10;
        public int missedThreshold2Star = 3;
        public int missedThreshold3Star = 0;

        [Header("완주 시간 별점 기준 (초, 이내일 때 별 획득)")]
        public float timeThreshold1Star = 150f; // 2:30
        public float timeThreshold2Star = 120f; // 2:00
        public float timeThreshold3Star = 105f; // 1:45

        public int MissedRecipeCount { get; private set; }

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
            // TODO: 지형지물(장애물) 충돌로도 "비법"을 떨어뜨려야 하지만(CLAUDE.md), 아직
            // 비키니시티 전용 지형 오브젝트가 없어 이 트리거는 마련되지 않음. 지형 프리팹이
            // 생기면 해당 콜라이더에서 NotifyRecipeDropped()를 호출하도록 연결할 것.
        }

        void OnDisable()
        {
            if (vehicleController != null)
            {
                vehicleController.OnHitByAttackItem -= HandleHitByAttackItem;
            }
        }

        void HandleHitByAttackItem() => NotifyRecipeDropped();

        public void NotifyRecipeDropped()
        {
            MissedRecipeCount++;
        }

        public int ComputeMissedStars()
        {
            int stars = 0;
            if (MissedRecipeCount <= missedThreshold1Star) stars++;
            if (MissedRecipeCount <= missedThreshold2Star) stars++;
            if (MissedRecipeCount <= missedThreshold3Star) stars++;
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
