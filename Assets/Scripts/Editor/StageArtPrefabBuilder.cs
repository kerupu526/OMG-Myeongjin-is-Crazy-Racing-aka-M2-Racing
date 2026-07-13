using M2.Stage;
using UnityEditor;
using UnityEngine;

namespace M2.Editor
{
    /// <summary>Builds small, reusable stage-art prefabs from the bundled Kenney source models.</summary>
    public static class StageArtPrefabBuilder
    {
        const string PrefabFolder = "Assets/Prefabs/Stage";
        const string LibraryPath = "Assets/Resources/StageArtPrefabLibrary.asset";

        static readonly (StageArtPrefabId id, string sourcePath, string prefabName)[] Definitions =
        {
            (StageArtPrefabId.BikiniTerrainRock,
                "Assets/Art/Models/kenney_nature-kit/Models/FBX format/rock_largeB.fbx", "BikiniTerrainRock"),
            (StageArtPrefabId.AfricaBroadcastTower,
                "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-j.fbx", "AfricaBroadcastTower"),
            (StageArtPrefabId.NetherLavaRock,
                "Assets/Art/Models/kenney_castle-kit/Models/FBX format/rocks-small.fbx", "NetherLavaRock"),
            (StageArtPrefabId.NetherCoolingArch,
                "Assets/Art/Models/kenney_castle-kit/Models/FBX format/gate.fbx", "NetherCoolingArch"),
        };

        [MenuItem("M2/Build Stage Art Prefabs")]
        public static void Build()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);
            EnsureFolder("Assets/Resources");

            StageArtPrefabLibrary library = AssetDatabase.LoadAssetAtPath<StageArtPrefabLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<StageArtPrefabLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            foreach ((StageArtPrefabId id, string sourcePath, string prefabName) in Definitions)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null) throw new System.InvalidOperationException($"Stage art model missing: {sourcePath}");

                GameObject instance = Object.Instantiate(source);
                instance.name = prefabName;
                string outputPath = $"{PrefabFolder}/{prefabName}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
                Object.DestroyImmediate(instance);

                if (prefab == null) throw new System.InvalidOperationException($"Could not create stage prefab: {outputPath}");
                library.Set(id, prefab);
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"M2_STAGE_ART_PREFAB_BUILD_OK: {Definitions.Length} prefabs");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new System.InvalidOperationException($"Invalid Unity asset folder: {path}");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
