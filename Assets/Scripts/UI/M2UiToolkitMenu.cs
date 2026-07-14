using System;
using System.Collections.Generic;
using M2.Core;
using M2.Network;
using M2.Stage;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace M2.UI
{
    /// <summary>
    /// UI Toolkit presentation for the M2 Racing entry flow. The view is authored in UXML/USS;
    /// this component only binds the existing Relay, lobby, profile, and settings data to it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class M2UiToolkitMenu : MonoBehaviour
    {
        enum Screen
        {
            None,
            Main,
            Host,
            Join,
            Lobby,
            Avatar,
            Settings,
            Hidden,
        }

        readonly struct DecorativeMotion
        {
            public readonly VisualElement Element;
            public readonly Vector2 Amplitude;
            public readonly float Period;
            public readonly float Phase;
            public readonly float BaseRotation;
            public readonly float RotationAmplitude;
            public readonly bool Spin;

            public DecorativeMotion(VisualElement element, Vector2 amplitude, float period, float phase,
                float baseRotation = 0f, float rotationAmplitude = 0f, bool spin = false)
            {
                Element = element;
                Amplitude = amplitude;
                Period = period;
                Phase = phase;
                BaseRotation = baseRotation;
                RotationAmplitude = rotationAmplitude;
                Spin = spin;
            }
        }

        const string ResourceRoot = "M2UI/";
        const string RaceTestSceneName = "Stage_BikiniCity";

        static readonly Color Ink = new Color32(26, 16, 48, 255);
        static readonly Color Pink = new Color32(255, 47, 158, 255);
        static readonly Color Purple = new Color32(90, 49, 158, 255);
        static readonly Color Cyan = new Color32(95, 216, 245, 255);
        static readonly Color Yellow = new Color32(255, 217, 61, 255);
        static readonly Color Mint = new Color32(182, 243, 107, 255);
        static readonly Color SoftPink = new Color32(255, 157, 224, 255);
        static readonly Color Muted = new Color32(241, 236, 250, 255);

        static readonly string[] AvatarPresetNames =
        {
            "레이서 #001", "핑크불릿 #077", "아쿠아 #013", "라임몬 #024", "퍼플킹 #999",
        };

        static readonly M2AvatarAppearance[] AvatarPresets =
        {
            new M2AvatarAppearance(0, M2AvatarEyes.Round, M2AvatarMouth.Smile, true, true, M2AvatarHat.None, 0),
            new M2AvatarAppearance(1, M2AvatarEyes.Cool, M2AvatarMouth.Flat, false, true, M2AvatarHat.Cap, 1),
            new M2AvatarAppearance(2, M2AvatarEyes.Happy, M2AvatarMouth.Smile, true, false, M2AvatarHat.None, 0),
            new M2AvatarAppearance(3, M2AvatarEyes.Round, M2AvatarMouth.Open, true, true, M2AvatarHat.None, 0),
            new M2AvatarAppearance(4, M2AvatarEyes.Round, M2AvatarMouth.Smile, true, true, M2AvatarHat.Crown, 2),
        };

        readonly Dictionary<Screen, VisualElement> screens = new Dictionary<Screen, VisualElement>();

        NetworkBootstrapUI bootstrap;
        RoomSettingsUI roomSettingsUi;
        NetworkRaceManager raceManager;
        UIDocument document;
        GameObject documentGameObject;
        PanelSettings fallbackPanelSettings;
        VisualElement app;
        bool initialized;
        bool subscribed;
        Screen currentScreen;

        Label mainRacerName;
        Label mainFeedback;
        Label hostFeedback;
        Label joinFeedback;
        Label lobbyRoomCode;
        Label lobbyHostName;
        Label lobbyHostMeta;
        Label lobbyHostReady;
        Label lobbyGuestName;
        Label lobbyGuestMeta;
        Label lobbyGuestReady;
        Label lobbyStatus;
        Label lobbyAuthority;
        Label lobbyRules;
        Label avatarPreviewName;
        Label avatarSummary;
        Label avatarFeedback;
        Label settingsMasterValue;
        Label settingsBgmValue;
        Label settingsSfxValue;
        Label settingsFeedback;
        TextField joinRoomCodeInput;
        TextField avatarNameInput;
        Slider settingsMasterSlider;
        Slider settingsBgmSlider;
        Slider settingsSfxSlider;
        Button hostModeButton;
        Button hostLapButton;
        Button hostVictoryButton;
        Button lobbyReadyButton;
        Button lobbyLeaveButton;
        Button[] lobbyModeButtons;
        Button[] lobbyLapButtons;
        Button[] lobbyVictoryButtons;
        Button[] lobbyStageButtons;
        Button[] avatarColorButtons;
        Button[] avatarPresetButtons;
        Button[] avatarCheeksButtons;
        Button[] avatarEarsButtons;
        Button[] avatarHatButtons;
        Button[] avatarPlateButtons;
        Button[] settingsQualityButtons;
        Button[] settingsLanguageButtons;
        Button[] settingsScreenModeButtons;
        IVisualElementScheduledItem decorationMotion;
        DecorativeMotion[] decorativeMotions;

        M2AvatarAppearance draftAppearance;
        M2GraphicsQuality draftGraphicsQuality;
        M2Language draftLanguage;
        bool draftFullscreen;
        StageType fallbackLobbyStage = StageType.BikiniCity;
        bool fallbackLobbySettingsDirty;

        public string CurrentScreenName => currentScreen.ToString();

        /// <summary>
        /// Creates the document on the existing menu canvas and binds it to the service-facing
        /// uGUI controls. Those controls remain the session authority but never render.
        /// </summary>
        public bool Initialize(NetworkBootstrapUI source, RoomSettingsUI settings)
        {
            bootstrap = source;
            roomSettingsUi = settings;

            if (!EnsureDocument()) return false;
            if (!initialized)
            {
                CacheElements();
                RegisterCallbacks();
                ConfigureImages();
                ConfigureDecorativeMotion();
                initialized = true;
            }

            Subscribe();
            HideLegacyControls();

            if (bootstrap != null && bootstrap.HasActiveSession)
            {
                ShowLobby(bootstrap.ActiveRoomCode, bootstrap.IsHostingSession);
            }
            else
            {
                ShowMain();
            }
            return true;
        }

        void Update()
        {
            if (currentScreen != Screen.Lobby) return;
            if (raceManager == null)
            {
                raceManager = FindFirstObjectByType<NetworkRaceManager>();
                if (raceManager != null && fallbackLobbySettingsDirty && IsLocalHost())
                {
                    RaceMode mode = roomSettingsUi != null ? roomSettingsUi.selectedMode : RaceMode.Item;
                    int laps = roomSettingsUi != null ? roomSettingsUi.selectedItemLapCount : 3;
                    VictoryCondition victory = roomSettingsUi != null
                        ? roomSettingsUi.selectedItemVictoryCondition
                        : VictoryCondition.SimpleFinish;
                    raceManager.RequestLobbySettings(mode, laps, victory, fallbackLobbyStage);
                    fallbackLobbySettingsDirty = false;
                }
            }
            RefreshLobbyPresentation();
        }

        void OnDestroy()
        {
            Unsubscribe();
            decorationMotion?.Pause();
            if (documentGameObject != null) Destroy(documentGameObject);
            if (fallbackPanelSettings != null) Destroy(fallbackPanelSettings);
        }

        bool EnsureDocument()
        {
            if (app != null) return true;

            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>(ResourceRoot + "M2MenuView");
            StyleSheet style = Resources.Load<StyleSheet>(ResourceRoot + "M2Menu");
            if (tree == null || style == null) return false;

            document = GetComponent<UIDocument>();
            if (document == null)
            {
                // A standalone document is intentionally not parented to the legacy uGUI canvas.
                // This makes the UI Toolkit panel fill the actual game display instead of inheriting
                // the canvas' presentation bounds or being clipped by its sibling graphics.
                documentGameObject = new GameObject("M2UiToolkitDocument");
                documentGameObject.SetActive(false);
                document = documentGameObject.AddComponent<UIDocument>();
            }

            if (document.panelSettings == null)
            {
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
            }

            document.sortingOrder = 100f;
            // Keep the UXML on the document itself. Cloning directly into rootVisualElement
            // looks correct during Awake, but UIDocument rebuilds that root on its first panel
            // attachment and would discard the manually cloned menu before the first frame.
            document.visualTreeAsset = tree;
            if (documentGameObject != null && !documentGameObject.activeSelf) documentGameObject.SetActive(true);
            VisualElement root = document.rootVisualElement;
            app = root.Q<VisualElement>("m2-app");
            if (app == null) return false;
            app.styleSheets.Add(style);
            return true;
        }

        void CacheElements()
        {
            screens[Screen.Main] = app.Q<VisualElement>("screen-main");
            screens[Screen.Host] = app.Q<VisualElement>("screen-host");
            screens[Screen.Join] = app.Q<VisualElement>("screen-join");
            screens[Screen.Lobby] = app.Q<VisualElement>("screen-lobby");
            screens[Screen.Avatar] = app.Q<VisualElement>("screen-avatar");
            screens[Screen.Settings] = app.Q<VisualElement>("screen-settings");

            mainRacerName = app.Q<Label>("main-racer-name");
            mainFeedback = app.Q<Label>("main-feedback");
            hostFeedback = app.Q<Label>("host-feedback");
            joinFeedback = app.Q<Label>("join-feedback");
            lobbyRoomCode = app.Q<Label>("lobby-room-code");
            lobbyHostName = app.Q<Label>("lobby-host-name");
            lobbyHostMeta = app.Q<Label>("lobby-host-meta");
            lobbyHostReady = app.Q<Label>("lobby-host-ready");
            lobbyGuestName = app.Q<Label>("lobby-guest-name");
            lobbyGuestMeta = app.Q<Label>("lobby-guest-meta");
            lobbyGuestReady = app.Q<Label>("lobby-guest-ready");
            lobbyStatus = app.Q<Label>("lobby-status");
            lobbyAuthority = app.Q<Label>("lobby-authority");
            lobbyRules = app.Q<Label>("lobby-rules");
            avatarPreviewName = app.Q<Label>("avatar-preview-name");
            avatarSummary = app.Q<Label>("avatar-summary");
            avatarFeedback = app.Q<Label>("avatar-feedback");
            settingsMasterValue = app.Q<Label>("settings-master-value");
            settingsBgmValue = app.Q<Label>("settings-bgm-value");
            settingsSfxValue = app.Q<Label>("settings-sfx-value");
            settingsFeedback = app.Q<Label>("settings-feedback");

            joinRoomCodeInput = app.Q<TextField>("join-room-code-input");
            avatarNameInput = app.Q<TextField>("avatar-name-input");
            settingsMasterSlider = app.Q<Slider>("settings-master-slider");
            settingsBgmSlider = app.Q<Slider>("settings-bgm-slider");
            settingsSfxSlider = app.Q<Slider>("settings-sfx-slider");

            hostModeButton = app.Q<Button>("host-mode-button");
            hostLapButton = app.Q<Button>("host-lap-button");
            hostVictoryButton = app.Q<Button>("host-victory-button");
            lobbyReadyButton = app.Q<Button>("lobby-ready-button");
            lobbyLeaveButton = app.Q<Button>("lobby-leave-button");
            lobbyModeButtons = new[]
            {
                app.Q<Button>("lobby-mode-item-button"), app.Q<Button>("lobby-mode-speed-button"),
            };
            lobbyLapButtons = new[]
            {
                app.Q<Button>("lobby-lap-1-button"), app.Q<Button>("lobby-lap-3-button"),
                app.Q<Button>("lobby-lap-5-button"),
            };
            lobbyVictoryButtons = new[]
            {
                app.Q<Button>("lobby-victory-finish-button"), app.Q<Button>("lobby-victory-star-button"),
            };
            lobbyStageButtons = new[]
            {
                app.Q<Button>("lobby-stage-bikini-button"),
                app.Q<Button>("lobby-stage-africa-button"),
                app.Q<Button>("lobby-stage-nether-button"),
            };
            avatarColorButtons = new[]
            {
                app.Q<Button>("avatar-color-0"), app.Q<Button>("avatar-color-1"),
                app.Q<Button>("avatar-color-2"), app.Q<Button>("avatar-color-3"),
                app.Q<Button>("avatar-color-4"), app.Q<Button>("avatar-color-5"),
            };
            avatarPresetButtons = new[]
            {
                app.Q<Button>("avatar-preset-0"), app.Q<Button>("avatar-preset-1"),
                app.Q<Button>("avatar-preset-2"), app.Q<Button>("avatar-preset-3"),
                app.Q<Button>("avatar-preset-4"),
            };
            avatarCheeksButtons = new[] { app.Q<Button>("avatar-cheeks-on"), app.Q<Button>("avatar-cheeks-off") };
            avatarEarsButtons = new[] { app.Q<Button>("avatar-ears-on"), app.Q<Button>("avatar-ears-off") };
            avatarHatButtons = new[]
            {
                app.Q<Button>("avatar-hat-none"), app.Q<Button>("avatar-hat-cap"), app.Q<Button>("avatar-hat-crown"),
            };
            avatarPlateButtons = new[]
            {
                app.Q<Button>("avatar-plate-0"), app.Q<Button>("avatar-plate-1"), app.Q<Button>("avatar-plate-2"),
            };
            settingsQualityButtons = new[]
            {
                app.Q<Button>("settings-quality-low"), app.Q<Button>("settings-quality-medium"),
                app.Q<Button>("settings-quality-high"),
            };
            settingsLanguageButtons = new[]
            {
                app.Q<Button>("settings-language-korean"), app.Q<Button>("settings-language-english"),
            };
            settingsScreenModeButtons = new[]
            {
                app.Q<Button>("settings-fullscreen-button"), app.Q<Button>("settings-windowed-button"),
            };
        }

        void RegisterCallbacks()
        {
            BindButton("main-create-button", ShowHostSetup);
            BindButton("main-join-button", ShowJoinSetup);
            BindButton("main-avatar-button", ShowAvatar);
            BindButton("main-avatar-settings-button", ShowAvatar);
            BindButton("main-settings-button", ShowSettings);
            BindButton("main-race-test-button", OpenLocalRaceTest);

            BindButton("host-back-button", ShowMain);
            BindButton("host-mode-button", ToggleHostMode);
            BindButton("host-lap-button", CycleHostLapCount);
            BindButton("host-victory-button", CycleHostVictoryCondition);
            BindButton("host-create-button", CreateRoom);

            BindButton("join-back-button", ShowMain);
            BindButton("join-submit-button", JoinRoom);
            if (joinRoomCodeInput != null)
            {
                joinRoomCodeInput.maxLength = M2RoomCode.Prefix.Length + M2RoomCode.SuffixLength;
                joinRoomCodeInput.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                    JoinRoom();
                    evt.StopPropagation();
                });
            }

            BindButton("lobby-leave-button", LeaveLobby);
            BindButton("lobby-mode-item-button", () => SelectLobbyMode(RaceMode.Item));
            BindButton("lobby-mode-speed-button", () => SelectLobbyMode(RaceMode.Speed));
            BindButton("lobby-lap-1-button", () => SelectLobbyLapCount(1));
            BindButton("lobby-lap-3-button", () => SelectLobbyLapCount(3));
            BindButton("lobby-lap-5-button", () => SelectLobbyLapCount(5));
            BindButton("lobby-victory-finish-button", () => SelectLobbyVictory(VictoryCondition.SimpleFinish));
            BindButton("lobby-victory-star-button", () => SelectLobbyVictory(VictoryCondition.StarBet));
            BindButton("lobby-stage-bikini-button", () => SelectLobbyStage(StageType.BikiniCity));
            BindButton("lobby-stage-africa-button", () => SelectLobbyStage(StageType.AfricaTv));
            BindButton("lobby-stage-nether-button", () => SelectLobbyStage(StageType.NetherFortress));
            BindButton("lobby-ready-button", ToggleLobbyReady);

            BindButton("avatar-back-button", ShowMain);
            for (int i = 0; i < avatarColorButtons.Length; i++)
            {
                int index = i;
                if (avatarColorButtons[i] != null) avatarColorButtons[i].clicked += () => SelectAvatarColor(index);
            }
            for (int i = 0; i < avatarPresetButtons.Length; i++)
            {
                int index = i;
                if (avatarPresetButtons[i] != null) avatarPresetButtons[i].clicked += () => SelectAvatarPreset(index);
            }
            BindButton("avatar-eyes-round", () => SelectAvatarEyes(M2AvatarEyes.Round));
            BindButton("avatar-eyes-happy", () => SelectAvatarEyes(M2AvatarEyes.Happy));
            BindButton("avatar-eyes-cool", () => SelectAvatarEyes(M2AvatarEyes.Cool));
            BindButton("avatar-mouth-smile", () => SelectAvatarMouth(M2AvatarMouth.Smile));
            BindButton("avatar-mouth-open", () => SelectAvatarMouth(M2AvatarMouth.Open));
            BindButton("avatar-mouth-flat", () => SelectAvatarMouth(M2AvatarMouth.Flat));
            BindButton("avatar-cheeks-on", () => SetAvatarCheeks(true));
            BindButton("avatar-cheeks-off", () => SetAvatarCheeks(false));
            BindButton("avatar-ears-on", () => SetAvatarEars(true));
            BindButton("avatar-ears-off", () => SetAvatarEars(false));
            BindButton("avatar-hat-none", () => SelectAvatarHat(M2AvatarHat.None));
            BindButton("avatar-hat-cap", () => SelectAvatarHat(M2AvatarHat.Cap));
            BindButton("avatar-hat-crown", () => SelectAvatarHat(M2AvatarHat.Crown));
            BindButton("avatar-plate-0", () => SelectAvatarPlate(0));
            BindButton("avatar-plate-1", () => SelectAvatarPlate(1));
            BindButton("avatar-plate-2", () => SelectAvatarPlate(2));
            BindButton("avatar-save-button", SaveAvatar);

            BindButton("settings-back-button", ShowMain);
            BindButton("settings-quality-low", () => SelectQuality(M2GraphicsQuality.Low));
            BindButton("settings-quality-medium", () => SelectQuality(M2GraphicsQuality.Medium));
            BindButton("settings-quality-high", () => SelectQuality(M2GraphicsQuality.High));
            BindButton("settings-fullscreen-button", () => SelectScreenMode(true));
            BindButton("settings-windowed-button", () => SelectScreenMode(false));
            BindButton("settings-language-korean", () => SelectLanguage(M2Language.Korean));
            BindButton("settings-language-english", () => SelectLanguage(M2Language.English));
            BindButton("settings-reset-button", ResetSettingsDraft);
            BindButton("settings-save-button", SaveSettings);

            ConfigureSlider(settingsMasterSlider, settingsMasterValue);
            ConfigureSlider(settingsBgmSlider, settingsBgmValue);
            ConfigureSlider(settingsSfxSlider, settingsSfxValue);
        }

        void ConfigureImages()
        {
            SetVectorImage("main-gradient", ResourceRoot + "Backgrounds/MainGradient");
            SetVectorImage("host-gradient", ResourceRoot + "Backgrounds/LobbyGradient");
            SetVectorImage("join-gradient", ResourceRoot + "Backgrounds/LobbyGradient");
            SetVectorImage("lobby-gradient", ResourceRoot + "Backgrounds/LobbyGradient");
            SetVectorImage("avatar-gradient", ResourceRoot + "Backgrounds/AvatarGradient");
            SetVectorImage("settings-gradient", ResourceRoot + "Backgrounds/SettingsGradient");
            SetTextureImage("main-photo-mj1", ResourceRoot + "Icons/mj1");
            SetTextureImage("main-photo-mj2", ResourceRoot + "Icons/mj2");
            SetTextureImage("main-photo-portrait", ResourceRoot + "Icons/1496688619451318292");
            SetVectorImage("main-create-icon", ResourceRoot + "Icons/add");
            SetVectorImage("main-create-icon-shadow", ResourceRoot + "Icons/add");
            SetVectorImage("main-join-icon", ResourceRoot + "Icons/door");
            SetVectorImage("main-join-icon-shadow", ResourceRoot + "Icons/door");
            SetVectorImage("main-avatar-icon", ResourceRoot + "Icons/mask");
            SetVectorImage("main-avatar-icon-shadow", ResourceRoot + "Icons/mask");
            SetVectorImage("main-settings-icon", ResourceRoot + "Icons/settings");
            SetVectorImage("main-settings-icon-shadow", ResourceRoot + "Icons/settings");
            SetVectorImage("host-flag-icon", ResourceRoot + "Icons/racing-flag");
            SetVectorImage("host-crown-icon", ResourceRoot + "Icons/crown");
            SetVectorImage("join-door-icon", ResourceRoot + "Icons/door");
            SetVectorImage("join-submit-icon-shadow", ResourceRoot + "Icons/crown");
            SetVectorImage("lobby-crown-icon", ResourceRoot + "Icons/crown");
            SetVectorImage("lobby-settings-icon", ResourceRoot + "Icons/settings");
            SetVectorImage("lobby-mode-icon", ResourceRoot + "Icons/banana");
            SetVectorImage("lobby-speed-icon", ResourceRoot + "Icons/racing-flag");
            SetVectorImage("lobby-stage-bikini-icon", ResourceRoot + "Icons/bikini-city");
            SetVectorImage("lobby-stage-africa-icon", ResourceRoot + "Icons/afreecatv");
            SetVectorImage("lobby-stage-nether-icon", ResourceRoot + "Icons/nether-fortress");
            SetVectorImage("lobby-star-icon", ResourceRoot + "Icons/star");
            SetVectorImage("lobby-ready-icon", ResourceRoot + "Icons/traffic-light");
            SetVectorImage("lobby-ready-icon-right", ResourceRoot + "Icons/traffic-light");
            SetVectorImage("avatar-save-icon", ResourceRoot + "Icons/checkmark");
            SetVectorImage("settings-save-icon", ResourceRoot + "Icons/checkmark");
        }

        void ConfigureDecorativeMotion()
        {
            decorativeMotions = new[]
            {
                new DecorativeMotion(app.Q<VisualElement>("main-deco-spark"), new Vector2(7f, 5f), 9f, 0f, spin: true),
                new DecorativeMotion(app.Q<VisualElement>("main-deco-star"), new Vector2(9f, 12f), 5.2f, 0.7f, rotationAmplitude: 7f),
                new DecorativeMotion(app.Q<VisualElement>("main-deco-shine"), new Vector2(12f, 9f), 6.1f, 1.4f, rotationAmplitude: 8f),
                new DecorativeMotion(app.Q<VisualElement>("main-deco-swirl"), new Vector2(6f, 7f), 12f, 0.4f, spin: true),
                new DecorativeMotion(app.Q<VisualElement>("main-deco-flag"), new Vector2(8f, 5f), 3.8f, 1.1f, rotationAmplitude: 9f),
                new DecorativeMotion(app.Q<VisualElement>("main-photo-mj1"), new Vector2(8f, 10f), 6f, 0.2f, -10f, 4f),
                new DecorativeMotion(app.Q<VisualElement>("main-photo-mj2"), new Vector2(10f, 8f), 5.5f, 1.6f, 8f, 4f),
                new DecorativeMotion(app.Q<VisualElement>("main-photo-portrait"), new Vector2(7f, 7f), 15f, 0.8f, 6f, spin: true),
                new DecorativeMotion(app.Q<VisualElement>("lobby-deco-gamepad"), new Vector2(10f, 12f), 5f, 0.5f, rotationAmplitude: 6f),
                new DecorativeMotion(app.Q<VisualElement>("lobby-deco-spark"), new Vector2(6f, 7f), 11f, 1.2f, spin: true),
                new DecorativeMotion(app.Q<VisualElement>("avatar-deco-left"), new Vector2(6f, 7f), 10f, 0.9f, spin: true),
                new DecorativeMotion(app.Q<VisualElement>("avatar-deco-right"), new Vector2(10f, 12f), 5f, 0.1f, rotationAmplitude: 6f),
                new DecorativeMotion(app.Q<VisualElement>("avatar-preview"), new Vector2(4f, 10f), 3.5f, 0.6f, rotationAmplitude: 1.5f),
                new DecorativeMotion(app.Q<VisualElement>("settings-deco-gear"), new Vector2(5f, 5f), 12f, 0.3f, spin: true),
                new DecorativeMotion(app.Q<VisualElement>("settings-deco-spark"), new Vector2(9f, 12f), 5f, 1.8f, rotationAmplitude: 7f),
            };
            decorationMotion = app.schedule.Execute(AnimateDecorations).Every(16);
        }

        void AnimateDecorations()
        {
            if (decorativeMotions == null) return;
            float time = Time.realtimeSinceStartup;
            foreach (DecorativeMotion motion in decorativeMotions)
            {
                if (motion.Element == null) continue;
                float phase = (time / Mathf.Max(0.1f, motion.Period)) * Mathf.PI * 2f + motion.Phase;
                float x = Mathf.Sin(phase) * motion.Amplitude.x;
                float y = Mathf.Cos(phase * 0.83f) * motion.Amplitude.y;
                float rotation = motion.Spin
                    ? motion.BaseRotation + Mathf.Repeat(time / motion.Period, 1f) * 360f
                    : motion.BaseRotation + Mathf.Sin(phase * 0.71f) * motion.RotationAmplitude;
                motion.Element.style.translate = new Translate(
                    new Length(x, LengthUnit.Pixel), new Length(y, LengthUnit.Pixel), 0f);
                motion.Element.style.rotate = new Rotate(new Angle(rotation, AngleUnit.Degree));
            }
        }

        void BindButton(string name, Action action)
        {
            Button button = app.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        void ConfigureSlider(Slider slider, Label valueLabel)
        {
            if (slider == null) return;
            slider.lowValue = 0f;
            slider.highValue = 1f;
            slider.RegisterValueChangedCallback(evt => SetPercentage(valueLabel, evt.newValue));
        }

        void SetVectorImage(string name, string resourcePath)
        {
            Image image = app.Q<Image>(name);
            if (image == null) return;
            image.image = null;
            image.vectorImage = Resources.Load<VectorImage>(resourcePath);
            image.scaleMode = ScaleMode.ScaleToFit;
            image.tintColor = image.ClassListContains("icon-shadow") || image.ClassListContains("icon-shadow-inline")
                ? Ink
                : Color.white;
        }

        void SetTextureImage(string name, string resourcePath)
        {
            Image image = app.Q<Image>(name);
            if (image == null) return;
            image.vectorImage = null;
            image.image = Resources.Load<Texture2D>(resourcePath);
            image.scaleMode = ScaleMode.StretchToFill;
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

        void HideLegacyControls()
        {
            if (bootstrap != null)
            {
                SetLegacyActive(bootstrap.hostButton, false);
                SetLegacyActive(bootstrap.joinButton, false);
                SetLegacyActive(bootstrap.joinCodeInputField, false);
                SetLegacyActive(bootstrap.statusText, false);
            }
            roomSettingsUi?.SetVisible(false);
        }

        static void SetLegacyActive(Component component, bool active)
        {
            if (component != null) component.gameObject.SetActive(active);
        }

        public void ShowMain()
        {
            SetScreen(Screen.Main);
            HideLegacyControls();
            RefreshProfilePresentation();
            SetFeedback(mainFeedback, "방 만들기 또는 방 참가를 선택하세요.", Color.white);
        }

        public void ShowHostSetup()
        {
            SetScreen(Screen.Host);
            HideLegacyControls();
            RefreshHostSettings();
            SetFeedback(hostFeedback, "모드와 규칙을 정한 뒤 방을 만드세요.", Color.white);
        }

        public void ShowJoinSetup()
        {
            SetScreen(Screen.Join);
            HideLegacyControls();
            if (joinRoomCodeInput != null) joinRoomCodeInput.SetValueWithoutNotify(string.Empty);
            SetFeedback(joinFeedback, "친구에게 받은 방 코드를 입력하세요.", Ink);
        }

        public void ShowLobby(string roomCode, bool isHost)
        {
            SetScreen(Screen.Lobby);
            HideLegacyControls();
            if (lobbyRoomCode != null) lobbyRoomCode.text = string.IsNullOrWhiteSpace(roomCode) ? "연결 중" : roomCode;
            raceManager = FindFirstObjectByType<NetworkRaceManager>();
            RefreshLobbyPresentation();
            if (raceManager == null)
            {
                SetFeedback(lobbyStatus, isHost
                    ? "친구에게 위 방 코드를 알려주세요."
                    : "방에 입장했습니다. 연결 상태를 확인하는 중입니다.", Color.white);
            }
        }

        public void ShowAvatar()
        {
            SetScreen(Screen.Avatar);
            HideLegacyControls();
            draftAppearance = M2PlayerProfile.Appearance;
            if (avatarNameInput != null) avatarNameInput.SetValueWithoutNotify(M2PlayerProfile.DisplayName);
            RefreshAvatarPreview();
            SetFeedback(avatarFeedback, "외형과 이름을 고른 뒤 저장하세요.", Ink);
        }

        public void ShowSettings()
        {
            SetScreen(Screen.Settings);
            HideLegacyControls();
            draftGraphicsQuality = M2GameSettings.GraphicsQuality;
            draftLanguage = M2GameSettings.Language;
            if (settingsMasterSlider != null) settingsMasterSlider.SetValueWithoutNotify(M2GameSettings.MasterVolume);
            if (settingsBgmSlider != null) settingsBgmSlider.SetValueWithoutNotify(M2GameSettings.BgmVolume);
            if (settingsSfxSlider != null) settingsSfxSlider.SetValueWithoutNotify(M2GameSettings.SfxVolume);
            draftFullscreen = M2GameSettings.Fullscreen;
            RefreshSettingsPresentation();
            SetFeedback(settingsFeedback, "변경한 뒤 저장하면 다음 실행에도 유지됩니다.", Ink);
        }

        void OpenLocalRaceTest()
        {
            if (bootstrap != null && bootstrap.HasActiveSession)
            {
                SetFeedback(mainFeedback, "온라인 방을 나간 뒤 로컬 레이스 테스트를 실행하세요.", SoftPink);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(RaceTestSceneName))
            {
                SetFeedback(mainFeedback, "로컬 레이스 테스트 씬을 불러올 수 없습니다.", SoftPink);
                return;
            }

            SetFeedback(mainFeedback, "로컬 레이스 테스트를 시작합니다.", Color.white);
            SceneManager.LoadScene(RaceTestSceneName, LoadSceneMode.Single);
        }

        public void HideMenu()
        {
            SetScreen(Screen.Hidden);
            HideLegacyControls();
        }

        void SetScreen(Screen screen)
        {
            currentScreen = screen;
            if (app == null) return;
            app.style.display = screen == Screen.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (KeyValuePair<Screen, VisualElement> item in screens)
            {
                if (item.Value != null)
                {
                    item.Value.style.display = item.Key == screen ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        void HandleStatusChanged(string message)
        {
            Color color = IsFailure(message) ? SoftPink : currentScreen == Screen.Main || currentScreen == Screen.Host || currentScreen == Screen.Lobby
                ? Color.white
                : Ink;
            switch (currentScreen)
            {
                case Screen.Main:
                    SetFeedback(mainFeedback, message, color);
                    break;
                case Screen.Host:
                    SetFeedback(hostFeedback, message, color);
                    break;
                case Screen.Join:
                    SetFeedback(joinFeedback, message, color);
                    break;
                case Screen.Lobby:
                    SetFeedback(lobbyStatus, message, color);
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
                (message.Contains("실패") || message.Contains("끊어") || message.Contains("입력") || message.Contains("문제"));
        }

        void ToggleHostMode()
        {
            roomSettingsUi?.ToggleMode();
            RefreshHostSettings();
        }

        void CycleHostLapCount()
        {
            roomSettingsUi?.CycleItemLapCount();
            RefreshHostSettings();
        }

        void CycleHostVictoryCondition()
        {
            roomSettingsUi?.CycleItemVictoryCondition();
            RefreshHostSettings();
        }

        void RefreshHostSettings()
        {
            RaceMode mode = roomSettingsUi != null ? roomSettingsUi.selectedMode : RaceMode.Item;
            int laps = roomSettingsUi != null
                ? RaceModeRules.NormalizeItemLapCount(roomSettingsUi.selectedItemLapCount)
                : 3;
            VictoryCondition victory = roomSettingsUi != null
                ? roomSettingsUi.selectedItemVictoryCondition
                : VictoryCondition.SimpleFinish;
            bool itemMode = mode == RaceMode.Item;

            SetButtonLabel(hostModeButton, mode == RaceMode.Speed ? "모드 · 스피드전  ↻" : "모드 · 아이템전  ↻");
            SetButtonLabel(hostLapButton, itemMode ? $"바퀴 수 · {laps}바퀴  ↻" : "바퀴 수 · 5바퀴 (스피드전 고정)");
            SetButtonLabel(hostVictoryButton, itemMode
                ? $"승리 조건 · {(victory == VictoryCondition.StarBet ? "별점 내기" : "단순 완주")}  ↻"
                : "승리 조건 · 단순 완주 (스피드전 고정)");
            SetEnabled(hostLapButton, itemMode);
            SetEnabled(hostVictoryButton, itemMode);
        }

        void CreateRoom()
        {
            if (bootstrap == null || bootstrap.hostButton == null) return;
            SetFeedback(hostFeedback, "M2 방을 생성하는 중...", Color.white);
            bootstrap.hostButton.onClick.Invoke();
        }

        void JoinRoom()
        {
            if (bootstrap == null || bootstrap.joinButton == null || bootstrap.joinCodeInputField == null) return;
            string input = joinRoomCodeInput != null ? joinRoomCodeInput.value : string.Empty;
            if (!M2RoomCode.TryNormalize(input, out string roomCode))
            {
                SetFeedback(joinFeedback, "M2-1L4G 형식의 방 코드를 입력하세요.", SoftPink);
                return;
            }

            joinRoomCodeInput?.SetValueWithoutNotify(roomCode);
            bootstrap.joinCodeInputField.text = roomCode;
            SetFeedback(joinFeedback, $"방 {roomCode} 참가 중...", Ink);
            bootstrap.joinButton.onClick.Invoke();
        }

        void RefreshProfilePresentation()
        {
            M2AvatarAppearance appearance = M2PlayerProfile.Appearance;
            string name = M2PlayerProfile.TaggedDisplayName;
            if (mainRacerName != null) mainRacerName.text = name;
            ApplyAvatar(app.Q<VisualElement>("main-avatar"), appearance);
        }

        void SelectAvatarColor(int colorIndex)
        {
            draftAppearance = draftAppearance.WithBodyColor(colorIndex);
            RefreshAvatarPreview();
            SetFeedback(avatarFeedback, "미리보기에 선택한 색을 적용했습니다.", Purple);
        }

        void SelectAvatarPreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= AvatarPresets.Length) return;
            draftAppearance = AvatarPresets[presetIndex];
            avatarNameInput?.SetValueWithoutNotify(AvatarPresetNames[presetIndex]);
            RefreshAvatarPreview();
        }

        void SelectAvatarEyes(M2AvatarEyes eyes)
        {
            draftAppearance = draftAppearance.WithEyes(eyes);
            RefreshAvatarPreview();
        }

        void SelectAvatarMouth(M2AvatarMouth mouth)
        {
            draftAppearance = draftAppearance.WithMouth(mouth);
            RefreshAvatarPreview();
        }

        void SetAvatarCheeks(bool enabled)
        {
            draftAppearance = draftAppearance.WithCheeks(enabled);
            RefreshAvatarPreview();
        }

        void SetAvatarEars(bool enabled)
        {
            draftAppearance = draftAppearance.WithEars(enabled);
            RefreshAvatarPreview();
        }

        void SelectAvatarHat(M2AvatarHat hat)
        {
            draftAppearance = draftAppearance.WithHat(hat);
            RefreshAvatarPreview();
        }

        void SelectAvatarPlate(int plateIndex)
        {
            draftAppearance = draftAppearance.WithPlate(plateIndex);
            RefreshAvatarPreview();
        }

        void RefreshAvatarPreview()
        {
            string name = M2PlayerProfile.WithPlateTag(
                avatarNameInput != null ? avatarNameInput.value : M2PlayerProfile.DisplayName,
                draftAppearance.PlateIndex);
            ApplyAvatar(app.Q<VisualElement>("avatar-preview"), draftAppearance);
            if (avatarPreviewName != null) avatarPreviewName.text = name;
            if (avatarSummary != null)
            {
                avatarSummary.text = $"{M2PlayerProfile.ResolvePlateLabel(draftAppearance.PlateIndex)} · {EyesLabel(draftAppearance.Eyes)} · {MouthLabel(draftAppearance.Mouth)}";
            }
            for (int i = 0; i < avatarColorButtons.Length; i++)
            {
                Button button = avatarColorButtons[i];
                if (button == null) continue;
                button.style.backgroundColor = M2PlayerProfile.ResolveAvatarColor(i);
                SetSelected(button, i == draftAppearance.BodyColorIndex);
            }
            SetOption(app.Q<Button>("avatar-eyes-round"), draftAppearance.Eyes == M2AvatarEyes.Round, Mint);
            SetOption(app.Q<Button>("avatar-eyes-happy"), draftAppearance.Eyes == M2AvatarEyes.Happy, Mint);
            SetOption(app.Q<Button>("avatar-eyes-cool"), draftAppearance.Eyes == M2AvatarEyes.Cool, Mint);
            SetOption(app.Q<Button>("avatar-mouth-smile"), draftAppearance.Mouth == M2AvatarMouth.Smile, Yellow);
            SetOption(app.Q<Button>("avatar-mouth-open"), draftAppearance.Mouth == M2AvatarMouth.Open, Yellow);
            SetOption(app.Q<Button>("avatar-mouth-flat"), draftAppearance.Mouth == M2AvatarMouth.Flat, Yellow);
            SetOption(avatarCheeksButtons[0], draftAppearance.HasCheeks, SoftPink);
            SetOption(avatarCheeksButtons[1], !draftAppearance.HasCheeks, SoftPink);
            SetOption(avatarEarsButtons[0], draftAppearance.HasEars, Cyan);
            SetOption(avatarEarsButtons[1], !draftAppearance.HasEars, Cyan);
            for (int i = 0; i < avatarHatButtons.Length; i++)
            {
                SetOption(avatarHatButtons[i], i == (int)draftAppearance.Hat, Pink);
            }
            for (int i = 0; i < avatarPlateButtons.Length; i++)
            {
                SetOption(avatarPlateButtons[i], i == draftAppearance.PlateIndex, Cyan);
            }
            for (int i = 0; i < avatarPresetButtons.Length; i++)
            {
                Button presetButton = avatarPresetButtons[i];
                if (presetButton == null) continue;
                ApplyAvatar(presetButton.Q<VisualElement>(className: "avatar--preset"), AvatarPresets[i]);
                SetOption(presetButton, SameAppearance(draftAppearance, AvatarPresets[i]), Yellow);
            }
        }

        void SaveAvatar()
        {
            string name = avatarNameInput != null ? avatarNameInput.value : M2PlayerProfile.DisplayName;
            M2PlayerProfile.Save(name, draftAppearance);
            raceManager ??= FindFirstObjectByType<NetworkRaceManager>();
            raceManager?.RequestProfileUpdate();
            RefreshProfilePresentation();
            RefreshAvatarPreview();
            SetFeedback(avatarFeedback, "아바타 설정을 저장했습니다.", Purple);
        }

        void RefreshSettingsPresentation()
        {
            SetPercentage(settingsMasterValue, settingsMasterSlider != null ? settingsMasterSlider.value : M2GameSettings.MasterVolume);
            SetPercentage(settingsBgmValue, settingsBgmSlider != null ? settingsBgmSlider.value : M2GameSettings.BgmVolume);
            SetPercentage(settingsSfxValue, settingsSfxSlider != null ? settingsSfxSlider.value : M2GameSettings.SfxVolume);
            Color[] qualityColors = { Mint, Cyan, Yellow };
            for (int i = 0; i < settingsQualityButtons.Length; i++)
            {
                SetOption(settingsQualityButtons[i], i == (int)draftGraphicsQuality, qualityColors[i]);
            }
            SetOption(settingsLanguageButtons[0], draftLanguage == M2Language.Korean, SoftPink);
            SetOption(settingsLanguageButtons[1], draftLanguage == M2Language.English, Cyan);
            SetOption(settingsScreenModeButtons[0], draftFullscreen, Cyan);
            SetOption(settingsScreenModeButtons[1], !draftFullscreen, Cyan);
        }

        void SelectQuality(M2GraphicsQuality quality)
        {
            draftGraphicsQuality = quality;
            RefreshSettingsPresentation();
        }

        void SelectLanguage(M2Language language)
        {
            draftLanguage = language;
            RefreshSettingsPresentation();
        }

        void SelectScreenMode(bool fullscreen)
        {
            draftFullscreen = fullscreen;
            RefreshSettingsPresentation();
        }

        void ResetSettingsDraft()
        {
            if (settingsMasterSlider != null) settingsMasterSlider.SetValueWithoutNotify(M2GameSettings.DefaultMasterVolume);
            if (settingsBgmSlider != null) settingsBgmSlider.SetValueWithoutNotify(M2GameSettings.DefaultBgmVolume);
            if (settingsSfxSlider != null) settingsSfxSlider.SetValueWithoutNotify(M2GameSettings.DefaultSfxVolume);
            draftFullscreen = M2GameSettings.DefaultFullscreen;
            draftGraphicsQuality = M2GameSettings.DefaultGraphicsQuality;
            draftLanguage = M2GameSettings.DefaultLanguage;
            RefreshSettingsPresentation();
            SetFeedback(settingsFeedback, "기본값을 불러왔습니다. 저장하면 적용됩니다.", Purple);
        }

        void SaveSettings()
        {
            float master = settingsMasterSlider != null ? settingsMasterSlider.value : M2GameSettings.MasterVolume;
            float bgm = settingsBgmSlider != null ? settingsBgmSlider.value : M2GameSettings.BgmVolume;
            float sfx = settingsSfxSlider != null ? settingsSfxSlider.value : M2GameSettings.SfxVolume;
            M2GameSettings.Save(master, bgm, sfx, draftGraphicsQuality, draftFullscreen, draftLanguage);
            RefreshSettingsPresentation();
            SetFeedback(settingsFeedback, "환경설정을 저장했습니다.", Purple);
        }

        void RefreshLobbyPresentation()
        {
            bool localIsHost = IsLocalHost();
            if (lobbyLeaveButton != null)
            {
                SetEnabled(lobbyLeaveButton, bootstrap != null && bootstrap.HasActiveSession && !bootstrap.IsSessionOperationInProgress);
            }

            if (raceManager == null)
            {
                ApplyLobbyFallback(localIsHost);
                return;
            }

            NetworkRacerResult host = raceManager.HostRacer;
            NetworkRacerResult guest = raceManager.ClientRacer;
            M2AvatarAppearance hostAppearance = host.HasProfile
                ? host.Appearance
                : M2PlayerProfile.Appearance.WithBodyColor(1);
            M2AvatarAppearance guestAppearance = guest.HasProfile
                ? guest.Appearance
                : M2PlayerProfile.Appearance.WithBodyColor(2);
            string hostName = host.HasProfile ? host.DisplayName : localIsHost ? M2PlayerProfile.TaggedDisplayName : "방장";
            string guestName = guest.HasProfile ? guest.DisplayName : localIsHost ? "상대 접속 대기" : M2PlayerProfile.TaggedDisplayName;
            ApplyLobbyPlayer(app.Q<VisualElement>("lobby-host-avatar"), lobbyHostName, lobbyHostMeta, lobbyHostReady,
                hostAppearance, hostName, "방장", raceManager.HostReady, host.HasProfile || localIsHost);
            ApplyLobbyPlayer(app.Q<VisualElement>("lobby-guest-avatar"), lobbyGuestName, lobbyGuestMeta, lobbyGuestReady,
                guestAppearance, guestName, "참가자", raceManager.ClientReady, guest.HasProfile);

            RaceMode mode = raceManager.Mode;
            int laps = raceManager.TargetLapCount;
            VictoryCondition victory = raceManager.CurrentVictoryCondition;
            StageType stage = raceManager.SelectedStage;
            bool itemMode = mode == RaceMode.Item;
            bool hostCanEdit = localIsHost && raceManager.LobbyOpen;

            RefreshLobbyOptionButtons(mode, laps, victory, stage, hostCanEdit);
            if (lobbyAuthority != null) lobbyAuthority.text = hostCanEdit ? "방장이 설정을 변경할 수 있습니다." : "방장만 변경할 수 있습니다.";

            string modeDetail = mode == RaceMode.Speed
                ? "스피드전 · 5초 자동 휘발유 · 최고 100km/h"
                : $"아이템전 · {laps}바퀴 · {(victory == VictoryCondition.StarBet ? "별점 내기" : "단순 완주")}";
            if (lobbyRules != null) lobbyRules.text = $"{StageLabel(stage)} · {modeDetail}";

            bool hasOpponent = NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds.Count >= 2;
            bool localReady = localIsHost ? raceManager.HostReady : raceManager.ClientReady;
            bool opponentReady = localIsHost ? raceManager.ClientReady : raceManager.HostReady;
            SetButtonLabel(lobbyReadyButton, localReady ? "✓ 준비 취소" : "✓ 준비 완료");
            SetEnabled(lobbyReadyButton, raceManager.LobbyOpen && hasOpponent);
            if (lobbyReadyButton != null) lobbyReadyButton.style.backgroundColor = localReady ? Yellow : Mint;

            if (lobbyStatus != null)
            {
                lobbyStatus.text = !hasOpponent
                    ? "상대 레이서가 입장하면 준비 버튼으로 함께 시작할 수 있습니다."
                    : raceManager.BothPlayersReady
                        ? "두 레이서가 준비되었습니다. 레이스를 시작합니다!"
                        : localReady
                            ? "내 준비 완료 · 상대 레이서의 준비를 기다리는 중입니다."
                            : opponentReady
                                ? "상대 레이서가 준비했습니다. 현재 설정을 확인하고 준비하세요."
                                : "방장이 설정을 확정하면 두 레이서가 준비할 수 있습니다.";
                lobbyStatus.style.color = Color.white;
            }
        }

        void ApplyLobbyFallback(bool localIsHost)
        {
            RaceMode mode = roomSettingsUi != null ? roomSettingsUi.selectedMode : RaceMode.Item;
            int laps = roomSettingsUi != null ? RaceModeRules.NormalizeItemLapCount(roomSettingsUi.selectedItemLapCount) : 3;
            VictoryCondition victory = roomSettingsUi != null ? roomSettingsUi.selectedItemVictoryCondition : VictoryCondition.SimpleFinish;
            ApplyLobbyPlayer(app.Q<VisualElement>("lobby-host-avatar"), lobbyHostName, lobbyHostMeta, lobbyHostReady,
                M2PlayerProfile.Appearance, localIsHost ? M2PlayerProfile.TaggedDisplayName : "방장", "방장", false, localIsHost);
            ApplyLobbyPlayer(app.Q<VisualElement>("lobby-guest-avatar"), lobbyGuestName, lobbyGuestMeta, lobbyGuestReady,
                M2PlayerProfile.Appearance.WithBodyColor(2), localIsHost ? "상대 접속 대기" : M2PlayerProfile.TaggedDisplayName,
                "참가자", false, !localIsHost);
            RefreshLobbyOptionButtons(mode, laps, victory, fallbackLobbyStage, localIsHost);
            SetEnabled(lobbyReadyButton, false);
            if (lobbyAuthority != null)
            {
                lobbyAuthority.text = localIsHost
                    ? "연결 확인 중 · 방 설정은 지금 변경할 수 있습니다."
                    : "연결 정보를 확인하는 중입니다.";
            }
            if (lobbyRules != null) lobbyRules.text = $"{StageLabel(fallbackLobbyStage)} · {(mode == RaceMode.Speed ? "스피드전 · 5바퀴" : $"아이템전 · {laps}바퀴 · {(victory == VictoryCondition.StarBet ? "별점 내기" : "단순 완주")}")}";
            if (lobbyStatus != null && localIsHost) lobbyStatus.text = "친구에게 위 방 코드를 알려주세요. 연결 중에도 방 설정을 바꿀 수 있습니다.";
        }

        void RefreshLobbyOptionButtons(RaceMode mode, int laps, VictoryCondition victory, StageType stage, bool canEdit)
        {
            bool itemMode = mode == RaceMode.Item;
            SetOption(lobbyModeButtons[0], itemMode, Yellow);
            SetOption(lobbyModeButtons[1], !itemMode, Cyan);
            foreach (Button button in lobbyModeButtons) SetEnabled(button, canEdit);

            int selectedLaps = itemMode ? RaceModeRules.NormalizeItemLapCount(laps) : 5;
            int[] lapValues = { 1, 3, 5 };
            for (int i = 0; i < lobbyLapButtons.Length; i++)
            {
                SetOption(lobbyLapButtons[i], lapValues[i] == selectedLaps, Mint);
                bool speedModeFixedLap = !itemMode && lapValues[i] == 5;
                SetEnabled(lobbyLapButtons[i], canEdit && (itemMode || speedModeFixedLap));
                lobbyLapButtons[i]?.EnableInClassList("is-speed-locked", !itemMode && lapValues[i] != 5);
            }

            VictoryCondition selectedVictory = itemMode ? victory : VictoryCondition.SimpleFinish;
            SetOption(lobbyVictoryButtons[0], selectedVictory == VictoryCondition.SimpleFinish, Purple);
            SetOption(lobbyVictoryButtons[1], selectedVictory == VictoryCondition.StarBet, Purple);
            foreach (Button button in lobbyVictoryButtons) SetEnabled(button, canEdit && itemMode);

            Color[] stageColors = { SoftPink, Cyan, Purple };
            for (int i = 0; i < lobbyStageButtons.Length; i++)
            {
                SetOption(lobbyStageButtons[i], i == (int)stage, stageColors[i]);
                SetEnabled(lobbyStageButtons[i], canEdit);
            }
        }

        void SelectLobbyMode(RaceMode mode)
        {
            ReadLobbySelection(out _, out int laps, out VictoryCondition victory, out StageType stage);
            ApplyLobbySelection(mode, laps, victory, stage);
        }

        void SelectLobbyLapCount(int laps)
        {
            ReadLobbySelection(out RaceMode mode, out _, out VictoryCondition victory, out StageType stage);
            if (mode == RaceMode.Speed && laps != 5) return;
            ApplyLobbySelection(mode, laps, victory, stage);
        }

        void SelectLobbyVictory(VictoryCondition victory)
        {
            ReadLobbySelection(out RaceMode mode, out int laps, out _, out StageType stage);
            ApplyLobbySelection(mode, laps, victory, stage);
        }

        void ReadLobbySelection(out RaceMode mode, out int laps, out VictoryCondition victory, out StageType stage)
        {
            mode = raceManager != null ? raceManager.Mode : roomSettingsUi != null ? roomSettingsUi.selectedMode : RaceMode.Item;
            laps = raceManager != null ? raceManager.TargetLapCount : roomSettingsUi != null ? roomSettingsUi.selectedItemLapCount : 3;
            victory = raceManager != null ? raceManager.CurrentVictoryCondition : roomSettingsUi != null ? roomSettingsUi.selectedItemVictoryCondition : VictoryCondition.SimpleFinish;
            stage = raceManager != null ? raceManager.SelectedStage : fallbackLobbyStage;
        }

        void ApplyLobbySelection(RaceMode mode, int laps, VictoryCondition victory, StageType stage)
        {
            if (!IsLocalHost()) return;
            if (mode == RaceMode.Speed)
            {
                laps = 5;
                victory = VictoryCondition.SimpleFinish;
            }
            else
            {
                laps = RaceModeRules.NormalizeItemLapCount(laps);
            }

            fallbackLobbyStage = stage;
            if (roomSettingsUi != null)
            {
                roomSettingsUi.selectedMode = mode;
                roomSettingsUi.selectedItemLapCount = laps;
                roomSettingsUi.selectedItemVictoryCondition = victory;
            }
            if (raceManager != null)
            {
                ApplyLobbySettings(mode, laps, victory, stage);
            }
            else
            {
                fallbackLobbySettingsDirty = true;
                RefreshLobbyPresentation();
            }
        }

        void ApplyLobbyPlayer(VisualElement avatar, Label nameLabel, Label metaLabel, Label readyLabel,
            M2AvatarAppearance appearance, string displayName, string role, bool ready, bool connected)
        {
            ApplyAvatar(avatar, appearance);
            if (nameLabel != null) nameLabel.text = displayName;
            if (metaLabel != null) metaLabel.text = $"{role} · {(ready ? "준비 완료" : "준비 중")}";
            if (readyLabel != null)
            {
                readyLabel.text = connected ? ready ? "✓ 준비 완료" : "준비 중" : "상대 접속 대기";
                readyLabel.style.width = 168f;
                readyLabel.style.minWidth = 168f;
                readyLabel.style.height = 40f;
                readyLabel.style.minHeight = 40f;
                readyLabel.style.flexShrink = 0f;
                readyLabel.style.whiteSpace = WhiteSpace.NoWrap;
                readyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                readyLabel.RemoveFromClassList("ready-pill--ready");
                readyLabel.RemoveFromClassList("ready-pill--waiting");
                readyLabel.AddToClassList(ready && connected ? "ready-pill--ready" : "ready-pill--waiting");
            }
        }

        void CycleLobbyMode()
        {
            if (!IsLocalHost() || raceManager == null) return;
            ApplyLobbySettings(raceManager.Mode == RaceMode.Item ? RaceMode.Speed : RaceMode.Item,
                raceManager.TargetLapCount, raceManager.CurrentVictoryCondition, raceManager.SelectedStage);
        }

        void CycleLobbyLapCount()
        {
            if (!IsLocalHost() || raceManager == null || raceManager.Mode == RaceMode.Speed) return;
            int laps = raceManager.TargetLapCount == 1 ? 3 : raceManager.TargetLapCount == 3 ? 5 : 1;
            ApplyLobbySettings(raceManager.Mode, laps, raceManager.CurrentVictoryCondition, raceManager.SelectedStage);
        }

        void CycleLobbyVictoryCondition()
        {
            if (!IsLocalHost() || raceManager == null || raceManager.Mode == RaceMode.Speed) return;
            VictoryCondition next = raceManager.CurrentVictoryCondition == VictoryCondition.SimpleFinish
                ? VictoryCondition.StarBet
                : VictoryCondition.SimpleFinish;
            ApplyLobbySettings(raceManager.Mode, raceManager.TargetLapCount, next, raceManager.SelectedStage);
        }

        void SelectLobbyStage(StageType stage)
        {
            ReadLobbySelection(out RaceMode mode, out int laps, out VictoryCondition victory, out _);
            ApplyLobbySelection(mode, laps, victory, stage);
        }

        void ApplyLobbySettings(RaceMode mode, int laps, VictoryCondition victory, StageType stage)
        {
            if (raceManager == null) return;
            fallbackLobbyStage = stage;
            raceManager.RequestLobbySettings(mode, laps, victory, stage);
            if (roomSettingsUi != null)
            {
                roomSettingsUi.selectedMode = mode;
                roomSettingsUi.selectedItemLapCount = laps;
                roomSettingsUi.selectedItemVictoryCondition = victory;
            }
        }

        void ToggleLobbyReady()
        {
            if (raceManager == null) return;
            bool localReady = IsLocalHost() ? raceManager.HostReady : raceManager.ClientReady;
            raceManager.RequestLobbyReady(!localReady);
        }

        void LeaveLobby()
        {
            if (bootstrap == null || !bootstrap.HasActiveSession)
            {
                ShowMain();
                return;
            }
            SetEnabled(lobbyLeaveButton, false);
            SetFeedback(lobbyStatus, "방을 정리하고 메인으로 돌아가는 중입니다...", Color.white);
            bootstrap.ExitSessionToMain();
        }

        bool IsLocalHost()
        {
            if (NetworkManager.Singleton != null) return NetworkManager.Singleton.IsHost;
            return bootstrap != null && bootstrap.IsHostingSession;
        }

        static void SetFeedback(Label label, string message, Color color)
        {
            if (label == null) return;
            label.text = message;
            label.style.color = color;
        }

        static void SetButtonLabel(Button button, string value)
        {
            if (button == null) return;
            Label label = button.Q<Label>("button-label");
            if (label != null) label.text = value;
        }

        static void SetEnabled(Button button, bool enabled)
        {
            button?.SetEnabled(enabled);
        }

        static void SetSelected(VisualElement element, bool selected)
        {
            if (element == null) return;
            if (selected) element.AddToClassList("is-selected");
            else element.RemoveFromClassList("is-selected");
        }

        static void SetOption(Button button, bool selected, Color selectedColor)
        {
            if (button == null) return;
            SetSelected(button, selected);
            button.style.backgroundColor = selected ? selectedColor : Muted;
        }

        static bool SameAppearance(M2AvatarAppearance left, M2AvatarAppearance right)
        {
            return left.BodyColorIndex == right.BodyColorIndex && left.Eyes == right.Eyes &&
                   left.Mouth == right.Mouth && left.HasCheeks == right.HasCheeks &&
                   left.HasEars == right.HasEars && left.Hat == right.Hat &&
                   left.PlateIndex == right.PlateIndex;
        }

        static void SetPercentage(Label label, float value)
        {
            if (label != null) label.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString();
        }

        static string StageLabel(StageType stage) => stage switch
        {
            StageType.BikiniCity => "비키니시티",
            StageType.AfricaTv => "아프리카TV",
            StageType.NetherFortress => "네더요새",
            _ => "비키니시티",
        };

        static string EyesLabel(M2AvatarEyes eyes) => eyes switch
        {
            M2AvatarEyes.Happy => "웃는눈",
            M2AvatarEyes.Cool => "선글라스",
            _ => "방울눈",
        };

        static string MouthLabel(M2AvatarMouth mouth) => mouth switch
        {
            M2AvatarMouth.Open => "우와 입",
            M2AvatarMouth.Flat => "시크 입",
            _ => "방긋 입",
        };

        static string HatLabel(M2AvatarHat hat) => hat switch
        {
            M2AvatarHat.Cap => "캡",
            M2AvatarHat.Crown => "왕관",
            _ => "없음",
        };

        static VectorImage crownVectorImage;

        static void ApplyAvatar(VisualElement avatar, M2AvatarAppearance appearance)
        {
            if (avatar == null) return;
            Color color = M2PlayerProfile.ResolveAvatarColor(appearance.BodyColorIndex);
            VisualElement body = avatar.Q<VisualElement>("avatar-body");
            VisualElement leftEar = avatar.Q<VisualElement>("avatar-ear-left");
            VisualElement rightEar = avatar.Q<VisualElement>("avatar-ear-right");
            VisualElement eyes = avatar.Q<VisualElement>("avatar-eyes");
            VisualElement cheeks = avatar.Q<VisualElement>("avatar-cheeks");
            VisualElement mouth = avatar.Q<VisualElement>("avatar-mouth");
            VisualElement capTop = avatar.Q<VisualElement>("avatar-hat-cap-top");
            VisualElement capBrim = avatar.Q<VisualElement>("avatar-hat-cap-brim");
            Image crown = avatar.Q<Image>("avatar-hat-crown");
            Label plate = avatar.Q<Label>("avatar-plate");

            if (body != null) body.style.backgroundColor = color;
            if (leftEar != null)
            {
                leftEar.style.backgroundColor = color;
                leftEar.style.display = appearance.HasEars ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (rightEar != null)
            {
                rightEar.style.backgroundColor = color;
                rightEar.style.display = appearance.HasEars ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (eyes != null)
            {
                eyes.RemoveFromClassList("avatar-eyes--happy");
                eyes.RemoveFromClassList("avatar-eyes--cool");
                if (appearance.Eyes == M2AvatarEyes.Happy) eyes.AddToClassList("avatar-eyes--happy");
                if (appearance.Eyes == M2AvatarEyes.Cool) eyes.AddToClassList("avatar-eyes--cool");

                // The Figma sunglasses use one cyan and one pink lens; USS cannot address the
                // second child, so tint the lenses here and clear the override otherwise.
                bool cool = appearance.Eyes == M2AvatarEyes.Cool;
                int lensIndex = 0;
                foreach (VisualElement lens in eyes.Children())
                {
                    if (cool)
                    {
                        lens.style.backgroundColor = lensIndex == 0
                            ? new Color(0.545f, 0.886f, 1f)
                            : new Color(1f, 0.616f, 0.878f);
                    }
                    else
                    {
                        lens.style.backgroundColor = StyleKeyword.Null;
                    }
                    lensIndex++;
                }
            }
            if (cheeks != null) cheeks.style.display = appearance.HasCheeks ? DisplayStyle.Flex : DisplayStyle.None;
            if (mouth != null)
            {
                mouth.RemoveFromClassList("avatar-mouth--open");
                mouth.RemoveFromClassList("avatar-mouth--flat");
                if (appearance.Mouth == M2AvatarMouth.Open) mouth.AddToClassList("avatar-mouth--open");
                if (appearance.Mouth == M2AvatarMouth.Flat) mouth.AddToClassList("avatar-mouth--flat");
            }
            bool capVisible = appearance.Hat == M2AvatarHat.Cap;
            bool crownVisible = appearance.Hat == M2AvatarHat.Crown;
            if (capTop != null) capTop.style.display = capVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (capBrim != null) capBrim.style.display = capVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (crown != null)
            {
                if (crown.vectorImage == null)
                {
                    if (crownVectorImage == null) crownVectorImage = Resources.Load<VectorImage>(ResourceRoot + "Icons/crown");
                    crown.vectorImage = crownVectorImage;
                    crown.scaleMode = ScaleMode.ScaleToFit;
                }
                crown.style.display = crownVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (plate != null) plate.text = M2PlayerProfile.ResolvePlateLabel(appearance.PlateIndex);
        }
    }
}
