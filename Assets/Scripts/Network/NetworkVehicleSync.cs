using M2.Player;
using Unity.Netcode;
using UnityEngine;

namespace M2.Network
{
    // Owner-authoritative networked companion for VehicleController. The owning client keeps
    // driving its own Rigidbody exactly like the non-networked local test flow always has
    // (VehicleController's FixedUpdate early-returns for anyone that doesn't own this object —
    // see VehicleController.IsOwnedLocally); OwnerAuthoritativeNetworkTransform on this same
    // GameObject then replicates the resulting position/rotation to every other client.
    //
    // A remote (non-owned) copy must not run real physics at all — its own Rigidbody would
    // otherwise fight the incoming network transform every frame (gravity/residual velocity vs.
    // the replicated position). Making it kinematic hands full control of the transform to
    // NetworkTransform for anyone who isn't the owner.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(OwnerAuthoritativeNetworkTransform))]
    public class NetworkVehicleSync : NetworkBehaviour
    {
        [Tooltip("두 플레이어가 스폰 시 겹치지 않도록 좌우로 벌려두는 거리(미터). Milestone 1 한정 — 실제 트랙/스폰 지점은 다음 라운드 범위.")]
        public float spawnSideOffset = 3f;

        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            rb.isKinematic = !IsOwner;

            if (IsOwner)
            {
                // Deterministic left/right placement so 2 players don't spawn stacked on each
                // other. This used to be set by the SERVER via
                // ConnectionApprovalResponse.Position, but that value's replication to remote
                // observers turned out unreliable in practice — playtester feedback: "스폰
                // 위치는 안고쳐졌어" (both cars still spawned in the same spot after the
                // server-side fix). Movement here is owner-authoritative
                // (OwnerAuthoritativeNetworkTransform), so the OWNING client is the only one
                // whose transform writes are actually treated as authoritative — setting
                // position here, instead of on the server, is what actually replicates
                // correctly to everyone else.
                bool isServerOwned = OwnerClientId == NetworkManager.ServerClientId;
                float side = isServerOwned ? -1f : 1f;
                transform.position = new Vector3(side * spawnSideOffset, 0.5f, 0f);

                // With 2 networked vehicles now dynamically spawned, the scene's one
                // Camera.main needs to be told which one is actually "mine" — unlike the local
                // test flow, where TestTrackBuilder wires VehicleCameraFollow.target once at
                // editor-build-time because there's only ever a single vehicle. Only the owner
                // retargets the camera; a remote player's spawn shouldn't steal the local
                // camera away from ours.
                VehicleCameraFollow cameraFollow = Camera.main != null ? Camera.main.GetComponent<VehicleCameraFollow>() : null;
                if (cameraFollow != null) cameraFollow.target = transform;
            }
        }

        public override void OnGainedOwnership()
        {
            rb.isKinematic = false;
        }

        public override void OnLostOwnership()
        {
            rb.isKinematic = true;
        }
    }
}
