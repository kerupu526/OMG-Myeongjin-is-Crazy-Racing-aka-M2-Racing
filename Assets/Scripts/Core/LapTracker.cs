using System;
using UnityEngine;

namespace M2.Core
{
    public class LapTracker : MonoBehaviour
    {
        public event Action<int> OnCheckpointPassed;
        public event Action<int> OnLapCompleted;
        // Fires when the vehicle crosses backward into the checkpoint it already passed —
        // e.g. reversing a long way, or turning around and driving forward the wrong way
        // around the loop. Checkpoint order validation below already made this harmless for
        // lap-counting (a backward crossing is silently ignored, can't be exploited to gain a
        // lap), but gave the player no feedback at all that anything unusual happened —
        // playtester feedback: "일정 부분 후진을 하거나... 반대방향으로 가면 보통 경고를
        // 하는데 우리는 그런 게 없어서 이상해보임". Doesn't block movement — CLAUDE.md's own
        // fix history documents reversing away from a wall as a legitimate recovery move.
        public event Action OnWrongWayDetected;

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
            if (index != nextExpectedIndex)
            {
                // A checkpoint trigger fires on entry from either side — crossing the one
                // immediately behind where we're headed means we've driven backward past it.
                int checkpointCount = lastCheckpointIndex + 1;
                int previousExpected = (nextExpectedIndex - 1 + checkpointCount) % checkpointCount;
                if (index == previousExpected)
                {
                    OnWrongWayDetected?.Invoke();
                }
                return;
            }

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
