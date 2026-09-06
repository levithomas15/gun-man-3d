using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GunMan.EditorTools
{
    /// <summary>
    /// Step 2: builds all game content (fx, prefabs, scenes) and renders preview screenshots.
    /// -executeMethod GunMan.EditorTools.GunManBuilder.BuildAll
    /// </summary>
    public static class GunManBuilder
    {
        [MenuItem("GunMan/2. Build Game Content")]
        public static void BuildAll()
        {
            Debug.Log("[GunMan] Build: start");
            GunManBootstrap.EnsureFolder(BuildUtil.PrefabDir);
            GunManBootstrap.EnsureFolder(BuildUtil.SceneDir);
            GunManBootstrap.EnsureFolder(BuildUtil.AnimDir);

            // work in a throw-away scene so prefab instantiation has a scene context
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var fx = FxBuilder.Build();
            var projectiles = PrefabBuilder.BuildProjectiles(fx);
            var weapons = PrefabBuilder.BuildWeapons(fx, projectiles);
            var prefabs = new SceneBuilder.Prefabs
            {
                fxLibrary = fx.fxLibraryPrefab,
                player = PrefabBuilder.BuildPlayer(weapons),
                npc = PrefabBuilder.BuildNpc(),
                target = PrefabBuilder.BuildTarget(),
                ammo = PrefabBuilder.BuildAmmoPickup(),
                crate = PrefabBuilder.BuildCrate(),
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"[GunMan] Prefabs built: {weapons.Count} weapons");

            var village = SceneBuilder.BuildVillage(prefabs);
            var arena = SceneBuilder.BuildArena(prefabs);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(village.path, true),
                new EditorBuildSettingsScene(arena.path, true),
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GunMan] Build: done");
        }

        [MenuItem("GunMan/3. Render Previews")]
        public static void RenderPreviews()
        {
            string dir = BuildUtil.PreviewDir;
            Directory.CreateDirectory(dir);
            Debug.Log($"[GunMan] Rendering previews to {dir}");

            // Kit dimension report (helps tuning the layout)
            var kit = new KitBuilder(1);
            var report = new List<string>();
            foreach (var n in new[] { "Wall_Plaster_Straight", "Wall_UnevenBrick_Straight", "Wall_UnevenBrick_Door_Flat", "Door_1_Flat", "Window_Wide_Flat1", "Corner_Exterior_Wood", "Floor_Brick", "Floor_WoodDark", "Floor_UnevenBrick", "Roof_RoundTiles_4x4", "Roof_RoundTiles_6x8", "Roof_Tower_RoundTiles", "Prop_Crate", "Prop_Wagon", "Prop_WoodenFence_Single", "Wall_BottomCover", "Stairs_Exterior_Platform", "Prop_Chimney" })
                report.Add($"{n}: {kit.Size(n)}");
            foreach (var n in new[] { "pew", "ak47", "awp", "shotgun", "rocketlaucher", "nade_low", "ammobox_low", "board" })
            {
                var m = BuildUtil.LoadModel(BuildUtil.GunsFbx, n);
                if (m == null) continue;
                var t = BuildUtil.Instantiate(m);
                report.Add($"gun {n}: {BuildUtil.WorldBounds(t).size}");
                Object.DestroyImmediate(t);
            }
            var ual = AssetDatabase.LoadAssetAtPath<GameObject>(BuildUtil.UalFbx);
            if (ual != null)
            {
                var t = BuildUtil.Instantiate(ual);
                report.Add($"UAL: {BuildUtil.WorldBounds(t).size}");
                Object.DestroyImmediate(t);
            }
            File.WriteAllLines(Path.Combine(dir, "dimensions.txt"), report);

            // Scenes
            foreach (var scenePath in new[] { $"{BuildUtil.SceneDir}/Village.unity", $"{BuildUtil.SceneDir}/Arena.unity" })
            {
                if (!File.Exists(scenePath)) continue;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                var player = Object.FindAnyObjectByType<PlayerController>();
                Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
                float yaw = player != null ? player.transform.eulerAngles.y : 0f;

                // hide the player capsule renderer (none) - render aerial + eye level shots
                PreviewRenderer.RenderShot(Path.Combine(dir, $"{sceneName}_aerial.png"), spawn + new Vector3(-40f, 70f, -60f), Quaternion.LookRotation(new Vector3(40f, -70f, 60f)), 60f);
                PreviewRenderer.RenderShot(Path.Combine(dir, $"{sceneName}_aerial2.png"), spawn + new Vector3(0f, 45f, -35f), Quaternion.LookRotation(new Vector3(0f, -45f, 35f)), 60f);
                for (int i = 0; i < 4; i++)
                {
                    var rot = Quaternion.Euler(4f, yaw + i * 90f, 0f);
                    PreviewRenderer.RenderShot(Path.Combine(dir, $"{sceneName}_eye{i}.png"), spawn + Vector3.up * 1.65f, rot, 72f);
                }

                // first-person weapon previews: activate each weapon and render through the player camera
                if (player != null && sceneName == "Village")
                {
                    var holder = player.GetComponentInChildren<WeaponHolder>(true);
                    var cam = player.GetComponentInChildren<Camera>(true);
                    var weaponObjs = holder.transform.Cast<Transform>().Where(t => t.GetComponent<Weapon>() != null).ToList();
                    foreach (var w in weaponObjs) w.gameObject.SetActive(false);
                    for (int i = 0; i < weaponObjs.Count; i++)
                    {
                        weaponObjs[i].gameObject.SetActive(true);
                        PreviewRenderer.RenderWithCamera(cam, Path.Combine(dir, $"weapon_{i + 1}_{weaponObjs[i].name}.png"));
                        weaponObjs[i].gameObject.SetActive(false);
                    }
                    // NPC close-up
                    var npc = Object.FindAnyObjectByType<NpcCharacter>();
                    if (npc != null)
                    {
                        var p = npc.transform.position;
                        PreviewRenderer.RenderShot(Path.Combine(dir, "npc_closeup.png"), p + new Vector3(2.5f, 1.4f, 2.5f), Quaternion.LookRotation((p + Vector3.up * 0.9f) - (p + new Vector3(2.5f, 1.4f, 2.5f))), 50f);
                    }
                    var target = Object.FindAnyObjectByType<PopupTarget>();
                    if (target != null)
                    {
                        var p = target.transform.position;
                        PreviewRenderer.RenderShot(Path.Combine(dir, "target_closeup.png"), p + new Vector3(-3f, 1.6f, -2f), Quaternion.LookRotation((p + Vector3.up * 1.2f) - (p + new Vector3(-3f, 1.6f, -2f))), 50f);
                    }
                }
            }
            Debug.Log("[GunMan] Previews done");
        }

        public static void BuildAndPreview()
        {
            BuildAll();
            RenderPreviews();
        }
    }

    public static class PreviewRenderer
    {
        public static void RenderShot(string path, Vector3 position, Quaternion rotation, float fov, int width = 1600, int height = 900)
        {
            var go = new GameObject("PreviewCamera");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(position, rotation);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.cullingMask = ~LayerMask.GetMask("Player");
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            try { RenderWithCamera(cam, path, width, height); }
            finally { Object.DestroyImmediate(go); }
        }

        public static void RenderWithCamera(Camera cam, string path, int width = 1600, int height = 900)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Debug.Log($"[GunMan] preview written: {path}");
        }
    }
}
