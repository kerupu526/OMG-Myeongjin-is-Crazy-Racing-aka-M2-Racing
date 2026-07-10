using UnityEngine;

namespace M2.Stage
{
    // Shared closed-loop track math so both the editor-time TestTrackBuilder and any runtime
    // stage wiring (StageAssembler) place hazards along the same centerline without
    // duplicating it. The centerline is a closed Catmull-Rom spline through arbitrary control
    // points (not just a plain ellipse), so the track can curve/wind like a real circuit
    // instead of a simple oval — callers don't need to change: theta still runs 0..2π once
    // around the whole loop, same as before.
    public struct TrackGeometry
    {
        public float TrackWidth;
        public float WallHeight;

        readonly Vector3[] controlPoints;

        public TrackGeometry(Vector3[] controlPoints, float trackWidth, float wallHeight)
        {
            this.controlPoints = controlPoints;
            TrackWidth = trackWidth;
            WallHeight = wallHeight;
        }

        public Vector3 PointAt(float theta) => Evaluate(theta, tangent: false);

        public Vector3 TangentAt(float theta) => Evaluate(theta, tangent: true).normalized;

        // Perpendicular to the tangent in the XZ plane. Which physical side counts as
        // "positive" depends on the loop's winding direction — callers that need a specific
        // side (e.g. push a hazard toward the inside of the track) should treat the sign as
        // arbitrary-but-consistent rather than assuming it points outward from the origin.
        public Vector3 NormalAt(float theta)
        {
            Vector3 t = TangentAt(theta);
            return new Vector3(-t.z, 0f, t.x);
        }

        public Vector3 OffsetPointAt(float theta, float lateralOffset) => PointAt(theta) + NormalAt(theta) * lateralOffset;

        // Approximates the centerline's local radius of curvature at theta via the
        // circumradius of three closely-spaced points. A single shared polar-radius wobble
        // (see TestTrackBuilder.BuildControlPoints) keeps the CENTERLINE itself from
        // self-intersecting, but an offset curve (wall ring / track surface edge) can still
        // fold over itself wherever the centerline turns tighter than the offset distance —
        // classic "offset curve self-intersection". Tight pinches in the wobble can and do
        // dip below TrackWidth/2 here, which previously produced walls that crossed the track
        // diagonally and wedged the car in place.
        public float LocalRadiusOfCurvature(float theta)
        {
            const float delta = 0.002f;
            Vector3 a = PointAt(theta - delta);
            Vector3 b = PointAt(theta);
            Vector3 c = PointAt(theta + delta);

            float ab = Vector3.Distance(a, b);
            float bc = Vector3.Distance(b, c);
            float ac = Vector3.Distance(a, c);
            // Twice the signed XZ area of triangle abc.
            float cross = Mathf.Abs((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z));
            if (cross < 0.00001f) return float.MaxValue; // effectively straight here

            // Circumradius: R = (ab * bc * ac) / (4 * area) = (ab * bc * ac) / (2 * cross)
            return (ab * bc * ac) / (2f * cross);
        }

        // Clamps a desired lateral offset so it never exceeds (a safety margin under) the
        // centerline's local turning radius at theta, preventing wall/track-edge offset curves
        // from folding over themselves on tight bends. Preserves the sign/direction of
        // desiredOffset, only shrinks its magnitude when the curve is too tight for it.
        public float SafeLateralOffset(float theta, float desiredOffset)
        {
            const float safetyMargin = 0.85f;
            float maxOffset = LocalRadiusOfCurvature(theta) * safetyMargin;
            return Mathf.Clamp(desiredOffset, -maxOffset, maxOffset);
        }

        Vector3 Evaluate(float theta, bool tangent)
        {
            int n = controlPoints.Length;
            float t = Mathf.Repeat(theta / (Mathf.PI * 2f), 1f);
            float scaled = t * n;
            int i1 = Mathf.FloorToInt(scaled) % n;
            float u = scaled - Mathf.Floor(scaled);
            int i0 = (i1 - 1 + n) % n;
            int i2 = (i1 + 1) % n;
            int i3 = (i1 + 2) % n;

            return tangent
                ? CatmullRomTangent(controlPoints[i0], controlPoints[i1], controlPoints[i2], controlPoints[i3], u)
                : CatmullRomPoint(controlPoints[i0], controlPoints[i1], controlPoints[i2], controlPoints[i3], u);
        }

        static Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u;
            return 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * u +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * u2);
        }
    }
}
