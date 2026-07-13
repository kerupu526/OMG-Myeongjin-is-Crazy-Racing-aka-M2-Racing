using NUnit.Framework;
using UnityEngine;
using M2.Stage;

namespace M2.Tests.PlayMode
{
    public class StageArtPrefabTests
    {
        [Test]
        public void Stage_Art_Library_References_Renderable_Authored_Prefabs()
        {
            StageArtPrefabLibrary library = Resources.Load<StageArtPrefabLibrary>("StageArtPrefabLibrary");
            Assert.IsNotNull(library, "Stage art prefab library should be built into Resources.");

            foreach (StageArtPrefabId id in System.Enum.GetValues(typeof(StageArtPrefabId)))
            {
                GameObject prefab = library.Get(id);
                Assert.IsNotNull(prefab, $"{id} should reference an authored prefab.");
                Assert.IsNotNull(prefab.GetComponentInChildren<Renderer>(true),
                    $"{id} should render a bundled model rather than an empty proxy.");
            }
        }
    }
}
