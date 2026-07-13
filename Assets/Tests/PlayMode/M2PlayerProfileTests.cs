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
            Assert.AreEqual(M2PlayerProfile.ResolveAvatarColor(0), M2PlayerProfile.ResolveAvatarColor(3));
            Assert.AreEqual(M2PlayerProfile.ResolveAvatarColor(2), M2PlayerProfile.ResolveAvatarColor(-1));
            Assert.AreNotEqual(M2PlayerProfile.ResolveAvatarColor(0), M2PlayerProfile.ResolveAvatarColor(1));
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
