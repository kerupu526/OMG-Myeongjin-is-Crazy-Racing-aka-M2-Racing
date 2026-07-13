using M2.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace M2.Tests.PlayMode
{
    public class UiTypographyTests
    {
        [Test]
        public void HtmlDesignFonts_AreBundledAsRuntimeResources()
        {
            Assert.IsNotNull(Resources.Load<Font>("Fonts/BlackHanSans-Regular"));
            Assert.IsNotNull(Resources.Load<Font>("Fonts/Jua-Regular"));
            Assert.IsNotNull(Resources.Load<Font>("Fonts/Fredoka"));
        }

        [Test]
        public void TypographyRoles_AssignTheExpectedFonts()
        {
            GameObject textObject = new GameObject("TypographyText", typeof(RectTransform), typeof(Text));
            try
            {
                Text text = textObject.GetComponent<Text>();

                UiTypography.Apply(text, UiFontRole.Body);
                Assert.AreEqual(UiTypography.Body, text.font);
                UiTypography.Apply(text, UiFontRole.Display);
                Assert.AreEqual(UiTypography.Display, text.font);
                UiTypography.Apply(text, UiFontRole.Metric);
                Assert.AreEqual(UiTypography.Metric, text.font);
            }
            finally
            {
                Object.DestroyImmediate(textObject);
            }
        }
    }
}
