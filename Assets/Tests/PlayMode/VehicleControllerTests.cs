using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using M2.Player;

namespace M2.Tests.PlayMode
{
    public class VehicleControllerTests : InputTestFixture
    {
        GameObject vehicleObject;
        VehicleController vehicle;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();

            vehicleObject = new GameObject("TestVehicle");
            vehicleObject.AddComponent<Rigidbody>();
            vehicle = vehicleObject.AddComponent<VehicleController>();
            vehicle.acceleration = 50f; // fast ramp so the test doesn't need many frames
        }

        public override void TearDown()
        {
            if (vehicleObject != null) Object.Destroy(vehicleObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Vehicle_Accelerates_When_Throttle_Held()
        {
            yield return null;
            Press(Keyboard.current.upArrowKey);

            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.Greater(vehicle.CurrentSpeed, 0f, "Vehicle should have gained forward speed while throttle is held.");

            // No explicit Release() here: by this point in the test, releasing the captured
            // control throws ArgumentNullException from the Input System's own test fixture
            // ("does not have an associated state") — base.TearDown() already resets all
            // test-added devices/state regardless, so this cleanup call is both unnecessary
            // and unreliable.
        }

        [UnityTest]
        public IEnumerator Vehicle_Stays_Stopped_When_Input_Locked()
        {
            vehicle.SetInputLocked(true);
            yield return null;
            Press(Keyboard.current.upArrowKey);

            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(0f, vehicle.CurrentSpeed, 0.01f, "A locked vehicle must not respond to throttle input.");
        }
    }
}
