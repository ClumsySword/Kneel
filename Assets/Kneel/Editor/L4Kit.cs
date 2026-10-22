using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // L4's own masonry: the merlon run and the low parapet of the note's wall kit (W02, W03), built from worn
    // stone blocks (L2PolyKit.Tread) in a palette sampled from the graded castle stone, so they sit on the pack
    // walls without a seam in colour. Every run takes a damage amount: merlons chipped, broken to stubs or
    // gone, their stones fallen onto the walk or over the edge.
    public static class L4Kit
    {
        public const float Course = 0.45f;          // one course of blocks
        public const float MerlonWidth = 1.2f, MerlonDepth = 0.6f, MerlonHeight = 1.8f, SillHeight = 0.9f, Module = 2f;

        // ---------------------------------------------------------------- The stone palette

        private static Material stone;

        // Eight cells in L2PolyKit's order (top, top light, top dark, side, end, edge, iron, joint), sampled from
        // the graded Knights palette where the castle wall's faces read it. Taken from the darker half: block tops
        // face the high sun, the wall faces do not, and mid-range samples made the merlons read as pale brick.
        public static Material Stone
        {
            get
            {
                if (stone == null)
                {
                    stone = AssetDatabase.LoadAssetAtPath<Material>(L4Build.MaterialsPath + "/L4_Palette_Stone.mat") ?? BuildStonePalette();
                }

                return stone;
            }
        }

        public static Material BuildStonePalette()
        {
            var graded = new Texture2D(2, 2);
            graded.LoadImage(File.ReadAllBytes(L4Build.MaterialsPath + "/Textures/L4_Knights_Stone.png"));
            var lums = new List<Color>();
            var wall = L3Build.Pack("K:SM_Bld_Castle_Wall_01").GetComponentInChildren<MeshFilter>().sharedMesh;
            foreach (var uv in wall.uv)
            {
                lums.Add(graded.GetPixelBilinear(uv.x, uv.y));
            }

            Object.DestroyImmediate(graded);
            lums.Sort((a, b) => a.grayscale.CompareTo(b.grayscale));
            Color At(float q) => lums[Mathf.Clamp(Mathf.RoundToInt(q * (lums.Count - 1)), 0, lums.Count - 1)];
            var tones = new[]
            {
                At(0.3f), At(0.42f), At(0.2f), At(0.26f) * 0.9f, At(0.36f), At(0.5f), new Color(0.11f, 0.105f, 0.1f), At(0.1f) * 0.7f,
            };

            stone = Palette("L4_Palette_Stone", tones, 0.08f);
            return stone;
        }

        // A one-row palette texture (point filtered) under an L4 URP Lit material; faces pick a cell by UV:
        // u = (cell + 0.5) / cells.
        public static Material Palette(string name, Color[] tones, float smoothness)
        {
            var tex = new Texture2D(tones.Length, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < tones.Length; i++)
            {
                var c = tones[i];
                c.a = 1f;
                tex.SetPixel(i, 0, c);
            }

            tex.Apply();
            string folder = L4Build.MaterialsPath + "/Textures";
            L2Build.EnsureFolder(folder);
            string path = folder + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var mat = L4Build.LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Vector2 Cell(int cell, int cells = 8)
        {
            return new Vector2((cell + 0.5f) / cells, 0.5f);
        }

        // ---------------------------------------------------------------- Runs

        // How one merlon module came through.
        public enum Merlon
        {
            Whole,
            Chipped,
            Stub,
            Gone,
        }

        // A merlon run along 'dir' (unit, horizontal) from 'start' (base of the first module's outer corner line,
        // on the walk deck), 'modules' of 2 m. 'outward' points away from the walk (where stones fall over the
        // edge). damage 0..1 sets how many merlons are broken; 'fallY' is the ground height over the edge.
        public static void MerlonRun(Transform parent, Vector3 start, Vector3 dir, Vector3 outward, int modules, int seed, float damage, float fallY, List<Merlon> states = null, Merlon? forced = null, bool scatter = true)
        {
            var rng = new System.Random(seed);
            var g = new GameObject("MerlonRun").transform;
            g.SetParent(parent, false);
            for (int m = 0; m < modules; m++)
            {
                var state = forced ?? Roll(rng, damage);
                states?.Add(state);
                Vector3 moduleStart = start + dir * (m * Module);
                // The crenel's sill runs the whole module; the merlon stands on the first 1.2 m of it.
                Courses(g, moduleStart + dir * 0.0f, dir, outward, Module, SillHeight, rng, 0f);
                float merlonTop = state == Merlon.Whole ? MerlonHeight : state == Merlon.Chipped ? MerlonHeight - Course * 0.5f : state == Merlon.Stub ? SillHeight + Course : SillHeight;
                if (merlonTop > SillHeight)
                {
                    Courses(g, moduleStart + dir * 0.4f + Vector3.up * SillHeight, dir, outward, MerlonWidth, merlonTop - SillHeight, rng, state == Merlon.Chipped ? 0.35f : 0f);
                }

                if (scatter && (state == Merlon.Stub || state == Merlon.Gone))
                {
                    // The fallen stones: most onto the walk, some over the edge.
                    int count = 2 + rng.Next(3);
                    for (int i = 0; i < count; i++)
                    {
                        bool over = rng.NextDouble() < 0.4;
                        Vector3 at = moduleStart + dir * (0.3f + (float)rng.NextDouble() * 1.6f);
                        at += over ? outward * (1.2f + (float)rng.NextDouble() * 2.2f) : -outward * (0.7f + (float)rng.NextDouble() * 1.8f);
                        if (over)
                        {
                            at.y = fallY;
                        }

                        Fallen(g, at, rng);
                    }
                }
            }
        }

        // A low parapet (0.9 m) of blocks along 'dir'; damage drops, tilts and lowers blocks.
        public static void ParapetRun(Transform parent, Vector3 start, Vector3 dir, Vector3 outward, float length, int seed, float damage, float depth = MerlonDepth, float height = SillHeight)
        {
            var rng = new System.Random(seed);
            var g = new GameObject("ParapetRun").transform;
            g.SetParent(parent, false);
            Courses(g, start, dir, outward, length, height, rng, damage, depth);
        }

        // ---------------------------------------------------------------- Gate arch

        // A gateway in a wall: two piers, a segmental arch of voussoirs (a larger keystone at the crown), the
        // haunches filled in courses up to the wall top. Built in the wall's local frame: the opening is centred
        // on x 0, the wall runs along x, its thickness along z (centred on 0), ground at y 0.
        public static void GateArch(Transform parent, float span, float pier, float springY, float rise, float height, float thickness, int seed, bool arch = true)
        {
            var rng = new System.Random(seed);
            var g = new GameObject("GateArch").transform;
            g.SetParent(parent, false);
            float half = span * 0.5f;
            float radius = (half * half + rise * rise) / (2f * rise);
            float centreY = springY + rise - radius;
            float halfWidth = half + pier;

            // Piers: courses from the ground to the springing, either side.
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side < 0 ? -halfWidth : half;
                Courses(g, new Vector3(x0, 0f, -thickness * 0.5f), Vector3.right, Vector3.back, pier, springY, rng, 0f, thickness);
            }

            if (arch == false)
            {
                return;   // a bay whose arch has fallen: the piers alone
            }

            // Haunches and the wall over the crown: each course filled from the arch's extrados out to the edge.
            int courses = Mathf.CeilToInt((height - springY) / Course);
            for (int c = 0; c < courses; c++)
            {
                float y = springY + c * Course;
                float ch = Mathf.Min(Course, height - y);
                float inner = OpeningHalfWidth(y, radius, centreY, half);
                foreach (float side in new[] { -1f, 1f })
                {
                    float from = Mathf.Min(inner + 0.55f, halfWidth);
                    if (y >= springY + rise)
                    {
                        from = 0f;
                    }

                    float length = halfWidth - from;
                    if (length < 0.1f)
                    {
                        continue;
                    }

                    float startX = side < 0 ? -halfWidth : from;
                    if (from == 0f && side > 0)
                    {
                        continue;   // above the crown one course spans the whole width
                    }

                    if (from == 0f)
                    {
                        length = halfWidth * 2f;
                    }

                    var row = new GameObject("Course").transform;
                    row.SetParent(g, false);
                    row.localPosition = new Vector3(0f, y, 0f);
                    Courses(row, new Vector3(startX, 0f, -thickness * 0.5f), Vector3.right, Vector3.back, length, ch, rng, 0f, thickness);
                }
            }

            // Voussoirs: true wedge stones between radial joints, about 0.55 m along the intrados, a deeper keystone
            // standing a little proud at the crown.
            float angle = Mathf.Asin(half / radius);
            float arc = 2f * angle * radius;
            int count = Mathf.Max(5, Mathf.RoundToInt(arc / 0.55f) | 1);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float gap = 0.012f / radius;
            for (int i = 0; i < count; i++)
            {
                float a0 = -angle + i * 2f * angle / count + gap, a1 = -angle + (i + 1) * 2f * angle / count - gap;
                bool key = i == count / 2;
                float r1 = radius + (key ? 0.95f : 0.7f + (float)rng.NextDouble() * 0.12f);
                float hz = thickness * 0.5f + (key ? 0.08f : 0.02f);
                int face = rng.NextDouble() < 0.35 ? 1 : rng.NextDouble() < 0.5 ? 0 : 2;
                Wedge(verts, uvs, tris, new Vector3(0f, centreY, 0f), radius, r1, a0, a1, hz, face);
            }

            // Backing: the voussoir joints and the seam between the ring and the haunch courses open through the
            // wall; a core set back from both faces, in strips that follow the arch just outside its intrados.
            var backing = new L3Mesh();
            for (float y = springY; y < height - 1e-3f; y += 0.15f)
            {
                float strip = Mathf.Min(0.15f, height - y);
                float open = OpeningHalfWidth(y, radius, centreY, half);
                var size = new Vector3(0f, strip, thickness - 0.08f);
                if (open <= 0f)
                {
                    size.x = halfWidth * 2f;
                    L4Shapes.Box(backing, 0, new Vector3(0f, y + strip * 0.5f, 0f), Quaternion.identity, size, Cell(L2PolyKit.Joint));
                    continue;
                }

                float from = open + 0.03f;
                if (from >= halfWidth)
                {
                    continue;
                }

                size.x = halfWidth - from;
                foreach (float side in new[] { -1f, 1f })
                {
                    L4Shapes.Box(backing, 0, new Vector3(side * (from + halfWidth) * 0.5f, y + strip * 0.5f, 0f), Quaternion.identity, size, Cell(L2PolyKit.Joint));
                }
            }

            L3Build.MeshChild(g, "Backing", backing.ToMesh("ArchBacking"), Vector3.zero, Stone);

            var mesh = new Mesh { name = "Voussoirs" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            L3Build.MeshChild(g, "Voussoirs", mesh, Vector3.zero, Stone);
        }

        // One wedge stone: intrados radius r0 to extrados r1 between angles a0 and a1 (measured from straight up),
        // thickness 2*hz along z. Flat-shaded quads; faces pick a palette cell (front/back 'face', soffit Side,
        // joints End).
        private static void Wedge(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 c, float r0, float r1, float a0, float a1, float hz, int face)
        {
            Vector3 P(float r, float a, float z) => c + new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r, z);
            float mid = (a0 + a1) * 0.5f;
            var radial = new Vector3(Mathf.Sin(mid), Mathf.Cos(mid), 0f);
            Quad(v, uv, tri, P(r0, a0, -hz), P(r0, a1, -hz), P(r1, a1, -hz), P(r1, a0, -hz), Vector3.back, face);
            Quad(v, uv, tri, P(r0, a0, hz), P(r0, a1, hz), P(r1, a1, hz), P(r1, a0, hz), Vector3.forward, face);
            Quad(v, uv, tri, P(r0, a0, -hz), P(r0, a1, -hz), P(r0, a1, hz), P(r0, a0, hz), -radial, 3);
            Quad(v, uv, tri, P(r1, a0, -hz), P(r1, a1, -hz), P(r1, a1, hz), P(r1, a0, hz), radial, 0);
            Quad(v, uv, tri, P(r0, a0, -hz), P(r1, a0, -hz), P(r1, a0, hz), P(r0, a0, hz), new Vector3(-Mathf.Cos(a0), Mathf.Sin(a0), 0f), 4);
            Quad(v, uv, tri, P(r0, a1, -hz), P(r1, a1, -hz), P(r1, a1, hz), P(r0, a1, hz), new Vector3(Mathf.Cos(a1), -Mathf.Sin(a1), 0f), 4);
        }

        // A quad facing 'outward' (the winding is flipped to match), all four corners on one palette cell.
        private static void Quad(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward, int cell)
        {
            int i = v.Count;
            v.Add(p0);
            v.Add(p1);
            v.Add(p2);
            v.Add(p3);
            var cellUv = new Vector2((cell + 0.5f) / 8f, 0.5f);
            for (int k = 0; k < 4; k++)
            {
                uv.Add(cellUv);
            }

            bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) < 0f;
            if (flip)
            {
                tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            else
            {
                tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }

        private static float OpeningHalfWidth(float y, float radius, float centreY, float half)
        {
            float dy = y - centreY;
            return dy >= radius ? 0f : Mathf.Min(half, Mathf.Sqrt(radius * radius - dy * dy));
        }

        private static Merlon Roll(System.Random rng, float damage)
        {
            double r = rng.NextDouble();
            if (r < damage * 0.18)
            {
                return Merlon.Gone;
            }

            if (r < damage * 0.42)
            {
                return Merlon.Stub;
            }

            return r < damage * 0.75 ? Merlon.Chipped : Merlon.Whole;
        }

        // Courses of blocks filling a slab 'length' long (along dir), MerlonDepth deep (into -outward from the
        // line through 'start'), 'height' high. Joints step from course to course. 'loss' knocks blocks out of the
        // top course (and tilts a few) so the top edge is ragged.
        public static void Courses(Transform g, Vector3 start, Vector3 dir, Vector3 outward, float length, float height, System.Random rng, float loss, float depth = MerlonDepth, float widthScale = 1f)
        {
            int courses = Mathf.Max(1, Mathf.RoundToInt(height / Course));
            float ch = height / courses;
            var rot = Quaternion.LookRotation(outward, Vector3.up);   // block width (x) along the run, depth (z) outward
            for (int c = 0; c < courses; c++)
            {
                bool top = c == courses - 1;
                float x = 0f;
                float first = c % 2 == 0 ? 0.35f + (float)rng.NextDouble() * 0.25f : 0.6f + (float)rng.NextDouble() * 0.25f;
                while (x < length - 0.05f)
                {
                    // Each block is split into two or three stones (Tread), so walls seen face-on want widthScale ~2.
                    float w = (x == 0f ? first : 0.5f + (float)rng.NextDouble() * 0.4f) * widthScale;
                    w = Mathf.Min(w, length - x);
                    if (top && rng.NextDouble() < loss * 0.6)
                    {
                        x += w;
                        continue;
                    }

                    var size = new Vector3(w - 0.02f, ch - 0.01f, depth);
                    Vector3 centre = start + dir * (x + w * 0.5f) + Vector3.up * (c * ch + ch * 0.5f) - outward * (depth * 0.5f);
                    var block = L3Build.MeshChild(g, "Block", L2PolyKit.Tread(size, rng.Next(100000)), centre, Stone);
                    var tilt = top && rng.NextDouble() < loss ? Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 14f, ((float)rng.NextDouble() - 0.5f) * 10f, ((float)rng.NextDouble() - 0.5f) * 14f) : Quaternion.identity;
                    block.transform.localRotation = rot * tilt;
                    x += w;
                }
            }

            // The mortar core: the blocks' joints run right through the slab, so without it the wall shows daylight
            // at every joint seen face-on. Set back from both faces; where the top is ragged it shows as rubble fill.
            float coreHeight = loss > 0f ? (courses - 1) * ch + ch * 0.4f : height;
            var coreCentre = start + dir * (length * 0.5f) + Vector3.up * (coreHeight * 0.5f) - outward * (depth * 0.5f);
            Core(g, coreCentre, rot, new Vector3(length, coreHeight, depth - 0.08f));
        }

        // A dark box in the joint colour, centred on 'centre' in g's frame.
        private static void Core(Transform g, Vector3 centre, Quaternion rot, Vector3 size)
        {
            if (size.x < 0.05f || size.y < 0.05f || size.z < 0.05f)
            {
                return;
            }

            var m = new L3Mesh();
            L4Shapes.Box(m, 0, Vector3.zero, Quaternion.identity, size, Cell(L2PolyKit.Joint));
            var core = L3Build.MeshChild(g, "Core", m.ToMesh("Core"), centre, Stone);
            core.transform.localRotation = rot;
        }

        // A floor of worn flags laid in courses, 'size' x by z, 'thickness' deep, its top at y 0 under 'parent'.
        public static void Flags(Transform parent, Vector2 size, float thickness, int seed)
        {
            var rng = new System.Random(seed);
            float z = -size.y * 0.5f;
            while (z < size.y * 0.5f - 0.05f)
            {
                float d = Mathf.Min(0.6f + (float)rng.NextDouble() * 0.35f, size.y * 0.5f - z);
                float x = -size.x * 0.5f;
                while (x < size.x * 0.5f - 0.05f)
                {
                    float w = Mathf.Min(0.7f + (float)rng.NextDouble() * 0.6f, size.x * 0.5f - x);
                    var flag = L3Build.MeshChild(parent, "Flag", L2PolyKit.Tread(new Vector3(w - 0.03f, thickness, d - 0.03f), rng.Next(100000)), new Vector3(x + w * 0.5f, -thickness * 0.5f, z + d * 0.5f), Stone);
                    flag.transform.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 1.2f, 0f, ((float)rng.NextDouble() - 0.5f) * 1.2f);
                    x += w;
                }

                z += d;
            }

            Core(parent, new Vector3(0f, -thickness * 0.5f - 0.02f, 0f), Quaternion.identity, new Vector3(size.x, thickness - 0.04f, size.y));
        }

        // A fallen block lying where it landed, half on its side.
        public static GameObject Fallen(Transform parent, Vector3 at, System.Random rng)
        {
            var size = new Vector3(0.45f + (float)rng.NextDouble() * 0.4f, Course - 0.02f, MerlonDepth - 0.05f);
            var block = L3Build.MeshChild(parent, "Fallen", L2PolyKit.Tread(size, rng.Next(100000)), at + Vector3.up * size.y * 0.35f, Stone);
            block.transform.rotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 40f, rng.Next(360), ((float)rng.NextDouble() - 0.5f) * 50f);
            return block;
        }
    }
}
