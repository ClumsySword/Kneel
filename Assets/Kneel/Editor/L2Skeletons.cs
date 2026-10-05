using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Low-poly skeletons for the burnt houses (none of the packs has one). Each is built from a posed joint
    // set: faceted bones with knobbed ends, a ribcage of arched ribs, a spine of vertebrae, a pelvis, and a
    // skull with dark eye sockets. Flat shaded, two materials (bone, soot-dark sockets and char).
    public static class L2Skeletons
    {
        private const string MeshFolder = L2Build.MeshesPath + "/Skeletons";
        private const string PrefabFolder = L2Build.PrefabsPath + "/Skeletons";

        public static readonly string[] Keys = { "Skeletons/L2_Skeleton_Supine", "Skeletons/L2_Skeleton_Prone", "Skeletons/L2_Skeleton_Curled", "Skeletons/L2_Skeleton_Slumped", "Skeletons/L2_Skeleton_Remains" };

        [MenuItem("Kneel/L2/Build/Skeletons")]
        public static void BuildMenu()
        {
            Debug.Log("[L2] " + BuildAll());
        }

        public static string BuildAll()
        {
            L2Build.EnsureFolder(MeshFolder);
            L2Build.EnsureFolder(PrefabFolder);
            var bone = Material("L2_Bone", L1Build.Hex("#A89E88"), 0.15f);
            var dark = Material("L2_BoneDark", L1Build.Hex("#1B1612"), 0.05f);

            var built = new List<string>();
            foreach (var pose in Poses())
            {
                var b = new Builder();
                if (pose.Name == "Remains")
                {
                    Remains(b);
                }
                else
                {
                    Body(b, pose);
                }

                built.Add(Save(b, "L2_Skeleton_" + pose.Name, bone, dark));
            }

            AssetDatabase.SaveAssets();
            return "skeletons: " + string.Join(", ", built);
        }

        private static Material Material(string name, Color color, float smoothness)
        {
            string path = L2Build.MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static string Save(Builder b, string name, Material bone, Material dark)
        {
            var mesh = b.ToMesh(name);
            string meshPath = MeshFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                existing = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                Object.DestroyImmediate(mesh);
            }

            var root = new GameObject(name);
            var body = new GameObject("Bones");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = existing;
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { bone, dark };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            L1Build.SetStatic(root, L1Build.PropStatic);
            L1Build.SavePrefab(root, PrefabFolder + "/" + name + ".prefab");
            return name;
        }

        // ---------------------------------------------------------------- Poses

        private class Pose
        {
            public string Name;
            public Dictionary<string, Vector3> J = new Dictionary<string, Vector3>();
            public Vector3 ChestFront;
            public Vector3 FaceFront;

            public Vector3 this[string joint] => J[joint];
        }

        private static Pose Make(string name, Vector3 chestFront, Vector3 faceFront, params (string, float, float, float)[] joints)
        {
            var p = new Pose { Name = name, ChestFront = chestFront.normalized, FaceFront = faceFront.normalized };
            foreach (var (j, x, y, z) in joints)
            {
                p.J[j] = new Vector3(x, y, z);
            }

            return p;
        }

        // Joint positions in metres, ground at y = 0, head toward +Z.
        private static IEnumerable<Pose> Poses()
        {
            // On its back, one arm flung above the head, one knee fallen outward.
            yield return Make("Supine", Vector3.up, new Vector3(0.35f, 1f, 0.1f),
                ("Pelvis", 0f, 0.09f, 0f), ("Chest", 0f, 0.12f, 0.45f), ("Neck", 0f, 0.1f, 0.57f), ("Head", 0.02f, 0.1f, 0.69f),
                ("ShoulderL", -0.18f, 0.1f, 0.47f), ("ElbowL", -0.42f, 0.05f, 0.64f), ("WristL", -0.5f, 0.04f, 0.88f), ("HandL", -0.53f, 0.03f, 1.04f),
                ("ShoulderR", 0.18f, 0.1f, 0.47f), ("ElbowR", 0.36f, 0.05f, 0.24f), ("WristR", 0.44f, 0.04f, 0.01f), ("HandR", 0.47f, 0.03f, -0.15f),
                ("HipL", -0.1f, 0.08f, -0.04f), ("KneeL", -0.18f, 0.07f, -0.48f), ("AnkleL", -0.2f, 0.06f, -0.9f), ("ToeL", -0.29f, 0.16f, -0.97f),
                ("HipR", 0.1f, 0.08f, -0.04f), ("KneeR", 0.3f, 0.07f, -0.42f), ("AnkleR", 0.33f, 0.06f, -0.84f), ("ToeR", 0.43f, 0.15f, -0.9f));

            // Face down, crawling: one arm reaching ahead, the other tucked, a leg drawn up.
            yield return Make("Prone", Vector3.down, new Vector3(0.7f, -0.7f, 0.1f),
                ("Pelvis", 0f, 0.1f, 0f), ("Chest", 0.02f, 0.13f, 0.45f), ("Neck", 0.03f, 0.11f, 0.57f), ("Head", 0.07f, 0.1f, 0.69f),
                ("ShoulderL", -0.17f, 0.11f, 0.46f), ("ElbowL", -0.34f, 0.05f, 0.28f), ("WristL", -0.3f, 0.04f, 0.05f), ("HandL", -0.23f, 0.03f, -0.09f),
                ("ShoulderR", 0.2f, 0.11f, 0.47f), ("ElbowR", 0.3f, 0.06f, 0.76f), ("WristR", 0.36f, 0.04f, 1.02f), ("HandR", 0.38f, 0.03f, 1.19f),
                ("HipL", -0.1f, 0.09f, -0.03f), ("KneeL", -0.22f, 0.07f, -0.45f), ("AnkleL", -0.27f, 0.08f, -0.86f), ("ToeL", -0.29f, 0.02f, -0.99f),
                ("HipR", 0.1f, 0.09f, -0.03f), ("KneeR", 0.34f, 0.07f, -0.33f), ("AnkleR", 0.3f, 0.06f, -0.76f), ("ToeR", 0.32f, 0.02f, -0.89f));

            // On its side, curled up against the heat.
            yield return Make("Curled", new Vector3(1f, 0f, 0f), new Vector3(1f, -0.2f, 0.25f),
                ("Pelvis", 0f, 0.13f, 0f), ("Chest", 0.06f, 0.15f, 0.44f), ("Neck", 0.11f, 0.13f, 0.55f), ("Head", 0.17f, 0.11f, 0.66f),
                ("ShoulderL", 0.05f, 0.05f, 0.45f), ("ElbowL", 0.26f, 0.04f, 0.34f), ("WristL", 0.36f, 0.04f, 0.53f), ("HandL", 0.38f, 0.03f, 0.69f),
                ("ShoulderR", 0.06f, 0.3f, 0.44f), ("ElbowR", 0.28f, 0.2f, 0.3f), ("WristR", 0.42f, 0.1f, 0.44f), ("HandR", 0.48f, 0.05f, 0.57f),
                ("HipL", 0f, 0.05f, 0f), ("KneeL", 0.38f, 0.05f, 0.12f), ("AnkleL", 0.22f, 0.05f, -0.27f), ("ToeL", 0.33f, 0.03f, -0.36f),
                ("HipR", 0f, 0.22f, 0f), ("KneeR", 0.42f, 0.16f, 0.02f), ("AnkleR", 0.28f, 0.1f, -0.37f), ("ToeR", 0.39f, 0.07f, -0.46f));

            // Sitting slumped against a wall (behind it, toward -Z), head fallen forward.
            yield return Make("Slumped", new Vector3(0f, 0.25f, 1f), new Vector3(0.3f, -0.6f, 1f),
                ("Pelvis", 0f, 0.12f, 0f), ("Chest", 0f, 0.53f, -0.1f), ("Neck", 0.02f, 0.63f, -0.08f), ("Head", 0.08f, 0.71f, 0.01f),
                ("ShoulderL", -0.18f, 0.51f, -0.1f), ("ElbowL", -0.26f, 0.25f, -0.04f), ("WristL", -0.28f, 0.05f, 0.12f), ("HandL", -0.3f, 0.02f, 0.29f),
                ("ShoulderR", 0.18f, 0.51f, -0.1f), ("ElbowR", 0.3f, 0.26f, 0f), ("WristR", 0.24f, 0.13f, 0.22f), ("HandR", 0.17f, 0.11f, 0.37f),
                ("HipL", -0.1f, 0.1f, 0.02f), ("KneeL", -0.16f, 0.32f, 0.4f), ("AnkleL", -0.2f, 0.06f, 0.75f), ("ToeL", -0.23f, 0.16f, 0.87f),
                ("HipR", 0.1f, 0.1f, 0.02f), ("KneeR", 0.2f, 0.08f, 0.46f), ("AnkleR", 0.28f, 0.06f, 0.9f), ("ToeR", 0.35f, 0.16f, 0.99f));

            yield return new Pose { Name = "Remains" };
        }

        // ---------------------------------------------------------------- Anatomy

        private const float Limb = 0.028f;

        private static void Body(Builder b, Pose p)
        {
            Vector3 up = (p["Neck"] - p["Pelvis"]).normalized;
            Vector3 front = Vector3.ProjectOnPlane(p.ChestFront, up).normalized;
            Vector3 right = Vector3.Cross(up, front).normalized;

            // Spine: vertebrae along a gentle curve from the pelvis to the neck.
            for (int i = 0; i <= 11; i++)
            {
                float t = i / 11f;
                Vector3 a = Bezier(p["Pelvis"], p["Chest"] - front * 0.05f, p["Neck"], t);
                Vector3 c = Bezier(p["Pelvis"], p["Chest"] - front * 0.05f, p["Neck"], t + 1f / 11f * 0.55f);
                b.Prism(0, a, c, 0.024f, 0.02f, 5);
                b.Knob(0, a - front * 0.022f, 0.014f);
            }

            Ribcage(b, p["Chest"], up, front, right);
            Pelvis(b, p["Pelvis"], up, front, right);

            // Collarbones, arms and hands.
            Vector3 sternumTop = p["Chest"] + front * 0.11f - up * 0.02f;
            foreach (string side in new[] { "L", "R" })
            {
                b.Prism(0, sternumTop, p["Shoulder" + side], 0.012f, 0.012f, 4);
                b.Knob(0, p["Shoulder" + side], 0.03f);
                b.Prism(0, p["Shoulder" + side], p["Elbow" + side], Limb, Limb * 0.8f, 6);
                b.Knob(0, p["Elbow" + side], 0.025f);
                Vector3 across = Vector3.Cross(p["Wrist" + side] - p["Elbow" + side], Vector3.up).normalized * 0.018f;
                b.Prism(0, p["Elbow" + side] + across, p["Wrist" + side] + across, Limb * 0.65f, Limb * 0.55f, 5);
                b.Prism(0, p["Elbow" + side] - across, p["Wrist" + side] - across, Limb * 0.6f, Limb * 0.5f, 5);
                Hand(b, p["Wrist" + side], p["Hand" + side]);

                b.Knob(0, p["Hip" + side], 0.034f);
                b.Prism(0, p["Hip" + side], p["Knee" + side], Limb * 1.25f, Limb * 1.05f, 6);
                b.Knob(0, p["Knee" + side], 0.032f);
                b.Prism(0, p["Knee" + side], p["Ankle" + side], Limb * 1.05f, Limb * 0.8f, 6);
                b.Knob(0, p["Ankle" + side], 0.024f);
                Foot(b, p["Ankle" + side], p["Toe" + side]);
            }

            Skull(b, p["Head"], p["Head"] - p["Neck"], p.FaceFront);
        }

        private static void Ribcage(Builder b, Vector3 chest, Vector3 up, Vector3 front, Vector3 right)
        {
            // Six rib pairs arching from the spine round to the breastbone, widest in the middle, sloping down
            // toward the front.
            Vector3 spineAt(float k) => chest - up * (0.03f + k * 0.055f) - front * 0.04f;
            for (int k = 0; k < 6; k++)
            {
                float width = 0.105f + 0.035f * Mathf.Sin((k + 1) / 7f * Mathf.PI);
                float depth = 0.085f + 0.02f * Mathf.Sin((k + 1) / 7f * Mathf.PI);
                float reach = k < 4 ? 0.93f : 0.72f;   // lower (floating) ribs stop short of the front
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 prev = spineAt(k);
                    const int segments = 6;
                    for (int s = 1; s <= segments; s++)
                    {
                        float a = s / (float)segments * Mathf.PI * reach;
                        Vector3 q = spineAt(k) + front * (depth - Mathf.Cos(a) * depth) + right * side * Mathf.Sin(a) * width - up * (a / Mathf.PI) * 0.07f;
                        b.Prism(0, prev, q, 0.014f, 0.012f, 4);
                        prev = q;
                    }
                }
            }

            // Breastbone.
            b.Prism(0, chest + front * 0.12f - up * 0.02f, chest + front * 0.12f - up * 0.25f, 0.018f, 0.012f, 4);
        }

        private static void Pelvis(Builder b, Vector3 pelvis, Vector3 up, Vector3 front, Vector3 right)
        {
            var frame = Quaternion.LookRotation(front, up);
            foreach (float side in new[] { -1f, 1f })
            {
                // The flared hip blades.
                b.Ellipsoid(0, pelvis + right * side * 0.085f + up * 0.03f - front * 0.01f, frame * Quaternion.Euler(0f, side * 25f, side * -18f), new Vector3(0.022f, 0.075f, 0.06f), 6, 4);
            }

            b.Ellipsoid(0, pelvis - front * 0.03f, frame, new Vector3(0.04f, 0.06f, 0.025f), 5, 3);        // sacrum
            b.Prism(0, pelvis + right * 0.075f + front * 0.03f - up * 0.04f, pelvis - right * 0.075f + front * 0.03f - up * 0.04f, 0.016f, 0.016f, 4);
        }

        private static void Hand(Builder b, Vector3 wrist, Vector3 tip)
        {
            Vector3 dir = tip - wrist;
            Vector3 across = Vector3.Cross(dir, Vector3.up).normalized;
            if (across.sqrMagnitude < 0.01f)
            {
                across = Vector3.right;
            }

            Vector3 palm = wrist + dir * 0.45f;
            b.Prism(0, wrist, palm, 0.018f, 0.02f, 4);
            for (int f = -2; f <= 2; f++)
            {
                Vector3 root = palm + across * f * 0.013f;
                Vector3 end = tip + across * f * 0.02f + Vector3.down * 0.01f * Mathf.Abs(f) - dir * 0.08f * Mathf.Abs(f) * 0.5f;
                b.Prism(0, root, end, 0.006f, 0.005f, 3);
            }
        }

        private static void Foot(Builder b, Vector3 ankle, Vector3 toe)
        {
            Vector3 dir = toe - ankle;
            Vector3 across = Vector3.Cross(dir, Vector3.up).normalized;
            if (across.sqrMagnitude < 0.01f)
            {
                across = Vector3.right;
            }

            b.Knob(0, ankle - dir.normalized * 0.04f, 0.025f);   // heel
            for (int t = -2; t <= 2; t++)
            {
                b.Prism(0, ankle + across * t * 0.01f, toe + across * t * 0.017f, 0.009f, 0.006f, 3);
            }
        }

        // The skull: a rounded cranium, cheekbones and jaw, dark eye sockets and nose.
        private static void Skull(Builder b, Vector3 center, Vector3 crownDir, Vector3 faceFront)
        {
            Vector3 up = crownDir.normalized;
            Vector3 front = Vector3.ProjectOnPlane(faceFront, up).normalized;
            if (front.sqrMagnitude < 0.01f)
            {
                front = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            }

            var frame = Quaternion.LookRotation(front, up);
            Vector3 right = frame * Vector3.right;
            b.Ellipsoid(0, center, frame, new Vector3(0.085f, 0.094f, 0.11f), 9, 6);
            b.Ellipsoid(0, center + front * 0.055f - up * 0.052f, frame, new Vector3(0.062f, 0.044f, 0.055f), 7, 4);   // cheeks
            b.Ellipsoid(0, center + front * 0.06f - up * 0.1f, frame * Quaternion.Euler(12f, 0f, 0f), new Vector3(0.045f, 0.018f, 0.04f), 6, 3); // jaw

            // Sockets and nose: dark polygons set just proud of the face.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 socket = center + front * 0.107f - up * 0.03f + right * side * 0.035f;
                b.Disc(1, socket, (front * 1f + right * side * 0.35f).normalized, 0.026f, 6);
            }

            b.Disc(1, center + front * 0.112f - up * 0.066f, (front + -up * 0.2f).normalized, 0.011f, 3);
            // Teeth line.
            b.Prism(1, center + front * 0.103f - up * 0.09f - right * 0.03f, center + front * 0.103f - up * 0.09f + right * 0.03f, 0.005f, 0.005f, 4);
        }

        // A few bones left where a body burnt away: skull, a thigh bone, a pelvis, scattered ribs.
        private static void Remains(Builder b)
        {
            Skull(b, new Vector3(0.18f, 0.075f, 0.12f), new Vector3(-0.9f, 0.3f, 0.2f), new Vector3(0.2f, 1f, 0.4f));
            b.Prism(0, new Vector3(-0.2f, 0.03f, -0.1f), new Vector3(0.22f, 0.03f, -0.22f), 0.026f, 0.022f, 6);
            b.Knob(0, new Vector3(-0.2f, 0.035f, -0.1f), 0.036f);
            b.Knob(0, new Vector3(0.22f, 0.035f, -0.22f), 0.033f);
            b.Prism(0, new Vector3(-0.32f, 0.02f, 0.18f), new Vector3(-0.02f, 0.02f, 0.34f), 0.018f, 0.016f, 5);
            b.Knob(0, new Vector3(-0.32f, 0.02f, 0.18f), 0.025f);
            Pelvis(b, new Vector3(-0.06f, 0.06f, 0.02f), Vector3.up, Vector3.forward, Vector3.right);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.3f;
                Vector3 c = new Vector3(0.05f + Mathf.Cos(a) * 0.25f, 0.015f, -0.02f + Mathf.Sin(a) * 0.2f);
                Vector3 prev = c;
                for (int s = 1; s <= 4; s++)
                {
                    float arc = s / 4f * 1.8f;
                    Vector3 q = c + Quaternion.Euler(0f, a * 57f, 0f) * new Vector3(Mathf.Sin(arc) * 0.1f, 0.01f * Mathf.Sin(arc), (1f - Mathf.Cos(arc)) * 0.07f);
                    b.Prism(0, prev, q, 0.01f, 0.009f, 4);
                    prev = q;
                }
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
        }

        // ---------------------------------------------------------------- Mesh building (flat shaded)

        private class Builder
        {
            private readonly List<Vector3> v = new List<Vector3>();
            private readonly List<Vector3> n = new List<Vector3>();
            private readonly List<int>[] tris = { new List<int>(), new List<int>() };

            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int i = v.Count;
                v.Add(a);
                v.Add(b);
                v.Add(c);
                n.Add(normal);
                n.Add(normal);
                n.Add(normal);
                tris[sub].Add(i);
                tris[sub].Add(i + 1);
                tris[sub].Add(i + 2);
            }

            // A tapered, faceted bone shaft from a to b, capped at both ends.
            public void Prism(int sub, Vector3 a, Vector3 b, float ra, float rb, int sides)
            {
                Vector3 axis = b - a;
                if (axis.sqrMagnitude < 1e-8f)
                {
                    return;
                }

                Vector3 d = axis.normalized;
                Vector3 u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 w = Vector3.Cross(d, u);
                var ringA = new Vector3[sides];
                var ringB = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float ang = i * Mathf.PI * 2f / sides;
                    Vector3 o = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                    ringA[i] = a + o * ra;
                    ringB[i] = b + o * rb;
                }

                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Tri(sub, ringA[i], ringB[i], ringB[j]);
                    Tri(sub, ringA[i], ringB[j], ringA[j]);
                    Tri(sub, a, ringA[i], ringA[j]);
                    Tri(sub, b, ringB[j], ringB[i]);
                }
            }

            // An octahedron: the knobbed end of a bone.
            public void Knob(int sub, Vector3 c, float r)
            {
                Vector3[] p = { c + Vector3.right * r, c + Vector3.forward * r, c + Vector3.left * r, c + Vector3.back * r };
                Vector3 top = c + Vector3.up * r * 0.9f, bottom = c + Vector3.down * r * 0.9f;
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    Tri(sub, top, p[j], p[i]);
                    Tri(sub, bottom, p[i], p[j]);
                }
            }

            public void Ellipsoid(int sub, Vector3 c, Quaternion rotation, Vector3 radii, int lon, int lat)
            {
                Vector3 Point(int i, int j)
                {
                    float theta = j / (float)lat * Mathf.PI;
                    float phi = i / (float)lon * Mathf.PI * 2f;
                    var local = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi) * radii.x, Mathf.Cos(theta) * radii.y, Mathf.Sin(theta) * Mathf.Sin(phi) * radii.z);
                    return c + rotation * local;
                }

                for (int j = 0; j < lat; j++)
                {
                    for (int i = 0; i < lon; i++)
                    {
                        Vector3 a = Point(i, j), b = Point(i + 1, j), cc = Point(i + 1, j + 1), d = Point(i, j + 1);
                        if (j > 0)
                        {
                            Tri(sub, a, b, cc);
                        }

                        if (j < lat - 1)
                        {
                            Tri(sub, a, cc, d);
                        }
                    }
                }
            }

            public void Disc(int sub, Vector3 c, Vector3 normal, float r, int sides)
            {
                Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 w = Vector3.Cross(normal, u);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Tri(sub, c, c + (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * r, c + (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * r);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(tris[0], 0);
                mesh.SetTriangles(tris[1], 1);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
