using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Animal skeletons for L3's carcasses (none of the packs has an animal, alive or dead), built the way
    // L2Skeletons builds its human ones: faceted bones with knobbed joints, arched ribs, a spine of vertebrae
    // and a skull with dark sockets. An ox in two poses: fallen on its side, and collapsed on its belly in
    // the yoke. Bare bone only (owner's review): no hide, and nothing on the ground under it. Materials:
    // bone, and dark for the sockets and hooves.
    public static class L3Bones
    {
        private const string MeshFolder = L3Build.MeshesPath + "/Bones";
        private const int Bone = 0, Dark = 1;
        private static readonly Vector2 Flat = Vector2.zero;

        public static Material[] Materials => new[] { L3Build.Mat("L3_Bone"), L3Build.Mat("L3_BoneDark") };

        public static Mesh OxOnSide => Load("L3_Ox_OnSide") ?? BuildAll()[0];

        public static Mesh OxInYoke => Load("L3_Ox_InYoke") ?? BuildAll()[1];

        private static Mesh Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + "/" + name + ".asset");
        }

        [MenuItem("Kneel/L3/Build/Animal Skeletons")]
        public static void BuildMenu()
        {
            BuildAll();
        }

        public static Mesh[] BuildAll()
        {
            L2Build.EnsureFolder(MeshFolder);
            return new[] { Save(OnSide(), "L3_Ox_OnSide"), Save(InYoke(), "L3_Ox_InYoke") };
        }

        private static Mesh Save(L3Mesh data, string name)
        {
            // Both material slots must exist even if a pose leaves one empty.
            while (data.Tris.Count < 2)
            {
                data.Tris.Add(new System.Collections.Generic.List<int>());
            }

            string path = MeshFolder + "/" + name + ".asset";
            var mesh = data.ToMesh(name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------------------------------------------------------------- Poses

        // Fallen on its right side: back toward +z, legs toward -z, head toward +x. Fits 2 x 1 x 0.6.
        // The ground-side ribs lie flat; the upper ribs still arch. The belly ribs are gone (it was eaten).
        private static L3Mesh OnSide()
        {
            var b = new L3Mesh();
            var rng = new System.Random(11);
            Vector3 pelvis = new Vector3(-0.66f, 0.13f, 0.27f), withers = new Vector3(0.32f, 0.14f, 0.3f);

            Spine(b, pelvis, new Vector3(-0.15f, 0.12f, 0.33f), withers, 13, Vector3.forward, 0.11f);

            // Ribs: nine pairs from the spine toward the belly (-z).
            for (int k = 0; k < 9; k++)
            {
                float t = (k + 0.5f) / 9f;
                Vector3 root = Vector3.Lerp(withers, pelvis, t * 0.72f) + new Vector3(0f, 0f, 0.02f);
                float size = 0.75f + 0.25f * Mathf.Sin((t * 0.8f + 0.2f) * Mathf.PI);
                // Upper side: arches up and over. The last ribs are broken short.
                float reach = k >= 6 ? 0.45f + (float)rng.NextDouble() * 0.2f : 0.86f;
                Rib(b, root, Vector3.back, Vector3.up, 0.31f * size, 0.4f * size, reach);
                // Ground side: pressed flat.
                if (k != 5 && k != 7)
                {
                    Rib(b, root + Vector3.down * 0.07f, Vector3.back, Vector3.up, 0.33f * size, 0.05f, 0.8f);
                }
            }

            Hip(b, pelvis, Quaternion.Euler(0f, 0f, 90f));
            Tail(b, pelvis + new Vector3(-0.08f, -0.02f, 0f), new Vector3(-1f, -0.25f, -0.35f), 0.26f);

            // Legs, lying where they fell.
            Leg(b, withers + new Vector3(0.05f, 0.02f, -0.12f), new Vector3(0.36f, 0.07f, -0.02f), new Vector3(0.5f, 0.06f, -0.3f), new Vector3(0.42f, 0.05f, -0.48f), 1f);
            Leg(b, withers + new Vector3(-0.05f, -0.06f, -0.14f), new Vector3(0.14f, 0.06f, -0.08f), new Vector3(0.1f, 0.05f, -0.36f), new Vector3(0.24f, 0.04f, -0.5f), 0.95f);
            Leg(b, pelvis + new Vector3(0.05f, 0f, -0.1f), new Vector3(-0.48f, 0.07f, -0.1f), new Vector3(-0.72f, 0.06f, -0.3f), new Vector3(-0.6f, 0.05f, -0.5f), 1.1f);
            Leg(b, pelvis + new Vector3(-0.02f, -0.07f, -0.1f), new Vector3(-0.8f, 0.06f, -0.06f), new Vector3(-0.9f, 0.05f, -0.3f), new Vector3(-0.8f, 0.04f, -0.48f), 1.05f);
            Scapula(b, withers + new Vector3(0.02f, 0.05f, -0.1f), Quaternion.Euler(70f, 10f, 0f));

            // Neck and skull, the skull on its side.
            Vector3 skull = new Vector3(0.68f, 0.11f, 0.12f);
            Neck(b, withers, skull - new Vector3(0.12f, 0f, -0.04f));
            Skull(b, skull, Quaternion.LookRotation(new Vector3(1f, -0.05f, -0.28f), new Vector3(0f, 0.25f, 1f)));
            return b;
        }

        // Collapsed on its belly where it stood in the yoke: spine on top, ribs down both sides, legs folded,
        // the skull dropped forward. Head toward +x. About 1.9 long, 0.9 wide, 0.6 high.
        private static L3Mesh InYoke()
        {
            var b = new L3Mesh();
            Vector3 pelvis = new Vector3(-0.6f, 0.4f, 0f), withers = new Vector3(0.32f, 0.5f, 0f);

            Spine(b, pelvis, new Vector3(-0.15f, 0.4f, 0f), withers, 13, Vector3.up, 0.1f);

            for (int k = 0; k < 9; k++)
            {
                float t = (k + 0.5f) / 9f;
                Vector3 root = Vector3.Lerp(withers, pelvis, t * 0.72f) + Vector3.down * 0.02f;
                float size = 0.75f + 0.25f * Mathf.Sin((t * 0.8f + 0.2f) * Mathf.PI);
                foreach (float side in new[] { -1f, 1f })
                {
                    bool broken = (k == 6 && side > 0f) || (k == 8 && side < 0f);
                    Rib(b, root, Vector3.down, Vector3.forward * side, 0.24f * size, 0.3f * size, broken ? 0.4f : 0.82f);
                }
            }

            Hip(b, pelvis, Quaternion.identity);
            Tail(b, pelvis + new Vector3(-0.1f, -0.03f, 0f), new Vector3(-1f, -0.9f, 0.2f), 0.3f);

            foreach (float side in new[] { -1f, 1f })
            {
                // Forelegs folded forward under the chest, hind legs splayed out behind.
                Leg(b, withers + new Vector3(0.02f, -0.18f, 0.2f * side), new Vector3(0.24f, 0.09f, 0.3f * side), new Vector3(0.56f, 0.07f, 0.3f * side), new Vector3(0.74f, 0.05f, 0.24f * side), 1f);
                Leg(b, pelvis + new Vector3(0f, -0.1f, 0.16f * side), new Vector3(-0.36f, 0.1f, 0.36f * side), new Vector3(-0.74f, 0.07f, 0.4f * side), new Vector3(-0.56f, 0.05f, 0.46f * side), 1.1f);
                Scapula(b, withers + new Vector3(0.02f, -0.1f, 0.2f * side), Quaternion.Euler(0f, 0f, 12f * side) * Quaternion.Euler(0f, 90f, 0f));
            }

            Vector3 skull = new Vector3(0.72f, 0.13f, 0.03f);
            Neck(b, withers, skull + new Vector3(-0.14f, 0.05f, 0f));
            Skull(b, skull, Quaternion.LookRotation(new Vector3(1f, -0.22f, 0.08f), Vector3.up));
            return b;
        }

        // ---------------------------------------------------------------- Parts

        private static void Knob(L3Mesh b, Vector3 centre, float radius)
        {
            b.Ellipsoid(Bone, centre, Quaternion.identity, Vector3.one * radius, 4, 2, Flat);
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            return Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);
        }

        // Vertebrae from the pelvis to the withers, each with its spine process pointing along 'back'
        // (tallest over the shoulders, where an ox carries its hump).
        private static void Spine(L3Mesh b, Vector3 pelvis, Vector3 mid, Vector3 withers, int count, Vector3 back, float process)
        {
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                Vector3 a = Bezier(pelvis, mid, withers, t), c = Bezier(pelvis, mid, withers, t + 0.6f / count);
                b.Prism(Bone, a, c, 0.034f, 0.03f, 5, Flat);
                float height = process * (0.35f + 0.65f * Mathf.Pow(t, 1.6f));
                b.Prism(Bone, a + back * 0.02f, a + back * (0.02f + height) + (pelvis - withers).normalized * height * 0.35f, 0.016f, 0.008f, 3, Flat);
            }
        }

        // One rib: from the spine it swings out along 'side' while curving along 'toward', like a hoop.
        private static void Rib(L3Mesh b, Vector3 root, Vector3 toward, Vector3 side, float depth, float width, float reach)
        {
            const int segments = 6;
            Vector3 previous = root;
            for (int s = 1; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * reach;
                Vector3 q = root + toward * (depth - Mathf.Cos(a) * depth) + side * Mathf.Sin(a) * width + Vector3.left * (a / Mathf.PI) * 0.08f;
                b.Prism(Bone, previous, q, 0.017f, 0.014f, 4, Flat);
                previous = q;
            }
        }

        private static void Hip(L3Mesh b, Vector3 pelvis, Quaternion roll)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                b.Ellipsoid(Bone, pelvis + roll * new Vector3(-0.02f, -0.03f, side * 0.11f), roll * Quaternion.Euler(side * 24f, 0f, 14f), new Vector3(0.13f, 0.035f, 0.07f), 6, 3, Flat);
            }

            b.Ellipsoid(Bone, pelvis + new Vector3(-0.04f, 0f, 0f), roll, new Vector3(0.08f, 0.04f, 0.05f), 5, 3, Flat);
        }

        private static void Tail(L3Mesh b, Vector3 from, Vector3 direction, float length)
        {
            Vector3 d = direction.normalized, p = from;
            for (int i = 0; i < 7; i++)
            {
                Vector3 q = p + (d + Vector3.down * i * 0.05f).normalized * length / 7f;
                q.y = Mathf.Max(q.y, 0.03f);
                b.Prism(Bone, p, q, 0.014f - i * 0.0012f, 0.012f - i * 0.0012f, 4, Flat);
                p = q;
            }
        }

        // Upper leg, lower leg, cannon bone and hoof, with knobbed joints.
        private static void Leg(L3Mesh b, Vector3 top, Vector3 elbow, Vector3 knee, Vector3 hoof, float weight)
        {
            float r = 0.03f * weight;
            Knob(b, top, r * 1.35f);
            b.Prism(Bone, top, elbow, r, r * 0.85f, 6, Flat);
            Knob(b, elbow, r * 1.25f);
            b.Prism(Bone, elbow, knee, r * 0.8f, r * 0.65f, 5, Flat);
            Knob(b, knee, r * 1.05f);
            b.Prism(Bone, knee, hoof, r * 0.6f, r * 0.55f, 5, Flat);
            Vector3 toe = hoof + (hoof - knee).normalized * 0.05f;
            b.Prism(Dark, hoof, toe, r * 0.85f, r * 0.6f, 5, Flat);
        }

        private static void Scapula(L3Mesh b, Vector3 centre, Quaternion rotation)
        {
            b.Ellipsoid(Bone, centre, rotation, new Vector3(0.1f, 0.016f, 0.15f), 5, 2, Flat);
        }

        private static void Neck(L3Mesh b, Vector3 withers, Vector3 skullBase)
        {
            Vector3 mid = Vector3.Lerp(withers, skullBase, 0.5f) + Vector3.down * 0.04f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = Bezier(withers, mid, skullBase, i / 6f), c = Bezier(withers, mid, skullBase, (i + 0.65f) / 6f);
                b.Prism(Bone, a, c, 0.036f, 0.032f, 5, Flat);
                Knob(b, a, 0.03f);
            }
        }

        // An ox skull: cranium, long tapering muzzle, lower jaw, dark sockets and nose, and two swept horns.
        // 'frame' looks down the muzzle; its up is the crown of the head.
        private static void Skull(L3Mesh b, Vector3 centre, Quaternion frame)
        {
            Vector3 front = frame * Vector3.forward, up = frame * Vector3.up, right = frame * Vector3.right;
            b.Ellipsoid(Bone, centre, frame, new Vector3(0.105f, 0.085f, 0.12f), 7, 4, Flat);
            Vector3 nose = centre + front * 0.36f - up * 0.035f;
            b.Prism(Bone, centre + front * 0.06f - up * 0.01f, nose, 0.078f, 0.045f, 6, Flat);
            b.Prism(Bone, centre + front * 0.02f - up * 0.085f, nose - up * 0.045f - front * 0.03f, 0.03f, 0.02f, 4, Flat);
            b.Disc(Dark, nose + front * 0.004f, front, 0.03f, 5, Flat);

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 socket = centre + front * 0.1f + up * 0.02f + right * side * 0.082f;
                b.Disc(Dark, socket + right * side * 0.006f, (right * side + front * 0.25f + up * 0.2f).normalized, 0.034f, 6, Flat);

                // The horn: out, then forward and up, thinning to a point.
                Vector3 root = centre - front * 0.03f + up * 0.05f + right * side * 0.09f;
                Vector3 a = root + right * side * 0.14f + up * 0.03f;
                Vector3 c = a + right * side * 0.08f + up * 0.1f + front * 0.07f;
                Vector3 tip = c + up * 0.1f + front * 0.08f - right * side * 0.02f;
                b.Prism(Bone, root, a, 0.03f, 0.024f, 5, Flat);
                b.Prism(Bone, a, c, 0.024f, 0.016f, 5, Flat);
                b.Prism(Bone, c, tip, 0.016f, 0.004f, 4, Flat);
            }
        }
    }
}
