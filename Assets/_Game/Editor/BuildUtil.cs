using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GunMan.EditorTools
{
    /// <summary>Shared helpers for the content builders.</summary>
    public static class BuildUtil
    {
        public const string PrefabDir = "Assets/_Game/Prefabs";
        public const string SceneDir = "Assets/_Game/Scenes";
        public const string FxMatDir = "Assets/_Game/Materials/FX";
        public const string AnimDir = "Assets/_Game/Animation";
        public const string GunsFbx = "Assets/ThirdParty/StylooGuns/FBX";
        public const string VillageFbx = "Assets/ThirdParty/MedievalVillage/FBX";
        public const string UalFbx = "Assets/ThirdParty/UniversalAnimationLibrary/UAL1_Standard.fbx";
        public const string CarsPrefabDir = "Assets/Awb-Free Low Poly Vehicles/Prefabs";
        public const string DriftDir = "Assets/Zukomazi - Pro Drift Controller v1";

        public static string PreviewDir =>
            System.Environment.GetEnvironmentVariable("GUNMAN_PREVIEW_DIR") is string s && !string.IsNullOrEmpty(s)
                ? s
                : Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Logs", "previews");

        public static GameObject LoadModel(string folder, string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.fbx");
            if (go == null) Debug.LogWarning($"[GunMan] model not found: {folder}/{name}.fbx");
            return go;
        }

        public static GameObject Instantiate(GameObject asset, Transform parent = null, string name = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (parent != null) go.transform.SetParent(parent, false);
            if (!string.IsNullOrEmpty(name)) go.name = name;
            return go;
        }

        /// <summary>
        /// World-space AABB computed from mesh bounds and the current transforms (Renderer.bounds is not reliable
        /// right after transform changes in batch/edit mode).
        /// </summary>
        public static Bounds WorldBounds(GameObject go)
        {
            bool has = false;
            var result = new Bounds(go.transform.position, Vector3.zero);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Encapsulate(ref result, ref has, mf.sharedMesh.bounds, mf.transform.localToWorldMatrix);
            }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                Encapsulate(ref result, ref has, smr.sharedMesh.bounds, smr.transform.localToWorldMatrix);
            }
            return result;
        }

        static void Encapsulate(ref Bounds result, ref bool has, Bounds local, Matrix4x4 m)
        {
            var min = local.min; var max = local.max;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                var w = m.MultiplyPoint3x4(c);
                if (!has) { result = new Bounds(w, Vector3.zero); has = true; }
                else result.Encapsulate(w);
            }
        }

        /// <summary>Move the object so that the bottom-centre of its render bounds sits on <paramref name="anchor"/>.</summary>
        public static void AlignBottomCenter(GameObject go, Vector3 anchor)
        {
            var b = WorldBounds(go);
            var bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.position += anchor - bottomCenter;
        }

        public static void AlignCenter(GameObject go, Vector3 anchor)
        {
            var b = WorldBounds(go);
            go.transform.position += anchor - b.center;
        }

        public static Material CreateMaterial(string path, string shaderName, System.Action<Material> setup)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception($"Shader not found: {shaderName}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            setup?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void SetupParticleTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_SrcBlend", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_BlendOp", (float)BlendOp.Add);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        public static GameObject SavePrefab(GameObject instance, string name)
        {
            GunManBootstrap.EnsureFolder(PrefabDir);
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);
            if (!success) Debug.LogError($"[GunMan] failed to save prefab {path}");
            Object.DestroyImmediate(instance);
            return prefab;
        }

        public static int EnsureLayer(string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
            for (int i = 6; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }
            return 0;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        public static void SetStaticRecursive(GameObject go, bool isStatic)
        {
            var flags = isStatic
                ? StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic
                : 0;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            foreach (Transform t in go.transform) SetStaticRecursive(t.gameObject, isStatic);
        }

        public static Texture2D DefaultParticleTexture()
        {
            return AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        }

        public static float RandRange(System.Random rng, float min, float max) => (float)(min + rng.NextDouble() * (max - min));
    }
}
