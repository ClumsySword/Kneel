using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The Ashen Footman's table for EnemyRig. Safe to rerun: it rebuilds the sockets, weapons and controller in place.
    public static class AshenFootmanSetup
    {
        private const string Folder = "Assets/Kneel/Enemies/AshenFootman/";
        private const string Prefab = Folder + "AshenFootman.prefab";
        private const string WeaponsFolder = Folder + "Weapons";

        private const string PackWeapons = "Assets/SyntyStudios/PolygonKnights/Prefabs/Weapons/";
        private const string PackAtlas = "Assets/SyntyStudios/PolygonKnights/Textures/Texture_01.png";

        private const string Sword = PackWeapons + "SM_Wep_Broadsword_01.prefab";
        private const string PackShield = PackWeapons + "SM_Wep_Shield_03.prefab";
        private const string Shield = WeaponsFolder + "/SM_Wep_Shield_03_Footman.prefab";

        // SM_Wep_Shield_03's arm straps and the inner face of its board, measured from the mesh.
        private static readonly Vector2 ShieldStrap = new Vector2(0.072f, -0.003f);
        private const float ShieldBoardZ = -0.008f;

        // The soldiers' crimson, as on the L4 banners.
        private static readonly Color Crimson = new Color32(0x51, 0x1A, 0x1E, 0xFF);

        // Far darker than it renders: L3's sun takes anything lighter close to white.
        private static readonly Color AshGrey = new Color32(0x4A, 0x4A, 0x4E, 0xFF);

        [MenuItem("Kneel/Enemies/Ashen Footman/Build Shield")]
        public static void BuildShield()
        {
            if (AssetDatabase.IsValidFolder(WeaponsFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets/Kneel/Enemies/AshenFootman", "Weapons");
            }

            // Shield_03's only coloured swatches: purple becomes crimson, blue becomes grey.
            Texture2D atlas = EnemyRig.RecolourAtlas(
                PackAtlas,
                WeaponsFolder + "/T_FootmanShield.png",
                new RectInt(448, 160, 32, 40),
                new EnemyRig.Swap { hueMin = 255f, hueMax = 285f, colour = Crimson },
                new EnemyRig.Swap { hueMin = 200f, hueMax = 225f, colour = AshGrey });

            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(PackShield).GetComponentInChildren<Renderer>().sharedMaterial;
            Material material = EnemyRig.CopyMaterial(pack, atlas, WeaponsFolder + "/M_FootmanShield.mat");
            EnemyRig.WeaponVariant(PackShield, material, Shield);
            AssetDatabase.SaveAssets();
        }

        // Clip seconds at which the blade tip passes straight ahead, measured on his rig with the animator running. The
        // Player's slash settings don't carry over: they mark where its hit window opens, earlier than contact.
        private const float OverheadHit = 0.62f;
        private const float SweepHit = 0.6f;

        // The overhead lands at 0.62 / 0.775 = 0.8 s: the design note's 24 frames at 30 fps. The sweep lands at 0.67 s.
        private const float OverheadSpeed = 0.775f;
        private const float SweepSpeed = 0.9f;

        private const float RecoveryLength = 0.8f;

        // death-1 is shared with the player and lands him 23 cm under the floor: his own copy brings him up to it as he goes
        // down.
        private const float DeathLift = 0.27f;
        private const float DeathFallStart = 1f;
        private const float DeathFallEnd = 2.2f;

        // A walk and run blended at 2.5 m/s cover less ground per step than their root speeds add up to, measured with the
        // playtest's stance speed. This is what brings the chase down to the slip of the clips at their own speeds.
        private const float ChaseStride = 1.16f;

        [MenuItem("Kneel/Enemies/Ashen Footman/Build Animator")]
        public static void BuildAnimator()
        {
            string animations = Folder + "Animations/";
            string player = EnemyRig.PlayerClips;

            AnimationClip stance = EnemyRig.FindClip(player + "sword and shield idle (4).fbx", "combat-idle");
            AnimationClip walk = EnemyRig.FindClip(player + "walk-forwards.fbx", "walk-forwards");
            AnimationClip run = EnemyRig.FindClip(player + "sword and shield run.fbx", "sprint-forwards");
            AnimationClip slash = EnemyRig.FindClip(player + "sword and shield slash.fbx", "slash-1");
            AnimationClip cut = EnemyRig.FindClip(player + "sword and shield slash (5).fbx", "slash-5");
            AnimationClip guard = EnemyRig.FindClip(player + "sword and shield block idle.fbx", "block-idle");

            AnimationClip overhead = EnemyRig.CopyClip(slash, animations + "Footman_Overhead.anim", 0f, slash.length,
                EnemyRig.Event("AttackHit", OverheadHit), EnemyRig.Event("AnimationTrigger", slash.length - 0.03f));
            AnimationClip sweep = EnemyRig.CopyClip(cut, animations + "Footman_Sweep.anim", 0f, cut.length,
                EnemyRig.Event("AttackHit", SweepHit), EnemyRig.Event("AnimationTrigger", cut.length - 0.03f));
            AnimationClip recovery = EnemyRig.CopyClip(stance, animations + "Footman_Recovery.anim", 0f, RecoveryLength,
                EnemyRig.Event("AnimationTrigger", RecoveryLength - 0.03f));

            AnimationClip sharedDeath = EnemyRig.FindClip(player + "sword and shield death.fbx", "death-1");
            AnimationClip death = EnemyRig.CopyClip(sharedDeath, animations + "Footman_Death.anim", 0f, sharedDeath.length);
            EnemyRig.LiftRoot(death, DeathLift, DeathFallStart, DeathFallEnd);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(animations + "AshenFootmanController.controller");
            float humanScale = HumanScale();

            EnemyRig.RemoveBlendTrees(controller);
            var locomotion = EnemyRig.SpeedTree(controller, "Locomotion", "Speed",
                (stance, 0f),
                (walk, EnemyRig.RootSpeed(walk, humanScale)),
                (run, EnemyRig.RootSpeed(run, humanScale)));

            EnemyRig.BuildBaseLayer(controller, "Idle",
                new EnemyRig.State { name = "Idle", motion = stance, exitBlend = 0.2f },
                new EnemyRig.State { name = "Walk", parameter = "Move", motion = locomotion, exitBlend = 0.25f },
                new EnemyRig.State { name = "Recovery", motion = recovery, exitBlend = 0.25f },
                new EnemyRig.State { name = "Chase", motion = locomotion, speed = ChaseStride, exitBlend = 0.2f },
                new EnemyRig.State { name = "Attack", motion = overhead, speed = OverheadSpeed },
                new EnemyRig.State { name = "Sweep", motion = sweep, speed = SweepSpeed },
                new EnemyRig.State { name = "Block", motion = EnemyRig.FindClip(player + "sword and shield block.fbx", "block-hit-1"), exitBlend = 0.1f },
                new EnemyRig.State { name = "Hit", motion = EnemyRig.FindClip(player + "sword and shield impact.fbx", "impact-1"), speed = 1.2f, exitBlend = 0.1f },
                new EnemyRig.State { name = "Stagger", motion = EnemyRig.FindClip(player + "sword and shield impact (2).fbx", "impact-2"), speed = 0.5f, exitBlend = 0.1f },
                new EnemyRig.State { name = "Dormant", motion = stance, exitBlend = 0f },
                new EnemyRig.State { name = "Dead", motion = death, final = true });

            AvatarMask mask = EnemyRig.ArmMask(animations + "AM_LeftArm.mask", true);
            EnemyRig.SetOverlayLayer(controller, "Guard", mask, "Guard", guard, 0.15f);

            foreach (Object asset in new Object[] { overhead, sweep, recovery, death, mask, controller })
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        private static float HumanScale()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            instance.hideFlags = HideFlags.HideAndDontSave;
            float scale = instance.GetComponent<Animator>().humanScale;
            Object.DestroyImmediate(instance);
            return scale;
        }

        [MenuItem("Kneel/Enemies/Ashen Footman/Equip Weapons")]
        public static void EquipWeapons()
        {
            AnimationClip guard = EnemyRig.FindClip(EnemyRig.PlayerClips + "sword and shield block idle.fbx", "block-idle");
            AnimationClip stance = EnemyRig.FindClip(EnemyRig.PlayerClips + "sword and shield idle (4).fbx", "combat-idle");

            GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var animator = root.GetComponent<Animator>();

                // His avatar maps index, middle and thumb only: the ring fingers need closing by hand.
                EnemyRig.CopyFingerPose(animator, HumanBodyBones.RightHand, "middle", "ring", stance, 0.5f);
                EnemyRig.CopyFingerPose(animator, HumanBodyBones.LeftHand, "middle", "ring", guard, 0.3f);

                Transform swordSocket = EnemyRig.SwordSocket(animator, HumanBodyBones.RightHand);
                EnemyRig.Equip(swordSocket, Sword);

                Transform shieldSocket = EnemyRig.ShieldSocket(animator, HumanBodyBones.LeftHand, guard, 0.3f, ShieldStrap, ShieldBoardZ);
                EnemyRig.Equip(shieldSocket, Shield);

                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
