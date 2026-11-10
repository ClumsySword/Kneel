using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // The Master, Phase 1: the prefab, its sword and sockets, the controller, the clip cuts and the measured contacts.
    // Safe to rerun: it rebuilds the prefab's components and the controller in place, keeping their GUIDs.
    public static partial class MasterSetup
    {
        public const string P1Prefab = Folder + "TheMaster_P1.prefab";
        public const string SettingsPath = Folder + "MasterSettings.asset";
        private const string P1Controller = Clips + "AC_MasterP1.controller";
        private const string MasterSword = Folder + "Weapons/SM_MasterSword.fbx";
        private const string Cuts = Clips + "Cuts/";

        // Fist around the sword's grip: the bend at each finger joint, and the thumb's swing across the palm and bend.
        private static readonly Vector3 FingerCurl = new Vector3(75f, 85f, 55f);
        private static readonly Vector2 ThumbCurl = new Vector2(55f, 35f);

        // The NavMesh agent he walks on (a 4.2 m man needs more room than the Humanoid agent).
        public const string MasterAgent = "Master";
        public const float MasterAgentRadius = 0.9f;
        public const float MasterAgentHeight = 4.2f;

        public static AnimationClip Copy(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(BossCopies + name + ".anim");

        // --- The prefab ---

        [MenuItem("Kneel/Enemies/The Master/Build P1 Prefab")]
        public static void BuildP1PrefabMenu() => Debug.Log(BuildP1Prefab());

        public static string BuildP1Prefab()
        {
            var log = new StringBuilder();
            MasterSettings settings = EnsureSettings();

            // His sword hand never lets go, and his left hand is the curse's open palm: no clip should move the fingers.
            EnemyRig.UnmapFingers(MasterModel);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MasterModel);
            GameObject root;
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(P1Prefab) != null;
            if (exists)
            {
                root = PrefabUtility.LoadPrefabContents(P1Prefab);
            }
            else
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "TheMaster_P1";
            }

            try
            {
                root.layer = LayerMask.NameToLayer("Enemy");

                var animator = Ensure<Animator>(root);
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(MasterModel).OfType<Avatar>().First();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(P1Controller);
                if (controller != null)
                    animator.runtimeAnimatorController = controller;

                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Models/Master/M_Master.mat");
                    r.updateWhenOffscreen = false;
                }

                var agent = Ensure<NavMeshAgent>(root);
                agent.agentTypeID = AgentType(MasterAgent, MasterAgentRadius, MasterAgentHeight);
                agent.radius = MasterAgentRadius;
                agent.height = MasterAgentHeight;
                agent.speed = settings.walkSpeed;
                agent.acceleration = 20f;
                agent.angularSpeed = 0f;
                agent.updateRotation = false;
                agent.stoppingDistance = 0.2f;
                agent.autoBraking = true;

                var capsule = Ensure<CapsuleCollider>(root);
                capsule.radius = 0.75f;
                capsule.height = 4f;
                capsule.center = new Vector3(0f, 2f, 0f);

                NoRegen(Ensure<Health>(root));
                var master = Ensure<MasterP1>(root);
                var tell = Ensure<BossTell>(root);
                master.settings = settings;
                master.bossTell = tell;
                master.attackFacingAngle = 25f;
                master.aggressionRange = 0f;
                SetPrivate(master, "playerMask", (LayerMask)LayerMask.GetMask("Player"));

                // Fist and sword.
                EnemyRig.CloseFist(animator, model, HumanBodyBones.RightHand, FingerCurl, ThumbCurl);
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                float k = animator.humanScale / Mathf.Max(hand.lossyScale.x, 1e-4f);
                Transform socket = EnemyRig.SwordSocket(animator, HumanBodyBones.RightHand, 12f, "Socket_SwordHand", 0.035f * k, 0.03f * k);
                GameObject sword = EnemyRig.Equip(socket, MasterSword);
                Mesh swordMesh = sword.GetComponentInChildren<MeshFilter>().sharedMesh;
                var tip = new GameObject("SwordTip").transform;
                tip.SetParent(sword.transform, false);
                tip.localPosition = new Vector3(0f, swordMesh.bounds.max.y - 0.05f, 0f);
                master.swordTip = tip;
                tell.swordTip = tip;

                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                master.palm = leftHand;
                tell.leftHand = leftHand;
                tell.bodyHeight = MasterAgentHeight;

                PrefabUtility.SaveAsPrefabAsset(root, P1Prefab);
                log.AppendLine(string.Format("{0}: humanScale {1:F2}, grip offsets x{2:F2}, sword tip {3:F2} m up the blade, agent '{4}' ({5})",
                    P1Prefab, animator.humanScale, k, tip.localPosition.y, MasterAgent, agent.agentTypeID));
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            return log.ToString();
        }

        // Health defaults to regenerating to full after 3 s (for training targets): a boss must not.
        private static void NoRegen(Health health)
        {
            var so = new SerializedObject(health);
            so.FindProperty("regenerateToFull").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T found = go.GetComponent<T>();
            return found != null ? found : go.AddComponent<T>();
        }

        private static MasterSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MasterSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<MasterSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            return settings;
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            if (value is LayerMask mask) p.intValue = mask.value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A NavMesh agent type, added to the project's settings if missing (as L3Level.AgentType, without SaveAssets,
        // which would also flush other people's unsaved edits).
        public static int AgentType(string name, float radius, float height)
        {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(id) == name)
                    return id;
            }

            int created = NavMesh.CreateSettings().agentTypeID;
            Object areas = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];
            var so = new SerializedObject(areas);
            SerializedProperty settings = so.FindProperty("m_Settings");
            SerializedProperty names = so.FindProperty("m_SettingNames");
            for (int i = 0; i < settings.arraySize; i++)
            {
                SerializedProperty entry = settings.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("agentTypeID").intValue != created)
                    continue;
                entry.FindPropertyRelative("agentRadius").floatValue = radius;
                entry.FindPropertyRelative("agentHeight").floatValue = height;
                entry.FindPropertyRelative("agentClimb").floatValue = 0.6f;
                entry.FindPropertyRelative("agentSlope").floatValue = 45f;
                if (names.arraySize <= i) names.arraySize = i + 1;
                names.GetArrayElementAtIndex(i).stringValue = name;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(areas);
            return created;
        }

        // --- Clips and controller ---

        // Measured 2026-11-10 on his rig (TipTimeline, the sword tip relative to his root): the clip second each blow
        // lands, and the blade's real reach, which sets the hit (the Footman lesson) rather than the note's numbers.
        private const float DecreeContact = 0.63f;
        private const float SentenceCut = 1.0f;     // sweep 1 = 0..1.0, sweep 2 = 1.0..1.45; the jump-slam after it is cut
        private const float SentenceEnd2 = 1.45f;
        private const float Sentence1Contact = 0.74f;
        private const float Sentence2Contact = 0.27f;
        private const float SeizeContact = 1.2f;
        private const float CommandRelease = 0.9f;  // the palm at the top (the clip is right-handed: the state mirrors it)
        private const float ThreadsRelease = 0.6f;  // the flick (mirrored too)
        private const float ThrustContact = 0.8f;
        private const float PommelContact = 1.35f;
        private const float DecreeReach = 4.6f;
        private const float SentenceReach = 5f;
        private const float ThrustReach = 4.2f;

        private class Cut
        {
            public string name, source;
            public float start, end = -1f;
            public bool pin, bakeRotation, bakeHeight;
        }

        // Every state plays a cut (a .anim in Cuts/), so root baking and pinning never touch the scaled copies.
        private static readonly Cut[] P1Cuts =
        {
            new Cut { name = "Seated", source = "Master_SittingIdle", bakeHeight = true },
            new Cut { name = "Rise", source = "Master_StandUp", bakeHeight = true, pin = true },
            new Cut { name = "Idle", source = "Master_Idle" },
            new Cut { name = "Walk", source = "Master_Walk" },
            new Cut { name = "Decree", source = "Master_Decree", bakeRotation = true, pin = true },
            new Cut { name = "Sentence1", source = "Master_Sentence", start = 0f, end = SentenceCut, bakeRotation = true, pin = true },
            new Cut { name = "Sentence2", source = "Master_Sentence", start = SentenceCut, end = SentenceEnd2, bakeRotation = true, pin = true },
            new Cut { name = "Seize", source = "Master_Seize", bakeRotation = true, pin = true },
            new Cut { name = "Command", source = "Shared_Command", bakeRotation = true, pin = true },
            new Cut { name = "Threads", source = "Master_Threads", bakeRotation = true, pin = true },
            new Cut { name = "GuardIn", source = "Master_GuardIn", bakeHeight = true, pin = true },
            new Cut { name = "GuardIdle", source = "Master_GuardIdle", bakeHeight = true, pin = true },
            new Cut { name = "Thrust", source = "Master_GuardThrust", bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "KneelDown", source = "Shared_KneelDown", bakeHeight = true, pin = true },
            new Cut { name = "KneelIdle", source = "Master_KneelIdle", bakeHeight = true, pin = true },
            new Cut { name = "KneelUp", source = "Master_KneelUp", bakeHeight = true, pin = true },
            new Cut { name = "Stagger", source = "Shared_Stagger", bakeRotation = true, pin = true },
            new Cut { name = "Flinch", source = "Master_Flinch", pin = true },
            new Cut { name = "Pommel", source = "Master_Pommel", bakeRotation = true, bakeHeight = true, pin = true },
            new Cut { name = "Summon", source = "Master_Summon", bakeHeight = true, pin = true },
            new Cut { name = "LeftArmRest", source = "Shared_KneelDown", start = 0f, end = 0.05f },
            new Cut { name = "PalmUp", source = "Shared_Command", start = CommandRelease - 0.05f, end = CommandRelease },
        };

        private static AnimationClip MakeCut(Cut cut)
        {
            AnimationClip source = Copy(cut.source);
            float end = cut.end > 0f ? cut.end : source.length;
            AnimationClip clip = EnemyRig.CopyClip(source, Cuts + "Master_" + cut.name + ".anim", cut.start, end);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = AnimationUtility.GetAnimationClipSettings(source).loopTime && cut.start <= 0f && cut.end < 0f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (cut.bakeRotation) EnemyRig.BakeRootRotation(clip);
            if (cut.bakeHeight) EnemyRig.BakeRootHeight(clip);
            if (cut.pin) EnemyRig.PinRoot(clip);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        [MenuItem("Kneel/Enemies/The Master/Build P1 Animator")]
        public static void BuildP1AnimatorMenu() => Debug.Log(BuildP1Animator());

        public static string BuildP1Animator()
        {
            EnsureFolder(Cuts);
            var cuts = new Dictionary<string, AnimationClip>();
            foreach (Cut cut in P1Cuts)
                cuts[cut.name] = MakeCut(cut);

            float walkSpeed = MeasureWalkSpeed(cuts["Walk"]);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(P1Controller);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(P1Controller);

            EnemyRig.RemoveBlendTrees(controller);
            // He walks slower than the clip covers on him: the walk sits at his walk speed, played slower to match, so the
            // feet don't slide; MoveRate speeds that up into his stride.
            float walk = EnsureSettings().walkSpeed;
            BlendTree move = EnemyRig.SpeedTree(controller, "Move", "Speed", (cuts["Idle"], 0f), (cuts["Walk"], walk));
            BlendTree circle = EnemyRig.SpeedTree(controller, "Circle", "Speed", (cuts["Idle"], 0f), (cuts["Walk"], walk * 0.5f));
            BlendTree guardWalk = EnemyRig.SpeedTree(controller, "GuardWalk", "Speed", (cuts["Idle"], 0f), (cuts["Walk"], walk));
            foreach (BlendTree tree in new[] { move, circle, guardWalk })
            {
                ChildMotion[] children = tree.children;
                children[1].timeScale = children[1].threshold / walkSpeed;
                tree.children = children;
            }

            EnemyRig.BuildBaseLayer(controller, "Idle",
                new EnemyRig.State { name = "Idle", motion = cuts["Idle"], exitBlend = 0.2f },
                new EnemyRig.State { name = "Seated", motion = cuts["Seated"], exitBlend = 0.3f },
                new EnemyRig.State { name = "Rise", motion = cuts["Rise"], exitBlend = 0.25f },
                new EnemyRig.State { name = "Move", motion = move, exitBlend = 0.2f },
                new EnemyRig.State { name = "Circle", motion = circle, exitBlend = 0.2f },
                new EnemyRig.State { name = "Decree", motion = cuts["Decree"] },
                new EnemyRig.State { name = "Sentence1", parameter = "Sentence", motion = cuts["Sentence1"] },
                new EnemyRig.State { name = "Sentence2", parameter = "Sentence", motion = cuts["Sentence2"], chained = true },
                new EnemyRig.State { name = "Sentence3", parameter = "Sentence", motion = cuts["Sentence1"], chained = true },
                new EnemyRig.State { name = "Seize", motion = cuts["Seize"] },
                new EnemyRig.State { name = "Command", motion = cuts["Command"] },
                new EnemyRig.State { name = "Threads", motion = cuts["Threads"] },
                // The Regal Guard walks in on his real walk (the guard is an upper-body layer over it: no glide).
                new EnemyRig.State { name = "GuardWalk", parameter = "Guard", motion = guardWalk },
                new EnemyRig.State { name = "Thrust", parameter = "Guard", motion = cuts["Thrust"], chained = true },
                // Parried: the stagger, not a kneel (owner, 2026-11-10).
                new EnemyRig.State { name = "ParriedStagger", parameter = "Parried", motion = cuts["Stagger"], exitBlend = 0.2f },
                new EnemyRig.State { name = "ParriedRecover", parameter = "Parried", motion = circle, chained = true, exitBlend = 0.2f },
                new EnemyRig.State { name = "Stagger", motion = cuts["Stagger"], exitBlend = 0.2f },
                new EnemyRig.State { name = "Flinch", motion = cuts["Flinch"], speed = 1.2f },
                new EnemyRig.State { name = "Pommel", motion = cuts["Pommel"], exitBlend = 0.25f },
                new EnemyRig.State { name = "Fallen", motion = cuts["KneelDown"], speed = 1.4f, final = true });

            // Speed parameters: the move states land their contact at the end of the tell (MoveSpeed); the walk trees speed up
            // into his stride (MoveRate); the rise fits the intro's stand time (RiseSpeed). The spells play mirrored, so the
            // left hand casts and the sword hand stays low.
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                AnimatorState s = child.state;
                string param = null;
                switch (s.name)
                {
                    case "Decree": case "Sentence1": case "Sentence2": case "Sentence3": case "Seize": case "Command": case "Threads": case "Thrust":
                    case "ParriedStagger": case "Stagger":
                        param = "MoveSpeed"; break;
                    case "Move": case "Circle": param = "MoveRate"; break;
                    case "Rise": param = "RiseSpeed"; break;
                }

                if (param != null)
                {
                    EnsureFloat(controller, param, 1f);
                    s.speedParameterActive = true;
                    s.speedParameter = param;
                }

                s.mirror = s.name == "Command" || s.name == "Threads";
            }

            AvatarMask leftArm = EnemyRig.ArmMask(Clips + "AM_LeftArm.mask", true);
            EnemyRig.SetOverlayLayer(controller, "LeftArmRest", leftArm, "LeftArmRest", cuts["LeftArmRest"], 0.2f);
            EnemyRig.SetOverlayLayer(controller, "PalmUp", leftArm, "PalmUp", cuts["PalmUp"], 0.6f);
            // The crossed blade of the Regal Guard over his walk: arms, spine and head (Great Sword Blocking Idle).
            AvatarMask upper = UpperBodyMask(Clips + "AM_UpperBody.mask");
            EnemyRig.SetOverlayLayer(controller, "GuardUp", upper, "GuardUp", cuts["GuardIdle"], 0.25f);
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "PalmUp") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                    if (child.state.name == "PalmUp") child.state.mirror = true;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.SaveAssetIfDirty(leftArm);

            // The table's measured values go on the prefab and into the settings.
            MasterSettings settings = EnsureSettings();
            settings.decreeReach = DecreeReach;
            settings.sentenceReach = SentenceReach;
            settings.thrustReach = ThrustReach;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);

            GameObject root = PrefabUtility.LoadPrefabContents(P1Prefab);
            try
            {
                root.GetComponent<Animator>().runtimeAnimatorController = controller;
                var master = root.GetComponent<MasterP1>();
                master.decreeContact = DecreeContact;
                master.sentenceContacts = new[] { Sentence1Contact, Sentence2Contact, Sentence1Contact };
                master.seizeContact = SeizeContact;
                master.commandRelease = CommandRelease;
                master.threadsRelease = ThreadsRelease;
                master.thrustContact = ThrustContact;
                master.pommelContact = PommelContact;
                master.staggerLength = cuts["Stagger"].length;
                master.walkClipSpeed = settings.walkSpeed;   // the Move tree's full-walk speed
                PrefabUtility.SaveAsPrefabAsset(root, P1Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            MasterRiseState.RiseClipLength = cuts["Rise"].length;
            return string.Format("{0}: {1} cuts, walk clip {2:F2} m/s on him, rise {3:F2} s", P1Controller, cuts.Count, walkSpeed, cuts["Rise"].length);
        }

        // Arms, fingers, spine and head; not the legs or the root (so the walk under it keeps its steps).
        private static AvatarMask UpperBodyMask(string path)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm
                    || part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, on);
            }

            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(mask);
            return mask;
        }

        private static void EnsureFloat(AnimatorController controller, string name, float value)
        {
            foreach (AnimatorControllerParameter p in controller.parameters)
                if (p.name == name) return;
            controller.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = value });
        }

        // The in-place walk's real speed on him: how fast a planted foot slides back under him, in world space (the Brute
        // lesson: read the feet in world space, his scale counts).
        public static float MeasureWalkSpeed(AnimationClip walk, string prefab = P1Prefab)
        {
            GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var animator = go.GetComponent<Animator>();
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                const int steps = 120;
                var samples = new List<(float y, float z)>[2] { new List<(float, float)>(), new List<(float, float)>() };
                for (int i = 0; i <= steps; i++)
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    walk.SampleAnimation(go, walk.length * i / steps);
                    samples[0].Add((left.position.y, left.position.z));
                    samples[1].Add((right.position.y, right.position.z));
                }

                float dt = walk.length / steps, total = 0f;
                int count = 0;
                foreach (var foot in samples)
                {
                    float minY = foot.Min(s => s.y);
                    for (int i = 1; i < foot.Count; i++)
                    {
                        // Planted: within 4 cm of the foot's lowest point; its backward slide is the body's travel.
                        if (foot[i].y < minY + 0.04f && foot[i - 1].y < minY + 0.04f)
                        {
                            total += -(foot[i].z - foot[i - 1].z) / dt;
                            count++;
                        }
                    }
                }

                return count > 0 ? total / count : 2f;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- L5 (edited in place: never rerun L5Level's Build Scene over the hand edits) ---

        private const string L5Scene = "Assets/Kneel/Scenes/Levels/L5_ThroneRoom.unity";
        private const string L5Folder = "Assets/Kneel/Scenes/Levels/L5_ThroneRoom";
        private static readonly Vector3 ThroneSeat = new Vector3(54f, 9.6f, 470.35f);
        private static readonly Vector3 DaisFoot = new Vector3(54f, 9f, 465.2f);
        private const float AggroZ = 432f;
        // The hall the boss fight can use (world), for the NavMesh bake: doors to apse, wall to wall.
        private static readonly Bounds HallVolume = new Bounds(new Vector3(54f, 10.5f, 440.5f), new Vector3(44f, 5f, 66f));

        [MenuItem("Kneel/Enemies/The Master/Place in L5")]
        public static void PlaceInL5Menu() => Debug.Log(PlaceInL5());

        // Puts the fight in the open L5 scene: a "TheMaster" root (MasterBoss, the seat, the dais foot, the ash-mist seal
        // and the Phase 1 prefab), scales the throne up for a 4.2 m sitter (open call 7: about 1.5x, his head level with
        // its crest when he stands), hides the old baked stand-in, and bakes his NavMesh over the hall. Saves the scene.
        public static string PlaceInL5()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != L5Scene)
                return "open L5_ThroneRoom first (active: " + scene.path + ")";

            var log = new StringBuilder();
            GameObject root = GameObject.Find("TheMaster") ?? new GameObject("TheMaster");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Transform seat = Child(root.transform, "ThroneSeat", ThroneSeat, Quaternion.Euler(0f, 180f, 0f));
            Transform foot = Child(root.transform, "DaisFoot", DaisFoot, Quaternion.Euler(0f, 180f, 0f));
            Transform sealT = Child(root.transform, "HallSeal", new Vector3(54f, 9f, AggroZ), Quaternion.identity);
            var seal = Ensure<HallSeal>(sealT.gameObject);
            seal.mistMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Smoke.mat");
            seal.emberMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Ember.mat");

            Transform p1 = root.transform.Find("TheMaster_P1");
            if (p1 == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P1Prefab), root.transform);
                p1 = instance.transform;
            }

            p1.SetPositionAndRotation(seat.position, seat.rotation);

            var boss = Ensure<MasterBoss>(root);
            boss.settings = EnsureSettings();
            boss.master = p1.GetComponent<MasterP1>();
            boss.seal = seal;
            boss.throneSeat = seat;
            boss.daisFoot = foot;
            boss.aggroZ = AggroZ + 0.5f;
            if (AssetDatabase.LoadAssetAtPath<BossHealthBar>(BossBarPath) == null)
                BuildBossBar();
            boss.barPrefab = AssetDatabase.LoadAssetAtPath<BossHealthBar>(BossBarPath);

            // The throne, scaled about its base so a 4.2 m man fits it (it was sized for the 2.8 m stand-in).
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name == "Throne" && t.parent != null && t.parent.name == "Dais" && t.localScale.x < 1.2f)
                {
                    t.localScale = Vector3.one * 1.5f;
                    log.AppendLine("throne scaled 1.5x");
                }

                if (t.name == "Throne" && t.parent != null && t.parent.name == "ThroneHall" && t.localScale.y < 4f)
                {
                    // The greybox throne's collider (its renderer is hidden), scaled the same about its base.
                    t.localScale = new Vector3(4.5f, 5.25f, 3.75f);
                    t.position = new Vector3(t.position.x, 9.6f + 5.25f * 0.5f, t.position.z + 0.6f);
                }

                if (t.name == "Master (stand-in)" && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    log.AppendLine("stand-in hidden");
                }
            }

            log.AppendLine(BakeMasterNavMesh(root));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            log.AppendLine("saved " + scene.path);
            return log.ToString();
        }

        private static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            t.SetPositionAndRotation(position, rotation);
            return t;
        }

        // His NavMesh, over the hall only, from the physics colliders on the walkable and obstacle layers.
        public static string BakeMasterNavMesh(GameObject root)
        {
            var surface = Ensure<Unity.AI.Navigation.NavMeshSurface>(root);
            surface.agentTypeID = AgentType(MasterAgent, MasterAgentRadius, MasterAgentHeight);
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.Volume;
            surface.center = HallVolume.center;
            surface.size = HallVolume.size;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default", "Ground", "Obstacles");
            surface.BuildNavMesh();
            EnsureFolder(L5Folder + "/");
            string path = L5Folder + "/NavMesh-Master.asset";
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
            var triangulation = NavMesh.CalculateTriangulation();
            return "NavMesh (Master) baked: " + path + ", " + (triangulation.indices.Length / 3) + " triangles in the scene's NavMesh";
        }

        // --- The player's side ---

        private const string PlayerController = "Assets/Kneel/Player/AC_Player.controller";
        private const string PlayerPrefab = "Assets/Kneel/Player/Player.prefab";

        // Kael's forced kneel (PlayerCombat's Kneeling state): KneelDown, KneelIdle and KneelUp on the FullBody layer,
        // cross-faded by code like the other reactions; and PlayerScar on the player. Leaves everything else alone.
        [MenuItem("Kneel/Enemies/The Master/Add Player Kneel")]
        public static void AddPlayerKneelMenu() => Debug.Log(AddPlayerKneel());

        public static string AddPlayerKneel()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerController);
            AnimatorStateMachine full = controller.layers.First(l => l.name == "FullBody").stateMachine;
            var log = new StringBuilder();
            foreach (var (state, clip, speed) in new[] { ("KneelDown", "kneel-down", 3.6f), ("KneelIdle", "kneel-idle", 1f), ("KneelUp", "kneel-up", 3f) })
            {
                AnimationClip motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(KaelCopies + clip + ".anim");
                AnimatorState existing = full.states.Select(s => s.state).FirstOrDefault(s => s.name == state);
                if (existing == null)
                    existing = full.AddState(state);
                existing.motion = motion;
                existing.speed = speed;
                existing.writeDefaultValues = true;
                log.Append(state + " ");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                if (root.GetComponent<PlayerScar>() == null)
                {
                    root.AddComponent<PlayerScar>();
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                    log.Append("+PlayerScar");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return log.ToString();
        }

        // --- Analysis: where the blade goes ---

        // Prints, for a clip on the built prefab, the sword tip's height, its distance in front of him, its bearing
        // (degrees from straight ahead, + = his right) and speed, and the hips' height, every `step` seconds. Used to pick
        // contact frames (the tip passing in front at speed) and cut points (where the blade is slowest).
        public static string TipTimeline(string clipName, float step = 0.05f)
        {
            AnimationClip clip = Copy(clipName);
            GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(P1Prefab));
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var master = go.GetComponent<MasterP1>();
                var animator = go.GetComponent<Animator>();
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var sb = new StringBuilder(clipName + " (" + clip.length.ToString("F2") + " s): t  tipY  tipFwd  bearing  speed  hipsY  lhandY lhandFwd\n");
                Vector3 last = Vector3.zero;
                for (float t = 0f; t <= clip.length + 1e-4f; t += step)
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    clip.SampleAnimation(go, t);
                    // Relative to his root: in game he doesn't use root motion, so the clip's travel never happens.
                    Vector3 tip = go.transform.InverseTransformPoint(master.swordTip.position);
                    Vector3 root = go.transform.position;
                    float speed = t > 0f ? (tip - last).magnitude / step : 0f;
                    last = tip;
                    float bearing = Mathf.Atan2(tip.x, tip.z) * Mathf.Rad2Deg;
                    Vector3 lh = go.transform.InverseTransformPoint(leftHand.position);
                    Vector3 hp = go.transform.InverseTransformPoint(hips.position);
                    sb.AppendLine(string.Format("{0,5:F2} {1,5:F2} {2,6:F2} {3,7:F0} {4,6:F1} {5,5:F2} {6,6:F2} {7,6:F2}  root {8,5:F2} {9,5:F2}",
                        t, tip.y, tip.z, bearing, speed, hp.y, lh.y, lh.z, root.z, root.y));
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
