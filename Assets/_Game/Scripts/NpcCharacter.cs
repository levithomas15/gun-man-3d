using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GunMan
{
    public enum NpcRole
    {
        /// <summary>Unarmed, wanders around and flees when shot.</summary>
        Civilian,
        /// <summary>Armed. Patrols peacefully until the <see cref="CombatDirector"/> makes it the active opponent.</summary>
        Soldier,
    }

    /// <summary>
    /// Animated character (Universal Animation Library mannequin) on the NavMesh.
    /// Civilians wander; soldiers carry a <see cref="Weapon"/> in the right hand and, when chosen as the opponent
    /// in combat mode, hunt and shoot the player. Only one NPC attacks at a time (see <see cref="CombatDirector"/>).
    /// Animator parameters: Speed (float), Hit / Die / Revive (triggers), Aiming (bool), Shoot / Reload (triggers);
    /// layer 1 ("Arms") holds the pistol poses and is only enabled for armed NPCs.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class NpcCharacter : MonoBehaviour
    {
        public static readonly List<NpcCharacter> All = new List<NpcCharacter>();

        [Header("Movement")]
        public float wanderRadius = 14f;
        public float walkSpeed = 1.6f;
        public float jogSpeed = 3.2f;
        public Vector2 idleTimeRange = new Vector2(1.5f, 4.5f);
        public float turnSpeed = 420f;

        [Header("Combat")]
        public NpcRole role = NpcRole.Civilian;
        [Tooltip("Weapon attached to the right hand (soldiers)")]
        public Weapon weapon;
        [Tooltip("The opponent tracks the player within this distance, even without line of sight")]
        public float huntRange = 150f;
        public float loseTargetAfter = 7f;
        [Tooltip("Distance the NPC tries to keep while shooting")]
        public float preferredRange = 12f;
        public int burstShots = 3;
        public Vector2 burstPauseRange = new Vector2(0.8f, 1.7f);
        public float eyeHeight = 1.6f;

        [Header("Visuals")]
        public Animator animator;
        public Renderer[] tintRenderers;
        public Color[] palette =
        {
            new Color(0.85f, 0.25f, 0.2f), new Color(0.2f, 0.45f, 0.85f), new Color(0.25f, 0.7f, 0.3f),
            new Color(0.9f, 0.7f, 0.2f), new Color(0.6f, 0.3f, 0.7f), new Color(0.9f, 0.5f, 0.15f),
        };
        public Color[] soldierPalette =
        {
            new Color(0.25f, 0.25f, 0.28f), new Color(0.35f, 0.3f, 0.2f), new Color(0.2f, 0.3f, 0.25f), new Color(0.45f, 0.15f, 0.15f),
        };

        public bool IsArmed => weapon != null && role != NpcRole.Civilian;
        /// <summary>True while this NPC is the active opponent and hunting the player.</summary>
        public bool IsEngaged => _engaged;
        public bool IsOpponent => CombatDirector.Instance != null && CombatDirector.Instance.IsOpponent(this);
        public Health Health => _health;
        public int ShotsFired { get; private set; }

        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int HitParam = Animator.StringToHash("Hit");
        static readonly int DieParam = Animator.StringToHash("Die");
        static readonly int ReviveParam = Animator.StringToHash("Revive");
        static readonly int AimingParam = Animator.StringToHash("Aiming");
        static readonly int ShootParam = Animator.StringToHash("Shoot");
        static readonly int ReloadParam = Animator.StringToHash("Reload");
        const int ArmsLayer = 1;

        NavMeshAgent _agent;
        Health _health;
        Collider[] _colliders;
        float _idleUntil;
        float _currentSpeed;
        Vector3 _home;
        LayerMask _sightMask;

        // combat state
        PlayerController _player;
        Health _playerHealth;
        float _playerSearchTime;
        bool _engaged;
        bool _canSee;
        float _lastSeenTime = -999f;
        Vector3 _lastKnownPos;
        float _nextShotTime;
        int _shotsLeftInBurst;
        float _repositionTime;
        bool _wasReloading;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _colliders = GetComponentsInChildren<Collider>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _home = transform.position;
            _sightMask = ~LayerMask.GetMask("Ignore Raycast");
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
            if (weapon != null)
            {
                weapon.SetOwner(gameObject, _sightMask);
                weapon.infiniteReserve = true;
            }
            SetArmsLayer(IsArmed);
            _idleUntil = Time.time + Random.Range(0.2f, 2f);
            _shotsLeftInBurst = burstShots;
        }

        void ApplyTint()
        {
            if (tintRenderers == null || tintRenderers.Length == 0) return;
            var colors = IsArmed && soldierPalette.Length > 0 ? soldierPalette : palette;
            if (colors.Length == 0) return;
            var color = colors[Random.Range(0, colors.Length)];
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

        void SetArmsLayer(bool on)
        {
            if (animator != null && animator.layerCount > ArmsLayer) animator.SetLayerWeight(ArmsLayer, on ? 1f : 0f);
        }

        void Update()
        {
            if (_health.IsDead) return;
            if (_agent == null || !_agent.isOnNavMesh)
            {
                animator?.SetFloat(SpeedParam, 0f);
                return;
            }

            if (IsArmed && IsOpponent) UpdateCombat();
            else
            {
                if (_engaged) Disengage();
                UpdateWander();
            }

            bool moving = !_agent.isStopped && (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + 0.05f);
            float target = moving ? (_agent.speed >= jogSpeed - 0.01f ? 1f : 0.5f) : 0f;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, Time.deltaTime * 3f);
            animator?.SetFloat(SpeedParam, _currentSpeed);
        }

        // ------------------------------------------------------------------ wandering

        void UpdateWander()
        {
            bool arrived = !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.05f;
            if (arrived && Time.time >= _idleUntil) PickNewDestination();
        }

        void PickNewDestination()
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 random = _home + new Vector3(Random.Range(-wanderRadius, wanderRadius), 0f, Random.Range(-wanderRadius, wanderRadius));
                if (NavMesh.SamplePosition(random, out var hit, 3f, NavMesh.AllAreas))
                {
                    _agent.speed = Random.value < 0.25f ? jogSpeed : walkSpeed;
                    _agent.isStopped = false;
                    _agent.SetDestination(hit.position);
                    _idleUntil = Time.time + Random.Range(idleTimeRange.x, idleTimeRange.y) + 3f;
                    return;
                }
            }
            _idleUntil = Time.time + 1f;
        }

        void MoveAwayFrom(Vector3 threat)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            Vector3 away = (transform.position - threat).normalized;
            Vector3 target = transform.position + away * 6f;
            if (NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas))
            {
                _agent.speed = jogSpeed;
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
                _idleUntil = Time.time + 2f;
            }
        }

        // ------------------------------------------------------------------ combat

        /// <summary>Called by the <see cref="CombatDirector"/> when this NPC becomes the opponent.</summary>
        public void StartAttacking()
        {
            FindPlayer(true);
            if (_player != null)
            {
                _lastKnownPos = _player.transform.position;
                _lastSeenTime = Time.time;
            }
            _engaged = true;
            _repositionTime = 0f;
            _shotsLeftInBurst = burstShots;
            _nextShotTime = Time.time + 0.6f;
        }

        public void StopAttacking()
        {
            if (_engaged) Disengage();
        }

        void FindPlayer(bool force = false)
        {
            if (!force && (_player != null || Time.time < _playerSearchTime)) return;
            _playerSearchTime = Time.time + 1f;
            _player = GameManager.Instance != null ? GameManager.Instance.player : FindAnyObjectByType<PlayerController>();
            _playerHealth = _player != null ? _player.GetComponent<Health>() : null;
        }

        Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

        bool HasLineOfSight(Vector3 targetPoint)
        {
            Vector3 origin = EyePosition;
            Vector3 dir = targetPoint - origin;
            float dist = dir.magnitude;
            if (dist < 0.01f) return true;
            if (!Weapon.RaycastIgnoring(origin, dir / dist, dist + 0.1f, _sightMask, transform, out var hit)) return true;
            return _player != null && hit.collider.transform.IsChildOf(_player.transform);
        }

        void UpdateCombat()
        {
            FindPlayer();
            bool playerAlive = _player != null && (_playerHealth == null || !_playerHealth.IsDead);
            Vector3 targetPoint = playerAlive ? _player.transform.position + Vector3.up * 1.25f : Vector3.zero;
            float dist = playerAlive ? Vector3.Distance(transform.position, _player.transform.position) : float.MaxValue;

            // --- tracking: the opponent always knows roughly where the player is
            _canSee = false;
            if (playerAlive && dist <= huntRange)
            {
                _canSee = HasLineOfSight(targetPoint);
                _lastSeenTime = Time.time;
                _lastKnownPos = _player.transform.position;
            }
            bool hasTarget = playerAlive && Time.time - _lastSeenTime <= loseTargetAfter;
            if (!hasTarget)
            {
                if (_engaged) Disengage();
                UpdateWander();
                return;
            }
            if (!_engaged)
            {
                _engaged = true;
                _repositionTime = 0f;
            }

            // --- movement: close in on the last known position, hold position when the player is in range and visible
            bool holdPosition = _canSee && dist <= preferredRange;
            bool tooClose = _canSee && dist < preferredRange * 0.45f;
            if (tooClose && Time.time >= _repositionTime)
            {
                Vector3 away = (transform.position - _player.transform.position).normalized;
                Vector3 back = transform.position + away * 4f + Vector3.Cross(Vector3.up, away) * Random.Range(-2f, 2f);
                if (NavMesh.SamplePosition(back, out var hit, 3f, NavMesh.AllAreas))
                {
                    _agent.isStopped = false;
                    _agent.speed = jogSpeed;
                    _agent.SetDestination(hit.position);
                }
                _repositionTime = Time.time + 1.5f;
            }
            else if (holdPosition)
            {
                if (!_agent.isStopped || _agent.hasPath)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
            }
            else if (Time.time >= _repositionTime)
            {
                if (NavMesh.SamplePosition(_lastKnownPos, out var hit, 4f, NavMesh.AllAreas))
                {
                    _agent.isStopped = false;
                    _agent.speed = jogSpeed;
                    _agent.SetDestination(hit.position);
                }
                _repositionTime = Time.time + 0.5f;
            }

            // --- facing: turn towards the player while it is visible, otherwise let the agent steer
            _agent.updateRotation = !_canSee;
            if (_canSee)
            {
                Vector3 flat = _player.transform.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
            }
            animator?.SetBool(AimingParam, true);

            // --- shooting
            if (weapon.IsReloading)
            {
                if (!_wasReloading) animator?.SetTrigger(ReloadParam);
                _wasReloading = true;
                return;
            }
            _wasReloading = false;
            if (!_canSee || Time.time < _nextShotTime) return;
            if (dist > weapon.range) return;
            Vector3 aim = targetPoint - weapon.muzzle.position;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            Vector3 aimFlat = new Vector3(aim.x, 0f, aim.z);
            if (Vector3.Angle(fwd, aimFlat) > 12f) return; // still turning
            if (weapon.AmmoInMag <= 0)
            {
                weapon.TryReload();
                return;
            }

            weapon.Fire(weapon.muzzle.position, aim.normalized, transform);
            ShotsFired++;
            animator?.SetTrigger(ShootParam);
            _shotsLeftInBurst--;
            float interval = 60f / Mathf.Max(1f, weapon.roundsPerMinute);
            if (_shotsLeftInBurst <= 0)
            {
                _shotsLeftInBurst = burstShots;
                _nextShotTime = Time.time + interval + Random.Range(burstPauseRange.x, burstPauseRange.y);
            }
            else _nextShotTime = Time.time + interval;
        }

        void Disengage()
        {
            _engaged = false;
            _canSee = false;
            _lastSeenTime = -999f;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.updateRotation = true;
                _agent.isStopped = false;
                _agent.speed = walkSpeed;
            }
            animator?.SetBool(AimingParam, false);
            _idleUntil = Time.time + 0.5f;
        }

        // ------------------------------------------------------------------ damage / death

        void OnDamaged(DamageInfo info)
        {
            if (_health.IsDead) return;
            animator?.SetTrigger(HitParam);
            if (_engaged) return; // the opponent keeps fighting

            // everybody else (civilians and off-duty soldiers) moves away from the shooter
            Vector3 threat = info.Source != null ? info.Source.transform.position : info.Point;
            MoveAwayFrom(threat);
        }

        void OnDied(DamageInfo info)
        {
            animator?.ResetTrigger(HitParam);
            animator?.SetTrigger(DieParam);
            animator?.SetFloat(SpeedParam, 0f);
            animator?.SetBool(AimingParam, false);
            SetArmsLayer(false);
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.updateRotation = true;
            }
            foreach (var c in _colliders) c.enabled = false;
            _engaged = false;
            _canSee = false;
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
                _agent.updateRotation = true;
            }
            animator?.SetTrigger(ReviveParam);
            SetArmsLayer(IsArmed);
            _currentSpeed = 0f;
            _lastSeenTime = -999f;
            _shotsLeftInBurst = burstShots;
            if (weapon != null) weapon.RefillFull();
            ApplyTint();
            _idleUntil = Time.time + 1f;
        }
    }
}
