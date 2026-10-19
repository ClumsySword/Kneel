using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Phase 3 (refinement): the ground the stronghold stands on, so it no longer floats in fog (owner, 2026-10-19:
    // "something along the edges of the stronghold so it does not look like the stronghold is floating in an
    // abyss"). The note's setting: the causeway crosses a dry ravine, smoke rises out of it, the foreign siege camp
    // lies beyond it.
    //
    // One low-poly heightfield, flat-shaded from a land palette like the packs: a rock shelf round the wall feet,
    // cliffs down to a dry ravine floor with scree at their feet, banks rising to rolling burnt land, the south bank
    // meeting the causeway's end with the road running on south. Pack rocks and cliff pieces break up the cliff
    // feet. Nothing here has a collider: a fall into the ravine still ends in the kill volume, and nothing out
    // here is walkable or baked. Rebuilding replaces only this group.
    public static class L4Surroundings
    {
        public const string GroupName = "Surroundings";

        private const float Cell = 2f;
        private const float MinX = -110f, MaxX = 230f, MinZ = -100f, MaxZ = 420f;
        private const int Tile = 32;

        // The ravine floor, and the causeway's line (its south end lands on the bank at y -10).
        public const float FloorY = -15f;
        private const float CausewayX = 60f, CausewaySouth = -10f;

        // Land palette: rock light, mid, dark; scree; ravine earth; burnt earth; dry grass; ash; then L3's fields
        // (refinement: the farmland the player comes from) ripe wheat, stubble, pasture, ploughed earth.
        private static readonly string[] Tones = { "#6A6359", "#57504A", "#433E39", "#4A433B", "#352F2A", "#433A31", "#4B483A", "#2E2B28", "#6C5C36", "#5A5036", "#4B4C31", "#3E3226" };

        public enum Field { None, Wheat, Stubble, Pasture, Ploughed }

        // How much the land at p is still L3's farmland (owner, 2026-10-19: "a smooth transition from lvl 3 Farmland
        // ... no hard lines"): south of the causeway's bank only, rising over 40 m away from the ravine, its edge
        // broken up by noise so fields give way to burnt land in ragged tongues, never along a line.
        public static float Farm(Vector2 p)
        {
            float south = Mathf.InverseLerp(-8f, -48f, p.y);
            float ragged = (Fbm(p.x * 0.06f + 70f, p.y * 0.06f) - 0.5f) * 0.9f;
            return Mathf.Clamp01(south + ragged);
        }

        // The patchwork: fields about 22 x 14 m on a grid turned a little off the road, each wheat, stubble, pasture
        // or plough; the track itself is no field.
        public static Field FieldAt(Vector2 p)
        {
            if (Mathf.Abs(p.x - CausewayX) < 5f || Farm(p) < 0.5f)
            {
                return Field.None;
            }

            var q = Quaternion.Euler(0f, 0f, 12f) * new Vector3(p.x, p.y, 0f);
            int i = Mathf.FloorToInt(q.x / 22f), j = Mathf.FloorToInt(q.y / 14f);
            int h = Mathf.Abs((i * 73856093) ^ (j * 19349663)) % 7;
            return h < 3 ? Field.Wheat : h < 4 ? Field.Stubble : h < 6 ? Field.Pasture : Field.Ploughed;
        }

        private static List<Rect> castle;

        [MenuItem("Kneel/L4/Build/Surroundings (replaces that group)")]
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
            castle = Footprint();
            int tiles = Ground(g);
            int rocks = Rocks(g);
            SettleCamp(root);
            return GroupName + ": " + tiles + " ground tiles, " + rocks + " rocks";
        }

        // ---------------------------------------------------------------- The shape

        // What the crag carries: every floor of the plan, and the throne hall's rock up to the keep.
        private static List<Rect> Footprint()
        {
            var rects = new List<Rect>();
            foreach (var f in L4Plan.Floors)
            {
                if (!f.Bridge)
                {
                    rects.Add(Rect.MinMaxRect(f.X0, f.Z0, f.X1, f.Z1));
                }
            }

            // The stairs cut into the outposts' bastions stand inside the crag too.
            foreach (var s in L4Plan.Stairs)
            {
                rects.Add(new Rect(s.Centre.x - 4f, s.Centre.z - 4f, 8f, 8f));
            }

            rects.Add(Rect.MinMaxRect(36f, 262f, 72f, 332f));
            return rects;
        }

        private static float Distance(Vector2 p)
        {
            castle ??= Footprint();
            float best = float.MaxValue;
            foreach (var r in castle)
            {
                float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }

            return best;
        }

        private static float Fbm(float x, float z) => L3Ground.Fbm(x, z);

        // Height of the ground at p. d is the distance from the castle's footprint.
        public static float Height(Vector2 p)
        {
            float d = Distance(p);
            if (d <= 0.01f)
            {
                return -0.6f;
            }

            // The shelf: the curtain's footing stands 2 m out; past it a ragged ledge of rock.
            float shelf = 2.2f + Fbm(p.x * 0.11f, p.y * 0.11f) * 1.6f;
            float cliff = 5f + Fbm(p.x * 0.07f + 13f, p.y * 0.07f) * 5f;
            float floorY = FloorY + (Fbm(p.x * 0.05f + 40f, p.y * 0.05f) - 0.5f) * 2.4f;
            float h;
            if (d < shelf)
            {
                h = -0.6f - d / shelf * 0.9f;
            }
            else if (d < shelf + cliff)
            {
                // The cliff: steep at the top, easing into the talus; strata break it into ledges.
                float t = (d - shelf) / cliff;
                float fall = 1f - Mathf.Pow(1f - t, 1.6f);
                float strata = (Fbm(p.x * 0.3f, p.y * 0.3f + 7f) - 0.5f) * 2.2f * Mathf.Sin(t * Mathf.PI);
                h = Mathf.Lerp(-1.5f, floorY + 3f, fall) + strata;
            }
            else
            {
                // Scree against the cliff foot, thinning out across the floor.
                float talus = 3f * (1f - Mathf.SmoothStep(0f, 1f, (d - shelf - cliff) / 9f));
                h = floorY + talus + (Fbm(p.x * 0.25f, p.y * 0.25f) - 0.5f) * 0.8f;
            }

            // The far bank. Near the causeway it is steep and sits exactly where the causeway ends.
            float nearCauseway = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(12f, 26f, Mathf.Abs(p.x - CausewayX))) * (p.y < 40f ? 1f : 0f);
            float bankAt = Mathf.Lerp(34f + (Fbm(p.x * 0.03f, p.y * 0.03f + 90f) - 0.5f) * 14f, 46f, nearCauseway);
            float rise = Mathf.Lerp(10f, 4f, nearCauseway);
            float land = Land(p, d);
            if (d > bankAt)
            {
                float t = Mathf.Clamp01((d - bankAt) / rise);
                float up = Mathf.Lerp(floorY + 1f, land, 1f - Mathf.Pow(1f - t, 2.2f));
                h = Mathf.Max(h, up + (Fbm(p.x * 0.2f, p.y * 0.2f + 3f) - 0.5f) * 1.2f * Mathf.Sin(t * Mathf.PI));
            }

            return h;
        }

        // Rolling burnt land beyond the ravine, rising slowly into hills with distance. South of the causeway the
        // road runs on at deck height.
        private static float Land(Vector2 p, float d)
        {
            float roll = (Fbm(p.x * 0.02f, p.y * 0.02f) - 0.5f) * 6f + (Fbm(p.x * 0.07f + 3f, p.y * 0.07f) - 0.5f) * 1.6f;
            // The farm's fields lie nearly flat, like L3's plain.
            roll *= Mathf.Lerp(1f, 0.25f, Farm(p));
            float hills = Mathf.Max(0f, d - 80f) * 0.05f;
            float h = 0.6f + roll + hills;
            if (p.y < CausewaySouth + 2f)
            {
                float road = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4.5f, 16f, Mathf.Abs(p.x - CausewayX)));
                h = Mathf.Lerp(-0.08f, h, road);
            }

            return h;
        }

        // ---------------------------------------------------------------- The mesh

        private static int Ground(Transform g)
        {
            var palette = L4Kit.Palette("L4_Palette_Land", System.Array.ConvertAll(Tones, Hex), 0.06f);
            int nx = Mathf.RoundToInt((MaxX - MinX) / Cell), nz = Mathf.RoundToInt((MaxZ - MinZ) / Cell);
            var heights = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            {
                for (int j = 0; j <= nz; j++)
                {
                    heights[i, j] = Height(new Vector2(MinX + i * Cell, MinZ + j * Cell));
                }
            }

            int count = 0;
            var parent = L3Build.Child(g, "Ground").transform;
            for (int ti = 0; ti < nx; ti += Tile)
            {
                for (int tj = 0; tj < nz; tj += Tile)
                {
                    var verts = new List<Vector3>();
                    var uvs = new List<Vector2>();
                    var tris = new List<int>();
                    for (int i = ti; i < Mathf.Min(ti + Tile, nx); i++)
                    {
                        for (int j = tj; j < Mathf.Min(tj + Tile, nz); j++)
                        {
                            Vector3 V(int a, int b) => new Vector3(MinX + a * Cell, heights[a, b], MinZ + b * Cell);
                            var p00 = V(i, j);
                            var p10 = V(i + 1, j);
                            var p01 = V(i, j + 1);
                            var p11 = V(i + 1, j + 1);
                            // Under the castle nothing shows; skip the cells fully under it.
                            if (p00.y <= -0.59f && p10.y <= -0.59f && p01.y <= -0.59f && p11.y <= -0.59f && Distance(new Vector2(p00.x + Cell * 0.5f, p00.z + Cell * 0.5f)) <= 0f)
                            {
                                continue;
                            }

                            // The farm's painted patch (L4Farmland) takes the ground there.
                            if (L4Farmland.Rect.Contains(new Vector2(p00.x + Cell * 0.5f, p00.z + Cell * 0.5f)))
                            {
                                continue;
                            }

                            // Alternate the diagonal so the facets do not line up.
                            bool flip = ((i * 7 + j * 13) & 1) == 0;
                            if (flip)
                            {
                                Face(verts, uvs, tris, p00, p01, p11);
                                Face(verts, uvs, tris, p00, p11, p10);
                            }
                            else
                            {
                                Face(verts, uvs, tris, p00, p01, p10);
                                Face(verts, uvs, tris, p10, p01, p11);
                            }
                        }
                    }

                    if (tris.Count == 0)
                    {
                        continue;
                    }

                    var mesh = new Mesh { name = "L4_Surroundings_" + ti / Tile + "_" + tj / Tile, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.SetVertices(verts);
                    mesh.SetUVs(0, uvs);
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    mesh = L4Level.SaveMesh(mesh);
                    var go = L3Build.MeshChild(parent, mesh.name, mesh, Vector3.zero, palette);
                    go.layer = L3Build.Layer(L3Build.Unlayered);
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    count++;
                }
            }

            return count;
        }

        // One flat-shaded triangle, its palette cell picked from its slope, height and a little noise.
        private static void Face(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            var centre = (a + b + c) / 3f;
            float slope = 1f - Mathf.Abs(n.y);
            // Large patches, not per-face noise: the facets' own shading already breaks the surface up.
            float region = Fbm(centre.x * 0.045f + 5f, centre.z * 0.045f);
            float speck = Mathf.PerlinNoise(centre.x * 0.9f + 11f, centre.z * 0.9f + 4f);
            int cell;
            if (slope > 0.35f)
            {
                cell = speck > 0.72f ? 2 : region > 0.62f ? 0 : 1;
            }
            else if (centre.y < -6f)
            {
                cell = speck > 0.78f ? 3 : region > 0.55f ? 3 : 4;
            }
            else if (centre.y < -1.2f || Distance(new Vector2(centre.x, centre.z)) < 12f)
            {
                cell = region > 0.5f ? 3 : 1;
            }
            else
            {
                // The land: dry grass and burnt earth in broad sweeps, ash where the fires ran hottest. (South of
                // the causeway L4Farmland paints L3's fields over its own patch.)
                float burn = Fbm(centre.x * 0.025f + 30f, centre.z * 0.025f);
                cell = burn > 0.6f ? 7 : burn > 0.42f ? 5 : 6;

            }

            var uv = L4Kit.Cell(cell, Tones.Length);
            int k = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);
            tris.Add(k);
            tris.Add(k + 1);
            tris.Add(k + 2);
        }

        // ---------------------------------------------------------------- Rocks

        // Pack rocks along the cliff feet and the bank tops, and pack cliff pieces set into the cliffs, so the
        // heightfield never shows its grid at the walls' feet.
        private static int Rocks(Transform g)
        {
            var parent = L3Build.Child(g, "Rocks").transform;
            var stone = L4Build.Mat("L4_Adventure_Stone");
            var cliffStone = L4Build.Mat("L4_Knights_Stone");
            var rng = new System.Random(4401);
            string[] rocks = { "A:SM_Env_Rock_01", "A:SM_Env_Rock_02", "A:SM_Env_Rock_03", "A:SM_Env_Rock_04", "A:SM_Env_Rock_05", "A:SM_Env_Rock_07", "A:SM_Env_Rock_08", "A:SM_Env_Rock_09", "A:SM_Env_Rock_010", "A:SM_Env_Rock_011", "A:SM_Env_Rock_012" };
            int n = 0;
            for (float x = MinX + 4f; x < MaxX - 4f; x += 3f)
            {
                for (float z = MinZ + 4f; z < MaxZ - 4f; z += 3f)
                {
                    var p = new Vector2(x + (float)rng.NextDouble() * 2.4f, z + (float)rng.NextDouble() * 2.4f);
                    float d = Distance(p);
                    // Under the causeway nothing may rise near its deck.
                    if (Mathf.Abs(p.x - CausewayX) < 7f && p.y > CausewaySouth - 2f && p.y < 42f)
                    {
                        continue;
                    }

                    float roll = (float)rng.NextDouble();
                    bool cliffFoot = d > 9f && d < 19f && roll < 0.16f;
                    bool scattered = d > 19f && d < 70f && roll < 0.02f;
                    if (!cliffFoot && !scattered)
                    {
                        continue;
                    }

                    float y = Height(p);
                    float yaw = (float)rng.NextDouble() * 360f;
                    {
                        float s = cliffFoot ? 1.4f + (float)rng.NextDouble() * 2.6f : 0.8f + (float)rng.NextDouble() * 1.6f;
                        var rock = L3Build.Part(parent, rocks[rng.Next(rocks.Length)], new Vector3(p.x, y - 0.35f * s, p.y), new Vector3(((float)rng.NextDouble() - 0.5f) * 20f, yaw, ((float)rng.NextDouble() - 0.5f) * 20f), Vector3.one * s, stone);
                        rock.name = "Rock";
                    }

                    n++;
                }
            }

            n += CliffRing(parent, cliffStone, rng);
            foreach (var r in parent.GetComponentsInChildren<Renderer>())
            {
                r.gameObject.layer = L3Build.Layer(L3Build.Unlayered);
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            }

            return n;
        }

        // The crag's cliffs: the pack's cliff mesas (25 x 14 x 25 m) squeezed to about 11 m across and set in a ring
        // just outside the walls, their tops 1-2.5 m under the wall feet, their bases on the ravine floor.
        private static int CliffRing(Transform parent, Material stone, System.Random rng)
        {
            var placed = new List<Vector2>();
            for (float x = MinX; x < MaxX; x += 2f)
            {
                for (float z = MinZ; z < MaxZ; z += 2f)
                {
                    var p = new Vector2(x + (float)rng.NextDouble() * 1.5f, z + (float)rng.NextDouble() * 1.5f);
                    float d = Distance(p);
                    if (d < 3.5f || d > 5.5f)
                    {
                        continue;
                    }

                    if (Mathf.Abs(p.x - CausewayX) < 9f && p.y < 44f)
                    {
                        continue;
                    }

                    bool near = false;
                    foreach (var q in placed)
                    {
                        if ((q - p).sqrMagnitude < 7.5f * 7.5f)
                        {
                            near = true;
                            break;
                        }
                    }

                    if (near)
                    {
                        continue;
                    }

                    placed.Add(p);
                    bool first = rng.NextDouble() < 0.5;
                    float across = (first ? 25f : 18.5f), sxz = (9.5f + (float)rng.NextDouble() * 4f) / across;
                    float sy = 1.02f + (float)rng.NextDouble() * 0.16f;
                    float top = -1f - (float)rng.NextDouble() * 1.5f;
                    var piece = L3Build.Part(parent, first ? "K:SM_Env_Cliff_01" : "K:SM_Env_Cliff_02", new Vector3(p.x, top - 14.5f * sy, p.y), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(sxz, sy, sxz), stone);
                    piece.name = "Cliff";
                }
            }

            return placed.Count;
        }

        // The siege camp sits on the east bank, beyond the ravine, not on its floor.
        private static void SettleCamp(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "SiegeCampRuin")
                {
                    var p = t.position;
                    t.position = new Vector3(p.x, Height(new Vector2(p.x, p.z)), p.z);
                }
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
