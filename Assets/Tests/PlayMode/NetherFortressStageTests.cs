using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using M2.Player;
using M2.Stage;

namespace M2.Tests.PlayMode
{
    public class NetherFortressStageTests : InputTestFixture
    {
        GameObject vehicleObject;
        VehicleController vehicle;
        NetherFortressTemperatureGauge gauge;
        NetherFortressStageState stageState;
        GameObject lavaZoneObject;
        LavaZone lavaZone;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();

            vehicleObject = new GameObject("TestVehicle");
            vehicleObject.tag = "Player";
            vehicleObject.AddComponent<Rigidbody>();
            vehicleObject.AddComponent<BoxCollider>();
            vehicle = vehicleObject.AddComponent<VehicleController>();
            vehicle.acceleration = 50f;

            gauge = vehicleObject.AddComponent<NetherFortressTemperatureGauge>();
            gauge.maxValue = 10f;
            gauge.passiveRatePerSecond = 0f; // only external triggers move it in these tests
            gauge.warningThresholdFraction = 0.8f; // warning at 8/10

            lavaZoneObject = new GameObject("TestLavaZone");
            lavaZoneObject.transform.position = new Vector3(100f, 0f, 0f); // start far away
            BoxCollider lavaCollider = lavaZoneObject.AddComponent<BoxCollider>();
            lavaCollider.size = Vector3.one * 5f;
            // Reset() (which normally sets isTrigger = true) only fires when a component is
            // added through the Editor UI, not via a programmatic AddComponent<> call — set
            // it explicitly here to match what TestTrackBuilder does for its own colliders.
            lavaCollider.isTrigger = true;
            lavaZone = lavaZoneObject.AddComponent<LavaZone>();

            stageState = vehicleObject.AddComponent<NetherFortressStageState>();
            stageState.temperatureGauge = gauge;
            stageState.lavaZone = lavaZone;
            stageState.normalHitTempBonus = 3f;
            stageState.lavaHitTempBonus = 7f;
            // Zeroed here so the hit-bonus tests below measure only the discrete hit event —
            // the dedicated passive-heating test further down opts back into a nonzero rate.
            stageState.lavaZonePassiveHeatPerSecond = 0f;
        }

        public override void TearDown()
        {
            if (vehicleObject != null) Object.Destroy(vehicleObject);
            if (lavaZoneObject != null) Object.Destroy(lavaZoneObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Reaching_Full_Temperature_Triggers_Immediate_GameOver_And_Locks_Input()
        {
            bool gameOverFired = false;
            gauge.OnBurnGameOver += () => gameOverFired = true;

            yield return null;
            gauge.ModifyValue(gauge.maxValue); // fill it completely — no grace period, unlike oxygen

            Assert.IsTrue(gameOverFired, "Reaching full temperature should fire OnBurnGameOver immediately.");
            Assert.IsTrue(gauge.IsDepleted);

            Press(Keyboard.current.upArrowKey);
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(0f, vehicle.CurrentSpeed, 0.01f, "Vehicle input should stay locked after a burn game over.");
        }

        [UnityTest]
        public IEnumerator Crossing_Warning_Threshold_Upward_Fires_Once_Per_Crossing()
        {
            int warningCount = 0;
            gauge.OnBurnWarning += () => warningCount++;

            yield return null;
            gauge.ModifyValue(9f); // 0 -> 9, crosses the 8.0 threshold upward
            Assert.AreEqual(1, warningCount, "Crossing the warning threshold upward should fire exactly once.");

            gauge.ModifyValue(-9f); // 9 -> 0, drops back below threshold
            Assert.AreEqual(1, warningCount, "Dropping below the threshold must not fire another warning.");

            gauge.ModifyValue(9f); // cross upward again
            Assert.AreEqual(2, warningCount, "Re-crossing the threshold upward should fire again.");
        }

        [UnityTest]
        public IEnumerator Hit_Near_Lava_Applies_Bigger_Temperature_Bonus_Than_Normal_Hit()
        {
            yield return null;
            vehicle.ApplyHitStun(0.1f);
            yield return null;
            Assert.AreEqual(3f, gauge.CurrentValue, 0.01f, "A normal hit (not near lava) should apply normalHitTempBonus.");

            gauge.ModifyValue(-gauge.CurrentValue); // reset for a clean second measurement

            // Move the lava zone onto the vehicle and let the physics trigger register the overlap.
            lavaZoneObject.transform.position = vehicleObject.transform.position;
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(lavaZone.IsPlayerInside, "Vehicle should now be registered as inside the lava zone.");

            vehicle.ApplyHitStun(0.1f);
            yield return null;
            Assert.AreEqual(7f, gauge.CurrentValue, 0.01f, "A hit while near lava should apply the larger lavaHitTempBonus.");
        }

        [UnityTest]
        public IEnumerator Standing_In_Lava_Zone_Raises_Temperature_Passively_Without_Being_Hit()
        {
            // Regression test for playtester feedback ("용암존 들어갔을 때 온도 상승을 안함") — the
            // gauge used to only move on a discrete attack-item hit, so simply driving into and
            // sitting in the lava zone did nothing at all.
            stageState.lavaZonePassiveHeatPerSecond = 5f;

            lavaZoneObject.transform.position = vehicleObject.transform.position;
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(lavaZone.IsPlayerInside, "Vehicle should be registered as inside the lava zone.");

            yield return new WaitForSeconds(0.5f);

            Assert.Greater(gauge.CurrentValue, 0f,
                "Merely standing in the lava zone (no hit) should still raise the temperature gauge.");
        }
    }
}
