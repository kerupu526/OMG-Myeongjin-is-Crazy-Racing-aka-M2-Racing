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

        void OnEnable()
        {
            SpawnNow();
        }

        public void NotifyCollected()
        {
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
            ItemDefinition definition = ItemCatalog.CreateRandomForSpawn();

            GameObject pickupObject = ItemPickupVisuals.Create(transform, definition, pickupHeight, withTriggerCollider: true);

            ItemPickup pickup = pickupObject.AddComponent<ItemPickup>();
            pickup.definition = definition;
            pickup.owner = this;
        }
    }
}
