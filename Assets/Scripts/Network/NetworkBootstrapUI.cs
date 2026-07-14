using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using M2.UI;

namespace M2.Network
{
    /// <summary>
    /// Creates and joins private two-player sessions through Unity Lobby + Relay. The Multiplayer
    /// Services package configures UnityTransport and starts NGO after the session operation
    /// succeeds, so the rest of the race remains identical to the direct-IP milestones.
    /// </summary>
    public class NetworkBootstrapUI : MonoBehaviour
    {
        [FormerlySerializedAs("ipInputField")]
        public InputField joinCodeInputField;
        public Button hostButton;
        public Button joinButton;
        public Text statusText;

        public event Action<string> StatusChanged;
        public event Action<string, bool> SessionReady;

        public bool HasActiveSession => activeSession != null;
        public string ActiveRoomCode => activeSession != null ? activeSession.Code : string.Empty;
        public bool IsHostingSession => activeSession != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public bool IsSessionOperationInProgress => busy;

        bool subscribed;
        bool busy;
        ISession activeSession;
        RoomSettingsUI roomSettingsUi;
        NetworkMenuUI menuUi;

        void Awake()
        {
            ConfigureVisibleUi();
            Canvas canvas = ResolveCanvas();
            if (canvas != null)
            {
                roomSettingsUi = canvas.GetComponent<RoomSettingsUI>();
                if (roomSettingsUi == null) roomSettingsUi = canvas.gameObject.AddComponent<RoomSettingsUI>();

                menuUi = canvas.GetComponent<NetworkMenuUI>();
                if (menuUi == null) menuUi = canvas.gameObject.AddComponent<NetworkMenuUI>();
                menuUi.Initialize(this, roomSettingsUi);
            }
            if (hostButton != null) hostButton.onClick.AddListener(StartHost);
            if (joinButton != null) joinButton.onClick.AddListener(StartClient);
        }

        Canvas ResolveCanvas()
        {
            if (hostButton != null)
            {
                Canvas buttonCanvas = hostButton.GetComponentInParent<Canvas>();
                if (buttonCanvas != null) return buttonCanvas;
            }
            if (joinButton != null)
            {
                Canvas buttonCanvas = joinButton.GetComponentInParent<Canvas>();
                if (buttonCanvas != null) return buttonCanvas;
            }
            if (joinCodeInputField != null)
            {
                Canvas inputCanvas = joinCodeInputField.GetComponentInParent<Canvas>();
                if (inputCanvas != null) return inputCanvas;
            }
            return FindFirstObjectByType<Canvas>();
        }

        void Start()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || subscribed) return;

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            subscribed = true;
        }

        void OnDestroy()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !subscribed) return;
            networkManager.OnServerStarted -= HandleServerStarted;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        void ConfigureVisibleUi()
        {
            SetButtonLabel(hostButton, "방 만들기");
            SetButtonLabel(joinButton, "방 참가");
            if (statusText != null)
            {
                UiTypography.Apply(statusText);
                statusText.rectTransform.sizeDelta = new Vector2(900f, 84f);
                statusText.fontSize = 30;
                statusText.alignment = TextAnchor.UpperCenter;
            }
            if (joinCodeInputField == null) return;

            joinCodeInputField.text = string.Empty;
            joinCodeInputField.characterLimit = 12;
            UiTypography.Apply(joinCodeInputField.textComponent);
            if (joinCodeInputField.placeholder is Text placeholder)
            {
                UiTypography.Apply(placeholder);
                placeholder.text = "방 코드";
            }
        }

        static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                UiTypography.Apply(text);
                text.text = label;
            }
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            bool hasRoom = networkManager == null || !networkManager.IsServer ||
                networkManager.ConnectedClientsIds.Count < 2;

            response.Approved = hasRoom;
            response.CreatePlayerObject = hasRoom;
            if (!hasRoom) response.Reason = "방이 가득 찼습니다.";
        }

        async void StartHost()
        {
            if (busy) return;
            await RunSessionOperation(async () =>
            {
                roomSettingsUi?.ApplyTo(FindFirstObjectByType<M2.Core.GameManager>());
                SetStatus("온라인 서비스 연결 중...");
                await EnsureServicesReadyAsync();

                SetStatus("Relay 방 생성 중...");
                SessionOptions options = new SessionOptions
                {
                    MaxPlayers = 2,
                    IsPrivate = true,
                    Name = "M2 Racing 1v1"
                }.WithRelayNetwork();

                activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                SetStatus($"방 코드: {activeSession.Code}  ·  상대를 기다리는 중...");
                SessionReady?.Invoke(activeSession.Code, true);
                if (hostButton != null) hostButton.gameObject.SetActive(false);
                if (joinCodeInputField != null) joinCodeInputField.gameObject.SetActive(false);
                if (joinButton != null) joinButton.gameObject.SetActive(false);
                roomSettingsUi?.SetVisible(false);
            });
        }

        async void StartClient()
        {
            if (busy) return;
            string code = joinCodeInputField != null
                ? joinCodeInputField.text.Trim().ToUpperInvariant()
                : string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("방 코드를 입력하세요.");
                return;
            }

            await RunSessionOperation(async () =>
            {
                SetStatus("온라인 서비스 연결 중...");
                await EnsureServicesReadyAsync();
                SetStatus($"방 {code} 참가 중...");
                activeSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                SessionReady?.Invoke(activeSession.Code, false);
                roomSettingsUi?.SetVisible(false);
            });
        }

        async Task RunSessionOperation(Func<Task> operation)
        {
            busy = true;
            SetButtonsInteractable(false);
            try
            {
                await operation();
            }
            catch (Exception exception)
            {
                SetStatus($"연결 실패: {exception.Message}");
                SetButtonsInteractable(true);
            }
            finally
            {
                busy = false;
            }
        }

        static async Task EnsureServicesReadyAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                InitializationOptions options = new InitializationOptions();
                options.SetProfile(BuildProfileName());
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        // ParrelSync clones share PlayerPrefs but live at different project paths. A stable profile
        // per path prevents both Editor instances from reusing the same anonymous player token.
        static string BuildProfileName()
        {
            uint hash = 2166136261;
            string path = Application.dataPath.ToLowerInvariant();
            for (int i = 0; i < path.Length; i++)
            {
                hash ^= path[i];
                hash *= 16777619;
            }
            return $"m2-{hash:x8}";
        }

        void HandleServerStarted()
        {
            // Keep the host's code visible until the remote player arrives.
            if (activeSession != null) SetStatus($"방 코드: {activeSession.Code}  ·  상대를 기다리는 중...");
        }

        void HandleClientConnected(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null) return;

            bool localClientConnected = !networkManager.IsHost && clientId == networkManager.LocalClientId;
            bool remoteJoinedHost = networkManager.IsHost && clientId != networkManager.LocalClientId;
            if (localClientConnected || remoteJoinedHost)
            {
                // Stay on the formal lobby until both racers explicitly confirm readiness.
                // Hiding it here used to skip the host's final rule/stage review entirely.
                if (menuUi != null) menuUi.ShowLobby(ActiveRoomCode, networkManager.IsHost);
                else HideConnectionUi();
            }
        }

        void HandleClientDisconnected(ulong clientId)
        {
            // Session API teardown deliberately stops NGO after the room is deleted/left. That
            // disconnect is expected, so do not briefly reopen the lobby with a false failure
            // message while the normal exit flow is still finishing.
            if (busy) return;

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null) return;

            if (networkManager.IsHost && clientId != networkManager.LocalClientId)
            {
                string message = activeSession != null
                    ? $"방 코드: {activeSession.Code}  ·  상대 연결이 끊어졌습니다."
                    : "상대 연결이 끊어졌습니다.";
                if (menuUi != null) menuUi.ShowLobby(ActiveRoomCode, true);
                else if (statusText != null) statusText.gameObject.SetActive(true);
                SetStatus(message);
            }
            else if (clientId == networkManager.LocalClientId)
            {
                ShowConnectionUi();
                SetStatus("연결이 끊어졌습니다. 다시 시도할 수 있습니다.");
            }
        }

        void HideConnectionUi()
        {
            if (menuUi != null)
            {
                menuUi.HideMenu();
                return;
            }
            if (hostButton != null) hostButton.gameObject.SetActive(false);
            if (joinButton != null) joinButton.gameObject.SetActive(false);
            if (joinCodeInputField != null) joinCodeInputField.gameObject.SetActive(false);
            if (statusText != null) statusText.gameObject.SetActive(false);
            roomSettingsUi?.SetVisible(false);
        }

        void ShowConnectionUi()
        {
            if (menuUi != null)
            {
                menuUi.ShowMain();
                SetButtonsInteractable(true);
                return;
            }
            if (hostButton != null) hostButton.gameObject.SetActive(true);
            if (joinButton != null) joinButton.gameObject.SetActive(true);
            if (joinCodeInputField != null) joinCodeInputField.gameObject.SetActive(true);
            if (statusText != null) statusText.gameObject.SetActive(true);
            roomSettingsUi?.SetVisible(true);
            SetButtonsInteractable(true);
        }

        /// <summary>
        /// Leaves the current Relay/NGO race from a result card or the formal lobby. The
        /// Multiplayer session owns the NGO lifecycle, so it must be asked to stop the network
        /// before the local manager is touched. Directly calling NetworkManager.Shutdown here
        /// makes the package observe an out-of-band shutdown and leaves a warning on exit.
        /// </summary>
        public async void ExitSessionToMain()
        {
            if (busy) return;

            busy = true;
            SetButtonsInteractable(false);
            ISession session = activeSession;
            activeSession = null;

            try
            {
                if (session != null)
                {
                    if (session.IsHost)
                    {
                        await session.AsHost().DeleteAsync();
                    }
                    else
                    {
                        await session.LeaveAsync();
                    }
                }
                else
                {
                    ShutdownUnmanagedNetwork();
                }

                if (this == null) return;
                ShowConnectionUi();
                SetStatus("방을 나왔습니다. 새 방을 만들거나 방 코드로 참가할 수 있습니다.");
            }
            catch (Exception exception)
            {
                // A direct transport can exist in legacy or failed session paths. Keep this
                // fallback narrow so normal Relay sessions always use their owned shutdown path.
                ShutdownUnmanagedNetwork();
                if (this == null) return;
                ShowConnectionUi();
                SetStatus($"방 종료 중 문제가 발생했습니다: {exception.Message}");
            }
            finally
            {
                if (this != null) busy = false;
            }
        }

        static void ShutdownUnmanagedNetwork()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        void SetButtonsInteractable(bool interactable)
        {
            if (hostButton != null) hostButton.interactable = interactable;
            if (joinButton != null) joinButton.interactable = interactable;
        }

        void SetStatus(string message)
        {
            if (statusText != null)
            {
                bool hasRoomCode = message != null && message.StartsWith("방 코드:", StringComparison.Ordinal);
                statusText.fontSize = hasRoomCode ? 42 : 30;
                statusText.fontStyle = hasRoomCode ? FontStyle.Bold : FontStyle.Normal;
                statusText.color = hasRoomCode ? new Color(1f, 0.851f, 0.239f) : Color.white;
                statusText.text = message;
            }
            StatusChanged?.Invoke(message);
        }
    }
}
