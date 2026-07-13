using System.Collections;
using M2.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace M2.Tests.PlayMode
{
    public class RaceFlowPresentationTests
    {
        GameObject canvasObject;

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
        }

        [UnityTest]
        public IEnumerator RaceStartButton_UsesOneCenteredLabel_AndReadableCanvasScale()
        {
            canvasObject = new GameObject("RaceFlowCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            GameObject briefingPanel = SimpleUIFactory.CreateFullscreenPanel(canvasObject.transform, "BriefingPanel", Color.black);
            Text briefingText = SimpleUIFactory.CreateCenteredText(briefingPanel.transform, "BriefingText", 28, Color.white);
            Button startButton = SimpleUIFactory.CreateButton(briefingPanel.transform, "StartButton", "시작",
                new Vector2(0f, -180f), new Vector2(200f, 60f));

            RaceFlowUI flow = canvasObject.AddComponent<RaceFlowUI>();
            flow.briefingPanel = briefingPanel;
            flow.briefingText = briefingText;
            flow.startButton = startButton;
            yield return null;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            Assert.AreEqual(RaceHUD.GameplayReferenceResolution, scaler.referenceResolution);

            Text[] labels = startButton.GetComponentsInChildren<Text>(true);
            Assert.AreEqual(1, labels.Length, "The start button must not create a duplicate presentation label.");
            Text label = labels[0];
            Assert.AreEqual("레이스 시작", label.text);
            Assert.AreEqual(TextAnchor.MiddleCenter, label.alignment);
            Assert.AreEqual(Vector2.zero, label.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, label.rectTransform.anchorMax);
            Assert.AreEqual(Vector2.zero, label.rectTransform.offsetMin);
            Assert.AreEqual(Vector2.zero, label.rectTransform.offsetMax);
            Assert.AreEqual(UiTypography.Body, label.font);
            Assert.AreEqual(new Vector2(320f, 76f), startButton.GetComponent<RectTransform>().sizeDelta);
        }
    }
}
