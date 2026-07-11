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
    // Also owns spawn placement: with ConnectionApproval off (the NGO default), both players'
    // NetworkVehicle instances would spawn stacked on top of each other at the origin. Turning
    // ConnectionApproval on and placing each connecting client on alternating sides is enough to
    // avoid that for exactly 2 players.
    public class NetworkBootstrapUI : MonoBehaviour
    {
        public InputField ipInputField;
        public Button hostButton;
        public Button joinButton;
        public Text statusText;

        [Tooltip("UnityTransport 포트. 방화벽에서 이 포트가 막혀 있으면 같은 기기가 아닌 경우 연결이 안 될 수 있음.")]
        public ushort port = 7777;

        [Tooltip("두 플레이어가 스폰 시 겹치지 않도록 좌우로 벌려두는 거리(미터). Milestone 1 한정 — 실제 트랙/스폰 지점은 다음 라운드 범위.")]
        public float spawnSideOffset = 3f;

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
            NetworkManager networkManager = NetworkManager.Singleton;
            // ConnectedClientsIds doesn't yet include the client currently being approved, so
            // the first approval sees 0 already-connected clients (→ left side), the second
            // sees 1 (→ right side) — exactly the alternation 2 players need.
            float side = networkManager.ConnectedClientsIds.Count % 2 == 0 ? -1f : 1f;

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = new Vector3(side * spawnSideOffset, 0.5f, 0f);
            response.Rotation = Quaternion.identity;
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
