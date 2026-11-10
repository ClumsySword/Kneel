using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Stage 5: the throne hall's systems (The Master.md §8), added to the open L5 scene in place (L5Level's Build Scene
    // is never rerun over it). Each step skips what's already there, so it can be rerun after a change:
    // - the four arena chandeliers on chains to winches at the pillars' feet (the y 422 one moves to y 443, over the
    //   arena), split at the bridle ring so the vault chain stays hanging when one falls;
    // - the four braziers as levers, and the floor fire they (and his Threads) leave;
    // - the arena floor cut into 30 panels over a burning undercroft, each with its hole, rubble lip and carve;
    // - four 5 m knight statues in the side bands, split at the knee;
    // - the darkness wave, the ash storm and the director;
    // - the Colossus's NavMesh agent, and both NavMeshes rebaked.
    public static partial class MasterSetup
    {
        private const string HallFolder = Folder + "Hall/";
        private const string HallMeshes = "Assets/Kneel/Meshes/L5/Kit/";
        public const string ColossusAgent = "Colossus";
        public const float ColossusAgentRadius = 1.6f;
        public const float ColossusAgentHeight = 6f;

        // The arena grid (decided 2026-11-10).
        private static readonly float[] ColumnEdges = { 40.5f, 46.5f, 51.5f, 56.5f, 61.5f, 67.5f };
        private const float RowStart = 432f;
        private const float RowDepth = 5.75f;
        private const int Rows = 6;
        private const float FloorTop = 9f;

        // The lava under the arena (owner, 2026-11-10: "lava, like outside the castle in L4, and the ember glow coming
        // out of it; the hole should not have empty space"): a fallen panel's hole is filled with it a little below
        // the rim.
        // Just under the slabs' undersides (they're 0.5 m thick), so a hole shows almost no wall above the lava.
        private const float LavaDepth = 0.45f;
        private const float LavaY = FloorTop - LavaDepth;

        [MenuItem("Kneel/Enemies/The Master/Build Hall Systems")]
        public static void BuildHallSystemsMenu() => Debug.Log(BuildHallSystems());

        public static string BuildHallSystems()
        {
            var log = new List<string>();
            EnsureFolder(HallFolder);
            EnsureFolder(HallFolder + "Materials");
            GameObject master = GameObject.Find("TheMaster");
            Transform hall = Child(master.transform, "Hall", Vector3.zero, Quaternion.identity);

            FloorFire fire = BuildFloorFire(hall);
            log.Add(BuildChandeliers(hall));
            log.Add(BuildBraziers(hall));
            HallFloor floor = BuildFloor(hall, log);
            log.Add(BuildStatues(hall));
            DarknessWave darkness = Ensure<DarknessWave>(Child(hall, "DarknessWave", Vector3.zero, Quaternion.identity).gameObject);
            AshStorm ash = BuildAshStorm(hall);

            var director = Ensure<BossArenaDirector>(master);
            director.boss = master.GetComponent<MasterBoss>();
            director.floor = floor;
            director.fire = fire;
            director.darkness = darkness;
            director.ashStorm = ash;
            Ensure<Kneel.Hazards.LevelReset>(master);

            log.Add(BakeHallNavMeshes(master));
            EditorSceneManager.MarkSceneDirty(master.scene);
            EditorSceneManager.SaveScene(master.scene);
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        // --- Shared bits ---

        private static Material HallMaterial(string name, string shader, Color color, Texture texture = null, bool additive = false, bool transparent = false)
        {
            string path = HallFolder + "Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find(shader);
            mat.SetColor("_BaseColor", color);
            if (texture != null)
                mat.SetTexture("_BaseMap", texture);
            if (transparent || additive)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", additive ? 2f : 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ShadowMaterial() =>
            HallMaterial("M_HallShadow", "Universal Render Pipeline/Unlit", new Color(0f, 0f, 0f, 0.8f), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Kneel/UI/Art/RadialSoft.png"), transparent: true);

        // A decal lying on the floor (the built-in quad, turned face up).
        private static GameObject FloorDecal(string name, Transform parent, Vector3 position, Vector2 size, float yaw, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, yaw, 0f));
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static Material Fx(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/" + name + ".mat");

        // A one-shot burst or a steady emitter from a box (x by z), particles rising (negative gravity) or falling.
        private static ParticleSystem Particles(string name, Transform parent, Vector3 position, Material material, Color color,
            Vector2 size, Vector2 life, Vector2 speed, Vector3 box, float rate, int burst, float gravity, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = loop;
            main.loop = loop;
            main.duration = loop ? 5f : 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(burst, Mathf.CeilToInt(rate * life.y * 1.3f)) + 8;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            if (burst > 0)
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        // --- Floor fire ---

        private static FloorFire BuildFloorFire(Transform hall)
        {
            var fire = Ensure<FloorFire>(Child(hall, "FloorFire", Vector3.zero, Quaternion.identity).gameObject);
            var slick = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kneel/Prefabs/L4/Hazards/L4_OilSlick.prefab");
            var so = new SerializedObject(fire);
            so.FindProperty("template").objectReferenceValue = slick.transform.Find("Burning").gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            fire.floorY = FloorTop;
            return fire;
        }

        // --- Chandeliers ---

        // Each chandelier's winch: the arena pillar it's tied to (alternating sides), by the chandelier's y.
        private static readonly (float z, Vector2 pillar)[] Tethers =
        {
            (436f, new Vector2(39f, 438f)),
            (443f, new Vector2(69f, 438f)),
            (450f, new Vector2(39f, 450f)),
            (463f, new Vector2(69f, 462f)),
        };

        private static string BuildChandeliers(Transform hall)
        {
            GameObject fixtures = GameObject.Find(HallFixturesPath);
            List<Transform> chandeliers = fixtures.transform.Cast<Transform>().Where(t => t.name == "Chandelier").ToList();
            var notes = new List<string>();
            Material shadow = ShadowMaterial();
            Material iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L4/L4_Iron.mat");

            foreach (Transform c in chandeliers)
            {
                // The one over the hall's entrance moves over the arena (§4, decided).
                if (Mathf.Abs(c.position.z - 422f) < 0.5f)
                    c.position = new Vector3(c.position.x, c.position.y, 443f);
            }

            for (int i = 0; i < chandeliers.Count; i++)
            {
                Transform c = chandeliers[i];
                var tie = Tethers.OrderBy(t => Mathf.Abs(t.z - c.position.z)).First();
                SplitChain(c, i);

                var drop = Ensure<ChandelierDrop>(c.gameObject);
                drop.chain = c.Find("Chain");
                drop.hangingLight = c.Find("Light")?.GetComponent<Light>();
                drop.floorY = FloorTop;

                // The winch at the pillar's foot, on the aisle side; the chain runs up the pillar, over its top and out
                // to the hoop.
                float side = tie.pillar.x < 54f ? -1f : 1f;
                Vector3 winchAt = new Vector3(tie.pillar.x + side * 2.45f, FloorTop, tie.pillar.y);
                Transform winch = c.parent.Find("Winch_" + i);
                if (winch == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kneel/Prefabs/L4/Hazards/L4_Winch.prefab");
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, c.parent);
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    go.name = "Winch_" + i;
                    Object.DestroyImmediate(go.GetComponent<Kneel.Hazards.Winch>());
                    Transform prompt = go.transform.Find("Prompt");
                    if (prompt != null)
                        Object.DestroyImmediate(prompt.gameObject);
                    winch = go.transform;
                }

                winch.SetPositionAndRotation(winchAt, Quaternion.Euler(0f, 90f, 0f));
                Transform strike = Child(winch, "Strike", winchAt + Vector3.up * 0.7f, winch.rotation);
                strike.gameObject.layer = LayerMask.NameToLayer("Enemy");
                var box = Ensure<BoxCollider>(strike.gameObject);
                box.size = new Vector3(2.2f, 1.4f, 1.1f);
                drop.winch = Ensure<HallStrikeTarget>(strike.gameObject);

                // The hoop's edge on the pillar's side.
                Vector3 hoop = c.position + new Vector3(side * 2.3f, 0.05f, 0f);
                float faceArena = tie.pillar.x - side * 1.95f;
                float faceAisle = tie.pillar.x + side * 1.95f;
                Transform tetherGo = Child(c, "Tether", c.position, Quaternion.identity);
                var line = Ensure<LineRenderer>(tetherGo.gameObject);
                line.useWorldSpace = true;
                line.positionCount = 5;
                line.SetPositions(new[]
                {
                    hoop,
                    new Vector3(faceArena, 18.3f, tie.pillar.y),
                    new Vector3(tie.pillar.x, 18.75f, tie.pillar.y),
                    new Vector3(faceAisle, 18.3f, tie.pillar.y),
                    winchAt + Vector3.up * 0.75f,
                });
                line.widthMultiplier = 0.07f;
                line.sharedMaterial = iron;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.numCornerVertices = 2;
                drop.tether = line;

                // Beside it, not under it: the shadow stays flat on the floor while it tilts as it falls.
                Transform shadowGo = c.parent.Find("Shadow_" + i);
                if (shadowGo == null)
                    shadowGo = FloorDecal("Shadow_" + i, c.parent, new Vector3(c.position.x, FloorTop + 0.03f, c.position.z), new Vector2(6f, 6f), 0f, shadow).transform;
                shadowGo.gameObject.SetActive(false);
                drop.shadow = shadowGo.gameObject;

                var wreck = Ensure<BoxCollider>(c.gameObject);
                wreck.center = new Vector3(0f, 0.3f, 0f);
                wreck.size = new Vector3(4.6f, 1.1f, 4.6f);
                wreck.enabled = false;
                drop.wreck = wreck;
                var carve = Ensure<NavMeshObstacle>(c.gameObject);
                carve.shape = NavMeshObstacleShape.Box;
                carve.center = new Vector3(0f, 0.5f, 0f);
                carve.size = new Vector3(4.8f, 2f, 4.8f);
                carve.carving = true;
                carve.enabled = false;
                drop.wreckCarve = carve;

                if (c.Find("SnapSparks") == null)
                    Particles("SnapSparks", c, winchAt, Fx("L1_FX_Ember"), new Color(1f, 0.7f, 0.3f), new Vector2(0.04f, 0.09f), new Vector2(0.3f, 0.8f), new Vector2(2f, 6f), new Vector3(0.4f, 0.4f, 0.4f), 0f, 40, 1f, false);
                if (c.Find("ImpactDust") == null)
                    Particles("ImpactDust", c, new Vector3(c.position.x, FloorTop + 0.2f, c.position.z), Fx("L1_FX_Smoke"), new Color(0.45f, 0.42f, 0.4f, 0.6f), new Vector2(1.2f, 2.4f), new Vector2(1.2f, 2.2f), new Vector2(1f, 3f), new Vector3(4f, 4f, 0.3f), 0f, 26, -0.05f, false);
                drop.snapSparks = c.Find("SnapSparks").GetComponent<ParticleSystem>();
                drop.impactDust = c.Find("ImpactDust").GetComponent<ParticleSystem>();
                notes.Add("chandelier y " + c.position.z + " tied to the pillar at (" + tie.pillar.x + ", " + tie.pillar.y + ")");
            }

            return "Chandeliers: " + string.Join("; ", notes);
        }

        // The vault chain (above the bridle ring, 2.55 m over the hoop) out of the chandelier's body into its own piece.
        private static void SplitChain(Transform chandelier, int index)
        {
            if (chandelier.Find("Chain") != null)
                return;
            Transform body = chandelier.Find("Body");
            var filter = body.GetComponent<MeshFilter>();
            Mesh mesh = filter.sharedMesh;
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            Vector2[] uv = mesh.uv;
            int subs = mesh.subMeshCount;
            var low = new List<int>[subs];
            var high = new List<int>[subs];
            for (int s = 0; s < subs; s++)
            {
                low[s] = new List<int>();
                high[s] = new List<int>();
                int[] tris = mesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    float y = (v[tris[i]].y + v[tris[i + 1]].y + v[tris[i + 2]].y) / 3f;
                    (y > 2.55f ? high[s] : low[s]).AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
                }
            }

            Mesh bodyMesh = Build(mesh.name, low, v, n, uv, Matrix4x4.identity);
            string path = AssetDatabase.GetAssetPath(mesh);
            mesh.Clear();
            EditorUtility.CopySerialized(bodyMesh, mesh);
            Object.DestroyImmediate(bodyMesh);
            Mesh chainMesh = SaveMesh(Build("L5_Hall_ChandelierChain_" + index, high, v, n, uv, Matrix4x4.identity), HallMeshes + "L5_Hall_ChandelierChain_" + index + ".asset");

            var chain = new GameObject("Chain");
            chain.transform.SetParent(chandelier, false);
            chain.AddComponent<MeshFilter>().sharedMesh = chainMesh;
            var r = chain.AddComponent<MeshRenderer>();
            var bodyRenderer = body.GetComponent<MeshRenderer>();
            r.sharedMaterials = bodyRenderer.sharedMaterials;
            r.renderingLayerMask = bodyRenderer.renderingLayerMask;
            r.shadowCastingMode = bodyRenderer.shadowCastingMode;

            // The fade covers the chain too.
            var fade = chandelier.GetComponent<OcclusionFade>();
            if (fade != null)
            {
                var so = new SerializedObject(fade);
                SerializedProperty renderers = so.FindProperty("renderers");
                renderers.arraySize++;
                renderers.GetArrayElementAtIndex(renderers.arraySize - 1).objectReferenceValue = r;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(mesh);
        }

        // --- Braziers ---

        private static string BuildBraziers(Transform hall)
        {
            Transform phase1 = GameObject.Find("L5_Root/Phase1").transform;
            Transform art = phase1.Find("Art");
            var notes = new List<string>();
            int i = 0;
            foreach (Transform brazier in art.Cast<Transform>().Where(t => t.name == "Brazier_Hazard").ToList())
            {
                Transform host = Child(hall, "Brazier_" + i, brazier.position, Quaternion.identity);
                var topple = Ensure<BrazierTopple>(host.gameObject);
                topple.art = brazier;
                topple.block = phase1.Cast<Transform>().FirstOrDefault(t => t.name == "Brazier" && HallActors.FlatDistance(t.position, brazier.position) < 0.5f);
                Transform strike = Child(brazier, "Strike", brazier.position + Vector3.up * 1f, Quaternion.identity);
                strike.gameObject.layer = LayerMask.NameToLayer("Enemy");
                var box = Ensure<BoxCollider>(strike.gameObject);
                box.size = new Vector3(1.2f, 2f, 1.2f);
                topple.strike = Ensure<HallStrikeTarget>(strike.gameObject);
                notes.Add("(" + brazier.position.x + ", " + brazier.position.z + ")");
                i++;
            }

            return "Braziers: " + string.Join(" ", notes);
        }

        // --- The floor ---

        private static HallFloor BuildFloor(Transform hall, List<string> log)
        {
            Transform floorRoot = Child(hall, "Floor", Vector3.zero, Quaternion.identity);
            var floor = Ensure<HallFloor>(floorRoot.gameObject);

            GameObject artFloor = GameObject.Find("L5_Root/Art/Floors/Hall");
            var artRenderer = artFloor.GetComponent<MeshRenderer>();
            Material ground = artRenderer.sharedMaterial;
            const string originalPath = "Assets/Kneel/Meshes/L5/Floors/L5_Floor_Hall.asset";
            Mesh original = AssetDatabase.LoadAssetAtPath<Mesh>(originalPath);
            System.Func<Vector3, Vector2> uvOf = FloorUv(original, artFloor.transform);

            float minX = ColumnEdges[0], maxX = ColumnEdges[ColumnEdges.Length - 1];
            float minZ = RowStart, maxZ = RowStart + Rows * RowDepth;
            Bounds art = TransformBounds(original.bounds, artFloor.transform);

            // The floor art without the arena: four rects round it (in the floor object's own space).
            var outer = new[]
            {
                Rect.MinMaxRect(art.min.x, art.min.z, art.max.x, minZ),
                Rect.MinMaxRect(art.min.x, maxZ, art.max.x, art.max.z),
                Rect.MinMaxRect(art.min.x, minZ, minX, maxZ),
                Rect.MinMaxRect(maxX, minZ, art.max.x, maxZ),
            };
            Mesh cut = SaveMesh(Slabs("L5_Floor_Hall_Cut", outer, FloorTop, 0f, uvOf, artFloor.transform.position), "Assets/Kneel/Meshes/L5/Floors/L5_Floor_Hall_Cut.asset");
            artFloor.GetComponent<MeshFilter>().sharedMesh = cut;

            if (floor.panels != null && floor.panels.Length == 30)
            {
                BuildLava(floorRoot, Rect.MinMaxRect(minX, minZ, maxX, maxZ));
                Transform pitFloor = floorRoot.Find("PitFloor");
                if (pitFloor != null)
                    pitFloor.position = new Vector3(pitFloor.position.x, LavaY - 0.65f, pitFloor.position.z);
                floor.lavaY = LavaY;
                Material shaftStone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L5/L5_StatueStone.mat");
                Material moltenEdge = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L4/L4_LavaCrack.mat");
                foreach (HallFloor.Panel panel in floor.panels)
                {
                    string meshName = "L5_HallPanel_" + panel.name.Substring("Panel_".Length);
                    panel.slab.GetComponent<MeshFilter>().sharedMesh = SaveMesh(Slabs(meshName, new[] { panel.rect }, FloorTop, 0.5f, uvOf, panel.slab.position), HallMeshes + meshName + ".asset");
                    panel.slab.GetComponent<MeshRenderer>().sharedMaterials = new[] { ground, shaftStone };
                    Transform shaftT = panel.hole.transform.Find("Shaft");
                    if (shaftT != null)
                        Object.DestroyImmediate(shaftT.gameObject);
                    BuildShaft(panel.hole.transform, panel.rect, moltenEdge);
                    panel.holeLight = BuildHoleFire(panel.hole.transform, panel.rect);
                }

                log.Add("Floor: already cut into panels (the floor art round the arena, the slabs and the shafts rebuilt)");
                return floor;
            }

            // The solid floor's collider without the arena; the pit's floor under it (Ignore Raycast: nothing aims at it).
            GameObject solid = GameObject.Find("L5_Root/ThroneHall/Floor");
            var slab = solid.GetComponent<BoxCollider>();
            Bounds sb = slab.bounds;
            slab.enabled = false;
            var colliderRects = new[]
            {
                Rect.MinMaxRect(sb.min.x, sb.min.z, sb.max.x, minZ),
                Rect.MinMaxRect(sb.min.x, maxZ, sb.max.x, sb.max.z),
                Rect.MinMaxRect(sb.min.x, minZ, minX, maxZ),
                Rect.MinMaxRect(maxX, minZ, sb.max.x, maxZ),
            };
            for (int i = 0; i < colliderRects.Length; i++)
            {
                Rect r = colliderRects[i];
                Transform part = Child(solid.transform.parent, "FloorSolid_" + i, new Vector3(r.center.x, sb.center.y, r.center.y), Quaternion.identity);
                part.gameObject.layer = solid.layer;
                var bc = Ensure<BoxCollider>(part.gameObject);
                bc.size = new Vector3(r.width, sb.size.y, r.height);
            }

            Transform pit = Child(floorRoot, "PitFloor", new Vector3((minX + maxX) * 0.5f, LavaY - 0.65f, (minZ + maxZ) * 0.5f), Quaternion.identity);
            pit.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            Ensure<BoxCollider>(pit.gameObject).size = new Vector3(maxX - minX, 0.5f, maxZ - minZ);

            BuildLava(floorRoot, Rect.MinMaxRect(minX, minZ, maxX, maxZ));

            // The hall's baked dressing on the arena floor (the processional kerbs) goes with the panels.
            GameObject hallBaked = GameObject.Find("L5_Root/Art/Hall/Baked");
            Mesh dressing = hallBaked.GetComponent<MeshFilter>().sharedMesh;
            Material[] dressingMaterials = hallBaked.GetComponent<MeshRenderer>().sharedMaterials;
            Vector3[] dv = dressing.vertices;
            Vector3[] dn = dressing.normals;
            Vector2[] duv = dressing.uv;
            Matrix4x4 dressingToWorld = hallBaked.transform.localToWorldMatrix;
            var keep = new List<int>[dressing.subMeshCount];
            var panelTris = new List<int>[Rows * 5][];
            for (int p = 0; p < panelTris.Length; p++)
            {
                panelTris[p] = new List<int>[dressing.subMeshCount];
                for (int s = 0; s < dressing.subMeshCount; s++)
                    panelTris[p][s] = new List<int>();
            }

            var rects = new Rect[Rows * 5];
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < 5; col++)
                    rects[row * 5 + col] = Rect.MinMaxRect(ColumnEdges[col], RowStart + row * RowDepth, ColumnEdges[col + 1], RowStart + (row + 1) * RowDepth);
            }

            for (int s = 0; s < dressing.subMeshCount; s++)
            {
                keep[s] = new List<int>();
                int[] tris = dressing.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 c = dressingToWorld.MultiplyPoint3x4((dv[tris[i]] + dv[tris[i + 1]] + dv[tris[i + 2]]) / 3f);
                    int owner = -1;
                    if (c.y < FloorTop + 1.5f && c.x > minX + 0.6f && c.x < maxX - 0.6f)
                    {
                        for (int p = 0; p < rects.Length && owner < 0; p++)
                        {
                            if (rects[p].Contains(new Vector2(c.x, c.z)))
                                owner = p;
                        }
                    }

                    (owner < 0 ? keep[s] : panelTris[owner][s]).AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
                }
            }

            Mesh rest = Build(dressing.name, keep, dv, dn, duv, Matrix4x4.identity);
            dressing.Clear();
            EditorUtility.CopySerialized(rest, dressing);
            Object.DestroyImmediate(rest);
            EditorUtility.SetDirty(dressing);

            // Panels.
            Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L5/L5_StatueStone.mat");
            Material crackMat = CrackMaterial();
            var rubble = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kneel/Prefabs/L4/Kit/L4_RubbleLow.prefab");
            var props = GameObject.Find("L5_Root/Art/Hall").transform.Cast<Transform>().Where(t => t.name.StartsWith("SM_Icon_")).ToList();
            var panels = new List<HallFloor.Panel>();
            var random = new System.Random(5150);
            for (int index = 0; index < rects.Length; index++)
            {
                Rect r = rects[index];
                int row = index / 5, col = index % 5;
                string name = "Panel_" + col + "_" + row;
                Vector3 centre = new Vector3(r.center.x, FloorTop, r.center.y);
                Transform panel = Child(floorRoot, name, centre, Quaternion.identity);

                // The slab that falls: the floor's own surface (world-mapped, so no seam) and its dressing.
                Transform slabT = Child(panel, "Slab", centre + Vector3.down * 0.25f, Quaternion.identity);
                Ensure<MeshFilter>(slabT.gameObject).sharedMesh = SaveMesh(Slabs("L5_HallPanel_" + col + "_" + row, new[] { r }, FloorTop, 0.5f, uvOf, slabT.position), HallMeshes + "L5_HallPanel_" + col + "_" + row + ".asset");
                var slabRenderer = Ensure<MeshRenderer>(slabT.gameObject);
                slabRenderer.sharedMaterials = new[] { ground, stone };
                slabRenderer.renderingLayerMask = artRenderer.renderingLayerMask;
                if (panelTris[index].Any(l => l.Count > 0))
                {
                    Transform detail = Child(slabT, "Dressing", Vector3.zero, Quaternion.identity);
                    detail.localPosition = Vector3.zero;
                    Matrix4x4 toSlab = slabT.worldToLocalMatrix * dressingToWorld;
                    Ensure<MeshFilter>(detail.gameObject).sharedMesh = SaveMesh(Build("L5_HallPanelDressing_" + col + "_" + row, panelTris[index], dv, dn, duv, toSlab), HallMeshes + "L5_HallPanelDressing_" + col + "_" + row + ".asset");
                    Ensure<MeshRenderer>(detail.gameObject).sharedMaterials = dressingMaterials;
                }

                foreach (Transform prop in props)
                {
                    if (r.Contains(new Vector2(prop.position.x, prop.position.z)))
                        prop.SetParent(slabT, true);
                }

                Transform top = Child(panel, "Top", centre + Vector3.down * 0.25f, Quaternion.identity);
                top.gameObject.layer = solid.layer;
                Ensure<BoxCollider>(top.gameObject).size = new Vector3(r.width, 0.5f, r.height);

                GameObject crack = FloorDecal("Crack", panel, centre + Vector3.up * 0.02f, new Vector2(r.width - 0.3f, r.height - 0.3f), random.Next(2) * 180f, crackMat);
                crack.SetActive(false);

                Transform hole = Child(panel, "Hole", centre, Quaternion.identity);
                BuildHole(hole, r, AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L4/L4_LavaCrack.mat"), rubble, random);
                hole.gameObject.SetActive(false);

                var carve = Ensure<NavMeshObstacle>(panel.gameObject);
                carve.shape = NavMeshObstacleShape.Box;
                carve.center = Vector3.zero;
                carve.size = new Vector3(r.width + 0.6f, 3f, r.height + 0.6f);
                carve.carving = true;
                carve.enabled = false;

                // Never: the centre column (the arena can't split), and the panels under the candelabras by the dais.
                bool never = col == 2 || (row == Rows - 1 && (col == 0 || col == 4));
                panels.Add(new HallFloor.Panel { name = name, rect = r, neverFalls = never, slab = slabT, top = top.GetComponent<Collider>(), crack = crack, hole = hole.gameObject, carve = carve, holeLight = BuildHoleFire(hole, r) });
            }

            floor.panels = panels.ToArray();
            floor.f1 = panels.FindIndex(p => p.rect.Contains(new Vector2(43.5f, 440f)));
            floor.floorY = FloorTop;
            floor.lavaY = LavaY;
            if (floorRoot.Find("Dust") == null)
                Particles("Dust", floorRoot, Vector3.zero, Fx("L1_FX_Smoke"), new Color(0.5f, 0.46f, 0.42f, 0.5f), new Vector2(0.6f, 1.4f), new Vector2(1f, 2f), new Vector2(0.2f, 0.8f), new Vector3(5f, 5f, 0.2f), 14f, 0, -0.02f, false);
            floor.dust = floorRoot.Find("Dust").GetComponent<ParticleSystem>();
            ParticleSystem.MainModule dustMain = floor.dust.main;
            dustMain.duration = 2f;
            log.Add("Floor: 30 panels (" + panels.Count(p => p.neverFalls) + " never fall), F1 = " + panels[floor.f1].name + ", dressing on " + panelTris.Count(t => t.Any(l => l.Count > 0)) + " panels");
            return floor;
        }

        private static Bounds TransformBounds(Bounds local, Transform t)
        {
            var b = new Bounds(t.TransformPoint(local.min), Vector3.zero);
            b.Encapsulate(t.TransformPoint(local.max));
            return b;
        }

        // The floor art's UVs as a function of world position (fitted from its top face), so the panels match it.
        private static System.Func<Vector3, Vector2> FloorUv(Mesh mesh, Transform t)
        {
            Vector3[] v = mesh.vertices;
            Vector2[] uv = mesh.uv;
            var top = Enumerable.Range(0, v.Length).Where(i => mesh.normals[i].y > 0.9f).Select(i => (p: t.TransformPoint(v[i]), uv: uv.Length > i ? uv[i] : Vector2.zero)).ToList();
            if (top.Count < 3)
                return p => new Vector2(p.x, p.z);

            // uv = a*x + b*z + c, per component, least squares over the top's corners.
            double sxx = 0, sxz = 0, szz = 0, sx = 0, sz = 0, n = top.Count;
            double[] sxu = new double[2], szu = new double[2], su = new double[2];
            foreach (var (p, u) in top)
            {
                sxx += p.x * p.x; sxz += p.x * p.z; szz += p.z * p.z; sx += p.x; sz += p.z;
                for (int k = 0; k < 2; k++)
                {
                    sxu[k] += p.x * u[k]; szu[k] += p.z * u[k]; su[k] += u[k];
                }
            }

            var m = new double[,] { { sxx, sxz, sx }, { sxz, szz, sz }, { sx, sz, n } };
            var coef = new double[2][];
            for (int k = 0; k < 2; k++)
                coef[k] = Solve3(m, new[] { sxu[k], szu[k], su[k] });
            return p => new Vector2((float)(coef[0][0] * p.x + coef[0][1] * p.z + coef[0][2]), (float)(coef[1][0] * p.x + coef[1][1] * p.z + coef[1][2]));
        }

        private static double[] Solve3(double[,] a, double[] b)
        {
            double det(double[,] m) => m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]) + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
            double d = det(a);
            var x = new double[3];
            for (int c = 0; c < 3; c++)
            {
                var m = (double[,])a.Clone();
                for (int r = 0; r < 3; r++)
                    m[r, c] = b[r];
                x[c] = System.Math.Abs(d) > 1e-9 ? det(m) / d : 0;
            }

            return x;
        }

        // Boxes over the rects, `thickness` deep under `top` (0: the top faces only), in space relative to `origin`.
        private static Mesh Slabs(string name, Rect[] rects, float top, float thickness, System.Func<Vector3, Vector2> uvOf, Vector3 origin)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            var sideTris = new List<int>();
            var tangents = new List<Vector4>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = v.Count;
                bool flat = Mathf.Abs(normal.y) > 0.5f;
                Vector3 along = flat ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
                foreach (Vector3 p in new[] { a, b, c, d })
                {
                    v.Add(p - origin);
                    n.Add(normal);
                    uv.Add(flat ? uvOf(p) : new Vector2(Vector3.Dot(p, along), p.y));
                    tangents.Add(new Vector4(along.x, along.y, along.z, 1f));
                }

                // The floor's ground shader only shades faces that look up (on a side it returns NaN, which the
                // bloom spreads into a white disc): sides and the bottom are a second submesh, in stone.
                (normal.y > 0.5f ? tris : sideTris).AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            foreach (Rect r in rects)
            {
                float y0 = top - thickness;
                Vector3 a = new Vector3(r.xMin, top, r.yMin), b = new Vector3(r.xMin, top, r.yMax), c = new Vector3(r.xMax, top, r.yMax), d = new Vector3(r.xMax, top, r.yMin);
                Quad(a, b, c, d, Vector3.up);
                if (thickness <= 0f)
                    continue;
                Vector3 down = Vector3.down * thickness;
                Quad(d + down, c + down, b + down, a + down, Vector3.down);
                Quad(a + down, a, d, d + down, Vector3.back);
                Quad(c + down, c, b, b + down, Vector3.forward);
                Quad(b + down, b, a, a + down, Vector3.left);
                Quad(d + down, d, c, c + down, Vector3.right);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = thickness > 0f ? 2 : 1;
            mesh.SetTriangles(tris, 0);
            if (thickness > 0f)
                mesh.SetTriangles(sideTris, 1);
            mesh.SetTangents(tangents);
            mesh.RecalculateBounds();
            return mesh;
        }

        // The lava under the arena, as L4's river of it outside the castle: one faceted sheet (flat-shaded, a hand's
        // swell so it catches the light facet by facet), L4's crust of dark plates split by glowing cracks, drifting
        // and breathing (Kneel.LavaFlow, its cracks a little finer than L4's so a 6 m hole shows several). Only the
        // holes show it: the panels cover the rest.
        private static void BuildLava(Transform floorRoot, Rect area)
        {
            Transform old = floorRoot.Find("Undercroft");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            const float cell = 0.75f;
            int nx = Mathf.CeilToInt(area.width / cell), nz = Mathf.CeilToInt(area.height / cell);
            var surface = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                    surface[i, j] = LavaY + (Mathf.PerlinNoise(i * 0.37f + 3.1f, j * 0.37f + 7.7f) - 0.5f) * 0.16f;

            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int k = v.Count;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    v.Add(p);
                    uv.Add(new Vector2(p.x, p.z));
                }

                tris.AddRange(new[] { k, k + 1, k + 2 });
            }

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float x = area.xMin + i * cell, z = area.yMin + j * cell;
                    var a = new Vector3(x, surface[i, j], z);
                    var b = new Vector3(x, surface[i, j + 1], z + cell);
                    var c = new Vector3(x + cell, surface[i + 1, j + 1], z + cell);
                    var d = new Vector3(x + cell, surface[i + 1, j], z);
                    if (((i + j) & 1) == 0)
                    {
                        Tri(a, b, c);
                        Tri(a, c, d);
                    }
                    else
                    {
                        Tri(a, b, d);
                        Tri(b, c, d);
                    }
                }
            }

            var mesh = new Mesh { name = "L5_HallLava", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            Transform lava = Child(floorRoot, "Lava", Vector3.zero, Quaternion.identity);
            Ensure<MeshFilter>(lava.gameObject).sharedMesh = SaveMesh(mesh, HallMeshes + "L5_HallLava.asset");
            var r = Ensure<MeshRenderer>(lava.gameObject);
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L4/L4_Lava.mat");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var flow = Ensure<Kneel.LavaFlow>(lava.gameObject);
            var so = new SerializedObject(flow);
            so.FindProperty("tiling").floatValue = 0.08f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The ember glow out of a hole: sparks lifting off the lava, a warm haze low over it, a tongue of flame
        // breaking through the crust, and a warm light thrown up the rubble (HallFloor keeps the newest few lit).
        private static Light BuildHoleFire(Transform hole, Rect r)
        {
            foreach (string old in new[] { "Embers", "Glow", "Breakout", "Uplight", "Heat" })
            {
                Transform t = hole.Find(old);
                if (t != null)
                    Object.DestroyImmediate(t.gameObject);
            }

            Vector3 c = hole.position;
            Vector3 surface = new Vector3(c.x, LavaY + 0.05f, c.z);
            ParticleSystem embers = Particles("Embers", hole, surface, Fx("L1_FX_Ember"), new Color(1f, 0.6f, 0.25f), new Vector2(0.04f, 0.11f), new Vector2(1.6f, 3.2f), new Vector2(1.2f, 3.2f), new Vector3(r.width * 0.85f, r.height * 0.85f, 0.2f), 18f, 0, -0.05f, true);
            ParticleSystem.NoiseModule noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            Particles("Glow", hole, surface + Vector3.up * 0.4f, Fx("L1_FX_Glow"), new Color(1f, 0.42f, 0.12f, 0.35f), new Vector2(2.4f, 3.6f), new Vector2(1.6f, 2.6f), new Vector2(0f, 0.15f), new Vector3(r.width * 0.6f, r.height * 0.6f, 0.2f), 2.5f, 0, 0f, true);
            Particles("Heat", hole, surface + Vector3.up * 0.2f, Fx("L1_FX_Smoke"), new Color(0.35f, 0.3f, 0.28f, 0.25f), new Vector2(1.2f, 2.4f), new Vector2(2.5f, 4f), new Vector2(0.4f, 0.9f), new Vector3(r.width * 0.7f, r.height * 0.7f, 0.2f), 3f, 0, -0.02f, true);
            L2Fire.Fire(hole, "Breakout", surface + new Vector3(r.width * 0.15f, 0f, -r.height * 0.1f), 0.8f, 0f, 0f, false, new Vector3(r.width * 0.45f, 0f, r.height * 0.45f));

            var lightGo = new GameObject("Uplight");
            lightGo.transform.SetParent(hole, false);
            lightGo.transform.position = surface + Vector3.up * 1.6f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.35f, 0.11f);
            light.intensity = 7f;
            light.range = 9f;
            light.shadows = LightShadows.None;
            return light;
        }

        // A fallen panel's hole: the shaft's stone walls down to the undercroft, a broken-rubble lip round the rim, and
        // invisible walls (Ignore Raycast) on the rim that stop walking, rolling and jumping in; embers rising out.
        private static void BuildHole(Transform hole, Rect r, Material stone, GameObject rubble, System.Random random)
        {
            Vector3 c = hole.position;
            BuildShaft(hole, r, stone);
            Vector3 p00 = new Vector3(r.xMin, FloorTop, r.yMin), p01 = new Vector3(r.xMin, FloorTop, r.yMax), p11 = new Vector3(r.xMax, FloorTop, r.yMax), p10 = new Vector3(r.xMax, FloorTop, r.yMin);
            BuildLip(hole, r, rubble, random, p00, p01, p11, p10, c);
        }

        private static void BuildShaft(Transform hole, Rect r, Material stone)
        {
            Vector3 c = hole.position;
            const float depth = LavaDepth + 0.3f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tangents = new List<Vector4>();
            var tris = new List<int>();

            // Each wall twice (inside and out), every vertex with its own normal, UV and tangent: a degenerate one
            // shades to NaN, and the bloom spreads a NaN pixel into a white disc.
            void Face(Vector3 a, Vector3 b, Vector3 normal)
            {
                Vector3 along = (b - a).normalized;
                int i0 = v.Count;
                Vector3[] corners = { a, b, b + Vector3.down * depth, a + Vector3.down * depth };
                foreach (Vector3 p in corners)
                {
                    v.Add(p - c);
                    n.Add(normal);
                    uv.Add(new Vector2(Vector3.Dot(p, along), p.y));
                    tangents.Add(new Vector4(along.x, along.y, along.z, 1f));
                }

                // Wound so the face is seen from the side `normal` points to.
                bool flip = Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), normal) > 0f;
                tris.AddRange(flip ? new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 } : new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            }

            Vector3 p00 = new Vector3(r.xMin, FloorTop, r.yMin), p01 = new Vector3(r.xMin, FloorTop, r.yMax), p11 = new Vector3(r.xMax, FloorTop, r.yMax), p10 = new Vector3(r.xMax, FloorTop, r.yMin);
            foreach (var (a, b, inward) in new[] { (p00, p01, Vector3.right), (p01, p11, Vector3.back), (p11, p10, Vector3.left), (p10, p00, Vector3.forward) })
            {
                Face(a, b, inward);
                Face(b, a, -inward);
            }

            var shaft = new Mesh { name = "Shaft" };
            shaft.SetVertices(v);
            shaft.SetNormals(n);
            shaft.SetUVs(0, uv);
            shaft.SetTangents(tangents);
            shaft.SetTriangles(tris, 0);
            shaft.RecalculateBounds();
            string path = HallMeshes + "L5_HallShaft_" + Mathf.RoundToInt(r.xMin * 10) + "_" + Mathf.RoundToInt(r.yMin * 10) + ".asset";
            Transform walls = Child(hole, "Shaft", c, Quaternion.identity);
            Ensure<MeshFilter>(walls.gameObject).sharedMesh = SaveMesh(shaft, path);
            Ensure<MeshRenderer>(walls.gameObject).sharedMaterial = stone;
        }

        private static void BuildLip(Transform hole, Rect r, GameObject rubble, System.Random random, Vector3 p00, Vector3 p01, Vector3 p11, Vector3 p10, Vector3 c)
        {
            // The lip: low rubble along the rim, half over the hole.
            var edges = new[] { (p00, p01), (p01, p11), (p11, p10), (p10, p00) };
            int k = 0;
            foreach (var (a, b) in edges)
            {
                float length = Vector3.Distance(a, b);
                int count = Mathf.Max(2, Mathf.RoundToInt(length / 1.5f));
                for (int i = 0; i < count; i++)
                {
                    Vector3 at = Vector3.Lerp(a, b, (i + 0.5f) / count);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(rubble, hole);
                    go.name = "Rubble_" + k++;
                    go.transform.SetPositionAndRotation(at + Vector3.down * 0.05f, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                    float s = 0.75f + (float)random.NextDouble() * 0.25f;
                    go.transform.localScale = new Vector3(s, 0.6f + (float)random.NextDouble() * 0.25f, s);
                }

                Transform wall = Child(hole, "Lip_" + k, (a + b) * 0.5f + Vector3.up * 1.5f, Quaternion.LookRotation(b - a));
                wall.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                Ensure<BoxCollider>(wall.gameObject).size = new Vector3(0.5f, 3f, length + 0.5f);
            }


        }

        // The undercroft: L4's lava, burning low (at full glow a 6 m hole floods the top-down frame with bloom).
        private static Material UndercroftMaterial()
        {
            string path = HallFolder + "Materials/M_HallUndercroft.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L4/L4_Lava.mat"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", new Color(0.35f, 0.3f, 0.28f));
            mat.SetColor("_EmissionColor", new Color(1.3f, 0.4f, 0.1f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Glowing ember cracks for a panel's tell (additive, so it brightens the floor under it).
        private static Material CrackMaterial()
        {
            string texPath = HallFolder + "Materials/T_HallCrack.png";
            if (File.Exists(texPath) == false)
            {
                const int size = 256;
                var alpha = new float[size, size];
                var rnd = new System.Random(77);
                void Walk(Vector2 p, float angle, int steps, float width)
                {
                    for (int s = 0; s < steps; s++)
                    {
                        angle += (float)(rnd.NextDouble() - 0.5) * 0.9f;
                        p += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2f;
                        for (int dx = -3; dx <= 3; dx++)
                            for (int dy = -3; dy <= 3; dy++)
                            {
                                int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                                if (x < 0 || y < 0 || x >= size || y >= size)
                                    continue;
                                float d = Mathf.Sqrt(dx * dx + dy * dy);
                                alpha[x, y] = Mathf.Max(alpha[x, y], Mathf.Clamp01(1f - d / width));
                            }

                        if (rnd.NextDouble() < 0.06 && width > 1.2f)
                            Walk(p, angle + (rnd.NextDouble() < 0.5 ? 1f : -1f) * 0.9f, steps / 2, width * 0.7f);
                    }
                }

                for (int i = 0; i < 6; i++)
                    Walk(new Vector2(128f, 128f), i * Mathf.PI / 3f + (float)rnd.NextDouble() * 0.5f, 70, 2.6f);
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int x = 0; x < size; x++)
                    for (int y = 0; y < size; y++)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha[x, y]));
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return HallMaterial("M_HallCrack", "Universal Render Pipeline/Particles/Unlit", new Color(4f, 1.3f, 0.35f, 1f), AssetDatabase.LoadAssetAtPath<Texture2D>(texPath), additive: true);
        }

        // --- Statues ---

        private static readonly (Vector2 at, float yaw)[] StatueSpots =
        {
            (new Vector2(36f, 441.7f), 90f),
            (new Vector2(36f, 458.3f), 90f),
            (new Vector2(72f, 441.7f), -90f),
            (new Vector2(72f, 458.3f), -90f),
        };

        private static string BuildStatues(Transform hall)
        {
            Transform root = Child(hall, "Statues", Vector3.zero, Quaternion.identity);
            var synty = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/SyntyStudios/PolygonKnights/Models/SM_Prop_Statue_01.fbx");
            if (synty == null)
                synty = AssetDatabase.LoadAllAssetsAtPath("Assets/SyntyStudios/PolygonKnights/Models/SM_Prop_Statue_01.fbx").OfType<Mesh>().First();
            Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L5/L5_StatueStone.mat");
            Material shadow = ShadowMaterial();
            const float height = 5f;
            float scale = height / synty.bounds.size.y;
            const float knee = 1f;

            // Split the Synty mesh (used, never edited) at the knee into two saved meshes.
            Vector3[] v = synty.vertices;
            Vector3[] n = synty.normals;
            Vector2[] uv = synty.uv;
            var lower = new List<int>[synty.subMeshCount];
            var upper = new List<int>[synty.subMeshCount];
            for (int s = 0; s < synty.subMeshCount; s++)
            {
                lower[s] = new List<int>();
                upper[s] = new List<int>();
                int[] tris = synty.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                    ((v[tris[i]].y + v[tris[i + 1]].y + v[tris[i + 2]].y) / 3f > knee ? upper[s] : lower[s]).AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
            }

            Vector3 pivot = new Vector3(0f, knee, synty.bounds.center.z + 0.15f);
            Mesh lowerMesh = SaveMesh(Build("L5_HallStatue_Lower", lower, v, n, uv, Matrix4x4.Scale(Vector3.one * scale)), HallMeshes + "L5_HallStatue_Lower.asset");
            Mesh upperMesh = SaveMesh(Build("L5_HallStatue_Upper", upper, v, n, uv, Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(-pivot)), HallMeshes + "L5_HallStatue_Upper.asset");
            float upperLength = (synty.bounds.max.y - knee) * scale;

            for (int i = 0; i < StatueSpots.Length; i++)
            {
                var (at, yaw) = StatueSpots[i];
                Transform statue = Child(root, "Statue_" + i, new Vector3(at.x, FloorTop, at.y), Quaternion.Euler(0f, yaw, 0f));
                statue.gameObject.layer = LayerMask.NameToLayer("Obstacles");
                Transform lowerT = Child(statue, "Lower", statue.position, statue.rotation);
                Ensure<MeshFilter>(lowerT.gameObject).sharedMesh = lowerMesh;
                Ensure<MeshRenderer>(lowerT.gameObject).sharedMaterial = stone;
                lowerT.gameObject.layer = statue.gameObject.layer;
                var lowerBox = Ensure<BoxCollider>(lowerT.gameObject);
                lowerBox.center = new Vector3(0f, knee * scale * 0.5f, 0f);
                lowerBox.size = new Vector3(1.4f, knee * scale, 1.4f);

                Transform kneeT = Child(statue, "Knee", statue.TransformPoint(pivot * scale), statue.rotation);
                Ensure<MeshFilter>(kneeT.gameObject).sharedMesh = upperMesh;
                Ensure<MeshRenderer>(kneeT.gameObject).sharedMaterial = stone;
                kneeT.gameObject.layer = statue.gameObject.layer;
                var upperBox = Ensure<BoxCollider>(kneeT.gameObject);
                upperBox.center = new Vector3(0f, upperLength * 0.5f, -0.15f * scale);
                upperBox.size = new Vector3(1.3f, upperLength, 1.3f);

                var hs = Ensure<HallStatue>(statue.gameObject);
                hs.upper = kneeT;
                hs.length = upperLength + 0.3f;
                var carve = Ensure<NavMeshObstacle>(statue.gameObject);
                carve.shape = NavMeshObstacleShape.Box;
                carve.center = new Vector3(0f, 0.5f, pivot.z * scale + upperLength * 0.5f);
                carve.size = new Vector3(1.8f, 2f, upperLength + 0.4f);
                carve.carving = true;
                carve.enabled = false;
                hs.fallenCarve = carve;

                Transform shadowT = statue.Find("Shadow");
                if (shadowT == null)
                {
                    Vector3 mid = statue.position + statue.forward * (upperLength * 0.5f + 0.3f) + Vector3.up * 0.03f;
                    shadowT = FloorDecal("Shadow", statue, mid, new Vector2(2.2f, upperLength + 0.6f), yaw, shadow).transform;
                }

                shadowT.gameObject.SetActive(false);
                hs.shadow = shadowT.gameObject;
                if (statue.Find("CrackDust") == null)
                    Particles("CrackDust", statue, kneeT.position, Fx("L1_FX_Smoke"), new Color(0.55f, 0.52f, 0.48f, 0.6f), new Vector2(0.3f, 0.7f), new Vector2(0.8f, 1.6f), new Vector2(0.3f, 1f), new Vector3(1.2f, 1.2f, 0.4f), 18f, 6, 0.2f, false);
                if (statue.Find("ImpactDust") == null)
                    Particles("ImpactDust", statue, statue.position + statue.forward * (upperLength * 0.6f) + Vector3.up * 0.2f, Fx("L1_FX_Smoke"), new Color(0.45f, 0.42f, 0.4f, 0.6f), new Vector2(1f, 2.2f), new Vector2(1.2f, 2.2f), new Vector2(1f, 3f), new Vector3(1.6f, upperLength, 0.3f), 0f, 24, -0.05f, false);
                hs.crackDust = statue.Find("CrackDust").GetComponent<ParticleSystem>();
                hs.impactDust = statue.Find("ImpactDust").GetComponent<ParticleSystem>();
                ParticleSystem.MainModule crackMain = hs.crackDust.main;
                crackMain.duration = 1.5f;
            }

            return "Statues: 4 at x 36 and 72, y 441.7 and 458.3, " + height + " m (x" + scale.ToString("F2") + "), split at the knee";
        }

        // --- Ash storm ---

        private static AshStorm BuildAshStorm(Transform hall)
        {
            Transform root = Child(hall, "AshStorm", Vector3.zero, Quaternion.identity);
            var storm = Ensure<AshStorm>(root.gameObject);
            ParticleSystem Side(string name, float x, float inward)
            {
                Transform t = root.Find(name);
                if (t == null)
                {
                    ParticleSystem ps = Particles(name, root, new Vector3(x, FloorTop + 2f, 452f), Fx("L1_FX_Smoke"), new Color(0.5f, 0.48f, 0.46f, 0.45f), new Vector2(1.5f, 3f), new Vector2(3f, 5f), new Vector2(0f, 0f), new Vector3(6f, 40f, 4f), 60f, 0, 0f, true);
                    ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.World;
                    vel.x = new ParticleSystem.MinMaxCurve(inward * 0.8f, inward * 2f);
                    vel.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.3f);
                    vel.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
                    ParticleSystem.MainModule main = ps.main;
                    main.playOnAwake = false;
                    t = ps.transform;
                }

                return t.GetComponent<ParticleSystem>();
            }

            storm.west = Side("West", 36f, 1f);
            storm.east = Side("East", 72f, -1f);
            return storm;
        }

        // --- NavMesh ---

        // His NavMesh and the Colossus's, over the hall, from the walkable and obstacle colliders.
        public static string BakeHallNavMeshes(GameObject root)
        {
            string master = BakeMasterNavMesh(root);
            Transform host = Child(root.transform, "NavMesh_Colossus", Vector3.zero, Quaternion.identity);
            var surface = Ensure<Unity.AI.Navigation.NavMeshSurface>(host.gameObject);
            surface.agentTypeID = AgentType(ColossusAgent, ColossusAgentRadius, ColossusAgentHeight);
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.Volume;
            surface.center = HallVolume.center;
            surface.size = HallVolume.size;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default", "Ground", "Obstacles");
            surface.BuildNavMesh();
            string path = L5Folder + "/NavMesh-Colossus.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.navMeshData = existing;
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(surface.navMeshData, path);
            }

            surface.RemoveData();
            surface.AddData();
            return master + "; NavMesh (Colossus) baked: " + path;
        }
    }
}
