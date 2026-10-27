// Body for UnityMCP execute_code. Run twice: first while still in Play (flushes the last screenshot and
// stops Play), then again once Play has stopped (removes the "Meshy 1080p" Game view size, goes back to
// Free Aspect and reopens the scene the user had open). Nothing is saved.
const string LAST_SHOT = "Temp/meshy_shots/near_5k.png";   // EDIT
const string REOPEN = "Assets/Kneel/UI/Scenes/MainMenu.unity";   // EDIT: the scene that was open before
if (EditorApplication.isPlaying) {
  EditorApplication.Step();
  if (!System.IO.File.Exists(LAST_SHOT)) return "last screenshot not written yet, run again";
  EditorApplication.isPaused = false; EditorApplication.isPlaying = false; return "stopping Play; run again to restore the editor";
}
var asm = typeof(EditorWindow).Assembly;
var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var SBF = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var sizesT = asm.GetType("UnityEditor.GameViewSizes");
var inst = typeof(ScriptableSingleton<>).MakeGenericType(sizesT).GetProperty("instance", SBF).GetValue(null);
var group = sizesT.GetProperty("currentGroup", BF).GetValue(inst); var gT = group.GetType();
int total = (int)gT.GetMethod("GetTotalCount", BF).Invoke(group, null); int idx = -1;
for (int i = 0; i < total; i++) { var sz = gT.GetMethod("GetGameViewSize", BF).Invoke(group, new object[] { i }); if ((string)sz.GetType().GetProperty("baseText", BF).GetValue(sz) == "Meshy 1080p") idx = i; }
var gv = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"), false, null, false);
gv.GetType().GetProperty("selectedSizeIndex", BF).SetValue(gv, 0);
if (idx >= 0) { int builtin = (int)gT.GetMethod("GetBuiltinCount", BF).Invoke(group, null); gT.GetMethod("RemoveCustomSize", BF).Invoke(group, new object[] { idx - builtin }); sizesT.GetMethod("SaveToHDD", BF).Invoke(inst, null); }
var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (sc.isDirty) return "removed size=" + idx + "; active scene is dirty, left open: " + sc.path;
if (sc.path != REOPEN) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(REOPEN);
return "removed size=" + idx + "; now " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
