using System;
using UnityEngine;

namespace M2.Stage
{
    // 방송사고 존 진입 전 경고 구역. CLAUDE.md: "방송사고 존: 진입 전 경고 있음." 실제 게임플레이
    // 효과는 없고, UI가 구독해서 경고 표지판/사이렌을 표시하는 신호 역할만 한다. static 이벤트인
    // 이유: 어느 경고 구역을 지났는지가 아니라 "경고가 떴다"는 사실 자체만 UI에 전달하면 되기 때문.
    [RequireComponent(typeof(Collider))]
    public class AccidentWarningZone : MonoBehaviour
    {
        public static event Action OnWarningEntered;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            OnWarningEntered?.Invoke();
        }
    }
}
