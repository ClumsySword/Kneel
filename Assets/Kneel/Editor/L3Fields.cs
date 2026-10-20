using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The breadbasket itself: crops on every side of the level. Dead wheat stands wherever the fire went
    // round (the unburnt strips of L3Ground.Burnt), drilled in rows that bend with the strips and break up at
    // their edges, and burnt stubble stands in the black strips between and on the floors of the two crop
    // fields the player fights in. Where a lane is fenced rather than walled the crop comes right up to the
    // rails and, in places, through them: the same rows carry on a little way into the verge, never onto
    // the road. The crop rows the player can light (L3_CropRow_12) use a dense wheat baked from the pack's
    // reeds; that is far too heavy to sow by the hectare, so a field plant here is a handful of blades and
    // ears on the same wheat palette and the same wind shader, a dozen triangles each. Baked into one mesh
    // per band of the level. No colliders.
    public static class L3Fields
    {
        private const float BandDepth = 40f;
        private const int Wind = 0, Still = 1, Stubble = 2;
        // Palette columns of L3_Wheat_Palette (see L3Crops).
        private const int Leaves = 3, EarA = 3, EarB = 4, Columns = 8;
        private const float Bucket = 4f;

        private static Vector2 Cell(int column, float height)
        {
            return new Vector2((column + 0.5f) / Columns, Mathf.Clamp(height, 0.03f, 0.97f));
        }

        public static Material[] Materials()
        {
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(L3Build.MaterialsPath + "/Textures/L3_Wheat_Palette.png");
            // The same wheat without the wind shader, for crops on raised ground (the shader sways a vertex by
            // its height above the mesh's pivot, which is the plain).
            L3Crops.MakeMaterial("L3_Wheat_Still", "Universal Render Pipeline/Lit", palette);
            return new[] { L3Crops.DryMaterial, L3Build.Mat("L3_Wheat_Still"), L3Crops.StubbleMaterial };
        }

        private static bool Fence(string kit)
        {
            return kit == "K06" || kit == "K09" || kit == "K10";
        }

        public static string Build(Transform root, char stage, List<L3Level.Piece> pieces)
        {
            var bands = new Dictionary<int, L3Mesh>();
            L3Mesh Band(float z)
            {
                int index = Mathf.FloorToInt((z - L3Plan.GroundMinZ) / BandDepth);
                if (!bands.TryGetValue(index, out var mesh))
                {
                    mesh = new L3Mesh();
                    while (mesh.Tris.Count < 3)
                    {
                        mesh.Tris.Add(new List<int>());
                    }

                    bands[index] = mesh;
                }

                return mesh;
            }

            float FloorDistance(Vector2 p)
            {
                float best = float.MaxValue;
                foreach (var f in L3Plan.Floors)
                {
                    if (f.Stage <= stage)
                    {
                        best = Mathf.Min(best, f.SignedDistance(p));
                    }
                }

                foreach (var b in L3Plan.Blocks)
                {
                    if (b.Stage <= stage)
                    {
                        best = Mathf.Min(best, L3Ground.RectDistance(p, b.X0, b.X1, b.Z0, b.Z1));
                    }
                }

                return best;
            }

            // ---- The boundary pieces, in buckets, so a plant can ask what stands near it.
            var buckets = new Dictionary<long, List<L3Level.Piece>>();
            long Key(int i, int j)
            {
                return ((long)i << 32) ^ (uint)j;
            }

            foreach (var piece in pieces)
            {
                long key = Key(Mathf.FloorToInt(piece.P.x / Bucket), Mathf.FloorToInt(piece.P.y / Bucket));
                if (!buckets.TryGetValue(key, out var list))
                {
                    buckets[key] = list = new List<L3Level.Piece>();
                }

                list.Add(piece);
            }

            // fence: the distance to the nearest rail. solid: the distance to the nearest hedge, wall or bank.
            // blocked: p is in or under one of them.
            void Near(Vector2 p, out float fence, out float solid, out bool blocked)
            {
                fence = float.MaxValue;
                solid = float.MaxValue;
                blocked = false;
                int ci = Mathf.FloorToInt(p.x / Bucket), cj = Mathf.FloorToInt(p.y / Bucket);
                for (int i = ci - 1; i <= ci + 1; i++)
                {
                    for (int j = cj - 1; j <= cj + 1; j++)
                    {
                        if (!buckets.TryGetValue(Key(i, j), out var list))
                        {
                            continue;
                        }

                        foreach (var piece in list)
                        {
                            float yaw = piece.Yaw * Mathf.Deg2Rad;
                            var along = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
                            Vector2 rel = p - piece.P;
                            float half = piece.Kit == "K07" ? 0.5f : 1f;
                            float dist = (rel - along * Mathf.Clamp(Vector2.Dot(rel, along), -half, half)).magnitude;
                            if (Fence(piece.Kit))
                            {
                                fence = Mathf.Min(fence, dist);
                                blocked |= dist < 0.2f;
                            }
                            else if (piece.Kit == "K07")
                            {
                                blocked |= dist < 0.45f;
                            }
                            else
                            {
                                solid = Mathf.Min(solid, dist);
                                blocked |= dist < (piece.Kit == "K05" ? 1.5f : piece.Kit == "K03" || piece.Kit == "K04" ? 0.8f : 1.05f);
                            }
                        }
                    }
                }
            }

            // Whatever stands or lies on a floor: nothing is sown under it.
            bool Occupied(Vector2 p)
            {
                foreach (var place in L3Plan.Places)
                {
                    if (place.Stage <= stage && (new Vector2(place.Position.x, place.Position.z) - p).sqrMagnitude < 2.5f * 2.5f)
                    {
                        return true;
                    }
                }

                foreach (var spawn in L3Plan.Spawns)
                {
                    if ((spawn.Position - p).sqrMagnitude < 2f * 2f)
                    {
                        return true;
                    }
                }

                foreach (var stain in L3Ground.Mud)
                {
                    if (stain.Stage <= stage && L3Ground.RectDistance(p, stain.X0, stain.X1, stain.Z0, stain.Z1) < 1.5f)
                    {
                        return true;
                    }
                }

                return false;
            }

            // The floors a crop may creep onto: level lanes and the two fenced fields. Not the verges of the
            // ditch, not the causeway, not the arenas with their own ground.
            bool Creeps(Vector2 p)
            {
                foreach (var f in L3Plan.Floors)
                {
                    if (f.Stage > stage || !f.Contains(p))
                    {
                        continue;
                    }

                    return !f.Virtual && !f.Raised && (f.Lane || f.Id == "F05" || f.Id == "F11") && f.Id != "F32" && !f.Id.StartsWith("F18") && !f.Id.StartsWith("F19");
                }

                return false;
            }

            int wheat = 0, stubble = 0, inPath = 0, seed = 0;
            float Rand()
            {
                return L3Level.Hash("field", seed++);
            }

            // ---- Rows half a metre apart that follow the lean of the strips, across the whole country.
            const float rowPitch = 0.55f, plantPitch = 0.5f;
            for (float u = L3Plan.GroundMinX - 20f; u < L3Plan.GroundMinX + L3Plan.GroundWidth + 20f; u += rowPitch)
            {
                for (float z = L3Plan.GroundMinZ + 1f; z < L3Plan.GroundMinZ + L3Plan.GroundLength - 1f; z += plantPitch)
                {
                    // The row's place at this z: the strips are constant in (x + lean), and so are the rows.
                    float lean = (L3Ground.Fbm(u * 0.03f, z * 0.014f) - 0.5f) * 30f + Mathf.Sin(z * 0.043f + u * 0.02f) * 3f;
                    float r1 = Rand(), r2 = Rand(), r3 = Rand();
                    var p = new Vector2(u - lean + (r1 - 0.5f) * 0.16f, z + (r2 - 0.5f) * 0.3f);
                    if (p.x < L3Plan.GroundMinX + 1f || p.x > L3Plan.GroundMinX + L3Plan.GroundWidth - 1f)
                    {
                        continue;
                    }

                    float d = FloorDistance(p);
                    if (d > 38f || L3Plan.KeepClearContains(p, stage) || L3Plan.Ditch.Contains(p, 1.2f))
                    {
                        continue;
                    }

                    bool inside = d < 0f;
                    float thin = 1f, tall = 1f;
                    if (d < 2.3f)
                    {
                        // Close to the path: only where a fence, not a hedge or a wall, is what stands there.
                        Near(p, out float fence, out float solid, out bool blocked);
                        if (blocked || fence > 2.9f || fence > solid)
                        {
                            continue;
                        }

                        if (inside)
                        {
                            // Through the rails: a fringe at their foot everywhere, and here and there a
                            // tongue of the crop reaching into the verge.
                            float tongue = L3Ground.Fbm(p.x * 0.11f + 41f, p.y * 0.11f + 13f);
                            float reach = 0.85f + 2.3f * L3Ground.Step(0.45f, 0.6f, tongue);
                            if (fence > reach || L3Ground.RoadDistance(p) < 0.7f || !Creeps(p) || Occupied(p))
                            {
                                continue;
                            }

                            thin = Mathf.Lerp(0.95f, 0.4f, fence / reach);
                            tall = Mathf.Lerp(0.9f, 0.7f, fence / reach);
                        }
                    }

                    // Thinner far from the path, where only the vistas will ever look.
                    if (d > 22f && r3 > Mathf.InverseLerp(40f, 22f, d))
                    {
                        continue;
                    }

                    float burnt = L3Ground.Burnt(p.x, p.y);
                    // Gaps: trampled places, bare earth, the ragged margin where a strip burned out.
                    float gaps = L3Ground.Fbm(p.x * 0.09f + 3f, p.y * 0.09f + 77f);
                    float y = inside ? 0.01f : L3Level.BackdropHeight(p, stage) + 0.04f;
                    if (burnt < (inside ? 0.45f : 0.34f))
                    {
                        if (gaps < 0.36f || r3 < burnt * 1.6f || Rand() > thin)
                        {
                            continue;
                        }

                        Plant(Band(p.y), y > 0.5f ? Still : Wind, new Vector3(p.x, y, p.y), Mathf.Lerp(0.62f, 0.95f, Rand()) * Mathf.Lerp(1f, 0.8f, burnt * 2.5f) * tall, ref seed);
                        wheat++;
                        inPath += inside ? 1 : 0;
                    }
                    else if (burnt > 0.55f && r3 < 0.45f * thin && gaps > 0.3f)
                    {
                        Stub(Band(p.y), new Vector3(p.x, y, p.y), ref seed);
                        stubble++;
                        inPath += inside ? 1 : 0;
                    }
                }
            }

            // ---- The two crop fields the player fights in: burnt stubble underfoot, clear of the road, the
            // live rows and the edges.
            foreach (string id in new[] { "F07", "F17" })
            {
                var f = L3Plan.Find(id);
                if (f.Stage > stage)
                {
                    continue;
                }

                for (float x = f.X0 + 0.6f; x < f.X1 - 0.5f; x += rowPitch)
                {
                    for (float z = f.Z0 + 0.6f; z < f.Z1 - 0.5f; z += plantPitch)
                    {
                        float r1 = Rand(), r2 = Rand(), r3 = Rand();
                        var p = new Vector2(x + (r1 - 0.5f) * 0.16f, z + (r2 - 0.5f) * 0.3f);
                        if (r3 > 0.5f || L3Ground.RoadDistance(p) < 0.6f || L3Plan.KeepClearContains(p, stage) || L3Ground.Fbm(p.x * 0.14f + 9f, p.y * 0.14f) < 0.42f)
                        {
                            continue;
                        }

                        Stub(Band(p.y), new Vector3(p.x, 0.01f, p.y), ref seed);
                        stubble++;
                    }
                }
            }

            var group = root.Find("Dressing");
            var materials = Materials();
            int triangles = 0;
            foreach (var pair in bands)
            {
                triangles += pair.Value.TriangleCount;
                var mesh = L3Build.SaveMesh(pair.Value, "Fields", "L3_Fields_" + pair.Key.ToString("00"));
                var go = L3Build.MeshChild(group, "Fields " + pair.Key.ToString("00") + " (z " + (L3Plan.GroundMinZ + pair.Key * BandDepth) + ")", mesh, Vector3.zero, materials);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return "fields: " + wheat + " wheat plants, " + stubble + " stubble plants (" + inPath + " of them on the path's verges), " + bands.Count + " baked meshes, " + triangles + " triangles";
        }

        // One dead wheat plant: a few blades from one root, some carrying an ear. Lit as if every blade faced
        // the sky, like the wheat of the crop rows: a field has no dark side from above.
        private static void Plant(L3Mesh mesh, int sub, Vector3 root, float height, ref int seed)
        {
            int blades = 4 + (int)(L3Level.Hash("plant", seed++) * 3f);
            for (int i = 0; i < blades; i++)
            {
                float turn = L3Level.Hash("plant", seed++) * Mathf.PI * 2f, spread = Mathf.Lerp(0.05f, 0.22f, L3Level.Hash("plant", seed++));
                float h = height * Mathf.Lerp(0.7f, 1.05f, L3Level.Hash("plant", seed++));
                var outward = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                var side = new Vector3(-outward.z, 0f, outward.x) * 0.034f;
                // Leaning a little downwind (north).
                Vector3 tip = root + outward * spread + new Vector3(0f, h, 0.07f * h);
                Vector3 foot = root + outward * 0.02f;
                int column = (int)(L3Level.Hash("plant", seed++) * Leaves) % Leaves;
                Vector3 normal = (Vector3.Cross(side, tip - foot).normalized * 0.4f + Vector3.up).normalized;
                mesh.Tri(sub, foot - side, foot + side, tip, Cell(column, 0.05f), Cell(column, 0.05f), Cell(column, 0.95f), normal);
                if (i < 3)
                {
                    // The ear: a small diamond hanging at the tip.
                    int ear = L3Level.Hash("plant", seed++) < 0.5f ? EarA : EarB;
                    Vector3 hang = tip + outward * 0.05f + Vector3.down * 0.02f, end = tip + outward * 0.1f + Vector3.down * 0.08f;
                    Vector3 wide = side * 1.6f;
                    mesh.Tri(sub, tip, hang - wide, hang + wide, Cell(ear, 0.6f), Cell(ear, 0.8f), Cell(ear, 0.8f), normal);
                    mesh.Tri(sub, hang - wide, end, hang + wide, Cell(ear, 0.8f), Cell(ear, 0.95f), Cell(ear, 0.8f), normal);
                }
            }
        }

        // What the fire left of one plant: three or four charred stumps.
        private static void Stub(L3Mesh mesh, Vector3 root, ref int seed)
        {
            int stumps = 3 + (int)(L3Level.Hash("stub", seed++) * 2f);
            for (int i = 0; i < stumps; i++)
            {
                float turn = L3Level.Hash("stub", seed++) * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                var side = new Vector3(-outward.z, 0f, outward.x) * 0.02f;
                Vector3 foot = root + outward * Mathf.Lerp(0.02f, 0.1f, L3Level.Hash("stub", seed++));
                Vector3 tip = foot + outward * 0.04f + Vector3.up * Mathf.Lerp(0.08f, 0.24f, L3Level.Hash("stub", seed++));
                int column = (int)(L3Level.Hash("stub", seed++) * Leaves) % Leaves;
                Vector3 normal = (Vector3.Cross(side, tip - foot).normalized * 0.4f + Vector3.up).normalized;
                mesh.Tri(Stubble, foot - side, foot + side, tip, Cell(column, 0.2f), Cell(column, 0.2f), Cell(column, 0.75f), normal);
            }
        }
    }
}
