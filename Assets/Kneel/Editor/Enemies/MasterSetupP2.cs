using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // The Ash Colossus (Phase 2): the prefab, the crusted sword in his right fist, his claw and mask, the controller,
    // the clip cuts and the measured contacts. Safe to rerun: it rebuilds the prefab's components and the controller in
    // place, keeping their GUIDs.
    public static partial class MasterSetup
    {
        public const string P2Prefab = Folder + "TheMaster_P2.prefab";
        private const string P2Controller = Clips + "AC_Colossus.controller";
        private const string ColossusSword = Folder + "Weapons/SM_ColossusSword.fbx";
        private const string P2Cuts = Clips + "CutsP2/";

        // His sword fist: the same grip as P1's, on a bigger hand.
        private static readonly Vector3 ColossusFingerCurl = new Vector3(80f, 90f, 60f);
        private static readonly Vector2 ColossusThumbCurl = new Vector2(60f, 40f);

        // --- Measured 2026-11-10 on his rig with his animator running (the sword tip and the claw relative to his root,
        // the hips): where each blow passes in front of him at speed ---
        private const float CleaveContact = 0.615f;       // the Decree's chop coming down across his front (P1's 0.63 is past it on his longer arm)
        private const float SweepContact = 1.3f;          // the claw at full speed, 3.8 m out
        private const float GraspContact = 0.7f;          // the claw furthest out, closing
        private const float HammerContact = 1.6f;         // both fists at the floor, the blade 4.3 m out
        private const float LeapTakeoff = 0.55f;          // the hips leave their crouch
        private const float LeapLand = 1.65f;             // and are back down
        private const float ColossusCommandRelease = 0.9f;
        private const float GatherDig = 1f;
        private const float LashRelease = 1.2f;           // both hands thrust forward
        // Dying Front Head Impact To Two Knees: the first 1.9 s are a standing flinch; he's on both knees at 2.75 s and
        // down on his hands, the mask near the floor, at 3.0 s. The Topple plays 1.9-3.0; Fallen stops on his knees.
        private const float ToppleStart = 1.9f;
        private const float ToppleCut = 3f;
        private const float FallenCut = 2.75f;
        // Kneeling To Pointing: the arm is fully raised at 1.6 s (it comes down again after 2 s).
        private const float LastCommandPeak = 1.6f;

        private static readonly Cut[] P2CutTable =
        {
            new Cut { name = "Idle", source = "Colossus_Idle" },
            new Cut { name = "Walk", source = "Colossus_Walk" },
            new Cut { name = "Rise", source = "Colossus_Flex", bakeHeight = true, pin = true },
            new Cut { name = "Roar", source = "Colossus_Roar", bakeHeight = true, pin = true },
            // The Cleave is the Master's Decree (Sword And Shield Downward Slash), not Great Sword Power Slash: on his rig
            // the Power Slash is a full turn that ends with the blade out to his side, never in the floor (filmed
            // 2026-11-10). The Decree is a one-handed overhead chop from the same rig: the same blow, grown huge.
            new Cut { name = "Cleave", source = "Master_Decree", bakeRotation = true, pin = true },
            new Cut { name = "Sweep", source = "Colossus_Sweep", bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "Grasp", source = "Colossus_Grasp", bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "Hammerfall", source = "Colossus_Hammerfall", bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "Leap", source = "Colossus_Leap", bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "Command", source = "Shared_Command", bakeRotation = true, pin = true },
            new Cut { name = "Gather", source = "Colossus_Gather", bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "ThreadLash", source = "Colossus_ThreadLash", bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "Recoil", source = "Shared_Stagger", bakeRotation = true, pin = true },
            new Cut { name = "Topple", source = "Colossus_Topple", start = ToppleStart, end = ToppleCut, bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "ToppleHold", source = "Colossus_Topple", start = ToppleCut - 0.05f, end = ToppleCut, bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "Fallen", source = "Colossus_Topple", start = ToppleStart, end = FallenCut, bakeRotation = true, bakeHeight = true, pin = true },
            // The finisher's last palm (§7): Kneeling To Pointing, played forward to raise it and backward to lower it.
            new Cut { name = "LastCommand", source = "Colossus_LastCommand", bakeRotation = true, bakeHeight = true, pin = true },
        };

        private static AnimationClip MakeP2Cut(Cut cut)
        {
            AnimationClip source = Copy(cut.source);
            float end = cut.end > 0f ? cut.end : source.length;
            AnimationClip clip = EnemyRig.CopyClip(source, P2Cuts + "Colossus_" + cut.name + ".anim", cut.start, end);
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = (AnimationUtility.GetAnimationClipSettings(source).loopTime && cut.start <= 0f && cut.end < 0f) || cut.name == "ToppleHold";
            AnimationUtility.SetAnimationClipSettings(clip, s);
            if (cut.bakeRotation)
            {
                EnemyRig.BakeRootRotation(clip);
                // Faced by his body, not the mocap's root: several of these clips (the Gather, the Thread Lash) were
                // recorded turned a quarter away, and he'd start them side-on.
                AnimationClipSettings facing = AnimationUtility.GetAnimationClipSettings(clip);
                facing.keepOriginalOrientation = false;
                facing.orientationOffsetY = 0f;
                AnimationUtility.SetAnimationClipSettings(clip, facing);
            }

            if (cut.bakeHeight) EnemyRig.BakeRootHeight(clip);
            if (cut.pin) EnemyRig.PinRoot(clip);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        // --- The prefab ---

        [MenuItem("Kneel/Enemies/The Master/Build P2 Prefab")]
        public static void BuildP2PrefabMenu() => Debug.Log(BuildP2Prefab());

        public static string BuildP2Prefab()
        {
            MasterSettings settings = EnsureSettings();

            // His claw stays the open, hooked hand it was modelled as, and his sword hand never lets go.
            EnemyRig.UnmapFingers(ColossusModel);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ColossusModel);
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(P2Prefab) != null;
            GameObject root;
            if (exists)
            {
                root = PrefabUtility.LoadPrefabContents(P2Prefab);
            }
            else
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "TheMaster_P2";
            }

            try
            {
                root.layer = LayerMask.NameToLayer("Enemy");
                var animator = Ensure<Animator>(root);
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ColossusModel).OfType<Avatar>().First();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(P2Controller);
                if (controller != null)
                    animator.runtimeAnimatorController = controller;

                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Models/AshColossus/M_AshColossus.mat");
                    r.updateWhenOffscreen = false;
                }

                var agent = Ensure<NavMeshAgent>(root);
                agent.agentTypeID = AgentType(ColossusAgent, ColossusAgentRadius, ColossusAgentHeight);
                agent.radius = ColossusAgentRadius;
                agent.height = ColossusAgentHeight;
                agent.speed = settings.p2WalkSpeed;
                agent.acceleration = 14f;
                agent.angularSpeed = 0f;
                agent.updateRotation = false;
                agent.stoppingDistance = 0.3f;
                agent.autoBraking = true;

                var capsule = Ensure<CapsuleCollider>(root);
                capsule.radius = 1.4f;
                capsule.height = 5.6f;
                capsule.center = new Vector3(0f, 2.8f, 0f);

                NoRegen(Ensure<Health>(root));
                var colossus = Ensure<AshColossus>(root);
                var tell = Ensure<BossTell>(root);
                colossus.settings = settings;
                colossus.bossTell = tell;
                colossus.attackFacingAngle = 25f;
                colossus.aggressionRange = 0f;
                colossus.smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Smoke.mat");
                colossus.emberMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Ember.mat");
                colossus.shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Hall/Materials/M_HallShadow.mat");
                colossus.crackMaterial = AssetDatabase.LoadAssetAtPath<Material>(RadialCrackMaterial);
                colossus.crackMap = AssetDatabase.LoadAssetAtPath<Texture2D>(RadialCracks);
                (colossus.debrisMesh, colossus.debrisMaterial) = Debris();
                SetPrivate(colossus, "playerMask", (LayerMask)LayerMask.GetMask("Player"));

                // The sword, its grip lost inside the fist.
                EnemyRig.CloseFist(animator, model, HumanBodyBones.RightHand, ColossusFingerCurl, ColossusThumbCurl);
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                float k = animator.humanScale / Mathf.Max(hand.lossyScale.x, 1e-4f);
                // Held, not fused (owner, 2026-11-10: "the sword does not look fused into his hand ... if it cannot be done,
                // make him hold it right"; a crust of rock over the fist read as a boulder). The grip runs through the
                // tunnel his curled claws make, the blade out of the index side, as one-handed sword clips expect.
                Transform socket = FistSocket(animator);
                GameObject sword = EnemyRig.Equip(socket, ColossusSword);
                Mesh swordMesh = sword.GetComponentInChildren<MeshFilter>().sharedMesh;
                Transform tip = sword.transform.Find("SwordTip");
                if (tip == null)
                {
                    tip = new GameObject("SwordTip").transform;
                    tip.SetParent(sword.transform, false);
                }

                tip.localPosition = new Vector3(0f, swordMesh.bounds.max.y - 0.08f, 0f);
                colossus.swordTip = tip;
                tell.swordTip = tip;
                BuildFistAsh(socket, k);

                // The claw (the curse's hand) and the mask (the Great Command's light).
                Transform claw = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                colossus.palm = claw;
                tell.leftHand = claw;
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform mask = head.Find("Mask");
                if (mask == null)
                {
                    mask = new GameObject("Mask").transform;
                    mask.SetParent(head, false);
                }

                mask.position = head.position + root.transform.forward * 0.55f + Vector3.up * 0.1f;
                colossus.mask = mask;
                tell.palmLightAnchor = mask;
                tell.bodyHeight = ColossusAgentHeight;
                tell.palmLightRange = 60f;
                tell.palmLightIntensity = 90f;

                PrefabUtility.SaveAsPrefabAsset(root, P2Prefab);
                return string.Format("{0}: humanScale {1:F2}, sword {2:F2} m, grip at {3} in the hand, agent '{4}' ({5})",
                    P2Prefab, animator.humanScale, swordMesh.bounds.size.y, socket.localPosition.ToString("F2"), ColossusAgent, agent.agentTypeID);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        // The sword socket in his closed fist, from his own finger bones (CC_Base): the grip's centre halfway between each
        // finger's knuckle and its curled tip, averaged over the four; the grip along the line from the little finger's
        // side to the index's (the blade out of the index side); the edge facing the way the fingers point.
        private static Transform FistSocket(Animator animator)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform Bone(string name) => hand.GetComponentsInChildren<Transform>(true).First(t => t.name == "CC_Base_R_" + name);
            string[] fingers = { "Index", "Mid", "Ring", "Pinky" };
            Vector3 centre = Vector3.zero;
            foreach (string f in fingers)
                centre += (Bone(f + "1").position + Bone(f + "3").position) * 0.5f;
            centre /= fingers.Length;
            Vector3 indexSide = (Bone("Index1").position + Bone("Index2").position + Bone("Index3").position) / 3f;
            Vector3 littleSide = (Bone("Pinky1").position + Bone("Pinky2").position + Bone("Pinky3").position) / 3f;
            Vector3 blade = (indexSide - littleSide).normalized;
            Vector3 pointing = (Bone("Mid1").position - hand.position).normalized;
            Vector3 edge = Vector3.ProjectOnPlane(pointing, blade).normalized;
            return EnemyRig.Socket(hand, "Socket_SwordHand", hand.InverseTransformPoint(centre),
                Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(Vector3.Cross(edge, blade), blade));
        }

        // Ash and embers clinging to the fist and the grip (§6's "crusted over with ash", as smoke, not a mesh).
        private static void BuildFistAsh(Transform socket, float k)
        {
            Transform old = socket.Find("FistAsh");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("FistAsh");
            go.transform.SetParent(socket, false);
            float scale = 1f / Mathf.Max(socket.lossyScale.x, 1e-4f);
            go.transform.localScale = Vector3.one * scale;

            AddFistParticles(go.transform, "Ash", AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Smoke.mat"),
                new Color(0.22f, 0.2f, 0.19f, 0.55f), new Vector2(0.35f, 0.7f), new Vector2(1.2f, 2f), 7f, 0.25f, -0.02f);
            AddFistParticles(go.transform, "Embers", AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Ember.mat"),
                new Color(1f, 0.5f, 0.18f), new Vector2(0.03f, 0.07f), new Vector2(0.6f, 1.2f), 10f, 0.3f, -0.15f);
        }

        private static void AddFistParticles(Transform parent, string name, Material material, Color color, Vector2 size, Vector2 life, float rate, float radius, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // --- The controller ---

        [MenuItem("Kneel/Enemies/The Master/Build P2 Animator")]
        public static void BuildP2AnimatorMenu() => Debug.Log(BuildP2Animator());

        public static string BuildP2Animator()
        {
            EnsureFolder(P2Cuts);
            var cuts = new Dictionary<string, AnimationClip>();
            foreach (Cut cut in P2CutTable)
                cuts[cut.name] = MakeP2Cut(cut);

            // Mutant Brutal Walk keeps its root travel (not in place), so its speed is the root's, at his scale.
            float walkSpeed = EnemyRig.RootSpeed(cuts["Walk"], AssetDatabase.LoadAssetAtPath<GameObject>(P2Prefab).GetComponent<Animator>().humanScale);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(P2Controller);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(P2Controller);

            EnemyRig.RemoveBlendTrees(controller);
            float walk = EnsureSettings().p2WalkSpeed;
            BlendTree move = EnemyRig.SpeedTree(controller, "Move", "Speed", (cuts["Idle"], 0f), (cuts["Walk"], walk));
            BlendTree circle = EnemyRig.SpeedTree(controller, "Circle", "Speed", (cuts["Idle"], 0f), (cuts["Walk"], walk * 0.5f));
            foreach (BlendTree tree in new[] { move, circle })
            {
                ChildMotion[] children = tree.children;
                children[1].timeScale = children[1].threshold / walkSpeed;
                tree.children = children;
            }

            EnemyRig.BuildBaseLayer(controller, "Idle",
                new EnemyRig.State { name = "Idle", motion = cuts["Idle"], exitBlend = 0.25f },
                new EnemyRig.State { name = "Rise", motion = cuts["Rise"], exitBlend = 0.3f },
                new EnemyRig.State { name = "RoarSplit", parameter = "Rise", motion = cuts["Roar"], chained = true, exitBlend = 0.3f },
                new EnemyRig.State { name = "Move", motion = move, exitBlend = 0.25f },
                new EnemyRig.State { name = "Circle", motion = circle, exitBlend = 0.25f },
                new EnemyRig.State { name = "Cleave", motion = cuts["Cleave"] },
                new EnemyRig.State { name = "Sweep", motion = cuts["Sweep"] },
                new EnemyRig.State { name = "Grasp", motion = cuts["Grasp"] },
                new EnemyRig.State { name = "Hammerfall", motion = cuts["Hammerfall"] },
                new EnemyRig.State { name = "Leap", motion = cuts["Leap"] },
                new EnemyRig.State { name = "Command", motion = cuts["Command"] },
                new EnemyRig.State { name = "Gather", motion = cuts["Gather"] },
                new EnemyRig.State { name = "ThreadLash", motion = cuts["ThreadLash"] },
                new EnemyRig.State { name = "Recoil", motion = cuts["Recoil"], exitBlend = 0.2f },
                new EnemyRig.State { name = "ToppleDown", parameter = "Topple", motion = cuts["Topple"] },
                new EnemyRig.State { name = "ToppleHold", parameter = "Topple", motion = cuts["ToppleHold"], chained = true },
                new EnemyRig.State { name = "ToppleUp", parameter = "Topple", motion = cuts["Topple"], speed = -1f, chained = true, exitBlend = 0.25f },
                new EnemyRig.State { name = "Roar", motion = cuts["Roar"], exitBlend = 0.3f },
                new EnemyRig.State { name = "Fallen", motion = cuts["Fallen"], final = true },
                new EnemyRig.State { name = "LastCommand", parameter = "Fallen", motion = cuts["LastCommand"], chained = true });

            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                AnimatorState s = child.state;
                string param = null;
                switch (s.name)
                {
                    case "Cleave": case "Sweep": case "Grasp": case "Hammerfall": case "Leap": case "Command": case "Gather": case "ThreadLash":
                    case "Recoil": case "ToppleDown": case "ToppleUp": case "Fallen": case "LastCommand":
                        param = "MoveSpeed"; break;
                    case "Move": case "Circle": param = "MoveRate"; break;
                }

                if (param != null)
                {
                    EnsureFloat(controller, param, 1f);
                    s.speedParameterActive = true;
                    s.speedParameter = param;
                }

                // The Great Command and the last palm are the claw's (left), the sword arm planted.
                s.mirror = s.name == "Command" || s.name == "LastCommand";
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject root = PrefabUtility.LoadPrefabContents(P2Prefab);
            try
            {
                root.GetComponent<Animator>().runtimeAnimatorController = controller;
                var colossus = root.GetComponent<AshColossus>();
                colossus.cleaveContact = CleaveContact;
                colossus.sweepContact = SweepContact;
                colossus.graspContact = GraspContact;
                colossus.hammerContact = HammerContact;
                colossus.leapTakeoff = LeapTakeoff;
                colossus.leapLand = LeapLand;
                colossus.commandRelease = ColossusCommandRelease;
                colossus.gatherDig = GatherDig;
                colossus.lashRelease = LashRelease;
                colossus.toppleDown = ToppleCut - ToppleStart;
                colossus.flexLength = cuts["Rise"].length;
                colossus.gatherLength = cuts["Gather"].length;
                colossus.recoilLength = cuts["Recoil"].length;
                colossus.lastCommandLength = LastCommandPeak;
                colossus.walkClipSpeed = walk;
                PrefabUtility.SaveAsPrefabAsset(root, P2Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return string.Format("{0}: {1} cuts, walk clip {2:F2} m/s on him", P2Controller, cuts.Count, walkSpeed);
        }

        // --- L5 (edited in place) ---

        [MenuItem("Kneel/Enemies/The Master/Place P2 in L5")]
        public static void PlaceP2InL5Menu() => Debug.Log(PlaceP2InL5());

        // The Colossus under the open L5 scene's "TheMaster" root (hidden at runtime until the Unmaking raises him),
        // and the Unmaking itself on the root. Saves the scene.
        public static string PlaceP2InL5()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != L5Scene)
                return "open L5_ThroneRoom first (active: " + scene.path + ")";

            GameObject root = GameObject.Find("TheMaster");
            if (root == null)
                return "no TheMaster root: run Place in L5 first";

            Transform p2 = root.transform.Find("TheMaster_P2");
            if (p2 == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P2Prefab), root.transform);
                p2 = instance.transform;
            }

            // Where it waits (hidden) until it's raised: the dais foot.
            p2.SetPositionAndRotation(DaisFoot, Quaternion.Euler(0f, 180f, 0f));

            var boss = root.GetComponent<MasterBoss>();
            boss.colossus = p2.GetComponent<AshColossus>();
            var unmaking = Ensure<Unmaking>(root);
            unmaking.smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Smoke.mat");
            unmaking.emberMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Ember.mat");
            unmaking.crackMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Hall/Materials/M_HallCrack.mat");
            unmaking.ringCrackMaterial = AssetDatabase.LoadAssetAtPath<Material>(RadialCrackMaterial);
            unmaking.ringCrackMap = AssetDatabase.LoadAssetAtPath<Texture2D>(RadialCracks);
            boss.unmaking = unmaking;
            EditorUtility.SetDirty(boss);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            return "placed " + p2.name + " and the Unmaking; saved " + scene.path;
        }

        // The ending's pieces in the open L5 scene: the finisher and the score on the "TheMaster" root, and the rest at Shrine
        // C (the Last Shrine, outside the grand doors; Kael comes back here after it's been rested at). Saves the scene.
        [MenuItem("Kneel/Enemies/The Master/Place Finisher, Music and Shrine C in L5")]
        public static void PlaceEndingInL5Menu() => Debug.Log(PlaceEndingInL5());

        public static string PlaceEndingInL5()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != L5Scene)
                return "open L5_ThroneRoom first (active: " + scene.path + ")";

            GameObject root = GameObject.Find("TheMaster");
            var boss = root.GetComponent<MasterBoss>();
            var finisher = Ensure<MasterFinisher>(root);
            finisher.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Kneel/UI/Fonts/CormorantGaramond-SemiBold SDF.asset");
            finisher.ashMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Smoke.mat");
            finisher.emberMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Ember.mat");
            boss.finisher = finisher;
            boss.music = Ensure<MusicDirector>(root);
            EditorUtility.SetDirty(boss);

            GameObject shrine = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(t => t.gameObject).FirstOrDefault(g => g.name == "ShrineC_LastShrine");
            string shrineLog = "no ShrineC_LastShrine";
            if (shrine != null)
            {
                var rest = Ensure<ShrineRest>(shrine);
                rest.flag = "L5.ShrineC";
                rest.respawnFacing = Vector3.forward;
                EditorUtility.SetDirty(rest);
                shrineLog = "rest at " + shrine.transform.position.ToString("F1") + ", Kael back at " + rest.RespawnPoint.ToString("F1");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            return "finisher and music on " + root.name + "; " + shrineLog + "; saved " + scene.path;
        }

        // Kael's knockdown (PlayerCombat's Knockdown state): KnockdownFall and KnockdownUp on the FullBody layer, at the
        // KnockdownSpeed the code sets. Leaves everything else alone.
        [MenuItem("Kneel/Enemies/The Master/Add Player Knockdown")]
        public static void AddPlayerKnockdownMenu() => Debug.Log(AddPlayerKnockdown());

        public static string AddPlayerKnockdown()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerController);
            EnsureFloat(controller, "KnockdownSpeed", 1f);
            AnimatorStateMachine full = controller.layers.First(l => l.name == "FullBody").stateMachine;
            var log = new StringBuilder();
            foreach (var (state, clip) in new[] { ("KnockdownFall", "knockdown-fall"), ("KnockdownUp", "knockdown-getup") })
            {
                AnimationClip motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(KaelCopies + clip + ".anim");
                AnimatorState existing = full.states.Select(x => x.state).FirstOrDefault(x => x.name == state);
                if (existing == null)
                    existing = full.AddState(state);
                existing.motion = motion;
                existing.speedParameterActive = true;
                existing.speedParameter = "KnockdownSpeed";
                existing.writeDefaultValues = true;
                log.Append(state + " (" + (motion != null ? motion.length.ToString("F2") : "missing") + " s) ");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return log.ToString();
        }

        // --- Analysis ---

        // For a clip (a scaled copy) on a built prefab: the sword tip and the left hand relative to his root (height, out
        // in front, bearing), the tip's speed, the hips' and feet's heights, every `step` seconds.
        public static string BodyTimeline(string prefabPath, string clipName, float step = 0.05f) =>
            BodyTimeline(prefabPath, Copy(clipName), step);

        public static string BodyTimeline(string prefabPath, AnimationClip clip, float step = 0.05f)
        {
            string clipName = clip.name;
            GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var body = go.GetComponent<MasterBody>();
                var animator = go.GetComponent<Animator>();
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                var sb = new StringBuilder(clipName + " (" + clip.length.ToString("F2") + " s): t | tip y fwd bearing speed | claw y fwd bearing speed | hips feet head\n");
                Vector3 lastTip = Vector3.zero, lastClaw = Vector3.zero;
                for (float t = 0f; t <= clip.length + 1e-4f; t += step)
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    clip.SampleAnimation(go, t);
                    Vector3 tip = go.transform.InverseTransformPoint(body.swordTip.position);
                    Vector3 claw = go.transform.InverseTransformPoint(leftHand.position);
                    float tipSpeed = t > 0f ? (tip - lastTip).magnitude / step : 0f;
                    float clawSpeed = t > 0f ? (claw - lastClaw).magnitude / step : 0f;
                    lastTip = tip;
                    lastClaw = claw;
                    sb.AppendLine(string.Format("{0,5:F2} | {1,5:F2} {2,5:F2} {3,5:F0} {4,5:F1} | {5,5:F2} {6,5:F2} {7,5:F0} {8,5:F1} | {9,5:F2} {10,5:F2} {11,5:F2} {12,5:F2}",
                        t, tip.y, tip.z, Mathf.Atan2(tip.x, tip.z) * Mathf.Rad2Deg, tipSpeed, claw.y, claw.z, Mathf.Atan2(claw.x, claw.z) * Mathf.Rad2Deg, clawSpeed,
                        hips.position.y, Mathf.Min(lf.position.y, rf.position.y), head.position.y, 0f));
                }

                return sb.ToString();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
