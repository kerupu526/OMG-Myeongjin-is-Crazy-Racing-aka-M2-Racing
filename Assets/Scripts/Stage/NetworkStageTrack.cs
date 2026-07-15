using System.Collections.Generic;
using M2.Core;
using M2.Network;
using UnityEngine;

namespace M2.Stage
{
    /// <summary>
    /// Runtime counterpart of the editor-only TestTrackBuilder used by NetworkRace.
    ///
    /// The online room is selected after the scene has loaded, so baking one track into
    /// NetworkRace meant that Africa TV and Nether Fortress could change their HUD and
    /// gauges while still racing on Bikini City's physical circuit.  This component rebuilds
    /// the deterministic, non-networked environment on every peer before a race starts.
    /// Player movement remains owner-authoritative and race progress remains host-authoritative;
    /// only identical scene geometry is reconstructed locally.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkStageTrack : MonoBehaviour
    {
        public const int CheckpointCount = 6;
        public const int ItemSpawnCount = 6;

        const float TrackLengthScale = 1.5f;
        const float BikiniTrackWidth = 16f;
        const float AfricaTrackWidth = 16f;
        const float NetherTrackWidth = 11f;
        const float WallHeight = 1.2f;
        const int WallSegments = 64;
        const float TrackTextureRepeatsPerLap = 24f;

        static readonly Vector3[] BikiniControlPoints = Scale(new[]
        {
            new Vector3(-48f, 0f, -28f), new Vector3(-25f, 0f, -32f),
            new Vector3(-6f, 0f, -23f), new Vector3(14f, 0f, -28f),
            new Vector3(34f, 0f, -26f), new Vector3(50f, 0f, -18f),
            new Vector3(60f, 0f, -2f), new Vector3(60f, 0f, 14f),
            new Vector3(46f, 0f, 24f), new Vector3(22f, 0f, 27f),
            new Vector3(-10f, 0f, 29f), new Vector3(-38f, 0f, 26f),
            new Vector3(-55f, 0f, 8f), new Vector3(-57f, 0f, -10f),
        }, TrackLengthScale);

        static readonly Vector3[] AfricaControlPoints = Scale(new[]
        {
            new Vector3(-70f, 0f, -35f), new Vector3(-45f, 0f, -42f),
            new Vector3(-20f, 0f, -30f), new Vector3(5f, 0f, -38f),
            new Vector3(30f, 0f, -30f), new Vector3(50f, 0f, -38f),
            new Vector3(72f, 0f, -25f), new Vector3(85f, 0f, -5f),
            new Vector3(85f, 0f, 15f), new Vector3(85f, 0f, 33f),
            new Vector3(65f, 0f, 42f), new Vector3(35f, 0f, 38f),
            new Vector3(0f, 0f, 40f), new Vector3(-35f, 0f, 38f),
            new Vector3(-60f, 0f, 30f), new Vector3(-78f, 0f, 10f),
            new Vector3(-82f, 0f, -12f), new Vector3(-78f, 0f, -28f),
        }, TrackLengthScale);

        static readonly Vector3[] NetherControlPoints = Scale(new[]
        {
            new Vector3(-35f, 0f, -22f), new Vector3(-10f, 0f, -26f),
            new Vector3(14f, 0f, -18f), new Vector3(24f, 0f, -2f),
            new Vector3(24f, 0f, 16f), new Vector3(10f, 0f, 24f),
            new Vector3(-14f, 0f, 22f), new Vector3(-32f, 0f, 12f),
            new Vector3(-42f, 0f, -8f),
        }, TrackLengthScale);

        readonly HashSet<string> bakedEnvironmentNames = new HashSet<string>
        {
            "Ground", "TrackSurface", "StartFinishLine", "OuterWall", "InnerWall",
            "BackgroundDecor", "Checkpoints",
        };

        GameObject runtimeRoot;
        TrackGeometry geometry;
        StageType currentStage;
        bool hasApplied;
        bool bakedEnvironmentCleared;

        public StageType CurrentStage => currentStage;
        public TrackGeometry Geometry => geometry;
        public Transform RuntimeRoot => runtimeRoot != null ? runtimeRoot.transform : null;

        public void Apply(StageType requestedStage)
        {
            StageType stage = Normalize(requestedStage);
            if (hasApplied && currentStage == stage && runtimeRoot != null) return;

            ClearBakedEnvironment();
            if (runtimeRoot != null)
            {
                runtimeRoot.SetActive(false);
                Destroy(runtimeRoot);
            }

            currentStage = stage;
            hasApplied = true;
            geometry = CreateGeometry(stage);
            runtimeRoot = new GameObject($"NetworkStageTrack_Runtime_{stage}");
            runtimeRoot.transform.SetParent(transform, false);

            CreateGround(runtimeRoot.transform, stage);
            CreateTrackSurface(runtimeRoot.transform);
            CreateStartFinishLine(runtimeRoot.transform);
            CreateWallRing(runtimeRoot.transform, "OuterWall", 1f);
            CreateWallRing(runtimeRoot.transform, "InnerWall", -1f);
            CreateCheckpoints(runtimeRoot.transform);
            CreateStageDecor(runtimeRoot.transform, stage);
            CreateStageGameplay(runtimeRoot.transform, stage);

            PositionStartGrid();
            PositionItemSpawnPoints();
            RefreshLapTrackers();
            RepositionOwnedVehicles();
        }

        static StageType Normalize(StageType stage) => stage switch
        {
            StageType.AfricaTv => StageType.AfricaTv,
            StageType.NetherFortress => StageType.NetherFortress,
            _ => StageType.BikiniCity,
        };

        static Vector3[] Scale(Vector3[] points, float multiplier)
        {
            var scaled = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) scaled[i] = points[i] * multiplier;
            return scaled;
        }

        static TrackGeometry CreateGeometry(StageType stage) => stage switch
        {
            StageType.AfricaTv => new TrackGeometry(AfricaControlPoints, AfricaTrackWidth, WallHeight),
            StageType.NetherFortress => new TrackGeometry(NetherControlPoints, NetherTrackWidth, WallHeight),
            _ => new TrackGeometry(BikiniControlPoints, BikiniTrackWidth, WallHeight),
        };

        void ClearBakedEnvironment()
        {
            if (bakedEnvironmentCleared) return;
            bakedEnvironmentCleared = true;

            // NetworkRace's old scene asset baked Bikini City's environment directly below this
            // root. Keep the networking, camera, HUD, grid, and item-marker objects intact.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (!bakedEnvironmentNames.Contains(child.name)) continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        void CreateGround(Transform parent, StageType stage)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);

            float maxAbsX = 0f;
            float maxAbsZ = 0f;
            Vector3[] points = PointsFor(stage);
            for (int i = 0; i < points.Length; i++)
            {
                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(points[i].x));
                maxAbsZ = Mathf.Max(maxAbsZ, Mathf.Abs(points[i].z));
            }
            ground.transform.localScale = new Vector3(
                (maxAbsX + geometry.TrackWidth) / 5f + 2f,
                1f,
                (maxAbsZ + geometry.TrackWidth) / 5f + 2f);

            Color color = stage switch
            {
                StageType.AfricaTv => new Color(0.32f, 0.30f, 0.46f),
                StageType.NetherFortress => new Color(0.22f, 0.08f, 0.07f),
                _ => new Color(0.714f, 0.953f, 0.420f),
            };
            RendererColorUtil.ApplyColor(ground.GetComponent<Renderer>(), color);
        }

        static Vector3[] PointsFor(StageType stage) => stage switch
        {
            StageType.AfricaTv => AfricaControlPoints,
            StageType.NetherFortress => NetherControlPoints,
            _ => BikiniControlPoints,
        };

        void CreateTrackSurface(Transform parent)
        {
            var vertices = new Vector3[WallSegments * 2];
            var uvs = new Vector2[WallSegments * 2];
            var triangles = new int[WallSegments * 6];

            for (int i = 0; i < WallSegments; i++)
            {
                float theta = i * Mathf.PI * 2f / WallSegments;
                vertices[i * 2] = geometry.OffsetPointAt(theta,
                    geometry.SafeLateralOffset(theta, -geometry.TrackWidth / 2f));
                vertices[i * 2 + 1] = geometry.OffsetPointAt(theta,
                    geometry.SafeLateralOffset(theta, geometry.TrackWidth / 2f));
                float u = i / (float)WallSegments * TrackTextureRepeatsPerLap;
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
                int triangle = i * 6;
                triangles[triangle] = innerA;
                triangles[triangle + 1] = outerB;
                triangles[triangle + 2] = outerA;
                triangles[triangle + 3] = innerA;
                triangles[triangle + 4] = innerB;
                triangles[triangle + 5] = outerB;
            }

            var mesh = new Mesh { name = "NetworkTrackSurfaceMesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject surface = new GameObject("TrackSurface");
            surface.transform.SetParent(parent);
            surface.transform.localPosition = Vector3.up * 0.01f;
            surface.AddComponent<MeshFilter>().mesh = mesh;
            Renderer renderer = surface.AddComponent<MeshRenderer>();
            RendererColorUtil.ApplyTexture(renderer, TrackTextureFactory.CreateAsphaltTexture(), Vector2.one, true);
        }

        void CreateStartFinishLine(Transform parent)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "StartFinishLine";
            line.transform.SetParent(parent);
            line.transform.position = geometry.PointAt(0f) + Vector3.up * 0.02f;
            line.transform.rotation = Quaternion.LookRotation(geometry.TangentAt(0f), Vector3.up);
            line.transform.localScale = new Vector3(geometry.TrackWidth, 0.05f, 2.5f);
            DisableCollider(line.GetComponent<Collider>());
            RendererColorUtil.ApplyTexture(line.GetComponent<Renderer>(), TrackTextureFactory.CreateCheckeredFlagTexture(), Vector2.one);
        }

        void CreateWallRing(Transform parent, string name, float sideSign)
        {
            Transform ring = new GameObject(name).transform;
            ring.SetParent(parent);
            float desiredOffset = sideSign * geometry.TrackWidth / 2f;
            float radius = geometry.WallHeight / 2f;
            var wallMaterial = new PhysicsMaterial($"{name}_Frictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0.2f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum,
            };

            for (int i = 0; i < WallSegments; i++)
            {
                float theta = i * Mathf.PI * 2f / WallSegments;
                float nextTheta = (i + 1) * Mathf.PI * 2f / WallSegments;
                Vector3 p0 = geometry.OffsetPointAt(theta, geometry.SafeLateralOffset(theta, desiredOffset));
                Vector3 p1 = geometry.OffsetPointAt(nextTheta, geometry.SafeLateralOffset(nextTheta, desiredOffset));
                Vector3 mid = (p0 + p1) / 2f;

                GameObject segment = new GameObject($"{name}_{i}");
                segment.transform.SetParent(ring);
                segment.transform.position = mid + Vector3.up * radius;
                segment.transform.rotation = Quaternion.LookRotation((p1 - p0).normalized, Vector3.up);

                CapsuleCollider capsule = segment.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = radius;
                capsule.height = Vector3.Distance(p0, p1) + radius * 2f;
                capsule.material = wallMaterial;
                segment.AddComponent<WallMarker>();
            }
        }

        void CreateCheckpoints(Transform parent)
        {
            Transform checkpoints = new GameObject("Checkpoints").transform;
            checkpoints.SetParent(parent);
            for (int i = 0; i < CheckpointCount; i++)
            {
                float theta = i * Mathf.PI * 2f / CheckpointCount;
                GameObject checkpoint = new GameObject($"Checkpoint_{i}");
                checkpoint.transform.SetParent(checkpoints);
                checkpoint.transform.position = geometry.PointAt(theta) + Vector3.up * (geometry.WallHeight / 2f);
                checkpoint.transform.rotation = Quaternion.LookRotation(geometry.TangentAt(theta), Vector3.up);

                BoxCollider box = checkpoint.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(geometry.TrackWidth, geometry.WallHeight * 2f, 1f);
                checkpoint.AddComponent<Checkpoint>().index = i;
            }
        }

        void CreateStageDecor(Transform parent, StageType stage)
        {
            Transform decor = new GameObject("BackgroundDecor").transform;
            decor.SetParent(parent);

            StageArtPrefabId prefabId = stage switch
            {
                StageType.AfricaTv => StageArtPrefabId.AfricaBroadcastTower,
                StageType.NetherFortress => StageArtPrefabId.NetherLavaRock,
                _ => StageArtPrefabId.BikiniTerrainRock,
            };
            Color tint = stage switch
            {
                StageType.AfricaTv => new Color(0.30f, 0.35f, 0.58f),
                StageType.NetherFortress => new Color(0.34f, 0.12f, 0.08f),
                _ => new Color(0.52f, 0.48f, 0.42f),
            };
            GameObject prefab = StageArtPrefabLibrary.Load()?.Get(prefabId);
            const int decorCount = 14;
            for (int i = 0; i < decorCount; i++)
            {
                float theta = (i + 0.19f * (i % 3)) * Mathf.PI * 2f / decorCount;
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 position = geometry.OffsetPointAt(theta, side * (geometry.TrackWidth / 2f + 9f + i % 3));
                GameObject prop = prefab != null
                    ? Instantiate(prefab, decor)
                    : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prop.name = $"{stage}_Decor_{i}";
                prop.transform.position = position;
                prop.transform.rotation = Quaternion.Euler(0f, i * 43f, 0f);
                float scale = stage == StageType.AfricaTv ? 0.62f : 0.9f;
                prop.transform.localScale *= scale;
                foreach (Collider collider in prop.GetComponentsInChildren<Collider>()) collider.enabled = false;
                foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>())
                    RendererColorUtil.ApplyColor(renderer, tint);
            }
        }

        void CreateStageGameplay(Transform parent, StageType stage)
        {
            Transform gameplay = new GameObject("StageGameplay").transform;
            gameplay.SetParent(parent);
            switch (stage)
            {
                case StageType.AfricaTv:
                    CreateAfricaAccidentZone(gameplay);
                    break;
                case StageType.NetherFortress:
                    CreateNetherZones(gameplay);
                    break;
                default:
                    CreateOxygenBubbles(gameplay);
                    break;
            }
        }

        void CreateOxygenBubbles(Transform parent)
        {
            for (int i = 0; i < 4; i++)
            {
                float theta = (i + 0.25f) * Mathf.PI * 2f / 4f;
                float lateral = (i % 2 == 0 ? -1f : 1f) * geometry.TrackWidth * 0.18f;
                GameObject spawner = new GameObject($"OxygenBubbleSpawner_{i}");
                spawner.transform.SetParent(parent);
                spawner.transform.position = geometry.OffsetPointAt(theta, lateral);
                spawner.AddComponent<OxygenBubbleSpawner>();
            }
        }

        void CreateAfricaAccidentZone(Transform parent)
        {
            const float accidentTheta = 0.6f * Mathf.PI * 2f;
            const float warningTheta = accidentTheta - 0.06f * Mathf.PI * 2f;
            GameObject accident = CreateTrackZone(parent, "BroadcastAccidentZone", accidentTheta, 11f, 3f, 2f);
            accident.AddComponent<BroadcastAccidentZone>();
            AddGroundTint(accident.transform, 11f, 3f, new Color(0.55f, 0.1f, 0.55f));

            GameObject warning = CreateTrackZone(parent, "AccidentWarningZone", warningTheta, 11f, 1f, 2f);
            warning.AddComponent<AccidentWarningZone>();

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "BroadcastWarningMarker";
            marker.transform.SetParent(parent);
            marker.transform.position = geometry.OffsetPointAt(warningTheta, geometry.TrackWidth / 2f + 1.5f) + Vector3.up;
            marker.transform.localScale = new Vector3(0.45f, 1f, 0.45f);
            DisableCollider(marker.GetComponent<Collider>());
            RendererColorUtil.ApplyEmissiveColor(marker.GetComponent<Renderer>(), new Color(1f, 0.18f, 0.62f),
                new Color(1f, 0.05f, 0.42f));
        }

        void CreateNetherZones(Transform parent)
        {
            const float lavaTheta = 0.35f * Mathf.PI * 2f;
            float lavaSize = geometry.TrackWidth * 0.28f;
            GameObject lava = CreateTrackZone(parent, "LavaZone", lavaTheta, lavaSize, lavaSize,
                geometry.TrackWidth * 0.34f);
            LavaZone lavaZone = lava.AddComponent<LavaZone>();
            AddGroundTint(lava.transform, lavaSize, lavaSize, new Color(0.95f, 0.20f, 0.02f), true);

            const float oasisTheta = 0.75f * Mathf.PI * 2f;
            float oasisWidth = geometry.TrackWidth * 0.35f;
            float oasisDepth = geometry.TrackWidth * 1.4f;
            GameObject oasis = CreateTrackZone(parent, "OasisZone", oasisTheta, oasisWidth, oasisDepth,
                geometry.TrackWidth * 0.3f);
            oasis.AddComponent<OasisZone>();
            AddGroundTint(oasis.transform, oasisWidth, oasisDepth, new Color(0.2f, 0.6f, 0.85f));

            GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fireball.name = "GhastFireball";
            fireball.transform.SetParent(parent);
            fireball.transform.position = geometry.OffsetPointAt(0.54f * Mathf.PI * 2f, geometry.TrackWidth * 0.28f)
                + Vector3.up;
            fireball.transform.localScale = Vector3.one * 1.2f;
            GhastFireball ghast = fireball.AddComponent<GhastFireball>();
            ghast.trackCenter = transform;
            RendererColorUtil.ApplyEmissiveColor(fireball.GetComponent<Renderer>(), new Color(0.95f, 0.26f, 0.03f),
                new Color(1f, 0.28f, 0.02f));

            // Keep a direct reference available to every locally-owned Nether state.
            foreach (NetherFortressStageState state in FindObjectsByType<NetherFortressStageState>(FindObjectsSortMode.None))
                state.lavaZone = lavaZone;
        }

        GameObject CreateTrackZone(Transform parent, string name, float theta, float width, float depth, float lateralOffset)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent);
            zone.transform.position = geometry.OffsetPointAt(theta, lateralOffset) + Vector3.up * (geometry.WallHeight / 2f);
            zone.transform.rotation = Quaternion.LookRotation(geometry.TangentAt(theta), Vector3.up);
            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width, geometry.WallHeight * 2f, depth);
            return zone;
        }

        void AddGroundTint(Transform parent, float width, float depth, Color color, bool emissive = false)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "GroundTint";
            visual.transform.SetParent(parent);
            visual.transform.localPosition = new Vector3(0f, 0.02f - geometry.WallHeight / 2f, 0f);
            visual.transform.localScale = new Vector3(width, 0.05f, depth);
            DisableCollider(visual.GetComponent<Collider>());
            if (emissive)
                RendererColorUtil.ApplyEmissiveColor(visual.GetComponent<Renderer>(), color, color * 1.5f);
            else
                RendererColorUtil.ApplyColor(visual.GetComponent<Renderer>(), color);
        }

        void PositionStartGrid()
        {
            RaceStartGrid grid = FindFirstObjectByType<RaceStartGrid>();
            if (grid == null) return;

            Vector3 tangent = geometry.TangentAt(0f);
            Vector3 basePosition = geometry.PointAt(0f) - tangent * 3f + Vector3.up * 0.5f;
            Vector3 normal = geometry.NormalAt(0f);
            float lateral = geometry.TrackWidth * 0.22f;
            grid.slot0Position = basePosition + normal * lateral;
            grid.slot1Position = basePosition - normal * lateral;
            grid.facing = Quaternion.LookRotation(tangent, Vector3.up);
        }

        void PositionItemSpawnPoints()
        {
            NetworkItemSpawnPoint[] points = FindObjectsByType<NetworkItemSpawnPoint>(FindObjectsSortMode.None);
            var ordered = new List<NetworkItemSpawnPoint>(points);
            ordered.Sort((a, b) => a.index.CompareTo(b.index));

            if (ordered.Count < ItemSpawnCount)
            {
                Transform root = new GameObject("ItemSpawnPoints_Runtime").transform;
                root.SetParent(transform);
                for (int i = ordered.Count; i < ItemSpawnCount; i++)
                {
                    GameObject point = new GameObject($"ItemSpawnPoint_{i}");
                    point.transform.SetParent(root);
                    ordered.Add(point.AddComponent<NetworkItemSpawnPoint>());
                    ordered[ordered.Count - 1].index = i;
                }
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                int index = ordered[i].index;
                float theta = (index + 0.5f) * Mathf.PI * 2f / ItemSpawnCount;
                ordered[i].transform.position = geometry.PointAt(theta);
            }
        }

        static void RefreshLapTrackers()
        {
            foreach (LapTracker tracker in FindObjectsByType<LapTracker>(FindObjectsSortMode.None))
                tracker.RefreshCheckpointLayout();
        }

        static void RepositionOwnedVehicles()
        {
            foreach (NetworkVehicleSync sync in FindObjectsByType<NetworkVehicleSync>(FindObjectsSortMode.None))
                sync.RepositionForCurrentStartGrid();
        }

        static void DisableCollider(Collider collider)
        {
            if (collider == null) return;
            collider.enabled = false;
            Destroy(collider);
        }
    }
}
