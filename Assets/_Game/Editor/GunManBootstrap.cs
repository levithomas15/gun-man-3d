using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GunMan.EditorTools
{
    /// <summary>
    /// Step 1: prepares the imported third-party assets (materials, texture extraction, URP conversion).
    /// Run via menu "GunMan/1. Prepare Assets" or with -executeMethod GunMan.EditorTools.GunManBootstrap.Prepare
    /// </summary>
    public static class GunManBootstrap
    {
        const string VillageTex = "Assets/ThirdParty/MedievalVillage/Textures";
        const string VillageFbx = "Assets/ThirdParty/MedievalVillage/FBX";
        const string GunsFbx = "Assets/ThirdParty/StylooGuns/FBX";
        const string GunsTex = "Assets/ThirdParty/StylooGuns/Textures";
        const string GunsMat = "Assets/_Game/Materials/Guns";
        const string UalFolder = "Assets/ThirdParty/UniversalAnimationLibrary";

        [MenuItem("GunMan/1. Prepare Assets")]
        public static void Prepare()
        {
            Debug.Log("[GunMan] Bootstrap: preparing assets…");
            EnsureFolder("Assets/_Game/Materials");
            EnsureFolder(GunManAssetPostprocessor.VillageMaterialsFolder);
            EnsureFolder(GunsMat);

            AssetDatabase.ImportAsset(VillageTex, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            CreateVillageMaterials();
            AssetDatabase.SaveAssets();

            // Re-import the village models so OnAssignMaterialModel picks up the new materials.
            AssetDatabase.ImportAsset(VillageFbx, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            PrepareGuns();
            PrepareAnimationLibrary();
            ConvertBuiltInMaterialsToUrp();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GunMan] Bootstrap finished.");
        }

        // ------------------------------------------------------------------ village

        static void CreateVillageMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new Exception("URP Lit shader not found");

            // name -> (baseColor, normal, roughness/orm, tint, smoothness, metallic)
            Create("MI_WoodTrim", "T_WoodTrim_BaseColor", "T_WoodTrim_Normal", 0.25f, 0f);
            Create("MI_WoodTrim_Wear", "T_WoodTrim_BaseColor", "T_WoodTrim_Normal", 0.2f, 0f, new Color(0.9f, 0.86f, 0.8f));
            Create("MI_Plaster", "T_Plaster_BaseColor", "T_Plaster_Normal", 0.15f, 0f);
            Create("MI_RockTrim", "T_RockTrim_BaseColor", "T_RockTrim_Normal", 0.2f, 0f);
            Create("MI_Brick", "T_Brick_BaseColor", "T_Brick_Normal", 0.2f, 0f);
            Create("MI_RedBrick", "T_RedBrick_BaseColor", "T_Brick_Normal", 0.2f, 0f);
            Create("MI_RoundTiles", "T_RoundTiles_BaseColor", "T_RoundTiles_Normal", 0.3f, 0f);
            Create("MI_UnevenBrick", "T_UnevenBrick_BaseColor", "T_UnevenBrick_Normal", 0.2f, 0f);
            Create("MI_MetalOrnaments", null, null, 0.6f, 0.85f, new Color(0.25f, 0.25f, 0.28f));

            // glass: transparent bluish
            var glass = CreateOrLoad("MI_WindowGlass", lit);
            glass.SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.9f, 0.45f));
            glass.SetFloat("_Smoothness", 0.95f);
            glass.SetFloat("_Metallic", 0.1f);
            SetTransparent(glass);
            EditorUtility.SetDirty(glass);

            // vines: alpha clipped leaves
            var vine = CreateOrLoad("MI_Vine", lit);
            var leaf = AssetDatabase.LoadAssetAtPath<Texture2D>($"{VillageTex}/T_VineLeaf.png");
            vine.SetTexture("_BaseMap", leaf);
            vine.SetColor("_BaseColor", new Color(0.75f, 0.95f, 0.65f, 1f));
            vine.SetFloat("_Smoothness", 0.2f);
            vine.SetFloat("_AlphaClip", 1f);
            vine.SetFloat("_Cutoff", 0.4f);
            vine.EnableKeyword("_ALPHATEST_ON");
            vine.SetFloat("_Cull", (float)CullMode.Off);
            vine.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(vine);

            // ground / plaza materials used by the builder
            var ground = CreateOrLoad("Ground_Grass", lit);
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>($"{VillageTex}/T_Noise_Terrain.png");
            ground.SetTexture("_BaseMap", noise);
            ground.SetColor("_BaseColor", new Color(0.42f, 0.55f, 0.28f, 1f));
            ground.SetTextureScale("_BaseMap", new Vector2(40f, 40f));
            ground.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(ground);

            var dirt = CreateOrLoad("Ground_Dirt", lit);
            dirt.SetTexture("_BaseMap", noise);
            dirt.SetColor("_BaseColor", new Color(0.5f, 0.42f, 0.3f, 1f));
            dirt.SetTextureScale("_BaseMap", new Vector2(30f, 30f));
            dirt.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(dirt);

            void Create(string name, string baseTex, string normalTex, float smooth, float metal, Color? tint = null)
            {
                var m = CreateOrLoad(name, lit);
                var b = baseTex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{VillageTex}/{baseTex}.png") : null;
                var n = normalTex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{VillageTex}/{normalTex}.png") : null;
                if (baseTex != null && b == null) Debug.LogWarning($"[GunMan] texture missing: {baseTex}");
                m.SetTexture("_BaseMap", b);
                m.SetTexture("_BumpMap", n);
                if (n != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
                m.SetColor("_BaseColor", tint ?? Color.white);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                EditorUtility.SetDirty(m);
            }
        }

        static Material CreateOrLoad(string name, Shader shader)
        {
            string path = $"{GunManAssetPostprocessor.VillageMaterialsFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        /// <summary>
        /// Puts a URP/Lit material into alpha-blended transparency. The blend factors mirror what URP's own
        /// material validation writes for a transparent surface with _BlendModePreserveSpecular (premultiplied
        /// alpha, no depth/shadow pass) – otherwise the editor rewrites the asset on every reimport.
        /// </summary>
        public static void SetTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("SHADOWCASTER", false);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        // ------------------------------------------------------------------ guns

        static void PrepareGuns()
        {
            EnsureFolder(GunsTex);
            var guids = AssetDatabase.FindAssets("t:Model", new[] { GunsFbx });
            int extractedTex = 0, extractedMat = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;

                // 1) embedded textures -> Textures folder (Unity remaps the materials automatically)
                try
                {
                    if (mi.ExtractTextures(GunsTex)) extractedTex++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GunMan] ExtractTextures failed for {path}: {e.Message}");
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(GunsTex, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(GunsFbx, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            // 2) embedded materials -> Materials/Guns as editable assets
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;
                string model = Path.GetFileNameWithoutExtension(path);
                var mats = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToList();
                bool changed = false;
                foreach (var mat in mats)
                {
                    string dest = $"{GunsMat}/{model}_{Sanitize(mat.name)}.mat";
                    if (AssetDatabase.LoadAssetAtPath<Material>(dest) != null) continue;
                    string err = AssetDatabase.ExtractAsset(mat, dest);
                    if (!string.IsNullOrEmpty(err))
                    {
                        Debug.LogWarning($"[GunMan] ExtractAsset failed {path}/{mat.name}: {err}");
                        continue;
                    }
                    extractedMat++;
                    changed = true;
                }
                if (changed)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }

            // 3) enforce the author's material recommendations on the extracted materials
            foreach (var mg in AssetDatabase.FindAssets("t:Material", new[] { GunsMat }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(mg));
                if (mat == null) continue;
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.18f);
                if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null) mat.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(mat);
            }
            Debug.Log($"[GunMan] Guns prepared: textures extracted from {extractedTex} models, {extractedMat} materials extracted.");
        }

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace('.', '_');
        }

        // ------------------------------------------------------------------ animation library

        static void PrepareAnimationLibrary()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { UalFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;
                var clips = mi.defaultClipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    Debug.LogWarning($"[GunMan] no clips found in {path}");
                    continue;
                }
                foreach (var clip in clips)
                {
                    string name = clip.takeName.Replace("Armature|", "");
                    clip.name = name;
                    clip.loopTime = name.EndsWith("_Loop") || name == "Sword_Idle";
                    clip.loopPose = false;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                }
                mi.clipAnimations = clips;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.bakeAxisConversion = true;
                mi.SaveAndReimport();

                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    Debug.LogWarning($"[GunMan] Humanoid avatar invalid for {path} -> falling back to Generic rig");
                    mi.animationType = ModelImporterAnimationType.Generic;
                    mi.SaveAndReimport();
                }
                var importedClips = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<AnimationClip>().ToList();
                Debug.Log($"[GunMan] {Path.GetFileName(path)}: {importedClips.Count} clips, rig={mi.animationType}, e.g. {string.Join(", ", importedClips.Take(6).Select(c => c.name))}");
            }
        }

        // ------------------------------------------------------------------ built-in -> URP

        [MenuItem("GunMan/Convert Built-in materials to URP")]
        public static void ConvertBuiltInMaterialsToUrp()
        {
            // Try the official converter first (handles all shader mappings and blend modes).
            try
            {
                var convertersType = Type.GetType("UnityEditor.Rendering.Universal.Converters, Unity.RenderPipelines.Universal.Editor");
                if (convertersType != null)
                {
                    var containerIdType = Type.GetType("UnityEditor.Rendering.Universal.ConverterContainerId, Unity.RenderPipelines.Universal.Editor");
                    var converterIdType = Type.GetType("UnityEditor.Rendering.Universal.ConverterId, Unity.RenderPipelines.Universal.Editor");
                    var filterType = Type.GetType("UnityEditor.Rendering.Universal.ConverterFilter, Unity.RenderPipelines.Universal.Editor");
                    if (containerIdType != null && converterIdType != null && filterType != null)
                    {
                        var listType = typeof(List<>).MakeGenericType(converterIdType);
                        var list = (System.Collections.IList)Activator.CreateInstance(listType);
                        list.Add(Enum.Parse(converterIdType, "Material"));
                        var method = convertersType.GetMethod("RunInBatchMode", BindingFlags.Public | BindingFlags.Static, null,
                            new[] { containerIdType, listType, filterType }, null);
                        if (method != null)
                        {
                            method.Invoke(null, new object[] { Enum.Parse(containerIdType, "BuiltInToURP"), list, Enum.Parse(filterType, "Inclusive") });
                            Debug.Log("[GunMan] Official URP material converter executed.");
                        }
                    }
                    else
                    {
                        var legacyType = Type.GetType("UnityEditor.Rendering.Universal.ContainerType, Unity.RenderPipelines.Universal.Editor");
                        var method = legacyType != null ? convertersType.GetMethod("RunInBatchMode", new[] { legacyType }) : null;
                        if (method != null)
                        {
                            method.Invoke(null, new[] { Enum.Parse(legacyType, "BuiltInToURP") });
                            Debug.Log("[GunMan] Official URP material converter (legacy API) executed.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GunMan] Official converter failed: {e.Message}");
            }

            // Manual fallback for anything still on a built-in shader.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;
                string sn = mat.shader.name;
                if (sn.StartsWith("Universal Render Pipeline") || sn.StartsWith("Skybox") || sn.StartsWith("Shader Graphs") || sn.StartsWith("Hidden")) continue;

                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Vector2 scale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
                Vector2 offset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : (mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor") : Color.white);
                Texture bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
                Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                Color emission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                float mode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0f;
                bool wasEmissive = mat.IsKeywordEnabled("_EMISSION");
                float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

                Shader target = lit;
                if (sn.StartsWith("Particles/") || sn.StartsWith("Legacy Shaders/Particles")) target = particlesUnlit;
                else if (sn.StartsWith("Unlit/") || sn.StartsWith("Mobile/Unlit") || sn.Contains("Skidmarks")) target = unlit;
                if (target == null) continue;

                mat.shader = target;
                if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", mainTex); mat.SetTextureScale("_BaseMap", scale); mat.SetTextureOffset("_BaseMap", offset); }
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (target == lit)
                {
                    mat.SetTexture("_BumpMap", bump);
                    if (bump != null) mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_MetallicGlossMap", metallicMap);
                    if (metallicMap != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_Metallic", metallic);
                    mat.SetFloat("_Smoothness", gloss);
                    mat.SetTexture("_EmissionMap", emissionMap);
                    mat.SetColor("_EmissionColor", emission);
                    if (wasEmissive && emission.maxColorComponent > 0.01f) mat.EnableKeyword("_EMISSION");
                    if (mode >= 2f) { SetTransparent(mat); }
                    else if (mode >= 1f)
                    {
                        mat.SetFloat("_AlphaClip", 1f);
                        mat.SetFloat("_Cutoff", cutoff);
                        mat.EnableKeyword("_ALPHATEST_ON");
                        mat.renderQueue = (int)RenderQueue.AlphaTest;
                    }
                }
                else if (target == particlesUnlit || target == unlit)
                {
                    if (sn.Contains("Additive") || sn.Contains("Alpha") || sn.Contains("Transparent") || sn.Contains("Skidmarks"))
                        SetTransparent(mat);
                }
                EditorUtility.SetDirty(mat);
                converted++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[GunMan] Manual URP material fallback converted {converted} materials.");
        }

        // ------------------------------------------------------------------ helpers

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
