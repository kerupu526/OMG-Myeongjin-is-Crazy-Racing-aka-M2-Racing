using M2.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace M2.Tests.PlayMode
{
    public class M2AvatarPortraitTests
    {
        GameObject portraitObject;

        [TearDown]
        public void TearDown()
        {
            if (portraitObject != null) Object.DestroyImmediate(portraitObject);
        }

        [Test]
        public void Portrait_Renders_The_Saved_Cosmetic_Choices()
        {
            portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            M2AvatarPortrait portrait = portraitObject.AddComponent<M2AvatarPortrait>();
            M2AvatarAppearance appearance = new M2AvatarAppearance(4, M2AvatarEyes.Cool,
                M2AvatarMouth.Flat, true, true, M2AvatarHat.Crown, 2);

            portrait.Apply(appearance, "레이서");

            Assert.AreEqual(M2PlayerProfile.ResolveAvatarColor(4), portraitObject.GetComponent<Image>().color);
            Assert.AreEqual("▰   ▰", portraitObject.transform.Find("FaceEyes").GetComponent<Text>().text);
            Assert.AreEqual("—", portraitObject.transform.Find("FaceMouth").GetComponent<Text>().text);
            Assert.AreEqual("♛", portraitObject.transform.Find("FaceHat").GetComponent<Text>().text);
            Assert.AreEqual("#999", portraitObject.transform.Find("FacePlate").GetComponent<Text>().text);
            Assert.IsTrue(portraitObject.transform.Find("FaceCheekLeft").GetComponent<Text>().enabled);
            Assert.IsTrue(portraitObject.transform.Find("FaceEarRight").GetComponent<Text>().enabled);
        }
    }
}
