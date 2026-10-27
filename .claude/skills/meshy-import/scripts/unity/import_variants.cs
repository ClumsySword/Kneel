// Body for UnityMCP execute_code. Imports candidate low-poly versions for an in-game comparison.
// Copy them into a temporary <asset folder>/_Compare/ first, as <PREFIX>_<tag>.fbx and T_<PREFIX>_Normal_<tag>.png.
// Each normal map gets the shipped normal map's importer settings, and each variant a copy of the shipped
// material with only _BumpMap swapped, so the comparison differs in mesh + normal map and nothing else.
// The whole _Compare folder is deleted (file system + refresh) once the user has picked one.
const string DIR = "Assets/Kneel/Player/Models/EmberWarden/";   // EDIT: folder of the asset being replaced
const string REF_NORMAL = "T_EmberWarden_Normal.png";           // EDIT: shipped normal map in DIR
const string REF_MAT = "M_EmberWarden.mat";                     // EDIT: shipped material in DIR
const string PREFIX = "EW";                                     // EDIT
string[] TAGS = { "30k", "15k", "10k" };                        // EDIT
if (EditorApplication.isPlaying) return "stop Play first";
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var refTex = (TextureImporter)AssetImporter.GetAtPath(DIR + REF_NORMAL);
var ts = new TextureImporterSettings(); refTex.ReadTextureSettings(ts);
var refPlat = refTex.GetDefaultPlatformTextureSettings();
var baseMat = AssetDatabase.LoadAssetAtPath<Material>(DIR + REF_MAT);
var sb = new System.Text.StringBuilder();
foreach (var t in TAGS) {
  string tp = DIR + "_Compare/T_" + PREFIX + "_Normal_" + t + ".png";
  var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
  ti.SetTextureSettings(ts);
  var p = ti.GetDefaultPlatformTextureSettings(); p.maxTextureSize = refPlat.maxTextureSize; p.textureCompression = refPlat.textureCompression; p.format = refPlat.format; ti.SetPlatformTextureSettings(p);
  ti.SaveAndReimport();
  string fp = DIR + "_Compare/" + PREFIX + "_" + t + ".fbx";
  var mi = (ModelImporter)AssetImporter.GetAtPath(fp);
  // Generic is enough for a runtime mesh swap; the shipped asset keeps its own (Humanoid) settings.
  mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = false;
  mi.animationType = ModelImporterAnimationType.Generic;
  mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk;
  mi.weldVertices = true; mi.maxBonesPerVertex = 4;
  mi.SaveAndReimport();
  string mp = DIR + "_Compare/M_" + PREFIX + "_" + t + ".mat";
  if (AssetDatabase.LoadAssetAtPath<Material>(mp) == null) {
    var mat = new Material(baseMat); mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
    AssetDatabase.CreateAsset(mat, mp);   // writes the file; no SaveAssets (it would flush unrelated dirty assets)
  }
  var go = AssetDatabase.LoadAssetAtPath<GameObject>(fp);
  var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
  Mesh m = smr != null ? smr.sharedMesh : go.GetComponentInChildren<MeshFilter>(true).sharedMesh;
  sb.AppendLine(t + ": verts=" + m.vertexCount + " tris=" + (m.triangles.Length / 3) + (smr != null ? " bones=" + smr.bones.Length : ""));
}
return sb.ToString();
