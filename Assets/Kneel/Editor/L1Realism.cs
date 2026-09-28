using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Realism pass for L1: the battlefield should read as a real place, not a level.
    //  - Encounter spaces keep their room but stop announcing themselves (no clean floor, no lit disc,
    //    no symmetric gates) and are dressed like the ground around them.
    //  - Corridors fill with the fallen, dropped weapons, ground cover and trees.
    //  - The hand-built walls of crates and palisades along the edges give way to a natural fringe of brush,
    //    rock, mounds and dead forest that thins out into the land beyond.
    // Everything is seeded and re-runnable; it rebuilds only its own groups.
    public static class L1Realism
    {
        private const int Seed = 1337;

        [MenuItem("Kneel/L1/Realism/Apply Realism Pass")]
        public static void ApplyMenu()
        {
            Debug.Log("[L1] " + Apply());
        }

        public static string Apply()
        {
            var root = L1LevelTools.Root.transform;
            var log = new List<string>();
            var rng = new System.Random(Seed);

            // Clear everything this pass owns first, so a re-run never places around its own old pieces.
            foreach (var path in new[] { "SetDressing/Fallen", "SetDressing/DroppedArms", "SetDressing/GroundCover", "SetDressing/Grass", "SetDressing/PathTrees", "Boundary/NaturalFringe", "_Lighting/LightBreaks" })
            {
                var old = root.Find(path);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            Physics.SyncTransforms();
            log.Add(RemoveGates(root));
            log.Add(ThinBorderWalls(root, rng));

            var spacing = new SpacingGrid(1f);
            foreach (var group in new[] { "SetDressing/Corpses", "SetDressing/Debris", "SetDressing/Monsters", "SetDressing/Standards", "Landmarks" })
            {
                var t = root.Find(group);
                if (t != null)
                {
                    foreach (Transform child in t)
                    {
                        spacing.Add(child.position, 1.2f);
                    }
                }
            }

            log.Add(Fallen(root, rng, spacing));
            log.Add(Weapons(root, rng, spacing));
            log.Add(GroundCover(root, rng));
            log.Add(Grass(root, rng));
            log.Add(Trees(root, rng, spacing));
            log.Add(NaturalFringe(root, rng));
            log.Add(LightBreaks(root, rng));

            L1LevelTools.ApplyLayers();
            log.Add(L1LevelTools.Optimize());
            log.Add(Wind(root));
            L1Look.RepaintGround();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return "Realism pass: " + string.Join(" | ", log);
        }

        // ---------------------------------------------------------------- Encounter spaces

        // The symmetric "arena gates" (shield thresholds, lintels, paired pylons and torches) mark every fight
        // in advance. Keep one fire of each pair for wayfinding and drop the rest.
        private static string RemoveGates(Transform root)
        {
            int removed = 0;
            foreach (var group in new[] { "SetDressing/Gates", "SetDressing/Torches" })
            {
                var t = root.Find(group);
                if (t == null)
                {
                    continue;
                }

                var doomed = new List<GameObject>();
                foreach (Transform child in t)
                {
                    if (child.name.Contains("Threshold") || child.name.Contains("BrokenLintel") || child.name.EndsWith("ExitPylon_R") || child.name.EndsWith("EntryTorch_R"))
                    {
                        doomed.Add(child.gameObject);
                    }
                }

                foreach (var go in doomed)
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }

            return $"gates removed {removed}";
        }

        // ---------------------------------------------------------------- Border walls

        // The band of crates, barrels, log piles and palisade sections reads as a built wall. Keep a fraction
        // (a battlefield has supply wreckage), and let the natural fringe take over the edge.
        private static string ThinBorderWalls(Transform root, System.Random rng)
        {
            var keepFraction = new Dictionary<string, float>
            {
                ["SM_Prop_Crate"] = 0.15f,
                ["SM_Prop_Barrel"] = 0.3f,
                ["SM_Prop_Logpile"] = 0.35f,
                ["SM_Prop_Loghalf"] = 0.5f,
                ["L1_PalisadeBroken"] = 0.4f,
            };

            // Only once: a marker records that the walls were already thinned.
            var band = root.Find("Boundary/Wreckage_Band");
            if (band == null || band.Find("_Thinned") != null)
            {
                return "wall pieces already thinned";
            }

            new GameObject("_Thinned").transform.SetParent(band, false);
            int removed = 0;
            foreach (var group in new[] { "Boundary/Wreckage_Band", "Boundary/Wreckage_Outer" })
            {
                var t = root.Find(group);
                if (t == null)
                {
                    continue;
                }

                var doomed = new List<GameObject>();
                foreach (Transform child in t)
                {
                    foreach (var kv in keepFraction)
                    {
                        if (child.name.StartsWith(kv.Key) && rng.NextDouble() > kv.Value && !doomed.Contains(child.gameObject))
                        {
                            doomed.Add(child.gameObject);
                        }
                    }
                }

                foreach (var go in doomed)
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }

            return $"wall pieces removed {removed}";
        }

        // ---------------------------------------------------------------- The fallen

        // More bodies across the walkable ground, arenas included (fewer there, never piled on the centre).
        private static string Fallen(Transform root, System.Random rng, SpacingGrid spacing)
        {
            var group = L1Build.Group(root.Find("SetDressing"), "Fallen", true);
            var bodies = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab L1_Corpse_", new[] { L1Build.PrefabsPath + "/Corpses" }))
            {
                bodies.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            int placed = 0;
            foreach (var p in Samples(rng, 5200))
            {
                float sd = L1Layout.SignedDistance(p);
                if (sd > 0.8f || Reserved(p))
                {
                    continue;
                }

                float density = InArena(p, out float r01) ? (r01 < 0.45f ? 0.18f : 0.4f) : 0.8f;
                if (rng.NextDouble() > density || !spacing.Free(p, 1.7f))
                {
                    continue;
                }

                // The Vanguard lost this battle: more of them than the enemy.
                var prefab = bodies[rng.Next(bodies.Count)];

                // Upright bodies (kneeling, slumped) would read as standing enemies mid-fight.
                if (InArena(p, out _) && (prefab.name.Contains("Kneeling") || prefab.name.Contains("Slumped")))
                {
                    continue;
                }
                if (prefab.name.Contains("Enemy") && rng.NextDouble() < 0.3)
                {
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                go.transform.position = Ground(p);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                Finish(go, false, L1Build.PropStatic);
                spacing.Add(p, 1.7f);
                placed++;
                if (placed >= 170)
                {
                    break;
                }
            }

            return $"fallen {placed}";
        }

        // Dropped weapons and shields on the ground; blades and polearms planted upright off the fighting line.
        private static string Weapons(Transform root, System.Random rng, SpacingGrid spacing)
        {
            var group = L1Build.Group(root.Find("SetDressing"), "DroppedArms", true);
            string[] flat = { "K/Weapons/SM_Wep_Shield_01", "K/Weapons/SM_Wep_Shield_03", "K/Weapons/SM_Wep_Broadsword_01", "A/Weapons/SM_Wep_Sword_01", "K/Weapons/SM_Wep_Zweihander_01", "K/Weapons/SM_Wep_Halberd_01" };
            string[] planted = { "K/Weapons/SM_Wep_Broadsword_01", "A/Weapons/SM_Wep_Sword_01", "K/Weapons/SM_Wep_Zweihander_01", "K/Weapons/SM_Wep_Halberd_01" };
            int nFlat = 0, nPlanted = 0;
            foreach (var p in Samples(rng, 5000))
            {
                float sd = L1Layout.SignedDistance(p);
                if (sd > 1.5f || Reserved(p) || !spacing.Free(p, 1f))
                {
                    continue;
                }

                bool arena = InArena(p, out float r01);
                bool upright = !arena && sd > -2f && rng.NextDouble() < 0.3;
                if (upright)
                {
                    if (nPlanted >= 45)
                    {
                        continue;
                    }

                    var key = planted[rng.Next(planted.Length)];
                    var go = L1Build.Spawn(key, group, Vector3.zero, Vector3.zero);
                    // Weapons point along +Y from the grip: tip down into the mud, leaning.
                    go.transform.rotation = Quaternion.Euler(180f + ((float)rng.NextDouble() - 0.5f) * 30f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 30f);
                    go.transform.position = Ground(p) + Vector3.up * (key.Contains("Halberd") ? 1.9f : 0.9f);
                    if (!FitsUnderCamera(go, p))
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }

                    Finish(go, false, L1Build.PropStatic);
                    nPlanted++;
                }
                else
                {
                    if (nFlat >= 150 || (arena && rng.NextDouble() < 0.5))
                    {
                        continue;
                    }

                    var go = L1Build.Spawn(flat[rng.Next(flat.Length)], group, Vector3.zero, Vector3.zero);
                    go.transform.rotation = Quaternion.Euler(90f + ((float)rng.NextDouble() - 0.5f) * 16f, (float)rng.NextDouble() * 360f, 0f);
                    go.transform.position = Ground(p) + Vector3.up * 0.05f;
                    Finish(go, false, L1Build.PropStatic);
                    nFlat++;
                }

                spacing.Add(p, 1f);
                if (nFlat >= 150 && nPlanted >= 45)
                {
                    break;
                }
            }

            return $"arms {nFlat} dropped, {nPlanted} planted";
        }

        // ---------------------------------------------------------------- Ground cover

        // Dry grass, weeds, pebbles and small stones: thick along the edges, clumped by noise, thin on the
        // trodden middle of the path. Nothing here has a collider.
        private static string GroundCover(Transform root, System.Random rng)
        {
            var group = L1Build.Group(root.Find("SetDressing"), "GroundCover", true);
            var pieces = new (string key, float min, float max, float weight)[]
            {
                ("A/Environments/SM_Env_Plant_01", 0.7f, 1.1f, 1.2f),
                ("A/Environments/SM_Env_Plant_02", 0.7f, 1.1f, 1.2f),
                ("A/Environments/SM_Env_Plant_03", 0.8f, 1.3f, 1.2f),
                ("A/Environments/SM_Env_Plant_04", 0.7f, 1.1f, 1f),
                ("A/Environments/SM_Env_Plant_05", 0.6f, 1f, 1f),
                ("A/Environments/SM_Env_Pebble_01", 1.5f, 3f, 2f),
                ("A/Environments/SM_Env_Pebble_03", 1.5f, 3f, 2f),
                ("A/Environments/SM_Env_Pebble_05", 1.5f, 3f, 2f),
                ("A/Environments/SM_Env_Pebble_07", 1.5f, 3f, 2f),
                ("A/Environments/SM_Env_Rock_01", 1f, 2.2f, 1.5f),
                ("A/Environments/SM_Env_Rock_014", 0.6f, 1.2f, 1f),
                ("A/Environments/SM_Env_Rock_016", 0.8f, 1.6f, 1.2f),
                ("A/Environments/SM_Env_Mushroom_01", 0.5f, 0.9f, 0.3f),
            };

            float total = 0f;
            foreach (var piece in pieces)
            {
                total += piece.weight;
            }

            var spacing = new SpacingGrid(0.6f);
            int placed = 0;
            foreach (var p in Samples(rng, 20000))
            {
                float sd = L1Layout.SignedDistance(p);
                if (sd < -8f || sd > 14f || Reserved(p))
                {
                    continue;
                }

                // Thick at the path's edge, sparse in the trodden middle and far out; clumped by noise.
                float edge = Mathf.Exp(-Mathf.Pow((sd - 1.5f) / 5f, 2f));
                float middle = sd < -2.5f ? 0.3f : 1f;
                float clump = Mathf.SmoothStep(0.2f, 0.8f, Mathf.PerlinNoise(p.x * 0.18f + 50f, p.y * 0.18f + 20f));
                if (rng.NextDouble() > edge * middle * (0.25f + clump) || !spacing.Free(p, 0.6f))
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

                // Grass grows in clumps of small tufts; everything else is placed singly.
                int tufts = chosen.key.Contains("Grass") ? 4 + rng.Next(3) : 1;
                for (int t = 0; t < tufts; t++)
                {
                    var at = t == 0 ? p : p + new Vector2(((float)rng.NextDouble() - 0.5f) * 1.1f, ((float)rng.NextDouble() - 0.5f) * 1.1f);
                    float scale = Mathf.Lerp(chosen.min, chosen.max, (float)rng.NextDouble());
                    var go = L1Build.Spawn(chosen.key, group, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), scale);
                    go.transform.position = Ground(at);
                    if (!chosen.key.Contains("Pebble") && !chosen.key.Contains("Rock"))
                    {
                        Vegetation(go);
                    }

                    Finish(go, false, L1Build.PropStatic);
                }

                spacing.Add(p, 0.6f);
                if (++placed >= 900)
                {
                    break;
                }
            }

            return $"ground cover {placed}";
        }

        // ---------------------------------------------------------------- Grass

        // Small tufts in clumps of two or three, scattered across the whole battlefield in loose patches:
        // thinner on the trodden path, fuller at its edges and out in the fields.
        private static string Grass(Transform root, System.Random rng)
        {
            var group = L1Build.Group(root.Find("SetDressing"), "Grass", true);
            var tufts = new (string key, float min, float max)[]
            {
                ("A/Environments/SM_Env_Grass_01", 0.8f, 1.3f),
                ("A/Environments/SM_Env_Grass_02", 0.8f, 1.3f),
                ("K/Environments/SM_Env_Grass_01", 0.7f, 1.1f),
            };

            var spacing = new SpacingGrid(0.9f);
            int clumps = 0, total = 0;
            foreach (var p in Samples(rng, 30000))
            {
                float sd = L1Layout.SignedDistance(p);
                if (sd < -9f || sd > 24f || Reserved(p))
                {
                    continue;
                }

                float where = sd < -2f ? 0.3f : sd < 5f ? 1f : 0.65f;
                float patch = Mathf.SmoothStep(0.15f, 0.75f, Mathf.PerlinNoise(p.x * 0.12f + 11f, p.y * 0.12f + 71f));
                if (rng.NextDouble() > where * (0.2f + patch) || !spacing.Free(p, 0.9f))
                {
                    continue;
                }

                int count = 2 + rng.Next(2);
                for (int t = 0; t < count; t++)
                {
                    var tuft = tufts[rng.Next(tufts.Length)];
                    var at = t == 0 ? p : p + new Vector2(((float)rng.NextDouble() - 0.5f) * 0.7f, ((float)rng.NextDouble() - 0.5f) * 0.7f);
                    float scale = Mathf.Lerp(tuft.min, tuft.max, (float)rng.NextDouble());
                    var go = L1Build.Spawn(tuft.key, group, Vector3.zero, new Vector3(((float)rng.NextDouble() - 0.5f) * 10f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 10f), scale);
                    go.transform.position = Ground(at);
                    Vegetation(go);
                    Finish(go, false, L1Build.PropStatic);
                    total++;
                }

                spacing.Add(p, 0.9f);
                if (++clumps >= 2200)
                {
                    break;
                }
            }

            return $"grass {clumps} clumps ({total} tufts)";
        }

        // ---------------------------------------------------------------- Wind

        // Only the grass sways (Kneel/Lit Wind); trees, brush and weeds stay on URP Lit and in static batching,
        // which keeps the cost down. Grass leaves static batching, which would merge the tufts and lose the base
        // each one sways from. Also undoes the earlier tree/brush wind if it is still in the scene.
        private static string Wind(Transform root)
        {
            var wind = Shader.Find("Kneel/Lit Wind");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var name in new[] { "L1_Vegetation_Dry_A", "L1_Vegetation_Dry_K" })
            {
                var mat = L1Build.Mat(name);
                if (mat != null && mat.shader != lit)
                {
                    mat.shader = lit;
                    EditorUtility.SetDirty(mat);
                }
            }

            var grassA = WindCopy("L1_Grass_Wind_A", "L1_Vegetation_Dry_A", wind);
            var grassK = WindCopy("L1_Grass_Wind_K", "L1_Vegetation_Dry_K", wind);
            var ashA = L1Build.Mat("L1_Adventure_Ash");
            var ashK = L1Build.Mat("L1_Knights_Ash");
            var grass = root.Find("SetDressing/Grass");

            int swaying = 0, batched = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer)
                {
                    continue;
                }

                bool isGrass = grass != null && r.transform.IsChildOf(grass);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        continue;
                    }

                    string n = mats[i].name;
                    if (isGrass)
                    {
                        mats[i] = n.EndsWith("_K") || n == "L1_Knights_Ash" ? grassK : grassA;
                    }
                    else if (n == "L1_Trees_Wind_A")
                    {
                        mats[i] = ashA;
                    }
                    else if (n == "L1_Trees_Wind_K")
                    {
                        mats[i] = ashK;
                    }
                }

                r.sharedMaterials = mats;
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if (isGrass)
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                    swaying++;
                }
                else if (flags != 0 && (flags & StaticEditorFlags.BatchingStatic) == 0 && r.GetComponentInParent<Animation>() == null)
                {
                    // Back into static batching (anything that was static before the wind took it out).
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.BatchingStatic);
                    batched++;
                }
            }

            foreach (var name in new[] { "L1_Trees_Wind_A", "L1_Trees_Wind_K" })
            {
                AssetDatabase.DeleteAsset(L1Build.MaterialsPath + "/" + name + ".mat");
            }

            AssetDatabase.SaveAssets();
            return $"wind on {swaying} grass tufts only; {batched} renderers back in static batching";
        }

        // Applies the Wind step on its own (no re-placement).
        [MenuItem("Kneel/L1/Realism/Apply Wind Only")]
        public static void WindMenu()
        {
            Debug.Log("[L1] " + Wind(L1LevelTools.Root.transform));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(L1LevelTools.Root.scene);
        }

        private static Material WindCopy(string name, string from, Shader wind)
        {
            var mat = L1Build.Mat(name);
            if (mat == null)
            {
                mat = new Material(L1Build.Mat(from));
                AssetDatabase.CreateAsset(mat, L1Build.MaterialsPath + "/" + name + ".mat");
            }

            mat.shader = wind;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Trees

        // Dead and scorched trees standing at the edges of the path and a little inside it, on the side away
        // from the camera (anything tall on the camera side would hide the player).
        private static string Trees(Transform root, System.Random rng, SpacingGrid spacing)
        {
            var group = L1Build.Group(root.Find("SetDressing"), "PathTrees", true);
            string[] trees =
            {
                "A/Environments/SM_Env_TreeDead_01", "A/Environments/SM_Env_TreeDead_02", "K/Environments/SM_Env_Tree_Twisted_01",
                "K/Environments/SM_Env_Tree_Twisted_02", "A/Environments/SM_Env_TreePine_01", "A/Environments/SM_Env_TreeBirch_01",
            };
            string[] low = { "A/Environments/SM_Env_TreeStump_01", "A/Environments/SM_Env_TreeLog_01", "A/Environments/SM_Env_Bush_03", "A/Environments/SM_Env_Bush_04" };

            int nTrees = 0, nLow = 0;
            foreach (var p in Samples(rng, 6000))
            {
                float sd = L1Layout.SignedDistance(p);
                if (sd < -1.4f || sd > 3.5f || Reserved(p) || InArena(p, out _) || !spacing.Free(p, 2.2f))
                {
                    continue;
                }

                bool tall = rng.NextDouble() < 0.55;
                string key = tall ? trees[rng.Next(trees.Length)] : low[rng.Next(low.Length)];
                float scale = tall ? 0.8f + (float)rng.NextDouble() * 0.45f : 0.8f + (float)rng.NextDouble() * 0.5f;
                var go = L1Build.Spawn(key, group, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), scale);
                go.transform.position = Ground(p);
                if (!FitsUnderCamera(go, p) || !L1Build.IsFree(go.transform.position, 0.6f))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                TrunkCollider(go, tall);
                if (key.Contains("Bush"))
                {
                    Vegetation(go);
                }

                Finish(go, true, L1Build.EnvironmentStatic);
                spacing.Add(p, 2.2f);
                if (tall)
                {
                    nTrees++;
                }
                else
                {
                    nLow++;
                }

                if (nTrees >= 70 && nLow >= 50)
                {
                    break;
                }
            }

            return $"path trees {nTrees}, stumps/logs/bushes {nLow}";
        }

        // ---------------------------------------------------------------- Natural fringe

        // The edge of the playable space: brush and rock close in, mounds and clumps of dead forest behind,
        // thinning into open ground and hills. Clustered by noise so it never reads as a line.
        private static string NaturalFringe(Transform root, System.Random rng)
        {
            var group = L1Build.Group(root.Find("Boundary"), "NaturalFringe", true);
            string[] names = { "brush", "rocks", "mounds", "trees", "stumps/logs", "hills" };
            var bands = new (string[] keys, float sdMin, float sdMax, float sMin, float sMax, int count, float gap, bool solid)[]
            {
                (new[] { "A/Environments/SM_Env_Bush_01", "A/Environments/SM_Env_Bush_02", "A/Environments/SM_Env_Bush_03", "A/Environments/SM_Env_Bush_04" }, 0.4f, 7f, 0.8f, 1.5f, 520, 1.3f, true),
                (new[] { "A/Environments/SM_Env_Rock_05", "A/Environments/SM_Env_Rock_010", "A/Environments/SM_Env_Rock_02", "A/Environments/SM_Env_Rock_03", "A/Environments/SM_Env_Rock_07", "A/Environments/SM_Env_Rock_012", "A/Environments/SM_Env_Rock_015" }, 0.8f, 12f, 0.6f, 1.5f, 230, 2.2f, true),
                (new[] { "A/Environments/SM_Env_GroundMounds_01", "A/Environments/SM_Env_GroundMounds_03", "A/Environments/SM_Env_GroundMounds_05", "A/Environments/SM_Env_GroundMounds_07", "A/Environments/SM_Env_DirtMound_01" }, 3f, 18f, 0.8f, 1.4f, 110, 5f, true),
                (new[] { "A/Environments/SM_Env_TreeDead_01", "A/Environments/SM_Env_TreeDead_02", "K/Environments/SM_Env_Tree_Twisted_01", "K/Environments/SM_Env_Tree_Twisted_02", "A/Environments/SM_Env_TreePine_01", "A/Environments/SM_Env_TreePine_02", "A/Environments/SM_Env_TreePine_03", "A/Environments/SM_Env_TreeBirch_02" }, 2.5f, 26f, 0.8f, 1.35f, 330, 2.6f, true),
                (new[] { "A/Environments/SM_Env_TreeStump_01", "A/Environments/SM_Env_TreeLog_01", "A/Environments/SM_Env_Reeds_01", "A/Environments/SM_Env_Plant_05" }, 1f, 14f, 0.8f, 1.4f, 160, 1.5f, false),
                (new[] { "A/Environments/SM_Env_Hill_01", "A/Environments/SM_Env_Hill_02", "A/Environments/SM_Env_Hill_03", "A/Environments/SM_Env_Hill_04" }, 18f, 45f, 0.9f, 1.6f, 28, 12f, false),
            };

            var spacing = new SpacingGrid(1f);
            var counts = new StringBuilder();
            for (int b = 0; b < bands.Length; b++)
            {
                var band = bands[b];
                int placed = 0;
                float noiseOffset = (float)rng.NextDouble() * 100f;
                foreach (var p in Samples(rng, band.count * 30))
                {
                    float sd = L1Layout.SignedDistance(p);
                    if (sd < band.sdMin || sd > band.sdMax || Reserved(p))
                    {
                        continue;
                    }

                    // Clusters and clearings: the fringe is thick in places, broken in others.
                    float clump = Mathf.PerlinNoise(p.x * 0.09f + noiseOffset, p.y * 0.09f - noiseOffset);
                    float falloff = 1f - Mathf.InverseLerp(band.sdMin, band.sdMax, sd) * 0.6f;
                    if (rng.NextDouble() > Mathf.SmoothStep(0.25f, 0.75f, clump) * falloff || !spacing.Free(p, band.gap))
                    {
                        continue;
                    }

                    string key = band.keys[rng.Next(band.keys.Length)];
                    float scale = Mathf.Lerp(band.sMin, band.sMax, (float)rng.NextDouble());
                    var go = L1Build.Spawn(key, group, Vector3.zero, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), scale);
                    go.transform.position = Ground(p) + Vector3.down * (key.Contains("Hill") || key.Contains("Mound") ? 0.3f : 0.05f);
                    if (!FitsUnderCamera(go, p))
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }

                    if (key.Contains("Tree"))
                    {
                        TrunkCollider(go, true);
                    }

                    if (key.Contains("Bush") || key.Contains("Reeds") || key.Contains("Plant"))
                    {
                        Vegetation(go);
                    }

                    Finish(go, band.solid, L1Build.EnvironmentStatic);
                    spacing.Add(p, band.gap);
                    if (++placed >= band.count)
                    {
                        break;
                    }
                }

                counts.Append(names[b]).Append(' ').Append(placed).Append(", ");
            }

            return "fringe " + counts;
        }

        // ---------------------------------------------------------------- Light breaks

        // The encounter lights become irregular breaks in the smoke (a cookie instead of a hard disc), and the
        // same breaks fall on the corridors too, so a pool of light no longer means "fight here".
        private static string LightBreaks(Transform root, System.Random rng)
        {
            var cookie = BreakCookie();
            var pools = root.Find("_Lighting/ArenaPools");
            float poolIntensity = 0f;
            if (pools != null)
            {
                foreach (var light in pools.GetComponentsInChildren<Light>())
                {
                    // Once only: the cookie spreads the light, so the break is widened and brightened to match.
                    if (light.cookie != cookie)
                    {
                        light.cookie = cookie;
                        light.spotAngle = Mathf.Min(light.spotAngle * 1.35f, 110f);
                        light.innerSpotAngle = light.spotAngle * 0.2f;
                        light.intensity *= 1.8f;
                        light.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
                    }

                    poolIntensity = Mathf.Max(poolIntensity, light.intensity);
                }
            }

            poolIntensity = poolIntensity > 0f ? poolIntensity : 100f;
            var group = L1Build.Group(root.Find("_Lighting"), "LightBreaks", true);
            int placed = 0;
            foreach (var capsule in L1Layout.Capsules)
            {
                if (capsule.HalfWidth < 4f)
                {
                    continue;
                }

                // One break on most corridor legs, set off the centre line.
                if (rng.NextDouble() < 0.3)
                {
                    continue;
                }

                var p = Vector2.Lerp(capsule.A, capsule.B, 0.3f + (float)rng.NextDouble() * 0.4f);
                if (InArena(p, out _))
                {
                    continue;
                }

                var go = new GameObject("LightBreak_" + placed);
                go.transform.SetParent(group, false);
                go.transform.position = new Vector3(p.x + ((float)rng.NextDouble() - 0.5f) * 4f, 18f, p.y);
                go.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = pools != null && pools.GetComponentInChildren<Light>() != null ? pools.GetComponentInChildren<Light>().color : new Color(0.72f, 0.76f, 0.83f);
                light.intensity = poolIntensity * (0.55f + (float)rng.NextDouble() * 0.25f);
                light.range = 26f;
                light.spotAngle = 60f + (float)rng.NextDouble() * 20f;
                light.innerSpotAngle = light.spotAngle * 0.2f;
                light.cookie = cookie;
                light.shadows = LightShadows.None;
                placed++;
            }

            return $"light breaks {placed} (+ encounter lights softened)";
        }

        // A soft, torn blob: brightest in the middle, ragged at the edge, zero at the borders.
        private static Texture2D BreakCookie()
        {
            string path = "Assets/Kneel/Lighting/L1/Textures/L1_BreakCookie.png";
            if (!File.Exists(path))
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                        float angle = Mathf.Atan2(v, u);
                        float ragged = 0.34f + 0.1f * (Mathf.PerlinNoise(Mathf.Cos(angle) * 1.6f + 7f, Mathf.Sin(angle) * 1.6f + 3f) - 0.5f) * 2f;
                        float d = Mathf.Sqrt(u * u + v * v);
                        float body = 1f - Mathf.SmoothStep(ragged * 0.35f, ragged, d);
                        float grain = Mathf.Lerp(0.7f, 1f, Mathf.PerlinNoise(u * 9f + 20f, v * 9f + 40f));
                        float a = Mathf.Clamp01(body * grain);
                        px[y * size + x] = new Color(a, a, a, a);
                    }
                }

                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Cookie;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- Helpers

        // Uniform candidate points over the ground (the caller filters them).
        private static IEnumerable<Vector2> Samples(System.Random rng, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new Vector2(
                    L1Layout.GroundMinX + (float)rng.NextDouble() * L1Layout.GroundWidth,
                    L1Layout.GroundMinZ + (float)rng.NextDouble() * L1Layout.GroundLength);
            }
        }

        private static bool InArena(Vector2 p, out float r01)
        {
            foreach (var a in L1Layout.Arenas)
            {
                float d = (p - a.Center).magnitude;
                if (d < a.Radius)
                {
                    r01 = d / a.Radius;
                    return true;
                }
            }

            r01 = 1f;
            return false;
        }

        // Keep clear: the ram, the shrine, the start, the exit road, lore spots and gameplay markers.
        private static bool Reserved(Vector2 p)
        {
            if (L1Layout.RamFootprint.Contains(p))
            {
                return true;
            }

            var keep = new (float x, float z, float r)[]
            {
                (0f, 0f, 2f), (0f, 337f, 5f), (4f, 368f, 4f), (7f, 15f, 3f), (-15f, 204f, 3.5f), (-2f, 256f, 3.5f), (7f, 346f, 3f),
                (28f, 133f, 2f), (-7f, 173f, 2f),
            };

            foreach (var k in keep)
            {
                if ((p - new Vector2(k.x, k.z)).sqrMagnitude < k.r * k.r)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 Ground(Vector2 p)
        {
            int ground = 1 << LayerMask.NameToLayer("Ground");
            var from = new Vector3(p.x, 40f, p.y);
            return Physics.Raycast(from, Vector3.down, out var hit, 80f, ground) ? hit.point : new Vector3(p.x, 0f, p.y);
        }

        // True if the piece's top stays under the camera-to-player sight line at this spot.
        private static bool FitsUnderCamera(GameObject go, Vector2 p)
        {
            float top = float.MinValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                top = Mathf.Max(top, r.bounds.max.y);
            }

            return top - Ground(p).y <= L1Layout.MaxPropHeight(p);
        }

        // Dry, dead grass and scrub: the ash palette lifted and warmed a little, so ground cover reads against
        // the mud without turning green.
        private static void Vegetation(GameObject go)
        {
            var adventure = DryMaterial("L1_Vegetation_Dry_A", "L1_Adventure_Ash");
            var knights = DryMaterial("L1_Vegetation_Dry_K", "L1_Knights_Ash");
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].name == "L1_Adventure_Ash")
                    {
                        mats[i] = adventure;
                    }
                    else if (mats[i] != null && mats[i].name == "L1_Knights_Ash")
                    {
                        mats[i] = knights;
                    }
                }

                r.sharedMaterials = mats;
            }
        }

        private static Material DryMaterial(string name, string from)
        {
            var mat = L1Build.Mat(name);
            if (mat == null)
            {
                mat = new Material(L1Build.Mat(from));
                AssetDatabase.CreateAsset(mat, L1Build.MaterialsPath + "/" + name + ".mat");
            }

            mat.SetColor("_BaseColor", new Color(1.28f, 1.22f, 1.0f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Replaces a tree's canopy-sized mesh collider with a trunk capsule, so mouse aim and movement
        // aren't caught by branches.
        private static void TrunkCollider(GameObject go, bool tree)
        {
            if (!tree)
            {
                return;
            }

            L1Build.StripColliders(go);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f / Mathf.Max(go.transform.localScale.x, 0.01f);
            capsule.height = 3f / Mathf.Max(go.transform.localScale.y, 0.01f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
        }

        private static void Finish(GameObject go, bool solid, StaticEditorFlags flags)
        {
            if (!solid)
            {
                L1Build.StripColliders(go);
            }

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = r.bounds.size.y < 0.6f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }

            L1Build.SetLayer(go, "Obstacles");
            L1Build.SetStatic(go, flags);
        }

        // Minimum-distance check over a coarse grid.
        private class SpacingGrid
        {
            private readonly float cell;
            private readonly Dictionary<Vector2Int, List<(Vector2 p, float r)>> cells = new Dictionary<Vector2Int, List<(Vector2, float)>>();

            public SpacingGrid(float cell)
            {
                this.cell = cell;
            }

            public void Add(Vector3 p, float radius)
            {
                Add(new Vector2(p.x, p.z), radius);
            }

            public void Add(Vector2 p, float radius)
            {
                var key = new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
                if (!cells.TryGetValue(key, out var list))
                {
                    cells[key] = list = new List<(Vector2, float)>();
                }

                list.Add((p, radius));
            }

            public bool Free(Vector2 p, float radius)
            {
                int reach = Mathf.CeilToInt((radius + 6f) / cell);
                var c = new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
                for (int x = -reach; x <= reach; x++)
                {
                    for (int y = -reach; y <= reach; y++)
                    {
                        if (!cells.TryGetValue(new Vector2Int(c.x + x, c.y + y), out var list))
                        {
                            continue;
                        }

                        foreach (var item in list)
                        {
                            float min = Mathf.Max(radius, item.r);
                            if ((item.p - p).sqrMagnitude < min * min)
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
        }
    }
}
