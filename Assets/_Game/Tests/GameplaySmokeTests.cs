using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GunMan.Tests
{
    /// <summary>Play-mode smoke tests: scenes load, every weapon fires without errors, NPCs walk on the NavMesh.</summary>
    public class GameplaySmokeTests
    {
        IEnumerator LoadScene(string name)
        {
            var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Village_AllWeaponsFire()
        {
            yield return LoadScene("Village");
            var holder = Object.FindAnyObjectByType<WeaponHolder>();
            Assert.IsNotNull(holder, "WeaponHolder missing");
            Assert.GreaterOrEqual(holder.Weapons.Count, 9, "expected 9 weapons");
            var cam = holder.playerCamera;
            Assert.IsNotNull(cam);

            for (int i = 0; i < holder.Weapons.Count; i++)
            {
                holder.Equip(i);
                yield return null;
                var w = holder.Current;
                Assert.AreEqual(holder.Weapons[i], w);
                Assert.IsTrue(w.gameObject.activeInHierarchy, $"{w.displayName} not active after equip");
                Assert.IsNotNull(w.muzzle, $"{w.displayName} muzzle missing");
                int before = w.AmmoInMag;
                w.FireOnce(cam);
                Assert.AreEqual(before - 1, w.AmmoInMag, $"{w.displayName} ammo not consumed");
                yield return new WaitForSeconds(0.15f);
            }
            // let projectiles explode
            yield return new WaitForSeconds(3.5f);
            Assert.IsNotNull(Object.FindAnyObjectByType<FxLibrary>());
        }

        [UnityTest]
        public IEnumerator Village_NpcsAreOnNavMesh()
        {
            yield return LoadScene("Village");
            yield return new WaitForSeconds(0.5f);
            var npcs = Object.FindObjectsByType<NpcCharacter>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(npcs.Length, 5, "expected NPCs");
            int onMesh = npcs.Count(n => n.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.GreaterOrEqual(onMesh, npcs.Length / 2, $"only {onMesh}/{npcs.Length} NPCs on NavMesh");
            var tri = NavMesh.CalculateTriangulation();
            Assert.Greater(tri.vertices.Length, 100, "NavMesh looks empty");
        }

        [UnityTest]
        public IEnumerator Village_ShootingTargetFalls()
        {
            yield return LoadScene("Village");
            var target = Object.FindAnyObjectByType<PopupTarget>();
            Assert.IsNotNull(target);
            var health = target.GetComponent<Health>();
            int before = PopupTarget.TotalKnockdowns;
            health.TakeDamage(new DamageInfo { Amount = 1000f, Point = target.transform.position, Direction = Vector3.forward, Force = 1f });
            yield return null;
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(before + 1, PopupTarget.TotalKnockdowns);
        }

        [UnityTest]
        public IEnumerator Village_NpcDiesAndRespawns()
        {
            yield return LoadScene("Village");
            yield return new WaitForSeconds(0.3f);
            var npc = Object.FindAnyObjectByType<NpcCharacter>();
            Assert.IsNotNull(npc);
            var health = npc.GetComponent<Health>();
            health.respawnDelay = 0.5f;
            health.TakeDamage(new DamageInfo { Amount = 1000f, Point = npc.transform.position, Direction = Vector3.forward, Force = 1f });
            yield return null;
            Assert.IsTrue(health.IsDead);
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(health.IsDead, "NPC should have respawned");
        }

        [UnityTest]
        public IEnumerator Village_PlayerTakesDamageDiesAndRespawns()
        {
            yield return LoadScene("Village");
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.Health, "player has no Health component");
            Assert.AreEqual(player.Health.maxHealth, player.Health.Current);

            player.Health.TakeDamage(new DamageInfo { Amount = 30f, Point = player.transform.position, Direction = Vector3.forward });
            Assert.AreEqual(player.Health.maxHealth - 30f, player.Health.Current, 0.01f);
            Assert.IsFalse(player.IsDead);
            Assert.Greater(player.LastHitTime, -1f, "HUD hit feedback not triggered");

            player.respawnDelay = 0.5f;
            player.Health.TakeDamage(new DamageInfo { Amount = 1000f, Point = player.transform.position, Direction = Vector3.forward });
            yield return null;
            Assert.IsTrue(player.IsDead);
            Assert.AreEqual(1, player.Deaths);
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(player.IsDead, "player should have respawned");
            Assert.AreEqual(player.Health.maxHealth, player.Health.Current, 0.01f);
            Assert.IsTrue(Object.FindAnyObjectByType<WeaponHolder>() != null, "weapons should be back after respawn");
        }

        [UnityTest]
        public IEnumerator Village_ArmedNpcsCarryDifferentWeapons()
        {
            yield return LoadScene("Village");
            yield return null;
            var npcs = Object.FindObjectsByType<NpcCharacter>(FindObjectsSortMode.None);
            var armed = npcs.Where(n => n.IsArmed).ToList();
            Assert.GreaterOrEqual(armed.Count, 4, "expected at least 4 armed NPCs");
            Assert.GreaterOrEqual(npcs.Count(n => !n.IsArmed), 2, "expected some civilians too");
            Assert.IsTrue(armed.All(n => n.role == NpcRole.Soldier), "armed NPCs should be soldiers");
            var names = armed.Select(n => n.weapon.displayName).Distinct().ToList();
            Assert.GreaterOrEqual(names.Count, 3, $"expected different weapons, got: {string.Join(", ", names)}");
            foreach (var n in armed)
            {
                Assert.IsTrue(n.weapon.transform.IsChildOf(n.transform), $"{n.name}: weapon not part of the NPC");
                Assert.IsNotNull(n.weapon.muzzle, $"{n.name}: weapon has no muzzle");
                var handBone = n.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "hand_r");
                Assert.IsNotNull(handBone, $"{n.name}: hand_r bone missing");
                Assert.IsTrue(n.weapon.transform.IsChildOf(handBone), $"{n.name}: weapon not attached to the right hand");
                Assert.AreEqual(1f, n.weapon.transform.lossyScale.x, 0.05f, $"{n.name}: weapon scale wrong");
            }
        }

        [UnityTest]
        public IEnumerator Village_PeacefulModeNobodyAttacks()
        {
            yield return LoadScene("Village");
            var player = Object.FindAnyObjectByType<PlayerController>();
            var director = CombatDirector.Instance;
            Assert.IsNotNull(director, "CombatDirector missing");
            Assert.IsFalse(director.CombatMode, "combat mode should be off by default");

            // stand in the middle of the plaza for a while
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            cc.enabled = true;
            float start = player.Health.Current;
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, NpcCharacter.All.Count(n => n.IsEngaged), "nobody should attack in peaceful mode");
            Assert.AreEqual(0, NpcCharacter.All.Sum(n => n.ShotsFired), "nobody should shoot in peaceful mode");
            Assert.AreEqual(start, player.Health.Current, 0.01f);
        }

        [UnityTest]
        public IEnumerator Village_CombatModeHasExactlyOneOpponent()
        {
            yield return LoadScene("Village");
            yield return new WaitForSeconds(0.3f);
            var director = CombatDirector.Instance;
            director.handoverDelay = 0.5f;
            director.SetCombatMode(true);
            Assert.IsTrue(director.CombatMode);

            float t = 0f;
            while (t < 4f && director.Opponent == null) { t += Time.deltaTime; yield return null; }
            var first = director.Opponent;
            Assert.IsNotNull(first, "no opponent picked");
            Assert.IsTrue(first.IsArmed);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, NpcCharacter.All.Count(n => n.IsEngaged), "exactly one NPC must be attacking");
            Assert.IsTrue(first.IsEngaged);

            // kill it -> next opponent with a different weapon takes over
            string firstWeapon = first.weapon.displayName;
            first.Health.TakeDamage(new DamageInfo { Amount = 1000f, Point = first.transform.position, Direction = Vector3.forward });
            yield return null;
            yield return null;
            Assert.IsNull(director.Opponent, "dead opponent should be cleared");
            Assert.AreEqual(1, director.Defeated);
            t = 0f;
            while (t < 4f && director.Opponent == null) { t += Time.deltaTime; yield return null; }
            var second = director.Opponent;
            Assert.IsNotNull(second, "no second opponent picked");
            Assert.AreNotEqual(first, second);
            Assert.AreNotEqual(firstWeapon, second.weapon.displayName, "next opponent should carry a different weapon");
            Assert.LessOrEqual(NpcCharacter.All.Count(n => n.IsEngaged), 1);

            // switching combat mode off stops the attack
            director.SetCombatMode(false);
            yield return null;
            yield return null;
            Assert.IsNull(director.Opponent);
            Assert.AreEqual(0, NpcCharacter.All.Count(n => n.IsEngaged), "nobody should attack after leaving combat mode");
        }

        [UnityTest]
        public IEnumerator Arena_OpponentShootsThePlayer()
        {
            yield return LoadScene("Arena");
            yield return new WaitForSeconds(0.3f);
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var soldier = Object.FindObjectsByType<NpcCharacter>(FindObjectsSortMode.None).FirstOrDefault(n => n.IsArmed);
            Assert.IsNotNull(soldier, "no soldier in the arena");

            // put the soldier 8 m in front of the player with a clear line of sight and perfect aim, then make it the opponent
            var agent = soldier.GetComponent<NavMeshAgent>();
            Vector3 wanted = player.transform.position + player.transform.forward * 8f;
            Assert.IsTrue(NavMesh.SamplePosition(wanted, out var hit, 3f, NavMesh.AllAreas), "no NavMesh in front of the player");
            agent.Warp(hit.position);
            soldier.weapon.spreadDegrees = 0f;
            float startHealth = player.Health.Current;
            CombatDirector.Instance.ForceOpponent(soldier);
            Assert.IsTrue(soldier.IsOpponent);

            float t = 0f;
            while (t < 8f && (soldier.ShotsFired == 0 || player.Health.Current >= startHealth))
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.Greater(soldier.ShotsFired, 0, "opponent never fired");
            Assert.IsTrue(soldier.IsEngaged, "opponent should be engaged");
            Assert.Less(player.Health.Current, startHealth, "player was never hit");
            Assert.Greater(player.LastHitTime, 0f, "player hit feedback missing");
            Assert.AreEqual(1, NpcCharacter.All.Count(n => n.IsEngaged), "only the opponent may attack");
        }

        [UnityTest]
        public IEnumerator Arena_LoadsWithPlayer()
        {
            yield return LoadScene("Arena");
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerController>());
            Assert.IsNotNull(Object.FindAnyObjectByType<GameManager>());
            Assert.Greater(Object.FindObjectsByType<PopupTarget>(FindObjectsSortMode.None).Length, 0);
        }
    }
}
