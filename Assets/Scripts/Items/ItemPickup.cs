using UnityEngine;

namespace M2.Items
{
    [RequireComponent(typeof(Collider))]
    public class ItemPickup : MonoBehaviour
    {
        public ItemDefinition definition;
        public ItemSpawner owner;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            ItemSlots slots = other.GetComponentInParent<ItemSlots>();
            if (slots == null) return;

            slots.CollectItem(definition);
            owner?.NotifyCollected();
            Destroy(gameObject);
        }
    }
}
