using M2.Stage;
using NUnit.Framework;
using UnityEngine;

namespace M2.Tests.PlayMode
{
    public class StageBalanceDefaultsTests
    {
        GameObject africa;
        GameObject nether;

        [TearDown]
        public void TearDown()
        {
            if (africa != null) Object.DestroyImmediate(africa);
            if (nether != null) Object.DestroyImmediate(nether);
        }

        [Test]
        public void AfricaTv_Uses_Confirmed_Mental_Baseline()
        {
            africa = new GameObject("AfricaBalance");
            AfricaTvMentalGauge gauge = africa.AddComponent<AfricaTvMentalGauge>();
            AfricaTvStageState state = africa.AddComponent<AfricaTvStageState>();

            Assert.AreEqual(0f, gauge.passiveRatePerSecond);
            Assert.AreEqual(2f, gauge.lockoutDuration);
            Assert.AreEqual(20f, state.mentalBonusOnHit);
            Assert.AreEqual(15f, state.mentalBonusOnAccidentZone);
        }

        [Test]
        public void NetherFortress_Uses_Confirmed_Temperature_Baseline()
        {
            nether = new GameObject("NetherBalance");
            NetherFortressTemperatureGauge gauge = nether.AddComponent<NetherFortressTemperatureGauge>();
            NetherFortressStageState state = nether.AddComponent<NetherFortressStageState>();

            Assert.AreEqual(1f, gauge.passiveRatePerSecond);
            Assert.AreEqual(0.8f, gauge.warningThresholdFraction);
            Assert.AreEqual(15f, state.normalHitTempBonus);
            Assert.AreEqual(40f, state.lavaHitTempBonus);
            Assert.AreEqual(10f, state.lavaZonePassiveHeatPerSecond);
        }
    }
}
