// Body for UnityMCP execute_code (pass safety_checks=false: it deletes its own flag file in Temp/).
// Opens a scene, enters Play at 1920x1080, freezes the player so every capture is the same frame, and
// pauses. Optionally zooms the camera and spawns a prop in front of the player (runtime only, the scene is
// never saved). Poll for Temp/meshy_cap_ready.txt + EditorApplication.isPaused before taking screenshots.
// Kneel enters Play without a domain reload, which is what lets this update callback survive into Play.
const string SCENE = "Assets/Kneel/Scenes/L1_BrokenLine_Playable_L.unity";  // EDIT if needed (never save it)
const float CAM_DISTANCE = 0f;          // EDIT: 0 = default 8.5 m follow camera; 3.1681416f = closest zoom
const string SPAWN = "";                // EDIT: prefab or model path to place in front of the player ("" = none)
const float SPAWN_AHEAD = 1.5f;         // EDIT: metres in front of the player
if (EditorApplication.isPlaying) return "already playing";
var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (s.isDirty) return "active scene has unsaved changes, not switching: " + s.path;
if (s.path != SCENE) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SCENE);
PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "Meshy 1080p");
var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
float start = -1f; int step = 0;
EditorApplication.CallbackFunction cb = null;
cb = () => {
  if (!EditorApplication.isPlaying) return;
  if (start < 0) start = Time.realtimeSinceStartup;
  float t = Time.realtimeSinceStartup - start;
  var pm = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>(); if (pm == null) return;
  var pc = pm.GetComponent<PlayerCombat>(); var anim = pm.GetComponentInChildren<Animator>();
  if (step == 0 && t > 0.5f) {
    Application.runInBackground = true;
    foreach (var c in new MonoBehaviour[] { pm, pc }) { var f = c.GetType().GetField("controls", BF); var ctl = f.GetValue(c); if (ctl != null) ctl.GetType().GetMethod("Disable").Invoke(ctl, null); }
    step = 1; }
  if (step == 1 && t > 1.5f) {
    var st = anim.GetCurrentAnimatorStateInfo(0); anim.Play(st.fullPathHash, 0, 0.25f); anim.speed = 0f;
    if (CAM_DISTANCE > 0) { var pcam = UnityEngine.Object.FindFirstObjectByType<PlayerCamera>();
      pcam.GetType().GetField("targetDistance", BF).SetValue(pcam, CAM_DISTANCE); pcam.GetType().GetField("distance", BF).SetValue(pcam, CAM_DISTANCE); }
    if (SPAWN != "") { var src = AssetDatabase.LoadAssetAtPath<GameObject>(SPAWN);
      var go = (GameObject)UnityEngine.Object.Instantiate(src, pm.transform.position + pm.transform.forward * SPAWN_AHEAD, pm.transform.rotation); go.name = "MeshyCaptureTarget"; }
    step = 2; }
  // 3.5 s for the camera to finish easing in; a capture taken while it moves differs by thousands of pixels
  if (step == 2 && t > 5f) { System.IO.File.WriteAllText("Temp/meshy_cap_ready.txt", "ready " + Screen.width + "x" + Screen.height + " cam=" + Camera.main.transform.position.ToString("F4"));
    EditorApplication.isPaused = true; step = 3; EditorApplication.update -= cb; }
};
EditorApplication.update += cb;
if (System.IO.File.Exists("Temp/meshy_cap_ready.txt")) System.IO.File.Delete("Temp/meshy_cap_ready.txt");
EditorApplication.isPlaying = true;
return "started in " + SCENE;
