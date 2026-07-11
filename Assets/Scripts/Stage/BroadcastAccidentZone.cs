using System;
using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // 아프리카TV "방송사고 존". CLAUDE.md: "들어가면 10초간 조작 반전."
    [RequireComponent(typeof(Collider))]
    public class BroadcastAccidentZone : MonoBehaviour
    {
        [Tooltip("조향 반전이 지속되는 시간(초). CLAUDE.md: 10초.")]
        public float reversalDuration = 10f;

        // AfricaTvStageState subscribes to also bump the mental gauge — a real "방송사고"
        // should be stressful, and without this the mental gauge (passiveRatePerSecond = 0,
        // see AfricaTvMentalGauge) only ever moved from being hit by another player's attack
        // item, so it never visibly rose at all in solo play (playtester feedback: "멘탈
        // 게이지도 오르질 않아").
        public static event Action OnAccidentEntered;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            VehicleController vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle == null) return;

            vehicle.SetSteeringInvertedFor(reversalDuration);
            OnAccidentEntered?.Invoke();
        }
    }
}
