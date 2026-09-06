using System.Collections;
using UnityEngine;

namespace GunMan
{
    /// <summary>
    /// Rocket or grenade. Explodes on impact (rockets) or after a fuse time (grenades).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public float explosionRadius = 5f;
        public float explosionDamage = 120f;
        public float explosionForce = 18f;
        public bool explodeOnImpact = true;
        [Tooltip("Seconds until it explodes on its own. <= 0 disables the fuse.")]
        public float fuseSeconds = 0f;
        public float maxLifetime = 12f;
        public bool useGravity = false;
        [Range(0f, 1f)] public float selfDamageFactor = 0.3f;
        public ParticleSystem trail;

        Rigidbody _rb;
        GameObject _owner;
        Health _ownerHealth;
        bool _exploded;
        float _launchTime;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = useGravity;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Launch(Vector3 velocity, GameObject owner)
        {
            _owner = owner;
            _ownerHealth = owner != null ? owner.GetComponentInParent<Health>() : null;
            _launchTime = Time.time;
            _rb.linearVelocity = velocity;
            if (!useGravity) transform.rotation = Quaternion.LookRotation(velocity);
            if (fuseSeconds > 0f) StartCoroutine(Fuse());
            Destroy(gameObject, maxLifetime);
        }

        IEnumerator Fuse()
        {
            yield return new WaitForSeconds(fuseSeconds);
            Explode();
        }

        void FixedUpdate()
        {
            if (!useGravity && _rb.linearVelocity.sqrMagnitude > 0.1f)
                transform.rotation = Quaternion.LookRotation(_rb.linearVelocity);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_exploded) return;
            if (explodeOnImpact)
            {
                Explode();
            }
            else
            {
                FxLibrary.PlayAt(ProceduralAudio.Click("bounce", 500f, 0.05f), transform.position, 0.4f);
            }
        }

        public void Explode()
        {
            if (_exploded) return;
            _exploded = true;
            Vector3 center = transform.position;

            var hits = Physics.OverlapSphere(center, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
            var damagedRoots = new System.Collections.Generic.HashSet<IDamageable>();
            foreach (var col in hits)
            {
                Vector3 closest = col.ClosestPoint(center);
                float dist = Vector3.Distance(center, closest);
                float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
                if (falloff <= 0f) continue;
                Vector3 dir = (closest - center);
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
                dir.Normalize();

                var dmg = new DamageInfo
                {
                    Amount = explosionDamage * falloff,
                    Point = closest,
                    Direction = (dir + Vector3.up * 0.4f).normalized,
                    Normal = -dir,
                    Force = explosionForce * falloff,
                    Source = _owner,
                    IsExplosion = true,
                };

                var target = col.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    // the shooter only takes a fraction of their own explosion damage (rocket jumps stay survivable)
                    if (_ownerHealth != null && ReferenceEquals(target, _ownerHealth)) dmg.Amount *= selfDamageFactor;
                    if (damagedRoots.Add(target)) target.TakeDamage(dmg);
                }
                else if (col.attachedRigidbody != null)
                {
                    col.attachedRigidbody.AddExplosionForce(explosionForce * 60f, center, explosionRadius, 0.6f, ForceMode.Impulse);
                }
                var player = col.GetComponentInParent<PlayerController>();
                if (player != null) player.AddImpulse(dmg.Direction * explosionForce * 0.5f * falloff);
            }

            FxLibrary.Explosion(center, explosionRadius);
            if (trail != null)
            {
                trail.transform.SetParent(null, true);
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(trail.gameObject, 3f);
            }
            Destroy(gameObject);
        }
    }
}
