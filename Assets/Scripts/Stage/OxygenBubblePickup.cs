using UnityEngine;

namespace M2.Stage
{
    [RequireComponent(typeof(Collider))]
    public class OxygenBubblePickup : MonoBehaviour
    {
        public OxygenBubbleSpawner owner;
        [Tooltip("Fraction of the gauge's max value recovered on pickup. CLAUDE.md: 숨방울로 30% 회복.")]
        public float recoverFraction = 0.3f;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            var gauge = other.GetComponentInParent<BikiniCityOxygenGauge>();
            if (gauge == null) return;

            gauge.ModifyValue(gauge.maxValue * recoverFraction);
            owner?.NotifyCollected();
            Destroy(gameObject);
        }
    }
}
