using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The level's end: past E5 the last houses give way to the first fields of the farmland (L3, The Fallow
    // Fields). Ploughed fields lie either side of the road, their furrows painted into the ground (L2Look) and
    // rows of dead stalks standing on the ridges; low field walls split them, fences line the road (L2Village's
    // street edges), scarecrows stand in the rows like waiting figures, a hay cart lies abandoned on the verge,
    // and a burnt-out farmstead with its well smoulders off to the east. All of it stays in the night palette.
    // Nothing starts on a line: the fields fade in along L2Layout.Rural's wandering edge, the last houses keep
    // kitchen gardens, and straw blows out along the verges into the village.
    public static class L2Farmland
    {
        private const int Seed = 4343;
        public const float RowSpacing = 1.6f;

        // Where each side's two fields meet (a field wall runs along it).
        private const float SplitWest = 134f;
        private const float SplitEast = 129f;

        // The road's centre line at a given z (the route's nodes from E5 to the exit).
        public static float RoadX(float z)
        {
            var route = L2Layout.Route;
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = route[i - 1].P, b = route[i].P;
                if (a.y > 100f && z >= a.y && z <= b.y)
                {
                    return Mathf.Lerp(a.x, b.x, Mathf.InverseLerp(a.y, b.y, z));
                }
            }

            return route[route.Length - 1].P.x;
        }

        // Distance from the field road's centre line.
        public static float RoadDistance(Vector2 p)
        {
            var route = L2Layout.Route;
            float best = float.MaxValue;
            for (int i = 1; i < route.Length; i++)
            {
                if (route[i - 1].P.y > 100f)
                {
                    best = Mathf.Min(best, L2Layout.DistanceToSegment(p, route[i - 1].P, route[i].P, out _));
                }
            }

            return best;
        }

        // How fully p has become field: 0 in the village, 1 once the ploughing starts in earnest. It follows the
        // rural band's wandering edge, a little behind it (yards and gardens first, then fields).
        public static float FieldBlend(Vector2 p)
        {
            return L2Layout.IsInArena(p, 3f) ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(110f, 121f, p.y + L2Layout.RuralWander(p)));
        }

        // Is p in a ploughed field (not the road, its verge or a field wall), and which way do its rows run?
        public static bool InField(Vector2 p, out Vector2 rowDirection)
        {
            return InField(p, out rowDirection, out _);
        }

        public static bool InField(Vector2 p, out Vector2 rowDirection, out float blend)
        {
            rowDirection = Vector2.up;
            blend = FieldBlend(p);
            if (blend < 0.02f || L2Layout.SignedDistance(p) < 1.2f)
            {
                return false;
            }

            bool west = p.x < RoadX(p.y);
            float split = west ? SplitWest : SplitEast;
            if (Mathf.Abs(p.y - split) < 0.9f)
            {
                return false;
            }

            bool alongZ = west ? p.y < split : p.y > split;
            rowDirection = alongZ ? Vector2.up : Vector2.right;
            return true;
        }

        // 1 on a furrow's ridge, 0 in its trough.
        public static float Furrow(Vector2 p, Vector2 rowDirection)
        {
            float across = rowDirection == Vector2.up ? p.x : p.y;
            return 0.5f + 0.5f * Mathf.Cos(across / RowSpacing * Mathf.PI * 2f);
        }

        // Footprints nothing in the fields may grow into: the lots (houses) and every building's bounds.
        private static readonly List<Bounds> Occupied = new List<Bounds>();

        private static bool OnOccupied(Vector2 p, float margin)
        {
            foreach (var b in Occupied)
            {
                if (p.x > b.min.x - margin && p.x < b.max.x + margin && p.y > b.min.z - margin && p.y < b.max.z + margin)
                {
                    return true;
                }
            }

            return false;
        }

        public static string Dress(Transform root)
        {
            Occupied.Clear();
            foreach (var path in new[] { "SetDressing/Lots", "SetDressing/Buildings" })
            {
                var t = root.Find(path);
                if (t == null)
                {
                    continue;
                }

                foreach (Transform item in t)
                {
                    var collider = item.GetComponent<Collider>();
                    if (collider != null)
                    {
                        Occupied.Add(collider.bounds);
                        continue;
                    }

                    foreach (var r in item.GetComponentsInChildren<MeshRenderer>())
                    {
                        Occupied.Add(r.bounds);
                    }
                }
            }

            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Farmland", true);
            var rng = new System.Random(Seed);
            int stalks = Stalks(group, rng);
            int walls = FieldWalls(group, rng);
            int scarecrows = 0;
            foreach (var (dx, z, yaw) in new[] { (-5.2f, 125f, 160f), (6.6f, 139.5f, 200f), (-8.5f, 149f, 175f) })
            {
                Scarecrow(group, new Vector2(RoadX(z) + dx, z), yaw, rng);
                scarecrows++;
            }

            int props = HayCart(group, rng) + Farmstead(group, rng) + Pumpkins(group, rng) + RoadLantern(group) + Verges(group, rng);
            int gardens = Gardens(group, rng);
            int straw = Straw(group, rng);
            return $"farmland: {stalks} stalks, {walls} field wall pieces, {scarecrows} scarecrows, {props} props, {gardens} kitchen gardens, {straw} straw tufts";
        }

        // Dead crops: rows of dry stalks on the furrows' ridges, thinned and trampled in patches.
        private static int Stalks(Transform group, System.Random rng)
        {
            string[] keys = { "A/Environments/SM_Env_Reeds_01", "A/Environments/SM_Env_Reeds_02", "A/Environments/SM_Env_Reeds_03" };
            int n = 0;
            for (float x = -26f; x < 36f; x += 0.45f)
            {
                for (float z = 104f; z < 162f; z += 0.45f)
                {
                    var p = new Vector2(x, z);
                    if (!InField(p, out var rows, out float blend) || L2Layout.SignedDistance(p) > 11f || OnOccupied(p, 0.6f))
                    {
                        continue;
                    }

                    // Only on a ridge line, every ~0.9 m along it.
                    float across = rows == Vector2.up ? x : z, along = rows == Vector2.up ? z : x;
                    float k = across / RowSpacing;
                    if (Mathf.Abs(k - Mathf.Round(k)) * RowSpacing > 0.22f || Mathf.Abs(along / 0.9f - Mathf.Round(along / 0.9f)) * 0.9f > 0.22f)
                    {
                        continue;
                    }

                    // Gaps, patches trampled flat, and the rows thinning out where the field fades into the village.
                    if (rng.NextDouble() > 0.7f * blend || Mathf.PerlinNoise(x * 0.18f + 5f, z * 0.18f + 9f) > 0.64f || L2Dressing.Reserved(p))
                    {
                        continue;
                    }

                    var go = L2Build.Spawn(keys[rng.Next(keys.Length)], group, Vector3.zero,
                        new Vector3(((float)rng.NextDouble() - 0.5f) * 18f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 18f), 0.55f + (float)rng.NextDouble() * 0.25f);
                    go.transform.position = new Vector3(p.x + ((float)rng.NextDouble() - 0.5f) * 0.2f, L2Layout.TerrainHeight(p) - 0.03f, p.y + ((float)rng.NextDouble() - 0.5f) * 0.2f);
                    // Dead straw, pale enough to catch the moon (the soot palette would turn the rows to black).
                    var straw = CropMaterial(rng.Next(2));
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            mats[m] = straw;
                        }

                        r.sharedMaterials = mats;
                    }

                    L2Dressing.Finish(go, L1Build.PropStatic, false);
                    n++;
                }
            }

            return n;
        }

        // Kitchen gardens behind the last houses of the village: a small plot inside a picket fence with a gap for
        // the gate, a few rows of dead stalks and a pumpkin or two. The first sign that the fields are near.
        private static int Gardens(Transform group, System.Random rng)
        {
            var placed = new List<Vector2>();
            for (int attempt = 0; attempt < 400 && placed.Count < 6; attempt++)
            {
                var c = new Vector2(-16f + (float)rng.NextDouble() * 44f, 100f + (float)rng.NextDouble() * 20f);
                float rural = L2Layout.Rural(c);
                float sd = L2Layout.SignedDistance(c);
                if (rural < 0.1f || rural > 0.85f || sd < 3.2f || sd > 9f || OnOccupied(c, 2.2f) || L2Dressing.Reserved(c) || L2Layout.IsInArena(c, 4f)
                    || L2Layout.InGateWall(c, 2f) || L2Dressing.Near(placed, c, 7f))
                {
                    continue;
                }

                placed.Add(c);
                float yaw = (float)rng.NextDouble() * 30f - 15f;
                var garden = new GameObject("KitchenGarden").transform;
                garden.SetParent(group, false);
                garden.SetPositionAndRotation(new Vector3(c.x, L2Layout.TerrainHeight(c), c.y), Quaternion.Euler(0f, yaw, 0f));
                const float w = 3.2f, d = 2.6f;
                // The fence: picket sections round the plot (1.6 m each), one left out for the way in, one leaning.
                int gap = rng.Next(4);
                for (int side = 0; side < 4; side++)
                {
                    bool alongX = side % 2 == 0;
                    float length = alongX ? w : d;
                    int pieces = Mathf.RoundToInt(length / 1.6f);
                    for (int k = 0; k < pieces; k++)
                    {
                        if (side == gap && k == 0)
                        {
                            continue;
                        }

                        float t = -length * 0.5f + (k + 0.5f) * (length / pieces);
                        Vector3 local = alongX ? new Vector3(t, 0f, side == 0 ? -d * 0.5f : d * 0.5f) : new Vector3(side == 1 ? w * 0.5f : -w * 0.5f, 0f, t);
                        float pieceYaw = alongX ? 0f : 90f;
                        var fence = L2Build.Spawn("K/Props/SM_Prop_Fence_01", garden, local + Quaternion.Euler(0f, pieceYaw, 0f) * new Vector3(0.8f, 0f, 0f),
                            new Vector3(rng.NextDouble() < 0.15 ? 12f : 0f, pieceYaw + ((float)rng.NextDouble() - 0.5f) * 5f, 0f), 1f, "Picket");
                        fence.transform.localScale = new Vector3(1f, 0.7f, 1f);
                        L2Dressing.Charred(fence, 0.2f, rng);
                        L2Dressing.Finish(fence, 0, false);
                    }
                }

                // Three short rows of dead stalks, and a pumpkin or two.
                for (int row = 0; row < 3; row++)
                {
                    for (float x = -w * 0.5f + 0.5f; x < w * 0.5f - 0.3f; x += 0.6f)
                    {
                        if (rng.NextDouble() < 0.3)
                        {
                            continue;
                        }

                        var stalk = L2Build.Spawn("A/Environments/SM_Env_Reeds_0" + (1 + rng.Next(3)), garden, new Vector3(x, -0.03f, -0.7f + row * 0.7f),
                            new Vector3(((float)rng.NextDouble() - 0.5f) * 16f, (float)rng.NextDouble() * 360f, 0f), 0.4f + (float)rng.NextDouble() * 0.15f, "Stalk");
                        SetMaterial(stalk, CropMaterial(rng.Next(2)));
                        L2Dressing.Finish(stalk, 0, false);
                    }
                }

                for (int k = 0; k < 1 + rng.Next(2); k++)
                {
                    var pumpkin = L2Build.Spawn("A/Props/SM_Prop_Pumpkin_0" + (1 + rng.Next(2)), garden, new Vector3(((float)rng.NextDouble() - 0.5f) * 2f, 0f, ((float)rng.NextDouble() - 0.5f) * 1.6f),
                        new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 0.9f, "Pumpkin");
                    L2Dressing.Charred(pumpkin, 0.4f, rng);
                    L2Dressing.Finish(pumpkin, 0, false);
                }

                L1Build.SetStatic(garden.gameObject, L1Build.PropStatic);
            }

            return placed.Count;
        }

        // Loose straw blown out of the fields along the verges and into the last yards, thinning toward the village.
        private static int Straw(Transform group, System.Random rng)
        {
            int n = 0;
            for (int attempt = 0; attempt < 2600 && n < 220; attempt++)
            {
                var p = new Vector2(-14f + (float)rng.NextDouble() * 40f, 100f + (float)rng.NextDouble() * 24f);
                float rural = L2Layout.Rural(p);
                float sd = L2Layout.SignedDistance(p);
                if (rng.NextDouble() > rural * 0.9f || sd < 0.2f || sd > 7f || OnOccupied(p, 0.3f) || L2Dressing.Reserved(p) || InField(p, out _))
                {
                    continue;
                }

                var tuft = L2Build.Spawn("A/Environments/SM_Env_Reeds_0" + (1 + rng.Next(3)), group, Vector3.zero,
                    new Vector3(((float)rng.NextDouble() - 0.5f) * 40f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 40f), 0.25f + (float)rng.NextDouble() * 0.25f, "Straw");
                tuft.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) - 0.05f, p.y);
                SetMaterial(tuft, CropMaterial(rng.Next(2)));
                L2Dressing.Finish(tuft, L1Build.PropStatic, false);
                n++;
            }

            return n;
        }

        private static void SetMaterial(GameObject go, Material material)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    mats[m] = material;
                }

                r.sharedMaterials = mats;
            }
        }

        private static Material CropMaterial(int tone)
        {
            string path = L2Build.MaterialsPath + "/L2_DeadCrop_" + tone + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", L1Build.Hex(tone == 0 ? "#8A7B60" : "#74674F"));   // grey straw, never gold or green
            mat.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Low dry-stone walls between the fields, broken by a gap or two.
        private static int FieldWalls(Transform group, System.Random rng)
        {
            int n = 0;
            foreach (var (z, side) in new[] { (SplitWest, -1f), (SplitEast, 1f) })
            {
                float start = RoadX(z) + side * 3.8f;
                for (int i = 0; i < 4; i++)
                {
                    if (i == 2 && side < 0f)
                    {
                        continue;   // a gap where the wall fell
                    }

                    var p = new Vector2(start + side * (i * 3.7f + 1.9f), z + ((float)rng.NextDouble() - 0.5f) * 0.3f);
                    if (L2Layout.SignedDistance(p) < 1f)
                    {
                        continue;
                    }

                    var wall = L2Build.Spawn("A/Buildings/SM_Bld_Wall_01", group, Vector3.zero, Vector3.zero, 1f, "FieldWall");
                    wall.transform.SetPositionAndRotation(new Vector3(p.x, L2Layout.TerrainHeight(p) - 0.08f, p.y), Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 6f, 0f));
                    wall.transform.localScale = new Vector3(0.95f, 0.75f + (float)rng.NextDouble() * 0.2f, 1f);
                    L2Village.Burnt(wall);
                    L2Dressing.Charred(wall, 0.3f, rng);
                    L2Dressing.Finish(wall, L1Build.PropStatic, false);
                    n++;
                }

                // A dead tree where the wall leaves the road.
                var treeAt = new Vector2(start + side * 1.2f, z + 1.3f);
                var tree = L2Build.Spawn(rng.NextDouble() < 0.5 ? "A/Environments/SM_Env_TreeDead_01" : "A/Environments/SM_Env_TreeDead_02", group, Vector3.zero,
                    new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 0.7f);
                tree.transform.position = new Vector3(treeAt.x, L2Layout.TerrainHeight(treeAt) - 0.05f, treeAt.y);
                L2Dressing.Charred(tree, 0.6f, rng);
                L2Dressing.Finish(tree, L1Build.EnvironmentStatic, false);
                L2Build.AddFade(tree);
                n++;
            }

            return n;
        }

        // A scarecrow: a post, a crossbar, a sack for a head and a rag coat hanging off the bar, leaning a little.
        // From the road, at night, it reads as a figure standing in the field (L3's false silhouettes).
        private static void Scarecrow(Transform group, Vector2 p, float yaw, System.Random rng)
        {
            var root = new GameObject("Scarecrow").transform;
            root.SetParent(group, false);
            root.SetPositionAndRotation(new Vector3(p.x, L2Layout.TerrainHeight(p), p.y), Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 10f, yaw, ((float)rng.NextDouble() - 0.5f) * 8f));
            Beam(root, new Vector3(0f, -0.3f, 0f), new Vector3(0f, 2.25f, 0f), 0.5f);
            Beam(root, new Vector3(-0.75f, 1.7f, 0f), new Vector3(0.78f, 1.64f, 0f), 0.42f);
            var head = L2Build.Spawn("A/Props/SM_Prop_Sack_01", root, new Vector3(0f, 2.05f, 0f), new Vector3(8f, 30f, -12f), 0.75f, "Head");
            L2Dressing.Finish(head, 0, false);
            var coat = L2Build.Spawn("K/Props/SM_Prop_Banner_01", root, new Vector3(0f, 1.78f, 0.04f), new Vector3(0f, 0f, 3f), 1f, "Coat");
            coat.transform.localScale = new Vector3(0.95f, 0.36f, 1f);
            L2Dressing.Finish(coat, 0, false);
            L1Build.SetStatic(root.gameObject, L1Build.PropStatic);
        }

        private static void Beam(Transform parent, Vector3 a, Vector3 b, float thickness)
        {
            var go = L2Build.Spawn("K/Props/SM_Prop_Beam_01", parent, a, Quaternion.FromToRotation(Vector3.up, b - a).eulerAngles);
            go.transform.localScale = new Vector3(thickness, (b - a).magnitude / 2.48f, thickness);
            L2Dressing.Finish(go, 0, false);
        }

        // The evacuation that never got away: a hay cart tipped off the verge, sacks spilt, a wheel in the furrows.
        private static int HayCart(Transform group, System.Random rng)
        {
            float z = 121.5f;
            var at = new Vector2(RoadX(z) + 4.3f, z);
            var cart = L2Build.Spawn("K/Props/SM_Prop_CartHay_01", group, Vector3.zero, Vector3.zero, 1f, "AbandonedHayCart");
            cart.transform.SetPositionAndRotation(new Vector3(at.x, L2Layout.TerrainHeight(at) - 0.1f, at.y), Quaternion.Euler(0f, 18f, -9f));
            L2Dressing.Charred(cart, 0.5f, rng);
            L2Dressing.Finish(cart, L1Build.PropStatic, false);
            // The lantern they carried, dropped in the road dust by the wheel and still alight.
            var dropped = at + new Vector2(-1.6f, -0.9f);
            var lantern = L2Build.Spawn("A/Items/SM_Item_Lantern_01", group, Vector3.zero, new Vector3(0f, 40f, 78f), 1f, "DroppedLantern");
            lantern.transform.position = new Vector3(dropped.x, L2Layout.TerrainHeight(dropped) + 0.14f, dropped.y);
            L2Dressing.Finish(lantern, L1Build.PropStatic, false);
            L2Fire.Candle(lantern.transform, "Flame", Vector3.up * 0.22f, 7f, 7f);
            int n = 2;
            foreach (var (dx, dz, key) in new[] { (1.3f, -1.4f, "A/Props/SM_Prop_Sack_02"), (1.9f, -0.6f, "A/Props/SM_Prop_Sack_04"), (0.9f, 1.9f, "A/Props/SM_Prop_Sack_03"), (2.4f, 1.2f, "K/Props/SM_Prop_CartWheel_01") })
            {
                var p = at + new Vector2(dx, dz);
                var go = L2Build.Spawn(key, group, Vector3.zero, new Vector3(key.Contains("Wheel") ? 85f : 0f, (float)rng.NextDouble() * 360f, 0f), 1f);
                go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) + (key.Contains("Wheel") ? 0.08f : 0f), p.y);
                L2Dressing.Finish(go, L1Build.PropStatic, false);
                n++;
            }

            return n;
        }

        // A farmstead burnt out off the road to the east: the shell of the farmhouse still smouldering, its well
        // in the yard. Its fire is the last warm light before the exit.
        private static int Farmstead(Transform group, System.Random rng)
        {
            float z = 136f;
            var at = new Vector2(RoadX(z) + 11.5f, z);
            var house = L2Build.Spawn("L2_RuinedHouse_B", group, Vector3.zero, Vector3.zero, 0.95f, "BurntFarmhouse");
            house.transform.SetPositionAndRotation(new Vector3(at.x, L2Layout.TerrainHeight(at) - 0.1f, at.y), Quaternion.Euler(0f, -100f, 0f));
            L2Dressing.Finish(house, L1Build.EnvironmentStatic, false);
            L2Build.AddFade(house);
            L2Fire.Smoulder(house.transform, "Smoulder", new Vector3(0.4f, 0.08f, 0.2f), new Vector2(2.4f, 2f));
            var fire = L2Fire.Fire(house.transform, "LastFire", Vector3.zero, 0.7f, 26f, 12f, true, new Vector3(1.2f, 0f, 0.8f), L2Fire.Shape.Box, 0.6f);
            fire.position = house.transform.position + Vector3.up * 0.3f + house.transform.right * -1.2f;

            var wellAt = at + new Vector2(-4.2f, -2.6f);
            var well = L2Build.Spawn("A/Buildings/SM_Bld_Well_01", group, Vector3.zero, new Vector3(0f, 25f, 0f), 0.85f, "FarmWell");
            well.transform.position = new Vector3(wellAt.x, L2Layout.TerrainHeight(wellAt) - 0.05f, wellAt.y);
            L2Village.Burnt(well);
            L2Dressing.Finish(well, L1Build.PropStatic, false);
            L2Build.AddFade(well);
            return 2;
        }

        // Along the field road's verges, just inside the fences: what a farm leaves lying about and what the
        // fleeing dropped (sacks, baskets, a pitchfork against the rails, a scythe in the grass, a cart wheel).
        private static int Verges(Transform group, System.Random rng)
        {
            (string key, Vector3 euler, float y)[] items =
            {
                ("A/Props/SM_Prop_Sack_01", Vector3.zero, 0f), ("A/Props/SM_Prop_Sack_03", Vector3.zero, 0f), ("A/Props/SM_Prop_Basket_02", Vector3.zero, 0f),
                ("A/Props/SM_Prop_Basket_04", new Vector3(0f, 0f, 75f), 0.12f), ("A/Weapons/SM_Wep_Pitchfork_01", new Vector3(-18f, 0f, 0f), 0f),
                ("A/Weapons/SM_Wep_Scythe_01", new Vector3(0f, 0f, 86f), 0.05f), ("K/Props/SM_Prop_CartWheel_01", new Vector3(80f, 0f, 0f), 0.1f),
                ("A/Props/SM_Prop_Pumpkin_02", Vector3.zero, 0f),
            };
            int n = 0;
            for (float z = L2Layout.FarmlandStart + 1f; z < 141f; z += 3.2f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    if (rng.NextDouble() < 0.4)
                    {
                        continue;
                    }

                    var p = new Vector2(RoadX(z) + side * 2.9f, z + ((float)rng.NextDouble() - 0.5f) * 1.5f);
                    float sd = L2Layout.SignedDistance(p);
                    if (sd < 0.1f || sd > 1.3f || L2Dressing.Reserved(p))
                    {
                        continue;
                    }

                    var item = items[rng.Next(items.Length)];
                    var go = L2Build.Spawn(item.key, group, Vector3.zero, item.euler + new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 1f, "Verge");
                    go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) + item.y, p.y);
                    L2Dressing.Charred(go, 0.2f, rng);
                    L2Dressing.Finish(go, L1Build.PropStatic, false);
                    n++;
                }
            }

            return n;
        }

        // A lantern hung on a post where the field road leaves the village: the first light of the fields.
        private static int RoadLantern(Transform group)
        {
            float z = 126.5f;
            var at = new Vector2(RoadX(z) - 3.3f, z);
            var post = new GameObject("LanternPost").transform;
            post.SetParent(group, false);
            post.SetPositionAndRotation(new Vector3(at.x, L2Layout.TerrainHeight(at), at.y), Quaternion.Euler(0f, 15f, 0f));
            Beam(post, new Vector3(0f, -0.3f, 0f), new Vector3(0f, 1.9f, 0f), 0.55f);
            Beam(post, new Vector3(-0.05f, 1.75f, 0f), new Vector3(0.6f, 1.8f, 0f), 0.35f);
            var lantern = L2Build.Spawn("A/Items/SM_Item_Lantern_01", post, new Vector3(0.52f, 1.22f, 0f), Vector3.zero, 1f, "Lantern");
            L2Dressing.Finish(lantern, 0, false);
            L2Fire.Candle(lantern.transform, "Flame", Vector3.up * 0.22f, 10f, 9f);
            L1Build.SetStatic(post.gameObject, L1Build.PropStatic);
            return 1;
        }

        // What is left of the crop in one corner: pumpkins rotting in the rows.
        private static int Pumpkins(Transform group, System.Random rng)
        {
            int n = 0;
            for (int i = 0; i < 9; i++)
            {
                float z = 143f + (float)rng.NextDouble() * 6f;
                var p = new Vector2(RoadX(z) + 4.5f + (float)rng.NextDouble() * 4f, z);
                if (!InField(p, out _))
                {
                    continue;
                }

                var go = L2Build.Spawn(rng.NextDouble() < 0.5 ? "A/Props/SM_Prop_Pumpkin_01" : "A/Props/SM_Prop_Pumpkin_02", group, Vector3.zero,
                    new Vector3(0f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 30f), 0.8f + (float)rng.NextDouble() * 0.4f);
                go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) - 0.04f, p.y);
                L2Dressing.Charred(go, 0.45f, rng);
                L2Dressing.Finish(go, L1Build.PropStatic, false);
                n++;
            }

            return n;
        }
    }
}
