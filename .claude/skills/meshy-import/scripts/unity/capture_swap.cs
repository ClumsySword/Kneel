// Body for UnityMCP execute_code, while capture_start's Play session is paused. Puts one variant's mesh and
// material on the target renderer and steps one frame. manage_camera screenshots are async: they are only
// written on the next frame, so the Step here also flushes the previous screenshot (the return value says
// whether it landed). Loop: screenshot -> capture_swap (next variant) -> screenshot -> ...
// For a skinned variant the bone order must equal the target's (the check below refuses otherwise);
// FBXs exported by lowpoly.export_fbx from the same rig always match.
const string TARGET = "EmberWarden";         // EDIT: renderer name under the player, or "MeshyCaptureTarget"
const string MODEL = "Assets/Kneel/Player/Models/EmberWarden/_Compare/EW_15k.fbx";   // EDIT: variant FBX
const string MAT = "Assets/Kneel/Player/Models/EmberWarden/_Compare/M_EW_15k.mat";   // EDIT: its material
const string PREV_SHOT = "Temp/meshy_shots/near_30k.png";                            // EDIT: last screenshot
Renderer target = null;
foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.name == TARGET || (r.transform.root.name == TARGET && target == null)) target = r;
if (target == null) return "target renderer not found: " + TARGET;
var src = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL);
var smr = target as SkinnedMeshRenderer;
if (smr != null) {
  var v = src.GetComponentInChildren<SkinnedMeshRenderer>(true);
  bool same = v.bones.Length == smr.bones.Length; for (int i = 0; same && i < v.bones.Length; i++) same = v.bones[i].name == smr.bones[i].name;
  if (!same) return "bone order differs, not swapping";
  smr.sharedMesh = v.sharedMesh;
} else target.GetComponent<MeshFilter>().sharedMesh = src.GetComponentInChildren<MeshFilter>(true).sharedMesh;
var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT);
if (mat != null) target.sharedMaterial = mat;
EditorApplication.Step();
Mesh now = smr != null ? smr.sharedMesh : target.GetComponent<MeshFilter>().sharedMesh;
return "prev shot saved=" + System.IO.File.Exists(PREV_SHOT) + " now tris=" + (now.triangles.Length / 3);
