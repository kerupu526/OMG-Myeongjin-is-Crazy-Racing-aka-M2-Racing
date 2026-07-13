using UnityEngine;

namespace M2.Network
{
    // A plain (non-networked) scene marker for one item spawn location on the track. Baked into
    // NetworkRace.unity identically on both peers by NetworkRaceSceneBuilder, at the same track
    // angles the local scene's ItemSpawners use. NetworkItemSpawnManager reads these for their
    // positions and, driven by replicated spawn state, builds/removes the cosmetic pickup visual
    // as a child of each one.
    public class NetworkItemSpawnPoint : MonoBehaviour
    {
        public int index;
    }
}
