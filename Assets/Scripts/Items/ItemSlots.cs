using System;
using System.Collections.Generic;
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

        [Tooltip("기본 폭탄을 사용하지 않고 보유하면 원자폭탄으로 변하는 시간(초).")]
        public float atomicUpgradeDelay = 150f;

        VehicleController vehicleController;
        readonly List<RemoteC4Charge> remoteCharges = new List<RemoteC4Charge>();
        float primaryHeldTime;
        float secondaryHeldTime;

        void Awake()
        {
            vehicleController = GetComponent<VehicleController>();
        }

        void OnEnable()
        {
            vehicleController.OnAccelItemUsed += UseAccelItem;
            vehicleController.OnAttackDefenseItemUsed += UseAttackDefenseItem;
            vehicleController.OnRemoteItemTriggered += DetonateRemoteCharges;
            vehicleController.OnHitByAttackItem += DetonateChargingBomb;
        }

        void OnDisable()
        {
            vehicleController.OnAccelItemUsed -= UseAccelItem;
            vehicleController.OnAttackDefenseItemUsed -= UseAttackDefenseItem;
            vehicleController.OnRemoteItemTriggered -= DetonateRemoteCharges;
            vehicleController.OnHitByAttackItem -= DetonateChargingBomb;
        }

        void Update()
        {
            PrimarySlot = TickAtomicUpgrade(PrimarySlot, ref primaryHeldTime);
            SecondarySlot = TickAtomicUpgrade(SecondarySlot, ref secondaryHeldTime);
            remoteCharges.RemoveAll(charge => charge == null);
        }

        ItemDefinition TickAtomicUpgrade(ItemDefinition slot, ref float heldTime)
        {
            if (slot == null || slot.id != NetItemId.Bomb)
            {
                heldTime = 0f;
                return slot;
            }

            heldTime += Time.deltaTime;
            if (heldTime < atomicUpgradeDelay) return slot;
            heldTime = 0f;
            return ItemCatalog.CreateFromId(NetItemId.AtomicBomb);
        }

        // Confirmed rule: primary fills first, secondary fills second, and a pickup collected
        // while both are full replaces the primary slot.
        public void CollectItem(ItemDefinition definition)
        {
            if (PrimarySlot == null)
            {
                PrimarySlot = definition;
                primaryHeldTime = 0f;
            }
            else if (SecondarySlot == null)
            {
                SecondarySlot = definition;
                secondaryHeldTime = 0f;
            }
            else
            {
                PrimarySlot = definition;
                primaryHeldTime = 0f;
            }
        }

        void UseAccelItem()
        {
            if (PrimarySlot != null && PrimarySlot.type == ItemType.Accel)
            {
                vehicleController.ApplySpeedBoost(PrimarySlot.speedBonus, PrimarySlot.duration);
                OnItemUsed?.Invoke(PrimarySlot);
                PrimarySlot = null;
                primaryHeldTime = 0f;
            }
            else if (SecondarySlot != null && SecondarySlot.type == ItemType.Accel)
            {
                vehicleController.ApplySpeedBoost(SecondarySlot.speedBonus, SecondarySlot.duration);
                OnItemUsed?.Invoke(SecondarySlot);
                SecondarySlot = null;
                secondaryHeldTime = 0f;
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
                RemoteC4Charge charge = ItemEffects.SpawnAttack(transform.position, item, vehicleController);
                if (charge != null) remoteCharges.Add(charge);
            }
            else
            {
                vehicleController.ActivateShield(item.duration, item.shieldStrength);
            }

            OnItemUsed?.Invoke(item);

            if (usePrimary)
            {
                PrimarySlot = null;
                primaryHeldTime = 0f;
            }
            else
            {
                SecondarySlot = null;
                secondaryHeldTime = 0f;
            }
        }

        void DetonateRemoteCharges()
        {
            for (int i = remoteCharges.Count - 1; i >= 0; i--)
            {
                if (remoteCharges[i] != null) remoteCharges[i].Detonate();
            }
            remoteCharges.Clear();
        }

        void DetonateChargingBomb()
        {
            bool primaryCharging = PrimarySlot != null && PrimarySlot.id == NetItemId.Bomb;
            bool secondaryCharging = SecondarySlot != null && SecondarySlot.id == NetItemId.Bomb;
            if (!primaryCharging && !secondaryCharging) return;

            // Clear before resolving the blast: the owner is inside the 10km radius, so the hit
            // event would otherwise recursively detonate the same charging item.
            if (primaryCharging)
            {
                PrimarySlot = null;
                primaryHeldTime = 0f;
            }
            else
            {
                SecondarySlot = null;
                secondaryHeldTime = 0f;
            }

            ItemEffects.SpawnAttack(transform.position,
                ItemCatalog.CreateFromId(NetItemId.AtomicBomb), vehicleController);
        }
    }
}
