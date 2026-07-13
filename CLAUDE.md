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
  - 27~28차 작업(2026-07-12)에서 착수. Netcode for GameObjects는 Milestone 1(기본 연결 + 차량
    위치 동기화, 직접 IP 접속)까지 완료 — 실제 호스트+클라이언트 2인 라이브 접속 테스트까지
    통과 확인됨(28차). 29차(2026-07-12)에서 Milestone 2a(레이스 상태 서버-권위 동기화 — 실제
    트랙/체크포인트/GameManager를 온라인 씬에 올려 바퀴/타이머/승패를 호스트 권위로 복제),
    30차(2026-07-13)에서 Milestone 2b(아이템 스폰/획득/사용 서버-권위 동기화 — NetItemId 와이어
    스킴 + `NetworkItemSlots`/`NetworkItemSpawnManager` + 효과 RPC)를 코드+자동검증까지 완료
    (2a·2b 통합 라이브 2인 테스트는 사용자 확인 대기). Relay/Lobby(Milestone 3)는 아직 미착수.
    자세한 내용은 아래 "진행 상황"의 27~30차 작업 항목 참고.

## 개발 우선순위
1. 로컬 코어 루프: 차량 조작감 + 트랙 1개(비키니시티) + 바퀴/체크포인트/타이머 판정
2. 아이템 시스템 (스폰, 슬롯, 사용, 피격효과)
3. 스테이지 게이지 시스템 (공용 베이스 클래스로 3종 재사용 — 아래 참조)
4. Netcode for GameObjects 연결, 온라인 1v1 검증
5. 나머지 스테이지/아이템/승리조건 확장

## ⚠️ 알려진 버그

### 트랙 벽 충돌 판정 실패 (맵 탈출 가능) — 2026-07-10, 4차 수정 완료·플레이테스트로 검증됨
**증상**: 차량이 트랙 바깥쪽/안쪽 벽을 그냥 뚫고 나가버림 (맵 탈출).

**근본 원인 (4차 수정에서 특정)**: `VehicleController.ApplyThrottle`이 매 `FixedUpdate`마다
`rb.linearVelocity = forward * currentSpeed`로 velocity를 **강제 덮어쓰는 구조**가 PhysX의 충돌
해소(penetration resolution)를 완전히 무효화하고 있었음. PhysX가 충돌 응답으로 차를 밀어내도, 다음
프레임에 스크립트가 다시 벽 방향으로 velocity를 덮어쓰면 충돌이 없었던 것처럼 관통됨. 콜라이더
형태(박스/메시/캡슐)와 무관한 구조적 문제였음.

**시도 이력**:
1. **원래 코드(회전된 박스 세그먼트 겹쳐 쌓기)**: 커브 안쪽에서 톱니 모서리 → 끼임.
2. **1차 수정 (두께 0 MeshCollider)**: 터널링 → 뚫림.
3. **2차 수정 (두께 1m MeshCollider)**: PhysX 쿠킹 실패로 추정 → 뚫림.
4. **3차 수정 (CapsuleCollider 체인)**: 콜라이더 자체는 올바르나, velocity 덮어쓰기가 여전히 문제 →
   미검증 상태로 남아있었음.
5. **4차 수정 (현재, velocity 덮어쓰기 문제 해결)**:
   - `VehicleController`에 **벽 충돌 슬라이딩** 추가: `OnCollisionStay`에서 벽 접촉 노멀을
     축적하고, `ApplyThrottle`에서 velocity의 벽 방향 성분을 제거(Vector3.ProjectOnPlane)해서
     벽면을 따라 미끄러지도록 함. 벽 쪽으로 밀어 넣는 힘 자체를 원천 차단.
   - **분리 넉지(WallSeparationForce)**: 잔여 관통 해소용으로 벽 바깥 방향 미세 속도 추가.
   - **벽 PhysicsMaterial**: 마찰 0(Minimum combine) + 반발 0.2(Maximum combine)으로 설정해서
     캡슐 표면에 걸리지 않고 매끄럽게 슬라이딩.
   - **currentSpeed 클램프**: 벽에 닿은 상태에서 throttle force가 축적되어 접촉 해제 순간
     한꺼번에 관통하는 것 방지.

**검증 상태**: 사용자가 에디터 Play 모드에서 직접 확인 완료(2026-07-10) — 직진/빗각/급커브 안쪽 모두 뚫림 없음,
후진으로 벽에서 정상 탈출, 가속 아이템 부스트 상태에서도 벽 유지됨. **4차 수정으로 최종 해결.**

**관련 파일**: [TestTrackBuilder.cs](file:///C:/Users/User/Desktop/unity/M2_Racing/Assets/Scripts/Editor/TestTrackBuilder.cs)(`CreateWallRing` — CapsuleCollider + PhysicsMaterial),
[VehicleController.cs](file:///C:/Users/User/Desktop/unity/M2_Racing/Assets/Scripts/Player/VehicleController.cs)(`AccumulateWallContact`, `ApplyThrottle` wall-slide 로직),
[TrackGeometry.cs](file:///C:/Users/User/Desktop/unity/M2_Racing/Assets/Scripts/Stage/TrackGeometry.cs)(`OffsetPointAt`).

### 트랙 중앙 투명벽 끼임 버그 — 2026-07-10, 재진단 후 최종 해결
**증상**: 트랙을 달리다가 특정 커브 구간에서 갑자기 뚝 멈추고 그 이후 조작이 아예 안 먹음(조향도 안 됨).
히트박스 디버그(H 키)로 보면 벽/체크포인트 와이어프레임이 트랙을 대각선으로 가로지르며 서로 교차하는
모습이 보임 — 예전에는 이걸 "네더요새 용암 존 진입 직전의 거대한 큐브"로 오인했었음.

**시도 이력 및 실패 사례 (이전 진단들, 전부 증상 재현 실패)**:
1. **1차 시도 (실패)**: `VehicleController`의 `currentSpeed` 클램프가 범인이라 오판하여 클램프를 제거함.
2. **2차 시도 (실패)**: Ground Plane의 노멀 잔여값을 벽으로 오인하는 것이라 오판, `AccumulateWallContact` 수정.
3. **3차 시도 (실패)**: 트리거 `isTrigger`가 무효화된다고 오판, `Reset()` 콜백을 전부 `Awake()`로 교체.
4. **4차 시도 ("최종 해결"이라고 잘못 기록했던 진단)**: `StageAssembler`가 런타임에 `LavaVisual` 큐브의
   `BoxCollider`를 `Object.Destroy()`로 지우는데, Destroy가 프레임 끝까지 지연되는 유니티 특성상 핫스왑
   순간 1프레임 동안 솔리드 큐브가 남는다고 진단하고 `SafeDestroy`에서 즉시 `collider.enabled = false`
   처리를 추가함. **이 수정 자체는 유효하고 코드에 남겨둠(실제로 존재하는 별개의 버그였음)**, 하지만
   이번에 사용자가 히트박스 디버그(H 키)로 실제 현상을 스크린샷으로 보여주면서 **이게 진짜 원인이
   아니었다는 게 드러남** — 문제의 "벽"은 큐브가 아니라 트랙 벽/체크포인트 콜라이더 체인 자체가
   대각선으로 꼬여있는 모습이었음. 재현 위치도 네더요새 한정이 아니라 트랙의 특정 커브 구간 전반이었음.

**근본 원인 (최종 판명, 5차 진단)**:
`TrackGeometry.OffsetPointAt`이 중심선(centerline)에서 일정 거리(`lateralOffset`, 트랙 폭의 절반 = 6m)만큼
법선 방향으로 밀어서 벽/트랙 가장자리 좌표를 계산하는데, **중심선 자체가 자기교차하지 않아도 그 offset
곡선은 자기교차할 수 있음** (오프셋 거리가 그 지점의 곡률 반경보다 크면 오프셋 곡선이 접힘 — 표준적인
"오프셋 곡선 자기교차" 문제). `TestTrackBuilder`의 트랙 형태(`BuildControlPoints`, 극좌표 반지름을 3주기
sin으로 흔드는 방식)를 실제로 수치 계산해보면 커브 2곳(θ≈3.7, θ≈5.73 라디안)에서 곡률 반경이
**최소 4.18m**까지 좁아지는데, 벽/트랙 표면 오프셋은 6m를 그대로 사용하고 있었음 → 그 구간에서
`CreateWallRing`의 캡슐 체인과 `CreateTrackSurface`의 메쉬 가장자리가 둘 다 스스로 접혀 트랙을 대각선으로
가로지르는 콜라이더를 만듦. 차가 거기 진입하면 완전히 끼이고(속도 0), `ApplySteering`은
`minSpeedToSteer` 미만에서 조향을 무시하므로 조작도 같이 먹통이 됨 — "갑자기 뚝 멈추고 조작 안 먹는다"는
증상과 정확히 일치.

**최종 해결 방안**:
`TrackGeometry`에 `LocalRadiusOfCurvature(theta)`(3점 외접원 근사로 국소 곡률 반경 계산)와
`SafeLateralOffset(theta, desiredOffset)`(원하는 offset을 그 지점 곡률 반경의 85%로 클램프)를 추가.
`CreateWallRing`(벽)과 `CreateTrackSurface`(트랙 표면 메쉬 가장자리) 둘 다 고정 `TrackWidth/2` 대신
이 클램프된 값을 쓰도록 수정 — 두 군데가 항상 같은 클램프 로직을 쓰므로 벽이 표면 가장자리와 항상
정확히 맞물림. Node.js로 수치 검증: 클램프 적용 전엔 안쪽/바깥쪽 벽 모두 자기교차 있었고, 적용 후엔
자기교차 0건, 가장 좁아지는 지점도 폭 7.1m(차량 폭 1.2m 대비 충분)로 확인됨.

---

## 진행 상황 (2026-07-11 기준, 13차 작업 완료 — 사용자 플레이테스트 피드백 반영)

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
- **8차 작업(2026-07-10) — 실제로 에디터에서 플레이해보면서 나온 버그 4개 수정 + 테스트 편의 기능 추가**
  - **게이지가 안 움직이던 버그**: `AfricaTvMentalGauge`/`NetherFortressTemperatureGauge` 둘 다
    `passiveRatePerSecond`를 따로 안 정해줘서 부모 클래스(`StageGaugeSystem`) 기본값(-2, 산소 소모용)를
    그대로 물려받고 있었음. `dangerAtMax` 게이지는 0에서 시작하는데 -2가 적용되면 즉시 0으로
    clamp돼서 영원히 안 움직임 — 특히 네더요새 체온이 "빠르게 상승"해야 하는데 반대로 죽어있었음.
    각 `Awake()`에서 명시적으로 설정(멘탈=0 의도된 값, 체온=+6/초 플레이스홀더)해서 고침.
  - **충돌 후 후진이 안 먹던 버그**: `VehicleController`가 매 프레임 선속도는 강제로 덮어쓰지만
    각속도(`angularVelocity`)는 안 건드려서, 벽에 세게 부딪히면 물리엔진이 남긴 회전이 차를 미세하게
    계속 돌려버렸음 — 조향 입력 없인 차가 절대 저절로 안 돌게 `FixedUpdate`마다 `angularVelocity`를
    0으로 리셋.
  - **트랙이 실제 주행환경처럼 굽어지게 변경**: `TrackGeometry`를 단순 타원에서 닫힌 Catmull-Rom
    스플라인으로 교체(`PointAt`/`TangentAt`/`NormalAt` API는 그대로 유지해서 체크포인트/아이템/스테이지
    해저드 배치 코드는 손 안 댐). 첫 구현에서 X/Z축 반지름을 서로 다른 위상(sin/cos)으로 흔들다가
    트랙이 자기 자신과 교차하는 버그가 났음(사용자가 "맵이 고장나있다"고 리포트) — 하나의 극좌표
    반지름 변조로 통일해서 항상 단순 폐곡선이 되도록 고침.
  - **'시작' 버튼이 안 눌리던 버그**: 씬에 `EventSystem`이 아예 없어서 UI 클릭 자체가 어디로도
    전달이 안 되고 있었음. 게다가 프로젝트가 새 Input System 전용(`activeInputHandler: 1`)이라
    기본 `EventSystem`(레거시 `StandaloneInputModule`)으로는 있었어도 안 먹었을 것 —
    `TestTrackBuilder`에서 `EventSystem` + `InputSystemUIInputModule`을 생성하도록 고침.
  - **조작법 안내 수동 진행**: `GameManager.waitForManualStart`(기본 false, 기존 동작 그대로) 추가.
    테스트 트랙에서는 이 옵션을 켜서, 브리핑 화면이 고정 시간 대신 화면의 **'시작' 버튼**(또는
    **Space** 키)을 누를 때까지 대기함. 레이스가 실제 시작되면 기존 로직 그대로 안내 화면 자동으로 꺼짐.
  - **히트박스 디버그 토글**: `HitboxDebugToggle`(신규, `M2.UI`) — **H 키**로 씬의 모든 콜라이더 경계를
    GL 즉시모드 와이어프레임으로 Play 화면에 바로 표시/숨김. 충돌 판정이 실제로 어디서 일어나는지
    확인하기 위한 임시 개발 도구.
  - 이 세션에서 나온 수정들은 전부 24개 PlayMode 테스트로 검증(23 통과, 1 Inconclusive — 헤드리스
    스크린샷, 정상).
- **9차 작업 착수(2026-07-10) — 3D 배경/트랙 아트 파이프라인 준비**
  - `Assets/Art/{Models, Textures, Sprites, UI}` 폴더 신설(그동안 CLAUDE.md에 문서화만 되고 실제로는
    없었음). 로우폴리 + 평면 텍스처(컬러 팔레트 텍스처링) 스타일로 방향 확정.
  - `TestTrackBuilder.CreateTrackSurface`가 만드는 트랙 메시에 UV 배열 추가(그 전엔 vertices/triangles만
    있어서 텍스처를 얹으면 왜곡됐음) — U는 루프 진행률(진짜 arc length는 아니고 세그먼트 인덱스 기준,
    스플라인 구간별 속도가 달라 텍스처 밀도가 약간 들쭉날쭉할 수 있음, 필요시 나중에 개선)에 반복횟수
    (`TrackTextureRepeatsPerLap`)를 곱해서, V는 안쪽(0)/바깥쪽(1) 에지로 매핑.
  - `RendererColorUtil`에 `ApplyTexture(Renderer, Texture, tiling, doubleSided)` 추가 — 기존
    `ApplyColor`(단색 플레이스홀더)와 나란히 공존, 아트가 준비된 항목만 점진적으로 텍스처 경로로 전환
    예정. 아직 실제 텍스처 에셋은 없어서 트랙은 여전히 `ApplyColor`로 단색 렌더링 중.
  - 아트 제작 도구는 Kenney.nl(무료 CC0 로우폴리 에셋팩, 배경 소품 채우기용) + Blockbench(무료 로우폴리
    전용 모델링+텍스처 페인팅 툴, 스테이지 고유 오브젝트 제작용) 조합으로 결정, AI 텍스트/이미지→3D
    (Meshy/Tripo3D)는 시간 부족 시 히어로 오브젝트 초안 생성 보조용으로만 사용하기로 함. 실제 모델/텍스처
    제작 자체는 이번 세션 범위 밖(에셋 파일이 아직 없음).
- **10차 작업 완료(2026-07-10) — 트랙 벽/표면 자기교차 버그 재진단·해결, 벽 충돌 피드백 개선, 확대,
  비키니시티 정식 씬 분리**
  - **트랙 중앙 투명벽 버그 재진단**: "최종 해결"이라 기록했던 `LavaVisual` 1프레임 콜라이더 진단은
    실제 원인이 아니었음이 사용자의 히트박스 디버그(H 키) 스크린샷으로 드러남. 진짜 원인은
    `TrackGeometry.OffsetPointAt`이 중심선에서 고정 거리(`TrackWidth/2`)만큼 오프셋할 때, 커브가 그
    거리보다 더 급하게 꺾이는 지점(오프셋 거리 > 국소 곡률 반경)에서 오프셋 곡선 자체가 접혀버리는
    표준적인 "오프셋 곡선 자기교차" 문제였음 — 벽/트랙 표면이 커브 2곳에서 대각선으로 트랙을 가로질러
    차가 완전히 끼이고(속도 0) `minSpeedToSteer` 때문에 조향도 같이 먹통이 됨.
    `TrackGeometry`에 `LocalRadiusOfCurvature`(3점 외접원 근사)와 `SafeLateralOffset`(그 지점 곡률
    반경의 85%로 오프셋 클램프) 추가, `CreateWallRing`/`CreateTrackSurface` 둘 다 이걸 통해 오프셋을
    계산하도록 수정. Node.js로 Catmull-Rom+외접원 수식을 그대로 포팅해 자기교차 0건 수치 검증, 사용자가
    Play 모드에서 직진/빗각/급커브/후진/부스트 전부 확인 완료. 자세한 시도 이력은 위 "알려진 버그"
    섹션 참고.
  - **벽 충돌 시 속도 감쇠(`wallScrapeDeceleration`)**: 위 수정 직후 벽을 따라 미끄러질 때 속도가 전혀
    안 깎여서 "레이싱 같지 않다"는 피드백 → `VehicleController.ApplyThrottle`에서 실제로 벽 쪽으로
    파고드는 순간(`intoWall < 0`)에만 `currentSpeed`를 초당 `wallScrapeDeceleration`(기본 10)만큼
    깎되, `minSpeedToSteer + 1` 밑으로는 안 내려가게 바닥을 둠(그 밑으로 내려가면 예전의 "끼어서 조향
    불가" 버그가 재현되므로). 살짝 스치기만 하면(`intoWall >= 0`) 페널티 없음.
  - **비키니시티: 벽 충돌도 "비법" 드랍**: `VehicleController`에 `OnWallHit` 이벤트 신설(1초 쿨다운으로
    연속 슬라이딩 중 과다 카운트 방지), `BikiniCityStageState`가 구독해서 `NotifyRecipeDropped()` 호출
    — 공격 아이템/지형지물에 이어 벽 충돌도 놓친 횟수에 포함됨.
  - **트랙 확대(2인용)**: `BaseRadiusX 32→48`, `BaseRadiusZ 22→34`, `TrackWidth 12→16`. 새 곡률
    클램프가 동적으로 안전을 보장하므로 상수만 바꿔서 확대 — 가장 좁아지는 지점도 폭 11m 이상 유지,
    한 바퀴 길이 약 202m→306m. 이 상수들은 3개 스테이지가 공유하므로 아프리카TV/네더요새도 같이 커짐
    (의도된 부작용).
  - **비키니시티 정식 씬 분리 착수** (다음 단계 후보 3번 완료, 비키니시티 한정): `TestTrackBuilder.Build`가
    `rootName`/`attachStageTestSelector` 옵션 파라미터를 받도록 확장(기존 3개 메뉴 아이템은 무변경).
    신규 `TestTrackBuilder.BuildAndSaveBikiniCityScene()`(+ 메뉴 `M2/Build Persisted Scene/Bikini City`)이
    빈 씬을 새로 만들고 `StageTestSelector` 없이 비키니시티만 빌드한 뒤
    `Assets/Scenes/Stage_BikiniCity.unity`로 저장. 헤드리스 검증용으로 `BuildCheck.BuildBikiniCityScene()`
    래퍼 추가, `BuildCheck.SceneSmokeTest()`가 이제 이 새 씬을 열어서 확인(예전엔 `SampleScene.unity`
    하드코딩 + TODO였음, 이제 해소). `check_build.ps1`도 `M2_SCENE_BUILD_OK` 마커를 인식하도록 수정.
    **고정(freeze)의 트레이드오프**: 이 씬은 저장 시점 스냅샷이라 이후 `TestTrackBuilder` 변경사항이
    자동 반영 안 됨 — 갱신하려면 `BuildAndSaveBikiniCityScene`을 다시 실행해서 통째로 덮어써야 하고,
    그러면 씬에 직접 손댄 변경사항은 날아감. 아프리카TV/네더요새는 여전히 `TestTrackBuilder`+
    `StageTestSelector` 그대로라 상수 변경이 항상 자동 반영됨.
  - **검증 완료**: 에디터를 닫은 뒤 컴파일 체크 → `BuildBikiniCityScene` → `SceneSmokeTest` →
    PlayMode 전체(23개 중 22 통과, 나머지는 헤드리스 환경상 정상적인 Inconclusive) 순서로 전부 통과.
    사용자가 에디터에서 직접 Play로 확인(트랙 확대, 벽 충돌 피드백, 비법 드랍 전부 정상).
  - 커밋됨(`5b94c23`) — 단, `Assets/Art/`(134MB, Kenney 팩 전체)는 `.gitignore`로 제외하고 실제 코드가
    참조하는 파일만 `Assets/Resources/KenneyProps`에 커밋. CC0라 필요하면 kenney.nl에서 다시 받으면 됨.
- **11차 작업 완료(2026-07-10) — 드리프트 도입(브레이크 대체), 트랙 코너 레이아웃 재설계**
  - **드리프트(Shift, 브레이크 대체)**: 플레이테스터 피드백("드리프트 있으면 재밌겠다")으로 브레이크
    키를 드리프트로 교체. 마리오카트식 — Shift 누른 채 조향하면 `driftTurnMultiplier`(기본 1.6배)로
    차체(facing)가 더 빨리 돌지만, 실제 이동 방향(`moveDirection`, 신규 필드)은
    `driftSlipRecoverySpeed`(기본 200°/s)로 천천히 따라가서 그 차이가 슬라이드로 나타남. 뗀 순간
    홀드 시간(최소 `minDriftHoldTimeForBoost`=0.3초 이상)에 비례해 `maxDriftBoostSpeed`(기본 8, 최대
    `driftBoostChargeTime`=1.5초 홀드 시 풀차지)까지 부스트 지급, `driftBoostDuration`(기본 1초) 유지
    — 아이템 가속(`itemSpeedBonus`)과는 별도 필드(`driftSpeedBonus`)로 분리해서 서로 안 덮어씀.
    드리프트 안 하면 `moveDirection`이 매 프레임 `transform.forward`로 그대로 스냅되므로 예전 항상-정렬
    거동과 100% 동일 — 드리프트할 때만 슬립이 생김. 벽 충돌 시엔 이미 `wallScrapeDeceleration`으로
    속도가 깎이므로 예전 브레이크 없어도 급감속 수단은 남아있고, 후진(↓)도 기존처럼 먼저 순방향 운동량을
    `brakeDeceleration`으로 죽인 뒤 반대로 가속하는 구조라 급정거 수단 자체는 사라지지 않음.
  - **트랙 코너 레이아웃 재설계**: 기존 절차적 극좌표 wobble 타원(`BuildControlPoints`, sin 변조)을
    버리고 손으로 배치한 14개 컨트롤 포인트로 교체(`TestTrackBuilder.BikiniCityTrackControlPoints`) —
    2인용 추월전을 염두에 두고 (a) 시케인이 낀 프론트 스트레이트, (b) 넓게 감아 도는 헤어핀(급제동
    추월 포인트), (c) 순수 직선 약 39m의 백 스트레이트(드래프팅용), (d) 나란히 달려도 안전한 넓은
    웨스트 스위퍼로 구성. `TrackGeometry.PointAt/TangentAt/NormalAt` API는 그대로라 체크포인트/아이템
    스폰/스테이지 해저드 배치 코드는 무변경. Node.js로 사전 검증: 자기교차 0건(64/128 세그먼트 둘 다),
    최소 곡률 반경 9.3m(트랙 폭 절반 8m보다 넉넉해서 어디서도 안 좁아짐), 한 바퀴 길이 약 306m 유지.
    설계 중 컨트롤 포인트 간격이 불균등하면 Catmull-Rom이 오버슈트해서 의도보다 훨씬 좁은 곡률을
    만드는 문제를 실제로 겪음(닫힘 지점 근처에 너무 가까운 점 하나 때문에 곡률 반경 1m까지 떨어짐) —
    제거해서 해결. `CreateGround`의 바닥 크기 계산도 옛 반지름 공식 대신 컨트롤 포인트 실제 bounding box
    기준으로 변경.
  - **검증 완료**: 에디터를 닫은 뒤 컴파일 체크 → `BuildBikiniCityScene`(새 트랙 모양 반영해서
    `Stage_BikiniCity.unity` 재생성) → `SceneSmokeTest` → PlayMode 전체(22/22 통과) 순서로 전부 통과.
    사용자가 에디터에서 직접 Play로 확인 완료 — 새 코너 레이아웃 주행감, 드리프트 느낌, 벽 충돌/비법
    드랍 등 기존 기능이 새 트랙에서도 잘 동작함.
- **12차 작업 완료(2026-07-11) — 아프리카TV/네더요새 정식 씬 분리, 스테이지별 고유 트랙 레이아웃**
  - **결정**: 다음 단계 후보 4번에서 미뤄뒀던 질문("정식 씬으로 뗄지" / "고유 레이아웃 vs 공용 트랙 재사용")을
    사용자에게 확인 — 두 스테이지 모두 정식 씬으로 분리하고, 비키니시티처럼 각자 손으로 설계한 고유
    코너 레이아웃을 새로 만들기로 결정(CLAUDE.md 기획 문서가 아프리카TV="트랙 길이: 가장 김",
    네더요새="트랙 길이: 가장 짧음"이라 명시하는데, 그동안 셋이 전부 비키니시티 트랙을 그대로 공유하고
    있었던 것 자체가 기획과의 괴리였음).
  - **아프리카TV 트랙**(`TestTrackBuilder.AfricaTvControlPoints`, 18개 컨트롤 포인트): 프론트
    시케인 → 더블 에시스(esses) → 넓은 헤어핀 → 세 스테이지 중 가장 긴 백 스트레이트(드래프팅 존) →
    웨스트 스위퍼로 폐곡선. Node.js로 사전 검증: 자기교차 0건(64/128 세그먼트 둘 다), 최소 곡률 반경
    7.13m(한 지점에서 트랙 폭이 16m→12.1m로 좁아짐 — 차량 폭 1.2m 대비 10배 이상 여유가 있어서 굳이
    비키니시티처럼 모든 코너를 8m 이상으로 강제하지 않고 그대로 둠), 한 바퀴 약 444m(비키니시티
    306m 대비 확실히 김).
  - **네더요새 트랙**(`TestTrackBuilder.NetherFortressControlPoints`, 9개 컨트롤 포인트): 프론트
    스트레이트 → 넓은 헤어핀 → 백 스트레이트 → 웨스트 스위퍼로 좁게 도는 요새 안뜰 형태. 처음 설계한
    10점짜리 초안은 닫히는 지점 근처 두 점이 5m밖에 안 떨어져 있어서 Catmull-Rom이 오버슈트 →
    자기교차 나는 걸 Node.js 검증에서 잡아냄(11차 작업에서 이미 문서화된 것과 동일한 실패 패턴) —
    그 점 하나를 제거하고 9점으로 정리해서 해결. 최종: 자기교차 0건, 최소 곡률 반경 14.49m(트랙 폭
    절반 8m보다 넉넉해서 전 구간 16m 풀 폭 유지), 한 바퀴 약 190m(세 스테이지 중 가장 짧음).
  - **`TestTrackBuilder` 리팩터**: 스테이지 하나에 트랙 하나만 있던 구조(`BikiniCityTrackControlPoints`
    + 전역 `readonly Geometry` 필드 하나)를 `ControlPointsFor(StageType)` + `Build()` 시작 시
    재할당하는 `static Geometry` 필드로 교체. `TrackGeometry`(Catmull-Rom + `SafeLateralOffset`
    곡률 클램프)와 `StageAssembler`의 해저드 배치 코드는 전부 `geo.PointAt`/`OffsetPointAt` 같은
    API로만 좌표를 얻으므로 무변경 — 스테이지별로 다른 모양이 들어와도 자동으로 맞게 동작함.
  - **정식 씬 빌더 확장**: `BuildAndSaveBikiniCityScene()` 하나뿐이던 걸 `BuildAndSavePersistedScene
    (StageType, rootName, scenePath)` 공용 헬퍼로 리팩터하고, `BuildAndSaveAfricaTvScene()`/
    `BuildAndSaveNetherFortressScene()` 및 메뉴 `M2/Build Persisted Scene/Africa TV`,
    `.../Nether Fortress` 추가. `Assets/Scenes/Stage_AfricaTV.unity`, `Stage_NetherFortress.unity`
    신규 생성됨. `BuildCheck.cs`도 같은 방식으로 `BuildScene`/`SmokeTestScene` 공용 헬퍼 + 스테이지별
    `Build*Scene`/`SceneSmokeTest*` 6개 메서드로 정리(기존 `BuildBikiniCityScene`/`SceneSmokeTest`
    이름은 그대로 유지해서 `check_build.ps1` 기본 호출은 무변경).
  - **알려진 트레이드오프(비키니시티와 동일)**: 이 두 씬도 저장 시점 스냅샷이라 이후 컨트롤 포인트를
    또 바꾸면 `BuildAndSave*Scene`을 다시 실행해서 통째로 덮어써야 함.
  - **StageTestSelector(1/2/3 테스트 전환)의 제약, 이번에 처음으로 의미가 생김**: 이 스위처는 스테이지
    해저드/게이지/UI만 갈아끼우고(`StageAssembler.Attach/Detach`) 트랙 지오메트리 자체는 다시 만들지
    않음 — 세 스테이지가 트랙을 공유하던 지금까지는 상관없었지만, 이제 트랙 모양이 스테이지마다
    달라졌으므로 예를 들어 `M2/Build Test Track Scene/Bikini City` 메뉴로 만든 뒤 Play 중 2번 키를
    누르면 "비키니시티 모양 트랙 위에 아프리카TV 해저드"가 나오는 하이브리드 상태가 됨(의도된 동작은
    아니지만 버그도 아님 — 애초에 "정식 게임에는 없는 임시 테스트 도구"로 문서화돼 있던 기능이라
    지오메트리까지 다시 만들게 확장하지 않고 이 제약을 문서로만 남겨둠). 각 스테이지의 정식 트랙
    모양을 보려면 `M2/Build Test Track Scene/<스테이지>` 메뉴로 새로 빌드하거나, 정식 씬
    (`Stage_*.unity`)을 열 것.
  - **검증 완료**: 컴파일 체크 → `BuildAfricaTvScene`/`BuildNetherFortressScene` →
    `SceneSmokeTestAfricaTv`/`SceneSmokeTestNetherFortress` → PlayMode 전체(23개 중 22 통과, 나머지는
    헤드리스 환경상 정상적인 Inconclusive) 순서로 전부 통과. 헤드리스 환경이라 사용자의 에디터 Play
    확인은 아직 못함 — 다음 세션에서 실제로 플레이해보며 두 트랙의 체감 난이도/곡률 확인 필요.

- **13차 작업 완료(2026-07-11) — 사용자가 에디터에서 직접 플레이해보고 준 피드백 반영**
  - **네더요새 체온 게이지가 Briefing/Countdown 중에도 이미 오르고 있던 버그(최우선 수정)**:
    `StageGaugeSystem.Update()`가 `GameManager.CurrentState`를 확인하지 않고 항상
    `passiveRatePerSecond`를 적용하고 있었음 — 레이스가 실제로 시작하기도 전에 게이지가 다
    차서, 유예 없이 즉시 게임오버되는 네더요새는 "시작" 버튼을 늦게 누르면 레이스가 시작하는
    순간 이미 조작 불가 상태였음(사용자 리포트: "차량이 바로 멈춰버리니까 물리엔진 오류난
    것 같아" — 실제로는 오버레이 자체는 정상 작동했고, Briefing 중 조기 발동이 근본 원인).
    `StageGaugeSystem`에 `GameManager` 참조를 추가해 `CurrentState == Racing`일 때만 수동
    tick이 적용되도록 게이트 추가 — 3개 스테이지 게이지 전부에 공통 적용되는 베이스 클래스
    수정이라 비키니시티 산소에도 동일하게 적용됨(그동안은 -2/초라 Briefing 7초 정도로는
    티가 안 났을 뿐, 같은 버그였음).
  - **네더요새 체온 상승속도 6/초 → 1/초**(사용자: "5초에 5씩, 1초에 1씩 닳도록").
  - **네더요새 오아시스(냉각) 존 신규 구현**: CLAUDE.md 기획 문서에 "체온 (오아시스 존에서
    회복)"이라고만 적혀 있었을 뿐 실제로는 한 번도 구현된 적이 없었음(플레이테스트로 처음
    드러남). `OasisZone.cs`(신규) — 안에 머무는 동안 `OnTriggerStay`로 체온 게이지를 서서히
    식힘. 사용자 요청대로 "세로(진행방향)로 길고 가로(폭방향)로 좁은" 형태로 배치
    (`StageAssembler.CreateOasisZone`, 폭 TrackWidth*0.35/깊이 TrackWidth*1.4, 트랙 폭 비례라
    스테이지별로 다시 좁혀도 같이 스케일됨) — 식히려면 레이싱 라인을 벗어나 일부러 좁은
    통로로 들어와야 하는 트레이드오프.
  - **네더요새 용암존 축소**: 기존 TrackWidth*0.6 정사각형(중심에서 0.2배 오프셋)이 좁아진
    트랙 폭과 맞물려 사실상 피할 수 없었음(사용자: "피하기 어려워") — TrackWidth*0.3로 축소,
    오프셋은 0.32배로 가장자리 쪽에 붙여서 반대편에 확실한 회피 공간을 남김.
  - **네더요새 트랙 폭 16m → 11m**(사용자: "폭도 좀 넓은 것 같고") — `TestTrackBuilder`가
    그동안 3개 스테이지 전부 하나의 `const TrackWidth`를 공유하던 걸 `TrackWidthFor(StageType)`
    per-stage 값으로 교체(비키니시티/아프리카TV는 16m 그대로, 네더요새만 11m). Node.js로
    재검증: 11m 폭에서도 자기교차 0건(최소 곡률 반경 14.49m가 트랙 폭 절반 5.5m보다 훨씬 커서
    전 구간 풀 폭 유지).
  - **네더요새 화상 게임오버 텍스트**: "화상!\nGAME OVER" → "게임 오버!\n(화상)"으로 정리
    (오버레이/입력잠금 자체는 이미 정상 구현돼 있었음 — 위 Briefing 조기발동 버그가 실제 원인).
  - **아프리카TV 방송사고존에 물리적 표식 추가**: 그동안은 화면 오버레이 경고만 있고 트랙
    위에는 아무 표식도 없어서 "그냥 반전되는 느낌"이었음(사용자 피드백) —
    `StageAssembler.CreateWarningSign`으로 경고 구역 바로 앞 트랙 가장자리에 주황색 표지판
    오브젝트 배치, 사고 구역 자체에도 `AddFlatGroundTint`로 바닥에 보라색 타일을 깔아 위험
    구역이 실제로 도로 위에서 보이게 함.
  - **아프리카TV 방송사고존/경고존 폭 축소**: 기존엔 `geo.TrackWidth` 그대로 트랙 전체 폭을
    가로막고 있어서 무조건 걸릴 수밖에 없었음(사용자: "무조건 닿는 게 아닐 수 있도록") —
    `CreateZoneTrigger`에 `widthRatio`/`lateralOffsetRatio` 파라미터 추가, 방송사고존은
    0.55배 폭으로 좁히고 0.25배만큼 한쪽으로 밀어서 반대편에 확실한 회피 공간을 둠.
  - **아프리카TV 멘탈 게이지가 안 오르던 문제**: `AfricaTvMentalGauge.passiveRatePerSecond`가
    의도적으로 0(공격 아이템 피격으로만 오르게 설계됨)이었는데, 혼자 플레이테스트할 땐 다른
    플레이어의 공격을 맞을 일이 없어서 게이지가 영원히 0에 머물렀음 — `BroadcastAccidentZone`에
    `OnAccidentEntered` 정적 이벤트를 추가하고 `AfricaTvStageState`가 구독해서 방송사고존
    진입 시에도 멘탈 게이지가 오르도록 함(조향반전 효과에 새 부수 효과 추가, 이중 타격 패턴과
    동일한 방식).
  - **공통: 출발/도착선 마커 추가**(사용자: "출발/도착선이 없어") — `TrackTextureFactory`에
    체커보드 텍스처 생성기 추가, `TestTrackBuilder.CreateStartFinishLine`이 theta=0(체크포인트
    0/차량 스폰 위치와 동일) 지점에 트랙 폭 전체를 가로지르는 체커 무늬 라인을 깜(3개 스테이지
    전부 공용 로직이라 자동 적용).
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드(`BuildBikiniCityScene`/`BuildAfricaTvScene`/
    `BuildNetherFortressScene`) → 3개 스모크 테스트 → PlayMode 전체(22/22, 나머지 1개는
    헤드리스 환경상 정상적인 Inconclusive) 순서로 전부 통과.
  - **작업 중 발견한 도구 이슈(참고용)**: 이번 세션에서 Windows-MCP(데스크톱 자동화)로 직접
    Unity 에디터를 열어 플레이해보려 시도했으나, 좌표 기반 클릭(`Click`의 `loc` 파라미터)이
    이 세션에서 구조적으로 깨져 있었고(배열 파라미터가 문자열로 잘못 직렬화됨), 새 Input
    System 프로젝트라 키보드 합성 입력(`keybd_event`)도 차량에 반영되지 않아 실질적인
    플레이테스트가 불가능했음 — 사용자가 직접 플레이하고 피드백을 주는 방식으로 전환함.
    (에디터를 실행한 채로 두면 배치모드 빌드가 "다른 에디터에서 프로젝트가 열려있음" 에러로
    막히므로, 다음에도 Windows-MCP로 에디터를 열었다면 빌드 체크 전에 반드시 닫을 것 —
    사용자가 명시: 깜빡하고 안 끄면 직접 닫고 다시 켜달라고 안내할 것.)
- **14차 작업 완료(2026-07-11) — 사용자 2차 피드백 반영(13차 수정에 대한 재플레이 결과)**
  - **아프리카TV 방송사고존 폭 재조정**: 13차에서 0.55배로 좁힌 게 오히려 어딘가 어색했던
    모양(사용자: "버그가 좀 있네", 정확한 원인은 특정하지 않음 — 큰 문제 아니라며 크기만
    키워달라고 요청) — 0.8배로 다시 넓힘(오프셋은 그대로 유지해서 반대편에 여전히 약 5.6m
    회피 공간은 남김).
  - **아이템 줍기 판정 확대**: `ItemSpawner`가 만드는 픽업 `SphereCollider`가 프리미티브
    기본값(반지름 0.5, 시각 스케일 1.2 적용 후 실질 반경 0.6m)에 머물러 있어서 차량 폭
    1.2m와 비슷한 크기라 "달리면서 줍기엔 너무 빡빡함"(사용자: "먹기 쉽게 조금 널널하게") —
    반지름을 1.1로 명시적으로 키움(실질 반경 약 1.3m, 시각 스프라이트 크기는 그대로 두고
    판정 콜라이더만 더 관대하게).
  - **트랙 길이 전체 1.2배 확대**: 아프리카TV 기준 휘발유 부스터를 계속 이어 쓰면서 3바퀴
    도는 데 1분밖에 안 걸릴 정도로 전체적으로 짧게 느껴졌음(사용자: "맵 길이가 전체적으로
    짧은 거 같기도... 아주 조금만 넓히자") — `TestTrackBuilder`의 3개 스테이지 컨트롤
    포인트 배열 전부를 감싸는 `Scale(points, factor)` 헬퍼 + `TrackLengthScale = 1.2f`
    상수 추가. 균일 스케일이라 자기교차 여부·곡률 대 트랙폭 비율 등 기존에 검증된 성질이
    전부 그대로 유지되지만(둘 다 절대 크기에 의존하지 않음), Node.js로 재검증까지 완료:
    비키니시티 367m, 아프리카TV 533m, 네더요새 228m으로 전부 늘었고 자기교차는 여전히
    0건. `TrackWidth`(폭)와는 독립적인 축이라 13차에서 좁힌 네더요새 폭(11m)은 그대로 유지.
  - **작업 도중 에디터 종료 관련 대화**: 사용자가 재차 명확히 함 — 배치모드 빌드가 막힐 때
    Claude가 먼저 물어보지 말고 그냥 닫을 것(이미 포괄적으로 허용된 사항이었는데 이번에
    다시 물어봐서 지적받음). 또한 여러 단계짜리 작업(빌드/테스트 연속 실행 등) 진행 중엔
    마지막에 결과만 몰아서 요약하지 말고 단계마다 짧게 진행 상황을 말하면서 작업할 것 — 둘
    다 메모리에 기록함.
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드 → 3개 스모크 테스트 → PlayMode 전체(22/22)
    순서로 전부 통과.
- **15차 작업 완료(2026-07-11) — 상태이상 HUD 추가, 커밋 자동화 방침 확정**
  - 사용자가 14차 수정사항을 실제로 플레이해보고 만족("오 됐다") — 방송사고존 크기/아이템
    판정/트랙 길이 전부 확정.
  - **상태이상(디버프/버프) HUD 신규 추가**(사용자 제안): 화면 우측 상단(스테이지 게이지
    라벨 바로 아래, `(-20,-70)`)에 현재 차량에 걸려있는 상태를 나열해서 보여줌 — 기절
    (`VehicleController.IsStunned`), 조향 반전(`IsSteeringInverted`), 넉백(`IsKnockedBack`),
    방어막(`HasShield`), 가속 부스트(`HasSpeedBoost`), 드리프트 부스트(`HasDriftBoost`).
    `VehicleController`에 기존 private 상태 필드를 읽기 전용으로 노출하는 프로퍼티만 추가하고,
    새 `VehicleStatusHUD`(`M2.UI`)가 매 프레임 폴링해서 텍스트로 그림(상태마다 시작/종료
    이벤트 쌍을 새로 만들 필요 없이 단순 bool 읽기로 충분). 스테이지별 게이지(산소/멘탈/체온)는
    이미 각자 UI가 있어서 건드리지 않음 — 이 HUD는 범용 차량 상태만 다룸.
  - **커밋 워크플로우 확정**: 사용자가 명시적으로 요청 — 이 프로젝트에서는 검증(컴파일+빌드
    +테스트) 통과한 작업 단위마다 매번 커밋해도 되는지 물어보지 말고 바로 커밋할 것(단,
    `git push`는 여전히 매번 승인받을 것). CLAUDE.md의 "개발 워크플로우 — 커밋" 섹션에
    명문화함.
  - **에디터 종료 관련 재확인**: 배치모드 빌드가 "다른 에디터에서 열려있음"으로 막힐 때
    Claude가 다시 한번 먼저 물어봤다가 지적받음("내가 너보고 닫을 수 있으니까 닫으라고
    했잖니?") — 이 허용은 조건 없이 포괄적임을 재확인, 메모리에 강화 기록함.
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드 → 3개 스모크 테스트 → PlayMode 전체(22/22)
    순서로 전부 통과.
- **16차 작업 완료(2026-07-11) — 아프리카TV 방송사고존 "회피 불가능" 버그 진짜 원인 발견/수정**
  - 15차에서 만든 상태이상 HUD를 플레이해보던 중, 사용자가 "왜 자꾸 조향 반전이 되는 거지?
    난 피해갔는데?"라고 리포트 — 실제로 회피가 원천적으로 불가능한 구간이 있었음.
  - **근본 원인**: `CreateBroadcastAccidentZone`의 `widthRatio=0.8f`/`lateralOffsetRatio=0.25f`
    (TrackWidth=16 기준)가 존의 바깥쪽 경계를 offset 4.0+half-width 6.4=10.4로 만드는데,
    실제 바깥쪽 벽은 8.0에 있음 — 존이 벽을 2.4m나 넘어서 있었던 것. 즉 "바깥쪽으로 피하면
    되겠지"라고 자연스러운 레이싱 라인(바깥쪽)을 탔다면 애초에 여유 공간이 전혀 없어서
    100% 충돌. 안쪽 5.6m 쪽만 실제로 회피 가능했는데 코드 주석에는 "바깥쪽에 5.6m 여유가
    있다"고 (반대로) 잘못 적혀 있었음 — Node.js로 실제 벽 위치 대비 존 경계를 다시 계산해서야
    발견함(이전엔 폭/오프셋 숫자만 눈대중으로 확인하고 벽 경계까지는 검산 안 했음). 돌아보면
    13차의 원래 0.55/0.25 버전도 이미 0.4m만큼 바깥쪽을 넘어서 있었음(사용자가 "버그가 좀
    있다"고 느꼈던 것도 아마 이거였을 가능성) — 14차에서 크기만 키우면서 그 결함이 2.4m로
    증폭된 것.
  - **수정**: `widthRatio`/`lateralOffsetRatio`를 11/16, 2/16으로 재계산 — 존이 안쪽 벽에서
    4.5m 떨어진 지점부터 시작해서(1.2m 폭 차량 기준 확실히 여유 있는 실제 회피 차선) 바깥쪽
    벽 0.5m 앞에서 끝나도록(벽을 절대 넘지 않게) 폭 11m로 재구성. 폭 자체는 이전 0.55 버전
    (8.8m)보다 커서 "크기 키워달라"는 요청도 여전히 만족.
  - **검증 완료**: 컴파일 체크 → AfricaTV 씬 재빌드 → 스모크 테스트 → PlayMode 전체(22/22)
    순서로 전부 통과.
  - **교훈**: 트랙 위 해저드의 폭/오프셋을 조정할 때는 반지름/폭 숫자만 눈대중으로 확인하지
    말고, 반드시 실제 벽 위치(`SafeLateralOffset` 적용 후)와 비교해서 회피 차선이 진짜
    존재하는지(그리고 반대편이 벽을 넘어서지 않는지) 계산으로 검증할 것 — 이번에 이미 두 번
    (Nether 용암존은 처음부터 잘 계산했지만 아프리카TV 방송사고존은 두 차례 다 눈대중으로
    처리했다가 걸림).
- **17차 작업 완료(2026-07-11) — 드리프트 밸런스 1차 튜닝**
  - 사용자 피드백: 드리프트 자체(슬립/회전 배율)는 괜찮음. 다만 (1) 홀드 타이밍이 조금
    어색함 — 구체적으로는 풀차지(1.5초)까지 다 채우기 어려움, 대부분의 코너가 그만큼 길지
    않음(AskUserQuestion으로 확인). (2) 부스트 속도가 좀 약한 것 같음.
  - `VehicleController.driftBoostChargeTime`: 1.5f → 0.9f (일반적인 코너 길이에서도 풀차지에
    도달할 수 있도록 단축).
  - `VehicleController.maxDriftBoostSpeed`: 8f → 12f(+50%, `maxSpeed` 기본값 20 대비 1/5에서
    거의 1/3 수준으로 — 좀 더 확실한 미니터보 느낌).
  - `minDriftHoldTimeForBoost`(0.3초)는 그대로 유지 — 이번엔 이 값이 문제라는 피드백은 없었음.
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드(VehicleController 기본값이 바뀌었으므로 3개
    전부 다시 구워야 함) → 3개 스모크 테스트 → PlayMode 전체(22/22) 순서로 전부 통과.
  - 아직 1차 튜닝 — 실제 플레이 후 재조정 가능성 있음(다음 세션 우선 확인 항목).
- **18차 작업 완료(2026-07-11) — 트랙 길이 2차 확대(1.2배 → 1.5배), 명시적 목표 시간 기준 도입**
  - 드리프트 튜닝은 사용자 확인 완료("음, 좋네"). 다음으로 "2번(트랙 미세조정), 3번(배경
    소품)" 진행 요청 — 3번(팔레트/Kenney 소품)은 순서상 뒤로 미루고 2번부터 착수.
  - 사용자 피드백: "네더요새 맵이 너무 짧아서 아프리카TV 기준 빠르게 진행하면 1분 30초
    걸리는 맵으로 전체 확장". 17차 이전 "3바퀴 1분" 리포트에서 역산한 지속 부스트 속도
    (~22.2 m/s)를 기준으로, `TrackLengthScale`을 1.2f → 1.5f로 재조정하면 아프리카TV 3바퀴
    기준 정확히 ~90.1초가 나옴을 Node.js로 사전 계산·검증 후 반영(1.2배 버전은 3바퀴
    약 72초였음 — 목표에 못 미쳤던 것).
  - 결과 랩 길이: 비키니시티 306m→459m, 아프리카TV 444m→666m, 네더요새 190m→285m(네더요새
    "너무 짧다"는 원 불만이 직접 해소됨). 균일 스케일이라 자기교차·곡률 안전성은 그대로
    유지(Node.js로 3개 스테이지 전부 재검증, 0건).
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드 → 3개 스모크 테스트 → PlayMode 전체(22/22)
    순서로 전부 통과.
  - 3번(팔레트 텍스처/Kenney 배경 소품)은 다음 순서로 남겨둠.
- **19차 작업 완료(2026-07-11) — 트랙/그라운드 팔레트를 UI 디자인 시안 스타일로 통일**
  - 사용자가 "M2 레이싱 게임 UI 디자인/" 폴더의 HTML HUD/로비 목업 스크린샷(`hud.png`,
    `lobby.png`, `nav.png`, `icons.png`, `avatar-*.png`)을 보고 그 스타일대로 만들어달라고
    요청. 밝은 색 위주의 귀여운 카툰풍(다크 네이비 배경 + 골드/핫핑크/라임/시안/퍼플 포인트,
    두꺼운 라운드 UI)이 확인됨. `M2 Racing UI.dc.html` 소스에서 실제 사용된 hex 색상 빈도를
    직접 추출(`grep -oE '#[0-9a-fA-F]{6}'`)해서 정확한 값을 확보: 다크 네이비 `#1a1030`,
    골드 `#ffd93d`, 핫핑크 `#ff2f9e`, 라임 `#b6f36b`, 시안 `#5fd8f5`, 퍼플 `#8a3fd0`.
  - `TrackTextureFactory.CreateAsphaltTexture()`: 회색 아스팔트 → 다크 네이비(`#1a1030`)
    베이스 + 골드(`#ffd93d`) 중앙 대시라인 + 시안(`#5fd8f5`) 가장자리 커브라인.
  - `TrackTextureFactory.CreateCheckeredFlagTexture()`(17차에서 추가한 출발/도착선): 흑백
    체커 → 골드/네이비 체커로 교체.
  - `TestTrackBuilder.CreateGround`(오프트랙 그라운드): 어두운 올리브색 → 라임그린
    (`#b6f36b`).
  - **범위 밖으로 남겨둔 것**: 배경 소품(Kenney 나무/빌딩/탑 모델)은 이미 자체 텍스처
    아틀라스를 쓰고 있어서 이번엔 리컬러하지 않음 — 모델 자체 교체(Blockbench 작업)는
    여전히 뒤 순서 항목으로 유지.
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드 → 3개 스모크 테스트 → PlayMode 전체(22/22)
    순서로 전부 통과. 에디터를 열어 Scene 뷰로 그라운드 색상 변경까지는 육안 확인함(트랙
    표면 근접 확대는 Windows-MCP 합성 입력 한계로 카메라 줌이 안 먹혀서 스킵 — 사용자가
    직접 플레이하며 최종 확인 필요).
- **20차 작업 완료(2026-07-11) — 사용자 3차 피드백: 역주행 경고 신설, 드리프트 부스트 재조정,
  산소 방울 판정 확대**
  - 사용자가 "후진했는데 왜 계속 가짐?"을 처음엔 급한 버그처럼 보고했으나, AskUserQuestion으로
    되물어봐도 명확하지 않아 자유 서술을 요청 — 실제로는 물리 버그가 아니라 **누락된 기능**이었음:
    "일정 부분 후진을 하거나 아예 방향을 틀어서 뒷편으로 직진을 하면 보통 못 가게 막거나 방향을
    교정해주고 경고하는 게 보통인데, 우리는 그런 게 없어서 이상해보임" — 즉 트랙 역주행에 대한
    경고/제지가 전혀 없다는 지적. (`LapTracker`의 체크포인트 순서 검증 자체는 이미 안전했음 —
    역주행으로 체크포인트를 거꾸로 통과해도 바퀴 수가 잘못 올라가는 건 불가능했으나, 아무 피드백도
    없어서 "고장난 것"처럼 느껴졌던 것.) 벽에서 탈출하려고 후진하는 건 CLAUDE.md에 이미 정당한
    복구 수단으로 문서화돼 있어서, 이동 자체를 막지는 않고 경고만 추가함:
    - `LapTracker`에 `OnWrongWayDetected` 이벤트 추가 — 다음에 기대하는 체크포인트 "바로
      이전" 체크포인트를 (역방향으로) 통과하면 발생. 체크포인트 트리거는 방향과 무관하게
      양쪽에서 다 걸리므로, 이 하나의 조건으로 "오래 후진"과 "완전히 돌아서 정방향으로
      역주행" 둘 다 잡아냄.
    - 신규 `WrongWayWarning`(`M2.UI`, `ItemUseNotifier`와 동일한 임시 배너 패턴) — 화면 중앙에
      "⚠ 반대 방향입니다! ⚠" 2초간 표시. `TestTrackBuilder.SetupHud`에 배선.
  - **드리프트 부스트 재조정**: 13차에서 8(약하다) → 17차에서 12(너무 빠르다) → 이번에 10으로
    절충(둘 사이 중간값, 또 다른 극단으로 과보정하지 않음).
  - **산소 방울 판정 확대**: `OxygenBubbleSpawner`의 `SphereCollider`가 프리미티브 기본
    반지름(0.5, 시각 스케일도 1배라 전혀 안 커짐)에 머물러 있어서, 14차에서 이미 넓힌 일반
    아이템 판정(반지름 1.1)보다도 더 좁았음(사용자: "산소 방울 같은 것도 아이템이랑 크기가
    같아서 먹기가 어려워"). 일반 아이템과 동일하게 반지름 1.1로 명시적으로 키움(비키니시티
    전용이라 다른 스테이지에는 영향 없음).
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드 → 3개 스모크 테스트 → PlayMode 전체(22/22)
    순서로 전부 통과.
- **21차 작업 완료(2026-07-11) — 역주행 시스템 재설계(진짜 방지) + 안 뜨던 배너 원인 해결**
  - 사용자 재확인: "역주행 방지 기능 만들어. 배너도 전혀 안 뜨니까 고쳐." — 20차의 체크포인트
    역방향 통과 감지 방식은 실제로 거의 발동하지 않았음. 근본 원인: 체크포인트가 스테이지당
    6개뿐이라 트랙이 길어진 지금(18차 이후) 서로 76~111m씩 떨어져 있어서, 짧게 후진하거나
    살짝 방향을 틀어보는 정도로는 이전 체크포인트까지 거꾸로 도달할 일이 거의 없었음 — 그래서
    배너가 뜬 적이 없었던 것.
  - **재설계**: 체크포인트 간격에 의존하지 않는 방식으로 교체. `VehicleController`가
    `usedReverseDistance`를 자체적으로 적분(후진 중엔 -currentSpeed*dt만큼 누적, 전진 중엔
    같은 비율로 회복)해서, `maxReverseDistance`(15m)를 넘으면 `IsReverseBlocked=true`가 되고
    `ApplyThrottle`이 후진 입력 자체를 무시(target을 0으로 고정 + brakeDeceleration으로
    딱 멈춤)하도록 만듦 — 이제 실제로 "더 이상 후진 안 됨"이 진짜로 발동함(사용자 요청의
    "방지" 부분 충족). 벽에서 탈출하는 정도의 짧은 후진(15m 이내)은 그대로 자유로움.
  - **배너**: `WrongWayWarning`을 코루틴 방식 대신 `VehicleStatusHUD`와 같은 매 프레임 폴링
    방식으로 재작성 — `IsReverseBlocked`가 true인 동안 "⚠ 더 이상 후진할 수 없습니다! ⚠"를
    계속 표시(막혀있는 동안 계속 보여야 하므로 일회성 코루틴 방식은 부적합했음). 20차의
    체크포인트 기반 감지(`LapTracker.OnWrongWayDetected`)는 완전히 턴해서 정방향으로
    역주행하는, 후진이 아예 없는 케이스의 보조 신호로 남겨둠(2초 플래시).
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드 → 3개 스모크 테스트 → PlayMode 전체(22/22)
    순서로 전부 통과.
- **22차 작업 완료(2026-07-11) — 역주행 감지를 "후진 거리" 대신 "체크포인트 방향 기준"으로
  재설계(진짜 원인 해결)**
  - 사용자 재확인: "후진은 잘 막히는데, 내가 뒤로 돌아서 전진 키를 누르는 건 안 먹혀서 의미가
    없는데." — 21차의 후진거리 예산(`usedReverseDistance`, currentSpeed<0일 때만 누적)은
    말 그대로 "후진"만 잡고, 차를 완전히 돌려서 "전진" 키로 반대 방향으로 달리는 경우는 정의상
    놓칠 수밖에 없는 설계였음(차량 입장에서는 정상적인 전진과 구분이 안 됨).
  - **재설계**: 체크포인트 통과 여부가 아니라 **체크포인트 "방향"** 자체를 기준으로 삼음.
    `LapTracker`에 `NextCheckpointPosition`(다음에 기대하는 체크포인트의 월드 좌표, `Start()`
    에서 인덱스별로 캐싱) 프로퍼티를 추가하고, `LapTracker.OnWrongWayDetected`/체크포인트
    역방향 통과 감지 로직은 완전히 제거(새 방식이 두 경우를 다 커버하므로 중복 불필요).
    `VehicleController.FixedUpdate`가 매 프레임 "실제 이동 방향(속도 부호 반영한
    moveDirection)"과 "다음 체크포인트 방향" 사이의 내적을 계산해서, 멀어지는 쪽으로 이동한
    거리만큼 `usedWrongWayDistance`를 누적(가까워지면 회복) — 후진이든 정방향-역주행이든
    "체크포인트에서 멀어지고 있다"는 사실 자체로 잡아냄. 그리고 **어느 페달을 막을지도 방향
    기준으로 판단**: 차가 체크포인트 쪽을 보고 있으면 후진이 멀어지는 쪽이라 후진을 막고,
    차가 반대쪽을 보고 있으면 전진이 멀어지는 쪽이라 전진을 막음(후진은 오히려 되돌아오는
    데 도움되므로 그대로 허용) — `IsReverseBlocked` → `IsWrongWayBlocked`로 이름도 정리.
  - `WrongWayWarning`도 단순화: 이제 `IsWrongWayBlocked` 하나만 매 프레임 폴링하면 되므로
    `LapTracker` 구독/코루틴 로직 전부 제거.
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드 → 3개 스모크 테스트 → PlayMode 전체(22/22)
    순서로 전부 통과.

### 알려진 사소한 제약
- **역주행 방지, 급커브 오탐**: 사용자 확인(2026-07-11) — 22차의 방향 기반 역주행 감지가 아주
  급한 커브를 돌 때 가끔 오탐(false positive)함. 체크포인트까지 직선 거리 기준으로 판정하다
  보니, 실제 도로가 크게 휘어지는 구간에서는 "체크포인트 방향"과 "그 순간의 올바른 진행
  방향"이 일시적으로 어긋날 수 있어서임(다음 단계 후보 1번에 원인 가설 기록해둠). 사용자가
  "별 문제 없을 것 같다"고 판단해서 우선순위 낮음 — 나중에 체감상 거슬리면 체크포인트
  직선거리 대신 `TrackGeometry` 접선(tangent) 기준으로 바꾸는 방안 고려.

- **24차 작업 완료(2026-07-11) — Kenney 배경 소품 종류/밀도 확충**
  - `TestTrackBuilder.CreateBackgroundDecor`: 스테이지당 배경 소품 개수 10→18개로 증가.
    모델 목록도 확장 — 비키니시티(야자수 2종 추가, 큰바위/키큰바위 추가, 일반 나무 제거),
    아프리카TV(빌딩 2종 + 고층빌딩 2종 추가), 네더요새(사각탑/사각탑 지붕/모서리+탑 벽 추가).
    전부 로컬에 실제로 존재하는 Kenney 에셋 경로만 사용(임포트 없이 이미 있는 걸 활용).
  - **플랫컬러 폴백 추가**: 비키니시티가 쓰는 `kenney_nature-kit`은 애초에 텍스처(colormap)가
    없는 팩이라(`ls`로 확인), 그동안 배경 소품 렌더러가 아무 머티리얼도 못 받고 기본
    흰색/분홍 셰이더 오류색으로 나올 위험이 있었음(코드 주석에만 리스크로 남아있고 실제
    해소는 안 된 상태였음). `FallbackColorFor(modelPath)` 추가 — 텍스처 없으면 파일명에
    "rock"/"cliff" 포함 여부로 돌/일반(나무 등) 색을 구분해 `ApplyColor`로 대체.
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드(새 소품 반영해서 persisted scene 3개 전부
    재생성) → 3개 스모크 테스트 → PlayMode 전체(23/23) 순서로 전부 통과. 에디터를 직접 열어
    비키니시티 테스트 트랙을 빌드+Play까지 해서 트랙/그라운드 렌더링 자체는 육안 확인함 —
    다만 이 환경에서는 Windows-MCP로 시뮬레이션한 키보드 입력이 Unity의 새 Input System에
    안 먹혀서(이전 세션에도 확인된 제약) 차를 실제로 몰아 배경 소품 근처까지 가보는 건
    안 됨. 배경 소품 자체의 육안 확인은 사용자가 직접 플레이하면서 확인 필요.

- **25차 작업 완료(2026-07-11) — Blockbench 없이 코드로 스테이지 고유 오브젝트 합성**
  - 사용자 확인: 고유 오브젝트를 Blockbench로 직접 제작할 필요 없이, 프리미티브를 코드로
    조합하는 방식(1번 대안)으로 진행하기로 함 — AI 3D 생성 도구(Meshy/Tripo3D, 이미
    CLAUDE.md에 백업안으로 기록됨)는 외부 웹 툴을 사용자가 직접 조작해야 해서 보류.
  - `RendererColorUtil`에 `ApplyEmissiveColor(baseColor, emissionColor)` 추가 — 씬 조명과
    무관하게 항상 더해지는 발광색이라 광원 없이도 용암/불덩이가 "뜨겁게" 보임(URP Lit의
    `_EmissionColor` 프로퍼티가 없는 셰이더에서는 조용히 무시).
  - **네더요새 용암존**(`StageAssembler.CreateLavaZone`): 기존 단색 평면 슬랩에 발광 적용 +
    풀 가장자리를 감싸는 흑요석 바위 8개(각도 기반 결정론적 크기/회전 변화, `Random` 미사용이라
    재빌드해도 항상 같은 배치)를 큐브로 조합해서 추가 — 색칠된 사각형이 아니라 화산 분화구처럼
    보이도록.
  - **네더요새 오아시스존**(`StageAssembler.CreateOasisZone`): 긴 가장자리를 따라 가느다란
    갈대 6개(발판→끝점을 잇는 원기둥, 신규 공용 헬퍼 `CreateCylinderBetween` 사용) 추가.
  - **아프리카TV 경고 표지판**(`StageAssembler.CreateWarningSign`): 납작한 사각 판때기 →
    기둥(원기둥) + 45도 회전한 큐브(마름모 표지판 면) 조합으로 교체, 실제 도로 경고 표지판
    모양에 가깝게.
  - **아프리카TV 방송 카메라 소품 신규 추가**(`StageAssembler.CreateBroadcastCameraProp`):
    방송사고존 지점에 삼각대(다리 3개, `CreateCylinderBetween`으로 접지점→허브점을 잇는
    원기둥) + 카메라 몸체(큐브) + 렌즈(원기둥) + 빨간 탤리라이트(발광 구)로 구성된 완전히
    새로운 소품. 그동안 경고 표지판만 있고 "방송 세트"를 실제로 보여주는 오브젝트가 없어서
    "그냥 반전되는 느낌"이라는 기존 피드백(16차)의 남은 부분을 마저 해소.
  - **네더요새 가스트 파이어볼**(`StageAssembler.CreateGhastFireball`): 단일 구체 → 발광
    코어 + 8방향으로 뻗은 작은 발광 스파이크 구체 조합("불덩이" 형태) + `GhastFireball.cs`에
    순수 장식용 자전(`visualSpinSpeed`, 콜라이더가 구형이라 판정 영향 없음) 추가.
  - 비키니시티 지형지물은 이미 10차 작업에서 Kenney `rock-sand-b` 실제 모델을 붙여둔 상태라
    이번 작업 대상에서 제외(플레이스홀더 큐브가 아니라 이미 실제 바위 모델).
  - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드 → 3개 스모크 테스트 → PlayMode 전체(23/23,
    나머지 1개는 헤드리스 환경상 정상적인 Inconclusive) 순서로 전부 통과. 자동화 환경에서는
    차를 몰아 육안 확인이 안 되므로(키보드 입력이 Input System에 안 먹힘, 기존 제약) 실제
    플레이 화면에서 크기/배치가 어색하지 않은지는 사용자 확인 필요.
  - **후속 수정(같은 세션, 사용자가 직접 플레이 후 발견) — 용암존에 그냥 들어가만 있으면 체온이
    안 오르던 버그**: 사용자 리포트 "용암존 들어갔을 때 온도 상승을 안함". 원인은 버그가 아니라
    애초에 그렇게만 설계돼 있었던 것 — `NetherFortressStageState.HandleHitByAttackItem`이
    "공격 아이템에 맞았을 때, 용암 안이면 더 크게" 콤보 보너스만 처리했고, `LavaZone` 자체는
    `IsPlayerInside` 플래그만 노출할 뿐 게이지를 직접 건드리는 코드가 어디에도 없었음 — 혼자
    플레이테스트하면(다른 플레이어의 공격을 맞을 일이 없음) 용암에 서 있어도 정말 아무 일도
    안 일어남. `NetherFortressStageState`에 `lavaZonePassiveHeatPerSecond`(플레이스홀더 8/초)
    필드 + `Update()`를 추가해서, 피격 여부와 무관하게 용암 안에 있는 동안 매 프레임
    `temperatureGauge.ModifyValue`로 추가 가열되도록 함(기존 피격 콤보 보너스는 그대로 유지,
    별개로 더해짐). `StageGaugeSystem`의 패시브 틱과 동일하게 `GameManager.CurrentState ==
    Racing`일 때만 적용되도록 게이트도 맞춰 넣음(13차에서 겪었던 "Briefing 중에도 게이지가 참"
    버그가 이 새 경로에서 재현되지 않도록).
    `NetherFortressStageTests.Hit_Near_Lava_Applies_Bigger_Temperature_Bonus_Than_Normal_Hit`가
    새 패시브 가열과 겹쳐서 실패하길래(피격 콤보 보너스만 재던 기존 테스트가 그 사이 몇 프레임
    동안의 패시브 가열분까지 합산해서 기대값보다 살짝 커짐) 테스트 셋업에서
    `lavaZonePassiveHeatPerSecond = 0f`로 명시적으로 꺼서 기존 테스트는 순수 피격 이벤트만
    재도록 격리하고, 신규 테스트
    `Standing_In_Lava_Zone_Raises_Temperature_Passively_Without_Being_Hit`를 별도로 추가해
    패시브 가열 자체를 검증. 검증 완료: 컴파일 체크 → NetherFortress 씬 재빌드/스모크 테스트 →
    PlayMode 전체(24개 중 23 통과, 나머지 1개는 기존과 동일한 Inconclusive) 순서로 전부 통과.
  - **2차 후속 수정(같은 세션) — 용암존 패시브 가열 8→10/초 + 화상 게임오버가 실제 레이스를
    안 끝내던 문제 수정**: 사용자 피드백 "초당 10도 정도로 높이고, 100도 됐을 때 게임오버 하면
    나오는 게임오버 화면이 아직 안 만들어졌어. 패배한 걸로 치고 일단 무승부 처리를 내자."
    - `lavaZonePassiveHeatPerSecond` 기본값 8f → 10f.
    - **근본 원인**: 화상 게임오버가 발생하면 `NetherFortressStageUI`의 작은 오버레이("게임
      오버!\n(화상)")만 뜨고 차량 입력만 잠겼을 뿐, `GameManager`는 레이스가 끝난 걸 전혀
      모르는 상태였음 — `RaceState`가 계속 `Racing`에 머물러 레이스 타이머가 뒤에서 계속
      돌고, `RaceFlowUI`의 진짜 결과화면(순위/통계/별점)은 절대 안 뜸. 사용자가 "게임오버
      화면이 아직 안 만들어졌어"라고 느낀 게 바로 이 결과화면 부재였음(비키니시티 산소
      게임오버도 구조가 똑같아서 동일한 문제가 있지만, 이번엔 요청받은 네더요새만 수정 —
      나중에 비키니시티도 정리 필요).
    - `GameManager.EndRace`가 무승부 사유 문자열을 받도록 확장(`drawReason`, 기본값
      "제한시간 초과"로 기존 타임아웃 동작 그대로 유지), `OnRaceDraw` 이벤트 시그니처를
      `Action` → `Action<string>`으로 변경, 스테이지 해저드가 레이스를 조기 종료시킬 수 있는
      공개 메서드 `GameManager.EndRaceAsDraw(string reason)` 추가(실제 승패 판정은 아직
      없음 — 우선순위 5 — 이라 지금은 전부 무승부로 처리). `RaceFlowUI.HandleRaceDraw`도
      결과화면에 사유를 그대로 표시하도록 수정("무승부\n제한시간 초과" / "무승부\n화상").
    - `NetherFortressStageState`가 `temperatureGauge.OnBurnGameOver`를 구독해서
      `gameManager.EndRaceAsDraw("화상")`을 호출 — 화상 게임오버가 이제 실제로 레이스를
      끝내고 정식 결과화면을 띄움. 기존 `NetherFortressStageUI`의 작은 오버레이는 그대로
      남겨둠(같이 떠도 기능적으로 문제없음, 필요하면 다음에 정리).
    - `GameManagerRaceFlowTests`의 기존 무승부 테스트가 시그니처 변경으로 깨져서
      `Action<string>` 람다로 수정 + 사유 검증 추가, `EndRaceAsDraw`가 커스텀 사유로 레이스를
      끝내는지 확인하는 신규 테스트
      `EndRaceAsDraw_Lets_A_Stage_Hazard_End_The_Race_Early_With_A_Custom_Reason` 추가.
    - **검증 완료**: 컴파일 체크 → 3개 씬 재빌드(`GameManager`/`RaceFlowUI`가 공용 코드라 전부
      재생성) → 3개 스모크 테스트 → PlayMode 전체(25개 중 24 통과, 나머지 1개는 기존과 동일한
      Inconclusive) 순서로 전부 통과.
  - **3차 후속 수정(같은 세션) — 비키니시티 산소 게임오버도 동일하게 수정**: 사용자 요청
    "비키니시티도 고쳐." — 위에서 남겨뒀던 것과 정확히 같은 구조적 문제(오버레이만 뜨고
    `GameManager`는 레이스 종료를 모름)였음. `BikiniCityStageState`에 `oxygenGauge` 참조 +
    `GameManager` 참조 추가, `BikiniCityOxygenGauge.OnOxygenGameOver`를 구독해서
    `gameManager.EndRaceAsDraw("산소 부족")` 호출. `StageAssembler.AttachBikiniCity`에서
    `stageState.oxygenGauge = gauge;` 배선 추가(기존엔 이 참조 자체가 없었음). 이제 3개
    스테이지의 게임오버 조건(산소 고갈/화상/[아프리카TV 멘탈 고갈은 즉시 게임오버가 아니라
    일시 조작불가라 대상 아님]) 중 실제로 레이스를 끝내야 하는 두 케이스가 전부 정식
    결과화면으로 이어짐.
    - **검증 완료**: 컴파일 체크 → BikiniCity 씬 재빌드/스모크 테스트 → PlayMode 전체(25개
      중 24 통과, 나머지 1개는 기존과 동일한 Inconclusive) 순서로 전부 통과.

- **27차 작업 착수(2026-07-12) — Netcode for GameObjects Milestone 1: 기본 온라인 연결 +
  차량 위치 동기화 (Relay/Lobby 없이 직접 IP 연결까지만)**
  - 사용자가 우선순위 4(Netcode) 착수를 명시적으로 요청. 프로젝트 전체 규모 작업이라 CLAUDE.md
    본문의 "로컬 핵심 루프 먼저, 네트워킹은 후반부에" 철학을 네트워킹 내부에도 그대로 적용해서
    가장 작은 검증 가능한 조각(Milestone 1)부터 착수 — 아이템/레이스 상태 동기화, Relay, Lobby는
    의도적으로 다음 라운드로 미룸.
  - **패키지**: `com.unity.netcode.gameobjects` 2.13.0 + `com.unity.transport` 2.6.0 추가
    (WebSearch/GitHub package.json으로 Unity 6000.3 호환 버전 확인 후 반영).
  - **`M2.Network` 네임스페이스 신설**(`Assets/Scripts/Network/`), 기존 `M2.Runtime` asmdef
    안에 그대로 포함(이 프로젝트엔 네임스페이스별 asmdef 분리가 없어서 순환참조 위험 없음).
    `M2.Runtime.asmdef`/`M2.Editor.asmdef`/`M2.Tests.PlayMode.asmdef`에 `Unity.Netcode.Runtime`
    + `Unity.Networking.Transport` 참조 추가(`UnityTransport.SetConnectionData`가 내부적으로
    `NetworkEndpoint` 타입을 노출해서 후자도 명시적으로 있어야 컴파일됨).
  - **오너-권위(owner-authoritative) 이동 방식 채택**: 서버-권위 + 클라이언트 예측/재조정 대신,
    소유한 클라이언트가 지금과 똑같이 로컬 물리 시뮬레이션을 하고 그 결과만 복제하는 훨씬 단순한
    방식 — 학교 축제용 캐주얼 레이싱에 충분하고 코드량이 훨씬 적음(부정행위 방지 불필요 전제).
    `OwnerAuthoritativeNetworkTransform`(`NetworkTransform` 상속, `OnIsServerAuthoritative() =>
    false`만 오버라이드 — NGO 자체 Bootstrap 샘플의 `ClientNetworkTransform`과 동일한 패턴을
    직접 재현, 샘플은 `Samples~` 폴더라 기본 임포트 안 됨) + `NetworkVehicleSync`(`NetworkBehaviour`,
    `OnNetworkSpawn`에서 `rb.isKinematic = !IsOwner`로 원격 차량은 물리 시뮬레이션 자체를
    끔 — 안 그러면 자기 Rigidbody가 들어오는 네트워크 트랜스폼과 매 프레임 충돌함).
  - **`VehicleController`에 소유권 게이트 추가**: `IsOwnedLocally`(NetworkObject 없으면
    항상 true — 기존 로컬 씬은 전부 무변경으로 그대로 동작) 프로퍼티 추가,
    `FixedUpdate` 맨 위에서 `if (!IsOwnedLocally) return;`으로 원격 차량은 입력을 아예 안 읽음.
    `VehicleControllerTests`에 회귀 테스트(`IsOwnedLocally_Defaults_True_Without_A_NetworkObject`)
    추가 — "소유자가 아닌 경우"는 실제 2클라이언트 접속이 있어야 검증 가능해서 자동화 테스트
    범위 밖(아래 한계 참고).
  - **네트워크 차량을 실제 `.prefab` 에셋으로 제작**(`Assets/Prefabs/Player/NetworkVehicle.prefab`)
    — 이 프로젝트의 기존 "전부 코드로 절차 생성, 저장 에셋 없음" 방침에서 의도적으로 벗어난
    유일한 예외. 이유: NGO의 `NetworkManager.AddNetworkPrefab(GameObject)`가 "동적으로 생성된
    오브젝트도 된다"고 문서화돼 있어서 처음엔 무저장 방식을 시도했는데, 실제로 확인해보니
    `NetworkObject.GlobalObjectIdHash`(서버/클라이언트가 스폰 메시지를 어느 등록 프리팹과
    매칭시킬지 결정하는 값)가 `NetworkObject.OnValidate`(에디터 전용 콜백)에서만 계산되고,
    이 콜백은 **배치모드에서는 자동으로 안 불림** — 실제로 `PrefabUtility.SaveAsPrefabAsset`
    직후는 물론 강제 `AssetDatabase.ImportAsset` 리임포트를 걸어도 저장된 프리팹의
    `GlobalObjectIdHash`가 계속 0으로 나오는 걸 직접 확인함(일반 NGO 튜토리얼은 전부 에디터 GUI에서
    상호작용하며 저장하기 때문에 이 문제를 아무도 겪지 않는 것). 이 환경은 GUI 에디터가 백신
    차단으로 반복 실패해서(Windows-MCP로 열면 매번 "Unity Package Manager Error" 재발) 인터랙티브
    경로 자체가 막혀있어, `NetworkObject.OnValidate`를 **리플렉션으로 직접 호출**하는 방식으로
    우회(`NetworkPrefabBuilder.cs` 코드 주석에 전체 경위 기록) — 저장된 에셋을 리로드한 뒤
    호출하니 실제로 0이 아닌 값(예: 979420377)이 채워지는 걸 확인. `BuildCheck.
    SmokeTestNetworkVehiclePrefab`도 같은 리플렉션으로 해시가 0이 아닌지 자동 검증하도록 함.
  - **카메라**: `NetworkVehicleSync.OnNetworkSpawn`에서 `IsOwner`일 때만
    `Camera.main.GetComponent<VehicleCameraFollow>().target`을 자기 자신으로 재배선 — 로컬
    테스트 흐름은 차량이 1대뿐이라 `TestTrackBuilder`가 빌드 시점에 한 번만 연결하면 됐지만,
    온라인에서는 동적으로 스폰되는 2대 중 "내 것"만 로컬 카메라가 따라가야 함.
  - **최소 Host/Join 부트스트랩 씬**(`Assets/Scenes/NetworkBootstrap.unity`,
    `NetworkBootstrapSceneBuilder.cs`로 코드 생성): Relay/Lobby 없이 `UnityTransport` 직접 접속
    (기본 `127.0.0.1:7777`). `NetworkBootstrapUI`가 호스트/접속 버튼 + IP 입력창 + 상태 텍스트를
    담당하고, `ConnectionApproval`을 켜서 두 플레이어가 스폰 시 겹치지 않도록 좌우로
    벌려 배치(`spawnSideOffset`, 플레이스홀더 3m) — 레이스 흐름/트랙은 이번 범위 밖이라 그냥
    평평한 바닥 하나뿐.
  - **`check_build.ps1` 버그 수정(이번 작업 중 발견)**: 컴파일 체크 성공/실패 판정이 로그에서
    "M2_COMPILE_CHECK_OK" 등 3개 마커 문자열만 정확히 찾는 방식이었는데, NGO 패키지 임포트 로그가
    `Packages/com.unity.netcode.gameobjects/Runtime/Exceptions` 폴더 경로를 출력하면서
    "Exception"이라는 단어가 (플레이스홀더로 취급되던) 이 폴더명의 일부로 우연히 매치돼서, 실제로는
    성공한 컴파일을 실패로 오판하는 버그가 있었음 — `Exception(?!s)`(뒤에 "s"가 안 오는 경우만)로
    수정, 그리고 새 BuildCheck 진입점이 늘어날 때마다 매번 이 스크립트를 고칠 필요 없도록 성공
    판정 자체도 하드코딩된 3개 마커 나열 대신 범용 `M2_.*_OK` 패턴으로 일반화.
  - **검증 완료**: 컴파일 체크 → 네트워크 프리팹/부트스트랩 씬 빌드+스모크 테스트 → 기존 3개
    스테이지 씬 재빌드/스모크 테스트(공용 asmdef 변경 영향 확인) → PlayMode 전체(26개 중 25
    통과, 나머지 1개는 기존과 동일한 Inconclusive) 순서로 전부 통과.
  - **후속 수정(같은 세션, 사용자가 직접 에디터에서 Play해보고 발견) — `NetworkManager`가 씬
    루트가 아니라 "NetworkBootstrap" 밑에 중첩돼서 Play 진입 시 NGO 자체 검증 에러("NetworkManager
    is nested under NetworkBootstrap.")가 뜸**: `NetworkManager`는 자기 자신에게
    `DontDestroyOnLoad`를 거는데, 이건 씬 루트(부모 없음) 오브젝트에서만 동작 — 처음 버전은
    다른 오브젝트들처럼 편의상 "NetworkBootstrap" 루트 밑에 같이 묶어뒀던 게 문제였음.
    `NetworkBootstrapSceneBuilder.CreateNetworkManagerAndUi`에서 `NetworkManager` GameObject를
    부모 없이 생성하도록 수정, `BuildCheck.SmokeTestNetworkBootstrapScene`에도 회귀 검사(부모가
    있으면 실패) 추가. 자동화 배치모드가 아니라 사용자가 직접 에디터에서 Play해서 잡아낸 첫
    번째 버그 — 이 마일스톤의 "실제 동작 확인은 사용자가 직접" 한계를 그대로 보여준 사례.
    씬 파일을 직접 열어 `NetworkManager`의 `m_Father: {fileID: 0}`로 확인 완료.
  - **자동화의 한계, 실제로 부딪힘**: 이 환경은 실제 네트워크 피어 2개를 붙여서 자동 검증하는 게
    근본적으로 불가능함(Windows-MCP 입력이 게임플레이엔 안 먹히고, GUI 에디터 실행도 반복적으로
    막힘) — 그래서 이번엔 처음으로 **사용자가 ParrelSync를 직접 설치하고, 실제 호스트+클라이언트
    두 인스턴스로 라이브 테스트**를 진행함. 아래 4개의 진짜 버그가 전부 이 라이브 테스트로만
    드러났고(자동 검증은 전부 통과한 상태였음), 사용자가 매번 실제 콘솔 로그/에러 메시지를
    직접 붙여넣어줘서 원인을 하나씩 추적함.
  - **버그 1 — 스폰된 차량이 보라색(머티리얼 없음)**: `NetworkPrefabBuilder`가 `.material`
    (나중엔 `.sharedMaterial`로도 시도)로 만든 머티리얼은 메모리상의 임시(loose) 오브젝트일
    뿐이라, `PrefabUtility.SaveAsPrefabAsset`이 프리팹으로 저장할 때 그 참조를 조용히
    `{fileID: 0}`(빈 슬롯)으로 버림 — 씬으로 저장할 때는 이 문제가 없어서(트랙/그라운드 등
    기존 코드는 전부 씬에만 저장) 이번에 프리팹을 처음 저장하면서 처음 드러남. 저장된 프리팹
    YAML을 직접 열어 `m_Materials: [{fileID: 0}]`로 실제 확인. **최종 수정**: 각 머티리얼을
    `Assets/Prefabs/Player/Materials/*.mat`로 실제 에셋 저장한 뒤 프리팹에 연결하도록 변경 —
    저장 직후 프리팹을 다시 열어서 실제로 채워졌는지까지 검증하는 코드 추가(`NetworkPrefabBuilder`).
    `RendererColorUtil`의 `.material` → `.sharedMaterial` 전환(에디터 타임 "Instantiating
    material..." 경고 제거)도 같이 반영 — 프로젝트 전역에 적용되는 개선이지만 이것만으론
    프리팹 문제를 해결 못 했고, 위의 `.mat` 에셋 저장이 진짜 수정.
  - **버그 2 — 두 차량이 같은 위치에 스폰되면서 서로 밀려 바닥에 파묻힘**: 사용자가 직접 원인을
    특정("차가 같은 곳에 설치되니까 튕겨지는데 튕겨질 곳이 없으니까 땅바닥으로 꺼지는 거였음").
    `ConnectionApprovalResponse.Position`을 서버 쪽에서 지정하는 방식이었는데, 이 값이 원격
    관전자(반대편 클라이언트가 보는 복제본)에게 안정적으로 복제되지 않음 — 오너-권위
    (owner-authoritative) 이동 방식이라 애초에 서버가 아니라 **소유한 클라이언트 자신**만
    트랜스폼에 대한 권위가 있음. `NetworkVehicleSync.OnNetworkSpawn`에서 `IsOwner`일 때 자신의
    `OwnerClientId`가 서버 ID인지로 좌/우를 정하고 직접 `transform.position`을 설정하도록
    이동(`NetworkBootstrapUI`는 승인/생성만 담당, 위치 지정 로직 제거).
  - **버그 3 — 씬 빌드 중 `NullReferenceException`**: NGO 소스의 `NetworkManager.NetworkConfig`
    필드가 초기화자 없이 선언돼 있어서(`public NetworkConfig NetworkConfig;`), 인스펙터로 직접
    추가할 때만 유니티가 자동으로 기본 인스턴스를 채워주고, **에디터 스크립트로
    `AddComponent<NetworkManager>()`할 때는 `null`로 남음** — `NetworkBootstrapSceneBuilder`에서
    명시적으로 `networkManager.NetworkConfig = new NetworkConfig();`를 채우도록 수정. 이 예외
    때문에 씬 저장이 중간에 멈춰서 이전 저장본이 남아있는 상태로 Play를 누르는 바람에 사용자가
    "뭔가 크게 고장남"이라고 느꼈던 것 — 실제로는 씬 파일 자체가 손상된 건 아니었음. 유니티의
    라이브 `Editor.log`(`%LOCALAPPDATA%\Unity\Editor\Editor.log`)를 직접 읽어서 정확한 예외
    위치를 확인 — 사용자가 콘솔을 일일이 복사해줄 필요 없이 이 로그 파일로 직접 진단 가능하다는
    걸 이번에 알게 됨(다음에도 유용할 방법).
  - **최종 검증 완료(2026-07-12, 사용자 확인)**: "오! 드디어 멀쩡히 되네." — 호스트+클라이언트
    (ParrelSync 클론) 양쪽에서 차량이 좌우로 벌어져 스폰되고, 텍스처 정상, 각자 자기 차만
    조작되고, 서로의 움직임이 반대쪽 화면에도 반영되는 것까지 실제로 확인됨. **Milestone 1은
    이제 코드+자동 검증+실제 라이브 2인 접속 테스트까지 전부 통과한 상태.**

### 미착수
- **우선순위 4 (Netcode) — Milestone 1 완료(실제 2인 접속까지 확인) + Milestone 2a(레이스 상태
  서버-권위 동기화) + Milestone 2b(아이템 스폰/획득/사용 서버-권위 동기화) 코드+자동검증 완료
  (2a·2b 통합 라이브 2인 테스트는 사용자 확인 대기 — 30차 항목 참고)**.
  다음 범위: Milestone 3 — Unity Relay(방 코드 발급/입장), Unity Lobby(대기방/방 목록). 아직
  미착수. 로컬 핫싯(2P 스플릿)은 CLAUDE.md 기획 자체가 "온라인 1v1"만 명시하고 있어서 범위 아님.
- **우선순위 5**: 아이템 최종형 로스터, 정식 스테이지 3종 아트/씬, 나머지 승리조건(별점 내기) 확장,
  아프리카TV/네더요새 게이지 밸런스 수치 확정(이번에 네더요새 체온만 확정, 나머지는 여전히
  플레이스홀더).
- **아트 에셋 자체**: 트랙 표면/그라운드 팔레트는 19차, 배경 소품 종류/밀도는 24차, 스테이지별
  고유 오브젝트(용암존/오아시스/방송 세트/가스트 파이어볼/경고 표지판)는 25차에서 프리미티브
  합성으로 확충. 실제 모델링 도구(Blockbench)로 만든 에셋은 여전히 없음(현재로선 불필요하다고
  판단, 25차 결정 참고).

- **28차 작업 완료(2026-07-12) — Netcode Milestone 1 실제 2인 접속 테스트, 발견된 버그 4개 수정**:
  사용자가 ParrelSync 설치 후 호스트+클론 두 인스턴스로 직접 라이브 테스트 진행. 위 27차 항목의
  "버그 1~3"(머티리얼 프리팹 미저장, 스폰 위치 동기화 안 됨, `NetworkConfig` null 참조 예외)을
  전부 이 라이브 테스트 도중 실시간으로 발견·수정. 최종 확인: "오! 드디어 멀쩡히 되네." — 상세
  내용은 위 27차 항목의 "최종 검증 완료" 참고.

- **29차 작업 완료(2026-07-12) — Netcode Milestone 2a: 레이스 상태 서버-권위 동기화(코드+자동검증)**
  - 사용자가 우선순위 4의 다음 마일스톤으로 Milestone 2(게임 상태 동기화)를 선택. 규모가 커서
    2a(레이스 상태 — 바퀴/타이머/승패)와 2b(아이템 스폰/획득/사용)로 분할, 이번엔 2a를 완료.
  - **핵심 설계 — Milestone 1의 오너-권위(owner-authoritative) 이동 결정을 레이스 전체로 그대로
    확장**: 서버-권위 + 클라이언트 예측/재조정 같은 복잡한 방식 대신, **호스트가 레이스 전체를
    권위적으로 실행하고 결과 상태만 클라이언트에 복제**함. 호스트는 두 차량의 동기화된 복사본을
    모두 갖고 있고(자기 차는 로컬 물리, 상대 차는 `NetworkTransform`이 kinematic으로 구동), 이
    복사본들이 씬의 `Checkpoint` 트리거를 그대로 통과하므로 **호스트의 `LapTracker`가 양쪽 바퀴를
    권위적으로 카운트**함(per-client 바퀴 RPC 불필요 — kinematic Rigidbody도 트리거 이벤트를
    발생시킨다는 성질을 이용). 클라이언트의 로컬 물리가 바퀴 수에 대해 서버와 어긋날 여지가 없음.
  - **`GameManager` 네트워크 훅 추가(하위호환)**: `autoStartOnStart`(기본 true — 로컬 씬은 기존처럼
    `Start()`에서 즉시 시작), `BeginRaceFlow()`(멱등), `RegisterRacer()`. 온라인 씬은
    `autoStartOnStart=false`로 두고, 호스트의 `NetworkRaceManager`가 두 플레이어가 모두 스폰된 뒤
    레이서를 등록하고 흐름을 시작함. 클라이언트의 `GameManager`는 그냥 유휴 상태로 둠.
  - **`NetworkRaceManager`(신규, `M2.Network`, 호스트가 스폰하는 프리팹)**: 서버가 라이브
    `GameManager`의 상태(레이스 상태/남은 시간/카운트다운/플레이어별 바퀴/최종 결과)를
    `NetworkVariable`로 미러링. 모든 인스턴스가 복제된 상태로 자기 차량의 입력 잠금을 구동
    (브리핑/카운트다운/종료 시 잠금, 레이싱 시 해제). 승자 판정은 `LapTracker`→`clientId` 매핑으로.
  - **`NetworkRaceHUD`(신규)**: `NetworkRaceManager`를 읽어 상태/시간/양쪽 바퀴/결과 표시. 로컬
    씬의 `RaceHUD`/`RaceFlowUI`는 라이브 `GameManager`에 묶여있어 클라이언트(유휴 GM)에선 못 쓰므로
    별도 최소 HUD로 구현.
  - **`NetworkRaceBootstrap`(신규)**: 레이스 매니저 프리팹을 **런타임에 등록**(`AddNetworkPrefab`)
    하고 서버 시작 시 스폰. 편집 시점 `NetworkConfig.Prefabs.Add()`는 안 됨 — `NetworkPrefabs.
    m_Prefabs`가 `[NonSerialized]`라 씬 저장 시 사라지는 걸 스모크 테스트로 직접 확인함(직렬화되는 건
    `NetworkPrefabsLists`뿐). 플레이어 차량이 이걸 안 겪는 건 `NetworkConfig.PlayerPrefab`이 별도
    직렬화 필드라 NGO가 자동 등록하기 때문.
  - **`RaceStartGrid`(신규) + `NetworkVehicleSync` 확장**: 출발선(theta=0) 뒤에 좌우로 벌린 그리드
    슬롯 2개를 씬에 구움. 오너가 스폰 시 자기 슬롯에 위치를 잡음(오너-권위라 오너만 트랜스폼 쓰기가
    안정적으로 복제됨 — Milestone 1의 스폰 위치 버그 교훈). 부트스트랩 씬엔 그리드가 없으므로 기존
    원점 좌우 분리로 폴백(Milestone 1 무변경).
  - **에셋 빌더**: `NetworkVehicle.prefab`에 `LapTracker` 추가(체크포인트로 호스트가 바퀴 카운트 +
    오너 클라이언트의 역주행 감지가 덤으로 딸려옴). 신규 `NetworkRaceManager.prefab`(빈 로직
    오브젝트, `NetworkPrefabBuilder`에 빌더 추가 — GlobalObjectIdHash 리플렉션 로직을 공용 헬퍼로
    추출), 신규 `NetworkRace.unity` 씬(`NetworkRaceSceneBuilder`, 비키니시티 트랙 — `TestTrackBuilder.
    BuildTrackEnvironment`로 로컬 씬과 동일한 손으로 만든 트랙을 재사용). `NetworkBootstrapSceneBuilder`
    의 NetworkManager+UI 생성을 `CreateNetworkManager`/`AttachHostJoinUi` 공용 internal 헬퍼로 추출해
    두 온라인 씬이 동일하게 매니저를 만들게 함.
  - **검증 완료**: 컴파일 체크 → 네트워크 차량/레이스매니저 프리팹 빌드+스모크(GlobalObjectIdHash
    비-0 확인) → 네트워크 레이스 씬 빌드+스모크 → Milestone 1 부트스트랩 스모크(리팩터 회귀 없음
    확인) → 3개 스테이지 씬 재빌드+스모크(공용 `GameManager` 변경 영향) → PlayMode 전체(27개 중 26
    통과, 1 inconclusive는 헤드리스 스크린샷 정상). 신규 PlayMode 테스트
    `AutoStartDisabled_Waits_For_BeginRaceFlow` 추가(autoStart off일 때 자동 시작 안 하고
    `BeginRaceFlow`로만 시작 + 멱등성 검증). 커밋됨(`0218e06`).
  - **후속 수정(같은 세션, 사용자 라이브 테스트 피드백으로 발견)** — 자동 검증은 전부 통과했지만
    실제 ParrelSync 2인 접속에서만 드러난 3건:
    1. **접속 UI 클릭 안 됨**: `NetworkRaceHUD`의 큰 배너 텍스트가 화면 거의 전체를 덮는데
       `Text.raycastTarget` 기본값이 true라, 캔버스의 `GraphicRaycaster`가 그 아래 호스트/접속
       버튼으로 갈 클릭을 전부 삼킴(사용자: "버튼이 안 눌리는데?"). HUD 라벨 2개(배너·정보)의
       `raycastTarget=false`로 해결. 커밋됨(`a3e109d`).
    2. **레이스 매니저가 스폰 안 됨(HUD가 "상대를 기다리는 중..."에 고정)**: `NetworkRaceBootstrap`이
       `OnEnable`에서 `NetworkManager.Singleton`을 읽어 null이면 조기 리턴했는데, NGO는 Singleton을
       `NetworkManager.OnEnable`에서 설정하므로 같은 오브젝트 컴포넌트의 `OnEnable` 순서에 의존하는
       게 불안정했음 → 등록/구독을 아예 못 해 스폰 실패. 등록/구독/스폰을 `Start()`(모든 `OnEnable`
       이후 보장) + `Update()` 폴백(서버에서 `IsListening`되면 1회 스폰)으로 옮기고 진단 로그(`M2Net:`)
       추가. 사용자 재테스트로 스폰 확인됨("뜨긴 뜨는데"). **같은 `OnEnable`/Singleton 교훈을
       `NetworkBootstrapUI`에도 적용**(아래 3번).
    3. **접속 후에도 호스트/접속 버튼·IP창·"호스팅 중..." 안 사라짐**: `NetworkBootstrapUI`에 접속
       성공 시(`OnServerStarted`=호스트, `OnClientConnectedCallback`=클라이언트) 이 UI를 숨기는 로직
       추가(실패 시엔 재시도용으로 남겨둠). 구독은 `Start()`에서(2번과 동일한 Singleton 타이밍 이유).
       공용 컴포넌트라 Milestone 1 부트스트랩 씬에도 같이 적용됨(거기서도 접속 후 버튼은 불필요).
    - 2·3번은 컴파일 체크까지 통과. **3번(UI 숨김)은 사용자가 자러 가서 라이브 미확인** — 다음 세션
      첫 확인 항목.
  - **아직 사용자 라이브 확인 필요(이 환경 한계)**: 실제 2인(ParrelSync 호스트+클론) 접속으로만
    검증 가능한 것들 — 스폰 위치는 확인됨(OK), 접속 UI 클릭도 해결됨. 남은 것: (a) 접속 후 UI 숨김
    (위 후속수정 3번, 미확인), (b) 2명 다 접속 시 카운트다운이 도는지, (c) 호스트에서 kinematic 상대
    차의 체크포인트 트리거로 바퀴가 카운트되는지, (d) 타이머/승패 복제. **주의: 레이스는 2명이 다
    접속해야 시작됨**(`requiredPlayers=2`) — 호스트 혼자선 브리핑에서 대기.

- **30차 작업 완료(2026-07-13) — Netcode Milestone 2b: 아이템 스폰/획득/사용 서버-권위 동기화(코드+자동검증)**
  - 사용자가 2a 라이브 확인과 2b를 먼저 다 구현한 뒤 ParrelSync 라이브 테스트 한 번으로 몰아서
    점검하기로 결정. 이번 세션은 2b 구현 본체를 코드+자동검증까지 완료, 라이브 점검은 다음 세션.
  - **핵심 설계 — 2a의 호스트-권위 결정을 아이템 전체로 그대로 확장**: 호스트가 두 차량의 동기화
    복사본을 모두 갖고 있으므로(바퀴 카운트와 동일 성질), 아이템 롤·픽업 판정·폭탄 폭발 판정을
    전부 호스트가 수행. 지속 상태는 NetworkVariable/NetworkList로 복제, 일회성 효과(부스트/기절/
    방패)만 RPC로 — 오너-권위 물리라서 효과는 반드시 해당 차량이 실제 시뮬레이션되는 오너
    클라이언트에서 실행돼야 함. **레포 내 첫 RPC 도입 지점**(2a까지는 NetworkVariable만 썼음).
  - **아이템 ID 와이어 스킴**: `ItemDefinition`은 클래스라 복제 불가 → `NetItemId`(byte enum,
    None/Accel·Attack·Defense × base/derived 6종) 1바이트만 복제하고 각 피어가
    `ItemCatalog.CreateFromId`로 로컬 재구성(이름/스탯은 절대 전송 안 함). `ItemCatalog`에
    `IdFor`/`TypeOf`/`CreateRandomIdForSpawn`/`CreateFromId` 추가, 기존 `CreateRandomForSpawn`
    (로컬 씬용)은 무변경.
  - **`NetworkItemSlots`**(신규, 차량 프리팹): 서버-권위 2슬롯 인벤토리를 `NetworkVariable<byte>`
    2개로 복제. 채움/교체 규칙(주→보조→주 교체)과 사용 슬롯 선택 규칙을 로컬 `ItemSlots`와
    동일하게, 단 `public static` 순수 함수로 추출해 NetworkManager 없이 PlayMode 테스트. 오너가
    입력(Ctrl/E) 구독 → `[Rpc(SendTo.Server)]`로 사용 요청 → 서버가 슬롯 검증/클리어 후 효과 적용
    (가속=Owner RPC로 `ApplySpeedBoost`, 방패=호스트 복사본 `ActivateShield`+Owner RPC HUD 미러,
    폭탄=Everyone RPC로 코스메틱 마커 + 호스트만 `OverlapSphere` 권위 판정 → 피해자별 방패 소모
    또는 Owner RPC 기절). 네트워크 프리팹은 로컬 `ItemSlots` 대신 이걸 탑재.
  - **`NetworkItemSpawnManager`**(신규, NetworkRaceManager 프리팹에 두 번째 컴포넌트로 탑재 —
    부트스트랩/등록 변경 0): 스폰 상태를 `NetworkList<byte>`(6지점 × NetItemId, 0=수집됨)로 복제.
    NetworkObject를 픽업마다 스폰/디스폰하지 않고 상태만 복제하는 방식(5초마다 6지점 재스폰을
    프리팹 6개 등록·GlobalObjectIdHash 배치모드 우회·스폰 순서 문제 없이 처리). 서버가
    지점별 롤 + 거리 폴링으로 픽업 판정(트리거 대신 거리 — 클라 마커는 콜라이더 없고 kinematic
    엣지케이스 없음) + 5초 후 재롤. 모든 피어가 복제 상태를 코스메틱 픽업 비주얼(콜라이더 없음)로
    렌더. `NetworkList`는 레포 내 첫 사용 — 말썽 시 폴백(`NetworkVariable<byte>` 6개/비트팩)을
    코드 주석에 명시. 신규 `NetworkItemSpawnPoint`(비네트워크 씬 마커, `NetworkRaceSceneBuilder`가
    로컬 `ItemSpawner`와 동일한 트랙 각도에 6개 배치).
  - **비주얼 재사용**: `ItemPickupVisuals.Create`(스프라이트/밥/선택적 트리거 콜라이더)를
    `ItemSpawner`에서 추출 — 로컬은 콜라이더 O, 네트워크 코스메틱은 콜라이더 X. `ItemEffects.
    SpawnBombVisual`을 `SpawnBomb`에서 분리(마커만, BombRunner 없음) — 로컬 단일플레이 동작 무변경.
  - **HUD**: `NetworkRaceHUD`에 로컬 플레이어 슬롯 2개(복제 ID→`ItemCatalog` 재구성) + 방어막/부스트
    플래그 표시 추가(새 캔버스 오브젝트 없이 기존 info 라벨 재사용).
  - **검증 완료**: 6단계 각각 컴파일 체크 → 관련 프리팹/씬 재빌드+스모크 → PlayMode 전체(35개 중
    34 통과, 1 inconclusive는 헤드리스 스크린샷 정상) 순서로 전부 통과. 신규 PlayMode 테스트
    `NetworkItemLogicTests`(ID↔정의 라운드트립, `TypeOf`, 채움/선택 규칙 — 순수 로직만, NGO
    NetworkBehaviour는 NetworkManager 없이 단위 테스트 불가라 RPC/복제 경로는 라이브 테스트 영역).
    스모크 확장: 차량 프리팹에 `NetworkItemSlots`, 레이스매니저 프리팹에 `NetworkItemSpawnManager`,
    씬에 스폰 마커 6개(인덱스 0~5 유일·비원점) 검증. 단계별 커밋 완료(6개 커밋).
  - **`M2Net:` 진단 로그**: 스폰 롤/픽업/재스폰/아이템 사용/폭발/기절/방패 막음에 추가 — 라이브
    테스트로 어디서 막히는지 추적용. **통합 라이브 테스트 통과 후 2a·2b 로그 전부 제거 정리 커밋 예정.**
  - **아직 사용자 라이브 확인 필요(이 환경 한계, 자동 검증은 전부 통과)**: 실제 2인 접속으로만
    검증 가능 — 아래 "다음 단계 후보"의 통합 체크리스트 참고.

## 다음 단계 후보 (31차 작업)
1. **통합 라이브 테스트(최우선, 사용자만 가능)**: ParrelSync 호스트+클론으로 `NetworkRace.unity`
   접속. **2a 잔여 + 2b 신규를 한 번에 점검**:
   - (2a) 접속 후 호스트/접속 버튼·IP창·상태 UI가 양쪽에서 사라지는지(29차 후속수정 3번, 미확인).
   - (2a) 2명 접속 시에만 카운트다운 시작(`requiredPlayers=2`, 호스트 혼자선 브리핑 대기), 양쪽
     3-2-1-GO. 호스트가 양쪽 바퀴 카운트(클라 차로 한 바퀴→양쪽 HUD 증가). 타이머 동일 tick,
     완주 시 올바른 쪽 승리/패배.
   - (2b) GO 전 6지점에 양 피어 동일 픽업(색/종류 일치). 호스트/클라 각각 픽업 시 양쪽 화면에서
     소멸+HUD 슬롯 표시+~5초 후 재스폰. 슬롯 2개 채우고 3개째 주면 주 슬롯 교체.
   - (2b) 클라가 휘발유(Ctrl): 자기 차 부스트 체감(1 RTT 지연 허용 수준인지)+슬롯 클리어+호스트
     화면에도 가속. 폭탄(E): 양쪽 폭탄 표시→armTime 후 반경 내 차가 기절(정지→재가속), 양방향
     확인. 방패: 켜고 폭발 진입→기절 없음+방패 소모(HUD 플래그 해제), 두 번째 폭발엔 기절.
   - Console `M2Net:` 로그로 막히는 지점 추적. **전부 통과하면 2a·2b `M2Net:` 로그 제거 정리 커밋.**
2. **Netcode Milestone 3(Unity Relay/Lobby)**: 직접 IP 대신 방 코드 발급/입장 — Unity Cloud
   프로젝트 이미 연동됨(`cloudProjectId` 확인됨).
3. 아이템 최종형 로스터(재석 유/원자폭탄/C4/황금방패 등), 정식 스테이지 아트/씬, 승리조건 확장.
4. 25차에서 남겨둔 배경 소품/프리미티브 오브젝트의 실제 플레이 화면 육안 확인 — 아직 확인 안 됨.
5. 미확정 디자인 항목 정리: 아이템 슬롯 교체 규칙 확정, 막대형 수류탄 트리거 거리, 비키니시티 IP
   리스킨 여부, 아프리카TV/네더요새 게이지 밸런스 수치.
6. **참고**: 유니티 라이브 에디터 콘솔 로그를 사용자가 복사해줄 필요 없이
   `%LOCALAPPDATA%\Unity\Editor\Editor.log`를 직접 읽어 진단 가능 — 에디터 이상 보고 시 이것부터 확인.

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

## 개발 워크플로우 — 커밋 (2026-07-11부터 적용, 이 프로젝트 한정 사전 승인)
사용자가 명시적으로 요청함: 이 프로젝트에서는 작업(기능 추가/버그 수정 등) 하나가 끝나고
컴파일 체크 + 관련 빌드/스모크 테스트 + PlayMode 테스트가 전부 통과하면, 커밋해도 되는지
매번 물어보지 말고 바로 커밋할 것. 일반 원칙("사용자가 명시적으로 요청할 때만 커밋")은 이 문서의
이 지침으로 이 프로젝트 한정 대체됨 — 매번 재확인 불필요.
- **커밋만 자동, push는 아님**: `git push`는 여전히 매번 명시적으로 물어보고 사용자 승인 받을 것
  (원격/공유 상태에 영향을 주는 행위라 로컬 커밋과는 성격이 다름).
- 커밋 메시지는 기존 로그 스타일(짧은 요약 1줄 + 필요하면 본문, `Co-Authored-By: Claude ...` 트레일러)
  그대로 따를 것.
- 여러 개의 작은 수정이 한 세션 안에 이어질 경우, 사용자가 다음 지시를 내리기 전까지는 검증이 끝난
  단위로 묶어서 커밋(매 파일 저장마다 커밋하는 게 아니라 "한 라운드의 피드백/한 기능" 단위).
- Unity 씬 파일(`Assets/Scenes/*.unity`)처럼 자동 재생성되는 바이너리성 파일도 실제로 변경됐다면
  같이 커밋 대상에 포함(지금까지도 그렇게 해왔음).
- 의심스러운 내용(시크릿처럼 보이는 것 등)이 스테이징에 섞여 있으면 이 자동 승인과 무관하게
  평소처럼 확인하고 사용자에게 알릴 것 — 이 지침은 "언제 커밋할지"에 대한 승인이지 "무엇을
  커밋해도 되는지 확인하는 절차"를 생략해도 된다는 뜻은 아님.

## 조작
| 키 | 기능 |
|---|---|
| ←/→ | 조향 |
| ↑/↓ | 가속/감속(후진) |
| Shift (누르고 있기) | 드리프트 — 조향 중 누르고 있으면 차체가 실제 진행 방향보다 더 빨리 돌아서 코너를 미끄러지듯 통과. 뗀 순간 누르고 있던 시간에 비례한 스피드 부스트 지급(마리오카트식 미니터보, 최소 홀드 시간 미만이면 부스트 없음). |
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
- 공격 아이템에 맞거나 지형지물에 닿거나 트랙 벽에 부딪히면 "비법"을 떨어뜨림 (놓친 횟수가 별점 기준).
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
  Scenes/ (MainMenu, Lobby, Stage_BikiniCity, Stage_AfricaTV, Stage_NetherFortress, ResultScreen,
    NetworkBootstrap — Netcode Milestone 1용, 27차 작업; NetworkRace — Milestone 2a 온라인 레이스, 29차)
  Prefabs/{Player(NetworkVehicle), Network(NetworkRaceManager), Items, Stage}/
  Art/{Sprites, UI}/
```