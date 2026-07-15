using UnityEngine;
using UnityEditor;

/// <summary>
/// 클로드 코드가 headless(배치모드)로 유니티를 돌려서 컴파일 에러를 확인하기 위한 진입점.
/// Assets/Editor/ 폴더에 위치해야 함 (Unity Editor 스크립트 규칙).
///
/// 사용법: Unity.exe -batchmode -nographics -quit -projectPath [경로]
///         -executeMethod BuildCheck.CompileCheck -logFile build.log
///
/// 이 메서드까지 실행이 도달했다는 것 자체가 "컴파일 에러 없이 스크립트 로딩 성공"을 의미함.
/// 컴파일 에러가 있으면 Unity가 -executeMethod를 아예 실행하지 못하고 로그에 error CS... 만 남긴 채 종료됨.
/// </summary>
public static class BuildCheck
{
    public static void CompileCheck()
    {
        Debug.Log("=== M2_COMPILE_CHECK_START ===");

        // 여기 도달했다는 것 자체가 컴파일 성공의 증거.
        // 필요하면 여기에 씬 로드/간단한 스모크 테스트를 추가할 수 있음.
        Debug.Log("=== M2_COMPILE_CHECK_OK ===");

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Force-reimports the M2 UI Toolkit assets and verifies every menu VectorImage actually
    /// loads through Resources. Fixes stale Library imports where an SVG's VectorImage
    /// sub-asset is missing (symptom: menu gradients render as flat colors in one project
    /// while a freshly imported clone shows them).
    /// </summary>
    public static void ReimportM2UiAssets()
    {
        Debug.Log("=== M2_UI_REIMPORT_START ===");
        AssetDatabase.ImportAsset("Assets/Resources/M2UI",
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
        AssetDatabase.Refresh();

        string[] vectorPaths =
        {
            "M2UI/Backgrounds/MainGradient", "M2UI/Backgrounds/LobbyGradient",
            "M2UI/Backgrounds/AvatarGradient", "M2UI/Backgrounds/SettingsGradient",
            "M2UI/Backgrounds/ResultGradient", "M2UI/Backgrounds/GaugeFill",
            "M2UI/Backgrounds/HudTopFade", "M2UI/Icons/crown",
        };
        bool ok = true;
        foreach (string path in vectorPaths)
        {
            if (Resources.Load<UnityEngine.UIElements.VectorImage>(path) == null)
            {
                ok = false;
                Debug.LogError($"M2_UI_REIMPORT_MISSING {path}");
            }
        }

        Debug.Log(ok ? "=== M2_UI_REIMPORT_OK ===" : "=== M2_UI_REIMPORT_FAIL ===");
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static void BuildItemSpriteLibrary()
    {
        Debug.Log("=== M2_ITEM_ART_BUILD_START ===");
        try
        {
            M2.Editor.ItemArtBuilder.Build();
            Debug.Log("=== M2_ITEM_ART_BUILD_OK ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"M2_ITEM_ART_BUILD_FAIL: {e}");
            EditorApplication.Exit(1);
        }
    }

    public static void BuildStageArtPrefabs()
    {
        Debug.Log("=== M2_STAGE_ART_PREFAB_BUILD_START ===");
        try
        {
            M2.Editor.StageArtPrefabBuilder.Build();
            Debug.Log("=== M2_STAGE_ART_PREFAB_BUILD_OK ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"M2_STAGE_ART_PREFAB_BUILD_FAIL: {e}");
            EditorApplication.Exit(1);
        }
    }

    public static void SmokeTestStageArtPrefabs()
    {
        Debug.Log("=== M2_STAGE_ART_PREFAB_SMOKE_TEST_START ===");
        var library = AssetDatabase.LoadAssetAtPath<M2.Stage.StageArtPrefabLibrary>(
            "Assets/Resources/StageArtPrefabLibrary.asset");
        if (library == null)
        {
            Debug.LogError("M2_STAGE_ART_PREFAB_SMOKE_TEST_FAIL: library missing");
            EditorApplication.Exit(1);
            return;
        }

        foreach (M2.Stage.StageArtPrefabId id in System.Enum.GetValues(typeof(M2.Stage.StageArtPrefabId)))
        {
            GameObject prefab = library.Get(id);
            if (prefab == null || prefab.GetComponentInChildren<Renderer>(true) == null)
            {
                Debug.LogError($"M2_STAGE_ART_PREFAB_SMOKE_TEST_FAIL: {id} prefab missing renderer");
                EditorApplication.Exit(1);
                return;
            }
        }

        Debug.Log("=== M2_STAGE_ART_PREFAB_SMOKE_TEST_OK ===");
        EditorApplication.Exit(0);
    }

    public static void SmokeTestItemSpriteLibrary()
    {
        Debug.Log("=== M2_ITEM_ART_SMOKE_TEST_START ===");
        var library = AssetDatabase.LoadAssetAtPath<M2.Items.ItemSpriteLibrary>(
            "Assets/Resources/ItemSpriteLibrary.asset");
        if (library == null || library.Entries.Count != M2.Items.ItemCatalog.AllIds.Length)
        {
            Debug.LogError("M2_ITEM_ART_SMOKE_TEST_FAIL: library missing or roster count mismatch");
            EditorApplication.Exit(1);
            return;
        }

        var seen = new System.Collections.Generic.HashSet<M2.Items.NetItemId>();
        foreach (var entry in library.Entries)
        {
            string path = AssetDatabase.GetAssetPath(entry.sprite);
            if (entry.sprite == null || !seen.Add(entry.id) ||
                path.EndsWith("jindungongcheong.png", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"M2_ITEM_ART_SMOKE_TEST_FAIL: invalid/duplicate/forbidden entry {entry.id} ({path})");
                EditorApplication.Exit(1);
                return;
            }
        }

        Debug.Log("=== M2_ITEM_ART_SMOKE_TEST_OK ===");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// TestTrackBuilder.BuildAndSave*Scene()을 헤드리스로 실행해서 Assets/Scenes/Stage_*.unity를
    /// 실제로 생성/갱신하는 진입점들. -executeMethod BuildCheck.BuildBikiniCityScene 처럼 호출.
    /// SceneSmokeTest*는 해당 Build*Scene이 최소 1회 성공한 뒤에야 의미가 있음(그 전엔 씬 파일 자체가 없음).
    /// </summary>
    public static void BuildBikiniCityScene() => BuildScene(M2.Editor.TestTrackBuilder.BuildAndSaveBikiniCityScene);

    public static void BuildAfricaTvScene() => BuildScene(M2.Editor.TestTrackBuilder.BuildAndSaveAfricaTvScene);

    public static void BuildNetherFortressScene() => BuildScene(M2.Editor.TestTrackBuilder.BuildAndSaveNetherFortressScene);

    static void BuildScene(System.Action build)
    {
        Debug.Log("=== M2_SCENE_BUILD_START ===");
        try
        {
            build();
            Debug.Log("=== M2_SCENE_BUILD_OK ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"M2_SCENE_BUILD_FAIL: {e}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 씬이 실제로 로드되고 주요 오브젝트(GameManager 등)가 존재하는지까지 확인하는 확장판들.
    /// 필요해지면 이 메서드들을 -executeMethod로 대신 호출하면 됨.
    ///
    /// NOTE: 각 Stage_*.unity에 GameManager가 저장되어 있어야 통과합니다.
    /// 씬이 아직 없다면 먼저 해당 Build*Scene을 실행할 것.
    /// </summary>
    public static void SceneSmokeTest() => SmokeTestScene("Assets/Scenes/Stage_BikiniCity.unity");

    public static void SceneSmokeTestAfricaTv() => SmokeTestScene("Assets/Scenes/Stage_AfricaTV.unity");

    public static void SceneSmokeTestNetherFortress() => SmokeTestScene("Assets/Scenes/Stage_NetherFortress.unity");

    static void SmokeTestScene(string scenePath)
    {
        Debug.Log("=== M2_SMOKE_TEST_START ===");

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

        var gm = Object.FindFirstObjectByType<M2.Core.GameManager>();
        if (gm == null)
        {
            Debug.LogError("M2_SMOKE_TEST_FAIL: GameManager를 씬에서 찾을 수 없음");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("=== M2_SMOKE_TEST_OK ===");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// M2.Editor.NetworkPrefabBuilder를 헤드리스로 실행해서 Assets/Prefabs/Player/NetworkVehicle.prefab을
    /// 실제로 생성/갱신하는 진입점. -executeMethod BuildCheck.BuildNetworkVehiclePrefab 처럼 호출.
    /// </summary>
    public static void BuildNetworkVehiclePrefab()
    {
        Debug.Log("=== M2_NETWORK_PREFAB_BUILD_START ===");
        try
        {
            M2.Editor.NetworkPrefabBuilder.BuildNetworkVehiclePrefab();
            Debug.Log("=== M2_NETWORK_PREFAB_BUILD_OK ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"M2_NETWORK_PREFAB_BUILD_FAIL: {e}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// NetworkVehicle.prefab이 필요한 컴포넌트를 다 갖췄는지, 그리고 GlobalObjectIdHash(스폰
    /// 메시지 매칭에 쓰이는 값)가 실제로 0이 아닌지까지 확인. GlobalObjectIdHash는 NGO 어셈블리
    /// 내부에서만 접근 가능한 internal 필드라 리플렉션으로 읽음(NetworkPrefabBuilder.cs가 저장할
    /// 때도 같은 방식으로 값을 채워 넣음 — 코드 주석 참고, 배치모드에서는 이 필드가 자동으로
    /// 채워지지 않는 걸 직접 확인했음). BuildNetworkVehiclePrefab이 최소 1회 성공한 뒤에야
    /// 의미가 있음. (실제 스폰 성공 여부 자체는 2개 클라이언트 접속 테스트로만 확인 가능 —
    /// CLAUDE.md 참고, 이 스모크 테스트는 "스폰이 실패할 수밖에 없는 상태"만 미리 걸러냄.)
    /// </summary>
    public static void SmokeTestNetworkVehiclePrefab()
    {
        Debug.Log("=== M2_NETWORK_PREFAB_SMOKE_TEST_START ===");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/NetworkVehicle.prefab");
        if (prefab == null)
        {
            Debug.LogError("M2_NETWORK_PREFAB_SMOKE_TEST_FAIL: NetworkVehicle.prefab을 찾을 수 없음");
            EditorApplication.Exit(1);
            return;
        }

        var networkObject = prefab.GetComponent<Unity.Netcode.NetworkObject>();
        if (networkObject == null ||
            prefab.GetComponent<M2.Player.VehicleController>() == null ||
            prefab.GetComponent<M2.Network.NetworkVehicleSync>() == null ||
            prefab.GetComponent<M2.Network.NetworkItemSlots>() == null)
        {
            Debug.LogError("M2_NETWORK_PREFAB_SMOKE_TEST_FAIL: NetworkObject/VehicleController/NetworkVehicleSync/NetworkItemSlots 중 하나가 없음");
            EditorApplication.Exit(1);
            return;
        }

        var hashField = typeof(Unity.Netcode.NetworkObject).GetField("GlobalObjectIdHash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        uint hash = (uint)hashField.GetValue(networkObject);
        if (hash == 0)
        {
            Debug.LogError("M2_NETWORK_PREFAB_SMOKE_TEST_FAIL: GlobalObjectIdHash가 0 — 이 프리팹은 네트워크로 스폰될 수 없음");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"=== M2_NETWORK_PREFAB_SMOKE_TEST_OK (GlobalObjectIdHash={hash}) ===");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// M2.Editor.NetworkBootstrapSceneBuilder를 헤드리스로 실행해서
    /// Assets/Scenes/NetworkBootstrap.unity를 실제로 생성/갱신하는 진입점.
    /// BuildNetworkVehiclePrefab이 먼저 최소 1회 성공해야 NetworkManager.PlayerPrefab이 채워짐.
    /// </summary>
    public static void BuildNetworkBootstrapScene()
    {
        Debug.Log("=== M2_NETWORK_BOOTSTRAP_SCENE_BUILD_START ===");
        try
        {
            M2.Editor.NetworkBootstrapSceneBuilder.BuildAndSaveScene();
            Debug.Log("=== M2_NETWORK_BOOTSTRAP_SCENE_BUILD_OK ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"M2_NETWORK_BOOTSTRAP_SCENE_BUILD_FAIL: {e}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// NetworkBootstrap.unity가 NetworkManager(+PlayerPrefab 배선) / NetworkBootstrapUI(버튼
    /// 배선) / VehicleCameraFollow를 다 갖췄는지 확인. BuildNetworkBootstrapScene이 최소 1회
    /// 성공한 뒤에야 의미가 있음.
    /// </summary>
    public static void SmokeTestNetworkBootstrapScene()
    {
        Debug.Log("=== M2_NETWORK_BOOTSTRAP_SMOKE_TEST_START ===");

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/NetworkBootstrap.unity");

        var networkManager = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("M2_NETWORK_BOOTSTRAP_SMOKE_TEST_FAIL: NetworkManager를 씬에서 찾을 수 없음");
            EditorApplication.Exit(1);
            return;
        }

        // NetworkManager는 반드시 씬 루트(부모 없음)여야 함 — DontDestroyOnLoad를 자기 자신에게
        // 거는데 이건 루트 오브젝트에서만 동작함. 실제로 한 번 중첩시켜서 NGO의 "NetworkManager
        // is nested under [X]" 검증 에러를 직접 겪은 뒤 추가한 회귀 검사(NetworkBootstrapSceneBuilder.cs 참고).
        if (networkManager.transform.parent != null)
        {
            Debug.LogError($"M2_NETWORK_BOOTSTRAP_SMOKE_TEST_FAIL: NetworkManager가 " +
                $"'{networkManager.transform.parent.name}' 밑에 중첩돼있음 — 씬 루트여야 함");
            EditorApplication.Exit(1);
            return;
        }

        if (networkManager.NetworkConfig.PlayerPrefab == null)
        {
            Debug.LogError("M2_NETWORK_BOOTSTRAP_SMOKE_TEST_FAIL: NetworkConfig.PlayerPrefab이 비어있음");
            EditorApplication.Exit(1);
            return;
        }

        var bootstrapUi = Object.FindFirstObjectByType<M2.Network.NetworkBootstrapUI>();
        if (bootstrapUi == null || bootstrapUi.hostButton == null || bootstrapUi.joinButton == null ||
            bootstrapUi.joinCodeInputField == null)
        {
            Debug.LogError("M2_NETWORK_BOOTSTRAP_SMOKE_TEST_FAIL: NetworkBootstrapUI 버튼/입력창 배선이 비어있음");
            EditorApplication.Exit(1);
            return;
        }

        if (Object.FindFirstObjectByType<M2.Player.VehicleCameraFollow>() == null)
        {
            Debug.LogError("M2_NETWORK_BOOTSTRAP_SMOKE_TEST_FAIL: VehicleCameraFollow가 씬에 없음");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("=== M2_NETWORK_BOOTSTRAP_SMOKE_TEST_OK ===");
        EditorApplication.Exit(0);
    }

    // ---- Milestone 2a: server-authoritative race-state sync ----

    /// <summary>
    /// NetworkRaceManager.prefab(호스트가 스폰하는 레이스 상태 복제 오브젝트)을 생성/갱신.
    /// </summary>
    public static void BuildNetworkRaceManagerPrefab()
    {
        Debug.Log("=== M2_NETWORK_RACE_MANAGER_PREFAB_BUILD_START ===");
        try
        {
            M2.Editor.NetworkPrefabBuilder.BuildNetworkRaceManagerPrefab();
            Debug.Log("=== M2_NETWORK_RACE_MANAGER_PREFAB_BUILD_OK ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"M2_NETWORK_RACE_MANAGER_PREFAB_BUILD_FAIL: {e}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// NetworkRaceManager.prefab이 NetworkObject + NetworkRaceManager를 갖췄고 GlobalObjectIdHash가
    /// 0이 아닌지(스폰 가능 상태인지) 확인. BuildNetworkRaceManagerPrefab이 먼저 성공해야 함.
    /// </summary>
    public static void SmokeTestNetworkRaceManagerPrefab()
    {
        Debug.Log("=== M2_NETWORK_RACE_MANAGER_PREFAB_SMOKE_TEST_START ===");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/NetworkRaceManager.prefab");
        if (prefab == null)
        {
            Debug.LogError("M2_NETWORK_RACE_MANAGER_PREFAB_SMOKE_TEST_FAIL: 프리팹을 찾을 수 없음");
            EditorApplication.Exit(1);
            return;
        }

        var networkObject = prefab.GetComponent<Unity.Netcode.NetworkObject>();
        if (networkObject == null ||
            prefab.GetComponent<M2.Network.NetworkRaceManager>() == null ||
            prefab.GetComponent<M2.Network.NetworkItemSpawnManager>() == null)
        {
            Debug.LogError("M2_NETWORK_RACE_MANAGER_PREFAB_SMOKE_TEST_FAIL: NetworkObject/NetworkRaceManager/NetworkItemSpawnManager 중 하나가 없음");
            EditorApplication.Exit(1);
            return;
        }

        var hashField = typeof(Unity.Netcode.NetworkObject).GetField("GlobalObjectIdHash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        uint hash = (uint)hashField.GetValue(networkObject);
        if (hash == 0)
        {
            Debug.LogError("M2_NETWORK_RACE_MANAGER_PREFAB_SMOKE_TEST_FAIL: GlobalObjectIdHash가 0 — 스폰 불가");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"=== M2_NETWORK_RACE_MANAGER_PREFAB_SMOKE_TEST_OK (GlobalObjectIdHash={hash}) ===");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// NetworkRace.unity(온라인 레이스 씬)을 생성/갱신. 차량/레이스매니저 프리팹이 먼저 빌드돼야
    /// 각각 PlayerPrefab / NetworkConfig.Prefabs에 배선됨.
    /// </summary>
    public static void BuildNetworkRaceScene()
    {
        Debug.Log("=== M2_NETWORK_RACE_SCENE_BUILD_START ===");
        try
        {
            M2.Editor.NetworkRaceSceneBuilder.BuildAndSaveScene();
            Debug.Log("=== M2_NETWORK_RACE_SCENE_BUILD_OK ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"M2_NETWORK_RACE_SCENE_BUILD_FAIL: {e}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// NetworkRace.unity가 NetworkManager(+PlayerPrefab / 레이스매니저 프리팹 등록) / GameManager
    /// (autoStartOnStart=false) / RaceStartGrid / NetworkRaceBootstrap / NetworkRaceHUD를 다
    /// 갖췄는지 확인. BuildNetworkRaceScene이 먼저 성공해야 함.
    /// </summary>
    public static void SmokeTestNetworkRaceScene()
    {
        Debug.Log("=== M2_NETWORK_RACE_SCENE_SMOKE_TEST_START ===");

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/NetworkRace.unity");

        var networkManager = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>();
        if (networkManager == null || networkManager.transform.parent != null)
        {
            Debug.LogError("M2_NETWORK_RACE_SCENE_SMOKE_TEST_FAIL: NetworkManager가 없거나 씬 루트가 아님");
            EditorApplication.Exit(1);
            return;
        }
        if (networkManager.NetworkConfig.PlayerPrefab == null)
        {
            Debug.LogError("M2_NETWORK_RACE_SCENE_SMOKE_TEST_FAIL: PlayerPrefab이 비어있음");
            EditorApplication.Exit(1);
            return;
        }
        var gm = Object.FindFirstObjectByType<M2.Core.GameManager>();
        if (gm == null || gm.autoStartOnStart)
        {
            Debug.LogError("M2_NETWORK_RACE_SCENE_SMOKE_TEST_FAIL: GameManager가 없거나 autoStartOnStart가 꺼져있지 않음");
            EditorApplication.Exit(1);
            return;
        }

        // 레이스매니저 프리팹은 런타임(NetworkRaceBootstrap.AddNetworkPrefab)에 등록되므로 편집
        // 시점 NetworkConfig.Prefabs에는 없음 — 대신 부트스트랩에 프리팹이 배선돼 있는지 확인.
        var bootstrap = Object.FindFirstObjectByType<M2.Network.NetworkRaceBootstrap>();
        if (bootstrap == null || bootstrap.raceManagerPrefab == null)
        {
            Debug.LogError("M2_NETWORK_RACE_SCENE_SMOKE_TEST_FAIL: NetworkRaceBootstrap가 없거나 raceManagerPrefab 배선이 비어있음");
            EditorApplication.Exit(1);
            return;
        }

        if (Object.FindFirstObjectByType<M2.Network.RaceStartGrid>() == null ||
            Object.FindFirstObjectByType<M2.Network.NetworkRaceHUD>() == null)
        {
            Debug.LogError("M2_NETWORK_RACE_SCENE_SMOKE_TEST_FAIL: RaceStartGrid/NetworkRaceHUD 중 하나가 없음");
            EditorApplication.Exit(1);
            return;
        }

        // Milestone 2b: the 6 item spawn markers must exist, carry distinct indices 0..5, and sit
        // out on the track (not stacked at the origin) — i.e. actually placed along the geometry.
        var spawnPoints = Object.FindObjectsByType<M2.Network.NetworkItemSpawnPoint>(FindObjectsSortMode.None);
        if (spawnPoints.Length != 6)
        {
            Debug.LogError($"M2_NETWORK_RACE_SCENE_SMOKE_TEST_FAIL: 아이템 스폰 지점이 6개가 아님 (실제 {spawnPoints.Length}개)");
            EditorApplication.Exit(1);
            return;
        }
        bool[] seenIndex = new bool[6];
        foreach (var sp in spawnPoints)
        {
            if (sp.index < 0 || sp.index >= 6 || seenIndex[sp.index])
            {
                Debug.LogError($"M2_NETWORK_RACE_SCENE_SMOKE_TEST_FAIL: 아이템 스폰 지점 인덱스가 0..5 유일값이 아님 (index={sp.index})");
                EditorApplication.Exit(1);
                return;
            }
            seenIndex[sp.index] = true;
            if (sp.transform.position.sqrMagnitude < 1f)
            {
                Debug.LogError($"M2_NETWORK_RACE_SCENE_SMOKE_TEST_FAIL: 아이템 스폰 지점 {sp.index}이 원점에 붙어있음 — 트랙 위에 배치되지 않음");
                EditorApplication.Exit(1);
                return;
            }
        }

        Debug.Log("=== M2_NETWORK_RACE_SCENE_SMOKE_TEST_OK ===");
        EditorApplication.Exit(0);
    }
}
