using System.Collections.Generic;
using UnityEngine;

namespace M2.Core
{
    public class RaceTimer : MonoBehaviour
    {
        public LapTracker lapTracker;

        public bool IsRunning { get; private set; }
        public float ElapsedTime { get; private set; }
        public IReadOnlyList<float> LapSplits => lapSplits;

        readonly List<float> lapSplits = new List<float>();
        float lastSplitTime;

        void OnEnable()
        {
            if (lapTracker != null)
            {
                lapTracker.OnLapCompleted += HandleLapCompleted;
            }
        }

        void OnDisable()
        {
            if (lapTracker != null)
            {
                lapTracker.OnLapCompleted -= HandleLapCompleted;
            }
        }

        void Update()
        {
            if (IsRunning)
            {
                ElapsedTime += Time.deltaTime;
            }
        }

        public void StartRace()
        {
            ElapsedTime = 0f;
            lastSplitTime = 0f;
            lapSplits.Clear();
            IsRunning = true;
        }

        public void StopRace()
        {
            IsRunning = false;
        }

        void HandleLapCompleted(int lapNumber)
        {
            float splitTime = ElapsedTime - lastSplitTime;
            lapSplits.Add(splitTime);
            lastSplitTime = ElapsedTime;
        }
    }
}
