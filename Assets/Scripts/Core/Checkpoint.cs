using UnityEngine;

namespace M2.Core
{
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Order of this checkpoint along the track. Index 0 is the finish line.")]
        public int index;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            LapTracker tracker = other.GetComponentInParent<LapTracker>();
            if (tracker != null)
            {
                tracker.NotifyCheckpointPassed(index);
            }
        }
    }
}
