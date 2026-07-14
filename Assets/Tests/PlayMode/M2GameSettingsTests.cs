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

        [Test]
        public void Graphics_And_Language_Use_Bounded_Persistent_Choices()
        {
            Assert.AreEqual(M2GraphicsQuality.Low,
                M2GameSettings.NormalizeGraphicsQuality((M2GraphicsQuality)(-1)));
            Assert.AreEqual(M2GraphicsQuality.High,
                M2GameSettings.NormalizeGraphicsQuality((M2GraphicsQuality)5));
            Assert.AreEqual(M2Language.Korean, M2GameSettings.NormalizeLanguage((M2Language)(-1)));
            Assert.AreEqual(M2Language.English, M2GameSettings.NormalizeLanguage((M2Language)5));
            Assert.AreEqual(0, M2GameSettings.ResolveUnityQualityLevel(M2GraphicsQuality.Low, 3));
            Assert.AreEqual(1, M2GameSettings.ResolveUnityQualityLevel(M2GraphicsQuality.Medium, 3));
            Assert.AreEqual(2, M2GameSettings.ResolveUnityQualityLevel(M2GraphicsQuality.High, 3));
            Assert.AreEqual(-1, M2GameSettings.ResolveUnityQualityLevel(M2GraphicsQuality.High, 0));
        }
    }
}
