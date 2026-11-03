using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The Blighted Brute's table for EnemyRig. Safe to rerun: it rebuilds the clips, controller, cleavers and sockets in place.
    public static class BlightedBruteSetup
    {
        private const string Folder = "Assets/Kneel/Enemies/BlightedBrute/";
        private const string Prefab = Folder + "BlightedBrute.prefab";
        private const string Model = Folder + "Models/BlightedBrute.fbx";
        private const string Clips = Folder + "Animations/Mixamo/";
        private const string Cleaver = Folder + "Weapons/SM_BruteCleaver.fbx";

        // Mixamo's clips have its bone names and his rig is CC_Base, so they can't copy his avatar: they copy the Archer's,
        // a Mixamo rig with a true T-pose, and Humanoid retargets them onto him from there.
        private const string MixamoRig = "Assets/Kneel/Enemies/HollowArcher/Models/HollowArcher.fbx";

        private static readonly (string file, string clip, bool loop)[] Mixamo =
        {
            ("mutant-breathing-idle", "brute-idle", true),
            ("mutant-walking", "brute-walk", true),
            ("mutant-run", "brute-run", true),
            ("mutant-dying", "brute-death", false),
            ("standing-melee-attack-horizontal", "brute-swing", false),
            ("standing-melee-attack-downward", "brute-chop", false),
            ("standing-melee-attack-360-high", "brute-spin", false),
            ("dual-weapon-combo", "brute-dual", false),
            ("mutant-roaring", "brute-roar", false),
            ("standing-block-react-large", "brute-parried", false),
            ("standing-react-large-gut", "brute-hit", false),
            ("zombie-reaction-hit", "brute-stagger", false),
        };

        // Measured on his rig with the animator running: when the cleaver's tip passes straight ahead.
        private const float SwingHit = 0.92f;
        private const float ChopHit = 0.875f;

        // The spin's arms reach full stretch here, on his way out of the turn.
        private const float SpinHit = 1.15f;

        // Clip seconds of each blow of Dual Weapon Combo, played whole: where a blade crosses in front of him at full stretch.
        private static readonly float[] ComboHits = { 0.74f, 1.1f, 1.87f, 2.63f, 3.09f };
        private const float ComboEnd = 3.6f;

        // Slowed so the wind-ups are the ones in BlightedBrute: three times the Combat Feel Spec's 300 ms.
        private const float SwingWindup = 0.9f;
        private const float ChopWindup = 1f;
        private const float SpinWindup = 1.15f;
        private const float ComboWindup = 0.9f;
        private const float SwingSpeed = SwingHit / SwingWindup;
        private const float ChopSpeed = ChopHit / ChopWindup;
        private const float SpinSpeed = SpinHit / SpinWindup;
        private static readonly float ComboSpeed = ComboHits[0] / ComboWindup;

        // The follow-through, where the clips start to return to the stance.
        private const float SwingEnd = 1.6f;
        private const float ChopEnd = 1.7f;
        private const float SpinEnd = 2.3f;

        // The roar, once, when combat first starts.
        private const float RoarStart = 0.5f;
        private const float RoarEnd = 4.6f;

        private const float StanceLength = 0.1f;

        // The death clip's fall lands his back 0.3 m under the floor at his 1.5 scale: this brings him up to it as he goes down.
        private const float DeathLift = 0.23f;
        private const float DeathFallStart = 1.2f;
        private const float DeathFallEnd = 2.5f;

        // Measured from the feet in stance on his rig, since Mixamo's download carries no root travel even with In Place off.
        private const float WalkClipSpeed = 1.75f;
        private const float RunClipSpeed = 2.9f;

        // Slow on purpose. Both clips play at about 0.7x, so he takes long, heavy steps rather than short quick ones, and
        // a sprint leaves the chase behind.
        private const float PatrolSpeed = 1.2f;
        private const float ChaseSpeed = 2f;

        // Fist shape: the bend at each joint of the fingers, and the thumb's swing across the palm and its bend.
        private static readonly Vector3 FingerCurl = new Vector3(75f, 85f, 55f);
        private static readonly Vector2 ThumbCurl = new Vector2(55f, 35f);

        // The handle's seat in his big fist, in the hand bone's units.
        private const float GripBack = 0.05f;
        private const float GripDepth = 0.075f;

        [MenuItem("Kneel/Enemies/Blighted Brute/Convert Clips")]
        public static void ConvertClips()
        {
            foreach (var entry in Mixamo)
            {
                EnemyRig.ConvertToHumanoid(Clips + entry.file + ".fbx", entry.clip, entry.loop, MixamoRig);
            }
        }

        private static AnimationClip Clip(string file, string clip) => EnemyRig.FindClip(Clips + file + ".fbx", clip);

        [MenuItem("Kneel/Enemies/Blighted Brute/Build Animator")]
        public static void BuildAnimator()
        {
            string animations = Folder + "Animations/";

            AnimationClip idle = Clip("mutant-breathing-idle", "brute-idle");
            AnimationClip walk = Clip("mutant-walking", "brute-walk");
            AnimationClip run = Clip("mutant-run", "brute-run");
            AnimationClip swingClip = Clip("standing-melee-attack-horizontal", "brute-swing");
            AnimationClip chopClip = Clip("standing-melee-attack-downward", "brute-chop");
            AnimationClip spinClip = Clip("standing-melee-attack-360-high", "brute-spin");
            AnimationClip dualClip = Clip("dual-weapon-combo", "brute-dual");
            AnimationClip roar = Clip("mutant-roaring", "brute-roar");

            AnimationClip swing = EnemyRig.CopyClip(swingClip, animations + "Brute_Swing.anim", 0f, SwingEnd,
                EnemyRig.Event("AttackHit", SwingHit), EnemyRig.Event("AnimationTrigger", SwingEnd - 0.03f));
            AnimationClip chop = EnemyRig.CopyClip(chopClip, animations + "Brute_Chop.anim", 0f, ChopEnd,
                EnemyRig.Event("AttackHit", ChopHit), EnemyRig.Event("AnimationTrigger", ChopEnd - 0.03f));
            AnimationClip spin = EnemyRig.CopyClip(spinClip, animations + "Brute_Spin.anim", 0f, SpinEnd,
                EnemyRig.Event("AttackHit", SpinHit), EnemyRig.Event("AnimationTrigger", SpinEnd - 0.03f));
            var comboEvents = new List<AnimationEvent>();
            foreach (float hit in ComboHits)
            {
                comboEvents.Add(EnemyRig.Event("AttackHit", hit));
            }

            comboEvents.Add(EnemyRig.Event("AnimationTrigger", ComboEnd - 0.03f));
            AnimationClip combo = EnemyRig.CopyClip(dualClip, animations + "Brute_Combo.anim", 0f, ComboEnd, comboEvents.ToArray());
            AnimationClip notice = EnemyRig.CopyClip(roar, animations + "Brute_Notice.anim", RoarStart, RoarEnd,
                EnemyRig.Event("AnimationTrigger", RoarEnd - RoarStart - 0.03f));
            AnimationClip deathClip = Clip("mutant-dying", "brute-death");
            AnimationClip death = EnemyRig.CopyClip(deathClip, animations + "Brute_Death.anim", 0f, deathClip.length);
            AnimationClip stance = EnemyRig.CopyClip(swingClip, animations + "Brute_Stance.anim", 0f, StanceLength);

            // The attacks turn his whole body through the blow and back.
            EnemyRig.BakeRootRotation(swing);
            EnemyRig.BakeRootRotation(chop);
            EnemyRig.BakeRootRotation(spin);
            EnemyRig.BakeRootRotation(combo);
            EnemyRig.PinRoot(combo);
            EnemyRig.BakeRootRotation(notice);
            EnemyRig.BakeRootHeight(death);
            EnemyRig.LiftRoot(death, DeathLift, DeathFallStart, DeathFallEnd);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(animations + "BlightedBruteController.controller");

            EnemyRig.RemoveBlendTrees(controller);
            var patrol = EnemyRig.SpeedTree(controller, "Patrol", "Speed", (idle, 0f), (walk, PatrolSpeed));
            var chase = EnemyRig.SpeedTree(controller, "Chase", "Speed", (idle, 0f), (run, ChaseSpeed));

            EnemyRig.BuildBaseLayer(controller, "Idle",
                new EnemyRig.State { name = "Idle", motion = idle, exitBlend = 0.25f },
                new EnemyRig.State { name = "Walk", parameter = "Move", motion = patrol, speed = PatrolSpeed / WalkClipSpeed, exitBlend = 0.25f },
                new EnemyRig.State { name = "Notice", motion = notice, exitBlend = 0.25f },
                new EnemyRig.State { name = "Recovery", motion = idle, exitBlend = 0.25f },
                new EnemyRig.State { name = "Chase", motion = chase, speed = ChaseSpeed / RunClipSpeed, exitBlend = 0.2f },
                new EnemyRig.State { name = "Attack", motion = swing, speed = SwingSpeed },
                new EnemyRig.State { name = "Chop", motion = chop, speed = ChopSpeed },
                new EnemyRig.State { name = "Spin", motion = spin, speed = SpinSpeed },
                new EnemyRig.State { name = "Combo", motion = combo, speed = ComboSpeed },
                new EnemyRig.State { name = "Parried", motion = Clip("standing-block-react-large", "brute-parried"), speed = 1.2f, exitBlend = 0.2f },
                new EnemyRig.State { name = "Hit", motion = Clip("standing-react-large-gut", "brute-hit"), speed = 1.2f, exitBlend = 0.15f },
                new EnemyRig.State { name = "Stagger", motion = Clip("zombie-reaction-hit", "brute-stagger"), speed = 0.8f, exitBlend = 0.2f },
                new EnemyRig.State { name = "Dead", motion = death, final = true });

            AvatarMask arms = EnemyRig.ArmsMask(animations + "AM_Arms.mask");
            EnemyRig.SetOverlayLayer(controller, "Arms", arms, "Hold", stance, 0.25f);

            foreach (Object asset in new Object[] { swing, chop, spin, combo, notice, death, stance, arms, controller })
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        [MenuItem("Kneel/Enemies/Blighted Brute/Equip Weapons")]
        public static void EquipWeapons()
        {
            // No clip here was made to hold anything in both hands, so his fingers stay out of them.
            EnemyRig.UnmapFingers(Model);

            GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var brute = root.GetComponent<BlightedBrute>();
                var animator = root.GetComponent<Animator>();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);

                EnemyRig.CloseFist(animator, model, HumanBodyBones.RightHand, FingerCurl, ThumbCurl);
                EnemyRig.CloseFist(animator, model, HumanBodyBones.LeftHand, FingerCurl, ThumbCurl);

                EnemyRig.Equip(EnemyRig.SwordSocket(animator, HumanBodyBones.RightHand, 12f, "Socket_CleaverRight", GripBack, GripDepth), Cleaver);
                EnemyRig.Equip(EnemyRig.SwordSocket(animator, HumanBodyBones.LeftHand, 12f, "Socket_CleaverLeft", GripBack, GripDepth), Cleaver);

                brute.moveSpeed = PatrolSpeed;
                brute.chaseSpeed = ChaseSpeed;
                brute.swingWindup = SwingWindup;
                brute.chopWindup = ChopWindup;
                brute.spinWindup = SpinWindup;
                brute.comboHits = System.Array.ConvertAll(ComboHits, hit => hit / ComboSpeed);

                // Measured from the cleaver's tip at the frame it lands, with the 1.5 scale: 2.7 m for the swing, whose
                // sphere sits close in so it also takes the arc's sides, and 2.45 m for the chop.
                brute.attackRange = 2.5f;
                brute.attackReach = 1.2f;
                brute.attackRadius = 1.5f;
                brute.chopReach = 1.75f;
                brute.chopRadius = 0.7f;
                brute.spinRadius = 2.6f;
                brute.comboReach = 1.2f;
                brute.comboRadius = 1.5f;

                brute.attackDamage = 45f;
                brute.attackPoiseDamage = 80f;
                brute.attackImpact = 1f;
                brute.chopDamage = 60f;
                brute.chopPoiseDamage = 90f;
                brute.spinDamage = 40f;
                brute.spinPoiseDamage = 70f;
                brute.comboDamage = 18f;
                brute.comboPoiseDamage = 18f;

                // The cooldown after each blow, from the end of its clip: with the follow-through, about 2.7 s of opening, which a heavy pressed as he stops can land in.
                brute.swingRecovery = 1.9f;
                brute.chopRecovery = 1.7f;
                brute.spinRecovery = 1.5f;
                brute.comboRecovery = 2.1f;
                brute.comboAdvanceSpeed = 1.15f;

                // Two heavies (70 each) or two bashes (25 × 2.4) break it, and four lights don't. It holds a bash for
                // long enough to land the second one on his next opening, even after a full combo.
                brute.poise = new Poise(120f, 8f, 20f);
                brute.windupPoiseMultiplier = 1f;

                // Long enough for a heavy to land inside it.
                brute.staggerTime = 2.5f;
                brute.hitStunTime = 0.6f;
                brute.knockback = 0.15f;

                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Kneel/Enemies/Blighted Brute/Build All")]
        public static void BuildAll()
        {
            ConvertClips();
            BuildAnimator();
            EquipWeapons();
        }
    }
}
