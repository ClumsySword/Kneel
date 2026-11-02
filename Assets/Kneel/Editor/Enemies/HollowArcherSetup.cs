using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The Hollow Archer's table for EnemyRig. Safe to rerun: it rebuilds the clips, controller, bow and sockets in place.
    public static class HollowArcherSetup
    {
        private const string Folder = "Assets/Kneel/Enemies/HollowArcher/";
        private const string Prefab = Folder + "HollowArcher.prefab";
        private const string Model = Folder + "Models/HollowArcher.fbx";
        private const string Clips = Folder + "Animations/Mixamo/";
        private const string Weapons = Folder + "Weapons/";
        private const string BowPrefab = Weapons + "ArcherBow.prefab";
        private const string ArrowPrefab = Folder + "Arrow.prefab";

        // Mixamo's Longbow clips, downloaded in place on its default character. His rig has its bone names, so they copy
        // his avatar.
        private static readonly (string file, string clip, bool loop)[] Mixamo =
        {
            ("standing-idle-01", "archer-idle", true),
            ("standing-walk-forward", "archer-walk", true),
            ("standing-aim-walk-forward", "archer-aim-walk", true),
            ("standing-draw-arrow", "archer-draw-arrow", false),
            ("standing-aim-overdraw", "archer-overdraw", false),
            ("standing-aim-recoil", "archer-recoil", false),
            ("standing-react-small-from-front", "archer-hit", false),
            ("standing-react-large-from-front", "archer-stagger", false),
            ("standing-death-backward-01", "archer-death", false),
        };

        // Clip seconds on standing-draw-arrow, measured from the right hand on his rig: over the shoulder, then back at
        // the bow.
        private const float QuiverTime = 0.34f;
        private const float NockTime = 0.59f;

        // Quick enough that the arrow leaves (1.3 s in) while a player sprinting in from his range is still 4 m out.
        private const float NockSpeed = 1.6f;

        // Overdraw's slow pull, sped up to fill the design note's 20 frames at 30 fps: the final tell. It starts from the
        // pose the nock clip ends on.
        private const float DrawLength = 3.6f;
        private const float DrawTell = 20f / 30f;

        // Recoil opens on the same pull: this skips to where the string hand has gone as far back as the draw took it.
        private const float RecoilStart = 0.17f;

        private const float RecoveryLength = 0.9f;

        // The death clip's fall lands him 10 cm under the floor: this brings him up to it as he goes down.
        private const float DeathLift = 0.1f;
        private const float DeathFallStart = 0.8f;
        private const float DeathFallEnd = 1.8f;

        // Measured from the feet in stance on his rig, since the clips carry no root travel.
        private const float WalkClipSpeed = 1.12f;
        private const float AimWalkClipSpeed = 1.12f;

        // Slow on purpose: both are under the player's walk. Patrol is the clip at its own speed.
        private const float ChaseStride = 1.3f;
        private const float PatrolSpeed = WalkClipSpeed;
        private const float ChaseSpeed = AimWalkClipSpeed * ChaseStride;

        // The string's ends on the limb tips, in ArcherBow.prefab's space, measured from the mesh.
        private static readonly Vector3 StringTop = new Vector3(0.01f, 0.65f, -0.25f);
        private static readonly Vector3 StringBottom = new Vector3(0.01f, -0.65f, -0.25f);

        // Where the arrow crosses the bow: above the grip, on the archer's left of it.
        private static readonly Vector3 ArrowRest = new Vector3(-0.022f, 0.04f, 0.02f);

        // On mixamorig:Spine2, in its own axes (+X his right, +Z his front): the quiver's base by the left of his waist,
        // leaning across his back so its arrows stand behind the right shoulder.
        private static readonly Vector3 QuiverBase = new Vector3(-0.09f, -0.3f, -0.235f);
        private static readonly Vector3 QuiverAxis = new Vector3(0.36f, 0.93f, 0f);

        // How far up the quiver the hand reaches: into the arrows, above the tube.
        private const float QuiverReach = 0.66f;

        [MenuItem("Kneel/Enemies/Hollow Archer/Convert Clips")]
        public static void ConvertClips()
        {
            foreach (var entry in Mixamo)
            {
                EnemyRig.ConvertToHumanoid(Clips + entry.file + ".fbx", entry.clip, entry.loop, Model);
            }
        }

        private static AnimationClip Clip(string file, string clip) => EnemyRig.FindClip(Clips + file + ".fbx", clip);

        [MenuItem("Kneel/Enemies/Hollow Archer/Build Animator")]
        public static void BuildAnimator()
        {
            string animations = Folder + "Animations/";

            AnimationClip idle = Clip("standing-idle-01", "archer-idle");
            AnimationClip walk = Clip("standing-walk-forward", "archer-walk");
            AnimationClip aimWalk = Clip("standing-aim-walk-forward", "archer-aim-walk");
            AnimationClip reach = Clip("standing-draw-arrow", "archer-draw-arrow");
            AnimationClip overdraw = Clip("standing-aim-overdraw", "archer-overdraw");
            AnimationClip recoil = Clip("standing-aim-recoil", "archer-recoil");

            AnimationClip nock = EnemyRig.CopyClip(reach, animations + "Archer_Nock.anim", 0f, reach.length,
                EnemyRig.Event("ArrowDrawn", QuiverTime), EnemyRig.Event("ArrowNocked", NockTime));
            AnimationClip draw = EnemyRig.CopyClip(overdraw, animations + "Archer_Draw.anim", 0f, DrawLength,
                EnemyRig.Event("ArrowRelease", DrawLength - 0.02f));
            AnimationClip shoot = EnemyRig.CopyClip(recoil, animations + "Archer_Shoot.anim", RecoilStart, recoil.length,
                EnemyRig.Event("AnimationTrigger", recoil.length - RecoilStart - 0.03f));
            AnimationClip recovery = EnemyRig.CopyClip(idle, animations + "Archer_Recovery.anim", 0f, RecoveryLength,
                EnemyRig.Event("AnimationTrigger", RecoveryLength - 0.03f));
            AnimationClip deathClip = Clip("standing-death-backward-01", "archer-death");
            AnimationClip death = EnemyRig.CopyClip(deathClip, animations + "Archer_Death.anim", 0f, deathClip.length);

            // The shot turns his whole body from the stance to side-on to the target and back.
            EnemyRig.BakeRootRotation(nock);
            EnemyRig.BakeRootRotation(draw);
            EnemyRig.BakeRootRotation(shoot);

            // He falls to the floor: without this he lies in mid-air at standing height.
            EnemyRig.BakeRootHeight(death);
            EnemyRig.LiftRoot(death, DeathLift, DeathFallStart, DeathFallEnd);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(animations + "HollowArcher.controller");

            EnemyRig.RemoveBlendTrees(controller);
            var patrol = EnemyRig.SpeedTree(controller, "Patrol", "Speed", (idle, 0f), (walk, WalkClipSpeed));
            var chase = EnemyRig.SpeedTree(controller, "Chase", "Speed", (idle, 0f), (aimWalk, AimWalkClipSpeed));

            EnemyRig.BuildBaseLayer(controller, "Idle",
                new EnemyRig.State { name = "Idle", motion = idle, exitBlend = 0.2f },
                new EnemyRig.State { name = "Walk", parameter = "Move", motion = patrol, exitBlend = 0.25f },
                new EnemyRig.State { name = "Recovery", motion = recovery, exitBlend = 0.25f },
                new EnemyRig.State { name = "Chase", motion = chase, speed = ChaseStride, exitBlend = 0.2f },
                new EnemyRig.State { name = "Attack - Idle", parameter = "Attack", motion = nock, speed = NockSpeed },
                new EnemyRig.State { name = "Attack - Draw", parameter = "Attack", motion = draw, speed = DrawLength / DrawTell, chained = true },
                new EnemyRig.State { name = "Attack - Shoot", parameter = "Attack", motion = shoot, chained = true },
                new EnemyRig.State { name = "Hit", motion = Clip("standing-react-small-from-front", "archer-hit"), speed = 1.5f, exitBlend = 0.1f },
                new EnemyRig.State { name = "Stagger", motion = Clip("standing-react-large-from-front", "archer-stagger"), speed = 0.7f, exitBlend = 0.1f },
                new EnemyRig.State { name = "Dead", motion = death, final = true });

            EnemyRig.Chain(controller, "Attack - Idle", "Attack - Draw", 0.05f);
            EnemyRig.Chain(controller, "Attack - Draw", "Attack - Shoot", 0.04f, "Shoot");
            EnemyRig.EnableIK(controller);

            foreach (Object asset in new Object[] { nock, draw, shoot, recovery, death, controller })
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        [MenuItem("Kneel/Enemies/Hollow Archer/Build Bow")]
        public static void BuildBow()
        {
            var root = new GameObject("ArcherBow");

            // The FBX's limbs sweep toward its +Z: turned round, the bow's +Z is the way the arrow goes.
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Weapons + "SM_ArcherBow.fbx"), root.transform);
            mesh.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

            Transform top = new GameObject("StringTop").transform;
            top.SetParent(root.transform, false);
            top.localPosition = StringTop;
            Transform bottom = new GameObject("StringBottom").transform;
            bottom.SetParent(root.transform, false);
            bottom.localPosition = StringBottom;

            var material = AssetDatabase.LoadAssetAtPath<Material>(Weapons + "M_BowString.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", new Color32(0xC4, 0xB8, 0xA4, 0xFF));
                AssetDatabase.CreateAsset(material, Weapons + "M_BowString.mat");
            }

            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.widthMultiplier = 0.009f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var bowString = root.AddComponent<BowString>();
            var fields = new SerializedObject(bowString);
            fields.FindProperty("top").objectReferenceValue = top;
            fields.FindProperty("bottom").objectReferenceValue = bottom;
            fields.FindProperty("line").objectReferenceValue = line;
            fields.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BowPrefab);
            Object.DestroyImmediate(root);
        }

        // The arrow in flight: its nock is the origin, the same as the one in his hand, and the hit sits on the tip.
        [MenuItem("Kneel/Enemies/Hollow Archer/Build Arrow")]
        public static void BuildArrow()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ArrowPrefab);
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Weapons + "SM_ArcherArrow.fbx").GetComponentInChildren<MeshFilter>();
                root.transform.localScale = Vector3.one;
                root.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                root.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;

                var tip = root.GetComponent<SphereCollider>();
                tip.center = new Vector3(0f, 0f, 0.93f);
                tip.radius = 0.06f;

                // No gravity: a slow arrow would drop metres over his range.
                var body = root.GetComponent<Rigidbody>();
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                // A shaft alone all but disappears in the levels' dark, and an arrow has to be seen to be rolled through.
                var trail = root.GetComponent<TrailRenderer>();
                if (trail == null)
                {
                    trail = root.AddComponent<TrailRenderer>();
                }

                trail.sharedMaterial = TrailMaterial();
                trail.time = 0.25f;
                trail.widthCurve = AnimationCurve.Linear(0f, 0.05f, 1f, 0f);
                trail.minVertexDistance = 0.08f;
                trail.numCapVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.colorGradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color32(0xFF, 0xC0, 0x70, 0xFF), 0f), new GradientColorKey(new Color32(0xD0, 0x50, 0x18, 0xFF), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
                };

                PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material TrailMaterial()
        {
            string path = Weapons + "M_ArrowTrail.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // Takes its colour from the trail's gradient, which URP's own unlit shader ignores.
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        [MenuItem("Kneel/Enemies/Hollow Archer/Equip Weapons")]
        public static void EquipWeapons()
        {
            AnimationClip aim = Clip("standing-aim-overdraw", "archer-overdraw");

            GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var archer = root.GetComponent<HollowArcher>();
                var animator = root.GetComponent<Animator>();

                // Out of the bow while it's rebuilt, so the prefab's reference to it survives.
                archer.shootPoint.SetParent(root.transform, false);

                Transform bowSocket = EnemyRig.BowSocket(animator, HumanBodyBones.LeftHand, aim, 0f);
                GameObject bow = EnemyRig.Equip(bowSocket, BowPrefab);
                archer.bowString = bow.GetComponent<BowString>();

                archer.shootPoint.SetParent(bow.transform, false);
                archer.shootPoint.SetLocalPositionAndRotation(ArrowRest, Quaternion.identity);

                // A rigid prop on the spine, not part of the skin.
                Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                Quaternion lean = Quaternion.LookRotation(Vector3.back, QuiverAxis);
                Transform quiverSocket = EnemyRig.Socket(chest, "Socket_Quiver", QuiverBase, lean);
                EnemyRig.Equip(quiverSocket, Weapons + "SM_ArcherQuiver.fbx");

                Transform mouth = new GameObject("QuiverMouth").transform;
                mouth.SetParent(quiverSocket, false);
                mouth.localPosition = Vector3.up * QuiverReach;
                archer.quiverMouth = mouth;

                archer.stringHand = EnemyRig.PinchSocket(animator, HumanBodyBones.RightHand);
                GameObject arrow = EnemyRig.Equip(archer.stringHand, Weapons + "SM_ArcherArrow.fbx");
                arrow.name = "HeldArrow";
                arrow.SetActive(false);
                archer.heldArrow = arrow.transform;

                archer.moveSpeed = PatrolSpeed;
                archer.chaseSpeed = ChaseSpeed;
                archer.fireRate = 0.3f;
                archer.arrowSpeed = 14f;

                // In seconds of the shot, so at the speed the state plays the clip.
                archer.reachTime = QuiverTime / NockSpeed;
                archer.nockSwing = (NockTime - QuiverTime) / NockSpeed;
                archer.aimStart = NockTime / NockSpeed;

                // Two lights (12 each) and a heavy (35), as the design note says, the same as the Footman.
                var health = new SerializedObject(root.GetComponent<Health>());
                health.FindProperty("maxHealth").floatValue = 50f;
                health.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Kneel/Enemies/Hollow Archer/Build All")]
        public static void BuildAll()
        {
            ConvertClips();
            BuildAnimator();
            BuildBow();
            BuildArrow();
            EquipWeapons();
        }
    }
}
