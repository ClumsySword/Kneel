using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The Fool's table for EnemyRig. Safe to rerun: it rebuilds the clips, controller, greatsword and socket in place.
    public static class TheFoolSetup
    {
        private const string Folder = "Assets/Kneel/Enemies/TheFool/";
        private const string Prefab = Folder + "TheFool.prefab";
        private const string Animations = Folder + "Animations/";
        private const string Clips = Animations + "Mixamo/";
        private const string Greatsword = Folder + "Weapons/SM_FoolGreatsword.fbx";

        // Mixamo's clips have its bone names and his rig is CC_Base, so they can't copy his avatar: they copy the Archer's,
        // a Mixamo rig with a true T-pose, and Humanoid retargets them onto him from there. The Great Sword Pack was
        // downloaded on the Archer, so its skeleton is that avatar's own.
        private const string MixamoRig = "Assets/Kneel/Enemies/HollowArcher/Models/HollowArcher.fbx";

        private static readonly (string file, bool loop)[] Mixamo =
        {
            ("great-sword-idle", true),
            ("great-sword-walk", true),
            ("great-sword-run", true),
            ("great-sword-combo-slash", false),
            ("great-sword-downward-slash", false),
            ("great-sword-low-slash", false),
            ("great-sword-crouch-slash", false),
            ("great-sword-high-spin-attack", false),
            ("great-sword-strike-a-pose", false),
            ("great-sword-admiring-idle", true),
            ("great-sword-unblocked-impact", false),
            ("great-sword-head-impact", false),
            ("two-handed-sword-death", false),
        };

        // One swing: a piece of a clip, from where the sword is slowest before the blow to where it slows after it.
        // `contact` is the clip second the blade crosses straight ahead of him, measured on his rig. `windup` overrides
        // FollowupWindup when the swing follows another.
        private struct SwingCut
        {
            public string name;
            public string clip;
            public float start;
            public float end;
            public float contact;
            public float windup;

            public SwingCut(string name, string clip, float start, float end, float contact, float windup = 0f)
            {
                this.name = name;
                this.clip = clip;
                this.start = start;
                this.end = end;
                this.contact = contact;
                this.windup = windup;
            }
        }

        // Great Sword Combo Slash gives the chop and its two sweeps from his left, about 0.9 m of travel each. The others
        // are whole clips cut to their blow: a downward chop, a low sweep from his left, a crouching sweep from his right
        // (his only blow from that side), and the spin, a full turn into a downward slam, whose turn is its own tell.
        private static readonly SwingCut[] Swings =
        {
            new SwingCut("Chop", "great-sword-combo-slash", 0f, 1.3f, 0.87f),
            new SwingCut("Sweep", "great-sword-combo-slash", 1.3f, 2.15f, 1.76f),
            new SwingCut("Sweep2", "great-sword-combo-slash", 2.15f, 3.53f, 2.65f),
            new SwingCut("Downward", "great-sword-downward-slash", 0f, 0.85f, 0.66f),
            new SwingCut("LowSweep", "great-sword-low-slash", 0.5f, 1.2f, 0.93f),
            new SwingCut("CrouchSweep", "great-sword-crouch-slash", 0f, 0.95f, 0.48f),
            new SwingCut("Spin", "great-sword-high-spin-attack", 0.3f, 1.45f, 1.15f, 0.7f),
        };

        // His four strings, by swing name. The first is the design note's three swings, and the one he opens with.
        private static readonly (string name, string[] swings)[] Strings =
        {
            ("Three", new[] { "Chop", "Sweep", "Sweep2" }),
            ("Two", new[] { "Downward", "CrouchSweep" }),
            ("Spin", new[] { "Chop", "LowSweep", "Spin" }),
            ("Four", new[] { "Downward", "Sweep", "CrouchSweep", "Spin" }),
        };

        // The brief: the first wind-up is slow, about a second. The ones between swings are the Combat Feel Spec's 300 ms
        // with a margin, close to what Combo Slash does on its own (0.46 s and 0.5 s).
        private const float FirstWindup = 1f;
        private const float FollowupWindup = 0.4f;

        // He points the sword at the player, then comes for them.
        private const float NoticeEnd = 2.9f;
        private const float NoticeSpeed = 1.5f;

        // Falling Back Death lands his back 16 cm under the floor: this brings him up to it as he goes down.
        private const float DeathLift = 0.18f;
        private const float DeathFallStart = 1f;
        private const float DeathFallEnd = 1.6f;

        // Measured on his rig from the root travel: the pack's walk and run keep theirs.
        private const float WalkClipSpeed = 0.95f;
        private const float RunClipSpeed = 3.74f;

        // Fast on purpose: he outruns a sprinting player (4.57 m/s). The run plays at 1.34x, a short-legged scurry.
        private const float PatrolSpeed = 1.2f;
        private const float ChaseSpeed = 5f;

        // The pose the greatsword's socket is taken from: his stance at the start of the string.
        private const float GripPoseTime = 0f;

        // Three lights (12 + 20 + 12) kill him and two don't. So does one heavy (35) or a riposte.
        private const float MaxHealth = 34f;

        [MenuItem("Kneel/Enemies/The Fool/Convert Clips")]
        public static void ConvertClips()
        {
            foreach (var entry in Mixamo)
            {
                EnemyRig.ConvertToHumanoid(Clips + entry.file + ".fbx", entry.file, entry.loop, MixamoRig);
            }
        }

        private static AnimationClip Clip(string file) => EnemyRig.FindClip(Clips + file + ".fbx", file);

        [MenuItem("Kneel/Enemies/The Fool/Build Animator")]
        public static void BuildAnimator()
        {
            AnimationClip idle = Clip("great-sword-idle");
            AnimationClip walk = Clip("great-sword-walk");
            AnimationClip run = Clip("great-sword-run");
            var swingStates = new List<EnemyRig.State>();
            var saved = new List<Object>();
            foreach (SwingCut cut in Swings)
            {
                AnimationClip swing = EnemyRig.CopyClip(Clip(cut.clip), Animations + "Fool_" + cut.name + ".anim", cut.start, cut.end,
                    EnemyRig.Event("AttackHit", cut.contact - cut.start), EnemyRig.Event("AnimationTrigger", cut.end - cut.start - 0.03f));

                // The swings carry him forward in code, so the body stays over his root; the turns stay in the pose.
                EnemyRig.BakeRootRotation(swing);
                EnemyRig.PinRoot(swing);

                // Each is started by name (AttackState_TheFool), so only the first needs an entry from the Attack bool.
                swingStates.Add(new EnemyRig.State { name = "Swing_" + cut.name, parameter = "Attack", motion = swing, chained = swingStates.Count > 0 });
                saved.Add(swing);
            }

            AnimationClip notice = EnemyRig.CopyClip(Clip("great-sword-strike-a-pose"), Animations + "Fool_Notice.anim", 0f, NoticeEnd,
                EnemyRig.Event("AnimationTrigger", NoticeEnd - 0.03f));
            AnimationClip deathClip = Clip("two-handed-sword-death");
            AnimationClip death = EnemyRig.CopyClip(deathClip, Animations + "Fool_Death.anim", 0f, deathClip.length);

            EnemyRig.BakeRootRotation(notice);
            EnemyRig.BakeRootHeight(death);
            EnemyRig.LiftRoot(death, DeathLift, DeathFallStart, DeathFallEnd);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "TheFoolController.controller");

            EnemyRig.RemoveBlendTrees(controller);
            var patrol = EnemyRig.SpeedTree(controller, "Patrol", "Speed", (idle, 0f), (walk, PatrolSpeed));
            var chase = EnemyRig.SpeedTree(controller, "Chase", "Speed", (idle, 0f), (run, ChaseSpeed));

            var states = new List<EnemyRig.State>
            {
                new EnemyRig.State { name = "Idle", motion = idle, exitBlend = 0.2f },
                new EnemyRig.State { name = "Walk", parameter = "Move", motion = patrol, speed = PatrolSpeed / WalkClipSpeed, exitBlend = 0.2f },
                new EnemyRig.State { name = "Notice", motion = notice, speed = NoticeSpeed, exitBlend = 0.15f },
                new EnemyRig.State { name = "Chase", motion = chase, speed = ChaseSpeed / RunClipSpeed, exitBlend = 0.15f },
            };
            states.AddRange(swingStates);
            states.Add(new EnemyRig.State { name = "Recovery", motion = Clip("great-sword-admiring-idle"), exitBlend = 0.2f });
            states.Add(new EnemyRig.State { name = "Hit", motion = Clip("great-sword-unblocked-impact"), speed = 1.2f, exitBlend = 0.15f });
            states.Add(new EnemyRig.State { name = "Stagger", motion = Clip("great-sword-head-impact"), speed = 0.65f, exitBlend = 0.2f });
            states.Add(new EnemyRig.State { name = "Dead", motion = death, final = true });
            EnemyRig.BuildBaseLayer(controller, "Idle", states.ToArray());

            // Each swing plays at whatever speed lands its blade at the end of its wind-up (AttackState_TheFool).
            if (System.Array.Exists(controller.parameters, p => p.name == "SwingSpeed") == false)
            {
                controller.AddParameter(new AnimatorControllerParameter { name = "SwingSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            }

            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name.StartsWith("Swing_"))
                {
                    child.state.speedParameter = "SwingSpeed";
                    child.state.speedParameterActive = true;
                }
            }

            saved.AddRange(new Object[] { notice, death, controller });
            foreach (Object asset in saved)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        [MenuItem("Kneel/Enemies/The Fool/Equip Greatsword")]
        public static void EquipGreatsword()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var fool = root.GetComponent<TheFool>();
                var animator = root.GetComponent<Animator>();

                AnimationClip stance = Clip("great-sword-idle");
                Transform socket = EnemyRig.TwoHandedSocket(animator, HumanBodyBones.RightHand, HumanBodyBones.LeftHand, stance, GripPoseTime);
                EnemyRig.Equip(socket, Greatsword);
                fool.offHandGrip = EnemyRig.OffHandGrip(animator, socket, HumanBodyBones.LeftHand, stance, GripPoseTime);
                fool.offHandReach = 0.25f;

                fool.moveSpeed = PatrolSpeed;
                fool.chaseSpeed = ChaseSpeed;
                fool.turnSpeed = 5f;
                fool.aggressionRange = 12f;
                fool.attackRange = 4f;
                fool.attackFacingAngle = 20f;

                fool.firstWindup = FirstWindup;
                fool.followupWindup = FollowupWindup;
                fool.swings = System.Array.ConvertAll(Swings, cut => new TheFool.Swing { state = "Swing_" + cut.name, contact = cut.contact - cut.start, windup = cut.windup });
                fool.strings = System.Array.ConvertAll(Strings, entry => new TheFool.SwingString
                {
                    name = entry.name,
                    swings = System.Array.ConvertAll(entry.swings, name => System.Array.FindIndex(Swings, cut => cut.name == name)),
                });
                fool.followThroughSpeed = 1f;
                fool.commitTime = 0.3f;
                fool.lungeDistance = 2.5f;
                fool.lungeLead = 0.25f;
                fool.lungeTail = 0.12f;
                fool.lungeStopDistance = 1.2f;
                fool.recoveryTime = 1.6f;
                fool.breachReach = 1f;

                // The blade's tip passes 2.4-2.6 m out from his hips at each blow, mostly across the front.
                fool.attackDamage = 25f;
                fool.attackPoiseDamage = 20f;
                fool.attackImpact = 0.5f;
                fool.attackReach = 1.5f;
                fool.attackRadius = 1.1f;

                fool.hitStunTime = 0.6f;
                fool.staggerTime = 2f;
                fool.knockback = 0.35f;

                var health = new SerializedObject(root.GetComponent<Health>());
                health.FindProperty("maxHealth").floatValue = MaxHealth;
                health.ApplyModifiedPropertiesWithoutUndo();

                var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
                agent.acceleration = 20f;

                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Kneel/Enemies/The Fool/Build All")]
        public static void BuildAll()
        {
            ConvertClips();
            BuildAnimator();
            EquipGreatsword();
        }
    }
}
