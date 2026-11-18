#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

// TEMPORARY debug tool (Claude, 2026-11-17): plays a draw then a sheathe at a fixed 60 fps and saves three views of the
// player every `step` clip-seconds to <project>/Temp/DrawCaptures, plus a CSV of hand/shield positions in player space.
// Start it from code: DrawCaptureProbe.Run(). Safe to delete.
public class DrawCaptureProbe : MonoBehaviour
{
    public static string OutDir => Path.Combine(Application.dataPath, "../Temp/DrawCaptures");
    public static bool Done;
    public float step = 0.1f;
    public int size = 420;

    public static void Run(float step = 0.1f)
    {
        Done = false;
        var go = new GameObject("_DrawCaptureProbe");
        go.AddComponent<DrawCaptureProbe>().step = step;
    }

    private PlayerCombat combat;
    private Animator anim;
    private Camera cam;
    private RenderTexture rt;
    private Texture2D tex, sheet;
    private readonly StringBuilder csv = new StringBuilder();

    private IEnumerator Start()
    {
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);
        combat = FindFirstObjectByType<PlayerCombat>();
        anim = combat.GetComponentInChildren<Animator>();
        cam = new GameObject("_ProbeCam").AddComponent<Camera>();
        cam.enabled = false; cam.fieldOfView = 35f; cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
        rt = new RenderTexture(size, size, 24); tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        sheet = new Texture2D(size * 3, size, TextureFormat.RGB24, false);
        csv.AppendLine("phase,t,lhand_x,lhand_y,lhand_z,rhand_x,rhand_y,rhand_z,shield_x,shield_y,shield_z,lshoulder_x,lshoulder_y,lshoulder_z");

        Time.captureFramerate = 60;
        var m = typeof(PlayerCombat).GetMethod("StartDraw", BindingFlags.NonPublic | BindingFlags.Instance);
        yield return null; yield return null;
        m.Invoke(combat, null);
        yield return Record("draw", PlayerCombat.CombatState.Drawing, combat.Settings.drawDuration);
        for (int i = 0; i < 30; i++) yield return null;
        typeof(PlayerCombat).GetMethod("StartSheath", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(combat, null);
        yield return Record("sheath", PlayerCombat.CombatState.Sheathing, combat.Settings.sheathDuration);
        for (int i = 0; i < 20; i++) yield return null;
        yield return new WaitForEndOfFrame();
        Shot("sheath", 99f);
        File.WriteAllText(Path.Combine(OutDir, "track.csv"), csv.ToString());
        Time.captureFramerate = 0;
        Destroy(cam.gameObject); rt.Release();
        Done = true;
        Destroy(gameObject);
    }

    private IEnumerator Record(string phase, PlayerCombat.CombatState state, float duration)
    {
        float next = 0f;
        while (true)
        {
            yield return new WaitForEndOfFrame();
            float t = combat.StateTime * combat.Settings.drawSpeed;
            bool inState = combat.State == state;
            if (!inState && t < 0.05f) continue;
            Log(phase, inState ? t : duration + 0.01f);
            if (inState && t >= next)
            {
                Shot(phase, t);
                next += step;
            }
            if (!inState) { Shot(phase, duration); yield break; }
        }
    }

    private void Log(string phase, float t)
    {
        Transform root = combat.transform;
        Vector3 L(Vector3 w) => root.InverseTransformPoint(w);
        var eq = combat.GetComponent<PlayerEquipment>();
        Vector3 lh = L(anim.GetBoneTransform(HumanBodyBones.LeftHand).position);
        Vector3 rh = L(anim.GetBoneTransform(HumanBodyBones.RightHand).position);
        Vector3 sh = L(eq.ShieldRenderer.bounds.center);
        Vector3 ls = L(anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position);
        csv.AppendLine(string.Join(",", phase, t.ToString("F3"), V(lh), V(rh), V(sh), V(ls)));
    }

    private static string V(Vector3 v) => $"{v.x:F3},{v.y:F3},{v.z:F3}";

    private void Shot(string phase, float t)
    {
        Transform root = combat.transform;
        Vector3 chest = root.TransformPoint(new Vector3(0f, 1.3f, 0f));
        // front-left 3/4, back-left 3/4, and the game's high top-down angle
        Vector3[] eyes =
        {
            root.TransformPoint(new Vector3(-1.5f, 1.8f, 1.9f)),
            root.TransformPoint(new Vector3(-1.4f, 1.9f, -1.9f)),
            root.TransformPoint(new Vector3(0.3f, 4.6f, -2.2f)),
        };
        for (int i = 0; i < 3; i++)
        {
            cam.transform.position = eyes[i];
            cam.transform.LookAt(chest + (i == 2 ? Vector3.down * 0.4f : Vector3.zero));
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            sheet.SetPixels(i * size, 0, size, size, tex.GetPixels());
        }
        RenderTexture.active = null; cam.targetTexture = null;
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, $"{phase}_{t:00.00}.png"), sheet.EncodeToPNG());
    }
}
#endif
