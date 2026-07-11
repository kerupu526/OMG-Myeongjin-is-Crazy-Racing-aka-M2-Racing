using UnityEngine;

namespace M2.Network
{
    // Two staggered start-grid slots near the track's start/finish line, baked into the
    // networked race scene by NetworkRaceSceneBuilder. NetworkVehicleSync's owner reads this to
    // place itself at the correct slot on spawn.
    //
    // Why the OWNER positions itself (not the server): movement is owner-authoritative
    // (OwnerAuthoritativeNetworkTransform), so only the owning client's transform writes
    // replicate reliably — a server-set spawn position didn't reach remote observers in
    // Milestone 1 (see NetworkVehicleSync's comment / CLAUDE.md). The grid slots are plain scene
    // data, identical on host and client since both load the same scene, so each owner can read
    // its slot locally and place itself.
    public class RaceStartGrid : MonoBehaviour
    {
        public Vector3 slot0Position;
        public Vector3 slot1Position;
        // Shared facing (both cars point down the track the same way).
        public Quaternion facing = Quaternion.identity;

        // isServerOwned: the host's car takes slot 0, the joining client's car takes slot 1.
        public void GetSlot(bool isServerOwned, out Vector3 position, out Quaternion rotation)
        {
            position = isServerOwned ? slot0Position : slot1Position;
            rotation = facing;
        }
    }
}
