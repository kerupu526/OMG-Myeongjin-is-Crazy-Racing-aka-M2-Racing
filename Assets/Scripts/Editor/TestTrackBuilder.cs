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
        // Used only by BuildAndSavePersistedScene — "(Generated)"/"TestTrack" branding is
        // misleading for a hierarchy meant to live permanently in its own saved scene.
        const string PersistedBikiniCityRootName = "BikiniCity";
        const string PersistedBikiniCityScenePath = "Assets/Scenes/Stage_BikiniCity.unity";
        const string PersistedAfricaTvRootName = "AfricaTv";
        const string PersistedAfricaTvScenePath = "Assets/Scenes/Stage_AfricaTV.unity";
        const string PersistedNetherFortressRootName = "NetherFortress";
        const string PersistedNetherFortressScenePath = "Assets/Scenes/Stage_NetherFortress.unity";

        // Hand-authored corner layout (replaces the old polar-wobble oval) — designed for
        // 2-player overtaking: two straights long enough to draft on, each feeding into a
        // genuine passing zone (a heavy-braking hairpin, and a chicane right before it), plus
        // a wide sweeper where two cars can run side by side without contact. Catmull-Rom
        // smooths between these points into flowing curves exactly like the old formula did —
        // everything downstream (walls, checkpoints, item/hazard placement) only ever calls
        // Geometry.PointAt/TangentAt/NormalAt and doesn't know the shape is hand-placed now.
        //
        // Verified via a throwaway Node.js port of this exact Catmull-Rom + SafeLateralOffset
        // math before committing these numbers: 0 wall self-intersections (checked at both the
        // in-game 64-segment resolution and a stricter 128-segment pass), minimum curvature
        // radius 9.3m (above TrackWidth/2=8m, so the track never has to narrow — full 16m width
        // everywhere), ~306m lap length. Uneven spacing between neighboring points can make
        // Catmull-Rom overshoot into tighter-than-intended curvature (this bit once already,
        // see the removed "closing" point that used to sit here) — if these points are ever
        // hand-tweaked again, re-verify rather than assuming a small nudge is safe.
        // Applied uniformly to every stage's control points below. Playtester feedback after
        // the per-stage-width tuning pass: "맵 길이가 전체적으로 짧은 거 같기도... 아주 조금만
        // 넓히자" (AfricaTV's ~444m lap took only ~1 minute chaining gasoline boosts) — first
        // bumped to 1.2x. Still felt short (네더요새 특히), so raised again to 1.5x with an
        // explicit target this time: a fast/boosted 3-lap AfricaTV run should take about 1:30.
        // At the ~22.2 m/s sustained boosted speed the original 1-minute report implies, 1.5x
        // puts AfricaTV's 3-lap time at ~90s (444m * 1.5 * 3 / 22.2 ≈ 90.1s) — confirmed via the
        // same Node.js port used for self-intersection checks. A uniform scale preserves every
        // already-verified property exactly (self-intersection-free, curvature-to-TrackWidth
        // ratio) since neither depends on absolute scale — re-verified anyway before committing
        // this (0 self-intersections on all 3 stages at the new size).
        const float TrackLengthScale = 1.5f;

        static Vector3[] Scale(Vector3[] points, float factor)
        {
            var scaled = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) scaled[i] = points[i] * factor;
            return scaled;
        }

        static readonly Vector3[] BikiniCityControlPoints = Scale(new[]
        {
            new Vector3(-48f, 0f, -28f), // 0: front straight, start/finish area
            new Vector3(-25f, 0f, -32f), // 1: chicane kick-out
            new Vector3(-6f, 0f, -23f),  // 2: chicane kick-back-in
            new Vector3(14f, 0f, -28f),  // 3: front straight resumes
            new Vector3(34f, 0f, -26f),  // 4: braking zone into the hairpin
            new Vector3(50f, 0f, -18f),  // 5: hairpin entry curl
            new Vector3(60f, 0f, -2f),   // 6: hairpin far side
            new Vector3(60f, 0f, 14f),   // 7: hairpin apex (wide loop, not a cusp)
            new Vector3(46f, 0f, 24f),   // 8: hairpin exit
            new Vector3(22f, 0f, 27f),   // 9: back straight (long — main draft/overtake zone)
            new Vector3(-10f, 0f, 29f),  // 10: back straight continues
            new Vector3(-38f, 0f, 26f),  // 11: into the west sweeper
            new Vector3(-55f, 0f, 8f),   // 12: west sweeper — wide, fast, side-by-side room
            new Vector3(-57f, 0f, -10f), // 13: west sweeper continues, closes back to point 0
        }, TrackLengthScale);

        // Longest of the 3 stages (per CLAUDE.md's "트랙 길이: 가장 김") — a bigger, more
        // technical layout: front chicane into a double-apex esses, a long braking zone into a
        // wide hairpin loop, then the longest back straight of any stage (main draft zone),
        // closing through a west sweeper. Verified the same way as BikiniCity: 0 wall
        // self-intersections at both 64 and 128 segments, min curvature radius 7.13m (narrows
        // to a 12.1m-wide pinch at one point vs the full 16m elsewhere — still >10x the 1.2m
        // vehicle width, so left as-is rather than forcing every corner above TrackWidth/2 the
        // way BikiniCity's does), ~444m lap length.
        static readonly Vector3[] AfricaTvControlPoints = Scale(new[]
        {
            new Vector3(-70f, 0f, -35f), // 0: front straight, start/finish
            new Vector3(-45f, 0f, -42f), // 1: chicane kick-out
            new Vector3(-20f, 0f, -30f), // 2: chicane kick-back-in
            new Vector3(5f, 0f, -38f),   // 3: esses valley
            new Vector3(30f, 0f, -30f),  // 4: esses ridge
            new Vector3(50f, 0f, -38f),  // 5: esses valley
            new Vector3(72f, 0f, -25f),  // 6: braking zone into the hairpin
            new Vector3(85f, 0f, -5f),   // 7: hairpin entry curl
            new Vector3(85f, 0f, 15f),   // 8: hairpin far side
            new Vector3(85f, 0f, 33f),   // 9: hairpin apex (wide loop)
            new Vector3(65f, 0f, 42f),   // 10: hairpin exit
            new Vector3(35f, 0f, 38f),   // 11: back straight (longest of any stage)
            new Vector3(0f, 0f, 40f),    // 12: back straight continues
            new Vector3(-35f, 0f, 38f),  // 13: back straight continues more
            new Vector3(-60f, 0f, 30f),  // 14: into the west sweeper
            new Vector3(-78f, 0f, 10f),  // 15: west sweeper wide
            new Vector3(-82f, 0f, -12f), // 16: west sweeper continues
            new Vector3(-78f, 0f, -28f), // 17: closes back to point 0
        }, TrackLengthScale);

        // Shortest of the 3 stages (per CLAUDE.md's "트랙 길이: 가장 짧음") — a tight fortress
        // courtyard loop, but still comfortably wide: front straight into a wide hairpin, a
        // short back straight, and a west sweeper closing the loop. Verified: 0 wall
        // self-intersections at both 64 and 128 segments, min curvature radius 14.5m (above
        // TrackWidth/2=8m everywhere — full 16m width, no pinch), ~190m lap length. An earlier
        // 10-point draft had two points only 5m apart right at the closing seam, which made
        // Catmull-Rom overshoot into a self-crossing wall (same failure mode CLAUDE.md already
        // documents for BikiniCity) — fixed by dropping to 9 points with even spacing throughout.
        static readonly Vector3[] NetherFortressControlPoints = Scale(new[]
        {
            new Vector3(-35f, 0f, -22f), // 0: front straight, start/finish
            new Vector3(-10f, 0f, -26f), // 1: front straight continues
            new Vector3(14f, 0f, -18f),  // 2: braking zone into the hairpin
            new Vector3(24f, 0f, -2f),   // 3: hairpin entry curl
            new Vector3(24f, 0f, 16f),   // 4: hairpin apex (wide loop)
            new Vector3(10f, 0f, 24f),   // 5: hairpin exit
            new Vector3(-14f, 0f, 22f),  // 6: back straight
            new Vector3(-32f, 0f, 12f),  // 7: into the west sweeper
            new Vector3(-42f, 0f, -8f),  // 8: west sweeper wide, closes back to point 0
        }, TrackLengthScale);

        static Vector3[] ControlPointsFor(StageType stage) => stage switch
        {
            StageType.AfricaTv => AfricaTvControlPoints,
            StageType.NetherFortress => NetherFortressControlPoints,
            _ => BikiniCityControlPoints,
        };

        static float TrackWidthFor(StageType stage) => stage switch
        {
            StageType.AfricaTv => AfricaTvTrackWidth,
            StageType.NetherFortress => NetherFortressTrackWidth,
            _ => BikiniCityTrackWidth,
        };

        // Set at the start of Build() from the stage passed in — every helper below reads
        // this instead of taking a TrackGeometry parameter. Not thread-safe, but this whole
        // class is editor-only batch/GUI tooling that never runs Build() concurrently.
        static Vector3[] currentControlPoints = BikiniCityControlPoints;
        static float currentTrackWidth = BikiniCityTrackWidth;
        static TrackGeometry Geometry = new TrackGeometry(BikiniCityControlPoints, BikiniCityTrackWidth, WallHeight);

        // Vehicle body is 1.2m wide (see CreateVehicle scale). Widened from 12m for real
        // 2-player racing (room to draft/overtake side by side, not just squeeze past).
        internal const float VehicleWidth = 1.2f;
        const float BikiniCityTrackWidth = 16f;
        const float AfricaTvTrackWidth = 16f;
        // Narrower than the other two stages per playtester feedback ("네더요새 폭이 넓다") — still
        // safely clear of NetherFortressControlPoints' min curvature radius (14.49m, so half of
        // even this narrower width never triggers TrackGeometry.SafeLateralOffset's clamp).
        const float NetherFortressTrackWidth = 11f;
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
        internal const string VehicleModelPath = "Assets/Art/Models/kenney_car-kit/Models/FBX format/race.fbx";
        internal const string VehicleModelTexturePath = "Assets/Art/Models/kenney_car-kit/Models/FBX format/Textures/colormap.png";
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

        // Freezes a stage build into a real, permanent Assets/Scenes/Stage_*.unity scene file
        // instead of building into whatever scene happens to be open. Re-running this
        // OVERWRITES the saved scene from scratch — any manual edits made directly in the
        // scene file since the last run will be lost. Deliberately has NO
        // EditorApplication.Exit call (unlike BuildCheck's headless methods) since these carry
        // [MenuItem]s — a human can click one from an open Editor, and Exit-ing there would
        // force-quit their whole Editor session.
        [MenuItem("M2/Build Persisted Scene/Bikini City (비키니시티)")]
        public static void BuildAndSaveBikiniCityScene() =>
            BuildAndSavePersistedScene(StageType.BikiniCity, PersistedBikiniCityRootName, PersistedBikiniCityScenePath);

        [MenuItem("M2/Build Persisted Scene/Africa TV (아프리카TV)")]
        public static void BuildAndSaveAfricaTvScene() =>
            BuildAndSavePersistedScene(StageType.AfricaTv, PersistedAfricaTvRootName, PersistedAfricaTvScenePath);

        [MenuItem("M2/Build Persisted Scene/Nether Fortress (네더요새)")]
        public static void BuildAndSaveNetherFortressScene() =>
            BuildAndSavePersistedScene(StageType.NetherFortress, PersistedNetherFortressRootName, PersistedNetherFortressScenePath);

        static void BuildAndSavePersistedScene(StageType stage, string rootName, string scenePath)
        {
            if (EditorSceneManager.GetActiveScene().isDirty &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning($"M2: 저장 안 된 변경사항이 있는 씬에서 취소를 선택함 — {scenePath} 빌드 중단.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build(stage, rootName: rootName, attachStageTestSelector: false);

            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log(saved
                ? $"M2: {stage} 씬 저장 완료 → {scenePath}"
                : $"M2: {stage} 씬 저장 실패 → {scenePath}");
        }

        // rootName/attachStageTestSelector let a caller opt out of the shared "(Generated)"
        // ephemeral-test-track identity and the temporary 1/2/3 stage-switcher — used by
        // BuildAndSaveBikiniCityScene to freeze a stage into its own permanent, single-stage
        // scene. The 3 MenuItems above keep calling this with just a StageType, so their
        // behavior is unchanged (both new params default to the original ephemeral-build values).
        public static void Build(StageType initialStage, string rootName = RootName, bool attachStageTestSelector = true)
        {
            currentControlPoints = ControlPointsFor(initialStage);
            currentTrackWidth = TrackWidthFor(initialStage);
            Geometry = new TrackGeometry(currentControlPoints, currentTrackWidth, WallHeight);

            GameObject existingRoot = GameObject.Find(rootName);
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot);
            }

            var root = new GameObject(rootName);

            CreateGround(root.transform);
            CreateTrackSurface(root.transform);
            CreateStartFinishLine(root.transform);
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
            // Sized off the control points' actual bounding box (the track is hand-placed now,
            // not a formula-derived oval) plus track width, so the track and its walls never
            // poke past the visible ground plane. Unity's primitive Plane is 10x10m at scale 1,
            // hence the /5 to convert a desired half-extent in meters into a scale factor.
            float maxAbsX = 0f, maxAbsZ = 0f;
            foreach (Vector3 p in currentControlPoints)
            {
                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(p.x));
                maxAbsZ = Mathf.Max(maxAbsZ, Mathf.Abs(p.z));
            }
            float scaleX = (maxAbsX + currentTrackWidth) / 5f + 2f;
            float scaleZ = (maxAbsZ + currentTrackWidth) / 5f + 2f;
            ground.transform.localScale = new Vector3(scaleX, 1f, scaleZ);

            // This plane is the off-track surface only — keep it a distinct, brighter color so
            // the dark navy-purple track ring (see CreateTrackSurface, now palette-matched to
            // "M2 레이싱 게임 UI 디자인"'s mockups) reads clearly as the drivable path instead of
            // blending into the rest of the ground. #b6f36b lime green — the same UI mockup's
            // most common accent after gold/pink, and a cheerful contrast against the track.
            RendererColorUtil.ApplyColor(ground.GetComponent<Renderer>(), new Color(0.714f, 0.953f, 0.420f));
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
                vertices[i * 2] = Geometry.OffsetPointAt(theta, Geometry.SafeLateralOffset(theta, -currentTrackWidth / 2f));
                vertices[i * 2 + 1] = Geometry.OffsetPointAt(theta, Geometry.SafeLateralOffset(theta, currentTrackWidth / 2f));

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

        // Checkered start/finish line at theta=0 (matches Checkpoint_0 / the vehicle's spawn
        // point) — playtester feedback: "출발/도착선이 없어". Editor-only build (no runtime
        // hot-swap path uses this), so Object.DestroyImmediate on the leftover collider is safe
        // here, unlike StageAssembler's hazards which need the Play-mode-safe SafeDestroy.
        static void CreateStartFinishLine(Transform parent)
        {
            const float depth = 2.5f; // along-track thickness of the line
            Vector3 position = Geometry.PointAt(0f);
            Vector3 tangent = Geometry.TangentAt(0f);

            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "StartFinishLine";
            line.transform.SetParent(parent);
            line.transform.position = position + Vector3.up * 0.02f; // above TrackSurface's 0.01 to avoid z-fighting
            line.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            line.transform.localScale = new Vector3(currentTrackWidth, 0.05f, depth);
            Object.DestroyImmediate(line.GetComponent<BoxCollider>());

            RendererColorUtil.ApplyTexture(line.GetComponent<Renderer>(), TrackTextureFactory.CreateCheckeredFlagTexture(), Vector2.one);
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
            float desiredOffset = sideSign * currentTrackWidth / 2f;
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

        // Was 10 — playtester feedback ("빈 배경") plus only 3 model variants per stage made
        // the background outside the track read as sparse/empty. Bumped alongside adding more
        // model variety per stage (below) rather than just repeating the same 3 props more often.
        const int BackgroundDecorCount = 18;
        const float BackgroundDecorMargin = 8f; // how far outside the outer wall decor sits

        static void CreateBackgroundDecor(Transform parent, StageType stage)
        {
            string[] modelPaths;
            string texturePath;

            switch (stage)
            {
                case StageType.BikiniCity:
                    // No colormap.png in kenney_nature-kit (confirmed — the pack ships no
                    // Textures folder at all, unlike the other two kits below) — it relies on
                    // baked vertex colors, which URP's default Lit shader doesn't read without
                    // extra material setup this project hasn't done. Rather than leave these
                    // renderers with whatever default material Unity assigns (risking a blank/
                    // white look), FallbackColorFor below applies a flat rock/foliage tint as a
                    // safe substitute. Palm trees swapped in for tree_detailed — reads as a
                    // beach much more than a generic forest tree.
                    modelPaths = new[]
                    {
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/cliff_blockCave_rock.fbx",
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/rock_largeA.fbx",
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/rock_largeC.fbx",
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/rock_tallB.fbx",
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/tree_palm.fbx",
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/tree_palmBend.fbx",
                        "Assets/Art/Models/kenney_nature-kit/Models/FBX format/tree_palmDetailedTall.fbx",
                    };
                    texturePath = null;
                    break;
                case StageType.AfricaTv:
                    modelPaths = new[]
                    {
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-a.fbx",
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-e.fbx",
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-j.fbx",
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-b.fbx",
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-d.fbx",
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-skyscraper-a.fbx",
                        "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/building-skyscraper-c.fbx",
                    };
                    texturePath = "Assets/Art/Models/kenney_city-kit-commercial_2.1/Models/FBX format/Textures/colormap.png";
                    break;
                case StageType.NetherFortress:
                    modelPaths = new[]
                    {
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/tower-hexagon-base.fbx",
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/tower-hexagon-mid.fbx",
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/bridge-straight.fbx",
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/tower-square-base.fbx",
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/tower-square-top-roof.fbx",
                        "Assets/Art/Models/kenney_castle-kit/Models/FBX format/wall-corner-half-tower.fbx",
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
                Vector3 position = Geometry.OffsetPointAt(theta, currentTrackWidth / 2f + BackgroundDecorMargin);

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
                else
                {
                    Color fallback = FallbackColorFor(path);
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                    {
                        RendererColorUtil.ApplyColor(renderer, fallback);
                    }
                }
            }
        }

        // Flat tint for models with no texture atlas to fall back on (currently only
        // BikiniCity's kenney_nature-kit props) — rock/cliff props read as stone gray, everything
        // else (palm trees) as foliage green. A crude heuristic on the filename, but the two
        // categories are the only ones in play here.
        static Color FallbackColorFor(string modelPath)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(modelPath);
            bool isRock = fileName.Contains("rock") || fileName.Contains("cliff");
            return isRock ? new Color(0.55f, 0.53f, 0.5f) : new Color(0.25f, 0.55f, 0.2f);
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
                box.size = new Vector3(currentTrackWidth, WallHeight * 2f, 1f);

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

        // internal, not static-private: NetworkPrefabBuilder reuses this to give the networked
        // vehicle prefab the same real Kenney model instead of duplicating this loading logic.
        internal static void CreateVehicleModel(Transform parent)
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

            // Top-right status-effect list (기절/조향반전/넉백/방어막/부스트) — sits just below
            // where a stage gauge label (oxygen/mental/temperature) gets placed at (-20,-20) by
            // StageAssembler, so the two never overlap.
            GameObject statusTextObject = new GameObject("VehicleStatusLabel");
            statusTextObject.transform.SetParent(canvasObject.transform);
            Text statusText = statusTextObject.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 22;
            statusText.color = Color.yellow;
            statusText.alignment = TextAnchor.UpperRight;

            RectTransform statusRect = statusTextObject.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(1f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(1f, 1f);
            statusRect.anchoredPosition = new Vector2(-20f, -70f);
            statusRect.sizeDelta = new Vector2(260f, 180f);

            VehicleStatusHUD statusHud = canvasObject.AddComponent<VehicleStatusHUD>();
            statusHud.vehicleController = vehicle.GetComponent<VehicleController>();
            statusHud.label = statusText;

            // Center-screen "반대 방향입니다!" banner, shown for as long as
            // VehicleController's wrong-way prevention is actively refusing movement —
            // playtester feedback: driving backward past a checkpoint (or turning around
            // entirely and driving forward) had no warning or resistance at all, which felt
            // broken compared to how racing games normally handle it.
            GameObject wrongWayTextObject = new GameObject("WrongWayLabel");
            wrongWayTextObject.transform.SetParent(canvasObject.transform);
            Text wrongWayText = wrongWayTextObject.AddComponent<Text>();
            wrongWayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            wrongWayText.fontSize = 40;
            wrongWayText.color = Color.red;
            wrongWayText.alignment = TextAnchor.MiddleCenter;
            wrongWayText.fontStyle = FontStyle.Bold;

            RectTransform wrongWayRect = wrongWayTextObject.GetComponent<RectTransform>();
            wrongWayRect.anchorMin = new Vector2(0.5f, 0.5f);
            wrongWayRect.anchorMax = new Vector2(0.5f, 0.5f);
            wrongWayRect.pivot = new Vector2(0.5f, 0.5f);
            wrongWayRect.anchoredPosition = new Vector2(0f, 150f);
            wrongWayRect.sizeDelta = new Vector2(700f, 80f);

            WrongWayWarning wrongWayWarning = canvasObject.AddComponent<WrongWayWarning>();
            wrongWayWarning.label = wrongWayText;
            wrongWayWarning.vehicleController = vehicle.GetComponent<VehicleController>();
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
