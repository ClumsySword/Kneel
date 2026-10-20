using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Low-poly detail meshes for the hand-built pieces (bridge boards, stair blocks): flat-shaded, faceted shapes
    // whose faces take their colour from a small palette texture, the way the Synty assets do. Texture comes
    // from the geometry (grain strips, chamfers, chips and worn tops), not from a painted map.
    public static class L2PolyKit
    {
        public const string DetailMeshPath = L2Build.MeshesPath + "/Detail";
        private const string TexturesPath = L2Build.MaterialsPath + "/Textures";

        // Palette cells, left to right.
        public static readonly string[] WoodTones = { "#4F4236", "#5B4C3E", "#433830", "#3A3029", "#2C2520", "#665645", "#1C1A19", "#352C25" };
        public static readonly string[] CharredTones = { "#2A2420", "#302823", "#231E1B", "#1D1917", "#141211", "#3A2D24", "#1C1A19", "#251F1B" };
        public static readonly string[] StoneTones = { "#3A3632", "#433E38", "#322E2B", "#2A2724", "#4C463F", "#55504A", "#211F1D", "#3E3934" };

        // Cells by role (same layout in every palette).
        public const int Top = 0, TopLight = 1, TopDark = 2, Side = 3, End = 4, Edge = 5, Iron = 6, Joint = 7;

        public static Material PaletteMaterial(string name, string[] tones, float smoothness)
        {
            L2Build.EnsureFolder(TexturesPath);
            string texPath = TexturesPath + "/" + name + ".png";
            var tex = new Texture2D(tones.Length, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < tones.Length; i++)
            {
                tex.SetPixel(i, 0, L1Build.Hex(tones[i]));
            }

            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            string path = L2Build.MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material Wood => AssetDatabase.LoadAssetAtPath<Material>(L2Build.MaterialsPath + "/L2_Palette_Wood.mat") ?? PaletteMaterial("L2_Palette_Wood", WoodTones, 0.1f);
        public static Material CharredWood => AssetDatabase.LoadAssetAtPath<Material>(L2Build.MaterialsPath + "/L2_Palette_WoodCharred.mat") ?? PaletteMaterial("L2_Palette_WoodCharred", CharredTones, 0.06f);
        public static Material Stone => AssetDatabase.LoadAssetAtPath<Material>(L2Build.MaterialsPath + "/L2_Palette_Stone.mat") ?? PaletteMaterial("L2_Palette_Stone", StoneTones, 0.14f);

        public static void RebuildMaterials()
        {
            PaletteMaterial("L2_Palette_Wood", WoodTones, 0.1f);
            PaletteMaterial("L2_Palette_WoodCharred", CharredTones, 0.06f);
            PaletteMaterial("L2_Palette_Stone", StoneTones, 0.14f);
        }

        // ---------------------------------------------------------------- Builder

        private class Builder
        {
            public readonly List<Vector3> Verts = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Tris = new List<int>();
            private readonly int cells;
            private readonly Vector3 centre;

            public Builder(int cells, Vector3 centre)
            {
                this.cells = cells;
                this.centre = centre;
            }

            // A flat-shaded triangle facing away from the shape's centre (or along 'outward' when given).
            public void Tri(Vector3 a, Vector3 b, Vector3 c, int cell, Vector3 outward = default)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 o = outward == default ? (a + b + c) / 3f - centre : outward;
                if (Vector3.Dot(n, o) < 0f)
                {
                    (b, c) = (c, b);
                }

                var uv = new Vector2((cell + 0.5f) / cells, 0.5f);
                int i = Verts.Count;
                Verts.Add(a);
                Verts.Add(b);
                Verts.Add(c);
                Uvs.Add(uv);
                Uvs.Add(uv);
                Uvs.Add(uv);
                Tris.Add(i);
                Tris.Add(i + 1);
                Tris.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int cell, Vector3 outward = default)
            {
                Tri(a, b, c, cell, outward);
                Tri(a, c, d, cell, outward);
            }

            // A chamfered block from min to max: the top edges bevelled by 'bevel', its four top corners raised or
            // sunk by up to 'wear', its top split into two facets. No bottom face.
            public void Block(Vector3 min, Vector3 max, float bevel, float wear, System.Random rng, int topCell, int sideCell, int edgeCell)
            {
                float J() => ((float)rng.NextDouble() - 0.5f) * 2f * wear;
                Vector3 c = (min + max) * 0.5f;
                float y0 = min.y, y1 = max.y - bevel;
                // Outer ring at the chamfer's foot, inner ring on top.
                var lo = new[] { new Vector3(min.x, y1 + J() * 0.5f, min.z), new Vector3(max.x, y1 + J() * 0.5f, min.z), new Vector3(max.x, y1 + J() * 0.5f, max.z), new Vector3(min.x, y1 + J() * 0.5f, max.z) };
                var hi = new[]
                {
                    new Vector3(min.x + bevel, max.y + J(), min.z + bevel), new Vector3(max.x - bevel, max.y + J(), min.z + bevel),
                    new Vector3(max.x - bevel, max.y + J(), max.z - bevel), new Vector3(min.x + bevel, max.y + J(), max.z - bevel),
                };
                var bottom = new[] { new Vector3(min.x, y0, min.z), new Vector3(max.x, y0, min.z), new Vector3(max.x, y0, max.z), new Vector3(min.x, y0, max.z) };
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    Vector3 outward = new Vector3((lo[i].x + lo[j].x) * 0.5f - c.x, 0f, (lo[i].z + lo[j].z) * 0.5f - c.z);
                    Quad(bottom[i], bottom[j], lo[j], lo[i], sideCell, outward);
                    Quad(lo[i], lo[j], hi[j], hi[i], edgeCell, outward + Vector3.up * outward.magnitude);
                }

                // The top: two facets split along a random diagonal (worn unevenly, never a regular pattern).
                if (rng.NextDouble() < 0.5)
                {
                    Tri(hi[0], hi[1], hi[2], topCell, Vector3.up);
                    Tri(hi[0], hi[2], hi[3], Alternate(topCell), Vector3.up);
                }
                else
                {
                    Tri(hi[0], hi[1], hi[3], topCell, Vector3.up);
                    Tri(hi[1], hi[2], hi[3], Alternate(topCell), Vector3.up);
                }
            }

            // The neighbouring tone, so the facets of one top read as separate planes.
            private static int Alternate(int cell)
            {
                return cell == Top ? TopLight : cell == TopLight || cell == TopDark ? Top : cell;
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(Verts);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(Tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ---------------------------------------------------------------- Shapes

        // A deck board centred on the origin: length along x, thickness y, width z. The top is three grain strips at
        // slightly different heights (each a little shorter or longer at the ends), the long edges chamfered, nail
        // heads over the three stringers.
        public static Mesh Plank(Vector3 size, int seed)
        {
            var rng = new System.Random(seed);
            var b = new Builder(WoodTones.Length, Vector3.zero);
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            const int strips = 3;
            for (int s = 0; s < strips; s++)
            {
                float z0 = -hz + size.z * s / strips, z1 = -hz + size.z * (s + 1) / strips;
                float x0 = -hx + (float)rng.NextDouble() * 0.06f, x1 = hx - (float)rng.NextDouble() * 0.06f;
                float top = hy + ((float)rng.NextDouble() - 0.5f) * 0.012f;
                int cell = rng.NextDouble() < 0.33 ? TopLight : rng.NextDouble() < 0.5 ? Top : TopDark;
                b.Block(new Vector3(x0, -hy, z0), new Vector3(x1, top, z1), s == 0 || s == strips - 1 ? 0.012f : 0.004f, 0.004f, rng, cell, Side, Edge);
            }

            foreach (float x in new[] { -hx + 0.45f, 0f, hx - 0.45f })
            {
                foreach (float z in new[] { -hz * 0.55f, hz * 0.55f })
                {
                    var c = new Vector3(x + ((float)rng.NextDouble() - 0.5f) * 0.04f, hy + 0.004f, z);
                    b.Block(c - new Vector3(0.018f, 0.008f, 0.018f), c + new Vector3(0.018f, 0.008f, 0.018f), 0.004f, 0f, rng, Iron, Iron, Iron);
                }
            }

            return b.Build("Plank");
        }

        // A stair tread: two or three worn stone blocks across its width (thin dark joints between them), each
        // chamfered and chipped, its top split into facets. Centred on the origin: width along x, height y, depth z.
        public static Mesh Tread(Vector3 size, int seed)
        {
            var rng = new System.Random(seed);
            var b = new Builder(StoneTones.Length, Vector3.zero);
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            int blocks = 2 + rng.Next(2);
            var cuts = new List<float> { -hx };
            for (int i = 1; i < blocks; i++)
            {
                cuts.Add(-hx + size.x * (i + ((float)rng.NextDouble() - 0.5f) * 0.5f) / blocks);
            }

            cuts.Add(hx);
            for (int i = 0; i < blocks; i++)
            {
                float x0 = cuts[i] + (i == 0 ? 0f : 0.015f), x1 = cuts[i + 1] - (i == blocks - 1 ? 0f : 0.015f);
                float top = hy + ((float)rng.NextDouble() - 0.5f) * 0.03f;
                float front = -hz + (float)rng.NextDouble() * 0.03f;
                int cell = rng.NextDouble() < 0.35 ? TopLight : rng.NextDouble() < 0.5 ? Top : TopDark;
                b.Block(new Vector3(x0, -hy, front), new Vector3(x1, top, hz), 0.035f, 0.018f, rng, cell, Side, Edge);
            }

            // The joints, a shade darker, sunk between the blocks.
            for (int i = 1; i < blocks; i++)
            {
                b.Block(new Vector3(cuts[i] - 0.016f, -hy, -hz + 0.02f), new Vector3(cuts[i] + 0.016f, hy - 0.03f, hz - 0.01f), 0f, 0f, rng, Joint, Joint, Joint);
            }

            return b.Build("Tread");
        }

        // Saves (or overwrites in place) a generated mesh asset.
        public static Mesh Save(Mesh mesh, string name)
        {
            L2Build.EnsureFolder(DetailMeshPath);
            string path = DetailMeshPath + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetUVs(0, mesh.uv);
            existing.SetTriangles(mesh.triangles, 0);
            existing.SetNormals(mesh.normals);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }
    }
}
