using UnityEngine;
using UnityEngine.AI;

namespace GunMan
{
    /// <summary>
    /// One placed wooden piece. Owns its generated render + collision mesh, an edit mask (removed sub-cells),
    /// a <see cref="Health"/> (weapons can break it) and a carving NavMesh obstacle so NPCs walk around it.
    /// Created and registered by <see cref="BuildSystem"/>.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class BuildPiece : MonoBehaviour
    {
        public BuildPlacement placement;
        public int removedMask;
        public int materialIndex;

        public BuildSystem System { get; private set; }
        public Health Health { get; private set; }
        public BuildPieceType Type => placement.type;
        public BuildSlot Slot => placement.Slot;
        public int SubCellCount => BuildGrid.SubCellCount(placement.type);
        public int PresentSubCells
        {
            get
            {
                int n = 0;
                for (int i = 0; i < SubCellCount; i++) if (!IsSubCellRemoved(i)) n++;
                return n;
            }
        }
        public Mesh VisualMesh => _visual;

        MeshFilter _mf;
        MeshRenderer _mr;
        MeshCollider _mc;
        NavMeshObstacle _obstacle;
        Mesh _visual, _collision;
        bool _broken;

        public void Init(BuildSystem system, BuildPlacement p, int matIndex, Material material, float health)
        {
            System = system;
            placement = p;
            materialIndex = matIndex;
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            _mc = GetComponent<MeshCollider>();
            _mr.sharedMaterial = material;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            transform.SetPositionAndRotation(p.Position, p.Rotation);

            Health = GetComponent<Health>();
            if (Health == null) Health = gameObject.AddComponent<Health>();
            Health.maxHealth = health;
            Health.respawnDelay = 0f;               // destroyed when broken
            Health.applyImpactForceToRigidbody = false;
            Health.ResetHealth();
            Health.onDied.AddListener(OnBroken);

            if (p.type != BuildPieceType.Floor && p.type != BuildPieceType.Ceiling)
            {
                _obstacle = gameObject.AddComponent<NavMeshObstacle>();
                _obstacle.shape = NavMeshObstacleShape.Box;
                _obstacle.carving = true;
                _obstacle.carveOnlyStationary = true;
            }
            Rebuild();
        }

        /// <summary>Regenerates render + collision meshes from the type and the removed sub-cells.</summary>
        public void Rebuild()
        {
            if (_visual != null) Destroy(_visual);
            if (_collision != null && _collision != _visual) Destroy(_collision);
            _visual = BuildMeshFactory.BuildVisual(placement.type, removedMask);
            _collision = placement.type == BuildPieceType.Stairs ? BuildMeshFactory.BuildCollision(placement.type, removedMask) : _visual;
            _mf.sharedMesh = _visual;
            _mc.sharedMesh = null;
            _mc.sharedMesh = _collision;
            if (_obstacle != null)
            {
                var b = _collision.bounds;
                _obstacle.center = b.center;
                _obstacle.size = b.size;
            }
        }

        public bool IsSubCellRemoved(int index) => index >= 0 && index < SubCellCount && (removedMask & (1 << index)) != 0;

        /// <summary>Removes or restores one sub-cell. Refuses to remove the last remaining sub-cell.</summary>
        public bool SetSubCellRemoved(int index, bool removed)
        {
            if (index < 0 || index >= SubCellCount) return false;
            if (IsSubCellRemoved(index) == removed) return true;
            if (removed && PresentSubCells <= 1) return false;
            if (removed) removedMask |= 1 << index;
            else removedMask &= ~(1 << index);
            Rebuild();
            return true;
        }

        public bool ToggleSubCell(int index) => SetSubCellRemoved(index, !IsSubCellRemoved(index));

        /// <summary>
        /// Rotates the piece in place: stairs / tiles / roofs by 90°, walls by 180° (mirrors the edit grid). The slot never changes.
        /// </summary>
        public void Rotate()
        {
            int step = placement.type == BuildPieceType.Wall ? 2 : 1;
            placement.rot = (placement.rot + step) & 3;
            transform.rotation = placement.Rotation;
        }

        public void SetMaterial(int index, Material material)
        {
            materialIndex = index;
            if (material != null) _mr.sharedMaterial = material;
        }

        void OnBroken(DamageInfo info)
        {
            if (_broken) return;
            _broken = true;
            FxLibrary.PlayAt(ProceduralAudio.Gunshot("woodbreak", 0.4f, 0.12f, 0.5f, 0.9f), transform.position + Vector3.up, 0.8f, Random.Range(0.85f, 1.1f));
            SpawnDebris(info);
        }

        /// <summary>A handful of wooden chunks flying away from the hit point.</summary>
        void SpawnDebris(DamageInfo info)
        {
            var b = _visual != null ? _visual.bounds : new Bounds(Vector3.zero, Vector3.one);
            int debrisLayer = LayerMask.NameToLayer("Dynamic");
            if (debrisLayer < 0) debrisLayer = 0;
            int count = 7;
            for (int i = 0; i < count; i++)
            {
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "WoodDebris";
                float size = Random.Range(0.18f, 0.4f);
                chunk.transform.localScale = new Vector3(size, size * 0.4f, size * Random.Range(0.8f, 2f));
                var local = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
                chunk.transform.position = transform.TransformPoint(local);
                chunk.transform.rotation = Random.rotation;
                chunk.GetComponent<MeshRenderer>().sharedMaterial = _mr.sharedMaterial;
                chunk.layer = debrisLayer;
                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 2f;
                Vector3 dir = info.Direction.sqrMagnitude > 0.01f ? info.Direction : Vector3.up;
                rb.AddForce((dir + Random.insideUnitSphere * 0.8f + Vector3.up * 0.5f).normalized * Random.Range(4f, 9f), ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
                Destroy(chunk, Random.Range(2.5f, 4f));
            }
        }

        void OnDestroy()
        {
            if (System != null) System.Unregister(this);
            if (_visual != null) Destroy(_visual);
            if (_collision != null && _collision != _visual) Destroy(_collision);
        }
    }
}
