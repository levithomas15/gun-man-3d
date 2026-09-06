using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GunMan.EditorTools
{
    /// <summary>Assembles playable scenes from the kit + gameplay prefabs.</summary>
    public static class SceneBuilder
    {
        public class Prefabs
        {
            public GameObject player, npc, target, ammo, crate, fxLibrary;
        }

        public struct SceneResult
        {
            public string path;
            public Vector3 playerSpawn;
            public float playerYaw;
        }

        // ------------------------------------------------------------------ common

        static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return scene;
        }

        static void SetupLighting(Color sunColor, float sunIntensity, Vector3 sunRotation, Color fogColor, float fogStart, float fogEnd)
        {
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(sunRotation);
            RenderSettings.sun = sun;

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            GunManBootstrap.EnsureFolder("Assets/_Game/Settings");
            string lsPath = "Assets/_Game/Settings/GunManLighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null)
            {
                ls = new LightingSettings { bakedGI = false, realtimeGI = false, name = "GunManLighting" };
                AssetDatabase.CreateAsset(ls, lsPath);
            }
            Lightmapping.lightingSettings = ls;
        }

        static void SetupPostFx()
        {
            GunManBootstrap.EnsureFolder("Assets/_Game/Settings");
            string path = "Assets/_Game/Settings/GunManPostFX.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(0.45f);
                bloom.threshold.Override(1.05f);
                bloom.scatter.Override(0.65f);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.28f);
                vignette.smoothness.Override(0.4f);
                AssetDatabase.AddObjectToAsset(vignette, profile);
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.ACES);
                AssetDatabase.AddObjectToAsset(tone, profile);
                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(0.15f);
                color.contrast.Override(8f);
                color.saturation.Override(8f);
                AssetDatabase.AddObjectToAsset(color, profile);
                AssetDatabase.SaveAssets();
            }
            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 0;
            vol.sharedProfile = profile;
        }

        static GameObject AddGround(Material mat, float size)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
            BuildUtil.SetStaticRecursive(ground, true);
            return ground;
        }

        static void AddBoundary(Transform parent, float halfSize, float height)
        {
            // invisible walls so the player cannot walk off the map
            for (int i = 0; i < 4; i++)
            {
                var wall = new GameObject($"Boundary{i}");
                wall.transform.SetParent(parent, false);
                var box = wall.AddComponent<BoxCollider>();
                bool alongX = i < 2;
                float sign = i % 2 == 0 ? -1f : 1f;
                wall.transform.position = alongX ? new Vector3(0f, height / 2f, sign * halfSize) : new Vector3(sign * halfSize, height / 2f, 0f);
                box.size = alongX ? new Vector3(halfSize * 2f, height, 1f) : new Vector3(1f, height, halfSize * 2f);
                BuildUtil.SetStaticRecursive(wall, true);
            }
        }

        static void BakeNavMesh(GameObject root, string dataPath)
        {
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~LayerMask.GetMask("Dynamic", "Player");
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.15f;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                AssetDatabase.DeleteAsset(dataPath);
                AssetDatabase.CreateAsset(surface.navMeshData, dataPath);
                var tri = NavMesh.CalculateTriangulation();
                Debug.Log($"[GunMan] NavMesh baked: {tri.vertices.Length} verts, {tri.indices.Length / 3} tris -> {dataPath}");
            }
            else Debug.LogWarning("[GunMan] NavMesh bake produced no data");
        }

        static void AddGameplay(Prefabs prefabs, Vector3 spawn, float yaw)
        {
            var gameplay = new GameObject("Gameplay");
            var fx = BuildUtil.Instantiate(prefabs.fxLibrary, gameplay.transform);
            fx.transform.localPosition = Vector3.zero;

            var player = BuildUtil.Instantiate(prefabs.player, gameplay.transform, "Player");
            player.transform.position = spawn;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var gm = new GameObject("GameManager");
            gm.transform.SetParent(gameplay.transform, false);
            gm.AddComponent<GameManager>().player = player.GetComponent<PlayerController>();
            gm.AddComponent<GameHud>().holder = player.GetComponentInChildren<WeaponHolder>();
        }

        static void PlaceOnGround(GameObject go, Vector3 xz, float yOffset = 0f)
        {
            Vector3 origin = new Vector3(xz.x, 200f, xz.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 500f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
                go.transform.position = hit.point + Vector3.up * yOffset;
            else go.transform.position = new Vector3(xz.x, yOffset, xz.z);
        }

        static void SpawnNpcs(Prefabs prefabs, Transform parent, IEnumerable<Vector3> positions, float wanderRadius)
        {
            int i = 0;
            foreach (var p in positions)
            {
                var npc = BuildUtil.Instantiate(prefabs.npc, parent, $"NPC_{i++}");
                PlaceOnGround(npc, p);
                npc.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                npc.GetComponent<NpcCharacter>().wanderRadius = wanderRadius;
            }
        }

        static void SpawnTargets(Prefabs prefabs, Transform parent, IEnumerable<(Vector3 pos, float yaw)> items)
        {
            int i = 0;
            foreach (var (pos, yaw) in items)
            {
                var t = BuildUtil.Instantiate(prefabs.target, parent, $"Target_{i++}");
                PlaceOnGround(t, pos);
                t.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        static void SpawnAmmo(Prefabs prefabs, Transform parent, IEnumerable<Vector3> positions)
        {
            int i = 0;
            foreach (var p in positions)
            {
                var a = BuildUtil.Instantiate(prefabs.ammo, parent, $"Ammo_{i++}");
                PlaceOnGround(a, p);
            }
        }

        static void SpawnCrates(Prefabs prefabs, Transform parent, KitBuilder kit, IEnumerable<Vector3> positions, int dynamicLayer)
        {
            int i = 0;
            foreach (var p in positions)
            {
                int count = kit.RandInt(1, 4);
                for (int c = 0; c < count; c++)
                {
                    var crate = BuildUtil.Instantiate(prefabs.crate, parent, $"Crate_{i++}");
                    var off = new Vector3(kit.Rand(-0.9f, 0.9f), 0f, kit.Rand(-0.9f, 0.9f));
                    PlaceOnGround(crate, p + off, 0.05f + c * 0.0f);
                    crate.transform.rotation = Quaternion.Euler(0f, kit.Rand(0f, 360f), 0f);
                    BuildUtil.SetLayerRecursive(crate, dynamicLayer);
                }
            }
        }

        // ------------------------------------------------------------------ Village

        public static SceneResult BuildVillage(Prefabs prefabs)
        {
            NewScene();
            int dynamicLayer = BuildUtil.EnsureLayer("Dynamic");
            var kit = new KitBuilder(1337);
            var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Village/Ground_Grass.mat");
            if (grass != null) { grass.SetTextureScale("_BaseMap", new Vector2(110f, 110f)); grass.SetColor("_BaseColor", new Color(0.5f, 0.63f, 0.33f)); EditorUtility.SetDirty(grass); }

            SetupLighting(new Color(1f, 0.93f, 0.8f), 1.7f, new Vector3(48f, -35f, 0f), new Color(0.72f, 0.8f, 0.9f), 120f, 520f);
            SetupPostFx();

            var env = new GameObject("Environment");
            AddGround(grass, 320f).transform.SetParent(env.transform, true);
            AddBoundary(env.transform, 150f, 30f);

            // central plaza
            float plaza = 26f;
            kit.BuildFloorArea(env.transform, Vector3.zero, plaza, plaza, "Floor_UnevenBrick", 0.02f);
            kit.BuildFloorArea(env.transform, new Vector3(0f, 0f, plaza / 2f + 14f), 8f, 28f, "Floor_Brick", 0.02f); // north road
            kit.BuildFloorArea(env.transform, new Vector3(0f, 0f, -(plaza / 2f + 14f)), 8f, 28f, "Floor_Brick", 0.02f); // south road
            kit.BuildFloorArea(env.transform, new Vector3(plaza / 2f + 14f, 0f, 0f), 28f, 8f, "Floor_Brick", 0.02f); // east
            kit.BuildFloorArea(env.transform, new Vector3(-(plaza / 2f + 14f), 0f, 0f), 28f, 8f, "Floor_Brick", 0.02f); // west

            var houses = new GameObject("Houses");
            houses.transform.SetParent(env.transform, false);
            var houseInfos = new List<KitBuilder.HouseInfo>();
            var style = new KitBuilder.HouseStyle();
            var styleB = new KitBuilder.HouseStyle { groundWall = "Wall_Plaster_Straight_Base", groundWindow = "Wall_Plaster_Window_Wide_Flat2", groundDoor = "Wall_Plaster_Door_Flat", upperWindow = "Wall_Plaster_Window_Thin_Round", windowInsert = "Window_Thin_Round1", shutters = "WindowShutters_Thin_Round_Open" };
            if (!kit.Has(styleB.groundWall)) styleB.groundWall = "Wall_Plaster_Straight";

            // inner ring: 8 houses facing the plaza, skipping the 4 road gaps
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                float angle = 22.5f + i * 45f; // avoid roads at 0/90/180/270
                float radius = 27f;
                Vector3 center = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad)) * radius;
                int nx = kit.RandInt(2, 5), nz = kit.RandInt(2, 4), storeys = kit.Chance(0.65f) ? 2 : 1;
                float yaw = angle + 180f; // door (-Z) faces the plaza
                houseInfos.Add(kit.BuildHouse(houses.transform, center, yaw, nx, nz, storeys, kit.Chance(0.5f) ? style : styleB, $"House_{count++}"));
            }
            // outer ring: 8 more houses
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f + kit.Rand(-8f, 8f);
                float radius = 48f + kit.Rand(-3f, 5f);
                Vector3 center = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad)) * radius;
                // keep roads free
                if (i % 2 == 0) center += new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, -Mathf.Sin(angle * Mathf.Deg2Rad)) * 12f;
                int nx = kit.RandInt(2, 4), nz = kit.RandInt(2, 4), storeys = kit.Chance(0.5f) ? 2 : 1;
                float yaw = angle + 180f + kit.Rand(-15f, 15f);
                houseInfos.Add(kit.BuildHouse(houses.transform, center, yaw, nx, nz, storeys, kit.Chance(0.5f) ? style : styleB, $"House_{count++}"));
            }

            // props
            var props = new GameObject("Props");
            props.transform.SetParent(env.transform, false);
            kit.Place("Prop_Wagon", props.transform, new Vector3(-8f, 0f, 6f), 30f);
            kit.Place("Prop_Wagon", props.transform, new Vector3(30f, 0f, -34f), -60f);
            kit.BuildFenceLine(props.transform, new Vector3(-13f, 0f, -13f), new Vector3(-13f, 0f, -4f));
            kit.BuildFenceLine(props.transform, new Vector3(13f, 0f, 4f), new Vector3(13f, 0f, 13f));
            kit.BuildFenceLine(props.transform, new Vector3(-4f, 0f, 13f), new Vector3(-13f, 0f, 13f));
            kit.BuildFenceLine(props.transform, new Vector3(4f, 0f, -13f), new Vector3(13f, 0f, -13f));
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3(kit.Rand(-60f, 60f), 0f, kit.Rand(-60f, 60f));
                if (p.magnitude < 16f) continue;
                kit.Place(kit.Pick("Prop_Brick1", "Prop_Brick2", "Prop_Brick3", "Prop_Brick4"), props.transform, p, kit.Rand(0f, 360f));
            }
            // vines on a few house fronts
            for (int i = 0; i < houseInfos.Count; i += 3)
            {
                var h = houseInfos[i];
                var pos = h.doorWorldPos + h.doorOutward * 0.3f + Vector3.Cross(Vector3.up, h.doorOutward) * (h.width * 0.3f);
                float yaw = Quaternion.LookRotation(h.doorOutward).eulerAngles.y;
                kit.Place(kit.Pick("Prop_Vine1", "Prop_Vine2", "Prop_Vine4", "Prop_Vine5"), props.transform, pos, yaw);
            }
            // exterior stone border around the plaza corners
            kit.Place("Prop_ExteriorBorder_Corner", props.transform, new Vector3(-13.5f, 0f, -13.5f), 0f);
            kit.Place("Prop_ExteriorBorder_Corner", props.transform, new Vector3(13.5f, 0f, -13.5f), 90f);
            kit.Place("Prop_ExteriorBorder_Corner", props.transform, new Vector3(13.5f, 0f, 13.5f), 180f);
            kit.Place("Prop_ExteriorBorder_Corner", props.transform, new Vector3(-13.5f, 0f, 13.5f), 270f);

            // stairs + platform as a vantage point on the plaza's north side
            kit.Place("Stairs_Exterior_Platform", props.transform, new Vector3(0f, 0f, 10f), 180f);

            Physics.SyncTransforms();

            // ---- gameplay
            var gameplay = new GameObject("GameplayProps");
            var crates = new List<Vector3>();
            foreach (var h in houseInfos.Take(10))
                crates.Add(h.doorWorldPos + h.doorOutward * 2.5f + Vector3.Cross(Vector3.up, h.doorOutward) * (h.width * 0.35f));
            crates.Add(new Vector3(6f, 0f, -6f));
            crates.Add(new Vector3(-4f, 0f, -10f));
            SpawnCrates(prefabs, gameplay.transform, kit, crates, dynamicLayer);

            // shooting range: targets along the east road at increasing distance + a few around
            var targets = new List<(Vector3, float)>();
            for (int i = 0; i < 6; i++) targets.Add((new Vector3(14f + i * 7f, 0f, (i % 2 == 0 ? -2.5f : 2.5f)), -90f));
            targets.Add((new Vector3(-18f, 0f, 20f), 135f));
            targets.Add((new Vector3(20f, 0f, 22f), -135f));
            targets.Add((new Vector3(-9f, 0f, -40f), 0f));
            targets.Add((new Vector3(0f, 0f, 40f), 180f));
            SpawnTargets(prefabs, gameplay.transform, targets);

            SpawnAmmo(prefabs, gameplay.transform, new[] { new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 9f), new Vector3(0f, 0f, -30f), new Vector3(40f, 0f, 0f) });

            var npcPositions = new List<Vector3>();
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                float r = i % 3 == 0 ? 8f : (i % 3 == 1 ? 20f : 38f);
                npcPositions.Add(new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r));
            }

            BakeNavMesh(env, $"{BuildUtil.SceneDir}/NavMesh-Village.asset");
            SpawnNpcs(prefabs, gameplay.transform, npcPositions, 18f);

            Vector3 spawn = new Vector3(-11.5f, 0.1f, -11.5f);
            float spawnYaw = 65f; // look towards the plaza / shooting range
            AddGameplay(prefabs, spawn, spawnYaw);

            string path = $"{BuildUtil.SceneDir}/Village.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
            Debug.Log($"[GunMan] Village scene saved: {path} ({houseInfos.Count} houses)");
            return new SceneResult { path = path, playerSpawn = spawn, playerYaw = spawnYaw };
        }

        // ------------------------------------------------------------------ Arena

        public static SceneResult BuildArena(Prefabs prefabs)
        {
            NewScene();
            int dynamicLayer = BuildUtil.EnsureLayer("Dynamic");
            var kit = new KitBuilder(4242);
            var dirt = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Village/Ground_Dirt.mat");
            if (dirt != null) { dirt.SetTextureScale("_BaseMap", new Vector2(90f, 90f)); dirt.SetColor("_BaseColor", new Color(0.58f, 0.5f, 0.38f)); EditorUtility.SetDirty(dirt); }

            SetupLighting(new Color(1f, 0.85f, 0.7f), 1.5f, new Vector3(30f, 140f, 0f), new Color(0.8f, 0.72f, 0.62f), 90f, 400f);
            SetupPostFx();

            var env = new GameObject("Environment");
            AddGround(dirt, 260f).transform.SetParent(env.transform, true);

            float W = 60f, D = 44f;
            // outer walls: 2 storeys brick
            var walls = new GameObject("Walls");
            walls.transform.SetParent(env.transform, false);
            kit.BuildWallLine(walls.transform, new Vector3(-W / 2f, 0f, -D / 2f), new Vector3(W / 2f, 0f, -D / 2f), 2);
            kit.BuildWallLine(walls.transform, new Vector3(W / 2f, 0f, -D / 2f), new Vector3(W / 2f, 0f, D / 2f), 2);
            kit.BuildWallLine(walls.transform, new Vector3(W / 2f, 0f, D / 2f), new Vector3(-W / 2f, 0f, D / 2f), 2);
            kit.BuildWallLine(walls.transform, new Vector3(-W / 2f, 0f, D / 2f), new Vector3(-W / 2f, 0f, -D / 2f), 2);
            // corner towers
            var towerStyle = new KitBuilder.HouseStyle { roofPrefix = "Roof_Tower_", chimney = "" };
            if (!kit.Has("Roof_Tower_RoundTiles")) towerStyle.roofPrefix = "Roof_RoundTiles_";
            foreach (var c in new[] { new Vector3(-W / 2f, 0f, -D / 2f), new Vector3(W / 2f, 0f, -D / 2f), new Vector3(W / 2f, 0f, D / 2f), new Vector3(-W / 2f, 0f, D / 2f) })
                kit.BuildHouse(walls.transform, c, 0f, 2, 2, 3, towerStyle, "Tower");

            // floor
            kit.BuildFloorArea(env.transform, Vector3.zero, W - 4f, D - 4f, "Floor_RedBrick", 0.02f);

            // cover: low walls and a couple of small buildings inside
            var cover = new GameObject("Cover");
            cover.transform.SetParent(env.transform, false);
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector3(kit.Rand(-W / 2f + 8f, W / 2f - 8f), 0f, kit.Rand(-D / 2f + 8f, D / 2f - 8f));
                float yaw = kit.Pick(0f, 90f);
                kit.Place("Wall_BottomCover", cover.transform, p, yaw);
                if (kit.Chance(0.5f)) kit.Place("Wall_Arch", cover.transform, p + new Vector3(0f, 0f, 3f), yaw);
            }
            var style = new KitBuilder.HouseStyle();
            kit.BuildHouse(cover.transform, new Vector3(-14f, 0f, 8f), 90f, 2, 2, 1, style, "Hut_A");
            kit.BuildHouse(cover.transform, new Vector3(16f, 0f, -8f), -90f, 3, 2, 2, style, "Hut_B");
            kit.Place("Stairs_Exterior_PlatformU", cover.transform, new Vector3(0f, 0f, 12f), 180f);
            kit.Place("Prop_Wagon", cover.transform, new Vector3(4f, 0f, -12f), 20f);

            Physics.SyncTransforms();

            var gameplay = new GameObject("GameplayProps");
            var crates = new List<Vector3>();
            for (int i = 0; i < 8; i++) crates.Add(new Vector3(kit.Rand(-W / 2f + 6f, W / 2f - 6f), 0f, kit.Rand(-D / 2f + 6f, D / 2f - 6f)));
            SpawnCrates(prefabs, gameplay.transform, kit, crates, dynamicLayer);

            var targets = new List<(Vector3, float)>();
            for (int i = 0; i < 5; i++) targets.Add((new Vector3(W / 2f - 5f, 0f, -D / 2f + 6f + i * 8f), -90f));
            for (int i = 0; i < 3; i++) targets.Add((new Vector3(-W / 2f + 10f + i * 10f, 0f, D / 2f - 5f), 180f));
            SpawnTargets(prefabs, gameplay.transform, targets);
            SpawnAmmo(prefabs, gameplay.transform, new[] { new Vector3(0f, 0f, -D / 2f + 5f), new Vector3(-W / 2f + 6f, 0f, D / 2f - 8f), new Vector3(W / 2f - 8f, 0f, D / 2f - 8f) });

            var npcPositions = new List<Vector3>();
            for (int i = 0; i < 10; i++) npcPositions.Add(new Vector3(kit.Rand(-W / 2f + 6f, W / 2f - 6f), 0f, kit.Rand(-D / 2f + 6f, D / 2f - 6f)));

            BakeNavMesh(env, $"{BuildUtil.SceneDir}/NavMesh-Arena.asset");
            SpawnNpcs(prefabs, gameplay.transform, npcPositions, 22f);

            Vector3 spawn = new Vector3(-W / 2f + 6f, 0.1f, 0f);
            float spawnYaw = 90f;
            AddGameplay(prefabs, spawn, spawnYaw);

            string path = $"{BuildUtil.SceneDir}/Arena.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
            Debug.Log($"[GunMan] Arena scene saved: {path}");
            return new SceneResult { path = path, playerSpawn = spawn, playerYaw = spawnYaw };
        }
    }
}
