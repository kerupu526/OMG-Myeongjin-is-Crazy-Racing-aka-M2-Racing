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
        public IEnumerator RaceHud_Builds_Readable_Item_And_Gauge_Presentation()
        {
            canvasObject = new GameObject("RaceHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            GameObject legacyLabel = new GameObject("RaceLabel", typeof(RectTransform), typeof(Text));
            legacyLabel.transform.SetParent(canvasObject.transform, false);

            RaceHUD hud = canvasObject.AddComponent<RaceHUD>();
            hud.label = legacyLabel.GetComponent<Text>();
            yield return null;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(RaceHUD.GameplayReferenceResolution, scaler.referenceResolution);
            Assert.AreEqual(UiTypography.Display, legacyLabel.GetComponent<Text>().font);
            Assert.IsNotNull(canvasObject.GetComponent<StageGaugeHUD>());

            Assert.AreEqual(0.9f, RaceHUD.GameplayHudScale);
            AssertReadableCard("RaceHud_LapCard", RaceHUD.ScaleGameplayHud(new Vector2(166f, 112f)));
            AssertReadableCard("RaceHud_VersusCard", RaceHUD.ScaleGameplayHud(new Vector2(300f, 112f)));
            AssertReadableCard("RaceHud_TimeCard", RaceHUD.ScaleGameplayHud(new Vector2(198f, 112f)));
            AssertReadableCard("RaceHud_PrimarySlot", RaceHUD.ScaleGameplayHud(new Vector2(170f, 170f)));
            AssertReadableCard("RaceHud_SecondarySlot", RaceHUD.ScaleGameplayHud(new Vector2(170f, 170f)));
            AssertReadableCard("RaceHud_ItemDetailCard", RaceHUD.ScaleGameplayHud(new Vector2(590f, 138f)));
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

        void AssertReadableCard(string name, Vector2 expectedSize)
        {
            Transform card = canvasObject.transform.Find(name);
            Assert.IsNotNull(card, $"{name} should be part of the formal race HUD.");
            Assert.AreEqual(expectedSize, card.GetComponent<RectTransform>().sizeDelta, $"{name} should retain the readable game-view layout.");
        }
    }
}
