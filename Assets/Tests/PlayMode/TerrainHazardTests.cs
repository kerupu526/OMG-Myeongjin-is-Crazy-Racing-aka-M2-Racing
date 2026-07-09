using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using M2.Stage;

namespace M2.Tests.PlayMode
{
    public class TerrainHazardTests
    {
        GameObject hazardObject;
        GameObject vehicleObject;
        BikiniCityStageState stageState;

        [SetUp]
        public void SetUp()
        {
            hazardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hazardObject.AddComponent<TerrainHazard>();

            vehicleObject = new GameObject("TestVehicle");
            vehicleObject.tag = "Player";
            vehicleObject.transform.position = new Vector3(0f, 0f, -5f);
            Rigidbody rb = vehicleObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            vehicleObject.AddComponent<BoxCollider>();
            stageState = vehicleObject.AddComponent<BikiniCityStageState>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(hazardObject);
            Object.Destroy(vehicleObject);
        }

        [UnityTest]
        public IEnumerator Colliding_With_Hazard_Drops_One_Recipe()
        {
            vehicleObject.GetComponent<Rigidbody>().linearVelocity = new Vector3(0f, 0f, 10f); // drive straight into the hazard at the origin

            float timeout = Time.time + 3f;
            while (stageState.MissedRecipeCount == 0 && Time.time < timeout)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(1, stageState.MissedRecipeCount, "Colliding with a terrain hazard should drop exactly one 비법.");

            // Keep simulating a bit longer — OnCollisionEnter shouldn't refire while still in contact.
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(1, stageState.MissedRecipeCount, "Staying in contact must not drop additional 비법 beyond the initial hit.");
        }
    }
}
