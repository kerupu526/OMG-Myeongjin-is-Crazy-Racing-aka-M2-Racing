using M2.Core;
using M2.Player;
using M2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace M2.Stage
{
    // Attaches/detaches a stage's hazards + gauge/state + UI onto an already-built test
    // track and vehicle. Runtime-safe (no UnityEditor dependency) so both TestTrackBuilder
    // (editor build time) and StageTestSelector (in-Play hotkey switching) go through this
    // single place instead of duplicating the wiring.
    public class BuiltStage
    {
        public StageType Type;
        public GameObject WorldRoot; // this stage's hazards/zones in the 3D scene
        public GameObject UiRoot;    // this stage's UI panels
        public Component StageState; // BikiniCityStageState / AfricaTvStageState / NetherFortressStageState
    }

    public static class StageAssembler
    {
        const int OxygenBubbleSpawnCount = 4;
        const int TerrainHazardCount = 3;

        public static BuiltStage Attach(StageType type, Transform worldParent, Transform trackCenter,
            GameObject vehicle, Canvas canvas, RaceFlowUI flowUI, TrackGeometry geometry)
        {
            return type switch
            {
                StageType.BikiniCity => AttachBikiniCity(worldParent, vehicle, canvas, flowUI, geometry),
                StageType.AfricaTv => AttachAfricaTv(worldParent, vehicle, canvas, flowUI, geometry),
                StageType.NetherFortress => AttachNetherFortress(worldParent, trackCenter, vehicle, canvas, flowUI, geometry),
                _ => null,
            };
        }

        public static void Detach(BuiltStage built, GameObject vehicle, RaceFlowUI flowUI)
        {
            if (built == null) return;

            if (built.WorldRoot != null) Object.Destroy(built.WorldRoot);
            if (built.UiRoot != null) Object.Destroy(built.UiRoot);
            if (built.StageState != null) Object.Destroy(built.StageState);

            switch (built.Type)
            {
                case StageType.BikiniCity:
                    RemoveIfPresent<BikiniCityOxygenGauge>(vehicle);
                    flowUI.bikiniCityStageState = null;
                    break;
                case StageType.AfricaTv:
                    RemoveIfPresent<AfricaTvMentalGauge>(vehicle);
                    flowUI.africaTvStageState = null;
                    break;
                case StageType.NetherFortress:
                    RemoveIfPresent<NetherFortressTemperatureGauge>(vehicle);
                    flowUI.netherFortressStageState = null;
                    break;
            }
        }

        static void RemoveIfPresent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component != null) Object.Destroy(component);
        }

        // ---------------- Bikini City ----------------

        static BuiltStage AttachBikiniCity(Transform worldParent, GameObject vehicle, Canvas canvas, RaceFlowUI flowUI, TrackGeometry geo)
        {
            GameObject worldRoot = new GameObject("BikiniCity_World");
            worldRoot.transform.SetParent(worldParent);

            Transform bubbleRoot = new GameObject("OxygenBubbleSpawners").transform;
            bubbleRoot.SetParent(worldRoot.transform);
            CreateOxygenBubbleSpawners(bubbleRoot, geo);

            Transform hazardRoot = new GameObject("TerrainHazards").transform;
            hazardRoot.SetParent(worldRoot.transform);
            CreateTerrainHazards(hazardRoot, geo);

            BikiniCityOxygenGauge gauge = vehicle.AddComponent<BikiniCityOxygenGauge>();
            BikiniCityStageState stageState = vehicle.AddComponent<BikiniCityStageState>();
            flowUI.bikiniCityStageState = stageState;

            GameObject uiRoot = new GameObject("BikiniCity_UI");
            uiRoot.transform.SetParent(canvas.transform, false);

            Text oxygenLabel = SimpleUIFactory.CreateCornerText(uiRoot.transform, "OxygenLabel",
                new Vector2(1f, 1f), new Vector2(-20f, -20f), TextAnchor.UpperRight);

            GameObject warningPanelObj = SimpleUIFactory.CreateFullscreenPanel(uiRoot.transform, "OxygenWarningOverlay",
                new Color(1f, 0f, 0f, 0.35f));

            GameObject gameOverPanelObj = SimpleUIFactory.CreateFullscreenPanel(uiRoot.transform, "OxygenGameOverPanel",
                new Color(0f, 0f, 0f, 0.85f));
            Text gameOverText = SimpleUIFactory.CreateCenteredText(gameOverPanelObj.transform, "OxygenGameOverText",
                48, Color.red);

            warningPanelObj.SetActive(false);
            gameOverPanelObj.SetActive(false);

            BikiniCityStageUI stageUI = uiRoot.AddComponent<BikiniCityStageUI>();
            stageUI.oxygenGauge = gauge;
            stageUI.vehicleController = vehicle.GetComponent<VehicleController>();
            stageUI.oxygenLabel = oxygenLabel;
            stageUI.warningOverlay = warningPanelObj.GetComponent<Image>();
            stageUI.gameOverPanel = gameOverPanelObj;
            stageUI.gameOverText = gameOverText;

            return new BuiltStage { Type = StageType.BikiniCity, WorldRoot = worldRoot, UiRoot = uiRoot, StageState = stageState };
        }

        static void CreateOxygenBubbleSpawners(Transform parent, TrackGeometry geo)
        {
            for (int i = 0; i < OxygenBubbleSpawnCount; i++)
            {
                // Offset from checkpoints/items so bubbles don't stack on top of either.
                float theta = (i + 0.25f) * Mathf.PI * 2f / OxygenBubbleSpawnCount;
                Vector3 position = geo.PointAt(theta);

                GameObject spawner = new GameObject($"OxygenBubbleSpawner_{i}");
                spawner.transform.SetParent(parent);
                spawner.transform.position = position;
                spawner.AddComponent<OxygenBubbleSpawner>();
            }
        }

        static void CreateTerrainHazards(Transform parent, TrackGeometry geo)
        {
            for (int i = 0; i < TerrainHazardCount; i++)
            {
                // Offset again, pulled to one side of the track band (via the local normal,
                // not a from-origin approximation — the centerline isn't a plain ellipse
                // anymore) so hazards are avoidable, not a guaranteed hit.
                float theta = (i + 0.75f) * Mathf.PI * 2f / TerrainHazardCount;
                Vector3 position = geo.OffsetPointAt(theta, geo.TrackWidth * 0.25f);

                GameObject hazard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hazard.name = $"TerrainHazard_{i}";
                hazard.transform.SetParent(parent);
                hazard.transform.position = position + Vector3.up * 0.5f;
                hazard.transform.localScale = new Vector3(1.5f, 1f, 1.5f);

                // The cube stays as the invisible collision proxy (TerrainHazard needs
                // OnCollisionEnter, i.e. a solid non-trigger collider sized like this box) — a
                // Kenney rock model renders the actual visual, same collider+visual-child split
                // used for the vehicle in TestTrackBuilder.CreateVehicleModel.
                // Using SafeDestroy (which checks Application.isPlaying) ensures that during
                // runtime hot-swaps, we call Destroy() to safely queue the destruction, whereas
                // during editor time we call DestroyImmediate().
                SafeDestroy(hazard.GetComponent<MeshRenderer>());
                SafeDestroy(hazard.GetComponent<MeshFilter>());
                // 4x is a first guess, not measured — nudge further if it still reads too
                // small/large once you see it in Play mode.
                AttachVisualModel(hazard.transform, "KenneyProps/rock-sand-b", "KenneyProps/survivalkit_colormap", modelScale: 4f);

                hazard.AddComponent<TerrainHazard>();
            }
        }

        // ---------------- Africa TV ----------------

        static BuiltStage AttachAfricaTv(Transform worldParent, GameObject vehicle, Canvas canvas, RaceFlowUI flowUI, TrackGeometry geo)
        {
            GameObject worldRoot = new GameObject("AfricaTv_World");
            worldRoot.transform.SetParent(worldParent);
            CreateBroadcastAccidentZone(worldRoot.transform, geo);

            AfricaTvMentalGauge gauge = vehicle.AddComponent<AfricaTvMentalGauge>();
            AfricaTvStageState stageState = vehicle.AddComponent<AfricaTvStageState>();
            stageState.mentalGauge = gauge;
            flowUI.africaTvStageState = stageState;

            GameObject uiRoot = new GameObject("AfricaTv_UI");
            uiRoot.transform.SetParent(canvas.transform, false);

            Text mentalLabel = SimpleUIFactory.CreateCornerText(uiRoot.transform, "MentalLabel",
                new Vector2(1f, 1f), new Vector2(-20f, -20f), TextAnchor.UpperRight);

            GameObject lockoutPanelObj = SimpleUIFactory.CreateFullscreenPanel(uiRoot.transform, "MentalLockoutPanel",
                new Color(1f, 0f, 1f, 0.35f));
            Text lockoutText = SimpleUIFactory.CreateCenteredText(lockoutPanelObj.transform, "MentalLockoutText",
                40, Color.white);

            GameObject warningPanelObj = SimpleUIFactory.CreateFullscreenPanel(uiRoot.transform, "AccidentWarningPanel",
                new Color(1f, 0.6f, 0f, 0.35f));
            Text warningText = SimpleUIFactory.CreateCenteredText(warningPanelObj.transform, "AccidentWarningText",
                36, Color.white);

            lockoutPanelObj.SetActive(false);
            warningPanelObj.SetActive(false);

            AfricaTvStageUI stageUI = uiRoot.AddComponent<AfricaTvStageUI>();
            stageUI.mentalGauge = gauge;
            stageUI.mentalLabel = mentalLabel;
            stageUI.lockoutPanel = lockoutPanelObj;
            stageUI.lockoutText = lockoutText;
            stageUI.warningPanel = warningPanelObj;
            stageUI.warningText = warningText;

            return new BuiltStage { Type = StageType.AfricaTv, WorldRoot = worldRoot, UiRoot = uiRoot, StageState = stageState };
        }

        static void CreateBroadcastAccidentZone(Transform parent, TrackGeometry geo)
        {
            // Placed at a fixed spot around the oval, offset from checkpoints/items. Travel
            // direction follows increasing theta (matches the vehicle's spawn heading in
            // TestTrackBuilder), so the warning zone sits at a slightly smaller theta —
            // i.e. just "before" the accident zone along the track.
            const float accidentTheta = 0.6f * Mathf.PI * 2f;
            const float warningTheta = accidentTheta - 0.06f * Mathf.PI * 2f;
            // Narrowed + pushed to one side instead of spanning the full track width — a
            // player who reacts to the warning can dodge past on the clear side instead of
            // being guaranteed to clip it (playtester feedback: "무조건 닿는 게 아닐 수 있도록").
            // 0.55 read as too small on a second pass (playtester: "크기만 좀 키워줘") — widened
            // back up to 0.8 while keeping the same lateral offset, so there's still a clear
            // ~5.6m lane on the far side to dodge through.
            const float widthRatio = 0.8f;
            const float lateralOffsetRatio = 0.25f;

            GameObject accidentZone = CreateZoneTrigger(parent, "BroadcastAccidentZone", geo, accidentTheta, depth: 3f,
                widthRatio: widthRatio, lateralOffsetRatio: lateralOffsetRatio);
            accidentZone.AddComponent<BroadcastAccidentZone>();
            AddFlatGroundTint(accidentZone.transform, geo, geo.TrackWidth * widthRatio, 3f, new Color(0.55f, 0.1f, 0.55f));

            GameObject warningZone = CreateZoneTrigger(parent, "AccidentWarningZone", geo, warningTheta, depth: 1f,
                widthRatio: widthRatio, lateralOffsetRatio: lateralOffsetRatio);
            warningZone.AddComponent<AccidentWarningZone>();

            // A physical world-space sign at the track edge, standing on the same side as the
            // upcoming accident zone, a little before the warning trigger — the only warning
            // used to be a screen-space flash on ENTERING the zone, which read as "it just
            // reverses on you out of nowhere" the first time through (playtester feedback:
            // "표식이나 그런 게 없으니까 그냥 반전되는 느낌").
            CreateWarningSign(parent, geo, warningTheta - 0.03f * Mathf.PI * 2f, lateralOffsetRatio);
        }

        static void CreateWarningSign(Transform parent, TrackGeometry geo, float theta, float lateralOffsetRatio)
        {
            // Right at the track edge on the danger side — visible without blocking the
            // drivable lane (the zones themselves already leave the other side clear).
            float edgeOffset = (lateralOffsetRatio >= 0f ? 1f : -1f) * geo.TrackWidth / 2f;
            Vector3 position = geo.OffsetPointAt(theta, edgeOffset);
            Vector3 tangent = geo.TangentAt(theta);

            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "AccidentWarningSign";
            sign.transform.SetParent(parent);
            sign.transform.position = position + Vector3.up * 1f;
            sign.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            sign.transform.localScale = new Vector3(1.5f, 2f, 0.2f);
            SafeDestroy(sign.GetComponent<BoxCollider>());
            RendererColorUtil.ApplyColor(sign.GetComponent<Renderer>(), new Color(1f, 0.55f, 0f));
        }

        // Flat tinted slab matching a zone trigger's footprint exactly (same parent transform,
        // so it inherits the zone's position/rotation) — makes an otherwise-invisible trigger
        // box actually read as a hazard patch on the road, same "flat primitive" approach as
        // LavaZone/OasisZone's visuals.
        static void AddFlatGroundTint(Transform parent, TrackGeometry geo, float width, float depth, Color color)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "GroundTint";
            visual.transform.SetParent(parent);
            // parent (the zone) sits at y = WallHeight/2 — bring the tint back down near the
            // ground (matching the small +0.01~0.02 offsets TestTrackBuilder uses elsewhere to
            // avoid z-fighting with the track surface).
            visual.transform.localPosition = new Vector3(0f, 0.02f - geo.WallHeight / 2f, 0f);
            visual.transform.localScale = new Vector3(width, 0.05f, depth);
            SafeDestroy(visual.GetComponent<BoxCollider>());
            RendererColorUtil.ApplyColor(visual.GetComponent<Renderer>(), color);
        }

        // ---------------- Nether Fortress ----------------

        static BuiltStage AttachNetherFortress(Transform worldParent, Transform trackCenter, GameObject vehicle, Canvas canvas, RaceFlowUI flowUI, TrackGeometry geo)
        {
            GameObject worldRoot = new GameObject("NetherFortress_World");
            worldRoot.transform.SetParent(worldParent);
            LavaZone lavaZone = CreateLavaZone(worldRoot.transform, geo);
            CreateGhastFireball(worldRoot.transform, trackCenter, geo);
            CreateOasisZone(worldRoot.transform, geo);

            NetherFortressTemperatureGauge gauge = vehicle.AddComponent<NetherFortressTemperatureGauge>();
            NetherFortressStageState stageState = vehicle.AddComponent<NetherFortressStageState>();
            stageState.temperatureGauge = gauge;
            stageState.lavaZone = lavaZone;
            flowUI.netherFortressStageState = stageState;

            GameObject uiRoot = new GameObject("NetherFortress_UI");
            uiRoot.transform.SetParent(canvas.transform, false);

            Text temperatureLabel = SimpleUIFactory.CreateCornerText(uiRoot.transform, "TemperatureLabel",
                new Vector2(1f, 1f), new Vector2(-20f, -20f), TextAnchor.UpperRight);

            GameObject warningFlashObj = SimpleUIFactory.CreateFullscreenPanel(uiRoot.transform, "BurnWarningFlash",
                new Color(1f, 0.3f, 0f, 0.4f));

            GameObject gameOverPanelObj = SimpleUIFactory.CreateFullscreenPanel(uiRoot.transform, "BurnGameOverPanel",
                new Color(0.3f, 0f, 0f, 0.85f));
            Text gameOverText = SimpleUIFactory.CreateCenteredText(gameOverPanelObj.transform, "BurnGameOverText",
                48, Color.red);

            warningFlashObj.SetActive(false);
            gameOverPanelObj.SetActive(false);

            NetherFortressStageUI stageUI = uiRoot.AddComponent<NetherFortressStageUI>();
            stageUI.temperatureGauge = gauge;
            stageUI.vehicleController = vehicle.GetComponent<VehicleController>();
            stageUI.temperatureLabel = temperatureLabel;
            stageUI.warningFlash = warningFlashObj.GetComponent<Image>();
            stageUI.gameOverPanel = gameOverPanelObj;
            stageUI.gameOverText = gameOverText;

            return new BuiltStage { Type = StageType.NetherFortress, WorldRoot = worldRoot, UiRoot = uiRoot, StageState = stageState };
        }

        static LavaZone CreateLavaZone(Transform parent, TrackGeometry geo)
        {
            const float theta = 0.35f * Mathf.PI * 2f;
            // Was 0.6*TrackWidth square offset only 0.2*TrackWidth from center — on Nether's
            // narrower track that covered most of the road with barely a sliver to dodge
            // through. Shrunk + pushed further toward one edge so roughly half the track
            // width stays clear on the other side (playtester feedback: "피하기 어려워").
            const float sizeRatio = 0.3f;
            const float offsetRatio = 0.32f;
            Vector3 position = geo.OffsetPointAt(theta, geo.TrackWidth * offsetRatio);

            GameObject lava = new GameObject("LavaZone");
            lava.transform.SetParent(parent);
            lava.transform.position = position + Vector3.up * 0.5f;

            BoxCollider box = lava.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(geo.TrackWidth * sizeRatio, 2f, geo.TrackWidth * sizeRatio);

            // Visual placeholder so the lava patch actually reads on the track — a flat
            // tinted slab, terrain (3D mesh) rather than a billboard sprite per the 2.5D rule.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "LavaVisual";
            visual.transform.SetParent(lava.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(geo.TrackWidth * sizeRatio, 0.05f, geo.TrackWidth * sizeRatio);
            // SafeDestroy handles both editor-time builds and runtime hot-swaps.
            SafeDestroy(visual.GetComponent<BoxCollider>());
            RendererColorUtil.ApplyColor(visual.GetComponent<Renderer>(), new Color(0.9f, 0.25f, 0f));

            return lava.AddComponent<LavaZone>();
        }

        static void CreateOasisZone(Transform parent, TrackGeometry geo)
        {
            // Opposite side of the lap from the lava zone (theta 0.35) / ghast fireball
            // (theta 0.45) so cooling off doesn't sit right on top of a heat hazard.
            const float theta = 0.75f * Mathf.PI * 2f;
            // Narrow across the track (must swerve in on purpose, not something you clip by
            // accident) but long along it (real time to cool if you commit to hugging this
            // lane) — playtester ask: "세로로 길되 가로로 짧은 열을 식힐 수 있는 공간".
            // depthRatio is relative to TrackWidth too (not a fixed meters value) so it scales
            // down along with the width on a narrower stage instead of looking oversized.
            const float widthRatio = 0.35f;
            const float depthRatio = 1.4f;
            const float offsetRatio = 0.3f;

            Vector3 position = geo.OffsetPointAt(theta, geo.TrackWidth * offsetRatio);
            Vector3 tangent = geo.TangentAt(theta);

            GameObject oasis = new GameObject("OasisZone");
            oasis.transform.SetParent(parent);
            oasis.transform.position = position + Vector3.up * (geo.WallHeight / 2f);
            oasis.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

            BoxCollider box = oasis.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(geo.TrackWidth * widthRatio, geo.WallHeight * 2f, geo.TrackWidth * depthRatio);

            // Flat cool-blue slab so the cooling lane actually reads against the asphalt/lava,
            // terrain (3D mesh) rather than a billboard sprite per the 2.5D rule.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "OasisVisual";
            visual.transform.SetParent(oasis.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(geo.TrackWidth * widthRatio, 0.05f, geo.TrackWidth * depthRatio);
            SafeDestroy(visual.GetComponent<BoxCollider>());
            RendererColorUtil.ApplyColor(visual.GetComponent<Renderer>(), new Color(0.2f, 0.6f, 0.85f));

            oasis.AddComponent<OasisZone>();
        }

        static void CreateGhastFireball(Transform parent, Transform trackCenter, TrackGeometry geo)
        {
            const float theta = 0.35f * Mathf.PI * 2f + 0.1f * Mathf.PI * 2f;
            // Offset to the side of the track (like the terrain hazards / lava zone) instead of
            // dead-center on the racing line where the car couldn't help hitting it every lap —
            // that centerline placement is why Nether felt like it randomly grabbed you mid-road.
            Vector3 position = geo.OffsetPointAt(theta, geo.TrackWidth * 0.28f);

            GameObject fireball = new GameObject("GhastFireball");
            fireball.transform.SetParent(parent);
            fireball.transform.position = position + Vector3.up * 1f;

            SphereCollider collider = fireball.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            // 0.8 turned out to overcorrect: the fireball sits 1m up while the vehicle's
            // collider is centered at 0.5m, so with only 0.5m of vertical overlap left, a
            // sphere of radius 0.8 only reaches ~0.6m sideways from the exact centerline —
            // basically un-hittable while actually driving. 1.2 gives ~1.1m of horizontal
            // reach at the vehicle's height, closer without going back to the original
            // "knocked from a mile away" 1.5.
            collider.radius = 1.2f;

            // Was a bare invisible trigger before — no Kenney pack here has a fireball/ghast
            // model, so a simple tinted sphere stands in (same "flat tinted primitive" approach
            // as LavaZone's visual) rather than leaving the hazard completely unmarked.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "GhastFireballVisual";
            visual.transform.SetParent(fireball.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 1.2f;
            // SafeDestroy handles both editor-time builds and runtime hot-swaps.
            SafeDestroy(visual.GetComponent<SphereCollider>());
            RendererColorUtil.ApplyColor(visual.GetComponent<Renderer>(), new Color(1f, 0.45f, 0.1f));

            GhastFireball ghast = fireball.AddComponent<GhastFireball>();
            ghast.trackCenter = trackCenter;
        }

        // Safe helper to destroy objects that works both during editor-time script runs
        // (where Destroy() is ignored) and during active Play mode (where DestroyImmediate() is dangerous).
        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;

            // If destroying a Collider, immediately disable it. During runtime hot-swaps, Object.Destroy
            // is deferred to the end of the frame, which would leave a solid collider (like LavaVisual's box)
            // physically active for 1 frame, causing the car to crash into an "invisible wall" instantly.
            if (obj is Collider col)
            {
                col.enabled = false;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        // ---------------- Shared helpers ----------------

        // Instantiates a Kenney model (from Resources, so this stays usable both at editor
        // build-time and during in-Play stage hot-swapping) as a child visual, counter-scaling
        // it against the parent's (possibly non-uniform) scale so it renders at its own authored
        // proportions instead of being squashed/stretched by a collider proxy's box shape — same
        // split used for the vehicle in TestTrackBuilder.CreateVehicleModel.
        static void AttachVisualModel(Transform parent, string resourcePath, string textureResourcePath = null, float modelScale = 1f)
        {
            GameObject source = Resources.Load<GameObject>(resourcePath);
            if (source == null)
            {
                Debug.LogWarning($"M2: hazard visual model not found at Resources/{resourcePath}.");
                return;
            }

            GameObject model = Object.Instantiate(source, parent);
            model.name = "Visual";

            // modelScale is an extra multiplier on top of the parent-scale cancellation below —
            // kenney_survival-kit's props (rock-sand-b included) are authored noticeably smaller
            // than kenney_car-kit/racing-kit's ~1-unit-per-meter convention, so they read as tiny
            // next to the vehicle/track without a boost here.
            Vector3 proxyScale = parent.localScale;
            model.transform.localScale = new Vector3(
                modelScale / proxyScale.x, modelScale / proxyScale.y, modelScale / proxyScale.z);
            model.transform.localPosition = new Vector3(0f, -0.5f / proxyScale.y, 0f);

            if (textureResourcePath == null) return;

            Texture2D texture = Resources.Load<Texture2D>(textureResourcePath);
            if (texture == null) return;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                RendererColorUtil.ApplyTexture(renderer, texture, Vector2.one);
            }
        }

        // widthRatio/lateralOffsetRatio default to the original full-width/centered behavior —
        // pass narrower/offset values (as CreateBroadcastAccidentZone now does) to leave part
        // of the track clear so a zone is dodgeable instead of guaranteed to hit.
        static GameObject CreateZoneTrigger(Transform parent, string name, TrackGeometry geo, float theta, float depth,
            float widthRatio = 1f, float lateralOffsetRatio = 0f)
        {
            Vector3 position = geo.OffsetPointAt(theta, geo.TrackWidth * lateralOffsetRatio);
            Vector3 tangent = geo.TangentAt(theta);

            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent);
            zone.transform.position = position + Vector3.up * (geo.WallHeight / 2f);
            zone.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(geo.TrackWidth * widthRatio, geo.WallHeight * 2f, depth);

            return zone;
        }
    }
}
