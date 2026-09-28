using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kneel.EditorTools
{
    // Bakes posed, re-proportioned Synty characters (plus grafted parts) into static meshes:
    // soldier corpses, the two monster types (as carcasses) and the colossal fallen giant.
    public static class L1Bakers
    {
        private const string MeshesPath = "Assets/Kneel/Meshes/L1";
        private const string KnightsCharacters = "Assets/SyntyStudios/PolygonKnights/Prefabs/Characters/";
        private const string AdventureCharacters = "Assets/SyntyStudios/PolygonAdventure/Prefabs/Characters/";

        public delegate void PoseFn(Transform root, float side);

        // (length, girth) multipliers per bone, relative to the original rig. Bones run along local X.
        public class Proportions : Dictionary<string, Vector2> { }

        private struct Part
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public int Group;
        }

        // ---------------------------------------------------------------- Poses

        public static readonly Dictionary<string, (PoseFn pose, Quaternion lie)> Poses = new Dictionary<string, (PoseFn, Quaternion)>
        {
            ["FaceDown_A"] = ((r, s) =>
            {
                var l = new Vector3(s, 0f, 0f); var rt = -l;
                Aim(r, "Shoulder_L", "Elbow_L", l * 0.55f + new Vector3(0f, 0.85f, 0.05f)); Aim(r, "Elbow_L", "Hand_L", l * 0.25f + new Vector3(0f, 1f, 0.05f));
                Aim(r, "Shoulder_R", "Elbow_R", rt * 0.3f + new Vector3(0f, -1f, 0.03f)); Aim(r, "Elbow_R", "Hand_R", rt * 0.05f + new Vector3(0f, -1f, 0.12f));
                Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.22f + new Vector3(0f, -1f, 0f)); Aim(r, "LowerLeg_L", "Ankle_L", l * 0.12f + new Vector3(0f, -1f, -0.05f));
                Aim(r, "UpperLeg_R", "LowerLeg_R", rt * 0.55f + new Vector3(0f, -1f, 0.02f)); Aim(r, "LowerLeg_R", "Ankle_R", rt * 0.1f + new Vector3(0f, -1f, -0.1f));
                Turn(r, "Head", 75f * s);
            }, Quaternion.Euler(90f, 0f, 0f)),
            ["FaceDown_B"] = ((r, s) =>
            {
                var l = new Vector3(s, 0f, 0f); var rt = -l;
                Aim(r, "Shoulder_L", "Elbow_L", l + new Vector3(0f, 0.25f, 0.05f)); Aim(r, "Elbow_L", "Hand_L", l * 0.35f + new Vector3(0f, 1f, 0.05f));
                Aim(r, "Shoulder_R", "Elbow_R", rt + new Vector3(0f, -0.35f, 0.05f)); Aim(r, "Elbow_R", "Hand_R", rt * 0.6f + new Vector3(0f, -1f, 0.05f));
                Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.35f + new Vector3(0f, -1f, 0f)); Aim(r, "LowerLeg_L", "Ankle_L", l * 0.2f + new Vector3(0f, -1f, -0.05f));
                Aim(r, "UpperLeg_R", "LowerLeg_R", rt * 0.15f + new Vector3(0f, -1f, 0f)); Aim(r, "LowerLeg_R", "Ankle_R", new Vector3(0f, -1f, -0.05f));
                Turn(r, "Head", -70f * s);
            }, Quaternion.Euler(90f, 0f, 0f)),
            ["OnSide"] = ((r, s) =>
            {
                var l = new Vector3(s, 0f, 0f);
                Aim(r, "Spine_01", "Spine_02", new Vector3(0f, 1f, 0.3f)); Aim(r, "Neck", "Head", new Vector3(0f, 0.7f, 0.6f));
                Aim(r, "Shoulder_L", "Elbow_L", l * 0.15f + new Vector3(0f, -0.4f, 1f)); Aim(r, "Elbow_L", "Hand_L", new Vector3(-s * 0.2f, 0.3f, 1f));
                Aim(r, "Shoulder_R", "Elbow_R", -l * 0.15f + new Vector3(0f, -0.7f, 0.8f)); Aim(r, "Elbow_R", "Hand_R", new Vector3(s * 0.1f, 0.1f, 1f));
                Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.1f + new Vector3(0f, -0.45f, 1f)); Aim(r, "LowerLeg_L", "Ankle_L", new Vector3(0f, -1f, -0.35f));
                Aim(r, "UpperLeg_R", "LowerLeg_R", -l * 0.1f + new Vector3(0f, -0.75f, 0.7f)); Aim(r, "LowerLeg_R", "Ankle_R", new Vector3(0f, -1f, -0.15f));
            }, Quaternion.Euler(0f, 0f, 90f)),
            ["OnBack"] = ((r, s) =>
            {
                var l = new Vector3(s, 0f, 0f); var rt = -l;
                Aim(r, "Shoulder_L", "Elbow_L", l + new Vector3(0f, -0.35f, 0f)); Aim(r, "Elbow_L", "Hand_L", l * 0.7f + new Vector3(0f, 0.6f, 0f));
                Aim(r, "Shoulder_R", "Elbow_R", rt * 0.45f + new Vector3(0f, -1f, 0f)); Aim(r, "Elbow_R", "Hand_R", rt * 0.3f + new Vector3(0f, -1f, 0.25f));
                Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.3f + new Vector3(0f, -1f, 0f)); Aim(r, "LowerLeg_L", "Ankle_L", l * 0.15f + new Vector3(0f, -1f, 0f));
                Aim(r, "UpperLeg_R", "LowerLeg_R", rt * 0.2f + new Vector3(0f, -1f, 0.35f)); Aim(r, "LowerLeg_R", "Ankle_R", new Vector3(0f, -1f, -0.2f));
                Turn(r, "Head", 45f * s);
            }, Quaternion.Euler(-90f, 0f, 0f)),
            ["Slumped"] = ((r, s) =>
            {
                var l = new Vector3(s, 0f, 0f); var rt = -l;
                Aim(r, "Spine_01", "Spine_02", new Vector3(0.05f, 1f, -0.45f)); Aim(r, "Neck", "Head", new Vector3(0.25f * s, 0.35f, 0.9f));
                Aim(r, "Shoulder_L", "Elbow_L", l * 0.3f + new Vector3(0f, -1f, 0.1f)); Aim(r, "Elbow_L", "Hand_L", l * 0.1f + new Vector3(0f, -0.5f, 0.8f));
                Aim(r, "Shoulder_R", "Elbow_R", rt * 0.4f + new Vector3(0f, -1f, -0.05f)); Aim(r, "Elbow_R", "Hand_R", rt * 0.3f + new Vector3(0f, -1f, 0.2f));
                Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.3f + new Vector3(0f, -0.08f, 1f)); Aim(r, "LowerLeg_L", "Ankle_L", l * 0.15f + new Vector3(0f, -0.3f, 1f));
                Aim(r, "UpperLeg_R", "LowerLeg_R", rt * 0.2f + new Vector3(0f, 0.2f, 1f)); Aim(r, "LowerLeg_R", "Ankle_R", new Vector3(0f, -1f, 0.2f));
            }, Quaternion.identity),
            ["Kneeling"] = ((r, s) =>
            {
                var l = new Vector3(s, 0f, 0f); var rt = -l;
                Aim(r, "Spine_01", "Spine_02", new Vector3(0f, 1f, 0.22f)); Aim(r, "Neck", "Head", new Vector3(0f, 0.5f, 0.85f));
                Aim(r, "Shoulder_L", "Elbow_L", l * 0.2f + new Vector3(0f, -0.75f, 0.65f)); Aim(r, "Elbow_L", "Hand_L", rt * 0.55f + new Vector3(0f, 0.15f, 0.8f));
                Aim(r, "Shoulder_R", "Elbow_R", rt * 0.2f + new Vector3(0f, -0.75f, 0.65f)); Aim(r, "Elbow_R", "Hand_R", l * 0.55f + new Vector3(0f, 0.15f, 0.8f));
                Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.12f + new Vector3(0f, -1f, 0.2f)); Aim(r, "LowerLeg_L", "Ankle_L", new Vector3(0f, -0.08f, -1f));
                Aim(r, "UpperLeg_R", "LowerLeg_R", rt * 0.15f + new Vector3(0f, -0.05f, 1f)); Aim(r, "LowerLeg_R", "Ankle_R", new Vector3(0f, -1f, 0.05f));
            }, Quaternion.identity),
            // Crawler: arched and clawing forward, dead on its belly.
            ["Crawl"] = ((r, s) =>
            {
                var l = new Vector3(s, 0f, 0f); var rt = -l;
                Aim(r, "Spine_01", "Spine_02", new Vector3(0f, 1f, -0.25f)); Aim(r, "Spine_03", "Neck", new Vector3(0f, 1f, 0.35f)); Aim(r, "Neck", "Head", new Vector3(0f, 0.6f, 0.8f));
                Aim(r, "Shoulder_L", "Elbow_L", l * 0.45f + new Vector3(0f, 0.9f, 0.1f)); Aim(r, "Elbow_L", "Hand_L", l * 0.1f + new Vector3(0f, 1f, 0.35f));
                Aim(r, "Shoulder_R", "Elbow_R", rt * 0.7f + new Vector3(0f, 0.6f, 0.1f)); Aim(r, "Elbow_R", "Hand_R", rt * 0.3f + new Vector3(0f, 1f, 0.3f));
                Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.45f + new Vector3(0f, -1f, 0.1f)); Aim(r, "LowerLeg_L", "Ankle_L", l * 0.3f + new Vector3(0f, -0.6f, -0.8f));
                Aim(r, "UpperLeg_R", "LowerLeg_R", rt * 0.3f + new Vector3(0f, -1f, -0.05f)); Aim(r, "LowerLeg_R", "Ankle_R", rt * 0.15f + new Vector3(0f, -1f, -0.1f));
                Turn(r, "Head", 60f * s);
            }, Quaternion.Euler(90f, 0f, 0f)),
            // A grafted victim: arms hanging, head lolled.
            ["Graft"] = ((r, s) =>
            {
                var l = new Vector3(s, 0f, 0f); var rt = -l;
                Aim(r, "Neck", "Head", new Vector3(0.4f * s, 0.5f, 0.75f));
                Aim(r, "Shoulder_L", "Elbow_L", l * 0.5f + new Vector3(0f, -0.6f, 0.6f)); Aim(r, "Elbow_L", "Hand_L", l * 0.2f + new Vector3(0f, -0.9f, 0.4f));
                Aim(r, "Shoulder_R", "Elbow_R", rt * 0.7f + new Vector3(0f, -0.2f, 0.6f)); Aim(r, "Elbow_R", "Hand_R", rt * 0.3f + new Vector3(0f, -1f, 0.2f));
            }, Quaternion.identity),
        };

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private static void Aim(Transform root, string bone, string child, Vector3 dir)
        {
            var b = Find(root, bone);
            var c = Find(root, child);
            b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
        }

        private static void Turn(Transform root, string bone, float degrees)
        {
            var b = Find(root, bone);
            b.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * b.rotation;
        }

        // ---------------------------------------------------------------- Core baking

        // Poses a character in its standing frame (faces +Z, feet at y = 0) and returns the skinned mesh
        // baked into that frame, plus the posed rig for attaching parts. Caller destroys the rig.
        private static Mesh BakeStanding(string prefabPath, string rendererName, PoseFn pose, Proportions proportions, out GameObject rig)
        {
            rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var root = rig.transform;

            if (proportions != null)
            {
                ApplyProportions(Find(root, "Root"), proportions, Vector2.one);
            }

            float side = Mathf.Sign(Find(root, "Shoulder_L").position.x);
            pose?.Invoke(root, side);

            var smr = Find(root, rendererName).GetComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            smr.BakeMesh(mesh, true);

            // SMR space -> standing frame.
            var toRoot = root.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            TransformMesh(mesh, toRoot);
            return mesh;
        }

        // Top-down, so a bone's scale is relative to what it inherits: (length along X, girth on Y/Z).
        private static void ApplyProportions(Transform bone, Proportions proportions, Vector2 inherited)
        {
            Vector2 absolute = proportions.TryGetValue(bone.name, out var p) ? p : inherited;
            bone.localScale = new Vector3(bone.localScale.x * absolute.x / inherited.x, bone.localScale.y * absolute.y / inherited.y, bone.localScale.z * absolute.y / inherited.y);
            foreach (Transform child in bone)
            {
                ApplyProportions(child, proportions, absolute);
            }
        }

        private static void TransformMesh(Mesh mesh, Matrix4x4 m)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = m.MultiplyPoint3x4(v[i]);
                n[i] = m.MultiplyVector(n[i]).normalized;
            }

            mesh.vertices = v;
            mesh.normals = n;
            mesh.RecalculateBounds();
        }

        // Merges parts into one mesh with one submesh per group, lays it down, and grounds it.
        private static Mesh Assemble(List<Part> parts, int groups, Quaternion lie, float sink)
        {
            var perGroup = new List<Mesh>();
            for (int g = 0; g < groups; g++)
            {
                var combine = new List<CombineInstance>();
                foreach (var part in parts)
                {
                    if (part.Group != g)
                    {
                        continue;
                    }

                    for (int s = 0; s < part.Mesh.subMeshCount; s++)
                    {
                        combine.Add(new CombineInstance { mesh = part.Mesh, subMeshIndex = s, transform = part.Matrix });
                    }
                }

                var merged = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                merged.CombineMeshes(combine.ToArray(), true, true);
                perGroup.Add(merged);
            }

            var all = new List<CombineInstance>();
            foreach (var m in perGroup)
            {
                all.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
            }

            var result = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            result.CombineMeshes(all.ToArray(), false, true);
            foreach (var m in perGroup)
            {
                Object.DestroyImmediate(m);
            }

            TransformMesh(result, Matrix4x4.Rotate(lie));
            var b = result.bounds;
            TransformMesh(result, Matrix4x4.Translate(new Vector3(-b.center.x, -b.min.y - sink, -b.center.z)));
            result.RecalculateTangents();
            return result;
        }

        private static Mesh SaveMesh(Mesh mesh, string folder, string name)
        {
            string dir = MeshesPath + "/" + folder;
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder(MeshesPath, folder);
            }

            mesh.name = name;
            string path = dir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static GameObject SaveStaticPrefab(string folder, string name, Mesh mesh, Material[] materials, bool collider)
        {
            string dir = L1Build.PrefabsPath + "/" + folder;
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder(L1Build.PrefabsPath, folder);
            }

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            if (collider)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            GameObjectUtility.SetStaticEditorFlags(go, L1Build.PropStatic);
            return L1Build.SavePrefab(go, dir + "/" + name + ".prefab");
        }

        private static Mesh PropMesh(string key, out Matrix4x4 local)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L1Build.PrefabPath(key));
            var filter = prefab.GetComponentInChildren<MeshFilter>();
            local = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            return filter.sharedMesh;
        }

        // ---------------------------------------------------------------- Soldier corpses

        [MenuItem("Kneel/L1/Bake/Soldier Corpses")]
        public static void BakeCorpses()
        {
            var kael = L1Build.Mat("L1_Regiment_Kael");
            var enemy = L1Build.Mat("L1_Regiment_Enemy");
            foreach (var body in new[] { "Soldier_01", "Soldier_02", "Knight_02" })
            {
                foreach (var pose in new[] { "FaceDown_A", "FaceDown_B", "OnSide", "OnBack", "Slumped", "Kneeling" })
                {
                    var standing = BakeStanding(KnightsCharacters + "Character_" + body + "_Blue.prefab", "Character_" + body, Poses[pose].pose, null, out var rig);
                    Object.DestroyImmediate(rig);
                    var mesh = Assemble(new List<Part> { new Part { Mesh = standing, Matrix = Matrix4x4.identity, Group = 0 } }, 1, Poses[pose].lie, 0.04f);
                    Object.DestroyImmediate(standing);
                    mesh = SaveMesh(mesh, "Corpses", "L1_Corpse_" + body + "_" + pose);
                    SaveStaticPrefab("Corpses", "L1_Corpse_Kael_" + body + "_" + pose, mesh, new[] { kael }, false);
                    SaveStaticPrefab("Corpses", "L1_Corpse_Enemy_" + body + "_" + pose, mesh, new[] { enemy }, false);
                }
            }

            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- Monsters

        private static readonly (string name, string pose, bool pinned)[] CarcassPoses =
        {
            ("Sprawled", "FaceDown_B", false),
            ("Curled", "OnSide", false),
            ("Pinned", "FaceDown_A", true),
        };

        [MenuItem("Kneel/L1/Bake/Monsters + Giant")]
        public static void BakeMonstersMenu()
        {
            Debug.Log("[L1] " + BakeMonsters());
        }

        public static string BakeMonsters()
        {
            string knightTexture = CharacterTexture(KnightsCharacters + "Character_Knight_02_Black.prefab", "Character_Knight_02");
            string warriorTexture = CharacterTexture(AdventureCharacters + "Character_Warrior_Brown.prefab", "Character_Warrior");
            var thrallMat = MonsterMaterial("L1_Monster_Thrall", knightTexture, 0.03f, 0.35f, 0.5f, L1Build.Hex("#3B1D18"));
            var crawlerMat = MonsterMaterial("L1_Monster_Crawler", warriorTexture, 0.2f, 0.14f, 0.72f, L1Build.Hex("#4A1F1A"));
            var kael = L1Build.Mat("L1_Regiment_Kael");
            var weapons = L1Build.Mat("L1_Knights_Ash");
            var bone = L1Build.Mat("L1_Adventure_Ash");
            int made = 0;

            foreach (var carcass in CarcassPoses)
            {
                var thrall = BuildThrall(carcass.pose == "OnSide" ? "OnSide" : carcass.pose, carcass.pinned);
                SaveStaticPrefab("Monsters", "L1_Monster_Thrall_" + carcass.name, SaveMesh(thrall, "Monsters", "L1_Monster_Thrall_" + carcass.name), new[] { thrallMat, kael, weapons }, true);

                var crawler = BuildCrawler(carcass.pose == "FaceDown_B" ? "Crawl" : carcass.pose, carcass.pinned);
                SaveStaticPrefab("Monsters", "L1_Monster_Crawler_" + carcass.name, SaveMesh(crawler, "Monsters", "L1_Monster_Crawler_" + carcass.name), new[] { crawlerMat, bone, weapons }, false);
                made += 2;
            }

            var giant = BuildGiant();
            SaveStaticPrefab("Monsters", "L1_FallenGiant", SaveMesh(giant, "Monsters", "L1_FallenGiant"), new[] { MonsterMaterial("L1_Giant", knightTexture, 0.06f, 0.18f, 0.55f, L1Build.Hex("#2E2622")), weapons }, false);
            AssetDatabase.SaveAssets();
            return $"Baked {made} monster carcasses and the fallen giant.";
        }

        // Ashbound Thrall: a grafted hulk. Swollen chest and arms, shrunken head, two of Kael's
        // soldiers grafted out of its back, blades driven into it.
        private static Mesh BuildThrall(string pose, bool pinned)
        {
            var proportions = new Proportions
            {
                ["Spine_02"] = new Vector2(1.1f, 1.25f), ["Spine_03"] = new Vector2(1.25f, 1.4f),
                ["Clavicle_L"] = new Vector2(1.35f, 1.3f), ["Clavicle_R"] = new Vector2(1.35f, 1.3f),
                ["Shoulder_L"] = new Vector2(1.45f, 1.35f), ["Shoulder_R"] = new Vector2(1.45f, 1.35f),
                ["Elbow_L"] = new Vector2(1.45f, 1.3f), ["Elbow_R"] = new Vector2(1.45f, 1.3f),
                ["Hand_L"] = new Vector2(1.7f, 1.6f), ["Hand_R"] = new Vector2(1.7f, 1.6f),
                ["Neck"] = new Vector2(1.2f, 1.1f), ["Head"] = new Vector2(0.72f, 0.72f),
                ["UpperLeg_L"] = new Vector2(1.05f, 1.3f), ["UpperLeg_R"] = new Vector2(1.05f, 1.3f),
                ["LowerLeg_L"] = new Vector2(1.05f, 1.25f), ["LowerLeg_R"] = new Vector2(1.05f, 1.25f),
            };

            const float scale = 1.35f;
            var body = BakeStanding(KnightsCharacters + "Character_Knight_02_Black.prefab", "Character_Knight_02", Poses[pose].pose, proportions, out var rig);
            var parts = new List<Part> { new Part { Mesh = body, Matrix = Matrix4x4.Scale(Vector3.one * scale), Group = 0 } };
            var root = rig.transform;
            Vector3 upperBack = Find(root, "Spine_03").position * scale;
            Vector3 lowerBack = Find(root, "Spine_01").position * scale;
            Vector3 right = Vector3.right * Mathf.Sign(Find(root, "Shoulder_L").position.x);
            Object.DestroyImmediate(rig);

            // Grafted victims, half sunk into the shoulders, torsos reaching out of the back.
            var graftBody = BakeStanding(KnightsCharacters + "Character_Soldier_01_Blue.prefab", "Character_Soldier_01", Poses["Graft"].pose, null, out var graftRig);
            Object.DestroyImmediate(graftRig);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 outward = (Vector3.back * 0.75f + Vector3.up * 0.55f + right * side * 0.45f).normalized;
                Quaternion orient = Quaternion.FromToRotation(Vector3.up, outward) * Quaternion.Euler(0f, side * 35f, 0f);
                Vector3 anchor = upperBack + right * side * 0.28f * scale + Vector3.back * 0.12f * scale;
                const float graftScale = 0.72f;
                Vector3 origin = anchor - orient * (Vector3.up * 0.95f * graftScale);
                parts.Add(new Part { Mesh = graftBody, Matrix = Matrix4x4.TRS(origin, orient, Vector3.one * graftScale), Group = 1 });
            }

            // Blades driven into its back.
            var rng = new System.Random(pose.GetHashCode() + (pinned ? 7 : 0));
            string[] blades = { "K/Weapons/SM_Wep_Broadsword_01", "K/Weapons/SM_Wep_Zweihander_01", "K/Weapons/SM_Wep_Broadsword_01", "A/Weapons/SM_Wep_Sword_01", "K/Weapons/SM_Wep_Halberd_01" };
            for (int i = 0; i < blades.Length; i++)
            {
                var mesh = PropMesh(blades[i], out var local);
                float t = (float)i / (blades.Length - 1);
                Vector3 anchor = Vector3.Lerp(lowerBack, upperBack, t) + right * ((float)rng.NextDouble() - 0.5f) * 0.5f * scale + Vector3.back * 0.2f * scale;
                Vector3 inward = (Vector3.forward * 0.9f + Vector3.down * 0.25f + right * ((float)rng.NextDouble() - 0.5f) * 0.6f).normalized;
                // Weapons point along +Y from the grip; drive the blade in, leave the hilt out.
                Quaternion orient = Quaternion.FromToRotation(Vector3.up, inward);
                Vector3 grip = anchor - inward * (blades[i].Contains("Halberd") ? 1.1f : 0.45f);
                parts.Add(new Part { Mesh = mesh, Matrix = Matrix4x4.TRS(grip, orient, Vector3.one) * local, Group = 2 });
            }

            if (pinned)
            {
                AddPinningSpears(parts, lowerBack, upperBack, right, 2);
            }

            var result = Assemble(parts, 3, Poses[pose].lie, 0.06f);
            Object.DestroyImmediate(body);
            Object.DestroyImmediate(graftBody);
            return result;
        }

        // Carrion Crawler: an emaciated, stretched ghoul with long clawed limbs and bone spikes down its spine.
        private static Mesh BuildCrawler(string pose, bool pinned)
        {
            var proportions = new Proportions
            {
                ["Spine_01"] = new Vector2(1.2f, 0.72f), ["Spine_02"] = new Vector2(1.2f, 0.7f), ["Spine_03"] = new Vector2(1.15f, 0.78f),
                ["Clavicle_L"] = new Vector2(1.1f, 0.8f), ["Clavicle_R"] = new Vector2(1.1f, 0.8f),
                ["Shoulder_L"] = new Vector2(1.65f, 0.7f), ["Shoulder_R"] = new Vector2(1.65f, 0.7f),
                ["Elbow_L"] = new Vector2(1.7f, 0.65f), ["Elbow_R"] = new Vector2(1.7f, 0.65f),
                ["Hand_L"] = new Vector2(1.9f, 1.25f), ["Hand_R"] = new Vector2(1.9f, 1.25f),
                ["Neck"] = new Vector2(2f, 0.7f), ["Head"] = new Vector2(0.85f, 0.8f),
                ["UpperLeg_L"] = new Vector2(1.3f, 0.7f), ["UpperLeg_R"] = new Vector2(1.3f, 0.7f),
                ["LowerLeg_L"] = new Vector2(1.35f, 0.65f), ["LowerLeg_R"] = new Vector2(1.35f, 0.65f),
            };

            var body = BakeStanding(AdventureCharacters + "Character_Warrior_Brown.prefab", "Character_Warrior", Poses[pose].pose, proportions, out var rig);
            var parts = new List<Part> { new Part { Mesh = body, Matrix = Matrix4x4.identity, Group = 0 } };
            var root = rig.transform;
            var spine = new List<Vector3>();
            foreach (var name in new[] { "Spine_01", "Spine_02", "Spine_03", "Neck" })
            {
                spine.Add(Find(root, name).position);
            }

            Vector3 right = Vector3.right * Mathf.Sign(Find(root, "Shoulder_L").position.x);
            Object.DestroyImmediate(rig);

            // Bone spikes erupting along the spine.
            var spike = PropMesh("A/Environments/SM_Env_Stalagmite_01", out var spikeLocal);
            float spikeHeight = Mathf.Max(0.01f, spike.bounds.size.y);
            var rng = new System.Random(pose.GetHashCode());
            for (int i = 0; i < spine.Count; i++)
            {
                for (int k = 0; k < 2; k++)
                {
                    float length = 0.28f + (float)rng.NextDouble() * 0.25f;
                    float s = length / spikeHeight;
                    Vector3 dir = (Vector3.back + Vector3.up * 0.35f * (k == 0 ? 1f : -0.2f) + right * (k == 0 ? -0.25f : 0.25f)).normalized;
                    Vector3 at = spine[i] + Vector3.back * 0.1f + right * (k == 0 ? -0.05f : 0.05f);
                    parts.Add(new Part { Mesh = spike, Matrix = Matrix4x4.TRS(at, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(s * 0.45f, s, s * 0.45f)) * spikeLocal, Group = 1 });
                }
            }

            if (pinned)
            {
                AddPinningSpears(parts, spine[0], spine[2], right, 2);
            }

            var result = Assemble(parts, 3, Poses[pose].lie, 0.04f);
            Object.DestroyImmediate(body);
            return result;
        }

        // Halberds driven down through the back into the ground (they point down once the body lies face down).
        private static void AddPinningSpears(List<Part> parts, Vector3 lowerBack, Vector3 upperBack, Vector3 right, int count)
        {
            var mesh = PropMesh("K/Weapons/SM_Wep_Halberd_01", out var local);
            for (int i = 0; i < count; i++)
            {
                Vector3 anchor = Vector3.Lerp(lowerBack, upperBack, 0.3f + 0.4f * i) + right * (i == 0 ? 0.08f : -0.1f);
                Vector3 inward = (Vector3.forward + right * (i == 0 ? 0.15f : -0.2f) + Vector3.up * 0.1f).normalized;
                Vector3 grip = anchor - inward * 1.35f;
                parts.Add(new Part { Mesh = mesh, Matrix = Matrix4x4.TRS(grip, Quaternion.FromToRotation(Vector3.up, inward), Vector3.one) * local, Group = 2 });
            }
        }

        // A colossal knight, face down, with siege spears standing out of its back.
        private static Mesh BuildGiant()
        {
            const float scale = 9f;
            var body = BakeStanding(KnightsCharacters + "Character_Knight_01_Black.prefab", "Character_Knight_01", Poses["FaceDown_B"].pose, null, out var rig);
            var root = rig.transform;
            Vector3 lowerBack = Find(root, "Spine_01").position * scale;
            Vector3 upperBack = Find(root, "Spine_03").position * scale;
            Object.DestroyImmediate(rig);

            var parts = new List<Part> { new Part { Mesh = body, Matrix = Matrix4x4.Scale(Vector3.one * scale), Group = 0 } };
            var spear = PropMesh("K/Props/SM_Prop_Beam_01", out var local);
            var rng = new System.Random(3);
            for (int i = 0; i < 7; i++)
            {
                Vector3 anchor = Vector3.Lerp(lowerBack, upperBack, (float)rng.NextDouble()) + new Vector3(((float)rng.NextDouble() - 0.5f) * 4f, 0f, -1.2f);
                Vector3 inward = (Vector3.forward + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.8f, ((float)rng.NextDouble() - 0.3f) * 0.6f, 0f)).normalized;
                float length = 4f + (float)rng.NextDouble() * 3f;
                Vector3 foot = anchor - inward * length;
                parts.Add(new Part { Mesh = spear, Matrix = Matrix4x4.TRS(foot, Quaternion.FromToRotation(Vector3.up, inward), new Vector3(1.6f, (length + 1.5f) / 2.5f, 1.6f)) * local, Group = 1 });
            }

            var result = Assemble(parts, 2, Poses["FaceDown_B"].lie, 0.8f);
            Object.DestroyImmediate(body);
            return result;
        }

        // ---------------------------------------------------------------- Monster materials

        // The palette texture a Synty character renderer uses (works for Standard and URP/Lit materials).
        private static string CharacterTexture(string prefabPath, string rendererName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var renderer = Find(prefab.transform, rendererName).GetComponent<SkinnedMeshRenderer>();
            var material = renderer.sharedMaterial;
            var texture = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap") : material.mainTexture;
            return AssetDatabase.GetAssetPath(texture);
        }

        // Recolours a Synty character texture (hue, saturation, value) and adds a faint ember-crack emission.
        private static Material MonsterMaterial(string name, string sourceTexture, float hue, float saturation, float value, Color flesh)
        {
            string texPath = L1Build.MaterialsPath + "/Monsters/" + name + ".png";
            string emitPath = L1Build.MaterialsPath + "/Monsters/" + name + "_Embers.png";
            if (!AssetDatabase.IsValidFolder(L1Build.MaterialsPath + "/Monsters"))
            {
                AssetDatabase.CreateFolder(L1Build.MaterialsPath, "Monsters");
            }

            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(sourceTexture));
            var px = src.GetPixels();
            var emit = new Color[px.Length];
            int w = src.width;
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                bool warm = (h < 0.1f || h > 0.92f) && s > 0.25f;   // skin and cloth cells become raw flesh
                // Cap non-flesh brightness so pale trims (cloth bands, bone) never pop against the ash palette.
                var c = warm ? flesh * Mathf.Lerp(0.8f, 1.2f, v) : Color.HSVToRGB(hue, saturation, Mathf.Min(v * value, 0.4f));
                c.a = 1f;
                px[i] = c;

                float x = (i % w) / (float)w, y = (i / w) / (float)w;
                float ridge = Mathf.Abs(Mathf.PerlinNoise(x * 14f, y * 14f) - 0.5f);
                float crack = Mathf.Clamp01(1f - ridge / 0.025f) * (Mathf.PerlinNoise(x * 3f + 9f, y * 3f + 4f) > 0.5f ? 1f : 0f);
                emit[i] = new Color(crack, crack * 0.35f, crack * 0.08f, 1f);
            }

            WritePng(texPath, px, w, src.height, true);
            WritePng(emitPath, emit, w, src.height, true);
            Object.DestroyImmediate(src);

            string matPath = L1Build.MaterialsPath + "/Monsters/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.28f);
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emitPath));
            // Dying embers only: kept well under the bloom threshold so it never competes with gameplay highlights.
            mat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.2f) * 0.45f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void WritePng(string path, Color[] pixels, int width, int height, bool srgb)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = srgb;
            importer.SaveAndReimport();
        }
    }
}
