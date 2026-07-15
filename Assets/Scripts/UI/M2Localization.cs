using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace M2.UI
{
    /// <summary>
    /// Small, deliberately local menu catalog. The project does not use Unity Localization yet,
    /// but a real language option must change visible copy instead of only persisting an enum.
    /// Keeping the catalog here makes the menu's Korean source text the single canonical value
    /// and lets a player switch back and forth without accumulating translated strings.
    /// </summary>
    public static class M2Localization
    {
        static readonly Dictionary<string, string> EnglishByKorean =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "© 2026 인평랜드 · 학교축제 에디션", "© 2026 Inpyeong Land · School Festival Edition" },
                { "← → / A D  조향", "← → / A D  Steering" },
                { "← 나가기", "← Leave" },
                { "← 뒤로", "← Back" },
                { "↑ ↓ / W S  가속·후진", "↑ ↓ / W S  Accelerate / Reverse" },
                { "▶ 꾸미기", "▶ Customize" },
                { "▶ 로컬 레이스 시작", "▶ Start Local Race" },
                { "⚙️ 게임 설정", "⚙️ Game Setup" },
                { "✓ 준비 완료", "✓ Ready" },
                { "✓ 준비 취소", "✓ Cancel Ready" },
                { "✦ 내 아바타", "✦ My Avatar" },
                { "✦ 미리보기", "✦ Preview" },
                { "💬 친구에게 위 방 코드를 알려주세요.", "💬 Share the room code above with a friend." },
                { "🔊 사운드", "🔊 Sound" },
                { "🕹️ 인평이네 오락실", "🕹️ Inpyeong Arcade" },
                { "🖥️ 화면 & 게임", "🖥️ Display & Game" },
                { "👀 방울눈", "👀 Round Eyes" },
                { "👑 왕관", "👑 Crown" },
                { "🧢 캡모자", "🧢 Cap" },
                { "😊 웃는눈", "😊 Happy Eyes" },
                { "😎 선글라스", "😎 Sunglasses" },
                { "1바퀴", "1 Lap" },
                { "3바퀴", "3 Laps" },
                { "5바퀴", "5 Laps" },
                { "게임 모드", "Game Mode" },
                { "게임 설정", "Game Setup" },
                { "게임 시작! 가보자고", "Start Race!" },
                { "귀", "Ears" },
                { "그래픽 품질", "Graphics Quality" },
                { "기본값", "Defaults" },
                { "네더 요새", "Nether Fortress" },
                { "높음", "High" },
                { "누가 1등? 😏", "Who will win? 😏" },
                { "눈", "Eyes" },
                { "닉네임", "Nickname" },
                { "단순완주", "Simple Finish" },
                { "단순 완주", "Simple Finish" },
                { "도전장 접수 · Lv.42", "Challenge received · Lv.42" },
                { "레이서 #001", "Racer #001" },
                { "레이스 온 🔥", "Race On 🔥" },
                { "마스터 볼륨", "Master Volume" },
                { "모드와 규칙을 정한 뒤 방을 만드세요.", "Choose a mode and rules, then create a room." },
                { "모자 / 악세서리", "Hat / Accessory" },
                { "몸 색상", "Body Color" },
                { "바퀴 수", "Lap Count" },
                { "방 만들기", "Create Room" },
                { "방 만들기 또는 방 참가를 선택하세요.", "Choose Create Room or Join Room." },
                { "방 참가", "Join Room" },
                { "방 참가하기", "Join Room" },
                { "방 코드", "Room Code" },
                { "방 코드 입력", "Enter Room Code" },
                { "방긋", "Smile" },
                { "방긋 입", "Smiling Mouth" },
                { "방장", "Host" },
                { "방장만 변경할 수 있습니다.", "Only the host can change these settings." },
                { "방장이 설정을 변경할 수 있습니다.", "The host can change these settings." },
                { "배경음악 (BGM)", "Background Music (BGM)" },
                { "번호판", "Plate" },
                { "변경한 뒤 저장하면 다음 실행에도 유지됩니다.", "Save changes to keep them next time." },
                { "별점내기", "Star Bet" },
                { "별점 내기", "Star Bet" },
                { "보통", "Medium" },
                { "볼터치", "Cheeks" },
                { "비키니 시티", "Bikini City" },
                { "비키니시티", "Bikini City" },
                { "상대 접속 대기", "Waiting for Opponent" },
                { "스테이지", "Stage" },
                { "스피드전", "Speed Mode" },
                { "승리 조건", "Victory Condition" },
                { "시크", "Cool" },
                { "시크 입", "Cool Mouth" },
                { "아바타 설정", "Avatar Settings" },
                { "아이템전", "Item Mode" },
                { "아프리카TV", "AfreecaTV" },
                { "언어", "Language" },
                { "없음", "None" },
                { "예시 · M2-7X4K", "Example · M2-7X4K" },
                { "완주 시 획득한 별점이 더 높은 쪽 승리", "The racer with more finish stars wins." },
                { "외형과 이름을 고른 뒤 저장하세요.", "Choose your look and name, then save." },
                { "우와", "Wow" },
                { "우와 입", "Open Mouth" },
                { "인평에서 레이싱으로 미쳐 날뛰어보자 🏁", "Go wild on the track at Inpyeong 🏁" },
                { "입", "Mouth" },
                { "있음", "On" },
                { "저번 대회 우승자 · Lv.99", "Last event champion · Lv.99" },
                { "저장하기", "Save" },
                { "적용", "Apply" },
                { "전체화면", "Fullscreen" },
                { "조작키", "Controls" },
                { "준비 중", "Preparing" },
                { "준비 중...", "Preparing..." },
                { "창모드", "Windowed" },
                { "친구에게 받은 방 코드를 입력하세요.", "Enter the room code from your friend." },
                { "친구에게 받은 M2 방 코드로 합류하세요.", "Join with the M2 room code from your friend." },
                { "친구와 함께 달릴 프라이빗 레이스를 설정하세요.", "Set up a private race with a friend." },
                { "프리셋", "Presets" },
                { "한국어", "Korean" },
                { "화면 모드", "Screen Mode" },
                { "환경설정", "Settings" },
                { "효과음 (SFX)", "Sound Effects (SFX)" },
                { "Ctrl  가속 아이템", "Ctrl  Boost Item" },
                { "E  공격·방어", "E  Attack / Defense" },
                { "Shift  드리프트", "Shift  Drift" },

                // Runtime feedback and selection labels used by M2UiToolkitMenu.
                { "기본값을 불러왔습니다. 저장하면 적용됩니다.", "Defaults loaded. Save to apply them." },
                { "내 준비 완료 · 상대 레이서의 준비를 기다리는 중입니다.", "You are ready · waiting for the other racer." },
                { "두 레이서가 준비되었습니다. 레이스를 시작합니다!", "Both racers are ready. Starting the race!" },
                { "로컬 레이스 연결 정보를 찾을 수 없습니다.", "Local race connection details were not found." },
                { "미리보기에 선택한 색을 적용했습니다.", "Applied the selected color to the preview." },
                { "방에 입장했습니다. 연결 상태를 확인하는 중입니다.", "Joined the room. Checking the connection…" },
                { "방을 정리하고 메인으로 돌아가는 중입니다...", "Closing the room and returning to the main menu…" },
                { "방장이 설정을 확정하면 두 레이서가 준비할 수 있습니다.", "Both racers can ready up after the host confirms the rules." },
                { "상대 레이서가 입장하면 준비 버튼으로 함께 시작할 수 있습니다.", "You can start together when the other racer joins." },
                { "상대 레이서가 준비했습니다. 현재 설정을 확인하고 준비하세요.", "The other racer is ready. Check the settings and get ready." },
                { "연결 정보를 확인하는 중입니다.", "Checking connection details…" },
                { "연결 중", "Connecting" },
                { "연결 확인 중 · 방 설정은 지금 변경할 수 있습니다.", "Checking connection · you can still change room settings." },
                { "온라인 레이스와 같은 흐름으로 로컬 레이스를 시작합니다.", "Starting a local race with the same flow as online play." },
                { "친구에게 위 방 코드를 알려주세요.", "Share the room code above with a friend." },
                { "친구에게 위 방 코드를 알려주세요. 연결 중에도 방 설정을 바꿀 수 있습니다.", "Share the room code above. You can change room settings while waiting." },
                { "현재 레이스를 나간 뒤 로컬 레이스를 시작하세요.", "Leave the current race before starting a local race." },
                { "환경설정을 저장했습니다.", "Settings saved." },
                { "M2-1L4G 형식의 방 코드를 입력하세요.", "Enter a room code in the M2-1L4G format." },

                // Race HUD and result cards.
                { "나의 완주 기록 ✨", "My Finish ✨" },
                { "다시 하기", "Rematch" },
                { "다시 하기 · 대기", "Rematch · Waiting" },
                { "다시 하기를 누르면 바로 새 레이스를 시작합니다.", "Press Rematch to start a new race now." },
                { "다시 하기 또는 로비로는 상대 레이서의 같은 선택을 기다립니다.", "Rematch or Lobby waits for the other racer's matching choice." },
                { "다음 라운드도 기대해요!", "Looking forward to the next round!" },
                { "다음엔 꼭 이깁니다 ㅠㅠ", "I'll win next time ㅠㅠ" },
                { "기록 집계 완료", "Results Complete" },
                { "미완주", "DNF" },
                { "비법 놓친 횟수", "Missed Items" },
                { "새 레이스를 준비합니다...", "Preparing a new race…" },
                { "시간이 종료되었습니다. 다음 기록에 도전해 보세요.", "Time is up. Try again for a new record." },
                { "오늘의 별점 통계", "Today's Star Stats" },
                { "오늘의 챔피언 👑", "Today's Champion 👑" },
                { "완주 시간", "Finish Time" },
                { "완주했습니다! 나의 기록을 확인해 보세요.", "Finished! Check your record." },
                { "치열한 승부였습니다! ✨", "That was a close race! ✨" },
                { "패배 · 상대 레이서가 먼저 결승 조건을 달성했습니다.", "Defeat · the opponent reached the finish condition first." },
                { "로비로", "Lobby" },
                { "로비로 · 대기", "Lobby · Waiting" },
                { "무승부 · 두 레이서의 기록이 동점으로 처리되었습니다.", "Draw · both racer records are tied." },
                { "승리! 가장 먼저 결승 조건을 달성했습니다.", "Victory! You reached the finish condition first." },
            };

        static readonly Dictionary<string, string> KoreanByEnglish = CreateInverseCatalog();

        public static string Translate(string source) => Translate(source, M2GameSettings.Language);

        public static string Translate(string source, M2Language language)
        {
            if (string.IsNullOrEmpty(source)) return source;

            string canonicalKorean = KoreanByEnglish.TryGetValue(source, out string korean)
                ? korean
                : source;
            return language == M2Language.English && EnglishByKorean.TryGetValue(canonicalKorean, out string english)
                ? english
                : canonicalKorean;
        }

        /// <summary>Refreshes static UXML labels and dynamic menu copy after a language switch.</summary>
        public static void ApplyTo(VisualElement root)
        {
            if (root == null) return;

            List<Label> labels = root.Query<Label>().ToList();
            foreach (Label label in labels)
            {
                label.text = Translate(label.text);
            }
        }

        static Dictionary<string, string> CreateInverseCatalog()
        {
            var inverse = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in EnglishByKorean)
            {
                if (!inverse.ContainsKey(entry.Value)) inverse.Add(entry.Value, entry.Key);
            }
            return inverse;
        }
    }
}
