# M2 : Myeongjin is Crazy (Racing) 2

학교 축제용 1v1 온라인 레이싱 게임. 이 문서는 프로젝트 전체 기획/기술 결정을 담고 있음.
Notion에 더 상세한 기획서가 있지만 오프라인 작업 시 이 문서가 단일 진실 소스(source of truth) 역할을 함.

## 기술 스택
- Unity 6.3 LTS
- 언어: C#
- **2.5D 방식**: 3D 프로젝트로 생성. 트랙/지형은 3D 메시(경사, 높이차 표현 가능), 캐릭터/차량/아이템은
  항상 카메라를 향하는 **빌보드 스프라이트**로 렌더링해서 일러스트 그림체 유지. 이동은 XZ 평면으로 제한.
- 네트워킹: Netcode for GameObjects + Unity Relay + Unity Lobby (방 코드 생성/입장, 온라인 1v1)
  - **개발 순서상 후반부에 연결**. 초반에는 로컬(싱글/핫싯)로 핵심 루프부터 완성.

## 개발 우선순위
1. 로컬 코어 루프: 차량 조작감 + 트랙 1개(비키니시티) + 바퀴/체크포인트/타이머 판정
2. 아이템 시스템 (스폰, 슬롯, 사용, 피격효과)
3. 스테이지 게이지 시스템 (공용 베이스 클래스로 3종 재사용 — 아래 참조)
4. Netcode for GameObjects 연결, 온라인 1v1 검증
5. 나머지 스테이지/아이템/승리조건 확장

## 진행 상황 (2026-07-09 기준, 3차 작업 완료)

### 완료
- **우선순위 1 (로컬 코어 루프) — 완료**
  - 차량 조작(`M2.Player.VehicleController`), 체크포인트/바퀴 판정(`M2.Core.Checkpoint`,
    `LapTracker`), 구간기록 타이머(`RaceTimer`) 구현됨.
  - 경기 진행 흐름(`M2.Core.GameManager` 신설): 조작법 안내(Briefing) → 3초 카운트다운 → 레이싱
    (1바퀴 180초 제한, 2/3/4바퀴 완주 시 +45/+30/+15초 지급) → 제한시간 초과 시 무승부, 목표 바퀴수
    완주 시 즉시 승리 → 결과화면(`M2.UI.RaceFlowUI`, 승자/무승부 + 바퀴별 스플릿 통계).
  - `GameManager`는 `List<LapTracker>`/`List<VehicleController>`를 순회하는 구조로, 나중에 로컬
    핫싯/온라인으로 레이서가 늘어나도 그대로 동작 (레이서별 이벤트 구독을 클로저+딕셔너리로 구분해
    2인 이상에서도 보너스 시간 중복 지급/승자 오판정이 나지 않도록 처리됨).
  - 정식 씬/프리팹은 아직 없음 — 아트 리소스가 없는 상태이므로 `TestTrackBuilder.cs`
    (`M2/Build Test Track Scene` 메뉴)가 절차적으로 트랙+GameManager+UI를 조립. 아트가 들어오는
    시점(우선순위 3/5)에 정식 씬/프리팹 전환 여부를 다시 판단하기로 함.
- **우선순위 2 (아이템 시스템) — 프레임워크만 완료, 로스터는 우선순위 5로 유예**
  - 스폰(균일 타입 + 10% 파생 승급), 슬롯(주/보조 2개, 꽉 찬 상태에서 주우면 주 슬롯 교체 — CLAUDE.md
    미명시라 가정으로 구현됨, 확정 필요), 사용, 피격효과(정지 후 서서히 재가속) 구현됨.
  - 현재는 base/1차 파생 tier만 존재 (휘발유/슈퍼휘발유, 폭탄/다이너마이트, 방패/가시방패). 최종형
    (재석 유, 원자폭탄, C4, 황금방패, 막대형 수류탄, 명진이의 러브레터 등)은 미구현.
- **BuildCheck 정리**: `Assets/Editor/BuildCheck.cs`로 이동 완료(과거 `Assets/Scripts/Editor/`에
  잘못 위치해 있었음). `SceneSmokeTest()`는 임시로 `Assets/Scenes/SampleScene.unity` +
  `GameManager` 존재 여부를 검증하도록 수정(TODO: Stage_BikiniCity 정식 씬이 생기면 경로 교체).

- **우선순위 3 (스테이지 게이지 시스템) — 비키니시티만 구현, 나머지 2개 스테이지는 미착수**
  - `M2.Stage.StageGaugeSystem` 추상 클래스 신설: 게이지 값/최대치, 초당 증감(passiveRatePerSecond,
    부호로 소모/축적 방향 표현), `ModifyValue`로 즉발 증감(아이템/피격 등), 고갈↔회복 전이 시
    `HandleDepleted`/`HandleRecovered` 훅 — 아프리카TV(멘탈)/네더요새(체온) 게이지도 이 클래스만
    상속하면 됨(아직 미구현).
  - `BikiniCityOxygenGauge`(산소 100, 초당 -2): 고갈 시 30초 유예 후 게임오버 이벤트, 유예 중
    회복하면 취소. `OxygenBubblePickup`/`OxygenBubbleSpawner`(숨방울, 30% 회복, `ItemPickup`/
    `ItemSpawner`와 동일 패턴)로 트랙에 배치.
  - `BikiniCityStageState`: "비법" 놓친 횟수 카운트(공격 아이템 피격 시 —
    `VehicleController.OnHitByAttackItem` 이벤트 / 지형지물 충돌 시 — `TerrainHazard` 컴포넌트가
    직접 `NotifyRecipeDropped()` 호출) + 목표/추가목표 별점 계산(최대 6★), 결과화면(`RaceFlowUI`)에
    표시됨. `TestTrackBuilder`가 지형지물 3개를 트랙에 배치(플레이스홀더 3D 큐브, 재충돌 방지용
    쿨다운 있음).
  - `BikiniCityStageUI`: 산소 수치 표시, 고갈 경고 시 화면 붉은 오버레이, 게임오버 시 차량 조작 잠금
    + 패널 표시.
  - `TestTrackBuilder`에 전부 배선 완료. 비키니시티 IP 리스킨 여부는 이번 작업 범위에서 의도적으로
    제외(기존 SpongeBob 참조 유지, 결정 보류 상태 그대로).
  - `StageGaugeSystem`에 `dangerAtMax` 플래그 추가됨(6차 작업) — 원래 "0에서 위험"만 가정했던 구조를
    "maxValue에서 위험"도 지원하도록 일반화(시작값도 안전한 쪽으로 자동 설정). 산소 게이지는 영향 없음.
- **아프리카TV(멘탈 게이지) — 구현 완료, `TestTrackBuilder` 배선까지 완료(7차 작업)**
  - `AfricaTvMentalGauge`(`dangerAtMax=true`): 가득 차면 일시 조작 불가(`lockoutDuration`, 고정 시간
    후 자동 해제 — 오요존 게임오버처럼 "회복 시 취소"가 아니라 그냥 타이머).
  - `AfricaTvStageState`: 공격 아이템 피격 시 별풍선 손실(놓친 횟수 증가) + 멘탈 게이지 추가 상승
    "이중 타격"(`VehicleController.OnHitByAttackItem` 재사용) + 별점 계산(놓친 별풍선 15/5/0회,
    완주시간 2:30/2:50/3:15).
  - `BroadcastAccidentZone`(10초 조향 반전 — `VehicleController.SetSteeringInvertedFor` 신규),
    `AccidentWarningZone`(진입 전 경고, UI만을 위한 static 이벤트).
  - `AfricaTvStageUI`: 멘탈 수치, 조작불가 배너, 방송사고 경고 배너.
  - **CLAUDE.md가 멘탈 게이지의 초당 변화량/피격당 상승폭을 명시하지 않아 플레이스홀더 수치로 구현함
    (밸런스 확정 필요).**
- **네더요새(체온 게이지) — 구현 완료, `TestTrackBuilder` 배선까지 완료(7차 작업)**
  - `NetherFortressTemperatureGauge`(`dangerAtMax=true`): 가득 차면 즉시 화상 게임오버(오요존과 달리
    유예 없음). `warningThresholdFraction`(기본 80%) 이상으로 올라갈 때마다 `OnBurnWarning` 발행 —
    "화상 경고 횟수" 별점 지표로 사용(임계값을 위→아래→위로 재통과하면 다시 카운트).
  - `NetherFortressStageState`: 공격 피격 시 체온 상승, `LavaZone` 안에서 맞으면 "정지 중 게이지가
    훨씬 빠르게 상승"을 큰 보너스치로 반영(콤보 전략). 별점 계산(화상 경고 5/2/0회, 완주시간
    1:00/1:10/1:20).
  - `GhastFireball`: 이동 발사체 AI는 범위 밖 — 고정 위치 트리거로 맞으면 트랙 바깥쪽으로 넉백
    (`VehicleController.ApplyKnockback` 신규, `isKnockedBack` 동안 스로틀 제어를 잠깐 넘겨줘야
    넉백 속도가 다음 FixedUpdate에 덮어써지지 않음).
  - `NetherFortressStageUI`: 체온 수치, 화상 경고 플래시, 화상 게임오버 패널.
  - **CLAUDE.md가 체온 게이지의 초당 상승량/피격당 상승폭을 명시하지 않아 플레이스홀더 수치로 구현함
    (밸런스 확정 필요, 산소보다 빠르게 오르도록만 방향 맞춤).**
- `RaceFlowUI` 결과화면이 `bikiniCityStageState`/`africaTvStageState`/`netherFortressStageState`
  중 채워진 하나를 골라 별점을 표시하도록 확장(스테이지 하나에 하나만 채워지는 걸 전제).
- **`TestTrackBuilder` 스테이지 선택 배선(7차 작업)**: `M2.Stage.StageAssembler`(신규, 런타임 세이프
  static 클래스)가 스테이지별 해저드/게이지/UI를 붙이고(`Attach`) 떼는(`Detach`) 로직을 한 곳에 모음
  — 에디터 빌드 시점(`TestTrackBuilder`)과 플레이 중 전환(`StageTestSelector`) 둘 다 이걸 통해서만
  스테이지를 구성해서 로직이 두 군데서 따로 놀지 않게 함.
  - `TestTrackBuilder` 메뉴가 3개로 분리됨: `M2/Build Test Track Scene/Bikini City`, `.../Africa TV`,
    `.../Nether Fortress`. 트랙 자체(지형/체크포인트/아이템)는 동일하게 만들고 스테이지 부분만
    `StageAssembler.Attach()`로 교체.
  - `StageTestSelector`(신규, `M2.UI`): 플레이 모드에서 레이스 시작 전(Racing/Finished가 아닐 때)에만
    숫자키 1/2/3으로 스테이지를 즉시 바꿔볼 수 있는 **임시 테스트용 기능**. 화면 좌하단에 현재
    스테이지 표시. 정식 게임에는 없는 개발 편의 기능 — 스테이지별 정식 씬이 생기면 제거될 예정.
  - 이 리팩터로 `TrackGeometry`(트랙 좌표 계산), `RendererColorUtil`(머티리얼 색상 지정),
    `SimpleUIFactory`(패널/텍스트 생성) 세 개의 작은 공용 런타임 헬퍼도 같이 뽑아냄 — 전부
    `TestTrackBuilder`에 중복돼 있던 것들을 한 곳으로 모은 것.
  - 아프리카TV용 `BroadcastAccidentZone`+`AccidentWarningZone`, 네더요새용
    `LavaZone`+`GhastFireball`이 이번에 처음으로 실제 트랙에 배치됨(그 전엔 테스트에서만 검증됨).

### 미착수
- **우선순위 4 (Netcode)**: Netcode for GameObjects / Relay / Lobby 관련 코드 전무. `M2.Network`
  네임스페이스 없음.
- **우선순위 5**: 아이템 최종형 로스터, 정식 스테이지 3종 아트/씬, 나머지 승리조건(별점 내기) 확장,
  아프리카TV/네더요새 게이지 밸런스 수치 확정.

## 다음 단계 후보 (8차 작업)
1. 정식 스테이지별 씬 분리 착수 — `TestTrackBuilder`/`StageTestSelector`는 어디까지나 임시 테스트
   도구이므로, 아트가 들어오기 시작하면 `Assets/Scenes/Stage_BikiniCity.unity` 등 정식 씬으로
   전환하고 `StageTestSelector`는 제거.
2. 진행하면서 미확정 항목(아이템 슬롯 교체 규칙, 막대형 수류탄 트리거 거리, 비키니시티 IP 리스킨 여부,
   아프리카TV/네더요새 게이지 밸런스 수치)도 같이 정리.

## 개발 워크플로우 — 컴파일 자동 체크 (중요, 항상 이 루프를 따를 것)
스크립트를 새로 만들거나 수정할 때마다, Unity 에디터를 직접 열어서 확인하는 대신
**아래 배치모드 스크립트로 컴파일 여부를 자동으로 확인**한다.

### 사전 조건
- `Assets/Editor/BuildCheck.cs` 가 프로젝트에 존재해야 함 (최초 1회만 복사)
- `check_build.ps1` (Windows) 또는 `check_build.sh` (Linux/macOS) 를 프로젝트 루트에 위치
- 스크립트 내 `UnityPath` 변수를 실제 Unity 6.3 LTS 설치 경로로 수정

### 컴파일 체크 루프
1. `.cs` 파일을 생성/수정
2. 배치모드로 컴파일 체크 실행:
   - Windows: `powershell -File check_build.ps1 -ProjectPath <절대경로>`
   - Linux/macOS: `bash check_build.sh <절대경로>`
3. **종료 코드 != 0** → `build.log`에서 `error CS` 줄을 읽어 원인 파악 → 코드 수정 → 2번으로
4. **종료 코드 == 0** (`✅ 컴파일 성공`) → 다음 작업 진행

이 루프는 에디터 GUI 없이 반복 가능하므로, 여러 파일을 연속 수정할 때 매번 실행해 회귀 에러를 조기에 잡는다.
필요 시 `BuildCheck.SceneSmokeTest()` 를 `-executeMethod` 로 호출해 씬 로드 + GameManager 존재 여부까지 검증한다.

## 개발 워크플로우 — PlayMode 자동 테스트 (실제 동작까지 검증할 때)
컴파일 체크는 "에러 없이 빌드되는가"만 확인함. 실제로 게임 로직이 의도대로 동작하는지(차량이 가속하는지,
바퀴 판정이 맞는지, 경기 흐름이 정상 진행되는지 등)는 **PlayMode 테스트**로 검증한다. Unity 에디터 GUI를
직접 켜서 조작할 수 없는 환경이므로, 이 자동화된 방식이 사실상 유일한 "플레이테스트" 수단.

### 구조
- `Assets/Scripts/M2.Runtime.asmdef` + `Assets/Scripts/Editor/M2.Editor.asmdef`: 게임 코드를
  asmdef로 분리해서 테스트 어셈블리가 참조할 수 있게 함 (asmdef 없는 기본 Assembly-CSharp은 다른
  asmdef에서 참조 불가능하기 때문).
- `Assets/Tests/PlayMode/M2.Tests.PlayMode.asmdef`: NUnit + Input System 테스트 유틸(`InputTestFixture`)
  참조하는 테스트 전용 어셈블리.
- 기존 테스트: `VehicleControllerTests`, `LapTrackerTests`, `GameManagerRaceFlowTests`,
  `BikiniCityOxygenGaugeTests`, `TerrainHazardTests`, `AfricaTvStageTests`,
  `NetherFortressStageTests`, `ScreenshotSmokeTest`(시각 확인용, 렌더링 불가 환경에서는
  `Inconclusive` 반환).

### 실행
- Windows: `powershell -File check_playtest.ps1 -ProjectPath <절대경로>`
- 결과: `playtest_results.xml`(NUnit XML), 로그: `playtest.log`
- **주의**: 디스플레이 없는 헤드리스 환경에서는 기본값(`-nographics`)으로 실행해야 함 — 아니면 그래픽
  디바이스 생성 단계에서 무한 대기함. 이 경우 스크린샷 캡처는 파일이 안 만들어지고 `Inconclusive`로
  보고됨(정상). 실제 디스플레이 있는 머신에서 `-Graphics` 스위치를 붙이면 렌더링은 되지만, **배치모드
  PlayMode 테스트는 애초에 실제 Game View 창을 띄우지 않으므로 `ScreenCapture.CaptureScreenshot`는
  GPU가 있어도 여전히 빈 파일임** — 진짜 스크린샷이 필요하면 에디터를 직접 열어 Play 모드로 확인하거나
  Standalone Player 빌드로 테스트를 돌려야 함(이번 범위에서는 안 함).
- **주의**: 배치모드 Unity 실행이 완료 신호(로그 마지막 줄 출력)를 내보내기 전에 프로세스가 몇 분간
  조용히 계속 실행 중일 수 있음(특히 asmdef 추가 등으로 전체 재임포트가 걸릴 때). `check_build.ps1`/
  `check_playtest.ps1` 둘 다 결과 파일이 안 보이면 60초까지 자동으로 폴링하도록 되어 있지만, 그래도
  실패로 뜨면 `tasklist`로 Unity.exe가 아직 떠있는지, `Temp/UnityLockfile`이 아직 있는지 먼저 확인할
  것 — 남아있으면 그냥 좀 더 기다리면 됨(강제 종료하지 말 것, 사용자가 직접 켜둔 에디터일 수도 있음).
  너무 빨리 재실행하면 "다른 Unity 에디터에서 이 프로젝트가 열려 있습니다" 락 충돌로 이번엔 진짜로
  실패함 — 락/프로세스가 완전히 사라진 뒤에만 재실행할 것.

### 새 테스트 작성 시
- `Assets/Tests/PlayMode/`에 `.cs` 추가, `[UnityTest]` + `IEnumerator` 사용
- 트리거 콜라이더를 코드로 붙일 때(`AddComponent<BoxCollider>()` 등) `Reset()`은 자동으로 안 불림 —
  `Reset()`은 에디터 UI에서 컴포넌트를 추가할 때만 호출되는 콜백이라, 순수 코드로 `AddComponent`할
  때는 `collider.isTrigger = true;`를 직접 명시해야 함. 안 하면 트리거가 아니라 솔리드 콜라이더가
  되어 `OnTriggerEnter`가 영영 안 불림 (실제로 `LavaZone` 테스트에서 이 문제로 한 번 걸림).
- 키보드 입력 시뮬레이션은 `InputTestFixture` 상속 + `InputSystem.AddDevice<Keyboard>()` 사용.
  `Press()`/`Release()`는 은근히 불안정해서 순서를 지켜야 함:
  - **`WaitForFixedUpdate()`를 한 번이라도 거친 뒤에 `Press()`/`Release()`를 호출하면**
    `ArgumentNullException: does not have an associated state`가 나기 쉬움 — 누르는 시점은 항상
    맨 처음 `yield return null;` 직후, 물리 스텝을 밟기 전에 미리 다 눌러둘 것.
  - **`Press()`를 프레임 사이 텀 없이 연달아 두 번 호출하면 첫 번째 입력이 조용히 씹힘**(예외도 안 남).
    키를 두 개 이상 눌러야 하면 `Press(a); yield return null; Press(b);`처럼 사이에 프레임을 끼울 것.
  - `Keyboard.current.xxxKey`는 캡처해서 재사용하지 말고 그때그때 새로 읽을 것.
  - 이런 제약 때문에 정 불안정하면 `Release()` 호출 자체를 생략해도 됨 — `TearDown()`이 테스트
    디바이스/상태를 알아서 정리해줌.

## 조작
| 키 | 기능 |
|---|---|
| ←/→ | 조향 |
| ↑/↓ | 가속/감속 |
| Ctrl | 가속 아이템 사용 |
| E | 공격/방어 아이템 사용 |

## 게임 모드 (호스트가 방 설정에서 선택)
- **아이템전**: 트랙에 랜덤 스폰되는 아이템(가속/공격/방어)으로 상대와 대결.
  플레이어는 주/보조 슬롯 2개를 가지며, 2개 다 찬 상태에서 아이템을 주우면 주운 아이템으로 교체됨.
- **스피드전**: 순수 실력전, 5바퀴 고정, 최대 100km/h.
  휘발유를 지속 자동 지급받음(사용 후 5초 뒤 재지급). 지급받고 30초간 미사용 시 30% 확률로 슈퍼 휘발유로 강화.

## 바퀴 수 / 난이도
1, 3(기본), 5바퀴 중 선택 가능. **바퀴 수가 많을수록 어려움** (5=전문가용, 3=캐주얼, 1=초심자 적응용).

## 승리 조건 (방 설정에서 선택)
- **단순 완주**: 목표 바퀴 수를 먼저 완주하면 승리.
- **별점 내기**: 완주 시 획득한 별점(스테이지당 최대 6★ = 목표 3★ + 추가목표 3★)이 더 높은 쪽 승리.
  동점이면 완주 시간이 빠른 쪽 승리. **양쪽 다 완주 못해 별점이 0이면 무승부.**

## 경기 진행 흐름
1. 카운트다운 전 간단 조작법 안내 화면 몇 초간 표시
2. 3초 카운트다운
3. 1바퀴째 제한시간 약 3분(180초)으로 시작
4. 2바퀴째 +45초, 3바퀴째 +30초, 4바퀴째 +15초 추가 지급
5. 제한시간 내 아무도 완주 못하면 무승부. 한 명이라도 완주하면 그 플레이어 승리.
6. 종료 시 결과화면(순위, 통계) 표시

## 아이템 시스템 상세
- **스폰 규칙**: 아이템이 스폰될 때 가속/공격/방어 중 균일한 확률로 종류가 정해지고,
  그 순간 10%의 확률로 해당 계열의 파생(상위) 아이템으로 대체되어 생성됨 (스폰 시 1회 판정).
- **공격 아이템 피격 시**: 잠깐 정지한 후 서서히 재가속 (코루틴으로 서서히 원래 속도로 복귀).

### 아이템 목록
**가속 아이템** (Ctrl로 사용)
- 휘발유 (2.0초, +20km/h)
  - 슈퍼 휘발유 (2.0초, +35km/h)
  - 해피버스데이투유 (4.0초, +0km/h — 개그용, 효과 없음)
  - **재석 유** (최종형, 1.0초, +100km/h)

**공격 아이템** (E로 사용)
- 폭탄 (3.0초 대기, 반경 5m)
  - C4 (수동 트리거 P키, 무한 대기, 반경 8m, 직격 시 즉시 기절)
  - 다이너마이트 (4.0초 대기, 반경 10m)
  - **원자폭탄** (최종형, 즉시폭발, 반경 10km)
    - 획득 후 2분 30초간 미사용 시 자동 업그레이드
    - 충전 중 공격 맞으면 즉시 터짐 (리스크 요소)
  - ☆SPECIAL SKIN☆ 명진이의 러브레터 (폭탄과 동일 스탯, 피격 시 하트 이펙트만 다름)
- 막대형 수류탄 (자동 근접 트리거, 반경 10m — **트리거 판정 거리 미확정, 확인 필요**)

**방어 아이템** (E로 사용)
- 방패 (4.0초 지속, 폭탄류 방어, 일회성)
  - 가시 방패 (4.0초 지속, 폭탄+다이너마이트 방어/반사, 일회성)
  - **황금방패** (최종형, 1.0초 지속, 즉사 제외 모든 공격 방어, 일회성)
  - ⚠️ 아이콘/플레이버 텍스트는 반드시 오리지널로 제작 (실존 국가 문장·정치풍자 금지)

## 스테이지 3종
공통 구조: 각 스테이지는 `StageGaugeSystem` 추상 클래스를 상속받는 고유 게이지를 가짐.
**게이지 로직은 상속으로 재사용, 중복 구현 금지.**

### 1. 비키니시티 (트랙 길이: 중간)
- ⚠️ 스폰지밥 IP 차용 중. 아트 작업 전 오리지널 대체 여부 최종 확인 필요 (축제 한정으로 감수 결정).
- 게이지: 산소 (최대 100, 초당 -2 소모, 숨방울로 30% 회복). 고갈 시 30초간 화면 붉어짐 → 게임오버.
- 공격 아이템에 맞거나 지형지물에 닿으면 "비법"을 떨어뜨림 (놓친 횟수가 별점 기준).
- 메롱시티(위험구역) 진입 전 경고 표지판/사이렌 표시. 심해 물고기에게 잡히면 해당 바퀴 첫 시작점 재시작.
- 목표(3★): 비법 놓친 횟수 10/3/0회 이하. 추가목표(3★): 1:45/2:00/2:30 이내 완주.

### 2. 아프리카TV (트랙 길이: 가장 김, 최고 난이도)
- 게이지: 멘탈 (팬 응원 존에서 회복). 가득 차면 잠시 조작 불가.
- 공격 아이템 피격 시 별풍선 손실 + 멘탈 게이지 추가 상승 (이중 타격).
- 방송사고 존: 진입 전 경고 있음. 들어가면 10초간 조작 반전.
- 목표(3★): 별풍선 놓친 횟수 15/5/0회 이하. 추가목표(3★): 2:30/2:50/3:15 이내 완주.

### 3. 네더 요새 (트랙 길이: 가장 짧음, 체온 게이지가 빠르게 상승해 밸런스 유지)
- 게이지: 체온 (오아시스 존에서 회복). 가득 차면 화상 → 즉시 게임오버.
- 용암 근처에서 공격 피격 시 정지 중 체온 게이지 평소보다 훨씬 빠르게 상승 (콤보 전략 유도).
- 가스트 파이어볼에 맞으면 코스 밖으로 튕겨나감.
- 목표(3★): 화상 경고 횟수 5/2/0회 이하. 추가목표(3★): 1:00/1:10/1:20 이내 완주.

## 코드 컨벤션
- 네임스페이스: `M2.Core`, `M2.Player`, `M2.Items`, `M2.Stage`, `M2.Network`, `M2.UI`
- 게이지 시스템은 항상 `StageGaugeSystem` 상속으로 구현
- 수치는 하드코딩하지 말고 Inspector 노출 public 필드로 작성

## 아직 미확정인 것 (구현 중 사용자에게 확인 필요)
- 막대형 수류탄의 자동 근접 트리거 판정 거리
- 아이템 드랍 확률 분포 (현재는 균일 확률로 가정)
- 비키니시티 IP 리스킨 여부 최종 확정

## 폴더 구조
```
Assets/
  Editor/           ← BuildCheck.cs 위치 (컴파일 자동 체크용)
  Scripts/{Core, Player, Items, Stage, Network, UI}/
  Prefabs/{Player, Items, Stage}/
  Scenes/ (MainMenu, Lobby, Stage_BikiniCity, Stage_AfricaTV, Stage_NetherFortress, ResultScreen)
  Art/{Sprites, UI}/
```