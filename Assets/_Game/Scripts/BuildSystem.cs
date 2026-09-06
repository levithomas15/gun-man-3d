using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GunMan
{
    /// <summary>
    /// Fortnite style building with wood. Lives on the player.
    /// B toggles build mode (weapons are put away), 1-5 / mouse wheel pick wall / floor / ceiling / stairs / roof,
    /// R rotates, LMB places the ghost, G edits the targeted piece (sub-cells on/off, R rotates, T wood), X tears it down,
    /// T cycles the wood texture. Pieces snap to a <see cref="BuildGrid.Cell"/> metre grid and register in a slot map so nothing overlaps.
    /// </summary>
    public class BuildSystem : MonoBehaviour
    {
        [Header("References")]
        public PlayerController player;
        public WeaponHolder holder;
        public Camera cam;

        [Header("Wood (materials are created by the editor builder)")]
        public Material[] woodMaterials = new Material[0];
        public string[] woodNames = new string[0];
        public int defaultMaterialIndex;
        public Material ghostValidMaterial;
        public Material ghostInvalidMaterial;
        public Material editPresentMaterial;
        public Material editRemovedMaterial;
        public Material editHoverMaterial;

        [Header("Tuning")]
        [Tooltip("Distance in front of the camera where the ghost snaps to the grid when nothing closer is hit")]
        public float placeDistance = 3.5f;
        [Tooltip("How far away pieces can be targeted for editing / tearing down")]
        public float reach = 12f;
        public float pieceHealth = 150f;
        [Tooltip("Lowest storey pieces may be placed on (0 = ground)")]
        public int minLevel = 0;

        public bool BuildMode { get; private set; }
        public bool EditMode => Editing != null;
        public BuildPieceType SelectedType { get; private set; } = BuildPieceType.Wall;
        /// <summary>Extra rotation (0..3) applied on top of the player's facing.</summary>
        public int Rotation { get; private set; }
        public int MaterialIndex { get; private set; }
        public int MaterialCount => woodMaterials != null && woodMaterials.Length > 0 ? woodMaterials.Length : 1;
        public string MaterialName => woodNames != null && MaterialIndex < woodNames.Length ? woodNames[MaterialIndex] : "Holz";
        /// <summary>Piece under the crosshair (within reach), null otherwise.</summary>
        public BuildPiece Target { get; private set; }
        public BuildPiece Editing { get; private set; }
        /// <summary>Sub-cell of the edited piece under the crosshair, -1 = none.</summary>
        public int HoverSubCell { get; private set; } = -1;
        public bool GhostValid { get; private set; }
        public string GhostReason { get; private set; } = "";
        public BuildPlacement GhostPlacement { get; private set; }
        public int PieceCount => _pieces.Count;
        public int Placed { get; private set; }
        public string Message { get; private set; } = "";
        public float MessageTime { get; private set; } = -99f;

        readonly Dictionary<BuildSlot, BuildPiece> _pieces = new Dictionary<BuildSlot, BuildPiece>();
        readonly Dictionary<BuildPieceType, Mesh> _ghostMeshes = new Dictionary<BuildPieceType, Mesh>();
        readonly List<MeshRenderer> _markers = new List<MeshRenderer>();
        Transform _root;
        GameObject _ghost;
        MeshFilter _ghostMf;
        MeshRenderer _ghostMr;
        AudioSource _audio;
        LayerMask _aimMask, _playerMask;
        Material _fallbackWood;

        Transform Root
        {
            get
            {
                if (_root == null) _root = new GameObject("Buildings").transform;
                return _root;
            }
        }

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>() ?? FindAnyObjectByType<PlayerController>();
            if (holder == null) holder = GetComponentInChildren<WeaponHolder>(true) ?? FindAnyObjectByType<WeaponHolder>();
            if (cam == null) cam = GetComponentInChildren<Camera>(true) ?? Camera.main;
            _aimMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
            _playerMask = LayerMask.GetMask("Player");
            MaterialIndex = Mathf.Clamp(defaultMaterialIndex, 0, MaterialCount - 1);
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.playOnAwake = false;
            EnsureMaterials();
        }

        void OnDestroy()
        {
            foreach (var m in _ghostMeshes.Values) if (m != null) Destroy(m);
            _ghostMeshes.Clear();
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool dead = player != null && player.IsDead;

            if (dead && BuildMode) { SetBuildMode(false); return; }
            if (locked && !dead && kb.bKey.wasPressedThisFrame) SetBuildMode(!BuildMode);
            if (!BuildMode) return;

            // a respawn (F5) re-enables the weapons: keep them away while building
            if (holder != null && holder.gameObject.activeSelf) holder.gameObject.SetActive(false);
            if (!locked || cam == null) { HideGhost(); return; }

            Vector3 origin = cam.transform.position, fwd = cam.transform.forward;
            bool hit = Weapon.RaycastIgnoring(origin, fwd, reach, _aimMask, player != null ? player.transform : null, out var aimHit);
            Target = hit ? aimHit.collider.GetComponentInParent<BuildPiece>() : null;

            if (EditMode)
            {
                UpdateEdit(kb, mouse, origin, fwd);
                return;
            }

            for (int i = 0; i < BuildGrid.AllTypes.Length; i++)
                if (DigitPressed(kb, i + 1)) SelectType(BuildGrid.AllTypes[i]);
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.01f) SelectType(BuildGrid.AllTypes[(TypeIndex(SelectedType) + BuildGrid.AllTypes.Length - 1) % BuildGrid.AllTypes.Length]);
                else if (scroll < -0.01f) SelectType(BuildGrid.AllTypes[(TypeIndex(SelectedType) + 1) % BuildGrid.AllTypes.Length]);
            }
            if (kb.rKey.wasPressedThisFrame) { Rotation = (Rotation + 1) & 3; Click("build-rotate", 1100f); }
            if (kb.tKey.wasPressedThisFrame) CycleMaterial(kb.leftShiftKey.isPressed ? -1 : 1);
            if (kb.gKey.wasPressedThisFrame && Target != null) { BeginEdit(Target); return; }
            if (kb.xKey.wasPressedThisFrame && Target != null) { Remove(Target); return; }

            // ghost: snap the aim point to the grid
            Vector3 target = hit && aimHit.distance < placeDistance ? aimHit.point + aimHit.normal * 0.1f : origin + fwd * placeDistance;
            float yaw = player != null ? player.transform.eulerAngles.y : cam.transform.eulerAngles.y;
            float feetY = player != null ? player.transform.position.y : origin.y - 1.6f;
            var placement = ComputePlacement(SelectedType, target, feetY, BuildGrid.FacingFromYaw(yaw), Rotation, minLevel);
            GhostPlacement = placement;
            GhostValid = CanPlace(placement, out var reason);
            GhostReason = reason;
            ShowGhost(placement, GhostValid);

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (GhostValid) TryPlace(placement, out _);
                else Say(reason);
            }
        }

        void UpdateEdit(Keyboard kb, Mouse mouse, Vector3 origin, Vector3 fwd)
        {
            HideGhost();
            if (Editing == null) { EndEdit(); return; }
            HoverSubCell = ComputeHoverSubCell(Editing, origin, fwd, reach);

            if (kb.gKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame) { EndEdit(); return; }
            if (kb.xKey.wasPressedThisFrame) { Remove(Editing); return; }
            if (kb.rKey.wasPressedThisFrame) { Editing.Rotate(); Click("build-rotate", 1100f); }
            if (kb.tKey.wasPressedThisFrame)
            {
                CycleMaterial(kb.leftShiftKey.isPressed ? -1 : 1);
                Editing.SetMaterial(MaterialIndex, MaterialAt(MaterialIndex));
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && HoverSubCell >= 0)
            {
                if (Editing.ToggleSubCell(HoverSubCell)) Click("build-edit", 900f);
                else Say("Mindestens ein Feld muss stehen bleiben");
            }
            if (Editing != null) UpdateMarkers();
        }

        static int TypeIndex(BuildPieceType t) => System.Array.IndexOf(BuildGrid.AllTypes, t);

        static bool DigitPressed(Keyboard kb, int digit) => digit switch
        {
            1 => kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame,
            2 => kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame,
            3 => kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame,
            4 => kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame,
            5 => kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame,
            _ => false,
        };

        // ------------------------------------------------------------------ mode / selection

        public void SetBuildMode(bool on)
        {
            if (on == BuildMode) return;
            if (on && player != null && player.IsDead) return;
            BuildMode = on;
            if (on)
            {
                Rotation = 0;
                if (holder != null)
                {
                    holder.gameObject.SetActive(false);
                    if (cam != null) cam.fieldOfView = holder.normalFov;
                }
                if (player != null) player.ExtraPitch = 0f;
                Say("Baumodus: Holz · 1-5 Teil · LMB bauen · G bearbeiten · B zurück");
            }
            else
            {
                EndEdit();
                HideGhost();
                Target = null;
                if (holder != null && (player == null || !player.IsDead)) holder.gameObject.SetActive(true);
            }
            Click(on ? "build-on" : "build-off", on ? 620f : 420f);
        }

        public void SelectType(BuildPieceType type)
        {
            if (type == SelectedType) return;
            SelectedType = type;
            Click("build-select", 800f);
        }

        public void SetRotation(int rot) => Rotation = rot & 3;

        public void CycleMaterial(int delta)
        {
            int n = MaterialCount;
            MaterialIndex = ((MaterialIndex + delta) % n + n) % n;
            Click("build-select", 800f);
        }

        public Material MaterialAt(int index)
        {
            if (woodMaterials == null || woodMaterials.Length == 0) return _fallbackWood;
            return woodMaterials[Mathf.Clamp(index, 0, woodMaterials.Length - 1)];
        }

        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Pure grid snapping. <paramref name="target"/> is the aim point, <paramref name="facing"/> the player's cardinal direction
        /// (0 = +Z, 1 = +X, 2 = -Z, 3 = -X), <paramref name="rotation"/> the extra rotation chosen with R.
        /// Walls go on the grid line nearest to the target, perpendicular to the view; stairs rise away from the player;
        /// ceilings snap to the nearest grid height above the player's head.
        /// </summary>
        public static BuildPlacement ComputePlacement(BuildPieceType type, Vector3 target, float playerFeetY, int facing, int rotation, int minLevel = 0)
        {
            const float c = BuildGrid.Cell, h = BuildGrid.Height;
            int level = Mathf.Max(minLevel, Mathf.FloorToInt((target.y + 0.5f) / h));
            int cx = Mathf.FloorToInt(target.x / c), cz = Mathf.FloorToInt(target.z / c);
            int rot = (facing + rotation) & 3;
            switch (type)
            {
                case BuildPieceType.Ceiling:
                {
                    int playerLevel = Mathf.Max(minLevel, Mathf.FloorToInt((playerFeetY + 0.5f) / h));
                    int tile = Mathf.Max(playerLevel + 1, Mathf.RoundToInt(target.y / h));
                    return new BuildPlacement(type, cx, cz, tile - 1, rot);
                }
                case BuildPieceType.Wall:
                    return (rot & 1) == 0
                        ? new BuildPlacement(type, cx, Mathf.RoundToInt(target.z / c), level, rot)
                        : new BuildPlacement(type, Mathf.RoundToInt(target.x / c), cz, level, rot);
                default:
                    return new BuildPlacement(type, cx, cz, level, rot);
            }
        }

        public BuildPiece GetPiece(BuildSlot slot) => _pieces.TryGetValue(slot, out var p) ? p : null;
        public bool IsOccupied(BuildSlot slot) => _pieces.ContainsKey(slot);

        public bool CanPlace(BuildPlacement p) => CanPlace(p, out _);

        public bool CanPlace(BuildPlacement p, out string reason)
        {
            reason = "";
            if (p.level < minLevel) { reason = "Zu tief"; return false; }
            if (_pieces.ContainsKey(p.Slot)) { reason = "Hier steht schon etwas"; return false; }
            if (player != null)
            {
                if (Vector3.Distance(p.Position, player.transform.position) > reach + BuildGrid.Cell) { reason = "Zu weit weg"; return false; }
                bool horizontal = p.type == BuildPieceType.Floor || p.type == BuildPieceType.Ceiling;
                if (!horizontal)
                {
                    var b = GhostMesh(p.type).bounds;
                    Vector3 centre = p.Position + p.Rotation * b.center;
                    Vector3 ext = Vector3.Max(b.extents - Vector3.one * 0.05f, Vector3.one * 0.01f);
                    if (Physics.CheckBox(centre, ext, p.Rotation, _playerMask, QueryTriggerInteraction.Ignore)) { reason = "Du stehst im Weg"; return false; }
                }
            }
            return true;
        }

        /// <summary>Places a piece if the slot is free. Returns the new piece.</summary>
        public bool TryPlace(BuildPlacement p, out BuildPiece piece)
        {
            piece = null;
            if (!CanPlace(p, out var reason)) { Say(reason); return false; }
            var go = new GameObject($"{p.type}_{p.cx}_{p.cz}_L{p.level}");
            go.transform.SetParent(Root, false);
            piece = go.AddComponent<BuildPiece>();
            piece.Init(this, p, MaterialIndex, MaterialAt(MaterialIndex), pieceHealth);
            _pieces[p.Slot] = piece;
            Placed++;
            if (p.type == BuildPieceType.Floor || p.type == BuildPieceType.Ceiling) LiftPlayerOnto(p);
            Click("build-place", 260f, 0.14f, 0.9f);
            FxLibrary.PlayAt(ProceduralAudio.Impact(), p.Position + Vector3.up, 0.5f, 0.7f);
            return true;
        }

        /// <summary>A floor placed under the player's feet pushes the player up onto it (like Fortnite).</summary>
        void LiftPlayerOnto(BuildPlacement p)
        {
            if (player == null) return;
            Vector3 pos = player.transform.position, c = p.Position;
            if (Mathf.Abs(pos.x - c.x) > BuildGrid.Cell / 2f || Mathf.Abs(pos.z - c.z) > BuildGrid.Cell / 2f) return;
            float top = c.y + BuildGrid.Thickness;
            if (pos.y < top && pos.y > c.y - 0.6f) player.Nudge(Vector3.up * (top - pos.y + 0.02f));
        }

        public bool Remove(BuildPiece piece)
        {
            if (piece == null) return false;
            if (Editing == piece) EndEdit();
            Unregister(piece);
            FxLibrary.PlayAt(ProceduralAudio.Gunshot("woodbreak", 0.4f, 0.12f, 0.5f, 0.9f), piece.transform.position + Vector3.up, 0.6f);
            Destroy(piece.gameObject);
            return true;
        }

        /// <summary>Called by pieces when they are destroyed (torn down or broken by damage).</summary>
        public void Unregister(BuildPiece piece)
        {
            if (piece == null) return;
            if (_pieces.TryGetValue(piece.Slot, out var current) && current == piece) _pieces.Remove(piece.Slot);
            if (Editing == piece) EndEdit();
            if (Target == piece) Target = null;
        }

        // ------------------------------------------------------------------ editing

        public bool BeginEdit(BuildPiece piece)
        {
            if (piece == null) return false;
            EndEdit();
            Editing = piece;
            HoverSubCell = -1;
            int n = piece.SubCellCount;
            for (int i = 0; i < n; i++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m.name = $"EditTile_{i}";
                Destroy(m.GetComponent<Collider>());
                m.transform.SetParent(piece.transform, false);
                SubCellBox(piece.Type, i, out var centre, out var size);
                m.transform.localPosition = centre;
                m.transform.localScale = size;
                var mr = m.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _markers.Add(mr);
            }
            UpdateMarkers();
            Say(n > 0 ? "Bearbeiten: LMB Feld an/aus · R drehen · T Holz · X abreißen · G fertig" : "Bearbeiten: R drehen · T Holz · X abreißen · G fertig");
            Click("build-edit", 900f);
            return true;
        }

        public void EndEdit()
        {
            foreach (var m in _markers) if (m != null) Destroy(m.gameObject);
            _markers.Clear();
            Editing = null;
            HoverSubCell = -1;
        }

        void UpdateMarkers()
        {
            for (int i = 0; i < _markers.Count; i++)
            {
                var mr = _markers[i];
                if (mr == null) continue;
                bool removed = Editing.IsSubCellRemoved(i);
                mr.sharedMaterial = i == HoverSubCell ? editHoverMaterial : (removed ? editRemovedMaterial : editPresentMaterial);
            }
        }

        /// <summary>Local centre and size of an edit marker.</summary>
        public static void SubCellBox(BuildPieceType type, int index, out Vector3 centre, out Vector3 size)
        {
            const float c = BuildGrid.Cell, h = BuildGrid.Height, t = BuildGrid.Thickness, gap = 0.08f;
            switch (type)
            {
                case BuildPieceType.Wall:
                {
                    int i = index % BuildGrid.WallCols, j = index / BuildGrid.WallCols;
                    float cw = c / BuildGrid.WallCols, ch = h / BuildGrid.WallRows;
                    centre = new Vector3(-c / 2f + (i + 0.5f) * cw, (j + 0.5f) * ch, 0f);
                    size = new Vector3(cw - gap, ch - gap, t + gap);
                    return;
                }
                case BuildPieceType.Roof:
                {
                    int ix = index % BuildGrid.TileDiv, iz = index / BuildGrid.TileDiv;
                    float q = c / BuildGrid.TileDiv;
                    centre = new Vector3(-c / 2f + (ix + 0.5f) * q, 0.8f, -c / 2f + (iz + 0.5f) * q);
                    size = new Vector3(q - gap, 1.2f, q - gap);
                    return;
                }
                default:
                {
                    int ix = index % BuildGrid.TileDiv, iz = index / BuildGrid.TileDiv;
                    float q = c / BuildGrid.TileDiv;
                    centre = new Vector3(-c / 2f + (ix + 0.5f) * q, t / 2f, -c / 2f + (iz + 0.5f) * q);
                    size = new Vector3(q - gap, t + gap, q - gap);
                    return;
                }
            }
        }

        /// <summary>Sub-cell of <paramref name="piece"/> hit by the view ray (intersection with the piece's own plane in local space), -1 if none.</summary>
        public static int ComputeHoverSubCell(BuildPiece piece, Vector3 origin, Vector3 direction, float maxDistance)
        {
            if (piece == null || piece.SubCellCount == 0) return -1;
            var tr = piece.transform;
            Vector3 lo = tr.InverseTransformPoint(origin);
            Vector3 ld = tr.InverseTransformDirection(direction);
            const float c = BuildGrid.Cell, h = BuildGrid.Height;
            if (piece.Type == BuildPieceType.Wall)
            {
                if (Mathf.Abs(ld.z) < 1e-5f) return -1;
                float s = -lo.z / ld.z;
                if (s < 0f || s > maxDistance) return -1;
                Vector3 p = lo + ld * s;
                int i = Mathf.FloorToInt((p.x + c / 2f) / (c / BuildGrid.WallCols));
                int j = Mathf.FloorToInt(p.y / (h / BuildGrid.WallRows));
                if (i < 0 || i >= BuildGrid.WallCols || j < 0 || j >= BuildGrid.WallRows) return -1;
                return j * BuildGrid.WallCols + i;
            }
            float planeY = piece.Type == BuildPieceType.Roof ? 0.8f : BuildGrid.Thickness / 2f;
            if (Mathf.Abs(ld.y) < 1e-5f) return -1;
            float sy = (planeY - lo.y) / ld.y;
            if (sy < 0f || sy > maxDistance) return -1;
            Vector3 q = lo + ld * sy;
            int ix = Mathf.FloorToInt((q.x + c / 2f) / (c / BuildGrid.TileDiv));
            int iz = Mathf.FloorToInt((q.z + c / 2f) / (c / BuildGrid.TileDiv));
            if (ix < 0 || ix >= BuildGrid.TileDiv || iz < 0 || iz >= BuildGrid.TileDiv) return -1;
            return iz * BuildGrid.TileDiv + ix;
        }

        // ------------------------------------------------------------------ ghost

        Mesh GhostMesh(BuildPieceType type)
        {
            if (!_ghostMeshes.TryGetValue(type, out var mesh) || mesh == null)
            {
                mesh = BuildMeshFactory.BuildVisual(type, 0);
                _ghostMeshes[type] = mesh;
            }
            return mesh;
        }

        void ShowGhost(BuildPlacement p, bool valid)
        {
            if (_ghost == null)
            {
                _ghost = new GameObject("BuildGhost");
                _ghostMf = _ghost.AddComponent<MeshFilter>();
                _ghostMr = _ghost.AddComponent<MeshRenderer>();
                _ghostMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ghostMr.receiveShadows = false;
            }
            _ghost.SetActive(true);
            var mesh = GhostMesh(p.type);
            if (_ghostMf.sharedMesh != mesh) _ghostMf.sharedMesh = mesh;
            _ghostMr.sharedMaterial = valid ? ghostValidMaterial : ghostInvalidMaterial;
            _ghost.transform.SetPositionAndRotation(p.Position, p.Rotation);
        }

        void HideGhost()
        {
            if (_ghost != null && _ghost.activeSelf) _ghost.SetActive(false);
        }

        public bool GhostVisible => _ghost != null && _ghost.activeSelf;

        // ------------------------------------------------------------------ helpers

        void Say(string msg)
        {
            Message = msg;
            MessageTime = Time.time;
        }

        void Click(string key, float pitch, float duration = 0.06f, float volume = 0.45f)
        {
            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.Click(key, pitch, duration), volume);
        }

        /// <summary>Runtime fallbacks so the system also works without the builder-made materials (tests, old prefabs).</summary>
        void EnsureMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (woodMaterials == null || woodMaterials.Length == 0)
            {
                _fallbackWood = lit != null ? new Material(lit) : new Material(Shader.Find("Standard"));
                _fallbackWood.name = "Wood_Fallback";
                _fallbackWood.color = new Color(0.62f, 0.42f, 0.24f);
            }
            if (ghostValidMaterial == null) ghostValidMaterial = MakeTransparent(lit, "Ghost_Valid", new Color(0.3f, 1f, 0.45f, 0.35f));
            if (ghostInvalidMaterial == null) ghostInvalidMaterial = MakeTransparent(lit, "Ghost_Invalid", new Color(1f, 0.25f, 0.2f, 0.35f));
            if (editPresentMaterial == null) editPresentMaterial = MakeTransparent(lit, "Edit_Present", new Color(0.3f, 0.9f, 1f, 0.22f));
            if (editRemovedMaterial == null) editRemovedMaterial = MakeTransparent(lit, "Edit_Removed", new Color(1f, 0.3f, 0.2f, 0.28f));
            if (editHoverMaterial == null) editHoverMaterial = MakeTransparent(lit, "Edit_Hover", new Color(1f, 0.9f, 0.3f, 0.55f));
        }

        public static Material MakeTransparent(Shader lit, string name, Color color)
        {
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(lit) { name = name };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            m.color = color;
            return m;
        }
    }
}
