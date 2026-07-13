using M2.Core;
using M2.Player;
using UnityEngine;

namespace M2.Items
{
    // Shared construction of the visible item pickup (billboard sprite + floating bob,
    // optionally a trigger collider). Local scenes use the collider variant via
    // ItemSpawner; the networked scene builds collider-less cosmetic copies from replicated
    // spawn state (NetworkItemSpawnManager), so the two paths always look identical.
    public static class ItemPickupVisuals
    {
        // World radius the primitive-sphere trigger ends up with: the 1.1 local radius is
        // scaled by the 1.2 pickup scale below. Kept as a constant so the networked
        // distance-based pickup check can match the local trigger's reach.
        public const float TriggerWorldRadius = 1.1f * 1.2f;

        public static GameObject Create(Transform parent, ItemDefinition definition, float height, bool withTriggerCollider)
        {
            GameObject pickupObject;
            if (withTriggerCollider)
            {
                pickupObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                // 2.5D rule (CLAUDE.md): items render as a billboard sprite, never a visible
                // 3D mesh. The sphere stays only as the (invisible) pickup trigger volume.
                Object.Destroy(pickupObject.GetComponent<MeshRenderer>());
                Object.Destroy(pickupObject.GetComponent<MeshFilter>());

                SphereCollider collider = pickupObject.GetComponent<SphereCollider>();
                collider.isTrigger = true;
                // Left at the primitive default (~0.6m world radius) it barely out-sized the
                // 1.2m-wide vehicle, making pickups feel too fiddly to grab while driving
                // (playtester feedback: "먹기 쉽게 조금 널널하게"). A generous grab radius is
                // the norm for arcade racers, not a hitbox bug.
                collider.radius = 1.1f;
            }
            else
            {
                // Cosmetic-only copy (networked spawn markers): no collider, because pickup
                // detection there is done server-side by distance, not by trigger.
                pickupObject = new GameObject();
            }

            pickupObject.name = $"ItemPickup_{definition.itemName}";
            pickupObject.transform.SetParent(parent);
            pickupObject.transform.localPosition = Vector3.up * height;
            pickupObject.transform.localScale = Vector3.one * 1.2f;

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

            return pickupObject;
        }

        public static Color ColorForType(ItemType type) => type switch
        {
            ItemType.Accel => Color.yellow,
            ItemType.Attack => Color.red,
            _ => Color.cyan,
        };
    }
}
