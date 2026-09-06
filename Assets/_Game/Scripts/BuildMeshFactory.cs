using System.Collections.Generic;
using UnityEngine;

namespace GunMan
{
    /// <summary>
    /// Generates the meshes of the wooden building pieces at runtime.
    /// Local space: the cell centre is at XZ = 0, Y runs up from the storey base. Walls lie in the local XY plane (z = 0),
    /// stairs rise towards local +Z. UVs are planar in metres (scaled by <see cref="UvScale"/>) so every wood texture tiles.
    /// </summary>
    public static class BuildMeshFactory
    {
        public const float UvScale = 0.5f;   // one texture repeat every 2 m
        public const int StairSteps = 10;
        public const float StairSlab = 0.3f; // thickness of the slab under the steps

        const int NegX = 1, PosX = 2, NegY = 4, PosY = 8, NegZ = 16, PosZ = 32;

        /// <summary>Render mesh of a piece with the given removed sub-cells (bit i = sub-cell i removed).</summary>
        public static Mesh BuildVisual(BuildPieceType type, int removedMask)
        {
            var md = new MeshData();
            switch (type)
            {
                case BuildPieceType.Wall: AddWall(md, removedMask); break;
                case BuildPieceType.Floor:
                case BuildPieceType.Ceiling: AddTile(md, removedMask); break;
                case BuildPieceType.Stairs: AddStairs(md, false); break;
                case BuildPieceType.Roof: AddRoof(md, removedMask); break;
            }
            return md.ToMesh($"{type}_{removedMask}");
        }

        /// <summary>Collision mesh. Identical to the visual mesh except for stairs, which use a smooth ramp.</summary>
        public static Mesh BuildCollision(BuildPieceType type, int removedMask)
        {
            if (type != BuildPieceType.Stairs) return BuildVisual(type, removedMask);
            var md = new MeshData();
            AddStairs(md, true);
            return md.ToMesh("Stairs_collision");
        }

        static bool Present(int mask, int index) => (mask & (1 << index)) == 0;

        // ------------------------------------------------------------------ pieces

        static void AddWall(MeshData md, int mask)
        {
            const int cols = BuildGrid.WallCols, rows = BuildGrid.WallRows;
            float cw = BuildGrid.Cell / cols, ch = BuildGrid.Height / rows, t = BuildGrid.Thickness;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int idx = j * cols + i;
                    if (!Present(mask, idx)) continue;
                    var min = new Vector3(-BuildGrid.Cell / 2f + i * cw, j * ch, -t / 2f);
                    var max = new Vector3(min.x + cw, min.y + ch, t / 2f);
                    int skip = 0;
                    if (i > 0 && Present(mask, idx - 1)) skip |= NegX;
                    if (i < cols - 1 && Present(mask, idx + 1)) skip |= PosX;
                    if (j > 0 && Present(mask, idx - cols)) skip |= NegY;
                    if (j < rows - 1 && Present(mask, idx + cols)) skip |= PosY;
                    md.Box(min, max, skip);
                }
        }

        static void AddTile(MeshData md, int mask)
        {
            const int div = BuildGrid.TileDiv;
            float q = BuildGrid.Cell / div, t = BuildGrid.Thickness;
            for (int iz = 0; iz < div; iz++)
                for (int ix = 0; ix < div; ix++)
                {
                    int idx = iz * div + ix;
                    if (!Present(mask, idx)) continue;
                    var min = new Vector3(-BuildGrid.Cell / 2f + ix * q, 0f, -BuildGrid.Cell / 2f + iz * q);
                    var max = new Vector3(min.x + q, t, min.z + q);
                    int skip = 0;
                    if (ix > 0 && Present(mask, idx - 1)) skip |= NegX;
                    if (ix < div - 1 && Present(mask, idx + 1)) skip |= PosX;
                    if (iz > 0 && Present(mask, idx - div)) skip |= NegZ;
                    if (iz < div - 1 && Present(mask, idx + div)) skip |= PosZ;
                    md.Box(min, max, skip);
                }
        }

        /// <summary>Pyramid roof split into four quarters (2x2 edit grid). Every quarter carries a bottom face so the piece stays closed.</summary>
        static void AddRoof(MeshData md, int mask)
        {
            const int div = BuildGrid.TileDiv;
            float half = BuildGrid.Cell / 2f, h = BuildGrid.RoofHeight;
            var apex = new Vector3(0f, h, 0f);
            var centre = Vector3.zero;
            for (int iz = 0; iz < div; iz++)
                for (int ix = 0; ix < div; ix++)
                {
                    int idx = iz * div + ix;
                    if (!Present(mask, idx)) continue;
                    float sx = ix == 0 ? -1f : 1f, sz = iz == 0 ? -1f : 1f;
                    var corner = new Vector3(sx * half, 0f, sz * half);
                    var midX = new Vector3(0f, 0f, sz * half);   // middle of the outer z edge
                    var midZ = new Vector3(sx * half, 0f, 0f);   // middle of the outer x edge
                    md.Tri(corner, apex, midX, new Vector3(0f, 1f, sz));   // sloped face on the ±Z side
                    md.Tri(corner, midZ, apex, new Vector3(sx, 1f, 0f));   // sloped face on the ±X side
                    md.Quad(corner, midX, centre, midZ, Vector3.down);      // underside
                    // vertical cut faces where the neighbouring quarter is missing
                    bool neighbourX = Present(mask, iz * div + (1 - ix));
                    bool neighbourZ = Present(mask, (1 - iz) * div + ix);
                    if (!neighbourX) md.Tri(midX, centre, apex, new Vector3(-sx, 0f, 0f));
                    if (!neighbourZ) md.Tri(midZ, centre, apex, new Vector3(0f, 0f, -sz));
                }
        }

        /// <summary>
        /// Stairs rising towards +Z: a sloped slab with <see cref="StairSteps"/> treads on top. The collision variant is only the slab,
        /// lifted by half a riser so the capsule glides over the treads and arrives level with the floor tile of the storey above.
        /// </summary>
        static void AddStairs(MeshData md, bool collision)
        {
            float half = BuildGrid.Cell / 2f, h = BuildGrid.Height;
            float rise = h / StairSteps, run = BuildGrid.Cell / StairSteps;
            float lift = collision ? rise / 2f : 0f;
            float s = StairSlab;

            var tnl = new Vector3(-half, lift, -half); var tnr = new Vector3(half, lift, -half);
            var tfl = new Vector3(-half, h + lift, half); var tfr = new Vector3(half, h + lift, half);
            var bnl = new Vector3(-half, -s, -half); var bnr = new Vector3(half, -s, -half);
            var bfl = new Vector3(-half, h - s, half); var bfr = new Vector3(half, h - s, half);
            md.Quad(tnl, tfl, tfr, tnr, new Vector3(0f, 1f, -1f));   // slope top
            md.Quad(bnl, bnr, bfr, bfl, new Vector3(0f, -1f, 1f));   // slope bottom
            md.Quad(tnl, bnl, bfl, tfl, Vector3.left);
            md.Quad(tnr, tfr, bfr, bnr, Vector3.right);
            md.Quad(tnl, tnr, bnr, bnl, Vector3.back);
            md.Quad(tfl, bfl, bfr, tfr, Vector3.forward);
            if (collision) return;

            for (int i = 0; i < StairSteps; i++)
            {
                float z0 = -half + i * run;
                md.Box(new Vector3(-half, i * rise, z0), new Vector3(half, (i + 1) * rise, z0 + run), NegY | PosZ);
            }
        }

        // ------------------------------------------------------------------ mesh assembly

        class MeshData
        {
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<Vector3> _n = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<int> _t = new List<int>();

            /// <summary>Adds a triangle whose front face points roughly along <paramref name="outward"/> (winding is fixed automatically).</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, outward) < 0f) { (b, c) = (c, b); n = -n; }
                if (n.sqrMagnitude < 1e-10f) return;
                n.Normalize();
                int baseIndex = _v.Count;
                foreach (var p in new[] { a, b, c })
                {
                    _v.Add(p);
                    _n.Add(n);
                    _uv.Add(PlanarUv(p, n));
                }
                _t.Add(baseIndex); _t.Add(baseIndex + 1); _t.Add(baseIndex + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            public void Box(Vector3 min, Vector3 max, int skipFaces = 0)
            {
                var p000 = new Vector3(min.x, min.y, min.z); var p001 = new Vector3(min.x, min.y, max.z);
                var p010 = new Vector3(min.x, max.y, min.z); var p011 = new Vector3(min.x, max.y, max.z);
                var p100 = new Vector3(max.x, min.y, min.z); var p101 = new Vector3(max.x, min.y, max.z);
                var p110 = new Vector3(max.x, max.y, min.z); var p111 = new Vector3(max.x, max.y, max.z);
                if ((skipFaces & PosY) == 0) Quad(p010, p011, p111, p110, Vector3.up);
                if ((skipFaces & NegY) == 0) Quad(p000, p100, p101, p001, Vector3.down);
                if ((skipFaces & PosX) == 0) Quad(p100, p110, p111, p101, Vector3.right);
                if ((skipFaces & NegX) == 0) Quad(p001, p011, p010, p000, Vector3.left);
                if ((skipFaces & PosZ) == 0) Quad(p101, p111, p011, p001, Vector3.forward);
                if ((skipFaces & NegZ) == 0) Quad(p000, p010, p110, p100, Vector3.back);
            }

            static Vector2 PlanarUv(Vector3 p, Vector3 n)
            {
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                Vector2 uv;
                if (ay >= ax && ay >= az) uv = new Vector2(p.x, p.z);
                else if (ax >= az) uv = new Vector2(p.z, p.y);
                else uv = new Vector2(p.x, p.y);
                return uv * UvScale;
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
