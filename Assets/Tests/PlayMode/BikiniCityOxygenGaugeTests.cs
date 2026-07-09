using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using M2.Stage;

namespace M2.Tests.PlayMode
{
    public class BikiniCityOxygenGaugeTests
    {
        GameObject gaugeObject;
        BikiniCityOxygenGauge gauge;

        [SetUp]
        public void SetUp()
        {
            gaugeObject = new GameObject("TestOxygenGauge");
            gauge = gaugeObject.AddComponent<BikiniCityOxygenGauge>();
            gauge.maxValue = 10f;
            gauge.passiveRatePerSecond = -1000f; // drains near-instantly so the test doesn't wait for a real 2-min depletion
            gauge.gameOverGraceSeconds = 0.2f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(gaugeObject);
        }

        [UnityTest]
        public IEnumerator Gauge_Depletes_And_Triggers_GameOver_After_Grace_Period()
        {
            bool gameOverFired = false;
            gauge.OnOxygenGameOver += () => gameOverFired = true;

            yield return new WaitForSeconds(0.35f); // deplete (near-instant) + grace period (0.2s)

            Assert.IsTrue(gauge.IsDepleted);
            Assert.IsTrue(gameOverFired, "Game over should fire once the grace period elapses while still depleted.");
        }

        [UnityTest]
        public IEnumerator Recovering_Before_Grace_Period_Cancels_GameOver()
        {
            bool gameOverFired = false;
            bool cancelled = false;
            gauge.OnOxygenGameOver += () => gameOverFired = true;
            gauge.OnDepletionWarningCancelled += () => cancelled = true;

            yield return new WaitForSeconds(0.05f); // let it deplete
            Assert.IsTrue(gauge.IsDepleted);

            gauge.ModifyValue(gauge.maxValue); // recover fully before the 0.2s grace period ends
            gauge.passiveRatePerSecond = 0f; // freeze so the recovered state actually holds for the assertion below

            yield return new WaitForSeconds(0.35f); // well past the original grace period

            Assert.IsTrue(cancelled, "Recovering before the grace period should cancel the pending game over.");
            Assert.IsFalse(gameOverFired, "Game over must not fire if the gauge recovered in time.");
        }
    }
}
