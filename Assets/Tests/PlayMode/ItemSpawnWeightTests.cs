using NUnit.Framework;
using M2.Items;

namespace M2.Tests.PlayMode
{
    public class ItemSpawnWeightTests
    {
        [Test]
        public void Derived_Item_Weights_Are_Explicit_And_Rare_Items_Remain_Rare()
        {
            Assert.AreEqual(60, ItemCatalog.DerivedSpawnWeight(NetItemId.SuperGasoline));
            Assert.AreEqual(28, ItemCatalog.DerivedSpawnWeight(NetItemId.HappyBirthdayToYou));
            Assert.AreEqual(12, ItemCatalog.DerivedSpawnWeight(NetItemId.JaeSeokGasoline));
            Assert.AreEqual(4, ItemCatalog.DerivedSpawnWeight(NetItemId.AtomicBomb));
            Assert.AreEqual(72, ItemCatalog.DerivedSpawnWeight(NetItemId.SpikedShield));
            Assert.AreEqual(28, ItemCatalog.DerivedSpawnWeight(NetItemId.GoldenShield));
            Assert.AreEqual(0, ItemCatalog.DerivedSpawnWeight(NetItemId.Bomb));
        }

        [Test]
        public void Derived_Item_Selection_Uses_Weight_Boundaries_Not_Array_Order()
        {
            Assert.AreEqual(NetItemId.SuperGasoline, ItemCatalog.SelectDerivedId(ItemType.Accel, 0f));
            Assert.AreEqual(NetItemId.HappyBirthdayToYou, ItemCatalog.SelectDerivedId(ItemType.Accel, 0.60f));
            Assert.AreEqual(NetItemId.JaeSeokGasoline, ItemCatalog.SelectDerivedId(ItemType.Accel, 0.88f));

            Assert.AreEqual(NetItemId.C4, ItemCatalog.SelectDerivedId(ItemType.Attack, 0.01f));
            Assert.AreEqual(NetItemId.Dynamite, ItemCatalog.SelectDerivedId(ItemType.Attack, 0.23f));
            Assert.AreEqual(NetItemId.StickGrenade, ItemCatalog.SelectDerivedId(ItemType.Attack, 0.51f));
            Assert.AreEqual(NetItemId.AtomicBomb, ItemCatalog.SelectDerivedId(ItemType.Attack, 0.73f));
            Assert.AreEqual(NetItemId.LoveLetter, ItemCatalog.SelectDerivedId(ItemType.Attack, 0.77f));
        }
    }
}
