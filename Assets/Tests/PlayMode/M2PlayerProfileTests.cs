using M2.UI;
using NUnit.Framework;
using UnityEngine;

namespace M2.Tests.PlayMode
{
    public class M2PlayerProfileTests
    {
        [Test]
        public void Avatar_Palette_Normalizes_Indices_Without_Writing_PlayerPrefs()
        {
            Assert.AreEqual(M2PlayerProfile.ResolveAvatarColor(0), M2PlayerProfile.ResolveAvatarColor(6));
            Assert.AreEqual(M2PlayerProfile.ResolveAvatarColor(5), M2PlayerProfile.ResolveAvatarColor(-1));
            Assert.AreNotEqual(M2PlayerProfile.ResolveAvatarColor(0), M2PlayerProfile.ResolveAvatarColor(1));
        }

        [Test]
        public void Avatar_Appearance_Normalizes_All_Synchronized_Cosmetic_Choices()
        {
            M2AvatarAppearance appearance = new M2AvatarAppearance(
                7, (M2AvatarEyes)4, (M2AvatarMouth)(-1), true, false, (M2AvatarHat)5, 4);

            Assert.AreEqual(1, appearance.BodyColorIndex);
            Assert.AreEqual(M2AvatarEyes.Happy, appearance.Eyes);
            Assert.AreEqual(M2AvatarMouth.Flat, appearance.Mouth);
            Assert.IsTrue(appearance.HasCheeks);
            Assert.IsFalse(appearance.HasEars);
            Assert.AreEqual(M2AvatarHat.Crown, appearance.Hat);
            Assert.AreEqual("#077", M2PlayerProfile.ResolvePlateLabel(appearance.PlateIndex));
        }

        [Test]
        public void Display_Name_Uses_A_Readable_Default_And_A_Bounded_Label()
        {
            Assert.AreEqual(M2PlayerProfile.DefaultDisplayName, M2PlayerProfile.NormalizeDisplayName("   "));
            Assert.AreEqual("레이서 #001", M2PlayerProfile.NormalizeDisplayName("레이서 #001"));
            Assert.AreEqual(12, M2PlayerProfile.NormalizeDisplayName("123456789012345").Length);
        }
    }
}
