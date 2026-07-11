using System.Collections;
using M2.Core;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // Center-screen "반대 방향입니다!" flash when LapTracker detects the vehicle crossing
    // backward past a checkpoint it already cleared (reversing a long way, or turning around
    // and driving forward the wrong way). Same temporary-banner pattern as ItemUseNotifier —
    // doesn't block movement, just tells the player something unusual happened, since reverse
    // itself is a legitimate wall-recovery move (see CLAUDE.md's fix history).
    public class WrongWayWarning : MonoBehaviour
    {
        LapTracker lapTracker;
        public Text label;
        public float displaySeconds = 2f;

        Coroutine hideRoutine;

        // Explicit bind (not a public field) for the same reason as ItemUseNotifier.Bind:
        // AddComponent<T>() runs OnEnable synchronously, before a caller can assign fields.
        public void Bind(LapTracker tracker)
        {
            if (lapTracker != null) lapTracker.OnWrongWayDetected -= HandleWrongWay;
            lapTracker = tracker;
            if (lapTracker != null) lapTracker.OnWrongWayDetected += HandleWrongWay;
        }

        void OnEnable()
        {
            if (label != null) label.text = "";
        }

        void OnDisable()
        {
            if (lapTracker != null) lapTracker.OnWrongWayDetected -= HandleWrongWay;
        }

        void HandleWrongWay()
        {
            if (label == null) return;

            label.text = "⚠ 반대 방향입니다! ⚠";
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displaySeconds);
            label.text = "";
            hideRoutine = null;
        }
    }
}
