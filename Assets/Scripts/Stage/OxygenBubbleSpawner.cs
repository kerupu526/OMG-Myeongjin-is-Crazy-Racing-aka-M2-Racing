using System.Collections;
using M2.Core;
using UnityEngine;

namespace M2.Stage
{
    // Marks a point on the 비키니시티 track where a 숨방울(oxygen bubble) pickup spawns.
    // Mirrors M2.Items.ItemSpawner's respawn pattern.
    public class OxygenBubbleSpawner : MonoBehaviour
    {
        public float respawnDelay = 8f;
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
            GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pickupObject.name = "OxygenBubble";
            pickupObject.transform.SetParent(transform);
            pickupObject.transform.localPosition = Vector3.up * pickupHeight;
            pickupObject.transform.localScale = Vector3.one * 1f;

            // 2.5D rule (CLAUDE.md): renders as a billboard sprite, never a visible 3D mesh.
            Destroy(pickupObject.GetComponent<MeshRenderer>());
            Destroy(pickupObject.GetComponent<MeshFilter>());

            SphereCollider collider = pickupObject.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            OxygenBubblePickup pickup = pickupObject.AddComponent<OxygenBubblePickup>();
            pickup.owner = this;

            pickupObject.AddComponent<FloatingBob>();

            GameObject spriteChild = new GameObject("Sprite");
            spriteChild.transform.SetParent(pickupObject.transform);
            spriteChild.transform.localPosition = Vector3.zero;
            spriteChild.transform.localScale = Vector3.one;
            SpriteRenderer spriteRenderer = spriteChild.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite(new Color(0.5f, 0.85f, 1f), Color.white, 96, 64f);
            spriteRenderer.sortingOrder = 5;
            spriteChild.AddComponent<M2.Player.BillboardSprite>();
        }
    }
}
