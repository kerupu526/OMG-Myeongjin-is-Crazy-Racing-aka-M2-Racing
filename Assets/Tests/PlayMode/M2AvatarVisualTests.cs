using M2.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace M2.Tests.PlayMode
{
    public class M2AvatarVisualTests
    {
        [Test]
        public void SharedToolkitAvatar_Builds_And_Reapplies_All_Profile_Parts()
        {
            VisualElement root = new VisualElement();
            M2AvatarAppearance crownProfile = new M2AvatarAppearance(4, M2AvatarEyes.Cool,
                M2AvatarMouth.Flat, false, false, M2AvatarHat.Crown, 2);

            M2AvatarVisual.Apply(root, crownProfile);

            Assert.IsNotNull(root.Q<VisualElement>("avatar-body"));
            Assert.IsNotNull(root.Q<VisualElement>("avatar-eyes"));
            Assert.IsNotNull(root.Q<VisualElement>("avatar-mouth"));
            Assert.IsNotNull(root.Q<VisualElement>("avatar-hat-cap-top"));
            Assert.IsNotNull(root.Q<Image>("avatar-hat-crown"));
            Assert.AreEqual(DisplayStyle.None, root.Q<VisualElement>("avatar-ear-left").style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q<VisualElement>("avatar-cheeks").style.display.value);
            Assert.IsTrue(root.Q<VisualElement>("avatar-eyes").ClassListContains("avatar-eyes--cool"));
            Assert.IsTrue(root.Q<VisualElement>("avatar-mouth").ClassListContains("avatar-mouth--flat"));
            Assert.AreEqual("#999", root.Q<Label>("avatar-plate").text);

            M2AvatarAppearance capProfile = new M2AvatarAppearance(1, M2AvatarEyes.Happy,
                M2AvatarMouth.Open, true, true, M2AvatarHat.Cap, 0);
            M2AvatarVisual.Apply(root, capProfile);

            Assert.AreEqual(DisplayStyle.Flex, root.Q<VisualElement>("avatar-ear-left").style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, root.Q<VisualElement>("avatar-cheeks").style.display.value);
            Assert.IsTrue(root.Q<VisualElement>("avatar-eyes").ClassListContains("avatar-eyes--happy"));
            Assert.IsTrue(root.Q<VisualElement>("avatar-mouth").ClassListContains("avatar-mouth--open"));
            Assert.AreEqual(DisplayStyle.Flex, root.Q<VisualElement>("avatar-hat-cap-top").style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q<Image>("avatar-hat-crown").style.display.value);
            Assert.AreEqual("#001", root.Q<Label>("avatar-plate").text);
        }

        [Test]
        public void WaitingOpponentPlaceholder_HidesFace_And_ProfileApplyRestoresIt()
        {
            VisualElement root = new VisualElement();
            M2AvatarVisual.Apply(root, M2PlayerProfile.Appearance);

            M2AvatarVisual.ApplyWaitingOpponent(root);

            Image question = root.Q<Image>("avatar-question-icon");
            Assert.IsNotNull(question);
            Assert.AreEqual(DisplayStyle.Flex, question.style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q<VisualElement>("avatar-ear-left").style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q<VisualElement>("avatar-eyes").style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q<VisualElement>("avatar-mouth").style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q<Label>("avatar-plate").style.display.value);

            M2AvatarVisual.Apply(root, M2PlayerProfile.Appearance);

            Assert.AreEqual(DisplayStyle.None, question.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, root.Q<VisualElement>("avatar-eyes").style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, root.Q<VisualElement>("avatar-mouth").style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, root.Q<Label>("avatar-plate").style.display.value);
        }
    }
}
