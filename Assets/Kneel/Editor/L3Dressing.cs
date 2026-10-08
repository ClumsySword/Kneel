using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The loose, uneven things that make a plan into a place: dead grass along the foot of every wall and hedge
    // and in the verges of the lanes, stones fallen from the walls, pebbles, and outside the walls the burnt
    // country itself (stubble, patches of dead wheat the fire missed, rocks, stumps, charred trees, reeds in
    // the ditch). All of it is copied from pack meshes and the level's own wheat into a few baked meshes on
    // the level's re-graded palettes. Nothing here has a collider and nothing stands tall on the camera side of
    // a floor, so it changes how the level looks and not how it plays.
    public static class L3Dressing
    {
        private const float BandDepth = 40f;
        private const int Withered = 0, Stone = 1, Charred = 2, StrawStill = 3, Stubble = 4, Wheat = 5, Slots = 6;

        private class Source
        {
            public L3Mesh Mesh;
            public Bounds Bounds;
        }

        private static readonly Dictionary<string, Source> sources = new Dictionary<string, Source>();
        private static readonly Dictionary<int, L3Mesh> bands = new Dictionary<int, L3Mesh>();
        private static System.Random random;
        private static char builtStage;

        private static float Rand()
        {
            return (float)random.NextDouble();
        }

        private static float Rand(float a, float b)
        {
            return Mathf.Lerp(a, b, Rand());
        }

        private static string Pick(params string[] keys)
        {
            return keys[random.Next(keys.Length)];
        }

        // A pack prefab's mesh (its first LOD), in the prefab's own space.
        private static Source Get(string key)
        {
            if (sources.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var prefab = L3Build.Pack(key);
            var merged = new L3Mesh();
            var lods = prefab.GetComponent<LODGroup>();
            var filters = new List<MeshFilter>();
            if (lods != null && lods.GetLODs().Length > 0)
            {
                foreach (var r in lods.GetLODs()[0].renderers)
                {
                    if (r != null && r.GetComponent<MeshFilter>() != null)
                    {
                        filters.Add(r.GetComponent<MeshFilter>());
                    }
                }
            }
            else
            {
                filters.AddRange(prefab.GetComponentsInChildren<MeshFilter>(true));
            }

            foreach (var filter in filters)
            {
                if (filter.sharedMesh != null)
                {
                    merged.Append(L3Mesh.From(filter.sharedMesh), prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix, 0);
                }
            }

            var source = new Source { Mesh = merged, Bounds = merged.Bounds() };
            sources[key] = source;
            return source;
        }

        // One of the level's own crop meshes, without the square bed it stands on in a crop row: out in the
        // fields the stalks stand in the painted ground, and a scatter of square beds would read as tiles.
        private static Source Get(Mesh mesh)
        {
            string key = "mesh:" + mesh.name;
            if (!sources.TryGetValue(key, out var source))
            {
                var data = L3Mesh.From(mesh).Where((centre, normal) => centre.y > 0.07f || normal.y < 0.85f);
                source = new Source { Mesh = data, Bounds = data.Bounds() };
                sources[key] = source;
            }

            return source;
        }

        // Stands a copy on the ground at p: its footprint centred, its lowest point a little below ground.
        private static void Put(Source source, Vector3 p, float scale, float yaw, int slot, float sink = 0.04f, float tiltX = 0f, float tiltZ = 0f, float squash = 1f)
        {
            int band = Mathf.FloorToInt((p.z - L3Plan.GroundMinZ) / BandDepth);
            if (!bands.TryGetValue(band, out var mesh))
            {
                mesh = new L3Mesh();
                while (mesh.Tris.Count < Slots)
                {
                    mesh.Tris.Add(new List<int>());
                }

                bands[band] = mesh;
            }

            Bounds b = source.Bounds;
            var matrix = Matrix4x4.TRS(p + Vector3.down * sink, Quaternion.Euler(tiltX, yaw, tiltZ), new Vector3(scale, scale * squash, scale))
                * Matrix4x4.Translate(new Vector3(-b.center.x, -b.min.y, -b.center.z));
            mesh.Append(source.Mesh, matrix, slot);
        }

        private static void PutHigh(string key, Vector3 p, float height, int slot, float sink = 0.04f, float tilt = 0f)
        {
            var source = Get(key);
            Put(source, p, height / Mathf.Max(0.01f, source.Bounds.size.y), Rand(0f, 360f), slot, sink, Rand(-tilt, tilt), Rand(-tilt, tilt));
        }

        private static void PutWide(string key, Vector3 p, float width, int slot, float sink = 0.04f, float tilt = 0f, float squash = 1f)
        {
            var source = Get(key);
            Put(source, p, width / Mathf.Max(0.01f, Mathf.Max(source.Bounds.size.x, source.Bounds.size.z)), Rand(0f, 360f), slot, sink, Rand(-tilt, tilt), Rand(-tilt, tilt), squash);
        }

        // ---------------------------------------------------------------- Where things may go

        private static L3Plan.Floor FloorAt(Vector2 p)
        {
            foreach (var f in L3Plan.Floors)
            {
                if (!f.Virtual && f.Stage <= builtStage && f.Contains(p))
                {
                    return f;
                }
            }

            return null;
        }

        private static float FloorDistance(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var f in L3Plan.Floors)
            {
                if (f.Stage <= builtStage)
                {
                    best = Mathf.Min(best, f.SignedDistance(p));
                }
            }

            foreach (var b in L3Plan.Blocks)
            {
                if (b.Stage <= builtStage)
                {
                    best = Mathf.Min(best, L3Ground.RectDistance(p, b.X0, b.X1, b.Z0, b.Z1));
                }
            }

            return best;
        }

        private static float GroundY(Vector2 p)
        {
            var f = FloorAt(p);
            return f != null ? f.HeightAt(p.y) : L3Level.BackdropHeight(p, builtStage) + 0.03f;
        }

        // Standing water, mud, scorch, crop rows, decks: nothing grows or lies there.
        private static bool Bare(Vector2 p)
        {
            foreach (var keep in L3Plan.KeepClear)
            {
                if (keep.Stage <= builtStage && keep.Contains(p, 0.6f))
                {
                    return true;
                }
            }

            var ditch = L3Plan.Ditch;
            if (builtStage >= ditch.Stage && ditch.Contains(p, 0.5f))
            {
                return true;
            }

            foreach (var stain in L3Ground.Mud)
            {
                if (stain.Stage <= builtStage && L3Ground.StainWeight(p, stain.X0, stain.X1, stain.Z0, stain.Z1) > 0.3f)
                {
                    return true;
                }
            }

            return builtStage >= 'D' && L3Ground.RectDistance(p, 163f, 185f, 330f, 338f) < 0.5f;
        }

        // On the camera side of a floor (south of it, within its width): tall things there would hide the player.
        private static bool SouthOfFloor(Vector2 p)
        {
            foreach (var f in L3Plan.Floors)
            {
                if (!f.Virtual && f.Stage <= builtStage && p.x > f.X0 - 3f && p.x < f.X1 + 3f && p.y < f.Z0 && f.Z0 - p.y < 12f)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- The scatter

        // Dead grass the colour of old straw. The pack's green palette is re-graded twice: once on the wind
        // shader, once still.
        private static Color Straw(float h, float s, float v)
        {
            bool green = h > 0.17f && h < 0.48f;
            return green
                ? Color.HSVToRGB(Mathf.Lerp(0.1f, 0.125f, v), Mathf.Min(s * 0.42f, 0.4f), Mathf.Lerp(0.28f, 0.54f, v))
                : Color.HSVToRGB(Mathf.Lerp(h, 0.09f, 0.5f), s * 0.35f, Mathf.Min(v * 0.6f, 0.45f));
        }

        public static void Materials()
        {
            L3Look.Grade("L3_Adventure_Straw", "PolyAdventureMaterial_01", Straw, true);
            L3Look.Grade("L3_Adventure_StrawStill", "PolyAdventureMaterial_01", Straw, false);
        }

        // A clump: a few blades' worth of the pack's small grass, now and then a broader dead weed.
        private static void Tuft(Vector2 p, float size = 1f)
        {
            float y = GroundY(p);
            // The wind shader sways a vertex by its height above the mesh's pivot, which is the plain: grass on
            // raised ground would slide about, so it gets the still material.
            int slot = y > 0.5f ? StrawStill : Withered;
            int blades = random.Next(2, 5);
            for (int i = 0; i < blades; i++)
            {
                var q = p + new Vector2(Rand(-0.16f, 0.16f), Rand(-0.16f, 0.16f)) * size;
                PutHigh(Pick("A:SM_Env_Grass_01", "A:SM_Env_Grass_02"), new Vector3(q.x, y, q.y), Rand(0.24f, 0.46f) * size, slot, 0.03f, 10f);
            }

            if (Rand() < 0.05f)
            {
                PutHigh(Pick("A:SM_Env_Plant_01", "A:SM_Env_Plant_02"), new Vector3(p.x, y, p.y), Rand(0.3f, 0.5f) * size, slot, 0.03f, 6f);
            }
        }

        private static void Pebble(Vector2 p, float min = 0.1f, float max = 0.28f)
        {
            PutWide(Pick("A:SM_Env_Pebble_01", "A:SM_Env_Pebble_02", "A:SM_Env_Pebble_03", "A:SM_Env_Pebble_04", "A:SM_Env_Pebble_05", "A:SM_Env_Pebble_06", "A:SM_Env_Pebble_07"),
                new Vector3(p.x, GroundY(p), p.y), Rand(min, max), Stone, 0.03f, 20f);
        }

        private static void Rock(Vector2 p, float width)
        {
            PutWide(Pick("A:SM_Env_Rock_01", "A:SM_Env_Rock_02", "A:SM_Env_Rock_03", "A:SM_Env_Rock_05", "A:SM_Env_Rock_012", "A:SM_Env_Rock_014", "A:SM_Env_Rock_015", "A:SM_Env_Rock_016"),
                new Vector3(p.x, GroundY(p), p.y), width, Stone, width * 0.18f, 10f, Rand(0.55f, 0.9f));
        }

        public static string Build(Transform root, char stage, List<L3Level.Piece> pieces)
        {
            builtStage = stage;
            Materials();
            sources.Clear();
            bands.Clear();
            random = new System.Random(3071);
            int tufts = 0, stones = 0, trees = 0, reeds = 0;

            // ---- Lanes: grass and pebbles in the verges, either side of the track.
            foreach (var f in L3Plan.Floors)
            {
                if (f.Virtual || f.Stage > stage)
                {
                    continue;
                }

                float area = f.Diagonal ? f.Length * f.Width : (f.X1 - f.X0) * (f.Z1 - f.Z0);
                if (f.Lane)
                {
                    int tries = Mathf.RoundToInt(area * 0.55f);
                    for (int i = 0; i < tries; i++)
                    {
                        var p = new Vector2(Rand(f.X0, f.X1), Rand(f.Z0, f.Z1));
                        if (!f.Contains(p) || Bare(p) || L3Ground.RoadDistance(p) < 0.45f)
                        {
                            continue;
                        }

                        if (Rand() < 0.68f)
                        {
                            Tuft(p);
                            tufts++;
                        }
                        else
                        {
                            Pebble(p);
                            stones++;
                        }
                    }

                    continue;
                }

                // ---- Fields and yards: a ragged band of growth along the edges, a little straw elsewhere.
                int edge = Mathf.RoundToInt(((f.X1 - f.X0) + (f.Z1 - f.Z0)) * 2f * 1.1f);
                for (int i = 0; i < edge; i++)
                {
                    float inset = Rand() * Rand() * 2.4f + 0.1f;
                    bool alongX = Rand() < (f.X1 - f.X0) / ((f.X1 - f.X0) + (f.Z1 - f.Z0));
                    var p = alongX ? new Vector2(Rand(f.X0, f.X1), Rand() < 0.5f ? f.Z0 + inset : f.Z1 - inset) : new Vector2(Rand() < 0.5f ? f.X0 + inset : f.X1 - inset, Rand(f.Z0, f.Z1));
                    if (Bare(p) || OnTrail(p) || L3Ground.RoadDistance(p) < 0.45f)
                    {
                        continue;
                    }

                    if (Rand() < 0.75f)
                    {
                        Tuft(p, Rand(0.9f, 1.25f));
                        tufts++;
                    }
                    else
                    {
                        Pebble(p);
                        stones++;
                    }
                }

                int loose = Mathf.RoundToInt(area * 0.035f);
                for (int i = 0; i < loose; i++)
                {
                    var p = new Vector2(Rand(f.X0 + 1f, f.X1 - 1f), Rand(f.Z0 + 1f, f.Z1 - 1f));
                    if (Bare(p) || L3Ground.RoadDistance(p) < 0.45f)
                    {
                        continue;
                    }

                    if (Rand() < 0.5f)
                    {
                        Tuft(p, 0.8f);
                        tufts++;
                    }
                    else
                    {
                        Pebble(p, 0.08f, 0.2f);
                        stones++;
                    }
                }
            }

            // ---- The foot of every wall and hedge: grass both sides, and stones fallen from the drystone.
            foreach (var piece in pieces)
            {
                bool hedge = piece.Kit == "K01" || piece.Kit == "K02", stone = piece.Kit == "K03" || piece.Kit == "K04", fence = L3Level.Fence(piece.Kit);
                if (!hedge && !stone && !fence)
                {
                    continue;
                }

                float yaw = piece.Yaw * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
                var across = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
                int count = hedge ? 3 : 2;
                for (int i = 0; i < count; i++)
                {
                    var p = piece.P + along * Rand(-1f, 1f) + across * (Rand() < 0.5f ? -1f : 1f) * (fence ? Rand(0.05f, 0.55f) : Rand(0.5f, 1.05f));
                    if (Bare(p))
                    {
                        continue;
                    }

                    Tuft(p, Rand(0.9f, 1.4f));
                    tufts++;
                }

                if (stone && Rand() < 0.3f)
                {
                    int fallen = random.Next(1, 4);
                    var heap = piece.P + along * Rand(-0.8f, 0.8f) + across * (Rand() < 0.5f ? -1f : 1f) * Rand(0.6f, 0.95f);
                    for (int i = 0; i < fallen; i++)
                    {
                        var p = heap + new Vector2(Rand(-0.35f, 0.35f), Rand(-0.35f, 0.35f));
                        if (!Bare(p))
                        {
                            Pebble(p, 0.2f, 0.42f);
                            stones++;
                        }
                    }
                }
            }

            // ---- Outside the walls: the burnt country, close enough to the path to be seen.
            int candidates = Mathf.RoundToInt(L3Plan.GroundWidth * L3Plan.GroundLength * 0.075f);
            for (int i = 0; i < candidates; i++)
            {
                var p = new Vector2(Rand(L3Plan.GroundMinX + 2f, L3Plan.GroundMinX + L3Plan.GroundWidth - 2f), Rand(L3Plan.GroundMinZ + 2f, L3Plan.GroundMinZ + L3Plan.GroundLength - 2f));
                float d = FloorDistance(p);
                if (d < 2.4f || d > 18f || Bare(p) || L3Plan.Ditch.Contains(p, 2f))
                {
                    continue;
                }

                // Thick near the walls, thinning into the smoke.
                if (Rand() > Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(3f, 18f, d)))
                {
                    continue;
                }

                float y = GroundY(p);
                bool low = !SouthOfFloor(p) || d > 13f;
                float kind = Rand();
                if (kind < 0.4f)
                {
                    int n = random.Next(2, 6);
                    for (int k = 0; k < n; k++)
                    {
                        Tuft(p + new Vector2(Rand(-0.9f, 0.9f), Rand(-0.9f, 0.9f)), Rand(1f, 1.7f));
                        tufts++;
                    }
                }
                else if (kind < 0.56f)
                {
                    float width = Rand() < 0.2f && d > 4.5f && low ? Rand(1.1f, 2.1f) : Rand(0.3f, 0.9f);
                    Rock(p, width);
                    if (Rand() < 0.5f)
                    {
                        Pebble(p + new Vector2(Rand(-0.8f, 0.8f), Rand(-0.8f, 0.8f)), 0.15f, 0.35f);
                    }

                    stones++;
                }
                else if (kind < 0.66f)
                {
                    // Nothing: the crops themselves stand here (L3Fields).
                }
                else if (kind < 0.82f)
                {
                    PutHigh("A:SM_Env_TreeStump_01", new Vector3(p.x, y, p.y), Rand(0.45f, 0.9f), Charred, 0.08f, 6f);
                    trees++;
                }
                else if (kind < 0.9f)
                {
                    PutHigh(Pick("A:SM_Env_Bush_02", "A:SM_Env_Bush_03", "A:SM_Env_Bush_04"), new Vector3(p.x, y, p.y), Rand(0.6f, 1.1f), Charred, 0.1f, 5f);
                    trees++;
                }
                else if (kind < 0.94f)
                {
                    PutWide("A:SM_Env_TreeLog_01", new Vector3(p.x, y, p.y), Rand(1.8f, 3.2f), Charred, 0.12f, 4f);
                    trees++;
                }
                else if (low && d > 5f)
                {
                    // Bare, burnt trees: only where they cannot stand between the camera and the player.
                    PutHigh(Pick("A:SM_Env_TreeDead_01", "A:SM_Env_TreeDead_02"), new Vector3(p.x, y, p.y), Rand(4.2f, 7.5f), Charred, 0.2f, 5f);
                    trees++;
                }
            }

            // ---- By each abandoned cart: what fell off it, or was thrown off to lighten it.
            foreach (var place in L3Plan.Places)
            {
                if (place.Stage > stage || !place.Name.StartsWith("Poi_Cart"))
                {
                    continue;
                }

                var cart = new Vector2(place.Position.x, place.Position.z);
                int things = random.Next(4, 8);
                for (int i = 0; i < things; i++)
                {
                    float angle = Rand(0f, Mathf.PI * 2f);
                    var p = cart + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Rand(1.5f, 2.6f);
                    if (FloorAt(p) == null || Bare(p) || L3Ground.RoadDistance(p) < -0.9f)
                    {
                        continue;
                    }

                    var at = new Vector3(p.x, GroundY(p), p.y);
                    float kindOfThing = Rand();
                    if (kindOfThing < 0.45f)
                    {
                        PutWide(Pick("A:SM_Prop_Sack_01", "A:SM_Prop_Sack_02", "A:SM_Prop_Sack_03", "A:SM_Prop_Sack_04"), at, Rand(0.5f, 0.75f), Stone, 0.02f, 8f);
                    }
                    else if (kindOfThing < 0.7f)
                    {
                        PutWide(Pick("A:SM_Prop_Crate_01", "A:SM_Prop_Crate_02"), at, Rand(0.45f, 0.6f), Stone, 0.03f, 14f);
                    }
                    else if (kindOfThing < 0.9f)
                    {
                        PutWide(Pick("A:SM_Prop_Basket_02", "A:SM_Prop_Basket_04"), at, Rand(0.35f, 0.5f), Stone, 0.02f, 25f);
                    }
                    else
                    {
                        PutWide("A:SM_Prop_Cart_Wheel_01", at, Rand(0.8f, 0.95f), Stone, 0.04f, 4f);
                    }

                    stones++;
                }
            }

            // ---- The ditch: a ragged line of reeds along both sides, standing in the water.
            var ditch = L3Plan.Ditch;
            if (stage >= ditch.Stage)
            {
                foreach (float bank in new[] { -1f, 1f })
                {
                    float side = bank < 0f ? ditch.Z0 : ditch.Z1;
                    // Not where the cut closes at its ends.
                    float x = ditch.X0 + 7f;
                    while (x < ditch.X1 - 7f)
                    {
                        // In the shallows at the foot of the bank (L3Ditch), wherever that wanders to.
                        var p = new Vector2(x, side - bank * (L3Ditch.Toe(x, bank) + Rand(-0.1f, 0.45f)));
                        bool underDeck = false;
                        foreach (var keep in L3Plan.KeepClear)
                        {
                            underDeck |= keep.Stage <= stage && keep.Contains(p, 0.3f);
                        }

                        // In stands a few metres long with open water between them, none tall enough to hide it.
                        if (!underDeck && Mathf.PerlinNoise(x * 0.16f, side) > 0.56f)
                        {
                            PutHigh(Pick("A:SM_Env_Reeds_01", "A:SM_Env_Reeds_02", "A:SM_Env_Reeds_03"), new Vector3(p.x, -2.68f, p.y), Rand(0.9f, 1.5f), Withered, 0f, 7f);
                            reeds++;
                        }

                        x += Rand(0.45f, 1.1f);
                    }
                }
            }

            // ---- The ditch's lips: grass leaning over the edge and stones along it, so the cut is not a ruled line.
            if (stage >= ditch.Stage)
            {
                foreach (float lip in new[] { ditch.Z0 - 0.25f, ditch.Z1 + 0.25f })
                {
                    float x = ditch.X0 - 1f;
                    while (x < ditch.X1 + 1f)
                    {
                        var p = new Vector2(x, lip + Rand(-0.2f, 0.2f));
                        bool deck = false;
                        foreach (var keep in L3Plan.KeepClear)
                        {
                            deck |= keep.Stage <= stage && keep.Contains(p, 0.2f);
                        }

                        if (!deck)
                        {
                            if (Rand() < 0.6f)
                            {
                                Tuft(p, Rand(1f, 1.6f));
                                tufts++;
                            }
                            else if (FloorAt(p) == null)
                            {
                                // Only off the walkable verges: a stone on the lip of the dyke lane would look
                                // like something to stand on.
                                Rock(p, Rand(0.35f, 0.8f));
                                stones++;
                            }
                            else
                            {
                                Pebble(p, 0.12f, 0.3f);
                                stones++;
                            }
                        }

                        x += Rand(0.35f, 1.3f);
                    }
                }
            }

            // ---- Bake.
            var group = root.Find("Dressing");
            var materials = new[]
            {
                L3Build.Mat("L3_Adventure_Straw"), L3Build.Mat("L3_Adventure_Dawn"), L3Build.Mat("L3_Adventure_Charred"), L3Build.Mat("L3_Adventure_StrawStill"),
                L3Crops.StubbleMaterial, L3Crops.DryMaterial,
            };
            int triangles = 0;
            foreach (var pair in bands)
            {
                triangles += pair.Value.TriangleCount;
                var mesh = L3Build.SaveMesh(pair.Value, "Dressing", "L3_Dressing_" + pair.Key.ToString("00"));
                var go = L3Build.MeshChild(group, "Band " + pair.Key.ToString("00") + " (z " + (L3Plan.GroundMinZ + pair.Key * BandDepth) + ")", mesh, Vector3.zero, materials);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            return "dressing: " + tufts + " tufts, " + stones + " stones, " + trees + " stumps, bushes, logs and trees, " + reeds + " reeds; "
                + bands.Count + " baked meshes, " + triangles + " triangles";
        }

        // Trails stay clear of growth (a rough test: the paint knows the exact lines, this keeps the middles of
        // the gateways open).
        private static bool OnTrail(Vector2 p)
        {
            foreach (var f in L3Plan.Floors)
            {
                if (f.Openings == null || f.Stage > builtStage)
                {
                    continue;
                }

                foreach (var o in f.Openings)
                {
                    bool alongX = o.Side == 'S' || o.Side == 'N';
                    float line = o.Side == 'S' ? f.Z0 : o.Side == 'N' ? f.Z1 : o.Side == 'W' ? f.X0 : f.X1;
                    float t = alongX ? p.x : p.y, across = alongX ? p.y : p.x;
                    if (t > o.A - 0.3f && t < o.B + 0.3f && Mathf.Abs(across - line) < 3f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
