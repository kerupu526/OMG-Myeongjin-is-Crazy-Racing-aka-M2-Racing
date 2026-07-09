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

                // Terrain features are real 3D meshes per CLAUDE.md's 2.5D rule (only
                // characters/vehicles/items are billboard sprites), so this stays visible.
                RendererColorUtil.ApplyColor(hazard.GetComponent<Renderer>(), new Color(0.45f, 0.3f, 0.15f));

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

            CreateZoneTrigger(parent, "BroadcastAccidentZone", geo, accidentTheta, depth: 3f)
                .AddComponent<BroadcastAccidentZone>();

            CreateZoneTrigger(parent, "AccidentWarningZone", geo, warningTheta, depth: 1f)
                .AddComponent<AccidentWarningZone>();
        }

        // ---------------- Nether Fortress ----------------

        static BuiltStage AttachNetherFortress(Transform worldParent, Transform trackCenter, GameObject vehicle, Canvas canvas, RaceFlowUI flowUI, TrackGeometry geo)
        {
            GameObject worldRoot = new GameObject("NetherFortress_World");
            worldRoot.transform.SetParent(worldParent);
            LavaZone lavaZone = CreateLavaZone(worldRoot.transform, geo);
            CreateGhastFireball(worldRoot.transform, trackCenter, geo);

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
            Vector3 position = geo.OffsetPointAt(theta, geo.TrackWidth * 0.2f);

            GameObject lava = new GameObject("LavaZone");
            lava.transform.SetParent(parent);
            lava.transform.position = position + Vector3.up * 0.5f;

            BoxCollider box = lava.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(geo.TrackWidth * 0.6f, 2f, geo.TrackWidth * 0.6f);

            // Visual placeholder so the lava patch actually reads on the track — a flat
            // tinted slab, terrain (3D mesh) rather than a billboard sprite per the 2.5D rule.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "LavaVisual";
            visual.transform.SetParent(lava.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(geo.TrackWidth * 0.6f, 0.05f, geo.TrackWidth * 0.6f);
            Object.Destroy(visual.GetComponent<BoxCollider>()); // the parent's trigger collider already covers this
            RendererColorUtil.ApplyColor(visual.GetComponent<Renderer>(), new Color(0.9f, 0.25f, 0f));

            return lava.AddComponent<LavaZone>();
        }

        static void CreateGhastFireball(Transform parent, Transform trackCenter, TrackGeometry geo)
        {
            const float theta = 0.35f * Mathf.PI * 2f + 0.1f * Mathf.PI * 2f;
            Vector3 position = geo.PointAt(theta);

            GameObject fireball = new GameObject("GhastFireball");
            fireball.transform.SetParent(parent);
            fireball.transform.position = position + Vector3.up * 1f;

            SphereCollider collider = fireball.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.5f;

            GhastFireball ghast = fireball.AddComponent<GhastFireball>();
            ghast.trackCenter = trackCenter;
        }

        // ---------------- Shared helpers ----------------

        static GameObject CreateZoneTrigger(Transform parent, string name, TrackGeometry geo, float theta, float depth)
        {
            Vector3 position = geo.PointAt(theta);
            Vector3 tangent = geo.TangentAt(theta);

            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent);
            zone.transform.position = position + Vector3.up * (geo.WallHeight / 2f);
            zone.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(geo.TrackWidth, geo.WallHeight * 2f, depth);

            return zone;
        }
    }
}
