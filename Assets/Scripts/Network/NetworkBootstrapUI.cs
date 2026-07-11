using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace M2.Network
{
    // Minimal Host/Join UI for Milestone 1 — no Relay/Lobby yet (see CLAUDE.md's Netcode
    // section), just a direct UnityTransport connection defaulting to 127.0.0.1 so two Editor
    // instances (or an Editor + a Development Build) on the same machine can connect to each
    // other for manual testing.
    //
    // ConnectionApproval is turned on purely so a connecting client can be approved/create its
    // player object at all under this project's config — NOT for spawn placement. Placement
    // (left/right so 2 players don't spawn stacked on each other) used to be set here via
    // ConnectionApprovalResponse.Position, but that value's replication to remote observers
    // turned out unreliable in practice (playtester feedback: "스폰 위치는 안고쳐졌어" — both
    // cars kept spawning in the same spot even after fixing the side-selection logic itself).
    // Movement is owner-authoritative (see OwnerAuthoritativeNetworkTransform), so only the
    // OWNING client's transform writes are treated as authoritative — NetworkVehicleSync.
    // OnNetworkSpawn sets the actual spawn position now, since it runs on the owner.
    public class NetworkBootstrapUI : MonoBehaviour
    {
        public InputField ipInputField;
        public Button hostButton;
        public Button joinButton;
        public Text statusText;

        [Tooltip("UnityTransport 포트. 방화벽에서 이 포트가 막혀 있으면 같은 기기가 아닌 경우 연결이 안 될 수 있음.")]
        public ushort port = 7777;

        void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(StartHost);
            if (joinButton != null) joinButton.onClick.AddListener(StartClient);

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.NetworkConfig.ConnectionApproval = true;
                networkManager.ConnectionApprovalCallback = ApproveConnection;
            }
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
        }

        void StartHost()
        {
            SetConnectionPort();
            NetworkManager.Singleton.StartHost();
            SetStatus("호스팅 중...");
        }

        void StartClient()
        {
            string ip = ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text)
                ? ipInputField.text.Trim()
                : "127.0.0.1";

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(ip, port);

            NetworkManager.Singleton.StartClient();
            SetStatus($"{ip}:{port} 접속 시도 중...");
        }

        void SetConnectionPort()
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", port);
        }

        void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
