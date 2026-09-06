using System.Collections;
using UnityEngine;

namespace GunMan
{
    /// <summary>
    /// Scene singleton that spawns visual/audio effects. The referenced prefabs are created by the editor builder.
    /// </summary>
    public class FxLibrary : MonoBehaviour
    {
        public static FxLibrary Instance { get; private set; }

        public GameObject impactPrefab;      // particle burst (sparks / dust)
        public GameObject explosionPrefab;   // particle burst (fire + smoke)
        public GameObject bulletHolePrefab;  // small dark quad
        public Material tracerMaterial;
        public float bulletHoleLifetime = 25f;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Impact(Vector3 point, Vector3 normal, Collider hitCollider, bool leaveHole = true)
        {
            var fx = Instance;
            if (fx == null) return;
            if (fx.impactPrefab != null)
            {
                var go = Instantiate(fx.impactPrefab, point + normal * 0.02f, Quaternion.LookRotation(normal));
                Destroy(go, 2f);
            }
            if (leaveHole && fx.bulletHolePrefab != null && hitCollider != null && hitCollider.attachedRigidbody == null)
            {
                var hole = Instantiate(fx.bulletHolePrefab, point + normal * 0.005f,
                    Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
                hole.transform.SetParent(hitCollider.transform, true);
                Destroy(hole, fx.bulletHoleLifetime);
            }
            PlayAt(ProceduralAudio.Impact(), point, 0.35f, Random.Range(0.8f, 1.2f));
        }

        public static void Explosion(Vector3 point, float radius)
        {
            var fx = Instance;
            if (fx != null && fx.explosionPrefab != null)
            {
                var go = Instantiate(fx.explosionPrefab, point, Quaternion.identity);
                go.transform.localScale = Vector3.one * Mathf.Max(0.5f, radius / 5f);
                Destroy(go, 4f);
            }
            var light = new GameObject("ExplosionLight").AddComponent<Light>();
            light.transform.position = point + Vector3.up * 0.5f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.65f, 0.3f);
            light.intensity = 12f;
            light.range = radius * 3f;
            if (fx != null) fx.StartCoroutine(FadeLight(light, 0.35f));
            PlayAt(ProceduralAudio.Explosion(), point, 1f, Random.Range(0.9f, 1.1f), 60f);
            CameraShake.Shake(0.35f * Mathf.Clamp01(radius / 4f), 0.4f, point, radius * 4f);
        }

        static IEnumerator FadeLight(Light l, float duration)
        {
            float start = l.intensity;
            float t = 0f;
            while (t < duration && l != null)
            {
                t += Time.deltaTime;
                l.intensity = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            if (l != null) Destroy(l.gameObject);
        }

        public static void Tracer(Vector3 from, Vector3 to, float width = 0.02f, float life = 0.06f)
        {
            var fx = Instance;
            if (fx == null || fx.tracerMaterial == null) return;
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = fx.tracerMaterial;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = width;
            lr.endWidth = width * 0.5f;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Destroy(go, life);
        }

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 40f)
        {
            if (clip == null) return;
            var go = new GameObject("OneShotAudio");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 1f;
            src.minDistance = 2f;
            src.maxDistance = maxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }
    }
}
