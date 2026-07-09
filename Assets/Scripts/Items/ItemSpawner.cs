using System.Collections;
using M2.Core;
using M2.Player;
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

            GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pickupObject.name = $"ItemPickup_{definition.itemName}";
            pickupObject.transform.SetParent(transform);
            pickupObject.transform.localPosition = Vector3.up * pickupHeight;
            pickupObject.transform.localScale = Vector3.one * 1.2f;

            // 2.5D rule (CLAUDE.md): items render as a billboard sprite, never a visible
            // 3D mesh. This sphere stays only as the (invisible) pickup trigger volume.
            Destroy(pickupObject.GetComponent<MeshRenderer>());
            Destroy(pickupObject.GetComponent<MeshFilter>());

            SphereCollider collider = pickupObject.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            ItemPickup pickup = pickupObject.AddComponent<ItemPickup>();
            pickup.definition = definition;
            pickup.owner = this;

            // Bobs the whole pickup (collider included) so it visibly floats in place.
            pickupObject.AddComponent<FloatingBob>();

            GameObject spriteChild = new GameObject("Sprite");
            spriteChild.transform.SetParent(pickupObject.transform);
            spriteChild.transform.localPosition = Vector3.zero;
            spriteChild.transform.localScale = Vector3.one;
            SpriteRenderer spriteRenderer = spriteChild.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite(ColorForType(definition.type), Color.black, 96, 64f);
            spriteRenderer.sortingOrder = 5;
            spriteChild.AddComponent<BillboardSprite>();
        }

        static Color ColorForType(ItemType type) => type switch
        {
            ItemType.Accel => Color.yellow,
            ItemType.Attack => Color.red,
            _ => Color.cyan,
        };
    }
}
