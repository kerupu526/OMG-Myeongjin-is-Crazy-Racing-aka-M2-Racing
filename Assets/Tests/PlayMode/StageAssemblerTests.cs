using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using M2.Player;
using M2.Stage;
using M2.UI;

namespace M2.Tests.PlayMode
{
    public class StageAssemblerTests
    {
        GameObject vehicleObject;
        GameObject canvasObject;
        Canvas canvas;
        GameObject worldParentObject;
        RaceFlowUI flowUI;
        TrackGeometry geometry;

        [SetUp]
        public void SetUp()
        {
            vehicleObject = new GameObject("TestVehicle");
            vehicleObject.AddComponent<Rigidbody>();
            vehicleObject.AddComponent<VehicleController>();

            canvasObject = new GameObject("TestCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            flowUI = canvasObject.AddComponent<RaceFlowUI>();

            worldParentObject = new GameObject("TestWorldParent");

            geometry = new TrackGeometry { CenterRadiusX = 10f, CenterRadiusZ = 8f, TrackWidth = 4f, WallHeight = 1f };
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(vehicleObject);
            Object.Destroy(canvasObject);
            Object.Destroy(worldParentObject);
        }

        [UnityTest]
        public IEnumerator Attach_BikiniCity_Adds_Gauge_And_Wires_RaceFlowUI()
        {
            BuiltStage built = StageAssembler.Attach(StageType.BikiniCity, worldParentObject.transform,
                worldParentObject.transform, vehicleObject, canvas, flowUI, geometry);
            yield return null;

            Assert.IsNotNull(vehicleObject.GetComponent<BikiniCityOxygenGauge>());
            Assert.IsNotNull(vehicleObject.GetComponent<BikiniCityStageState>());
            Assert.AreSame(vehicleObject.GetComponent<BikiniCityStageState>(), flowUI.bikiniCityStageState);
            Assert.AreEqual(StageType.BikiniCity, built.Type);
        }

        [UnityTest]
        public IEnumerator Switching_From_BikiniCity_To_AfricaTv_Removes_Old_Components()
        {
            BuiltStage bikini = StageAssembler.Attach(StageType.BikiniCity, worldParentObject.transform,
                worldParentObject.transform, vehicleObject, canvas, flowUI, geometry);
            yield return null;

            StageAssembler.Detach(bikini, vehicleObject, flowUI);
            StageAssembler.Attach(StageType.AfricaTv, worldParentObject.transform,
                worldParentObject.transform, vehicleObject, canvas, flowUI, geometry);
            yield return null;

            Assert.IsNull(vehicleObject.GetComponent<BikiniCityOxygenGauge>(), "Old stage gauge should be removed after switching.");
            Assert.IsNull(flowUI.bikiniCityStageState, "RaceFlowUI's old stage-state reference should be cleared.");
            Assert.IsNotNull(vehicleObject.GetComponent<AfricaTvMentalGauge>());
            Assert.AreSame(vehicleObject.GetComponent<AfricaTvStageState>(), flowUI.africaTvStageState);
        }

        [UnityTest]
        public IEnumerator Attach_NetherFortress_Wires_LavaZone_Reference()
        {
            StageAssembler.Attach(StageType.NetherFortress, worldParentObject.transform,
                worldParentObject.transform, vehicleObject, canvas, flowUI, geometry);
            yield return null;

            NetherFortressStageState stageState = vehicleObject.GetComponent<NetherFortressStageState>();
            Assert.IsNotNull(stageState);
            Assert.IsNotNull(stageState.lavaZone, "NetherFortressStageState should have its LavaZone reference wired by StageAssembler.");
            Assert.AreSame(stageState, flowUI.netherFortressStageState);
        }
    }
}
