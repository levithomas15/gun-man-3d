using System.Collections;
using UnityEngine;

namespace GunMan
{
    /// <summary>
    /// Shooting-range target: falls over when its health is depleted and pops back up after a delay.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PopupTarget : MonoBehaviour
    {
        public Transform pivot;
        public float fallAngle = 88f;
        public float fallDuration = 0.35f;
        public float riseDuration = 0.6f;

        public static int TotalKnockdowns { get; private set; }

        Health _health;
        Coroutine _anim;

        void Awake()
        {
            _health = GetComponent<Health>();
            if (pivot == null) pivot = transform;
            _health.onDied.AddListener(_ => Fall());
            _health.onRespawned.AddListener(Rise);
        }

        void Fall()
        {
            TotalKnockdowns++;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Rotate(0f, fallAngle, fallDuration));
            FxLibrary.PlayAt(ProceduralAudio.Click("target-hit", 300f, 0.15f), transform.position, 0.8f);
        }

        void Rise()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Rotate(fallAngle, 0f, riseDuration));
        }

        IEnumerator Rotate(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                pivot.localRotation = Quaternion.Euler(Mathf.Lerp(from, to, k), 0f, 0f);
                yield return null;
            }
            pivot.localRotation = Quaternion.Euler(to, 0f, 0f);
        }
    }
}
