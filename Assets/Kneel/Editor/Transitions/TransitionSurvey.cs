using System.Linq;
using System.Text;
using Kneel.Markers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.EditorTools.Transitions
{
    // Read-only: dumps what each level's seam is made of (player, markers, music, grade, sun) to Temp/TransitionSurvey.txt,
    // so the transition data can be authored against the scenes rather than the notes.
    public static class TransitionSurvey
    {
        [MenuItem("Kneel/Transitions/Survey Levels (read-only)")]
        public static void SurveyAll()
        {
            var sb = new StringBuilder();
            foreach (Kneel.Core.LevelCatalog.Level level in Kneel.Core.LevelCatalog.Levels)
            {
                string path = EditorBuildSettings.scenes.Select(s => s.path).FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == level.Scene);
                sb.AppendLine("##### " + level.Scene + "  " + path);
                if (path == null)
                    continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Survey(sb);
            }

            System.IO.File.WriteAllText("Temp/TransitionSurvey.txt", sb.ToString());
            Debug.Log("TransitionSurvey written");
        }

        public static string V(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

        public static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                p = t.name + "/" + p;
            }

            return p;
        }

        public static void Survey(StringBuilder sb)
        {
            foreach (PlayerMovement p in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include))
                sb.AppendLine($"Player {PathOf(p.transform)} at {V(p.transform.position)} yaw {p.transform.eulerAngles.y:0}");
            foreach (PlayerCamera c in Object.FindObjectsByType<PlayerCamera>(FindObjectsInactive.Include))
                sb.AppendLine($"Camera {PathOf(c.transform)} settings {(c.Settings != null ? AssetDatabase.GetAssetPath(c.Settings) : "-")} pitch {c.Settings?.pitch} yaw {c.Settings?.yaw} start {c.Settings?.startDistance} fov {c.GetComponent<Camera>().fieldOfView} far {c.GetComponent<Camera>().farClipPlane}");
            foreach (ExitMarker m in Object.FindObjectsByType<ExitMarker>(FindObjectsInactive.Include))
            {
                var box = m.GetComponent<BoxCollider>();
                sb.AppendLine($"Exit {PathOf(m.transform)} dest '{m.destination}' at {V(m.transform.position)} rot {V(m.transform.eulerAngles)} box {(box != null ? V(box.size) + " c" + V(box.center) + " trig " + box.isTrigger : "none")} layer {LayerMask.LayerToName(m.gameObject.layer)} active {m.gameObject.activeInHierarchy}");
            }

            foreach (PlayerSpawnMarker m in Object.FindObjectsByType<PlayerSpawnMarker>(FindObjectsInactive.Include))
                sb.AppendLine($"Spawn {PathOf(m.transform)} at {V(m.transform.position)} yaw {m.transform.eulerAngles.y:0}");
            foreach (ShrineMarker m in Object.FindObjectsByType<ShrineMarker>(FindObjectsInactive.Include))
                sb.AppendLine($"Shrine '{m.id}' {PathOf(m.transform)} at {V(m.transform.position)}");
            foreach (ShrineRest m in Object.FindObjectsByType<ShrineRest>(FindObjectsInactive.Include))
                sb.AppendLine($"ShrineRest '{m.flag}' {PathOf(m.transform)} at {V(m.transform.position)}");
            foreach (VistaMarker m in Object.FindObjectsByType<VistaMarker>(FindObjectsInactive.Include))
                sb.AppendLine($"Vista '{m.id}' {PathOf(m.transform)} at {V(m.transform.position)} pitch {m.pitch} fov {m.fieldOfView} back {m.pullBack}");
            foreach (AudioSource a in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (a.loop == false && a.playOnAwake == false)
                    continue;
                sb.AppendLine($"Audio {PathOf(a.transform)} clip {(a.clip != null ? a.clip.name : "-")} group {(a.outputAudioMixerGroup != null ? a.outputAudioMixerGroup.name : "-")} vol {a.volume:0.##} loop {a.loop} awake {a.playOnAwake} spatial {a.spatialBlend:0.#}");
            }

            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                string n = mb.GetType().Name;
                if (n.Contains("Music") || n.Contains("Ambience") || n.Contains("LevelFlow") || n.Contains("Restart") || n.Contains("Reset") || n.Contains("Pickup") || n.Contains("Gate") && n.Contains("OnScreen") == false)
                    sb.AppendLine($"Comp {n} on {PathOf(mb.transform)}");
            }

            foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
            {
                sb.Append($"Volume {PathOf(v.transform)} global {v.isGlobal} prio {v.priority} weight {v.weight} profile {(v.sharedProfile != null ? AssetDatabase.GetAssetPath(v.sharedProfile) : "-")}");
                if (v.sharedProfile != null && v.sharedProfile.TryGet(out ColorAdjustments ca))
                    sb.Append($" CA exp {ca.postExposure.value:0.##}/{ca.postExposure.overrideState} con {ca.contrast.value:0.#} sat {ca.saturation.value:0.#} filter {ca.colorFilter.value}");
                if (v.sharedProfile != null && v.sharedProfile.TryGet(out WhiteBalance wb))
                    sb.Append($" WB temp {wb.temperature.value:0.#}/{wb.temperature.overrideState} tint {wb.tint.value:0.#}");
                if (v.sharedProfile != null && v.sharedProfile.TryGet(out LiftGammaGain lgg))
                    sb.Append($" LGG {lgg.lift.overrideState}/{lgg.gamma.overrideState}/{lgg.gain.overrideState}");
                if (v.sharedProfile != null && v.sharedProfile.TryGet(out SplitToning st))
                    sb.Append($" ST {st.shadows.value}/{st.highlights.value} {st.shadows.overrideState}");
                sb.AppendLine();
            }

            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional)
                    sb.AppendLine($"Sun {PathOf(l.transform)} rot {V(l.transform.eulerAngles)} col {l.color} int {l.intensity:0.##} shadows {l.shadows}");
            sb.AppendLine($"Render ambient {RenderSettings.ambientMode} sky {RenderSettings.ambientSkyColor} eq {RenderSettings.ambientEquatorColor} gr {RenderSettings.ambientGroundColor} int {RenderSettings.ambientIntensity:0.##} fog {RenderSettings.fog} {RenderSettings.fogMode} {RenderSettings.fogColor} dens {RenderSettings.fogDensity:0.####} {RenderSettings.fogStartDistance}-{RenderSettings.fogEndDistance} skybox {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "-")}");
            var scene = EditorSceneManager.GetActiveScene();
            sb.AppendLine("Roots: " + string.Join(", ", scene.GetRootGameObjects().Select(g => g.name + (g.activeSelf ? "" : "(off)"))));
            sb.AppendLine();
        }
    }
}
