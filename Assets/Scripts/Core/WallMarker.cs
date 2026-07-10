using UnityEngine;

namespace M2.Core
{
    // Marker component attached to wall collider segments by TestTrackBuilder.CreateWallRing.
    // VehicleController.AccumulateWallContact checks for this component to distinguish real
    // track walls from all other static colliders (ground plane, terrain hazards, background
    // decor, etc.) — only WallMarker-tagged colliders trigger the wall-slide velocity
    // correction that prevents the car from tunneling through walls.
    //
    // Previous approach (checking collision.rigidbody == null + contact normal direction)
    // had two failure modes: (1) ground plane contacts with near-vertical normals produced
    // floating-point noise in XZ after zeroing Y, which got normalized into an arbitrary
    // horizontal direction and blocked movement as an "invisible wall"; (2) any other solid
    // static collider (e.g. TerrainHazard cubes in BikiniCity) was also treated as a wall,
    // making the car unable to push past small obstacles it should bounce off of.
    public class WallMarker : MonoBehaviour { }
}
