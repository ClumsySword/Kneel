using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The Master's table for EnemyRig (the boss of L5: the Master in P1, the Ash Colossus in P2). Safe to rerun.
    // Stage 3 holds the body import; the clips, controller, sockets and prefab are added with the fight code.
    public static class MasterSetup
    {
        private const string Folder = "Assets/Kneel/Enemies/TheMaster/";
        public const string MasterModel = Folder + "Models/Master/Master.fbx";
        public const string ColossusModel = Folder + "Models/AshColossus/AshColossus.fbx";
        private const string Clips = Folder + "Animations/";
        private const string PlayerClips = "Assets/Kneel/Player/Animations/";
        // The scaled copies everything plays: the boss's here, Kael's under the player's animations.
        public const string BossCopies = Clips + "Clips/";
        public const string KaelCopies = PlayerClips + "Scaled/";

        // Every clip was downloaded on one Mixamo rig: Mixamo's auto-rig of the Master's 4.2 m mesh. Both bodies are
        // CC_Base (AccuRig), so the clips can't copy their avatars. They copy a Mixamo rig with a true T-pose instead, as
        // the Brute's and the Fool's do: the Archer's, for the boss and for Kael's new clips alike (the player's other clips
        // copy Mixamo_POLYGON_Guy_Naked, whose bones carry the Knight's names, not mixamorig, so it can't be copied). Measured
        // 2026-11-10 against the clip's own skeleton: palm facing within 2-21 degrees (no hand roll), limbs a few degrees.
        // That rig is 2x human size, so Humanoid reads its hips twice as high and every body floats (the Master's feet
        // 2.6 m up). The importer's scale factor doesn't help: Copy From Other Avatar ignores it (only an FBX's own unit
        // conversion reaches the humanoid curves). So each clip gets a .anim copy with its humanoid position curves (root
        // and IK goals) scaled until its standing hip height is the Archer's; rotations (muscles) are scale-free.
        // A Without Skin file's rest pose is its first frame, so the hip height comes from leg lengths, on the idle.
        private const string ArcherModel = "Assets/Kneel/Enemies/HollowArcher/Models/HollowArcher.fbx";

        // file (under Clips, without .fbx), loops. Each clip is named after its file.
        private static readonly (string file, bool loop)[] BossClips =
        {
            ("P1/Master_SittingIdle", true),
            ("P1/Master_StandUp", false),
            ("P1/Master_Idle", true),
            ("P1/Master_Walk", true),
            ("P1/Master_Decree", false),
            ("P1/Master_Sentence", false),
            ("P1/Master_Seize", false),
            ("P1/Master_Threads", false),
            ("P1/Master_GuardIn", false),
            ("P1/Master_GuardIdle", true),
            ("P1/Master_GuardOut", false),
            ("P1/Master_GuardThrust", false),
            ("P1/Master_Flinch", false),
            ("P1/Master_KneelIdle", true),
            ("P1/Master_KneelUp", false),
            ("P1/Master_Pommel", false),
            ("P1/Master_Summon", false),
            ("Shared/Shared_Command", false),
            ("Shared/Shared_KneelDown", false),
            ("Shared/Shared_Stagger", false),
            ("P2/Colossus_Flex", false),
            ("P2/Colossus_Roar", false),
            ("P2/Colossus_Idle", true),
            ("P2/Colossus_Walk", true),
            ("P2/Colossus_Cleave", false),
            ("P2/Colossus_Sweep", false),
            ("P2/Colossus_Grasp", false),
            ("P2/Colossus_Hammerfall", false),
            ("P2/Colossus_Leap", false),
            ("P2/Colossus_Gather", false),
            ("P2/Colossus_ThreadLash", false),
            ("P2/Colossus_Topple", false),
            ("P2/Colossus_LastCommand", false),
        };

        // Kael's forced kneel and knockdown (player states), downloaded on the same rig.
        private static readonly (string file, bool loop)[] KaelClips =
        {
            ("kneel-down", false),
            ("kneel-idle", true),
            ("kneel-up", false),
            ("knockdown-fall", false),
            ("knockdown-getup", false),
        };

        [MenuItem("Kneel/Enemies/The Master/Convert Clips")]
        public static void ConvertClips()
        {
            Debug.Log(ConvertAll());
        }

        public static string ConvertAll()
        {
            var log = new System.Text.StringBuilder();
            float archerHips = HipsHeight(ArcherModel);
            float clipHips = StandingHipsHeight(Clips + "P1/Master_Idle.fbx");
            float scale = archerHips / clipHips;
            log.AppendLine(string.Format("standing hips: clip rig {0:F3}, Archer {1:F3} -> position curves x{2:F4}", clipHips, archerHips, scale));
            EnsureFolder(BossCopies);
            EnsureFolder(KaelCopies);
            foreach (var entry in BossClips)
            {
                log.AppendLine(Convert(Clips + entry.file + ".fbx", entry.loop, ArcherModel, BossCopies, scale));
            }

            foreach (var entry in KaelClips)
            {
                log.AppendLine(Convert(PlayerClips + entry.file + ".fbx", entry.loop, ArcherModel, KaelCopies, scale));
            }

            return log.ToString();
        }

        private static string Convert(string path, bool loop, string avatarModel, string copyFolder, float scale)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return "missing " + path;
            }

            // A clip that copies another avatar may have failed to import (no takes), so start it from Generic again.
            Avatar target = AssetDatabase.LoadAllAssetsAtPath(avatarModel).OfType<Avatar>().FirstOrDefault();
            bool otherAvatar = importer.animationType == ModelImporterAnimationType.Human && importer.sourceAvatar != target;
            if (otherAvatar || !Mathf.Approximately(importer.globalScale, 1f) || !importer.useFileScale)
            {
                if (otherAvatar)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.sourceAvatar = null;
                }

                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.SaveAndReimport();
            }

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            EnemyRig.ConvertToHumanoid(path, name, loop, avatarModel);
            AnimationClip clip = EnemyRig.FindClip(path, name);
            if (clip == null)
            {
                return "no clip in " + path;
            }

            AnimationClip copy = ScaledCopy(clip, copyFolder + name + ".anim", scale);
            return string.Format("{0}: {1:F2} s, humanoid={2}, loop={3} -> {4}", name, copy.length, copy.humanMotion, loop, AssetDatabase.GetAssetPath(copy));
        }

        // A standalone .anim of a Humanoid clip with its position curves (RootT and the IK goals' T) scaled. Overwrites in
        // place, so the asset keeps its GUID.
        public static AnimationClip ScaledCopy(AnimationClip source, string path, float scale)
        {
            var copy = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path), frameRate = source.frameRate };
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                string p = binding.propertyName;
                if (p.StartsWith("RootT.") || p.StartsWith("LeftFootT.") || p.StartsWith("RightFootT.") || p.StartsWith("LeftHandT.") || p.StartsWith("RightHandT."))
                {
                    Keyframe[] keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].value *= scale;
                        keys[i].inTangent *= scale;
                        keys[i].outTangent *= scale;
                    }

                    curve = new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
                }

                AnimationUtility.SetEditorCurve(copy, binding, curve);
            }

            AnimationUtility.SetAnimationClipSettings(copy, AnimationUtility.GetAnimationClipSettings(source));
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(copy, path);
                return copy;
            }

            EditorUtility.CopySerialized(copy, existing);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        private static void EnsureFolder(string folder)
        {
            string path = folder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(path))
            {
                int slash = path.LastIndexOf('/');
                AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
            }
        }

        // Hip height of a Humanoid model at its own rest pose.
        private static float HipsHeight(string modelPath)
        {
            GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            try
            {
                return go.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).position.y;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Standing hip height of a Without Skin Mixamo clip's skeleton, at import scale 1, from bone lengths: the pelvis
        // drop to the hip joint, thigh, shin, and the ankle's height over the ground. Pose-independent but for the ankle,
        // so take it from a standing clip.
        private static float StandingHipsHeight(string clipPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(clipPath);
            float scale = importer.useFileScale ? importer.globalScale : 1f;
            GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(clipPath));
            try
            {
                Transform[] all = go.GetComponentsInChildren<Transform>(true);
                System.Func<string, Vector3> at = n => all.First(t => t.name == "mixamorig:" + n).position;
                float pelvis = at("Hips").y - at("LeftUpLeg").y;
                float thigh = Vector3.Distance(at("LeftUpLeg"), at("LeftLeg"));
                float shin = Vector3.Distance(at("LeftLeg"), at("LeftFoot"));
                float ankle = at("LeftFoot").y - go.transform.position.y;
                return (pelvis + thigh + shin + ankle) / scale;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [MenuItem("Kneel/Enemies/The Master/Import Bodies")]
        public static void ImportBodies()
        {
            Debug.Log(ImportBody(MasterModel));
            Debug.Log(ImportBody(ColossusModel));
        }

        // Both bodies are AccuRig rigs (CC_Base skeleton) of Meshy meshes bound in an A-pose.
        // - AccuRig marks every face hard, so normals are calculated smooth (180 degrees): the baked normal map was made
        //   against smooth normals, and Unity's Mikk tangents then match Blender's bake.
        // - The avatar's skeleton is the file's own "0_T-Pose" take. Unity's guessed T-pose from the A-pose bind plays the
        //   arms about 50 degrees off. The take is read in a Generic pass first, then the Humanoid avatar is built on it.
        public static string ImportBody(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return path + ": not found";
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalSmoothingSource = ModelImporterNormalSmoothingSource.FromAngle;
            importer.normalSmoothingAngle = 180f;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.maxBonesPerVertex = 4;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;

            // Pass 1: Generic, with the takes, to read the T-pose.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.SaveAndReimport();
            AnimationClip tpose = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name.Contains("T-Pose") && !c.name.StartsWith("__preview"));
            if (tpose == null)
            {
                return path + ": no T-pose take";
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject posed = Object.Instantiate(model);
            posed.name = model.name;
            try
            {
                tpose.SampleAnimation(posed, 0f);
                var skeleton = new List<SkeletonBone>();
                foreach (Transform t in posed.GetComponentsInChildren<Transform>(true))
                {
                    skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
                }

                // Pass 2: Humanoid, auto-mapped, for Unity's bone mapping. Pass 3: the same mapping on the T-pose skeleton.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.SaveAndReimport();
                HumanDescription description = importer.humanDescription;
                description.skeleton = skeleton.ToArray();
                importer.humanDescription = description;
                importer.SaveAndReimport();

                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
                Transform leftHand = posed.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CC_Base_L_Hand");
                Transform head = posed.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CC_Base_Head");
                return string.Format("{0}: avatar valid={1} human={2} mapped={3} skeleton={4} | mesh verts={5} tris={6} bounds={7} | T-pose L hand {8} head {9}",
                    path, avatar != null && avatar.isValid, avatar != null && avatar.isHuman, description.human.Length, skeleton.Count,
                    mesh != null ? mesh.vertexCount : 0, mesh != null ? mesh.triangles.Length / 3 : 0, mesh != null ? mesh.bounds.size.ToString("F2") : "-",
                    leftHand != null ? leftHand.position.ToString("F2") : "-", head != null ? head.position.ToString("F2") : "-");
            }
            finally
            {
                Object.DestroyImmediate(posed);
            }
        }
    }
}
