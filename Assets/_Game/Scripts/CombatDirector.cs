using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunMan
{
    /// <summary>
    /// Owns the combat mode. Off: nobody attacks the player (armed NPCs just patrol).
    /// On: exactly one armed NPC is the active opponent and hunts the player. When it dies, the next one
    /// (preferably with a different weapon) takes over after <see cref="handoverDelay"/> seconds.
    /// </summary>
    public class CombatDirector : MonoBehaviour
    {
        public static CombatDirector Instance { get; private set; }

        [Tooltip("Seconds between an opponent dying and the next one taking over")]
        public float handoverDelay = 3f;
        [Tooltip("Preferred minimum distance of a new opponent to the player")]
        public float minSpawnDistance = 10f;
        public bool startInCombatMode = false;

        public bool CombatMode { get; private set; }
        public NpcCharacter Opponent { get; private set; }
        public int Defeated { get; private set; }
        public int Round { get; private set; }
        /// <summary>Seconds until the next opponent is picked (0 when one is active).</summary>
        public float NextOpponentIn => CombatMode && Opponent == null ? Mathf.Max(0f, _nextPickTime - Time.time) : 0f;
        public string Message { get; private set; } = "";
        public float MessageTime { get; private set; } = -999f;

        float _nextPickTime;
        string _lastWeapon;
        PlayerController _player;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _player = GameManager.Instance != null ? GameManager.Instance.player : FindAnyObjectByType<PlayerController>();
            if (startInCombatMode) SetCombatMode(true);
        }

        public void Toggle() => SetCombatMode(!CombatMode);

        public void SetCombatMode(bool on)
        {
            if (on == CombatMode) return;
            CombatMode = on;
            if (on)
            {
                Round = 0;
                _nextPickTime = Time.time + 1f;
                Announce("Kampfmodus aktiviert – ein Gegner greift dich an!");
            }
            else
            {
                if (Opponent != null) Opponent.StopAttacking();
                Opponent = null;
                Announce("Kampfmodus beendet");
            }
        }

        public bool IsOpponent(NpcCharacter npc) => CombatMode && Opponent == npc;

        void Announce(string text)
        {
            Message = text;
            MessageTime = Time.time;
        }

        void Update()
        {
            if (!CombatMode) return;
            if (_player == null) _player = FindAnyObjectByType<PlayerController>();

            if (Opponent != null)
            {
                bool gone = !Opponent.isActiveAndEnabled || Opponent.Health == null;
                if (gone || Opponent.Health.IsDead)
                {
                    if (!gone)
                    {
                        Defeated++;
                        Announce($"{Opponent.weapon.displayName}-Gegner besiegt!");
                    }
                    Opponent = null;
                    _nextPickTime = Time.time + handoverDelay;
                }
                return;
            }

            if (Time.time < _nextPickTime) return;
            var next = PickNext();
            if (next == null)
            {
                _nextPickTime = Time.time + 1f; // everybody dead / respawning, try again
                return;
            }
            Opponent = next;
            Round++;
            _lastWeapon = next.weapon.displayName;
            next.StartAttacking();
            Announce($"Runde {Round}: Gegner mit {_lastWeapon}");
        }

        NpcCharacter PickNext()
        {
            var candidates = NpcCharacter.All.Where(n => n.IsArmed && n.Health != null && !n.Health.IsDead && n.isActiveAndEnabled).ToList();
            if (candidates.Count == 0) return null;
            Vector3 playerPos = _player != null ? _player.transform.position : Vector3.zero;
            float Dist(NpcCharacter n) => Vector3.Distance(n.transform.position, playerPos);

            // a different weapon than last time, far enough away, otherwise the nearest one
            var preferred = candidates.Where(n => n.weapon.displayName != _lastWeapon).ToList();
            if (preferred.Count == 0) preferred = candidates;
            var farEnough = preferred.Where(n => Dist(n) >= minSpawnDistance).ToList();
            if (farEnough.Count == 0) farEnough = preferred;
            return farEnough.OrderBy(Dist).First();
        }

        /// <summary>Convenience for scripts/tests: pick a specific NPC as the current opponent.</summary>
        public void ForceOpponent(NpcCharacter npc)
        {
            if (!CombatMode) SetCombatMode(true);
            if (Opponent != null && Opponent != npc) Opponent.StopAttacking();
            Opponent = npc;
            Round++;
            _lastWeapon = npc != null && npc.weapon != null ? npc.weapon.displayName : _lastWeapon;
            npc?.StartAttacking();
        }
    }
}
