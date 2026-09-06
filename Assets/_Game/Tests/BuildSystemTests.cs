using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GunMan.Tests
{
    /// <summary>Play-mode tests of the wooden building system: grid snapping, placing, editing, tearing down, breaking.</summary>
    public class BuildSystemTests
    {
        IEnumerator LoadVillage()
        {
            var op = SceneManager.LoadSceneAsync("Village", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
        }

        /// <summary>True if the ray passes through <paramref name="piece"/> (other colliders such as houses or NPCs are ignored).</summary>
        static bool HitsPiece(Vector3 from, Vector3 dir, float dist, BuildPiece piece, out RaycastHit hit)
        {
            hit = default;
            foreach (var h in Physics.RaycastAll(from, dir, dist, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.gameObject == piece.gameObject) { hit = h; return true; }
            return false;
        }

        /// <summary>The player's build system with the reach limit lifted (the test cells are far from the spawn).</summary>
        static BuildSystem System()
        {
            var system = Object.FindAnyObjectByType<BuildSystem>();
            Assert.IsNotNull(system, "BuildSystem missing on the player");
            system.reach = 1000f;
            return system;
        }

        static float SurfaceArea(Mesh mesh)
        {
            var v = mesh.vertices; var t = mesh.triangles;
            float area = 0f;
            for (int i = 0; i < t.Length; i += 3) area += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude * 0.5f;
            return area;
        }

        // far away from the player spawn (-11.5, 0, -11.5) so the "player in the way" check never triggers
        const int Cx = 8, Cz = 8;

        [Test]
        public void ComputePlacement_SnapsToGrid()
        {
            // player in the middle of cell (0,0) looking along +Z, aim point 3.5 m ahead at eye height
            var target = new Vector3(2f, 1.65f, 5.5f);
            var floor = BuildSystem.ComputePlacement(BuildPieceType.Floor, target, 0f, 0, 0);
            Assert.AreEqual((0, 1, 0), (floor.cx, floor.cz, floor.level), "floor goes into the next cell on the ground storey");
            Assert.AreEqual(new Vector3(2f, 0f, 6f), floor.Position);

            var wall = BuildSystem.ComputePlacement(BuildPieceType.Wall, target, 0f, 0, 0);
            Assert.AreEqual(0, wall.rot & 1, "wall faces the player (spans X)");
            Assert.AreEqual(new Vector3(2f, 0f, 4f), wall.Position, "wall sits on the grid line between the two cells");
            var wallRotated = BuildSystem.ComputePlacement(BuildPieceType.Wall, target, 0f, 0, 1);
            Assert.AreEqual(1, wallRotated.rot & 1);
            Assert.AreEqual(new Vector3(0f, 0f, 6f), wallRotated.Position, "rotated wall spans Z on the nearest x line");
            Assert.AreNotEqual(wall.Slot, wallRotated.Slot);

            var stairs = BuildSystem.ComputePlacement(BuildPieceType.Stairs, target, 0f, 0, 0);
            Assert.AreEqual(0, stairs.rot, "stairs rise away from the player");
            Assert.AreEqual((0, 1, 0), (stairs.cx, stairs.cz, stairs.level));

            var ceiling = BuildSystem.ComputePlacement(BuildPieceType.Ceiling, target, 0f, 0, 0);
            Assert.AreEqual(0, ceiling.level, "ceiling of the ground storey");
            Assert.AreEqual(4f, ceiling.Position.y, 0.001f, "ceiling tile sits one storey up");
            var floorAbove = new BuildPlacement(BuildPieceType.Floor, 0, 1, 1, 0);
            Assert.AreEqual(floorAbove.Slot, ceiling.Slot, "a ceiling is the floor of the storey above");

            // looking up moves walls one storey up
            var wallUp = BuildSystem.ComputePlacement(BuildPieceType.Wall, new Vector3(2f, 4.7f, 5.5f), 0f, 0, 0);
            Assert.AreEqual(1, wallUp.level);

            // facing +X: walls span Z
            var wallEast = BuildSystem.ComputePlacement(BuildPieceType.Wall, new Vector3(5.5f, 1.65f, 2f), 0f, 1, 0);
            Assert.AreEqual(1, wallEast.rot & 1);
            Assert.AreEqual(new Vector3(4f, 0f, 2f), wallEast.Position);
        }

        [Test]
        public void MeshFactory_BuildsClosedPiecesAndEditsRemoveGeometry()
        {
            foreach (var type in BuildGrid.AllTypes)
            {
                var mesh = BuildMeshFactory.BuildVisual(type, 0);
                Assert.Greater(mesh.vertexCount, 0, $"{type} has no vertices");
                Assert.AreEqual(0, mesh.triangles.Length % 3);
                var size = mesh.bounds.size;
                Assert.AreEqual(BuildGrid.Cell, size.x, 0.001f, $"{type} width");
                if (type == BuildPieceType.Wall)
                {
                    Assert.AreEqual(BuildGrid.Height, size.y, 0.001f);
                    Assert.AreEqual(BuildGrid.Thickness, size.z, 0.001f);
                }
                else if (type == BuildPieceType.Stairs) Assert.AreEqual(BuildGrid.Height + BuildMeshFactory.StairSlab, size.y, 0.001f);
                else if (type == BuildPieceType.Roof) Assert.AreEqual(BuildGrid.RoofHeight, size.y, 0.001f);
                else Assert.AreEqual(BuildGrid.Thickness, size.y, 0.001f);
                Object.DestroyImmediate(mesh);
            }

            var full = BuildMeshFactory.BuildVisual(BuildPieceType.Wall, 0);
            var door = BuildMeshFactory.BuildVisual(BuildPieceType.Wall, (1 << 1) | (1 << 4)); // bottom-middle + centre removed
            Assert.Less(SurfaceArea(door), SurfaceArea(full), "door wall must have less surface than a full wall");
            Assert.AreEqual(2f * (BuildGrid.Cell * BuildGrid.Height + BuildGrid.Cell * BuildGrid.Thickness + BuildGrid.Height * BuildGrid.Thickness), SurfaceArea(full), 0.01f, "full wall is a closed box");
            Assert.AreEqual(BuildGrid.Height, door.bounds.size.y, 0.001f, "the top row still spans the full height");
            var halfRoof = BuildMeshFactory.BuildVisual(BuildPieceType.Roof, 0b1100);
            Assert.AreEqual(BuildGrid.Cell / 2f, halfRoof.bounds.size.z, 0.001f, "half roof covers half the cell");
            var ramp = BuildMeshFactory.BuildCollision(BuildPieceType.Stairs, 0);
            Assert.AreEqual(12, ramp.triangles.Length / 3, "stairs collision is a simple prism");
            foreach (var m in new[] { full, door, halfRoof, ramp }) Object.DestroyImmediate(m);
        }

        [UnityTest]
        public IEnumerator Village_PlacesEveryPieceTypeOnce()
        {
            yield return LoadVillage();
            var system = System();
            Assert.IsFalse(system.BuildMode);
            int before = system.PieceCount;

            // pieces must be within reach of the player
            system.reach = 12f;
            Assert.IsFalse(system.CanPlace(new BuildPlacement(BuildPieceType.Floor, Cx, Cz, 0, 0), out var far), "cell 64 m away must be out of reach");
            Assert.IsNotEmpty(far);
            system.reach = 1000f;

            var placements = new[]
            {
                new BuildPlacement(BuildPieceType.Floor, Cx, Cz, 0, 0),
                new BuildPlacement(BuildPieceType.Wall, Cx, Cz, 0, 0),
                new BuildPlacement(BuildPieceType.Wall, Cx, Cz, 0, 1),
                new BuildPlacement(BuildPieceType.Stairs, Cx + 1, Cz, 0, 2),
                new BuildPlacement(BuildPieceType.Ceiling, Cx, Cz, 0, 0),
                new BuildPlacement(BuildPieceType.Roof, Cx, Cz, 1, 0),
            };
            foreach (var p in placements)
            {
                Assert.IsTrue(system.CanPlace(p, out var reason), $"{p}: {reason}");
                Assert.IsTrue(system.TryPlace(p, out var piece), $"{p} not placed");
                Assert.IsNotNull(piece);
                Assert.Less(Vector3.Distance(p.Position, piece.transform.position), 0.001f);
                Assert.AreEqual(p.Slot, piece.Slot);
                Assert.IsNotNull(piece.GetComponent<MeshCollider>().sharedMesh, $"{p.type} has no collider mesh");
                Assert.Greater(piece.VisualMesh.vertexCount, 0);
                Assert.IsNotNull(piece.Health);
                Assert.AreEqual(system.pieceHealth, piece.Health.Current, 0.01f);
                Assert.AreEqual(piece, system.GetPiece(p.Slot));
            }
            Assert.AreEqual(before + placements.Length, system.PieceCount);

            // the same slot can only be used once; a floor of storey 1 is the ceiling of storey 0
            Assert.IsFalse(system.CanPlace(new BuildPlacement(BuildPieceType.Wall, Cx, Cz, 0, 2), out var r1), "mirrored wall shares the slot");
            Assert.IsNotEmpty(r1);
            Assert.IsFalse(system.CanPlace(new BuildPlacement(BuildPieceType.Floor, Cx, Cz, 1, 0)), "floor above == ceiling below");
            Assert.IsFalse(system.CanPlace(new BuildPlacement(BuildPieceType.Roof, Cx + 1, Cz, 0, 0)), "roof and stairs share the cell volume");
            Assert.IsFalse(system.TryPlace(new BuildPlacement(BuildPieceType.Floor, Cx, Cz, 0, 0), out var dup));
            Assert.IsNull(dup);
            Assert.AreEqual(before + placements.Length, system.PieceCount);

            // the collider of a piece is hit by physics rays (weapons / NPC line of sight)
            var wall = system.GetPiece(new BuildPlacement(BuildPieceType.Wall, Cx, Cz, 0, 0).Slot);
            Vector3 from = wall.transform.position + new Vector3(0f, 2f, -3f);
            Assert.IsTrue(HitsPiece(from, Vector3.forward, 6f, wall, out _), "wall not hit by raycast");
        }

        [UnityTest]
        public IEnumerator Village_StairsRampIsWalkableAndReachesTheNextStorey()
        {
            yield return LoadVillage();
            var system = System();
            var p = new BuildPlacement(BuildPieceType.Stairs, Cx, Cz + 3, 0, 0);
            Assert.IsTrue(system.TryPlace(p, out var stairs));
            yield return null;

            float x = p.Position.x;
            float lastY = -1f;
            for (int i = 0; i <= 8; i++)
            {
                float z = p.Position.z - BuildGrid.Cell / 2f + 0.1f + i * (BuildGrid.Cell - 0.2f) / 8f;
                Assert.IsTrue(HitsPiece(new Vector3(x, 20f, z), Vector3.down, 40f, stairs, out var hit), $"stairs not under z={z}");
                Assert.Greater(hit.point.y, lastY, "ramp must rise monotonically");
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                Assert.Less(slope, 50f, "ramp steeper than the player's slope limit");
                lastY = hit.point.y;
            }
            Assert.AreEqual(BuildGrid.Height + BuildGrid.Thickness, lastY, 0.15f, "top of the ramp meets the floor tile of the storey above");
        }

        [UnityTest]
        public IEnumerator Village_EditRotateAndTearDown()
        {
            yield return LoadVillage();
            var system = System();
            var p = new BuildPlacement(BuildPieceType.Wall, Cx + 3, Cz, 0, 0);
            Assert.IsTrue(system.TryPlace(p, out var wall));
            float fullArea = SurfaceArea(wall.VisualMesh);

            // cut a door: bottom-middle + centre
            Assert.IsTrue(wall.SetSubCellRemoved(1, true));
            Assert.IsTrue(wall.SetSubCellRemoved(4, true));
            Assert.IsTrue(wall.IsSubCellRemoved(1));
            Assert.AreEqual(7, wall.PresentSubCells);
            Assert.Less(SurfaceArea(wall.VisualMesh), fullArea, "mesh not rebuilt after edit");
            yield return null;
            // the opening lets a ray through, the remaining wall does not
            Vector3 c = wall.transform.position;
            Assert.IsFalse(HitsPiece(c + new Vector3(0f, 1.0f, -2f), Vector3.forward, 4f, wall, out _), "door opening is blocked");
            Assert.IsTrue(HitsPiece(c + new Vector3(-1.3f, 1.0f, -2f), Vector3.forward, 4f, wall, out _), "wall next to the door missing");

            // never remove the last sub-cell
            for (int i = 0; i < wall.SubCellCount; i++) wall.SetSubCellRemoved(i, true);
            Assert.AreEqual(1, wall.PresentSubCells, "at least one sub-cell must remain");

            // restore + rotate (walls mirror by 180°, slot unchanged)
            Assert.IsTrue(wall.SetSubCellRemoved(4, false));
            var slot = wall.Slot;
            wall.Rotate();
            Assert.AreEqual(2, wall.placement.rot);
            Assert.AreEqual(slot, wall.Slot);
            Assert.AreEqual(180f, wall.transform.eulerAngles.y, 0.01f);

            // edit mode via the system: markers appear, hover maps the view ray onto sub-cells
            Assert.IsTrue(system.BeginEdit(wall));
            Assert.IsTrue(system.EditMode);
            Assert.AreEqual(wall, system.Editing);
            Assert.AreEqual(wall.SubCellCount, wall.GetComponentsInChildren<MeshRenderer>().Length - 1, "one marker per sub-cell");
            int hover = BuildSystem.ComputeHoverSubCell(wall, c + new Vector3(0f, 0.5f, -3f), Vector3.forward, 10f);
            Assert.AreEqual(1, hover, "looking at the bottom-middle sub-cell");
            int hoverTop = BuildSystem.ComputeHoverSubCell(wall, c + new Vector3(1.5f, 3.5f, 3f), Vector3.back, 10f);
            Assert.AreEqual(6, hoverTop, "mirrored wall: world +x is local -x");
            Assert.AreEqual(-1, BuildSystem.ComputeHoverSubCell(wall, c + new Vector3(0f, 0.5f, -3f), Vector3.back, 10f), "looking away");
            system.EndEdit();
            Assert.IsFalse(system.EditMode);
            yield return null;
            Assert.AreEqual(0, wall.GetComponentsInChildren<MeshRenderer>().Length - 1, "markers removed");

            // tear down frees the slot
            int count = system.PieceCount;
            Assert.IsTrue(system.Remove(wall));
            yield return null;
            Assert.IsTrue(wall == null, "piece object should be destroyed");
            Assert.AreEqual(count - 1, system.PieceCount);
            Assert.IsNull(system.GetPiece(slot));
            Assert.IsTrue(system.TryPlace(p, out _), "slot must be free again");
        }

        [UnityTest]
        public IEnumerator Village_WeaponDamageBreaksWood()
        {
            yield return LoadVillage();
            var system = System();
            var p = new BuildPlacement(BuildPieceType.Roof, Cx + 5, Cz + 5, 0, 0);
            Assert.IsTrue(system.TryPlace(p, out var roof));
            var slot = roof.Slot;
            int count = system.PieceCount;

            roof.Health.TakeDamage(new DamageInfo { Amount = 40f, Point = roof.transform.position, Direction = Vector3.forward, Source = null });
            Assert.IsFalse(roof.Health.IsDead);
            Assert.AreEqual(system.pieceHealth - 40f, roof.Health.Current, 0.01f);
            roof.Health.TakeDamage(new DamageInfo { Amount = 1000f, Point = roof.transform.position, Direction = Vector3.forward });
            Assert.IsTrue(roof.Health.IsDead);
            yield return null;
            yield return null;
            Assert.IsTrue(roof == null, "broken piece should be destroyed");
            Assert.AreEqual(count - 1, system.PieceCount);
            Assert.IsFalse(system.IsOccupied(slot));
            Assert.IsNotNull(GameObject.Find("WoodDebris"), "debris expected");
        }

        [UnityTest]
        public IEnumerator Village_BuildModePutsWeaponsAwayAndFloorLiftsPlayer()
        {
            yield return LoadVillage();
            var system = System();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var holder = player.GetComponentInChildren<WeaponHolder>(true);
            Assert.IsTrue(holder.gameObject.activeSelf);

            system.SetBuildMode(true);
            Assert.IsTrue(system.BuildMode);
            yield return null;
            Assert.IsFalse(holder.gameObject.activeSelf, "weapons should be put away while building");
            system.SelectType(BuildPieceType.Stairs);
            Assert.AreEqual(BuildPieceType.Stairs, system.SelectedType);
            int matBefore = system.MaterialIndex;
            system.CycleMaterial(1);
            Assert.AreEqual((matBefore + 1) % system.MaterialCount, system.MaterialIndex);
            Assert.IsNotNull(system.MaterialAt(system.MaterialIndex));

            // a floor under the feet lifts the player onto it
            Vector3 feet = player.transform.position;
            int cx = Mathf.FloorToInt(feet.x / BuildGrid.Cell), cz = Mathf.FloorToInt(feet.z / BuildGrid.Cell);
            var floor = new BuildPlacement(BuildPieceType.Floor, cx, cz, 0, 0);
            Assert.IsTrue(system.CanPlace(floor, out var why), why);
            Assert.IsTrue(system.TryPlace(floor, out _));
            yield return null;
            Assert.GreaterOrEqual(player.transform.position.y, BuildGrid.Thickness - 0.05f, "player should stand on the new floor");

            // a wall through the player is refused
            var wallHere = new BuildPlacement(BuildPieceType.Wall, cx, Mathf.RoundToInt(feet.z / BuildGrid.Cell), 0, 0);
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(wallHere.Position.x, player.transform.position.y, wallHere.Position.z);
            cc.enabled = true;
            Physics.SyncTransforms();
            Assert.IsFalse(system.CanPlace(wallHere, out var blocked), "wall through the player must be refused");
            Assert.IsNotEmpty(blocked);

            system.SetBuildMode(false);
            Assert.IsFalse(system.BuildMode);
            Assert.IsTrue(holder.gameObject.activeSelf, "weapons back after leaving build mode");

            // dying leaves build mode and keeps the weapons hidden until the respawn
            system.SetBuildMode(true);
            player.respawnDelay = 0.4f;
            player.Health.TakeDamage(new DamageInfo { Amount = 1000f, Point = player.transform.position, Direction = Vector3.forward });
            yield return null;
            yield return null;
            Assert.IsFalse(system.BuildMode, "death ends build mode");
            Assert.IsFalse(holder.gameObject.activeSelf, "no weapons while dead");
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(player.IsDead);
            Assert.IsTrue(holder.gameObject.activeSelf, "weapons back after respawn");
        }
    }
}
