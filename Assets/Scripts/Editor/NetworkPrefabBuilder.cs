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

        const string RaceManagerPrefabDirectory = "Assets/Prefabs/Network";
        const string RaceManagerPrefabPath = RaceManagerPrefabDirectory + "/NetworkRaceManager.prefab";

        [MenuItem("M2/Build Network Prefabs")]
        public static void BuildNetworkVehiclePrefab()
        {
            // Milestone 2a adds LapTracker (below) so the host's synced copy of each car counts
            // laps by passing through the race scene's Checkpoint triggers — see
            // NetworkRaceManager. ItemSlots (and the full item roster) stay out of scope until
            // Milestone 2b's server-authoritative item sync.
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
            // Milestone 2a: LapTracker lets the host authoritatively count this car's laps from
            // its synced copy (checkpoints on the host fire for both cars). It also gives the
            // owning client wrong-way detection for free (VehicleController reads it in Awake).
            vehicle.AddComponent<M2.Core.LapTracker>();
            vehicle.AddComponent<NetworkObject>();
            vehicle.AddComponent<OwnerAuthoritativeNetworkTransform>();
            vehicle.AddComponent<NetworkVehicleSync>();

            TestTrackBuilder.CreateVehicleModel(vehicle.transform);

            // Playtester feedback: the networked vehicle rendered with no material at all
            // (empty slots) — confirmed by inspecting the saved .prefab's YAML directly
            // (m_Materials: [{fileID: 0}] on every renderer). CreateVehicleModel silently skips
            // its texture-apply loop if the texture asset failed to load, so this had no
            // visible failure anywhere in the build log until now. Fail loudly instead of
            // producing a silently-broken prefab.
            Renderer[] modelRenderers = vehicle.GetComponentsInChildren<Renderer>();
            if (modelRenderers.Length == 0)
            {
                throw new System.Exception("NetworkVehicle model has zero renderers — " +
                    $"CreateVehicleModel likely failed to load {TestTrackBuilder.VehicleModelPath}.");
            }
            foreach (Renderer renderer in modelRenderers)
            {
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null)
                {
                    throw new System.Exception($"NetworkVehicle renderer '{renderer.name}' has no material/texture " +
                        $"— CreateVehicleModel likely failed to load {TestTrackBuilder.VehicleModelTexturePath}.");
                }
            }

            if (!AssetDatabase.IsValidFolder(PrefabDirectory))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                }
                AssetDatabase.CreateFolder("Assets/Prefabs", "Player");
            }

            // The REAL root cause of the empty-material bug (the previous "use sharedMaterial
            // instead of material" fix in RendererColorUtil.cs was necessary but not
            // sufficient): CreateVehicleModel's RendererColorUtil.ApplyTexture calls create
            // loose, in-memory Material instances — never saved as their own asset file.
            // PrefabUtility.SaveAsPrefabAsset silently serializes any component reference to
            // such a "loose" object as {fileID: 0} (null) — confirmed by rebuilding after the
            // sharedMaterial fix and finding the exact same empty slots in the saved YAML.
            // Scene saves don't have this problem (a loose Material embeds directly into a
            // .unity file fine), which is exactly why this only ever surfaced here — this is
            // the one place in the project saving a prefab ASSET. Giving each material a real,
            // separate identity via AssetDatabase.CreateAsset — BEFORE the prefab save — is
            // what makes SaveAsPrefabAsset able to serialize a real reference to it instead of
            // dropping it.
            const string MaterialsDirectory = PrefabDirectory + "/Materials";
            if (!AssetDatabase.IsValidFolder(MaterialsDirectory))
            {
                AssetDatabase.CreateFolder(PrefabDirectory, "Materials");
            }
            foreach (Renderer renderer in modelRenderers)
            {
                string materialPath = $"{MaterialsDirectory}/NetworkVehicle_{renderer.name}.mat";
                AssetDatabase.CreateAsset(renderer.sharedMaterial, materialPath);
            }
            AssetDatabase.SaveAssets();

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

            // Re-check materials on the SAVED prefab asset itself (not the pre-save in-memory
            // GameObject checked above) — that in-memory check alone previously passed while the
            // actual saved file still ended up with every material slot empty, exactly the class
            // of bug this is meant to catch going forward.
            foreach (Renderer savedRenderer in savedPrefab.GetComponentsInChildren<Renderer>())
            {
                if (savedRenderer.sharedMaterial == null || savedRenderer.sharedMaterial.mainTexture == null)
                {
                    throw new System.Exception($"NetworkVehicle.prefab renderer '{savedRenderer.name}' has no " +
                        "material/texture on the SAVED asset even though the pre-save check passed — the " +
                        "loose-Material-reference issue this method works around may have reappeared.");
                }
            }

            Debug.Log($"M2_NETWORK_PREFAB_BUILT: {PrefabPath} (GlobalObjectIdHash={hash})");
        }

        // Builds Assets/Prefabs/Network/NetworkRaceManager.prefab — the single NetworkObject the
        // host spawns to run/replicate the race (see NetworkRaceManager / NetworkRaceBootstrap).
        // Nothing visual: it's a bare logic object.
        [MenuItem("M2/Build Network Race Manager Prefab")]
        public static void BuildNetworkRaceManagerPrefab()
        {
            GameObject raceManager = new GameObject("NetworkRaceManager");
            raceManager.AddComponent<NetworkObject>();
            raceManager.AddComponent<NetworkRaceManager>();

            if (!AssetDatabase.IsValidFolder(RaceManagerPrefabDirectory))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                }
                AssetDatabase.CreateFolder("Assets/Prefabs", "Network");
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(raceManager, RaceManagerPrefabPath);
            Object.DestroyImmediate(raceManager);

            uint hash = AssignAndReadGlobalObjectIdHash(savedPrefab);
            Debug.Log($"M2_NETWORK_RACE_MANAGER_PREFAB_BUILT: {RaceManagerPrefabPath} (GlobalObjectIdHash={hash})");
        }

        // NetworkObject.GlobalObjectIdHash is only computed inside NetworkObject.OnValidate, an
        // Editor-only callback Unity does NOT fire on its own in headless batch mode (confirmed
        // for the vehicle prefab — see BuildNetworkVehiclePrefab's long comment). Invoke it via
        // reflection on the saved asset, re-save, then read the (internal) hash back to confirm
        // it's non-zero. Shared by both prefab builders so the workaround lives in one place.
        static uint AssignAndReadGlobalObjectIdHash(GameObject savedPrefab)
        {
            NetworkObject savedNetworkObject = savedPrefab.GetComponent<NetworkObject>();
            MethodInfo onValidate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
            onValidate.Invoke(savedNetworkObject, null);

            PrefabUtility.SavePrefabAsset(savedPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            FieldInfo hashField = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.NonPublic | BindingFlags.Instance);
            return (uint)hashField.GetValue(savedNetworkObject);
        }
    }
}
