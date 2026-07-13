using System.Collections;
using M2.Core;
using M2.Player;
using UnityEngine;

namespace M2.Items
{
    public static class ItemEffects
    {
        // Local (single-player) bomb: cosmetic marker + its own arm/explosion runner.
        public static void SpawnBomb(Vector3 position, ItemDefinition definition)
        {
            GameObject marker = SpawnBombVisual(position, definition);
            marker.AddComponent<BombRunner>().Init(definition);
        }

        // Just the visible bomb marker (no arm timer, no explosion). The networked path
        // (Milestone 2b) spawns this cosmetically on every peer via RPC while the host runs
        // the single authoritative explosion, so the visual is shared but the OverlapSphere
        // damage resolves in exactly one place.
        public static GameObject SpawnBombVisual(Vector3 position, ItemDefinition definition)
        {
            GameObject marker = new GameObject($"Bomb_{definition.itemName}");
            marker.transform.position = position;

            // 2.5D rule: the armed bomb reads as a billboard sprite, not a 3D mesh.
            GameObject spriteChild = new GameObject("Sprite");
            spriteChild.transform.SetParent(marker.transform);
            spriteChild.transform.localPosition = Vector3.up * 0.4f;
            SpriteRenderer spriteRenderer = spriteChild.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite(Color.black, new Color(1f, 0.3f, 0f), 64, 64f);
            spriteRenderer.sortingOrder = 5;
            spriteChild.AddComponent<BillboardSprite>();

            return marker;
        }
    }

    // Runs a bomb's arm-timer/explosion on its own marker object instead of needing
    // a persistent manager — the marker destroys itself once it goes off.
    class BombRunner : MonoBehaviour
    {
        ItemDefinition definition;

        public void Init(ItemDefinition def)
        {
            definition = def;
            StartCoroutine(ArmThenExplode());
        }

        IEnumerator ArmThenExplode()
        {
            yield return new WaitForSeconds(definition.armTime);

            foreach (Collider hit in Physics.OverlapSphere(transform.position, definition.attackRadius))
            {
                if (!hit.CompareTag("Player")) continue;

                VehicleController vehicle = hit.GetComponentInParent<VehicleController>();
                if (vehicle == null) continue;
                if (vehicle.TryConsumeShield()) continue;

                vehicle.ApplyHitStun();
            }

            Destroy(gameObject);
        }
    }
}
