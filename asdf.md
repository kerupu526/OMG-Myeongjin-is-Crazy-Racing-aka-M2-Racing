# 3차 작업: 경기 진행 흐름 + GameManager + BuildCheck 정리

## Context

CLAUDE.md 개발 우선순위 1번(로컬 코어 루프)은 차량 조작/체크포인트/바퀴 판정/타이머까지는 구현됐지만,
"경기 진행 흐름"(조작법 안내 → 카운트다운 → 제한시간 레이스 → 승리/무승부 판정 → 결과화면)이 코드화되지
않아 아직 마무리되지 않은 상태다. 이 흐름을 관장할 `M2.Core.GameManager` 클래스도 아직 존재하지 않는다.

더 심각한 문제로, `Assets/Scripts/Editor/BuildCheck.cs`의 `SceneSmokeTest()`가 존재하지 않는
`M2.Core.GameManager` 타입과 `Assets/Scenes/Stage_BikiniCity.unity` 씬을 참조하고 있어, 이 파일이 포함된
어셈블리 전체가 컴파일되지 않을 위험이 있다(CS0246). CLAUDE.md가 정의한 "컴파일 자동 체크 루프"가 이 문제
때문에 정상 동작하지 않을 수 있으므로, GameManager 신설과 BuildCheck 경로/참조 정리를 함께 진행한다.

씬/프리팹 구조는 아직 정식 아트가 없는 상태이므로 이번 작업에서는 절차적 테스트 트랙(`TestTrackBuilder.cs`)
방식을 유지하고, 여기에 GameManager/UI를 추가로 조립하는 방향으로 간다 (과설계 방지).

멀티플레이어(향후 온라인 1v1) 대비는 `GameManager`가 단일 레퍼런스 대신 `List<LapTracker>`/
`List<VehicleController>`를 순회하는 정도로만 최소 반영한다 — 코드량은 동일하고, 나중에 로컬 핫싯/Netcode로
플레이어가 늘어나도 이 클래스는 그대로 동작한다. 그 이상의 추상화(인터페이스, 플레이어 ID, 네트워크 동기화
스텁 등)는 추가하지 않는다.

## 작업 내용

### 1. `VehicleController`에 입력 잠금 훅 추가
파일: `Assets/Scripts/Player/VehicleController.cs`

기존 `isStunned` 처리 경로를 재사용하는 최소 변경:
```csharp
bool inputLocked;

public void SetInputLocked(bool locked)
{
    inputLocked = locked;
    if (locked)
    {
        currentSpeed = 0f;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }
}
```
`FixedUpdate`의 `if (isStunned)` 조건을 `if (isStunned || inputLocked)`로 변경. 브리핑/카운트다운 중
조작을 막고, 레이스 종료 후에도 차량을 정지시키는 데 재사용한다.

### 2. `M2.Core.GameManager` 신설 (신규 파일)
파일: `Assets/Scripts/Core/GameManager.cs`

- `RaceState` enum: `PreRace, Briefing, Countdown, Racing, Finished`
- `List<LapTracker> racers`, `List<VehicleController> vehicles`, `RaceTimer raceTimer` (Inspector 노출,
  비어있으면 `Start()`에서 `FindObjectsByType`으로 자동 수집)
- Inspector 노출 수치 필드: `briefingDuration`(기본 4초), `countdownSeconds`(3), `targetLapCount`(3),
  `lap1TimeLimit`(180f), `lapBonusSeconds`(`{45f, 30f, 15f}` — 2/3/4바퀴 완주 시 지급)
- `RunRaceFlow()` 코루틴: Briefing(차량 잠금) → Countdown(1초 간격 `OnCountdownTick` 발행) → Racing
  (`raceTimer.StartRace()`, 차량 잠금 해제, `TimeRemaining = lap1TimeLimit`)
- `Update()`: Racing 상태일 때 `TimeRemaining` 감소, 0 이하면 `EndRace(null)` (무승부)
- `HandleLapCompleted(racer, lapNumber)`: 바퀴 완주 시 `lapBonusSeconds`로 시간 추가 지급, `lapNumber >=
  targetLapCount`면 `EndRace(racer)` (승리)
- `EndRace(LapTracker winner)`: 중복 호출 가드, `raceTimer.StopRace()`, 전 차량 입력 잠금, `winner`가
  null이면 `OnRaceDraw`, 아니면 `OnRaceWon(winner)` 발행
- 공개 이벤트: `OnStateChanged(RaceState)`, `OnCountdownTick(int)`, `OnRaceStarted`, `OnRaceWon(LapTracker)`,
  `OnRaceDraw`
- `RaceTimer`는 수정하지 않는다 — `ElapsedTime`(카운트업, 통계/스플릿용)과 `GameManager.TimeRemaining`
  (카운트다운, 레이스 종료 판정용)은 목적이 달라 별도로 둔다.

체크: `check_build.ps1` 실행 (기본 `CompileCheck`) — 이 시점에서 `BuildCheck.cs`의 `GameManager` 미해결
타입 문제가 해소되어야 한다.

### 3. UI 추가
- `Assets/Scripts/UI/RaceHUD.cs`: `public GameManager gameManager;` 필드 추가, `Update()`에 "Time Left"
  한 줄 추가 (기존 구조를 유지한 채 additive하게만 수정 — "Not final UI design" 주석이 있는 임시 HUD이므로
  과도한 리팩터링 금지)
- `Assets/Scripts/UI/RaceFlowUI.cs` (신규): 브리핑 패널(조작법 안내 텍스트, Inspector에서 고정 텍스트 설정),
  카운트다운 패널(큰 숫자/"GO!"), 결과 패널(승자 또는 무승부 + `raceTimer.LapSplits` 통계)을 `GameManager`
  이벤트 구독으로 토글. 기존 UI 스크립트들과 동일하게 `UnityEngine.UI.Text` 기반 텍스트 플레이스홀더로 충분.

체크: `check_build.ps1` 실행.

### 4. `TestTrackBuilder.cs`에 GameManager/UI 조립 로직 추가
파일: `Assets/Scripts/Editor/TestTrackBuilder.cs`

기존 차량/타이머/HUD 생성 로직 뒤에 `GameManager` 오브젝트 생성 + `racers`/`vehicles`/`raceTimer` 배선,
`RaceHUD.gameManager` 연결, `RaceFlowUI` 인스턴스화 및 패널/이벤트 배선을 추가. 절차적 생성 방식은 그대로
유지 (`M2/Build Test Track Scene` 메뉴로 재생성 가능).

체크: `check_build.ps1` 실행.

### 5. `BuildCheck.cs` 정리
- `Assets/Scripts/Editor/BuildCheck.cs` → `Assets/Editor/BuildCheck.cs`로 이동 (CLAUDE.md가 명시한 경로에
  맞춤; `.meta` 파일도 함께 이동)
- `SceneSmokeTest()`가 참조하는 씬 경로를 존재하지 않는 `Stage_BikiniCity.unity` 대신 현재 존재하는
  `Assets/Scenes/SampleScene.unity`로 변경 (Stage_BikiniCity 관련 정식 씬 제작은 이번 범위 밖이므로 TODO
  주석으로 남김)
- `SceneSmokeTest`가 통과하려면 `SampleScene.unity`에 `GameManager`가 저장되어 있어야 함 — 이는 Unity
  에디터에서 `M2/Build Test Track Scene` 실행 후 씬 저장이라는 수동 1회 작업이 필요하며, 배치모드로 자동화할
  수 없는 부분이므로 별도 안내

체크: `check_build.ps1` 실행 (기본 `CompileCheck`). 이후 수동으로 씬을 저장한 뒤
`check_build.ps1 -Method "BuildCheck.SceneSmokeTest"`로 검증.

### 6. 씬/프리팹 구조 전환 여부 — 전환하지 않음
정식 아트 리소스가 없는 상태이므로 `Stage_BikiniCity` 등 정식 씬을 지금 만들어도 `TestTrackBuilder`가
생성하는 것과 실질적으로 다르지 않다. MainMenu/Lobby/ResultScreen을 별도 씬으로 분리하려면 씬 전환 플로우가
필요한데 이번 범위에서 요구되지 않았다. "결과화면" 요구사항은 같은 씬 내 `RaceFlowUI`의 결과 패널로 충족한다.
정식 씬/프리팹화는 아트/스테이지 콘텐츠가 들어오는 우선순위 3/5 단계에서 다시 판단한다.

## 실행 순서 및 검증
1. `VehicleController.SetInputLocked` 추가 → `check_build.ps1`
2. `GameManager.cs` 신규 작성 → `check_build.ps1`
3. `RaceHUD.cs` 확장 + `RaceFlowUI.cs` 신규 → `check_build.ps1`
4. `TestTrackBuilder.cs` 배선 추가 → `check_build.ps1`
5. `BuildCheck.cs` 이동 + `SceneSmokeTest` 씬 경로 수정 → `check_build.ps1`
6. (수동) Unity 에디터에서 `M2/Build Test Track Scene` 실행 → 브리핑→카운트다운→레이스→승리/무승부→결과
   화면 흐름 플레이테스트 → `SampleScene.unity` 저장
7. `check_build.ps1 -Method "BuildCheck.SceneSmokeTest"` 실행해 GameManager가 씬에서 발견되는지 확인

### 주요 파일
- `Assets/Scripts/Core/GameManager.cs` (신규)
- `Assets/Scripts/Player/VehicleController.cs`
- `Assets/Scripts/UI/RaceFlowUI.cs` (신규)
- `Assets/Scripts/UI/RaceHUD.cs`
- `Assets/Scripts/Editor/TestTrackBuilder.cs`
- `Assets/Editor/BuildCheck.cs` (이동, 기존 `Assets/Scripts/Editor/BuildCheck.cs`)
