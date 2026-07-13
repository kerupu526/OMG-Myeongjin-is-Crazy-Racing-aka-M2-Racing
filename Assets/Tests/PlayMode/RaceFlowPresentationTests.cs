using System.Collections;
using System.Reflection;
using M2.Core;
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
        GameObject racerObject;

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            if (racerObject != null) Object.DestroyImmediate(racerObject);
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

        [UnityTest]
        public IEnumerator ResultCard_ContainsTheLongestSupportedLocalRaceSummary()
        {
            canvasObject = new GameObject("RaceFlowCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            GameObject resultPanel = SimpleUIFactory.CreateFullscreenPanel(canvasObject.transform, "ResultPanel", Color.black);
            Text resultText = SimpleUIFactory.CreateCenteredText(resultPanel.transform, "ResultText", 28, Color.white);

            RaceFlowUI flow = canvasObject.AddComponent<RaceFlowUI>();
            flow.resultPanel = resultPanel;
            flow.resultText = resultText;
            yield return null;

            resultPanel.SetActive(true);
            resultText.text = "<size=44><color=#FFD93D>🏆 승리!</color></size>\n" +
                "<size=26>Vehicle_Placeholder</size>\n" +
                "<size=28><color=#B6F36B>아이템전 · 별점 내기</color></size>\n\n" +
                "<color=#FFD93D>최종 순위</color>\n" +
                "1위  Vehicle_Placeholder · 01:59.37 · ★ 6/6\n" +
                "2위  Vehicle_Placeholder 2 · 02:00.00 · ★ 5/6\n\n" +
                "총 시간: 01:59.37\nLap 1: 00:18.09\nLap 2: 00:23.38\nLap 3: 00:17.90\nLap 4: 00:18.00\nLap 5: 00:18.00\n" +
                "<size=28>★ 6/6 (비법 3★ + 시간 3★, 놓친 비법 0회)</size>";
            Canvas.ForceUpdateCanvases();

            RectTransform card = resultPanel.transform.Find("ResultCard").GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(760f, 620f), card.sizeDelta);
            Assert.AreEqual(32, resultText.fontSize);
            Assert.AreEqual(0.86f, resultText.lineSpacing);
            Assert.LessOrEqual(resultText.preferredHeight, resultText.rectTransform.rect.height,
                "A five-lap two-player result must remain inside the result card.");
        }

        [UnityTest]
        public IEnumerator ResultHeader_UsesSavedLocalProfileInsteadOfGeneratedVehicleName()
        {
            canvasObject = new GameObject("RaceFlowCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            GameObject resultPanel = SimpleUIFactory.CreateFullscreenPanel(canvasObject.transform, "ResultPanel", Color.black);
            Text resultText = SimpleUIFactory.CreateCenteredText(resultPanel.transform, "ResultText", 28, Color.white);
            racerObject = new GameObject("Vehicle_Placeholder");
            LapTracker localRacer = racerObject.AddComponent<LapTracker>();

            RaceFlowUI flow = canvasObject.AddComponent<RaceFlowUI>();
            flow.resultPanel = resultPanel;
            flow.resultText = resultText;
            flow.localRacer = localRacer;
            yield return null;

            MethodInfo handleRaceWon = typeof(RaceFlowUI).GetMethod("HandleRaceWon",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handleRaceWon);
            handleRaceWon.Invoke(flow, new object[] { localRacer });

            StringAssert.Contains(M2PlayerProfile.DisplayName, resultText.text);
            StringAssert.DoesNotContain("Vehicle_Placeholder", resultText.text);
        }
    }
}
