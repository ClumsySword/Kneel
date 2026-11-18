using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Builds the back draw/sheathe clips from the frames baked in Blender (Assets/Kneel/Player/Animations/BackDraw/*.json:
// every bone's pose in the Ember Warden's mesh space, 30 fps) into native Humanoid clips through HumanPoseHandler,
// so they retarget like any other clip on the Warden's own avatar. Also applies the back carry layout (scabbard on
// Spine_03, diagonal, hilt over the right shoulder; the shield's back seat moved out over it).
// Kneel/Animation/Build Back Draw Clips  and  Kneel/Animation/Apply Back Carry Layout.
public static class BackDrawClipBuilder
{
    private const string Folder = "Assets/Kneel/Player/Animations/BackDraw";
    private const string ModelPath = "Assets/Kneel/Player/Models/EmberWarden/Player_EmberWarden.fbx";
    private const string PrefabPath = "Assets/Kneel/Player/Player.prefab";

    [System.Serializable] private class Bake { public float fps; }

    [MenuItem("Kneel/Animation/Build Back Draw Clips")]
    public static void BuildClips()
    {
        Build("back_draw", "BackDraw");
        Build("back_sheath", "BackSheath");
        ApplyTimings();
        AssetDatabase.SaveAssets();
    }

    // The sword's hand-off times and the shield's baked path in the left hand socket, into PlayerCombatSettings.
    public static string ApplyTimings()
    {
        var s = AssetDatabase.LoadAssetAtPath<PlayerCombatSettings>("Assets/Kneel/Player/Settings/PlayerCombatSettings.asset");
        Undo.RecordObject(s, "Back draw timings");
        var draw = MiniJson(File.ReadAllText(Path.Combine(Folder, "back_draw.json")));
        var sheath = MiniJson(File.ReadAllText(Path.Combine(Folder, "back_sheath.json")));
        float fps = System.Convert.ToSingle(draw["fps"]);
        var de = (Dictionary<string, object>)draw["events"]; var se = (Dictionary<string, object>)sheath["events"];
        s.backClipFps = fps;
        s.drawSpeed = 1f;
        s.drawGrabTime = System.Convert.ToSingle(de["swordGrab"]);
        s.drawDuration = (((List<object>)draw["frames"]).Count - 1) / fps;
        s.sheathReleaseTime = System.Convert.ToSingle(se["swordRelease"]);
        s.sheathDuration = (((List<object>)sheath["frames"]).Count - 1) / fps;
        s.backShieldReleaseTime = System.Convert.ToSingle(se["shieldRelease"]);

        void ReadPath(List<object> list, out int first, out Vector3[] pos, out Quaternion[] rot)
        {
            first = list.FindIndex(x => x != null);
            int last = list.FindLastIndex(x => x != null);
            var p = new List<Vector3>(); var r = new List<Quaternion>();
            for (int i = first; i <= last; i++)
            {
                var v = ((List<object>)list[i]).Select(System.Convert.ToSingle).ToArray();
                p.Add(new Vector3(v[0], v[1], v[2])); r.Add(new Quaternion(v[3], v[4], v[5], v[6]));
            }
            pos = p.ToArray(); rot = r.ToArray();
        }
        ReadPath((List<object>)draw["shield"], out int df, out s.backDrawShieldPositions, out s.backDrawShieldRotations);
        s.backDrawShieldFirstFrame = df;
        ReadPath((List<object>)sheath["shield"], out int sf, out s.backSheathShieldPositions, out s.backSheathShieldRotations);
        EditorUtility.SetDirty(s);
        return $"grab {s.drawGrabTime:F3} draw {s.drawDuration:F2}s, release {s.sheathReleaseTime:F3} sheathe {s.sheathDuration:F2}s, shield draw frames {df}+{s.backDrawShieldPositions.Length}, sheathe {sf}+{s.backSheathShieldPositions.Length}, shield off {s.backShieldReleaseTime:F3}";
    }

    public static string Build(string json, string clipName)
    {
        string text = File.ReadAllText(Path.Combine(Folder, json + ".json"));
        var root = MiniJson(text);
        float fps = System.Convert.ToSingle(root["fps"]);
        var frames = (List<object>)root["frames"];

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().First();
        var go = Object.Instantiate(model);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        try
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var bones = go.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var ordered = go.GetComponentsInChildren<Transform>(); // parents before children
            var handler = new HumanPoseHandler(avatar, go.transform);
            var pose = new HumanPose();

            var curves = new Dictionary<string, AnimationCurve>();
            string[] muscleProps = HumanTrait.MuscleName.Select(ClipName).ToArray();
            System.Action<string, float, float> key = (n, t, v) =>
            {
                if (!curves.TryGetValue(n, out var c)) curves[n] = c = new AnimationCurve();
                c.AddKey(new Keyframe(t, v));
            };

            for (int f = 0; f < frames.Count; f++)
            {
                var fr = (Dictionary<string, object>)frames[f];
                Matrix4x4 meshToWorld = smr.transform.localToWorldMatrix;
                foreach (var t in ordered)
                {
                    if (!fr.TryGetValue(t.name, out var raw)) continue;
                    var v = ((List<object>)raw).Select(System.Convert.ToSingle).ToArray();
                    Vector3 p = meshToWorld.MultiplyPoint3x4(new Vector3(v[0], v[1], v[2]));
                    Quaternion q = meshToWorld.rotation * new Quaternion(v[3], v[4], v[5], v[6]);
                    t.SetPositionAndRotation(p, q);
                }

                handler.GetHumanPose(ref pose);
                float time = f / fps;
                key("RootT.x", time, pose.bodyPosition.x); key("RootT.y", time, pose.bodyPosition.y); key("RootT.z", time, pose.bodyPosition.z);
                key("RootQ.x", time, pose.bodyRotation.x); key("RootQ.y", time, pose.bodyRotation.y);
                key("RootQ.z", time, pose.bodyRotation.z); key("RootQ.w", time, pose.bodyRotation.w);
                for (int m = 0; m < pose.muscles.Length; m++) key(muscleProps[m], time, pose.muscles[m]);
            }

            var clip = new AnimationClip { name = clipName, frameRate = fps };
            foreach (var kv in curves)
            {
                for (int i = 0; i < kv.Value.length; i++) AnimationUtility.SetKeyLeftTangentMode(kv.Value, i, AnimationUtility.TangentMode.ClampedAuto);
                for (int i = 0; i < kv.Value.length; i++) AnimationUtility.SetKeyRightTangentMode(kv.Value, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key), kv.Value);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string path = Path.Combine(Folder, clipName + ".anim");
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(clip, path);
            handler.Dispose();
            return $"{clipName}: {frames.Count} frames, {curves.Count} curves, {clip.length:F2}s";
        }
        finally { Object.DestroyImmediate(go); }
    }

    // HumanTrait's finger muscle names differ from the clip property names ("Left Index 1 Stretched" -> "LeftHand.Index.1 Stretched").
    private static string ClipName(string muscle)
    {
        foreach (var side in new[] { "Left", "Right" })
            foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                string pre = side + " " + finger + " ";
                if (muscle.StartsWith(pre))
                {
                    string rest = muscle.Substring(pre.Length);           // "1 Stretched" or "Spread"
                    if (rest == "Spread") return $"{side}Hand.{finger}.Spread";
                    return $"{side}Hand.{finger}.{rest.Substring(0, 1)} {rest.Substring(2)}";
                }
            }
        return muscle;
    }

    [MenuItem("Kneel/Animation/Apply Back Carry Layout")]
    public static string ApplyLayout()
    {
        var root = MiniJson(File.ReadAllText(Path.Combine(Folder, "back_layout.json")));
        Matrix4x4 M(string k) { var l = ((List<object>)root[k]).Select(System.Convert.ToSingle).ToArray(); var m = new Matrix4x4(); for (int i = 0; i < 16; i++) m[i % 4, i / 4] = l[i]; return m; }
        Matrix4x4 sheathTarget = M("sheathSocketMesh"), shieldTarget = M("shieldBackSocketMesh");

        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var eq = prefab.GetComponent<PlayerEquipment>();
            var smr = prefab.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.sharedMesh != null && r.sharedMesh.name.Contains("EmberWarden"));
            var boneIdx = smr.bones.Select((b, i) => (b, i)).ToDictionary(x => x.b, x => x.i);
            var spine = smr.bones.First(b => b.name == (string)root["parentBone"]);
            Matrix4x4 BindOf(Transform t) { var b = t; while (!boneIdx.ContainsKey(b)) b = b.parent; return smr.sharedMesh.bindposes[boneIdx[b]].inverse * b.worldToLocalMatrix * t.localToWorldMatrix; }
            Matrix4x4 spineBind = smr.sharedMesh.bindposes[boneIdx[spine]].inverse;

            Transform sheath = eq.SwordSheathSocket, scabbard = sheath.parent;
            Matrix4x4 scabbardTarget = sheathTarget * BindOf(sheath).inverse * BindOf(scabbard);
            SetLocal(scabbard, spine, spineBind.inverse * scabbardTarget);
            Transform back = eq.ShieldBackSocket;
            SetLocal(back, spine, spineBind.inverse * shieldTarget);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            return $"scabbard -> {spine.name} local {scabbard.localPosition:F3} {scabbard.localEulerAngles:F1}; shield back socket local {back.localPosition:F3}";
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    // `local` is the child's pose in the parent bone's frame at bind (mesh units); the parent bone's own scale is 1.
    private static void SetLocal(Transform t, Transform parent, Matrix4x4 local)
    {
        t.SetParent(parent, false);
        t.localPosition = local.GetColumn(3);
        t.localRotation = local.rotation;
        t.localScale = local.lossyScale;
    }

    // --- tiny JSON reader (objects, arrays, numbers, strings) ---
    private static Dictionary<string, object> MiniJson(string s) { int i = 0; return (Dictionary<string, object>)Val(s, ref i); }
    private static object Val(string s, ref int i)
    {
        while (char.IsWhiteSpace(s[i])) i++;
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++;
            while (true)
            {
                while (char.IsWhiteSpace(s[i]) || s[i] == ',') i++;
                if (s[i] == '}') { i++; return d; }
                string k = (string)Val(s, ref i);
                while (char.IsWhiteSpace(s[i]) || s[i] == ':') i++;
                d[k] = Val(s, ref i);
            }
        }
        if (c == '[')
        {
            var l = new List<object>(); i++;
            while (true)
            {
                while (char.IsWhiteSpace(s[i]) || s[i] == ',') i++;
                if (s[i] == ']') { i++; return l; }
                l.Add(Val(s, ref i));
            }
        }
        if (c == '"') { int j = s.IndexOf('"', i + 1); string str = s.Substring(i + 1, j - i - 1); i = j + 1; return str; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        string num = s.Substring(st, i - st);
        if (num == "") { if (s.Substring(i, 4) == "true") { i += 4; return 1.0; } if (s.Substring(i, 5) == "false") { i += 5; return 0.0; } if (s.Substring(i, 4) == "null") { i += 4; return null; } }
        return double.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
    }
}
