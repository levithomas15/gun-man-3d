using System.IO;
using UnityEditor;
using UnityEngine;

namespace GunMan.EditorTools
{
    /// <summary>
    /// Import rules for the third-party packs (Styloo guns, Quaternius village + animation library).
    /// </summary>
    public class GunManAssetPostprocessor : AssetPostprocessor
    {
        public const string GunsFolder = "Assets/ThirdParty/StylooGuns/";
        public const string VillageFolder = "Assets/ThirdParty/MedievalVillage/";
        public const string UalFolder = "Assets/ThirdParty/UniversalAnimationLibrary/";
        public const string VillageMaterialsFolder = "Assets/_Game/Materials/Village";

        void OnPreprocessModel()
        {
            var mi = (ModelImporter)assetImporter;
            if (assetPath.StartsWith(GunsFolder))
            {
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                mi.bakeAxisConversion = true;
                mi.addCollider = false;
                mi.importBlendShapes = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.isReadable = true;
            }
            else if (assetPath.StartsWith(VillageFolder))
            {
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                mi.bakeAxisConversion = true;
                mi.addCollider = true;
                mi.importBlendShapes = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.isReadable = true;
                mi.generateSecondaryUV = false;
            }
            else if (assetPath.StartsWith(UalFolder))
            {
                mi.importAnimation = true;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.bakeAxisConversion = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
                mi.importConstraints = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.optimizeGameObjects = false;
            }
            else if (assetPath.StartsWith("Assets/Awb-Free Low Poly Vehicles/"))
            {
                mi.isReadable = true;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(UalFolder)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var clip in clips)
            {
                string name = clip.takeName.Replace("Armature|", "");
                clip.name = name;
                bool loop = name.EndsWith("_Loop") || name == "Sword_Idle";
                clip.loopTime = loop;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            bool isNormal = file.EndsWith("_Normal") || file.ToLowerInvariant().Contains("_normal");
            if (assetPath.StartsWith(VillageFolder) || assetPath.StartsWith(GunsFolder) || assetPath.StartsWith(UalFolder))
            {
                ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !isNormal && !file.EndsWith("_ORM") && !file.EndsWith("_Roughness");
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 2048;
            }
        }

        /// <summary>Village FBX materials are mapped by name to hand-built URP materials.</summary>
        Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!assetPath.StartsWith(VillageFolder)) return null;
            string path = $"{VillageMaterialsFolder}/{material.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            return existing; // null -> Unity keeps its generated material until the bootstrap creates ours
        }

        void OnPostprocessMaterial(Material material)
        {
            if (assetPath.StartsWith(GunsFolder))
            {
                // Author recommends: no metallic, rough surface.
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
                if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null)
                    material.EnableKeyword("_NORMALMAP");
            }
            else if (assetPath.StartsWith(UalFolder))
            {
                if (material.name.Contains("Joints"))
                {
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.2f));
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.5f);
                }
                else
                {
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f));
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
                }
            }
        }
    }
}
