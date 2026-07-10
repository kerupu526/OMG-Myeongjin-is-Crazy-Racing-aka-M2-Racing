using UnityEngine;

namespace M2.Stage
{
    // 비키니시티 지형지물(장애물). CLAUDE.md: "공격 아이템에 맞거나 지형지물에 닿으면
    // '비법'을 떨어뜨림 (놓친 횟수가 별점 기준)." 공격 아이템 쪽은
    // VehicleController.OnHitByAttackItem -> BikiniCityStageState가 이미 처리하고,
    // 트랙 경계 벽 충돌은 VehicleController.OnWallHit -> BikiniCityStageState가 처리한다.
    // 이 컴포넌트는 그 둘과는 별개인, 트랙 위에 놓인 지형지물(장애물) 충돌만 담당한다.
    [RequireComponent(typeof(Collider))]
    public class TerrainHazard : MonoBehaviour
    {
        [Tooltip("같은 지형지물에 계속 붙어있어도 짧은 시간 안에는 중복으로 비법을 떨어뜨리지 않도록 하는 최소 재판정 간격(초). 물리 접촉이 같은 충돌 안에서 떨어졌다 붙었다 하는 경우를 대비.")]
        public float hitCooldown = 1f;

        float lastHitTime = -Mathf.Infinity;

        void OnCollisionEnter(Collision collision) => TryRegisterHit(collision.collider);

        void TryRegisterHit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (Time.time - lastHitTime < hitCooldown) return;

            BikiniCityStageState stageState = other.GetComponentInParent<BikiniCityStageState>();
            if (stageState == null) return;

            lastHitTime = Time.time;
            stageState.NotifyRecipeDropped();
        }
    }
}
