using M2.Player;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // Optional development readout for tuning vehicle physics. The final race HUD hides it.
    public class VehicleDebugHUD : MonoBehaviour
    {
        public VehicleController vehicleController;
        public Text label;
        [Tooltip("정식 플레이 화면에서는 끈다. 물리 튜닝이 필요할 때만 Inspector에서 켠다.")]
        public bool visible;

        void Start()
        {
            SetVisible(visible);
        }

        public void SetVisible(bool shouldBeVisible)
        {
            visible = shouldBeVisible;
            if (label != null) label.gameObject.SetActive(visible);
        }

        void Update()
        {
            if (!visible || label == null || vehicleController == null) return;

            label.text = $"Collision: {(vehicleController.IsColliding ? "Yes" : "No")}\n" +
                         $"Accel: {vehicleController.CurrentAcceleration:0.0} m/s^2\n" +
                         $"Speed: {vehicleController.CurrentSpeed:0.0} m/s";
        }
    }
}
