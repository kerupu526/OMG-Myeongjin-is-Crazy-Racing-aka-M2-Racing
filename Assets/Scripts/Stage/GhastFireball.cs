using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // 가스트 파이어볼. CLAUDE.md: "가스트 파이어볼에 맞으면 코스 밖으로 튕겨나감."
    // 이동하는 발사체 AI는 이번 범위 밖(우선순위 5) — 지금은 고정 위치의 트리거형 장애물로,
    // 맞으면 트랙 바깥쪽으로 넉백을 가하는 것까지만 구현.
    [RequireComponent(typeof(Collider))]
    public class GhastFireball : MonoBehaviour
    {
        [Tooltip("맞았을 때 트랙 중심에서 바깥쪽으로 밀어내는 힘의 크기 (플레이스홀더 수치).")]
        public float knockbackForce = 15f;
        public float knockbackDuration = 0.8f;

        [Tooltip("넉백 방향 계산 기준이 되는 트랙 중심점. 비워두면 이 오브젝트 위치를 기준으로 씀.")]
        public Transform trackCenter;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            VehicleController vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle == null) return;

            Vector3 origin = trackCenter != null ? trackCenter.position : transform.position;
            Vector3 outward = other.transform.position - origin;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.001f ? outward.normalized : other.transform.forward;

            vehicle.ApplyKnockback(outward * knockbackForce, knockbackDuration);
        }
    }
}
