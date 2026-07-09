using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using M2.Core;

namespace M2.Tests.PlayMode
{
    public class LapTrackerTests
    {
        GameObject trackerObject;
        LapTracker tracker;

        [SetUp]
        public void SetUp()
        {
            // LapTracker.Start() scans the scene for existing Checkpoint components to find
            // the highest index, so the checkpoints must exist before that Start() runs.
            CreateCheckpoint(0);
            CreateCheckpoint(1);
            CreateCheckpoint(2);

            trackerObject = new GameObject("TestLapTracker");
            tracker = trackerObject.AddComponent<LapTracker>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
            {
                Object.Destroy(cp.gameObject);
            }
            Object.Destroy(trackerObject);
        }

        static void CreateCheckpoint(int index)
        {
            var go = new GameObject($"Checkpoint_{index}");
            go.AddComponent<BoxCollider>().isTrigger = true;
            var cp = go.AddComponent<Checkpoint>();
            cp.index = index;
        }

        [UnityTest]
        public IEnumerator Completing_All_Checkpoints_Raises_OnLapCompleted()
        {
            yield return null; // let Start() run and scan the checkpoints above

            int lapCompletedCount = -1;
            tracker.OnLapCompleted += lap => lapCompletedCount = lap;

            tracker.NotifyCheckpointPassed(1);
            tracker.NotifyCheckpointPassed(2);
            tracker.NotifyCheckpointPassed(0);

            Assert.AreEqual(1, lapCompletedCount, "Passing all checkpoints in order and returning to 0 should complete lap 1.");
            Assert.AreEqual(1, tracker.LapCount);
        }

        [UnityTest]
        public IEnumerator Out_Of_Order_Checkpoint_Is_Ignored()
        {
            yield return null;

            int lapCompletedCount = 0;
            tracker.OnLapCompleted += _ => lapCompletedCount++;

            tracker.NotifyCheckpointPassed(2); // skips checkpoint 1 — should be ignored
            tracker.NotifyCheckpointPassed(0);

            Assert.AreEqual(0, lapCompletedCount, "Skipping ahead to a checkpoint out of order must not count.");
        }
    }
}
