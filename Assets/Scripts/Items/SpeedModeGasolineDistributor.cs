using M2.Core;
using M2.Network;
using M2.Player;
using UnityEngine;

namespace M2.Items
{
    /// <summary>Automatically applies speed mode's basic gasoline boost to every active racer.</summary>
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

            // The baseline boost begins with the race. Subsequent boosts use the configured
            // cadence and bypass the item inventory entirely.
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
                    if (networkSlots.IsServer) networkSlots.ServerApplySpeedModeGasoline();
                    continue;
                }

                vehicle.ApplySpeedBoost(gasoline.speedBonus, gasoline.duration);
            }
        }
    }
}
