using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace GunMan
{
    /// <summary>
    /// Wandering animated character (Universal Animation Library mannequin). Reacts to hits, dies and respawns.
    /// Animator parameters: Speed (float), Hit (trigger), Die (trigger), Revive (trigger).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class NpcCharacter : MonoBehaviour
    {
        public float wanderRadius = 14f;
        public float walkSpeed = 1.6f;
        public float jogSpeed = 3.2f;
        public Vector2 idleTimeRange = new Vector2(1.5f, 4.5f);
        public Animator animator;
        public Renderer[] tintRenderers;
        public Color[] palette =
        {
            new Color(0.85f, 0.25f, 0.2f), new Color(0.2f, 0.45f, 0.85f), new Color(0.25f, 0.7f, 0.3f),
            new Color(0.9f, 0.7f, 0.2f), new Color(0.6f, 0.3f, 0.7f), new Color(0.9f, 0.5f, 0.15f),
        };

        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int HitParam = Animator.StringToHash("Hit");
        static readonly int DieParam = Animator.StringToHash("Die");
        static readonly int ReviveParam = Animator.StringToHash("Revive");

        NavMeshAgent _agent;
        Health _health;
        Collider[] _colliders;
        float _idleUntil;
        float _currentSpeed;
        Vector3 _home;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _colliders = GetComponentsInChildren<Collider>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _home = transform.position;
            _health.onDamaged.AddListener(OnDamaged);
            _health.onDied.AddListener(OnDied);
            _health.onRespawned.AddListener(OnRespawned);
        }

        void Start()
        {
            ApplyTint();
            if (_agent != null)
            {
                _agent.speed = walkSpeed;
                _agent.acceleration = 8f;
                _agent.angularSpeed = 240f;
                _agent.stoppingDistance = 0.4f;
                if (!_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 4f, NavMesh.AllAreas))
                    _agent.Warp(hit.position);
            }
            _idleUntil = Time.time + Random.Range(0.2f, 2f);
        }

        void ApplyTint()
        {
            if (tintRenderers == null || tintRenderers.Length == 0 || palette.Length == 0) return;
            var color = palette[Random.Range(0, palette.Length)];
            var block = new MaterialPropertyBlock();
            foreach (var r in tintRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }

        void Update()
        {
            if (_health.IsDead) return;
            if (_agent == null || !_agent.isOnNavMesh)
            {
                animator?.SetFloat(SpeedParam, 0f);
                return;
            }

            bool arrived = !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.05f;
            if (arrived && Time.time >= _idleUntil)
            {
                PickNewDestination();
            }

            float target = arrived ? 0f : (_agent.speed >= jogSpeed - 0.01f ? 1f : 0.5f);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, Time.deltaTime * 3f);
            animator?.SetFloat(SpeedParam, _currentSpeed);
        }

        void PickNewDestination()
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 random = _home + new Vector3(Random.Range(-wanderRadius, wanderRadius), 0f, Random.Range(-wanderRadius, wanderRadius));
                if (NavMesh.SamplePosition(random, out var hit, 3f, NavMesh.AllAreas))
                {
                    _agent.speed = Random.value < 0.25f ? jogSpeed : walkSpeed;
                    _agent.SetDestination(hit.position);
                    _idleUntil = Time.time + Random.Range(idleTimeRange.x, idleTimeRange.y) + 3f;
                    return;
                }
            }
            _idleUntil = Time.time + 1f;
        }

        void OnDamaged(DamageInfo info)
        {
            if (_health.IsDead) return;
            animator?.SetTrigger(HitParam);
            if (_agent != null && _agent.isOnNavMesh)
            {
                // flee a bit from the shooter
                Vector3 away = (transform.position - info.Point).normalized;
                if (info.Source != null) away = (transform.position - info.Source.transform.position).normalized;
                Vector3 target = transform.position + away * 6f;
                if (NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas))
                {
                    _agent.speed = jogSpeed;
                    _agent.SetDestination(hit.position);
                }
            }
        }

        void OnDied(DamageInfo info)
        {
            animator?.ResetTrigger(HitParam);
            animator?.SetTrigger(DieParam);
            animator?.SetFloat(SpeedParam, 0f);
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            foreach (var c in _colliders) c.enabled = false;
            StartCoroutine(SinkAfter(3f));
        }

        IEnumerator SinkAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            float t = 0f;
            var start = transform.position;
            while (t < 1.2f && _health.IsDead)
            {
                t += Time.deltaTime;
                transform.position = start + Vector3.down * t * 0.8f;
                yield return null;
            }
        }

        void OnRespawned()
        {
            StopAllCoroutines();
            foreach (var c in _colliders) c.enabled = true;
            if (_agent != null)
            {
                Vector3 spawn = _home + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                if (NavMesh.SamplePosition(spawn, out var hit, 4f, NavMesh.AllAreas)) spawn = hit.position;
                _agent.Warp(spawn);
                _agent.isStopped = false;
            }
            animator?.SetTrigger(ReviveParam);
            _currentSpeed = 0f;
            ApplyTint();
            _idleUntil = Time.time + 1f;
        }
    }
}
