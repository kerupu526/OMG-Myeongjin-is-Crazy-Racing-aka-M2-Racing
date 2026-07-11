using Unity.Netcode;
using UnityEngine;

namespace M2.Network
{
    // Sits in the networked race scene and spawns the single NetworkRaceManager once the host's
    // server starts. The race manager is a spawned prefab (not an in-scene NetworkObject) because
    // NGO scene management is off (Milestone 1's EnableSceneManagement=false), under which
    // in-scene NetworkObjects don't spawn reliably — spawning a registered prefab from the host
    // is the same supported path the player vehicle already uses.
    public class NetworkRaceBootstrap : MonoBehaviour
    {
        [Tooltip("NetworkRaceManager가 붙어있는 프리팹. 런타임에 AddNetworkPrefab으로 등록됨(호스트·클라이언트 양쪽).")]
        public GameObject raceManagerPrefab;

        bool spawned;
        bool prefabRegistered;
        bool serverStartedHooked;

        // Registration and event hookup live in Start(), not OnEnable/Awake: NetworkManager sets
        // NetworkManager.Singleton in its own OnEnable, and relying on same-GameObject component
        // OnEnable ordering to read it was fragile — a null Singleton there made the earlier
        // OnEnable-based version silently return and never register/spawn (playtester: HUD stuck
        // on "상대를 기다리는 중...", i.e. the race manager never appeared). Start() runs after
        // every component's Awake AND OnEnable, so Singleton is guaranteed set, and it still runs
        // at scene load — well before the user clicks Host/Join — so the AddNetworkPrefab happens
        // before any connection on both sides, as NGO requires.
        void Start()
        {
            RegisterPrefab();

            var manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnServerStarted += HandleServerStarted;
                serverStartedHooked = true;
                if (manager.IsServer) HandleServerStarted();
            }
        }

        void OnDestroy()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null && serverStartedHooked) manager.OnServerStarted -= HandleServerStarted;
        }

        // Belt-and-suspenders: if the OnServerStarted event was somehow missed (e.g. hook order),
        // spawn as soon as we observe the server running. Server-only, one-shot.
        void Update()
        {
            if (spawned) return;
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer && manager.IsListening) HandleServerStarted();
        }

        void RegisterPrefab()
        {
            if (prefabRegistered) return;
            var manager = NetworkManager.Singleton;
            if (manager == null || raceManagerPrefab == null) return;
            if (raceManagerPrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogWarning("M2Net: raceManagerPrefab에 NetworkObject가 없음 — 등록/스폰 불가.");
                return;
            }
            // AddNetworkPrefab must run at runtime on BOTH host and client before connecting so
            // both agree on the prefab's GlobalObjectIdHash. Editor-time NetworkConfig.Prefabs.Add()
            // doesn't persist (NetworkPrefabs.m_Prefabs is [NonSerialized]).
            manager.AddNetworkPrefab(raceManagerPrefab);
            prefabRegistered = true;
            Debug.Log("M2Net: raceManagerPrefab 등록 완료 (AddNetworkPrefab).");
        }

        void HandleServerStarted()
        {
            if (spawned) return;
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;
            if (raceManagerPrefab == null)
            {
                Debug.LogWarning("M2Net: raceManagerPrefab이 비어있음 — 레이스 매니저가 스폰되지 않음.");
                return;
            }

            RegisterPrefab(); // ensure registered even if Start() hadn't yet

            spawned = true;
            GameObject instance = Instantiate(raceManagerPrefab);
            instance.GetComponent<NetworkObject>().Spawn();
            Debug.Log("M2Net: NetworkRaceManager 스폰됨.");
        }
    }
}
