using UnityEngine;

namespace M2.Stage
{
    public enum StageArtPrefabId
    {
        BikiniTerrainRock,
        AfricaBroadcastTower,
        NetherLavaRock,
        NetherCoolingArch,
    }

    /// <summary>
    /// Runtime bridge to authored stage prefabs. The prefabs themselves stay under
    /// Assets/Prefabs/Stage; this lightweight resource only stores their references.
    /// </summary>
    public class StageArtPrefabLibrary : ScriptableObject
    {
        const string ResourcePath = "StageArtPrefabLibrary";

        public GameObject bikiniTerrainRock;
        public GameObject africaBroadcastTower;
        public GameObject netherLavaRock;
        public GameObject netherCoolingArch;

        static StageArtPrefabLibrary cached;

        public static StageArtPrefabLibrary Load()
        {
            if (cached == null) cached = Resources.Load<StageArtPrefabLibrary>(ResourcePath);
            return cached;
        }

        public GameObject Get(StageArtPrefabId id)
        {
            return id switch
            {
                StageArtPrefabId.BikiniTerrainRock => bikiniTerrainRock,
                StageArtPrefabId.AfricaBroadcastTower => africaBroadcastTower,
                StageArtPrefabId.NetherLavaRock => netherLavaRock,
                StageArtPrefabId.NetherCoolingArch => netherCoolingArch,
                _ => null,
            };
        }

        public void Set(StageArtPrefabId id, GameObject prefab)
        {
            switch (id)
            {
                case StageArtPrefabId.BikiniTerrainRock: bikiniTerrainRock = prefab; break;
                case StageArtPrefabId.AfricaBroadcastTower: africaBroadcastTower = prefab; break;
                case StageArtPrefabId.NetherLavaRock: netherLavaRock = prefab; break;
                case StageArtPrefabId.NetherCoolingArch: netherCoolingArch = prefab; break;
            }
        }
    }
}
