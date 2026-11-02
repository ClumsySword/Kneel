using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Shared setup for rigged enemy prefabs: Humanoid clip import, .anim copies that carry animation events, weapon
    // sockets on the hands, and the animator controller. One enemy's setup is a table of calls into this (see
    // AshenFootmanSetup), so the next enemy only needs its own table.
    public static class EnemyRig
    {
        public const string PlayerClips = "Assets/Kneel/Player/Animations/";

        private const string MixamoAvatar = PlayerClips + "Mixamo_POLYGON_Guy_Naked.fbx";

        // --- Clips ---

        // Switches a Generic Mixamo FBX to Humanoid with the shared Mixamo avatar. Does nothing if it already is. Clips
        // downloaded on Mixamo's default character have its bone names, not the Knight's, so they can't copy that avatar;
        // `avatarModel` names another to copy. An enemy on a Mixamo rig should pass its own: a Without Skin file's skeleton
        // sits in the clip's first frame, so an avatar built from the file itself has a guessed T-pose, and the hands come
        // out rolled 90 degrees.
        public static void ConvertToHumanoid(string fbxPath, string clipName, bool loop, string avatarModel = MixamoAvatar)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            Avatar avatar = LoadAvatar(avatarModel);
            if (importer.animationType == ModelImporterAnimationType.Human && importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther && importer.sourceAvatar == avatar)
            {
                return;
            }

            TakeInfo take = importer.importedTakeInfos[importer.importedTakeInfos.Length - 1];
            foreach (TakeInfo info in importer.importedTakeInfos)
            {
                // "With skin" downloads can add a bind-pose Take 001 next to the real take.
                if (info.name == "mixamo.com")
                {
                    take = info;
                }
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.clipAnimations = new[]
            {
                new ModelImporterClipAnimation
                {
                    name = clipName,
                    takeName = take.name,
                    firstFrame = take.startTime * take.sampleRate,
                    lastFrame = take.stopTime * take.sampleRate,
                    loopTime = loop,
                    keepOriginalOrientation = true,
                    keepOriginalPositionY = true,
                    keepOriginalPositionXZ = true,
                },
            };
            importer.SaveAndReimport();
        }

        public static AnimationClip FindClip(string assetPath, string clipName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
            }

            Debug.LogError("No clip '" + clipName + "' in " + assetPath);
            return null;
        }

        // Writes a standalone .anim from an imported clip, trimmed to [start, end] seconds and carrying events at
        // times measured on the trimmed clip. Overwrites in place, so the asset keeps its GUID.
        public static AnimationClip CopyClip(AnimationClip source, string path, float start, float end, params AnimationEvent[] events)
        {
            var copy = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path), frameRate = source.frameRate };

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationUtility.SetEditorCurve(copy, binding, Trim(AnimationUtility.GetEditorCurve(source, binding), start, end));
            }

            // Clips that carry events are one-shots, whatever the source was.
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.startTime = 0f;
            settings.stopTime = end - start;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AnimationUtility.SetAnimationEvents(copy, events);

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(copy, path);
                return copy;
            }

            EditorUtility.CopySerialized(copy, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        public static AnimationEvent Event(string function, float time) =>
            new AnimationEvent { functionName = function, time = time };

        // Enemies don't use root motion, so a clip's root rotation is thrown away: a body that turns through the clip stays
        // where it started while its arms play on, then snaps round at the next clip. This keeps the turn in the pose.
        public static void BakeRootRotation(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopBlendOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        // Real travel speed of a clip on a body: the RootT delta over its length, scaled by that avatar's human scale.
        // AnimationClip.averageSpeed is skewed by lean, so it isn't used.
        public static float RootSpeed(AnimationClip clip, float humanScale)
        {
            AnimationCurve x = null;
            AnimationCurve z = null;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName == "RootT.x")
                {
                    x = AnimationUtility.GetEditorCurve(clip, binding);
                }
                else if (binding.propertyName == "RootT.z")
                {
                    z = AnimationUtility.GetEditorCurve(clip, binding);
                }
            }

            if (x == null || z == null)
            {
                return 0f;
            }

            float dx = x.Evaluate(clip.length) - x.Evaluate(0f);
            float dz = z.Evaluate(clip.length) - z.Evaluate(0f);
            return Mathf.Sqrt(dx * dx + dz * dz) / clip.length * humanScale;
        }

        // --- Sockets ---

        // Socket for a one-handed sword, built from the hand's own bones so it fits any rig. The item's +Y (blade)
        // leaves the index side of the fist, leaning `bladeLean` degrees toward the fingers, and its +X (edge) faces
        // the punch direction. The origin (grip centre) sits behind the middle knuckle, on the palm side.
        public static Transform SwordSocket(Animator animator, HumanBodyBones hand, float bladeLean = 12f, string socketName = "Socket_SwordHand")
        {
            Transform handBone = animator.GetBoneTransform(hand);
            GetHandAxes(handBone, out Vector3 knuckle, out Vector3 fingers, out Vector3 indexSide, out Vector3 palm);

            Vector3 blade = Quaternion.AngleAxis(bladeLean, palm) * indexSide;
            if (Vector3.Dot(blade, fingers) < 0f)
            {
                blade = Quaternion.AngleAxis(-bladeLean, palm) * indexSide;
            }

            Vector3 edge = Vector3.ProjectOnPlane(fingers, blade).normalized;
            Vector3 position = knuckle - fingers * 0.035f + palm * 0.03f;
            return Socket(handBone, socketName, position, Quaternion.LookRotation(Vector3.Cross(edge, blade), blade));
        }

        // Socket for a strapped shield, set from the grip itself. In a guard the palm faces the chest, so the board lies
        // against the back of the hand and forearm: the strap runs across the fist, the forearm runs along the board
        // toward the other strap. `strap` is the fist's strap in the shield's space (the other is at -x) and `boardZ`
        // the board's inner face. The board is seated just clear of the skin as posed at `poseTime` of `guardPose`, so
        // the arm can't poke through it. `tilt` turns the shield in its own plane about the fist.
        public static Transform ShieldSocket(Animator animator, HumanBodyBones hand, AnimationClip guardPose, float poseTime, Vector2 strap, float boardZ, float tilt = 0f, float clearance = 0.004f, string socketName = "Socket_ShieldHand")
        {
            Transform handBone = animator.GetBoneTransform(hand);
            GetHandAxes(handBone, out Vector3 knuckle, out Vector3 fingers, out Vector3 indexSide, out Vector3 palm);

            var posed = Object.Instantiate(animator.gameObject);
            posed.hideFlags = HideFlags.HideAndDontSave;
            posed.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            guardPose.SampleAnimation(posed, poseTime);

            var posedAnimator = posed.GetComponent<Animator>();
            Transform posedHand = posedAnimator.GetBoneTransform(hand);
            Transform posedElbow = posedAnimator.GetBoneTransform(hand == HumanBodyBones.LeftHand ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Vector3 forearm = posedHand.InverseTransformDirection(posedHand.position - posedElbow.position).normalized;

            // The strap's end that points up in the guard is the shield's +Y.
            Vector3 up = Vector3.Dot(posedHand.TransformDirection(indexSide), Vector3.up) >= 0f ? indexSide : -indexSide;
            up = Quaternion.AngleAxis(tilt, palm) * up;
            Quaternion rotation = Quaternion.LookRotation(-palm, up);

            // The fist holds the strap further down the arm; the other one rides on the forearm.
            float side = Vector3.Dot(rotation * Vector3.right, forearm) >= 0f ? 1f : -1f;
            Vector3 held = new Vector3(Mathf.Abs(strap.x) * side, strap.y, 0f);

            Vector3 fist = knuckle - fingers * 0.035f;
            float depth = SkinDepth(posed, posedElbow, posedHand, posedHand.TransformPoint(fist), posedHand.TransformDirection(-palm));
            Object.DestroyImmediate(posed);

            Vector3 position = fist - rotation * held - palm * (depth + clearance - boardZ);
            return Socket(handBone, socketName, position, rotation);
        }

        // Socket for a bow in its hand, set from the aim, as posed at `poseTime` of `aimPose`. The item's +Z (toward the
        // target) runs along the forearm, its +Y (the upper limb) stands straight up, tilted `cant` degrees about the arm,
        // and its origin (the grip) sits in the palm.
        public static Transform BowSocket(Animator animator, HumanBodyBones hand, AnimationClip aimPose, float poseTime, float cant = 0f, string socketName = "Socket_BowHand")
        {
            Transform handBone = animator.GetBoneTransform(hand);
            GetHandAxes(handBone, out Vector3 knuckle, out Vector3 fingers, out Vector3 indexSide, out Vector3 palm);

            var posed = Object.Instantiate(animator.gameObject);
            posed.hideFlags = HideFlags.HideAndDontSave;
            posed.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            aimPose.SampleAnimation(posed, poseTime);

            var posedAnimator = posed.GetComponent<Animator>();
            Transform posedHand = posedAnimator.GetBoneTransform(hand);
            Transform posedElbow = posedAnimator.GetBoneTransform(hand == HumanBodyBones.LeftHand ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Vector3 forearm = posedHand.InverseTransformDirection(posedHand.position - posedElbow.position).normalized;
            Vector3 up = Quaternion.AngleAxis(cant, forearm) * posedHand.InverseTransformDirection(Vector3.up);
            Object.DestroyImmediate(posed);

            Vector3 position = knuckle - fingers * 0.035f + palm * 0.03f;
            return Socket(handBone, socketName, position, Quaternion.LookRotation(forearm, up));
        }

        // Socket at the pinch of a drawing hand, where the string and an arrow's nock sit.
        public static Transform PinchSocket(Animator animator, HumanBodyBones hand, string socketName = "Socket_StringHand")
        {
            Transform handBone = animator.GetBoneTransform(hand);
            GetHandAxes(handBone, out Vector3 knuckle, out Vector3 fingers, out _, out Vector3 palm);
            return Socket(handBone, socketName, knuckle + fingers * 0.03f + palm * 0.025f, Quaternion.identity);
        }

        // How far the arm's skin reaches past `origin` along `direction`, for the vertices that follow the elbow or hand.
        private static float SkinDepth(GameObject posed, Transform elbow, Transform hand, Vector3 origin, Vector3 direction)
        {
            var skin = posed.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            skin.BakeMesh(baked, true);

            var arm = new HashSet<int>();
            for (int i = 0; i < skin.bones.Length; i++)
            {
                if (skin.bones[i] == elbow || skin.bones[i].IsChildOf(elbow))
                {
                    arm.Add(i);
                }
            }

            float depth = 0f;
            Vector3[] vertices = baked.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, skin.transform.lossyScale);
            for (int i = 0; i < vertices.Length; i++)
            {
                if (arm.Contains(weights[i].boneIndex0))
                {
                    depth = Mathf.Max(depth, Vector3.Dot(toWorld.MultiplyPoint3x4(vertices[i]) - origin, direction));
                }
            }

            Object.DestroyImmediate(baked);
            return depth;
        }

        // Clips only drive the fingers an avatar maps, so a finger it leaves out stays straight at the bind pose. This
        // gives it another finger's grip from `pose`, as a fixed pose on the prefab's bones.
        public static void CopyFingerPose(Animator animator, HumanBodyBones hand, string from, string to, AnimationClip pose, float poseTime)
        {
            var posed = Object.Instantiate(animator.gameObject);
            posed.hideFlags = HideFlags.HideAndDontSave;
            pose.SampleAnimation(posed, poseTime);

            Transform source = FindFinger(posed.GetComponent<Animator>().GetBoneTransform(hand), from);
            Transform target = FindFinger(animator.GetBoneTransform(hand), to);
            while (source != null && target != null && target.childCount > 0)
            {
                target.localRotation = source.localRotation;
                source = source.childCount > 0 ? source.GetChild(0) : null;
                target = target.GetChild(0);
            }

            Object.DestroyImmediate(posed);
        }

        // Puts a weapon prefab on a socket at identity (the socket defines the pose). Weapons are visual only: they keep
        // no collider, since Enemy hits are overlap spheres and a collider here would block the player.
        public static GameObject Equip(Transform socket, string weaponPrefabPath)
        {
            for (int i = socket.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(socket.GetChild(i).gameObject);
            }

            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(weaponPrefabPath), socket);
            weapon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            weapon.transform.localScale = Vector3.one;

            foreach (Collider collider in weapon.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            return weapon;
        }

        // --- Weapon colours ---

        // One swatch colour to replace: any saturated pixel with a hue in [hueMin, hueMax] degrees becomes `colour`. The
        // swatch's shading is kept, so `colour` lands on its average brightness.
        public struct Swap
        {
            public float hueMin;
            public float hueMax;
            public Color colour;
        }

        // A pack's colour atlas is shared by the whole pack and must not be edited, so a recolour is a copy of it with
        // only `region` (in pixels, from the bottom left) touched.
        public static Texture2D RecolourAtlas(string sourcePath, string destPath, RectInt region, params Swap[] swaps)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            image.LoadImage(System.IO.File.ReadAllBytes(sourcePath));

            foreach (Swap swap in swaps)
            {
                float total = 0f;
                int count = 0;
                foreach (Vector2Int pixel in region.allPositionsWithin)
                {
                    if (Matches(image.GetPixel(pixel.x, pixel.y), swap, out float value))
                    {
                        total += value;
                        count++;
                    }
                }

                foreach (Vector2Int pixel in region.allPositionsWithin)
                {
                    if (Matches(image.GetPixel(pixel.x, pixel.y), swap, out float value))
                    {
                        image.SetPixel(pixel.x, pixel.y, swap.colour * (value / (total / count)));
                    }
                }
            }

            System.IO.File.WriteAllBytes(destPath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(destPath);

            // The atlas is a set of flat swatches: same sampling as the source, or the colours bleed.
            var source = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(destPath);
            importer.filterMode = source.filterMode;
            importer.maxTextureSize = source.maxTextureSize;
            importer.mipmapEnabled = source.mipmapEnabled;
            importer.textureCompression = source.textureCompression;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(destPath);
        }

        // A Kneel-owned copy of a pack material with its own atlas.
        public static Material CopyMaterial(Material source, Texture2D baseMap, string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(source);
            }

            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MainTex", baseMap);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A prefab variant of a pack weapon wearing another material, so the pack's prefab stays as it is.
        public static string WeaponVariant(string weaponPrefabPath, Material material, string variantPath)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(weaponPrefabPath));
            instance.GetComponentInChildren<Renderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            Object.DestroyImmediate(instance);
            return variantPath;
        }

        // --- Animator controller ---

        public class State
        {
            public string name;

            // The animator bool. Defaults to the state's name.
            public string parameter;
            public Motion motion;
            public float speed = 1f;

            // Blend time of the state -> Exit transition.
            public float exitBlend = 0.15f;

            // No transition out: for final states such as death.
            public bool final;

            // Reached from another state of the same action (see Chain), not from Entry.
            public bool chained;
        }

        // Rebuilds the base layer in the one pattern every enemy uses: Entry -> state when its bool is true, state ->
        // Exit when it's false. `defaultState` plays with no bool set. The asset keeps its GUID.
        public static void BuildBaseLayer(AnimatorController controller, string defaultState, params State[] states)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states)
            {
                machine.RemoveState(child.state);
            }

            foreach (AnimatorTransition entry in machine.entryTransitions)
            {
                machine.RemoveEntryTransition(entry);
            }

            foreach (State spec in states)
            {
                string parameter = spec.parameter ?? spec.name;
                EnsureParameter(controller, parameter, AnimatorControllerParameterType.Bool);

                AnimatorState state = machine.AddState(spec.name);
                state.motion = spec.motion;
                state.speed = spec.speed;

                if (spec.name == defaultState)
                {
                    machine.defaultState = state;
                }
                else if (spec.chained == false)
                {
                    machine.AddEntryTransition(state).AddCondition(AnimatorConditionMode.If, 0f, parameter);
                }

                if (spec.final == false)
                {
                    AnimatorStateTransition exit = state.AddExitTransition();
                    exit.hasExitTime = false;
                    exit.duration = spec.exitBlend;
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
                }
            }

            EditorUtility.SetDirty(controller);
        }

        // An action made of several clips: `to` follows `from`, on `trigger` if there is one, else when `from` ends.
        public static void Chain(AnimatorController controller, string from, string to, float blend, string trigger = null)
        {
            AnimatorState Find(string name)
            {
                foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                {
                    if (child.state.name == name)
                    {
                        return child.state;
                    }
                }

                return null;
            }

            AnimatorStateTransition next = Find(from).AddTransition(Find(to));
            next.duration = blend;
            next.hasExitTime = trigger == null;
            next.exitTime = 1f;
            if (trigger != null)
            {
                EnsureParameter(controller, trigger, AnimatorControllerParameterType.Trigger);
                next.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            }

            EditorUtility.SetDirty(controller);
        }

        // Turns on the base layer's IK pass, which OnAnimatorIK needs.
        public static void EnableIK(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
        }

        // A masked layer that overlays one looping pose while a bool is on, such as a raised shield.
        public static void SetOverlayLayer(AnimatorController controller, string layerName, AvatarMask mask, string parameter, Motion pose, float blend)
        {
            EnsureParameter(controller, parameter, AnimatorControllerParameterType.Bool);

            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = layers.Length - 1; i >= 1; i--)
            {
                if (layers[i].name == layerName)
                {
                    controller.RemoveLayer(i);
                }
            }

            controller.AddLayer(layerName);
            layers = controller.layers;
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.defaultWeight = 1f;
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;

            AnimatorState empty = layer.stateMachine.AddState("Empty");
            AnimatorState raised = layer.stateMachine.AddState(layerName);
            raised.motion = pose;
            layer.stateMachine.defaultState = empty;

            AnimatorStateTransition up = empty.AddTransition(raised);
            up.hasExitTime = false;
            up.duration = blend;
            up.AddCondition(AnimatorConditionMode.If, 0f, parameter);

            AnimatorStateTransition down = raised.AddTransition(empty);
            down.hasExitTime = false;
            down.duration = blend;
            down.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);

            controller.layers = layers;
            EditorUtility.SetDirty(controller);
        }

        // A 1D blend tree on a speed parameter (m/s): each child sits at its measured speed and the tree plays at
        // Speed / that speed, so the feet match the agent however fast it is actually going.
        public static BlendTree SpeedTree(AnimatorController controller, string name, string parameter, params (Motion motion, float speed)[] children)
        {
            EnsureParameter(controller, parameter, AnimatorControllerParameterType.Float);

            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameter,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            var motions = new ChildMotion[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                motions[i] = new ChildMotion { motion = children[i].motion, threshold = children[i].speed, timeScale = 1f };
            }

            tree.children = motions;
            return tree;
        }

        // Drops blend trees left in the controller asset by an earlier build.
        public static void RemoveBlendTrees(AnimatorController controller)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)))
            {
                if (asset is BlendTree)
                {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                }
            }
        }

        // Mask for one arm and its fingers, nothing else.
        public static AvatarMask ArmMask(string path, bool left)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }

            mask.SetHumanoidBodyPartActive(left ? AvatarMaskBodyPart.LeftArm : AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(left ? AvatarMaskBodyPart.LeftFingers : AvatarMaskBodyPart.RightFingers, true);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name)
                {
                    return;
                }
            }

            controller.AddParameter(name, type);
        }

        private static Avatar LoadAvatar(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Avatar avatar)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static bool Matches(Color pixel, Swap swap, out float value)
        {
            Color.RGBToHSV(pixel, out float hue, out float saturation, out value);
            hue *= 360f;
            return saturation > 0.2f && hue >= swap.hueMin && hue <= swap.hueMax;
        }

        private static AnimationCurve Trim(AnimationCurve curve, float start, float end)
        {
            var keys = new List<Keyframe>();
            keys.Add(new Keyframe(0f, curve.Evaluate(start)));
            foreach (Keyframe key in curve.keys)
            {
                if (key.time > start + 0.0001f && key.time < end - 0.0001f)
                {
                    Keyframe shifted = key;
                    shifted.time -= start;
                    keys.Add(shifted);
                }
            }

            keys.Add(new Keyframe(end - start, curve.Evaluate(end)));
            return new AnimationCurve(keys.ToArray());
        }

        private static void GetHandAxes(Transform handBone, out Vector3 knuckle, out Vector3 fingers, out Vector3 indexSide, out Vector3 palm)
        {
            Vector3 Local(Transform finger) => handBone.InverseTransformPoint(finger.position);

            Vector3 index = Local(FindFinger(handBone, "index"));
            Vector3 thumb = Local(FindFinger(handBone, "thumb"));
            knuckle = Local(FindFinger(handBone, "middle"));

            // Auto-rigged hands can stop at three fingers: take the outermost one there is.
            Transform outer = FindFinger(handBone, "pinky") ?? FindFinger(handBone, "ring") ?? FindFinger(handBone, "middle");

            fingers = knuckle.normalized;
            indexSide = Vector3.ProjectOnPlane(index - Local(outer), fingers).normalized;

            // The thumb sits on the palm's side of the hand.
            palm = Vector3.Cross(fingers, indexSide).normalized;
            if (Vector3.Dot(palm, thumb - Vector3.Project(thumb, fingers) - Vector3.Project(thumb, indexSide)) < 0f)
            {
                palm = -palm;
            }
        }

        // Auto-rigged avatars map only some fingers, so GetBoneTransform can come back empty. Mixamo and CC_Base rigs both
        // name the first bone of each finger as a child of the hand.
        private static Transform FindFinger(Transform hand, string finger)
        {
            foreach (Transform child in hand)
            {
                if (child.name.ToLowerInvariant().Contains(finger))
                {
                    return child;
                }
            }

            return null;
        }

        public static Transform Socket(Transform bone, string socketName, Vector3 position, Quaternion rotation)
        {
            Transform socket = bone.Find(socketName);
            if (socket == null)
            {
                socket = new GameObject(socketName).transform;
                socket.SetParent(bone, false);
            }

            socket.SetLocalPositionAndRotation(position, rotation);

            // Sockets cancel any scale on the rig, so items sit at world scale 1 at identity.
            Vector3 parentScale = bone.lossyScale;
            socket.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            return socket;
        }
    }
}
