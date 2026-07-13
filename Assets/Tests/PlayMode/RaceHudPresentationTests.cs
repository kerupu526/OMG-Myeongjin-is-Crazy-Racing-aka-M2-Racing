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
    public class RaceHudPresentationTests
    {
        GameObject canvasObject;
        GameObject vehicleObject;
        GameObject gameManagerObject;

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            if (vehicleObject != null) Object.DestroyImmediate(vehicleObject);
            if (gameManagerObject != null) Object.DestroyImmediate(gameManagerObject);
        }

        [UnityTest]
        public IEnumerator RaceHud_Builds_Compact_Item_And_Gauge_Presentation()
        {
            canvasObject = new GameObject("RaceHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            GameObject legacyLabel = new GameObject("RaceLabel", typeof(RectTransform), typeof(Text));
            legacyLabel.transform.SetParent(canvasObject.transform, false);

            RaceHUD hud = canvasObject.AddComponent<RaceHUD>();
            hud.label = legacyLabel.GetComponent<Text>();
            yield return null;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
            Assert.IsNotNull(canvasObject.GetComponent<StageGaugeHUD>());

            AssertCompactCard("RaceHud_LapCard", new Vector2(82f, 64f));
            AssertCompactCard("RaceHud_VersusCard", new Vector2(116f, 64f));
            AssertCompactCard("RaceHud_TimeCard", new Vector2(94f, 64f));
            AssertCompactCard("RaceHud_PrimarySlot", new Vector2(86f, 86f));
            AssertCompactCard("RaceHud_SecondarySlot", new Vector2(86f, 86f));
            AssertCompactCard("RaceHud_ItemDetailCard", new Vector2(300f, 84f));
        }

        [UnityTest]
        public IEnumerator RaceHud_Shows_Collected_Item_Sprite_And_Detail()
        {
            canvasObject = new GameObject("RaceHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            GameObject legacyLabel = new GameObject("RaceLabel", typeof(RectTransform), typeof(Text));
            legacyLabel.transform.SetParent(canvasObject.transform, false);

            vehicleObject = new GameObject("RaceHudVehicle");
            vehicleObject.AddComponent<Rigidbody>().useGravity = false;
            vehicleObject.AddComponent<VehicleController>();
            ItemSlots slots = vehicleObject.AddComponent<ItemSlots>();

            gameManagerObject = new GameObject("RaceHudGameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            gameManager.briefingDuration = 0f;
            gameManager.countdownSeconds = 0;

            RaceHUD hud = canvasObject.AddComponent<RaceHUD>();
            hud.label = legacyLabel.GetComponent<Text>();
            hud.itemSlots = slots;
            hud.gameManager = gameManager;

            for (int i = 0; i < 8 && gameManager.CurrentState != RaceState.Racing; i++) yield return null;
            Assert.AreEqual(RaceState.Racing, gameManager.CurrentState);

            ItemDefinition gasoline = ItemCatalog.CreateFromId(NetItemId.Gasoline);
            slots.CollectItem(gasoline);
            yield return null;

            Image icon = canvasObject.transform.Find("RaceHud_PrimarySlot/Icon").GetComponent<Image>();
            Text itemName = canvasObject.transform.Find("RaceHud_PrimarySlot/Name").GetComponent<Text>();
            Text detail = canvasObject.transform.Find("RaceHud_ItemDetailCard/ItemDetail").GetComponent<Text>();
            Assert.IsTrue(icon.enabled, "A collected item should enable the slot sprite.");
            Assert.IsNotNull(icon.sprite, "The slot should use ItemSpriteLibrary through ItemArt.");
            StringAssert.Contains(gasoline.itemName, itemName.text);
            StringAssert.Contains(gasoline.description, detail.text);
        }

        void AssertCompactCard(string name, Vector2 expectedSize)
        {
            Transform card = canvasObject.transform.Find(name);
            Assert.IsNotNull(card, $"{name} should be part of the formal race HUD.");
            Assert.AreEqual(expectedSize, card.GetComponent<RectTransform>().sizeDelta, $"{name} should retain the compact game-view layout.");
        }
    }
}
