using M2.Network;
using NUnit.Framework;
using UnityEngine;

namespace M2.Tests.PlayMode
{
    public class NetworkRaceManagerLocalRaceTests
    {
        [Test]
        public void ConfigureSoloLocalRace_UsesOnlyTheHostGridSlot()
        {
            GameObject gameObject = new GameObject("LocalRaceManagerTest");
            try
            {
                NetworkRaceManager manager = gameObject.AddComponent<NetworkRaceManager>();

                manager.ConfigureSoloLocalRace();

                Assert.IsTrue(manager.IsSoloLocalRace);
                Assert.AreEqual(1, manager.requiredPlayers);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
