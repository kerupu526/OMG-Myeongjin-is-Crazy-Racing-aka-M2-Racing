using System.Collections;
using M2.Core;
using M2.Items;
using M2.Player;
using M2.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace M2.Tests.PlayMode
{
    public class SpeedModeTests
    {
        GameObject vehicleObject;
        GameObject gameManagerObject;
        GameObject canvasObject;

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            if (vehicleObject != null) Object.DestroyImmediate(vehicleObject);
            if (gameManagerObject != null) Object.DestroyImmediate(gameManagerObject);
        }

        GameManager CreateGameManager()
        {
            gameManagerObject = new GameObject("SpeedModeGameManager");
            return gameManagerObject.AddComponent<GameManager>();
        }

        VehicleController CreateVehicleWithSlots(out ItemSlots slots)
        {
            vehicleObject = new GameObject("SpeedModeVehicle");
            vehicleObject.AddComponent<Rigidbody>().useGravity = false;
            VehicleController vehicle = vehicleObject.AddComponent<VehicleController>();
            slots = vehicleObject.AddComponent<ItemSlots>();
            return vehicle;
        }

        [Test]
        public void Speed_Mode_Fixes_Five_Laps_And_Caps_Vehicle_At_OneHundredKph()
        {
            VehicleController vehicle = CreateVehicleWithSlots(out _);
            GameManager gameManager = CreateGameManager();
            gameManager.vehicles.Add(vehicle);

            gameManager.ConfigureRoomSettings(RaceMode.Speed, 1, VictoryCondition.StarBet);

            Assert.IsTrue(gameManager.IsSpeedMode);
            Assert.AreEqual(RaceModeRules.SpeedModeLapCount, gameManager.targetLapCount);
            Assert.AreEqual(VictoryCondition.SimpleFinish, gameManager.victoryCondition);
            Assert.AreEqual(RaceModeRules.SpeedModeMaximumKph / 3.6f, vehicle.AbsoluteSpeedLimit, 0.001f);
        }

        [UnityTest]
        public IEnumerator Speed_Mode_Automatically_Grants_Basic_Gasoline_While_Racing()
        {
            VehicleController vehicle = CreateVehicleWithSlots(out ItemSlots slots);
            GameManager gameManager = CreateGameManager();
            gameManager.vehicles.Add(vehicle);
            gameManager.briefingDuration = 0f;
            gameManager.countdownSeconds = 0;
            gameManager.speedModeGasolineInterval = 0.05f;
            gameManager.ConfigureRoomSettings(RaceMode.Speed, 3, VictoryCondition.SimpleFinish);

            for (int i = 0; i < 10 && gameManager.CurrentState != RaceState.Racing; i++) yield return null;
            Assert.AreEqual(RaceState.Racing, gameManager.CurrentState);

            yield return new WaitForSeconds(0.12f);
            Assert.IsNotNull(slots.PrimarySlot);
            Assert.AreEqual(NetItemId.Gasoline, slots.PrimarySlot.id);
        }

        [UnityTest]
        public IEnumerator Room_Settings_UI_Cycles_Item_Rules_And_Enforces_Speed_Rules()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.autoStartOnStart = false;
            canvasObject = new GameObject("RoomSettingsCanvas", typeof(RectTransform), typeof(Canvas));
            RoomSettingsUI settings = canvasObject.AddComponent<RoomSettingsUI>();
            settings.gameManager = gameManager;
            yield return null;

            Assert.IsNotNull(canvasObject.transform.Find("RoomSettingsPanel/ModeButton"));
            Assert.IsNotNull(canvasObject.transform.Find("RoomSettingsPanel/LapButton"));
            Assert.IsNotNull(canvasObject.transform.Find("RoomSettingsPanel/VictoryButton"));

            settings.ToggleMode();
            Assert.AreEqual(RaceMode.Speed, gameManager.raceMode);
            Assert.AreEqual(5, gameManager.targetLapCount);

            settings.ToggleMode();
            settings.CycleItemLapCount();
            settings.CycleItemVictoryCondition();
            Assert.AreEqual(RaceMode.Item, gameManager.raceMode);
            Assert.AreEqual(5, gameManager.targetLapCount);
            Assert.AreEqual(VictoryCondition.StarBet, gameManager.victoryCondition);
        }
    }
}
