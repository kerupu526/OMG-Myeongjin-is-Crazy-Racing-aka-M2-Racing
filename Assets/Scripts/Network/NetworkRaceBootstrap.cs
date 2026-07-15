using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace M2.Network
{
    // Sits in the networked race scene and spawns the single NetworkRaceManager once the host's
    // server starts. The race manager is a spawned prefab (not an in-scene NetworkObject) because
    // NGO scene management is off (Milestone 1's EnableSceneManagement=false), under which
    // in-scene NetworkObjects don't spawn reliably — spawning a registered prefab from the host
    // is the same supported path the player vehicle already uses.
    public class NetworkRaceBootstrap : MonoBehaviour
    {
        const string RaceSceneName = "NetworkRace";

        [Tooltip("NetworkRaceManager가 붙어있는 프리팹. 런타임에 AddNetworkPrefab으로 등록됨(호스트·클라이언트 양쪽).")]
        public GameObject raceManagerPrefab;

        static bool queuedSoloLocalRace;
        bool spawned;
        bool prefabRegistered;
        bool serverStartedHooked;
        bool soloLocalRace;

        /// <summary>
        /// Arms the next spawned race manager for a one-player local run. This must be called
        /// before NGO starts the host so the manager can skip the online lobby without changing
        /// any of the shared race/HUD code paths.
        /// </summary>
        public void PrepareSoloLocalRace()
        {
            if (spawned) return;
            soloLocalRace = true;
        }

        /// <summary>
        /// Opens the shared network-race scene from the menu-only bootstrap scene and carries a
        /// one-player local-run request across the scene boundary. The race scene consumes the
        /// request only after its prefab registration hook is ready, so StartHost cannot beat
        /// NetworkRaceBootstrap.Start on component execution order.
        /// </summary>
        public static bool LoadSoloLocalRaceScene()
        {
            if (!Application.CanStreamedLevelBeLoaded(RaceSceneName)) return false;
            if (queuedSoloLocalRace) return true;

            queuedSoloLocalRace = true;
            // NetworkManager makes itself persistent even before a host/client starts. Loading
            // NetworkRace immediately would therefore leave this menu scene's manager alive and
            // cause the race scene's own manager (and its NetworkRaceBootstrap) to destroy
            // itself as a duplicate. A short-lived persistent runner removes the idle manager,
            // waits for Unity to clear its Singleton, then activates the shared race scene.
            GameObject transitionObject = new GameObject("M2SoloLocalRaceTransition");
            DontDestroyOnLoad(transitionObject);
            transitionObject.AddComponent<SoloLocalRaceSceneTransition>();
            return true;
        }

        /// <summary>Clears the one-shot local-run state after its NetworkManager is shut down.</summary>
        public void ResetForNextSession()
        {
            spawned = false;
            soloLocalRace = false;
        }

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

            if (!queuedSoloLocalRace) return;
            queuedSoloLocalRace = false;
            if (manager == null)
            {
                Debug.LogError("[NetworkRaceBootstrap] 로컬 레이스용 NetworkManager를 찾지 못했습니다.");
                return;
            }

            PrepareSoloLocalRace();
            FindFirstObjectByType<NetworkBootstrapUI>()?.BeginSoloLocalRacePresentation();
            if (!manager.IsListening && !manager.StartHost())
            {
                soloLocalRace = false;
                Debug.LogError("[NetworkRaceBootstrap] 로컬 레이스 호스트를 시작하지 못했습니다.");
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
                return;
            }
            // AddNetworkPrefab must run at runtime on BOTH host and client before connecting so
            // both agree on the prefab's GlobalObjectIdHash. Editor-time NetworkConfig.Prefabs.Add()
            // doesn't persist (NetworkPrefabs.m_Prefabs is [NonSerialized]).
            manager.AddNetworkPrefab(raceManagerPrefab);
            prefabRegistered = true;
        }

        void HandleServerStarted()
        {
            if (spawned) return;
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;
            if (raceManagerPrefab == null)
            {
                return;
            }

            RegisterPrefab(); // ensure registered even if Start() hadn't yet

            spawned = true;
            GameObject instance = Instantiate(raceManagerPrefab);
            if (soloLocalRace)
            {
                NetworkRaceManager raceManager = instance.GetComponent<NetworkRaceManager>();
                raceManager?.ConfigureSoloLocalRace();
            }
            instance.GetComponent<NetworkObject>().Spawn();
        }

        sealed class SoloLocalRaceSceneTransition : MonoBehaviour
        {
            void Start()
            {
                StartCoroutine(ReplaceMenuManagerAndLoadRace());
            }

            IEnumerator ReplaceMenuManagerAndLoadRace()
            {
                NetworkManager menuManager = NetworkManager.Singleton;
                if (menuManager != null)
                {
                    if (menuManager.IsListening) menuManager.Shutdown();
                    Destroy(menuManager.gameObject);
                }

                // Destroy is deferred to the end of the frame. The following frame has a clear
                // NetworkManager.Singleton, letting the scene-authored manager initialise.
                yield return null;
                SceneManager.LoadScene(RaceSceneName, LoadSceneMode.Single);
                // This runner deliberately survives the scene load for one frame, then removes
                // itself so retries cannot accumulate persistent transition objects.
                Destroy(gameObject);
            }
        }
    }
}
