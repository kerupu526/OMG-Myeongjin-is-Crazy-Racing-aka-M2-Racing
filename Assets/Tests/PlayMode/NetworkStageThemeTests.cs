using M2.Stage;
using NUnit.Framework;
using UnityEngine;

namespace M2.Tests.PlayMode
{
    public class NetworkStageThemeTests
    {
        GameObject stageObject;

        [TearDown]
        public void TearDown()
        {
            if (stageObject != null) Object.DestroyImmediate(stageObject);
        }

        [Test]
        public void Theme_Builds_A_NonColliding_Visual_Group_For_Selected_Stage()
        {
            stageObject = new GameObject("NetworkStageThemeTest");
            NetworkStageTheme theme = stageObject.AddComponent<NetworkStageTheme>();

            theme.Apply(StageType.NetherFortress);

            Transform root = stageObject.transform.Find("NetworkStageTheme_NetherFortress");
            Assert.IsNotNull(root);
            Assert.AreEqual(StageType.NetherFortress, theme.CurrentStage);
            Assert.IsNotNull(root.Find("NetherStageSign"));
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                Assert.IsFalse(collider.enabled, "Lobby stage-theme props must never alter race collision.");
            }
        }
    }
}
