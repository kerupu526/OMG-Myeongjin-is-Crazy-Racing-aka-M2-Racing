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
            stageState.oxygenGauge = gauge;
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
            //
            // BUG (found on a later playtest — "왜 자꾸 조향 반전이 되는 거지? 난 피해갔는데?"):
            // widthRatio=0.8f/lateralOffsetRatio=0.25f (TrackWidth=16) put the zone's far edge
            // at offset 4.0+6.4=10.4, which is PAST the actual outer wall at 8.0 — the "far
            // side" everyone assumed was a dodge lane didn't exist; the zone butted right up
            // against (and overshot) the wall, so only the near/inner side had any clearance at
            // all. A player taking the natural wide racing line on the far side had zero room
            // and always clipped it. (The original 0.55f/0.25f version had the same flaw, just
            // a smaller 0.4m overshoot — not zero, but still not the "avoidable" gap the comment
            // above claimed.) Recomputed so the near edge sits 4.5m off the inner wall (a real,
            // comfortable lane for the 1.2m-wide car) and the far edge stops 0.5m short of the
            // outer wall (never overshoots) — width 11.0m, centered at offset 2.0.
            const float widthRatio = 11f / 16f;
            const float lateralOffsetRatio = 2f / 16f;

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

            // A tripod camera standing right at the accident zone itself — sells "this is an
            // active broadcast set, don't drive through it" better than a warning sign alone.
            // No Kenney pack here has a camera/tripod model, so this is a composed low-poly
            // prop built from primitives (same approach as LavaZone/GhastFireball's visuals).
            CreateBroadcastCameraProp(parent, geo, accidentTheta, lateralOffsetRatio);
        }

        static void CreateWarningSign(Transform parent, TrackGeometry geo, float theta, float lateralOffsetRatio)
        {
            // Right at the track edge on the danger side — visible without blocking the
            // drivable lane (the zones themselves already leave the other side clear).
            float edgeOffset = (lateralOffsetRatio >= 0f ? 1f : -1f) * geo.TrackWidth / 2f;
            Vector3 position = geo.OffsetPointAt(theta, edgeOffset);
            Vector3 tangent = geo.TangentAt(theta);

            GameObject sign = new GameObject("AccidentWarningSign");
            sign.transform.SetParent(parent);
            sign.transform.position = position;
            sign.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

            // Post: a thin cylinder standing up from the ground.
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(sign.transform);
            post.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            post.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);
            SafeDestroy(post.GetComponent<CapsuleCollider>());
            RendererColorUtil.ApplyColor(post.GetComponent<Renderer>(), new Color(0.25f, 0.25f, 0.25f));

            // Sign face: a cube rotated 45 degrees around the post's own forward axis (which
            // faces oncoming traffic, per the LookRotation above) so it reads as a diamond
            // road-warning plate instead of a plain flat rectangle — replaces the single flat
            // cube this used to be.
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "SignFace";
            face.transform.SetParent(sign.transform);
            face.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            face.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            face.transform.localScale = new Vector3(0.9f, 0.9f, 0.08f);
            SafeDestroy(face.GetComponent<BoxCollider>());
            RendererColorUtil.ApplyColor(face.GetComponent<Renderer>(), new Color(1f, 0.55f, 0f));
        }

        // A tripod-mounted camera (legs + body + lens + a small "on air" tally light) built
        // entirely from primitives — a composed low-poly prop stands in for a hand-modeled
        // Blockbench asset here, same tradeoff already made for LavaZone/GhastFireball.
        static void CreateBroadcastCameraProp(Transform parent, TrackGeometry geo, float theta, float lateralOffsetRatio)
        {
            float edgeOffset = (lateralOffsetRatio >= 0f ? 1f : -1f) * geo.TrackWidth / 2f;
            Vector3 position = geo.OffsetPointAt(theta, edgeOffset);
            Vector3 tangent = geo.TangentAt(theta);

            GameObject camera = new GameObject("BroadcastCameraProp");
            camera.transform.SetParent(parent);
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

            Color legColor = new Color(0.15f, 0.15f, 0.15f);
            Color bodyColor = new Color(0.2f, 0.2f, 0.22f);

            // Three legs splayed outward from ground-level feet up to a shared hub point,
            // like a real tripod.
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                Vector3 foot = new Vector3(Mathf.Cos(angle) * 0.4f, 0f, Mathf.Sin(angle) * 0.4f);
                Vector3 hub = new Vector3(0f, 1.05f, 0f);
                CreateCylinderBetween(camera.transform, $"TripodLeg_{i}", foot, hub, 0.045f, legColor);
            }

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CameraBody";
            body.transform.SetParent(camera.transform);
            body.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            body.transform.localScale = new Vector3(0.4f, 0.3f, 0.6f);
            SafeDestroy(body.GetComponent<BoxCollider>());
            RendererColorUtil.ApplyColor(body.GetComponent<Renderer>(), bodyColor);

            // Lens points toward the track, aimed at passing racers.
            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lens.name = "Lens";
            lens.transform.SetParent(camera.transform);
            lens.transform.localPosition = new Vector3(0f, 1.15f, 0.42f);
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lens.transform.localScale = new Vector3(0.16f, 0.12f, 0.16f);
            SafeDestroy(lens.GetComponent<CapsuleCollider>());
            RendererColorUtil.ApplyColor(lens.GetComponent<Renderer>(), Color.black);

            // Small red "on air" tally light on top.
            GameObject tally = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tally.name = "TallyLight";
            tally.transform.SetParent(camera.transform);
            tally.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            tally.transform.localScale = Vector3.one * 0.12f;
            SafeDestroy(tally.GetComponent<SphereCollider>());
            RendererColorUtil.ApplyEmissiveColor(tally.GetComponent<Renderer>(), Color.red, Color.red * 2f);
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
            // Glowing (emissive) so it reads as hot rather than just a colored rectangle.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "LavaVisual";
            visual.transform.SetParent(lava.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(geo.TrackWidth * sizeRatio, 0.05f, geo.TrackWidth * sizeRatio);
            // SafeDestroy handles both editor-time builds and runtime hot-swaps.
            SafeDestroy(visual.GetComponent<BoxCollider>());
            RendererColorUtil.ApplyEmissiveColor(visual.GetComponent<Renderer>(),
                new Color(0.9f, 0.25f, 0f), new Color(1.4f, 0.35f, 0f));

            // A ring of jagged obsidian rocks framing the pool — composed from primitives
            // (no Kenney lava/obsidian model available) so the hazard reads as a volcanic
            // vent rather than a flat colored rectangle. Angle-derived, not Random, so a
            // rebuild always produces the same layout.
            float halfSize = geo.TrackWidth * sizeRatio * 0.5f;
            const int rockCount = 8;
            for (int i = 0; i < rockCount; i++)
            {
                float angle = (i / (float)rockCount) * 360f;
                float rad = angle * Mathf.Deg2Rad;
                float radiusJitter = 0.9f + 0.2f * Mathf.Sin(i * 2.7f);
                float heightJitter = 0.35f + 0.25f * Mathf.Abs(Mathf.Cos(i * 1.9f));

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = $"LavaRock_{i}";
                rock.transform.SetParent(lava.transform);
                rock.transform.localPosition = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * halfSize * radiusJitter
                    + Vector3.down * 0.4f;
                rock.transform.localRotation = Quaternion.Euler(angle * 0.3f, angle * 2.1f, angle * 0.7f);
                rock.transform.localScale = new Vector3(0.5f, heightJitter, 0.5f);
                SafeDestroy(rock.GetComponent<BoxCollider>());
                RendererColorUtil.ApplyColor(rock.GetComponent<Renderer>(), new Color(0.12f, 0.08f, 0.08f));
            }

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

            // A few reed-like stalks along the long edges (composed from thin cylinders — no
            // Kenney reed/palm model available in the castle-kit pack this stage uses) so the
            // cooling lane reads as an oasis strip rather than a flat blue rectangle.
            float groundLocalY = -geo.WallHeight / 2f;
            float halfWidth = geo.TrackWidth * widthRatio * 0.5f;
            float halfLength = geo.TrackWidth * depthRatio * 0.5f;
            const int reedCount = 6;
            for (int i = 0; i < reedCount; i++)
            {
                float t = (i / (float)(reedCount - 1)) - 0.5f;
                float side = (i % 2 == 0) ? 1f : -1f;
                Vector3 foot = new Vector3(side * halfWidth * 0.9f, groundLocalY, t * halfLength * 1.8f);
                Vector3 tip = foot + new Vector3(side * 0.15f, 0.9f + 0.2f * Mathf.Abs(Mathf.Sin(i)), 0.1f);
                CreateCylinderBetween(oasis.transform, $"Reed_{i}", foot, tip, 0.05f, new Color(0.25f, 0.55f, 0.25f));
            }

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
            // model, so a composed low-poly stand-in (glowing core + radiating spikes) reads
            // as a fireball rather than leaving the hazard completely unmarked or looking like
            // a plain sphere.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "GhastFireballVisual";
            visual.transform.SetParent(fireball.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 1.2f;
            // SafeDestroy handles both editor-time builds and runtime hot-swaps.
            SafeDestroy(visual.GetComponent<SphereCollider>());
            RendererColorUtil.ApplyEmissiveColor(visual.GetComponent<Renderer>(),
                new Color(1f, 0.45f, 0.1f), new Color(1.2f, 0.35f, 0f));

            Vector3[] spikeDirections =
            {
                Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                new Vector3(1f, 1f, 1f).normalized, new Vector3(-1f, 1f, -1f).normalized,
            };
            foreach (Vector3 dir in spikeDirections)
            {
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spike.name = "FireSpike";
                spike.transform.SetParent(fireball.transform);
                spike.transform.localPosition = dir * 0.6f;
                spike.transform.localScale = Vector3.one * 0.45f;
                SafeDestroy(spike.GetComponent<SphereCollider>());
                RendererColorUtil.ApplyEmissiveColor(spike.GetComponent<Renderer>(),
                    new Color(1f, 0.75f, 0.15f), new Color(1.6f, 0.9f, 0.1f));
            }

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

        // Builds a cylinder primitive stretched and oriented to span two local points —
        // used for tripod legs / reed stalks, anything that reads better as "a strut between
        // two points" than a shape dropped at a single position+rotation.
        static GameObject CreateCylinderBetween(Transform parent, string name, Vector3 localA, Vector3 localB, float radius, Color color)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent);

            Vector3 delta = localB - localA;
            float length = delta.magnitude;
            cylinder.transform.localPosition = (localA + localB) * 0.5f;
            cylinder.transform.localRotation = length > 0.0001f
                ? Quaternion.FromToRotation(Vector3.up, delta.normalized)
                : Quaternion.identity;
            // Unit cylinder mesh is 2 units tall (radius 0.5), so half-length maps directly to
            // the Y scale and radius maps directly to X/Z scale.
            cylinder.transform.localScale = new Vector3(radius * 2f, length / 2f, radius * 2f);

            SafeDestroy(cylinder.GetComponent<CapsuleCollider>());
            RendererColorUtil.ApplyColor(cylinder.GetComponent<Renderer>(), color);
            return cylinder;
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
