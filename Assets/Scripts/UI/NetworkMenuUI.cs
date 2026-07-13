using M2.Core;
using M2.Network;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace M2.UI
{
    /// <summary>
    /// Presentation shell for the Relay entry flow. It keeps the existing NetworkBootstrapUI
    /// controls and service calls intact, but presents them as the supplied design's main,
    /// room-setup, join, and waiting-lobby screens.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkMenuUI : MonoBehaviour
    {
        enum Screen
        {
            None,
            Main,
            HostSetup,
            JoinSetup,
            Lobby,
            Avatar,
            Settings,
            Hidden,
        }

        static readonly Color Ink = new Color(0.102f, 0.063f, 0.188f);
        static readonly Color DeepPurple = new Color(0.173f, 0.094f, 0.322f);
        static readonly Color Purple = new Color(0.353f, 0.188f, 0.620f);
        static readonly Color Pink = new Color(1f, 0.184f, 0.620f);
        static readonly Color SoftPink = new Color(1f, 0.616f, 0.878f);
        static readonly Color Yellow = new Color(1f, 0.851f, 0.239f);
        static readonly Color Mint = new Color(0.714f, 0.953f, 0.420f);
        static readonly Color Cyan = new Color(0.373f, 0.847f, 0.961f);
        NetworkBootstrapUI bootstrap;
        RoomSettingsUI roomSettingsUi;
        bool subscribed;
        bool built;
        Screen currentScreen;

        GameObject root;
        GameObject mainScreen;
        GameObject hostSetupScreen;
        GameObject joinScreen;
        GameObject lobbyScreen;
        GameObject avatarScreen;
        GameObject settingsScreen;
        Transform hostSettingsSlot;
        Transform joinCard;

        Text mainFeedback;
        Text hostFeedback;
        Text joinFeedback;
        Text lobbyCode;
        Text lobbyRole;
        Text lobbyGuest;
        Text lobbyRules;
        Text lobbyStatus;
        Image mainAvatarImage;
        Text mainAvatarInitials;
        Text mainAvatarName;
        Image lobbyHostAvatarImage;
        Text lobbyHostInitials;
        Text lobbyHostName;
        Image avatarPreviewImage;
        Text avatarPreviewInitials;
        Text avatarPreviewName;
        InputField avatarNameInput;
        Text avatarFeedback;
        int draftAvatarColorIndex;
        Slider settingsVolumeSlider;
        Text settingsVolumeValue;
        Toggle settingsFullscreenToggle;
        Text settingsFeedback;

        /// <summary>Small inspection hook used by presentation tests and UI diagnostics.</summary>
        public string CurrentScreenName => currentScreen.ToString();

        void Awake()
        {
            ConfigureCanvasScaler();
            M2GameSettings.ApplyRuntime();
        }

        void Start()
        {
            EnsureBuilt();
            if (currentScreen == Screen.None) ShowMain();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Binds the existing service-facing controls to this visual shell. It is safe to call
        /// again after an additive scene setup has finished creating the canvas.
        /// </summary>
        public void Initialize(NetworkBootstrapUI source, RoomSettingsUI settings)
        {
            if (bootstrap != source)
            {
                Unsubscribe();
                bootstrap = source;
                Subscribe();
            }

            roomSettingsUi = settings != null ? settings : roomSettingsUi;
            EnsureBuilt();
            ConfigureRoomSettingsPresentation();
            ArrangeLegacyControls();

            if (bootstrap != null && bootstrap.HasActiveSession)
            {
                ShowLobby(bootstrap.ActiveRoomCode, bootstrap.IsHostingSession);
            }
            else if (currentScreen == Screen.None)
            {
                ShowMain();
            }
        }

        public void ShowMain()
        {
            EnsureBuilt();
            SetScreen(Screen.Main);
            SetLegacyControlVisibility(false, false, false);
            roomSettingsUi?.SetVisible(false);
            SetFeedback(mainFeedback, "방 만들기 또는 방 참가를 선택하세요.", Color.white);
        }

        public void ShowHostSetup()
        {
            EnsureBuilt();
            ArrangeLegacyControls();
            SetScreen(Screen.HostSetup);
            SetLegacyControlVisibility(true, false, false);
            roomSettingsUi?.SetVisible(true);
            SetFeedback(hostFeedback, "모드와 규칙을 정한 뒤 방을 만드세요.", Color.white);
        }

        public void ShowJoinSetup()
        {
            EnsureBuilt();
            ArrangeLegacyControls();
            SetScreen(Screen.JoinSetup);
            SetLegacyControlVisibility(false, true, true);
            roomSettingsUi?.SetVisible(false);
            SetFeedback(joinFeedback, "친구에게 받은 방 코드를 입력하세요.", Color.white);
        }

        public void ShowAvatar()
        {
            EnsureBuilt();
            SetScreen(Screen.Avatar);
            SetLegacyControlVisibility(false, false, false);
            roomSettingsUi?.SetVisible(false);

            draftAvatarColorIndex = M2PlayerProfile.AvatarColorIndex;
            if (avatarNameInput != null) avatarNameInput.text = M2PlayerProfile.DisplayName;
            RefreshAvatarPreview();
            SetFeedback(avatarFeedback, "색과 이름을 고른 뒤 저장하세요.", Ink);
        }

        public void ShowSettings()
        {
            EnsureBuilt();
            SetScreen(Screen.Settings);
            SetLegacyControlVisibility(false, false, false);
            roomSettingsUi?.SetVisible(false);

            if (settingsVolumeSlider != null)
            {
                settingsVolumeSlider.SetValueWithoutNotify(M2GameSettings.MasterVolume);
            }
            if (settingsFullscreenToggle != null)
            {
                settingsFullscreenToggle.SetIsOnWithoutNotify(M2GameSettings.Fullscreen);
            }
            RefreshSettingsPresentation();
            SetFeedback(settingsFeedback, "변경한 뒤 저장하면 다음 실행에도 유지됩니다.", Ink);
        }

        public void ShowLobby(string roomCode, bool isHost)
        {
            EnsureBuilt();
            SetScreen(Screen.Lobby);
            SetLegacyControlVisibility(false, false, false);
            roomSettingsUi?.SetVisible(false);

            lobbyCode.text = string.IsNullOrWhiteSpace(roomCode) ? "연결 중" : roomCode;
            lobbyRole.text = isHost ? "HOST · 방장" : "GUEST · 참가자";
            lobbyGuest.text = isHost ? "상대 접속 대기" : "호스트와 연결 중";
            lobbyStatus.text = isHost
                ? "친구에게 위 방 코드를 알려주세요."
                : "방에 입장했습니다. 연결 상태를 확인하는 중입니다.";
            RefreshLobbyRules();
            RefreshProfilePresentation();
        }

        /// <summary>Called by NetworkBootstrapUI when both players have connected.</summary>
        public void HideMenu()
        {
            EnsureBuilt();
            SetScreen(Screen.Hidden);
            SetLegacyControlVisibility(false, false, false);
            roomSettingsUi?.SetVisible(false);
        }

        void Subscribe()
        {
            if (bootstrap == null || subscribed) return;
            bootstrap.StatusChanged += HandleStatusChanged;
            bootstrap.SessionReady += HandleSessionReady;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (bootstrap == null || !subscribed) return;
            bootstrap.StatusChanged -= HandleStatusChanged;
            bootstrap.SessionReady -= HandleSessionReady;
            subscribed = false;
        }

        void HandleStatusChanged(string message)
        {
            switch (currentScreen)
            {
                case Screen.HostSetup:
                    SetFeedback(hostFeedback, message, IsFailure(message) ? SoftPink : Color.white);
                    break;
                case Screen.JoinSetup:
                    SetFeedback(joinFeedback, message, IsFailure(message) ? SoftPink : Color.white);
                    break;
                case Screen.Lobby:
                    SetFeedback(lobbyStatus, message, IsFailure(message) ? SoftPink : Color.white);
                    break;
                case Screen.Main:
                    SetFeedback(mainFeedback, message, IsFailure(message) ? SoftPink : Color.white);
                    break;
            }
        }

        void HandleSessionReady(string roomCode, bool isHost)
        {
            ShowLobby(roomCode, isHost);
        }

        static bool IsFailure(string message)
        {
            return !string.IsNullOrEmpty(message) &&
                (message.Contains("실패") || message.Contains("끊어") || message.Contains("입력"));
        }

        void EnsureBuilt()
        {
            if (built) return;
            built = true;

            root = CreateFullscreenPanel(transform, "NetworkMenuRoot", Color.clear);
            root.GetComponent<Image>().raycastTarget = false;

            mainScreen = CreateMainScreen(root.transform);
            hostSetupScreen = CreateHostSetupScreen(root.transform);
            joinScreen = CreateJoinScreen(root.transform);
            lobbyScreen = CreateLobbyScreen(root.transform);
            avatarScreen = CreateAvatarScreen(root.transform);
            settingsScreen = CreateSettingsScreen(root.transform);
            root.transform.SetAsLastSibling();

            ConfigureRoomSettingsPresentation();
            ArrangeLegacyControls();
            RefreshProfilePresentation();
        }

        void ConfigureCanvasScaler()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                // NetworkRace also creates a race HUD canvas. Keep the entry shell above it
                // until the network handshake completes and HideMenu() removes this overlay.
                canvas.overrideSorting = true;
                canvas.sortingOrder = 50;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        void ConfigureRoomSettingsPresentation()
        {
            if (roomSettingsUi != null && hostSettingsSlot != null)
            {
                roomSettingsUi.SetPresentationParent(hostSettingsSlot, true);
            }
        }

        void ArrangeLegacyControls()
        {
            if (bootstrap == null) return;

            if (bootstrap.hostButton != null && hostSetupScreen != null)
            {
                bootstrap.hostButton.transform.SetParent(hostSetupScreen.transform, false);
                StyleExistingButton(bootstrap.hostButton, "방 만들기", new Vector2(262f, -272f),
                    new Vector2(440f, 78f), Yellow, Ink);
            }

            if (bootstrap.joinButton != null && joinCard != null)
            {
                bootstrap.joinButton.transform.SetParent(joinCard, false);
                StyleExistingButton(bootstrap.joinButton, "방 참가", new Vector2(0f, -104f),
                    new Vector2(440f, 78f), Pink, Color.white);
            }

            if (bootstrap.joinCodeInputField != null && joinCard != null)
            {
                InputField input = bootstrap.joinCodeInputField;
                input.transform.SetParent(joinCard, false);
                StyleExistingInput(input, new Vector2(0f, 20f), new Vector2(440f, 76f));
            }

            if (bootstrap.statusText != null)
            {
                // The status text is kept as the service-facing source of truth, while its
                // value is mirrored into the currently visible design screen by StatusChanged.
                bootstrap.statusText.gameObject.SetActive(false);
            }
        }

        void SetLegacyControlVisibility(bool hostVisible, bool joinVisible, bool inputVisible)
        {
            if (bootstrap == null) return;
            if (bootstrap.hostButton != null) bootstrap.hostButton.gameObject.SetActive(hostVisible);
            if (bootstrap.joinButton != null) bootstrap.joinButton.gameObject.SetActive(joinVisible);
            if (bootstrap.joinCodeInputField != null) bootstrap.joinCodeInputField.gameObject.SetActive(inputVisible);
            if (bootstrap.statusText != null) bootstrap.statusText.gameObject.SetActive(false);
        }

        void SetScreen(Screen nextScreen)
        {
            currentScreen = nextScreen;
            if (root != null) root.SetActive(nextScreen != Screen.Hidden);
            SetActive(mainScreen, nextScreen == Screen.Main);
            SetActive(hostSetupScreen, nextScreen == Screen.HostSetup);
            SetActive(joinScreen, nextScreen == Screen.JoinSetup);
            SetActive(lobbyScreen, nextScreen == Screen.Lobby);
            SetActive(avatarScreen, nextScreen == Screen.Avatar);
            SetActive(settingsScreen, nextScreen == Screen.Settings);
        }

        static void SetActive(GameObject target, bool value)
        {
            if (target != null) target.SetActive(value);
        }

        GameObject CreateMainScreen(Transform parent)
        {
            GameObject screen = CreateScreen(parent, "Screen_Main");

            Text title = CreateText(screen.transform, "Logo", "M2 RACING", 78, Yellow, TextAnchor.MiddleCenter,
                UiFontRole.Display);
            SetAnchored(title.rectTransform, new Vector2(0f, -108f), new Vector2(860f, 100f), new Vector2(0.5f, 1f));
            AddOutline(title, Ink, new Vector2(4f, -4f));

            Text subtitle = CreateText(screen.transform, "Subtitle", "누가 더 빠른가?  ·  M2 RACING", 24, Color.white,
                TextAnchor.MiddleCenter, UiFontRole.Metric);
            SetAnchored(subtitle.rectTransform, new Vector2(0f, -188f), new Vector2(760f, 42f), new Vector2(0.5f, 1f));

            CreateAvatarCard(screen.transform);

            CreateButton(screen.transform, "CreateRoomButton", "방 만들기", new Vector2(286f, 48f),
                new Vector2(420f, 84f), Yellow, Ink, ShowHostSetup);
            CreateButton(screen.transform, "JoinRoomButton", "방 참가", new Vector2(286f, -58f),
                new Vector2(420f, 84f), Pink, Color.white, ShowJoinSetup);
            CreateButton(screen.transform, "AvatarButton", "아바타", new Vector2(286f, -164f),
                new Vector2(198f, 70f), Mint, Ink, ShowAvatar);
            CreateButton(screen.transform, "SettingsButton", "설정", new Vector2(508f, -164f),
                new Vector2(198f, 70f), Color.white, Ink, ShowSettings);

            mainFeedback = CreateText(screen.transform, "Feedback", "", 20, Color.white, TextAnchor.MiddleCenter);
            SetAnchored(mainFeedback.rectTransform, new Vector2(0f, 42f), new Vector2(1000f, 44f), new Vector2(0.5f, 0f));
            AddOutline(mainFeedback, Ink, new Vector2(1f, -1f));
            return screen;
        }

        GameObject CreateHostSetupScreen(Transform parent)
        {
            GameObject screen = CreateScreen(parent, "Screen_HostSetup");
            CreateBackButton(screen.transform, ShowMain);
            CreateScreenTitle(screen.transform, "방 만들기", "친구와 함께 달릴 프라이빗 레이스를 설정하세요.");

            GameObject information = CreateCard(screen.transform, "HostInformationCard", new Vector2(-292f, -18f),
                new Vector2(390f, 348f), DeepPurple, Color.white);
            Text eyebrow = CreateText(information.transform, "Eyebrow", "PRIVATE 1v1 RACE", 20, Yellow,
                TextAnchor.MiddleCenter, UiFontRole.Metric);
            SetAnchored(eyebrow.rectTransform, new Vector2(0f, 124f), new Vector2(330f, 34f));
            Text heading = CreateText(information.transform, "Heading", "친구를 초대해\n레이스를 시작하세요", 36, Color.white,
                TextAnchor.MiddleCenter, UiFontRole.Display);
            SetAnchored(heading.rectTransform, new Vector2(0f, 52f), new Vector2(340f, 110f));
            AddOutline(heading, Ink, new Vector2(2f, -2f));
            Text hint = CreateText(information.transform, "Hint", "방을 만든 뒤 표시되는 코드를\n친구에게 전달하면 바로 참가할 수 있습니다.",
                22, SoftPink, TextAnchor.MiddleCenter);
            SetAnchored(hint.rectTransform, new Vector2(0f, -84f), new Vector2(340f, 86f));

            GameObject slot = new GameObject("HostSettingsSlot", typeof(RectTransform));
            slot.transform.SetParent(screen.transform, false);
            hostSettingsSlot = slot.transform;
            SetAnchored(slot.GetComponent<RectTransform>(), new Vector2(262f, -20f), new Vector2(500f, 400f));

            hostFeedback = CreateText(screen.transform, "Feedback", "", 20, Color.white, TextAnchor.MiddleCenter);
            SetAnchored(hostFeedback.rectTransform, new Vector2(262f, -330f), new Vector2(510f, 42f));
            AddOutline(hostFeedback, Ink, new Vector2(1f, -1f));
            return screen;
        }

        GameObject CreateJoinScreen(Transform parent)
        {
            GameObject screen = CreateScreen(parent, "Screen_Join");
            CreateBackButton(screen.transform, ShowMain);
            CreateScreenTitle(screen.transform, "방 참가", "친구가 만든 방 코드로 레이스에 합류하세요.");

            GameObject card = CreateCard(screen.transform, "JoinCard", new Vector2(0f, -34f), new Vector2(580f, 356f),
                Color.white, Ink);
            joinCard = card.transform;
            Text heading = CreateText(card.transform, "Heading", "방 코드 입력", 36, Ink, TextAnchor.MiddleCenter,
                UiFontRole.Display);
            SetAnchored(heading.rectTransform, new Vector2(0f, 132f), new Vector2(460f, 52f));
            Text hint = CreateText(card.transform, "Hint", "예시  ·  M2-7X4K", 20, Purple, TextAnchor.MiddleCenter,
                UiFontRole.Metric);
            SetAnchored(hint.rectTransform, new Vector2(0f, 91f), new Vector2(440f, 30f));

            joinFeedback = CreateText(card.transform, "Feedback", "", 19, Ink, TextAnchor.MiddleCenter);
            SetAnchored(joinFeedback.rectTransform, new Vector2(0f, -162f), new Vector2(500f, 38f));
            return screen;
        }

        GameObject CreateAvatarScreen(Transform parent)
        {
            GameObject screen = CreateScreen(parent, "Screen_Avatar");
            CreateBackButton(screen.transform, ShowMain);
            CreateScreenTitle(screen.transform, "아바타 설정", "나만의 색과 레이서 이름을 메인과 로비에 반영하세요.");

            GameObject preview = CreateCard(screen.transform, "AvatarPreviewCard", new Vector2(-294f, -26f),
                new Vector2(410f, 372f), Color.white, Ink);
            Text previewCaption = CreateText(preview.transform, "Caption", "MY RACER PREVIEW", 19, Purple,
                TextAnchor.MiddleCenter, UiFontRole.Metric);
            SetAnchored(previewCaption.rectTransform, new Vector2(0f, 144f), new Vector2(340f, 32f));
            GameObject portrait = CreateCard(preview.transform, "AvatarPreview", new Vector2(0f, 42f),
                new Vector2(164f, 164f), Pink, Ink);
            avatarPreviewImage = portrait.GetComponent<Image>();
            avatarPreviewInitials = CreateText(portrait.transform, "Initials", "M2", 54, Color.white,
                TextAnchor.MiddleCenter, UiFontRole.Display);
            SetAnchored(avatarPreviewInitials.rectTransform, Vector2.zero, new Vector2(142f, 82f));
            avatarPreviewName = CreateText(preview.transform, "Name", M2PlayerProfile.DefaultDisplayName, 34, Ink,
                TextAnchor.MiddleCenter, UiFontRole.Display);
            SetAnchored(avatarPreviewName.rectTransform, new Vector2(0f, -86f), new Vector2(350f, 52f));
            Text previewHint = CreateText(preview.transform, "Hint", "저장하면 메인 프로필과 로비 카드에 적용됩니다.", 19, Purple,
                TextAnchor.MiddleCenter);
            SetAnchored(previewHint.rectTransform, new Vector2(0f, -132f), new Vector2(360f, 36f));

            GameObject editor = CreateCard(screen.transform, "AvatarEditorCard", new Vector2(274f, -26f),
                new Vector2(472f, 372f), Color.white, Ink);
            Text heading = CreateText(editor.transform, "Heading", "프로필 꾸미기", 34, Ink, TextAnchor.MiddleCenter,
                UiFontRole.Display);
            SetAnchored(heading.rectTransform, new Vector2(0f, 144f), new Vector2(400f, 52f));
            Text nameLabel = CreateText(editor.transform, "NameLabel", "레이서 이름", 21, Ink, TextAnchor.MiddleLeft);
            SetAnchored(nameLabel.rectTransform, new Vector2(-172f, 94f), new Vector2(300f, 32f));
            avatarNameInput = CreateProfileInput(editor.transform, "AvatarNameInput", new Vector2(0f, 48f),
                new Vector2(398f, 62f));
            avatarNameInput.onValueChanged.AddListener(_ => RefreshAvatarPreview());

            Text colorLabel = CreateText(editor.transform, "ColorLabel", "아바타 색", 21, Ink, TextAnchor.MiddleLeft);
            SetAnchored(colorLabel.rectTransform, new Vector2(-172f, -12f), new Vector2(300f, 32f));
            CreateButton(editor.transform, "ColorPink", "핑크", new Vector2(-132f, -60f), new Vector2(118f, 60f),
                Pink, Color.white, () => SelectAvatarColor(0));
            CreateButton(editor.transform, "ColorCyan", "하늘", new Vector2(0f, -60f), new Vector2(118f, 60f),
                Cyan, Ink, () => SelectAvatarColor(1));
            CreateButton(editor.transform, "ColorMint", "민트", new Vector2(132f, -60f), new Vector2(118f, 60f),
                Mint, Ink, () => SelectAvatarColor(2));
            CreateButton(editor.transform, "SaveAvatarButton", "저장하기", new Vector2(0f, -132f),
                new Vector2(398f, 70f), Yellow, Ink, SaveAvatar);

            avatarFeedback = CreateText(editor.transform, "Feedback", "", 18, Ink, TextAnchor.MiddleCenter);
            SetAnchored(avatarFeedback.rectTransform, new Vector2(0f, -172f), new Vector2(420f, 30f));
            return screen;
        }

        GameObject CreateSettingsScreen(Transform parent)
        {
            GameObject screen = CreateScreen(parent, "Screen_Settings");
            CreateBackButton(screen.transform, ShowMain);
            CreateScreenTitle(screen.transform, "환경설정", "사운드와 화면 표시를 내 취향에 맞게 조절하세요.");

            GameObject card = CreateCard(screen.transform, "SettingsCard", new Vector2(0f, -28f),
                new Vector2(700f, 400f), Color.white, Ink);
            Text heading = CreateText(card.transform, "Heading", "게임 환경", 36, Ink, TextAnchor.MiddleCenter,
                UiFontRole.Display);
            SetAnchored(heading.rectTransform, new Vector2(0f, 150f), new Vector2(580f, 52f));

            Text volumeLabel = CreateText(card.transform, "VolumeLabel", "마스터 볼륨", 24, Ink,
                TextAnchor.MiddleLeft, UiFontRole.Body);
            SetAnchored(volumeLabel.rectTransform, new Vector2(-252f, 76f), new Vector2(210f, 38f));
            settingsVolumeSlider = CreateSettingsSlider(card.transform, "MasterVolumeSlider", new Vector2(18f, 72f),
                new Vector2(410f, 48f));
            settingsVolumeSlider.onValueChanged.AddListener(_ => RefreshSettingsPresentation());
            settingsVolumeValue = CreateText(card.transform, "VolumeValue", "80%", 24, Purple, TextAnchor.MiddleRight,
                UiFontRole.Metric);
            SetAnchored(settingsVolumeValue.rectTransform, new Vector2(272f, 76f), new Vector2(94f, 38f));

            Text fullscreenLabel = CreateText(card.transform, "FullscreenLabel", "전체 화면", 24, Ink,
                TextAnchor.MiddleLeft, UiFontRole.Body);
            SetAnchored(fullscreenLabel.rectTransform, new Vector2(-252f, -12f), new Vector2(210f, 38f));
            settingsFullscreenToggle = CreateSettingsToggle(card.transform, "FullscreenToggle", new Vector2(210f, -12f),
                "게임 실행 시 전체 화면 사용");

            CreateButton(card.transform, "ResetSettingsButton", "기본값", new Vector2(-132f, -112f),
                new Vector2(246f, 70f), Mint, Ink, ResetSettingsDraft);
            CreateButton(card.transform, "SaveSettingsButton", "저장하기", new Vector2(132f, -112f),
                new Vector2(246f, 70f), Yellow, Ink, SaveSettings);
            settingsFeedback = CreateText(card.transform, "Feedback", "", 19, Ink, TextAnchor.MiddleCenter);
            SetAnchored(settingsFeedback.rectTransform, new Vector2(0f, -164f), new Vector2(620f, 32f));
            return screen;
        }

        void ResetSettingsDraft()
        {
            if (settingsVolumeSlider != null)
            {
                settingsVolumeSlider.SetValueWithoutNotify(M2GameSettings.DefaultMasterVolume);
            }
            if (settingsFullscreenToggle != null)
            {
                settingsFullscreenToggle.SetIsOnWithoutNotify(M2GameSettings.DefaultFullscreen);
            }
            RefreshSettingsPresentation();
            SetFeedback(settingsFeedback, "기본값을 불러왔습니다. 저장하면 적용됩니다.", Purple);
        }

        void SaveSettings()
        {
            float volume = settingsVolumeSlider != null ? settingsVolumeSlider.value : M2GameSettings.MasterVolume;
            bool fullscreen = settingsFullscreenToggle == null || settingsFullscreenToggle.isOn;
            M2GameSettings.Save(volume, fullscreen);
            RefreshSettingsPresentation();
            SetFeedback(settingsFeedback, "환경설정을 저장했습니다.", Purple);
        }

        void RefreshSettingsPresentation()
        {
            if (settingsVolumeValue != null)
            {
                float volume = settingsVolumeSlider != null ? settingsVolumeSlider.value : M2GameSettings.MasterVolume;
                settingsVolumeValue.text = $"{Mathf.RoundToInt(M2GameSettings.NormalizeVolume(volume) * 100f)}%";
            }
        }

        void SelectAvatarColor(int index)
        {
            draftAvatarColorIndex = index;
            RefreshAvatarPreview();
            SetFeedback(avatarFeedback, "미리보기에 선택한 색을 적용했습니다.", Purple);
        }

        void SaveAvatar()
        {
            string displayName = avatarNameInput != null ? avatarNameInput.text : M2PlayerProfile.DisplayName;
            M2PlayerProfile.Save(displayName, draftAvatarColorIndex);
            if (avatarNameInput != null) avatarNameInput.text = M2PlayerProfile.DisplayName;
            RefreshProfilePresentation();
            RefreshAvatarPreview();
            SetFeedback(avatarFeedback, "아바타 설정을 저장했습니다.", Purple);
        }

        GameObject CreateLobbyScreen(Transform parent)
        {
            GameObject screen = CreateScreen(parent, "Screen_Lobby");
            CreateScreenTitle(screen.transform, "로비", "상대 접속을 기다리는 동안 방 정보를 확인하세요.");

            GameObject codeCard = CreateCard(screen.transform, "RoomCodeCard", new Vector2(-66f, -62f),
                new Vector2(356f, 108f), Color.white, Ink, new Vector2(1f, 1f));
            Text codeCaption = CreateText(codeCard.transform, "Caption", "방 코드", 20, Purple, TextAnchor.UpperCenter);
            SetAnchored(codeCaption.rectTransform, new Vector2(0f, -26f), new Vector2(300f, 32f), new Vector2(0.5f, 1f));
            lobbyCode = CreateText(codeCard.transform, "Code", "연결 중", 42, Ink, TextAnchor.MiddleCenter, UiFontRole.Metric);
            SetAnchored(lobbyCode.rectTransform, new Vector2(0f, -10f), new Vector2(310f, 54f));
            AddOutline(lobbyCode, Yellow, new Vector2(1f, -1f));
            RectTransform codeRect = codeCard.GetComponent<RectTransform>();
            codeRect.anchorMin = new Vector2(1f, 1f);
            codeRect.anchorMax = new Vector2(1f, 1f);
            codeRect.pivot = new Vector2(1f, 1f);
            codeRect.anchoredPosition = new Vector2(-54f, -44f);

            GameObject hostCard = CreatePlayerCard(screen.transform, "HostPlayerCard", new Vector2(-294f, 28f),
                M2PlayerProfile.AvatarColor, M2PlayerProfile.DisplayName, "방장 · 로컬 플레이어", true);
            lobbyHostAvatarImage = hostCard.transform.Find("Avatar").GetComponent<Image>();
            lobbyHostInitials = hostCard.transform.Find("Avatar/Initial").GetComponent<Text>();
            lobbyHostName = hostCard.transform.Find("Name").GetComponent<Text>();
            lobbyRole = hostCard.transform.Find("Role").GetComponent<Text>();
            GameObject guestCard = CreatePlayerCard(screen.transform, "GuestPlayerCard", new Vector2(-294f, -162f),
                Cyan, "상대", "접속 대기");
            lobbyGuest = guestCard.transform.Find("Role").GetComponent<Text>();

            GameObject settings = CreateCard(screen.transform, "LobbyRulesCard", new Vector2(282f, -24f),
                new Vector2(454f, 330f), Color.white, Ink);
            Text settingsTitle = CreateText(settings.transform, "Title", "방 설정", 34, Ink, TextAnchor.MiddleCenter,
                UiFontRole.Display);
            SetAnchored(settingsTitle.rectTransform, new Vector2(0f, 126f), new Vector2(380f, 48f));
            lobbyRules = CreateText(settings.transform, "Rules", "", 26, Ink, TextAnchor.MiddleCenter);
            SetAnchored(lobbyRules.rectTransform, new Vector2(0f, 14f), new Vector2(380f, 170f));

            lobbyStatus = CreateText(screen.transform, "LobbyStatus", "", 22, Color.white, TextAnchor.MiddleCenter);
            SetAnchored(lobbyStatus.rectTransform, new Vector2(0f, 38f), new Vector2(1050f, 48f), new Vector2(0.5f, 0f));
            AddOutline(lobbyStatus, Ink, new Vector2(1f, -1f));
            return screen;
        }

        GameObject CreateScreen(Transform parent, string name)
        {
            GameObject screen = CreateFullscreenPanel(parent, name, DeepPurple);
            screen.GetComponent<Image>().raycastTarget = false;
            CreateBackdrop(screen.transform);
            return screen;
        }

        void CreateBackdrop(Transform parent)
        {
            GameObject purpleBand = CreateDecorativePanel(parent, "PurpleBand", new Color(Purple.r, Purple.g, Purple.b, 0.72f));
            SetAnchored(purpleBand.GetComponent<RectTransform>(), new Vector2(-84f, 212f), new Vector2(800f, 450f),
                new Vector2(0f, 1f));
            purpleBand.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -13f);

            GameObject pinkBand = CreateDecorativePanel(parent, "PinkBand", new Color(Pink.r, Pink.g, Pink.b, 0.55f));
            SetAnchored(pinkBand.GetComponent<RectTransform>(), new Vector2(162f, -160f), new Vector2(760f, 360f),
                new Vector2(1f, 0f));
            pinkBand.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 17f);

            GameObject cyanBand = CreateDecorativePanel(parent, "CyanBand", new Color(Cyan.r, Cyan.g, Cyan.b, 0.36f));
            SetAnchored(cyanBand.GetComponent<RectTransform>(), new Vector2(-174f, -118f), new Vector2(520f, 250f),
                new Vector2(1f, 1f));
            cyanBand.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -24f);
        }

        static GameObject CreateDecorativePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        void CreateAvatarCard(Transform parent)
        {
            GameObject card = CreateCard(parent, "ProfileCard", new Vector2(-286f, -30f), new Vector2(420f, 306f),
                Color.white, Ink);
            Text caption = CreateText(card.transform, "Caption", "MY RACER", 19, Purple, TextAnchor.MiddleCenter,
                UiFontRole.Metric);
            SetAnchored(caption.rectTransform, new Vector2(0f, 126f), new Vector2(340f, 32f));

            GameObject portrait = CreateCard(card.transform, "Avatar", new Vector2(0f, 36f), new Vector2(126f, 126f),
                Pink, Ink);
            mainAvatarImage = portrait.GetComponent<Image>();
            mainAvatarInitials = CreateText(portrait.transform, "Initials", "M2", 45, Color.white, TextAnchor.MiddleCenter,
                UiFontRole.Display);
            SetAnchored(mainAvatarInitials.rectTransform, Vector2.zero, new Vector2(112f, 72f));
            mainAvatarName = CreateText(card.transform, "Name", M2PlayerProfile.DefaultDisplayName, 32, Ink,
                TextAnchor.MiddleCenter, UiFontRole.Display);
            SetAnchored(mainAvatarName.rectTransform, new Vector2(0f, -72f), new Vector2(350f, 52f));
            Text description = CreateText(card.transform, "Description", "아바타와 칭호를 꾸며 보세요", 20, Purple,
                TextAnchor.MiddleCenter);
            SetAnchored(description.rectTransform, new Vector2(0f, -116f), new Vector2(350f, 34f));
        }

        void RefreshProfilePresentation()
        {
            string displayName = M2PlayerProfile.DisplayName;
            Color avatarColor = M2PlayerProfile.AvatarColor;
            string initials = GetInitials(displayName);

            if (mainAvatarImage != null) mainAvatarImage.color = avatarColor;
            if (mainAvatarInitials != null) mainAvatarInitials.text = initials;
            if (mainAvatarName != null) mainAvatarName.text = displayName;

            if (lobbyHostAvatarImage != null) lobbyHostAvatarImage.color = avatarColor;
            if (lobbyHostInitials != null) lobbyHostInitials.text = initials;
            if (lobbyHostName != null) lobbyHostName.text = displayName;
        }

        void RefreshAvatarPreview()
        {
            string displayName = avatarNameInput != null
                ? M2PlayerProfile.NormalizeDisplayName(avatarNameInput.text)
                : M2PlayerProfile.DisplayName;
            if (avatarPreviewImage != null) avatarPreviewImage.color = M2PlayerProfile.ResolveAvatarColor(draftAvatarColorIndex);
            if (avatarPreviewInitials != null) avatarPreviewInitials.text = GetInitials(displayName);
            if (avatarPreviewName != null) avatarPreviewName.text = displayName;
        }

        static string GetInitials(string displayName)
        {
            string normalized = M2PlayerProfile.NormalizeDisplayName(displayName).Replace(" ", string.Empty);
            return normalized.Length <= 2 ? normalized : normalized.Substring(0, 2);
        }

        static InputField CreateProfileInput(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject fieldObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            fieldObject.transform.SetParent(parent, false);
            SetAnchored(fieldObject.GetComponent<RectTransform>(), position, size);

            Image background = fieldObject.GetComponent<Image>();
            background.color = new Color(0.957f, 0.925f, 1f);
            AddOutline(background, Ink, new Vector2(3f, -3f));

            InputField field = fieldObject.GetComponent<InputField>();
            field.characterLimit = 12;

            Text value = CreateText(fieldObject.transform, "Text", string.Empty, 28, Ink, TextAnchor.MiddleCenter,
                UiFontRole.Body);
            Stretch(value.rectTransform, new Vector2(14f, 6f), new Vector2(-14f, -6f));
            Text placeholder = CreateText(fieldObject.transform, "Placeholder", "레이서 이름", 26,
                new Color(Ink.r, Ink.g, Ink.b, 0.45f), TextAnchor.MiddleCenter);
            Stretch(placeholder.rectTransform, new Vector2(14f, 6f), new Vector2(-14f, -6f));

            field.textComponent = value;
            field.placeholder = placeholder;
            field.text = M2PlayerProfile.DisplayName;
            return field;
        }

        static Slider CreateSettingsSlider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            SetAnchored(sliderObject.GetComponent<RectTransform>(), position, size);
            sliderObject.GetComponent<Image>().color = Color.clear;

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(sliderObject.transform, false);
            Stretch(background.GetComponent<RectTransform>(), new Vector2(8f, 15f), new Vector2(-8f, -15f));
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.84f, 0.76f, 0.95f);
            AddOutline(backgroundImage, Ink, new Vector2(2f, -2f));

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(10f, 16f), new Vector2(-10f, -16f));
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            fill.GetComponent<Image>().color = Pink;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), new Vector2(4f, 2f), new Vector2(-4f, -2f));
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(32f, 42f);
            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = Yellow;
            AddOutline(handleImage, Ink, new Vector2(2f, -2f));

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            return slider;
        }

        static Toggle CreateSettingsToggle(Transform parent, string name, Vector2 position, string label)
        {
            GameObject toggleObject = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);
            SetAnchored(toggleObject.GetComponent<RectTransform>(), position, new Vector2(326f, 48f));

            GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(toggleObject.transform, false);
            SetAnchored(box.GetComponent<RectTransform>(), new Vector2(-142f, 0f), new Vector2(38f, 38f));
            Image boxImage = box.GetComponent<Image>();
            boxImage.color = new Color(0.957f, 0.925f, 1f);
            AddOutline(boxImage, Ink, new Vector2(2f, -2f));

            GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(box.transform, false);
            Stretch(check.GetComponent<RectTransform>(), new Vector2(8f, 8f), new Vector2(-8f, -8f));
            check.GetComponent<Image>().color = Mint;

            Text text = CreateText(toggleObject.transform, "Label", label, 20, Purple, TextAnchor.MiddleLeft);
            SetAnchored(text.rectTransform, new Vector2(36f, 0f), new Vector2(238f, 42f));

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = check.GetComponent<Image>();
            return toggle;
        }

        void CreateScreenTitle(Transform parent, string titleValue, string subtitleValue)
        {
            Text title = CreateText(parent, "Title", titleValue, 52, Yellow, TextAnchor.MiddleCenter, UiFontRole.Display);
            SetAnchored(title.rectTransform, new Vector2(0f, -82f), new Vector2(860f, 70f), new Vector2(0.5f, 1f));
            AddOutline(title, Ink, new Vector2(3f, -3f));

            Text subtitle = CreateText(parent, "Subtitle", subtitleValue, 22, Color.white, TextAnchor.MiddleCenter);
            SetAnchored(subtitle.rectTransform, new Vector2(0f, -136f), new Vector2(920f, 36f), new Vector2(0.5f, 1f));
            AddOutline(subtitle, Ink, new Vector2(1f, -1f));
        }

        void CreateBackButton(Transform parent, UnityAction action)
        {
            Button button = CreateButton(parent, "BackButton", "← 메인", new Vector2(0f, 0f), new Vector2(156f, 56f),
                Color.white, Ink, action);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(106f, -52f);
        }

        GameObject CreatePlayerCard(Transform parent, string name, Vector2 position, Color avatarColor,
            string playerName, string role, bool isLocalPlayer = false)
        {
            GameObject card = CreateCard(parent, name, position, new Vector2(450f, 154f), Color.white, Ink);
            GameObject avatar = CreateCard(card.transform, "Avatar", new Vector2(-151f, 0f), new Vector2(92f, 92f),
                avatarColor, Ink);
            Text avatarText = CreateText(avatar.transform, "Initial", isLocalPlayer ? GetInitials(playerName) : "?", 32, Color.white,
                TextAnchor.MiddleCenter, UiFontRole.Display);
            SetAnchored(avatarText.rectTransform, Vector2.zero, new Vector2(74f, 58f));
            Text label = CreateText(card.transform, "Name", playerName, 30, Ink, TextAnchor.MiddleLeft, UiFontRole.Display);
            SetAnchored(label.rectTransform, new Vector2(-83f, 28f), new Vector2(250f, 44f));
            Text roleLabel = CreateText(card.transform, "Role", role, 20, Purple, TextAnchor.MiddleLeft);
            SetAnchored(roleLabel.rectTransform, new Vector2(-83f, -23f), new Vector2(260f, 36f));
            return card;
        }

        void RefreshLobbyRules()
        {
            if (lobbyRules == null) return;

            RaceMode mode = roomSettingsUi != null ? roomSettingsUi.selectedMode : RaceMode.Item;
            if (mode == RaceMode.Speed)
            {
                lobbyRules.text = "스피드전\n5바퀴 · 단순 완주\n5초 자동 휘발유 지급";
                return;
            }

            int laps = roomSettingsUi != null
                ? RaceModeRules.NormalizeItemLapCount(roomSettingsUi.selectedItemLapCount)
                : 3;
            VictoryCondition condition = roomSettingsUi != null
                ? roomSettingsUi.selectedItemVictoryCondition
                : VictoryCondition.SimpleFinish;
            string victory = condition == VictoryCondition.StarBet ? "별점 내기" : "단순 완주";
            lobbyRules.text = $"아이템전\n{laps}바퀴 · {victory}\n아이템 슬롯 2칸";
        }

        static GameObject CreateFullscreenPanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        static GameObject CreateCard(Transform parent, string name, Vector2 position, Vector2 size, Color color,
            Color outlineColor, Vector2? anchor = null)
        {
            GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            card.GetComponent<Image>().color = color;
            card.GetComponent<Image>().raycastTarget = false;
            SetAnchored(card.GetComponent<RectTransform>(), position, size, anchor ?? new Vector2(0.5f, 0.5f));
            AddOutline(card.GetComponent<Image>(), outlineColor, new Vector2(4f, -4f));
            return card;
        }

        static Text CreateText(Transform parent, string name, string value, int fontSize, Color color,
            TextAnchor alignment, UiFontRole role = UiFontRole.Body)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            UiTypography.Apply(text, role);
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
            Color fill, Color labelColor, UnityAction action)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetAnchored(rect, position, size);

            Image background = buttonObject.GetComponent<Image>();
            background.color = fill;
            Button button = buttonObject.GetComponent<Button>();
            ConfigureButtonColors(button);
            AddOutline(background, Ink, new Vector2(3f, -3f));

            Text text = CreateText(buttonObject.transform, "Label", label, 30, labelColor, TextAnchor.MiddleCenter,
                UiFontRole.Body);
            Stretch(text.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            AddOutline(text, labelColor == Ink ? Color.white : Ink, new Vector2(1f, -1f));
            if (action != null) button.onClick.AddListener(action);
            return button;
        }

        static void StyleExistingButton(Button button, string label, Vector2 position, Vector2 size, Color fill,
            Color labelColor)
        {
            if (button == null) return;
            SetAnchored(button.GetComponent<RectTransform>(), position, size);
            Image background = button.GetComponent<Image>();
            if (background != null)
            {
                background.color = fill;
                AddOutline(background, Ink, new Vector2(3f, -3f));
            }
            ConfigureButtonColors(button);

            Text text = button.GetComponentInChildren<Text>(true);
            if (text == null) return;
            UiTypography.Apply(text);
            text.text = label;
            text.fontSize = 30;
            text.color = labelColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            Stretch(text.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            AddOutline(text, labelColor == Ink ? Color.white : Ink, new Vector2(1f, -1f));
        }

        static void StyleExistingInput(InputField input, Vector2 position, Vector2 size)
        {
            if (input == null) return;
            SetAnchored(input.GetComponent<RectTransform>(), position, size);
            Image background = input.GetComponent<Image>();
            if (background != null)
            {
                background.color = Color.white;
                AddOutline(background, Ink, new Vector2(3f, -3f));
            }

            input.characterLimit = 12;
            if (input.textComponent != null)
            {
                UiTypography.Apply(input.textComponent, UiFontRole.Metric);
                input.textComponent.fontSize = 32;
                input.textComponent.color = Ink;
                input.textComponent.alignment = TextAnchor.MiddleCenter;
                Stretch(input.textComponent.rectTransform, new Vector2(14f, 8f), new Vector2(-14f, -8f));
            }
            if (input.placeholder is Text placeholder)
            {
                UiTypography.Apply(placeholder);
                placeholder.text = "방 코드";
                placeholder.fontSize = 28;
                placeholder.color = new Color(Ink.r, Ink.g, Ink.b, 0.48f);
                placeholder.alignment = TextAnchor.MiddleCenter;
                Stretch(placeholder.rectTransform, new Vector2(14f, 8f), new Vector2(-14f, -8f));
            }
        }

        static void ConfigureButtonColors(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
        }

        static void SetFeedback(Text target, string message, Color color)
        {
            if (target == null) return;
            target.text = message ?? string.Empty;
            target.color = color;
        }

        static void SetAnchored(RectTransform rect, Vector2 position, Vector2 size, Vector2? anchor = null)
        {
            Vector2 resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
            rect.anchorMin = resolvedAnchor;
            rect.anchorMax = resolvedAnchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void AddOutline(Graphic target, Color color, Vector2 distance)
        {
            if (target == null) return;
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = false;
        }
    }
}
