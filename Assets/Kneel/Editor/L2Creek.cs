using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The creek (L2Layout.Creek) and the timber bridge that carries the road over it: a flush deck collider the
    // road runs onto without a step (the bed is carved under it), the water itself (a strip following the bed,
    // murky, with soft ripples flowing downstream: Kneel/Water), the culvert it springs from at the foot of the
    // churchyard, reeds and stones along the banks.
    public static class L2Creek
    {
        private const string TexturesPath = L2Build.MaterialsPath + "/Textures";

        // Greybox step: the deck the road crosses on (walkable, on the Ground layer under Terrain).
        public static string BuildDeck(Transform root)
        {
            var terrain = L2Build.Group(root, "Terrain", false);
            var old = terrain.Find("BridgeDeck");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var deck = new GameObject("BridgeDeck");
            deck.transform.SetParent(terrain, false);
            Vector2 c = L2Layout.BridgeCenter;
            Vector2 d = L2Layout.BridgeDirection;
            float road = L2Layout.Height(c);
            deck.transform.SetPositionAndRotation(new Vector3(c.x, road - 0.15f, c.y), Quaternion.Euler(0f, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0f));
            deck.AddComponent<BoxCollider>().size = new Vector3(L2Layout.BridgeWidth, 0.3f, L2Layout.BridgeLength);
            deck.layer = LayerMask.NameToLayer("Ground");
            return "bridge deck";
        }

        // Dressing step.
        public static string Dress(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Creek", true);
            Water(group);
            int bridge = Bridge(group);
            Culvert(group);
            int banks = Banks(group);
            return $"creek: water, bridge ({bridge} pieces), culvert, {banks} bank pieces";
        }

        // ---------------------------------------------------------------- Water

        private static void Water(Transform group)
        {
            // A grid of half-metre cells over every spot within the strip's reach of the creek's centre line, each
            // vertex at the water level there. A grid never folds over itself at the bends the way a ribbon does,
            // and the shader lays its ripples in world space, so the surface has no seams. It reaches well up
            // under the banks: the shader fades the waterline on depth, so the bank decides the shore.
            const float cell = 0.5f;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var c in L2Layout.Creek)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minZ = Mathf.Min(minZ, c.y); maxZ = Mathf.Max(maxZ, c.y);
            }

            float reach = WaterStripHalfWidth + cell;
            int nx = Mathf.CeilToInt((maxX - minX + 2f * reach) / cell), nz = Mathf.CeilToInt((maxZ - minZ + 2f * reach) / cell);
            float x0 = minX - reach, z0 = minZ - reach;
            var distance = new float[nx + 1, nz + 1];
            var index = new int[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            {
                for (int j = 0; j <= nz; j++)
                {
                    distance[i, j] = L2Layout.CreekDistance(new Vector2(x0 + i * cell, z0 + j * cell), out _);
                    index[i, j] = -1;
                }
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var centre = new List<Vector2>();
            var tris = new List<int>();
            int Vertex(int i, int j)
            {
                if (index[i, j] < 0)
                {
                    var q = new Vector2(x0 + i * cell, z0 + j * cell);
                    L2Layout.CreekDistance(q, out float along);
                    index[i, j] = verts.Count;
                    verts.Add(new Vector3(q.x, L2Layout.WaterLevel(along), q.y));
                    uvs.Add(q);
                    centre.Add(new Vector2(distance[i, j], 0f));
                }

                return index[i, j];
            }

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float nearest = Mathf.Min(Mathf.Min(distance[i, j], distance[i + 1, j]), Mathf.Min(distance[i, j + 1], distance[i + 1, j + 1]));
                    if (nearest > WaterStripHalfWidth)
                    {
                        continue;
                    }

                    int a = Vertex(i, j), b = Vertex(i + 1, j), c = Vertex(i, j + 1), d = Vertex(i + 1, j + 1);
                    tris.AddRange(new[] { a, c, b, b, c, d });   // clockwise seen from above
                }
            }

            // Rewrite the asset's own data in place (copying a new mesh over it can leave stale GPU buffers).
            string meshPath = L2Build.MeshesPath + "/L2_Creek_Water.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null)
            {
                existing = new Mesh { name = "L2_Creek_Water" };
                AssetDatabase.CreateAsset(existing, meshPath);
            }

            existing.Clear();
            existing.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            existing.SetVertices(verts);
            existing.SetUVs(0, uvs);
            existing.SetUVs(1, centre);
            existing.SetTriangles(tris, 0);
            existing.SetNormals(Enumerable.Repeat(Vector3.up, verts.Count).ToList());
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);

            var go = new GameObject("Water");
            go.transform.SetParent(group, false);
            go.AddComponent<MeshFilter>().sharedMesh = existing;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = WaterMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        private const float WaterStripHalfWidth = 3f;

        private static Material WaterMaterial()
        {
            string normalPath = TexturesPath + "/L2_Water_Normal.png";
            const int size = 256;
            // Tileable soft ripples: a few dozen gentle waves at whole-number frequencies in random directions,
            // longer along the flow (v) than across it, amplitude falling with frequency so no crest is sharp.
            var rng = new System.Random(17);
            var waves = new List<Vector4>();
            for (int i = 0; i < 28; i++)
            {
                int fx = rng.Next(-6, 7), fy = rng.Next(-4, 5);
                if (fx == 0 && fy == 0)
                {
                    continue;
                }

                float f = Mathf.Sqrt(fx * fx + fy * fy);
                waves.Add(new Vector4(fx, fy, (float)rng.NextDouble() * Mathf.PI * 2f, 1f / Mathf.Pow(f, 1.4f)));
            }

            var heights = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f, w = y / (float)size * Mathf.PI * 2f, h = 0f;
                    foreach (var wave in waves)
                    {
                        h += Mathf.Sin(u * wave.x + w * wave.y + wave.z) * wave.w;
                    }

                    heights[y * size + x] = h;
                }
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float hl = heights[y * size + (x - 1 + size) % size], hr = heights[y * size + (x + 1) % size];
                    float hd = heights[((y - 1 + size) % size) * size + x], hu = heights[((y + 1) % size) * size + x];
                    var n = new Vector3((hl - hr) * 3f, (hd - hu) * 3f, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(normalPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();

            string path = L2Build.MaterialsPath + "/L2_Creek_Water.mat";
            var shader = Shader.Find("Kneel/Water");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            mat.shaderKeywords = new string[0];
            mat.renderQueue = -1;
            mat.SetTexture("_RippleMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            mat.SetFloat("_HalfWidth", WaterStripHalfWidth);
            // Slow, heavy water: faint ripples and a soft sheen, so lights smear into long glints, not sparkles.
            mat.SetFloat("_RippleStrength", 0.085f);
            mat.SetFloat("_Smoothness", 0.88f);
            mat.SetFloat("_Reflection", 0.5f);
            mat.SetFloat("_Glint", 0.16f);
            mat.SetColor("_DeepColor", new Color(0.1f, 0.105f, 0.075f));   // peat-brown murk, a hair of green
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Bridge

        // A heavy timber trestle on stone abutments: the road runs onto a plank deck carried by three stringers,
        // which rest on the abutments at either bank and on two braced trestles standing in the water. A post-and-
        // rail fence with diagonal braces runs along both sides, and a lantern sits on the first post at each end,
        // its glow lying in the water below. The war has passed over it: scorched boards, a charred rail.
        private static int Bridge(Transform group)
        {
            var bridge = new GameObject("Bridge").transform;
            bridge.SetParent(group, false);
            Vector2 c = L2Layout.BridgeCenter;
            Vector2 d = L2Layout.BridgeDirection;
            float road = L2Layout.Height(c);
            bridge.SetPositionAndRotation(new Vector3(c.x, road, c.y), Quaternion.Euler(0f, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0f));
            var rng = new System.Random(48);
            float half = L2Layout.BridgeLength * 0.5f, width = L2Layout.BridgeWidth, edge = width * 0.5f;
            L2Layout.CreekDistance(c, out float along);
            float bed = L2Layout.WaterLevel(along) - 0.7f - road;   // local height of the creek bed
            int n = 0;

            // Abutments: a dry-stone wall across each bank under the deck ends, its top flush with the road.
            foreach (float end in new[] { -1f, 1f })
            {
                foreach (float x in new[] { -1.7f, 1.7f })
                {
                    var wall = L2Build.Spawn("K/Buildings/SM_Bld_Rockwall_Straight_01", bridge, new Vector3(x, -2.75f, end * (half - 0.55f)), new Vector3(0f, 90f + end * 2f, 0f), 1f, "Abutment");
                    L2Village.Burnt(wall);
                    L2Dressing.Finish(wall, L1Build.EnvironmentStatic, false);
                    n++;
                }
            }

            // Stringers, end to end, under the boards.
            foreach (float x in new[] { -edge + 0.45f, 0f, edge - 0.45f })
            {
                BeamBetween(bridge, new Vector3(x, -0.27f, -half + 0.1f), new Vector3(x, -0.27f, half - 0.1f), 1.3f);
                n++;
            }

            // Trestles in the water: four posts on the bed under a cap beam, the outer bays cross-braced.
            foreach (float z in new[] { -1.55f, 1.55f })
            {
                float[] xs = { -edge + 0.45f, -0.75f, 0.75f, edge - 0.45f };
                foreach (float x in xs)
                {
                    BeamBetween(bridge, new Vector3(x, bed, z), new Vector3(x, -0.45f, z), 1.15f);
                    n++;
                }

                BeamBetween(bridge, new Vector3(-edge + 0.2f, -0.55f, z), new Vector3(edge - 0.2f, -0.55f, z), 1.25f);
                BeamBetween(bridge, new Vector3(xs[0], bed + 0.3f, z), new Vector3(xs[1], -0.65f, z), 0.7f);
                BeamBetween(bridge, new Vector3(xs[3], bed + 0.3f, z), new Vector3(xs[2], -0.65f, z), 0.7f);
                n += 3;
            }

            // Boards across the deck: random widths with thin gaps, ragged ends, the odd one askew, sunk a
            // little or scorched.
            for (float z = -half + 0.2f; z < half - 0.15f;)
            {
                float board = 0.34f + (float)rng.NextDouble() * 0.16f;
                float shift = ((float)rng.NextDouble() - 0.5f) * 0.3f;
                float sink = rng.NextDouble() < 0.15 ? -0.025f : 0f;
                var plank = Board(bridge, new Vector3(shift, -0.05f + sink, z + board * 0.5f), new Vector3(width - 0.1f - Mathf.Abs(shift), 0.09f, board),
                    ((float)rng.NextDouble() - 0.5f) * 2.5f, rng.Next(1000));
                if (rng.NextDouble() < 0.1)
                {
                    plank.GetComponent<MeshRenderer>().sharedMaterial = L2PolyKit.CharredWood;   // the odd scorched board
                }

                z += board + 0.035f;
                n++;
            }

            // Kerb beams along both edges, then the fence: posts bolted to the deck's side, a top and a mid rail,
            // a diagonal brace in every bay.
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (edge - 0.2f);
                BeamBetween(bridge, new Vector3(x, 0.02f, -half + 0.15f), new Vector3(x, 0.02f, half - 0.15f), 0.9f);
                const int bays = 4;
                float span = (L2Layout.BridgeLength - 0.5f) / bays;
                float px = side * (edge - 0.08f);
                for (int k = 0; k <= bays; k++)
                {
                    float z = -half + 0.25f + k * span;
                    bool end = k == 0 || k == bays;
                    BeamBetween(bridge, new Vector3(px, -0.45f, z), new Vector3(px, end ? 1.3f : 1.12f, z), end ? 1.35f : 1.05f);
                    n++;
                    if (k == bays)
                    {
                        continue;
                    }

                    // The east rail took the fire: one bay hangs broken and black.
                    bool broken = side > 0 && k == 2;
                    var rail = BeamBetween(bridge, new Vector3(px, 1.02f, z), new Vector3(px, broken ? 0.35f : 1.02f, z + span), 0.85f);
                    var mid = BeamBetween(bridge, new Vector3(px, 0.54f, z), new Vector3(px, 0.54f, z + span), 0.7f);
                    bool rising = k % 2 == 0;
                    var brace = BeamBetween(bridge, new Vector3(px, 0.1f, rising ? z : z + span), new Vector3(px, 0.98f, rising ? z + span : z), 0.6f);
                    if (broken || (side > 0 && rng.NextDouble() < 0.5))
                    {
                        L2Dressing.Charred(rail, 1f, rng);
                        L2Dressing.Charred(mid, 1f, rng);
                        L2Dressing.Charred(brace, 1f, rng);
                    }

                    n += 3;
                }
            }

            // A lantern on the first post at each end (diagonally opposite), still burning low.
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, 1f) })
            {
                var lantern = L2Build.Spawn("A/Items/SM_Item_Lantern_01", bridge, new Vector3(x * (edge - 0.08f), 1.3f, z * (half - 0.25f)), Vector3.zero, 1f, "Lantern");
                L2Dressing.Finish(lantern, L1Build.PropStatic, false);
                L2Fire.Candle(lantern.transform, "Flame", Vector3.up * 0.22f, 6f, 6f);
                n++;
            }

            L1Build.SetStatic(bridge.gameObject, L1Build.PropStatic);
            L1Build.SetLayer(bridge.gameObject, "Obstacles");
            return n;
        }

        // A deck board: a plain box in one of three weathered-wood tones (the beam prop's texture bands would
        // repeat across a whole deck of stretched beams).
        // A deck board: a faceted low-poly plank (L2PolyKit: grain strips, chamfered edges, nail heads) sized to fit.
        private static GameObject Board(Transform parent, Vector3 local, Vector3 size, float yaw, int seed)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Reboard(go, size, seed, false);
            return go;
        }

        // Gives a board (a new one, or one already in the scene, wherever it was placed) its plank mesh at the given
        // size, keeping its position and rotation.
        public static void Reboard(GameObject board, Vector3 size, int seed, bool charred)
        {
            var filter = board.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = board.AddComponent<MeshFilter>();
            }

            var renderer = board.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = board.AddComponent<MeshRenderer>();
            }
            string name = $"L2_Plank_{Mathf.RoundToInt(size.x * 100f)}x{Mathf.RoundToInt(size.z * 100f)}_{seed}";
            filter.sharedMesh = L2PolyKit.Save(L2PolyKit.Plank(size, seed), name);
            renderer.sharedMaterial = charred ? L2PolyKit.CharredWood : L2PolyKit.Wood;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            board.transform.localScale = Vector3.one;
            foreach (var c in board.GetComponents<Collider>())
            {
                Object.DestroyImmediate(c);
            }
        }

        // A beam (the 2.48 m prop, pivot at one end) laid from a to b in the parent's space, its cross-section
        // scaled by thickness.
        private static GameObject BeamBetween(Transform parent, Vector3 a, Vector3 b, float thickness)
        {
            var go = L2Build.Spawn("K/Props/SM_Prop_Beam_01", parent, a, Quaternion.FromToRotation(Vector3.up, b - a).eulerAngles);
            go.transform.localScale = new Vector3(thickness, (b - a).magnitude / 2.48f, thickness);
            L2Dressing.Finish(go, L1Build.PropStatic, false);
            return go;
        }

        // ---------------------------------------------------------------- Spring and banks

        // The creek springs from a stone culvert under the churchyard: an arch set into the bank, walls either side.
        private static void Culvert(Transform group)
        {
            Vector2 a = L2Layout.Creek[0], b = L2Layout.Creek[1];
            Vector2 dir = (b - a).normalized;
            Vector2 at = a - dir * 0.6f;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            L2Layout.CreekDistance(a, out float along);
            float water = L2Layout.WaterLevel(along);
            var arch = L2Build.Spawn("K/Buildings/SM_Bld_Rockwall_Archway_01", group, Vector3.zero, Vector3.zero, 1f, "Culvert");
            arch.transform.SetPositionAndRotation(new Vector3(at.x, water - 0.5f, at.y), Quaternion.Euler(0f, yaw + 90f, 0f));
            L2Village.Burnt(arch);
            L2Dressing.Finish(arch, L1Build.EnvironmentStatic, false);
            Vector2 side = new Vector2(-dir.y, dir.x);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 p = at + side * s * 2.9f - dir * 0.2f;
                var wall = L2Build.Spawn("K/Buildings/SM_Bld_Rockwall_Straight_01", group, Vector3.zero, Vector3.zero, 1f, "CulvertWall");
                wall.transform.SetPositionAndRotation(new Vector3(p.x, water - 0.5f, p.y), Quaternion.Euler(0f, yaw + 90f, 0f));
                L2Village.Burnt(wall);
                L2Dressing.Finish(wall, L1Build.EnvironmentStatic, false);
            }
        }

        // Reeds standing in the shallows, stones along the water's edge, the odd log washed against a bank.
        private static int Banks(Transform group)
        {
            var rng = new System.Random(77);
            string[] reeds = { "A/Environments/SM_Env_Reeds_01", "A/Environments/SM_Env_Reeds_02", "A/Environments/SM_Env_Reeds_03" };
            string[] stones = { "A/Environments/SM_Env_Rock_01", "A/Environments/SM_Env_Rock_02", "A/Environments/SM_Env_Rock_03", "A/Environments/SM_Env_Pebble_04" };
            int n = 0;
            for (int i = 1; i < L2Layout.Creek.Length; i++)
            {
                Vector2 a = L2Layout.Creek[i - 1], b = L2Layout.Creek[i];
                float length = (b - a).magnitude;
                Vector2 dir = (b - a) / length;
                Vector2 side = new Vector2(-dir.y, dir.x);
                for (float s = 0f; s < length; s += 0.9f)
                {
                    foreach (float bank in new[] { -1f, 1f })
                    {
                        if (rng.NextDouble() < 0.35)
                        {
                            continue;
                        }

                        Vector2 p = Vector2.Lerp(a, b, s / length) + side * bank * (L2Layout.WaterHalfWidth + 0.1f + (float)rng.NextDouble() * 0.9f);
                        if (L2Layout.OnBridge(p, 0.8f) || L2Layout.SignedDistance(p) < 0.6f || p.x > 86f)
                        {
                            continue;
                        }

                        L2Layout.CreekDistance(p, out float along);
                        float water = L2Layout.WaterLevel(along);
                        bool reed = rng.NextDouble() < 0.55;
                        var go = L2Build.Spawn(reed ? reeds[rng.Next(reeds.Length)] : stones[rng.Next(stones.Length)], group, Vector3.zero,
                            new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), reed ? 0.45f + (float)rng.NextDouble() * 0.3f : 0.25f + (float)rng.NextDouble() * 0.35f);
                        go.transform.position = new Vector3(p.x, Mathf.Max(water - 0.15f, L2Layout.TerrainHeight(p) - 0.05f), p.y);
                        if (reed)
                        {
                            // Dry winter reeds in the soot-dark palette: never pale blades under the moon.
                            L2Village.Burnt(go);
                        }
                        else if (rng.NextDouble() < 0.4)
                        {
                            L2Dressing.Charred(go, 1f, rng);
                        }

                        L2Dressing.Finish(go, L1Build.PropStatic, false);
                        n++;
                    }
                }

                // A log washed up against the bank on some stretches.
                if (rng.NextDouble() < 0.4)
                {
                    Vector2 p = Vector2.Lerp(a, b, 0.5f) + side * (L2Layout.WaterHalfWidth - 0.2f);
                    if (!L2Layout.OnBridge(p, 1f) && L2Layout.SignedDistance(p) > 0.6f && p.x < 86f)
                    {
                        L2Layout.CreekDistance(p, out float along);
                        var log = L2Build.Spawn("A/Environments/SM_Env_TreeLog_01", group, Vector3.zero, new Vector3(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 10f, 0f), 0.6f);
                        log.transform.position = new Vector3(p.x, L2Layout.WaterLevel(along) - 0.1f, p.y);
                        L2Dressing.Charred(log, 0.6f, rng);
                        L2Dressing.Finish(log, L1Build.PropStatic, false);
                        n++;
                    }
                }
            }

            return n;
        }
    }
}
