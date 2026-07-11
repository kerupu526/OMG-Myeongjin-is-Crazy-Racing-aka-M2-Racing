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
        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            rb.isKinematic = !IsOwner;

            // With 2 networked vehicles now dynamically spawned, the scene's one Camera.main
            // needs to be told which one is actually "mine" — unlike the local test flow, where
            // TestTrackBuilder wires VehicleCameraFollow.target once at editor-build-time
            // because there's only ever a single vehicle. Only the owner retargets the camera;
            // a remote player's spawn shouldn't steal the local camera away from ours.
            if (IsOwner)
            {
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
