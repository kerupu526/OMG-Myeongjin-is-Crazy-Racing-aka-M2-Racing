using NUnit.Framework;
using M2.Items;

namespace M2.Tests.PlayMode
{
    // Pure-logic tests for the Milestone 2b item wire scheme. No NetworkManager is
    // involved — these only exercise the byte-ID <-> ItemDefinition round trip and the
    // arithmetic ID layout, which is exactly the part that must stay consistent across
    // host and client.
    public class NetworkItemLogicTests
    {
        [Test]
        public void IdFor_Uses_TypeTimesTwo_Plus_Tier_Plus_One()
        {
            Assert.AreEqual(NetItemId.AccelBase, ItemCatalog.IdFor(ItemType.Accel, 0));
            Assert.AreEqual(NetItemId.AccelDerived, ItemCatalog.IdFor(ItemType.Accel, 1));
            Assert.AreEqual(NetItemId.AttackBase, ItemCatalog.IdFor(ItemType.Attack, 0));
            Assert.AreEqual(NetItemId.AttackDerived, ItemCatalog.IdFor(ItemType.Attack, 1));
            Assert.AreEqual(NetItemId.DefenseBase, ItemCatalog.IdFor(ItemType.Defense, 0));
            Assert.AreEqual(NetItemId.DefenseDerived, ItemCatalog.IdFor(ItemType.Defense, 1));
        }

        [Test]
        public void CreateFromId_None_Returns_Null()
        {
            Assert.IsNull(ItemCatalog.CreateFromId(NetItemId.None));
        }

        [Test]
        public void CreateFromId_Roundtrips_All_Six_Definitions()
        {
            AssertRoundtrip(ItemCatalog.CreateAccelBase());
            AssertRoundtrip(ItemCatalog.CreateAccelDerived());
            AssertRoundtrip(ItemCatalog.CreateAttackBase());
            AssertRoundtrip(ItemCatalog.CreateAttackDerived());
            AssertRoundtrip(ItemCatalog.CreateDefenseBase());
            AssertRoundtrip(ItemCatalog.CreateDefenseDerived());
        }

        // A definition -> its ID -> a freshly rebuilt definition must be identical in
        // every gameplay-relevant field. This is the guarantee the network path relies on:
        // the client never receives names/stats, only the ID, and rebuilds the rest.
        static void AssertRoundtrip(ItemDefinition original)
        {
            NetItemId id = ItemCatalog.IdFor(original.type, original.tier);
            ItemDefinition rebuilt = ItemCatalog.CreateFromId(id);

            Assert.IsNotNull(rebuilt, $"{original.itemName} should rebuild from id {id}.");
            Assert.AreEqual(original.itemName, rebuilt.itemName);
            Assert.AreEqual(original.type, rebuilt.type);
            Assert.AreEqual(original.tier, rebuilt.tier);
            Assert.AreEqual(original.duration, rebuilt.duration);
            Assert.AreEqual(original.speedBonus, rebuilt.speedBonus);
            Assert.AreEqual(original.armTime, rebuilt.armTime);
            Assert.AreEqual(original.attackRadius, rebuilt.attackRadius);
        }

        [Test]
        public void CreateRandomIdForSpawn_Always_Produces_A_Valid_NonNull_Item()
        {
            // 200 rolls: every result must be a real (non-None) item that rebuilds cleanly.
            for (int i = 0; i < 200; i++)
            {
                NetItemId id = ItemCatalog.CreateRandomIdForSpawn();
                Assert.AreNotEqual(NetItemId.None, id, "A spawn roll must never produce an empty item.");
                Assert.IsNotNull(ItemCatalog.CreateFromId(id), $"Rolled id {id} must rebuild to a definition.");
            }
        }
    }
}
