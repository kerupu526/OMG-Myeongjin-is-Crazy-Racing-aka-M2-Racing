using System.Collections.Generic;
using M2.Core;
using M2.Player;
using M2.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace M2.Network
{
    /// <summary>
    /// A client-readable projection of one authoritative result row. The primitive fields live
    /// in NetworkVariables inside <see cref="NetworkRaceManager"/>; this type keeps HUD code from
    /// knowing which slots are host/client implementation details.
    /// </summary>
    public readonly struct NetworkRacerResult
    {
        public string DisplayName { get; }
        public int AvatarColorIndex { get; }
        public int Rank { get; }
        public bool Finished { get; }
        public float FinishTime { get; }
        public int Stars { get; }

        public bool HasProfile => !string.IsNullOrWhiteSpace(DisplayName);

        public NetworkRacerResult(string displayName, int avatarColorIndex, int rank,
            bool finished, float finishTime, int stars)
        {
            DisplayName = displayName;
            AvatarColorIndex = avatarColorIndex;
            Rank = rank;
            Finished = finished;
            FinishTime = finishTime;
            Stars = stars;
        }
    }

    // Milestone 2a: server-authoritative race-state synchronization.
    //
    // Consistent with the owner-authoritative MOVEMENT decision already made for Milestone 1
    // (see OwnerAuthoritativeNetworkTransform / NetworkVehicleSync), the whole RACE is run
    // authoritatively on the host only, and the result is replicated to clients as plain state:
    //
    //   - The host has a fully synced copy of BOTH vehicles (its own, driven locally; the
    //     client's, driven kinematically by the replicated NetworkTransform). Those synced
    //     copies pass through the scene's Checkpoint triggers on the host exactly like a local
    //     car does, so the host's LapTrackers count both players' laps authoritatively — no
    //     per-client lap RPC needed, and no way for a client's local physics to disagree about
    //     who has how many laps.
    //   - The scene's regular GameManager runs the entire race flow (briefing → countdown →
    //     timed race → win/draw), but ONLY on the host: the networked scene sets
    //     GameManager.autoStartOnStart=false, and this component calls gm.BeginRaceFlow() once
    //     both players have actually spawned. A client's own GameManager just sits idle.
    //   - This component mirrors the host GameManager's state (race state, time remaining,
    //     countdown value, each player's lap count, and complete final result rows) into
    //     NetworkVariables. Each player also submits their saved local profile once, so the
    //     same HUD/result information can be shown on both peers.
    //     Clients read those to drive their HUD (NetworkRaceHUD) and to lock/unlock their own
    //     vehicle's input for briefing/countdown/finish.
    //
    // It lives on a prefab the host spawns at runtime (NetworkRaceBootstrap) rather than an
    // in-scene NetworkObject, because Milestone 1 deliberately left NGO scene management off
    // (EnableSceneManagement=false) and in-scene NetworkObjects don't spawn reliably without it.
    // Spawning a registered prefab from the host is the same well-supported path the player
    // vehicle already uses.
    public class NetworkRaceManager : NetworkBehaviour
    {
        [Tooltip("이 인원수만큼 플레이어가 스폰되면 호스트가 레이스를 시작함. 축제용 1v1 기준 2.")]
        public int requiredPlayers = 2;

        // Server-written, everyone-read. Clients never write these (default write permission is
        // Server, which is exactly what owner-authoritative-movement-but-server-authoritative-
        // race wants here).
        readonly NetworkVariable<int> netState = new NetworkVariable<int>((int)RaceState.PreRace);
        readonly NetworkVariable<float> netTimeRemaining = new NetworkVariable<float>(0f);
        // -1 = not counting down (hide the countdown UI); 0 = "GO!"; 1..N = seconds left.
        readonly NetworkVariable<int> netCountdown = new NetworkVariable<int>(-1);
        readonly NetworkVariable<int> netHostLaps = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> netClientLaps = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> netRaceMode = new NetworkVariable<int>((int)RaceMode.Item);
        readonly NetworkVariable<int> netVictoryCondition = new NetworkVariable<int>((int)VictoryCondition.SimpleFinish);
        readonly NetworkVariable<int> netTargetLapCount = new NetworkVariable<int>(3);
        readonly NetworkVariable<float> netSpeedModeMaximumKph = new NetworkVariable<float>(RaceModeRules.SpeedModeMaximumKph);
        readonly NetworkVariable<FixedString64Bytes> netHostDisplayName = new NetworkVariable<FixedString64Bytes>("");
        readonly NetworkVariable<int> netHostAvatarColorIndex = new NetworkVariable<int>(0);
        readonly NetworkVariable<FixedString64Bytes> netClientDisplayName = new NetworkVariable<FixedString64Bytes>("");
        readonly NetworkVariable<int> netClientAvatarColorIndex = new NetworkVariable<int>(0);
        // 0 = no result yet, 1 = someone won, 2 = draw.
        readonly NetworkVariable<int> netResult = new NetworkVariable<int>(0);
        readonly NetworkVariable<ulong> netWinnerClientId = new NetworkVariable<ulong>(0);
        readonly NetworkVariable<FixedString64Bytes> netDrawReason = new NetworkVariable<FixedString64Bytes>("");
        readonly NetworkVariable<bool> netHostFinished = new NetworkVariable<bool>(false);
        readonly NetworkVariable<float> netHostFinishTime = new NetworkVariable<float>(0f);
        readonly NetworkVariable<int> netHostStars = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> netHostRank = new NetworkVariable<int>(0);
        readonly NetworkVariable<bool> netClientFinished = new NetworkVariable<bool>(false);
        readonly NetworkVariable<float> netClientFinishTime = new NetworkVariable<float>(0f);
        readonly NetworkVariable<int> netClientStars = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> netClientRank = new NetworkVariable<int>(0);

        // --- Read-only accessors for the HUD (valid on every instance) ---
        public RaceState State => (RaceState)netState.Value;
        public float TimeRemaining => netTimeRemaining.Value;
        public int Countdown => netCountdown.Value;
        public int HostLaps => netHostLaps.Value;
        public int ClientLaps => netClientLaps.Value;
        public RaceMode Mode => (RaceMode)netRaceMode.Value;
        public VictoryCondition CurrentVictoryCondition => (VictoryCondition)netVictoryCondition.Value;
        public int TargetLapCount => netTargetLapCount.Value;
        public float SpeedModeMaximumKph => netSpeedModeMaximumKph.Value;
        public int Result => netResult.Value;
        public ulong WinnerClientId => netWinnerClientId.Value;
        public string DrawReason => netDrawReason.Value.ToString();
        public NetworkRacerResult HostRacer => new NetworkRacerResult(netHostDisplayName.Value.ToString(),
            netHostAvatarColorIndex.Value, netHostRank.Value, netHostFinished.Value,
            netHostFinishTime.Value, netHostStars.Value);
        public NetworkRacerResult ClientRacer => new NetworkRacerResult(netClientDisplayName.Value.ToString(),
            netClientAvatarColorIndex.Value, netClientRank.Value, netClientFinished.Value,
            netClientFinishTime.Value, netClientStars.Value);

        // --- Server-only authoritative references ---
        GameManager gameManager;
        LapTracker hostTracker;
        LapTracker clientTracker;
        ulong otherClientId;
        bool flowBegun;
        bool eventsHooked;
        NetworkItemSpawnManager itemSpawnManager;

        void Awake()
        {
            itemSpawnManager = GetComponent<NetworkItemSpawnManager>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                gameManager = FindFirstObjectByType<GameManager>();
                HookGameManagerEvents();
            }

            // The manager is server-owned rather than player-owned, so profile submission uses
            // an Everyone-permitted server RPC. The server records the actual sender ID instead
            // of trusting any ID from the client payload.
            if (IsClient) SubmitLocalProfile();

            // Every instance (host included) drives its OWN vehicle's input lock from the
            // replicated race state — the host's GameManager also locks its own car via
            // SetAllInputLocked, so on the host this is a harmless double-set; on a pure client
            // it's the only thing that locks/unlocks the local car (its GameManager never runs).
            netState.OnValueChanged += HandleStateChangedForInputLock;
            netRaceMode.OnValueChanged += HandleRaceRulesChanged;
            netSpeedModeMaximumKph.OnValueChanged += HandleSpeedLimitChanged;
            ApplyLocalInputLock((RaceState)netState.Value);
            ApplyLocalRaceRules();
            ApplyItemSpawnRules();
        }

        public override void OnNetworkDespawn()
        {
            netState.OnValueChanged -= HandleStateChangedForInputLock;
            netRaceMode.OnValueChanged -= HandleRaceRulesChanged;
            netSpeedModeMaximumKph.OnValueChanged -= HandleSpeedLimitChanged;
            UnhookGameManagerEvents();
        }

        void HookGameManagerEvents()
        {
            if (eventsHooked || gameManager == null) return;
            eventsHooked = true;
            gameManager.OnStateChanged += HandleStateChanged;
            gameManager.OnCountdownTick += HandleCountdownTick;
            gameManager.OnRaceWon += HandleRaceWon;
            gameManager.OnRaceDraw += HandleRaceDraw;
        }

        void UnhookGameManagerEvents()
        {
            if (!eventsHooked || gameManager == null) return;
            eventsHooked = false;
            gameManager.OnStateChanged -= HandleStateChanged;
            gameManager.OnCountdownTick -= HandleCountdownTick;
            gameManager.OnRaceWon -= HandleRaceWon;
            gameManager.OnRaceDraw -= HandleRaceDraw;
        }

        // ---- Server: drive/mirror the authoritative GameManager ----

        void Update()
        {
            if (!IsServer)
            {
                ApplyLocalRaceRules();
                return;
            }

            SyncRoomSettings();

            if (!flowBegun)
            {
                TryBeginRace();
                return;
            }

            // Per-frame mirror of the values the events below don't cover.
            netTimeRemaining.Value = gameManager != null ? gameManager.TimeRemaining : 0f;
            if (hostTracker != null) netHostLaps.Value = hostTracker.LapCount;
            if (clientTracker != null) netClientLaps.Value = clientTracker.LapCount;
        }

        void SyncRoomSettings()
        {
            if (gameManager == null) return;
            netRaceMode.Value = (int)gameManager.raceMode;
            netVictoryCondition.Value = (int)gameManager.victoryCondition;
            netTargetLapCount.Value = gameManager.targetLapCount;
            netSpeedModeMaximumKph.Value = gameManager.speedModeMaximumKph;
            ApplyItemSpawnRules();
        }

        // Waits until both players' vehicles exist, then registers them with the host GameManager
        // and starts the race. Polling connected clients' PlayerObjects (rather than having each
        // vehicle register itself) sidesteps any spawn-vs-racemanager ordering fragility — this
        // just picks up whatever is present each frame until it has the full grid.
        void TryBeginRace()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
                HookGameManagerEvents();
                if (gameManager == null) return;
            }

            var manager = NetworkManager;
            if (manager == null) return;

            int found = 0;
            foreach (var client in manager.ConnectedClientsList)
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject == null) continue;

                LapTracker tracker = playerObject.GetComponent<LapTracker>();
                VehicleController vehicle = playerObject.GetComponent<VehicleController>();
                if (tracker == null || vehicle == null) continue;

                found++;
                gameManager.RegisterRacer(tracker, vehicle);

                if (client.ClientId == NetworkManager.ServerClientId)
                {
                    hostTracker = tracker;
                }
                else
                {
                    clientTracker = tracker;
                    otherClientId = client.ClientId;
                }
            }

            if (found >= requiredPlayers && hostTracker != null)
            {
                flowBegun = true;
                gameManager.BeginRaceFlow();
            }
        }

        void HandleStateChanged(RaceState state)
        {
            netState.Value = (int)state;
            // Stop advertising a countdown number once we're actually racing / done.
            if (state == RaceState.Racing || state == RaceState.Finished)
            {
                netCountdown.Value = -1;
            }
        }

        void HandleCountdownTick(int value) => netCountdown.Value = value;

        void HandleRaceWon(LapTracker winner)
        {
            netResult.Value = 1;
            netDrawReason.Value = "";
            if (winner == clientTracker)
            {
                netWinnerClientId.Value = otherClientId;
            }
            else
            {
                netWinnerClientId.Value = NetworkManager.ServerClientId;
            }
            MirrorFinalResults();
        }

        void HandleRaceDraw(string reason)
        {
            netResult.Value = 2;
            netWinnerClientId.Value = 0;
            netDrawReason.Value = reason ?? "";
            MirrorFinalResults();
        }

        void SubmitLocalProfile()
        {
            FixedString64Bytes displayName = new FixedString64Bytes(
                M2PlayerProfile.NormalizeDisplayName(M2PlayerProfile.DisplayName));
            int avatarColorIndex = M2PlayerProfile.AvatarColorIndex;
            if (IsServer)
            {
                WriteRacerProfile(NetworkManager.ServerClientId, displayName, avatarColorIndex);
            }
            else
            {
                SubmitLocalProfileRpc(displayName, avatarColorIndex);
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitLocalProfileRpc(FixedString64Bytes displayName, int avatarColorIndex,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            WriteRacerProfile(rpcParams.Receive.SenderClientId, displayName, avatarColorIndex);
        }

        void WriteRacerProfile(ulong clientId, FixedString64Bytes displayName, int avatarColorIndex)
        {
            string normalizedName = M2PlayerProfile.NormalizeDisplayName(displayName.ToString());
            FixedString64Bytes normalizedFixedName = new FixedString64Bytes(normalizedName);
            int normalizedColorIndex = M2PlayerProfile.NormalizeAvatarColorIndex(avatarColorIndex);
            if (clientId == NetworkManager.ServerClientId)
            {
                netHostDisplayName.Value = normalizedFixedName;
                netHostAvatarColorIndex.Value = normalizedColorIndex;
            }
            else
            {
                netClientDisplayName.Value = normalizedFixedName;
                netClientAvatarColorIndex.Value = normalizedColorIndex;
            }
        }

        void MirrorFinalResults()
        {
            if (!IsServer || gameManager == null) return;

            RaceFinishResult hostResult = FindFinalResult(hostTracker);
            RaceFinishResult clientResult = FindFinalResult(clientTracker);
            RaceResultOrdering.GetPairRanks(hostResult, clientResult, gameManager.victoryCondition,
                out int hostRank, out int clientRank);

            netHostFinished.Value = hostResult.finished;
            netHostFinishTime.Value = hostResult.finishTime;
            netHostStars.Value = hostResult.stars;
            netHostRank.Value = hostRank;
            netClientFinished.Value = clientResult.finished;
            netClientFinishTime.Value = clientResult.finishTime;
            netClientStars.Value = clientResult.stars;
            netClientRank.Value = clientRank;

            if (hostTracker != null) netHostLaps.Value = hostTracker.LapCount;
            if (clientTracker != null) netClientLaps.Value = clientTracker.LapCount;
        }

        RaceFinishResult FindFinalResult(LapTracker racer)
        {
            if (racer == null) return default;
            IReadOnlyList<RaceFinishResult> results = gameManager.LastRaceResults;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].racer == racer) return results[i];
            }

            return new RaceFinishResult { racer = racer };
        }

        // ---- Every instance: local input lock follows the replicated state ----

        void HandleStateChangedForInputLock(int _, int newValue)
        {
            ApplyLocalInputLock((RaceState)newValue);
        }

        void HandleRaceRulesChanged(int _, int __)
        {
            ApplyLocalRaceRules();
            ApplyItemSpawnRules();
        }

        void HandleSpeedLimitChanged(float _, float __) => ApplyLocalRaceRules();

        void ApplyLocalInputLock(RaceState state)
        {
            VehicleController localVehicle = LocalOwnedVehicle();
            if (localVehicle == null) return;
            localVehicle.SetInputLocked(state != RaceState.Racing);
        }

        void ApplyLocalRaceRules()
        {
            VehicleController localVehicle = LocalOwnedVehicle();
            if (localVehicle == null) return;
            if (Mode == RaceMode.Speed) localVehicle.SetAbsoluteSpeedLimitKph(SpeedModeMaximumKph);
            else localVehicle.ClearAbsoluteSpeedLimit();
        }

        void ApplyItemSpawnRules()
        {
            if (itemSpawnManager == null) itemSpawnManager = GetComponent<NetworkItemSpawnManager>();
            if (itemSpawnManager == null) return;

            RaceMode mode = IsServer && gameManager != null ? gameManager.raceMode : Mode;
            itemSpawnManager.SetSpawnEnabled(mode != RaceMode.Speed);
        }

        VehicleController LocalOwnedVehicle()
        {
            var manager = NetworkManager;
            NetworkObject playerObject = manager != null && manager.LocalClient != null
                ? manager.LocalClient.PlayerObject
                : null;
            return playerObject != null ? playerObject.GetComponent<VehicleController>() : null;
        }
    }
}
