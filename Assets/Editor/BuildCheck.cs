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
    /// 씬이 실제로 로드되고 주요 오브젝트(GameManager 등)가 존재하는지까지 확인하는 확장판.
    /// 필요해지면 이 메서드를 -executeMethod로 대신 호출하면 됨.
    ///
    /// NOTE: SampleScene.unity에 GameManager가 저장되어 있어야 통과합니다.
    /// Unity 에디터에서 M2/Build Test Track Scene 실행 후 씬을 저장하세요.
    /// TODO: Stage_BikiniCity 정식 씬이 만들어지면 경로를 교체할 것.
    /// </summary>
    public static void SceneSmokeTest()
    {
        Debug.Log("=== M2_SMOKE_TEST_START ===");

        // TODO: Stage_BikiniCity 정식 씬이 생기면 아래 경로를 교체
        var scenePath = "Assets/Scenes/SampleScene.unity";
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
}
