using System;
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

        void Start()
        {
            lastCheckpointIndex = 0;
            var checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            foreach (var checkpoint in checkpoints)
            {
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
