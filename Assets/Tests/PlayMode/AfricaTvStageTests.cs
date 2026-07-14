using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using M2.Player;
using M2.Stage;

namespace M2.Tests.PlayMode
{
    public class AfricaTvStageTests : StableInputTestFixture
    {
        GameObject vehicleObject;
        VehicleController vehicle;
        AfricaTvMentalGauge mentalGauge;
        AfricaTvStageState stageState;

        public override void Setup()
        {
            base.Setup();
            AddTestKeyboard();

            vehicleObject = new GameObject("TestVehicle");
            vehicleObject.AddComponent<Rigidbody>();
            vehicle = vehicleObject.AddComponent<VehicleController>();
            vehicle.acceleration = 50f;

            mentalGauge = vehicleObject.AddComponent<AfricaTvMentalGauge>();
            mentalGauge.maxValue = 10f;
            mentalGauge.passiveRatePerSecond = 0f; // only external triggers move it in these tests
            mentalGauge.lockoutDuration = 0.2f;

            stageState = vehicleObject.AddComponent<AfricaTvStageState>();
            stageState.mentalGauge = mentalGauge;
            stageState.mentalBonusOnHit = 4f;
        }

        public override void TearDown()
        {
            if (vehicleObject != null) Object.DestroyImmediate(vehicleObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Mental_Gauge_Starts_Empty()
        {
            yield return null;
            Assert.AreEqual(0f, mentalGauge.CurrentValue, 0.01f, "Mental (danger-at-max gauge) should start empty, not full.");
        }

        [UnityTest]
        public IEnumerator Reaching_Full_Mental_Locks_Input_Then_Releases()
        {
            yield return null;
            mentalGauge.ModifyValue(mentalGauge.maxValue); // fill it completely -> HandleDepleted locks input

            vehicle.SetInputOverride(1f, 0f);
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(0f, vehicle.CurrentSpeed, 0.01f, "Vehicle should not respond to throttle while mental-lockout is active.");

            yield return new WaitForSeconds(0.35f); // past lockoutDuration (0.2s)

            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(vehicle.CurrentSpeed, 0f, "Vehicle should respond to throttle again once the lockout window ends.");
        }

        [UnityTest]
        public IEnumerator Attack_Hit_Drops_One_Balloon_And_Raises_Mental()
        {
            yield return null;
            vehicle.ApplyHitStun(0.1f);
            yield return null;

            Assert.AreEqual(1, stageState.MissedStarBalloonCount, "A landed attack hit should count as one missed 별풍선.");
            Assert.AreEqual(4f, mentalGauge.CurrentValue, 0.01f, "The same hit should also raise the mental gauge by mentalBonusOnHit (double hit).");
        }
    }
}
