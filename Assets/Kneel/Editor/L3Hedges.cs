using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // L3's hedgerows are thorn thickets the fire has been through, not clipped garden hedges: arching canes
    // growing out of each other, hung with dead leaves in ochre, rust and grey-olive. Nothing is laid under
    // them: a hedge is only the hedge, and stands on whatever ground it is put on. One generated mesh per
    // hedge piece, coloured through a small palette like the wheat
    // (column = cane or leaf kind, row = height, so the inside of the thicket is dark and its top catches the
    // light), and moving with the project's wind shader.
    public static class L3Hedges
    {
        private const int Bark = 0, BarkDark = 1, LeafOchre = 2, LeafOlive = 3, LeafRust = 4, LeafPale = 5, Char = 6, Litter = 7;

        private static readonly string[,] Ramps =
        {
            { "#15110E", "#3B2F25" }, { "#0E0C0A", "#2A211B" }, { "#2C2317", "#5E4D2E" }, { "#24231B", "#4C4A39" },
            { "#2A1A15", "#553422" }, { "#36302A", "#6A6352" }, { "#0B0A09", "#221D19" }, { "#1F1914", "#2F261E" },
        };

        public static Material Material => L3Build.Mat("L3_Bramble");

        public static void Materials()
        {
            L3Crops.MakeMaterial("L3_Bramble", "Kneel/Lit Wind", L3Crops.PaletteTexture("L3_Bramble_Palette", Ramps));
        }

        // A thicket filling a box 'length' (x) by 'depth' (z) by 'height', standing on the origin.
        // 'density' 1 is a hedge nothing gets through; below 0.5 it is burnt thin, with gaps.
        public static Mesh Thicket(string name, float length, float depth, float height, int seed, float density)
        {
            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector2 Cell(int column, float y) => new Vector2((column + 0.5f) / 8f, Mathf.Clamp(y / height, 0.04f, 0.96f));
            float halfX = length * 0.5f, halfZ = depth * 0.5f;
            var mesh = new L3Mesh();

            // The canes.
            int canes = Mathf.Max(3, Mathf.RoundToInt(length * 15f * density * Mathf.Max(1f, depth)));
            for (int n = 0; n < canes; n++)
            {
                var root = new Vector3(Range(-halfX + 0.08f, halfX - 0.08f), 0.02f, Range(-halfZ + 0.1f, halfZ - 0.1f));
                float top = Mathf.Min(height * Range(0.5f, 1f), height - 0.02f);
                float angle = Range(0f, Mathf.PI * 2f);
                var lean = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle) * 0.5f) * Range(0.25f, 0.85f) * Mathf.Min(1f, height / 1.6f);
                bool burnt = rng.NextDouble() < 0.22;
                int barkColumn = burnt ? Char : rng.Next(2) == 0 ? Bark : BarkDark;

                const int segments = 5;
                Vector3 Point(float t)
                {
                    // Up fast, then over: a cane arches under its own weight.
                    Vector3 p = root + Vector3.up * top * (1f - (1f - t) * (1f - t)) + lean * Mathf.Pow(t, 1.6f);
                    p.x = Mathf.Clamp(p.x, -halfX - 0.06f, halfX + 0.06f);
                    p.z = Mathf.Clamp(p.z, -halfZ - 0.08f, halfZ + 0.08f);
                    return p;
                }

                Vector3 previous = root;
                for (int s = 1; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    Vector3 p = Point(t) + new Vector3(Range(-0.05f, 0.05f), 0f, Range(-0.04f, 0.04f));
                    p.y = Mathf.Min(p.y, height - 0.01f);
                    float r0 = Mathf.Lerp(0.03f, 0.008f, (s - 1) / (float)segments), r1 = Mathf.Lerp(0.03f, 0.008f, t);
                    mesh.Prism(0, previous, p, r0, r1, 3, Cell(barkColumn, p.y));

                    // A side shoot, and the leaves that have not fallen yet.
                    if (s >= 1)
                    {
                        Vector3 shoot = p + new Vector3(Range(-0.3f, 0.3f), Range(-0.05f, 0.22f), Range(-0.18f, 0.18f));
                        shoot.y = Mathf.Clamp(shoot.y, 0.05f, height - 0.01f);
                        shoot.z = Mathf.Clamp(shoot.z, -halfZ - 0.08f, halfZ + 0.08f);
                        mesh.Prism(0, p, shoot, r1 * 0.7f, 0.004f, 3, Cell(barkColumn, shoot.y));
                        int leaves = burnt ? 1 : 3;
                        for (int l = 0; l < leaves; l++)
                        {
                            Vector3 at = Vector3.Lerp(p, shoot, Range(0.2f, 1f));
                            Leaf(mesh, rng, at, height, halfZ, burnt ? Char : new[] { LeafOchre, LeafOlive, LeafRust, LeafPale, LeafOchre, LeafOlive }[rng.Next(6)], Cell);
                        }
                    }

                    previous = p;
                }
            }

            return L3Build.SaveMesh(mesh, "Hedges", name);
        }

        // One dead leaf: a small diamond hanging off a twig, lit mostly from above so the thicket has no black side.
        private static void Leaf(L3Mesh mesh, System.Random rng, Vector3 at, float height, float halfZ, int column, System.Func<int, float, Vector2> cell)
        {
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var direction = new Vector3(Range(-1f, 1f), Range(-0.7f, 0.35f), Range(-1f, 1f)).normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            side = side.sqrMagnitude < 0.01f ? Vector3.right : side.normalized;
            float length = Range(0.2f, 0.34f), width = length * Range(0.4f, 0.55f);
            Vector3 tip = at + direction * length, mid = at + direction * length * 0.45f;
            tip.y = Mathf.Clamp(tip.y, 0.04f, height);
            tip.z = Mathf.Clamp(tip.z, -halfZ - 0.1f, halfZ + 0.1f);
            Vector3 left = mid + side * width * 0.5f, right = mid - side * width * 0.5f;
            Vector3 normal = (Vector3.Cross(left - at, tip - at).normalized * 0.4f + Vector3.up).normalized;
            if (normal.y < 0f)
            {
                normal = -normal;
            }

            Vector2 uv = cell(column, at.y), uvTip = cell(column, tip.y);
            mesh.Tri(0, at, left, tip, uv, uv, uvTip, normal);
            mesh.Tri(0, at, tip, right, uv, uvTip, uv, normal);
        }
    }
}
