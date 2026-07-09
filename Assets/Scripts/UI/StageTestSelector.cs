using M2.Core;
using M2.Stage;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace M2.UI
{
    // 임시 테스트용 스테이지 전환기. 레이스가 시작되기 전(Racing/Finished가 아닐 때)에만
    // 숫자키 1/2/3으로 비키니시티/아프리카TV/네더요새 게이지를 바로 바꿔볼 수 있게 해준다.
    // 정식 게임에는 없는 개발용 기능 — 스테이지별 정식 씬이 생기면 제거될 예정.
    public class StageTestSelector : MonoBehaviour
    {
        public GameManager gameManager;
        public GameObject vehicle;
        public Canvas canvas;
        public RaceFlowUI flowUI;
        public Transform worldParent;
        public Transform trackCenter;
        public TrackGeometry geometry;

        public Text hintLabel;

        BuiltStage current;

        // TestTrackBuilder calls this right after its own initial StageAssembler.Attach() so
        // this selector knows what's already on the vehicle instead of attaching a duplicate
        // set the first time a hotkey is pressed.
        public void Initialize(BuiltStage initial)
        {
            current = initial;
            UpdateHint();
        }

        void Update()
        {
            if (gameManager != null &&
                (gameManager.CurrentState == RaceState.Racing || gameManager.CurrentState == RaceState.Finished))
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SwitchTo(StageType.BikiniCity);
            else if (keyboard.digit2Key.wasPressedThisFrame) SwitchTo(StageType.AfricaTv);
            else if (keyboard.digit3Key.wasPressedThisFrame) SwitchTo(StageType.NetherFortress);
        }

        public void SwitchTo(StageType type)
        {
            if (current != null && current.Type == type) return;

            StageAssembler.Detach(current, vehicle, flowUI);
            current = StageAssembler.Attach(type, worldParent, trackCenter, vehicle, canvas, flowUI, geometry);
            UpdateHint();
        }

        void UpdateHint()
        {
            if (hintLabel == null || current == null) return;
            hintLabel.text = $"[테스트용] 현재 스테이지: {StageName(current.Type)} (1/2/3 키로 전환)";
        }

        static string StageName(StageType type) => type switch
        {
            StageType.BikiniCity => "비키니시티",
            StageType.AfricaTv => "아프리카TV",
            StageType.NetherFortress => "네더요새",
            _ => type.ToString(),
        };
    }
}
