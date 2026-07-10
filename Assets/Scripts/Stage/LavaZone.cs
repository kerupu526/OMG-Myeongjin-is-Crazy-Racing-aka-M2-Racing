using UnityEngine;

namespace M2.Stage
{
    // 네더요새 용암 근접 구역. 플레이어가 안에 있는 동안 NetherFortressStageState가
    // "용암 근처 피격은 체온이 훨씬 빠르게 오른다"는 콤보 페널티를 적용할 수 있도록
    // IsPlayerInside 플래그를 노출한다.
    [RequireComponent(typeof(Collider))]
    public class LavaZone : MonoBehaviour
    {
        public bool IsPlayerInside { get; private set; }

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) IsPlayerInside = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player")) IsPlayerInside = false;
        }
    }
}
