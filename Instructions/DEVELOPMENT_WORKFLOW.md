# 개발·검증·커밋 절차

## 컴파일과 씬 스모크

스크립트를 수정할 때마다 다음을 실행한다. 현재 환경에서 PowerShell 실행 정책이 막힐 수 있으므로 프로세스 한정 `-ExecutionPolicy Bypass`를 사용한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\check_build.ps1 -ProjectPath "C:\Users\User\Desktop\unity\M2_Racing"
```

필요한 경우 같은 명령에 `-Method`를 붙여 특정 스모크를 실행한다.

- `BuildCheck.SceneSmokeTest`, `SceneSmokeTestAfricaTv`, `SceneSmokeTestNetherFortress`
- `BuildCheck.SmokeTestNetworkBootstrapScene`, `SmokeTestNetworkRaceScene`
- `BuildCheck.SmokeTestNetworkVehiclePrefab`, `SmokeTestNetworkRaceManagerPrefab`
- `BuildCheck.BuildItemSpriteLibrary`, `SmokeTestItemSpriteLibrary`

Unity 최초 임포트/전체 재컴파일은 래퍼의 60초 폴링 한도보다 길 수 있다. 래퍼가 먼저 실패해도 `Unity.exe`와 `Temp/UnityLockfile`이 남아 있으면 종료시키지 말고 `build.log`의 `M2_..._OK` 또는 실제 오류를 기다린다.

## PlayMode 테스트

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\check_playtest.ps1 -ProjectPath "C:\Users\User\Desktop\unity\M2_Racing"
```

- 결과는 `playtest_results.xml`, 로그는 `playtest.log`에 있다.
- 헤드리스 스크린샷 테스트의 `Inconclusive` 1개는 정상이다. 그 외 실패는 허용하지 않는다.
- 코드로 트리거 콜라이더를 추가하면 `Reset()`이 호출되지 않으므로 `isTrigger = true`를 직접 지정한다.
- Input System PlayMode 테스트는 `StableInputTestFixture`의 `AddTestKeyboard()`와 `Press()`를 사용한다. 활성 UI 입력 모듈이 있는 Unity 6 에디터에서는 전역 상태를 교체하는 `InputTestFixture`를 사용하지 않는다. `WaitForFixedUpdate()` 뒤에 `Press()`를 호출하지 말고, 여러 키 입력 사이에는 프레임을 하나 둔다.

## Unity 편집기와 씬

- 씬/프리팹 재생성은 빌더를 수정했거나 생성물이 실제로 필요한 경우에만 수행한다.
- 사용자가 만든 미커밋 씬 변경이 있으면 재생성으로 덮어쓰지 않는다. 이번 작업에서는 `Assets/Scenes/NetworkRace.unity`가 사용자 변경으로 남아 있으므로 커밋 대상에서 제외했다.
- 실제 화면은 Unity 에디터 Play 모드에서 확인한다. 31차에는 세 스테이지를 확인했고, 공통 HUD 중첩은 다음 UI 실제 구현 단계의 작업이다.

## 커밋 규칙

- 이 프로젝트는 기능·수정 단위가 검증되면 사용자 재확인 없이 로컬 커밋한다.
- push는 자동으로 하지 않는다. 사용자가 명시적으로 승인할 때만 수행한다.
- 스테이징 전에 `git status`, `git diff --cached --check`, `git diff --cached --stat`을 확인한다.
- 기존 사용자 변경과 무관한 파일만 pathspec으로 명시해 스테이징한다. 시크릿처럼 보이는 파일은 자동 승인 대상이 아니다.
