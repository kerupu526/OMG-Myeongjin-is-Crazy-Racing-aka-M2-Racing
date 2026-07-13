using M2.UI;
using NUnit.Framework;

namespace M2.Tests.PlayMode
{
    public class M2GameSettingsTests
    {
        [Test]
        public void Master_Volume_Is_Clamped_Without_Writing_PlayerPrefs()
        {
            Assert.AreEqual(0f, M2GameSettings.NormalizeVolume(-0.25f));
            Assert.AreEqual(0.45f, M2GameSettings.NormalizeVolume(0.45f));
            Assert.AreEqual(1f, M2GameSettings.NormalizeVolume(2f));
        }
    }
}
