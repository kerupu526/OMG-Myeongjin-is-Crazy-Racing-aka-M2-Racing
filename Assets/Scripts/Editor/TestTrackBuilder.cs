using M2.Core;
using M2.Items;
using M2.Player;
using M2.Stage;
using M2.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace M2.Editor
{
    // Procedurally assembles a throwaway oval test track (ground, walls, checkpoints,
    // vehicle, camera, HUD) so the core loop can be play-tested without hand-wiring
    // GameObjects in the Inspector. Re-runnable: clears the previous build first.
    // Stage-specific hazards/gauge/UI (비키니시티/아프리카TV/네더요새) are wired in via
    // StageAssembler, the same runtime-safe helper StageTestSelector uses to hot-swap
    // stages while in Play mode (see the 1/2/3 hotkeys).
    public static class TestTrackBuilder
    {
        const string RootName = "M2_TestTrack (Generated)";
        // Used only by BuildAndSaveBikiniCityScene — "(Generated)"/"TestTrack" branding is
        // misleading for a hierarchy meant to live permanently in its own saved scene.
        const string PersistedBikiniCityRootName = "BikiniCity";
        const string PersistedBikiniCityScenePath = "Assets/Scenes/Stage_BikiniCity.unity";

        // Base oval radii, perturbed by a sine "wobble" per control point so the centerline
        // winds like a real circuit (sweepers + pinches) instead of a plain ellipse. Purely a
        // shape parameter — everything downstream (walls, checkpoints, item/hazard placement)
        // just calls Geometry.PointAt/TangentAt/NormalAt and doesn't know the shape isn't an
        // ellipse anymore.
        //
        // Both axes share ONE radial multiplier (a true r(θ) polar curve) rather than being
        // wobbled independently — an earlier version used sin() for the X radius and cos() for
        // the Z radius, a 90° phase mismatch that let the two axes bulge out of sync with each
        // other and made the loop cross itself (a broken/tangled track). A single shared
        // multiplier keeps this a simple (non-self-intersecting) closed curve as long as it
        // stays positive, which it always does here since WobbleStrength < 1.
        // 2인용 레이싱에 맞게 확대(원래 32/22, 12) — 곡률 클램프(SafeLateralOffset)가 동적으로
        // 안전을 보장하므로 이 상수들만 바꿔도 자기교차 걱정 없이 커짐. 검증: 이 비율로 가장 좁은
        // 지점도 폭 11m 이상 유지, 한 바퀴 길이 약 202m -> 306m로 증가(Node.js로 사전 검증).
        const float BaseRadiusX = 48f;
        const float BaseRadiusZ = 34f;
        const float WobbleStrength = 0.3f; // fraction of the base radius the loop bulges/pinches by
        const float WobbleFrequency = 3f; // how many bulge/pinch pairs around the loop
        const int ControlPointCount = 24; // spline smoothness — more points = smoother curves

        static readonly TrackGeometry Geometry = new TrackGeometry(BuildControlPoints(), TrackWidth, WallHeight);

        static Vector3[] BuildControlPoints()
        {
            var points = new Vector3[ControlPointCount];
            for (int i = 0; i < ControlPointCount; i++)
            {
                float theta = i * Mathf.PI * 2f / ControlPointCount;
                float wobble = 1f + WobbleStrength * Mathf.Sin(theta * WobbleFrequency);
                points[i] = new Vector3(BaseRadiusX * wobble * Mathf.Cos(theta), 0f, BaseRadiusZ * wobble * Mathf.Sin(theta));
            }
            return points;
        }

        // Vehicle body is 1.2m wide (see CreateVehicle scale). Widened from 12m for real
        // 2-player racing (room to draft/overtake side by side, not just squeeze past).
        const float VehicleWidth = 1.2f;
        const float TrackWidth = 16f;
        const float WallHeight = 1.2f;
        const int WallSegments = 64; // higher than before — sharper curves need more segments to read smoothly
        const int CheckpointCount = 6;
        const int ItemSpawnCount = 6;

        // Vehicle visual: a real Kenney (kenney_car-kit, CC0) low-poly model instead of a
        // billboard sprite. Cars physically rotate to face their travel direction (steering),
        // unlike characters/items — so unlike CLAUDE.md's default "everything is a camera-facing
        // billboard" rule, the vehicle is an exception and renders as an actual rotating 3D mesh.
        // "race.fbx" was picked over kenney_racing-kit's raceCarRed/Green/etc. because it ships
        // with a UV-mapped colormap.png texture atlas (guaranteed to render correctly); the
        // racing-kit cars rely on baked vertex colors, which URP's default Lit shader doesn't
        // read without extra material setup.
        const string VehicleModelPath = "Assets/Art/Models/kenney_car-kit/Models/FBX format/race.fbx";
        const string VehicleModelTexturePath = "Assets/Art/Models/kenney_car-kit/Models/FBX format/Textures/colormap.png";
        // Kenney car-kit models are authored ~2 units long facing +Z already, matching this
        // project's forward convention — but this is unverified without opening the Editor GUI
        // (headless batchmode has no visual feedback). If the car appears to drive backwards in
        // Play mode, add 180 here.
        const float VehicleModelYawOffset = 0f;

        [MenuItem("M2/Build Test Track Scene/Bikini City (비키니시티)")]
        public static void BuildBikiniCity() => Build(StageType.BikiniCity);

        [MenuItem("M2/Build Test Track Scene/Africa TV (아프리카TV)")]
        public static void BuildAfricaTv() => Build(StageType.AfricaTv);

        [MenuItem("M2/Build Test Track Scene/Nether Fortress (네더요새)")]
        public static void BuildNetherFortress() => Build(StageType.NetherFortress);

        // Freezes a BikiniCity build into a real, permanent Assets/Scenes/Stage_BikiniCity.unity
        // scene file instead of building into whatever scene happens to be open — first step
        // away from TestTrackBuilder for this stage (아프리카TV/네더요새는 당분간 그대로 유지).
        // Re-running this OVERWRITES the saved scene from scratch — any manual edits made
        // directly in Stage_BikiniCity.unity since the last run will be lost. Deliberately has
        // NO EditorApplication.Exit call (unlike BuildCheck's headless methods) since this
        // carries a [MenuItem] — a human can click it from an open Editor, and Exit-ing there
        // would force-quit their whole Editor session.
        [MenuItem("M2/Build Persisted Scene/Bikini City (비키니시티)")]
        public static void BuildAndSaveBikiniCityScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("M2: 저장 안 된 변경사항이 있는 씬에서 취소를 선택함 — Stage_BikiniCity 빌드 중단.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build(StageType.BikiniCity, rootName: PersistedBikiniCityRootName, attachStageTestSelector: false);

            bool saved = EditorSceneManager.SaveScene(scene, PersistedBikiniCityScenePath);
            Debug.Log(saved
                ? $"M2: Stage_BikiniCity 씬 저장 완료 → {PersistedBikiniCityScenePath}"
                : $"M2: Stage_BikiniCity 씬 저장 실패 → {PersistedBikiniCityScenePath}");
        }

        // rootName/attachStageTestSelector let a caller opt out of the shared "(Generated)"
        // ephemeral-test-track identity and the temporary 1/2/3 stage-switcher — used by
        // BuildAndSaveBikiniCityScene to freeze a stage into its own permanent, single-stage
        // scene. The 3 MenuItems above keep calling this with just a StageType, so their
        // behavior is unchanged (both new params default to the original ephemeral-build values).
        public static void Build(StageType initialStage, string rootName = RootName, bool attachStageTestSelector = true)
        {
            GameObject existingRoot = GameObject.Find(rootName);
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot);
            }

            var root = new GameObject(rootName);

            CreateGround(root.transform);
            CreateTrackSurface(root.transform);
            CreateWallRing(root.transform, "OuterWall", +1f);
            CreateWallRing(root.transform, "InnerWall", -1f);
            CreateBackgroundDecor(root.transform, initialStage);

            var checkpointsRoot = new GameObject("Checkpoints").transform;
            checkpointsRoot.SetParent(root.transform);
            CreateCheckpoints(checkpointsRoot);

            var itemSpawnersRoot = new GameObject("ItemSpawners").transform;
            itemSpawnersRoot.SetParent(root.transform);
            CreateItemSpawners(itemSpawnersRoot);

            GameObject vehicle = CreateVehicle(root.transform);
            GameObject camera = SetupCamera(root.transform, vehicle.transform);
            SetupHud(root.transform, vehicle);
            SetupGameManager(root.transform, vehicle, initialStage, attachStageTestSelector);

            Selection.activeGameObject = root;
            string switcherHint = attachStageTestSelector
                ? " 1/2/3 키로 스테이지를 바로 바꿔볼 수 있음 (레이스 시작 전에만)."
                : "";
            Debug.Log($"M2 test track built ({initialStage}). Enter Play mode and drive with Arrow Keys/WASD.{switcherHint}");
        }

        static void CreateGround(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            // Sized off the widest possible bulge (base * (1 + WobbleStrength)), not just the
            // base radius, so the wavy centerline never pokes past the visible ground plane.
            float maxWobbleMultiplier = 1f + WobbleStrength;
            float scaleX = (BaseRadiusX * maxWobbleMultiplier + TrackWidth) / 5f + 2f;
            float scaleZ = (BaseRadiusZ * maxWobbleMultiplier + TrackWidth) / 5f + 2f;
            ground.transform.localScale = new Vector3(scaleX, 1f, scaleZ);

            // This plane is the off-track surface only — keep it a distinct, lighter
            // "grass" color so the darker track ring (see CreateTrackSurface) reads clearly
            // as the drivable path instead of blending into the rest of the ground.
            RendererColorUtil.ApplyColor(ground.GetComponent<Renderer>(), new Color(0.25f, 0.4f, 0.22f));
        }

        // How many times a track texture repeats around one full lap. Purely a look parameter —
        // raise it if/when a real texture reads as too stretched along the track's length.
        const float TrackTextureRepeatsPerLap = 24f;

        static void CreateTrackSurface(Transform parent)
        {
            var vertices = new Vector3[WallSegments * 2];
            var uvs = new Vector2[WallSegments * 2];
            var triangles = new int[WallSegments * 6];

            for (int i = 0; i < WallSegments; i++)
            {
                float theta = i * Mathf.PI * 2f / WallSegments;
                // Clamped to the centerline's local turning radius so this edge matches
                // CreateWallRing's wall placement exactly — see TrackGeometry.SafeLateralOffset.
                vertices[i * 2] = Geometry.OffsetPointAt(theta, Geometry.SafeLateralOffset(theta, -TrackWidth / 2f));
                vertices[i * 2 + 1] = Geometry.OffsetPointAt(theta, Geometry.SafeLateralOffset(theta, TrackWidth / 2f));

                // U follows progress around the loop (not true arc length, so texture density
                // varies slightly with the spline's speed) — V spans inner(0) to outer(1) edge.
                float u = (i / (float)WallSegments) * TrackTextureRepeatsPerLap;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }

            for (int i = 0; i < WallSegments; i++)
            {
                int next = (i + 1) % WallSegments;
                int innerA = i * 2;
                int outerA = i * 2 + 1;
                int innerB = next * 2;
                int outerB = next * 2 + 1;

                int t = i * 6;
                triangles[t] = innerA;
                triangles[t + 1] = outerB;
                triangles[t + 2] = outerA;

                triangles[t + 3] = innerA;
                triangles[t + 4] = innerB;
                triangles[t + 5] = outerB;
            }

            var mesh = new Mesh { name = "TrackSurfaceMesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject trackSurface = new GameObject("TrackSurface");
            trackSurface.transform.SetParent(parent);
            trackSurface.transform.localPosition = Vector3.up * 0.01f; // avoid z-fighting with the ground plane

            trackSurface.AddComponent<MeshFilter>().mesh = mesh;
            Renderer renderer = trackSurface.AddComponent<MeshRenderer>();
            // Repeats are already baked into the UVs (TrackTextureRepeatsPerLap), so tiling here
            // stays at 1:1 — doubling it up would re-multiply the repeat count.
            RendererColorUtil.ApplyTexture(renderer, TrackTextureFactory.CreateAsphaltTexture(), Vector2.one, doubleSided: true);
        }

        static void CreateWallRing(Transform parent, string name, float sideSign)
        {
            // Chain of CapsuleColliders instead of rotated box segments or a hand-rolled
            // MeshCollider. Two prior attempts both failed here: overlapping rotated boxes
            // poked jagged inward corners on tight bends that wedged the car (ApplySteering
            // ignores input below minSpeedToSteer, so a wedged car couldn't turn free either);
            // a custom zero/extruded-thickness MeshCollider apparently didn't cook/collide
            // reliably at all (car drove straight through). A capsule's rounded caps overlap
            // seamlessly around any curve with no inward corner, and CapsuleCollider is a
            // native PhysX primitive with guaranteed solid continuous collision — no custom
            // geometry that can silently fail to collide.
            float desiredOffset = sideSign * TrackWidth / 2f;
            float radius = WallHeight / 2f;

            var ring = new GameObject(name).transform;
            ring.SetParent(parent);

            for (int i = 0; i < WallSegments; i++)
            {
                float theta = i * Mathf.PI * 2f / WallSegments;
                float nextTheta = (i + 1) * Mathf.PI * 2f / WallSegments;

                // Clamped per-point so the wall never offsets further than the centerline's
                // local turning radius allows — otherwise this ring folds over itself on tight
                // bends and cuts a diagonal "invisible wall" across the track (see
                // TrackGeometry.SafeLateralOffset for why). Matches CreateTrackSurface's edges.
                Vector3 p0 = Geometry.OffsetPointAt(theta, Geometry.SafeLateralOffset(theta, desiredOffset));
                Vector3 p1 = Geometry.OffsetPointAt(nextTheta, Geometry.SafeLateralOffset(nextTheta, desiredOffset));
                Vector3 mid = (p0 + p1) / 2f;
                float segmentLength = Vector3.Distance(p0, p1);

                GameObject segment = new GameObject($"{name}_{i}");
                segment.transform.SetParent(ring);
                segment.transform.position = mid + Vector3.up * radius;
                segment.transform.rotation = Quaternion.LookRotation((p1 - p0).normalized, Vector3.up);

                CapsuleCollider capsule = segment.AddComponent<CapsuleCollider>();
                capsule.direction = 2; // local Z — matches the LookRotation forward axis above
                capsule.radius = radius;
                // +radius*2 so the rounded caps reach into the neighboring segments, closing
                // the seam instead of leaving a gap or a corner between adjacent capsules.
                capsule.height = segmentLength + radius * 2f;

                // Zero-friction material so the car slides along the wall instead of catching
                // on the capsule surface. Without this, default friction can grip the vehicle
                // in tight bends and fight against the steering/throttle, making it feel stuck.
                // Minimum combine so this wall's zero always wins regardless of the car's material.
                var wallMat = new PhysicsMaterial("WallFrictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0.2f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
                capsule.material = wallMat;
                segment.AddComponent<M2.Core.WallMarker>();
            }
        }

        const int BackgroundDecorCount = 10;
        const float BackgroundDecorMargin = 8f; // how far outside the outer wall decor sits

        static void CreateBackgroundDecor(Transform parent, StageType stage)
        {
            string[] modelPaths;
            string texturePath;

            switch (stage)
            {
                case StageType.BikiniCity:
                    // No colormap.png in kenney_nature-kit — it relies on baked vertex colors,
                    // which URP's default Lit shader doesn't read. Left in as a real test: if
                    // these render blank/white in Play mode, swap to a texture-atlas pack instead.
                    modelPaths = new[]
                    {
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/cliff_blockCave_rock.fbx",
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/rock_largeA.fbx",
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/tree_detailed.fbx",
                    };
                    texturePath = null;
                    break;
                case StageType.AfricaTv:
                    modelPaths = new[]
                    {
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-a.fbx",
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-e.fbx",
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-j.fbx",
                    };
                    texturePath = "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/Textures/colormap.png";
                    break;
                case StageType.NetherFortress:
                    modelPaths = new[]
                    {
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/tower-hexagon-base.fbx",
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/tower-hexagon-mid.fbx",
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/bridge-straight.fbx",
                    };
                    texturePath = "Assets/Art/Models/kenney_castle-kit/Models/FBX format/Textures/colormap.png";
                    break;
                default:
                    return;
            }

            Texture2D texture = texturePath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) : null;
            // Fixed seed so re-running the build menu gives the same layout each time, instead
            // of a different scatter on every rebuild.
            var rng = new System.Random(12345);

            var decorRoot = new GameObject("BackgroundDecor").transform;
            decorRoot.SetParent(parent);

            for (int i = 0; i < BackgroundDecorCount; i++)
            {
                float theta = (i + (float)rng.NextDouble() * 0.6f) * Mathf.PI * 2f / BackgroundDecorCount;
                Vector3 position = Geometry.OffsetPointAt(theta, TrackWidth / 2f + BackgroundDecorMargin);

                string path = modelPaths[rng.Next(modelPaths.Length)];
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) continue;

                GameObject instance = Object.Instantiate(source, decorRoot);
                instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
                instance.transform.position = position;
                instance.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                if (texture != null)
                {
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                    {
                        RendererColorUtil.ApplyTexture(renderer, texture, Vector2.one);
                    }
                }
            }
        }

        static void CreateCheckpoints(Transform parent)
        {
            for (int i = 0; i < CheckpointCount; i++)
            {
                float theta = i * Mathf.PI * 2f / CheckpointCount;
                Vector3 position = Geometry.PointAt(theta);
                Vector3 tangent = Geometry.TangentAt(theta);

                GameObject checkpoint = new GameObject($"Checkpoint_{i}");
                checkpoint.transform.SetParent(parent);
                checkpoint.transform.position = position + Vector3.up * (WallHeight / 2f);
                checkpoint.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

                BoxCollider box = checkpoint.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(TrackWidth, WallHeight * 2f, 1f);

                Checkpoint cp = checkpoint.AddComponent<Checkpoint>();
                cp.index = i;
            }
        }

        static void CreateItemSpawners(Transform parent)
        {
            for (int i = 0; i < ItemSpawnCount; i++)
            {
                // Offset from the checkpoint angles so pickups sit mid-track, not on top of a gate.
                float theta = (i + 0.5f) * Mathf.PI * 2f / ItemSpawnCount;
                Vector3 position = Geometry.PointAt(theta);

                GameObject spawner = new GameObject($"ItemSpawner_{i}");
                spawner.transform.SetParent(parent);
                spawner.transform.position = position;
                spawner.AddComponent<ItemSpawner>();
            }
        }

        static GameObject CreateVehicle(Transform parent)
        {
            Vector3 startPos = Geometry.PointAt(0f);
            Vector3 tangent = Geometry.TangentAt(0f);

            GameObject vehicle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vehicle.name = "Vehicle_Placeholder";
            vehicle.tag = "Player";
            vehicle.transform.SetParent(parent);
            vehicle.transform.position = startPos - tangent * 3f + Vector3.up * 0.5f;
            vehicle.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            vehicle.transform.localScale = new Vector3(VehicleWidth, 0.6f, 2f);

            // This cube is kept invisible as the physics/collision proxy only — the visible car
            // is the real 3D model attached below.
            Object.DestroyImmediate(vehicle.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(vehicle.GetComponent<MeshFilter>());

            Rigidbody rb = vehicle.AddComponent<Rigidbody>();
            rb.mass = 1f;
            // Discrete detection can tunnel a fast (item-boosted) car straight through the
            // thin wall colliders in a single physics step — this is what let players
            // escape the map. Continuous detection sweeps the move instead of just
            // sampling start/end positions.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            vehicle.AddComponent<VehicleController>();
            vehicle.AddComponent<LapTracker>();
            vehicle.AddComponent<ItemSlots>();
            // Stage-specific gauge/state (BikiniCityOxygenGauge, etc.) is attached later by
            // StageAssembler, not here — which stage is active can change at runtime via
            // StageTestSelector.

            CreateVehicleModel(vehicle.transform);

            return vehicle;
        }

        static void CreateVehicleModel(Transform parent)
        {
            var modelSource = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelPath);
            if (modelSource == null)
            {
                Debug.LogWarning($"M2: vehicle model not found at {VehicleModelPath} — falling back to no visual mesh.");
                return;
            }

            GameObject model = Object.Instantiate(modelSource, parent);
            model.name = "VehicleModel";

            // `parent` (the vehicle root) is a primitive Cube scaled non-uniformly to
            // (VehicleWidth, 0.6, 2) to size the invisible collision proxy — a child inherits
            // that scale, which would otherwise squash/stretch this model's real proportions
            // to match the box rather than rendering at its own authored size. Counter-scale
            // here so the visual mesh isn't distorted by the collider's shape, and divide the
            // position offset by the same factors since localPosition is interpreted in the
            // parent's (non-uniformly scaled) local space.
            Vector3 proxyScale = parent.localScale;
            model.transform.localScale = new Vector3(1f / proxyScale.x, 1f / proxyScale.y, 1f / proxyScale.z);
            model.transform.localPosition = new Vector3(0f, -0.3f / proxyScale.y, 0f); // sit the model on the "ground" of the collider proxy
            model.transform.localRotation = Quaternion.Euler(0f, VehicleModelYawOffset, 0f);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(VehicleModelTexturePath);
            if (texture != null)
            {
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                {
                    RendererColorUtil.ApplyTexture(renderer, texture, Vector2.one);
                }
            }
        }

        static GameObject SetupCamera(Transform parent, Transform vehicleTransform)
        {
            GameObject cameraObject = Camera.main != null ? Camera.main.gameObject : null;
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
            }

            VehicleCameraFollow follow = cameraObject.GetComponent<VehicleCameraFollow>();
            if (follow == null)
            {
                follow = cameraObject.AddComponent<VehicleCameraFollow>();
            }
            follow.target = vehicleTransform;

            return cameraObject;
        }

        static void SetupHud(Transform parent, GameObject vehicle)
        {
            GameObject canvasObject = new GameObject("HUD_Canvas");
            canvasObject.transform.SetParent(parent);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            // Without an EventSystem, UI clicks (e.g. the "시작" button) never register at
            // all — nothing routes pointer input to Graphic Raycasters. The project uses the
            // New Input System exclusively (Project Settings > Active Input Handling), so this
            // needs InputSystemUIInputModule, not the legacy StandaloneInputModule that
            // EventSystem.AddComponent<EventSystem>() alone would leave unusable.
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.transform.SetParent(parent);
                eventSystemObject.AddComponent<EventSystem>();
                var uiModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                // AddComponent<T>() at script time skips the auto-wiring the Inspector's "Add
                // Component" button normally does (via the component's Reset callback) — without
                // this, the module has no Point/Click/etc. actions bound at all, so it silently
                // never registers any pointer input (no exceptions, just nothing happens; this is
                // what caused clicks on the "시작" button to do nothing with a clean console).
                uiModule.AssignDefaultActions();
            }

            GameObject textObject = new GameObject("RaceLabel");
            textObject.transform.SetParent(canvasObject.transform);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(320f, 220f);

            GameObject timerObject = new GameObject("RaceTimer");
            timerObject.transform.SetParent(parent);
            RaceTimer timer = timerObject.AddComponent<RaceTimer>();
            timer.lapTracker = vehicle.GetComponent<LapTracker>();
            // Don't call timer.StartRace() here — GameManager controls when the race starts.

            RaceHUD hud = canvasObject.AddComponent<RaceHUD>();
            hud.lapTracker = vehicle.GetComponent<LapTracker>();
            hud.raceTimer = timer;
            hud.itemSlots = vehicle.GetComponent<ItemSlots>();
            // hud.gameManager is wired in SetupGameManager after this method runs.
            hud.label = text;

            // Bottom-right debug readout: collision state / acceleration / speed, for tuning.
            GameObject debugTextObject = new GameObject("VehicleDebugLabel");
            debugTextObject.transform.SetParent(canvasObject.transform);
            Text debugText = debugTextObject.AddComponent<Text>();
            debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            debugText.fontSize = 22;
            debugText.color = Color.white;
            debugText.alignment = TextAnchor.LowerRight;

            RectTransform debugRect = debugTextObject.GetComponent<RectTransform>();
            debugRect.anchorMin = new Vector2(1f, 0f);
            debugRect.anchorMax = new Vector2(1f, 0f);
            debugRect.pivot = new Vector2(1f, 0f);
            debugRect.anchoredPosition = new Vector2(-20f, 20f);
            debugRect.sizeDelta = new Vector2(320f, 120f);

            VehicleDebugHUD debugHud = canvasObject.AddComponent<VehicleDebugHUD>();
            debugHud.vehicleController = vehicle.GetComponent<VehicleController>();
            debugHud.label = debugText;

            // Top-center item-use popup ("휘발유 사용!" etc.) — the boost itself has no VFX yet.
            GameObject itemUseTextObject = new GameObject("ItemUseLabel");
            itemUseTextObject.transform.SetParent(canvasObject.transform);
            Text itemUseText = itemUseTextObject.AddComponent<Text>();
            itemUseText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemUseText.fontSize = 32;
            itemUseText.color = Color.yellow;
            itemUseText.alignment = TextAnchor.UpperCenter;
            itemUseText.fontStyle = FontStyle.Bold;

            RectTransform itemUseRect = itemUseTextObject.GetComponent<RectTransform>();
            itemUseRect.anchorMin = new Vector2(0.5f, 1f);
            itemUseRect.anchorMax = new Vector2(0.5f, 1f);
            itemUseRect.pivot = new Vector2(0.5f, 1f);
            itemUseRect.anchoredPosition = new Vector2(0f, -20f);
            itemUseRect.sizeDelta = new Vector2(500f, 60f);

            ItemUseNotifier notifier = canvasObject.AddComponent<ItemUseNotifier>();
            notifier.label = itemUseText;
            notifier.Bind(vehicle.GetComponent<ItemSlots>());
        }

        // ---- GameManager + RaceFlowUI + stage assembly ----

        static void SetupGameManager(Transform parent, GameObject vehicle, StageType initialStage, bool attachStageTestSelector)
        {
            // --- GameManager ---
            GameObject gmObject = new GameObject("GameManager");
            gmObject.transform.SetParent(parent);
            GameManager gm = gmObject.AddComponent<GameManager>();
            // 테스트 트랙 전용: 조작법 안내(Briefing)를 고정 시간이 아니라 '시작' 버튼(또는
            // Space 키)을 누를 때까지 대기하게 함. 레이스가 실제로 시작되면(Countdown 진입)
            // RaceFlowUI가 기존 로직 그대로 브리핑 패널을 꺼줌.
            gm.waitForManualStart = true;

            gm.racers.Add(vehicle.GetComponent<LapTracker>());
            gm.vehicles.Add(vehicle.GetComponent<VehicleController>());

            RaceTimer timer = Object.FindFirstObjectByType<RaceTimer>();
            gm.raceTimer = timer;

            // Wire gameManager on RaceHUD
            RaceHUD hud = Object.FindFirstObjectByType<RaceHUD>();
            if (hud != null) hud.gameManager = gm;

            // --- RaceFlowUI ---
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            GameObject canvasObject = canvas.gameObject;

            RaceFlowUI flowUI = canvasObject.AddComponent<RaceFlowUI>();
            flowUI.gameManager = gm;
            flowUI.raceTimer = timer;

            // Briefing panel
            GameObject briefingPanelObj = SimpleUIFactory.CreateFullscreenPanel(canvasObject.transform, "BriefingPanel",
                new Color(0f, 0f, 0f, 0.75f));
            Text briefingText = SimpleUIFactory.CreateCenteredText(briefingPanelObj.transform, "BriefingText",
                28, Color.white);
            flowUI.briefingPanel = briefingPanelObj;
            flowUI.briefingText = briefingText;

            Button startButton = SimpleUIFactory.CreateButton(briefingPanelObj.transform, "StartButton", "시작",
                new Vector2(0f, -180f), new Vector2(200f, 60f));
            flowUI.startButton = startButton;

            // Countdown panel
            GameObject countdownPanelObj = SimpleUIFactory.CreateFullscreenPanel(canvasObject.transform, "CountdownPanel",
                new Color(0f, 0f, 0f, 0.5f));
            Text countdownText = SimpleUIFactory.CreateCenteredText(countdownPanelObj.transform, "CountdownText",
                96, Color.yellow);
            flowUI.countdownPanel = countdownPanelObj;
            flowUI.countdownText = countdownText;

            // Result panel
            GameObject resultPanelObj = SimpleUIFactory.CreateFullscreenPanel(canvasObject.transform, "ResultPanel",
                new Color(0f, 0f, 0f, 0.8f));
            Text resultText = SimpleUIFactory.CreateCenteredText(resultPanelObj.transform, "ResultText",
                36, Color.white);
            flowUI.resultPanel = resultPanelObj;
            flowUI.resultText = resultText;

            // Hide all panels initially (RaceFlowUI.Start will also do this at runtime)
            briefingPanelObj.SetActive(false);
            countdownPanelObj.SetActive(false);
            resultPanelObj.SetActive(false);

            // --- Stage-specific hazards/gauge/UI ---
            GameObject stageHazardsRoot = new GameObject("StageHazards");
            stageHazardsRoot.transform.SetParent(parent);

            BuiltStage builtStage = StageAssembler.Attach(initialStage, stageHazardsRoot.transform, parent,
                vehicle, canvas, flowUI, Geometry);

            // --- Temporary in-Play stage switcher (1/2/3 hotkeys), 테스트 전용 ---
            // 정식 씬으로 고정되는 빌드(BuildAndSaveBikiniCityScene)는 이 스위처가 필요 없음 —
            // 그 씬은 처음부터 한 스테이지 전용이라 다른 스테이지로 바꿀 일이 없음.
            if (attachStageTestSelector)
            {
                Text hintLabel = SimpleUIFactory.CreateCornerText(canvasObject.transform, "StageSwitchHint",
                    new Vector2(0f, 0f), new Vector2(20f, 20f), TextAnchor.LowerLeft);
                hintLabel.color = Color.green;

                StageTestSelector selector = canvasObject.AddComponent<StageTestSelector>();
                selector.gameManager = gm;
                selector.vehicle = vehicle;
                selector.canvas = canvas;
                selector.flowUI = flowUI;
                selector.worldParent = stageHazardsRoot.transform;
                selector.trackCenter = parent;
                selector.geometry = Geometry;
                selector.hintLabel = hintLabel;
                selector.Initialize(builtStage);
            }

            // --- 임시 디버그: H 키로 콜라이더(히트박스) 와이어프레임 토글 ---
            canvasObject.AddComponent<HitboxDebugToggle>();
        }
    }
}
