using System.Collections.Generic;
using M2.Items;
using Unity.Netcode;
using UnityEngine;

namespace M2.Network
{
    // Milestone 2b: server-authoritative item spawning. Lives as a second NetworkBehaviour on the
    // NetworkRaceManager prefab (the host already spawns that at runtime, so no extra prefab
    // registration is needed).
    //
    // Design, consistent with the rest of the netcode layer:
    //   - The HOST rolls every item (type + 10% derived) and owns all pickup arbitration, exactly
    //     like it owns lap counting — it has synced copies of both cars, so measuring pickup
    //     collection against those copies can't disagree with what each client sees.
    //   - Spawn state is replicated as a NetworkList<byte> of NetItemId (one entry per spawn point,
    //     0 = collected/empty) rather than spawning/despawning a NetworkObject per pickup. Pickups
    //     respawn every few seconds at 6 points; replicating a small byte array in one already-
    //     spawned object avoids registering 6 more prefabs, the batch-mode GlobalObjectIdHash
    //     workaround, and spawn-ordering races — for purely cosmetic, collider-less markers.
    //     (Escape hatch if NetworkList proves troublesome on this NGO version: six
    //     NetworkVariable<byte> or one bit-packed NetworkVariable<int>.)
    //   - Every peer turns that state into local cosmetic pickup visuals (no colliders); the host
    //     detects collection by distance against the connected players' PlayerObjects.
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkItemSpawnManager : NetworkBehaviour
    {
        [Tooltip("픽업 획득 판정 반경(m). 로컬 트리거 실효 반경(약 1.32m)에 차폭 절반을 더한 값 정도.")]
        public float pickupRadius = 2f;

        [Tooltip("픽업을 주운 뒤 같은 지점에 새 아이템이 다시 나오기까지의 시간(초). 로컬 ItemSpawner와 동일.")]
        public float respawnDelay = 5f;

        [Tooltip("이 반경(m) 안에 플레이어가 있으면 재스폰을 미룸 — 같은 자리에 죽치고 앉아 원하는 아이템이 나올 때까지 반복해서 뽑는 것(캠핑) 방지. 픽업 판정 반경보다 살짝 크게.")]
        public float noRespawnPlayerRadius = 3f;

        [Tooltip("픽업 비주얼이 지점 위로 떠 있는 높이(m). 로컬 ItemSpawner.pickupHeight와 동일.")]
        public float pickupHeight = 1f;

        // Server-written, everyone-read. One byte (NetItemId) per spawn point; None = collected.
        readonly NetworkList<byte> netSpawnItems = new NetworkList<byte>();

        // Spawn points from the scene, indexed by their NetworkItemSpawnPoint.index. Resolved on
        // spawn on every peer.
        NetworkItemSpawnPoint[] points;
        // The cosmetic pickup visual currently shown at each point (null = none). Local to each peer.
        GameObject[] visuals;
        NetItemId[] shownIds;

        // Server-only respawn countdowns per point (seconds remaining; <= 0 = not waiting).
        float[] respawnTimers;

        public override void OnNetworkSpawn()
        {
            ResolveSpawnPoints();

            netSpawnItems.OnListChanged += HandleSpawnListChanged;

            if (IsServer)
            {
                respawnTimers = new float[points.Length];
                // Seed one item per point. NetworkList entries added here are part of the initial
                // replicated state clients receive when they spawn their copy of this object.
                for (int i = 0; i < points.Length; i++)
                {
                    netSpawnItems.Add((byte)ItemCatalog.CreateRandomIdForSpawn());
                }
                Debug.Log($"M2Net: 아이템 스폰 매니저 시작 — {points.Length}개 지점에 아이템 롤 완료.");
            }

            // Build visuals for whatever state exists now (on a client the list arrives pre-populated
            // via the spawn payload, so no OnListChanged fires for those initial entries).
            RefreshAllVisuals();
        }

        public override void OnNetworkDespawn()
        {
            netSpawnItems.OnListChanged -= HandleSpawnListChanged;
            DestroyAllVisuals();
        }

        void ResolveSpawnPoints()
        {
            var found = new List<NetworkItemSpawnPoint>(
                FindObjectsByType<NetworkItemSpawnPoint>(FindObjectsSortMode.None));
            found.Sort((a, b) => a.index.CompareTo(b.index));
            points = found.ToArray();
            visuals = new GameObject[points.Length];
            shownIds = new NetItemId[points.Length];
        }

        // ---- Server: pickup detection + respawn ----

        void Update()
        {
            if (!IsServer || !IsSpawned) return;

            DetectPickups();
            TickRespawns();
        }

        void DetectPickups()
        {
            var manager = NetworkManager;
            if (manager == null) return;

            float radiusSqr = pickupRadius * pickupRadius;

            for (int i = 0; i < points.Length && i < netSpawnItems.Count; i++)
            {
                NetItemId id = (NetItemId)netSpawnItems[i];
                if (id == NetItemId.None) continue;

                Vector3 pickupPos = points[i].transform.position;

                foreach (var client in manager.ConnectedClientsList)
                {
                    NetworkObject playerObject = client.PlayerObject;
                    if (playerObject == null) continue;

                    // Horizontal distance only — the track is 2.5D on the XZ plane, and the
                    // pickup floats above the road, so height shouldn't affect grab range.
                    Vector3 delta = playerObject.transform.position - pickupPos;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > radiusSqr) continue;

                    var slots = playerObject.GetComponent<NetworkItemSlots>();
                    if (slots == null) continue;

                    slots.ServerCollect(id);
                    netSpawnItems[i] = (byte)NetItemId.None;
                    respawnTimers[i] = respawnDelay;
                    Debug.Log($"M2Net: 지점 {i}의 아이템({id})을 클라이언트 {client.ClientId}가 획득.");
                    break;
                }
            }
        }

        void TickRespawns()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < points.Length && i < netSpawnItems.Count; i++)
            {
                if ((NetItemId)netSpawnItems[i] != NetItemId.None) continue;

                // Count down the post-pickup delay first.
                if (respawnTimers[i] > 0f)
                {
                    respawnTimers[i] -= dt;
                    if (respawnTimers[i] > 0f) continue;
                }

                // Delay elapsed. Hold the respawn while a player is loitering on the spot, so they
                // can't camp it and keep re-rolling until the item they want appears — it only
                // reappears once the area is clear.
                if (IsPlayerNear(points[i].transform.position)) continue;

                NetItemId rolled = ItemCatalog.CreateRandomIdForSpawn();
                netSpawnItems[i] = (byte)rolled;
                Debug.Log($"M2Net: 지점 {i}에 새 아이템({rolled}) 재스폰.");
            }
        }

        bool IsPlayerNear(Vector3 position)
        {
            var manager = NetworkManager;
            if (manager == null) return false;

            float radiusSqr = noRespawnPlayerRadius * noRespawnPlayerRadius;
            foreach (var client in manager.ConnectedClientsList)
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject == null) continue;

                Vector3 delta = playerObject.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr) return true;
            }
            return false;
        }

        // ---- Every instance: replicated state -> cosmetic visuals ----

        void HandleSpawnListChanged(NetworkListEvent<byte> _)
        {
            // Only 6 points; a full refresh on any change is cheap and keeps the mapping simple.
            RefreshAllVisuals();
        }

        void RefreshAllVisuals()
        {
            if (points == null) return;

            for (int i = 0; i < points.Length; i++)
            {
                NetItemId desired = i < netSpawnItems.Count ? (NetItemId)netSpawnItems[i] : NetItemId.None;
                if (desired == shownIds[i] && (desired == NetItemId.None) == (visuals[i] == null))
                {
                    continue; // already showing the right thing
                }

                if (visuals[i] != null)
                {
                    Destroy(visuals[i]);
                    visuals[i] = null;
                }
                shownIds[i] = desired;

                if (desired != NetItemId.None)
                {
                    ItemDefinition def = ItemCatalog.CreateFromId(desired);
                    visuals[i] = ItemPickupVisuals.Create(points[i].transform, def, pickupHeight, withTriggerCollider: false);
                }
            }
        }

        void DestroyAllVisuals()
        {
            if (visuals == null) return;
            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null) Destroy(visuals[i]);
                visuals[i] = null;
            }
        }
    }
}
