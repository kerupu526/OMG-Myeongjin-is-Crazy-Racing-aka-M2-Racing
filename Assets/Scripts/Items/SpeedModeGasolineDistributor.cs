using M2.Core;
using M2.Network;
using M2.Player;
using UnityEngine;

namespace M2.Items
{
    /// <summary>Issues the speed-mode's regular basic-gasoline pickup to every active racer.</summary>
    [DisallowMultipleComponent]
    public class SpeedModeGasolineDistributor : MonoBehaviour
    {
        GameManager gameManager;
        float elapsed;

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
            for (int i = 0; i < gameManager.vehicles.Count; i++)
            {
                VehicleController vehicle = gameManager.vehicles[i];
                if (vehicle == null) continue;

                ItemSlots localSlots = vehicle.GetComponent<ItemSlots>();
                if (localSlots != null)
                {
                    localSlots.CollectItem(ItemCatalog.CreateFromId(NetItemId.Gasoline));
                    continue;
                }

                NetworkItemSlots networkSlots = vehicle.GetComponent<NetworkItemSlots>();
                if (networkSlots != null && networkSlots.IsServer)
                    networkSlots.ServerCollect(NetItemId.Gasoline);
            }
        }
    }
}
