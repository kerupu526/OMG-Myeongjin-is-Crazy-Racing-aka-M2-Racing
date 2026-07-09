using UnityEngine;

namespace M2.Player
{
    public class VehicleCameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 6f, -8f);
        public float positionSmoothTime = 0.15f;
        public float rotationSmoothSpeed = 8f;

        Vector3 velocity;

        void LateUpdate()
        {
            if (target == null) return;

            // Offset is rotated by the target's yaw so the camera stays behind the
            // car as it turns around the track, instead of drifting to the side.
            Vector3 desiredPosition = target.position + target.rotation * offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

            Quaternion desiredRotation = Quaternion.LookRotation((target.position - transform.position).normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
        }
    }
}
