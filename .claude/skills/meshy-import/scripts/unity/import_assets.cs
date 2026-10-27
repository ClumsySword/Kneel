// Body for UnityMCP execute_code. Sets importer settings on files already copied into the project and makes
// the URP Lit material. For a NEW asset only: when replacing an existing asset in place, its .meta already
// holds the right settings and must not be touched (the avatar's T-pose skeleton lives there).
// Expects in DIR: <MODEL>.fbx, T_<NAME>_BaseColor.png, T_<NAME>_MetallicSmoothness.png, T_<NAME>_Normal.png.
const string DIR = "Assets/Kneel/Props/Example/";   // EDIT: feature folder under Assets/Kneel/
const string NAME = "Example";                      // EDIT: texture/material name part
const string MODEL = "SM_Example";                  // EDIT: FBX file name without extension
const bool SKINNED = false;                         // EDIT: true for a rigged mesh
const int MAX_TEX = 2048;                           // EDIT: 1024 is plenty for small props
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var log = new System.Text.StringBuilder();
System.Action<string, bool, bool> tex = (kind, srgb, normal) => {
  string p = DIR + "T_" + NAME + "_" + kind + ".png";
  var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) { log.AppendLine("missing " + p); return; }
  ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
  ti.sRGBTexture = srgb; ti.alphaIsTransparency = false; ti.mipmapEnabled = true; ti.maxTextureSize = MAX_TEX;
  ti.textureCompression = TextureImporterCompression.Compressed;
  if (normal) ti.flipGreenChannel = false;   // Meshy and Blender bakes are OpenGL (green up), like Unity
  ti.SaveAndReimport(); log.AppendLine(p + " ok");
};
tex("BaseColor", true, false); tex("MetallicSmoothness", false, false); tex("Normal", false, true);
var mi = AssetImporter.GetAtPath(DIR + MODEL + ".fbx") as ModelImporter;
if (mi != null) {
  mi.globalScale = 1; mi.useFileScale = true;
  mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;   // slots get remapped below
  mi.importNormals = ModelImporterNormals.Import;                 // keep the smooth normals the bake used
  mi.importTangents = ModelImporterTangents.CalculateMikk;        // matches Blender's bake tangent space
  mi.importAnimation = false; mi.importBlendShapes = false; mi.importCameras = false; mi.importLights = false;
  mi.isReadable = false; mi.meshCompression = ModelImporterMeshCompression.Off;
  if (SKINNED) { mi.maxBonesPerVertex = 4; mi.skinWeights = ModelImporterSkinWeights.Standard; }
  else mi.animationType = ModelImporterAnimationType.None;
  mi.SaveAndReimport(); log.AppendLine(DIR + MODEL + ".fbx ok");
}
string matPath = DIR + "M_" + NAME + ".mat";
var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DIR + "T_" + NAME + "_BaseColor.png"));
mat.SetColor("_BaseColor", Color.white);
var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(DIR + "T_" + NAME + "_MetallicSmoothness.png");
mat.SetTexture("_MetallicGlossMap", ms); mat.SetFloat("_SmoothnessTextureChannel", 0); mat.SetFloat("_Smoothness", 1);
if (ms != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(DIR + "T_" + NAME + "_Normal.png");
mat.SetTexture("_BumpMap", nm); mat.SetFloat("_BumpScale", 1);
if (nm != null) mat.EnableKeyword("_NORMALMAP");
EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);   // not SaveAssets: that flushes other people's unsaved edits too
if (mi != null) {
  // Remap every material the FBX carries (Blender's slot names) to the new material, the way
  // Player_EmberWarden.fbx.meta maps M_EmberWarden, so every prefab and instance picks it up.
  foreach (var a in AssetDatabase.LoadAllAssetsAtPath(DIR + MODEL + ".fbx")) {
    var src = a as Material; if (src == null) continue;
    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat); log.AppendLine("remap " + src.name + " -> " + mat.name);
  }
  mi.SaveAndReimport();
  var go = AssetDatabase.LoadAssetAtPath<GameObject>(DIR + MODEL + ".fbx");
  foreach (var r in go.GetComponentsInChildren<Renderer>(true)) {
    Mesh m = r is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)r).sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
    log.AppendLine(r.name + ": verts=" + m.vertexCount + " tris=" + (m.triangles.Length / 3) + " size=" + m.bounds.size.ToString("F3") +
      " mats=" + string.Join(",", System.Array.ConvertAll(r.sharedMaterials, q => q ? q.name : "null")));
  }
}
return log.ToString();
