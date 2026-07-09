using UnityEngine;

namespace M2.Player
{
    // Attach to any sprite (vehicle, character, item) that must always face the camera.
    // Only yaws around Y so 2.5D sprites never tilt up/down as the camera moves.
    public class BillboardSprite : MonoBehaviour
    {
        Camera targetCamera;

        void OnEnable()
        {
            targetCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            Vector3 toCamera = targetCamera.transform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }
    }
}
