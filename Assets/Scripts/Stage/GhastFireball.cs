using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // 가스트 파이어볼. CLAUDE.md: "가스트 파이어볼에 맞으면 코스 밖으로 튕겨나감."
    // 이동하는 발사체 AI는 이번 범위 밖(우선순위 5) — 지금은 고정 위치의 트리거형 장애물로,
    // 맞으면 트랙 바깥쪽으로 가볍게 튕겨내는 것까지만 구현(조작을 막던 넉백 → 통통 튀는 방식으로 변경).
    [RequireComponent(typeof(Collider))]
    public class GhastFireball : MonoBehaviour
    {
        [Tooltip("맞았을 때 트랙 바깥쪽으로 튕겨내는 힘의 크기. 조작을 막지 않는 가벼운 '통통 튀는' " +
            "밀어냄이라 예전 넉백처럼 차가 먹통이 되지 않음 (플레이스홀더 수치).")]
        public float bounceForce = 9f;

        [Tooltip("넉백 방향 계산 기준이 되는 트랙 중심점. 비워두면 이 오브젝트 위치를 기준으로 씀.")]
        public Transform trackCenter;

        [Tooltip("한 번 튕긴 뒤 다시 판정되기까지의 최소 간격(초). 넉백 힘이 약해 트리거 반경을 " +
            "채 못 벗어난 채로 다시 밀고 들어가면 매 프레임 재판정되어 조작이 안 먹는 것처럼 " +
            "보였던 문제 — 이 쿨다운으로 한 번 튕긴 뒤엔 확실히 벗어날 시간을 줌.")]
        public float hitCooldown = 1.5f;

        float lastHitTime = -Mathf.Infinity;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (Time.time - lastHitTime < hitCooldown) return;

            VehicleController vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle == null) return;

            lastHitTime = Time.time;

            Vector3 origin = trackCenter != null ? trackCenter.position : transform.position;
            Vector3 outward = other.transform.position - origin;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.001f ? outward.normalized : other.transform.forward;

            vehicle.ApplyBounce(outward * bounceForce);
        }
    }
}
