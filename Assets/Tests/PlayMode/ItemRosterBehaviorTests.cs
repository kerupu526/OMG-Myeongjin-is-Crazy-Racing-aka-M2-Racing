using System.Collections;
using M2.Items;
using M2.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace M2.Tests.PlayMode
{
    public class ItemRosterBehaviorTests
    {
        GameObject vehicleObject;

        [TearDown]
        public void TearDown()
        {
            if (vehicleObject != null) Object.DestroyImmediate(vehicleObject);
            foreach (AttackRunner runner in Object.FindObjectsByType<AttackRunner>(FindObjectsSortMode.None))
                Object.DestroyImmediate(runner.gameObject);
        }

        VehicleController CreateVehicle()
        {
            vehicleObject = new GameObject("ItemBehaviorVehicle");
            vehicleObject.tag = "Player";
            vehicleObject.AddComponent<Rigidbody>().useGravity = false;
            vehicleObject.AddComponent<BoxCollider>();
            return vehicleObject.AddComponent<VehicleController>();
        }

        [UnityTest]
        public IEnumerator ShieldPolicies_Block_Only_Their_Documented_Attacks()
        {
            VehicleController vehicle = CreateVehicle();
            yield return null;

            vehicle.ActivateShield(10f, ShieldStrength.Basic);
            Assert.IsTrue(vehicle.TryBlockAttack(ItemCatalog.CreateFromId(NetItemId.Bomb), out bool reflected));
            Assert.IsFalse(reflected);

            vehicle.ActivateShield(10f, ShieldStrength.Basic);
            Assert.IsFalse(vehicle.TryBlockAttack(ItemCatalog.CreateFromId(NetItemId.Dynamite), out _));

            vehicle.ActivateShield(10f, ShieldStrength.Spiked);
            Assert.IsTrue(vehicle.TryBlockAttack(ItemCatalog.CreateFromId(NetItemId.Dynamite), out reflected));
            Assert.IsTrue(reflected);

            vehicle.ActivateShield(10f, ShieldStrength.Golden);
            Assert.IsTrue(vehicle.TryBlockAttack(ItemCatalog.CreateFromId(NetItemId.C4), out reflected));
            Assert.IsFalse(reflected);

            vehicle.ActivateShield(10f, ShieldStrength.Golden);
            Assert.IsFalse(vehicle.TryBlockAttack(ItemCatalog.CreateFromId(NetItemId.AtomicBomb), out _));
        }

        [UnityTest]
        public IEnumerator C4_Waits_Until_Manual_Detonation()
        {
            VehicleController owner = CreateVehicle();
            yield return null;

            RemoteC4Charge charge = ItemEffects.SpawnAttack(
                new Vector3(100f, 0f, 100f), ItemCatalog.CreateFromId(NetItemId.C4), owner);
            Assert.IsNotNull(charge);
            yield return null;
            Assert.IsNotNull(charge, "C4 must not auto-detonate.");

            charge.Detonate();
            yield return null;
            Assert.IsTrue(charge == null);
        }

        [UnityTest]
        public IEnumerator Held_Bomb_Upgrades_To_AtomicBomb()
        {
            CreateVehicle();
            ItemSlots slots = vehicleObject.AddComponent<ItemSlots>();
            slots.atomicUpgradeDelay = 0f;
            yield return null;

            slots.CollectItem(ItemCatalog.CreateFromId(NetItemId.Bomb));
            yield return null;
            Assert.AreEqual(NetItemId.AtomicBomb, slots.PrimarySlot.id);
        }

        [Test]
        public void StickGrenade_Uses_FiveMeter_ProximityTrigger()
        {
            ItemDefinition grenade = ItemCatalog.CreateFromId(NetItemId.StickGrenade);
            Assert.AreEqual(ItemBehavior.ProximityGrenade, grenade.behavior);
            Assert.AreEqual(5f, grenade.triggerDistance);
            Assert.AreEqual(10f, grenade.attackRadius);
        }
    }
}
