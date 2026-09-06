using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunMan.EditorTools
{
    /// <summary>
    /// Places modular pieces of the Quaternius Medieval Village kit. All placements are bounds-driven so
    /// arbitrary pivots in the FBX files do not matter.
    /// </summary>
    public class KitBuilder
    {
        readonly Dictionary<string, GameObject> _assets = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Vector3> _sizes = new Dictionary<string, Vector3>();
        public readonly System.Random Rng;

        public KitBuilder(int seed)
        {
            Rng = new System.Random(seed);
        }

        public GameObject Asset(string name)
        {
            if (_assets.TryGetValue(name, out var a)) return a;
            a = BuildUtil.LoadModel(BuildUtil.VillageFbx, name);
            _assets[name] = a;
            return a;
        }

        public bool Has(string name) => Asset(name) != null;

        /// <summary>Render-bounds size of a piece at identity rotation.</summary>
        public Vector3 Size(string name)
        {
            if (_sizes.TryGetValue(name, out var s)) return s;
            var asset = Asset(name);
            if (asset == null) return Vector3.zero;
            var temp = BuildUtil.Instantiate(asset);
            s = BuildUtil.WorldBounds(temp).size;
            Object.DestroyImmediate(temp);
            _sizes[name] = s;
            return s;
        }

        public GameObject Place(string name, Transform parent, Vector3 bottomCenter, float yRot = 0f, Vector3? scale = null, bool isStatic = true)
        {
            var asset = Asset(name);
            if (asset == null) return null;
            var go = BuildUtil.Instantiate(asset, parent);
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f) * asset.transform.rotation;
            if (scale.HasValue) go.transform.localScale = Vector3.Scale(asset.transform.localScale, scale.Value);
            BuildUtil.AlignBottomCenter(go, bottomCenter);
            if (isStatic) BuildUtil.SetStaticRecursive(go, true);
            return go;
        }

        public GameObject PlaceCentered(string name, Transform parent, Vector3 center, float yRot = 0f, Vector3? scale = null, bool isStatic = true)
        {
            var asset = Asset(name);
            if (asset == null) return null;
            var go = BuildUtil.Instantiate(asset, parent);
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f) * asset.transform.rotation;
            if (scale.HasValue) go.transform.localScale = Vector3.Scale(asset.transform.localScale, scale.Value);
            BuildUtil.AlignCenter(go, center);
            if (isStatic) BuildUtil.SetStaticRecursive(go, true);
            return go;
        }

        public float Rand(float min, float max) => BuildUtil.RandRange(Rng, min, max);
        public int RandInt(int minInclusive, int maxExclusive) => Rng.Next(minInclusive, maxExclusive);
        public bool Chance(float p) => Rng.NextDouble() < p;
        public T Pick<T>(params T[] items) => items[Rng.Next(items.Length)];

        // ------------------------------------------------------------------ houses

        public class HouseStyle
        {
            public string groundWall = "Wall_UnevenBrick_Straight";
            public string groundWindow = "Wall_UnevenBrick_Window_Wide_Flat";
            public string groundDoor = "Wall_UnevenBrick_Door_Flat";
            public string upperWall = "Wall_Plaster_Straight";
            public string upperWindow = "Wall_Plaster_Window_Wide_Flat";
            public string windowInsert = "Window_Wide_Flat1";
            public string shutters = "WindowShutters_Wide_Flat_Open";
            public string door = "Door_1_Flat";
            public string corner = "Corner_Exterior_Wood";
            public string floorUpper = "Floor_WoodDark";
            public string floorGround = "Floor_Brick";
            public string roofPrefix = "Roof_RoundTiles_";
            public string chimney = "Prop_Chimney";
        }

        public struct HouseInfo
        {
            public Vector3 center;
            public float width, depth, height;
            public Vector3 doorWorldPos;
            public Vector3 doorOutward;
        }

        /// <summary>
        /// Builds a rectangular house of nx by nz wall segments with the given number of storeys.
        /// The house is centred at <paramref name="center"/> (ground level) and rotated by <paramref name="yRot"/>;
        /// the door is on the local -Z side.
        /// </summary>
        public HouseInfo BuildHouse(Transform parent, Vector3 center, float yRot, int nx, int nz, int storeys, HouseStyle style, string name = "House")
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = center;
            root.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            var wallSize = Size(style.groundWall);
            bool wallAlongX = wallSize.x >= wallSize.z;
            float L = wallAlongX ? wallSize.x : wallSize.z;
            float H = wallSize.y;
            float W = nx * L, D = nz * L;
            float baseRot = wallAlongX ? 0f : 90f; // rotation that makes a wall run along local X

            int doorSegment = nx / 2;
            for (int s = 0; s < storeys; s++)
            {
                float y = s * H;
                bool ground = s == 0;
                string wall = ground ? style.groundWall : style.upperWall;
                string window = ground ? style.groundWindow : style.upperWindow;

                // front (-Z) and back (+Z)
                for (int i = 0; i < nx; i++)
                {
                    float x = -W / 2f + L / 2f + i * L;
                    // front
                    bool isDoor = ground && i == doorSegment;
                    bool isWindow = !isDoor && Chance(ground ? 0.45f : 0.7f);
                    PlaceWall(root.transform, isDoor ? style.groundDoor : (isWindow ? window : wall), new Vector3(x, y, -D / 2f), baseRot, isWindow, isDoor, style, s);
                    // back
                    bool bWindow = Chance(ground ? 0.35f : 0.6f);
                    PlaceWall(root.transform, bWindow ? window : wall, new Vector3(x, y, D / 2f), baseRot + 180f, bWindow, false, style, s);
                }
                // left (-X) and right (+X)
                for (int j = 0; j < nz; j++)
                {
                    float z = -D / 2f + L / 2f + j * L;
                    bool lWindow = Chance(0.4f);
                    PlaceWall(root.transform, lWindow ? window : wall, new Vector3(-W / 2f, y, z), baseRot + 90f, lWindow, false, style, s);
                    bool rWindow = Chance(0.4f);
                    PlaceWall(root.transform, rWindow ? window : wall, new Vector3(W / 2f, y, z), baseRot - 90f, rWindow, false, style, s);
                }

                // corners
                if (Has(style.corner))
                {
                    LocalPlace(style.corner, root.transform, new Vector3(-W / 2f, y, -D / 2f), 0f);
                    LocalPlace(style.corner, root.transform, new Vector3(W / 2f, y, -D / 2f), 90f);
                    LocalPlace(style.corner, root.transform, new Vector3(W / 2f, y, D / 2f), 180f);
                    LocalPlace(style.corner, root.transform, new Vector3(-W / 2f, y, D / 2f), 270f);
                }

                // floor
                string floor = ground ? style.floorGround : style.floorUpper;
                if (Has(floor))
                {
                    var fs = Size(floor);
                    float tile = Mathf.Max(fs.x, fs.z);
                    int fx = Mathf.Max(1, Mathf.RoundToInt(W / tile));
                    int fz = Mathf.Max(1, Mathf.RoundToInt(D / tile));
                    float sx = W / (fx * tile), sz = D / (fz * tile);
                    for (int a = 0; a < fx; a++)
                        for (int b = 0; b < fz; b++)
                        {
                            float x = -W / 2f + tile * sx * (a + 0.5f);
                            float z = -D / 2f + tile * sz * (b + 0.5f);
                            LocalPlace(floor, root.transform, new Vector3(x, y + (ground ? 0.01f : -fs.y * 0.5f), z), 0f, new Vector3(sx, 1f, sz));
                        }
                }
            }

            // roof
            float roofY = storeys * H;
            string roof = PickRoof(style.roofPrefix, W, D, out bool rotated, out Vector3 roofScale);
            GameObject roofGo = null;
            if (roof != null)
                roofGo = LocalPlace(roof, root.transform, new Vector3(0f, roofY, 0f), rotated ? 90f : 0f, roofScale);

            // chimney
            if (!string.IsNullOrEmpty(style.chimney) && Has(style.chimney) && roofGo != null && Chance(0.8f))
            {
                var rb = BuildUtil.WorldBounds(roofGo);
                float cy = Mathf.Lerp(rb.min.y, rb.max.y, 0.55f);
                var chimneyLocal = new Vector3(Rand(-W * 0.3f, W * 0.3f), cy - center.y, Rand(-D * 0.15f, D * 0.15f));
                LocalPlace(style.chimney, root.transform, chimneyLocal, 0f);
            }

            var info = new HouseInfo
            {
                center = center,
                width = W,
                depth = D,
                height = roofY,
                doorWorldPos = root.transform.TransformPoint(new Vector3(-W / 2f + L / 2f + doorSegment * L, 0f, -D / 2f)),
                doorOutward = root.transform.TransformDirection(Vector3.back),
            };
            return info;
        }

        void PlaceWall(Transform root, string piece, Vector3 local, float yRot, bool window, bool door, HouseStyle style, int storey)
        {
            var go = LocalPlace(piece, root, local, yRot);
            if (go == null) return;
            if (window && Has(style.windowInsert))
            {
                var ins = LocalPlace(style.windowInsert, root, local, yRot);
                // align insert to wall centre height
                if (ins != null)
                {
                    var wb = BuildUtil.WorldBounds(go);
                    var ib = BuildUtil.WorldBounds(ins);
                    ins.transform.position += new Vector3(0f, wb.center.y - ib.center.y, 0f);
                }
                if (Has(style.shutters) && Chance(0.6f))
                {
                    var sh = LocalPlace(style.shutters, root, local, yRot);
                    if (sh != null)
                    {
                        var wb = BuildUtil.WorldBounds(go);
                        var sb = BuildUtil.WorldBounds(sh);
                        sh.transform.position += new Vector3(0f, wb.center.y - sb.center.y, 0f);
                    }
                }
            }
            if (door && Has(style.door))
            {
                var d = LocalPlace(style.door, root, local, yRot + (Chance(0.5f) ? 0f : 0f));
                if (d != null)
                {
                    // door leaf: nudge inward so it sits in the opening
                    var wb = BuildUtil.WorldBounds(go);
                    var db = BuildUtil.WorldBounds(d);
                    d.transform.position += new Vector3(0f, wb.min.y - db.min.y, 0f);
                }
            }
        }

        /// <summary>Place using coordinates local to <paramref name="root"/> (rotation added to root rotation).</summary>
        public GameObject LocalPlace(string name, Transform root, Vector3 localBottomCenter, float localYRot, Vector3? scale = null)
        {
            float rootYaw = root.eulerAngles.y;
            var world = root.TransformPoint(localBottomCenter);
            return Place(name, root, world, rootYaw + localYRot, scale);
        }

        string PickRoof(string prefix, float w, float d, out bool rotated, out Vector3 scale)
        {
            rotated = false;
            scale = Vector3.one;
            var candidates = AssetDatabase.FindAssets("t:Model", new[] { BuildUtil.VillageFbx })
                .Select(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)))
                .Where(n => n.StartsWith(prefix)).ToList();
            if (candidates.Count == 0) return null;
            string best = null;
            float bestScore = float.MaxValue;
            bool bestRot = false;
            float bestA = 1f, bestB = 1f;
            foreach (var c in candidates)
            {
                // roofs are named ..._AxB where A/B are the wall lengths (metres) they are designed for;
                // pieces without a size suffix (e.g. Roof_Tower_RoundTiles) use their render bounds instead.
                float a, b;
                var m = System.Text.RegularExpressions.Regex.Match(c, @"_(\d+)x(\d+)$");
                if (m.Success) { a = float.Parse(m.Groups[1].Value); b = float.Parse(m.Groups[2].Value); }
                else { var s = Size(c); a = s.x / 1.35f; b = s.z / 1.35f; }
                if (a < 0.1f || b < 0.1f) continue;
                float score0 = Mathf.Abs(Mathf.Log(w / a)) + Mathf.Abs(Mathf.Log(d / b));
                float score1 = Mathf.Abs(Mathf.Log(w / b)) + Mathf.Abs(Mathf.Log(d / a));
                if (score0 < bestScore) { bestScore = score0; best = c; bestRot = false; bestA = a; bestB = b; }
                if (score1 < bestScore) { bestScore = score1; best = c; bestRot = true; bestA = a; bestB = b; }
            }
            if (best == null) return null;
            rotated = bestRot;
            float sx = w / (bestRot ? bestB : bestA);
            float sz = d / (bestRot ? bestA : bestB);
            // scale is applied in the piece's own local axes, i.e. before rotation
            scale = bestRot ? new Vector3(sz, 1f, sx) : new Vector3(sx, 1f, sz);
            scale.y = Mathf.Clamp((sx + sz) * 0.5f, 0.85f, 1.5f);
            return best;
        }

        // ------------------------------------------------------------------ misc structures

        public void BuildFenceLine(Transform parent, Vector3 from, Vector3 to, string piece = "Prop_WoodenFence_Single")
        {
            if (!Has(piece)) return;
            var s = Size(piece);
            float len = Mathf.Max(s.x, s.z);
            bool alongX = s.x >= s.z;
            Vector3 dir = (to - from);
            float total = dir.magnitude;
            if (total < 0.1f) return;
            dir /= total;
            int n = Mathf.Max(1, Mathf.FloorToInt(total / len));
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (alongX ? 90f : 0f);
            for (int i = 0; i < n; i++)
            {
                var p = from + dir * (len * (i + 0.5f));
                Place(piece, parent, p, yaw);
            }
        }

        public void BuildFloorArea(Transform parent, Vector3 center, float width, float depth, string piece, float y = 0f)
        {
            if (!Has(piece)) return;
            var s = Size(piece);
            float tile = Mathf.Max(s.x, s.z);
            int nx = Mathf.Max(1, Mathf.RoundToInt(width / tile));
            int nz = Mathf.Max(1, Mathf.RoundToInt(depth / tile));
            float sx = width / (nx * tile), sz = depth / (nz * tile);
            var area = new GameObject($"Floor_{piece}");
            area.transform.SetParent(parent, false);
            for (int a = 0; a < nx; a++)
                for (int b = 0; b < nz; b++)
                {
                    float x = center.x - width / 2f + tile * sx * (a + 0.5f);
                    float z = center.z - depth / 2f + tile * sz * (b + 0.5f);
                    Place(piece, area.transform, new Vector3(x, y, z), 0f, new Vector3(sx, 1f, sz));
                }
        }

        public void BuildWallLine(Transform parent, Vector3 from, Vector3 to, int storeys, string piece = "Wall_UnevenBrick_Straight")
        {
            if (!Has(piece)) return;
            var s = Size(piece);
            float len = Mathf.Max(s.x, s.z);
            bool alongX = s.x >= s.z;
            Vector3 dir = to - from;
            float total = dir.magnitude;
            dir /= total;
            int n = Mathf.Max(1, Mathf.RoundToInt(total / len));
            float step = total / n;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (alongX ? 90f : 0f);
            for (int st = 0; st < storeys; st++)
                for (int i = 0; i < n; i++)
                {
                    var p = from + dir * (step * (i + 0.5f)) + Vector3.up * (st * s.y);
                    Place(piece, parent, p, yaw, new Vector3(alongX ? step / len : 1f, 1f, alongX ? 1f : step / len));
                }
        }
    }
}
