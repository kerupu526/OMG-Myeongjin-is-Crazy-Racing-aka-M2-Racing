using M2.Core;
using M2.Player;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // Center-screen wrong-way banner, driven by two independent signals:
    // 1. VehicleController.IsReverseBlocked (polled continuously, like VehicleStatusHUD) —
    //    true for as long as the reverse-distance budget is maxed out, i.e. the prevention
    //    is actively refusing further reverse input right now. This is the primary, reliable
    //    signal (see maxReverseDistance's comment for why the earlier checkpoint-crossing-only
    //    detector almost never fired: playtester feedback "배너도 전혀 안 뜨니까 고쳐").
    // 2. LapTracker.OnWrongWayDetected (a one-shot flash) — the coarser "crossed backward past
    //    an already-cleared checkpoint" signal, kept as a secondary catch for the harder-to-detect
    //    case of turning fully around and driving forward the wrong way (no reverse involved, so
    //    signal 1 alone wouldn't catch it).
    public class WrongWayWarning : MonoBehaviour
    {
        public VehicleController vehicleController;
        LapTracker lapTracker;
        public Text label;
        public float flashDisplaySeconds = 2f;

        float flashUntilTime = -1f;

        // Explicit bind (not a public field) for the same reason as ItemUseNotifier.Bind:
        // AddComponent<T>() runs OnEnable synchronously, before a caller can assign fields.
        public void Bind(LapTracker tracker)
        {
            if (lapTracker != null) lapTracker.OnWrongWayDetected -= HandleWrongWayFlash;
            lapTracker = tracker;
            if (lapTracker != null) lapTracker.OnWrongWayDetected += HandleWrongWayFlash;
        }

        void OnEnable()
        {
            if (label != null) label.text = "";
        }

        void OnDisable()
        {
            if (lapTracker != null) lapTracker.OnWrongWayDetected -= HandleWrongWayFlash;
        }

        void HandleWrongWayFlash()
        {
            flashUntilTime = Time.time + flashDisplaySeconds;
        }

        void Update()
        {
            if (label == null) return;

            if (vehicleController != null && vehicleController.IsReverseBlocked)
            {
                label.text = "⚠ 더 이상 후진할 수 없습니다! ⚠";
            }
            else if (Time.time < flashUntilTime)
            {
                label.text = "⚠ 반대 방향입니다! ⚠";
            }
            else
            {
                label.text = "";
            }
        }
    }
}
