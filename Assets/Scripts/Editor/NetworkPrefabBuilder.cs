using System.Reflection;
using M2.Network;
using M2.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace M2.Editor
{
    // Builds Assets/Prefabs/Player/NetworkVehicle.prefab — a real, saved .prefab asset, unlike
    // every other visual in this project (TestTrackBuilder composes everything else purely at
    // editor-build-time with no persisted asset).
    //
    // Why this one needs a real asset: NGO's NetworkManager.AddNetworkPrefab(GameObject) is
    // documented to accept "any GameObject with a NetworkObject component, from any source...
    // dynamically created" — which sounded like it would let this project keep its "everything
    // built by code, no persisted asset" pattern for the network vehicle too. But
    // NetworkObject.GlobalObjectIdHash (what the server and client use to agree on which
    // registered prefab a spawn message refers to) is only ever assigned inside
    // NetworkObject.OnValidate — an Editor-only callback, gated on
    // UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow, which needs the object to already have a
    // real identity (a saved asset or scene object) to hash. A GameObject built purely at
    // runtime (never saved as an asset) would never get OnValidate called on it in a built
    // player, leaving GlobalObjectIdHash stuck at 0 — unverified without a live 2-client
    // connection (this environment can't run one), so rather than gamble on undocumented
    // behavior for the one thing that has to work for players to see each other at all, this
    // takes the standard, well-supported path instead: a real prefab asset, still built by code
    // (same "generate a persisted artifact via an -executeMethod-callable Editor script" pattern
    // TestTrackBuilder.BuildAndSavePersistedScene already uses for the Stage_*.unity scenes) —
    // re-run this menu item (or BuildCheck.BuildNetworkVehiclePrefab in batch mode) any time
    // VehicleController or its visual changes to regenerate it.
    public static class NetworkPrefabBuilder
    {
        const string PrefabDirectory = "Assets/Prefabs/Player";
        const string PrefabPath = PrefabDirectory + "/NetworkVehicle.prefab";

        [MenuItem("M2/Build Network Prefabs")]
        public static void BuildNetworkVehiclePrefab()
        {
            // Milestone 1 scope only: Rigidbody + VehicleController + the real visual model +
            // the 3 networking components. No LapTracker/ItemSlots/tag-dependent gauge systems
            // yet — race flow and items are explicitly out of scope for this round (see
            // CLAUDE.md's Netcode section) and adding them here would need checkpoints/item
            // spawners that don't exist in a bare network bootstrap scene anyway.
            GameObject vehicle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vehicle.name = "NetworkVehicle";
            vehicle.tag = "Player";
            vehicle.transform.localScale = new Vector3(TestTrackBuilder.VehicleWidth, 0.6f, 2f);

            // Same invisible-collision-proxy-plus-real-model split TestTrackBuilder.CreateVehicle
            // uses — the cube is the physics shape only, the child model below is what renders.
            Object.DestroyImmediate(vehicle.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(vehicle.GetComponent<MeshFilter>());

            Rigidbody rb = vehicle.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            vehicle.AddComponent<VehicleController>();
            vehicle.AddComponent<NetworkObject>();
            vehicle.AddComponent<OwnerAuthoritativeNetworkTransform>();
            vehicle.AddComponent<NetworkVehicleSync>();

            TestTrackBuilder.CreateVehicleModel(vehicle.transform);

            if (!AssetDatabase.IsValidFolder(PrefabDirectory))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                }
                AssetDatabase.CreateFolder("Assets/Prefabs", "Player");
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(vehicle, PrefabPath);
            Object.DestroyImmediate(vehicle);

            // NetworkObject.GlobalObjectIdHash is computed inside NetworkObject.OnValidate, an
            // Editor-only callback Unity normally fires automatically after an asset import —
            // but in a headless batch-mode session (-batchmode -nographics -quit), that never
            // happens on its own: confirmed by reading the saved .prefab's serialized YAML
            // after SaveAsPrefabAsset (and even after a forced AssetDatabase.ImportAsset
            // reimport) and finding GlobalObjectIdHash still 0. Every NGO tutorial relies on
            // this getting computed automatically because they build/save the prefab
            // interactively in the Editor GUI, where OnValidate does fire — this project has no
            // interactive path (this environment's GUI Unity Editor sessions are blocked by an
            // unrelated antivirus/Package-Manager issue), so OnValidate is invoked directly via
            // reflection instead. This is safe to do here specifically because `savedPrefab` is
            // the actual saved asset (has a real AssetDatabase identity for
            // GlobalObjectId.GetGlobalObjectIdSlow to hash), not a transient runtime object.
            NetworkObject savedNetworkObject = savedPrefab.GetComponent<NetworkObject>();
            MethodInfo onValidate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
            onValidate.Invoke(savedNetworkObject, null);

            PrefabUtility.SavePrefabAsset(savedPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // GlobalObjectIdHash is internal to Unity.Netcode.Runtime, so it's read back via
            // reflection too, purely to confirm in the log that it actually ended up non-zero.
            FieldInfo hashField = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.NonPublic | BindingFlags.Instance);
            uint hash = (uint)hashField.GetValue(savedNetworkObject);
            Debug.Log($"M2_NETWORK_PREFAB_BUILT: {PrefabPath} (GlobalObjectIdHash={hash})");
        }
    }
}
