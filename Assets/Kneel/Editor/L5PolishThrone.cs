using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The throne and the wall behind it (owner, 2026-11-12: "the backwall of the throne room looks unfinished. The chair
    // looks cheap and the wall behind it is plain. Please make these look premium within the scope of the art style
    // (low poly) synty.").
    // - The throne is fitted to the Master as he really sits (his seated clip sampled on his rig at the throne seat):
    //   seat height under him, the back against his back, a footstool under his feet (they were 10 cm off the dais).
    //   A stone seat with a crimson cushion and a bronze edge; arms with bronze caps and stepped finials; a pointed
    //   (lancet) back with a crimson inset framed in bronze; two pinnacles with spires; a crown of iron blades over
    //   it all, which reads from the gameplay camera's height as a dark star behind him.
    // - The apse wall: coursed pilasters with bases and capitals, a blind arcade along its foot, a string course and a
    //   cornice, the King's shields on the pilasters, and two more great banners.
    // The old throne's stone and cushion were baked into the dais mesh (their own submeshes, basalt and crimson):
    // those submeshes are emptied, the rest of the dais is untouched.
    public static partial class L5Polish
    {
        private const string DaisMesh = "Assets/Kneel/Meshes/L5/Kit/L5_Dais.asset";
        // The throne's own stone and metal: a polished near-black (basalt read tan a metre from the hellfire) and a
        // worn gilt that holds its colour against it; the crown's blades a dark iron.
        private static Material Basalt => ThroneMaterial("L5_ThroneStone", new Color(0.085f, 0.07f, 0.075f), 0.45f, 0f);
        private static Material Iron => ThroneMaterial("L5_ThroneIron", new Color(0.07f, 0.065f, 0.06f), 0.4f, 0.6f);
        private static Material Bronze => ThroneMaterial("L5_ThroneGilt", new Color(0.55f, 0.4f, 0.2f), 0.5f, 0.75f);

        private static Material ThroneMaterial(string name, Color colour, float smoothness, float metallic)
        {
            string path = "Assets/Kneel/Materials/L5/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }
        private static Material Crimson => AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L5/L5_BannerRed.mat");
        private static Material Stone => L4Kit.Stone;
        private static readonly Vector2 BronzeUv = new Vector2(0.8521f, 0.4221f);

        [MenuItem("Kneel/L5/Polish/Throne and Back Wall")]
        public static void ThroneMenu() => Debug.Log(ThroneWall());

        public static string ThroneWall()
        {
            OpenScene();
            var log = new List<string>();
            log.Add(EmptyOldThrone());
            SeatFit fit = MeasureSeat();
            log.Add("seat " + fit.seatY.ToString("F2") + ", back " + fit.backZ.ToString("F2") + ", feet " + fit.feetY.ToString("F2"));
            Transform dais = GameObject.Find("L5_Root/Art/Dais").transform;
            BuildThrone(dais, fit);
            BuildApse(dais);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return "L5 throne wall: " + string.Join("; ", log);
        }

        // The old throne out of the baked dais (its basalt and crimson submeshes), and its loose parts.
        private static string EmptyOldThrone()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DaisMesh);
            var renderer = GameObject.Find("L5_Root/Art/Dais/Baked").GetComponent<MeshRenderer>();
            int emptied = 0;
            for (int s = 0; s < mesh.subMeshCount && s < renderer.sharedMaterials.Length; s++)
            {
                string m = renderer.sharedMaterials[s].name;
                if ((m == "L4_Basalt" || m == "L5_BannerRed") && mesh.GetTriangles(s).Length > 0)
                {
                    mesh.SetTriangles(new int[0], s, false);
                    emptied++;
                }
            }

            if (emptied > 0)
            {
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssetIfDirty(mesh);
            }

            GameObject old = GameObject.Find("L5_Root/Art/Dais/Throne");
            if (old != null)
                Object.DestroyImmediate(old);
            return emptied + " old throne submeshes emptied";
        }

        private struct SeatFit
        {
            public float seatY, backZ, frontZ, feetY, halfWidth;
        }

        // The Master's seated clip on his own rig at the throne seat: where he sits, how far back his back is, where his
        // feet are, how wide he is.
        private static SeatFit MeasureSeat()
        {
            var boss = Object.FindAnyObjectByType<MasterBoss>(FindObjectsInactive.Include);
            MasterP1 master = boss.master;
            var anim = master.GetComponent<Animator>();
            var controller = (UnityEditor.Animations.AnimatorController)anim.runtimeAnimatorController;
            AnimationClip clip = null;
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                if (state.state.name == "Seated")
                    clip = state.state.motion as AnimationClip;
            }

            Vector3 oldPosition = master.transform.position;
            Quaternion oldRotation = master.transform.rotation;
            var graph = UnityEngine.Playables.PlayableGraph.Create();
            var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "seat", anim);
            var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
            UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
            master.transform.SetPositionAndRotation(boss.throneSeat.position, boss.throneSeat.rotation);
            UnityEngine.Playables.PlayableExtensions.SetTime(playable, clip.length * 0.5);
            graph.Evaluate(0f);

            Vector3 hips = anim.GetBoneTransform(HumanBodyBones.Hips).position;
            Vector3 knee = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;
            var skin = master.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
            float seat = float.MaxValue, back = float.MinValue, feet = float.MaxValue, half = 0f;
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(v);
                float dx = Mathf.Abs(w.x - hips.x);
                half = Mathf.Max(half, dx);
                // Under the pelvis: the seat.
                if (dx < 0.5f && w.y < hips.y && w.y > hips.y - 0.6f && Mathf.Abs(w.z - hips.z) < 0.35f)
                    seat = Mathf.Min(seat, w.y);
                // His back, between the hips and the shoulders.
                if (dx < 0.45f && w.y > hips.y && w.y < hips.y + 1.2f)
                    back = Mathf.Max(back, w.z);
                feet = Mathf.Min(feet, w.y);
            }

            Object.DestroyImmediate(baked);
            graph.Destroy();
            master.transform.SetPositionAndRotation(oldPosition, oldRotation);
            return new SeatFit { seatY = seat, backZ = back + 0.04f, frontZ = knee.z + 0.12f, feetY = feet, halfWidth = half };
        }

        private static GameObject Piece(Transform parent, string name, Mesh mesh, Vector3 world, Material material)
        {
            var go = L3Build.MeshChild(parent, name, mesh, parent.InverseTransformPoint(world), material);
            go.isStatic = true;
            return go;
        }

        private static void Block(Transform parent, string name, Vector3 size, Vector3 world, Material material, ref int seed)
        {
            Piece(parent, name, L2PolyKit.Tread(size, seed++), world, material);
        }

        private static Mesh Built(LeafBuilder b, string name)
        {
            Mesh m = b.ToMesh();
            m.name = name;
            EnsureFolder(MeshFolder);
            return SaveMesh(m, MeshFolder + "/" + name + ".asset");
        }

        private static void BuildThrone(Transform dais, SeatFit fit)
        {
            // Not "Throne": MasterSetup.PlaceInL5 scales a Dais/Throne 1.5x (it was sized for the old stand-in).
            Transform existing = dais.Find("MasterThrone");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            var t = new GameObject("MasterThrone").transform;
            t.SetParent(dais, false);
            t.position = new Vector3(54f, 9.6f, 471f);
            int seed = 7100;
            const float floor = 9.6f;
            float seatY = fit.seatY;
            float backZ = fit.backZ;
            float front = Mathf.Min(fit.frontZ, backZ - 0.9f);
            float inner = Mathf.Max(1.0f, fit.halfWidth * 0.62f);

            // The footstool under his feet, a bronze lip on its front edge.
            float stoolTop = Mathf.Max(floor + 0.04f, fit.feetY);
            Block(t, "Footstool", new Vector3(inner * 2f + 1.0f, stoolTop - floor, 1.2f), new Vector3(54f, (floor + stoolTop) * 0.5f, front - 0.6f), Basalt, ref seed);
            Block(t, "FootstoolEdge", new Vector3(inner * 2f + 1.05f, 0.05f, 0.06f), new Vector3(54f, stoolTop - 0.02f, front - 1.2f), Bronze, ref seed);

            // The seat: a stone block under him, the cushion on it, a bronze edge along its front.
            float seatDepth = backZ + 0.5f - front;
            Block(t, "Seat", new Vector3(inner * 2f + 0.5f, seatY - 0.1f - floor, seatDepth), new Vector3(54f, (floor + seatY - 0.1f) * 0.5f, front + seatDepth * 0.5f), Basalt, ref seed);
            Block(t, "SeatEdge", new Vector3(inner * 2f + 0.55f, 0.07f, 0.07f), new Vector3(54f, seatY - 0.13f, front - 0.02f), Bronze, ref seed);
            Block(t, "Cushion", new Vector3(inner * 2f - 0.1f, 0.13f, backZ - front - 0.05f), new Vector3(54f, seatY - 0.04f, (front + backZ) * 0.5f), Crimson, ref seed);

            // The arms: stone, bronze-capped, a stepped finial at the front.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = 54f + s * (inner + 0.22f);
                float top = seatY + 0.72f;
                Block(t, "Arm", new Vector3(0.42f, top - floor, seatDepth + 0.1f), new Vector3(x, (floor + top) * 0.5f, front + seatDepth * 0.5f - 0.05f), Basalt, ref seed);
                Block(t, "ArmCap", new Vector3(0.5f, 0.07f, seatDepth + 0.16f), new Vector3(x, top + 0.035f, front + seatDepth * 0.5f - 0.05f), Bronze, ref seed);
                Block(t, "Finial", new Vector3(0.34f, 0.22f, 0.34f), new Vector3(x, top + 0.18f, front + 0.05f), Basalt, ref seed);
                var tip = new LeafBuilder();
                tip.Pyramid(new Vector3(0f, 0f, 0f), 0.26f, 0.42f, BronzeUv);
                Piece(t, "FinialTip", Built(tip, "L5_Throne_FinialTip"), new Vector3(x, top + 0.29f, front + 0.05f), Bronze);
            }

            // The back: a lancet of black stone, a crimson inset framed in bronze.
            float half = inner + 0.45f;
            float shoulder = seatY + 3.4f;
            const float depth = 0.45f;
            var back = new LeafBuilder();
            back.Extrude(Lancet(half, floor, shoulder, 18), 0f, depth, Vector2.zero);
            Piece(t, "Back", Built(back, "L5_Throne_Back"), new Vector3(54f, 0f, backZ), Basalt);
            var inset = new LeafBuilder();
            Vector2[] insetShape = Lancet(half - 0.32f, seatY + 0.15f, shoulder - 0.05f, 18);
            inset.Extrude(insetShape, -0.04f, 0.01f, Vector2.zero);
            Piece(t, "BackCloth", Built(inset, "L5_Throne_BackCloth"), new Vector3(54f, 0f, backZ), Crimson);
            var frame = new LeafBuilder();
            for (int i = 0; i < insetShape.Length; i++)
            {
                Vector2 a = insetShape[i], c = insetShape[(i + 1) % insetShape.Length];
                frame.Bar(new Vector3(a.x, a.y, -0.07f), new Vector3(c.x, c.y, -0.07f), 0.07f, BronzeUv);
            }

            Piece(t, "BackFrame", Built(frame, "L5_Throne_BackFrame"), new Vector3(54f, 0f, backZ), Bronze);

            // The pinnacles either side of the back: coursed posts, a bronze band, a capital, a spire with a bronze tip.
            float apex = shoulder + half * Mathf.Sqrt(3f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x = 54f + s * (half + 0.24f);
                float z = backZ + depth * 0.5f;
                float postTop = shoulder + 1.2f;
                float y = floor;
                int course = 0;
                while (y < postTop - 0.05f)
                {
                    float h = Mathf.Min(0.85f + (course % 2) * 0.2f, postTop - y);
                    Block(t, "Post", new Vector3(0.48f - (course % 2) * 0.03f, h - 0.02f, 0.48f - (course % 2) * 0.03f), new Vector3(x, y + h * 0.5f, z), Basalt, ref seed);
                    y += h;
                    course++;
                }

                Block(t, "PostBand", new Vector3(0.54f, 0.1f, 0.54f), new Vector3(x, seatY + 0.9f, z), Bronze, ref seed);
                Block(t, "Capital", new Vector3(0.64f, 0.22f, 0.64f), new Vector3(x, postTop + 0.11f, z), Basalt, ref seed);
                var spire = new LeafBuilder();
                spire.Pyramid(Vector3.zero, 0.5f, 1.7f, Vector2.zero);
                Piece(t, "Spire", Built(spire, "L5_Throne_Spire"), new Vector3(x, postTop + 0.22f, z), Basalt);
                var cap = new LeafBuilder();
                cap.Pyramid(Vector3.zero, 0.16f, 0.36f, BronzeUv);
                Piece(t, "SpireTip", Built(cap, "L5_Throne_SpireTip"), new Vector3(x, postTop + 1.85f, z), Bronze);
            }

            // The crown over it all: seven iron blades fanned from a bronze boss at the lancet's point.
            var crown = new LeafBuilder();
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-62f, 62f, i / 6f) * Mathf.Deg2Rad;
                float length = Mathf.Lerp(1.15f, 2.1f, 1f - Mathf.Abs(i - 3) / 3f);
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                var side = new Vector2(-dir.y, dir.x);
                Vector2 root = dir * 0.2f;
                var blade = new[] { root - side * 0.13f, root + side * 0.13f, root + dir * length * 0.8f + side * 0.07f, root + dir * length, root + dir * length * 0.8f - side * 0.07f };
                crown.Extrude(blade, 0.1f, 0.22f, Vector2.zero);
            }

            Piece(t, "Crown", Built(crown, "L5_Throne_Crown"), new Vector3(54f, apex - 0.35f, backZ), Iron);
        }

        // A lancet (equilateral pointed arch) outline: straight sides from `bottom` to `shoulder`, two arcs to the
        // point, half-width `half`.
        private static Vector2[] Lancet(float half, float bottom, float shoulder, int steps)
        {
            var points = new List<Vector2> { new Vector2(-half, bottom), new Vector2(half, bottom) };
            float radius = half * 2f;
            // The right arc is centred on the left springing point, from the right springing up to the point.
            for (int k = 0; k <= steps; k++)
            {
                float a = Mathf.Lerp(0f, 60f, k / (float)steps) * Mathf.Deg2Rad;
                points.Add(new Vector2(-half + radius * Mathf.Cos(a), shoulder + radius * Mathf.Sin(a)));
            }

            for (int k = steps - 1; k >= 0; k--)
            {
                float a = Mathf.Lerp(0f, 60f, k / (float)steps) * Mathf.Deg2Rad;
                points.Add(new Vector2(half - radius * Mathf.Cos(a), shoulder + radius * Mathf.Sin(a)));
            }

            return points.ToArray();
        }

        // The apse wall either side of the niche (x 31.5-48 and 60-76.5, its face at z 472.5).
        private static void BuildApse(Transform dais)
        {
            Transform old = dais.Find("ApseDressing");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var g = new GameObject("ApseDressing").transform;
            g.SetParent(dais, false);
            int seed = 7400;
            const float face = 472.5f, floor = 9.6f, top = 20.4f;

            float[] pilasters = { 34.0f, 40.6f, 46.2f, 61.8f, 67.4f, 74.0f };
            foreach (float x in pilasters)
            {
                Block(g, "PilasterBase", new Vector3(1.5f, 0.8f, 0.9f), new Vector3(x, floor + 0.4f, face - 0.45f), Stone, ref seed);
                float y = floor + 0.8f;
                int course = 0;
                while (y < top - 0.6f)
                {
                    float h = Mathf.Min(0.62f + (course % 3) * 0.06f, top - 0.6f - y);
                    Block(g, "Pilaster", new Vector3(1.12f - (course % 2) * 0.06f, h - 0.03f, 0.6f), new Vector3(x, y + h * 0.5f, face - 0.3f), Stone, ref seed);
                    y += h;
                    course++;
                }

                Block(g, "Capital", new Vector3(1.5f, 0.32f, 0.85f), new Vector3(x, top - 0.44f, face - 0.42f), Stone, ref seed);
                Block(g, "CapitalBand", new Vector3(1.3f, 0.12f, 0.75f), new Vector3(x, top - 0.66f, face - 0.37f), Bronze, ref seed);

                // The King's shield on it, crimson field.
                var shield = L3Build.Part(g, "K:SM_Wep_Shield_01", g.InverseTransformPoint(new Vector3(x, 16.2f, face - 0.62f)), new Vector3(0f, 180f, 0f), Vector3.one * 2.4f, L4Citadel.BannerRed);
                shield.name = "Shield";
            }

            // The cornice along the top and a string course across the middle, in runs between the pilasters.
            foreach (var (x0, x1) in new[] { (31.6f, 48.0f), (60.0f, 76.4f) })
            {
                for (float x = x0; x < x1 - 0.05f;)
                {
                    float w = Mathf.Min(1.6f + (seed % 5) * 0.2f, x1 - x);
                    Block(g, "Cornice", new Vector3(w - 0.02f, 0.42f, 0.7f), new Vector3(x + w * 0.5f, top + 0.21f, face - 0.35f), Stone, ref seed);
                    Block(g, "CorniceDrip", new Vector3(w - 0.02f, 0.16f, 0.45f), new Vector3(x + w * 0.5f, top - 0.08f, face - 0.22f), Stone, ref seed);
                    Block(g, "StringCourse", new Vector3(w - 0.02f, 0.22f, 0.32f), new Vector3(x + w * 0.5f, 14.8f, face - 0.16f), Stone, ref seed);
                    x += w;
                }
            }

            // A blind arcade along the wall's foot between the pilasters: round-headed panels sunk into the stone.
            for (int i = 0; i < pilasters.Length - 1; i++)
            {
                float a = pilasters[i] + 0.56f, b = pilasters[i + 1] - 0.56f;
                if (b - a > 10f || b <= a)
                    continue;
                int arches = Mathf.Max(1, Mathf.RoundToInt((b - a) / 2.4f));
                float span = (b - a) / arches;
                for (int k = 0; k < arches; k++)
                {
                    var ring = new GameObject("BlindArch").transform;
                    ring.SetParent(g, false);
                    ring.position = new Vector3(a + span * (k + 0.5f), floor, face - 0.12f);
                    L4Kit.GateArch(ring, span - 0.5f, 0.25f, 2.6f, (span - 0.5f) * 0.5f, 4.6f, 0.24f, seed++);
                }
            }

            // A torch on each of the four outer pilasters, so the dressing reads (the dais's own sconce, copied: the same
            // flame, light, flicker and layers).
            Transform template = null;
            foreach (Transform c in dais)
            {
                if (c.name == "Sconce")
                {
                    template = c;
                    break;
                }
            }

            if (template != null)
            {
                foreach (float x in new[] { 34.0f, 40.6f, 67.4f, 74.0f })
                {
                    var sconce = Object.Instantiate(template.gameObject, g);
                    sconce.name = "Sconce";
                    sconce.transform.SetPositionAndRotation(new Vector3(x, template.position.y, face - 0.6f), template.rotation);
                }
            }

            // Two more great banners, between the outer pilasters, as the niche's.
            L4Citadel.HangBanner(g, g.InverseTransformPoint(new Vector3(37.3f, 19.6f, face - 0.1f)), 0f, 2.6f, 1);
            L4Citadel.HangBanner(g, g.InverseTransformPoint(new Vector3(70.7f, 19.6f, face - 0.1f)), 0f, 2.6f, 2);
        }
    }
}
