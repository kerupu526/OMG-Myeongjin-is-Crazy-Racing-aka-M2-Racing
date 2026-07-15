using System.Collections.Generic;
using M2.Player;
using UnityEngine;

namespace M2.Stage
{
    // 네더요새 용암 근접 구역. 플레이어가 안에 있는 동안 NetherFortressStageState가
    // "용암 근처 피격은 체온이 훨씬 빠르게 오른다"는 콤보 페널티를 적용할 수 있도록
    // IsPlayerInside 플래그를 노출한다.
    [RequireComponent(typeof(Collider))]
    public class LavaZone : MonoBehaviour
    {
        readonly HashSet<VehicleController> vehiclesInside = new HashSet<VehicleController>();

        // Kept for existing local UI/tests. Networked consumers must ask for their own vehicle
        // so a remote car entering the same local trigger cannot heat the host's gauge.
        public bool IsPlayerInside => vehiclesInside.Count > 0;

        public bool IsVehicleInside(VehicleController vehicle) =>
            vehicle != null && vehiclesInside.Contains(vehicle);

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            VehicleController vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle != null) vehiclesInside.Add(vehicle);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            VehicleController vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle != null) vehiclesInside.Remove(vehicle);
        }

        void OnDisable() => vehiclesInside.Clear();
    }
}
