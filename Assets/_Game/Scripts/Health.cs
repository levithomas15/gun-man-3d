using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace GunMan
{
    /// <summary>
    /// Generic health component. Handles death, optional respawn and forwards events.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;
        [Tooltip("< 0 = never respawn, 0 = destroy on death, > 0 = respawn after seconds")]
        public float respawnDelay = -1f;
        public bool applyImpactForceToRigidbody = true;

        public UnityEvent<DamageInfo> onDamaged = new UnityEvent<DamageInfo>();
        public UnityEvent<DamageInfo> onDied = new UnityEvent<DamageInfo>();
        public UnityEvent onRespawned = new UnityEvent();

        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        Vector3 _spawnPos;
        Quaternion _spawnRot;
        Rigidbody _rb;

        void Awake()
        {
            Current = maxHealth;
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
            _rb = GetComponent<Rigidbody>();
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead) return;

            if (applyImpactForceToRigidbody && _rb != null && info.Force > 0f)
            {
                if (info.IsExplosion)
                    _rb.AddForceAtPosition(info.Direction * info.Force, info.Point, ForceMode.Impulse);
                else
                    _rb.AddForceAtPosition(info.Direction * info.Force, info.Point, ForceMode.Impulse);
            }

            Current -= info.Amount;
            onDamaged.Invoke(info);
            if (Current <= 0f)
            {
                IsDead = true;
                onDied.Invoke(info);
                if (respawnDelay == 0f) Destroy(gameObject);
                else if (respawnDelay > 0f) StartCoroutine(RespawnRoutine());
            }
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);
            transform.SetPositionAndRotation(_spawnPos, _spawnRot);
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            Current = maxHealth;
            IsDead = false;
            onRespawned.Invoke();
        }

        public void ResetHealth()
        {
            Current = maxHealth;
            IsDead = false;
        }
    }
}
