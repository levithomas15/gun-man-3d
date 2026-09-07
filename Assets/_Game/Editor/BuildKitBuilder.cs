using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunMan.EditorTools
{
    /// <summary>
    /// Creates the assets of the wooden building system: one URP material per wood texture in <see cref="TexDir"/>
    /// plus the translucent ghost / edit-marker materials. The player prefab's <see cref="BuildSystem"/> references them.
    /// </summary>
    public static class BuildKitBuilder
    {
        public const string TexDir = "Assets/_Game/Textures/Wood";
        public const string MatDir = "Assets/_Game/Materials/Build";
        public const string DefaultWood = "WoodPlanks_10";

        public class BuildAssets
        {
            public List<Material> woods = new List<Material>();
            public List<string> names = new List<string>();
            public int defaultIndex;
            public Material ghostValid, ghostInvalid, editPresent, editRemoved, editHover;
        }

        public static BuildAssets Build()
        {
            var assets = new BuildAssets();
            GunManBootstrap.EnsureFolder(MatDir);
            if (AssetDatabase.IsValidFolder(TexDir)) AssetDatabase.ImportAsset(TexDir, ImportAssetOptions.ImportRecursive);

            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToList();
            foreach (var path in paths)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                EnsureTextureImport(path);
                string name = Path.GetFileNameWithoutExtension(path);
                var mat = BuildUtil.CreateMaterial($"{MatDir}/Wood_{name}.mat", "Universal Render Pipeline/Lit", m =>
                {
                    m.SetTexture("_BaseMap", tex);
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Smoothness", 0.22f);
                    m.SetFloat("_Metallic", 0f);
                    m.SetTextureScale("_BaseMap", Vector2.one); // tiling comes from the generated UVs (metres)
                });
                assets.woods.Add(mat);
                assets.names.Add(name);
            }
            assets.defaultIndex = Mathf.Max(0, assets.names.IndexOf(DefaultWood));
            if (assets.woods.Count == 0) Debug.LogWarning($"[GunMan] no wood textures found in {TexDir} – the build system falls back to a plain material");

            assets.ghostValid = Transparent("Ghost_Valid", new Color(0.3f, 1f, 0.45f, 0.35f));
            assets.ghostInvalid = Transparent("Ghost_Invalid", new Color(1f, 0.25f, 0.2f, 0.35f));
            assets.editPresent = Transparent("Edit_Present", new Color(0.3f, 0.9f, 1f, 0.22f));
            assets.editRemoved = Transparent("Edit_Removed", new Color(1f, 0.3f, 0.2f, 0.28f));
            assets.editHover = Transparent("Edit_Hover", new Color(1f, 0.9f, 0.3f, 0.55f));
            Debug.Log($"[GunMan] Build kit: {assets.woods.Count} wood materials (default {DefaultWood} = #{assets.defaultIndex})");
            return assets;
        }

        static Material Transparent(string name, Color color)
        {
            return BuildUtil.CreateMaterial($"{MatDir}/{name}.mat", "Universal Render Pipeline/Lit", m =>
            {
                GunManBootstrap.SetTransparent(m);
                m.SetColor("_BaseColor", color);
                m.SetColor("_Color", color); // legacy alias, keeps the serialised asset stable
                m.SetFloat("_Smoothness", 0.1f);
                m.SetFloat("_Metallic", 0f);
            });
        }

        static void EnsureTextureImport(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            bool dirty = false;
            if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
            if (ti.anisoLevel < 4) { ti.anisoLevel = 4; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }

        /// <summary>Wires the assets into a <see cref="BuildSystem"/> component.</summary>
        public static void Apply(BuildSystem system, BuildAssets assets)
        {
            system.woodMaterials = assets.woods.ToArray();
            system.woodNames = assets.names.ToArray();
            system.defaultMaterialIndex = assets.defaultIndex;
            system.ghostValidMaterial = assets.ghostValid;
            system.ghostInvalidMaterial = assets.ghostInvalid;
            system.editPresentMaterial = assets.editPresent;
            system.editRemovedMaterial = assets.editRemoved;
            system.editHoverMaterial = assets.editHover;
        }
    }
}
