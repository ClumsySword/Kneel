using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Scale and character pass: big silhouettes at bends, the fallen giant, the shrine's grace motes,
    // banner sway, and monster carcasses placed where the story wants them.
    public static class L1Scale
    {
        private const string AnimationsPath = "Assets/Kneel/Levels/L1/Animations";

        [MenuItem("Kneel/L1/Scale/Rebuild All")]
        public static void RebuildAllMenu()
        {
            Debug.Log("[L1] " + RebuildAll());
        }

        public static string RebuildAll()
        {
            BuildLadderPrefab();
            string report = PlaceBendSilhouettes() + " " + PlaceGiant() + " " + AddGrace() + " " + AddBannerSway() + " " + PlaceMonsters();
            EditorSceneManager.MarkSceneDirty(L1LevelTools.Root.scene);
            return report;
        }

        // ---------------------------------------------------------------- Siege ladder

        public static void BuildLadderPrefab()
        {
            var ladder = new GameObject("L1_SiegeLadder");
            L1Build.Spawn("K/Props/SM_Prop_Beam_01", ladder.transform, new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, 0f), 1f, "Rail_L").transform.localScale = new Vector3(1.3f, 2.6f, 1.3f);
            L1Build.Spawn("K/Props/SM_Prop_Beam_01", ladder.transform, new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 3f), 1f, "Rail_R_Snapped").transform.localScale = new Vector3(1.3f, 1.9f, 1.3f);
            for (int i = 0; i < 8; i++)
            {
                if (i == 3 || i == 6)
                {
                    continue; // missing rungs
                }

                float y = 0.5f + i * 0.72f;
                if (y > 4.6f)
                {
                    // Above the snapped rail the rungs hang off one side.
                    var hanging = L1Build.Spawn("K/Props/SM_Prop_Beam_01", ladder.transform, new Vector3(-0.5f, y, 0f), new Vector3(0f, 0f, -120f), 1f, "Rung_" + i);
                    hanging.transform.localScale = new Vector3(0.8f, 0.36f, 0.8f);
                    continue;
                }

                var rung = L1Build.Spawn("K/Props/SM_Prop_Beam_01", ladder.transform, new Vector3(-0.5f, y, 0f), new Vector3(0f, 0f, -90f), 1f, "Rung_" + i);
                rung.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            }

            L1Build.SetStatic(ladder, L1Build.EnvironmentStatic);
            L1Build.SavePrefab(ladder, L1Build.PrefabsPath + "/L1_SiegeLadder.prefab");
        }

        // ---------------------------------------------------------------- Bends

        public static string PlaceBendSilhouettes()
        {
            var root = L1LevelTools.Root;
            var group = L1Build.Group(root.transform.Find("Boundary"), "Silhouettes", true);
            Physics.SyncTransforms();
            var rng = new System.Random(21);
            int ladders = 0, tents = 0, clumps = 0;
            var caps = L1Layout.Capsules;

            for (int i = 0; i < caps.Length - 1; i++)
            {
                if ((caps[i].B - caps[i + 1].A).sqrMagnitude > 0.01f || caps[i + 1].HalfWidth < 4f)
                {
                    continue;
                }

                Vector2 d0 = (caps[i].B - caps[i].A).normalized;
                Vector2 d1 = (caps[i + 1].B - caps[i + 1].A).normalized;
                if (Vector2.Angle(d0, d1) < 25f || L1Layout.IsInArena(caps[i].B, 6f))
                {
                    continue;
                }

                // Outside of the turn: the silhouettes frame the corner the player walks around.
                // Inside normals of both legs point into the turn; the outside of the corner is opposite their average.
                float turn = Mathf.Sign(d0.x * d1.y - d0.y * d1.x);
                Vector2 inside = (new Vector2(-d0.y, d0.x) + new Vector2(-d1.y, d1.x)).normalized * turn;
                Vector2 outside = -inside;
                Vector2 bend = caps[i].B;

                if (TryPlace(group, "L1_SiegeLadder", bend + outside * (caps[i].HalfWidth + 2.2f), outside, 6f, 1.2f, rng, out var ladder))
                {
                    // Leaning out, away from the path.
                    float yaw = Mathf.Atan2(outside.x, outside.y) * Mathf.Rad2Deg;
                    ladder.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(28f + (float)rng.NextDouble() * 10f, 0f, 0f);
                    ladders++;
                }

                // The collapsed tent is about 1.7 m tall and 6.5 m across.
                if (TryPlace(group, "L1_TentWreck", bend + outside * (caps[i].HalfWidth + 6f) + d1 * 5f, outside, 1.8f, 2.4f, rng, out _))
                {
                    tents++;
                }

                string[] trees = { "A/Environments/SM_Env_TreeDead_01", "A/Environments/SM_Env_TreeDead_02", "K/Environments/SM_Env_Tree_Twisted_01" };
                Vector2 clumpCentre = bend + outside * (caps[i].HalfWidth + 7f) - d0 * 5f;
                int placed = 0;
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 2.1f + (float)rng.NextDouble();
                    Vector2 p = clumpCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f;
                    if (TryPlace(group, trees[k], p, outside, 6.6f, 1.2f, rng, out var tree))
                    {
                        tree.transform.localScale = Vector3.one * (0.85f + (float)rng.NextDouble() * 0.3f);
                        placed++;
                    }
                }

                if (placed > 0)
                {
                    clumps++;
                }
            }

            L1Build.SetLayer(group.gameObject, "Obstacles");
            return $"Bend silhouettes: {ladders} ladders, {tents} tents, {clumps} tree clumps.";
        }

        // Places a piece at p (nudged outward if needed) when it clears the path, the camera height limit and other dressing.
        private static bool TryPlace(Transform parent, string key, Vector2 p, Vector2 outward, float height, float radius, System.Random rng, out GameObject placed)
        {
            placed = null;
            for (float step = 0f; step <= 6f; step += 0.5f)
            {
                Vector2 c = p + outward * step;
                if (L1Layout.SignedDistance(c) < radius + 0.3f || L1Layout.MaxPropHeight(c) < height || L1Layout.IsInArena(c, 6f))
                {
                    continue;
                }

                if (!L1Build.IsFree(new Vector3(c.x, 0f, c.y), radius))
                {
                    continue;
                }

                placed = L1Build.Spawn(key, parent, new Vector3(c.x, 0f, c.y), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f));
                Physics.SyncTransforms();
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- Giant

        // The colossal fallen knight lies in the outskirts between the ram and E3: seen when the
        // camera zooms out or edge-pans west. Outskirt dressing under it is cleared.
        public static string PlaceGiant()
        {
            var root = L1LevelTools.Root;
            var hero = root.transform.Find("Landmarks/Hero");
            var old = hero.Find("L1_FallenGiant");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var giant = L1Build.Spawn("Monsters/L1_FallenGiant", hero, new Vector3(-29f, 0f, 163f), new Vector3(0f, 58f, 0f), 1f, "L1_FallenGiant");
            var bounds = giant.GetComponent<Renderer>().bounds;

            // It must never encroach on walkable space or rise above the camera line near it.
            int violations = 0;
            for (float x = bounds.min.x; x <= bounds.max.x; x += 1f)
            {
                for (float z = bounds.min.z; z <= bounds.max.z; z += 1f)
                {
                    var p = new Vector2(x, z);
                    if (L1Layout.SignedDistance(p) < 1f)
                    {
                        violations++;
                    }
                }
            }

            int cleared = 0;
            var footprint = new Bounds(bounds.center, new Vector3(bounds.size.x * 0.8f, 100f, bounds.size.z * 0.8f));
            foreach (var groupPath in new[] { "Boundary/Wreckage_Outer", "SetDressing/DeadTrees" })
            {
                var g = root.transform.Find(groupPath);
                var remove = new List<GameObject>();
                foreach (Transform t in g)
                {
                    if (footprint.Contains(new Vector3(t.position.x, bounds.center.y, t.position.z)))
                    {
                        remove.Add(t.gameObject);
                    }
                }

                foreach (var r in remove)
                {
                    Object.DestroyImmediate(r);
                    cleared++;
                }
            }

            L1Build.SetStatic(giant, L1Build.EnvironmentStatic);
            L1Build.SetLayer(giant, "Obstacles");
            return $"Giant placed ({bounds.size.x:F0}x{bounds.size.z:F0} m, {violations} walkable overlaps, {cleared} outskirt pieces cleared).";
        }

        // ---------------------------------------------------------------- Grace

        // Slow golden motes rising around the shrine statue: the one sacred, safe place.
        public static string AddGrace()
        {
            const string path = L1Build.PrefabsPath + "/L1_CheckpointShrine.prefab";
            var shrine = PrefabUtility.LoadPrefabContents(path);
            var old = shrine.transform.Find("Grace");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var grace = new GameObject("Grace").transform;
            grace.SetParent(shrine.transform, false);
            grace.localPosition = new Vector3(0f, 0.6f, 0f);

            var motes = L1Wayfinding.Particles("GraceMotes", grace, GraceMaterial(), 40, 6f, new Vector2(5f, 7f), new Vector2(0.15f, 0.35f),
                new Vector2(0.04f, 0.08f), new Color(1f, 0.86f, 0.5f, 1f), new Color(1f, 0.75f, 0.35f, 0.7f), -0.02f);
            motes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.4f;
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.2f;
            noise.frequency = 0.25f;

            var lightGo = new GameObject("GraceLight");
            lightGo.transform.SetParent(grace, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, -0.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = L1Build.Hex("#FFD98A");
            light.intensity = 0.9f;
            light.range = 3.5f;
            light.shadows = LightShadows.None;

            PrefabUtility.SaveAsPrefabAsset(shrine, path);
            PrefabUtility.UnloadPrefabContents(shrine);
            return "Grace motes on the shrine.";
        }

        private static Material GraceMaterial()
        {
            const string path = L1Build.MaterialsPath + "/FX/L1_FX_Grace.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(L1Build.FxMat("L1_FX_Ember"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.CopyPropertiesFromMaterial(L1Build.FxMat("L1_FX_Ember"));
            mat.SetColor("_BaseColor", L1Build.Hex("#FFD27A") * 2.2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Banner sway

        // Hanging banners sway from their top pivot. Legacy Animation clips of different lengths keep them out of step.
        public static string AddBannerSway()
        {
            var clips = new[] { SwayClip("L1_BannerSway_A", 3.7f, 4f), SwayClip("L1_BannerSway_B", 4.3f, 3f), SwayClip("L1_BannerSway_C", 5.1f, 5f) };
            int n = 0;

            foreach (var prefab in new[] { "L1_BatteringRam", "L1_CollapsedWatchtower", "L1_GatePylon", "L1_Lore_Rearguard" })
            {
                string path = L1Build.PrefabsPath + "/" + prefab + ".prefab";
                var contents = PrefabUtility.LoadPrefabContents(path);
                foreach (var banner in HangingBanners(contents.transform))
                {
                    MakeSway(banner, clips[n++ % clips.Length]);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            // Standing standards placed straight in the scene.
            var standards = L1LevelTools.Root.transform.Find("SetDressing/Standards");
            foreach (var banner in HangingBanners(standards))
            {
                MakeSway(banner, clips[n++ % clips.Length]);
            }

            return $"Banner sway on {n} banners.";
        }

        // Banners that hang from a pole (not the fallen ones lying on the ground).
        private static List<Transform> HangingBanners(Transform root)
        {
            var list = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                bool isBanner = t.name.StartsWith("SM_Prop_Banner") || t.name == "TatteredBanner" || t.name == "Banner_Torn" || t.name == "Rag";
                bool lying = Vector3.Angle(t.up, Vector3.up) > 45f;
                bool alreadySwaying = t.GetComponent<Animation>() != null;
                if (isBanner && !lying && !alreadySwaying && !t.name.StartsWith("Fallen_"))
                {
                    list.Add(t);
                }
            }

            return list;
        }

        // Moves the banner under a pivot that holds its placement, so the clip can rotate it around identity.
        private static void MakeSway(Transform banner, AnimationClip clip)
        {
            var pivot = new GameObject(banner.name + "_Pivot").transform;
            pivot.SetParent(banner.parent, false);
            pivot.localPosition = banner.localPosition;
            pivot.localRotation = banner.localRotation;
            pivot.localScale = banner.localScale;
            pivot.SetSiblingIndex(banner.GetSiblingIndex());
            banner.SetParent(pivot, false);
            banner.localPosition = Vector3.zero;
            banner.localRotation = Quaternion.identity;
            banner.localScale = Vector3.one;

            foreach (var t in banner.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            }

            var animation = banner.gameObject.AddComponent<Animation>();
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.playAutomatically = true;
            animation.wrapMode = WrapMode.Loop;
            animation.cullingType = AnimationCullingType.BasedOnRenderers;
        }

        private static AnimationClip SwayClip(string name, float period, float degrees)
        {
            var x = new AnimationCurve();
            var z = new AnimationCurve();
            for (int i = 0; i <= 16; i++)
            {
                float t = period * i / 16f;
                float phase = t / period * Mathf.PI * 2f;
                x.AddKey(t, Mathf.Sin(phase) * degrees);
                z.AddKey(t, Mathf.Sin(phase * 2f + 1.3f) * degrees * 0.4f);
            }

            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", x);
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", z);
            return L1Build.SaveLegacyClip(AnimationsPath + "/" + name + ".anim", clip);
        }

        // ---------------------------------------------------------------- Monster carcasses

        private struct Carcass
        {
            public string Prefab;
            public Vector2 Near;
            public float MinSd;
            public float MaxSd;
            public Carcass(string prefab, float x, float z, float minSd, float maxSd)
            {
                Prefab = prefab;
                Near = new Vector2(x, z);
                MinSd = minSd;
                MaxSd = maxSd;
            }
        }

        // Where the monsters fell tells the story: they broke the line at the ram, the rearguard
        // held them at the nook, and one Thrall died under the watchtower it pulled down.
        public static string PlaceMonsters()
        {
            var root = L1LevelTools.Root;
            var group = L1Build.Group(root.transform.Find("SetDressing"), "Monsters", true);
            Physics.SyncTransforms();
            var rng = new System.Random(66);

            var plan = new[]
            {
                new Carcass("L1_Monster_Thrall_Sprawled", -9f, 136f, -2.5f, 0.5f),
                new Carcass("L1_Monster_Thrall_Pinned", 9f, 124f, -2.5f, 0.5f),
                new Carcass("L1_Monster_Crawler_Sprawled", -7f, 121f, -2.5f, 0.5f),
                new Carcass("L1_Monster_Crawler_Curled", 10f, 139f, -2.5f, 0.5f),
                new Carcass("L1_Monster_Crawler_Pinned", -11f, 128f, -2f, 0.8f),
                new Carcass("L1_Monster_Thrall_Curled", -13f, 208f, -1.5f, 1.5f),
                new Carcass("L1_Monster_Crawler_Sprawled", -19f, 201f, -1.5f, 1.5f),
                new Carcass("L1_Monster_Crawler_Pinned", -12f, 199f, -2f, 0.5f),
                new Carcass("L1_Monster_Crawler_Curled", 4f, -5f, -1.5f, 1f),
                new Carcass("L1_Monster_Thrall_Pinned", -7f, 344f, 0.3f, 3f),
                new Carcass("L1_Monster_Crawler_Sprawled", 10f, 62f, -1.5f, 1.2f),
                new Carcass("L1_Monster_Crawler_Curled", -8f, 150f, -1.5f, 1.2f),
                new Carcass("L1_Monster_Crawler_Sprawled", 13f, 250f, -1.5f, 1.2f),
                new Carcass("L1_Monster_Crawler_Pinned", -5f, 305f, -1.5f, 1.2f),
            };

            var markers = new List<Vector2>();
            foreach (var t in root.transform.Find("_Gameplay").GetComponentsInChildren<Transform>())
            {
                markers.Add(new Vector2(t.position.x, t.position.z));
            }

            int placed = 0;
            var missed = new List<string>();
            foreach (var c in plan)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L1Build.PrefabsPath + "/Monsters/" + c.Prefab + ".prefab");
                var size = prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                bool done = false;

                for (int attempt = 0; attempt < 400 && !done; attempt++)
                {
                    float r = attempt * 0.02f;
                    var p = c.Near + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 2f * (1f + r * 6f);
                    float sd = L1Layout.SignedDistance(p);
                    if (sd < c.MinSd || sd > c.MaxSd || L1Layout.IsInArena(p, 1.5f) || L1Layout.RamFootprint.Contains(p))
                    {
                        continue;
                    }

                    if (L1Layout.MaxPropHeight(p) < size.y || NearMarker(markers, p, 3f) || !L1Build.IsFree(new Vector3(p.x, 0f, p.y), Mathf.Max(size.x, size.z) * 0.35f))
                    {
                        continue;
                    }

                    // Keep both ram forks and the heal passage passable.
                    if (p.x > 11f && p.y > 126f && p.y < 134f)
                    {
                        continue;
                    }

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                    go.transform.SetPositionAndRotation(new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    Physics.SyncTransforms();
                    placed++;
                    done = true;
                }

                if (!done)
                {
                    missed.Add(c.Prefab + "@" + c.Near);
                }
            }

            L1Build.SetLayer(group.gameObject, "Obstacles");
            return $"Monster carcasses: {placed}/{plan.Length}" + (missed.Count > 0 ? " (missed: " + string.Join(", ", missed) + ")" : "") + ".";
        }

        private static bool NearMarker(List<Vector2> markers, Vector2 p, float radius)
        {
            foreach (var m in markers)
            {
                if ((m - p).magnitude < radius)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
