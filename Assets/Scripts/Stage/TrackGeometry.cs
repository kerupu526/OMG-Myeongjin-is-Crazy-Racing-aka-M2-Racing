using UnityEngine;

namespace M2.Stage
{
    // Shared oval-track math so both the editor-time TestTrackBuilder and any runtime stage
    // wiring (StageAssembler) place hazards using the same geometry without duplicating it.
    public struct TrackGeometry
    {
        public float CenterRadiusX;
        public float CenterRadiusZ;
        public float TrackWidth;
        public float WallHeight;

        public Vector3 PointAt(float theta) =>
            new Vector3(CenterRadiusX * Mathf.Cos(theta), 0f, CenterRadiusZ * Mathf.Sin(theta));

        public Vector3 TangentAt(float theta)
        {
            Vector3 derivative = new Vector3(-CenterRadiusX * Mathf.Sin(theta), 0f, CenterRadiusZ * Mathf.Cos(theta));
            return derivative.normalized;
        }
    }
}
