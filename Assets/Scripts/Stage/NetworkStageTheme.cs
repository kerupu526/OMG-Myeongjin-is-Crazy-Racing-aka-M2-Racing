using M2.Core;
using UnityEngine;

namespace M2.Stage
{
    /// <summary>
    /// Local visual half of the network room's stage choice. The host synchronizes only the
    /// StageType value; every peer deterministically builds the same non-colliding art group so
    /// a selected stage is visible before and during the race without changing NGO authority.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkStageTheme : MonoBehaviour
    {
        GameObject currentRoot;

        public StageType CurrentStage { get; private set; } = StageType.BikiniCity;

        public void Apply(StageType stage)
        {
            if (currentRoot != null && CurrentStage == stage) return;
            if (currentRoot != null) Destroy(currentRoot);

            CurrentStage = stage;
            currentRoot = new GameObject($"NetworkStageTheme_{stage}");
            currentRoot.transform.SetParent(transform, false);

            switch (stage)
            {
                case StageType.AfricaTv:
                    BuildAfricaTheme(currentRoot.transform);
                    break;
                case StageType.NetherFortress:
                    BuildNetherTheme(currentRoot.transform);
                    break;
                default:
                    BuildBikiniTheme(currentRoot.transform);
                    break;
            }
        }

        void BuildBikiniTheme(Transform parent)
        {
            CreatePrefabProp(parent, "BikiniRock_A", StageArtPrefabId.BikiniTerrainRock,
                new Vector3(-47f, 0f, 34f), 1.6f, new Color(0.54f, 0.49f, 0.41f));
            CreatePrefabProp(parent, "BikiniRock_B", StageArtPrefabId.BikiniTerrainRock,
                new Vector3(42f, 0f, -37f), 1.25f, new Color(0.46f, 0.57f, 0.52f));
            CreateBanner(parent, "BikiniStageSign", new Vector3(0f, 2.6f, -50f),
                new Vector3(8f, 1.7f, 0.25f), new Color(0.373f, 0.847f, 0.961f));
        }

        void BuildAfricaTheme(Transform parent)
        {
            CreatePrefabProp(parent, "BroadcastTower_A", StageArtPrefabId.AfricaBroadcastTower,
                new Vector3(-48f, 0f, 30f), 0.72f, new Color(0.34f, 0.34f, 0.44f));
            CreatePrefabProp(parent, "BroadcastTower_B", StageArtPrefabId.AfricaBroadcastTower,
                new Vector3(47f, 0f, -29f), 0.58f, new Color(0.45f, 0.30f, 0.56f));
            CreateBanner(parent, "BroadcastStageSign", new Vector3(0f, 2.8f, -50f),
                new Vector3(9f, 1.9f, 0.25f), new Color(1f, 0.184f, 0.620f));
        }

        void BuildNetherTheme(Transform parent)
        {
            CreatePrefabProp(parent, "LavaRock_A", StageArtPrefabId.NetherLavaRock,
                new Vector3(-48f, 0f, 28f), 1.45f, new Color(0.62f, 0.16f, 0.08f));
            CreatePrefabProp(parent, "LavaRock_B", StageArtPrefabId.NetherLavaRock,
                new Vector3(48f, 0f, -28f), 1.25f, new Color(0.72f, 0.28f, 0.06f));
            CreatePrefabProp(parent, "CoolingArch", StageArtPrefabId.NetherCoolingArch,
                new Vector3(0f, 0f, -50f), 0.78f, new Color(0.30f, 0.18f, 0.26f));
            CreateBanner(parent, "NetherStageSign", new Vector3(0f, 3.1f, -50f),
                new Vector3(8.5f, 1.55f, 0.25f), new Color(1f, 0.36f, 0.16f));
        }

        static void CreatePrefabProp(Transform parent, string name, StageArtPrefabId id, Vector3 position,
            float scale, Color tint)
        {
            StageArtPrefabLibrary library = StageArtPrefabLibrary.Load();
            GameObject prefab = library != null ? library.Get(id) : null;
            if (prefab == null) return;

            GameObject instance = Instantiate(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localScale *= scale;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                RendererColorUtil.ApplyColor(renderer, tint);
            }
        }

        static void CreateBanner(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = name;
            banner.transform.SetParent(parent, false);
            banner.transform.localPosition = position;
            banner.transform.localScale = scale;
            Collider collider = banner.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = banner.GetComponent<Renderer>();
            if (renderer != null) RendererColorUtil.ApplyColor(renderer, color);
        }
    }
}
