using System;
using System.Collections;
using System.Collections.Generic;
using M2.Items;
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
        [Tooltip("true면 briefingDuration 타이머 대신 RequestStart()가 호출될 때까지 Briefing 상태로 대기함. 테스트 트랙에서 수동으로 '시작' 버튼을 누르게 하기 위한 옵션 — 기본은 false(기존 타이머 방식 그대로).")]
        public bool waitForManualStart = false;
        [Tooltip("true(기본)면 Start()에서 곧바로 레이스 흐름을 시작함 — 기존 로컬 씬 동작 그대로. 온라인(Netcode) 씬에서는 false로 두고, 호스트의 NetworkRaceManager가 두 플레이어가 모두 스폰된 뒤 BeginRaceFlow()를 직접 호출함.")]
        public bool autoStartOnStart = true;

        [Header("Race Rules")]
        public VictoryCondition victoryCondition = VictoryCondition.SimpleFinish;
        public int targetLapCount = 3;
        public float lap1TimeLimit = 180f;
        [Tooltip("Bonus seconds added on completing laps 2, 3, 4 respectively.")]
        public float[] lapBonusSeconds = { 45f, 30f, 15f };

        [Header("Room Mode Settings")]
        public RaceMode raceMode = RaceMode.Item;
        [Tooltip("스피드전의 절대 최고 속도(km/h). 아이템·드리프트 가속도 이 값을 넘을 수 없음.")]
        public float speedModeMaximumKph = RaceModeRules.SpeedModeMaximumKph;
        [Tooltip("스피드전에서 각 레이서에게 기본 휘발유를 자동 지급하는 간격(초).")]
        public float speedModeGasolineInterval = 15f;

        [Header("References (auto-collected if empty)")]
        public List<LapTracker> racers = new List<LapTracker>();
        public List<VehicleController> vehicles = new List<VehicleController>();
        public RaceTimer raceTimer;

        // --- Public state ---
        public RaceState CurrentState { get; private set; } = RaceState.PreRace;
        public float TimeRemaining { get; private set; }
        public float RaceElapsedTime { get; private set; }

        // --- Events ---
        public event Action<RaceState> OnStateChanged;
        public event Action<int> OnCountdownTick;   // 3, 2, 1
        public event Action OnRaceStarted;
        public event Action<LapTracker> OnRaceWon;
        public event Action<string> OnRaceDraw; // reason shown on the result screen (e.g. "제한시간 초과", "화상")

        bool raceEnded;
        bool raceFlowStarted;
        bool startRequested;
        readonly Dictionary<LapTracker, Action<int>> lapHandlers = new Dictionary<LapTracker, Action<int>>();
        readonly Dictionary<LapTracker, RaceFinishResult> finishResults = new Dictionary<LapTracker, RaceFinishResult>();

        public bool IsSpeedMode => raceMode == RaceMode.Speed;

        void Awake()
        {
            if (GetComponent<SpeedModeGasolineDistributor>() == null)
                gameObject.AddComponent<SpeedModeGasolineDistributor>();
        }

        // Called by UI (a "시작" button) or a key press to end the Briefing wait when
        // waitForManualStart is true. Harmless no-op otherwise/at any other time.
        public void RequestStart()
        {
            startRequested = true;
        }

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

            ApplyRaceModeRules();

            // Local scenes (TestTrackBuilder / persisted Stage_*.unity) auto-start immediately,
            // exactly as before. Networked scenes set autoStartOnStart=false and let the host's
            // NetworkRaceManager call BeginRaceFlow() once both players have actually spawned —
            // otherwise the race would start (and the timer would run) before the second player
            // is even connected, and the client's own GameManager would run a second, divergent
            // copy of the flow.
            if (autoStartOnStart)
            {
                BeginRaceFlow();
            }
        }

        // Starts the race flow coroutine (idempotent — a second call is a no-op). Public so a
        // networked host can trigger the start after both players spawn; local scenes call it
        // from Start() via autoStartOnStart.
        public void BeginRaceFlow()
        {
            if (raceFlowStarted) return;
            ApplyRaceModeRules();
            raceFlowStarted = true;
            StartCoroutine(RunRaceFlow());
        }

        public void ConfigureRoomSettings(RaceMode selectedMode, int requestedLapCount,
            VictoryCondition requestedVictoryCondition)
        {
            raceMode = selectedMode;
            if (raceMode == RaceMode.Speed)
            {
                targetLapCount = RaceModeRules.SpeedModeLapCount;
                victoryCondition = VictoryCondition.SimpleFinish;
            }
            else
            {
                targetLapCount = RaceModeRules.NormalizeItemLapCount(requestedLapCount);
                victoryCondition = requestedVictoryCondition;
            }

            ApplyRaceModeRules();
        }

        public void ApplyRaceModeRules()
        {
            if (raceMode == RaceMode.Speed)
            {
                targetLapCount = RaceModeRules.SpeedModeLapCount;
                victoryCondition = VictoryCondition.SimpleFinish;
            }

            for (int i = 0; i < vehicles.Count; i++) ApplyModeToVehicle(vehicles[i]);
        }

        // Adds a racer/vehicle pair before the flow starts — used by the networked host, whose
        // vehicles spawn dynamically (NetworkVehicleSync) rather than being wired at build time
        // like a local scene's single vehicle. Ignores duplicates and no-ops once racing has
        // begun (the lap-event subscription in RunRaceFlow happens at Racing start, so racers
        // must be registered before BeginRaceFlow).
        public void RegisterRacer(LapTracker racer, VehicleController vehicle)
        {
            if (racer != null && !racers.Contains(racer)) racers.Add(racer);
            if (vehicle != null && !vehicles.Contains(vehicle)) vehicles.Add(vehicle);
            ApplyModeToVehicle(vehicle);
        }

        void ApplyModeToVehicle(VehicleController vehicle)
        {
            if (vehicle == null) return;
            if (raceMode == RaceMode.Speed) vehicle.SetAbsoluteSpeedLimitKph(speedModeMaximumKph);
            else vehicle.ClearAbsoluteSpeedLimit();
        }

        void Update()
        {
            if (CurrentState != RaceState.Racing) return;

            TimeRemaining -= Time.deltaTime;
            RaceElapsedTime += Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                if (victoryCondition == VictoryCondition.StarBet) ResolveStarBetRace();
                else EndRace(null); // Time's up — draw
            }
        }

        // ---- Race flow coroutine ----

        IEnumerator RunRaceFlow()
        {
            // Lock all vehicles at start
            SetAllInputLocked(true);

            // Briefing
            SetState(RaceState.Briefing);
            if (waitForManualStart)
            {
                startRequested = false;
                yield return new WaitUntil(() => startRequested);
            }
            else
            {
                yield return new WaitForSeconds(briefingDuration);
            }

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
            RaceElapsedTime = 0f;
            finishResults.Clear();

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
                if (victoryCondition == VictoryCondition.SimpleFinish)
                {
                    EndRace(racer);
                }
                else if (!finishResults.ContainsKey(racer))
                {
                    finishResults[racer] = new RaceFinishResult
                    {
                        racer = racer,
                        finished = true,
                        finishTime = RaceElapsedTime,
                        stars = ComputeStars(racer, RaceElapsedTime)
                    };
                    VehicleController vehicle = racer.GetComponent<VehicleController>();
                    if (vehicle != null) vehicle.SetInputLocked(true);
                    if (finishResults.Count >= racers.Count) ResolveStarBetRace();
                }
            }
        }

        int ComputeStars(LapTracker racer, float finishTime)
        {
            foreach (MonoBehaviour component in racer.GetComponents<MonoBehaviour>())
            {
                if (component is IRaceStarProvider provider) return provider.ComputeTotalStars(finishTime);
            }
            return 0;
        }

        void ResolveStarBetRace()
        {
            var results = new List<RaceFinishResult>(racers.Count);
            foreach (LapTracker racer in racers)
            {
                if (finishResults.TryGetValue(racer, out RaceFinishResult result)) results.Add(result);
                else results.Add(new RaceFinishResult { racer = racer, finished = false });
            }

            LapTracker winner = RaceResultResolver.ResolveStarBet(results, out string drawReason);
            EndRace(winner, drawReason ?? "별점 내기 종료");
        }

        void EndRace(LapTracker winner, string drawReason = "제한시간 초과")
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
                OnRaceDraw?.Invoke(drawReason);
            }
        }

        // Lets a stage-specific instant-loss hazard (e.g. Nether Fortress's burn game over)
        // end the race early. Real per-racer win/loss handling doesn't exist yet (CLAUDE.md
        // 우선순위 5), so this is treated as a draw for now — playtester ask: "패배한 걸로 치고
        // 일단 무승부 처리를 내자".
        public void EndRaceAsDraw(string reason)
        {
            EndRace(null, reason);
        }

        public void EndRaceWithLoss(LapTracker loser, string reason)
        {
            LapTracker winner = null;
            foreach (LapTracker racer in racers)
            {
                if (racer != null && racer != loser)
                {
                    winner = racer;
                    break;
                }
            }
            EndRace(winner, winner == null ? reason : null);
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
