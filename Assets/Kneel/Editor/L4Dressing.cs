using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Phase 3 (refinement): the aftermath on the floors. The blockout's yards were clean paving; a stronghold that
    // fell three days ago is not. Per floor, seeded:
    // - rubble along the wall feet, where stone fell from the walls above, thinning toward the middle;
    // - the dead, long since bones (owner, 2026-10-19: the stronghold fell to the master long ago), more where the
    //   fighting was;
    // - dropped weapons and shields round them.
    // All of it low (under 0.5 m) and without colliders, so it never reads as cover or blocks a dodge; kept clear
    // of every placed piece, hazard, stair, spawn and the route's narrow joins. Rebuilding replaces only this group.
    public static class L4Dressing
    {
        public const string GroupName = "Aftermath";

        private static readonly string[] Bodies = { "Supine", "Prone", "Curled", "Remains", "Supine", "Prone", "Remains" };

        private static readonly string[] Weapons = { "K:SM_Wep_Broadsword_01", "K:SM_Wep_Halberd_01", "K:SM_Wep_Shield_01", "K:SM_Wep_Shield_02", "K:SM_Wep_Shield_03", "K:SM_Wep_Shield_04", "K:SM_Wep_Zweihander_01", "K:SM_Wep_Rapier_01" };

        // Fighting floors get the dead; walks and lanes only rubble and the odd weapon.
        private static readonly HashSet<string> Battlefields = new HashSet<string> { "F02", "F03", "F04", "F09", "F11", "F17", "F20", "F23", "F22" };

        [MenuItem("Kneel/L4/Build/Aftermath (replaces that group)")]
        public static void BuildMenu()
        {
            var root = L4Level.Root;
            if (root == null)
            {
                Debug.LogWarning("Open L4_BurningGate first.");
                return;
            }

            Debug.Log(Build(root.transform));
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        public static string Build(Transform root)
        {
            var old = root.Find(GroupName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var g = L3Build.Child(root, GroupName).transform;
            Physics.SyncTransforms();
            var keepOut = KeepOut(root);
            var knights = L4Build.Mat("L4_Knights_Stone");
            int stones = 0, dead = 0, arms = 0;
            foreach (var f in L4Plan.Floors)
            {
                float area = (f.X1 - f.X0) * (f.Z1 - f.Z0);
                if (f.Mass || f.Tower || f.Hidden || area < 24f)
                {
                    continue;
                }

                var parent = L3Build.Child(g, f.Id).transform;
                var rng = new System.Random(f.Id.GetHashCode());
                bool battle = Battlefields.Contains(f.Id);

                // Rubble: try many spots, keep them by how close they are to an edge.
                int tries = Mathf.RoundToInt(area * 0.5f);
                for (int i = 0; i < tries; i++)
                {
                    var p = new Vector2(Mathf.Lerp(f.X0 + 0.4f, f.X1 - 0.4f, (float)rng.NextDouble()), Mathf.Lerp(f.Z0 + 0.4f, f.Z1 - 0.4f, (float)rng.NextDouble()));
                    float edge = Mathf.Min(p.x - f.X0, f.X1 - p.x, p.y - f.Z0, f.Z1 - p.y);
                    float keep = edge < 1.4f ? 0.55f : edge < 3f ? 0.12f : 0.02f;
                    if (rng.NextDouble() > keep || !Clear(p, f, keepOut, 0.8f, out float y))
                    {
                        continue;
                    }

                    var size = new Vector3(0.18f + (float)rng.NextDouble() * 0.35f, 0.1f + (float)rng.NextDouble() * 0.2f, 0.15f + (float)rng.NextDouble() * 0.3f);
                    var stone = L3Build.MeshChild(parent, "Rubble", L2PolyKit.Tread(size, rng.Next(100000)), new Vector3(p.x, y + size.y * 0.3f, p.y), L4Kit.Stone);
                    stone.transform.rotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 30f, rng.Next(360), ((float)rng.NextDouble() - 0.5f) * 30f);
                    stones++;
                }

                // The dead, and what they dropped.
                int bodies = battle ? Mathf.RoundToInt(area / 80f) : Mathf.RoundToInt(area / 260f);
                int weapons = Mathf.RoundToInt(area / (battle ? 70f : 160f));
                for (int i = 0, placed = 0; i < bodies * 12 && placed < bodies; i++)
                {
                    var p = new Vector2(Mathf.Lerp(f.X0 + 1.5f, f.X1 - 1.5f, (float)rng.NextDouble()), Mathf.Lerp(f.Z0 + 1.5f, f.Z1 - 1.5f, (float)rng.NextDouble()));
                    if (!Clear(p, f, keepOut, 1.6f, out float y))
                    {
                        continue;
                    }

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L4Assets.Skeletons + Bodies[rng.Next(Bodies.Length)] + ".prefab");
                    if (prefab == null)
                    {
                        continue;
                    }

                    var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    body.transform.SetPositionAndRotation(new Vector3(p.x, y, p.y), Quaternion.Euler(0f, rng.Next(360), 0f));
                    keepOut.Add((p, 1.4f));
                    placed++;
                    dead++;
                }

                for (int i = 0, placed = 0; i < weapons * 12 && placed < weapons; i++)
                {
                    var p = new Vector2(Mathf.Lerp(f.X0 + 0.8f, f.X1 - 0.8f, (float)rng.NextDouble()), Mathf.Lerp(f.Z0 + 0.8f, f.Z1 - 0.8f, (float)rng.NextDouble()));
                    if (!Clear(p, f, keepOut, 0.9f, out float y))
                    {
                        continue;
                    }

                    string key = Weapons[rng.Next(Weapons.Length)];
                    bool shield = key.Contains("Shield");
                    // Lying flat: blades on their side, shields face up or face down.
                    var piece = L3Build.Part(parent, key, Vector3.zero, Vector3.zero, Vector3.one, knights);
                    piece.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f) * Quaternion.Euler(shield ? (rng.NextDouble() < 0.5 ? 90f : -90f) : 0f, 0f, shield ? 0f : 90f);
                    var b = L3Build.LocalBounds(piece, parent);
                    piece.transform.position = new Vector3(p.x, y + 0.01f, p.y) + (piece.transform.position - new Vector3(b.center.x, b.min.y, b.center.z));
                    keepOut.Add((p, 0.6f));
                    placed++;
                    arms++;
                }
            }

            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                r.gameObject.layer = L3Build.Layer(L3Build.Unlayered);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            }

            return GroupName + ": " + stones + " rubble, " + dead + " dead, " + arms + " weapons and shields";
        }

        // Everything the dressing keeps clear of, as circles in plan.
        private static List<(Vector2 at, float radius)> KeepOut(Transform root)
        {
            var list = new List<(Vector2, float)>();
            foreach (var p in L4Plan.Places)
            {
                float r = p.Group == "Hazards" ? 4f : p.Group == "Markers" ? 2f : p.Group == "Structures" ? 0f : 2.2f;
                if (r > 0f)
                {
                    list.Add((new Vector2(p.Position.x, p.Position.z), r));
                }
            }

            foreach (var e in L4Plan.Enemies)
            {
                list.Add((new Vector2(e.Position.x, e.Position.z), 1.8f));
            }

            foreach (var s in L4Plan.Stairs)
            {
                list.Add((new Vector2(s.Centre.x, s.Centre.z), 5f));
            }

            // Narrow joins the route passes through: the rubble gap, doorways, the shrine stair foot.
            foreach (var v in new[] { new Vector2(62f, 85f), new Vector2(62f, 125f), new Vector2(64f, 159f), new Vector2(57.5f, 166f), new Vector2(50.4f, 178f), new Vector2(65.4f, 186f), new Vector2(54f, 206f), new Vector2(54f, 226f), new Vector2(54f, 240f) })
            {
                list.Add((v, 3f));
            }

            return list;
        }

        // Whether p is free for a piece of the given radius, and the floor's surface height there.
        private static bool Clear(Vector2 p, L4Plan.Floor f, List<(Vector2 at, float radius)> keepOut, float radius, out float y)
        {
            y = f.Y;
            foreach (var (at, r) in keepOut)
            {
                if ((p - at).sqrMagnitude < (r + radius) * (r + radius))
                {
                    return false;
                }
            }

            // Only on the floor itself, never on a prop, a stair or another floor above it.
            int ground = 1 << L3Build.Layer(L3Build.Walkable);
            if (!Physics.Raycast(new Vector3(p.x, f.Y + 6f, p.y), Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (((1 << hit.collider.gameObject.layer) & ground) == 0 || Mathf.Abs(hit.point.y - f.Y) > 0.25f)
            {
                return false;
            }

            // Not where something stands just beside it either.
            // (A small sphere: rubble belongs against the walls.)
            int ignore = ground | (1 << L3Build.Layer(L3Build.Unlayered)) | (1 << 10);
            if (Physics.CheckSphere(new Vector3(p.x, f.Y + 0.6f, p.y), radius * 0.4f, ~ignore, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            y = hit.point.y;
            return true;
        }
    }
}
