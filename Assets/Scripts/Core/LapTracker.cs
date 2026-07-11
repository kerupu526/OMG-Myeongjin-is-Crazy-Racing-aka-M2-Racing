using System;
using System.Collections.Generic;
using UnityEngine;

namespace M2.Core
{
    public class LapTracker : MonoBehaviour
    {
        public event Action<int> OnCheckpointPassed;
        public event Action<int> OnLapCompleted;

        public int LapCount { get; private set; }

        int nextExpectedIndex;
        int lastCheckpointIndex;
        readonly Dictionary<int, Vector3> checkpointPositions = new Dictionary<int, Vector3>();

        // World position of whichever checkpoint the vehicle is currently supposed to be
        // driving toward. VehicleController compares its own movement against this to detect
        // driving the wrong way — reversing a long way, or turning fully around and driving
        // forward — without needing to wait for an actual checkpoint crossing (checkpoints can
        // sit 100+m apart on these tracks, so waiting for one made that detection effectively
        // never fire; playtester feedback: "배너도 전혀 안 뜨니까 고쳐").
        public Vector3 NextCheckpointPosition =>
            checkpointPositions.TryGetValue(nextExpectedIndex, out Vector3 pos) ? pos : transform.position;

        void Start()
        {
            lastCheckpointIndex = 0;
            var checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            foreach (var checkpoint in checkpoints)
            {
                checkpointPositions[checkpoint.index] = checkpoint.transform.position;
                if (checkpoint.index > lastCheckpointIndex)
                {
                    lastCheckpointIndex = checkpoint.index;
                }
            }

            // Vehicles spawn at the start/finish line (checkpoint 0), so that crossing
            // is already "used up" — the next one expected is checkpoint 1, not 0 again.
            nextExpectedIndex = lastCheckpointIndex > 0 ? 1 : 0;
        }

        public void NotifyCheckpointPassed(int index)
        {
            if (index != nextExpectedIndex) return;

            OnCheckpointPassed?.Invoke(index);

            if (index == lastCheckpointIndex)
            {
                nextExpectedIndex = 0;
            }
            else if (index == 0)
            {
                LapCount++;
                nextExpectedIndex = 1;
                OnLapCompleted?.Invoke(LapCount);
            }
            else
            {
                nextExpectedIndex = index + 1;
            }
        }
    }
}
