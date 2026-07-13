using System.Collections;
using M2.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace M2.Tests.PlayMode
{
    public class NetworkMenuPresentationTests
    {
        GameObject canvasObject;

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
        }

        [UnityTest]
        public IEnumerator Network_Menu_Uses_The_Reference_Layout_And_Navigates_Local_Screens()
        {
            canvasObject = new GameObject("NetworkMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            NetworkMenuUI menu = canvasObject.AddComponent<NetworkMenuUI>();
            yield return null;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1280f, 720f), scaler.referenceResolution);
            Assert.AreEqual("Main", menu.CurrentScreenName);
            Assert.IsTrue(canvasObject.transform.Find("NetworkMenuRoot/Screen_Main").gameObject.activeSelf);
            Assert.IsNotNull(canvasObject.transform.Find("NetworkMenuRoot/Screen_Main/CreateRoomButton"));
            Assert.IsNotNull(canvasObject.transform.Find("NetworkMenuRoot/Screen_Main/JoinRoomButton"));

            menu.ShowHostSetup();
            Assert.AreEqual("HostSetup", menu.CurrentScreenName);
            Assert.IsTrue(canvasObject.transform.Find("NetworkMenuRoot/Screen_HostSetup").gameObject.activeSelf);

            menu.ShowJoinSetup();
            Assert.AreEqual("JoinSetup", menu.CurrentScreenName);
            Assert.IsTrue(canvasObject.transform.Find("NetworkMenuRoot/Screen_Join").gameObject.activeSelf);

            menu.ShowAvatar();
            Assert.AreEqual("Avatar", menu.CurrentScreenName);
            Assert.IsTrue(canvasObject.transform.Find("NetworkMenuRoot/Screen_Avatar").gameObject.activeSelf);
            Assert.IsNotNull(canvasObject.transform.Find("NetworkMenuRoot/Screen_Avatar/AvatarEditorCard/AvatarNameInput"));
        }

        [UnityTest]
        public IEnumerator Host_Setup_Presents_Existing_Room_Settings_In_The_Design_Card()
        {
            canvasObject = new GameObject("NetworkMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            RoomSettingsUI settings = canvasObject.AddComponent<RoomSettingsUI>();
            NetworkMenuUI menu = canvasObject.AddComponent<NetworkMenuUI>();
            menu.Initialize(null, settings);
            yield return null;

            menu.ShowHostSetup();
            yield return null;

            Transform panel = canvasObject.transform.Find(
                "NetworkMenuRoot/Screen_HostSetup/HostSettingsSlot/RoomSettingsPanel");
            Assert.IsNotNull(panel);
            Assert.AreEqual(new Vector2(480f, 390f), panel.GetComponent<RectTransform>().sizeDelta);
            Assert.AreEqual(Color.white, panel.GetComponent<Image>().color);
            Assert.AreEqual(28, panel.Find("Title").GetComponent<Text>().fontSize);
            Assert.AreEqual(26, panel.Find("ModeButton/Label").GetComponent<Text>().fontSize);
        }
    }
}
