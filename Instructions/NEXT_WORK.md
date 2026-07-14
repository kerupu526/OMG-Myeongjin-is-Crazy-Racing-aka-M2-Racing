# 다음 작업 메모

기준일: 2026-07-13

## 35차 UI Toolkit 재구축 계획 — Figma 기준

현재 `NetworkMenuUI`는 런타임에서 uGUI `GameObject`와 절대 좌표를 대량 생성한다. 1280×720 기준에서도 아바타 선택·로비 설정·환경설정의 텍스트와 컨트롤이 겹치는 문제가 실제 실행 화면에서 확인됐으므로, 이 구조의 추가 보정은 중단한다.

### 시각 기준과 자산

- 기준 Figma: `lfLnf75LdgHNeiCpzgaVKU`의 사용자 제공 시안. 메인·로비·HUD·결과·아바타·환경설정이 이미 배치되어 있으며, 1280×720 화면 프레임과 Jua / Black Han Sans / Fredoka 서체를 사용한다.
- 원본 자산: `M2 레이싱 게임 UI 디자인/uploads/` 및 `(2)/figma/uploads/`의 SVG/PNG. `add`, `door`, `mask`, `settings`, `map`, `racing-flag`, `checkmark`, `restart`, `trophy`, `first-place-medal`, `second-place-medal`, `banana`, `bomb`, `Gasoline`, 스테이지 아이콘을 우선 반입한다. 이 폴더를 런타임에서 직접 참조하지 않는다.
- Figma 파일에는 로컬 토큰·컴포넌트·연결 라이브러리가 없다. Figma 편집 권한이 확인되면 먼저 M2 전용 토큰과 컴포넌트를 만든 뒤 화면에 인스턴스로 조립한다.

### 후속 구현 순서

1. **기반** — `Assets/UI Toolkit/M2/`에 `M2Theme.uss`, 공통 `M2Shell.uxml`, 아이콘 `VisualElement` 규칙, 1280×720 기준 레이아웃 토큰을 만든다. 배경 그라데이션, 진한 외곽선, 그림자, 카드·칩·버튼 상태를 USS로만 정의한다.
2. **공통 컴포넌트** — UXML 템플릿으로 `M2Button`, `M2IconButton`, `M2Panel`, `M2PlayerCard`, `M2RoomCode`, `M2Chip`, `M2SliderRow`, `M2Toast`를 만들고, Figma에서 상태·크기·아이콘 슬롯을 정의한다. 텍스트를 이모지로 대체하지 않고 SVG/PNG 아이콘을 사용한다.
3. **메뉴 전환 1차** — 메인, 방 만들기, 방 참가, 로비를 UI Toolkit으로 교체한다. 기존 `NetworkBootstrapUI`, `RoomSettingsUI`, `M2PlayerProfile`의 공개 동작은 어댑터로 연결하고, 새 `M2-XXXX` 코드만 표시·입력한다.
4. **메뉴 전환 2차** — 아바타·환경설정·온라인 HUD·결과를 UXML/USS로 이행한다. 선택 컨트롤은 반응형 `flex`/`grid` 구조로 만들고, 같은 화면에 절대 좌표로 개별 텍스트를 겹쳐 놓지 않는다.
5. **검증·정리** — 1280×720과 1920×1080에서 메인/로비/아바타/설정/HUD/결과를 점검한다. 기존 uGUI는 새 화면이 동등 기능을 통과할 때까지 제거하지 않으며, 사용자 미커밋 씬은 건드리지 않는다.

### Figma 작업 순서

1. `Foundations`: 색·공간·반경·테두리·그림자·타이포그래피 토큰을 만든다.
2. `Components`: 아이콘 버튼, 큰 메뉴 버튼, 칩, 플레이어 카드, 방 코드 카드, 설정 행을 하나씩 변형·상태까지 만든다.
3. `Screens`: 메인 → 로비 → HUD/결과 → 아바타/설정 순서로 새 컴포넌트 인스턴스를 조립하고 각 화면을 캡처 검수한다.
4. Figma 토큰과 UXML/USS 클래스명을 일대일 표로 남겨 구현 단계에서 색상·여백을 재해석하지 않게 한다.

### 방 코드 계약

- 사용자 입력·표시·세션 ID는 `M2-` 뒤에 숫자·대문자 4자리(`M2-1L4G`)를 사용한다.
- 호스트는 충돌 시 새 코드를 다시 생성하고, 참가자는 같은 문자열을 세션 ID로 참가한다. Unity가 별도로 생성하는 서비스 join code는 UI에 노출하지 않는다.
- 실제 호스트/Clone 2인 테스트에서 생성·참가·로비 표시가 모두 같은 코드를 유지하는지 검증한다.

## 32차 UI 실제 구현 반영 현황

기준 디자인은 사용자가 제공한 `M2 레이싱 게임 UI 디자인/M2 Racing UI.html`이다. 이번 연속 작업에서는 기존 기능 확인용 표현을 다음처럼 실제 Unity UI 흐름으로 교체·연결했다.

- `ecf1f2d Fix result layout and compact race HUD`: 결과 카드의 장문 2인·5바퀴 레이아웃이 카드 안에 들어가도록 조정하고, 레이스 HUD 전체를 10% 축소했다.
- `ee5f696 Implement Relay main and lobby UI`: 메인, 방 만들기, 방 참가, 대기 로비를 Relay 기존 동작에 연결했다. 방 코드는 1280×720 기준으로 크게 표시된다.
- `4545e57 Add persistent avatar profile UI`: 레이서 이름과 핑크·하늘·민트 아바타 색을 저장하고 메인·로비에 반영한다.
- `218c99f Add persistent game settings UI`: 마스터 볼륨과 전체 화면 선택값을 저장한다. 볼륨은 `AudioListener`에 즉시 적용되며, 전체 화면은 독립 실행 빌드에서 적용된다.
- `14416c7 Show saved racer name in results`: 결과 화면에서 `Vehicle_Placeholder` 대신 저장된 로컬 레이서 이름을 표시한다.
- `5fc6618 Stabilize PlayMode input test isolation`: Unity 6의 UI 입력 상태와 충돌하던 전역 Input System 재설정을 없애고, 테스트별 가상 키보드 생성·정리로 바꿨다.
- `6c3c7bd Implement formal network race HUD presentation`: 온라인 씬의 임시 정보/배너 텍스트를 90% 스케일의 카드형 HUD로 교체했다. 바퀴·시간·대전 상태·실제 아이템 스프라이트/상세·스피드전 자동 휘발유 안내·카운트다운·결과 카드가 네트워크 상태를 읽어 표시된다.
- `ea71a97 Synchronize online race result profiles`: 각 레이서의 저장된 이름·아바타 색과 호스트가 확정한 순위·완주 여부·완주 시간·별점·승리 조건을 양쪽 HUD/결과 카드에 복제한다.

기존 `RaceHUD`와 `NetworkRaceHUD`는 모두 바퀴·시간·대전 상태·아이템 상세를 1280×720 기준으로 제공하며, 스피드전에서는 랜덤 아이템 슬롯 대신 5초 자동 휘발유 안내를 표시한다. 온라인 HUD는 상대 이름·아바타 색과 상세 결과 행까지 표시하며, 실제 Relay 2인 연결 검증만 남아 있다.

## 다음 우선순위 — UI 실제 구현 후속

1. **결과 버튼 흐름과 실제 2인 검수**
   - 온라인 결과의 이름·아바타·순위·완주 시간·별점·승리 조건 복제는 구현됐다. Relay 방 코드로 호스트·참가자 양쪽의 실제 결과 카드가 같은 순서와 기록을 표시하는지 검수한다.
   - 결과 화면의 다시 하기·로비로·메인으로 버튼을 실제 네트워크 흐름에 연결한다. 로컬 재시작만 먼저 구현해 온라인 상태를 어긋나게 만들지 않는다.
2. **로비 설정 동기화 확장**
   - 방장 전용 스테이지 선택(비키니시티·아프리카TV·네더요새), 준비 상태, 참가자 표시를 구현하고 룸 설정으로 동기화한다.
   - 현재 개발용 스테이지 전환과 분리해 룸 설정 기반 로드 흐름을 만든다.
3. **아바타 범위 확장과 온라인 프로필**
   - 현재 이름·색 3종에서 몸/눈/입/볼/귀/모자/번호판으로 확장한다.
   - 이름·색은 레이스 HUD·결과에 동기화된다. 확장 선택값은 로비 참가자 카드와 상대방 화면까지 동기화한다.
4. **환경설정 범위 확장**
   - BGM/SFX 볼륨, 그래픽 품질, 창/전체 화면 전환, 언어를 실제 저장·적용한다.
   - 아직 구현되지 않은 항목을 설정 화면에 표시만 하지 않는다.
5. **실제 화면 검수**
   - 1280×720과 1920×1080에서 메인·로비·HUD·결과 카드의 여백, 텍스트 줄바꿈, 버튼 클릭 영역을 확인한다.
   - 사용자 수정 중인 씬을 저장하거나 재생성하지 않은 상태에서 안전한 별도 테스트 경로를 사용한다.

## 검증 상태

- 39차(2026-07-14): UI Toolkit 최종 보정 뒤 헤드리스 PlayMode 전체 84건 전원 통과(실패·건너뜀·inconclusive 0건). 실제 Play Mode 화면 캡처 증빙만 사용자 재개 후 남긴다.

- 이번 연속 작업에서 직접 실행한 PlayMode 범위: `M2PlayerProfileTests` 2건, `M2GameSettingsTests` 1건, `NetworkMenuPresentationTests` 2건, `RaceFlowPresentationTests` 3건 — 모두 통과했다.
- 이전에 실행한 `SpeedModeTests` 5건도 통과했다.
- 순위·동점·별점 우선 회귀 테스트를 추가한 뒤 전체 PlayMode 70건을 다시 실행했고, 70건 모두 통과했다(실패·inconclusive 0건).

## 온라인 검증 보류

- Relay 방 코드 실제 호스트·클라이언트 2인 라이브 테스트가 남아 있다.
- 현재 작업 지침의 인터넷 제한 때문에 Cloud 인증·방 생성은 실행하지 않는다. 사용자가 인터넷 사용을 허용한 다음 온라인 플레이테스트에서 수행한다.

## 결정 대기

- 비키니 시티의 특정 IP 리스킨 여부는 별도 사용자 결정이 필요하다. 어떤 리스킨도 임의로 추가하지 않는다.

## 작업 규칙

- `Assets/Scenes/NetworkRace.unity`와 `Assets/Scenes/Stage_BikiniCity.unity`의 사용자 미커밋 변경은 재생성·저장·커밋하지 않는다.
- 아이템 PNG는 안전한 `Assets/Art/Sprites`만 사용하며, `jindungongcheong.png`는 참조하지 않는다.
- 기능 단위가 완료될 때마다 검증하고 명확한 메시지로 로컬 커밋한다. push는 사용자 승인 전까지 하지 않는다.
