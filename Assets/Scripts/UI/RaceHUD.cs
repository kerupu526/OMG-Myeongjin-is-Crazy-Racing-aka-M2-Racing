using M2.Core;
using M2.Items;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // Minimal debug HUD to verify the lap/timer loop and item slots. Not final UI design.
    public class RaceHUD : MonoBehaviour
    {
        public LapTracker lapTracker;
        public RaceTimer raceTimer;
        public ItemSlots itemSlots;
        public GameManager gameManager;
        public Text label;

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

        void HandleLapCompleted(int lapNumber)
        {
            // Forces an immediate refresh on lap change; Update() covers the timer tick.
        }

        void Update()
        {
            if (label == null) return;

            int lap = lapTracker != null ? lapTracker.LapCount : 0;
            float elapsed = raceTimer != null ? raceTimer.ElapsedTime : 0f;
            float lastLap = 0f;
            if (raceTimer != null && raceTimer.LapSplits.Count > 0)
            {
                lastLap = raceTimer.LapSplits[raceTimer.LapSplits.Count - 1];
            }

            string primary = itemSlots != null && itemSlots.PrimarySlot != null ? itemSlots.PrimarySlot.itemName : "-";
            string secondary = itemSlots != null && itemSlots.SecondarySlot != null ? itemSlots.SecondarySlot.itemName : "-";
            float timeLeft = gameManager != null ? gameManager.TimeRemaining : 0f;

            label.text = $"Lap: {lap}\nTime: {FormatTime(elapsed)}\nTime Left: {FormatTime(timeLeft)}\nLast Lap: {FormatTime(lastLap)}\nSlot 1: {primary}\nSlot 2: {secondary}";
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.00}";
        }
    }
}
