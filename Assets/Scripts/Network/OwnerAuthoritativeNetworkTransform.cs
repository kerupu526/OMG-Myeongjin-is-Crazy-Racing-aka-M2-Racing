using Unity.Netcode.Components;

namespace M2.Network
{
    // Unity's built-in NetworkTransform defaults to server-authoritative (the server is the
    // only one allowed to write position/rotation, clients just receive it). This project uses
    // owner-authoritative movement instead — the client that owns a vehicle keeps running the
    // exact same local physics simulation VehicleController always has (see
    // VehicleController.IsOwnedLocally), and this component just replicates the result to
    // everyone else. This is the same single-method override NGO's own Bootstrap sample ships
    // as "ClientNetworkTransform" — recreated here directly since sample scripts live under a
    // Samples~ folder that isn't imported by default.
    public class OwnerAuthoritativeNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
