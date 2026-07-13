using M2.Items;
using M2.Network;
using NUnit.Framework;

namespace M2.Tests.PlayMode
{
    public class NetworkItemLogicTests
    {
        [Test]
        public void StableIds_Are_Grouped_By_Type()
        {
            Assert.AreEqual(1, (byte)NetItemId.Gasoline);
            Assert.AreEqual(16, (byte)NetItemId.Bomb);
            Assert.AreEqual(32, (byte)NetItemId.Shield);
            Assert.AreEqual(34, (byte)NetItemId.GoldenShield);
        }

        [Test]
        public void CreateFromId_None_Returns_Null()
        {
            Assert.IsNull(ItemCatalog.CreateFromId(NetItemId.None));
        }

        [Test]
        public void CreateFromId_Roundtrips_All_Thirteen_Definitions()
        {
            Assert.AreEqual(13, ItemCatalog.AllIds.Length);
            foreach (NetItemId id in ItemCatalog.AllIds)
            {
                ItemDefinition definition = ItemCatalog.CreateFromId(id);
                Assert.IsNotNull(definition, $"{id} should have a definition.");
                Assert.AreEqual(id, definition.id);
                Assert.AreEqual(ItemCatalog.TypeOf(id), definition.type);
                Assert.IsNotEmpty(definition.itemName);
                Assert.IsNotEmpty(definition.description);
                Assert.IsNotEmpty(definition.artKey);
            }
        }

        [Test]
        public void PdfStats_Are_Represented_In_Catalog()
        {
            ItemDefinition jaeSeok = ItemCatalog.CreateFromId(NetItemId.JaeSeokGasoline);
            Assert.AreEqual(1f, jaeSeok.duration);
            Assert.AreEqual(100f, jaeSeok.speedBonus);

            ItemDefinition c4 = ItemCatalog.CreateFromId(NetItemId.C4);
            Assert.AreEqual(ItemBehavior.RemoteC4, c4.behavior);
            Assert.AreEqual(-1f, c4.armTime);
            Assert.AreEqual(8f, c4.attackRadius);

            ItemDefinition atomic = ItemCatalog.CreateFromId(NetItemId.AtomicBomb);
            Assert.AreEqual(0f, atomic.armTime);
            Assert.AreEqual(10000f, atomic.attackRadius);

            ItemDefinition golden = ItemCatalog.CreateFromId(NetItemId.GoldenShield);
            Assert.AreEqual(1f, golden.duration);
            Assert.AreEqual(ShieldStrength.Golden, golden.shieldStrength);
        }

        [Test]
        public void CreateRandomIdForSpawn_Always_Produces_A_Valid_NonNull_Item()
        {
            for (int i = 0; i < 300; i++)
            {
                NetItemId id = ItemCatalog.CreateRandomIdForSpawn();
                Assert.AreNotEqual(NetItemId.None, id);
                Assert.IsNotNull(ItemCatalog.CreateFromId(id));
            }
        }

        [Test]
        public void TypeOf_Recovers_Type_For_Every_Real_Item()
        {
            foreach (NetItemId id in ItemCatalog.AllIds)
            {
                ItemDefinition definition = ItemCatalog.CreateFromId(id);
                Assert.AreEqual(definition.type, ItemCatalog.TypeOf(id), id.ToString());
            }
        }

        [Test]
        public void ApplyCollect_Fills_Primary_Then_Secondary_Then_Replaces_Primary()
        {
            NetworkItemSlots.ApplyCollect(NetItemId.None, NetItemId.None, NetItemId.Gasoline,
                out NetItemId primary, out NetItemId secondary);
            Assert.AreEqual(NetItemId.Gasoline, primary);
            Assert.AreEqual(NetItemId.None, secondary);

            NetworkItemSlots.ApplyCollect(NetItemId.Gasoline, NetItemId.None, NetItemId.Bomb,
                out primary, out secondary);
            Assert.AreEqual(NetItemId.Gasoline, primary);
            Assert.AreEqual(NetItemId.Bomb, secondary);

            NetworkItemSlots.ApplyCollect(NetItemId.Gasoline, NetItemId.Bomb, NetItemId.GoldenShield,
                out primary, out secondary);
            Assert.AreEqual(NetItemId.GoldenShield, primary);
            Assert.AreEqual(NetItemId.Bomb, secondary);
        }

        [Test]
        public void SelectAccelSlot_Prefers_Primary_Then_Secondary()
        {
            Assert.AreEqual(ItemSlotChoice.Primary,
                NetworkItemSlots.SelectAccelSlot(NetItemId.Gasoline, NetItemId.Bomb));
            Assert.AreEqual(ItemSlotChoice.Secondary,
                NetworkItemSlots.SelectAccelSlot(NetItemId.Bomb, NetItemId.JaeSeokGasoline));
            Assert.AreEqual(ItemSlotChoice.None,
                NetworkItemSlots.SelectAccelSlot(NetItemId.Bomb, NetItemId.GoldenShield));
        }

        [Test]
        public void SelectAttackDefenseSlot_Prefers_Primary_Then_Secondary_Skipping_Accel()
        {
            Assert.AreEqual(ItemSlotChoice.Primary,
                NetworkItemSlots.SelectAttackDefenseSlot(NetItemId.C4, NetItemId.Gasoline));
            Assert.AreEqual(ItemSlotChoice.Secondary,
                NetworkItemSlots.SelectAttackDefenseSlot(NetItemId.Gasoline, NetItemId.SpikedShield));
            Assert.AreEqual(ItemSlotChoice.None,
                NetworkItemSlots.SelectAttackDefenseSlot(NetItemId.Gasoline, NetItemId.SuperGasoline));
        }
    }
}
