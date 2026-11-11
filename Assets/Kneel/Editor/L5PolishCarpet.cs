using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Kneel.EditorTools
{
    // L5's carpets (owner, 2026-11-11): "There a stain running down the middle of the carpet. I do not like this. Please
    // find a better pattern to show that it is worn. The castle has been taken by the master, so it should look dirty
    // and worn." / "Make sure there are no cutoffs and that the outline only appears at the edges" / "at the top of the
    // staircase ... It should be the same width as down below and it should not be cutoff anywhere."
    // - Each carpet is one shape, its border drawn once along its outline: the nave's runner widening round the stair
    //   hall's fire bowl (a hole round the bowl, inside its candles) and on into the niche under the landing; and the
    //   landing's, from the top of both flights into one runner through the grand doors and down the hall to the dais,
    //   whose steps it climbs to the throne. Shapes are signed-distance fields (smooth unions), their outlines traced
    //   by marching squares; the field is gridded inside the outline, the border is a strip along it.
    // - All carpets are 5 m wide (as the nave's always was) with a 0.4 m border: an oxblood edge, gold lines, a chain
    //   of gold lozenges, and a dark pinstripe on which the field meets it. The flights' runners (1.6 m, L5Level) wear
    //   the same border, so the landing's arms run on from them with no change.
    // - The wear: flat, posterised patches in the low-poly manner, never a stripe. Soot and grime blotches in two
    //   depths, the nap trodden pale in scattered patches, grey ash specks, a few charred scorches, and the gold thread
    //   of some lozenges worn away. The field is mapped in world space (a 6 m tile), so it runs on across every shape.
    public static partial class L5Polish
    {
        private const float CarpetHalf = 2.36f;
        private const float CarpetBorder = 0.4f;
        private const float CarpetTrim = 0.14f;
        private const float CarpetLink = 0.6f;
        private const int BorderLinks = 8;
        private const float FieldTile = 6f;

        private static readonly Color CarpetRed = new Color32(0x4A, 0x14, 0x1B, 0xFF);
        private static readonly Color CarpetEdge = new Color32(0x1A, 0x06, 0x08, 0xFF);
        private static readonly Color CarpetBand = new Color32(0x33, 0x0B, 0x10, 0xFF);
        private static readonly Color CarpetGold = new Color32(0x8C, 0x6A, 0x34, 0xFF);
        private static readonly Color Grime = new Color32(0x1E, 0x0D, 0x0B, 0xFF);
        private static readonly Color Trodden = new Color32(0x6E, 0x3A, 0x34, 0xFF);
        private static readonly Color Ash = new Color32(0x5A, 0x52, 0x4E, 0xFF);
        private static readonly Color Char = new Color32(0x0E, 0x07, 0x06, 0xFF);

        // ---------------------------------------------------------------- Wear

        // Value noise on an integer lattice that wraps every `period` cells (period 0: never), smooth between.
        private static float LatticeNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float Hash(int i, int j)
            {
                if (period > 0)
                {
                    i = ((i % period) + period) % period;
                    j = ((j % period) + period) % period;
                }

                unchecked
                {
                    int h = i * 374761393 + j * 668265263 + seed * 2147483647;
                    h = (h ^ (h >> 13)) * 1274126177;
                    return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
                }
            }

            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), fx), Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), fx), fy);
        }

        // Noise in metres, features about `size` across; `tile` > 0 makes it repeat every `tile` metres.
        private static float WearNoise(float x, float y, float size, float tile, int seed)
        {
            float total = 0f, weight = 0f, amp = 1f;
            for (int o = 0; o < 3; o++)
            {
                float cells = size / (1 << o);
                int period = tile > 0f ? Mathf.Max(1, Mathf.RoundToInt(tile / cells)) : 0;
                float scale = tile > 0f ? period / tile : 1f / cells;
                total += amp * LatticeNoise(x * scale, y * scale, period, seed + o * 31);
                weight += amp;
                amp *= 0.5f;
            }

            return total / weight;
        }

        // A flat step with a hairline of anti-aliasing (the supersampling does the rest).
        private static float Step(float edge, float value) => Mathf.Clamp01((value - edge) / 0.008f + 0.5f);

        // The worn field at (x, y) metres: many small things, none loud. Grime settled in patches a hand or two across
        // (two flat depths), the nap trodden pale in scuffs, fine ash and grit, a few small charred burns from the fall
        // of the castle. Flat tones (the low-poly manner), posterised from noise, never a stripe.
        private static Color WornField(Color c, float x, float y, float tile)
        {
            float grime = WearNoise(x, y, 1.1f, tile, 11);
            c = Color.Lerp(c, Grime, 0.12f * Step(0.58f, grime) + 0.14f * Step(0.7f, grime));
            float scuff = WearNoise(x + 40f, y, 0.55f, tile, 23);
            c = Color.Lerp(c, Trodden, 0.22f * Step(0.69f, scuff) * (1f - Step(0.62f, grime)));
            float grit = WearNoise(x, y + 17f, 0.09f, tile, 37);
            c = Color.Lerp(c, Ash, 0.3f * Step(0.77f, grit) * Step(0.48f, WearNoise(x, y, 1.6f, tile, 41)));
            float fleck = WearNoise(x + 5f, y + 29f, 0.07f, tile, 47);
            c = Color.Lerp(c, Grime, 0.25f * Step(0.79f, fleck));
            float burn = WearNoise(x + 9f, y + 3f, 0.8f, tile, 53) * WearNoise(x, y, 4f, tile, 59);
            c = Color.Lerp(c, Char, 0.6f * Step(0.5f, burn));
            return c;
        }

        // The border at `e` metres in from the carpet's edge, `s` metres along it. Returns field colour inside it.
        private static Color BorderAt(float e, float s, float linkLength, bool worn, out bool inField)
        {
            inField = false;
            float link = Mathf.Repeat(s / linkLength, 1f);
            int linkIndex = Mathf.FloorToInt(s / linkLength);
            Color c;
            if (e < 0.05f)
                c = CarpetEdge;
            else if (e < 0.078f)
                c = CarpetGold;
            else if (e < 0.29f)
            {
                c = CarpetBand;
                float bu = (e - 0.184f) / 0.066f, bv = (link - 0.5f) * linkLength / 0.12f;
                bool diamond = Mathf.Abs(bu) + Mathf.Abs(bv) < 1f;
                float dv = Mathf.Min(link, 1f - link) * linkLength / 0.024f;
                bool dot = Mathf.Abs(bu) < 0.36f && dv < 1f;
                if (diamond || dot)
                {
                    // Some lozenges have lost their gold thread.
                    bool frayed = worn && LatticeNoise(linkIndex * 1.37f, 3.1f, 0, 71) > 0.72f;
                    c = frayed ? Color.Lerp(CarpetBand, CarpetGold, 0.35f) : CarpetGold;
                }
            }
            else if (e < 0.316f)
                c = CarpetGold;
            else if (e > 0.382f && e < 0.4f)
                c = CarpetBand;
            else
            {
                c = CarpetRed;
                inField = e >= 0.4f;
            }

            if (worn)
            {
                // Grime gathers at the outer edge, in patches along it.
                float g = WearNoise(s, e * 3f, 1.6f, 0f, 83);
                c = Color.Lerp(c, Grime, 0.35f * Step(0.55f, g) * Mathf.Clamp01(1f - e / 0.12f) + 0.2f * Step(0.68f, g));
            }

            return c;
        }

        private static Texture2D SaveTexture(string name, int w, int h, Func<float, float, Color> pixel, int ss = 3)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                            c += pixel((x + (sx + 0.5f) / ss) / w, (y + (sy + 0.5f) / ss) / h);
                    }

                    c /= ss * ss;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.SetPixels(px);
            tex.Apply();
            string path = L5Level.MaterialsPath + "/Textures/" + name + ".png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CarpetMat(string name, Texture2D tex, Vector2 scale)
        {
            string path = L5Level.MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.04f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", scale);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // The flights' runner (L5Level.CarpetMaterial): 1.6 m across, its texture eight widths long (12.8 m).
        public static Material RunnerMaterial()
        {
            const float width = 1.6f, length = width * 8f;
            const int links = 21;
            float linkLength = length / links;
            Texture2D tex = SaveTexture("L5_Carpet_Weave", 256, 2048, (u, v) =>
            {
                float e = Mathf.Min(u, 1f - u) * width, s = v * length;
                Color c = BorderAt(e, s + (u > 0.5f ? 0.3f : 0f), linkLength, true, out bool field);
                return field ? WornField(c, u * width + 101f, s, 0f) : c;
            });
            return CarpetMat("L5_Carpet", tex, new Vector2(1f, 1f / 8f));
        }

        // The field, tiled in world space every 6 m.
        private static Material FieldMaterial()
        {
            Texture2D tex = SaveTexture("L5_CarpetField", 512, 512, (u, v) => WornField(CarpetRed, u * FieldTile, v * FieldTile, FieldTile), 2);
            return CarpetMat("L5_CarpetField", tex, Vector2.one);
        }

        // The border: u across it (0 at the carpet's edge), v along it, BorderLinks lozenges a repeat.
        private static Material BorderMaterial()
        {
            float length = CarpetLink * BorderLinks;
            Texture2D tex = SaveTexture("L5_CarpetBorder", 128, 1024, (u, v) => BorderAt(u * CarpetBorder, v * length, CarpetLink, true, out _));
            return CarpetMat("L5_CarpetBorder", tex, Vector2.one);
        }

        // ---------------------------------------------------------------- Shapes

        private static float SdBox(Vector2 p, Vector2 centre, Vector2 half, float round)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        private static float SdCircle(Vector2 p, Vector2 centre, float radius) => (p - centre).magnitude - radius;

        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude - radius;
        }

        // A band of half-width `half` along an arc of `radius` round `centre`, from angle a0 to a1 (degrees), square ends.
        private static float SdArcBand(Vector2 p, Vector2 centre, float radius, float half, float a0, float a1)
        {
            Vector2 q = p - centre;
            float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
            float lo = Mathf.Min(a0, a1), hi = Mathf.Max(a0, a1), mid = (lo + hi) * 0.5f;
            float rel = Mathf.DeltaAngle(mid, ang);
            float across = Mathf.Abs(q.magnitude - radius) - half;
            float along = (Mathf.Abs(rel) - (hi - lo) * 0.5f) * Mathf.Deg2Rad * radius;
            return Mathf.Max(across, along);
        }

        private static float SmoothUnion(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        private class CarpetShape
        {
            public string name;
            public float floor;
            public Rect bounds;
            // The shape whose outline is drawn (and whose gradient sets the border's inward direction).
            public Func<Vector2, float> outline;
            // Where the carpet lies: the outline, cut where it runs on into another piece (no border on the cut).
            public Func<Vector2, float> sdf;
            public Func<Vector2, bool> cut = _ => false;
        }

        private static Vector2 Plan(Vector3 p) => new Vector2(p.x, p.z);

        // The nave's runner from the outer doors, round the fire bowl (inside its candles, outside the stair's feet), on
        // into the niche under the landing, short of its candles.
        private static CarpetShape LowerCarpet()
        {
            var bowl = Plan(L5Greybox.Centrepiece);
            Func<Vector2, float> outer = p => SmoothUnion(SdBox(p, new Vector2(54f, 333.2f), new Vector2(CarpetHalf, 63f), 0.9f), SdCircle(p, bowl, 8f), 2.6f);
            Func<Vector2, float> shape = p => Mathf.Max(outer(p), 4.2f - (p - bowl).magnitude);
            return new CarpetShape { name = "LowerCarpet", floor = 0f, bounds = Rect.MinMaxRect(44f, 268.5f, 64f, 398f), outline = shape, sdf = shape };
        }

        // The landing's: from the top tread of both flights (laid over their runners, which end at the treads' noses),
        // round the landing's front on the stair's circle, and one runner from the landing's front through the grand doors and down the hall to the dais's first
        // step, where the dais piece takes over.
        private const float DaisFront = 466.49f;
        private const float UpperLift = 0.03f;

        // The flights' top treads run from these angles (L5Greybox.StairTopFrom, mirrored) to the landing.
        private const float TopTreadWest = 120.4f, TopTreadEast = 180f - TopTreadWest;

        private static CarpetShape UpperCarpet()
        {
            Vector2 stair = L5Greybox.StairCentre;
            Func<Vector2, float> outline = p =>
            {
                // Along the stair's own circle from one flight's top to the other's (over each top tread, to its
                // nose), and the runner rising north out of the middle of it.
                float arc = SdArcBand(p, stair, 15f, 0.8f, TopTreadEast, TopTreadWest);
                float front = stair.y + 15f;
                float stem = SdBox(p, new Vector2(54f, (front + 470f) * 0.5f), new Vector2(CarpetHalf, (470f - front) * 0.5f), 0f);
                return SmoothUnion(stem, arc, 0.8f);
            };
            return new CarpetShape
            {
                name = "UpperCarpet",
                floor = L5Greybox.Hall,
                bounds = Rect.MinMaxRect(45.5f, 393.5f, 62.5f, 467f),
                outline = outline,
                sdf = p => Mathf.Max(outline(p), p.y - DaisFront),
                // No border across the dais front (the dais piece runs on) or across the flights' top treads' noses
                // (their runners run on down the stair).
                cut = p =>
                {
                    if (Mathf.Abs(p.y - DaisFront) < 0.03f)
                        return true;
                    Vector2 q = p - stair;
                    float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
                    return q.magnitude > 14.1f && q.magnitude < 15.9f && (Mathf.Abs(ang - TopTreadWest) < 0.25f || Mathf.Abs(ang - TopTreadEast) < 0.25f);
                },
            };
        }

        private static Vector2 Gradient(Func<Vector2, float> f, Vector2 p)
        {
            const float h = 0.01f;
            var g = new Vector2(f(p + new Vector2(h, 0f)) - f(p - new Vector2(h, 0f)), f(p + new Vector2(0f, h)) - f(p - new Vector2(0f, h)));
            return g.sqrMagnitude > 1e-12f ? g.normalized : Vector2.up;
        }

        // The zero contour of `f` over `box`, traced on a `h` grid: closed loops of points.
        private static List<List<Vector2>> Contours(Func<Vector2, float> f, Rect box, float h)
        {
            int nx = Mathf.CeilToInt(box.width / h), ny = Mathf.CeilToInt(box.height / h);
            var v = new float[nx + 1, ny + 1];
            for (int i = 0; i <= nx; i++)
            {
                for (int j = 0; j <= ny; j++)
                    v[i, j] = f(new Vector2(box.xMin + i * h, box.yMin + j * h));
            }

            Vector2 Corner(int i, int j) => new Vector2(box.xMin + i * h, box.yMin + j * h);
            // An edge's key: (i, j, 0) runs from corner (i, j) to (i + 1, j); (i, j, 1) to (i, j + 1).
            Vector2 EdgePoint((int i, int j, int o) e)
            {
                int i1 = e.o == 0 ? e.i + 1 : e.i, j1 = e.o == 0 ? e.j : e.j + 1;
                float a = v[e.i, e.j], b = v[i1, j1];
                float t = Mathf.Abs(a - b) > 1e-9f ? a / (a - b) : 0.5f;
                return Vector2.Lerp(Corner(e.i, e.j), Corner(i1, j1), Mathf.Clamp01(t));
            }

            var links = new Dictionary<(int, int, int), List<(int, int, int)>>();
            void Link((int, int, int) a, (int, int, int) b)
            {
                if (links.TryGetValue(a, out var la) == false)
                    links[a] = la = new List<(int, int, int)>();
                if (links.TryGetValue(b, out var lb) == false)
                    links[b] = lb = new List<(int, int, int)>();
                la.Add(b);
                lb.Add(a);
            }

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    bool c0 = v[i, j] < 0f, c1 = v[i + 1, j] < 0f, c2 = v[i + 1, j + 1] < 0f, c3 = v[i, j + 1] < 0f;
                    int code = (c0 ? 1 : 0) | (c1 ? 2 : 0) | (c2 ? 4 : 0) | (c3 ? 8 : 0);
                    var e0 = (i, j, 0);
                    var e1 = (i + 1, j, 1);
                    var e2 = (i, j + 1, 0);
                    var e3 = (i, j, 1);
                    bool centre = (v[i, j] + v[i + 1, j] + v[i + 1, j + 1] + v[i, j + 1]) < 0f;
                    switch (code)
                    {
                        case 1: case 14: Link(e3, e0); break;
                        case 2: case 13: Link(e0, e1); break;
                        case 3: case 12: Link(e3, e1); break;
                        case 4: case 11: Link(e1, e2); break;
                        case 6: case 9: Link(e0, e2); break;
                        case 7: case 8: Link(e2, e3); break;
                        case 5:
                            if (centre) { Link(e0, e1); Link(e2, e3); }
                            else { Link(e3, e0); Link(e1, e2); }
                            break;
                        case 10:
                            if (centre) { Link(e3, e0); Link(e1, e2); }
                            else { Link(e0, e1); Link(e2, e3); }
                            break;
                    }
                }
            }

            // Every edge the contour crosses joins exactly two segments (one in each cell beside it), so each run is a
            // closed loop; walk each once.
            var loops = new List<List<Vector2>>();
            var seen = new HashSet<(int, int, int)>();
            foreach (var start in links.Keys)
            {
                if (seen.Contains(start))
                    continue;
                var loop = new List<Vector2>();
                var prev = (-1, -1, -1);
                var cur = start;
                while (true)
                {
                    seen.Add(cur);
                    loop.Add(EdgePoint(cur));
                    List<(int, int, int)> nb = links[cur];
                    (int, int, int) step;
                    if (nb.Count == 2)
                        step = nb[0].Equals(prev) ? nb[1] : nb[0];
                    else if (nb.Count == 1 && nb[0].Equals(prev) == false)
                        step = nb[0];
                    else
                        break;
                    if (step.Equals(start) || seen.Contains(step))
                        break;
                    prev = cur;
                    cur = step;
                }

                if (loop.Count > 8)
                    loops.Add(loop);
            }

            return loops;
        }

        // The field: every cell of a `h` grid inside the outline (less the border), cut to it along the cells' edges.
        private static void Field(MeshBuilder b, CarpetShape shape, float y, float inset, int sub)
        {
            const float h = 0.25f;
            Rect box = shape.bounds;
            int nx = Mathf.CeilToInt(box.width / h), ny = Mathf.CeilToInt(box.height / h);
            var v = new float[nx + 1, ny + 1];
            for (int i = 0; i <= nx; i++)
            {
                for (int j = 0; j <= ny; j++)
                    v[i, j] = shape.sdf(new Vector2(box.xMin + i * h, box.yMin + j * h)) + inset;
            }

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    var corners = new[] { (i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1) };
                    if (corners.All(c => v[c.Item1, c.Item2] >= 0f))
                        continue;
                    var poly = new List<Vector2>();
                    for (int k = 0; k < 4; k++)
                    {
                        var (ai, aj) = corners[k];
                        var (bi, bj) = corners[(k + 1) % 4];
                        float fa = v[ai, aj], fb = v[bi, bj];
                        Vector2 pa = new Vector2(box.xMin + ai * h, box.yMin + aj * h), pb = new Vector2(box.xMin + bi * h, box.yMin + bj * h);
                        if (fa < 0f)
                            poly.Add(pa);
                        if ((fa < 0f) != (fb < 0f))
                            poly.Add(Vector2.Lerp(pa, pb, fa / (fa - fb)));
                    }

                    for (int k = 1; k + 1 < poly.Count; k++)
                    {
                        Vector3 A = new Vector3(poly[0].x, y, poly[0].y), B = new Vector3(poly[k].x, y, poly[k].y), C = new Vector3(poly[k + 1].x, y, poly[k + 1].y);
                        b.Tri(sub, A, B, C, poly[0] / FieldTile, poly[k] / FieldTile, poly[k + 1] / FieldTile, Vector3.up);
                    }
                }
            }
        }

        // The border along each outline loop, and the gold trim outside it; none along a cut.
        private static int Border(MeshBuilder b, CarpetShape shape, float y)
        {
            List<List<Vector2>> loops = Contours(shape.sdf, shape.bounds, 0.1f);
            float repeat = CarpetLink * BorderLinks;
            foreach (List<Vector2> loop in loops)
            {
                int n = loop.Count;
                // Smooth the traced line a little (it steps where the grid cuts it), keeping the cut's straight edge.
                for (int pass = 0; pass < 2; pass++)
                {
                    var smoothed = new List<Vector2>(loop);
                    for (int i = 0; i < n; i++)
                    {
                        if (shape.cut(loop[i]))
                            continue;
                        smoothed[i] = (loop[(i + n - 1) % n] + loop[i] * 2f + loop[(i + 1) % n]) * 0.25f;
                    }

                    loop.Clear();
                    loop.AddRange(smoothed);
                }

                float total = 0f;
                for (int i = 0; i < n; i++)
                    total += (loop[(i + 1) % n] - loop[i]).magnitude;
                bool closed = loop.All(p => shape.cut(p) == false);
                // A closed loop takes a whole number of lozenges, so its join doesn't show.
                float scale = closed ? Mathf.Max(1f, Mathf.Round(total / CarpetLink)) * CarpetLink / total : 1f;

                var inward = new Vector2[n];
                for (int i = 0; i < n; i++)
                    inward[i] = -Gradient(shape.outline, loop[i]);

                float s = 0f;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = loop[i], c = loop[(i + 1) % n];
                    float s1 = s + (c - a).magnitude * scale;
                    if (shape.cut(a) && shape.cut(c))
                    {
                        s = s1;
                        continue;
                    }

                    Vector3 P(Vector2 q, float lift) => new Vector3(q.x, y + lift, q.y);
                    Vector2 ai = a + inward[i] * CarpetBorder, ci = c + inward[(i + 1) % n] * CarpetBorder;
                    b.Quad(1, P(a, 0.003f), P(c, 0.003f), P(ci, 0.003f), P(ai, 0.003f),
                        new Vector2(0f, s / repeat), new Vector2(0f, s1 / repeat), new Vector2(1f, s1 / repeat), new Vector2(1f, s / repeat), Vector3.up);
                    Vector2 ao = a - inward[i] * CarpetTrim, co = c - inward[(i + 1) % n] * CarpetTrim;
                    b.Quad(2, P(ao, 0f), P(co, 0f), P(c, 0f), P(a, 0f), Vector2.zero, Vector3.up);
                    s = s1;
                }
            }

            return loops.Count;
        }

        // The dais piece: up the three steps and under the footstool, the same carpet in section.
        private static void DaisCarpet(MeshBuilder b, float lift)
        {
            const float floor = L5Greybox.Hall;
            var path = new List<Vector3>
            {
                new Vector3(54f, floor + lift, DaisFront),
                new Vector3(54f, floor + lift, 466.5f - 0.014f),
                new Vector3(54f, floor + 0.206f + lift, 466.5f - 0.014f),
                new Vector3(54f, floor + 0.206f + lift, 467f - 0.014f),
                new Vector3(54f, floor + 0.4f + lift, 467f - 0.014f),
                new Vector3(54f, floor + 0.4f + lift, 467.5f - 0.014f),
                new Vector3(54f, floor + 0.61f + lift, 467.5f - 0.014f),
                new Vector3(54f, floor + 0.61f + lift, 469.7f),
            };

            float repeat = CarpetLink * BorderLinks;
            float s = 0f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 a = path[i], c = path[i + 1];
                bool riser = Mathf.Abs(c.z - a.z) < 1e-4f;
                Vector3 facing = riser ? Vector3.back : Vector3.up;
                Vector3 off = riser ? Vector3.back * 0.003f : Vector3.up * 0.003f;
                float s1 = s + (c - a).magnitude;
                // The field, mapped as the flat carpet is (on a riser, its height stands in for its depth).
                float inner = CarpetHalf - CarpetBorder + 0.01f;
                Vector2 UV(Vector3 p) => new Vector2(p.x / FieldTile, (p.z + p.y) / FieldTile);
                Vector3 l0 = a + Vector3.left * inner, l1 = c + Vector3.left * inner, r0 = a + Vector3.right * inner, r1 = c + Vector3.right * inner;
                b.Quad(0, l0, l1, r1, r0, UV(l0), UV(l1), UV(r1), UV(r0), facing);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 e0 = a + Vector3.right * (side * CarpetHalf), e1 = c + Vector3.right * (side * CarpetHalf);
                    Vector3 i0 = a + Vector3.right * (side * (CarpetHalf - CarpetBorder)), i1 = c + Vector3.right * (side * (CarpetHalf - CarpetBorder));
                    b.Quad(1, e0 + off, e1 + off, i1 + off, i0 + off, new Vector2(0f, s / repeat), new Vector2(0f, s1 / repeat), new Vector2(1f, s1 / repeat), new Vector2(1f, s / repeat), facing);
                    Vector3 o0 = a + Vector3.right * (side * (CarpetHalf + CarpetTrim)), o1 = c + Vector3.right * (side * (CarpetHalf + CarpetTrim));
                    b.Quad(2, o0, o1, e1, e0, Vector2.zero, facing);
                }

                s = s1;
            }
        }

        private static string Carpets()
        {
            Transform art = GameObject.Find("L5_Root/Art").transform;
            Transform old = art.Find("Carpets");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var holder = new GameObject("Carpets").transform;
            holder.SetParent(art, false);

            // The old runners give way to the shapes (kept, switched off): the nave's, and the landing's two.
            int hidden = 0;
            foreach (Transform t in new[] { art.Find("Nave"), art.Find("Stair") })
            {
                foreach (Transform child in t)
                {
                    if (child.name == "Runner" && child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(false);
                        hidden++;
                    }
                }
            }

            L5Level.CarpetMaterial();
            Material[] materials = { FieldMaterial(), BorderMaterial(), AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L4/L4_Knights_Bronze.mat") };
            var log = new List<string>();
            foreach (CarpetShape shape in new[] { LowerCarpet(), UpperCarpet() })
            {
                var b = new MeshBuilder();
                float lift = shape.name == "UpperCarpet" ? UpperLift : CarpetLift;
                float y = shape.floor + lift;
                Field(b, shape, y, CarpetBorder - 0.012f, 0);
                int loops = Border(b, shape, y);
                if (shape.name == "UpperCarpet")
                    DaisCarpet(b, lift);
                b.Weld();
                Mesh mesh = SaveMeshAsset(b.ToMesh(3), PolishMeshes + "/L5_Carpet_" + shape.name + ".asset");
                var go = new GameObject(shape.name);
                go.transform.SetParent(holder, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = materials;
                r.shadowCastingMode = ShadowCastingMode.Off;
                log.Add(shape.name + " (" + loops + " outline" + (loops == 1 ? "" : "s") + ", " + mesh.vertexCount + " vertices)");
            }

            return "carpets: " + string.Join(", ", log) + "; " + hidden + " old runners off";
        }
    }
}
