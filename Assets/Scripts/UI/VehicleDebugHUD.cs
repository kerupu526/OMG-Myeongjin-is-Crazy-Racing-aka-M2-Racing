using M2.Player;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // Bottom-right debug readout for tuning vehicle physics feel. Not final UI design.
    public class VehicleDebugHUD : MonoBehaviour
    {
        public VehicleController vehicleController;
        public Text label;

        void Update()
        {
            if (label == null || vehicleController == null) return;

            label.text = $"Collision: {(vehicleController.IsColliding ? "Yes" : "No")}\n" +
                         $"Accel: {vehicleController.CurrentAcceleration:0.0} m/s^2\n" +
                         $"Speed: {vehicleController.CurrentSpeed:0.0} m/s";
        }
    }
}
