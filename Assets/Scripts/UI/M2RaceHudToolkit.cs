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
        VisualElement gaugeFill;
        Label lapValue;
        Label timeValue;
        Label localStatus;
        Label opponentStatus;
        Label itemCaption;
        Label boosterLabel;
        Label countdownLabel;
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
        StageGaugeSystem stageGauge;
        bool waitingShown;

        void Start()
        {
            if (!EnsureDocument()) return;
            CacheElements();
            ConfigureImages();
            RegisterCallbacks();
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
            if (documentGameObject != null) Destroy(documentGameObject);
            if (fallbackPanelSettings != null) Destroy(fallbackPanelSettings);
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
            gaugeFill = app.Q<VisualElement>("race-gauge-fill");
            lapValue = app.Q<Label>("race-lap-value");
            timeValue = app.Q<Label>("race-time-value");
            localStatus = app.Q<Label>("race-local-status");
            opponentStatus = app.Q<Label>("race-opponent-status");
            itemCaption = app.Q<Label>("race-item-caption");
            boosterLabel = app.Q<Label>("race-booster-label");
            countdownLabel = app.Q<Label>("race-countdown-label");
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
            SetVectorImage("race-hud-top-fade", ResourceRoot + "Backgrounds/HudTopFade");
            SetVectorImage("race-stage-icon", ResourceRoot + "Icons/bikini-city");
            SetVectorImageStretched("race-gauge-fill", ResourceRoot + "Backgrounds/GaugeFill");
            SetVectorImage("race-result-gradient", ResourceRoot + "Backgrounds/ResultGradient");
            SetVectorImage("result-first-medal", ResourceRoot + "Icons/first-place-medal");
            SetVectorImage("result-second-medal", ResourceRoot + "Icons/second-place-medal");
            SetVectorImage("result-first-crown", ResourceRoot + "Icons/crown");
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
            SetLabel(localStatus, $"{M2PlayerProfile.DisplayName} · 준비 중");
            SetLabel(opponentStatus, "상대 · 연결 대기");
            SetLabel(itemCaption, "아이템 슬롯 · 연결 대기");
            SetLabel(boosterLabel, "상대와 연결 중... ⏳");
            SetAvatarColor(localAvatar, M2PlayerProfile.AvatarColor);
            SetAvatarColor(opponentAvatar, new Color32(95, 216, 245, 255));
            SetGaugeFallback();
        }

        void RefreshPresentation()
        {
            bool localIsHost = LocalIsHost();
            int localLaps = localIsHost ? raceManager.HostLaps : raceManager.ClientLaps;
            int opponentLaps = localIsHost ? raceManager.ClientLaps : raceManager.HostLaps;
            int targetLaps = Mathf.Max(1, raceManager.TargetLapCount);

            NetworkRacerResult localRacer = localIsHost ? raceManager.HostRacer : raceManager.ClientRacer;
            NetworkRacerResult opponentRacer = localIsHost ? raceManager.ClientRacer : raceManager.HostRacer;
            string localName = DisplayNameOr(localRacer, M2PlayerProfile.DisplayName);
            string opponentName = DisplayNameOr(opponentRacer, "상대 레이서");
            Color localColor = ProfileColorOr(localRacer, M2PlayerProfile.AvatarColor);
            Color opponentColor = ProfileColorOr(opponentRacer, new Color32(95, 216, 245, 255));

            SetLabel(lapValue, $"{localLaps} / {targetLaps}");
            SetLabel(timeValue, FormatTime(raceManager.TimeRemaining));
            SetLabel(localStatus, $"{localName} · {PlacementText(localRacer, localLaps, opponentLaps)}");
            SetLabel(opponentStatus, $"{opponentName} · {OpponentStatus(localRacer, opponentRacer, localLaps, opponentLaps)}");
            SetAvatarColor(localAvatar, localColor);
            SetAvatarColor(opponentAvatar, opponentColor);

            RefreshItems();
            RefreshGauge();
            RefreshState(localRacer, opponentRacer, localName, opponentName, localColor, opponentColor,
                localLaps, opponentLaps, targetLaps);
        }

        void RefreshItems()
        {
            if (raceManager.Mode == RaceMode.Speed)
            {
                SetItemIcon(primaryIcon, null);
                SetItemIcon(secondaryIcon, null);
                SetLabel(itemCaption, "스피드전 · 기본 휘발유 자동 분사");
                SetLabel(boosterLabel, "부스터 발동! 🚀");
                return;
            }

            EnsureLocalRefs();
            NetItemId primary = localSlots != null ? localSlots.Primary : default;
            NetItemId secondary = localSlots != null ? localSlots.Secondary : default;
            bool hasPrimary = SetSlot(primary, primaryIcon);
            bool hasSecondary = SetSlot(secondary, secondaryIcon);
            SetLabel(itemCaption, hasPrimary || hasSecondary ? "아이템 슬롯 · 획득한 아이템을 사용하세요" : "아이템 슬롯 · 트랙의 픽업을 찾으세요");
            SetLabel(boosterLabel, localVehicle != null && localVehicle.HasSpeedBoost ? "부스터 발동! 🚀" : "아이템을 모아 역전하세요! ✨");
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
            if (stageGauge == null) stageGauge = FindFirstObjectByType<StageGaugeSystem>();
            if (stageGauge == null)
            {
                SetGaugeFallback();
                return;
            }

            float fraction = stageGauge.maxValue <= 0f ? 0f : Mathf.Clamp01(stageGauge.CurrentValue / stageGauge.maxValue);
            string title = stageGauge is BikiniCityOxygenGauge ? "💧 산소 게이지" :
                stageGauge is AfricaTvMentalGauge ? "💥 멘탈 게이지" : "🔥 체온 게이지";
            string suffix = stageGauge is BikiniCityOxygenGauge ? "— 실시간 감소 중..." :
                stageGauge.dangerAtMax ? "— 가득 차면 조작 불가" : "— 위험 구간 주의";
            SetLabel(gaugeLabel, $"{title} {suffix}");
            SetLabel(gaugeValue, $"{Mathf.CeilToInt(stageGauge.CurrentValue)} / {Mathf.CeilToInt(stageGauge.maxValue)}");
            SetLabel(gaugeAlert, stageGauge.IsDepleted ? "⚠️ 게이지 위험! 아이템으로 회복하세요" : "💨 게이지 상태를 유지하세요!");
            if (gaugeFill != null)
            {
                gaugeFill.style.width = Length.Percent(fraction * 100f);
                Color gaugeColor = stageGauge.IsDepleted ? new Color32(255, 47, 158, 255) :
                    stageGauge is BikiniCityOxygenGauge ? new Color32(95, 216, 245, 255) : new Color32(255, 217, 61, 255);
                gaugeFill.style.backgroundColor = gaugeColor;
            }
        }

        void SetGaugeFallback()
        {
            SetLabel(gaugeLabel, "💧 산소 게이지 — 레이스 시작을 기다리는 중...");
            SetLabel(gaugeValue, "100 / 100");
            SetLabel(gaugeAlert, "💨 레이스가 시작되면 게이지가 표시됩니다");
            if (gaugeFill != null)
            {
                gaugeFill.style.width = Length.Percent(100f);
                Color gaugeColor = new Color32(95, 216, 245, 255);
                gaugeFill.style.backgroundColor = gaugeColor;
            }
        }

        void RefreshState(NetworkRacerResult localRacer, NetworkRacerResult opponentRacer, string localName,
            string opponentName, Color localColor, Color opponentColor, int localLaps, int opponentLaps, int targetLaps)
        {
            if (raceManager.Result != 0)
            {
                SetDisplay(countdownCard, false);
                SetDisplay(resultScreen, true);
                RefreshResult(localRacer, opponentRacer, localName, opponentName, localColor, opponentColor,
                    localLaps, opponentLaps, targetLaps);
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
            string opponentName, Color localColor, Color opponentColor, int localLaps, int opponentLaps, int targetLaps)
        {
            bool draw = raceManager.Result == 2;
            bool localWon = !draw && IsLocalWinner();
            SetLabel(resultTitle, "GAME OVER!");
            SetLabel(resultSummary, draw ? "무승부 · 두 레이서의 기록이 동점으로 처리되었습니다." :
                localWon ? "승리! 가장 먼저 결승 조건을 달성했습니다." : "패배 · 상대 레이서가 먼저 결승 조건을 달성했습니다.");

            bool localFirst = !draw && (localRacer.Rank == 1 || (localRacer.Rank == 0 && localWon));
            NetworkRacerResult first = localFirst ? localRacer : opponentRacer;
            NetworkRacerResult second = localFirst ? opponentRacer : localRacer;
            string firstName = localFirst ? localName : opponentName;
            string secondName = localFirst ? opponentName : localName;
            Color firstColor = localFirst ? localColor : opponentColor;
            Color secondColor = localFirst ? opponentColor : localColor;
            int firstLaps = localFirst ? localLaps : opponentLaps;
            int secondLaps = localFirst ? opponentLaps : localLaps;

            SetLabel(resultFirstName, firstName);
            SetLabel(resultSecondName, secondName);
            SetLabel(resultFirstTime, ResultTime(first, firstLaps, targetLaps));
            SetLabel(resultSecondTime, ResultTime(second, secondLaps, targetLaps));
            SetLabel(resultFirstMessage, draw ? "치열한 승부였습니다! ✨" : "오늘의 챔피언 👑");
            SetLabel(resultSecondMessage, draw ? "다음 라운드도 기대해요!" : "다음엔 꼭 이깁니다 ㅠㅠ");
            SetAvatarColor(firstAvatar, firstColor);
            SetAvatarColor(secondAvatar, secondColor);

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
            if (rematchButton != null) rematchButton.SetEnabled(!awaiting);
            if (lobbyButton != null) lobbyButton.SetEnabled(!awaiting);
            SetButtonLabel(rematchButton, localChoice == 1 ? "다시 하기 · 대기" : "다시 하기");
            SetButtonLabel(lobbyButton, localChoice == 2 ? "로비로 · 대기" : "로비로");
            SetLabel(resultAction, localChoice == 0
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

        void SetVectorImageStretched(string name, string path)
        {
            Image image = app.Q<Image>(name);
            if (image == null) return;
            image.image = null;
            image.vectorImage = Resources.Load<VectorImage>(path);
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

        static void SetButtonLabel(Button button, string value)
        {
            if (button == null) return;
            Label label = button.Q<Label>("button-label");
            if (label != null) label.text = value;
        }

        static string DisplayNameOr(NetworkRacerResult racer, string fallback) => racer.HasProfile ? racer.DisplayName : fallback;

        static Color ProfileColorOr(NetworkRacerResult racer, Color fallback) =>
            racer.HasProfile ? M2PlayerProfile.ResolveAvatarColor(racer.AvatarColorIndex) : fallback;

        static string ResultTime(NetworkRacerResult racer, int laps, int targetLaps) =>
            racer.Finished ? FormatTime(racer.FinishTime) : $"미완주 {laps}/{targetLaps}";

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
