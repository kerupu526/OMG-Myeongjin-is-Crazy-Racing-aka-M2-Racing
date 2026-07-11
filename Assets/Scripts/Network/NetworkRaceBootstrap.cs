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
        [Tooltip("NetworkRaceManager가 붙어있는 프리팹. NetworkConfig.Prefabs에도 등록돼 있어야 함(씬 빌더가 처리).")]
        public GameObject raceManagerPrefab;

        bool spawned;
        bool prefabRegistered;

        void OnEnable()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null) return;

            // Register the race-manager prefab as a spawnable network prefab BEFORE any
            // connection starts. This must happen at runtime, on BOTH host and client, so both
            // sides agree on its GlobalObjectIdHash when the host spawns it. Editor-time
            // NetworkConfig.Prefabs.Add() does NOT work for this: NetworkPrefabs.m_Prefabs is
            // [NonSerialized], so an Add() made while authoring the scene is dropped when the
            // scene is saved (only NetworkPrefabsLists serializes). AddNetworkPrefab is the
            // supported runtime registration path (the player vehicle avoids this only because
            // NetworkConfig.PlayerPrefab is a separate serialized field NGO auto-registers).
            if (!prefabRegistered && raceManagerPrefab != null &&
                raceManagerPrefab.GetComponent<NetworkObject>() != null)
            {
                manager.AddNetworkPrefab(raceManagerPrefab);
                prefabRegistered = true;
            }

            manager.OnServerStarted += HandleServerStarted;
            // StartHost may already have fired OnServerStarted before this subscribed (scene
            // object enable order vs. the host button) — cover that by checking directly too.
            if (manager.IsServer) HandleServerStarted();
        }

        void OnDisable()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null) manager.OnServerStarted -= HandleServerStarted;
        }

        void HandleServerStarted()
        {
            if (spawned) return;
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;
            if (raceManagerPrefab == null)
            {
                Debug.LogWarning("M2: NetworkRaceBootstrap.raceManagerPrefab이 비어있음 — 레이스 매니저가 스폰되지 않음.");
                return;
            }

            spawned = true;
            GameObject instance = Instantiate(raceManagerPrefab);
            instance.GetComponent<NetworkObject>().Spawn();
        }
    }
}
