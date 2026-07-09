using UnityEngine;

namespace M2.Core
{
    // Vertical bob animation so pickups read as floating items rather than static props.
    public class FloatingBob : MonoBehaviour
    {
        public float amplitude = 0.25f;
        public float speed = 2f;

        Vector3 basePosition;

        void Start()
        {
            basePosition = transform.localPosition;
        }

        void Update()
        {
            float offset = Mathf.Sin(Time.time * speed) * amplitude;
            transform.localPosition = basePosition + Vector3.up * offset;
        }
    }
}
