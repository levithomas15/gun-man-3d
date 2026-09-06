using UnityEngine;

namespace GunMan
{
    /// <summary>Very small camera shake helper driven by explosions.</summary>
    public class CameraShake : MonoBehaviour
    {
        static CameraShake _instance;
        float _amplitude;
        float _timeLeft;
        float _duration;
        Vector3 _baseLocalPos;

        void Awake()
        {
            _instance = this;
            _baseLocalPos = transform.localPosition;
        }

        public static void Shake(float amplitude, float duration, Vector3 origin, float maxDistance)
        {
            if (_instance == null) return;
            float d = Vector3.Distance(_instance.transform.position, origin);
            float falloff = Mathf.Clamp01(1f - d / Mathf.Max(1f, maxDistance));
            if (falloff <= 0f) return;
            _instance._amplitude = Mathf.Max(_instance._amplitude, amplitude * falloff);
            _instance._timeLeft = Mathf.Max(_instance._timeLeft, duration);
            _instance._duration = duration;
        }

        void LateUpdate()
        {
            if (_timeLeft <= 0f) return;
            _timeLeft -= Time.deltaTime;
            float k = _timeLeft / Mathf.Max(0.01f, _duration);
            var offset = Random.insideUnitSphere * _amplitude * k;
            transform.localPosition = _baseLocalPos + offset;
            if (_timeLeft <= 0f)
            {
                transform.localPosition = _baseLocalPos;
                _amplitude = 0f;
            }
        }
    }
}
