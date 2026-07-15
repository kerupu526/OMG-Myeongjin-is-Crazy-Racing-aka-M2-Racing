using M2.Core;
using M2.Items;
using M2.Network;
using M2.Player;
using M2.Stage;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace M2.UI
{
    /// <summary>
    /// UI Toolkit implementation of Figma screens 03 (HUD) and 04 (result).
    /// NetworkRaceHUD retains the authoritative race-state binding as a hidden fallback;
    /// this view reads the same replicated state and supplies the visible presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class M2RaceHudToolkit : MonoBehaviour
    {
        const string ResourceRoot = "M2UI/";

        UIDocument document;
        GameObject documentGameObject;
        PanelSettings fallbackPanelSettings;
        VisualElement app;
        VisualElement countdownCard;
        VisualElement resultScreen;
        VisualElement localAvatar;
        VisualElement opponentAvatar;
        VisualElement firstAvatar;
        VisualElement secondAvatar;
        VisualElement secondResultCard;
        VisualElement gaugeFill;
        VisualElement mapTrack;
        VisualElement localMapDot;
        VisualElement opponentMapDot;
        Label lapValue;
        Label timeValue;
        Label localStatus;
        Label opponentStatus;
        Label itemCaption;
        Label countdownLabel;
        Label stageName;
        Label gaugeLabel;
        Label gaugeValue;
        Label gaugeAlert;
        Label resultTitle;
        Label resultSummary;
        Label resultFirstName;
        Label resultSecondName;
        Label resultFirstTime;
        Label resultSecondTime;
        Label resultFirstMessage;
        Label resultSecondMessage;
        Label resultMissedStars;
        Label resultTimeStars;
        Label resultAction;
        Image primaryIcon;
        Image secondaryIcon;
        Button rematchButton;
        Button lobbyButton;

        NetworkRaceManager raceManager;
        NetworkItemSlots localSlots;
        VehicleController localVehicle;
        VehicleController opponentVehicle;
        StageGaugeSystem stageGauge;
        bool waitingShown;
        StageType displayedStage = (StageType)(-1);
        Bounds miniMapWorldBounds;
        bool miniMapWorldBoundsReady;

        void Start()
        {
            if (!EnsureDocument()) return;
            CacheElements();
            ConfigureImages();
            ApplyLocalization();
            RegisterCallbacks();
            M2GameSettings.LanguageChanged += HandleLanguageChanged;
            GetComponent<NetworkRaceHUD>()?.SetLegacyPresentationSuppressed(true);
            ShowWaitingForOpponent();
        }

        void Update()
        {
            if (app == null) return;

            if (raceManager == null)
            {
                raceManager = FindFirstObjectByType<NetworkRaceManager>();
                if (raceManager == null)
                {
                    if (!waitingShown) ShowWaitingForOpponent();
                    return;
                }
            }

            waitingShown = false;
            RefreshPresentation();
        }

        void OnDestroy()
        {
            M2GameSettings.LanguageChanged -= HandleLanguageChanged;
            if (documentGameObject != null) Destroy(documentGameObject);
            if (fallbackPanelSettings != null) Destroy(fallbackPanelSettings);
        }

        void HandleLanguageChanged(M2Language _)
        {
            ApplyLocalization();
            if (raceManager != null) RefreshPresentation();
            else ShowWaitingForOpponent();
        }

        void ApplyLocalization()
        {
            M2Localization.ApplyTo(app);
        }

        bool EnsureDocument()
        {
            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>(ResourceRoot + "M2RaceHudView");
            StyleSheet style = Resources.Load<StyleSheet>(ResourceRoot + "M2RaceHud");
            if (tree == null || style == null)
            {
                Debug.LogError("[M2] Race HUD UXML/USS asset could not be loaded.");
                return false;
            }

            documentGameObject = new GameObject("M2RaceHudToolkitDocument");
            documentGameObject.SetActive(false);
            document = documentGameObject.AddComponent<UIDocument>();

            PanelSettings panelSettings = Resources.Load<PanelSettings>(ResourceRoot + "M2PanelSettings");
            if (panelSettings == null)
            {
                fallbackPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                fallbackPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                fallbackPanelSettings.referenceResolution = new Vector2Int(1280, 720);
                fallbackPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                fallbackPanelSettings.match = 0.5f;
                panelSettings = fallbackPanelSettings;
            }

            document.panelSettings = panelSettings;
            document.sortingOrder = 120f;
            document.visualTreeAsset = tree;
            documentGameObject.SetActive(true);

            app = document.rootVisualElement.Q<VisualElement>("m2-race-app");
            if (app == null) return false;
            app.styleSheets.Add(style);
            return true;
        }

        void CacheElements()
        {
            countdownCard = app.Q<VisualElement>("race-countdown-card");
            resultScreen = app.Q<VisualElement>("race-result-screen");
            localAvatar = app.Q<VisualElement>("race-local-avatar");
            opponentAvatar = app.Q<VisualElement>("race-opponent-avatar");
            firstAvatar = app.Q<VisualElement>("result-first-avatar");
            secondAvatar = app.Q<VisualElement>("result-second-avatar");
            secondResultCard = app.Q<VisualElement>(className: "result-card-wrap--second");
            gaugeFill = app.Q<VisualElement>("race-gauge-fill");
            mapTrack = app.Q<VisualElement>("race-map-track");
            localMapDot = app.Q<VisualElement>("race-map-dot-local");
            opponentMapDot = app.Q<VisualElement>("race-map-dot-opponent");
            lapValue = app.Q<Label>("race-lap-value");
            timeValue = app.Q<Label>("race-time-value");
            localStatus = app.Q<Label>("race-local-status");
            opponentStatus = app.Q<Label>("race-opponent-status");
            itemCaption = app.Q<Label>("race-item-caption");
            countdownLabel = app.Q<Label>("race-countdown-label");
            stageName = app.Q<Label>("race-stage-name");
            gaugeLabel = app.Q<Label>("race-gauge-label");
            gaugeValue = app.Q<Label>("race-gauge-value");
            gaugeAlert = app.Q<Label>("race-gauge-alert");
            resultTitle = app.Q<Label>("race-result-title");
            resultSummary = app.Q<Label>("race-result-summary");
            resultFirstName = app.Q<Label>("result-first-name");
            resultSecondName = app.Q<Label>("result-second-name");
            resultFirstTime = app.Q<Label>("result-first-time");
            resultSecondTime = app.Q<Label>("result-second-time");
            resultFirstMessage = app.Q<Label>("result-first-message");
            resultSecondMessage = app.Q<Label>("result-second-message");
            resultMissedStars = app.Q<Label>("result-missed-stars");
            resultTimeStars = app.Q<Label>("result-time-stars");
            resultAction = app.Q<Label>("race-result-action");
            primaryIcon = app.Q<Image>("race-slot-one-icon");
            secondaryIcon = app.Q<Image>("race-slot-two-icon");
            rematchButton = app.Q<Button>("race-rematch-button");
            lobbyButton = app.Q<Button>("race-lobby-button");
        }

        void ConfigureImages()
        {
            // Fades, stripes, and the result gradient are drawn procedurally; the SVG
            // VectorImage import renders CSS-style gradients unreliably (solid fills).
            SetTexture("race-hud-top-fade", M2UiGradients.Linear("hud-top-fade", 180f,
                new M2UiGradients.Stop(0f, new Color(0.102f, 0.063f, 0.188f, 0.8f)),
                new M2UiGradients.Stop(1f, new Color(0.102f, 0.063f, 0.188f, 0f))));
            SetVectorImage("race-stage-icon", ResourceRoot + "Icons/bikini-city");
            SetTexture("race-gauge-fill", M2UiGradients.Stripes("gauge-fill",
                new Color32(255, 47, 158, 255), new Color32(255, 217, 61, 255)));
            SetTexture("race-result-gradient", M2UiGradients.Linear("result", 160f,
                new M2UiGradients.Stop(0f, new Color32(255, 207, 61, 255)),
                new M2UiGradients.Stop(0.55f, new Color32(255, 107, 189, 255)),
                new M2UiGradients.Stop(1f, new Color32(154, 107, 255, 255))));
            SetVectorImage("result-first-medal", ResourceRoot + "Icons/first-place-medal");
            SetVectorImage("result-second-medal", ResourceRoot + "Icons/second-place-medal");
            SetVectorImage("result-star-icon", ResourceRoot + "Icons/star");
            SetVectorImage("race-rematch-icon", ResourceRoot + "Icons/restart");
            SetVectorImage("race-lobby-icon", ResourceRoot + "Icons/door");
            SetVectorImage("race-main-icon", ResourceRoot + "Icons/home");
            SetTextureImage("result-deco-one", ResourceRoot + "Icons/mj2");
            SetTextureImage("result-deco-two", ResourceRoot + "Icons/mj1");
            SetTextureImage("result-deco-three", ResourceRoot + "Icons/1496688619451318292");
            SetTextureImage("race-slot-one-icon", ResourceRoot + "Icons/bomb");
            SetTextureImage("race-slot-two-icon", ResourceRoot + "Icons/Gasoline");
        }

        void RegisterCallbacks()
        {
            if (rematchButton != null) rematchButton.clicked += () => raceManager?.RequestRematch();
            if (lobbyButton != null) lobbyButton.clicked += () => raceManager?.RequestReturnToLobby();
            Button mainButton = app.Q<Button>("race-main-button");
            if (mainButton != null) mainButton.clicked += ReturnToMain;
        }

        void ShowWaitingForOpponent()
        {
            waitingShown = true;
            SetDisplay(resultScreen, false);
            SetDisplay(countdownCard, false);
            SetLabel(lapValue, "– / –");
            SetLabel(timeValue, "--:--");
            SetLabel(localStatus, $"{M2PlayerProfile.TaggedDisplayName} · 준비 중");
            SetLabel(opponentStatus, "상대 · 연결 대기");
            SetLabel(itemCaption, "아이템 슬롯 · 연결 대기");
            SetAvatarColor(localAvatar, M2PlayerProfile.AvatarColor);
            SetAvatarColor(opponentAvatar, new Color32(95, 216, 245, 255));
            SetDisplay(localMapDot, false);
            SetDisplay(opponentMapDot, false);
            RefreshStageIdentity(StageType.BikiniCity);
            SetGaugeFallback(StageType.BikiniCity, waitingForConnection: true);
        }

        void RefreshPresentation()
        {
            bool localIsHost = LocalIsHost();
            int localLaps = localIsHost ? raceManager.HostLaps : raceManager.ClientLaps;
            int opponentLaps = localIsHost ? raceManager.ClientLaps : raceManager.HostLaps;
            int targetLaps = Mathf.Max(1, raceManager.TargetLapCount);

            NetworkRacerResult localRacer = localIsHost ? raceManager.HostRacer : raceManager.ClientRacer;
            NetworkRacerResult opponentRacer = localIsHost ? raceManager.ClientRacer : raceManager.HostRacer;
            string localName = DisplayNameOr(localRacer, M2PlayerProfile.TaggedDisplayName);
            string opponentName = DisplayNameOr(opponentRacer, "상대 레이서");
            Color localColor = ProfileColorOr(localRacer, M2PlayerProfile.AvatarColor);
            Color opponentColor = ProfileColorOr(opponentRacer, new Color32(95, 216, 245, 255));

            SetLabel(lapValue, $"{localLaps} / {targetLaps}");
            SetLabel(timeValue, FormatTime(raceManager.TimeRemaining));
            SetLabel(localStatus, $"{localName}\n{PlacementText(localRacer, localLaps, opponentLaps)}");
            SetLabel(opponentStatus, $"{opponentName}\n{OpponentStatus(localRacer, opponentRacer, localLaps, opponentLaps)}");
            SetAvatarColor(localAvatar, localColor);
            SetAvatarColor(opponentAvatar, opponentColor);

            RefreshItems();
            RefreshMiniMap(localColor, opponentColor);
            RefreshGauge();
            RefreshState(localRacer, opponentRacer, localName, opponentName, localLaps, opponentLaps, targetLaps);
        }

        void RefreshItems()
        {
            EnsureLocalRefs();
            NetItemId primary = localSlots != null ? localSlots.Primary : NetItemId.None;
            NetItemId secondary = localSlots != null ? localSlots.Secondary : NetItemId.None;
            bool hasPrimary = SetSlot(primary, primaryIcon);
            bool hasSecondary = SetSlot(secondary, secondaryIcon);

            if (raceManager.Mode == RaceMode.Speed)
            {
                string supply = hasPrimary || hasSecondary
                    ? "스피드전 · Ctrl로 지급된 휘발유 사용"
                    : $"스피드전 · {RaceModeRules.SpeedModeGasolineInterval:0.#}초마다 휘발유 자동 지급";
                SetLabel(itemCaption, supply);
                return;
            }

            SetLabel(itemCaption, hasPrimary || hasSecondary ? "아이템 슬롯 · 획득한 아이템을 사용하세요" : "아이템 슬롯 · 트랙의 픽업을 찾으세요");
        }

        bool SetSlot(NetItemId id, Image icon)
        {
            ItemDefinition definition = ItemCatalog.CreateFromId(id);
            if (definition == null)
            {
                SetItemIcon(icon, null);
                return false;
            }

            if (!ItemArt.TryGet(definition.id, out Sprite sprite, out _))
            {
                SetItemIcon(icon, null);
                return false;
            }

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.scaleMode = ScaleMode.ScaleToFit;
                icon.style.display = DisplayStyle.Flex;
            }
            return true;
        }

        void RefreshGauge()
        {
            StageType selectedStage = NormalizeStage(raceManager.SelectedStage);
            RefreshStageIdentity(selectedStage);
            EnsureLocalRefs();

            // The old global FindFirstObjectByType lookup could bind the Bikini City gauge left
            // in the scene even after the room selected Africa TV. Only use this player's gauge
            // when it belongs to the replicated stage choice.
            StageGaugeSystem localGauge = localVehicle != null ? localVehicle.GetComponent<StageGaugeSystem>() : null;
            stageGauge = GaugeMatchesStage(localGauge, selectedStage) ? localGauge : null;
            if (stageGauge == null)
            {
                SetGaugeFallback(selectedStage, waitingForConnection: false);
                return;
            }

            float fraction = stageGauge.maxValue <= 0f ? 0f : Mathf.Clamp01(stageGauge.CurrentValue / stageGauge.maxValue);
            string title = GaugeTitle(selectedStage);
            string suffix = GaugeSuffix(selectedStage);
            SetLabel(gaugeLabel, $"{title} {suffix}");
            SetLabel(gaugeValue, $"{Mathf.CeilToInt(stageGauge.CurrentValue)} / {Mathf.CeilToInt(stageGauge.maxValue)}");
            SetLabel(gaugeAlert, stageGauge.IsDepleted ? "⚠️ 게이지 위험! 스테이지 효과에 대응하세요" : GaugeAlert(selectedStage));
            if (gaugeFill != null)
            {
                gaugeFill.style.width = Length.Percent(fraction * 100f);
                Color gaugeColor = stageGauge.IsDepleted ? new Color32(255, 47, 158, 255) : GaugeColor(selectedStage);
                gaugeFill.style.backgroundColor = gaugeColor;
            }
        }

        void SetGaugeFallback(StageType stage, bool waitingForConnection)
        {
            bool startsAtMaximum = stage == StageType.BikiniCity;
            string suffix = waitingForConnection ? "— 상대 연결 대기" : GaugeSuffix(stage);
            SetLabel(gaugeLabel, $"{GaugeTitle(stage)} {suffix}");
            SetLabel(gaugeValue, startsAtMaximum ? "100 / 100" : "0 / 100");
            SetLabel(gaugeAlert, waitingForConnection ? "💨 상대와 연결되면 선택한 스테이지가 시작됩니다" : GaugeAlert(stage));
            if (gaugeFill != null)
            {
                gaugeFill.style.width = Length.Percent(startsAtMaximum ? 100f : 0f);
                Color gaugeColor = GaugeColor(stage);
                gaugeFill.style.backgroundColor = gaugeColor;
            }
        }

        void RefreshMiniMap(Color localColor, Color opponentColor)
        {
            EnsureLocalRefs();
            ResolveOpponentVehicle();

            if (localMapDot != null) localMapDot.style.backgroundColor = localColor;
            if (opponentMapDot != null) opponentMapDot.style.backgroundColor = opponentColor;

            bool hasTrackBounds = TryCacheMiniMapWorldBounds();
            bool showLocal = hasTrackBounds && localVehicle != null;
            bool showOpponent = hasTrackBounds && !raceManager.IsSoloLocalRace && opponentVehicle != null;
            SetDisplay(localMapDot, showLocal);
            SetDisplay(opponentMapDot, showOpponent);

            if (showLocal) PositionMiniMapDot(localMapDot, localVehicle.transform.position);
            if (showOpponent) PositionMiniMapDot(opponentMapDot, opponentVehicle.transform.position);
        }

        void ResolveOpponentVehicle()
        {
            opponentVehicle = null;
            foreach (NetworkObject networkObject in FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (!networkObject.IsPlayerObject || networkObject.IsOwner) continue;
                VehicleController vehicle = networkObject.GetComponent<VehicleController>();
                if (vehicle != null)
                {
                    opponentVehicle = vehicle;
                    return;
                }
            }
        }

        bool TryCacheMiniMapWorldBounds()
        {
            if (miniMapWorldBoundsReady) return true;

            Checkpoint[] checkpoints = FindObjectsByType<Checkpoint>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (checkpoints.Length < 2) return false;

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;
            foreach (Checkpoint checkpoint in checkpoints)
            {
                Vector3 position = checkpoint.transform.position;
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxZ = Mathf.Max(maxZ, position.z);
            }

            if (maxX - minX < 0.01f || maxZ - minZ < 0.01f) return false;
            miniMapWorldBounds = new Bounds(new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0f, maxZ - minZ));
            miniMapWorldBoundsReady = true;
            return true;
        }

        void PositionMiniMapDot(VisualElement dot, Vector3 worldPosition)
        {
            if (mapTrack == null || dot == null) return;

            float trackWidth = mapTrack.resolvedStyle.width;
            float trackHeight = mapTrack.resolvedStyle.height;
            if (trackWidth <= 0f || trackHeight <= 0f) return;

            float markerWidth = dot.resolvedStyle.width > 0f ? dot.resolvedStyle.width : 11f;
            float markerHeight = dot.resolvedStyle.height > 0f ? dot.resolvedStyle.height : 11f;
            Vector2 position = ProjectMiniMapPosition(worldPosition, miniMapWorldBounds,
                new Vector2(trackWidth, trackHeight), new Vector2(markerWidth, markerHeight));
            dot.style.left = position.x;
            dot.style.top = position.y;
        }

        /// <summary>Projects a world-space track position into the mini-map's drawable area.</summary>
        public static Vector2 ProjectMiniMapPosition(Vector3 worldPosition, Bounds worldBounds,
            Vector2 trackSize, Vector2 markerSize)
        {
            const float inset = 4f;
            float normalizedX = worldBounds.size.x > 0.01f
                ? Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPosition.x)
                : 0.5f;
            float normalizedY = worldBounds.size.z > 0.01f
                ? 1f - Mathf.InverseLerp(worldBounds.min.z, worldBounds.max.z, worldPosition.z)
                : 0.5f;
            float availableWidth = Mathf.Max(0f, trackSize.x - markerSize.x - inset * 2f);
            float availableHeight = Mathf.Max(0f, trackSize.y - markerSize.y - inset * 2f);
            return new Vector2(inset + Mathf.Clamp01(normalizedX) * availableWidth,
                inset + Mathf.Clamp01(normalizedY) * availableHeight);
        }

        void RefreshState(NetworkRacerResult localRacer, NetworkRacerResult opponentRacer, string localName,
            string opponentName, int localLaps, int opponentLaps, int targetLaps)
        {
            if (raceManager.Result != 0)
            {
                SetDisplay(countdownCard, false);
                SetDisplay(resultScreen, true);
                RefreshResult(localRacer, opponentRacer, localName, opponentName, localLaps, opponentLaps, targetLaps);
                return;
            }

            SetDisplay(resultScreen, false);
            if (raceManager.State == RaceState.Briefing)
            {
                SetDisplay(countdownCard, true);
                SetLabel(countdownLabel, "레이스 준비\n←/→ 조향 · ↑/↓ 가속\nCtrl 아이템 · Shift 드리프트");
            }
            else if (raceManager.State == RaceState.Countdown ||
                (raceManager.State == RaceState.Racing && raceManager.Countdown == 0))
            {
                SetDisplay(countdownCard, true);
                int countdown = raceManager.Countdown;
                SetLabel(countdownLabel, countdown > 0 ? countdown.ToString() : "GO!");
            }
            else
            {
                SetDisplay(countdownCard, false);
            }
        }

        void RefreshResult(NetworkRacerResult localRacer, NetworkRacerResult opponentRacer, string localName,
            string opponentName, int localLaps, int opponentLaps, int targetLaps)
        {
            bool draw = raceManager.Result == 2;
            bool solo = raceManager.IsSoloLocalRace;
            SetDisplay(secondResultCard, !solo);
            if (solo)
            {
                SetLabel(resultTitle, draw ? "TIME UP!" : "RACE CLEAR!");
                SetLocalizedLabel(resultSummary, draw
                    ? "시간이 종료되었습니다. 다음 기록에 도전해 보세요."
                    : "완주했습니다! 나의 기록을 확인해 보세요.");
                SetLabel(resultFirstName, localName);
                SetResultTime(resultFirstTime, localRacer, localLaps, targetLaps);
                SetLocalizedLabel(resultFirstMessage, localRacer.Finished ? "나의 완주 기록 ✨" : "기록 집계 완료");
                M2AvatarVisual.Apply(firstAvatar, localRacer.Appearance);
                SetLabel(resultMissedStars, StarString(Mathf.Clamp(localRacer.Stars, 0, 6)));
                SetLabel(resultTimeStars, "-");
                RefreshResultActions();
                return;
            }

            bool localWon = !draw && IsLocalWinner();
            SetLabel(resultTitle, "GAME OVER!");
            SetLocalizedLabel(resultSummary, draw ? "무승부 · 두 레이서의 기록이 동점으로 처리되었습니다." :
                localWon ? "승리! 가장 먼저 결승 조건을 달성했습니다." : "패배 · 상대 레이서가 먼저 결승 조건을 달성했습니다.");

            bool localFirst = !draw && (localRacer.Rank == 1 || (localRacer.Rank == 0 && localWon));
            NetworkRacerResult first = localFirst ? localRacer : opponentRacer;
            NetworkRacerResult second = localFirst ? opponentRacer : localRacer;
            string firstName = localFirst ? localName : opponentName;
            string secondName = localFirst ? opponentName : localName;
            int firstLaps = localFirst ? localLaps : opponentLaps;
            int secondLaps = localFirst ? opponentLaps : localLaps;

            SetLabel(resultFirstName, firstName);
            SetLabel(resultSecondName, secondName);
            SetResultTime(resultFirstTime, first, firstLaps, targetLaps);
            SetResultTime(resultSecondTime, second, secondLaps, targetLaps);
            SetLocalizedLabel(resultFirstMessage, draw ? "치열한 승부였습니다! ✨" : "오늘의 챔피언 👑");
            SetLocalizedLabel(resultSecondMessage, draw ? "다음 라운드도 기대해요!" : "다음엔 꼭 이깁니다 ㅠㅠ");
            M2AvatarVisual.Apply(firstAvatar, first.Appearance);
            M2AvatarVisual.Apply(secondAvatar, second.Appearance);

            int firstStars = Mathf.Clamp(first.Stars, 0, 6);
            int secondStars = Mathf.Clamp(second.Stars, 0, 6);
            SetLabel(resultMissedStars, StarString(firstStars));
            SetLabel(resultTimeStars, StarString(secondStars));
            RefreshResultActions();
        }

        void RefreshResultActions()
        {
            bool localIsHost = LocalIsHost();
            int localChoice = localIsHost ? raceManager.HostPostRaceChoice : raceManager.ClientPostRaceChoice;
            int opponentChoice = localIsHost ? raceManager.ClientPostRaceChoice : raceManager.HostPostRaceChoice;
            bool awaiting = localChoice != 0;
            bool solo = raceManager.IsSoloLocalRace;
            if (rematchButton != null) rematchButton.SetEnabled(!awaiting);
            SetDisplay(lobbyButton, !solo);
            if (lobbyButton != null) lobbyButton.SetEnabled(!awaiting && !solo);
            SetLocalizedButtonLabel(rematchButton, localChoice == 1 ? "다시 하기 · 대기" : "다시 하기");
            SetLocalizedButtonLabel(lobbyButton, localChoice == 2 ? "로비로 · 대기" : "로비로");
            if (solo)
            {
                SetLocalizedLabel(resultAction, localChoice == 0
                    ? "다시 하기를 누르면 바로 새 레이스를 시작합니다."
                    : "새 레이스를 준비합니다...");
                return;
            }

            SetLocalizedLabel(resultAction, localChoice == 0
                ? "다시 하기 또는 로비로는 상대 레이서의 같은 선택을 기다립니다."
                : opponentChoice == 0
                    ? "내 선택을 보냈습니다. 상대 레이서의 응답을 기다리는 중입니다."
                    : opponentChoice == localChoice
                        ? "두 레이서의 선택을 확인했습니다. 화면을 전환하는 중입니다."
                        : "상대 레이서가 다른 선택을 했습니다. 메인으로 나가거나 다시 선택하세요.");
        }

        void EnsureLocalRefs()
        {
            if (localSlots != null && localVehicle != null) return;
            NetworkManager manager = NetworkManager.Singleton;
            NetworkObject playerObject = manager != null && manager.LocalClient != null
                ? manager.LocalClient.PlayerObject
                : null;
            if (playerObject == null) return;
            localSlots = playerObject.GetComponent<NetworkItemSlots>();
            localVehicle = playerObject.GetComponent<VehicleController>();
        }

        bool LocalIsHost()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.LocalClientId == NetworkManager.ServerClientId;
        }

        bool IsLocalWinner()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && raceManager.WinnerClientId == manager.LocalClientId;
        }

        void ReturnToMain()
        {
            NetworkBootstrapUI bootstrap = FindFirstObjectByType<NetworkBootstrapUI>();
            if (bootstrap != null) bootstrap.ExitSessionToMain();
        }

        void SetVectorImage(string name, string path)
        {
            Image image = app.Q<Image>(name);
            if (image == null) return;
            image.image = null;
            image.vectorImage = Resources.Load<VectorImage>(path);
            image.scaleMode = ScaleMode.ScaleToFit;
        }

        void SetTexture(string name, Texture2D texture)
        {
            Image image = app.Q<Image>(name);
            if (image == null) return;
            image.vectorImage = null;
            image.image = texture;
            image.scaleMode = ScaleMode.StretchToFill;
        }

        void SetTextureImage(string name, string path)
        {
            Image image = app.Q<Image>(name);
            if (image == null) return;
            image.vectorImage = null;
            image.image = Resources.Load<Texture2D>(path);
            image.scaleMode = ScaleMode.ScaleToFit;
        }

        static void SetItemIcon(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.image = null;
            image.vectorImage = null;
            image.sprite = sprite;
            image.style.display = sprite == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static void SetAvatarColor(VisualElement avatar, Color color)
        {
            if (avatar != null) avatar.style.backgroundColor = color;
        }

        static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetLabel(Label label, string value)
        {
            if (label != null) label.text = value;
        }

        static void SetLocalizedLabel(Label label, string value)
        {
            SetLabel(label, M2Localization.Translate(value));
        }

        static void SetButtonLabel(Button button, string value)
        {
            if (button == null) return;
            Label label = button.Q<Label>("button-label");
            if (label != null) label.text = value;
        }

        static void SetLocalizedButtonLabel(Button button, string value)
        {
            SetButtonLabel(button, M2Localization.Translate(value));
        }

        static string DisplayNameOr(NetworkRacerResult racer, string fallback) => racer.HasProfile ? racer.DisplayName : fallback;

        static Color ProfileColorOr(NetworkRacerResult racer, Color fallback) =>
            racer.HasProfile ? M2PlayerProfile.ResolveAvatarColor(racer.AvatarColorIndex) : fallback;

        static string ResultTime(NetworkRacerResult racer, int laps, int targetLaps) =>
            racer.Finished ? FormatTime(racer.FinishTime) : $"{M2Localization.Translate("미완주")} {laps}/{targetLaps}";

        static void SetResultTime(Label label, NetworkRacerResult racer, int laps, int targetLaps)
        {
            if (label == null) return;
            label.text = ResultTime(racer, laps, targetLaps);
            label.EnableInClassList("result-time-value--unfinished", !racer.Finished);
        }

        void RefreshStageIdentity(StageType stage)
        {
            if (displayedStage == stage) return;
            displayedStage = stage;
            SetLabel(stageName, StageName(stage));
            SetVectorImage("race-stage-icon", stage switch
            {
                StageType.AfricaTv => ResourceRoot + "Icons/afreecatv",
                StageType.NetherFortress => ResourceRoot + "Icons/nether-fortress",
                _ => ResourceRoot + "Icons/bikini-city",
            });
        }

        static bool GaugeMatchesStage(StageGaugeSystem gauge, StageType stage) => stage switch
        {
            StageType.AfricaTv => gauge is AfricaTvMentalGauge,
            StageType.NetherFortress => gauge is NetherFortressTemperatureGauge,
            _ => gauge is BikiniCityOxygenGauge,
        };

        static StageType NormalizeStage(StageType stage) => stage switch
        {
            StageType.AfricaTv => StageType.AfricaTv,
            StageType.NetherFortress => StageType.NetherFortress,
            _ => StageType.BikiniCity,
        };

        static string StageName(StageType stage) => stage switch
        {
            StageType.AfricaTv => "아프리카TV",
            StageType.NetherFortress => "네더요새",
            _ => "비키니시티",
        };

        static string GaugeTitle(StageType stage) => stage switch
        {
            StageType.AfricaTv => "💥 멘탈 게이지",
            StageType.NetherFortress => "🔥 체온 게이지",
            _ => "💧 산소 게이지",
        };

        static string GaugeSuffix(StageType stage) => stage switch
        {
            StageType.AfricaTv => "— 가득 차면 조작 불가",
            StageType.NetherFortress => "— 가득 차면 화상 위험",
            _ => "— 실시간 감소 중...",
        };

        static string GaugeAlert(StageType stage) => stage switch
        {
            StageType.AfricaTv => "📺 방송사고와 공격을 조심하세요",
            StageType.NetherFortress => "🔥 용암 지대에서는 체온이 빠르게 올라갑니다",
            _ => "💨 숨방울을 찾아 산소를 유지하세요",
        };

        static Color GaugeColor(StageType stage) => stage switch
        {
            StageType.AfricaTv => new Color32(255, 217, 61, 255),
            StageType.NetherFortress => new Color32(255, 107, 61, 255),
            _ => new Color32(95, 216, 245, 255),
        };

        static string PlacementText(NetworkRacerResult racer, int localLaps, int opponentLaps)
        {
            if (racer.Rank > 0) return $"{racer.Rank}위";
            return localLaps >= opponentLaps ? "선두" : "추격 중";
        }

        static string OpponentStatus(NetworkRacerResult localRacer, NetworkRacerResult opponentRacer, int localLaps, int opponentLaps)
        {
            if (opponentRacer.Rank > 0) return $"{opponentRacer.Rank}위";
            int difference = opponentLaps - localLaps;
            return difference == 0 ? "동점" : difference > 0 ? $"+{difference}바퀴" : $"{difference}바퀴";
        }

        static string StarString(int filled)
        {
            filled = Mathf.Clamp(filled, 0, 6);
            return new string('★', filled) + new string('☆', 6 - filled);
        }

        static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.00}";
        }
    }
}
