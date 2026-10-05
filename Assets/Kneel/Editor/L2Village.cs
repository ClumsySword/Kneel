using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The village itself: which houses stand, burn or lie in ruins, the life left around them (woodpiles,
    // stores, laundry, gardens, a lantern still lit), the continuous edge that tells the player where the
    // street ends (walls, fences, hedges), trees and bushes, the fires that lead the way, and the dead.
    // Tall scenery on the camera side fades out of the way (OcclusionFade); only the camera-side rims of the
    // fights stay low, so nothing ever hides a telegraph.
    public static class L2Village
    {
        private const int Seed = 5151;

        public enum HouseState { Intact, Burning, Ruin, Yard }

        public class House
        {
            public L2Layout.Lot Lot;
            public HouseState State;
            public GameObject Go;
            public Vector2 Forward;   // toward the street (the lot's local +Z)
            public Vector2 Right;

            public Vector2 Front => Lot.Center + Forward * (Lot.Size.y * 0.5f);
        }

        public static readonly List<House> Houses = new List<House>();
        private static readonly List<L2Layout.Lot> AllLots = new List<L2Layout.Lot>();

        private static readonly string[] Ruins = { "L2_RuinedHouse_A", "L2_RuinedHouse_B", "L2_RuinedHouse_D", "L2_RuinedHouse_E" };

        // Which way each burning variant's fire faces, relative to the lot (so the flames face the street).
        private static readonly float[] FireSideYaw = { 0f, 180f, 90f };

        public static string BuildAll(Transform root)
        {
            var log = new List<string>
            {
                Buildings(root),
                Vignettes(root),
                EdgeWalls(root),
                Trees(root),
                Beacons(root),
                Skeletons(root),
            };
            return string.Join(" | ", log);
        }

        // ---------------------------------------------------------------- Buildings

        public static string Buildings(Transform root)
        {
            Houses.Clear();
            AllLots.Clear();
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Buildings", true);

            // The church stands on its greybox boxes (Landmarks/Church), not on a lot box.
            var churchGroup = L2Build.Group(L2Build.Group(root, "Landmarks", false), "Church", false);
            var old = churchGroup.Find("L2_CollapsedChurch");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var church = L2Build.Spawn("L2_CollapsedChurch", churchGroup, Vector3.zero, Vector3.zero);
            church.transform.position = new Vector3(L2Layout.ChurchNave.center.x, L2Layout.PlateauHeight, L2Layout.ChurchNave.center.y);
            L2Dressing.Finish(church, 0, false);
            L2Build.AddFade(church);

            var rng = new System.Random(Seed);
            var lots = new List<(L2Layout.Lot lot, Transform box)>(L2Greybox.ReadLots(root));
            var fires = new List<Vector2>();
            foreach (var (lot, _) in lots)
            {
                AllLots.Add(lot);
                if (lot.Kind == L2Layout.LotKind.Burning)
                {
                    fires.Add(lot.Center);
                }
            }

            int variant = 0;
            int nIntact = 0, nBurning = 0, nRuin = 0, nYard = 0;
            foreach (var (lot, _) in lots)
            {
                if (lot.Name == L2Layout.DoorframeLot.Name || lot.Kind == L2Layout.LotKind.Church)
                {
                    continue;
                }

                var house = new House { Lot = lot, Forward = Dir(lot.Yaw), Right = Dir(lot.Yaw + 90f), State = HouseState.Yard };
                GameObject go = null;
                switch (lot.Kind)
                {
                    case L2Layout.LotKind.Burning:
                        go = PlaceBurning(group, lot, ref variant);
                        house.State = HouseState.Burning;
                        break;
                    case L2Layout.LotKind.BurningLow:
                        go = Fit("L2_BurningFront", group, lot, lot.Yaw, true);
                        house.State = HouseState.Burning;
                        break;
                    case L2Layout.LotKind.Low:
                        break;   // a yard: the camera-side rim of a fight stays low (Vignettes fills it)
                    default:
                    {
                        double roll = rng.NextDouble();
                        if (roll < 0.34 && !L2Dressing.Near(fires, lot.Center, 12f))
                        {
                            go = PlaceBurning(group, lot, ref variant);
                            if (go != null)
                            {
                                house.State = HouseState.Burning;
                                fires.Add(lot.Center);
                            }
                        }

                        if (go == null)
                        {
                            bool ruin = rng.NextDouble() < 0.36;
                            string key = ruin ? Ruins[rng.Next(Ruins.Length)] : L2Houses.Intact[rng.Next(L2Houses.Intact.Length)];
                            go = Fit(key, group, lot, lot.Yaw + (rng.NextDouble() < 0.5 ? 0f : 180f), true);
                            house.State = ruin ? HouseState.Ruin : HouseState.Intact;
                        }

                        break;
                    }
                }

                if (go != null && !FitsArena(go))
                {
                    Object.DestroyImmediate(go);
                    go = null;
                    house.State = HouseState.Yard;
                }

                if (go != null)
                {
                    go.name = lot.Name + "_" + go.name;
                    L2Build.AddFade(go);
                }

                house.Go = go;
                Houses.Add(house);
                switch (house.State)
                {
                    case HouseState.Intact: nIntact++; break;
                    case HouseState.Burning: nBurning++; break;
                    case HouseState.Ruin: nRuin++; break;
                    default: nYard++; break;
                }
            }

            return $"houses: {nIntact} intact, {nBurning} burning, {nRuin} ruined, {nYard} yards, church";
        }

        // A burning house whose fire faces the street but never lands on walkable ground.
        private static GameObject PlaceBurning(Transform group, L2Layout.Lot lot, ref int variant)
        {
            for (int k = 0; k < L2Houses.Burning.Length; k++)
            {
                int index = (variant + k) % L2Houses.Burning.Length;
                foreach (float flip in new[] { 0f, 180f })
                {
                    var go = Fit(L2Houses.Burning[index], group, lot, lot.Yaw + FireSideYaw[index] + flip, false);
                    if (FiresClear(go))
                    {
                        variant++;
                        return go;
                    }

                    Object.DestroyImmediate(go);
                }
            }

            return null;
        }

        public static bool FiresClear(GameObject go)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.name != "Flames" && ps.name != "Heart" && ps.name != "GroundEmbers")
                {
                    continue;
                }

                var shape = ps.shape;
                var half = shape.shapeType == ParticleSystemShapeType.Box ? shape.scale * 0.5f : Vector3.one * 0.4f;
                foreach (var c in new[] { Vector3.zero, new Vector3(half.x, half.y, 0f), new Vector3(-half.x, half.y, 0f), new Vector3(half.x, -half.y, 0f), new Vector3(-half.x, -half.y, 0f) })
                {
                    var p = ps.transform.TransformPoint(c);
                    if (L2Layout.SignedDistance(new Vector2(p.x, p.z)) < 0.6f)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Places a prefab on a lot at a given yaw, scaled (within limits) to the lot's footprint.
        private static GameObject Fit(string key, Transform parent, L2Layout.Lot lot, float yaw, bool alignLongAxis)
        {
            var go = L2Build.Spawn(key, parent, Vector3.zero, Vector3.zero);
            var footprint = Footprint(go);
            if (alignLongAxis && (footprint.x > footprint.y) != (lot.Size.x > lot.Size.y))
            {
                yaw += 90f;
            }

            float relative = Mathf.Repeat(yaw - lot.Yaw, 180f);
            bool turned = relative > 45f && relative < 135f;
            if (turned)
            {
                footprint = new Vector2(footprint.y, footprint.x);
            }

            float scale = Mathf.Clamp(Mathf.Min(lot.Size.x / footprint.x, lot.Size.y / footprint.y), 0.72f, 1.1f);
            go.transform.SetPositionAndRotation(new Vector3(lot.Center.x, L2Greybox.LotGround(lot), lot.Center.y), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            L2Dressing.Finish(go, L1Build.EnvironmentStatic);
            return go;
        }

        private static Vector2 Footprint(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (first)
                {
                    b = r.bounds;
                    first = false;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return new Vector2(Mathf.Max(b.size.x, 1f), Mathf.Max(b.size.z, 1f));
        }

        // Only the camera-side rims of fights cap height now (see L2Layout.ArenaCap).
        public static bool FitsArena(GameObject go)
        {
            float bottom = go.transform.position.y;
            float cap = float.MaxValue;
            float top = float.MinValue;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                top = Mathf.Max(top, b.max.y);
                foreach (var c in new[] { new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z), new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.max.z), new Vector2(b.center.x, b.center.z) })
                {
                    cap = Mathf.Min(cap, L2Layout.ArenaCap(c));
                }
            }

            return top - bottom <= cap;
        }

        // ---------------------------------------------------------------- Spreading fire

        // Hours into the raid the fires have spread and burnt down unevenly: a house next to a burning one has
        // caught at the roof on the side facing it, and ruins smoulder in their rubble with the odd low flame.
        // Never near a fight (the telegraphs stay clear) and never over the street. Only some carry a light.
        public static string SpreadingFires(Transform root)
        {
            var rng = new System.Random(Seed + 11);
            var burning = new List<Vector2>();
            foreach (var h in Houses)
            {
                if (h.State == HouseState.Burning)
                {
                    burning.Add(h.Lot.Center);
                }
            }

            int caught = 0, smouldering = 0, lights = 0;
            foreach (var h in Houses)
            {
                if (h.Go == null || L2Layout.IsInArena(h.Lot.Center, 6f))
                {
                    continue;
                }

                Vector2 c = h.Lot.Center;
                if (h.State == HouseState.Intact)
                {
                    Vector2 near = c;
                    float best = 11f;
                    foreach (var b in burning)
                    {
                        float d = (b - c).magnitude;
                        if (d > 0.5f && d < best)
                        {
                            best = d;
                            near = b;
                        }
                    }

                    if (best >= 11f || rng.NextDouble() > 0.6)
                    {
                        continue;
                    }

                    // The roof catches on the side facing the fire, a little in from the gable.
                    Vector2 toward = (near - c).normalized;
                    Vector2 target = c + toward * Mathf.Min(h.Lot.Size.x, h.Lot.Size.y) * 0.3f;
                    if (!RoofPoint(h.Go, target, out var roof) || L2Layout.SignedDistance(new Vector2(roof.x, roof.z)) < 1f)
                    {
                        continue;
                    }

                    bool light = lights < 12 && rng.NextDouble() < 0.5;
                    float yaw = Mathf.Atan2(-toward.x, -toward.y) * Mathf.Rad2Deg;   // the burning strip runs across the line to the fire
                    var fire = L2Fire.Fire(h.Go.transform, "CaughtFire", Vector3.zero, 0.6f + (float)rng.NextDouble() * 0.25f, light ? 20f : 0f, 10f, true,
                        new Vector3(1.3f, 0f, 0.7f), L2Fire.Shape.Box, 0.55f);
                    fire.SetPositionAndRotation(roof + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
                    caught++;
                    lights += light ? 1 : 0;
                }
                else if (h.State == HouseState.Ruin && rng.NextDouble() < 0.5)
                {
                    Vector2 spot = c + h.Right * ((float)rng.NextDouble() - 0.5f) * h.Lot.Size.x * 0.4f + h.Forward * ((float)rng.NextDouble() - 0.5f) * h.Lot.Size.y * 0.4f;
                    if (L2Layout.SignedDistance(spot) < 1.5f)
                    {
                        continue;
                    }

                    var smoulder = L2Fire.Smoulder(h.Go.transform, "Smoulder", Vector3.zero, new Vector2(1.4f + (float)rng.NextDouble() * 0.8f, 1.2f + (float)rng.NextDouble() * 0.6f));
                    smoulder.SetPositionAndRotation(new Vector3(spot.x, L2Greybox.LotGround(h.Lot) + 0.06f, spot.y), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    if (rng.NextDouble() < 0.35)
                    {
                        // A last tongue of flame on a fallen timber.
                        var flame = L2Fire.Fire(h.Go.transform, "LowFlame", Vector3.zero, 0.45f, 0f, 0f, false, default, L2Fire.Shape.Point, 0.6f);
                        flame.position = new Vector3(spot.x, L2Greybox.LotGround(h.Lot) + 0.15f, spot.y);
                    }

                    smouldering++;
                }
            }

            return $"spreading fire: {caught} roofs caught ({lights} lit), {smouldering} ruins smouldering";
        }

        // The roof's surface over a point: a ray dropped onto the house through temporary mesh colliders (the
        // Synty meshes aren't CPU-readable, but physics can still bake them in the editor).
        private static bool RoofPoint(GameObject house, Vector2 target, out Vector3 roof)
        {
            roof = Vector3.zero;
            var temporary = new List<MeshCollider>();
            float top = house.transform.position.y;
            foreach (var mf in house.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<ParticleSystem>() != null)
                {
                    continue;
                }

                var c = mf.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = mf.sharedMesh;
                temporary.Add(c);
                top = Mathf.Max(top, mf.GetComponent<Renderer>() != null ? mf.GetComponent<Renderer>().bounds.max.y : top);
            }

            Physics.SyncTransforms();
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(target.x, top + 2f, target.y), Vector3.down, top + 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is MeshCollider mc && temporary.Contains(mc) && hit.point.y > best)
                {
                    best = hit.point.y;
                    roof = hit.point;
                }
            }

            foreach (var c in temporary)
            {
                Object.DestroyImmediate(c);
            }

            return best > house.transform.position.y + 2f;
        }

        // ---------------------------------------------------------------- Lived-in vignettes

        private struct Item
        {
            public string Key;
            public Vector3 P;       // x along the house front, y up, z out toward the street (0 = the wall)
            public Vector3 Euler;
            public float Scale;

            public Item(string key, float x, float z, float yaw = 0f, float scale = 1f, float y = 0f, float pitch = 0f, float roll = 0f)
            {
                Key = key;
                P = new Vector3(x, y, z);
                Euler = new Vector3(pitch, yaw, roll);
                Scale = scale;
            }
        }

        private static readonly Item[][] HomeTemplates =
        {
            // Firewood: a log pile, the chopping block with the axe still in it, split logs.
            new[]
            {
                new Item("A/Props/SM_Prop_Logpile_01", -1f, 0.75f, 0f, 0.62f), new Item("A/Environments/SM_Env_TreeStump_01", 0.9f, 0.95f, 30f, 0.5f),
                new Item("A/Weapons/SM_Wep_Axe_01", 0.95f, 0.95f, 80f, 1f, 0.42f, 0f, 35f), new Item("A/Props/SM_Prop_Loghalf_01", 0.3f, 1.25f, 65f, 0.8f),
                new Item("A/Props/SM_Prop_Loghalf_02", 1.5f, 1.3f, 10f, 0.8f),
            },
            // Stores: barrels, crates, a sack slumped against them.
            new[]
            {
                new Item("A/Props/SM_Prop_Barrel_01", -1.1f, 0.5f, 0f, 0.95f), new Item("A/Props/SM_Prop_Barrel_02", -0.4f, 0.45f, 40f, 0.9f),
                new Item("A/Props/SM_Prop_Crate_01", 0.55f, 0.55f, 12f, 0.9f), new Item("A/Props/SM_Prop_Crate_02", 0.6f, 0.55f, 40f, 0.65f, 0.72f),
                new Item("A/Props/SM_Prop_Sack_01", 1.25f, 0.7f, 70f), new Item("A/Props/SM_Prop_Sack_03", -0.8f, 1.05f, 20f),
            },
            // The doorstep: steps, a pot, a basket of vegetables, a broom-like pitchfork against the wall.
            new[]
            {
                new Item("K/Buildings/SM_Bld_House_StepsSmall_01", 0f, 0.35f), new Item("A/Props/SM_Prop_Pot_01", -1f, 0.35f, 30f),
                new Item("A/Props/SM_Prop_Basket_01", 0.95f, 0.45f, 10f), new Item("A/Props/SM_Prop_Pumpkin_01", 1.3f, 0.8f, 50f),
                new Item("A/Weapons/SM_Wep_Pitchfork_01", -1.55f, 0.18f, 0f, 1f, 0f, -14f),
            },
            // A kitchen garden: a raised bed, plants, pumpkins, the scythe left in it.
            new[]
            {
                new Item("A/Environments/SM_Env_DirtMound_01", 0f, 1f, 0f, 0.2f, -0.25f), new Item("A/Environments/SM_Env_Plant_01", -0.6f, 0.9f, 20f, 0.8f, 0.2f),
                new Item("A/Environments/SM_Env_Plant_02", 0.2f, 1.1f, 80f, 0.8f, 0.22f), new Item("A/Environments/SM_Env_Plant_04", 0.8f, 0.8f, 140f, 0.7f, 0.18f),
                new Item("A/Props/SM_Prop_Pumpkin_02", -1.3f, 1.35f, 20f), new Item("A/Weapons/SM_Wep_Scythe_01", 1.6f, 0.3f, 90f, 1f, 0.05f, 0f, 84f),
            },
            // Market goods left out: a table with cheese, meat, fruit and a basket.
            new[]
            {
                new Item("A/Props/SM_Prop_Stall_Table_01", 0f, 0.8f), new Item("A/Props/SM_Prop_Cheese_01", -0.4f, 0.8f, 20f, 1f, 0.82f),
                new Item("A/Props/SM_Prop_Meat_01", 0.35f, 0.75f, 60f, 1f, 0.82f), new Item("A/Items/SM_Item_Fruit_01", 0.05f, 0.95f, 0f, 1f, 0.82f),
                new Item("A/Props/SM_Prop_Basket_02", 1.3f, 0.7f, 0f), new Item("A/Props/SM_Prop_Sack_02", -1.3f, 0.6f, 40f),
            },
            // Laundry left on the line across the front.
            new[]
            {
                new Item("A/Props/SM_Prop_Washingline_02", 0f, 1.05f, 0f, 0.8f), new Item("A/Props/SM_Prop_Basket_03", -1.4f, 0.5f, 20f),
            },
        };

        // Around ruins and burning houses: what the raid left.
        private static readonly Item[][] RaidTemplates =
        {
            new[]
            {
                new Item("A/Props/SM_Prop_Barrel_01", -0.9f, 0.9f, 20f, 1f, 0.35f, 90f), new Item("A/Props/SM_Prop_Crate_02", 0.4f, 0.7f, 55f, 0.9f, 0f, 0f, 12f),
                new Item("A/Props/SM_Prop_Sack_04", 0.9f, 1.1f, 80f), new Item("A/Props/SM_Prop_Scroll_01", 0.1f, 1.35f, 30f),
            },
            new[]
            {
                new Item("A/Props/SM_Prop_Chest_01", -0.5f, 0.8f, 160f, 0.9f, 0f, 0f, 8f), new Item("A/Props/SM_Prop_Book_02", 0.5f, 1.2f, 30f),
                new Item("A/Weapons/SM_Wep_Shield_01", 1.1f, 0.8f, 70f, 1f, 0.05f, 90f), new Item("A/Props/SM_Prop_Basket_04", -1.4f, 1.1f, 0f, 1f, 0.15f, 0f, 80f),
            },
            new[]
            {
                new Item("K/Props/SM_Prop_Beam_01", 0f, 1f, 70f, 1.2f, 0.1f, 90f), new Item("A/Props/SM_Prop_Pot_03", -1f, 0.7f, 0f, 1f, 0.1f, 0f, 90f),
                new Item("A/Weapons/SM_Wep_Sword_01", 0.9f, 1.3f, 110f, 1f, 0.04f, 90f),
            },
        };

        // Arena rims on the camera side: yards, everything knee-high.
        private static readonly Item[] YardTemplate =
        {
            new Item("A/Buildings/SM_Bld_Fence_01", 0f, -0.6f, 0f, 1f), new Item("A/Props/SM_Prop_Logpile_01", -1.8f, -2.2f, 0f, 0.5f),
            new Item("A/Environments/SM_Env_DirtMound_01", 1.5f, -2.4f, 0f, 0.16f, -0.25f), new Item("A/Environments/SM_Env_Plant_03", 1.2f, -2.2f, 0f, 0.7f, 0.1f),
            new Item("A/Props/SM_Prop_Pumpkin_01", 2f, -1.9f, 30f),
        };

        public static string Vignettes(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "LivedIn", true);
            var rng = new System.Random(Seed + 1);
            int placed = 0, lanterns = 0;
            foreach (var h in Houses)
            {
                switch (h.State)
                {
                    case HouseState.Intact:
                    {
                        placed += Place(HomeTemplates[rng.Next(HomeTemplates.Length)], group, h, h.Front + h.Right * ((float)rng.NextDouble() - 0.5f) * 1.2f, rng, false);
                        if (rng.NextDouble() < 0.4)
                        {
                            placed += Place(HomeTemplates[rng.Next(HomeTemplates.Length)], group, h, h.Front + h.Right * (h.Lot.Size.x * 0.5f + 0.8f), rng, false);
                        }

                        // A lantern still burning by some doors: the village was lived in this morning.
                        if (rng.NextDouble() < 0.35 && Lantern(group, h, rng))
                        {
                            lanterns++;
                        }

                        break;
                    }

                    case HouseState.Ruin:
                    case HouseState.Burning:
                        if (rng.NextDouble() < 0.7)
                        {
                            placed += Place(RaidTemplates[rng.Next(RaidTemplates.Length)], group, h, h.Front + h.Right * ((float)rng.NextDouble() - 0.5f) * 2f, rng, true);
                        }

                        break;
                    case HouseState.Yard:
                        placed += Place(YardTemplate, group, h, h.Front, rng, false, 1.2f);
                        break;
                }
            }

            return $"lived-in: {placed} props, {lanterns} lanterns";
        }

        private static int Place(Item[] template, Transform group, House h, Vector2 origin, System.Random rng, bool raid, float maxHeight = float.MaxValue)
        {
            int n = 0;
            float yaw = h.Lot.Yaw;
            foreach (var item in template)
            {
                Vector2 p = origin + h.Right * item.P.x + h.Forward * item.P.z;
                float sd = L2Layout.SignedDistance(p);
                if (sd < -0.5f || L2Dressing.Reserved(p) || L2Layout.IsInArena(p, 0.5f) || InAnyLot(p, 0.1f) || L2Layout.InGateWall(p, 0.4f))
                {
                    continue;
                }

                var go = L2Build.Spawn(item.Key, group, Vector3.zero, Vector3.zero, item.Scale);
                go.transform.SetPositionAndRotation(L2Build.Ground(p) + Vector3.up * item.P.y, Quaternion.Euler(item.Euler.x, yaw + item.Euler.y, item.Euler.z));
                if (L2Build.Top(go) - go.transform.position.y > Mathf.Min(maxHeight, L2Layout.ArenaCap(p)))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                if (raid && rng.NextDouble() < 0.5)
                {
                    L2Dressing.Charred(go, 1f, rng);
                }

                L2Dressing.Finish(go, L1Build.PropStatic, false);
                n++;
            }

            return n;
        }

        private static bool Lantern(Transform group, House h, System.Random rng)
        {
            Vector2 p = h.Front + h.Forward * 0.35f + h.Right * (rng.NextDouble() < 0.5 ? -1.2f : 1.2f);
            if (L2Layout.SignedDistance(p) < -0.2f || L2Dressing.Reserved(p) || L2Layout.IsInArena(p, 1f))
            {
                return false;
            }

            var lantern = L2Build.Spawn("A/Items/SM_Item_Lantern_01", group, Vector3.zero, Vector3.zero, 1.2f);
            lantern.transform.position = L2Build.Ground(p);
            L2Dressing.Finish(lantern, L1Build.PropStatic, false);
            // The flame and its light sit inside the lantern's glass (its body is 0.54 m tall, unscaled).
            L2Fire.Candle(lantern.transform, "Flame", Vector3.up * 0.22f, 7f, 5.5f);
            L1Wayfinding.Particles("Glow", lantern.transform, L1Build.FxMat("L1_FX_Glow"), 2, 1f, new Vector2(2f, 2f), Vector2.zero, new Vector2(0.6f, 0.75f),
                new Color(1f, 0.7f, 0.35f, 0.35f), new Color(1f, 0.6f, 0.3f, 0.3f)).transform.localPosition = Vector3.up * 0.25f;
            return true;
        }

        // ---------------------------------------------------------------- Squares

        private static readonly Item[][] SquareTemplates =
        {
            // A market stall facing into the square, goods still on the table.
            new[]
            {
                new Item("A/Buildings/SM_Bld_Stall_0#", 0f, 0f), new Item("A/Props/SM_Prop_Crate_01", 2.1f, 0.3f, 15f, 0.9f),
                new Item("A/Props/SM_Prop_Sack_02", -2f, 0.4f, 60f), new Item("A/Props/SM_Prop_Basket_02", 1.4f, 1.2f, 0f),
                new Item("A/Props/SM_Prop_Pumpkin_01", -1.4f, 1.3f, 40f),
            },
            // A cart left in the square, half unloaded.
            new[]
            {
                new Item("A/Props/SM_Prop_Cart_03", 0f, 0.2f, 90f), new Item("A/Props/SM_Prop_Sack_01", 1.8f, 0.9f, 20f),
                new Item("A/Props/SM_Prop_Sack_03", 2.3f, 0.4f, 80f), new Item("A/Props/SM_Prop_Barrel_01", -2f, 0.2f, 0f, 0.95f),
            },
            // Stores stacked against the square's edge.
            new[]
            {
                new Item("A/Props/SM_Prop_Barrel_01", -0.8f, 0f, 0f), new Item("A/Props/SM_Prop_Barrel_02", 0f, 0.1f, 30f),
                new Item("A/Props/SM_Prop_Crate_01", 0.9f, 0f, 10f), new Item("A/Props/SM_Prop_Crate_02", 0.9f, 0f, 35f, 0.7f, 0.75f),
                new Item("A/Props/SM_Prop_Logpile_01", -2.3f, -0.2f, 0f, 0.55f),
            },
        };

        // The open squares (not the fights) get a lived-in rim: stalls, a cart, stores and a lamppost still lit,
        // set round the edge between the streets that leave the square, the middle kept clear.
        public static string Squares(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Squares", true);
            var rng = new System.Random(Seed + 6);
            int clusters = 0, lamps = 0;
            foreach (var area in L2Layout.Areas)
            {
                if (area.IsArena || area.Radius < 5f || area.Name == "Start" || area.Name == "Exit")
                {
                    continue;
                }

                int slots = Mathf.FloorToInt(2f * Mathf.PI * area.Radius / 5.5f);
                float phase = (float)rng.NextDouble() * 360f;
                bool lampPlaced = false;
                for (int k = 0; k < slots; k++)
                {
                    float angle = phase + k * 360f / slots;
                    Vector2 dir = Dir(angle);
                    if (IsExit(area, angle))
                    {
                        continue;
                    }

                    Vector2 at = area.Center + dir * (area.Radius - 1.4f);
                    var frame = new House
                    {
                        Lot = new L2Layout.Lot("Square", at.x, at.y, 4f, 1f, angle + 180f, L2Layout.LotKind.Low),
                        Forward = -dir,
                        Right = new Vector2(-dir.y, dir.x),
                    };

                    if (!lampPlaced && rng.NextDouble() < 0.5)
                    {
                        lampPlaced = Lamppost(group, at + frame.Right * 0.4f, angle + 180f);
                        lamps += lampPlaced ? 1 : 0;
                        continue;
                    }

                    var template = SquareTemplates[rng.Next(SquareTemplates.Length)];
                    if (template[0].Key.Contains("#"))
                    {
                        template = (Item[])template.Clone();
                        template[0].Key = template[0].Key.Replace("#", (1 + rng.Next(4)).ToString());
                    }

                    clusters += PlaceSquare(template, group, frame, at, rng) > 0 ? 1 : 0;
                }
            }

            return $"squares: {clusters} clusters, {lamps} lampposts";
        }

        // A street leaves the square in this direction: the ground just past the rim is still walkable.
        private static bool IsExit(L2Layout.Area area, float angle)
        {
            for (float da = -16f; da <= 16f; da += 8f)
            {
                Vector2 probe = area.Center + Dir(angle + da) * (area.Radius + 1.8f);
                if (L2Layout.SignedDistance(probe) < 0f)
                {
                    return true;
                }
            }

            return false;
        }

        // Stalls and carts are solid (they stand in walkable space; the NavMesh is rebaked round them); small
        // goods are visual only.
        private static int PlaceSquare(Item[] template, Transform group, House frame, Vector2 origin, System.Random rng)
        {
            int n = 0;
            foreach (var item in template)
            {
                Vector2 p = origin + frame.Right * item.P.x + frame.Forward * item.P.z;
                if (L2Dressing.Reserved(p) || L2Layout.IsInArena(p, 1f))
                {
                    continue;
                }

                var go = L2Build.Spawn(item.Key, group, Vector3.zero, Vector3.zero, item.Scale);
                go.transform.SetPositionAndRotation(L2Build.Ground(p) + Vector3.up * item.P.y, Quaternion.Euler(item.Euler.x, frame.Lot.Yaw + item.Euler.y, item.Euler.z));
                bool big = item.Key.Contains("Stall_0") || item.Key.Contains("Cart");
                L2Dressing.Finish(go, L1Build.PropStatic, big);
                if (big)
                {
                    L2Build.AddFade(go);
                }

                n++;
            }

            return n;
        }

        private static bool Lamppost(Transform group, Vector2 p, float yaw)
        {
            if (L2Dressing.Reserved(p) || L2Layout.IsInArena(p, 1f))
            {
                return false;
            }

            var post = L2Build.Spawn("K/Props/SM_Prop_Lampost_01", group, Vector3.zero, new Vector3(0f, yaw, 0f));
            post.transform.position = L2Build.Ground(p);
            L2Dressing.Finish(post, L1Build.PropStatic, true);
            L2Build.AddFade(post);

            // The lantern hangs from the end of the arm (1.27 m out); its flame and light sit inside it.
            var lamp = L2Fire.Candle(post.transform, "Lamp", new Vector3(0f, 2.45f, 1.27f), 12f, 8f);
            L1Wayfinding.Particles("Glow", lamp, L1Build.FxMat("L1_FX_Glow"), 2, 1f, new Vector2(2f, 2f), Vector2.zero, new Vector2(0.7f, 0.85f),
                new Color(1f, 0.7f, 0.35f, 0.4f), new Color(1f, 0.6f, 0.3f, 0.35f));
            return true;
        }

        // ---------------------------------------------------------------- The camera side of the streets

        // Small yard scenes for the strip between a street's south (camera-side) edge and the screen's bottom:
        // the player sees ~5 m of it at all times, so it must never be bare. All of it stays low (under ~1.6 m),
        // so none of it needs to fade.
        private static readonly Item[][] SouthClusters =
        {
            new[] { new Item("A/Props/SM_Prop_Logpile_01", 0f, 0f, 0f, 0.5f), new Item("A/Environments/SM_Env_TreeStump_01", 1.3f, 0.3f, 40f, 0.5f), new Item("A/Weapons/SM_Wep_Axe_01", 1.32f, 0.3f, 80f, 1f, 0.42f, 0f, 35f) },
            new[] { new Item("A/Props/SM_Prop_Barrel_01", 0f, 0f), new Item("A/Props/SM_Prop_Barrel_02", 0.65f, 0.2f, 30f), new Item("A/Props/SM_Prop_Crate_01", -0.7f, 0.3f, 15f, 0.9f), new Item("A/Props/SM_Prop_Sack_03", 0.3f, -0.6f, 70f) },
            new[] { new Item("A/Environments/SM_Env_DirtMound_01", 0f, 0f, 0f, 0.17f, -0.25f), new Item("A/Environments/SM_Env_Plant_02", -0.4f, 0.1f, 0f, 0.7f, 0.15f), new Item("A/Environments/SM_Env_Plant_04", 0.4f, -0.2f, 90f, 0.7f, 0.15f), new Item("A/Props/SM_Prop_Pumpkin_01", 1.1f, 0.5f, 30f), new Item("A/Props/SM_Prop_Pumpkin_02", -1.2f, -0.4f, 70f) },
            new[] { new Item("A/Buildings/SM_Bld_Fence_02", 0f, 0f, 0f), new Item("A/Buildings/SM_Bld_FencePost_01", 1f, 0.1f), new Item("A/Environments/SM_Env_Bush_03", -1.3f, -0.4f, 0f, 0.7f) },
            new[] { new Item("A/Environments/SM_Env_Bush_01", 0f, 0f, 0f, 0.6f), new Item("A/Environments/SM_Env_Bush_04", 1.2f, 0.4f, 120f, 0.55f), new Item("A/Environments/SM_Env_Plant_03", -0.9f, 0.5f, 0f, 0.8f) },
            new[] { new Item("A/Props/SM_Prop_Sack_01", 0f, 0f, 20f), new Item("A/Props/SM_Prop_Sack_02", 0.6f, 0.3f, 80f), new Item("A/Props/SM_Prop_Basket_02", -0.6f, 0.2f, 0f), new Item("A/Items/SM_Item_Fruit_02", -0.5f, 0.6f, 0f) },
            new[] { new Item("K/Props/SM_Prop_CartHay_01", 0f, 0f, 90f, 0.9f), new Item("A/Weapons/SM_Wep_Pitchfork_01", 1.4f, 0.2f, 30f, 1f, 0.05f, 86f) },
            new[] { new Item("A/Props/SM_Prop_Cart_02", 0f, 0f, 60f, 0.9f), new Item("A/Props/SM_Prop_Cart_Wheel_01", 1.6f, 0.6f, 20f, 1f, 0.05f, 0f, 88f) },
            new[] { new Item("A/Environments/SM_Env_TreeStump_01", 0f, 0f, 0f, 0.8f), new Item("A/Environments/SM_Env_Reeds_01", 0.7f, 0.3f, 0f, 0.8f), new Item("A/Environments/SM_Env_Mushroom_01", -0.4f, 0.4f, 0f) },
            new[] { new Item("A/Props/SM_Prop_Washingline_03", 0f, 0f, 0f, 0.8f), new Item("A/Props/SM_Prop_Basket_03", 1.6f, 0.3f, 20f) },
        };

        public static string SouthYards(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "SouthYards", true);
            var rng = new System.Random(Seed + 7);
            var taken = new List<Vector2>();
            int n = 0;
            foreach (var w in L2Layout.Walks)
            {
                int k = 0;
                foreach (var (edge, normal, _) in L2Greybox.Outline(w))
                {
                    // Camera-side edges only (south-facing), every other outline sample.
                    if (normal.y > -0.3f || (k++ % 2) == 1)
                    {
                        continue;
                    }

                    Vector2 q = edge + normal * (1.7f + (float)rng.NextDouble() * 2.6f) + new Vector2(-normal.y, normal.x) * ((float)rng.NextDouble() - 0.5f) * 1.5f;
                    if (L2Layout.SignedDistance(q) < 1.3f || InAnyLot(q, 0.4f) || L2Dressing.Reserved(q) || EdgeReserved(q) || L2Layout.InGateWall(q, 1f)
                        || L2Layout.IsInArena(q, 1f) || L2Dressing.Near(taken, q, 2.8f) || L2Layout.InStair(q, 1.2f))
                    {
                        continue;
                    }

                    var cluster = SouthClusters[rng.Next(SouthClusters.Length)];
                    float yaw = (float)rng.NextDouble() * 360f;
                    var frame = new House { Lot = new L2Layout.Lot("Yard", q.x, q.y, 3f, 3f, yaw, L2Layout.LotKind.Low), Forward = Dir(yaw), Right = Dir(yaw + 90f) };
                    int placed = PlaceLow(cluster, group, frame, q, rng, Mathf.Min(1.7f, L2Layout.ArenaCap(q)));
                    if (placed > 0)
                    {
                        taken.Add(q);
                        n++;
                    }
                }
            }

            return $"camera-side yards {n}";
        }

        private static int PlaceLow(Item[] template, Transform group, House frame, Vector2 origin, System.Random rng, float maxHeight)
        {
            int n = 0;
            foreach (var item in template)
            {
                Vector2 p = origin + frame.Right * item.P.x + frame.Forward * item.P.z;
                if (L2Layout.SignedDistance(p) < 0.9f || InBuilding(p, 0.1f) || L2Dressing.Reserved(p))
                {
                    continue;
                }

                var go = L2Build.Spawn(item.Key, group, Vector3.zero, Vector3.zero, item.Scale);
                go.transform.SetPositionAndRotation(new Vector3(p.x, L2Layout.TerrainHeight(p), p.y) + Vector3.up * item.P.y, Quaternion.Euler(item.Euler.x, frame.Lot.Yaw + item.Euler.y, item.Euler.z));
                if (L2Build.Top(go) - go.transform.position.y > maxHeight)
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                if (rng.NextDouble() < 0.25)
                {
                    L2Dressing.Charred(go, 1f, rng);
                }

                L2Dressing.Finish(go, L1Build.PropStatic, false);
                n++;
            }

            return n;
        }

        // ---------------------------------------------------------------- Fight rims (camera side)

        // The camera-side rim of every fight is capped at knee height (nothing may hide a telegraph), so it was
        // the barest ground in the level. It gets its own dense, low dressing: the back yards of the houses whose
        // fronts were burnt away. Everything stays under ~1.1 m.
        private static readonly Item[][] RimClusters =
        {
            new[] { new Item("A/Buildings/SM_Bld_Wall_01", 0f, 0f, 0f, 0.85f), new Item("A/Props/SM_Prop_Sack_01", 1.4f, -0.5f, 30f), new Item("A/Environments/SM_Env_Plant_03", -1.2f, -0.4f, 0f, 0.7f) },
            new[] { new Item("A/Buildings/SM_Bld_Fence_02", 0f, 0f, 0f, 0.85f), new Item("A/Buildings/SM_Bld_FencePost_01", 0.9f, 0.05f, 0f, 0.85f), new Item("A/Props/SM_Prop_Pumpkin_02", -0.6f, -0.6f, 40f) },
            new[] { new Item("A/Props/SM_Prop_Logpile_01", 0f, 0f, 0f, 0.55f), new Item("A/Environments/SM_Env_TreeStump_01", 1.3f, 0.3f, 30f, 0.5f), new Item("A/Props/SM_Prop_Loghalf_01", -1.2f, 0.4f, 70f, 0.8f) },
            new[] { new Item("A/Props/SM_Prop_Barrel_01", 0f, 0f, 30f, 0.9f, 0.3f, 90f), new Item("A/Props/SM_Prop_Crate_02", 0.9f, 0.3f, 25f, 0.8f), new Item("A/Props/SM_Prop_Sack_03", -0.8f, 0.2f, 60f) },
            new[] { new Item("A/Environments/SM_Env_DirtMound_01", 0f, 0f, 0f, 0.15f, -0.25f), new Item("A/Environments/SM_Env_Plant_04", -0.3f, 0.1f, 0f, 0.6f, 0.12f), new Item("A/Environments/SM_Env_Plant_02", 0.4f, -0.2f, 90f, 0.6f, 0.12f), new Item("A/Props/SM_Prop_Basket_02", 1.3f, 0.4f, 0f) },
            new[] { new Item("A/Environments/SM_Env_Bush_03", 0f, 0f, 0f, 0.55f), new Item("A/Environments/SM_Env_Bush_04", 1f, 0.5f, 120f, 0.45f), new Item("A/Environments/SM_Env_Rock_012", -0.9f, 0.2f, 0f, 0.3f) },
            new[] { new Item("A/Props/SM_Prop_Cart_Wheel_01", 0f, 0f, 20f, 1f, 0.05f, 0f, 88f), new Item("K/Props/SM_Prop_Beam_01", 0.8f, 0.6f, 60f, 0.8f, 0.1f, 90f), new Item("A/Props/SM_Prop_Pot_03", -0.7f, -0.3f, 0f, 1f, 0.1f, 0f, 90f) },
            new[] { new Item("Skeletons/L2_Skeleton_Prone", 0f, 0f, 0f), new Item("A/Weapons/SM_Wep_Pitchfork_01", 0.9f, 0.4f, 70f, 1f, 0.04f, 90f) },
            new[] { new Item("A/Props/SM_Prop_Chest_01", 0f, 0f, 15f, 0.85f), new Item("A/Props/SM_Prop_Book_01", 0.7f, 0.4f, 40f), new Item("A/Props/SM_Prop_Scroll_02", -0.6f, 0.5f, 10f) },
        };

        public static string ArenaRims(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "ArenaRims", true);
            var rng = new System.Random(Seed + 8);
            var taken = new List<Vector2>();
            int n = 0;
            foreach (var arena in L2Layout.Arenas)
            {
                // Rings outward from the fight's edge, over its camera-side (south) half.
                for (float ring = arena.Radius + 1f; ring < arena.Radius + 7.5f; ring += 1.7f)
                {
                    int steps = Mathf.CeilToInt(Mathf.PI * ring / 1.9f);
                    for (int k = 0; k <= steps; k++)
                    {
                        float a = Mathf.PI + Mathf.PI * k / steps + ((float)rng.NextDouble() - 0.5f) * 0.15f;   // 180..360 degrees: the south half
                        Vector2 q = arena.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ring + ((float)rng.NextDouble() - 0.5f) * 0.8f);
                        if (L2Layout.SignedDistance(q) < 0.8f || InBuilding(q, 0.4f) || L2Dressing.Reserved(q) || L2Layout.InGateWall(q, 1f)
                            || L2Layout.IsInArena(q, 0.6f) || L2Dressing.Near(taken, q, 1.9f) || L2Layout.InStair(q, 1.2f))
                        {
                            continue;
                        }

                        var cluster = RimClusters[rng.Next(RimClusters.Length)];
                        float yaw = (float)rng.NextDouble() * 360f;
                        var frame = new House { Lot = new L2Layout.Lot("Rim", q.x, q.y, 2f, 2f, yaw, L2Layout.LotKind.Low), Forward = Dir(yaw), Right = Dir(yaw + 90f) };
                        if (PlaceLow(cluster, group, frame, q, rng, Mathf.Min(1.15f, L2Layout.ArenaCap(q))) > 0)
                        {
                            taken.Add(q);
                            n++;
                        }
                    }
                }
            }

            return $"fight rims: {n} yard scenes";
        }

        // Inside a lot that has a house standing on it (yards are open ground).
        private static bool InBuilding(Vector2 p, float margin)
        {
            foreach (var h in Houses)
            {
                if (h.Go == null)
                {
                    continue;
                }

                var grown = h.Lot;
                grown.Size += Vector2.one * margin * 2f;
                if (L2Greybox.Contains(grown, p))
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- Street edges

        private enum EdgeStyle { Stone, Fence, Hedge, Picket, Rockwall }

        private static readonly Dictionary<EdgeStyle, (string key, float length, float height, float pivotShift)> EdgePieces = new Dictionary<EdgeStyle, (string, float, float, float)>
        {
            [EdgeStyle.Stone] = ("A/Buildings/SM_Bld_Wall_01", 3.9f, 1.04f, 0f),
            [EdgeStyle.Fence] = ("A/Buildings/SM_Bld_Fence_01", 3.1f, 1.25f, 0f),
            [EdgeStyle.Hedge] = ("A/Environments/SM_Env_Hedge_01", 5.2f, 1.92f, 0f),
            [EdgeStyle.Picket] = ("K/Props/SM_Prop_Fence_01", 1.6f, 1.54f, 0.8f),
            [EdgeStyle.Rockwall] = ("K/Buildings/SM_Bld_Rockwall_Straight_01", 3.9f, 2.7f, 0f),
        };

        // A continuous edge along every street where no house fronts it: low stone walls and fences in the
        // village, hedges and picket fences on the country road, stone walls round the churchyard. It says
        // "the street ends here" before the invisible boundary does.
        public static string EdgeWalls(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "EdgeWalls", true);
            var rng = new System.Random(Seed + 2);
            var segments = new List<(Vector2 c, float half)>();
            int n = 0;
            foreach (var w in L2Layout.Walks)
            {
                foreach (var (edge, normal, _) in L2Greybox.Outline(w))
                {
                    Vector2 q = edge + normal * 0.75f;
                    if (L2Layout.SignedDistance(q) < 0.5f || L2Layout.InStair(q, 1.2f) || EdgeReserved(q) || InAnyLot(q + normal * 0.9f, 0.7f) || InAnyLot(q, 0.4f))
                    {
                        continue;
                    }

                    var style = Style(q, normal, rng);
                    var piece = EdgePieces[style];
                    float cap = L2Layout.ArenaCap(q);
                    if (piece.height > cap)
                    {
                        style = EdgeStyle.Stone;
                        piece = EdgePieces[style];
                    }

                    float half = piece.length * 0.5f;
                    bool overlap = false;
                    foreach (var s in segments)
                    {
                        if ((s.c - q).magnitude < (s.half + half) * 0.86f)
                        {
                            overlap = true;
                            break;
                        }
                    }

                    if (overlap)
                    {
                        continue;
                    }

                    Vector2 tangent = new Vector2(-normal.y, normal.x);
                    float yaw = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg - 90f;
                    var go = L2Build.Spawn(piece.key, group, Vector3.zero, Vector3.zero);
                    Vector3 shift = Quaternion.Euler(0f, yaw, 0f) * new Vector3(piece.pivotShift, 0f, 0f);
                    go.transform.SetPositionAndRotation(new Vector3(q.x, L2Layout.TerrainHeight(q) - 0.05f, q.y) + shift, Quaternion.Euler(0f, yaw + (float)(rng.NextDouble() - 0.5) * 6f, 0f));
                    if (style == EdgeStyle.Stone || style == EdgeStyle.Rockwall)
                    {
                        // Smoke-stained field stone: never a pale slab under the moon.
                        Burnt(go);
                        L2Dressing.Charred(go, 0.3f, rng);
                    }
                    else if (style == EdgeStyle.Fence || style == EdgeStyle.Picket)
                    {
                        L2Dressing.Charred(go, 0.2f, rng);
                    }

                    L2Dressing.Finish(go, L1Build.PropStatic, false);
                    if (piece.height > 1.5f)
                    {
                        L2Build.AddFade(go);
                    }

                    segments.Add((q, half));
                    n++;
                }
            }

            return $"street edges {n}";
        }

        private static EdgeStyle Style(Vector2 q, Vector2 normal, System.Random rng)
        {
            bool country = q.x < -46f || q.y > 112f;
            bool churchyard = L2Layout.TerrainHeight(q) > 3.4f && q.x > -40f && q.x < 22f && q.y > -30f && q.y < 9f;
            double r = rng.NextDouble();
            if (churchyard)
            {
                return normal.y > 0.3f && r < 0.5 ? EdgeStyle.Rockwall : EdgeStyle.Stone;
            }

            if (L2Layout.InFarmland(q))
            {
                // The field road: post-and-rail fences and low field walls, the odd hedge.
                return r < 0.45 ? EdgeStyle.Fence : r < 0.7 ? EdgeStyle.Stone : r < 0.88 ? EdgeStyle.Picket : EdgeStyle.Hedge;
            }

            if (country)
            {
                return r < 0.45 ? EdgeStyle.Picket : r < 0.75 ? EdgeStyle.Hedge : EdgeStyle.Fence;
            }

            return r < 0.45 ? EdgeStyle.Stone : r < 0.8 ? EdgeStyle.Fence : EdgeStyle.Hedge;
        }

        private static bool EdgeReserved(Vector2 p)
        {
            if (L2Layout.InCreek(p, 0.2f) || L2Layout.OnBridge(p, 1.2f))
            {
                return true;   // the bridge has its own rails; the creek its own banks
            }

            foreach (var b in L2Layout.Blockers)
            {
                if ((p - b.Center).magnitude < Mathf.Max(b.Size.x, b.Size.y) * 0.5f + 1.4f)
                {
                    return true;
                }
            }

            foreach (var z in L2Layout.FireZones)
            {
                if ((p - z.Center).magnitude < Mathf.Max(z.Size.x, z.Size.y) * 0.5f + 1f)
                {
                    return true;
                }
            }

            if (L2Layout.InGateWall(p, 0.6f) || (p - L2Layout.Markers["Checkpoint_Shrine"]).magnitude < 3f || (p - L2Layout.Markers["Exit_ToL3"]).magnitude < 4f)
            {
                return true;
            }

            // The church itself fronts its yard.
            return L2Layout.ChurchNave.Contains(p) || (p - L2Layout.ChurchTower).magnitude < 3f;
        }

        // ---------------------------------------------------------------- Trees and bushes

        public static string Trees(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Trees", true);
            var rng = new System.Random(Seed + 3);
            string[] treeKeys =
            {
                "A/Environments/SM_Env_TreePine_01", "A/Environments/SM_Env_TreePine_02", "A/Environments/SM_Env_TreePine_03", "A/Environments/SM_Env_TreePine_04",
                "A/Environments/SM_Env_Tree_01", "A/Environments/SM_Env_Tree_03", "A/Environments/SM_Env_Tree_05", "A/Environments/SM_Env_TreeBirch_02",
                "A/Environments/SM_Env_TreeDead_01", "A/Environments/SM_Env_TreeDead_02", "K/Environments/SM_Env_Tree_Twisted_01", "K/Environments/SM_Env_Tree_Twisted_02",
            };
            string[] bushKeys = { "A/Environments/SM_Env_Bush_01", "A/Environments/SM_Env_Bush_02", "A/Environments/SM_Env_Bush_03", "A/Environments/SM_Env_Bush_04" };
            var taken = new List<Vector2>();
            var fires = new List<Vector2>();
            foreach (var h in Houses)
            {
                if (h.State == HouseState.Burning)
                {
                    fires.Add(h.Lot.Center);
                }
            }

            int nTrees = 0, nBushes = 0;
            for (int i = 0; i < 60000 && (nTrees < 230 || nBushes < 420); i++)
            {
                var p = new Vector2(-104f + (float)rng.NextDouble() * 172f, -72f + (float)rng.NextDouble() * 226f);
                float sd = L2Layout.SignedDistance(p);
                bool bush = rng.NextDouble() < 0.55;
                if (bush ? (sd < 0.9f || sd > 6f || nBushes >= 420) : (sd < 2.6f || sd > 16f || nTrees >= 230))
                {
                    continue;
                }

                // The church slope gets its own green-less shoulder of scrub and trees; elsewhere trees thin
                // with distance from the street.
                bool slope = L2Layout.DistanceToSegment(p, L2Layout.OnRouteXZ("E3_Bottom"), L2Layout.OnRouteXZ("E3_Top"), out _) < 12f;
                float keep = bush ? (slope ? 0.9f : 0.5f) : (slope ? 0.9f : Mathf.Lerp(0.8f, 0.25f, Mathf.InverseLerp(3f, 16f, sd)));
                if (L2Layout.InFarmland(p))
                {
                    keep *= 0.3f;   // open fields: only the odd tree along the field walls (L2Farmland plants the rest)
                }
                if (rng.NextDouble() > keep || L2Dressing.Reserved(p) || EdgeReserved(p) || InAnyLot(p, bush ? 0.6f : 1.4f) || L2Dressing.Near(taken, p, bush ? 1.5f : 3.2f)
                    || L2Layout.IsInArena(p, 1f) || L2Layout.InGateWall(p, 1f) || OnChurchyard(p))
                {
                    continue;
                }

                float cap = L2Layout.ArenaCap(p);
                GameObject go;
                if (bush)
                {
                    go = L2Build.Spawn(bushKeys[rng.Next(bushKeys.Length)], group, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 0.55f + (float)rng.NextDouble() * 0.4f);
                }
                else
                {
                    // Trees keep out of every fight's sight-lines except behind it (north), where they frame it.
                    if (cap < 3f || NearFightSide(p))
                    {
                        continue;
                    }

                    go = L2Build.Spawn(treeKeys[rng.Next(treeKeys.Length)], group, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 0.75f + (float)rng.NextDouble() * 0.45f);
                }

                go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) - 0.05f, p.y);
                // Nor trees at the bridge mouths: faded over the creek their canopies smear across the water.
                if (L2Build.Top(go) - go.transform.position.y > cap || (!bush && L2Layout.OnBridge(p, 4f)))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                // Near the fires the trees are scorched black.
                if (L2Dressing.Near(fires, p, 10f))
                {
                    L2Dressing.Charred(go, bush ? 0.7f : 0.85f, rng);
                }

                L2Dressing.Finish(go, L1Build.EnvironmentStatic, false);
                if (!bush)
                {
                    L2Build.AddFade(go);
                    nTrees++;
                }
                else
                {
                    nBushes++;
                }

                taken.Add(p);
            }

            return $"trees {nTrees}, bushes {nBushes}";
        }

        // More trees round the streets, on top of Trees: mixed kinds, sizes from sapling to old tree, every one turned
        // and leaning a little differently, all in the ground the player never walks. Additive: it reads what is
        // already standing (hand-placed pieces included) and keeps clear of it, and only ever rebuilds its own group.
        public static string MoreTrees(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "TreesExtra", true);
            var rng = new System.Random(Seed + 31);
            string[] keys =
            {
                "A/Environments/SM_Env_TreePine_01", "A/Environments/SM_Env_TreePine_02", "A/Environments/SM_Env_TreePine_03", "A/Environments/SM_Env_TreePine_04",
                "A/Environments/SM_Env_Tree_01", "A/Environments/SM_Env_Tree_03", "A/Environments/SM_Env_Tree_05", "A/Environments/SM_Env_TreeBirch_02",
                "A/Environments/SM_Env_TreeDead_01", "A/Environments/SM_Env_TreeDead_02", "K/Environments/SM_Env_Tree_Twisted_01", "K/Environments/SM_Env_Tree_Twisted_02",
            };

            // What already stands: tree trunks (spacing) and the footprint of everything else above knee height.
            var trunks = new List<Vector2>();
            var occupied = new List<Bounds>();
            foreach (var top in new[] { "SetDressing", "Landmarks" })
            {
                var t = root.Find(top);
                foreach (Transform g in t)
                {
                    if (g == group)
                    {
                        continue;
                    }

                    foreach (Transform item in g)
                    {
                        if (g.name == "Trees")
                        {
                            trunks.Add(new Vector2(item.position.x, item.position.z));
                            continue;
                        }

                        var col = item.GetComponent<Collider>();
                        if (col != null && g.name == "Lots")
                        {
                            occupied.Add(col.bounds);
                            continue;
                        }

                        foreach (var r in item.GetComponentsInChildren<MeshRenderer>())
                        {
                            if (r.enabled && r.bounds.size.y > 0.7f)
                            {
                                occupied.Add(r.bounds);
                            }
                        }
                    }
                }
            }

            var burning = new List<Vector2>();
            foreach (var (lot, _) in L2Greybox.ReadLots(root))
            {
                if (lot.Kind == L2Layout.LotKind.Burning || lot.Kind == L2Layout.LotKind.BurningLow)
                {
                    burning.Add(lot.Center);
                }
            }

            bool Occupied(Vector2 p, float margin)
            {
                foreach (var b in occupied)
                {
                    if (p.x > b.min.x - margin && p.x < b.max.x + margin && p.y > b.min.z - margin && p.y < b.max.z + margin)
                    {
                        return true;
                    }
                }

                return false;
            }

            int n = 0;
            for (int i = 0; i < 40000 && n < 140; i++)
            {
                var p = new Vector2(-104f + (float)rng.NextDouble() * 172f, -72f + (float)rng.NextDouble() * 226f);
                float sd = L2Layout.SignedDistance(p);
                if (sd < 2.2f || sd > 13f || rng.NextDouble() > Mathf.Lerp(0.9f, 0.3f, Mathf.InverseLerp(2.2f, 13f, sd)))
                {
                    continue;
                }

                if (L2Dressing.Reserved(p) || L2Layout.IsInArena(p, 2f) || NearFightSide(p) || L2Layout.InGateWall(p, 2f) || L2Layout.InCreek(p, 1f)
                    || L2Layout.OnBridge(p, 4f) || L2Farmland.InField(p, out _) || L2Dressing.Near(trunks, p, 2.8f) || Occupied(p, 1.1f))
                {
                    continue;
                }

                // Size from sapling to old tree; the bigger ones lean less.
                float scale = 0.5f + Mathf.Pow((float)rng.NextDouble(), 1.6f) * 0.9f;
                float lean = (1.5f - scale) * 6f;
                var go = L2Build.Spawn(keys[rng.Next(keys.Length)], group, Vector3.zero,
                    new Vector3(((float)rng.NextDouble() - 0.5f) * lean, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * lean), scale);
                go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) - 0.08f, p.y);
                if (L2Build.Top(go) - go.transform.position.y > L2Layout.ArenaCap(p))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                if (L2Dressing.Near(burning, p, 10f))
                {
                    L2Dressing.Charred(go, 0.85f, rng);   // scorched near the fires
                }

                L2Dressing.Finish(go, L1Build.EnvironmentStatic, false);
                L2Build.AddFade(go);
                trunks.Add(p);
                n++;
            }

            return $"more trees {n}";
        }

        private static bool NearFightSide(Vector2 p)
        {
            foreach (var arena in L2Layout.Arenas)
            {
                Vector2 offset = p - arena.Center;
                if (offset.magnitude < arena.Radius + 9f && Vector2.Dot(offset, L2Layout.CameraForward) < arena.Radius * 0.6f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OnChurchyard(Vector2 p)
        {
            return L2Layout.TerrainHeight(p) > 3.4f && p.x > -36f && p.x < 16f && p.y > -30f && p.y < 7f;
        }

        // ---------------------------------------------------------------- Fires that lead the way

        // Braziers the raiders lit, burning carts and a campfire at the turns of the route, wherever the way
        // on would otherwise be dark: the player walks from light to light.
        public static string Beacons(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Beacons", true);
            var rng = new System.Random(Seed + 4);
            var lights = new List<Vector2>();
            foreach (var l in root.GetComponentsInChildren<Light>())
            {
                if (l.type == LightType.Point && l.intensity > 15f)
                {
                    lights.Add(new Vector2(l.transform.position.x, l.transform.position.z));
                }
            }

            int n = 0;
            var route = L2Layout.Route;
            float sinceLight = 0f;
            for (int i = 1; i < route.Length - 1; i++)
            {
                sinceLight += (route[i].P - route[i - 1].P).magnitude;
                Vector2 into = (route[i].P - route[i - 1].P).normalized;
                Vector2 outOf = (route[i + 1].P - route[i].P).normalized;
                float turn = Vector2.SignedAngle(into, outOf);
                bool dark = !L2Dressing.Near(lights, route[i].P, 13f);
                if (!dark || (Mathf.Abs(turn) < 25f && sinceLight < 26f))
                {
                    continue;
                }

                // The outside of the turn (or the north side of a straight), just off the street.
                Vector2 bisector = (outOf - into);
                Vector2 outward = bisector.sqrMagnitude > 0.01f ? -bisector.normalized : new Vector2(0f, 1f);
                if (Mathf.Abs(turn) < 25f)
                {
                    outward = new Vector2(-into.y, into.x);
                    if (outward.y < 0f)
                    {
                        outward = -outward;
                    }
                }

                Vector2 p = route[i].P + outward * (route[i].HalfWidth + 1f);
                for (int k = 0; k < 6 && (L2Layout.SignedDistance(p) < 0.5f || InAnyLot(p, 0.3f)); k++)
                {
                    p += outward * 0.5f;
                }

                if (L2Layout.SignedDistance(p) < 0.5f || L2Layout.IsInArena(p, 1.5f) || L2Dressing.Reserved(p) || L2Layout.ArenaCap(p) < 2f)
                {
                    continue;
                }

                int kind = rng.Next(3);
                var head = new GameObject("Beacon_" + n).transform;
                head.SetParent(group, false);
                head.position = L2Build.Ground(p);
                if (kind == 0)
                {
                    var brazier = L2Build.Spawn("K/Props/SM_Prop_Brazier_01", head, Vector3.zero, Vector3.zero);
                    L2Dressing.Finish(brazier, L1Build.PropStatic, false);
                    L2Fire.Fire(head, "Fire", Vector3.up * 1.75f, 0.42f, 22f, 10f, false);
                }
                else if (kind == 1)
                {
                    var cart = L2Build.Spawn("A/Props/SM_Prop_Cart_01", head, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f));
                    L2Dressing.Charred(cart, 1f, rng);
                    L2Dressing.Finish(cart, L1Build.PropStatic, false);
                    L2Fire.Fire(head, "Fire", Vector3.up * 0.6f, 1f, 26f, 11f, true, new Vector3(1.2f, 0f, 2f));
                }
                else
                {
                    var camp = L2Build.Spawn("A/Environments/SM_Env_CampFire_01", head, Vector3.zero, Vector3.zero);
                    L2Dressing.Finish(camp, L1Build.PropStatic, false);
                    L2Fire.Fire(head, "Fire", Vector3.up * 0.15f, 0.6f, 20f, 9f, false);
                }

                L1Build.SetLayer(head.gameObject, "Obstacles");
                lights.Add(p);
                sinceLight = 0f;
                n++;
            }

            return $"beacons {n}";
        }

        // ---------------------------------------------------------------- The dead

        // Skeletons where the fire caught people: in front of burning and burnt-out houses, one slumped
        // against a wall, others fallen reaching for the street; a few by the fire beds.
        public static string Skeletons(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Skeletons", true);
            var rng = new System.Random(Seed + 5);
            var taken = new List<Vector2>();
            int n = 0;
            foreach (var h in Houses)
            {
                if (h.State != HouseState.Burning && h.State != HouseState.Ruin)
                {
                    continue;
                }

                int count = h.State == HouseState.Burning ? 1 + rng.Next(2) : rng.NextDouble() < 0.5 ? 1 : 0;
                for (int k = 0; k < count; k++)
                {
                    bool slumped = k == 0 && rng.NextDouble() < 0.35;
                    Vector2 p = h.Front + h.Right * ((float)rng.NextDouble() - 0.5f) * h.Lot.Size.x * 0.8f + h.Forward * (slumped ? 0.3f : 0.6f + (float)rng.NextDouble() * 1f);
                    float yaw = slumped ? h.Lot.Yaw : (float)rng.NextDouble() * 360f;
                    string key = slumped ? "Skeletons/L2_Skeleton_Slumped" : L2Skeletons.Keys[rng.Next(L2Skeletons.Keys.Length)];
                    if (key.EndsWith("Slumped") && !slumped)
                    {
                        key = "Skeletons/L2_Skeleton_Supine";
                    }

                    if (Skeleton(group, key, p, yaw, taken))
                    {
                        n++;
                    }
                }
            }

            foreach (var z in L2Layout.FireZones)
            {
                Vector2 p = z.Center + Dir(z.Yaw) * (z.Size.y * 0.5f + 1.2f) + Dir(z.Yaw + 90f) * ((float)rng.NextDouble() - 0.5f) * z.Size.x * 0.6f;
                if (Skeleton(group, L2Skeletons.Keys[rng.Next(L2Skeletons.Keys.Length - 1)], p, (float)rng.NextDouble() * 360f, taken))
                {
                    n++;
                }
            }

            return $"skeletons {n}";
        }

        private static bool Skeleton(Transform group, string key, Vector2 p, float yaw, List<Vector2> taken)
        {
            if (L2Layout.SignedDistance(p) < -0.6f || L2Layout.IsInArena(p, 0.5f) || L2Dressing.Reserved(p) || InAnyLot(p, 0f) || L2Dressing.Near(taken, p, 1.6f))
            {
                return false;
            }

            var go = L2Build.Spawn(key, group, Vector3.zero, new Vector3(0f, yaw, 0f));
            go.transform.position = L2Build.Ground(p) + Vector3.down * 0.02f;
            L2Dressing.Finish(go, L1Build.PropStatic, false);
            taken.Add(p);
            return true;
        }

        // ---------------------------------------------------------------- Helpers

        private static Vector2 Dir(float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        // Swaps the lived-in night palette for soot-dark stone.
        public static void Burnt(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        continue;
                    }

                    if (mats[i].name == "L2_Knights_Night")
                    {
                        mats[i] = L2Build.Mat("L2_Knights_Soot");
                    }
                    else if (mats[i].name == "L2_Adventure_Night")
                    {
                        mats[i] = L2Build.Mat("L2_Adventure_Soot");
                    }
                }

                r.sharedMaterials = mats;
            }
        }

        private static bool InAnyLot(Vector2 p, float margin)
        {
            foreach (var lot in AllLots)
            {
                var grown = lot;
                grown.Size += Vector2.one * margin * 2f;
                if (L2Greybox.Contains(grown, p))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
