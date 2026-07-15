using UnityEngine;

namespace M2.Stage
{
    // 네더요새 냉각(오아시스) 구역 — 안에 머무는 동안 체온 게이지가 서서히 식는다.
    // CLAUDE.md: "체온 (오아시스 존에서 회복)" — 이전까지 실제로 구현된 적이 없었던 부분
    // (플레이테스트로 드러남). StageAssembler.CreateOasisZone이 세로(트랙 진행 방향)로 길고
    // 가로(트랙 폭 방향)로 좁은 형태로 배치해서, 식히려면 레이싱 라인을 벗어나 일부러 좁은
    // 통로로 들어와야 하는 트레이드오프를 만든다.
    [RequireComponent(typeof(Collider))]
    public class OasisZone : MonoBehaviour
    {
        [Tooltip("안에 머무는 동안 초당 체온 게이지 감소량.")]
        public float coolingRatePerSecond = 10f;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerStay(Collider other)
        {
            // The collider can observe both the local owner and the remote kinematic racer in
            // a networked scene. Resolve the gauge from this exact vehicle instead of keeping
            // one shared reference that another racer can overwrite.
            if (!other.CompareTag("Player")) return;
            NetherFortressTemperatureGauge gauge = other.GetComponentInParent<NetherFortressTemperatureGauge>();
            if (gauge != null) gauge.ModifyValue(-coolingRatePerSecond * Time.deltaTime);
        }
    }
}
