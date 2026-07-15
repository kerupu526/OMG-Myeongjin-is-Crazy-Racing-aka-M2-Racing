using M2.Network;
using M2.Stage;
using NUnit.Framework;
using UnityEngine;

namespace M2.Tests.PlayMode
{
    public class NetworkStageTrackTests
    {
        GameObject raceRoot;

        [TearDown]
        public void TearDown()
        {
            if (raceRoot != null) Object.DestroyImmediate(raceRoot);
        }

        [Test]
        public void AfricaSelection_Rebuilds_PhysicalCircuit_Grid_Checkpoints_And_ItemMarkers()
        {
            raceRoot = new GameObject("NetworkRaceStageTrackTest");
            RaceStartGrid grid = raceRoot.AddComponent<RaceStartGrid>();
            var points = new NetworkItemSpawnPoint[NetworkStageTrack.ItemSpawnCount];
            for (int i = 0; i < points.Length; i++)
            {
                GameObject point = new GameObject($"ItemSpawnPoint_{i}");
                point.transform.SetParent(raceRoot.transform);
                points[i] = point.AddComponent<NetworkItemSpawnPoint>();
                points[i].index = i;
            }

            NetworkStageTrack track = raceRoot.AddComponent<NetworkStageTrack>();
            track.Apply(StageType.AfricaTv);

            Assert.AreEqual(StageType.AfricaTv, track.CurrentStage);
            Assert.IsNotNull(track.RuntimeRoot.Find("TrackSurface"));
            Assert.AreEqual(NetworkStageTrack.CheckpointCount,
                track.RuntimeRoot.GetComponentsInChildren<M2.Core.Checkpoint>().Length);
            Assert.IsNotNull(track.RuntimeRoot.Find("StageGameplay/BroadcastAccidentZone"));

            Vector3 expectedStart = track.Geometry.PointAt(0f);
            Assert.Less(Vector3.Distance(grid.slot0Position, expectedStart), track.Geometry.TrackWidth,
                "The start grid must move to Africa TV's own start, not keep Bikini City's baked position.");
            for (int i = 0; i < points.Length; i++)
            {
                float theta = (i + 0.5f) * Mathf.PI * 2f / points.Length;
                Assert.Less(Vector3.Distance(points[i].transform.position, track.Geometry.PointAt(theta)), 0.001f);
            }
        }

        [Test]
        public void StageSwap_Replaces_AfricaHazards_With_NetherHazards()
        {
            raceRoot = new GameObject("NetworkRaceStageSwapTest");
            raceRoot.AddComponent<RaceStartGrid>();
            NetworkStageTrack track = raceRoot.AddComponent<NetworkStageTrack>();

            track.Apply(StageType.AfricaTv);
            track.Apply(StageType.NetherFortress);

            Assert.AreEqual(StageType.NetherFortress, track.CurrentStage);
            Assert.IsNotNull(track.RuntimeRoot.Find("StageGameplay/LavaZone"));
            Assert.IsNotNull(track.RuntimeRoot.Find("StageGameplay/OasisZone"));
            Assert.IsNull(track.RuntimeRoot.Find("StageGameplay/BroadcastAccidentZone"));
            Assert.AreEqual(NetworkStageTrack.CheckpointCount,
                track.RuntimeRoot.GetComponentsInChildren<M2.Core.Checkpoint>().Length);
        }
    }
}
