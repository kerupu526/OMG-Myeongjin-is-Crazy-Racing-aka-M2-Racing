using M2.Core;
using M2.Items;
using M2.Player;
using M2.Stage;
using M2.UI;
using UnityEditor;
using UnityEngine;
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
        const float BaseRadiusX = 32f;
        const float BaseRadiusZ = 22f;
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

        // Vehicle body is 1.2m wide (see CreateVehicle scale). Track width fits two cars
        // side by side with room to steer, plus ~2m of extra safety margin: 12m total.
        const float VehicleWidth = 1.2f;
        const float TrackWidth = 12f;
        const float WallHeight = 1.2f;
        const int WallSegments = 64; // higher than before — sharper curves need more segments to read smoothly
        const int CheckpointCount = 6;
        const int ItemSpawnCount = 6;

        [MenuItem("M2/Build Test Track Scene/Bikini City (비키니시티)")]
        public static void BuildBikiniCity() => Build(StageType.BikiniCity);

        [MenuItem("M2/Build Test Track Scene/Africa TV (아프리카TV)")]
        public static void BuildAfricaTv() => Build(StageType.AfricaTv);

        [MenuItem("M2/Build Test Track Scene/Nether Fortress (네더요새)")]
        public static void BuildNetherFortress() => Build(StageType.NetherFortress);

        public static void Build(StageType initialStage)
        {
            GameObject existingRoot = GameObject.Find(RootName);
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot);
            }

            var root = new GameObject(RootName);

            CreateGround(root.transform);
            CreateTrackSurface(root.transform);
            CreateWallRing(root.transform, "OuterWall", +1f);
            CreateWallRing(root.transform, "InnerWall", -1f);

            var checkpointsRoot = new GameObject("Checkpoints").transform;
            checkpointsRoot.SetParent(root.transform);
            CreateCheckpoints(checkpointsRoot);

            var itemSpawnersRoot = new GameObject("ItemSpawners").transform;
            itemSpawnersRoot.SetParent(root.transform);
            CreateItemSpawners(itemSpawnersRoot);

            GameObject vehicle = CreateVehicle(root.transform);
            GameObject camera = SetupCamera(root.transform, vehicle.transform);
            SetupHud(root.transform, vehicle);
            SetupGameManager(root.transform, vehicle, initialStage);

            Selection.activeGameObject = root;
            Debug.Log($"M2 test track built ({initialStage}). Enter Play mode and drive with Arrow Keys/WASD. " +
                "1/2/3 키로 스테이지를 바로 바꿔볼 수 있음 (레이스 시작 전에만).");
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

        static void CreateTrackSurface(Transform parent)
        {
            var vertices = new Vector3[WallSegments * 2];
            var triangles = new int[WallSegments * 6];

            for (int i = 0; i < WallSegments; i++)
            {
                float theta = i * Mathf.PI * 2f / WallSegments;
                vertices[i * 2] = Geometry.OffsetPointAt(theta, -TrackWidth / 2f);
                vertices[i * 2 + 1] = Geometry.OffsetPointAt(theta, TrackWidth / 2f);
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
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject trackSurface = new GameObject("TrackSurface");
            trackSurface.transform.SetParent(parent);
            trackSurface.transform.localPosition = Vector3.up * 0.01f; // avoid z-fighting with the ground plane

            trackSurface.AddComponent<MeshFilter>().mesh = mesh;
            Renderer renderer = trackSurface.AddComponent<MeshRenderer>();
            RendererColorUtil.ApplyColor(renderer, new Color(0.16f, 0.18f, 0.2f), doubleSided: true);
        }

        static void CreateWallRing(Transform parent, string name, float sideSign)
        {
            var ring = new GameObject(name).transform;
            ring.SetParent(parent);
            float lateralOffset = sideSign * TrackWidth / 2f;

            for (int i = 0; i < WallSegments; i++)
            {
                float theta = i * Mathf.PI * 2f / WallSegments;
                float nextTheta = (i + 1) * Mathf.PI * 2f / WallSegments;

                Vector3 p0 = Geometry.OffsetPointAt(theta, lateralOffset);
                Vector3 p1 = Geometry.OffsetPointAt(nextTheta, lateralOffset);
                Vector3 mid = (p0 + p1) / 2f;
                float segmentLength = Vector3.Distance(p0, p1);

                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"{name}_{i}";
                segment.transform.SetParent(ring);
                segment.transform.position = mid + Vector3.up * (WallHeight / 2f);
                segment.transform.rotation = Quaternion.LookRotation((p1 - p0).normalized, Vector3.up);
                segment.transform.localScale = new Vector3(1f, WallHeight, segmentLength * 1.05f);

                // 2.5D look: the wall is collision-only. A standing 3D box here is exactly
                // what made the track read as "3D" — only the flat ground should be visible.
                Object.DestroyImmediate(segment.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(segment.GetComponent<MeshFilter>());
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

            // 2.5D rule (CLAUDE.md): vehicles render as a camera-facing billboard sprite,
            // never a 3D mesh. This cube is kept invisible as the physics/collision proxy only.
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

            GameObject spriteChild = new GameObject("BillboardSprite");
            spriteChild.transform.SetParent(vehicle.transform);
            spriteChild.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            spriteChild.transform.localScale = new Vector3(1.4f, 1.8f, 1f);
            SpriteRenderer spriteRenderer = spriteChild.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite(new Color(1f, 0.15f, 0.05f), Color.black, 128, 64f);
            spriteRenderer.sortingOrder = 10;
            spriteChild.AddComponent<BillboardSprite>();

            return vehicle;
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
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
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

        static void SetupGameManager(Transform parent, GameObject vehicle, StageType initialStage)
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

            // --- 임시 디버그: H 키로 콜라이더(히트박스) 와이어프레임 토글 ---
            canvasObject.AddComponent<HitboxDebugToggle>();
        }
    }
}
