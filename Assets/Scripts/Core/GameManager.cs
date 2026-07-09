using System;
using System.Collections;
using System.Collections.Generic;
using M2.Player;
using UnityEngine;

namespace M2.Core
{
    public enum RaceState
    {
        PreRace,
        Briefing,
        Countdown,
        Racing,
        Finished
    }

    public class GameManager : MonoBehaviour
    {
        [Header("Race Flow Timing")]
        public float briefingDuration = 4f;
        public int countdownSeconds = 3;

        [Header("Race Rules")]
        public int targetLapCount = 3;
        public float lap1TimeLimit = 180f;
        [Tooltip("Bonus seconds added on completing laps 2, 3, 4 respectively.")]
        public float[] lapBonusSeconds = { 45f, 30f, 15f };

        [Header("References (auto-collected if empty)")]
        public List<LapTracker> racers = new List<LapTracker>();
        public List<VehicleController> vehicles = new List<VehicleController>();
        public RaceTimer raceTimer;

        // --- Public state ---
        public RaceState CurrentState { get; private set; } = RaceState.PreRace;
        public float TimeRemaining { get; private set; }

        // --- Events ---
        public event Action<RaceState> OnStateChanged;
        public event Action<int> OnCountdownTick;   // 3, 2, 1
        public event Action OnRaceStarted;
        public event Action<LapTracker> OnRaceWon;
        public event Action OnRaceDraw;

        bool raceEnded;
        readonly Dictionary<LapTracker, Action<int>> lapHandlers = new Dictionary<LapTracker, Action<int>>();

        void Start()
        {
            // Auto-collect references if not wired from the Inspector / TestTrackBuilder.
            if (racers.Count == 0)
            {
                racers.AddRange(FindObjectsByType<LapTracker>(FindObjectsSortMode.None));
            }
            if (vehicles.Count == 0)
            {
                vehicles.AddRange(FindObjectsByType<VehicleController>(FindObjectsSortMode.None));
            }
            if (raceTimer == null)
            {
                raceTimer = FindFirstObjectByType<RaceTimer>();
            }

            StartCoroutine(RunRaceFlow());
        }

        void Update()
        {
            if (CurrentState != RaceState.Racing) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                EndRace(null); // Time's up — draw
            }
        }

        // ---- Race flow coroutine ----

        IEnumerator RunRaceFlow()
        {
            // Lock all vehicles at start
            SetAllInputLocked(true);

            // Briefing
            SetState(RaceState.Briefing);
            yield return new WaitForSeconds(briefingDuration);

            // Countdown
            SetState(RaceState.Countdown);
            for (int i = countdownSeconds; i >= 1; i--)
            {
                OnCountdownTick?.Invoke(i);
                yield return new WaitForSeconds(1f);
            }

            // GO!
            OnCountdownTick?.Invoke(0); // 0 = "GO!"

            // Racing
            SetState(RaceState.Racing);
            TimeRemaining = lap1TimeLimit;

            if (raceTimer != null) raceTimer.StartRace();
            SetAllInputLocked(false);

            // Subscribe to lap events from each racer. Each racer gets its own closure
            // so HandleLapCompleted knows exactly which racer fired the event — required
            // once there's more than one racer, otherwise bonus time and win checks would
            // run once per racer in the list instead of once per actual lap completion.
            foreach (var racer in racers)
            {
                LapTracker capturedRacer = racer;
                Action<int> handler = lapNumber => HandleLapCompleted(capturedRacer, lapNumber);
                lapHandlers[capturedRacer] = handler;
                capturedRacer.OnLapCompleted += handler;
            }

            OnRaceStarted?.Invoke();
        }

        // ---- Lap / finish handling ----

        void HandleLapCompleted(LapTracker racer, int lapNumber)
        {
            if (raceEnded) return;

            // lapBonusSeconds[0] = bonus for completing lap 2, [1] = lap 3, [2] = lap 4
            int bonusIndex = lapNumber - 2; // lap 2 → index 0
            if (bonusIndex >= 0 && bonusIndex < lapBonusSeconds.Length)
            {
                TimeRemaining += lapBonusSeconds[bonusIndex];
            }

            if (racer.LapCount >= targetLapCount)
            {
                EndRace(racer);
            }
        }

        void EndRace(LapTracker winner)
        {
            if (raceEnded) return;
            raceEnded = true;

            if (raceTimer != null) raceTimer.StopRace();
            SetAllInputLocked(true);
            SetState(RaceState.Finished);

            // Unsubscribe lap events (using the same delegate instances we subscribed with)
            foreach (var kvp in lapHandlers)
            {
                kvp.Key.OnLapCompleted -= kvp.Value;
            }
            lapHandlers.Clear();

            if (winner != null)
            {
                OnRaceWon?.Invoke(winner);
            }
            else
            {
                OnRaceDraw?.Invoke();
            }
        }

        // ---- Helpers ----

        void SetState(RaceState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        void SetAllInputLocked(bool locked)
        {
            foreach (var vehicle in vehicles)
            {
                if (vehicle != null) vehicle.SetInputLocked(locked);
            }
        }
    }
}
