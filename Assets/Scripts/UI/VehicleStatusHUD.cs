using System.Text;
using M2.Player;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // 화면 우측 상단에 현재 차량에 걸려있는 상태이상(디버프/버프)을 나열해서 보여준다.
    // 사용자 제안(14차 작업): "오른쪽 위에 상태이상 띄우는 것도 괜찮을듯". 스테이지별 게이지
    // (산소/멘탈/체온)는 이미 각자 UI가 있으니 여기서는 다루지 않고, VehicleController가
    // 노출하는 범용 상태(기절/조향반전/넉백/방어막/부스트)만 폴링해서 한 곳에 모음 — 상태마다
    // 시작/종료 이벤트 쌍을 새로 만드는 대신 매 프레임 bool 읽기만으로 충분히 단순함.
    public class VehicleStatusHUD : MonoBehaviour
    {
        public VehicleController vehicleController;
        public Text label;

        readonly StringBuilder builder = new StringBuilder();

        void Update()
        {
            if (label == null || vehicleController == null) return;

            builder.Clear();
            if (vehicleController.IsStunned) builder.AppendLine("기절");
            if (vehicleController.IsSteeringInverted) builder.AppendLine("조향 반전");
            if (vehicleController.IsKnockedBack) builder.AppendLine("넉백");
            if (vehicleController.HasShield) builder.AppendLine("방어막");
            if (vehicleController.HasSpeedBoost) builder.AppendLine("가속 부스트");
            if (vehicleController.HasDriftBoost) builder.AppendLine("드리프트 부스트");

            // Empty string draws nothing, so there's no need to separately toggle the
            // GameObject active/inactive when no status effect is running.
            label.text = builder.ToString();
        }
    }
}
