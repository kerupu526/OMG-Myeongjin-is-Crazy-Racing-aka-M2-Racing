using System.Collections.Generic;
using M2.Core;
using M2.Player;
using M2.Stage;
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
        public M2AvatarAppearance Appearance { get; }
        public int AvatarColorIndex => Appearance.BodyColorIndex;
        public int Rank { get; }
        public bool Finished { get; }
        public float FinishTime { get; }
        public int Stars { get; }

        public bool HasProfile => !string.IsNullOrWhiteSpace(DisplayName);

        public NetworkRacerResult(string displayName, M2AvatarAppearance appearance, int rank,
            bool finished, float finishTime, int stars)
        {
            DisplayName = displayName;
            Appearance = M2PlayerProfile.NormalizeAppearance(appearance);
            Rank = rank;
            Finished = finished;
            FinishTime = finishTime;
            Stars = stars;
        }

        public NetworkRacerResult(string displayName, int avatarColorIndex, int rank,
            bool finished, float finishTime, int stars)
            : this(displayName, new M2AvatarAppearance(avatarColorIndex, M2AvatarEyes.Round,
                M2AvatarMouth.Smile, true, true, M2AvatarHat.Cap, 0), rank, finished, finishTime, stars)
        {
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
        // The connected scene doubles as the lobby. The host owns rules/stage selection, while
        // both racers independently mark themselves ready before the authoritative flow begins.
        readonly NetworkVariable<bool> netLobbyOpen = new NetworkVariable<bool>(true);
        readonly NetworkVariable<int> netSelectedStage = new NetworkVariable<int>((int)StageType.BikiniCity);
        readonly NetworkVariable<bool> netHostReady = new NetworkVariable<bool>(false);
        readonly NetworkVariable<bool> netClientReady = new NetworkVariable<bool>(false);
        // 0 = none, 1 = rematch, 2 = return to lobby. Each racer submits one choice.
        readonly NetworkVariable<int> netHostPostRaceChoice = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> netClientPostRaceChoice = new NetworkVariable<int>(0);
        readonly NetworkVariable<FixedString64Bytes> netHostDisplayName = new NetworkVariable<FixedString64Bytes>("");
        readonly NetworkVariable<int> netHostAvatarColorIndex = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> netHostAvatarEyes = new NetworkVariable<int>((int)M2AvatarEyes.Round);
        readonly NetworkVariable<int> netHostAvatarMouth = new NetworkVariable<int>((int)M2AvatarMouth.Smile);
        readonly NetworkVariable<bool> netHostAvatarCheeks = new NetworkVariable<bool>(true);
        readonly NetworkVariable<bool> netHostAvatarEars = new NetworkVariable<bool>(true);
        readonly NetworkVariable<int> netHostAvatarHat = new NetworkVariable<int>((int)M2AvatarHat.Cap);
        readonly NetworkVariable<int> netHostAvatarPlate = new NetworkVariable<int>(0);
        readonly NetworkVariable<FixedString64Bytes> netClientDisplayName = new NetworkVariable<FixedString64Bytes>("");
        readonly NetworkVariable<int> netClientAvatarColorIndex = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> netClientAvatarEyes = new NetworkVariable<int>((int)M2AvatarEyes.Round);
        readonly NetworkVariable<int> netClientAvatarMouth = new NetworkVariable<int>((int)M2AvatarMouth.Smile);
        readonly NetworkVariable<bool> netClientAvatarCheeks = new NetworkVariable<bool>(true);
        readonly NetworkVariable<bool> netClientAvatarEars = new NetworkVariable<bool>(true);
        readonly NetworkVariable<int> netClientAvatarHat = new NetworkVariable<int>((int)M2AvatarHat.Cap);
        readonly NetworkVariable<int> netClientAvatarPlate = new NetworkVariable<int>(0);
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
        public bool LobbyOpen => netLobbyOpen.Value;
        public StageType SelectedStage => (StageType)netSelectedStage.Value;
        public bool HostReady => netHostReady.Value;
        public bool ClientReady => netClientReady.Value;
        public bool BothPlayersReady => netHostReady.Value && netClientReady.Value;
        public int HostPostRaceChoice => netHostPostRaceChoice.Value;
        public int ClientPostRaceChoice => netClientPostRaceChoice.Value;
        public int Result => netResult.Value;
        public ulong WinnerClientId => netWinnerClientId.Value;
        public string DrawReason => netDrawReason.Value.ToString();
        public NetworkRacerResult HostRacer => new NetworkRacerResult(netHostDisplayName.Value.ToString(),
            ReadAppearance(true), netHostRank.Value, netHostFinished.Value,
            netHostFinishTime.Value, netHostStars.Value);
        public NetworkRacerResult ClientRacer => new NetworkRacerResult(netClientDisplayName.Value.ToString(),
            ReadAppearance(false), netClientRank.Value, netClientFinished.Value,
            netClientFinishTime.Value, netClientStars.Value);

        // --- Server-only authoritative references ---
        GameManager gameManager;
        LapTracker hostTracker;
        LapTracker clientTracker;
        ulong otherClientId;
        bool flowBegun;
        bool eventsHooked;
        NetworkItemSpawnManager itemSpawnManager;
        NetworkStageTheme stageTheme;

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
            netSelectedStage.OnValueChanged += HandleStageChanged;
            ApplyLocalInputLock((RaceState)netState.Value);
            ApplyLocalRaceRules();
            ApplyItemSpawnRules();
            ApplySelectedStageTheme();
        }

        public override void OnNetworkDespawn()
        {
            netState.OnValueChanged -= HandleStateChangedForInputLock;
            netRaceMode.OnValueChanged -= HandleRaceRulesChanged;
            netSpeedModeMaximumKph.OnValueChanged -= HandleSpeedLimitChanged;
            netSelectedStage.OnValueChanged -= HandleStageChanged;
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
            TryResolvePostRaceChoice();
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

            if (found >= requiredPlayers && hostTracker != null && clientTracker != null)
            {
                if (netLobbyOpen.Value)
                {
                    // A room is not allowed to launch just because the second transport
                    // connection appeared. Both players visibly confirm the current host rules.
                    if (!BothPlayersReady) return;
                    netLobbyOpen.Value = false;
                }
                flowBegun = true;
                gameManager.BeginRaceFlow();
            }
        }

        // ---- Lobby rules and readiness ------------------------------------------------------

        /// <summary>Sets this client's ready chip. The server stores the sender identity itself.</summary>
        public void RequestLobbyReady(bool ready)
        {
            if (!IsClient || !LobbyOpen) return;
            if (IsServer)
            {
                SetRacerReady(NetworkManager.LocalClientId, ready);
            }
            else
            {
                RequestLobbyReadyRpc(ready);
            }
        }

        /// <summary>
        /// Host-only lobby configuration. Calling this on the host updates both the visible
        /// lobby data and the server's GameManager; clients can only receive the result.
        /// </summary>
        public void RequestLobbySettings(RaceMode mode, int itemLapCount,
            VictoryCondition victoryCondition, StageType stage)
        {
            if (!IsClient || !LobbyOpen) return;
            if (IsServer)
            {
                ApplyLobbySettings(NetworkManager.LocalClientId, mode, itemLapCount, victoryCondition, stage);
            }
            else
            {
                RequestLobbySettingsRpc((int)mode, itemLapCount, (int)victoryCondition, (int)stage);
            }
        }

        /// <summary>Republishes the saved local avatar after the player edits it in the lobby.</summary>
        public void RequestProfileUpdate()
        {
            if (IsClient) SubmitLocalProfile();
        }

        [Rpc(SendTo.Server)]
        void RequestLobbyReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            SetRacerReady(rpcParams.Receive.SenderClientId, ready);
        }

        [Rpc(SendTo.Server)]
        void RequestLobbySettingsRpc(int mode, int itemLapCount, int victoryCondition, int stage,
            RpcParams rpcParams = default)
        {
            ApplyLobbySettings(rpcParams.Receive.SenderClientId, (RaceMode)mode, itemLapCount,
                (VictoryCondition)victoryCondition, (StageType)stage);
        }

        void SetRacerReady(ulong clientId, bool ready)
        {
            if (!IsServer || !netLobbyOpen.Value || NetworkManager == null) return;
            if (clientId == NetworkManager.ServerClientId) netHostReady.Value = ready;
            else netClientReady.Value = ready;
        }

        void ApplyLobbySettings(ulong senderClientId, RaceMode mode, int itemLapCount,
            VictoryCondition victoryCondition, StageType stage)
        {
            if (!IsServer || !netLobbyOpen.Value || NetworkManager == null ||
                senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            RaceMode normalizedMode = NormalizeRaceMode(mode);
            VictoryCondition normalizedVictory = NormalizeVictoryCondition(victoryCondition);
            StageType normalizedStage = NormalizeStage(stage);
            gameManager ??= FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                gameManager.ConfigureRoomSettings(normalizedMode, itemLapCount, normalizedVictory);
                netRaceMode.Value = (int)gameManager.raceMode;
                netVictoryCondition.Value = (int)gameManager.victoryCondition;
                netTargetLapCount.Value = gameManager.targetLapCount;
                netSpeedModeMaximumKph.Value = gameManager.speedModeMaximumKph;
            }
            else
            {
                netRaceMode.Value = (int)normalizedMode;
                netTargetLapCount.Value = normalizedMode == RaceMode.Speed
                    ? RaceModeRules.SpeedModeLapCount
                    : RaceModeRules.NormalizeItemLapCount(itemLapCount);
                netVictoryCondition.Value = (int)(normalizedMode == RaceMode.Speed
                    ? VictoryCondition.SimpleFinish
                    : normalizedVictory);
            }

            netSelectedStage.Value = (int)normalizedStage;
            // Any rule change requires a fresh acknowledgement on both sides.
            netHostReady.Value = false;
            netClientReady.Value = false;
            ApplyItemSpawnRules();
        }

        static StageType NormalizeStage(StageType stage)
        {
            return stage switch
            {
                StageType.BikiniCity => StageType.BikiniCity,
                StageType.AfricaTv => StageType.AfricaTv,
                StageType.NetherFortress => StageType.NetherFortress,
                _ => StageType.BikiniCity,
            };
        }

        static RaceMode NormalizeRaceMode(RaceMode mode) =>
            mode == RaceMode.Speed ? RaceMode.Speed : RaceMode.Item;

        static VictoryCondition NormalizeVictoryCondition(VictoryCondition condition) =>
            condition == VictoryCondition.StarBet ? VictoryCondition.StarBet : VictoryCondition.SimpleFinish;

        // ---- Post-race navigation ----------------------------------------------------------

        /// <summary>Requests a synchronized rematch. The next race only begins after both agree.</summary>
        public void RequestRematch()
        {
            RequestPostRaceChoice(1);
        }

        /// <summary>Requests a synchronized return to the connected room lobby.</summary>
        public void RequestReturnToLobby()
        {
            RequestPostRaceChoice(2);
        }

        void RequestPostRaceChoice(int choice)
        {
            if (!IsClient || Result == 0 || choice < 1 || choice > 2) return;
            if (IsServer)
            {
                SetPostRaceChoice(NetworkManager.LocalClientId, choice);
            }
            else
            {
                RequestPostRaceChoiceRpc(choice);
            }
        }

        [Rpc(SendTo.Server)]
        void RequestPostRaceChoiceRpc(int choice, RpcParams rpcParams = default)
        {
            SetPostRaceChoice(rpcParams.Receive.SenderClientId, choice);
        }

        void SetPostRaceChoice(ulong clientId, int choice)
        {
            if (!IsServer || Result == 0 || NetworkManager == null || choice < 1 || choice > 2) return;
            if (clientId == NetworkManager.ServerClientId) netHostPostRaceChoice.Value = choice;
            else netClientPostRaceChoice.Value = choice;
        }

        void TryResolvePostRaceChoice()
        {
            if (!IsServer || Result == 0) return;
            int hostChoice = netHostPostRaceChoice.Value;
            int clientChoice = netClientPostRaceChoice.Value;
            if (hostChoice == 0 || clientChoice == 0 || hostChoice != clientChoice) return;

            ResetRound(hostChoice == 2);
        }

        void ResetRound(bool returnToLobby)
        {
            if (!IsServer) return;

            gameManager ??= FindFirstObjectByType<GameManager>();
            gameManager?.ResetRaceFlow();
            if (NetworkManager != null)
            {
                foreach (var client in NetworkManager.ConnectedClientsList)
                {
                    NetworkObject playerObject = client.PlayerObject;
                    if (playerObject == null) continue;
                    NetworkItemSlots slots = playerObject.GetComponent<NetworkItemSlots>();
                    if (slots != null) slots.ServerResetForNewRound();
                }
            }

            flowBegun = false;
            netLobbyOpen.Value = returnToLobby;
            netHostReady.Value = false;
            netClientReady.Value = false;
            netHostPostRaceChoice.Value = 0;
            netClientPostRaceChoice.Value = 0;
            netResult.Value = 0;
            netWinnerClientId.Value = 0;
            netDrawReason.Value = "";
            netHostLaps.Value = 0;
            netClientLaps.Value = 0;
            netHostFinished.Value = false;
            netClientFinished.Value = false;
            netHostFinishTime.Value = 0f;
            netClientFinishTime.Value = 0f;
            netHostStars.Value = 0;
            netClientStars.Value = 0;
            netHostRank.Value = 0;
            netClientRank.Value = 0;
            netTimeRemaining.Value = 0f;
            netCountdown.Value = -1;
            netState.Value = (int)RaceState.PreRace;
            ApplyItemSpawnRules();
            ResetLocalRoundRpc();
        }

        [Rpc(SendTo.Everyone)]
        void ResetLocalRoundRpc()
        {
            NetworkObject playerObject = NetworkManager != null && NetworkManager.LocalClient != null
                ? NetworkManager.LocalClient.PlayerObject
                : null;
            if (playerObject == null) return;

            LapTracker tracker = playerObject.GetComponent<LapTracker>();
            if (tracker != null) tracker.ResetRaceProgress();
            VehicleController vehicle = playerObject.GetComponent<VehicleController>();
            if (vehicle != null) vehicle.ResetRaceState();

            // Vehicle movement is owner-authoritative, so only its local owner moves back to
            // the grid; OwnerAuthoritativeNetworkTransform then replicates the reset to peers.
            if (!playerObject.IsOwner) return;
            RaceStartGrid grid = FindFirstObjectByType<RaceStartGrid>();
            if (grid == null) return;
            bool isServerOwned = playerObject.OwnerClientId == NetworkManager.ServerClientId;
            grid.GetSlot(isServerOwned, out Vector3 position, out Quaternion rotation);
            playerObject.transform.SetPositionAndRotation(position, rotation);
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
            FixedString64Bytes displayName = new FixedString64Bytes(M2PlayerProfile.TaggedDisplayName);
            M2AvatarAppearance appearance = M2PlayerProfile.Appearance;
            if (IsServer)
            {
                WriteRacerProfile(NetworkManager.ServerClientId, displayName, appearance);
            }
            else
            {
                SubmitLocalProfileRpc(displayName, appearance.BodyColorIndex, (int)appearance.Eyes,
                    (int)appearance.Mouth, appearance.HasCheeks, appearance.HasEars,
                    (int)appearance.Hat, appearance.PlateIndex);
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitLocalProfileRpc(FixedString64Bytes displayName, int avatarColorIndex, int eyes, int mouth,
            bool cheeks, bool ears, int hat, int plate,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            WriteRacerProfile(rpcParams.Receive.SenderClientId, displayName,
                new M2AvatarAppearance(avatarColorIndex, (M2AvatarEyes)eyes, (M2AvatarMouth)mouth,
                    cheeks, ears, (M2AvatarHat)hat, plate));
        }

        void WriteRacerProfile(ulong clientId, FixedString64Bytes displayName, M2AvatarAppearance appearance)
        {
            string normalizedName = M2PlayerProfile.NormalizeDisplayName(displayName.ToString());
            FixedString64Bytes normalizedFixedName = new FixedString64Bytes(normalizedName);
            M2AvatarAppearance normalizedAppearance = M2PlayerProfile.NormalizeAppearance(appearance);
            if (clientId == NetworkManager.ServerClientId)
            {
                netHostDisplayName.Value = normalizedFixedName;
                WriteAppearance(true, normalizedAppearance);
            }
            else
            {
                netClientDisplayName.Value = normalizedFixedName;
                WriteAppearance(false, normalizedAppearance);
            }
        }

        M2AvatarAppearance ReadAppearance(bool host)
        {
            return host
                ? new M2AvatarAppearance(netHostAvatarColorIndex.Value, (M2AvatarEyes)netHostAvatarEyes.Value,
                    (M2AvatarMouth)netHostAvatarMouth.Value, netHostAvatarCheeks.Value,
                    netHostAvatarEars.Value, (M2AvatarHat)netHostAvatarHat.Value, netHostAvatarPlate.Value)
                : new M2AvatarAppearance(netClientAvatarColorIndex.Value, (M2AvatarEyes)netClientAvatarEyes.Value,
                    (M2AvatarMouth)netClientAvatarMouth.Value, netClientAvatarCheeks.Value,
                    netClientAvatarEars.Value, (M2AvatarHat)netClientAvatarHat.Value, netClientAvatarPlate.Value);
        }

        void WriteAppearance(bool host, M2AvatarAppearance appearance)
        {
            if (host)
            {
                netHostAvatarColorIndex.Value = appearance.BodyColorIndex;
                netHostAvatarEyes.Value = (int)appearance.Eyes;
                netHostAvatarMouth.Value = (int)appearance.Mouth;
                netHostAvatarCheeks.Value = appearance.HasCheeks;
                netHostAvatarEars.Value = appearance.HasEars;
                netHostAvatarHat.Value = (int)appearance.Hat;
                netHostAvatarPlate.Value = appearance.PlateIndex;
            }
            else
            {
                netClientAvatarColorIndex.Value = appearance.BodyColorIndex;
                netClientAvatarEyes.Value = (int)appearance.Eyes;
                netClientAvatarMouth.Value = (int)appearance.Mouth;
                netClientAvatarCheeks.Value = appearance.HasCheeks;
                netClientAvatarEars.Value = appearance.HasEars;
                netClientAvatarHat.Value = (int)appearance.Hat;
                netClientAvatarPlate.Value = appearance.PlateIndex;
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

        void HandleStageChanged(int _, int __) => ApplySelectedStageTheme();

        void ApplySelectedStageTheme()
        {
            if (stageTheme == null)
            {
                GameObject root = GameObject.Find("NetworkStageTheme") ?? new GameObject("NetworkStageTheme");
                stageTheme = root.GetComponent<NetworkStageTheme>();
                if (stageTheme == null) stageTheme = root.AddComponent<NetworkStageTheme>();
            }
            stageTheme.Apply(NormalizeStage(SelectedStage));
        }

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
