using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using M2.Player;

namespace M2.Tests.PlayMode
{
    public class VehicleControllerTests : StableInputTestFixture
    {
        GameObject vehicleObject;
        VehicleController vehicle;

        public override void Setup()
        {
            base.Setup();
            AddTestKeyboard();

            vehicleObject = new GameObject("TestVehicle");
            vehicleObject.AddComponent<Rigidbody>();
            vehicle = vehicleObject.AddComponent<VehicleController>();
            vehicle.acceleration = 50f; // fast ramp so the test doesn't need many frames
        }

        public override void TearDown()
        {
            if (vehicleObject != null) Object.DestroyImmediate(vehicleObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Vehicle_Accelerates_When_Throttle_Held()
        {
            yield return null;
            vehicle.SetInputOverride(1f, 0f);

            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.Greater(vehicle.CurrentSpeed, 0f, "Vehicle should have gained forward speed while throttle is held.");

            // The fixture removes its synthetic keyboard during teardown, so an explicit
            // release is unnecessary here.
        }

        [Test]
        public void Keyboard_Bindings_Keep_Arrow_And_Wasd_Controls()
        {
            InputAction throttle = typeof(VehicleController)
                .GetField("throttleAction", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(vehicle) as InputAction;
            Assert.IsNotNull(throttle);
            Assert.IsTrue(HasBinding(throttle, "<Keyboard>/upArrow"));
            Assert.IsTrue(HasBinding(throttle, "<Keyboard>/w"));

            InputAction steering = typeof(VehicleController)
                .GetField("steerAction", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(vehicle) as InputAction;
            Assert.IsNotNull(steering);
            Assert.IsTrue(HasBinding(steering, "<Keyboard>/rightArrow"));
            Assert.IsTrue(HasBinding(steering, "<Keyboard>/d"));
        }

        [UnityTest]
        public IEnumerator Vehicle_Stays_Stopped_When_Input_Locked()
        {
            vehicle.SetInputLocked(true);
            yield return null;
            vehicle.SetInputOverride(1f, 0f);

            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(0f, vehicle.CurrentSpeed, 0.01f, "A locked vehicle must not respond to throttle input.");
        }

        [UnityTest]
        public IEnumerator ApplyKnockback_Overrides_Velocity_And_Suspends_Throttle_Control()
        {
            yield return null;
            vehicle.SetInputOverride(1f, 0f);

            vehicle.ApplyKnockback(new Vector3(5f, 0f, 0f), 0.3f);
            yield return new WaitForFixedUpdate();

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            Assert.AreEqual(5f, rb.linearVelocity.x, 0.5f, "Knockback should set an immediate outward velocity.");

            // Throttle has been held since before the knockback — it must not override the
            // knockback velocity while isKnockedBack is active.
            yield return new WaitForFixedUpdate();
            Assert.Greater(Mathf.Abs(rb.linearVelocity.x), 0.1f, "Throttle input must not override velocity while knocked back.");
        }

        [UnityTest]
        public IEnumerator SetSteeringInvertedFor_Flips_Turn_Direction()
        {
            yield return null;
            vehicle.acceleration = 100f;
            vehicle.SetSteeringInvertedFor(5f);
            vehicle.SetInputOverride(1f, 1f);

            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForFixedUpdate(); // get moving so steering isn't gated by minSpeedToSteer
            }
            float yawBefore = vehicle.transform.eulerAngles.y;

            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            float yawAfter = vehicle.transform.eulerAngles.y;

            float delta = Mathf.DeltaAngle(yawBefore, yawAfter);
            Assert.Less(delta, 0f, "With steering inverted, holding 'right' should turn left (negative yaw delta) instead of right.");
        }

        [UnityTest]
        public IEnumerator IsOwnedLocally_Defaults_True_Without_A_NetworkObject()
        {
            // Guards the Netcode Milestone 1 ownership gate added to FixedUpdate
            // (M2.Network.NetworkVehicleSync) — every existing local scene (TestTrackBuilder,
            // StageTestSelector, and every other PlayMode test in this project) builds a
            // VehicleController with no NetworkObject at all, and must keep driving exactly as
            // before. Actually verifying the "not the owner" branch requires a real second
            // connected network client, which this environment cannot exercise automatically —
            // see CLAUDE.md's Netcode section for the manual dual-instance test procedure.
            yield return null;

            Assert.IsTrue(vehicle.IsOwnedLocally,
                "Without a NetworkObject component, the vehicle must always be treated as locally owned.");

            vehicle.SetInputOverride(1f, 0f);
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.Greater(vehicle.CurrentSpeed, 0f,
                "The ownership gate must not block input/simulation in the existing non-networked local flow.");
        }

        static bool HasBinding(InputAction action, string path)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.path == path) return true;
            }
            return false;
        }
    }
}
