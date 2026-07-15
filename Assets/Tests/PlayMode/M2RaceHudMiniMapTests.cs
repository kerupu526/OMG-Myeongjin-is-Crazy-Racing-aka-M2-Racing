using M2.UI;
using NUnit.Framework;
using UnityEngine;

namespace M2.Tests.PlayMode
{
    public class M2RaceHudMiniMapTests
    {
        [Test]
        public void ProjectMiniMapPosition_MapsTrackExtentsInsideTheMarkerInset()
        {
            Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(200f, 0f, 100f));
            Vector2 trackSize = new Vector2(104f, 52f);
            Vector2 markerSize = new Vector2(11f, 11f);

            Vector2 northWest = M2RaceHudToolkit.ProjectMiniMapPosition(
                new Vector3(-100f, 0f, 50f), worldBounds, trackSize, markerSize);
            Vector2 southEast = M2RaceHudToolkit.ProjectMiniMapPosition(
                new Vector3(100f, 0f, -50f), worldBounds, trackSize, markerSize);

            Assert.That(northWest.x, Is.EqualTo(4f).Within(0.01f));
            Assert.That(northWest.y, Is.EqualTo(4f).Within(0.01f));
            Assert.That(southEast.x, Is.EqualTo(89f).Within(0.01f));
            Assert.That(southEast.y, Is.EqualTo(37f).Within(0.01f));
        }
    }
}
