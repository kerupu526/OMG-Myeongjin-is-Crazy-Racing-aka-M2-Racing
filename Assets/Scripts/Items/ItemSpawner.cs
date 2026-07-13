using System.Collections;
using UnityEngine;

namespace M2.Items
{
    // Marks a point on the track where an item pickup spawns. Rolls a fresh random
    // item (type + 10% derived chance) each time it (re)spawns, per CLAUDE.md.
    public class ItemSpawner : MonoBehaviour
    {
        public float respawnDelay = 5f;
        public float pickupHeight = 1f;

        Coroutine respawnRoutine;
        GameObject activePickup;

        /// <summary>Whether this point may show and respawn a track pickup.</summary>
        public bool SpawnEnabled { get; private set; } = true;

        void OnEnable()
        {
            if (SpawnEnabled) SpawnNow();
        }

        void OnDisable()
        {
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = null;
            ClearActivePickup();
        }

        /// <summary>
        /// Enables or disables this track pickup point. Disabling clears its active item and any
        /// pending respawn so a speed race cannot briefly expose an item pickup.
        /// </summary>
        public void SetSpawnEnabled(bool enabled)
        {
            if (SpawnEnabled == enabled) return;
            SpawnEnabled = enabled;

            if (!enabled)
            {
                if (respawnRoutine != null) StopCoroutine(respawnRoutine);
                respawnRoutine = null;
                ClearActivePickup();
                return;
            }

            if (isActiveAndEnabled) SpawnNow();
        }

        public void NotifyCollected()
        {
            activePickup = null;
            if (!SpawnEnabled || !isActiveAndEnabled) return;
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            SpawnNow();
        }

        void SpawnNow()
        {
            if (!SpawnEnabled || activePickup != null) return;
            ItemDefinition definition = ItemCatalog.CreateRandomForSpawn();

            GameObject pickupObject = ItemPickupVisuals.Create(transform, definition, pickupHeight, withTriggerCollider: true);
            activePickup = pickupObject;

            ItemPickup pickup = pickupObject.AddComponent<ItemPickup>();
            pickup.definition = definition;
            pickup.owner = this;
        }

        void ClearActivePickup()
        {
            if (activePickup != null) Destroy(activePickup);
            activePickup = null;
        }
    }
}
