using System;
using UnityEngine;

namespace GunMan
{
    /// <summary>The five wooden building pieces (Fortnite style).</summary>
    public enum BuildPieceType { Wall, Floor, Ceiling, Stairs, Roof }

    /// <summary>Grid constants of the building system. One cell is a cube of <see cref="Cell"/> x <see cref="Height"/> x <see cref="Cell"/> metres.</summary>
    public static class BuildGrid
    {
        public const float Cell = 4f;          // footprint of one cell (m)
        public const float Height = 4f;        // one storey (m)
        public const float Thickness = 0.2f;   // walls / floors
        public const float RoofHeight = 2f;    // pyramid apex above the storey base
        public const int WallCols = 3, WallRows = 3;   // wall edit grid (3x3 like Fortnite)
        public const int TileDiv = 2;                  // floor / ceiling / roof edit grid (2x2)

        public static readonly BuildPieceType[] AllTypes = { BuildPieceType.Wall, BuildPieceType.Floor, BuildPieceType.Ceiling, BuildPieceType.Stairs, BuildPieceType.Roof };

        /// <summary>Number of editable sub-cells of a piece type (0 = the piece can only be rotated).</summary>
        public static int SubCellCount(BuildPieceType type) => type switch
        {
            BuildPieceType.Wall => WallCols * WallRows,
            BuildPieceType.Stairs => 0,
            _ => TileDiv * TileDiv,
        };

        public static string DisplayName(BuildPieceType type) => type switch
        {
            BuildPieceType.Wall => "Wand",
            BuildPieceType.Floor => "Boden",
            BuildPieceType.Ceiling => "Decke",
            BuildPieceType.Stairs => "Treppe",
            BuildPieceType.Roof => "Dach",
            _ => type.ToString(),
        };

        /// <summary>Cardinal direction index (0 = +Z, 1 = +X, 2 = -Z, 3 = -X) closest to a yaw angle in degrees.</summary>
        public static int FacingFromYaw(float yawDegrees) => Mathf.RoundToInt(yawDegrees / 90f) & 3;
    }

    /// <summary>
    /// Identifies the place a piece occupies so that two pieces never overlap.
    /// kind 0 = horizontal tile (floor / ceiling) at grid height <c>level</c>; kind 1 = wall along X on the z grid line <c>b</c>;
    /// kind 2 = wall along Z on the x grid line <c>a</c>; kind 3 = cell volume (stairs / roof).
    /// </summary>
    public readonly struct BuildSlot : IEquatable<BuildSlot>
    {
        public readonly int kind, a, b, level;

        public BuildSlot(int kind, int a, int b, int level)
        {
            this.kind = kind; this.a = a; this.b = b; this.level = level;
        }

        public bool Equals(BuildSlot o) => kind == o.kind && a == o.a && b == o.b && level == o.level;
        public override bool Equals(object obj) => obj is BuildSlot s && Equals(s);
        public override int GetHashCode() => HashCode.Combine(kind, a, b, level);
        public override string ToString() => $"slot(k{kind} {a},{b} L{level})";
    }

    /// <summary>Where a piece goes: type, cell, storey and rotation (0..3, 90° steps). Pure data, computed by <see cref="BuildSystem.ComputePlacement"/>.</summary>
    [Serializable]
    public struct BuildPlacement
    {
        public BuildPieceType type;
        /// <summary>Cell indices. For walls: rot even → cx = cell, cz = z grid line; rot odd → cx = x grid line, cz = cell.</summary>
        public int cx, cz;
        /// <summary>Storey index (0 = ground). A ceiling stores the storey it covers (its tile sits at level + 1).</summary>
        public int level;
        public int rot;

        public BuildPlacement(BuildPieceType type, int cx, int cz, int level, int rot)
        {
            this.type = type; this.cx = cx; this.cz = cz; this.level = level; this.rot = rot & 3;
        }

        public BuildSlot Slot => type switch
        {
            BuildPieceType.Floor => new BuildSlot(0, cx, cz, level),
            BuildPieceType.Ceiling => new BuildSlot(0, cx, cz, level + 1),
            BuildPieceType.Wall => (rot & 1) == 0 ? new BuildSlot(1, cx, cz, level) : new BuildSlot(2, cx, cz, level),
            _ => new BuildSlot(3, cx, cz, level),
        };

        public Vector3 Position
        {
            get
            {
                const float c = BuildGrid.Cell, h = BuildGrid.Height;
                switch (type)
                {
                    case BuildPieceType.Ceiling: return new Vector3(cx * c + c / 2f, (level + 1) * h, cz * c + c / 2f);
                    case BuildPieceType.Wall:
                        return (rot & 1) == 0
                            ? new Vector3(cx * c + c / 2f, level * h, cz * c)
                            : new Vector3(cx * c, level * h, cz * c + c / 2f);
                    default: return new Vector3(cx * c + c / 2f, level * h, cz * c + c / 2f);
                }
            }
        }

        public Quaternion Rotation => Quaternion.Euler(0f, rot * 90f, 0f);

        public override string ToString() => $"{type} cell({cx},{cz}) L{level} rot{rot}";
    }
}
