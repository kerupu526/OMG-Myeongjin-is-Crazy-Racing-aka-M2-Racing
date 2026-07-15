using M2.Core;
using M2.Network;
using M2.Player;
using UnityEngine;

namespace M2.Items
{
    /// <summary>
    /// Supplies speed mode's basic gasoline to every active racer. Supply is automatic;
    /// activation remains an explicit player item-use action.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeedModeGasolineDistributor : MonoBehaviour
    {
        GameManager gameManager;
        float elapsed;
        bool raceWasActive;

        void Awake()
        {
            gameManager = GetComponent<GameManager>();
        }

        void Update()
        {
            if (gameManager == null) gameManager = GetComponent<GameManager>();
            if (gameManager == null || !gameManager.IsSpeedMode ||
                gameManager.CurrentState != RaceState.Racing)
            {
                elapsed = 0f;
                raceWasActive = false;
                return;
            }

            // The baseline supply begins with the race. Subsequent supplies use the configured
            // cadence and follow the normal primary → secondary → primary-replace inventory rule.
            if (!raceWasActive)
            {
                raceWasActive = true;
                GrantGasoline();
                return;
            }

            float interval = Mathf.Max(0.05f, gameManager.speedModeGasolineInterval);
            elapsed += Time.deltaTime;
            while (elapsed >= interval)
            {
                elapsed -= interval;
                GrantGasoline();
            }
        }

        public void GrantGasoline()
        {
            if (gameManager == null) return;
            ItemDefinition gasoline = ItemCatalog.CreateFromId(NetItemId.Gasoline);
            if (gasoline == null) return;

            for (int i = 0; i < gameManager.vehicles.Count; i++)
            {
                VehicleController vehicle = gameManager.vehicles[i];
                if (vehicle == null) continue;

                NetworkItemSlots networkSlots = vehicle.GetComponent<NetworkItemSlots>();
                if (networkSlots != null)
                {
                    if (networkSlots.IsServer) networkSlots.ServerGrantSpeedModeGasoline();
                    continue;
                }

                ItemSlots slots = vehicle.GetComponent<ItemSlots>();
                if (slots != null) slots.CollectItem(gasoline);
            }
        }
    }
}
