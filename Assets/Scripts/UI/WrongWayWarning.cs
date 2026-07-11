using M2.Player;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // Center-screen wrong-way banner. Polls VehicleController.IsWrongWayBlocked continuously
    // (like VehicleStatusHUD) rather than firing a timed flash, since it needs to stay up for
    // as long as the prevention is actively refusing movement, not just blip once — see
    // VehicleController.maxWrongWayDistance's comment for the full detection story (covers both
    // reversing too far and turning fully around to drive forward the wrong way).
    public class WrongWayWarning : MonoBehaviour
    {
        public VehicleController vehicleController;
        public Text label;

        void OnEnable()
        {
            if (label != null) label.text = "";
        }

        void Update()
        {
            if (label == null) return;

            bool blocked = vehicleController != null && vehicleController.IsWrongWayBlocked;
            label.text = blocked ? "⚠ 반대 방향입니다! ⚠" : "";
        }
    }
}
