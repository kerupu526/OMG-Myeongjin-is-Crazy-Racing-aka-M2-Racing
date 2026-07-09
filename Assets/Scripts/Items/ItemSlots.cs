using System;
using M2.Player;
using UnityEngine;

namespace M2.Items
{
    // Two-slot inventory (primary/secondary). Ctrl consumes whichever slot holds an
    // Accel item; E consumes whichever slot holds an Attack/Defense item.
    [RequireComponent(typeof(VehicleController))]
    public class ItemSlots : MonoBehaviour
    {
        public ItemDefinition PrimarySlot { get; private set; }
        public ItemDefinition SecondarySlot { get; private set; }

        // Fired the moment an item is actually consumed, so UI can show "used X" feedback.
        public event Action<ItemDefinition> OnItemUsed;

        VehicleController vehicleController;

        void Awake()
        {
            vehicleController = GetComponent<VehicleController>();
        }

        void OnEnable()
        {
            vehicleController.OnAccelItemUsed += UseAccelItem;
            vehicleController.OnAttackDefenseItemUsed += UseAttackDefenseItem;
        }

        void OnDisable()
        {
            vehicleController.OnAccelItemUsed -= UseAccelItem;
            vehicleController.OnAttackDefenseItemUsed -= UseAttackDefenseItem;
        }

        // CLAUDE.md: picking up an item while both slots are full replaces one of them
        // with the new pickup. Assumption: the primary slot is replaced.
        public void CollectItem(ItemDefinition definition)
        {
            if (PrimarySlot == null) PrimarySlot = definition;
            else if (SecondarySlot == null) SecondarySlot = definition;
            else PrimarySlot = definition;
        }

        void UseAccelItem()
        {
            if (PrimarySlot != null && PrimarySlot.type == ItemType.Accel)
            {
                vehicleController.ApplySpeedBoost(PrimarySlot.speedBonus, PrimarySlot.duration);
                OnItemUsed?.Invoke(PrimarySlot);
                PrimarySlot = null;
            }
            else if (SecondarySlot != null && SecondarySlot.type == ItemType.Accel)
            {
                vehicleController.ApplySpeedBoost(SecondarySlot.speedBonus, SecondarySlot.duration);
                OnItemUsed?.Invoke(SecondarySlot);
                SecondarySlot = null;
            }
        }

        void UseAttackDefenseItem()
        {
            bool usePrimary = PrimarySlot != null && PrimarySlot.type != ItemType.Accel;
            bool useSecondary = !usePrimary && SecondarySlot != null && SecondarySlot.type != ItemType.Accel;

            ItemDefinition item = usePrimary ? PrimarySlot : useSecondary ? SecondarySlot : null;
            if (item == null) return;

            if (item.type == ItemType.Attack)
            {
                ItemEffects.SpawnBomb(transform.position, item);
            }
            else
            {
                vehicleController.ActivateShield(item.duration);
            }

            OnItemUsed?.Invoke(item);

            if (usePrimary) PrimarySlot = null;
            else SecondarySlot = null;
        }
    }
}
