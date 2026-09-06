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
        public IEnumerator Arena_LoadsWithPlayer()
        {
            yield return LoadScene("Arena");
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerController>());
            Assert.IsNotNull(Object.FindAnyObjectByType<GameManager>());
            Assert.Greater(Object.FindObjectsByType<PopupTarget>(FindObjectsSortMode.None).Length, 0);
        }
    }
}
