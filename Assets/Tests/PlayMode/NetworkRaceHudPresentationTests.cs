using System.Collections;
using M2.Network;
using M2.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace M2.Tests.PlayMode
{
    /// <summary>
    /// Guards the runtime-built online HUD against reverting to the old generated-scene
    /// placeholder labels or losing the shared readable gameplay layout.
    /// </summary>
    public class NetworkRaceHudPresentationTests
    {
        GameObject canvasObject;
        GameObject legacyBannerObject;
        GameObject legacyInfoObject;

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
        }

        [UnityTest]
        public IEnumerator NetworkHud_Builds_Scaled_Cards_And_Hides_Legacy_Labels()
        {
            canvasObject = new GameObject("NetworkRaceHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            legacyBannerObject = new GameObject("LegacyRaceBanner", typeof(RectTransform), typeof(Text));
            legacyInfoObject = new GameObject("LegacyRaceInfo", typeof(RectTransform), typeof(Text));
            legacyBannerObject.transform.SetParent(canvasObject.transform, false);
            legacyInfoObject.transform.SetParent(canvasObject.transform, false);

            NetworkRaceHUD hud = canvasObject.AddComponent<NetworkRaceHUD>();
            hud.bannerLabel = legacyBannerObject.GetComponent<Text>();
            hud.infoLabel = legacyInfoObject.GetComponent<Text>();
            yield return null;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(RaceHUD.GameplayReferenceResolution, scaler.referenceResolution);
            Assert.AreEqual(0.9f, RaceHUD.GameplayHudScale);
            Assert.IsFalse(legacyBannerObject.activeSelf, "The old center banner must not overlap the formal result/countdown cards.");
            Assert.IsFalse(legacyInfoObject.activeSelf, "The old info label must not overlap the formal HUD cards.");

            Transform root = canvasObject.transform.Find("NetworkRaceHudPresentation");
            Assert.IsNotNull(root);
            AssertReadableCard(root, "LapCard", RaceHUD.ScaleGameplayHud(new Vector2(166f, 112f)));
            AssertReadableCard(root, "TimerCard", RaceHUD.ScaleGameplayHud(new Vector2(198f, 112f)));
            AssertReadableCard(root, "VersusCard", RaceHUD.ScaleGameplayHud(new Vector2(372f, 112f)));
            AssertReadableCard(root, "PrimaryItemCard", RaceHUD.ScaleGameplayHud(new Vector2(170f, 170f)));
            AssertReadableCard(root, "SecondaryItemCard", RaceHUD.ScaleGameplayHud(new Vector2(170f, 170f)));
            AssertReadableCard(root, "ItemDetailCard", RaceHUD.ScaleGameplayHud(new Vector2(590f, 130f)));
            AssertReadableCard(root.Find("ResultOverlay"), "ResultCard", RaceHUD.ScaleGameplayHud(new Vector2(670f, 420f)));

            Assert.IsNotNull(root.Find("PrimaryItemCard/Icon").GetComponent<Image>());
            Assert.IsNotNull(root.Find("SecondaryItemCard/Icon").GetComponent<Image>());
            Assert.IsNotNull(root.Find("CountdownCard/Label").GetComponent<Text>());
            Assert.IsFalse(root.Find("ResultOverlay").gameObject.activeSelf);
        }

        static void AssertReadableCard(Transform parent, string name, Vector2 expectedSize)
        {
            Transform card = parent.Find(name);
            Assert.IsNotNull(card, $"{name} should be part of the formal online HUD.");
            Assert.AreEqual(expectedSize, card.GetComponent<RectTransform>().sizeDelta,
                $"{name} should use the 90% gameplay HUD scale.");
        }
    }
}
