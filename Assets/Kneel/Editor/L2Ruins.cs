using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Burnt-out buildings and set pieces for L2, made from Synty parts without touching them.
    // Synty houses are single meshes (the roof is baked in), so ruins are baked by clipping a copy of the mesh
    // below a jagged burn line; the kept faces are duplicated inward on the charred material, so a roofless
    // shell reads as burnt black inside. Prefabs are visual only: collisions come from the level's lot boxes.
    public static class L2Ruins
    {
        private const string RuinMeshes = L2Build.MeshesPath + "/Ruins";

        // Fire light strengths, judged in the Game view against the moon (L2LightingPass): a burning house has to
        // throw a pool that reaches across the fight in front of it.
        public const float BurningHouseLight = 60f;
        public const float BlockerLight = 32f;
        public const float FireBedLight = 12f;
        public const float BackFireLight = 30f;
        private const string KnightsBuildings = "Assets/SyntyStudios/PolygonKnights/Prefabs/Buildings/";

        [MenuItem("Kneel/L2/Build/Ruins + Set Pieces")]
        public static void BuildAllMenu()
        {
            Debug.Log("[L2] " + BuildAll());
        }

        public static string BuildAll()
        {
            L2Build.EnsureFolder(RuinMeshes);
            L2Build.EnsureFolder(L2Build.PrefabsPath);
            if (L2Build.Mat("L2_Knights_Burnt") == null)
            {
                L2Look.RebuildMaterials();
            }

            EmberMaterial();
            var built = new List<string>();
            built.Add(RuinedHouseA());
            built.Add(RuinedHouseB());
            built.Add(RuinedHouseD());
            built.Add(RuinedHouseE());
            built.Add(BurningFront());
            built.Add(CollapsedChurch());
            built.Add(FireBlocker("L2_FireBlocker_A", false));
            built.Add(FireBlocker("L2_FireBlocker_B", true));
            built.Add(DebrisBlocker());
            built.Add(MarketRemains());
            built.Add(ArcherPerch());
            built.Add(LoreDoorframe());
            built.Add(LoreWell());
            built.Add(LootHeal());
            built.Add(LootWeapon());
            built.Add(ShortcutGate());
            built.Add(GateLever());
            built.Add(OverturnedCart());
            built.Add(DroppedBanner());
            AssetDatabase.SaveAssets();
            return $"Built {built.Count} L2 prefabs: " + string.Join(", ", built);
        }

        // ---------------------------------------------------------------- Mesh clipping

        // Cuts a mesh along the burn line h(x, z) = base + tilt.(x, z) + noise and returns [kept part + inward
        // charred copies, removed part]. Triangles that cross the line are subdivided until they are small, so
        // big wall faces burn down to a jagged edge instead of vanishing whole (which used to leave their small
        // trim and brick pieces floating in the air).
        public static Mesh[] Clip(Mesh src, float baseHeight, Vector2 tilt, float jag, int seed)
        {
            var v = src.vertices;
            var n = src.normals;
            var uv = src.uv;
            var tris = src.triangles;
            var keep = new MeshData();
            var gone = new MeshData();
            float Burn(Vector3 m) => baseHeight + tilt.x * m.x + tilt.y * m.z
                                     + (Mathf.PerlinNoise(m.x * 0.45f + seed * 13.1f, m.z * 0.45f + seed * 7.7f) - 0.5f) * 2f * jag
                                     + (Mathf.PerlinNoise((m.x + m.z) * 1.7f + seed, 3f) - 0.5f) * 0.6f;

            void Classify(Vtx a, Vtx b, Vtx c, int depth)
            {
                bool ba = a.P.y <= Burn(a.P), bb = b.P.y <= Burn(b.P), bc = c.P.y <= Burn(c.P);
                if (ba && bb && bc)
                {
                    keep.Add(a, b, c);
                    return;
                }

                if (!ba && !bb && !bc)
                {
                    gone.Add(a, b, c);
                    return;
                }

                float longest = Mathf.Max((a.P - b.P).sqrMagnitude, Mathf.Max((b.P - c.P).sqrMagnitude, (c.P - a.P).sqrMagnitude));
                if (depth >= 7 || longest < 0.22f * 0.22f)
                {
                    Vector3 m = (a.P + b.P + c.P) / 3f;
                    (m.y <= Burn(m) ? keep : gone).Add(a, b, c);
                    return;
                }

                Vtx ab = Vtx.Mid(a, b), bc2 = Vtx.Mid(b, c), ca = Vtx.Mid(c, a);
                Classify(a, ab, ca, depth + 1);
                Classify(ab, b, bc2, depth + 1);
                Classify(ca, bc2, c, depth + 1);
                Classify(ab, bc2, ca, depth + 1);
            }

            for (int i = 0; i < tris.Length; i += 3)
            {
                Classify(new Vtx(v, n, uv, tris[i]), new Vtx(v, n, uv, tris[i + 1]), new Vtx(v, n, uv, tris[i + 2]), 0);
            }

            return new[] { keep.TwoSided(), gone.TwoSided() };
        }

        private struct Vtx
        {
            public Vector3 P;
            public Vector3 N;
            public Vector2 UV;

            public Vtx(Vector3[] v, Vector3[] n, Vector2[] uv, int i)
            {
                P = v[i];
                N = n.Length > i ? n[i] : Vector3.up;
                UV = uv.Length > i ? uv[i] : Vector2.zero;
            }

            public static Vtx Mid(Vtx a, Vtx b)
            {
                return new Vtx { P = (a.P + b.P) * 0.5f, N = (a.N + b.N).normalized, UV = (a.UV + b.UV) * 0.5f };
            }
        }

        private class MeshData
        {
            private readonly List<Vector3> v = new List<Vector3>();
            private readonly List<Vector3> n = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> outer = new List<int>();

            public void Add(Vtx a, Vtx b, Vtx c)
            {
                int i = v.Count;
                foreach (var x in new[] { a, b, c })
                {
                    v.Add(x.P);
                    n.Add(x.N);
                    uv.Add(x.UV);
                }

                outer.Add(i);
                outer.Add(i + 1);
                outer.Add(i + 2);
            }

            // Outer faces on submesh 0; the same faces reversed, with flipped normals (the burnt inside), on submesh 1.
            public Mesh TwoSided()
            {
                int count = v.Count;
                var inner = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    v.Add(v[i]);
                    n.Add(-n[i]);
                    uv.Add(uv[i]);
                }

                for (int i = 0; i < outer.Count; i += 3)
                {
                    inner.Add(outer[i] + count);
                    inner.Add(outer[i + 2] + count);
                    inner.Add(outer[i + 1] + count);
                }

                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(outer, 0);
                mesh.SetTriangles(inner, 1);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private static Mesh SourceMesh(string prefab)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(KnightsBuildings + prefab + ".prefab").GetComponentInChildren<MeshFilter>().sharedMesh;
        }

        // Bakes (or re-bakes in place) a clipped copy of a Synty building mesh.
        public static Mesh Baked(string name, string prefab, float baseHeight, Vector2 tilt, float jag, int seed, bool removedPart = false)
        {
            var parts = Clip(SourceMesh(prefab), baseHeight, tilt, jag, seed);
            var mesh = parts[removedPart ? 1 : 0];
            Object.DestroyImmediate(parts[removedPart ? 0 : 1]);
            return SaveMesh(mesh, name);
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            mesh.name = name;
            string path = RuinMeshes + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetUVs(0, mesh.uv);
            existing.subMeshCount = mesh.subMeshCount;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                existing.SetTriangles(mesh.GetTriangles(s), s);
            }

            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static GameObject Part(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 euler, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { L2Build.Mat("L2_Knights_Burnt"), L2Build.Mat("L2_Knights_Charred") };
            return go;
        }

        private static GameObject Piece(string key, Transform parent, Vector3 position, Vector3 euler, float scale = 1f, string name = null)
        {
            var go = L2Build.Spawn(key, parent, position, euler, scale, name);
            L1Build.StripColliders(go);
            return go;
        }

        private static GameObject Charred(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = L2Build.Mat("L2_Knights_Charred");
                }

                r.sharedMaterials = mats;
            }

            return go;
        }

        private static string Save(GameObject root, bool environment = true)
        {
            L1Build.StripColliders(root);
            L1Build.SetStatic(root, environment ? L1Build.EnvironmentStatic : L1Build.PropStatic);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is ParticleSystemRenderer))
                {
                    r.shadowCastingMode = r.bounds.size.y < 0.5f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }

            string name = root.name;
            L1Build.SavePrefab(root, L2Build.PrefabsPath + "/" + name + ".prefab");
            return name;
        }

        // ---------------------------------------------------------------- Houses

        // A: roofless shell with its chimney still standing, the roof beams fallen in.
        private static string RuinedHouseA()
        {
            var root = new GameObject("L2_RuinedHouse_A");
            Part("Shell", root.transform, Baked("Shell_Room02", "SM_Bld_House_Room_02", 2.9f, Vector2.zero, 0.7f, 3), Vector3.zero, Vector3.zero);
            Piece("K/Buildings/SM_Bld_House_Chimney_02", root.transform, new Vector3(0f, 0f, 3.3f), Vector3.zero);
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(-0.8f, 0.15f, 1f), new Vector3(90f, 20f, 0f), 1.3f));
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(0.9f, 0.6f, -1.2f), new Vector3(70f, -35f, 0f), 1.4f));
            Piece("A/Environments/SM_Env_Rock_05", root.transform, new Vector3(0.4f, -0.2f, 0.3f), new Vector3(0f, 40f, 0f), 0.7f);
            Charred(Piece("A/Props/SM_Prop_Loghalf_02", root.transform, new Vector3(-0.5f, 0.1f, -2f), new Vector3(0f, 70f, 8f)));
            return Save(root);
        }

        // B: half-collapsed along a tilted line; the roof it lost lies in a heap beside it.
        private static string RuinedHouseB()
        {
            var root = new GameObject("L2_RuinedHouse_B");
            Part("Shell", root.transform, Baked("Shell_RoomTop03", "SM_Bld_House_RoomTop_03", 2.4f, new Vector2(0f, 0.32f), 0.6f, 5), Vector3.zero, Vector3.zero);
            var roof = Part("FallenRoof", root.transform, Baked("FallenRoof_RoomTop03", "SM_Bld_House_RoomTop_03", 2.4f, new Vector2(0f, 0.32f), 0.6f, 5, true), new Vector3(0.3f, -2.9f, -2.6f), new Vector3(-22f, 6f, 4f));
            roof.GetComponent<MeshRenderer>().sharedMaterials = new[] { L2Build.Mat("L2_Knights_Burnt"), L2Build.Mat("L2_Knights_Charred") };
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(-1.2f, 0.12f, -2.8f), new Vector3(90f, 60f, 0f), 1.5f));
            Piece("A/Environments/SM_Env_Rock_011", root.transform, new Vector3(1.2f, -0.5f, -1.6f), new Vector3(0f, 12f, 0f), 0.6f);
            return Save(root);
        }

        // D: a taller shell, gable ends still up, the ridge gone.
        private static string RuinedHouseD()
        {
            var root = new GameObject("L2_RuinedHouse_D");
            Part("Shell", root.transform, Baked("Shell_Room06", "SM_Bld_House_Room_06", 3.4f, new Vector2(0.1f, 0f), 0.9f, 7), Vector3.zero, Vector3.zero);
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(0.4f, 1.1f, 0.2f), new Vector3(35f, 90f, 0f), 1.6f));
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(-0.6f, 0.15f, -1.5f), new Vector3(90f, 5f, 0f), 1.4f));
            return Save(root);
        }

        // E: a narrow two-storey house burnt down to its lower floor, chimney standing.
        private static string RuinedHouseE()
        {
            var root = new GameObject("L2_RuinedHouse_E");
            Part("Shell", root.transform, Baked("Shell_RoomTall02", "SM_Bld_House_RoomTall_02", 3.8f, new Vector2(0.15f, 0f), 0.8f, 11), Vector3.zero, Vector3.zero);
            Piece("K/Buildings/SM_Bld_House_Chimney_03", root.transform, new Vector3(1.6f, 0f, 0f), new Vector3(0f, 90f, 0f));
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(-2.4f, 0.12f, 0.6f), new Vector3(90f, 80f, 0f), 1.3f));
            return Save(root);
        }

        // ---------------------------------------------------------------- Fire

        public static Material EmberMaterial()
        {
            const string path = L2Build.MaterialsPath + "/L2_Embers.mat";
            // Unlit HDR orange: glowing coals need no lighting, and Lit emission keywords get reset on validation.
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(unlit);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = unlit;
            mat.SetColor("_BaseColor", L1Build.Hex("#FF6A26") * 1.8f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // A burning spot (see L2Fire): flame tongues, a hot core, embers, a glow, optional smoke and light.
        public static Transform Fire(Transform parent, string name, Vector3 position, float size, float lightIntensity, float lightRange, bool smoke, Vector3 area = default)
        {
            return L2Fire.Fire(parent, name, position, size, lightIntensity, lightRange, smoke, area);
        }

        // A collapsed house front, still burning: seen over (knee height), never crossed.
        private static string BurningFront()
        {
            var root = new GameObject("L2_BurningFront");
            Part("Stubs", root.transform, Baked("BurnStub_Room07", "SM_Bld_House_Room_07", 1.0f, new Vector2(0f, 0.05f), 0.4f, 19), Vector3.zero, new Vector3(0f, 90f, 0f), 0.8f);
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(0.3f, 0.15f, -1.5f), new Vector3(90f, 10f, 0f), 1.4f));
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(-0.4f, 0.3f, 1.2f), new Vector3(75f, -25f, 0f), 1.2f));
            Fire(root.transform, "Fire", new Vector3(0f, 0.2f, 0f), 1f, 0f, 0f, false, new Vector3(2.8f, 0f, 5f));
            return Save(root);
        }

        // ---------------------------------------------------------------- Church

        // Three nave bays in a row (east bay whole, the others burnt open), a stacked tower at the south-east
        // corner, the west door on the front gable, and the fallen roof lying in the nave.
        private static string CollapsedChurch()
        {
            var root = new GameObject("L2_CollapsedChurch");
            var nave = L2Layout.ChurchNave;
            Vector3 origin = new Vector3(nave.center.x, 0f, nave.center.y);
            float[] bayX = { nave.xMax - 4.4f, nave.xMax - 13.2f, nave.xMax - 22f };
            Piece("K/Buildings/SM_Bld_Church_Room_01", root.transform, new Vector3(bayX[0], 0f, nave.center.y) - origin, new Vector3(0f, 90f, 0f), 1f, "Bay_East");
            Part("Bay_Middle", root.transform, Baked("Church_Bay_Middle", "SM_Bld_Church_Room_02", 4.6f, Vector2.zero, 1.2f, 21), new Vector3(bayX[1], 0f, nave.center.y) - origin, new Vector3(0f, 90f, 0f));
            Part("Bay_West", root.transform, Baked("Church_Bay_West", "SM_Bld_Church_Room_03", 3.4f, new Vector2(0f, -0.1f), 1.2f, 22), new Vector3(bayX[2], 0f, nave.center.y) - origin, new Vector3(0f, 90f, 0f));
            // The roof that fell in: charred rafters lying across each other on the nave floor, with rubble
            // where the upper walls came down.
            var rng = new System.Random(21);
            foreach (float bx in new[] { bayX[1], bayX[2] })
            {
                for (int i = 0; i < 9; i++)
                {
                    var p = new Vector3(bx + ((float)rng.NextDouble() - 0.5f) * 6f, 0.15f + (float)rng.NextDouble() * 0.5f, nave.center.y + ((float)rng.NextDouble() - 0.5f) * 3.5f) - origin;
                    Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, p, new Vector3(70f + (float)rng.NextDouble() * 25f, (float)rng.NextDouble() * 180f, ((float)rng.NextDouble() - 0.5f) * 30f), 1.4f + (float)rng.NextDouble() * 0.6f));
                }

                Charred(Piece("K/Environments/SM_Env_RockPile_02", root.transform, new Vector3(bx - 1.5f, -0.5f, nave.center.y - 1.8f) - origin, new Vector3(0f, 40f, 0f), 0.3f));
                Charred(Piece("K/Environments/SM_Env_RockPile_01", root.transform, new Vector3(bx + 2f, -0.5f, nave.center.y + 1.6f) - origin, new Vector3(0f, 200f, 0f), 0.28f));
            }

            // Embers still smouldering under the west bay's fallen rafters.
            Fire(root.transform, "Smoulder", new Vector3(bayX[2] + 0.5f, 0.2f, nave.center.y) - origin, 0.8f, 0f, 0f, true, new Vector3(3f, 0f, 2f));

            Vector3 tower = new Vector3(L2Layout.ChurchTower.x, 0f, L2Layout.ChurchTower.y) - origin;
            Piece("K/Buildings/SM_Bld_Church_TowerBase_01", root.transform, tower, Vector3.zero, 1f, "Tower_Base");
            Piece("K/Buildings/SM_Bld_Church_Tower_03", root.transform, tower + Vector3.up * 3.3f, Vector3.zero, 1f, "Tower_Mid");
            Piece("K/Buildings/SM_Bld_Church_Tower_01", root.transform, tower + Vector3.up * 6.6f, Vector3.zero, 0.84f, "Tower_Top");
            Piece("K/Buildings/SM_Bld_Church_Door_01", root.transform, new Vector3(nave.xMax + 0.1f, 0f, nave.center.y) - origin, new Vector3(0f, 90f, 0f), 1f, "Door");
            return Save(root);
        }

        // ---------------------------------------------------------------- Blockers

        // A route blocker that must read as impassable: a tall heap of burning timber and wreckage.
        private static string FireBlocker(string name, bool cart)
        {
            var root = new GameObject(name);
            if (cart)
            {
                Charred(Piece("A/Props/SM_Prop_Cart_01", root.transform, new Vector3(-0.6f, 0.4f, 0f), new Vector3(8f, 70f, 78f)));
                Charred(Piece("A/Props/SM_Prop_Logpile_01", root.transform, new Vector3(1.4f, 0f, 0.3f), new Vector3(0f, 15f, 0f)));
            }
            else
            {
                Charred(Piece("A/Props/SM_Prop_Barrel_01", root.transform, new Vector3(1.8f, 0f, 0.4f), new Vector3(0f, 10f, 0f)));
                Charred(Piece("K/Props/SM_Prop_CartWheel_01", root.transform, new Vector3(-1.9f, 0.5f, -0.3f), new Vector3(0f, 30f, 12f)));
            }

            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f;
                Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(-2.2f + i * 0.7f, 0.2f + (i % 3) * 0.35f, Mathf.Sin(a) * 0.5f),
                    new Vector3(60f + (i % 2) * 25f, 70f + i * 23f, (i % 3 - 1) * 20f), 1.3f));
            }

            Piece("K/Environments/SM_Env_RockPile_03", root.transform, new Vector3(0f, -0.6f, 0.2f), new Vector3(0f, 25f, 0f), 0.35f);
            Fire(root.transform, "Fire", new Vector3(0f, 0.6f, 0f), 1.5f, BlockerLight, 11f, true, new Vector3(3.6f, 0f, 1.2f));
            return Save(root);
        }

        // The crossroads' dead road: overturned carts and timber, cold (no fire).
        private static string DebrisBlocker()
        {
            var root = new GameObject("L2_DebrisBlocker");
            Charred(Piece("K/Props/SM_Prop_Cart_01", root.transform, new Vector3(-1f, 0.5f, 0f), new Vector3(0f, 80f, 85f)));
            Piece("A/Props/SM_Prop_Crate_01", root.transform, new Vector3(1.4f, 0f, 0.4f), new Vector3(0f, 20f, 0f));
            Piece("A/Props/SM_Prop_Crate_02", root.transform, new Vector3(1.6f, 0.8f, 0.2f), new Vector3(0f, 55f, 8f));
            Piece("A/Props/SM_Prop_Barrel_01", root.transform, new Vector3(2.3f, 0.35f, -0.6f), new Vector3(90f, 30f, 0f));
            for (int i = 0; i < 4; i++)
            {
                Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(-1.5f + i * 1f, 0.9f, 0.2f), new Vector3(70f, 80f + i * 30f, 10f), 1.5f));
            }

            Piece("K/Environments/SM_Env_RockPile_01", root.transform, new Vector3(0.2f, -0.7f, 0.5f), new Vector3(0f, 80f, 0f), 0.3f);
            return Save(root);
        }

        // ---------------------------------------------------------------- Village

        private static string MarketRemains()
        {
            var root = new GameObject("L2_MarketRemains");
            Piece("A/Buildings/SM_Bld_Stall_Cover_02", root.transform, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 6f));
            Charred(Piece("A/Props/SM_Prop_Stall_Table_01", root.transform, new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, 0f)));
            Piece("A/Props/SM_Prop_Sack_01", root.transform, new Vector3(1.4f, 0f, -0.6f), new Vector3(0f, 40f, 0f));
            Piece("A/Props/SM_Prop_Sack_03", root.transform, new Vector3(-1.2f, 0f, -0.9f), new Vector3(0f, 110f, 0f));
            Piece("A/Props/SM_Prop_Basket_02", root.transform, new Vector3(-0.4f, 0f, -1.3f), new Vector3(0f, 0f, 80f));
            Piece("A/Props/SM_Prop_Pot_02", root.transform, new Vector3(0.6f, 0f, -1.4f), new Vector3(0f, 20f, 0f));
            return Save(root, false);
        }

        // The archer's high ground: a broken parapet to shoot over and scorched timber around it (all knee-high).
        private static string ArcherPerch()
        {
            var root = new GameObject("L2_ArcherPerch");
            Part("Parapet", root.transform, Baked("Parapet_Rockwall", "SM_Bld_Rockwall_Straight_01", 1.0f, new Vector2(0f, 0.1f), 0.3f, 23), new Vector3(0f, 0f, -1.4f), new Vector3(0f, 90f, 0f), 0.9f);
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(1.4f, 0.12f, 0.6f), new Vector3(90f, 20f, 0f)));
            Piece("A/Props/SM_Prop_Barrel_01", root.transform, new Vector3(-1.5f, 0f, 0.8f), new Vector3(0f, 10f, 0f), 0.8f);
            return Save(root, false);
        }

        // ---------------------------------------------------------------- Story

        // Kael's recognition: a burnt doorway, its door kicked in, a strip of the Vanguard's red nailed to the frame.
        private static string LoreDoorframe()
        {
            var root = new GameObject("L2_Lore_ScorchedDoorframe");
            Part("Shell", root.transform, Baked("Shell_Room03", "SM_Bld_House_Room_03", 2.8f, new Vector2(-0.12f, 0f), 0.5f, 25), Vector3.zero, Vector3.zero);
            Charred(Piece("K/Buildings/SM_Bld_House_Door_01", root.transform, new Vector3(0.4f, 0.08f, -4.3f), new Vector3(-88f, 20f, 0f)));
            var rag = Piece("K/Props/SM_Prop_Banner_02", root.transform, new Vector3(-0.9f, 2.3f, -3.3f), new Vector3(0f, 0f, 8f), 0.45f, "VanguardRag");
            foreach (var r in rag.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = L2Build.Mat("L2_Vanguard_Cloth");
            }

            return Save(root);
        }

        private static string LoreWell()
        {
            var root = new GameObject("L2_Lore_Well");
            Piece("K/Buildings/SM_Bld_Village_Well_01", root.transform, Vector3.zero, Vector3.zero);
            Piece("A/Props/SM_Prop_Scroll_02", root.transform, new Vector3(-1.9f, 0.02f, 1f), new Vector3(0f, 30f, 0f));
            Piece("A/Environments/SM_Env_Rock_015", root.transform, new Vector3(-1.75f, 0f, 1.1f), Vector3.zero, 0.6f);
            Piece("A/Props/SM_Prop_Pot_01", root.transform, new Vector3(1.8f, 0f, -0.6f), new Vector3(0f, 0f, 95f));
            // A raider's torch, burnt out and dropped by the well.
            var torch = Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, new Vector3(1.4f, 0.08f, 1.5f), new Vector3(90f, 120f, 0f), 0.4f, "DroppedTorch"));
            var rag = Piece("K/Props/SM_Prop_Banner_03", torch.transform, new Vector3(0f, 2.3f, 0f), new Vector3(0f, 0f, 90f), 0.3f, "TorchRag");
            foreach (var r in rag.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = L2Build.Mat("L2_Vanguard_Cloth");
            }

            return Save(root, false);
        }

        private static string LootHeal()
        {
            var root = new GameObject("L2_Loot_Heal");
            Piece("A/Props/SM_Prop_Sack_02", root.transform, Vector3.zero, new Vector3(0f, 30f, 0f));
            Piece("A/Items/SM_Item_Potion_04", root.transform, new Vector3(0.35f, 0f, -0.2f), new Vector3(0f, 0f, 0f));
            return Save(root, false);
        }

        private static string LootWeapon()
        {
            var root = new GameObject("L2_Loot_Weapon");
            Piece("K/Weapons/SM_Wep_Broadsword_01", root.transform, new Vector3(0f, 0.85f, 0.2f), new Vector3(170f, 0f, 12f));
            Piece("A/Environments/SM_Env_Rock_014", root.transform, new Vector3(0.1f, 0f, 0.35f), Vector3.zero, 0.7f);
            return Save(root, false);
        }

        // The one-way shortcut: a heavy plank door filling the gatehouse arch (2.1 m), hinged on its west side,
        // with two iron-dark braces and a diagonal strut.
        private static string ShortcutGate()
        {
            var root = new GameObject("L2_ShortcutGate");
            var leaf = new GameObject("GateLeaf").transform;
            leaf.SetParent(root.transform, false);
            leaf.localPosition = new Vector3(-1.05f, 0f, 0.35f);   // hinge on the west side of the arch
            for (int i = 0; i < 8; i++)
            {
                Piece("K/Props/SM_Prop_Beam_01", leaf, new Vector3(0.13f + i * 0.26f, 0f, 0f), Vector3.zero, 1.02f, "Plank_" + i);
            }

            foreach (float y in new[] { 0.55f, 1.95f })
            {
                Charred(Piece("K/Props/SM_Prop_Beam_01", leaf, new Vector3(0.02f, y, 0.14f), new Vector3(0f, 0f, -90f), 0.84f, "Brace"));
            }

            Charred(Piece("K/Props/SM_Prop_Beam_01", leaf, new Vector3(0.25f, 0.55f, 0.16f), new Vector3(0f, 0f, -55f), 0.66f, "Strut"));
            return SaveWithMoving(root, leaf);
        }

        // The lever beside the gate, on the crossroads side: a post and a handle that tips over when pulled.
        private static string GateLever()
        {
            var root = new GameObject("L2_GateLever");
            Charred(Piece("K/Props/SM_Prop_Beam_01", root.transform, Vector3.zero, Vector3.zero, 0.5f, "Post"));
            Piece("A/Environments/SM_Env_Rock_05", root.transform, new Vector3(0f, -0.1f, 0f), Vector3.zero, 0.25f, "Footing");
            var handle = new GameObject("Handle").transform;
            handle.SetParent(root.transform, false);
            handle.localPosition = new Vector3(0f, 1.05f, 0f);
            handle.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            Piece("K/Props/SM_Prop_Beam_01", handle, Vector3.zero, Vector3.zero, 0.36f, "Arm");
            var rag = Piece("K/Props/SM_Prop_Banner_03", handle, new Vector3(0f, 0.85f, 0f), new Vector3(0f, 0f, 90f), 0.18f, "Rag");
            foreach (var r in rag.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = L2Build.Mat("L2_Vanguard_Cloth");
            }

            var lever = root.AddComponent<Kneel.GateLever>();
            var so = new SerializedObject(lever);
            so.FindProperty("handle").objectReferenceValue = handle;
            so.ApplyModifiedPropertiesWithoutUndo();
            return SaveWithMoving(root, handle);
        }

        // Saves a prefab whose moving part (hinge leaf, lever handle) stays out of static batching.
        private static string SaveWithMoving(GameObject root, Transform moving)
        {
            L1Build.StripColliders(root);
            L1Build.SetStatic(root, L1Build.PropStatic);
            foreach (var t in moving.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            }

            string name = root.name;
            L1Build.SavePrefab(root, L2Build.PrefabsPath + "/" + name + ".prefab");
            return name;
        }

        // ---------------------------------------------------------------- Raid evidence

        private static string OverturnedCart()
        {
            var root = new GameObject("L2_OverturnedCart");
            Charred(Piece("A/Props/SM_Prop_Cart_02", root.transform, new Vector3(0f, 0.85f, 0f), new Vector3(0f, 0f, 88f)));
            Piece("A/Props/SM_Prop_Sack_01", root.transform, new Vector3(-1.4f, 0f, 0.8f), new Vector3(0f, 60f, 70f));
            Piece("A/Props/SM_Prop_Sack_03", root.transform, new Vector3(-1.2f, 0f, -0.5f), new Vector3(0f, 10f, 0f));
            Piece("A/Props/SM_Prop_Basket_04", root.transform, new Vector3(-2f, 0f, 0.1f), new Vector3(0f, 0f, 75f));
            Piece("A/Props/SM_Prop_Cart_Wheel_01", root.transform, new Vector3(1.5f, 0.08f, 1.9f), new Vector3(0f, 40f, 90f));
            return Save(root, false);
        }

        // A dropped raider standard, trampled: the Vanguard's red, the only strong colour in the village.
        private static string DroppedBanner()
        {
            var root = new GameObject("L2_DroppedBanner");
            Charred(Piece("K/Buildings/SM_Bld_Castle_Flagpole_01", root.transform, new Vector3(0f, 0.1f, -1.2f), new Vector3(90f, 0f, 0f), 1.1f));
            var cloth = Piece("K/Props/SM_Prop_Banner_02", root.transform, new Vector3(0.15f, 0.08f, 0.3f), new Vector3(-88f, 0f, 0f));
            foreach (var r in cloth.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = L2Build.Mat("L2_Vanguard_Cloth");
            }

            return Save(root, false);
        }
    }
}
