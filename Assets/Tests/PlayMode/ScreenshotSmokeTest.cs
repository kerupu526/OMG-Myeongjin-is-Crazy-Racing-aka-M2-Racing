using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using M2.Core;
using M2.Player;

namespace M2.Tests.PlayMode
{
    // Builds a minimal drivable scene and captures screenshots while driving forward.
    // Not assertion-based — this is for visual/manual confirmation that rendering,
    // the vehicle sprite, camera follow, and UI actually show up correctly.
    public class ScreenshotSmokeTest : StableInputTestFixture
    {
        static readonly string ShotDirectory = Path.Combine(Application.dataPath, "..", "TestScreenshots");

        Keyboard keyboard;
        GameObject root;

        public override void Setup()
        {
            base.Setup();
            keyboard = AddTestKeyboard();

            Directory.CreateDirectory(ShotDirectory);

            root = new GameObject("ScreenshotSmokeTestRoot");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(root.transform);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.GetComponent<Renderer>().material.color = new Color(0.25f, 0.4f, 0.22f);

            GameObject vehicle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vehicle.tag = "Player";
            vehicle.transform.SetParent(root.transform);
            vehicle.transform.position = new Vector3(0f, 0.5f, 0f);
            vehicle.transform.localScale = new Vector3(1.2f, 0.6f, 2f);
            Object.Destroy(vehicle.GetComponent<MeshRenderer>());
            Object.Destroy(vehicle.GetComponent<MeshFilter>());
            vehicle.AddComponent<Rigidbody>();
            vehicle.AddComponent<VehicleController>();

            GameObject sprite = new GameObject("Sprite");
            sprite.transform.SetParent(vehicle.transform);
            sprite.transform.localPosition = Vector3.up * 1.8f;
            sprite.transform.localScale = new Vector3(1.4f, 1.8f, 1f);
            SpriteRenderer spriteRenderer = sprite.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite(new Color(1f, 0.15f, 0.05f), Color.black, 128, 64f);
            sprite.AddComponent<BillboardSprite>();

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            VehicleCameraFollow follow = cameraObject.AddComponent<VehicleCameraFollow>();
            follow.target = vehicle.transform;

            GameObject canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(root.transform);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(canvasObject.transform);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.text = "M2 Racing - Screenshot Smoke Test";
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(500f, 40f);
        }

        public override void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Capture_Driving_Sequence()
        {
            yield return null; // let everything Awake/Start once before the first shot

            Capture("00_start");
            yield return null;

            Press(keyboard.upArrowKey);
            yield return new WaitForSeconds(0.5f);
            Capture("01_accelerating");
            yield return null;

            Press(keyboard.rightArrowKey);
            yield return new WaitForSeconds(0.5f);
            Capture("02_turning");
            yield return null;

            // The fixture removes its synthetic keyboard during teardown, so an explicit
            // release is unnecessary here.

            bool anyShotWritten = Directory.Exists(ShotDirectory) && Directory.GetFiles(ShotDirectory, "*.png").Length > 0;
            if (!anyShotWritten)
            {
                // -nographics (headless, no display) skips real rendering, so
                // ScreenCapture.CaptureScreenshot silently produces no file. That's expected
                // in this sandbox — flag it as inconclusive rather than a false "Passed" so a
                // run on a machine with a real display is distinguishable from one without.
                Assert.Inconclusive("No screenshot files were written — likely running headless (-nographics) with no display attached. Re-run without -nographics on a machine with a display to get actual images.");
            }

            Assert.Pass("Screenshots captured to " + ShotDirectory);
        }

        static void Capture(string label)
        {
            string path = Path.Combine(ShotDirectory, $"{label}.png");
            ScreenCapture.CaptureScreenshot(path);
        }
    }
}
