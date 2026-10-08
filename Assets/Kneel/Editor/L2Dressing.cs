using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Dresses the L2 greybox with the burnt village. Lot, blocker and gate boxes stay as the (invisible)
    // collision, so no doorway can be entered and blockers keep their exact solid bounds; everything placed
    // here is visual. Each pass is seeded and rebuilds only its own group.
    public static class L2Dressing
    {
        private const int Seed = 2026;

        [MenuItem("Kneel/L2/Dressing/Rebuild All")]
        public static void RebuildAllMenu()
        {
            Debug.Log("[L2] " + RebuildAll());
        }

        public static string RebuildAll()
        {
            var root = L2Build.RequireRoot().transform;
            // Retired passes: stone meshes gave way to the paving material on the ground (L2Paving).
            foreach (var retired in new[] { "SetDressing/Cobbles", "SetDressing/Flagstones" })
            {
                var old = root.Find(retired);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            Physics.SyncTransforms();
            var log = new List<string>
            {
                HideGreybox(root),
                L2Village.Buildings(root),
                L2Village.SpreadingFires(root),
                SetPieces(root),
                FireBeds(root),
                Cover(root),
                RetainingWalls(root),
                Graveyard(root),
                ChurchStair(root),
            };
            Physics.SyncTransforms();
            log.Add(L2Village.Vignettes(root));
            log.Add(L2Creek.Dress(root));
            log.Add(L2Village.Squares(root));
            log.Add(L2Village.EdgeWalls(root));
            log.Add(L2Farmland.Dress(root));
            log.Add(L2Village.Trees(root));
            log.Add(L2Village.MoreTrees(root));
            log.Add(L2Village.SouthYards(root));
            log.Add(L2Village.ArenaRims(root));
            log.Add(StreetClutter(root));
            log.Add(GroundCover(root));
            log.Add(Backdrop(root));
            log.Add(L2Bakers.PlaceDead(root));
            log.Add(L2Village.Beacons(root));
            log.Add(L2Village.Skeletons(root));
            log.Add(ClearShrine(root));
            L2LevelTools.ApplyLayers();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return "Dressing: " + string.Join(" | ", log);
        }

        // ---------------------------------------------------------------- Greybox

        // Greybox boxes stop rendering; their colliders stay (lots, blockers, church, gate).
        private static string HideGreybox(Transform root)
        {
            int hidden = 0;
            foreach (var path in new[] { "SetDressing/Lots", "SetDressing/Blockers", "Landmarks/Church", "Landmarks/Gates" })
            {
                var group = root.Find(path);
                if (group == null)
                {
                    continue;
                }

                foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Only the plain greybox boxes; dressed prefab instances under the same groups keep rendering.
                    if (!PrefabUtility.IsPartOfPrefabInstance(r.gameObject))
                    {
                        r.enabled = false;
                        hidden++;
                    }
                }
            }

            return $"greybox hidden ({hidden})";
        }

        // ---------------------------------------------------------------- Set pieces

        private static string SetPieces(Transform root)
        {
            var landmarks = L2Build.Group(root, "Landmarks", false);
            var group = L2Build.Group(landmarks, "SetPieces", true);
            var m = L2Layout.Markers;
            int n = 0;

            // Blockers: tall burning heaps (or cold wreckage) standing exactly on their collider boxes.
            int fire = 0;
            foreach (var b in L2Layout.Blockers)
            {
                string key = !b.Fire ? "L2_DebrisBlocker" : fire++ % 2 == 0 ? "L2_FireBlocker_A" : "L2_FireBlocker_B";
                var go = L2Build.Spawn(key, group, Vector3.zero, Vector3.zero, 1f, b.Name + "_" + key);
                go.transform.SetPositionAndRotation(Ground(b.Center), Quaternion.Euler(0f, b.Yaw, 0f));
                go.transform.localScale = new Vector3(b.Size.x / 5f, 1f, Mathf.Max(1f, b.Size.y / 2.2f));
                Finish(go, L1Build.EnvironmentStatic);
                n++;
            }

            Put("L2_Lore_Well", group, m["Well"], 20f);
            var doorframe = Put("L2_Lore_ScorchedDoorframe", group, L2Layout.DoorframeLot.Center, 0f);
            L1Build.SetStatic(doorframe, L1Build.EnvironmentStatic);
            Put("L2_Loot_Heal", group, m["Loot_Heal"], 200f);
            Put("L2_Loot_Weapon", group, m["Loot_WeaponUpgrade"] + new Vector2(0f, -1.1f), 0f);
            var leanto = L2Build.Spawn("K/Buildings/SM_Bld_Leanto_01", group, Vector3.zero, Vector3.zero, 1f, "HealYard_CollapsedLeanto");
            leanto.transform.SetPositionAndRotation(Ground(new Vector2(15.9f, -57.3f)) + Vector3.down * 0.3f, Quaternion.Euler(0f, -90f, -14f));
            Finish(leanto, L1Build.PropStatic, false);

            // The checkpoint shrine faces its respawn point.
            var shrine = L2Build.Spawn("L1/L1_CheckpointShrine", group, Vector3.zero, Vector3.zero);
            // It stands just behind its marker (the spot the player kneels at), facing the respawn point.
            Vector2 face = (m["RespawnPoint"] - m["Checkpoint_Shrine"]).normalized;
            shrine.transform.SetPositionAndRotation(Ground(L2Layout.ShrinePosition), Quaternion.Euler(0f, Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg + L2Layout.ShrineYawOffset, 0f));

            // Archer perches: the parapet sits on the far side of each archer (they shoot over it into the square).
            Put("L2_ArcherPerch", group, m["E3_ArcherPerch_1"], 135f);
            Put("L2_ArcherPerch", group, m["E5_ArcherPerch_1"], 90f);
            Put("L2_ArcherPerch", group, m["E5_ArcherPerch_2"], -90f);

            foreach (var g in L2Layout.Gates)
            {
                Gatehouse(root, g);
            }

            // Raid evidence: dropped standards and carts overturned in flight, at path edges.
            foreach (var (key, p, yaw) in new[]
            {
                ("L2_DroppedBanner", new Vector2(-41f, -54.6f), 70f), ("L2_DroppedBanner", new Vector2(36.5f, 1.2f), 200f), ("L2_DroppedBanner", new Vector2(33.5f, 42.8f), 20f),
                ("L2_OverturnedCart", new Vector2(-64.5f, -53.4f), 15f), ("L2_OverturnedCart", new Vector2(12.2f, -40.8f), 100f), ("L2_OverturnedCart", new Vector2(44.2f, -31f), 80f),
                ("L2_OverturnedCart", new Vector2(-30.5f, 35.2f), 160f), ("L2_OverturnedCart", new Vector2(44.2f, 60f), 10f),
                ("L2_MarketRemains", new Vector2(-8.8f, -38.2f), 160f), ("L2_MarketRemains", new Vector2(8.6f, -37f), 200f), ("L2_MarketRemains", new Vector2(0.8f, 104.6f), 180f),
                ("L2_MarketRemains", new Vector2(19.2f, 104.2f), 175f), ("L2_MarketRemains", new Vector2(-2.5f, 88.5f), 110f),
            })
            {
                var go = Put(key, group, p, yaw);
                if (!FitsUnderCamera(go) || L2Layout.IsInArena(p, -2f))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                n++;
            }

            return $"set pieces {n + 11}";
        }

        // A gatehouse across the route: an arched gate with round towers either side and the wall running off
        // both ways. The leaf fills the arch and swings away when the lever on the near side is pulled.
        private static void Gatehouse(Transform root, L2Layout.Gate g)
        {
            var gateGroup = root.Find("Landmarks/Gates/" + g.Name);
            var gateBox = gateGroup != null ? gateGroup.Find("Gate") : null;
            if (gateBox == null)
            {
                return;
            }

            foreach (var name in new[] { "GateVisual", "LeverVisual", "Gatehouse" })
            {
                var old = gateGroup.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            var house = new GameObject("Gatehouse").transform;
            house.SetParent(gateGroup, false);
            Vector2 c = g.Center;
            float ground = L2Layout.Height(c);
            var rotation = Quaternion.Euler(0f, g.Yaw, 0f);
            Vector3 along = rotation * Vector3.right;
            Vector3 center = new Vector3(c.x, ground, c.y);

            // The arch: solid (its own colliders close the lane either side of the opening).
            var arch = L2Build.Spawn("K/Buildings/SM_Bld_Castle_Wall_Gate_01", house, Vector3.zero, Vector3.zero, 1f, "Arch");
            arch.transform.SetPositionAndRotation(center - Vector3.up * 0.05f, rotation);
            L2Village.Burnt(arch);
            Finish(arch, L1Build.EnvironmentStatic, true);
            L2Build.AddFade(arch);

            var rng = new System.Random(Seed + 9);
            foreach (float side in new[] { -1f, 1f })
            {
                // A round tower either side of the arch.
                Vector3 towerAt = center + along * side * 4.05f;
                var tower = L2Build.Spawn("K/Buildings/SM_Bld_Castle_Tower_Round_01", house, Vector3.zero, Vector3.zero, 1f, side < 0 ? "Tower_W" : "Tower_E");
                tower.transform.SetPositionAndRotation(new Vector3(towerAt.x, L2Layout.TerrainHeight(new Vector2(towerAt.x, towerAt.z)) - 0.1f, towerAt.z), rotation);
                L2Build.Spawn("K/Buildings/SM_Bld_Castle_Tower_Round_Top_01", tower.transform, Vector3.up * 3f, Vector3.zero, 1f, "Top");
                L2Village.Burnt(tower);
                Finish(tower, L1Build.EnvironmentStatic, true);
                L2Build.AddFade(tower);

                // The wall runs on from the towers to the houses, a tower at its end.
                Vector2 along2 = g.Along;
                float from = 5.6f;
                int segments = Mathf.CeilToInt((g.WallLength - from) / 3.9f);
                for (int i = 0; i < segments; i++)
                {
                    var p = c + along2 * side * (from + i * 3.9f + 1.95f) + g.Forward * ((float)rng.NextDouble() - 0.5f) * 0.3f;
                    var wall = L2Build.Spawn("K/Buildings/SM_Bld_Rockwall_Straight_01", house, Vector3.zero, Vector3.zero, 1f, "Wall_" + (side < 0 ? "N" : "S") + i);
                    wall.transform.SetPositionAndRotation(new Vector3(p.x, L2Layout.TerrainHeight(p) - 0.15f, p.y), rotation * Quaternion.Euler(0f, 90f, 0f));
                    wall.transform.localScale = new Vector3(1f, 1.1f, 1f);
                    L2Village.Burnt(wall);
                    Charred(wall, 0.25f, rng);
                    Finish(wall, L1Build.EnvironmentStatic, false);
                    L2Build.AddFade(wall);
                }

                Vector2 endAt = c + along2 * side * (g.WallLength + 0.6f);
                var endTower = L2Build.Spawn("K/Buildings/SM_Bld_Castle_Tower_Round_01", house, Vector3.zero, Vector3.zero, 0.85f, side < 0 ? "EndTower_N" : "EndTower_S");
                endTower.transform.SetPositionAndRotation(new Vector3(endAt.x, L2Layout.TerrainHeight(endAt) - 0.1f, endAt.y), rotation);
                L2Village.Burnt(endTower);
                Finish(endTower, L1Build.EnvironmentStatic, false);
                L2Build.AddFade(endTower);
            }

            // Torches either side of the arch on the near side: the gate reads as the player comes up to it.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 at = center + along * side * 2.2f - rotation * Vector3.forward * 1.45f + Vector3.up * 2.4f;
                L2Fire.Fire(house, side < 0 ? "Torch_W" : "Torch_E", house.InverseTransformPoint(at), 0.28f, side < 0 ? 14f : 0f, 8f, false);
            }

            // The leaf, in the arch.
            var gate = L2Build.Spawn("L2_ShortcutGate", gateGroup, Vector3.zero, Vector3.zero, 1f, "GateVisual");
            gate.transform.SetPositionAndRotation(new Vector3(gateBox.position.x, gateBox.position.y - gateBox.localScale.y * 0.5f, gateBox.position.z), gateBox.rotation);
            Finish(gate, 0, false);

            // The gate opens from the near side only: its lever stands there.
            var shortcut = gateGroup.GetComponent<Kneel.ShortcutGate>();
            if (shortcut == null)
            {
                shortcut = gateGroup.gameObject.AddComponent<Kneel.ShortcutGate>();
            }

            var so = new SerializedObject(shortcut);
            so.FindProperty("blocker").objectReferenceValue = gateBox.GetComponent<Collider>();
            so.FindProperty("leaf").objectReferenceValue = gate.transform.Find("GateLeaf");
            so.FindProperty("openAngle").floatValue = -95f;   // swings in, away from the player at the lever
            so.FindProperty("obstacle").objectReferenceValue = gateBox.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var lever = L2Build.Spawn("L2_GateLever", gateGroup, Vector3.zero, Vector3.zero, 1f, "LeverVisual");
            lever.transform.SetPositionAndRotation(Ground(g.Lever), Quaternion.Euler(0f, g.Yaw + 90f, 0f));
            Finish(lever, 0, false);
            var lso = new SerializedObject(lever.GetComponent<Kneel.GateLever>());
            lso.FindProperty("gate").objectReferenceValue = shortcut;
            lso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Put(string key, Transform parent, Vector2 p, float yaw)
        {
            var go = L2Build.Spawn(key, parent, Vector3.zero, Vector3.zero);
            go.transform.SetPositionAndRotation(Ground(p), Quaternion.Euler(0f, yaw, 0f));
            Finish(go, L1Build.PropStatic, false);
            return go;
        }

        // ---------------------------------------------------------------- Fire beds

        // Each passable fire: burning wreckage (charred beams, split planks, a smashed barrel or cart wheel)
        // with flames over exactly the trigger's bounds and embers glowing in the ash under it.
        private static string FireBeds(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "FireBeds", true);
            var rng = new System.Random(Seed + 1);
            var beds = new List<(L2Layout.FireZone zone, bool back)>();
            foreach (var z in L2Layout.FireZones)
            {
                beds.Add((z, false));
            }

            foreach (var z in L2Layout.BackFires)
            {
                beds.Add((z, true));
            }

            string[] wreck = { "K/Props/SM_Prop_Beam_01", "K/Props/SM_Prop_Beam_01", "A/Props/SM_Prop_Loghalf_01", "A/Props/SM_Prop_Loghalf_02", "K/Props/SM_Prop_CartWheel_01", "A/Props/SM_Prop_Crate_02", "A/Props/SM_Prop_Barrel_02" };
            foreach (var (z, back) in beds)
            {
                var bed = new GameObject(z.Name + "_Bed").transform;
                bed.SetParent(group, false);
                bed.SetPositionAndRotation(Ground(z.Center), Quaternion.Euler(0f, z.Yaw, 0f));

                // Timber lying across the bed; back-fires pile it higher (some ends propped up).
                int pieces = Mathf.CeilToInt(z.Size.x * z.Size.y * (back ? 1.1f : 0.8f)) + 2;
                for (int i = 0; i < pieces; i++)
                {
                    string key = i < pieces * 0.6f ? wreck[rng.Next(2)] : wreck[2 + rng.Next(wreck.Length - 2)];
                    var local = new Vector3(((float)rng.NextDouble() - 0.5f) * z.Size.x * 0.85f, 0.08f, ((float)rng.NextDouble() - 0.5f) * z.Size.y * 0.8f);
                    Vector3 euler = key.Contains("Beam")
                        ? new Vector3(back && rng.NextDouble() < 0.4 ? 55f + (float)rng.NextDouble() * 20f : 86f + (float)rng.NextDouble() * 6f, (float)rng.NextDouble() * 360f, 0f)
                        : new Vector3(key.Contains("Wheel") ? 0f : ((float)rng.NextDouble() - 0.5f) * 30f, (float)rng.NextDouble() * 360f, key.Contains("Wheel") ? 80f : ((float)rng.NextDouble() - 0.5f) * 40f);
                    var piece = L2Build.Spawn(key, bed, local, euler, key.Contains("Beam") ? 0.55f + (float)rng.NextDouble() * 0.45f : 0.8f + (float)rng.NextDouble() * 0.3f);
                    L1Build.StripColliders(piece);
                    Charred(piece, 1f, rng);
                    foreach (var r in piece.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }

                L2Fire.GroundEmbers(bed, Vector3.zero, new Vector2(z.Size.x * 0.95f, z.Size.y * 0.95f));
                float light = z.Name == "DZ_WeaponGap" ? L2Ruins.FireBedLight : back ? L2Ruins.BackFireLight : 0f;
                // Low tongues over the bed (the damage zone must read as fire), a little thinner than a fresh blaze.
                L2Fire.Fire(bed, "Flames", new Vector3(0f, 0.05f, 0f), back ? 0.8f : 0.6f, light, back ? 10f : 7f, back, new Vector3(z.Size.x, 0f, z.Size.y), L2Fire.Shape.Box, back ? 0.8f : 0.9f);
                L1Build.SetStatic(bed.gameObject, L1Build.PropStatic);
            }

            return $"fire beds {L2Layout.FireZones.Length}, back-fires {L2Layout.BackFires.Length}";
        }

        // ---------------------------------------------------------------- Archer cover

        // Hard cover (carts, low walls, rubble) staggered 3-6 m apart between the player and each archer,
        // with open ground between the pieces. All of it stays under 1.3 m.
        private static string Cover(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Cover", true);
            int n = 0;
            foreach (var (key, p, yaw, scale) in L2Layout.CoverPieces)
            {
                var go = L2Build.Spawn(key, group, Vector3.zero, Vector3.zero, scale);
                go.transform.SetPositionAndRotation(Ground(p), Quaternion.Euler(0f, yaw, 0f));
                Charred(go, 0.5f, new System.Random(n));
                Finish(go, L1Build.PropStatic, true);
                n++;
            }

            return $"archer cover {n}";
        }

        // ---------------------------------------------------------------- Retaining walls

        // Stone walls face every cliff the height field makes next to a path (the plateau, the ledge, the terraces).
        private static string RetainingWalls(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "RetainingWalls", true);
            var spacing = new List<Vector2>();
            var soot = new System.Random(Seed + 6);
            int n = 0;
            for (float x = -100f; x < 70f; x += 1f)
            {
                for (float z = -70f; z < 150f; z += 1f)
                {
                    var p = new Vector2(x, z);
                    float sd = L2Layout.SignedDistance(p);
                    if (sd < 0.3f || sd > 6f)
                    {
                        continue;
                    }

                    float h = L2Layout.TerrainHeight(p);
                    Vector2 gradient = new Vector2(L2Layout.TerrainHeight(p + Vector2.right * 0.5f) - L2Layout.TerrainHeight(p - Vector2.right * 0.5f),
                        L2Layout.TerrainHeight(p + Vector2.up * 0.5f) - L2Layout.TerrainHeight(p - Vector2.up * 0.5f));
                    if (gradient.magnitude < 1.4f || Near(spacing, p, 3.6f))
                    {
                        continue;
                    }

                    Vector2 down = -gradient.normalized;
                    float top = L2Layout.TerrainHeight(p - down * 1.5f);
                    float bottom = L2Layout.TerrainHeight(p + down * 1.5f);
                    float drop = top - bottom;
                    if (drop < 1.3f)
                    {
                        continue;
                    }

                    spacing.Add(p);
                    float yaw = Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg + 90f;
                    var wall = L2Build.Spawn("K/Buildings/SM_Bld_Rockwall_Straight_01", group, Vector3.zero, Vector3.zero);
                    wall.transform.SetPositionAndRotation(new Vector3(p.x + down.x * 0.4f, bottom - 0.3f, p.y + down.y * 0.4f), Quaternion.Euler(0f, yaw, 0f));
                    wall.transform.localScale = new Vector3(1f, (drop + 0.2f) / 2.7f, 1f);
                    Charred(wall, 0.75f, soot);   // smoke-stained stone, never a pale slab under the moon
                    Finish(wall, L1Build.EnvironmentStatic, false);
                    n++;
                }
            }

            return $"retaining walls {n}";
        }

        // ---------------------------------------------------------------- Church stair

        // The way down from the churchyard into E4: a straight flight of worn stone steps cut into the terrace,
        // a stepped stone wall either side, a brazier burning at the top so the stair reads from the path above.
        // The steps are dressing on the route's own ramp (the walking surface); each tread sits on it.
        private static string ChurchStair(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "ChurchStair", true);
            Vector2 top = L2Layout.StairTop, foot = L2Layout.StairFoot;
            float hTop = L2Layout.OnRoute("StairTop").y, hFoot = L2Layout.OnRoute("StairFoot").y;
            Vector2 dir = (foot - top).normalized;
            Vector2 side = new Vector2(dir.y, -dir.x);
            float length = (foot - top).magnitude;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            float HeightAt(float a) => Mathf.Lerp(hTop, hFoot, Mathf.Clamp01(a / length));
            var rng = new System.Random(Seed + 21);
            int n = 0;

            const int steps = 15;
            float tread = length / steps, rise = (hTop - hFoot) / steps;
            float width = L2Layout.StairHalfWidth * 2f + 0.2f;
            for (int k = 0; k < steps; k++)
            {
                float a = (k + 0.5f) * tread;
                float surface = hTop - rise * (k + 0.5f) + 0.06f;
                Vector2 c = top + dir * a + side * ((float)rng.NextDouble() - 0.5f) * 0.08f;
                // A tread of two or three worn, chamfered stone blocks (L2PolyKit), set a little askew.
                var step = new GameObject("Step");
                step.transform.SetParent(group, false);
                step.transform.SetPositionAndRotation(new Vector3(c.x, surface - 0.3f, c.y), Quaternion.Euler(0f, yaw + ((float)rng.NextDouble() - 0.5f) * 1.5f, 0f));
                var size = new Vector3(width + ((float)rng.NextDouble() - 0.5f) * 0.12f, 0.6f, tread + 0.06f);
                step.AddComponent<MeshFilter>().sharedMesh = L2PolyKit.Save(L2PolyKit.Tread(size, Seed + 300 + k), "L2_StairTread_" + k);
                var stepRenderer = step.AddComponent<MeshRenderer>();
                stepRenderer.sharedMaterial = L2PolyKit.Stone;
                Finish(step, L1Build.EnvironmentStatic, false);
                n++;
            }

            // The cheek walls: three dry-stone sections a side, stepping down with the flight, their tops a little
            // above the treads (a low parapet, so the camera still sees down the stair).
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i < 3; i++)
                {
                    float a0 = -0.4f + i * 2.4f, a1 = a0 + 2.6f;
                    float topY = HeightAt(a0) + 0.45f, baseY = HeightAt(a1) - 0.5f;
                    Vector2 c = top + dir * (a0 + a1) * 0.5f + side * s * (L2Layout.StairHalfWidth + 0.85f);
                    var wall = L2Build.Spawn("K/Buildings/SM_Bld_Rockwall_Straight_01", group, Vector3.zero, Vector3.zero, 1f, "CheekWall");
                    wall.transform.SetPositionAndRotation(new Vector3(c.x, baseY, c.y), Quaternion.Euler(0f, yaw + ((float)rng.NextDouble() - 0.5f) * 3f, 0f));
                    wall.transform.localScale = new Vector3(1f, (topY - baseY) / 2.7f, (a1 - a0) / 4f);
                    L2Village.Burnt(wall);
                    Charred(wall, 0.3f, rng);
                    Finish(wall, L1Build.EnvironmentStatic, false);
                    n++;
                }
            }

            // A brazier on the plateau at the top of the flight, burning low: the stair reads from the path above.
            Vector2 at = top - dir * 0.4f + side * (L2Layout.StairHalfWidth + 0.9f);
            var brazier = L2Build.Spawn("K/Props/SM_Prop_Brazier_01", group, Vector3.zero, Vector3.zero, 1f, "Brazier");
            brazier.transform.position = new Vector3(at.x, L2Layout.TerrainHeight(at), at.y);
            Finish(brazier, L1Build.PropStatic, false);
            L2Fire.Fire(brazier.transform, "Fire", Vector3.up * 0.95f, 0.35f, 16f, 9f, false);
            n++;

            return $"church stair ({n} pieces)";
        }

        // ---------------------------------------------------------------- The shrine's clearing

        // The checkpoint shrine stands in a small clearing: whatever any pass put within reach of it (a house
        // corner, a crate, a bush, a wall) is taken away again, so nothing ever cuts into the shrine.
        private static string ClearShrine(Transform root)
        {
            Vector2 shrine = L2Layout.ShrinePosition;
            var dressing = root.Find("SetDressing");
            int removed = 0;
            var doomed = new List<GameObject>();
            foreach (Transform group in dressing)
            {
                if (group.name == "Lots" || group.name == "Blockers")
                {
                    continue;
                }

                foreach (Transform item in group)
                {
                    bool hit = false;
                    foreach (var r in item.GetComponentsInChildren<Renderer>())
                    {
                        if (r is ParticleSystemRenderer)
                        {
                            continue;
                        }

                        var b = r.bounds;
                        float dx = Mathf.Max(b.min.x - shrine.x, 0f, shrine.x - b.max.x);
                        float dz = Mathf.Max(b.min.z - shrine.y, 0f, shrine.y - b.max.z);
                        if (dx * dx + dz * dz < 1.9f * 1.9f)
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (hit)
                    {
                        doomed.Add(item.gameObject);
                    }
                }
            }

            foreach (var go in doomed)
            {
                Object.DestroyImmediate(go);
                removed++;
            }

            return $"shrine clearing ({removed} removed)";
        }

        // ---------------------------------------------------------------- Graveyard

        // The churchyard the detour winds through: leaning headstones in loose rows, a broken low wall along the
        // plateau's edge, a few dead trees. All knee-high near the path (the camera looks over them).
        private static string Graveyard(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Graveyard", true);
            var rng = new System.Random(Seed + 5);
            string[] stones = { "K/Props/SM_Prop_Gravestone_01", "K/Props/SM_Prop_Gravestone_02", "K/Props/SM_Prop_Gravestone_02" };
            int nStones = 0, nWalls = 0, nTrees = 0;
            var taken = new List<Vector2>();

            // Headstones on the plateau around the path, in rough rows facing east (toward the church door).
            for (float x = -34f; x < 16f; x += 2.1f)
            {
                for (float z = -29f; z < 6f; z += 2.6f)
                {
                    var p = new Vector2(x + ((float)rng.NextDouble() - 0.5f) * 0.8f, z + ((float)rng.NextDouble() - 0.5f) * 0.8f);
                    float sd = L2Layout.SignedDistance(p);
                    bool onPlateau = L2Layout.Height(p) > 3.5f && L2Layout.TerrainHeight(p) > 3.5f;
                    if (!onPlateau || sd < 0.6f || sd > 7f || L2Layout.ChurchNave.Contains(p) || (p - L2Layout.ChurchTower).magnitude < 3f || rng.NextDouble() < 0.35 || Reserved(p))
                    {
                        continue;
                    }

                    var go = L2Build.Spawn(stones[rng.Next(stones.Length)], group, Vector3.zero,
                        new Vector3(((float)rng.NextDouble() - 0.5f) * 18f, 90f + ((float)rng.NextDouble() - 0.5f) * 20f, ((float)rng.NextDouble() - 0.5f) * 14f));
                    go.transform.position = Ground(p) + Vector3.down * 0.05f;
                    if (!FitsUnderCamera(go))
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }

                    Finish(go, L1Build.PropStatic, false);
                    taken.Add(p);
                    nStones++;
                }
            }

            // A broken churchyard wall along the south edge of the plateau (above the retaining wall).
            for (float x = -32f; x < 14f; x += 4.3f)
            {
                if (rng.NextDouble() < 0.3)
                {
                    continue; // breaches
                }

                var p = new Vector2(x, -29.4f);
                if (L2Layout.SignedDistance(p) < 0.8f || L2Layout.TerrainHeight(p) < 3.5f)
                {
                    continue;
                }

                var go = L2Build.Spawn("A/Buildings/SM_Bld_Wall_01", group, Vector3.zero, new Vector3(0f, ((float)rng.NextDouble() - 0.5f) * 8f, ((float)rng.NextDouble() - 0.5f) * 6f));
                go.transform.position = Ground(p);
                Finish(go, L1Build.PropStatic, false);
                nWalls++;
            }

            // Dead trees on the far side of the churchyard.
            string[] trees = { "A/Environments/SM_Env_TreeDead_01", "A/Environments/SM_Env_TreeDead_02", "K/Environments/SM_Env_Tree_Twisted_02" };
            for (int i = 0; i < 400 && nTrees < 9; i++)
            {
                var p = new Vector2(-36f + (float)rng.NextDouble() * 52f, -30f + (float)rng.NextDouble() * 36f);
                if (L2Layout.TerrainHeight(p) < 3.5f || L2Layout.SignedDistance(p) < 2f || L2Layout.ChurchNave.Contains(p) || Near(taken, p, 2.5f))
                {
                    continue;
                }

                var go = L2Build.Spawn(trees[rng.Next(trees.Length)], group, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 0.8f + (float)rng.NextDouble() * 0.3f);
                go.transform.position = Ground(p);
                if (!FitsUnderCamera(go))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                Finish(go, L1Build.EnvironmentStatic, false);
                taken.Add(p);
                nTrees++;
            }

            return $"graveyard: {nStones} headstones, {nWalls} wall pieces, {nTrees} trees";
        }

        // ---------------------------------------------------------------- Street clutter

        private static readonly (string key, float min, float max, float weight, bool solid)[] Clutter =
        {
            ("A/Props/SM_Prop_Barrel_01", 0.9f, 1.05f, 1.2f, true),
            ("A/Props/SM_Prop_Crate_01", 0.9f, 1.1f, 1f, true),
            ("A/Props/SM_Prop_Crate_02", 0.9f, 1.1f, 0.8f, true),
            ("A/Props/SM_Prop_Sack_01", 0.9f, 1.1f, 1f, false),
            ("A/Props/SM_Prop_Sack_03", 0.9f, 1.1f, 0.8f, false),
            ("A/Props/SM_Prop_Basket_02", 0.9f, 1.2f, 0.6f, false),
            ("A/Props/SM_Prop_Pot_03", 0.9f, 1.2f, 0.6f, false),
            ("A/Props/SM_Prop_Loghalf_01", 0.8f, 1.1f, 1f, false),
            ("A/Props/SM_Prop_Logpile_01", 0.8f, 1f, 0.4f, true),
            ("K/Props/SM_Prop_Beam_01", 1f, 1.3f, 0.35f, false),
            ("K/Props/SM_Prop_CartWheel_01", 0.9f, 1.1f, 0.5f, false),
            ("A/Environments/SM_Env_Rock_05", 0.25f, 0.4f, 0.4f, false),
            ("A/Environments/SM_Env_Rock_012", 0.2f, 0.35f, 0.3f, false),
            ("A/Buildings/SM_Bld_Fence_01", 1f, 1f, 1.4f, true),
            ("A/Buildings/SM_Bld_Fence_02", 1f, 1f, 1f, true),
            ("A/Buildings/SM_Bld_FencePost_01", 1f, 1.2f, 0.8f, false),
            ("K/Props/SM_Prop_ShopSign_01", 1f, 1f, 0.3f, false),
            ("A/Props/SM_Prop_Washingline_02", 1f, 1f, 0.3f, false),
        };

        // Clutter crowds the edges of the streets and thins toward their middle; fights keep their floors clear.
        private static string StreetClutter(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Clutter", true);
            var rng = new System.Random(Seed + 2);
            var spacing = new List<Vector2>();
            foreach (var t in root.Find("_Gameplay").GetComponentsInChildren<Transform>())
            {
                spacing.Add(new Vector2(t.position.x, t.position.z));
            }

            float total = 0f;
            foreach (var c in Clutter)
            {
                total += c.weight;
            }

            int placed = 0;
            foreach (var w in L2Layout.Walks)
            {
                float length = (w.B - w.A).magnitude + w.HalfWidth * 6f;
                int tries = Mathf.CeilToInt(length * 0.5f);
                for (int i = 0; i < tries; i++)
                {
                    // A point near the rim of this walk piece.
                    float t = (float)rng.NextDouble();
                    Vector2 axis = Vector2.Lerp(w.A, w.B, t);
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 p = axis + outward * (w.HalfWidth + ((float)rng.NextDouble() * 2.4f - 1.2f));
                    float sd = L2Layout.SignedDistance(p);
                    bool narrow = w.HalfWidth < 3f;
                    if (sd < (narrow ? -0.5f : -1.4f) || sd > 1.8f || L2Layout.IsInArena(p, 1f) || Reserved(p) || Near(spacing, p, 1.5f) || L2Layout.InFarmland(p))
                    {
                        continue;
                    }

                    double pick = rng.NextDouble() * total;
                    var chosen = Clutter[0];
                    foreach (var c in Clutter)
                    {
                        pick -= c.weight;
                        if (pick <= 0)
                        {
                            chosen = c;
                            break;
                        }
                    }

                    // Fences, signs and lines only stand outside the path; nothing solid inside a narrow lane.
                    bool edgeOnly = chosen.key.Contains("Fence") || chosen.key.Contains("Sign") || chosen.key.Contains("Washing");
                    if ((edgeOnly && sd < 0.4f) || (chosen.solid && sd < 0.2f))
                    {
                        continue;
                    }

                    float scale = Mathf.Lerp(chosen.min, chosen.max, (float)rng.NextDouble());
                    Vector2 tangent = w.B == w.A ? new Vector2(-outward.y, outward.x) : (w.B - w.A).normalized;
                    float yaw = edgeOnly ? Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg + 90f + (float)(rng.NextDouble() - 0.5) * 16f : (float)rng.NextDouble() * 360f;
                    Vector3 euler = new Vector3(0f, yaw, 0f);
                    if (chosen.key.Contains("Beam"))
                    {
                        euler = new Vector3(88f, yaw, 0f);
                    }
                    else if (chosen.key.Contains("CartWheel"))
                    {
                        euler = new Vector3(0f, yaw, 88f);
                    }
                    else if (chosen.key.Contains("Fence") && rng.NextDouble() < 0.35)
                    {
                        euler.z = (float)(rng.NextDouble() < 0.5 ? -1 : 1) * (18f + (float)rng.NextDouble() * 50f);   // knocked over
                    }

                    var go = L2Build.Spawn(chosen.key, group, Vector3.zero, euler, scale);
                    go.transform.position = Ground(p) + (chosen.key.Contains("Beam") ? Vector3.up * 0.12f : Vector3.zero);
                    if (!FitsUnderCamera(go))
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }

                    if (rng.NextDouble() < 0.45 || chosen.key.Contains("Beam") || chosen.key.Contains("Sign"))
                    {
                        Charred(go, 1f, rng);
                    }

                    Finish(go, L1Build.PropStatic, false);   // visual only: nothing in the clutter narrows a street
                    spacing.Add(p);
                    placed++;
                }
            }

            return $"street clutter {placed}";
        }

        // ---------------------------------------------------------------- Ground cover

        // Ash, soot-black stones, pebbles and dead scrub: thick at path edges and in the yards, thin on the paths.
        private static string GroundCover(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "GroundCover", true);
            var rng = new System.Random(Seed + 3);
            var pieces = new (string key, float min, float max, float weight, bool plant)[]
            {
                ("A/Environments/SM_Env_Pebble_01", 1.5f, 3f, 2f, false), ("A/Environments/SM_Env_Pebble_03", 1.5f, 3f, 2f, false),
                ("A/Environments/SM_Env_Pebble_07", 1.5f, 3f, 1.5f, false), ("A/Environments/SM_Env_Rock_014", 0.35f, 0.7f, 0.5f, false),
                ("A/Environments/SM_Env_Rock_016", 0.4f, 0.8f, 0.5f, false), ("A/Environments/SM_Env_Plant_03", 0.7f, 1.1f, 1f, true),
                ("A/Environments/SM_Env_Plant_05", 0.6f, 1f, 1f, true), ("A/Environments/SM_Env_Grass_02", 0.6f, 1f, 1.2f, true),
                ("A/Environments/SM_Env_TreeStump_01", 0.7f, 1.1f, 0.3f, false),
            };

            float total = 0f;
            foreach (var p in pieces)
            {
                total += p.weight;
            }

            var dry = L2Build.CopyMaterial(L1Build.MaterialsPath + "/L1_Vegetation_Dry_A.mat", "L2_Vegetation_Burnt");
            dry.SetColor("_BaseColor", new Color(0.62f, 0.58f, 0.52f));
            EditorUtility.SetDirty(dry);

            int placed = 0;
            var spacing = new List<Vector2>();
            for (int i = 0; i < 26000 && placed < 1400; i++)
            {
                var p = new Vector2(-102f + (float)rng.NextDouble() * 166f, -70f + (float)rng.NextDouble() * 222f);
                float sd = L2Layout.SignedDistance(p);
                if (sd < -3f || sd > 12f || L2Layout.IsInArena(p, -1f) || Reserved(p))
                {
                    continue;
                }

                float edge = Mathf.Exp(-Mathf.Pow((sd - 1.2f) / 4f, 2f));
                float clump = Mathf.SmoothStep(0.2f, 0.8f, Mathf.PerlinNoise(p.x * 0.17f + 50f, p.y * 0.17f + 20f));
                if (rng.NextDouble() > edge * (sd < -1f ? 0.3f : 1f) * (0.3f + clump) || NearCount(spacing, p, 0.7f))
                {
                    continue;
                }

                double pick = rng.NextDouble() * total;
                var chosen = pieces[0];
                foreach (var piece in pieces)
                {
                    pick -= piece.weight;
                    if (pick <= 0)
                    {
                        chosen = piece;
                        break;
                    }
                }

                var go = L2Build.Spawn(chosen.key, group, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), Mathf.Lerp(chosen.min, chosen.max, (float)rng.NextDouble()));
                go.transform.position = Ground(p);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (chosen.plant)
                    {
                        r.sharedMaterial = dry;
                    }
                    else if (rng.NextDouble() < 0.4)
                    {
                        r.sharedMaterial = L2Build.Mat("L2_Knights_Charred");
                    }

                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                Finish(go, L1Build.PropStatic, false);
                spacing.Add(p);
                placed++;
            }

            return $"ground cover {placed}";
        }

        // ---------------------------------------------------------------- Backdrop

        // Beyond the plots: burnt rooftops still standing (intact silhouettes read as a village), dead trees,
        // smoke columns from distant fires, and hills, so the level never shows an edge.
        private static string Backdrop(Transform root)
        {
            var background = L2Build.Group(root, "Background", false);
            var group = L2Build.Group(background, "Village", true);
            var trees = L2Build.Group(background, "DeadTrees", true);
            var smoke = L2Build.Group(background, "Smoke", true);
            var hills = L2Build.Group(background, "Hills", true);
            var rng = new System.Random(Seed + 4);
            string[] houses = { "K/Buildings/SM_Bld_House_Room_01", "K/Buildings/SM_Bld_House_Room_03", "K/Buildings/SM_Bld_House_RoomTop_02", "K/Buildings/SM_Bld_House_RoomTall_03", "K/Buildings/SM_Bld_House_TopRoomSmall_02", "L2_RuinedHouse_D", "L2_RuinedHouse_B" };
            string[] treeKeys = { "A/Environments/SM_Env_TreeDead_01", "A/Environments/SM_Env_TreeDead_02", "K/Environments/SM_Env_Tree_Twisted_01", "K/Environments/SM_Env_Tree_Twisted_02" };

            var taken = new List<Vector2>();
            foreach (var (lot, _) in L2Greybox.ReadLots(root))
            {
                taken.Add(lot.Center);
            }

            int nHouses = 0, nTrees = 0, nSmoke = 0;
            for (int i = 0; i < 9000; i++)
            {
                var p = new Vector2(-110f + (float)rng.NextDouble() * 180f, -80f + (float)rng.NextDouble() * 240f);
                float sd = L2Layout.SignedDistance(p);
                if (sd < 9f || sd > 30f || Reserved(p))
                {
                    continue;
                }

                bool house = sd < 20f && rng.NextDouble() < 0.55;
                if (house)
                {
                    if (Near(taken, p, 7.5f) || L2Layout.MaxPropHeight(p) < 7f)
                    {
                        continue;
                    }

                    var go = L2Build.Spawn(houses[rng.Next(houses.Length)], group, Vector3.zero, new Vector3(0f, rng.Next(4) * 90f + (float)(rng.NextDouble() - 0.5) * 20f, 0f));
                    go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) - 0.2f, p.y);
                    Finish(go, L1Build.EnvironmentStatic, false);
                    taken.Add(p);
                    nHouses++;

                    if (nSmoke < 7 && rng.NextDouble() < 0.12)
                    {
                        L2Ruins.Fire(smoke, "DistantFire_" + nSmoke, go.transform.position + Vector3.up * 0.5f, 1.4f, 0f, 0f, true);
                        nSmoke++;
                    }
                }
                else
                {
                    if (nTrees >= 110 || Near(taken, p, 2.5f) || L2Layout.MaxPropHeight(p) < 7f)
                    {
                        continue;
                    }

                    var go = L2Build.Spawn(treeKeys[rng.Next(treeKeys.Length)], trees, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 0.8f + (float)rng.NextDouble() * 0.5f);
                    go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p), p.y);
                    Finish(go, L1Build.EnvironmentStatic, false);
                    taken.Add(p);
                    nTrees++;
                }

                if (nHouses >= 60 && nTrees >= 110)
                {
                    break;
                }
            }

            int nHills = 0;
            string[] hillKeys = { "A/Environments/SM_Env_Hill_01", "A/Environments/SM_Env_Hill_02", "A/Environments/SM_Env_Hill_03", "A/Environments/SM_Env_Hill_04" };
            for (int i = 0; i < 3000 && nHills < 36; i++)
            {
                var p = new Vector2(-130f + (float)rng.NextDouble() * 230f, -100f + (float)rng.NextDouble() * 285f);
                float sd = L2Layout.SignedDistance(p);
                if (sd < 32f || Near(taken, p, 16f))
                {
                    continue;
                }

                var go = L2Build.Spawn(hillKeys[rng.Next(hillKeys.Length)], hills, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 1.2f + (float)rng.NextDouble() * 1.2f);
                go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) - 0.5f, p.y);
                Finish(go, L1Build.EnvironmentStatic, false);
                taken.Add(p);
                nHills++;
            }

            return $"backdrop: {nHouses} houses, {nTrees} dead trees, {nSmoke} distant fires, {nHills} hills";
        }

        // ---------------------------------------------------------------- Helpers

        // Keep clear: markers' immediate surroundings, the stair, the shrine, the well and the loot spots, the fire beds.
        internal static bool Reserved(Vector2 p)
        {
            if (L2Layout.InCreek(p, 0.2f) || L2Layout.OnBridge(p, 1f))
            {
                return true;
            }

            foreach (var kv in L2Layout.Markers)
            {
                if ((p - kv.Value).sqrMagnitude < 2.2f * 2.2f)
                {
                    return true;
                }
            }

            foreach (var z in L2Layout.FireZones)
            {
                if ((p - z.Center).magnitude < Mathf.Max(z.Size.x, z.Size.y) * 0.6f + 0.6f)
                {
                    return true;
                }
            }

            foreach (var z in L2Layout.BackFires)
            {
                if ((p - z.Center).magnitude < Mathf.Max(z.Size.x, z.Size.y) * 0.6f + 0.6f)
                {
                    return true;
                }
            }

            foreach (var b in L2Layout.Blockers)
            {
                if ((p - b.Center).magnitude < Mathf.Max(b.Size.x, b.Size.y) * 0.6f + 0.6f)
                {
                    return true;
                }
            }

            return L2Layout.InStair(p, 1.4f) || (p - L2Layout.ShrinePosition).magnitude < 3.2f;
        }

        private static Vector3 Ground(Vector2 p)
        {
            return L2Build.Ground(p);
        }

        public static bool FitsUnderCamera(GameObject go)
        {
            float top = L2Build.Top(go);
            var p = new Vector2(go.transform.position.x, go.transform.position.z);
            float cap = L2Layout.MaxPropHeight(p);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                foreach (var c in new[] { new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z), new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.max.z) })
                {
                    cap = Mathf.Min(cap, L2Layout.MaxPropHeight(c) + (L2Layout.TerrainHeight(c) - go.transform.position.y));
                }
            }

            return top - go.transform.position.y <= cap;
        }

        internal static void Charred(GameObject go, float chance, System.Random rng)
        {
            if (rng.NextDouble() > chance)
            {
                return;
            }

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = L2Build.Mat("L2_Knights_Charred");
                }

                r.sharedMaterials = mats;
            }
        }

        internal static void Finish(GameObject go, StaticEditorFlags flags, bool solid = false)
        {
            if (!solid)
            {
                L1Build.StripColliders(go);
            }

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is ParticleSystemRenderer))
                {
                    r.shadowCastingMode = r.bounds.size.y < 0.5f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }

            L1Build.SetLayer(go, "Obstacles");
            if (flags != 0)
            {
                L1Build.SetStatic(go, flags);
            }
        }

        internal static bool Near(List<Vector2> points, Vector2 p, float radius)
        {
            foreach (var q in points)
            {
                if ((q - p).sqrMagnitude < radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        // Ground cover: a coarse grid check is enough (cheap for thousands of points).
        private static bool NearCount(List<Vector2> points, Vector2 p, float radius)
        {
            for (int i = points.Count - 1; i >= 0 && i >= points.Count - 400; i--)
            {
                if ((points[i] - p).sqrMagnitude < radius * radius)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
